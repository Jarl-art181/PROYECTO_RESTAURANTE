
using Guna.Charts.WinForms;
using Guna.UI2.WinForms;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class FRM_MESAS : Form

    {

        // Guarda el número de mesa que seleccionamos.
        private int mesaSeleccionada = 0;
        private string filtroActual = "todas";

        // La tabla de reservas crea sus columnas una sola vez.
        private bool historialConfigurado = false;
        private readonly string cadenaConexion =
    @"Server=LAPTOP-GH63852S\SQLEXPRESS;
      Database=AL_ESTILO_PIURA;
      Integrated Security=True;";

        public FRM_MESAS()
        {


            InitializeComponent();
            this.VisibleChanged += FRM_MESAS_VisibleChanged;
            this.FormClosed += (s, args) =>
                ControlReservasAutomaticas.MesasActualizadas -= RefrescarMesasAutomaticamente;

            // Conectar el botón Liberar mesa.
            btnLiberarMesa.Click -= btnLiberarMesa_Click;
            btnLiberarMesa.Click += btnLiberarMesa_Click;
            // abrir mesa
            btnAbrirPedido.Click -= btnAbrirPedido_Click;
            btnAbrirPedido.Click += btnAbrirPedido_Click;

            // Configuración de la ventana.
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            ConfigurarBotonesIniciales();

            CargarContadoresMesas();
            CargarEstadosMesas();

            FiltrarMesas();
            PrepararSeleccionMesas();

            CargarHistorialMesas();
        }


        private void FRM_MESAS_VisibleChanged(object sender, EventArgs e)
        {
            ControlReservasAutomaticas.MesasActualizadas -= RefrescarMesasAutomaticamente;

            // El reloj solo trabaja mientras Mesas está visible.
            timerReloj.Enabled = this.Visible;

            if (this.Visible)
            {
                ActualizarReloj();

                // Al volver a mostrarse (el Navegador reutiliza esta
                // ventana) se recargan mesas, contadores y reservas.
                ControlReservasAutomaticas.MesasActualizadas += RefrescarMesasAutomaticamente;
                RefrescarMesasAutomaticamente(null, EventArgs.Empty);
            }
        }

        private void RefrescarMesasAutomaticamente(object sender, EventArgs e)
        {
            if (this.IsDisposed || !this.Visible) return;
            CargarContadoresMesas();
            CargarEstadosMesas();
            CargarHistorialMesas();
            if (mesaSeleccionada > 0)
                CargarDetalleMesa();
            FiltrarMesas();
        }




        private void btnLiberarMesa_Click(
            object sender, EventArgs e)
        {
            // =====================================
            // 1. VALIDAR MESA SELECCIONADA
            // =====================================

            if (mesaSeleccionada == 0)
            {
                MessageBox.Show(
                    "Primero seleccione una mesa.",
                    "Gestión de mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // Si la mesa está ocupada, abrir primero el cobro.
            if (lblDetalleEstado.Text.Trim().ToLower() == "ocupada")
            {
                CobrarYLiberarMesaOcupada();
                return;
            }

           

            // =====================================
            // 2. SOLO CANCELAR RESERVAS
            // =====================================

            if (lblDetalleEstado.Text.Trim().ToLower()
                != "reservada")
            {
                MessageBox.Show(
                    "Por ahora, este botón solo permite " +
                    "liberar mesas reservadas.\n\n" +
                    "Las mesas ocupadas necesitan " +
                    "validación del cobro y cierre " +
                    "del pedido.",
                    "Liberar mesa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // =====================================
            // 3. CONFIRMAR OPERACIÓN
            // =====================================

            DialogResult respuesta = MessageBox.Show(
                "¿Desea cancelar la reserva de la Mesa " +
                mesaSeleccionada + "?",
                "Confirmar liberación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (respuesta != DialogResult.Yes)
            {
                return;
            }

            // =====================================
            // 4. ACTUALIZAR SQL SERVER
            // =====================================

            string sql = @"
        UPDATE M
        SET estado = 'libre'
        FROM dbo.MESA M
        WHERE M.[número] = @NumeroMesa
          AND M.id_sucursal = @Sucursal
          AND LOWER(LTRIM(RTRIM(M.estado)))
              = 'reservada'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.PEDIDO P
              WHERE P.id_mesa = M.id_mesa
                AND P.estado IN
                (
                    'pendiente',
                    'en_preparacion',
                    'listo',
                    'entregado'
                )
          );";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@NumeroMesa",
                        SqlDbType.Int).Value =
                        mesaSeleccionada;

                    cmd.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    int filas = cmd.ExecuteNonQuery();

                    if (filas != 1)
                    {
                        MessageBox.Show(
                            "No se pudo liberar la mesa.\n" +
                            "Compruebe su estado actual.",
                            "Gestión de mesas",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        return;
                    }
                }

                // =====================================
                // 5. ACTUALIZAR LOS CONTROLES
                // =====================================

                CargarContadoresMesas();
                CargarEstadosMesas();
                CargarDetalleMesa();
                FiltrarMesas();

                MessageBox.Show(
                    "Mesa " + mesaSeleccionada +
                    " liberada correctamente.",
                    "Gestión de mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al liberar la mesa:\n" +
                    ex.Message,
                    "Gestión de mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private void CargarContadoresMesas()
        {
            string consulta = @"
        SELECT
            COUNT(*) AS TotalMesas,

            COUNT(CASE
                WHEN LOWER(LTRIM(RTRIM(M.estado))) =
                    'libre'
                THEN 1
            END) AS MesasLibres,

            COUNT(CASE
                WHEN LOWER(LTRIM(RTRIM(M.estado))) =
                    'ocupada'
                THEN 1
            END) AS MesasOcupadas,

            (
                SELECT COUNT(DISTINCT R.id_mesa)
                FROM dbo.RESERVA_MESA R

                INNER JOIN dbo.MESA M2
                    ON R.id_mesa = M2.id_mesa

                WHERE M2.id_sucursal = @Sucursal
                  AND R.estado = 'confirmada'
                  AND DATEADD(
                      MINUTE, 120, R.fecha_hora
                  ) > SYSDATETIME()

            ) AS MesasReservadas

        FROM dbo.MESA M

        WHERE M.id_sucursal = @Sucursal;";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand comando =
                    new SqlCommand(consulta, conexion))
                {
                    comando.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    using (SqlDataReader lector =
                        comando.ExecuteReader())
                    {
                        if (lector.Read())
                        {
                            lblMesasTotales.Text =
                                Convert.ToInt32(
                                    lector["TotalMesas"]).ToString("D2");

                            lblMesasLibres.Text =
                                Convert.ToInt32(
                                    lector["MesasLibres"]).ToString("D2");

                            lblMesasOcupadas.Text =
                                Convert.ToInt32(
                                    lector["MesasOcupadas"]).ToString("D2");

                            lblMesasReservadas.Text =
                                Convert.ToInt32(
                                    lector["MesasReservadas"]).ToString("D2");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar los contadores:\n" +
                    ex.Message,
                    "Gestión de Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }




        private void guna2Button2_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRMS_PEDIDOS>(this);
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_MESAS>(this);
        }

        private void guna2Button5_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_COCINA>(this);
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_EMPLEADOS>(this);
        }

        private void guna2Button9_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_MENU>(this);
        }

        private void guna2Button8_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INVENTARIO>(this);
        }

        private void guna2Button7_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_PROMOCIONES>(this);
        }

        private void guna2Button6_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_REPORTES>(this);
        }
        // mostrar la fecha y hora 

        private void timerReloj_Tick(object sender, EventArgs e)
        {
            ActualizarReloj();
        }
        private void ActualizarReloj()
        {
            DateTime ahora = DateTime.Now;

            lblHora.Text = ahora.ToString("HH:mm:ss");

            string[] dias =
            {
        "Dom.", "Lun.", "Mar.", "Mié.",
        "Jue.", "Vier.", "Sáb."
    };

            string[] meses =
            {
        "Ene", "Feb", "Mar", "Abr",
        "May", "Jun", "Jul", "Ago",
        "Sep", "Oct", "Nov", "Dic"
    };

            lblFecha.Text =
                dias[(int)ahora.DayOfWeek] + " " +
                ahora.Day.ToString("00") + " " +
                meses[ahora.Month - 1] + " " +
                ahora.Year;
        }



        // actualizar el flow de tarjetas de mesa 1 al 10


        private void CargarEstadosMesas()
        {
            // ==========================================
            // 1. ESTADOS DE LAS 10 MESAS
            // ==========================================

            Guna2TextBox[] estados =
            {
        lblEstadoMesa1, lblEstadoMesa2,
        lblEstadoMesa3, lblEstadoMesa4,
        lblEstadoMesa5, lblEstadoMesa6,
        lblEstadoMesa7, lblEstadoMesa8,
        lblEstadoMesa9, lblEstadoMesa10
    };

            // ==========================================
            // 2. PANELES DE PEDIDO Y MESERO
            // ==========================================

            Control[] panelesPedido =
            {
        pnlPedidoMesero1, pnlPedidoMesero2,
        pnlPedidoMesero3, pnlPedidoMesero4,
        pnlPedidoMesero5, pnlPedidoMesero6,
        pnlPedidoMesero7, pnlPedidoMesero8,
        pnlPedidoMesero9, pnlPedidoMesero10
    };

            // ==========================================
            // 3. NÚMEROS DE PEDIDO
            // ==========================================

            Guna2HtmlLabel[] pedidos =
            {
        lblPedidoMesa1, lblPedidoMesa2,
        lblPedidoMesa3, lblPedidoMesa4,
        lblPedidoMesa5, lblPedidoMesa6,
        lblPedidoMesa7, lblPedidoMesa8,
        lblPedidoMesa9, lblPedidoMesa10
    };

            // ==========================================
            // 4. NOMBRES DE MESEROS
            // ==========================================

            Guna2HtmlLabel[] meseros =
            {
        lblMeseroMesa1, lblMeseroMesa2,
        lblMeseroMesa3, lblMeseroMesa4,
        lblMeseroMesa5, lblMeseroMesa6,
        lblMeseroMesa7, lblMeseroMesa8,
        lblMeseroMesa9, lblMeseroMesa10
    };

            // ==========================================
            // 5. CONSULTAR MESAS EN SQL SERVER
            // ==========================================

            string consulta = @"
    SELECT
        M.[número] AS NumeroMesa,
        M.estado AS EstadoMesa,
        P.id_pedido AS NumeroPedido,

        LTRIM(RTRIM(
            ISNULL(E.nombre, '') + ' ' +
            ISNULL(E.apellidos, '')
        )) AS NombreMesero

    FROM dbo.MESA M

    OUTER APPLY
    (
        SELECT TOP (1)
            PD.id_pedido,
            PD.id_mesero
        FROM dbo.PEDIDO PD
        WHERE PD.id_mesa = M.id_mesa
          AND PD.id_sucursal = M.id_sucursal
          AND PD.canal = 'salon'
        AND PD.estado IN
(
    'pendiente',
    'en_preparacion',
    'listo',
    'entregado'
)
        ORDER BY PD.fecha_hora DESC,
                 PD.id_pedido DESC
    ) P

    LEFT JOIN dbo.EMPLEADO E
        ON E.id_empleado = P.id_mesero

    WHERE M.id_sucursal = @Sucursal
      AND M.[número] BETWEEN 1 AND 10

    ORDER BY M.[número];";


            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    using (SqlCommand comando =
                        new SqlCommand(consulta, conexion))
                    {
                        comando.Parameters.Add(
                            "@Sucursal", SqlDbType.Int).Value = 1;

                        using (SqlDataReader lector =
                            comando.ExecuteReader())
                        {
                            // ==================================
                            // 6. LIMPIAR LAS 10 TARJETAS
                            // ==================================

                            for (int i = 0; i < 10; i++)
                            {
                                panelesPedido[i].Visible = false;

                                pedidos[i].Text = "";
                                meseros[i].Text = "";

                                estados[i].ReadOnly = true;
                                estados[i].TabStop = false;

                                estados[i].TextAlign =
                                    HorizontalAlignment.Center;

                                estados[i].ForeColor = Color.Black;

                                estados[i].Text = "Sin datos";
                                estados[i].FillColor = Color.LightGray;
                            }

                            // ==================================
                            // 7. LEER LOS DATOS DE SQL SERVER
                            // ==================================

                            while (lector.Read())
                            {
                                int numeroMesa =
                                    Convert.ToInt32(
                                        lector["NumeroMesa"]);

                                if (numeroMesa < 1 ||
                                    numeroMesa > 10)
                                {
                                    continue;
                                }

                                // Mesa 1 = posición 0.
                                int posicion = numeroMesa - 1;

                                string estado =
                                    Convert.ToString(
                                        lector["EstadoMesa"])
                                        .Trim()
                                        .ToLower();

                                string numeroPedido = "";
                                string nombreMesero = "";

                                // ==============================
                                // 8. OBTENER NÚMERO DE PEDIDO
                                // ==============================

                                if (lector["NumeroPedido"] !=
                                    DBNull.Value)
                                {
                                    int idPedido =
                                        Convert.ToInt32(
                                            lector["NumeroPedido"]);

                                    numeroPedido =
                                        idPedido.ToString("D4");
                                }

                                // ==============================
                                // 9. OBTENER NOMBRE DEL MESERO
                                // ==============================

                                if (lector["NombreMesero"] !=
                                    DBNull.Value)
                                {
                                    nombreMesero =
                                        Convert.ToString(
                                            lector["NombreMesero"]);
                                }

                                // ==============================
                                // 10. MESA LIBRE - VERDE
                                // ==============================

                                if (estado == "libre")
                                {
                                    estados[posicion].Text =
                                        "Libre";

                                    estados[posicion].FillColor =
                                        Color.FromArgb(
                                            0, 235, 133);

                                    panelesPedido[posicion].Visible =
                                        false;
                                }

                                // ==============================
                                // 11. MESA RESERVADA - AMARILLO
                                // ==============================

                                else if (estado == "reservada")
                                {
                                    estados[posicion].Text = "Reservada";
                                    estados[posicion].FillColor =
                                        Color.FromArgb(242, 230, 139);

                                    // No mostrar detalle en la tarjeta.
                                    panelesPedido[posicion].Visible = false;
                                }

                                // ==============================
                                // 12. MESA OCUPADA - ROJO
                                // ==============================

                                else if (estado == "ocupada")
                                {
                                    estados[posicion].Text = "Ocupada";
                                    estados[posicion].FillColor =
                                        Color.FromArgb(250, 128, 120);

                                    // IMPORTANTE: NO mostrar pedido ni mesero
                                    // en la tarjeta antes de pulsar Detalle pedido.
                                    pedidos[posicion].Text = "";
                                    meseros[posicion].Text = "";
                                    panelesPedido[posicion].Visible = false;
                                }

                                // ==============================
                                // 13. MESA INACTIVA
                                // ==============================

                                else
                                {
                                    estados[posicion].Text =
                                        "Inactiva";

                                    estados[posicion].FillColor =
                                        Color.LightGray;

                                    panelesPedido[posicion].Visible =
                                        false;
                                }
                            }
                        }
                    }
                }

                // =====================================
                // 14. ACTUALIZAR FILTROS DE MESAS
                // =====================================

                FiltrarMesas();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar los estados de las mesas:\n" +
                    ex.Message,
                    "Gestión de Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }



        // filtrar mesas con el buton 

        private void FiltrarMesas()
        {
            // 1. Guardar las 10 tarjetas.
            Control[] tarjetas =
            {
        pnlMesa1,
        pnlMesa2,
        pnlMesa3,
        pnlMesa4,
        pnlMesa5,
        pnlMesa6,
        pnlMesa7,
        pnlMesa8,
        pnlMesa9,
        pnlMesa10
    };

            // 2. Guardar los estados.
            Guna2TextBox[] estados =
            {
        lblEstadoMesa1,
        lblEstadoMesa2,
        lblEstadoMesa3,
        lblEstadoMesa4,
        lblEstadoMesa5,
        lblEstadoMesa6,
        lblEstadoMesa7,
        lblEstadoMesa8,
        lblEstadoMesa9,
        lblEstadoMesa10
    };

            // 3. Obtener el texto de búsqueda.
            string busqueda =
                txtBuscarMesa.Text.Trim().ToLower();

            busqueda = busqueda.Replace("mesa", "").Trim();

            // 4. Suspender el diseño mientras filtramos.
            flowLayoutPanel1.SuspendLayout();

            // 5. Recorrer las 10 mesas.
            for (int i = 0; i < 10; i++)
            {
                int numeroMesa = i + 1;

                string estado =
                    estados[i].Text.Trim().ToLower();

                bool mostrar = true;

                // Verificar el filtro de estado.
                if (filtroActual != "todas")
                {
                    if (estado != filtroActual)
                    {
                        mostrar = false;
                    }
                }

                // Verificar el número buscado.
                if (busqueda != "")
                {
                    if (numeroMesa.ToString() != busqueda)
                    {
                        mostrar = false;
                    }
                }

                // Mostrar u ocultar la tarjeta.
                tarjetas[i].Visible = mostrar;
            }

            // 6. Reorganizar las tarjetas visibles.
            flowLayoutPanel1.ResumeLayout(true);
            flowLayoutPanel1.PerformLayout();

            // 7. Regresar la barra al inicio.
            flowLayoutPanel1.AutoScrollPosition = new Point(0, 0);
        }

        private void btnTodas_Click(object sender, EventArgs e)
        {
            filtroActual = "todas";
            FiltrarMesas();
        }

        private void btnLibres_Click(object sender, EventArgs e)
        {
            filtroActual = "libre";
            FiltrarMesas();
        }

        private void btnOcupadas_Click(object sender, EventArgs e)
        {
            filtroActual = "ocupada";
            FiltrarMesas();
        }


        private void btnReservadas_Click(object sender, EventArgs e)
        {
            filtroActual = "reservada";
            FiltrarMesas();
        }




        private void btnNuevaReserva_Click(
            object sender, EventArgs e)
        {
            using (RESERVAR_MESA ventana =
                new RESERVAR_MESA())
            {
                if (ventana.ShowDialog(this) ==
                    DialogResult.OK)
                {
                    // Actualizar reservas registradas.
                    CargarHistorialMesas();

                    // Actualizar estado actual de las mesas.
                    CargarContadoresMesas();
                    CargarEstadosMesas();
                    FiltrarMesas();
                }
            }
        }








        private void SeleccionarMesa(object sender, EventArgs e)
        {
            Control control = sender as Control;

            if (control == null || control.Tag == null)
                return;

            int numeroMesa;

            if (!int.TryParse(control.Tag.ToString(), out numeroMesa))
                return;

            mesaSeleccionada = numeroMesa;

            // Solo datos basicos, estado y botones.
            // Los productos se muestran al pulsar Detalle pedido.
            CargarDetalleMesa();
        }

        private void CargarDetalleMesa()
        {
            // ====================================================
            // 1. VERIFICAR LA MESA SELECCIONADA
            // ====================================================

            if (mesaSeleccionada == 0)
            {
                return;
            }

            // ====================================================
            // 2. CONSULTAR LOS DATOS EN SQL SERVER
            // ====================================================


            string consulta = @"
    SELECT
        M.[número] AS NumeroMesa,
        M.capacidad AS Capacidad,
        M.estado AS EstadoMesa,

        P.id_pedido AS NumeroPedido,
        P.fecha_hora AS FechaPedido,
        P.nombre_contacto AS Cliente,

        LTRIM(RTRIM(
            ISNULL(E.nombre, '') + ' ' +
            ISNULL(E.apellidos, '')
        )) AS NombreMesero

    FROM dbo.MESA M

    OUTER APPLY
    (
        SELECT TOP (1)
            PD.id_pedido,
            PD.fecha_hora,
            PD.nombre_contacto,
            PD.id_mesero

        FROM dbo.PEDIDO PD

        WHERE PD.id_mesa = M.id_mesa
          AND PD.id_sucursal = M.id_sucursal
          AND PD.canal = 'salon'
          AND PD.estado IN
          (
              'pendiente',
              'en_preparacion',
              'listo',
              'entregado'
          )

        ORDER BY PD.fecha_hora DESC,
                 PD.id_pedido DESC
    ) P

    LEFT JOIN dbo.EMPLEADO E
        ON E.id_empleado = P.id_mesero

    WHERE M.id_sucursal = @Sucursal
      AND M.[número] = @NumeroMesa;";


            try
            {
                // ====================================================
                // 3. CONECTAR CON SQL SERVER
                // ====================================================

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    using (SqlCommand comando =
                        new SqlCommand(consulta, conexion))
                    {
                        comando.Parameters.Add(
                            "@NumeroMesa",
                            SqlDbType.Int
                        ).Value = mesaSeleccionada;
                        comando.Parameters.Add(
    "@Sucursal",
    SqlDbType.Int).Value = 1;

                        using (SqlDataReader lector =
                            comando.ExecuteReader())
                        {
                            if (lector.Read())
                            {
                                // ========================================
                                // 4. OBTENER LOS DATOS PRINCIPALES
                                // ========================================

                                int numeroMesa =
                                    Convert.ToInt32(
                                        lector["NumeroMesa"]);

                                int capacidad =
                                    Convert.ToInt32(
                                        lector["Capacidad"]);

                                string estado =
                                    lector["EstadoMesa"]
                                    .ToString()
                                    .Trim()
                                    .ToLower();

                                // ========================================
                                // 5. MOSTRAR NÚMERO Y CAPACIDAD
                                // ========================================

                                lblTituloDetalleMesa.Text =
                                 "Mesa " + numeroMesa + " de 10";

                                lblDetalleNumero.Text =
                                    numeroMesa.ToString();

                                lblDetalleCapacidad.Text =
                                    capacidad + " personas";

                                // ========================================
                                // 6. LIMPIAR LA INFORMACIÓN ANTERIOR
                                // ========================================

                                lblDetallePedido.Text = "Ninguno";
                                lblDetalleCliente.Text = "Ninguno";
                                lblDetalleMesero.Text = "Ninguno";
                                lblDetalleTiempo.Text = "Ninguno";

                                // ========================================
                                // 7. VERIFICAR SI EXISTE UN PEDIDO
                                // ========================================

                                bool tienePedido = false;

                                if (lector["NumeroPedido"] != DBNull.Value)
                                {
                                    tienePedido = true;
                                }

                                // ========================================
                                // 8. ACTUALIZAR LOS DOS ESTADOS
                                // ========================================

                                // MESA LIBRE - VERDE
                                if (estado == "libre")
                                {
                                    lblDetalleEstado.Text = "Libre";

                                    textEstadoDetalleMesa.Text = "Libre";

                                    textEstadoDetalleMesa.FillColor =
                                        Color.FromArgb(0, 235, 133);
                                }

                                // MESA OCUPADA - ROJO
                                else if (estado == "ocupada")
                                {
                                    lblDetalleEstado.Text = "Ocupada";
                                    textEstadoDetalleMesa.Text = "Ocupada";
                                    textEstadoDetalleMesa.FillColor =
                                        Color.FromArgb(250, 128, 120);
                                }

                                // MESA RESERVADA - AMARILLO PASTEL
                                else if (estado == "reservada")
                                {
                                    lblDetalleEstado.Text = "Reservada";

                                    textEstadoDetalleMesa.Text = "Reservada";

                                    textEstadoDetalleMesa.FillColor =
                                        Color.FromArgb(242, 230, 139);
                                }

                                // MESA INACTIVA
                                else
                                {
                                    lblDetalleEstado.Text = "Inactiva";

                                    textEstadoDetalleMesa.Text = "Inactiva";

                                    textEstadoDetalleMesa.FillColor =
                                        Color.LightGray;
                                }

                                // Configuración del indicador superior.
                                textEstadoDetalleMesa.ForeColor = Color.Black;

                                textEstadoDetalleMesa.TextAlign =
                                    HorizontalAlignment.Center;

                                textEstadoDetalleMesa.ReadOnly = true;

                                // ========================================
                                // 9. MOSTRAR PEDIDO Y MESERO
                                // ========================================
                                // Mostrar información básica al seleccionar mesa.
                                if (estado == "ocupada")
                                {
                                    lblDetallePedido.Text = "En curso";
                                    lblDetalleCliente.Text = "Consultar detalle";
                                    lblDetalleMesero.Text = "Consultar detalle";
                                    lblDetalleTiempo.Text = "Consultar detalle";
                                }
                                else
                                {
                                    lblDetallePedido.Text = "Ninguno";
                                    lblDetalleCliente.Text = "-";
                                    lblDetalleMesero.Text = "-";
                                    lblDetalleTiempo.Text = "-";
                                }

                                // No mostrar los detalles todavía.
                                lblDetalleCliente.Text = "-";
                                lblDetalleMesero.Text = "-";
                                lblDetalleTiempo.Text = "-";


                                // ========================================
                                // 11. CONFIGURAR BOTONES SEGÚN EL ESTADO
                                // ========================================



                                if (estado == "libre")
                                {
                                    btnAbrirPedido.Text = "Nuevo pedido";
                                    btnAbrirPedido.Enabled = true;
                                    btnReservar.Enabled = true;
                                    btnLiberarMesa.Enabled = false;
                                }
                                else if (estado == "ocupada")
                                {
                                    btnAbrirPedido.Text = "Detalle pedido";
                                    btnAbrirPedido.Enabled = tienePedido;
                                    btnReservar.Enabled = false;
                                    btnLiberarMesa.Enabled = true;
                                }
                                else if (estado == "reservada")
                                {
                                    btnAbrirPedido.Text = "Confirmar llegada";
                                    btnAbrirPedido.Enabled = true;
                                    btnReservar.Enabled = false;
                                    btnLiberarMesa.Enabled = true;
                                }
                                else
                                {
                                    btnAbrirPedido.Text = "No disponible";
                                    btnAbrirPedido.Enabled = false;
                                    btnReservar.Enabled = false;
                                    btnLiberarMesa.Enabled = false;
                                }

                                // ========================================
                                // 12. CAMBIAR EL FILLCOLOR DE LOS BOTONES
                                // ========================================

                                Guna2Button[] botones =
                                {
                            btnAbrirPedido,
                            btnReservar,
                            btnLiberarMesa
                        };

                                foreach (Guna2Button boton in botones)
                                {
                                    if (boton.Enabled == true)
                                    {
                                        // Botón habilitado: dorado.
                                        boton.FillColor = Color.Gold;
                                    }
                                    else
                                    {
                                        // Botón deshabilitado: gris.
                                        boton.FillColor = Color.Gray;
                                    }

                                    // Guna UI2 utiliza DisabledState
                                    // cuando Enabled es false.
                                    boton.DisabledState.FillColor =
                                        Color.Gray;

                                    boton.DisabledState.ForeColor =
                                        Color.White;
                                }
                            }
                            else
                            {
                                MessageBox.Show(
                                    "No se encontró la mesa seleccionada.",
                                    "Gestión de Mesas",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar el detalle de la mesa:\n" +
                    ex.Message,
                    "Gestión de Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }




        private void PrepararSeleccionMesas()
        {
            // Guardar los diez paneles principales.
            Control[] tarjetas =
            {
        pnlMesa1, pnlMesa2, pnlMesa3,
        pnlMesa4, pnlMesa5, pnlMesa6,
        pnlMesa7, pnlMesa8, pnlMesa9,
        pnlMesa10
    };

            for (int i = 0; i < tarjetas.Length; i++)
            {
                // Número de la mesa.
                int numero = i + 1;

                // Asignar el número al panel.
                tarjetas[i].Tag = numero;

                // Conectar el clic del panel.
                tarjetas[i].Click -= SeleccionarMesa;
                tarjetas[i].Click += SeleccionarMesa;

                // Conectar los controles que están dentro.
                foreach (Control hijo in tarjetas[i].Controls)
                {
                    hijo.Tag = numero;

                    hijo.Click -= SeleccionarMesa;
                    hijo.Click += SeleccionarMesa;

                    // Incluye controles dentro de otros paneles.
                    foreach (Control nieto in hijo.Controls)
                    {
                        nieto.Tag = numero;

                        nieto.Click -= SeleccionarMesa;
                        nieto.Click += SeleccionarMesa;
                    }
                }
            }
        }
        private void ConfigurarBotonesIniciales()
        {
            // No hay ninguna mesa seleccionada.
            mesaSeleccionada = 0;

            // Deshabilitar los tres botones.
            btnAbrirPedido.Enabled = false;
            btnReservar.Enabled = false;
            btnLiberarMesa.Enabled = false;

            // Color gris cuando están deshabilitados.
            Guna2Button[] botones =
            {
        btnAbrirPedido,
        btnReservar,
        btnLiberarMesa
    };

            foreach (Guna2Button boton in botones)
            {
                boton.FillColor = Color.Gray;

                boton.DisabledState.FillColor = Color.Gray;
                boton.DisabledState.ForeColor = Color.White;
            }

            // Información inicial del panel derecho.
            lblTituloDetalleMesa.Text = "Elija una mesa";

            lblDetalleNumero.Text = "-";
            lblDetalleCapacidad.Text = "-";
            lblDetalleEstado.Text = "-";
            lblDetallePedido.Text = "Ninguno";
            lblDetalleCliente.Text = "Ninguno";
            lblDetalleMesero.Text = "Ninguno";
            lblDetalleTiempo.Text = "Ninguno";

            textEstadoDetalleMesa.Text = "Sin seleccionar";
            textEstadoDetalleMesa.FillColor = Color.LightGray;
        }


        private void CargarHistorialMesas()
        {
            try
            {
                // =====================================
                // 1. CONSULTAR RESERVAS EN SQL SERVER
                // =====================================

                string sql = @"
            SELECT
                R.id_reserva AS Numero,

                'Mesa ' +
                CONVERT(VARCHAR(10), M.[número])
                    AS Mesa,

                R.nombre_cliente AS Cliente,

                CONVERT(VARCHAR(10),
                    R.fecha_hora, 103) AS Fecha,

                CONVERT(VARCHAR(5),
                    R.fecha_hora, 108) AS Hora,

                CASE
                    WHEN R.estado = 'confirmada'
                        THEN 'Confirmada'
                    ELSE R.estado
                END AS Estado,

                ISNULL(
                    NULLIF(R.observacion, ''),
                    'Sin observación'
                ) AS Observacion

            FROM dbo.RESERVA_MESA R

            INNER JOIN dbo.MESA M
                ON R.id_mesa = M.id_mesa

            WHERE M.id_sucursal = @Sucursal
              AND R.estado = 'confirmada'

              AND DATEADD(
                    MINUTE, 120, R.fecha_hora
                  ) > SYSDATETIME()

            ORDER BY
                R.fecha_hora ASC,
                R.id_reserva ASC;";

                DataTable tabla = new DataTable();

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand comando =
                    new SqlCommand(sql, conexion))
                {
                    comando.Parameters.Add(
                        "@Sucursal", SqlDbType.Int
                    ).Value = 1;

                    using (SqlDataAdapter adaptador =
                        new SqlDataAdapter(comando))
                    {
                        adaptador.Fill(tabla);
                    }
                }

                // =====================================
                // 2. PREPARAR TABLA
                // =====================================

                dgvHistorialMesas.DataSource = null;
                // Columnas, anchos y estilos se crean UNA sola vez.
                // Antes se borraban y recreaban en cada refresco
                // (al mostrarse Mesas y con cada aviso de reservas).
                if (!historialConfigurado)
                {
                    dgvHistorialMesas.AutoGenerateColumns = false;
                    dgvHistorialMesas.Columns.Clear();

                    dgvHistorialMesas.Columns.Add(
                        "colNumero", "N°");

                    dgvHistorialMesas.Columns.Add(
                        "colMesa", "MESA");

                    dgvHistorialMesas.Columns.Add(
                        "colCliente", "CLIENTE");

                    dgvHistorialMesas.Columns.Add(
                        "colFecha", "FECHA");

                    dgvHistorialMesas.Columns.Add(
                        "colHora", "HORA");

                    dgvHistorialMesas.Columns.Add(
                        "colEstado", "ESTADO");

                    dgvHistorialMesas.Columns.Add(
                        "colObservacion", "OBSERVACIÓN");

                    // =====================================
                    // 3. RELACIONAR COLUMNAS
                    // =====================================

                    dgvHistorialMesas.Columns[0]
                        .DataPropertyName = "Numero";

                    dgvHistorialMesas.Columns[1]
                        .DataPropertyName = "Mesa";

                    dgvHistorialMesas.Columns[2]
                        .DataPropertyName = "Cliente";

                    dgvHistorialMesas.Columns[3]
                        .DataPropertyName = "Fecha";

                    dgvHistorialMesas.Columns[4]
                        .DataPropertyName = "Hora";

                    dgvHistorialMesas.Columns[5]
                        .DataPropertyName = "Estado";

                    dgvHistorialMesas.Columns[6]
                        .DataPropertyName = "Observacion";

                    // =====================================
                    // 4. TAMAÑO DE COLUMNAS
                    // =====================================

                    dgvHistorialMesas.AutoSizeColumnsMode =
                        DataGridViewAutoSizeColumnsMode.Fill;

                    dgvHistorialMesas.Columns[0].FillWeight = 7;
                    dgvHistorialMesas.Columns[1].FillWeight = 12;
                    dgvHistorialMesas.Columns[2].FillWeight = 22;
                    dgvHistorialMesas.Columns[3].FillWeight = 15;
                    dgvHistorialMesas.Columns[4].FillWeight = 10;
                    dgvHistorialMesas.Columns[5].FillWeight = 15;
                    dgvHistorialMesas.Columns[6].FillWeight = 19;

                    dgvHistorialMesas.Columns[6]
                        .DefaultCellStyle.WrapMode =
                            DataGridViewTriState.True;

                    // =====================================
                    // 5. CONFIGURAR TABLA
                    // =====================================

                    dgvHistorialMesas.ReadOnly = true;

                    dgvHistorialMesas.AllowUserToAddRows = false;

                    dgvHistorialMesas.AllowUserToDeleteRows = false;

                    dgvHistorialMesas.RowHeadersVisible = false;

                    dgvHistorialMesas.SelectionMode =
                        DataGridViewSelectionMode.FullRowSelect;

                    dgvHistorialMesas.AutoSizeRowsMode =
                        DataGridViewAutoSizeRowsMode.AllCells;

                    dgvHistorialMesas.RowTemplate.Height = 35;

                    dgvHistorialMesas.BackgroundColor =
                        Color.White;

                    dgvHistorialMesas.BorderStyle =
                        BorderStyle.None;

                    // =====================================
                    // 6. COLORES DE LAS FILAS
                    // =====================================

                    dgvHistorialMesas.DefaultCellStyle.BackColor =
                        Color.White;

                    dgvHistorialMesas.DefaultCellStyle.ForeColor =
                        Color.Black;

                    dgvHistorialMesas.AlternatingRowsDefaultCellStyle
                        .BackColor = Color.WhiteSmoke;

                    dgvHistorialMesas.AlternatingRowsDefaultCellStyle
                        .ForeColor = Color.Black;

                    dgvHistorialMesas.DefaultCellStyle
                        .SelectionBackColor =
                            Color.FromArgb(255, 245, 170);

                    dgvHistorialMesas.DefaultCellStyle
                        .SelectionForeColor = Color.Black;

                    // =====================================
                    // 7. ENCABEZADO AMARILLO
                    // =====================================

                    dgvHistorialMesas.EnableHeadersVisualStyles =
                        false;

                    dgvHistorialMesas.ColumnHeadersDefaultCellStyle
                        .BackColor = Color.Gold;

                    dgvHistorialMesas.ColumnHeadersDefaultCellStyle
                        .ForeColor = Color.Black;

                    dgvHistorialMesas.ColumnHeadersDefaultCellStyle
                        .Font = new Font(
                            "Segoe UI", 9, FontStyle.Bold);

                    dgvHistorialMesas.ColumnHeadersHeight = 38;

                    historialConfigurado = true;
                }

                // =====================================
                // 8. MOSTRAR RESERVAS
                // =====================================

                dgvHistorialMesas.DataSource = tabla;

                dgvHistorialMesas.ClearSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar reservas:\n" +
                    ex.Message,
                    "Gestión de Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private void btnAbrirPedido_Click(object sender, EventArgs e)
        {
            if (mesaSeleccionada <= 0)
            {
                MessageBox.Show("Seleccione una mesa.");
                return;
            }

            // Si la mesa está reservada, conservar aquí
            // la confirmación de llegada que ya implementamos.
            if (lblDetalleEstado.Text.Trim().ToLower() == "reservada")
            {
                DialogResult respuesta = MessageBox.Show(
                    "¿Llegó el comensal de la Mesa " +
                    mesaSeleccionada + "?",
                    "Confirmar llegada",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (respuesta != DialogResult.Yes)
                    return;

                try
                {
                    ControlReservasAutomaticas
                        .ConfirmarLlegadaPorNumeroMesa(mesaSeleccionada);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
            }

            // Abrir pedidos con la mesa seleccionada.
            // El Navegador descarta un Pedidos anterior (avisando si
            // tiene productos sin guardar) y crea uno para esta mesa.
            Navegador.AbrirPedidosConMesa(this, mesaSeleccionada);
        }

        private void btnReservar_Click(object sender, EventArgs e)
        {

            if (mesaSeleccionada == 0)
            {
                MessageBox.Show(
                    "Primero seleccione una mesa.",
                    "Gestión de Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // Abrir el formulario de reservas.
            btnNuevaReserva_Click(sender, e);
            // 2. Confirmar la reserva.
            DialogResult respuesta = MessageBox.Show(
                "¿Desea reservar la Mesa " +
                mesaSeleccionada + "?",
                "Confirmar reserva",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (respuesta != DialogResult.Yes)
            {
                return;
            }

            // 3. Actualizar la mesa en SQL Server.
            string sql = @"
       UPDATE M
SET estado = 'reservada'
FROM dbo.MESA M
        WHERE M.[número] = @NumeroMesa
          AND M.id_sucursal = @Sucursal
          AND LOWER(LTRIM(RTRIM(M.estado))) = 'libre'
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.PEDIDO P
              WHERE P.id_mesa = M.id_mesa
                AND P.estado IN
                (
                    'pendiente',
                    'en_preparacion',
                    'listo',
                    'entregado'
                )
          );";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@NumeroMesa",
                        SqlDbType.Int).Value =
                        mesaSeleccionada;

                    cmd.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    int actualizados =
                        cmd.ExecuteNonQuery();

                    if (actualizados == 0)
                    {
                        MessageBox.Show(
                            "No se pudo reservar la mesa.\n" +
                            "Verifique que siga libre.",
                            "Reserva",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }

                // 4. Actualizar toda la información.
                CargarContadoresMesas();
                CargarEstadosMesas();
                CargarDetalleMesa();
                FiltrarMesas();

                MessageBox.Show(
                    "Mesa " + mesaSeleccionada +
                    " reservada correctamente.",
                    "Reserva exitosa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al reservar la mesa:\n" +
                    ex.Message,
                    "Gestión de Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }


        }
        private void CobrarYLiberarMesaOcupada()
        {
            try
            {
                int idPedido = 0;

                // 1. Buscar el pedido activo de la mesa.
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    string sql = @"
                SELECT TOP (2) P.id_pedido
                FROM dbo.PEDIDO P
                INNER JOIN dbo.MESA M
                    ON P.id_mesa = M.id_mesa
                WHERE M.[número] = @Mesa
                  AND M.id_sucursal = 1
                  AND M.estado = 'ocupada'
                  AND P.canal = 'salon'
                  AND P.estado IN
                  (
                      'pendiente',
                      'en_preparacion',
                      'listo',
                      'entregado'
                  )
                ORDER BY P.fecha_hora DESC;";

                    using (SqlCommand cmd =
                        new SqlCommand(sql, conexion))
                    {
                        cmd.Parameters.Add(
                            "@Mesa", SqlDbType.Int).Value =
                            mesaSeleccionada;

                        using (SqlDataReader lector =
                            cmd.ExecuteReader())
                        {
                            if (!lector.Read())
                            {
                                MessageBox.Show(
                                    "No se encontró un pedido activo. " +
                                    "Revise el estado de la mesa.");
                                return;
                            }

                            idPedido = Convert.ToInt32(
                                lector["id_pedido"]);

                            if (lector.Read())
                            {
                                MessageBox.Show(
                                    "La mesa tiene más de un pedido activo. " +
                                    "Revise los pedidos antes de cobrar.");
                                return;
                            }
                        }
                    }
                }

                // 2. Solicitar confirmación.
                DialogResult respuesta = MessageBox.Show(
                    "Mesa " + mesaSeleccionada +
                    "\nPedido #" + idPedido.ToString("D4") +
                    "\n\n¿Desea abrir el cobro para liberar la mesa?",
                    "Cobrar pedido",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (respuesta != DialogResult.Yes)
                    return;

                // 3. Abrir el formulario de cobro existente.
                using (Generar_Boleta boleta =
                    new Generar_Boleta())
                {
                    boleta.Tag = idPedido;
                    boleta.ShowDialog(this);
                }

                // 4. Liberar solamente si el sistema
                // registró el cobro según el flujo actual.
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    using (SqlTransaction transaccion =
                        conexion.BeginTransaction(
                            IsolationLevel.Serializable))
                    {
                        string sql = @"
                    UPDATE M
                    SET estado = 'libre'
                    FROM dbo.MESA M
                    WHERE M.[número] = @Mesa
                      AND M.id_sucursal = 1
                      AND M.estado = 'ocupada'

                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.PEDIDO P
                          WHERE P.id_pedido = @Pedido
                            AND P.id_mesa = M.id_mesa
                            AND P.canal = 'salon'
                            AND P.estado = 'cobrado'
                            AND EXISTS
                            (
                                SELECT 1
                                FROM dbo.VENTA_INTERNA V
                                WHERE V.id_pedido =
                                      P.id_pedido
                            )
                      )

                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM dbo.PEDIDO P
                          WHERE P.id_mesa = M.id_mesa
                            AND P.canal = 'salon'
                            AND P.estado IN
                            (
                                'pendiente',
                                'en_preparacion',
                                'listo',
                                'entregado'
                            )
                      );";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                sql, conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Mesa", SqlDbType.Int).Value =
                                mesaSeleccionada;

                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;

                            int filas = cmd.ExecuteNonQuery();

                            if (filas != 1)
                            {
                                transaccion.Rollback();

                                MessageBox.Show(
                                    "La mesa continúa ocupada.\n\n" +
                                    "El cobro no quedó registrado " +
                                    "o existe otro pedido activo.",
                                    "Cobro pendiente",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);

                                return;
                            }
                        }

                        transaccion.Commit();
                    }
                }

                // 5. Actualizar la interfaz.
                CargarContadoresMesas();
                CargarEstadosMesas();
                CargarDetalleMesa();
                FiltrarMesas();

                MessageBox.Show(
                    "Pedido #" + idPedido.ToString("D4") +
                    " cobrado según el registro actual.\n\n" +
                    "Mesa " + mesaSeleccionada +
                    " liberada correctamente.",
                    "Mesa disponible",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo completar la operación:\n" +
                    ex.Message,
                    "Liberar mesa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void ButtonINICIO_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INICIO>(this);
        }
    }
}







