using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class FRM_COCINA : Form, IActualizableAlMostrar
    {
        private const int IdSucursal = 1;

        private readonly string cadenaConexion =
            BaseDatos.CadenaConexion;

        private readonly List<PedidoCocina> pedidos = new List<PedidoCocina>();
        private readonly Timer temporizadorCocina = new Timer();
        private string filtroCanal = "todos";
        private bool cargando = false;
        private DateTime horaServidor = DateTime.Now;

        private class PedidoCocina
        {
            public int IdPedido;
            public int NumeroMesa;
            public string Canal;
            public string Estado;
            public DateTime FechaHora;
            public List<string> Productos = new List<string>();
        }

        public FRM_COCINA()
        {
            InitializeComponent();

            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;

            // No reemplazar las tarjetas del diseñador en modo Diseño.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            ConfigurarPanelesCocina();

            guna2Button10.Click += (s, e) => CambiarFiltro("todos");
            guna2Button11.Click += (s, e) => CambiarFiltro("salon");
            guna2Button12.Click += (s, e) => CambiarFiltro("delivery");
            guna2Button13.Click += (s, e) => CambiarFiltro("recojo");

            txtBuscarProducto.TextChanged += (s, e) => MostrarPedidos();
            txtBuscarProducto.PlaceholderText = "Buscar mesa o pedido...";

            temporizadorCocina.Interval = 10000; // Actualizar cada 10 segundos.
            temporizadorCocina.Tick += (s, e) => CargarPedidosSQL();

            timerReloj.Tick += (s, e) => ActualizarFechaYHora();
            ActualizarFechaYHora();

            Shown += (s, e) =>
            {
                CargarPedidosSQL();
                temporizadorCocina.Start();
            };

            VisibleChanged += (s, e) =>
            {
                // El reloj y la actualización automática solo
                // trabajan mientras Cocina está visible.
                timerReloj.Enabled = Visible;

                if (Visible && IsHandleCreated)
                {
                    ActualizarFechaYHora();
                    temporizadorCocina.Start();
                }
                else
                {
                    temporizadorCocina.Stop();
                }
            };

            FormClosed += (s, e) => temporizadorCocina.Dispose();
        }

        // Llamado por el Navegador al volver a Cocina: cargar los
        // pedidos de inmediato, sin esperar los 10 segundos.
        public void ActualizarAlMostrar()
        {
            CargarPedidosSQL();
        }

        private void ActualizarFechaYHora()
        {
            lblHora.Text = DateTime.Now.ToString("HH:mm:ss");
            lblFecha.Text = DateTime.Now.ToString("ddd dd MMM yyyy",
                new System.Globalization.CultureInfo("es-PE"));
        }

        // Los tres contenedores YA existen en tu formulario.
        private void ConfigurarPanelesCocina()
        {
            FlowLayoutPanel[] columnas =
            {
                flowLayoutPanel1, // Pendientes
                flowLayoutPanel2, // En preparación
                flowLayoutPanel3  // Listos
            };

            foreach (FlowLayoutPanel columna in columnas)
            {
                // Retirar las seis tarjetas de demostración.
                Control[] ejemplos = columna.Controls.Cast<Control>().ToArray();
                columna.Controls.Clear();
                foreach (Control ejemplo in ejemplos)
                    ejemplo.Dispose();

                columna.FlowDirection = FlowDirection.TopDown;
                columna.WrapContents = false;
                columna.AutoScroll = true;
                columna.BackColor = Color.WhiteSmoke;
            }

            PintarFiltros();
            ActualizarContadores();
        }

        private void CambiarFiltro(string canal)
        {
            filtroCanal = canal;
            PintarFiltros();
            MostrarPedidos();
        }

        private void PintarFiltros()
        {
            Guna2Button[] botones =
            {
                guna2Button10, guna2Button11,
                guna2Button12, guna2Button13
            };
            string[] canales = { "todos", "salon", "delivery", "recojo" };

            for (int i = 0; i < botones.Length; i++)
            {
                bool activo = canales[i] == filtroCanal;
                botones[i].BorderThickness = activo ? 3 : 1;
                botones[i].BorderColor = activo ? Color.Black : Color.Goldenrod;
                botones[i].FillColor = Color.Gold;
                botones[i].ForeColor = Color.Black;
            }
        }

        // ======================================================
        // CARGA DE PEDIDOS (asíncrona)
        // ------------------------------------------------------
        // * La consulta se ejecuta en segundo plano: la interfaz
        //   no se congela aunque SQL Server tarde en responder.
        // * Solo se reconstruyen las tarjetas si los pedidos
        //   cambiaron (o cambió el minuto, para "Hace N min").
        // * Si falla, se conservan los últimos datos, se avisa en
        //   la cabecera (sin ventanas bloqueantes), se registra en
        //   el archivo de diagnóstico y se reintenta con espera
        //   creciente (10 → 20 → 40 → 60 s).
        // ======================================================
        private const string SqlPedidosCocina = @"
                SELECT
                    P.id_pedido,
                    P.canal,
                    P.estado,
                    P.fecha_hora,
                    ISNULL(M.[número], 0) AS NumeroMesa,
                    D.NombreProducto,
                    D.Cantidad,
                    SYSDATETIME() AS HoraServidor
                FROM dbo.PEDIDO P
                LEFT JOIN dbo.MESA M ON M.id_mesa = P.id_mesa
                LEFT JOIN
                (
                    SELECT DP.id_pedido, PL.nombre AS NombreProducto,
                           DP.cantidad AS Cantidad
                    FROM dbo.DETALLE_PEDIDO_PLATO DP
                    INNER JOIN dbo.PLATO PL ON PL.id_plato = DP.id_plato
                    UNION ALL
                    SELECT DC.id_pedido, C.nombre AS NombreProducto,
                           DC.cantidad AS Cantidad
                    FROM dbo.DETALLE_PEDIDO_COMBO DC
                    INNER JOIN dbo.COMBO C ON C.id_combo = DC.id_combo
                ) D ON D.id_pedido = P.id_pedido
                WHERE P.id_sucursal = @Sucursal
                  AND P.estado IN ('pendiente', 'en_preparacion', 'listo')
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM dbo.PAGO PG
                      WHERE PG.id_pedido = P.id_pedido
                  )
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM dbo.COMPROBANTE CB
                      WHERE CB.id_pedido = P.id_pedido
                  )
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM dbo.VENTA_INTERNA V
                      WHERE V.id_pedido = P.id_pedido
                  )
                  AND D.NombreProducto IS NOT NULL
                ORDER BY P.fecha_hora ASC, P.id_pedido ASC, D.NombreProducto ASC;";

        private const int IntervaloNormal = 10000;
        private string firmaPedidos = null;
        private int fallosSeguidos = 0;
        private Label lblEstadoConexion;

        private void CargarPedidosSQL()
        {
            if (cargando || IsDisposed || !Visible || !IsHandleCreated)
                return;

            cargando = true;
            string cadena = cadenaConexion;

            // La consulta va a un hilo de fondo; el resultado vuelve SIEMPRE
            // al hilo de la interfaz con BeginInvoke (no depende de que exista
            // un SynchronizationContext al momento del tic del temporizador).
            Task.Run(() => ConsultarPedidos(cadena)).ContinueWith(tarea =>
            {
                try
                {
                    if (IsDisposed || !IsHandleCreated) { cargando = false; return; }
                    BeginInvoke((Action)(() => AplicarResultado(tarea)));
                }
                catch (InvalidOperationException) { cargando = false; }   // ventana cerrada o cerrándose
            }, TaskScheduler.Default);
        }

        // Se ejecuta en el hilo de la interfaz.
        private void AplicarResultado(Task<DataTable> tarea)
        {
            try
            {
                if (IsDisposed)
                    return;

                if (tarea.IsFaulted)
                    throw tarea.Exception.GetBaseException();

                DataTable datos = tarea.Result;

                List<PedidoCocina> nuevos = new List<PedidoCocina>();
                Dictionary<int, PedidoCocina> indice = new Dictionary<int, PedidoCocina>();
                StringBuilder firma = new StringBuilder();
                DateTime hora = DateTime.Now;

                foreach (DataRow fila in datos.Rows)
                {
                    int id = Convert.ToInt32(fila["id_pedido"]);
                    PedidoCocina pedido;
                    if (!indice.TryGetValue(id, out pedido))
                    {
                        pedido = new PedidoCocina();
                        pedido.IdPedido = id;
                        pedido.NumeroMesa = Convert.ToInt32(fila["NumeroMesa"]);
                        pedido.Canal = Convert.ToString(fila["canal"]).Trim().ToLowerInvariant();
                        pedido.Estado = Convert.ToString(fila["estado"]).Trim().ToLowerInvariant();
                        pedido.FechaHora = Convert.ToDateTime(fila["fecha_hora"]);
                        indice.Add(id, pedido);
                        nuevos.Add(pedido);
                        firma.Append('|').Append(id).Append(':').Append(pedido.Estado);
                    }

                    int cantidad = Convert.ToInt32(fila["Cantidad"]);
                    string nombre = Convert.ToString(fila["NombreProducto"]);
                    pedido.Productos.Add(cantidad + " x " + nombre);
                    firma.Append(',').Append(cantidad).Append(nombre);
                    hora = Convert.ToDateTime(fila["HoraServidor"]);
                }

                // Conexión correcta: restablecer el ciclo normal.
                if (fallosSeguidos > 0)
                    Registro.Info("Cocina", "Carga restablecida tras " + fallosSeguidos + " intento(s) fallido(s).");
                fallosSeguidos = 0;
                temporizadorCocina.Interval = IntervaloNormal;
                MostrarEstadoConexion(null);

                // El minuto entra en la firma para refrescar "Hace N min".
                firma.Append("#").Append(hora.ToString("yyyyMMddHHmm"));
                string nuevaFirma = firma.ToString();

                if (nuevaFirma == firmaPedidos)
                    return;   // Nada cambió: no se reconstruyen las tarjetas.

                firmaPedidos = nuevaFirma;
                horaServidor = hora;
                pedidos.Clear();
                pedidos.AddRange(nuevos);

                ActualizarContadores();
                MostrarPedidos();
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                    return;

                fallosSeguidos++;
                Registro.Error("Cocina", ex);

                int espera = Math.Min(60000, IntervaloNormal * (1 << Math.Min(fallosSeguidos, 3)));
                temporizadorCocina.Interval = espera;

                MostrarEstadoConexion(
                    "⚠ SQL Server no respondió (" + DateTime.Now.ToString("HH:mm:ss") +
                    "). Se muestran los últimos pedidos; nuevo intento en " + (espera / 1000) + " s.");
            }
            finally
            {
                cargando = false;
            }
        }

        // Se ejecuta en segundo plano: no toca controles.
        private static DataTable ConsultarPedidos(string cadena)
        {
            DataTable datos = new DataTable();
            using (SqlConnection conexion = new SqlConnection(cadena))
            using (SqlCommand cmd = new SqlCommand(SqlPedidosCocina, conexion))
            {
                cmd.CommandTimeout = 20;
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                using (SqlDataAdapter adaptador = new SqlDataAdapter(cmd))
                    adaptador.Fill(datos);
            }
            return datos;
        }

        // Aviso no bloqueante en la cabecera amarilla.
        private void MostrarEstadoConexion(string mensaje)
        {
            if (lblEstadoConexion == null)
            {
                if (string.IsNullOrEmpty(mensaje))
                    return;

                lblEstadoConexion = new Label();
                lblEstadoConexion.AutoSize = false;
                lblEstadoConexion.BackColor = Color.Transparent;
                lblEstadoConexion.ForeColor = Color.Firebrick;
                lblEstadoConexion.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                lblEstadoConexion.TextAlign = ContentAlignment.MiddleRight;
                lblEstadoConexion.Location = new Point(560, 48);
                lblEstadoConexion.Size = new Size(620, 24);
                guna2Panel3.Controls.Add(lblEstadoConexion);
                lblEstadoConexion.BringToFront();
            }

            lblEstadoConexion.Text = mensaje ?? "";
            lblEstadoConexion.Visible = !string.IsNullOrEmpty(mensaje);
        }

        private void ActualizarContadores()
        {
            int pendientes = pedidos.Count(p => p.Estado == "pendiente");
            int preparacion = pedidos.Count(p => p.Estado == "en_preparacion");
            int listos = pedidos.Count(p => p.Estado == "listo");

            guna2HtmlLabel6.Text = pendientes.ToString("D2");
            guna2HtmlLabel9.Text = preparacion.ToString("D2");
            guna2HtmlLabel14.Text = listos.ToString("D2");
        }

        private void MostrarPedidos()
        {
            if (IsDisposed || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            string busqueda = txtBuscarProducto.Text.Trim().ToLowerInvariant();
            List<PedidoCocina> visibles = pedidos.Where(p =>
                (filtroCanal == "todos" || p.Canal == filtroCanal) &&
                (busqueda == "" ||
                 p.IdPedido.ToString().Contains(busqueda) ||
                 ("pedido " + p.IdPedido).Contains(busqueda) ||
                 ("mesa " + p.NumeroMesa).Contains(busqueda) ||
                 p.NumeroMesa.ToString() == busqueda ||
                 p.Canal.Contains(busqueda)))
                .ToList();

            MostrarColumna(flowLayoutPanel1,
                visibles.Where(p => p.Estado == "pendiente").ToList());
            MostrarColumna(flowLayoutPanel2,
                visibles.Where(p => p.Estado == "en_preparacion").ToList());
            MostrarColumna(flowLayoutPanel3,
                visibles.Where(p => p.Estado == "listo").ToList());

            // Totales inferiores corresponden al filtro aplicado.
            guna2HtmlLabel27.Text = visibles.Count(p => p.Estado == "pendiente").ToString();
            guna2HtmlLabel30.Text = visibles.Count(p => p.Estado == "en_preparacion").ToString();
            guna2HtmlLabel31.Text = visibles.Count(p => p.Estado == "listo").ToString();
        }

        private void MostrarColumna(FlowLayoutPanel contenedor,
            List<PedidoCocina> lista)
        {
            int desplazamiento = -contenedor.AutoScrollPosition.Y;
            contenedor.SuspendLayout();
            try
            {
                Control[] anteriores = contenedor.Controls.Cast<Control>().ToArray();
                contenedor.Controls.Clear();
                foreach (Control control in anteriores)
                    control.Dispose();

                if (lista.Count == 0)
                {
                    Label vacio = new Label();
                    vacio.Text = "No hay pedidos en este estado.";
                    vacio.Font = new Font("Segoe UI", 10F);
                    vacio.ForeColor = Color.DimGray;
                    vacio.TextAlign = ContentAlignment.MiddleCenter;
                    vacio.Size = new Size(Math.Max(260, contenedor.ClientSize.Width - 20), 85);
                    contenedor.Controls.Add(vacio);
                }
                else
                {
                    foreach (PedidoCocina pedido in lista)
                        contenedor.Controls.Add(CrearTarjetaPedido(pedido, contenedor));
                }
            }
            finally
            {
                contenedor.ResumeLayout(true);
                contenedor.AutoScrollPosition = new Point(0, desplazamiento);
            }
        }

        private Control CrearTarjetaPedido(PedidoCocina pedido,
            FlowLayoutPanel contenedor)
        {
            int ancho = Math.Max(295, contenedor.ClientSize.Width - 26);
            Guna2Panel tarjeta = new Guna2Panel();
            tarjeta.Width = ancho;
            tarjeta.FillColor = Color.White;
            tarjeta.BorderColor = Color.Gainsboro;
            tarjeta.BorderThickness = 1;
            tarjeta.BorderRadius = 10;
            tarjeta.Margin = new Padding(3, 4, 3, 7);

            Label titulo = new Label();
            titulo.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            titulo.ForeColor = Color.Black;
            titulo.BackColor = Color.Transparent;
            titulo.AutoSize = false;
            titulo.Location = new Point(12, 10);
            titulo.Size = new Size(ancho - 24, 25);
            titulo.Text = "Pedido #" + pedido.IdPedido.ToString("D4") +
                "  |  " + (pedido.Canal == "salon"
                    ? "Salón - Mesa " + pedido.NumeroMesa
                    : (pedido.Canal == "delivery" ? "Delivery" : "Recojo"));
            tarjeta.Controls.Add(titulo);

            Label tiempo = new Label();
            tiempo.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            tiempo.ForeColor = Color.DimGray;
            tiempo.BackColor = Color.Transparent;
            tiempo.AutoSize = false;
            tiempo.Location = new Point(12, 37);
            tiempo.Size = new Size(ancho - 24, 21);
            int minutos = Math.Max(0, (int)(horaServidor - pedido.FechaHora).TotalMinutes);
            tiempo.Text = "Recibido " + pedido.FechaHora.ToString("dd/MM HH:mm") +
                "  ·  Hace " + minutos + " min";
            tarjeta.Controls.Add(tiempo);

            Label productos = new Label();
            productos.Font = new Font("Segoe UI", 10F);
            productos.ForeColor = Color.Black;
            productos.BackColor = Color.Transparent;
            productos.AutoSize = true;
            productos.MaximumSize = new Size(ancho - 24, 0);
            productos.Location = new Point(12, 66);
            productos.Text = string.Join(Environment.NewLine, pedido.Productos);
            tarjeta.Controls.Add(productos);

            Guna2Button accion = new Guna2Button();
            accion.Width = ancho - 24;
            accion.Height = 38;
            accion.Location = new Point(12, Math.Max(100, productos.Bottom + 12));
            accion.BorderRadius = 8;
            accion.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            accion.ForeColor = pedido.Estado == "pendiente" ? Color.White : Color.Black;
            accion.FillColor = pedido.Estado == "pendiente"
                ? Color.FromArgb(230, 35, 35)
                : pedido.Estado == "en_preparacion"
                    ? Color.Gold : Color.LimeGreen;
            accion.Text = pedido.Estado == "pendiente" ? "INICIAR PREPARACIÓN"
                : pedido.Estado == "en_preparacion" ? "MARCAR LISTO"
                : "MARCAR ENTREGADO";
            int idPedido = pedido.IdPedido;
            string estadoActual = pedido.Estado;
            accion.Click += (s, e) => CambiarEstadoPedido(idPedido, estadoActual);
            tarjeta.Controls.Add(accion);

            tarjeta.Height = accion.Bottom + 12;
            return tarjeta;
        }


        private void CambiarEstadoPedido(int idPedido, string estadoActual)
        {
            // 1. Determinar el siguiente estado.
            string siguienteEstado;

            if (estadoActual == "pendiente")
                siguienteEstado = "en_preparacion";
            else if (estadoActual == "en_preparacion")
                siguienteEstado = "listo";
            else if (estadoActual == "listo")
                siguienteEstado = "entregado";
            else
                return;

            // 2. Identificar el pedido.
            PedidoCocina pedidoActual = pedidos.FirstOrDefault(
                p => p.IdPedido == idPedido);

            if (pedidoActual == null)
            {
                MessageBox.Show("No se encontró el pedido.");
                return;
            }

            int? idCocinero = null;
            int? idRepartidor = null;

            // 3. Asignar cocinero al iniciar preparación.
            if (siguienteEstado == "en_preparacion")
            {
                try
                {
                    idCocinero = SeleccionarCocinero();

                    if (!idCocinero.HasValue)
                        return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "No se pudo seleccionar el cocinero:\n" +
                        ex.Message);
                    return;
                }
            }

            // 4. Delivery: verificar el repartidor.
            if (pedidoActual.Canal == "delivery" &&
                (siguienteEstado == "listo" ||
                 siguienteEstado == "entregado"))
            {
                try
                {
                    object repartidorActual;

                    using (SqlConnection conexion =
                        new SqlConnection(cadenaConexion))
                    using (SqlCommand cmd = new SqlCommand(@"
                SELECT id_empleado_reparto
                FROM dbo.PEDIDO
                WHERE id_pedido = @Pedido
                  AND id_sucursal = @Sucursal;",
                        conexion))
                    {
                        cmd.Parameters.Add("@Pedido", SqlDbType.Int)
                            .Value = idPedido;

                        cmd.Parameters.Add("@Sucursal", SqlDbType.Int)
                            .Value = IdSucursal;

                        conexion.Open();

                        repartidorActual = cmd.ExecuteScalar();
                    }

                    if (repartidorActual == null)
                    {
                        MessageBox.Show(
                            "El pedido ya no existe.");
                        return;
                    }

                    // Si no tiene repartidor, solicitar uno.
                    if (repartidorActual == DBNull.Value)
                    {
                        idRepartidor = SeleccionarRepartidor();

                        if (!idRepartidor.HasValue)
                            return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "No se pudo seleccionar el repartidor:\n" +
                        ex.Message);
                    return;
                }
            }

            // 5. Confirmar la entrega.
            if (siguienteEstado == "entregado")
            {
                DialogResult respuesta = MessageBox.Show(
                    "¿Confirmar que el pedido #" +
                    idPedido.ToString("D4") +
                    " ya fue entregado?",
                    "Confirmar entrega",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (respuesta != DialogResult.Yes)
                    return;
            }

            // 6. Actualizar pedido en SQL Server.
            const string sql = @"
        UPDATE dbo.PEDIDO
        SET
            estado = @Nuevo,

            id_cocinero = CASE
                WHEN @Nuevo = 'en_preparacion'
                    THEN @Cocinero
                ELSE id_cocinero
            END,

            id_empleado_reparto = COALESCE(
                @Repartidor,
                id_empleado_reparto
            )

        WHERE id_pedido = @Pedido
          AND id_sucursal = @Sucursal
          AND estado = @Anterior

          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.PAGO
              WHERE id_pedido = @Pedido
          )

          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.COMPROBANTE
              WHERE id_pedido = @Pedido
          )

          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.VENTA_INTERNA
              WHERE id_pedido = @Pedido
          );";

            try
            {
                int filas;

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add("@Pedido", SqlDbType.Int)
                        .Value = idPedido;

                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int)
                        .Value = IdSucursal;

                    cmd.Parameters.Add("@Anterior", SqlDbType.VarChar, 30)
                        .Value = estadoActual;

                    cmd.Parameters.Add("@Nuevo", SqlDbType.VarChar, 30)
                        .Value = siguienteEstado;

                    cmd.Parameters.Add("@Cocinero", SqlDbType.Int)
                        .Value = idCocinero.HasValue
                            ? (object)idCocinero.Value
                            : DBNull.Value;

                    cmd.Parameters.Add("@Repartidor", SqlDbType.Int)
                        .Value = idRepartidor.HasValue
                            ? (object)idRepartidor.Value
                            : DBNull.Value;

                    conexion.Open();

                    filas = cmd.ExecuteNonQuery();
                }

                if (filas == 1)
                {
                    // Actualizar columnas y contadores.
                    CargarPedidosSQL();
                }
                else
                {
                    MessageBox.Show(
                        "El pedido ya cambió de estado " +
                        "o fue cobrado.",
                        "Cocina",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    CargarPedidosSQL();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo actualizar el pedido:\n" +
                    ex.Message,
                    "Cocina",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Navegación original.
        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            // "Cerrar Sesión": libera los módulos y vuelve al inicio de sesión.
            Navegador.CerrarSesion();
        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRMS_PEDIDOS>(this);
        }

        private void ButtonINICIO_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INICIO>(this);
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_MESAS>(this);
        }

        private void guna2Button4_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_CLIENTES>(this);
        }


        private int? SeleccionarCocinero()
        {
            DataTable cocineros = new DataTable();

            string sql = @"
    SELECT
        E.id_empleado,
        E.nombre + ' ' + E.apellidos AS Nombre
    FROM dbo.COCINERO C
    INNER JOIN dbo.EMPLEADO E
        ON C.id_empleado = E.id_empleado
    WHERE E.id_sucursal = @Sucursal
      AND E.estado = 'activo'
    ORDER BY E.nombre, E.apellidos;";

            using (SqlConnection conexion =
                new SqlConnection(cadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, conexion))
            {
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int)
                    .Value = IdSucursal;

                using (SqlDataAdapter adaptador =
                    new SqlDataAdapter(cmd))
                {
                    adaptador.Fill(cocineros);
                }
            }

            if (cocineros.Rows.Count == 0)
            {
                MessageBox.Show(
                    "No existen cocineros activos registrados.",
                    "Cocina",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return null;
            }

            using (Form ventana = new Form())
            {
                ventana.Text = "Asignar cocinero";
                ventana.StartPosition = FormStartPosition.CenterParent;
                ventana.FormBorderStyle = FormBorderStyle.FixedDialog;
                ventana.MaximizeBox = false;
                ventana.MinimizeBox = false;
                ventana.ClientSize = new Size(360, 150);

                Label etiqueta = new Label();
                etiqueta.Text = "Seleccione el cocinero responsable:";
                etiqueta.Location = new Point(20, 20);
                etiqueta.AutoSize = true;

                ComboBox combo = new ComboBox();
                combo.Location = new Point(20, 50);
                combo.Size = new Size(320, 30);
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.DataSource = cocineros;
                combo.DisplayMember = "Nombre";
                combo.ValueMember = "id_empleado";

                Button aceptar = new Button();
                aceptar.Text = "Aceptar";
                aceptar.Location = new Point(145, 100);
                aceptar.Size = new Size(90, 30);
                aceptar.DialogResult = DialogResult.OK;

                Button cancelar = new Button();
                cancelar.Text = "Cancelar";
                cancelar.Location = new Point(245, 100);
                cancelar.Size = new Size(90, 30);
                cancelar.DialogResult = DialogResult.Cancel;

                ventana.Controls.Add(etiqueta);
                ventana.Controls.Add(combo);
                ventana.Controls.Add(aceptar);
                ventana.Controls.Add(cancelar);

                ventana.AcceptButton = aceptar;
                ventana.CancelButton = cancelar;

                if (ventana.ShowDialog(this) == DialogResult.OK)
                {
                    return Convert.ToInt32(combo.SelectedValue);
                }
            }

            return null;
        }


        private int? SeleccionarRepartidor()
        {
            DataTable repartidores = new DataTable();

            string sql = @"
        SELECT
            E.id_empleado,
            LTRIM(RTRIM(
                ISNULL(E.nombre, '') + ' ' +
                ISNULL(E.apellidos, '')
            )) AS Nombre
        FROM dbo.REPARTIDOR R
        INNER JOIN dbo.EMPLEADO E
            ON R.id_empleado = E.id_empleado
        WHERE E.id_sucursal = @Sucursal
          AND E.estado = 'activo'
        ORDER BY E.nombre, E.apellidos;";

            using (SqlConnection conexion =
                new SqlConnection(cadenaConexion))
            using (SqlCommand cmd =
                new SqlCommand(sql, conexion))
            {
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int)
                    .Value = IdSucursal;

                using (SqlDataAdapter adaptador =
                    new SqlDataAdapter(cmd))
                {
                    adaptador.Fill(repartidores);
                }
            }

            if (repartidores.Rows.Count == 0)
            {
                MessageBox.Show(
                    "No hay repartidores activos registrados.",
                    "Cocina",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            using (Form ventana = new Form())
            {
                ventana.Text = "Asignar repartidor";
                ventana.StartPosition =
                    FormStartPosition.CenterParent;

                ventana.FormBorderStyle =
                    FormBorderStyle.FixedDialog;

                ventana.ClientSize = new Size(380, 160);
                ventana.MaximizeBox = false;
                ventana.MinimizeBox = false;

                Label etiqueta = new Label();
                etiqueta.Text = "Seleccione el repartidor:";
                etiqueta.Location = new Point(20, 20);
                etiqueta.AutoSize = true;

                ComboBox combo = new ComboBox();
                combo.Location = new Point(20, 50);
                combo.Size = new Size(340, 30);
                combo.DropDownStyle =
                    ComboBoxStyle.DropDownList;

                combo.DisplayMember = "Nombre";
                combo.ValueMember = "id_empleado";
                combo.DataSource = repartidores;

                Button aceptar = new Button();
                aceptar.Text = "Asignar";
                aceptar.Location = new Point(160, 105);
                aceptar.Size = new Size(95, 32);
                aceptar.DialogResult = DialogResult.OK;

                Button cancelar = new Button();
                cancelar.Text = "Cancelar";
                cancelar.Location = new Point(265, 105);
                cancelar.Size = new Size(95, 32);
                cancelar.DialogResult = DialogResult.Cancel;

                ventana.Controls.Add(etiqueta);
                ventana.Controls.Add(combo);
                ventana.Controls.Add(aceptar);
                ventana.Controls.Add(cancelar);

                ventana.AcceptButton = aceptar;
                ventana.CancelButton = cancelar;

                if (ventana.ShowDialog(this) == DialogResult.OK)
                {
                    return Convert.ToInt32(combo.SelectedValue);
                }
            }

            return null;
        }


    }
}
