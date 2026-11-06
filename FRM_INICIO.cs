
using Guna.Charts.WinForms;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class FRM_INICIO : Form, IActualizableAlMostrar
    {

        private readonly string cadenaConexion = BaseDatos.CadenaConexion;
        private Image imagenLibre;
        private Image imagenOcupada;
        private Image imagenReservada;
        public FRM_INICIO()
        {
            
            InitializeComponent();
            // ventana grande 
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            //Establecemos las 4 tarjetas 
            CargarVentasHoy();
            CargarPedidosActivos();
            CargarMesasOcupadas();

            // Establecemos el cuadrro 

            CargarUltimosPedidos();


            imagenLibre = Properties.Resources.mesaLibre;
            imagenOcupada = Properties.Resources.mesaOcupada;
            imagenReservada = Properties.Resources.mesaAmarilla; 
            // panel de mesas disponibles
            CargarEstadoMesas();

            // grafico de donas 
            CargarVentasPorCanal();
            CargarPedidosCobrados();
            CargarPlatosMasVendidos();
            AplicarEstiloInicio();

            // Últimos pedidos: número centrado e importe a la derecha
            // (el resto de la tabla ya se veía correctamente).
            if (dgvUltimoPedido.Columns.Count >= 4)
            {
                dgvUltimoPedido.Columns[0].DefaultCellStyle.Alignment = EstiloTablas.Centro;
                dgvUltimoPedido.Columns[0].HeaderCell.Style.Alignment = EstiloTablas.Centro;
                dgvUltimoPedido.Columns[3].DefaultCellStyle.Alignment = EstiloTablas.Derecha;
                dgvUltimoPedido.Columns[3].HeaderCell.Style.Alignment = EstiloTablas.Derecha;
            }

            // El reloj solo trabaja mientras Inicio está visible.
            this.VisibleChanged += (s, e) =>
            {
                timerReloj.Enabled = this.Visible;

                if (this.Visible)
                    ActualizarReloj();
            };
        }

        // Llamado por el Navegador al volver a Inicio:
        // recarga tarjetas, tabla, mesas y gráficos.
        public void ActualizarAlMostrar()
        {
            CargarVentasHoy();
            CargarPedidosActivos();
            CargarMesasOcupadas();
            CargarUltimosPedidos();
            CargarEstadoMesas();
            CargarVentasPorCanal();
            CargarPedidosCobrados();
            CargarPlatosMasVendidos();
        }


        private void ActualizarReloj()
        {
            DateTime ahora = DateTime.Now;

            // Hora: 22:47:00
            lblHora.Text = ahora.ToString("HH:mm:ss");

            // Fecha: Vier. 18 Abr 2025
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

            lblFecha.Text = dias[(int)ahora.DayOfWeek] + " " +
                            ahora.Day.ToString("00") + " " +
                            meses[ahora.Month - 1] + " " +
                            ahora.Year;
        }


        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            // "Cerrar Sesión": libera los módulos y vuelve al inicio de sesión.
            Navegador.CerrarSesion();
        }

        // tarjeta de ventas de hoy 



        private void CargarPlatosMasVendidos()
        {
            // ======================================
            // 1. CONTROLES DEL DISEÑO
            // ======================================

            Guna.UI2.WinForms.Guna2HtmlLabel[] nombres =
            {
        lblPlato1,
        lblPlato2,
        lblPlato3,
        lblPlato4
    };

            Guna.UI2.WinForms.Guna2HtmlLabel[] cantidades =
            {
        lblCantidadPlato1,
        lblCantidadPlato2,
        lblCantidadPlato3,
        lblCantidadPlato4
    };

            Guna.UI2.WinForms.Guna2HtmlLabel[] porcentajes =
            {
        lblPorcentajePlato1,
        lblPorcentajePlato2,
        lblPorcentajePlato3,
        lblPorcentajePlato4
    };

            Guna.UI2.WinForms.Guna2ProgressBar[] barras =
            {
        barraPlato1,
        barraPlato2,
        barraPlato3,
        barraPlato4
    };

            // ======================================
            // 2. REINICIAR LOS CONTROLES
            // ======================================

            for (int i = 0; i < 4; i++)
            {
                nombres[i].Text = "Sin ventas";
                cantidades[i].Text = "0";
                porcentajes[i].Text = "0%";

                barras[i].Minimum = 0;
                barras[i].Maximum = 100;
                barras[i].Value = 0;

                barras[i].FillColor =
                    Color.FromArgb(240, 240, 240);

                barras[i].ProgressColor = Color.Gold;
                barras[i].ProgressColor2 = Color.Goldenrod;
            }

            // ======================================
            // 3. CONSULTAR HISTORIAL DE COBROS
            // ======================================

            string sql = @"
        WITH PedidosCobrados AS
        (
            -- Pedidos con pago registrado.
            SELECT id_pedido
            FROM dbo.PAGO

            UNION

            -- Pedidos con venta interna registrada.
            SELECT id_pedido
            FROM dbo.VENTA_INTERNA
        ),

        ResumenPlatos AS
        (
            SELECT
                D.id_plato,
                PL.nombre AS Nombre,
                SUM(D.cantidad) AS Cantidad

            FROM dbo.DETALLE_PEDIDO_PLATO D

            INNER JOIN dbo.PLATO PL
                ON PL.id_plato = D.id_plato

            INNER JOIN PedidosCobrados PC
                ON PC.id_pedido = D.id_pedido

            INNER JOIN dbo.PEDIDO P
                ON P.id_pedido = D.id_pedido

            WHERE P.id_sucursal = @Sucursal
              AND P.estado <> 'cancelado'
              AND D.cantidad > 0

            GROUP BY D.id_plato, PL.nombre
        ),

        TotalPlatos AS
        (
            SELECT
                ISNULL(SUM(Cantidad), 0)
                    AS TotalUnidades
            FROM ResumenPlatos
        )

        SELECT TOP (4)
            R.Nombre,
            R.Cantidad,
            T.TotalUnidades

        FROM ResumenPlatos R

        CROSS JOIN TotalPlatos T

        ORDER BY
            R.Cantidad DESC,
            R.Nombre ASC;";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        int posicion = 0;

                        while (lector.Read())
                        {
                            // ==========================
                            // 4. OBTENER DATOS REALES
                            // ==========================

                            string nombre = Convert.ToString(
                                lector["Nombre"]);

                            int cantidad = Convert.ToInt32(
                                lector["Cantidad"]);

                            int totalUnidades = Convert.ToInt32(
                                lector["TotalUnidades"]);

                            // ==========================
                            // 5. CALCULAR PORCENTAJE
                            // ==========================

                            double porcentaje = 0;

                            if (totalUnidades > 0)
                            {
                                porcentaje =
                                    cantidad * 100.0 /
                                    totalUnidades;
                            }

                            // ==========================
                            // 6. ACTUALIZAR CONTROLES
                            // ==========================

                            nombres[posicion].Text = nombre;

                            cantidades[posicion].Text =
                                cantidad.ToString();

                            porcentajes[posicion].Text =
                                porcentaje.ToString("0.0") + "%";

                            int valor = Convert.ToInt32(
                                Math.Round(porcentaje));

                            barras[posicion].Value =
                                Math.Max(0,
                                    Math.Min(100, valor));

                            posicion++;

                            if (posicion >= 4)
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar platos más vendidos:\n" +
                    ex.Message,
                    "INICIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }






        private void CargarVentasHoy()
        {
            string sql = @"
        WITH PagosPorPedido AS
        (
            SELECT
                PG.id_pedido,
                SUM(PG.importe) AS importe
            FROM dbo.PAGO PG
            INNER JOIN dbo.PEDIDO P
                ON P.id_pedido = PG.id_pedido
            WHERE P.id_sucursal = @Sucursal
              AND P.estado <> 'cancelado'
            GROUP BY PG.id_pedido
        ),
        VentasInternas AS
        (
            SELECT
                VI.id_pedido,
                VI.importe
            FROM dbo.VENTA_INTERNA VI
            INNER JOIN dbo.PEDIDO P
                ON P.id_pedido = VI.id_pedido
            WHERE P.id_sucursal = @Sucursal
              AND P.estado <> 'cancelado'
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.PAGO PG
                  WHERE PG.id_pedido = VI.id_pedido
              )
        )
        SELECT
            COUNT(*) AS CantidadVentas,
            ISNULL(SUM(V.importe), 0) AS TotalVentas
        FROM
        (
            SELECT importe FROM PagosPorPedido

            UNION ALL

            SELECT importe FROM VentasInternas
        ) V;";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int).Value = 1;

                    // Tiempo de espera temporal.
                    cmd.CommandTimeout = 60;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        if (lector.Read())
                        {
                            decimal ventas =
                                Convert.ToDecimal(
                                    lector["TotalVentas"]);

                            lblVentasHoy.Text =
                                "S/ " + ventas.ToString("N2");

                            guna2HtmlLabel3.Text =
                                "VENTAS TOTALES";
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                lblVentasHoy.Text = "No disponible";

                if (ex.Number == -2)
                {
                    MessageBox.Show(
                        "SQL Server tardó demasiado en " +
                        "consultar las ventas.\n\n" +
                        "Es posible que exista una " +
                        "transacción bloqueando las tablas.\n\n" +
                        "Detalle: " + ex.Message,
                        "Tiempo de espera agotado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show(
                        "Error de SQL Server:\n" +
                        ex.Message,
                        "INICIO",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                lblVentasHoy.Text = "No disponible";

                MessageBox.Show(
                    "Error al cargar las ventas:\n" +
                    ex.Message,
                    "INICIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }







        private void CargarPedidosActivos()
        {
            // =====================================
            // CONTAR TODOS LOS PEDIDOS PENDIENTES
            // =====================================

            string sql = @"
        SELECT COUNT(*)
        FROM dbo.PEDIDO
        WHERE id_sucursal = @Sucursal
          AND LOWER(LTRIM(RTRIM(estado)))
              = 'pendiente';";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    int pendientes =
                        Convert.ToInt32(cmd.ExecuteScalar());

                    lblPedidosActivos.Text =
                        pendientes.ToString();
                }
            }
            catch (Exception ex)
            {
                lblPedidosActivos.Text = "N/D";

                MessageBox.Show(
                    "Error al cargar pedidos pendientes:\n"
                    + ex.Message,
                    "INICIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }




        private void CargarMesasOcupadas()
        {
            string sql = @"
        SELECT COUNT(*)
        FROM dbo.MESA
        WHERE id_sucursal = @Sucursal
          AND LOWER(LTRIM(RTRIM(estado))) =
              'ocupada';";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int).Value = 1;

                    conexion.Open();

                    int cantidad = Convert.ToInt32(
                        cmd.ExecuteScalar());

                    lblMesasOcupadas.Text =
                        cantidad.ToString();
                }
            }
            catch (Exception ex)
            {
                lblMesasOcupadas.Text = "No disponible";

                MessageBox.Show(
                    "Error al cargar mesas:\n" + ex.Message);
            }
        }


        private void CargarPedidosCobrados()
        {
            // =====================================
            // 1. CONSULTAR PEDIDOS COBRADOS
            // =====================================

            string sql = @"
        SELECT COUNT(*)
        FROM dbo.PEDIDO
        WHERE id_sucursal = @Sucursal
          AND LOWER(LTRIM(RTRIM(estado))) =
              'cobrado';";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    int cantidad = Convert.ToInt32(
                        cmd.ExecuteScalar());

                    // Mostrar cantidad de pedidos cobrados.
                    lblN_pedidos.Text =
                        cantidad.ToString();
                }
            }
            catch (Exception ex)
            {
                lblN_pedidos.Text = "N/D";

                MessageBox.Show(
                    "Error al consultar pedidos cobrados:\n" +
                    ex.Message,
                    "INICIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // establecemos la tabla 




        private void CargarUltimosPedidos()
        {
            // ==========================================
            // 1. CONSULTAR TODOS LOS PEDIDOS
            // ==========================================

            string sql = @"
    SELECT TOP (10)
        P.id_pedido,
            CASE
                WHEN P.canal = 'salon'
                    THEN 'Mesa ' +
                         ISNULL(
                             CAST(M.[número] AS VARCHAR(10)),
                             'Sin asignar'
                         )

                WHEN P.canal = 'delivery'
                    THEN 'Delivery'

                WHEN P.canal = 'recojo'
                    THEN 'Recojo'

                ELSE P.canal
            END AS MesaCanal,

            P.estado,

            (
                ISNULL(DP.TotalPlatos, 0)
                + ISNULL(DC.TotalCombos, 0)
                - ISNULL(P.descuento_aplicado, 0)
                + ISNULL(P.propina, 0)
            ) AS Total

        FROM dbo.PEDIDO P

        LEFT JOIN dbo.MESA M
            ON P.id_mesa = M.id_mesa

        OUTER APPLY
        (
            SELECT
                SUM(
                    cantidad * precio_unitario
                    - ISNULL(descuento, 0)
                ) AS TotalPlatos

            FROM dbo.DETALLE_PEDIDO_PLATO

            WHERE id_pedido = P.id_pedido
        ) DP

        OUTER APPLY
        (
            SELECT
                SUM(
                    cantidad * precio_unitario
                    - ISNULL(descuento, 0)
                ) AS TotalCombos

            FROM dbo.DETALLE_PEDIDO_COMBO

            WHERE id_pedido = P.id_pedido
        ) DC

        WHERE P.id_sucursal = @Sucursal

        ORDER BY P.id_pedido DESC;";

            try
            {
                // ==========================================
                // 2. PREPARAR LA TABLA
                // ==========================================

                dgvUltimoPedido.DataSource = null;
                dgvUltimoPedido.AutoGenerateColumns = false;

                dgvUltimoPedido.Rows.Clear();

                // Quitar columnas adicionales del gráfico.
                while (dgvUltimoPedido.Columns.Count > 4)
                {
                    int ultima =
                        dgvUltimoPedido.Columns.Count - 1;

                    dgvUltimoPedido.Columns.RemoveAt(ultima);
                }

                if (dgvUltimoPedido.Columns.Count != 4)
                {
                    throw new Exception(
                        "La tabla debe tener cuatro columnas.");
                }

                dgvUltimoPedido.ReadOnly = true;
                dgvUltimoPedido.AllowUserToAddRows = false;
                dgvUltimoPedido.AllowUserToDeleteRows = false;

                dgvUltimoPedido.RowHeadersVisible = false;
                dgvUltimoPedido.ScrollBars =
                    ScrollBars.Vertical;

                dgvUltimoPedido.AutoSizeColumnsMode =
                    DataGridViewAutoSizeColumnsMode.Fill;

                dgvUltimoPedido.AutoSizeRowsMode =
                    DataGridViewAutoSizeRowsMode.None;

                dgvUltimoPedido.RowTemplate.Height = 27;

                dgvUltimoPedido.EnableHeadersVisualStyles = false;

                dgvUltimoPedido.ColumnHeadersDefaultCellStyle
                    .BackColor = Color.Gold;

                dgvUltimoPedido.ColumnHeadersDefaultCellStyle
                    .ForeColor = Color.Black;

                dgvUltimoPedido.BackgroundColor = Color.White;

                // Encabezados.
                dgvUltimoPedido.Columns[0].HeaderText = "N.º";
                dgvUltimoPedido.Columns[1].HeaderText = "Mesa/Canal";
                dgvUltimoPedido.Columns[2].HeaderText = "Estado";
                dgvUltimoPedido.Columns[3].HeaderText = "Total";

                // Ancho de las columnas.
                dgvUltimoPedido.Columns[0].FillWeight = 15;
                dgvUltimoPedido.Columns[1].FillWeight = 35;
                dgvUltimoPedido.Columns[2].FillWeight = 25;
                dgvUltimoPedido.Columns[3].FillWeight = 25;

                // ==========================================
                // 3. CARGAR TODOS LOS PEDIDOS DE SQL
                // ==========================================

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int).Value = 1;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            int numero = Convert.ToInt32(
                                lector["id_pedido"]);

                            string mesa = Convert.ToString(
                                lector["MesaCanal"]);

                            string estado = Convert.ToString(
                                lector["estado"]).Trim().ToLower();

                            decimal total = Convert.ToDecimal(
                                lector["Total"]);

                            // Agregar fila.
                            int indice = dgvUltimoPedido.Rows.Add(
                                numero,
                                mesa,
                                estado,
                                "S/ " + total.ToString("N2"));

                            // ==================================
                            // 4. COLORES SEGÚN EL ESTADO
                            // ==================================

                            DataGridViewRow fila =
                                dgvUltimoPedido.Rows[indice];

                            Color fondo = Color.White;
                            Color letra = Color.Black;

                            if (estado == "cobrado")
                            {
                                fondo = Color.Honeydew;
                                letra = Color.DarkGreen;
                            }
                            else if (estado == "cancelado")
                            {
                                fondo = Color.MistyRose;
                                letra = Color.Firebrick;
                            }
                            else if (estado == "pendiente")
                            {
                                fondo = Color.LightGoldenrodYellow;
                                letra = Color.DarkGoldenrod;
                            }
                            else if (estado == "en_preparacion")
                            {
                                fondo = Color.AliceBlue;
                                letra = Color.SteelBlue;
                            }
                            else if (estado == "listo" ||
                                     estado == "entregado")
                            {
                                fondo = Color.Honeydew;
                                letra = Color.DarkGreen;
                            }

                            fila.DefaultCellStyle.BackColor = fondo;

                            fila.DefaultCellStyle.ForeColor =
                                Color.Black;

                            fila.DefaultCellStyle.SelectionBackColor =
                                fondo;

                            fila.DefaultCellStyle.SelectionForeColor =
                                Color.Black;

                            fila.Cells[2].Style.ForeColor = letra;
                            fila.Cells[2].Style.SelectionForeColor =
                                letra;
                        }
                    }
                }

                // ==========================================
                // 5. TERMINAR
                // ==========================================

                dgvUltimoPedido.ClearSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar todos los pedidos:\n" +
                    ex.Message,
                    "INICIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }






        private void CargarEstadoMesas()
        {
            // 1. Guardar los controles existentes.
            Guna.UI2.WinForms.Guna2Panel[] paneles =
            {
        panelMesa1, panelMesa2, panelMesa3,
        panelMesa4, panelMesa5, panelMesa6,
        panelMesa7, panelMesa8, panelMesa9,
        panelMesa10
    };

            Guna.UI2.WinForms.Guna2PictureBox[] imagenes =
            {
        picMesa1, picMesa2, picMesa3,
        picMesa4, picMesa5, picMesa6,
        picMesa7, picMesa8, picMesa9,
        picMesa10
    };

            Control[] etiquetas =
            {
        lblMesa1, lblMesa2, lblMesa3,
        lblMesa4, lblMesa5, lblMesa6,
        lblMesa7, lblMesa8, lblMesa9,
        lblMesa10
    };

            // 2. Estado inicial sin datos.
            for (int i = 0; i < 10; i++)
            {
                paneles[i].FillColor = Color.LightGray;
                paneles[i].BorderColor = Color.Gray;
                paneles[i].BorderThickness = 2;

                imagenes[i].Image = null;

                etiquetas[i].Text =
                    "Mesa " + (i + 1);
            }

            // 3. Consultar los estados reales.
            string sql = @"
        SELECT [número], estado
        FROM dbo.MESA
        WHERE id_sucursal = @Sucursal
          AND [número] BETWEEN 1 AND 10
        ORDER BY [número];";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int).Value = 1;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            int numero = Convert.ToInt32(
                                lector["número"]);

                            string estado = Convert.ToString(
                                lector["estado"])
                                .Trim()
                                .ToLowerInvariant();

                            int posicion = numero - 1;

                            if (posicion < 0 || posicion >= 10)
                                continue;

                            etiquetas[posicion].Text =
                                "Mesa " + numero;

                            // Mesa libre: verde.
                            if (estado == "libre")
                            {
                                paneles[posicion].FillColor =
                                    Color.FromArgb(226, 247, 232);

                                paneles[posicion].BorderColor =
                                    Color.SeaGreen;

                                imagenes[posicion].Image =
                                    imagenLibre;
                            }
                            // Mesa ocupada: rojo.
                            else if (estado == "ocupada")
                            {
                                paneles[posicion].FillColor =
                                    Color.FromArgb(255, 228, 228);

                                paneles[posicion].BorderColor =
                                    Color.IndianRed;

                                imagenes[posicion].Image =
                                    imagenOcupada;
                            }
                            // Mesa reservada: dorado.
                            else if (estado == "reservada")
                            {
                                paneles[posicion].FillColor =
                                    Color.FromArgb(255, 241, 198);

                                paneles[posicion].BorderColor =
                                    Color.Goldenrod;

                                imagenes[posicion].Image =
                                    imagenReservada;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar las mesas:\n" +
                    ex.Message,
                    "Inicio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }












        private void CargarVentasPorCanal()
        {
            // =====================================
            // 1. CONTADORES DE PEDIDOS CANCELADOS
            // =====================================

            int salon = 0;
            int delivery = 0;
            int recojo = 0;

            // =====================================
            // 2. CONSULTAR SOLO CANCELADOS
            // =====================================

            string sql = @"
        SELECT
            LOWER(LTRIM(RTRIM(canal))) AS Canal,
            COUNT(*) AS Cantidad
        FROM dbo.PEDIDO
        WHERE id_sucursal = @Sucursal
          AND LOWER(LTRIM(RTRIM(estado))) =
              'cancelado'
        GROUP BY
            LOWER(LTRIM(RTRIM(canal)));";

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            string canal =
                                Convert.ToString(
                                    lector["Canal"]);

                            int cantidad =
                                Convert.ToInt32(
                                    lector["Cantidad"]);

                            if (canal == "salon" ||
                                canal == "salón")
                            {
                                salon += cantidad;
                            }
                            else if (canal == "delivery")
                            {
                                delivery += cantidad;
                            }
                            else if (canal == "recojo")
                            {
                                recojo += cantidad;
                            }
                        }
                    }
                }

                // =====================================
                // 3. CALCULAR TOTAL DE CANCELADOS
                // =====================================

                int total = salon + delivery + recojo;

                double porcentajeSalon = 0;
                double porcentajeDelivery = 0;
                double porcentajeRecojo = 0;

                if (total > 0)
                {
                    porcentajeSalon =
                        salon * 100.0 / total;

                    porcentajeDelivery =
                        delivery * 100.0 / total;

                    porcentajeRecojo =
                        recojo * 100.0 / total;
                }

                // =====================================
                // 4. MOSTRAR CANTIDADES
                // =====================================

                lblCantidadSalon.Text =
                    salon.ToString();

                lblCantidadDelivery.Text =
                    delivery.ToString();

                lblCantidadRecojo.Text =
                    recojo.ToString();

                lblCantidadTotal.Text =
                    total.ToString();

                // =====================================
                // 5. MOSTRAR PORCENTAJES
                // =====================================

                lblPorcentajeSalon.Text =
                    porcentajeSalon.ToString("0.0") + "%";

                lblPorcentajeDelivery.Text =
                    porcentajeDelivery.ToString("0.0") + "%";

                lblPorcentajeRecojo.Text =
                    porcentajeRecojo.ToString("0.0") + "%";

                lblPorcentajeTotal.Text =
                    total > 0 ? "100%" : "0%";

                // =====================================
                // 6. ACTUALIZAR GRÁFICO DE DONA
                // =====================================

                GunaDona.Datasets.Clear();

                if (total > 0)
                {
                    GunaDoughnutDataset datos =
                        new GunaDoughnutDataset();

                    datos.Label = "Pedidos cancelados";

                    datos.DataPoints.Add(new LPoint()
                    {
                        Label = "Salón",
                        Y = salon
                    });

                    datos.DataPoints.Add(new LPoint()
                    {
                        Label = "Delivery",
                        Y = delivery
                    });

                    datos.DataPoints.Add(new LPoint()
                    {
                        Label = "Recojo",
                        Y = recojo
                    });

                    datos.TargetChart = GunaDona;

                    GunaDona.Datasets.Add(datos);
                }

                // Ocultar leyenda del gráfico:
                // ya tenemos cantidades a la derecha.
                GunaDona.Legend.Display = false;

                GunaDona.Update();
                GunaDona.Refresh();

                // Título correcto.
                guna2HtmlLabel50.Text =
                    "Pedidos cancelados por canal";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar pedidos cancelados:\n" +
                    ex.Message,
                    "INICIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
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

        private void ButtonINICIO_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INICIO>(this);
        }

        private void timerReloj_Tick(object sender, EventArgs e)
        {
            // ver hora y fecha 
            ActualizarReloj();
        }


        private void AplicarEstiloInicio()
        {

            // ======================================
            // VENTAS TOTALES - AMARILLO
            // ======================================
            guna2Panel4.FillColor = Color.LemonChiffon;
            guna2Panel4.BorderColor = Color.Gold;

            // ======================================
            // PEDIDOS ACTIVOS - ROJO
            // ======================================
            guna2Panel5.FillColor = Color.MistyRose;
            guna2Panel5.BorderColor = Color.IndianRed;

            // ======================================
            // MESAS OCUPADAS - AZUL
            // ======================================
            guna2Panel6.FillColor = Color.LightBlue;
            guna2Panel6.BorderColor = Color.SteelBlue;

            // ======================================
            // STOCK BAJO - VERDE
            // ======================================
            guna2Panel7.FillColor = Color.MediumAquamarine;
            guna2Panel7.BorderColor = Color.SeaGreen;


            // ======================================
            // 5. CORREGIR LOS TÍTULOS
            // ======================================
            guna2HtmlLabel50.Text =
                "Pedidos cancelados por canal";

            guna2HtmlLabel40.Text =
                "PRODUCTOS MÁS VENDIDOS";

            guna2HtmlLabel11.Text =
                "ÚLTIMOS PEDIDOS";

            // ======================================
            // 6. TABLA DE PEDIDOS
            // ======================================
            dgvUltimoPedido.EnableHeadersVisualStyles = false;

            dgvUltimoPedido.ColumnHeadersDefaultCellStyle
                .BackColor = Color.Gold;

            dgvUltimoPedido.ColumnHeadersDefaultCellStyle
                .ForeColor = Color.Black;

            dgvUltimoPedido.BackgroundColor =
                Color.White;

            dgvUltimoPedido.GridColor =
                Color.FromArgb(235, 235, 235);

            dgvUltimoPedido.BorderStyle =
                BorderStyle.None;
        }


    }
}


        

     