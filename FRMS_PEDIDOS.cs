using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class FRMS_PEDIDOS : Form
    {
        private int? idMeseroSeleccionado = null;


        // ==========================================
        // 1. CONEXIÓN SQL SERVER
        // ==========================================

        private readonly string cadenaConexion =
            @"Server=LAPTOP-GH63852S\SQLEXPRESS;
              Database=AL_ESTILO_PIURA;
              Integrated Security=True;";

        private Action reiniciarFiltroProductos;

        private const int IdSucursal = 1;

        // ==========================================
        // 2. VARIABLES
        // ==========================================

        private string tipoPedidoSeleccionado = "";

        private int? idMesaSeleccionada = null;
        private int numeroMesaSeleccionada = 0;
        private int? idPedidoSeleccionado = null;

        private int? idPedidoElegidoEnTabla = null;

        // Solo se usa cuando se abre Pedidos desde Gestión de Mesas.
        // El canal y la mesa quedan fijos; la lista muestra solo su pedido.
        private int? numeroMesaDesdeGestion = null;
        private bool modoMesaFija = false;


        private List<Guna2Button> botonesMesa =
            new List<Guna2Button>();

        private List<LineaPedido> carrito =
            new List<LineaPedido>();

        private DataGridView dgvDetalle;
        private DataGridView dgvListaPedidos;

        private Control btnGuardar;
        private Control btnCancelar;
        private Control btnLimpiar;
        private Control btnNuevo;

        private decimal descuentoGeneral = 0;
        private decimal propinaActual = 0;


        // ==========================================
        // 3. CLASE DEL CARRITO
        // ==========================================
        private class MesaInfo
        {
            public int IdMesa { get; set; }
            public int Numero { get; set; }
            public string Estado { get; set; }
        }
        private class LineaPedido
        {
            public string Tipo { get; set; }
            public int IdProducto { get; set; }
            public string Nombre { get; set; }
            public decimal Precio { get; set; }
            public int Cantidad { get; set; }

            // Cantidad que ya está en SQL.
            public int CantidadGuardada { get; set; }

            public decimal Descuento { get; set; }

            public decimal Subtotal
            {
                get
                {
                    return Cantidad * Precio - Descuento;
                }
            }
        }

        // ==========================================
        // 4. CONSTRUCTOR
        // ==========================================

        public FRMS_PEDIDOS()
        {
            InitializeComponent();

            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;

            ActualizarReloj();

            ConfigurarBotonesMesa();
            ConfigurarBotonesTipoPedido();
            ConfigurarControlesPedido();

            // Un solo método conecta los 19 botones.
            ConfigurarBotonesAgregarProductos();

            ConfigurarFiltroProductos();

            EstadoInicialVentana();

            Shown -= FRMS_PEDIDOS_Shown;
            Shown += FRMS_PEDIDOS_Shown;

            ConfigurarColumnaEliminar();
            // Activar el botón X de la tabla derecha.
            dgvDetallePedido.CellContentClick -= dgvDetallePedido_CellContentClick;
            dgvDetallePedido.CellContentClick += dgvDetallePedido_CellContentClick;

            ActualizarPagos();


        }
        // ABRIR PEDIDOS CON UNA MESA ENVIADA DESDE GESTIÓN DE MESAS
        public FRMS_PEDIDOS(int numeroMesa) : this()
        {
            numeroMesaDesdeGestion = numeroMesa;

            Shown += AbrirMesaSeleccionada;
        }

        private void AbrirMesaSeleccionada(object sender, EventArgs e)
        {
            try
            {
                if (!numeroMesaDesdeGestion.HasValue)
                    return;

                int numeroMesa = numeroMesaDesdeGestion.Value;

                if (numeroMesa < 1 || numeroMesa > 10)
                {
                    MessageBox.Show("El número de mesa no es válido.");
                    return;
                }

                // Primero marcar Salón y cargar las diez mesas desde SQL.
                SeleccionarTipoPedido("salon");

                if (tipoPedidoSeleccionado != "salon")
                    return;

                Guna2Button boton = botonesMesa[numeroMesa - 1];
                MesaInfo info = boton.Tag as MesaInfo;

                if (info == null)
                {
                    MessageBox.Show("No se encontró la Mesa " + numeroMesa);
                    return;
                }

                // Una mesa ocupada NO se selecciona como si estuviera libre.
                // Se consulta su pedido activo directamente por id_mesa.
                if (info.Estado == "ocupada")
                {
                    idMesaSeleccionada = info.IdMesa;
                    numeroMesaSeleccionada = info.Numero;

                    CargarPedidoMesaOcupada(info.IdMesa);

                    if (!idPedidoSeleccionado.HasValue)
                    {
                        MessageBox.Show(
                            "La Mesa " + numeroMesa +
                            " está ocupada, pero no tiene un pedido activo.");
                        return;
                    }

                    // Mantener identificado el mismo pedido para VER / Boleta.
                    idPedidoElegidoEnTabla = idPedidoSeleccionado.Value;

                    // Cargar solamente este pedido en la tabla inferior.
                    CargarTablaPedidos();

                    // El canal y la mesa son informativos, no modificables.
                    modoMesaFija = true;
                    BloquearSeleccionCanalYMesa();
                    return;
                }

                // Una mesa libre (o ya liberada tras confirmar la llegada)
                // permite comenzar un pedido nuevo con Salón y mesa elegidos.
                if (info.Estado == "libre")
                {
                    BotonMesa_Click(boton, EventArgs.Empty);
                    CargarTablaPedidos();
                    return;
                }

                MessageBox.Show(
                    "La Mesa " + numeroMesa +
                    " continúa reservada. Confirme primero la llegada.",
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo cargar el detalle de la mesa:\n" +
                    ex.Message,
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // Vista detalle: Salón y Mesa son solo informativos.
        // Los botones seleccionados conservan el dorado y el borde negro.
        private void BloquearSeleccionCanalYMesa()
        {
            // Cambiar títulos solo en esta vista, sin tocar el diseñador.
            guna2HtmlLabel3.Text = "DETALLE DEL PEDIDO";
            guna2HtmlLabel8.Text = "Productos de Mesa " + numeroMesaSeleccionada;
            guna2HtmlLabel9.Text = "Pedido de Mesa " + numeroMesaSeleccionada;

            Guna2Button[] canales =
            {
                btnTipoSalon,
                btnTipoDelivery,
                btnTipoRecojo
            };

            for (int i = 0; i < canales.Length; i++)
            {
                bool seleccionado = (i == 0); // Salón
                Guna2Button boton = canales[i];

                Color fondo = seleccionado
                    ? Color.FromArgb(255, 214, 0)
                    : Color.FromArgb(231, 234, 239);
                Color texto = seleccionado ? Color.Black : Color.DimGray;
                Color borde = seleccionado ? Color.Black : Color.LightGray;

                boton.FillColor = fondo;
                boton.ForeColor = texto;
                boton.BorderThickness = seleccionado ? 3 : 1;
                boton.BorderColor = borde;

                // Estilos necesarios porque los botones están deshabilitados.
                boton.DisabledState.FillColor = fondo;
                boton.DisabledState.ForeColor = texto;
                boton.DisabledState.BorderColor = borde;
                boton.Enabled = false;
            }

            for (int i = 0; i < botonesMesa.Count; i++)
            {
                Guna2Button boton = botonesMesa[i];
                bool seleccionada = (i + 1 == numeroMesaSeleccionada);

                Color fondo = seleccionada
                    ? Color.FromArgb(255, 214, 0)
                    : Color.FromArgb(238, 240, 243);
                Color texto = seleccionada ? Color.Black : Color.DimGray;
                Color borde = seleccionada ? Color.Black : Color.LightGray;

                boton.FillColor = fondo;
                boton.ForeColor = texto;
                boton.BorderThickness = seleccionada ? 4 : 1;
                boton.BorderColor = borde;
                boton.DisabledState.FillColor = fondo;
                boton.DisabledState.ForeColor = texto;
                boton.DisabledState.BorderColor = borde;
                boton.Enabled = false;
            }

            // Conservar Guardar y Generar Boleta para trabajar
            // sobre el mismo pedido; no cambiar el canal ni la mesa.
            Control nuevo = btnNuevo;
            if (nuevo == null)
                nuevo = BuscarControl("but_nuevopedido");
            if (nuevo != null)
                nuevo.Enabled = false;

            if (btnLimpiar != null)
                btnLimpiar.Enabled = false;

            MejorarDisenoTablasDetalle();
        }

        // Mejorar la legibilidad de las dos tablas de la vista detalle.
        private void MejorarDisenoTablasDetalle()
        {
            if (!modoMesaFija)
                return;

            // Tabla derecha: permitir ver nombres largos de platos.
            if (dgvDetallePedido != null && dgvDetallePedido.Columns.Count >= 5)
            {
                DataGridView tabla = dgvDetallePedido;
                tabla.BackgroundColor = Color.White;
                tabla.GridColor = Color.FromArgb(225, 228, 233);
                tabla.BorderStyle = BorderStyle.None;
                tabla.RowHeadersVisible = false;
                tabla.EnableHeadersVisualStyles = false;
                tabla.ColumnHeadersHeight = 38;
                tabla.ColumnHeadersDefaultCellStyle.BackColor = Color.Gold;
                tabla.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
                tabla.ColumnHeadersDefaultCellStyle.Font =
                    new Font("Segoe UI", 9, FontStyle.Bold);
                tabla.DefaultCellStyle.Font =
                    new Font("Segoe UI", 9, FontStyle.Regular);
                tabla.DefaultCellStyle.ForeColor = Color.Black;
                tabla.DefaultCellStyle.SelectionBackColor =
                    Color.FromArgb(255, 247, 207);
                tabla.DefaultCellStyle.SelectionForeColor = Color.Black;
                tabla.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                tabla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                tabla.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                tabla.RowTemplate.MinimumHeight = 32;

                float[] anchos = { 10f, 43f, 16f, 18f, 13f };
                for (int i = 0; i < 5; i++)
                    tabla.Columns[i].FillWeight = anchos[i];

                tabla.Columns[1].MinimumWidth = 120;
                tabla.Columns[0].DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleCenter;
                tabla.Columns[2].DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleRight;
                tabla.Columns[3].DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleRight;
                tabla.ClearSelection();
            }

            // Tabla inferior: dar espacio a la fecha/hora y a la mesa.
            if (dgvListaPedidos != null && dgvListaPedidos.Columns.Count == 6)
            {
                DataGridView tabla = dgvListaPedidos;
                tabla.BackgroundColor = Color.White;
                tabla.GridColor = Color.FromArgb(225, 228, 233);
                tabla.BorderStyle = BorderStyle.None;
                tabla.RowHeadersVisible = false;
                tabla.EnableHeadersVisualStyles = false;
                tabla.ColumnHeadersHeight = 36;
                tabla.ColumnHeadersDefaultCellStyle.BackColor = Color.Gold;
                tabla.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
                tabla.ColumnHeadersDefaultCellStyle.Font =
                    new Font("Segoe UI", 9, FontStyle.Bold);
                tabla.DefaultCellStyle.Font =
                    new Font("Segoe UI", 9, FontStyle.Regular);
                tabla.DefaultCellStyle.SelectionBackColor =
                    Color.FromArgb(255, 247, 207);
                tabla.DefaultCellStyle.SelectionForeColor = Color.Black;
                tabla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                tabla.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
                tabla.RowTemplate.Height = 34;

                float[] anchos = { 8f, 20f, 15f, 16f, 29f, 12f };
                for (int i = 0; i < 6; i++)
                    tabla.Columns[i].FillWeight = anchos[i];

                tabla.Columns[4].MinimumWidth = 140;
                tabla.ClearSelection();
            }
        }


        private int? SeleccionarMeseroActivo()
        {
            DataTable empleados = new DataTable();


            const string sql = @"
    SELECT
        E.id_empleado,
        LTRIM(RTRIM(
            E.nombre + ' ' + E.apellidos
        )) AS NombreCompleto

    FROM dbo.MESERO M

    INNER JOIN dbo.EMPLEADO E
        ON E.id_empleado = M.id_empleado

    WHERE E.id_sucursal = @Sucursal
      AND LOWER(LTRIM(RTRIM(E.estado))) = 'activo'

    ORDER BY E.nombre, E.apellidos;";


            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int
                    ).Value = IdSucursal;

                    using (SqlDataAdapter adaptador =
                        new SqlDataAdapter(cmd))
                    {
                        adaptador.Fill(empleados);
                    }
                }

                if (empleados.Rows.Count == 0)
                {
                    MessageBox.Show(
                        "No hay empleados activos en esta sucursal.",
                        "Seleccionar mesero",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return null;
                }

                // =====================================
                // CREAR VENTANA DE SELECCIÓN
                // =====================================

                using (Form ventana = new Form())
                {
                    ventana.Text = "Asignar mesero - Salón";
                    ventana.ClientSize = new Size(440, 180);
                    ventana.StartPosition =
                        FormStartPosition.CenterParent;

                    ventana.FormBorderStyle =
                        FormBorderStyle.FixedDialog;

                    ventana.MaximizeBox = false;
                    ventana.MinimizeBox = false;
                    ventana.ShowInTaskbar = false;

                    Label etiqueta = new Label();
                    etiqueta.Text = "Seleccione el mesero responsable:";
                    etiqueta.Location = new Point(20, 20);
                    etiqueta.AutoSize = true;

                    ComboBox cmbMeseros = new ComboBox();
                    cmbMeseros.Location = new Point(20, 50);
                    cmbMeseros.Size = new Size(395, 30);
                    cmbMeseros.DropDownStyle =
                        ComboBoxStyle.DropDownList;

                    cmbMeseros.DisplayMember = "NombreCompleto";
                    cmbMeseros.ValueMember = "id_empleado";
                    cmbMeseros.DataSource = empleados;
                    cmbMeseros.SelectedIndex = -1;

                    Button btnAceptar = new Button();
                    btnAceptar.Text = "Aceptar";
                    btnAceptar.Location = new Point(225, 110);
                    btnAceptar.Size = new Size(90, 35);

                    Button btnCerrar = new Button();
                    btnCerrar.Text = "Cancelar";
                    btnCerrar.Location = new Point(325, 110);
                    btnCerrar.Size = new Size(90, 35);

                    btnCerrar.DialogResult = DialogResult.Cancel;

                    btnAceptar.Click += (sender, e) =>
                    {
                        if (cmbMeseros.SelectedIndex < 0)
                        {
                            MessageBox.Show(
                                "Seleccione un mesero.",
                                "Dato obligatorio",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);

                            return;
                        }

                        ventana.DialogResult = DialogResult.OK;
                    };

                    ventana.Controls.Add(etiqueta);
                    ventana.Controls.Add(cmbMeseros);
                    ventana.Controls.Add(btnAceptar);
                    ventana.Controls.Add(btnCerrar);

                    ventana.AcceptButton = btnAceptar;
                    ventana.CancelButton = btnCerrar;

                    if (ventana.ShowDialog(this) != DialogResult.OK)
                        return null;

                    return Convert.ToInt32(
                        cmbMeseros.SelectedValue);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar empleados:\n" + ex.Message,
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return null;
            }
        }


        private void FRMS_PEDIDOS_Shown(
            object sender, EventArgs e)
        {
            // Al entrar desde Mesas, el otro evento Shown cargará
            // solamente el pedido asociado a la mesa seleccionada.
            if (!numeroMesaDesdeGestion.HasValue)
                CargarTablaPedidos();
        }

        // ==========================================
        // 5. BUSCAR CONTROLES EXISTENTES
        // ==========================================

        private Control BuscarControl(string nombre)
        {
            Control[] encontrados =
                Controls.Find(nombre, true);

            return encontrados.Length > 0
                ? encontrados[0]
                : null;
        }


        private void ConfigurarControlesPedido()
        {
            // ==========================================
            // TABLA DERECHA: DETALLE DE PEDIDO
            // ==========================================

            dgvDetalle =
                BuscarControl("dgvDetallePedido")
                as DataGridView;

            // ==========================================
            // TABLA INFERIOR: PEDIDOS GUARDADOS
            // ==========================================

            dgvListaPedidos =
                BuscarControl("dgvPedidos")
                as DataGridView;

            // ==========================================
            // BUSCAR BOTONES EXISTENTES
            // ==========================================

            btnGuardar =
                BuscarControl("BUT_GUARDAR");

            btnCancelar =
                BuscarControl("BUT_CANCELAR");

            btnLimpiar =
                BuscarControl("btnLimpiarPedido");

            btnNuevo =
                BuscarControl("btnNuevoPedido");

            // ==========================================
            // CONECTAR BOTÓN GUARDAR
            // ==========================================

            if (btnGuardar != null)
            {
                // Eliminar posibles conexiones anteriores
                // para evitar guardar dos veces.

                btnGuardar.Click -= btnGuardarPedido_Click;
                btnGuardar.Click -= BUT_GUARDAR_Click;

                // Conectar un solo evento.
                btnGuardar.Click += BUT_GUARDAR_Click;
            }
            else
            {
                MessageBox.Show(
                    "No se encontró BUT_GUARDAR.\n" +
                    "Revise su propiedad (Name).",
                    "Botón Guardar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // ==========================================
            // CONECTAR BOTÓN CANCELAR
            // ==========================================

            if (btnCancelar != null)
            {
                btnCancelar.Click -= CancelarPedido_Click;
                btnCancelar.Click += CancelarPedido_Click;
            }

            // ==========================================
            // BOTÓN LIMPIAR
            // ==========================================

            // btnLimpiarPedido ya llama a btnLimpiarPedido_Click
            // desde el Designer (con confirmación).
            // No se conecta un segundo manejador: solo se
            // retira cualquier conexión duplicada.
            if (btnLimpiar != null)
            {
                btnLimpiar.Click -= LimpiarPedido_Click;
            }

            // ==========================================
            // CONECTAR NUEVO PEDIDO
            // ==========================================

            // btnNuevoPedido ya llama a but_nuevopedido_Click desde el Designer.
            // La lógica se delega allí a NuevoPedido_Click, una sola vez.

            // ==========================================
            // PREPARAR LAS DOS TABLAS
            // ==========================================

            PrepararTablaDetalle();
            PrepararTablaPedidos();
        }


        // ==========================================
        // 6. PREPARAR DATAGRIDVIEW DERECHO
        // ==========================================

        private void PrepararTablaDetalle()
        {
            if (dgvDetalle == null)
                return;

            dgvDetalle.DataSource = null;
            dgvDetalle.AllowUserToAddRows = false;
            dgvDetalle.AllowUserToDeleteRows = false;
            dgvDetalle.RowHeadersVisible = false;
            dgvDetalle.ReadOnly = true;

            // Crear columnas solamente si no existen.
            if (dgvDetalle.Columns.Count == 0)
            {
                dgvDetalle.Columns.Add(
                    "colCantidad", "Cant.");

                dgvDetalle.Columns.Add(
                    "colDescripcion", "Descripción");

                dgvDetalle.Columns.Add(
                    "colPrecio", "P. Unit.");

                dgvDetalle.Columns.Add(
                    "colSubtotal", "Subtotal");

                DataGridViewButtonColumn eliminar =
                    new DataGridViewButtonColumn();

                eliminar.Name = "colEliminar";
                eliminar.HeaderText = "Eliminar";
                dgvDetalle.Columns.Add(eliminar);
            }

            // Evitar el error String -> Image.
            // Las primeras cuatro columnas deben
            // ser columnas de texto.
            for (int i = 0;
                 i < Math.Min(4, dgvDetalle.Columns.Count);
                 i++)
            {
                if (dgvDetalle.Columns[i]
                    is DataGridViewImageColumn)
                {
                    MessageBox.Show(
                        "La columna " + (i + 1) +
                        " de dgvDetallePedido debe ser " +
                        "DataGridViewTextBoxColumn.\n" +
                        "Corrígela en el diseñador.");
                    return;
                }
            }

            // Si Eliminar es una columna de imagen,
            // dejar sus celdas vacías por ahora.
            if (dgvDetalle.Columns.Count > 4 &&
                dgvDetalle.Columns[4]
                    is DataGridViewImageColumn)
            {
                dgvDetalle.Columns[4]
                    .DefaultCellStyle.NullValue = null;
            }
        }

        // ==========================================
        // 7. PREPARAR TABLA INFERIOR
        // ==========================================

        private void PrepararTablaPedidos()
        {
            if (dgvListaPedidos == null)
                return;

            dgvListaPedidos.DataSource = null;
            dgvListaPedidos.AllowUserToAddRows = false;
            dgvListaPedidos.AllowUserToDeleteRows = false;
            dgvListaPedidos.RowHeadersVisible = false;
            dgvListaPedidos.ReadOnly = true;

            // Conservar columnas diseñadas.
            if (dgvListaPedidos.Columns.Count == 0)
            {
                dgvListaPedidos.Columns.Add(
                    "colNumPedido", "N°");

                dgvListaPedidos.Columns.Add(
                    "colMesaCanal", "Mesa/Canal");

                dgvListaPedidos.Columns.Add(
                    "colEstadoPedido", "Estado");

                dgvListaPedidos.Columns.Add(
                    "colTotalPedido", "Total");

                dgvListaPedidos.Columns.Add(
                    "colFechaPedido", "Fecha y Hora");

                DataGridViewButtonColumn ver =
                    new DataGridViewButtonColumn();

                ver.Name = "colVerPedido";
                ver.HeaderText = "Ver";
                ver.Text = "VER";
                ver.UseColumnTextForButtonValue = true;

                dgvListaPedidos.Columns.Add(ver);
            }

            // Si la sexta columna era de imagen,
            // sustituir SOLO esa columna por botón.
            if (dgvListaPedidos.Columns.Count == 6 &&
                dgvListaPedidos.Columns[5]
                    is DataGridViewImageColumn)
            {
                string encabezado =
                    dgvListaPedidos.Columns[5].HeaderText;

                dgvListaPedidos.Columns.RemoveAt(5);

                DataGridViewButtonColumn ver =
                    new DataGridViewButtonColumn();

                ver.Name = "colVerPedido";
                ver.HeaderText = encabezado;
                ver.Text = "VER";
                ver.UseColumnTextForButtonValue = true;

                dgvListaPedidos.Columns.Insert(5, ver);
            }

            dgvListaPedidos.CellClick -= dgvPedidos_CellClick;
            dgvListaPedidos.CellClick += dgvPedidos_CellClick;
        }

        // ==========================================
        // 8. ESTADO INICIAL
        // ==========================================

        private void EstadoInicialVentana()
        {
            tipoPedidoSeleccionado = "";

            idMesaSeleccionada = null;
            numeroMesaSeleccionada = 0;
            idPedidoSeleccionado = null;
            idMeseroSeleccionado = null;

            EstiloBotonSeleccion(btnTipoSalon, false);
            EstiloBotonSeleccion(btnTipoDelivery, false);
            EstiloBotonSeleccion(btnTipoRecojo, false);

            DeshabilitarTodasLasMesas();
            ActualizarBotonCancelar();
            MostrarCarrito();
        }

        // ==========================================
        // 9. CONFIGURAR LOS 19 BOTONES +
        // ==========================================


        private void ConfigurarBotonesAgregarProductos()
        {
            // ==========================================
            // CONFIGURACIÓN DE LOS 19 PRODUCTOS
            // RESTAURANTE AL ESTILO PIURA
            // ==========================================

            // IDs reales de dbo.PLATO en SQL Server.

            Dictionary<string, int> productos =
                new Dictionary<string, int>()
                {
            // ==============================
            // PLATOS
            // ==============================

            { "btnAgregarBBQ5", 11 },
            { "btnAgregarAcevichadas5", 12 },
            { "btnAgregarBBQ4", 13 },
            { "btnAgregarAcevichadas4", 14 },
            { "btnAgregarSalchialitas", 15 },

            { "btnAgregarLomoCarne", 16 },
            { "btnAgregarLomoPollo", 17 },
            { "btnAgregarMostrito", 18 },
            { "btnAgregarMilanesaPollo", 19 },

            { "btnAgregarTallarinVerde", 20 },
            { "btnAgregarTallarinSaltado", 21 },

            // ==============================
            // AGREGADOS
            // ==============================

            { "btnAgregarHuevo", 22 },
            { "btnAgregarPlatano", 23 },

            // ==============================
            // BEBIDAS - MARACUYÁ
            // ==============================

            { "btnAgregarMaracuya1L", 24 },
            { "btnAgregarMaracuyaMedioL", 25 },

            // ==============================
            // BEBIDAS - GASEOSAS
            // ==============================

            { "btnAgregarCocaCola", 26 },
            { "btnAgregarIncaKola", 27 },
            { "btnAgregarSprite", 28 },
            { "btnAgregarFanta", 29 }
                };

            // ==========================================
            // CONECTAR LOS 19 BOTONES +
            // ==========================================

            foreach (KeyValuePair<string, int> producto
                in productos)
            {
                // Encontrar el botón en las tarjetas.
                Control boton =
                    BuscarControl(producto.Key);

                if (boton == null)
                {
                    // Continuar sin detener el formulario.
                    System.Diagnostics.Debug.WriteLine(
                        "Botón no encontrado: " + producto.Key);

                    continue;
                }

                // Asignar el ID real de SQL Server.
                boton.Tag = "plato:" + producto.Value;

                // Evitar conexiones duplicadas.
                boton.Click -= AgregarProducto_Click;

                // Conectar el evento del botón +.
                boton.Click += AgregarProducto_Click;
            }
        }


        // ==========================================
        // 10. ÚNICO EVENTO DE LOS 19 BOTONES +
        // ==========================================

        private void AgregarProducto_Click(
            object sender, EventArgs e)
        {
            Control boton = sender as Control;

            if (boton == null || boton.Tag == null)
                return;

            // Primero seleccionar modalidad.
            if (string.IsNullOrEmpty(tipoPedidoSeleccionado))
            {
                MessageBox.Show(
                    "Primero seleccione Salón, " +
                    "Delivery o Recojo.");
                return;
            }

            // En Salón se necesita mesa.
            if (tipoPedidoSeleccionado == "salon" &&
                !idMesaSeleccionada.HasValue)
            {
                MessageBox.Show(
                    "Primero seleccione una mesa.");
                return;
            }

            string[] partes =
                boton.Tag.ToString().Split(':');

            if (partes.Length != 2)
                return;

            try
            {
                string tipo =
                    partes[0].Trim().ToLowerInvariant();

                int idProducto;

                // Gaseosas sin ID conocido.
                if (tipo == "buscar")
                {
                    idProducto =
                        BuscarGaseosaPorNombre(partes[1]);

                    // Conservar el ID descubierto.
                    boton.Tag = "plato:" + idProducto;

                    tipo = "plato";
                }
                else
                {
                    if (!int.TryParse(
                        partes[1], out idProducto))
                        return;
                }

                // Agregar al cuadro derecho.
                AgregarProducto(tipo, idProducto);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Agregar producto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // ==========================================
        // 11. BUSCAR GASEOSA EN SQL SERVER
        // ==========================================

        private int BuscarGaseosaPorNombre(string palabra)
        {
            const string sql = @"
                SELECT TOP (2)
                    id_plato,
                    nombre
                FROM dbo.PLATO
                WHERE estado = 'activo'
                  AND LOWER(nombre)
                    LIKE '%' + LOWER(@Palabra) + '%'
                ORDER BY id_plato;";

            using (SqlConnection conexion =
                new SqlConnection(cadenaConexion))
            using (SqlCommand cmd =
                new SqlCommand(sql, conexion))
            {
                cmd.Parameters.Add(
                    "@Palabra", SqlDbType.VarChar, 100
                ).Value = palabra;

                conexion.Open();

                using (SqlDataReader lector =
                    cmd.ExecuteReader())
                {
                    if (!lector.Read())
                    {
                        throw new Exception(
                            "No se encontró la gaseosa: " +
                            palabra + "\n\n" +
                            "Primero debe existir como " +
                            "producto activo en dbo.PLATO.");
                    }

                    int id =
                        Convert.ToInt32(
                            lector["id_plato"]);

                    // No elegir un producto ambiguo.
                    if (lector.Read())
                    {
                        throw new Exception(
                            "Existen varias bebidas que " +
                            "coinciden con: " + palabra +
                            "\n\nConfigure su ID exacto " +
                            "en la propiedad Tag.");
                    }

                    return id;
                }
            }
        }

        // ==========================================
        // 12. AGREGAR PRODUCTO AL CARRITO
        // ==========================================

        public void AgregarProducto(
            string tipo, int idProducto)
        {
            if (tipo != "plato" && tipo != "combo")
                return;

            if (idProducto <= 0)
                return;

            try
            {
                // No modificar pedidos facturados.
                if (idPedidoSeleccionado.HasValue)
                {
                    using (SqlConnection conexion =
                        new SqlConnection(cadenaConexion))
                    {
                        conexion.Open();

                        if (TieneComprobante(
                            conexion,
                            null,
                            idPedidoSeleccionado.Value))
                        {
                            MessageBox.Show(
                                "El pedido ya tiene comprobante.");
                            return;
                        }
                    }
                }

                // Revisar si ya está en el carrito.
                LineaPedido existente =
                    carrito.FirstOrDefault(p =>
                        p.Tipo == tipo &&
                        p.IdProducto == idProducto);

                if (existente != null)
                {
                    existente.Cantidad++;

                    MostrarCarrito();
                    return;
                }

                // ==================================
                // CONSULTAR DATOS REALES DE SQL
                // ==================================

                string tabla =
                    tipo == "plato"
                    ? "dbo.PLATO"
                    : "dbo.COMBO";

                string campoId =
                    tipo == "plato"
                    ? "id_plato"
                    : "id_combo";

                string campoPrecio =
                    tipo == "plato"
                    ? "precio"
                    : "precio_combo";

                string sql =
                    "SELECT nombre, " +
                    campoPrecio + " AS Precio " +
                    "FROM " + tabla +
                    " WHERE " + campoId +
                    " = @Id AND estado = 'activo';";

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Id", SqlDbType.Int
                    ).Value = idProducto;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        if (!lector.Read())
                        {
                            MessageBox.Show(
                                "El producto no está disponible.");
                            return;
                        }

                        carrito.Add(new LineaPedido
                        {
                            Tipo = tipo,
                            IdProducto = idProducto,
                            Nombre = Convert.ToString(
                                lector["nombre"]),
                            Precio = Convert.ToDecimal(
                                lector["Precio"]),
                            Cantidad = 1,
                            CantidadGuardada = 0,
                            Descuento = 0
                        });
                    }
                }

                MostrarCarrito();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al agregar producto:\n" +
                    ex.Message);
            }
        }

        // ==========================================
        // 13. MOSTRAR EL CARRITO EN DATAGRIDVIEW
        // ==========================================

        private void MostrarCarrito()
        {
            if (dgvDetalle != null)
            {
                // La tabla derecha necesita:
                // 0 Cantidad
                // 1 Descripción
                // 2 Precio
                // 3 Subtotal
                // 4 Eliminar

                if (dgvDetalle.Columns.Count >= 4)
                {
                    bool columnasValidas = true;

                    for (int i = 0; i < 4; i++)
                    {
                        if (dgvDetalle.Columns[i]
                            is DataGridViewImageColumn)
                        {
                            columnasValidas = false;
                        }
                    }

                    if (columnasValidas)
                    {
                        dgvDetalle.DataSource = null;
                        dgvDetalle.Rows.Clear();

                        foreach (LineaPedido producto in carrito)
                        {
                            int fila =
                                dgvDetalle.Rows.Add();

                            dgvDetalle.Rows[fila].Tag =
                                producto.Tipo + ":" +
                                producto.IdProducto;

                            dgvDetalle.Rows[fila]
                                .Cells[0].Value =
                                    producto.Cantidad;

                            dgvDetalle.Rows[fila]
                                .Cells[1].Value =
                                    producto.Nombre;

                            dgvDetalle.Rows[fila]
                                .Cells[2].Value =
                                    producto.Precio;

                            dgvDetalle.Rows[fila]
                                .Cells[3].Value =
                                    producto.Subtotal;

                            // No escribir texto en
                            // una columna de imagen.
                            if (dgvDetalle.Columns.Count > 4 &&
                                dgvDetalle.Columns[4]
                                    is DataGridViewImageColumn)
                            {
                                dgvDetalle.Rows[fila]
                                    .Cells[4].Value = null;
                            }
                        }

                        dgvDetalle.Columns[2]
                            .DefaultCellStyle.Format = "N2";

                        dgvDetalle.Columns[3]
                            .DefaultCellStyle.Format = "N2";
                    }
                }
            }

            // ======================================
            // CALCULAR SUBTOTAL, IGV Y TOTAL
            // ======================================

            decimal subtotal =
                carrito.Sum(p => p.Subtotal);

            decimal importeConIgv =
                Math.Max(
                    0, subtotal - descuentoGeneral);

            // IGV incluido en los precios.
            decimal igv =
                Math.Round(
                    importeConIgv * 18m / 118m,
                    2);

            decimal total =
                importeConIgv + propinaActual;

            MostrarMonto("lblSubtotal", subtotal);
            MostrarMonto("lblDescuento", descuentoGeneral);
            MostrarMonto("lblIGV", igv);
            MostrarMonto("lblTotalPagar", total);
        }

        // ==========================================
        // 14. MOSTRAR MONTOS EN LAS ETIQUETAS
        // ==========================================

        private void MostrarMonto(
            string nombreControl, decimal monto)
        {
            Control control =
                BuscarControl(nombreControl);

            if (control != null)
            {
                control.Text =
                    "S/ " + monto.ToString("N2");
            }
        }

        // ==========================================
        // 15. DETECTAR CAMBIOS SIN GUARDAR
        // ==========================================

        private bool CambiosPendientes()
        {
            return carrito.Any(p =>
                p.Cantidad > p.CantidadGuardada);
        }

        private void LimpiarSeleccion()
        {
            idPedidoSeleccionado = null;
            idMesaSeleccionada = null;
            numeroMesaSeleccionada = 0;
            idMeseroSeleccionado = null;
            idPedidoElegidoEnTabla = null;

            descuentoGeneral = 0;
            propinaActual = 0;

            carrito.Clear();

            MostrarCarrito();
        }

        // ==========================================
        // 16. CONFIGURAR LAS DIEZ MESAS
        // ==========================================


        private void ConfigurarBotonesMesa()
        {
            botonesMesa = new List<Guna2Button>()
    {
        btnMesa01,
        btnMesa02,
        btnMesa03,
        btnMesa04,
        btnMesa05,
        btnMesa06,
        btnMesa07,
        btnMesa08,
        btnMesa09,
        btnMesa10
    };

            for (int i = 0; i < botonesMesa.Count; i++)
            {
                Guna2Button boton = botonesMesa[i];

                boton.Text = (i + 1).ToString();
                boton.FillColor = Color.FromArgb(255, 214, 0); // amarillo
                boton.ForeColor = Color.Black;

                boton.BorderRadius = 10;
                boton.BorderThickness = 2;
                boton.BorderColor = Color.Silver;

                boton.Click -= BotonMesa_Click;
                boton.Click += BotonMesa_Click;
            }
        }

        // ==========================================
        // 17. DESHABILITAR TODAS LAS MESAS
        // ==========================================

        private void DeshabilitarTodasLasMesas()
        {
            foreach (Guna2Button boton in botonesMesa)
            {
                boton.Enabled = false;
                boton.FillColor = Color.LightGray;
                boton.BorderThickness = 0;
                boton.BorderColor = Color.LightGray;

                boton.DisabledState.FillColor =
                    Color.LightGray;
            }
        }

        // ==========================================
        // 18. CONFIGURAR TIPOS DE PEDIDO
        // ==========================================

        private void ConfigurarBotonesTipoPedido()
        {
            btnTipoSalon.Click -= btnTipoSalon_Click;
            btnTipoSalon.Click += btnTipoSalon_Click;

            btnTipoDelivery.Click -= btnTipoDelivery_Click;
            btnTipoDelivery.Click += btnTipoDelivery_Click;

            btnTipoRecojo.Click -= btnTipoRecojo_Click;
            btnTipoRecojo.Click += btnTipoRecojo_Click;
        }

        // ==========================================
        // 19. SELECCIONAR TIPO DE PEDIDO
        // ==========================================

        private void SeleccionarTipoPedido(string tipo)
        {
            // Desde "Detalle pedido" el canal Salón permanece fijo.
            if (modoMesaFija)
                return;

            if (tipo != "salon" && tipo != "delivery" && tipo != "recojo")
                return;

            // Pressing the already active mode must not discard products.
            if (tipoPedidoSeleccionado == tipo)
                return;

            if (CambiosPendientes())
            {
                DialogResult respuesta = MessageBox.Show(
                    "Hay productos sin guardar.\n" +
                    "¿Descartarlos y cambiar de modalidad?",
                    "Cambiar modalidad",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (respuesta != DialogResult.Yes)
                    return;
            }

            LimpiarSeleccion();
            tipoPedidoSeleccionado = tipo;

            EstiloBotonSeleccion(btnTipoSalon, tipo == "salon");
            EstiloBotonSeleccion(btnTipoDelivery, tipo == "delivery");
            EstiloBotonSeleccion(btnTipoRecojo, tipo == "recojo");

            if (tipo == "salon")
                CargarMesasDisponibles();
            else
                DeshabilitarTodasLasMesas();

            ActualizarBotonCancelar();
        }

        private void btnTipoSalon_Click(
            object sender, EventArgs e)
        {
            SeleccionarTipoPedido("salon");
        }

        private void btnTipoDelivery_Click(
            object sender, EventArgs e)
        {
            SeleccionarTipoPedido("delivery");
        }

        private void btnTipoRecojo_Click(
            object sender, EventArgs e)
        {
            SeleccionarTipoPedido("recojo");
        }

        // ==========================================
        // 20. CARGAR MESAS DESDE SQL SERVER
        // ==========================================



        private void CargarMesasDisponibles()
        {
            const string sql = @"
        SELECT id_mesa, [número], estado
        FROM dbo.MESA
        WHERE id_sucursal = @Sucursal
        ORDER BY [número];";

            DeshabilitarTodasLasMesas();

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int).Value =
                        IdSucursal;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            int idMesa = Convert.ToInt32(
                                lector["id_mesa"]);

                            int numero = Convert.ToInt32(
                                lector["número"]);

                            string estado = Convert.ToString(
                                lector["estado"]).Trim().ToLower();

                            if (numero < 1 ||
                                numero > botonesMesa.Count)
                            {
                                continue;
                            }

                            Guna2Button boton =
                                botonesMesa[numero - 1];

                            boton.Tag = new MesaInfo()
                            {
                                IdMesa = idMesa,
                                Numero = numero,
                                Estado = estado
                            };

                            // Habilitar libres y ocupadas.
                            // Las ocupadas mostrarán una advertencia.
                            if (estado == "libre" ||
                                estado == "ocupada")
                            {
                                boton.Enabled = true;
                            }
                            else
                            {
                                boton.Enabled = false;
                            }

                            bool seleccionada =
                                idMesaSeleccionada.HasValue &&
                                idMesaSeleccionada.Value == idMesa;

                            AplicarEstiloMesa(
                                boton,
                                estado,
                                seleccionada);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar las mesas:\n" +
                    ex.Message,
                    "Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            // Cargar la información de SQL vuelve a configurar los botones.
            // En vista detalle debemos mantener Salón y la mesa bloqueados.
            if (modoMesaFija)
                BloquearSeleccionCanalYMesa();
        }


        // ==========================================
        // 21. SELECCIONAR MESA
        // ==========================================


        private void BotonMesa_Click(
            object sender, EventArgs e)
        {
            // No cambiar la mesa cuando se consulta su pedido.
            if (modoMesaFija)
                return;

            if (tipoPedidoSeleccionado != "salon")
                return;

            Guna2Button boton = sender as Guna2Button;

            if (boton == null)
                return;

            MesaInfo info = boton.Tag as MesaInfo;

            if (info == null)
                return;

            try
            {
                string estadoMesa;

                // Verificar el estado actualizado en SQL Server.
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd = new SqlCommand(@"
            SELECT estado
            FROM dbo.MESA
            WHERE id_mesa = @Mesa
              AND id_sucursal = @Sucursal;",
                    conexion))
                {
                    cmd.Parameters.Add(
                        "@Mesa", SqlDbType.Int).Value =
                        info.IdMesa;

                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int).Value =
                        IdSucursal;

                    conexion.Open();

                    object resultado = cmd.ExecuteScalar();

                    if (resultado == null)
                    {
                        MessageBox.Show(
                            "La mesa no existe.",
                            "Seleccionar mesa");

                        CargarMesasDisponibles();
                        return;
                    }

                    estadoMesa = resultado.ToString()
                        .Trim().ToLower();
                }

                // Mesa ocupada: mostrar advertencia.
                if (estadoMesa == "ocupada")
                {
                    MessageBox.Show(
                        "La mesa " + info.Numero +
                        " está ocupada.\n\n" +
                        "Por favor, elija otra mesa disponible.",
                        "Mesa ocupada",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    CargarMesasDisponibles();
                    return;
                }

                // Solo permitir mesas libres.
                if (estadoMesa != "libre")
                {
                    MessageBox.Show(
                        "Esta mesa no está disponible.",
                        "Seleccionar mesa",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    CargarMesasDisponibles();
                    return;
                }

                // Si ya estaba seleccionada, no hacer nada.
                if (idMesaSeleccionada.HasValue &&
                    idMesaSeleccionada.Value == info.IdMesa &&
                    !idPedidoSeleccionado.HasValue)
                {
                    return;
                }

                // Proteger productos que aún no se guardaron.
                if (CambiosPendientes())
                {
                    DialogResult respuesta = MessageBox.Show(
                        "Hay productos sin guardar.\n\n" +
                        "¿Desea descartarlos y cambiar de mesa?",
                        "Cambiar mesa",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (respuesta != DialogResult.Yes)
                        return;
                }

                // Limpiar selección anterior.
                LimpiarSeleccion();

                // Guardar la mesa libre seleccionada.
                idMesaSeleccionada = info.IdMesa;
                numeroMesaSeleccionada = info.Numero;

                // Actualizar los bordes de las mesas.
                CargarMesasDisponibles();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al seleccionar mesa:\n" +
                    ex.Message,
                    "Mesas",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // ==========================================
        // 22. BUSCAR PEDIDO ACTIVO
        // ==========================================

        private int? BuscarPedidoActivo(
            SqlConnection conexion,
            SqlTransaction transaccion,
            int idMesa)
        {
            const string sql = @"
                SELECT TOP (2) id_pedido
                FROM dbo.PEDIDO WITH (UPDLOCK, HOLDLOCK)
                WHERE id_mesa = @Mesa
                  AND id_sucursal = @Sucursal
                  AND canal = 'salon'
                  AND estado IN
                  (
                    'pendiente',
                    'en_preparacion',
                    'listo',
                    'entregado'
                  )
                ORDER BY fecha_hora DESC,
                         id_pedido DESC;";

            using (SqlCommand cmd =
                new SqlCommand(
                    sql, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Mesa", SqlDbType.Int
                ).Value = idMesa;

                cmd.Parameters.Add(
                    "@Sucursal", SqlDbType.Int
                ).Value = IdSucursal;

                using (SqlDataReader lector =
                    cmd.ExecuteReader())
                {
                    if (!lector.Read())
                        return null;

                    int pedido =
                        Convert.ToInt32(
                            lector["id_pedido"]);

                    if (lector.Read())
                    {
                        throw new Exception(
                            "La mesa tiene más de un pedido activo.");
                    }

                    return pedido;
                }
            }
        }

        // ==========================================
        // 23. CARGAR MESA OCUPADA
        // ==========================================

        private void CargarPedidoMesaOcupada(int idMesa)
        {
            using (SqlConnection conexion =
                new SqlConnection(cadenaConexion))
            {
                conexion.Open();

                int? pedido =
                    BuscarPedidoActivo(
                        conexion, null, idMesa);

                if (!pedido.HasValue)
                {
                    MessageBox.Show(
                        "Mesa ocupada sin pedido activo.");
                    return;
                }

                idPedidoSeleccionado = pedido.Value;

                CargarDetallePedido(
                    conexion, pedido.Value);
            }
        }

        // ==========================================
        // 24. CARGAR DETALLE EXISTENTE EN SQL
        // ==========================================

        private void CargarDetallePedido(
            SqlConnection conexion, int idPedido)
        {
            const string sql = @"
                SELECT
                    'plato' AS Tipo,
                    D.id_plato AS IdProducto,
                    P.nombre AS Descripcion,
                    D.cantidad AS Cantidad,
                    D.precio_unitario AS Precio,
                    D.descuento AS Descuento
                FROM dbo.DETALLE_PEDIDO_PLATO D
                INNER JOIN dbo.PLATO P
                    ON D.id_plato = P.id_plato
                WHERE D.id_pedido = @Pedido

                UNION ALL

                SELECT
                    'combo',
                    D.id_combo,
                    C.nombre,
                    D.cantidad,
                    D.precio_unitario,
                    D.descuento
                FROM dbo.DETALLE_PEDIDO_COMBO D
                INNER JOIN dbo.COMBO C
                    ON D.id_combo = C.id_combo
                WHERE D.id_pedido = @Pedido;";

            DataTable datos = new DataTable();

            using (SqlCommand cmd =
                new SqlCommand(sql, conexion))
            {
                cmd.Parameters.Add(
                    "@Pedido", SqlDbType.Int
                ).Value = idPedido;

                using (SqlDataAdapter adaptador =
                    new SqlDataAdapter(cmd))
                {
                    adaptador.Fill(datos);
                }
            }

            carrito.Clear();

            foreach (DataRow fila in datos.Rows)
            {
                int cantidad =
                    Convert.ToInt32(fila["Cantidad"]);

                carrito.Add(new LineaPedido
                {
                    Tipo = fila["Tipo"].ToString(),

                    IdProducto = Convert.ToInt32(
                        fila["IdProducto"]),

                    Nombre = fila["Descripcion"].ToString(),

                    Cantidad = cantidad,
                    CantidadGuardada = cantidad,

                    Precio = Convert.ToDecimal(
                        fila["Precio"]),

                    Descuento = Convert.ToDecimal(
                        fila["Descuento"])
                });
            }

            const string sqlImportes = @"
                SELECT
                    ISNULL(descuento_aplicado, 0)
                        AS Descuento,
                    propina
                FROM dbo.PEDIDO
                WHERE id_pedido = @Pedido;";

            using (SqlCommand cmd =
                new SqlCommand(sqlImportes, conexion))
            {
                cmd.Parameters.Add(
                    "@Pedido", SqlDbType.Int
                ).Value = idPedido;

                using (SqlDataReader lector =
                    cmd.ExecuteReader())
                {
                    if (lector.Read())
                    {
                        descuentoGeneral =
                            Convert.ToDecimal(
                                lector["Descuento"]);

                        propinaActual =
                            Convert.ToDecimal(
                                lector["propina"]);
                    }
                }
            }

            MostrarCarrito();
        }

        // ==========================================
        // 25. CREAR UN PEDIDO EN SQL
        // ==========================================



        private int CrearPedido(
            SqlConnection conexion,
            SqlTransaction transaccion,
            string canal,
            int? idMesa,
            int? idMesero)
        {
            // ==========================================
            // 1. VALIDAR MODALIDAD
            // ==========================================

            if (canal != "salon" &&
                canal != "delivery" &&
                canal != "recojo")
            {
                throw new Exception(
                    "Tipo de pedido no válido.");
            }

            if (canal == "salon")
            {
                if (!idMesa.HasValue)
                {
                    throw new Exception(
                        "El pedido de Salón necesita una mesa.");
                }

                if (!idMesero.HasValue)
                {
                    throw new Exception(
                        "El pedido de Salón necesita un mesero.");
                }

                // Verificar que el empleado siga activo
                // y pertenezca a la sucursal.

                const string sqlValidar = @"
            SELECT COUNT(*)
            FROM dbo.EMPLEADO
            WHERE id_empleado = @Mesero
              AND id_sucursal = @Sucursal
              AND LOWER(LTRIM(RTRIM(estado))) = 'activo';";

                using (SqlCommand validar =
                    new SqlCommand(
                        sqlValidar, conexion, transaccion))
                {
                    validar.Parameters.Add(
                        "@Mesero", SqlDbType.Int
                    ).Value = idMesero.Value;

                    validar.Parameters.Add(
                        "@Sucursal", SqlDbType.Int
                    ).Value = IdSucursal;

                    if (Convert.ToInt32(
                        validar.ExecuteScalar()) != 1)
                    {
                        throw new Exception(
                            "El mesero seleccionado no está activo " +
                            "o no pertenece a esta sucursal.");
                    }
                }
            }
            else
            {
                // Delivery y Recojo no usan mesa ni mesero.
                if (idMesa.HasValue || idMesero.HasValue)
                {
                    throw new Exception(
                        "Delivery y Recojo no deben registrar " +
                        "mesa ni mesero.");
                }
            }

            // ==========================================
            // 2. INSERTAR PEDIDO EN SQL SERVER
            // ==========================================

            const string sql = @"
        DECLARE @NuevoPedido TABLE
        (
            id_pedido INT NOT NULL
        );

        INSERT INTO dbo.PEDIDO
        (
            fecha_hora,
            canal,
            estado,
            propina,
            descuento_aplicado,
            id_sucursal,
            id_mesa,
            id_mesero
        )
        OUTPUT INSERTED.id_pedido
        INTO @NuevoPedido(id_pedido)
        VALUES
        (
            SYSDATETIME(),
            @Canal,
            'pendiente',
            0,
            0,
            @Sucursal,
            @Mesa,
            @Mesero
        );

        SELECT id_pedido
        FROM @NuevoPedido;";

            using (SqlCommand cmd =
                new SqlCommand(sql, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Canal", SqlDbType.VarChar, 20
                ).Value = canal;

                cmd.Parameters.Add(
                    "@Sucursal", SqlDbType.Int
                ).Value = IdSucursal;

                cmd.Parameters.Add(
                    "@Mesa", SqlDbType.Int
                ).Value = idMesa.HasValue
                    ? (object)idMesa.Value
                    : DBNull.Value;

                cmd.Parameters.Add(
                    "@Mesero", SqlDbType.Int
                ).Value = idMesero.HasValue
                    ? (object)idMesero.Value
                    : DBNull.Value;

                object resultado = cmd.ExecuteScalar();

                if (resultado == null ||
                    resultado == DBNull.Value)
                {
                    throw new Exception(
                        "No se obtuvo el ID del pedido.");
                }

                return Convert.ToInt32(resultado);
            }
        }



        // ==========================================
        // 26. VERIFICAR COMPROBANTE
        // ==========================================

        private bool TieneComprobante(
            SqlConnection conexion,
            SqlTransaction transaccion,
            int idPedido)
        {
            using (SqlCommand cmd =
                new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM dbo.COMPROBANTE
                    WHERE id_pedido = @Pedido;",
                    conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Pedido", SqlDbType.Int
                ).Value = idPedido;

                return Convert.ToInt32(
                    cmd.ExecuteScalar()) > 0;
            }
        }

        // ==========================================
        // 27. BOTÓN GUARDAR PEDIDO
        // ==========================================

        private void btnGuardarPedido_Click(
            object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(
                tipoPedidoSeleccionado))
            {
                MessageBox.Show(
                    "Seleccione Salón, Delivery o Recojo.");
                return;
            }
            if (tipoPedidoSeleccionado == "salon" &&
                !idMesaSeleccionada.HasValue)
            {
                MessageBox.Show("Seleccione una mesa antes de guardar.");
                return;
            }

            if (carrito.Count == 0)
            {
                MessageBox.Show(
                    "Agregue al menos un producto.");
                return;
            }

            // Evitar duplicar cantidades.
            if (!CambiosPendientes())
            {
                CargarTablaPedidos();

                MessageBox.Show(
                    "El pedido ya está guardado.");
                return;
            }

            // Nuevo pedido en Salón: seleccionar mesero solo después
            // de validar mesa y productos pendientes.
            if (tipoPedidoSeleccionado == "salon" &&
                !idPedidoSeleccionado.HasValue &&
                !idMeseroSeleccionado.HasValue)
            {
                idMeseroSeleccionado = SeleccionarMeseroActivo();
                if (!idMeseroSeleccionado.HasValue)
                    return;
            }

            try
            {
                int idPedido;

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    using (SqlTransaction transaccion =
                        conexion.BeginTransaction(
                            IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ==========================
                            // PEDIDO SALÓN
                            // ==========================

                            if (tipoPedidoSeleccionado == "salon")
                            {
                                int mesa =
                                    idMesaSeleccionada.Value;

                                string estadoMesa;

                                using (SqlCommand cmd =
                                    new SqlCommand(@"
                                        SELECT estado
                                        FROM dbo.MESA
                                            WITH (UPDLOCK, HOLDLOCK)
                                        WHERE id_mesa = @Mesa
                                        AND id_sucursal = @Sucursal;",
                                        conexion, transaccion))
                                {
                                    cmd.Parameters.Add(
                                        "@Mesa", SqlDbType.Int
                                    ).Value = mesa;

                                    cmd.Parameters.Add(
                                        "@Sucursal", SqlDbType.Int
                                    ).Value = IdSucursal;

                                    object resultado =
                                        cmd.ExecuteScalar();

                                    if (resultado == null)
                                        throw new Exception(
                                            "La mesa no existe.");

                                    estadoMesa =
                                        resultado.ToString()
                                        .Trim().ToLowerInvariant();
                                }

                                int? existente =
                                    BuscarPedidoActivo(
                                        conexion,
                                        transaccion,
                                        mesa);

                                if (estadoMesa == "ocupada")
                                {
                                    if (!existente.HasValue)
                                        throw new Exception(
                                            "Mesa ocupada sin pedido activo.");

                                    idPedido = existente.Value;

                                    if (idPedidoSeleccionado.HasValue &&
                                        idPedidoSeleccionado.Value
                                            != idPedido)
                                    {
                                        throw new Exception(
                                            "Cambió el pedido de la mesa.");
                                    }
                                }
                                else if (estadoMesa == "libre")
                                {
                                    if (existente.HasValue)
                                        throw new Exception(
                                            "Mesa libre con pedido activo.");

                                    idPedido = CrearPedido(
      conexion,
      transaccion,
      "salon",
      mesa,
      idMeseroSeleccionado);

                                    // Marcar mesa ocupada.
                                    using (SqlCommand cmd =
                                        new SqlCommand(@"
                                            UPDATE dbo.MESA
                                            SET estado = 'ocupada'
                                            WHERE id_mesa = @Mesa
                                            AND estado = 'libre';",
                                            conexion, transaccion))
                                    {
                                        cmd.Parameters.Add(
                                            "@Mesa", SqlDbType.Int
                                        ).Value = mesa;

                                        if (cmd.ExecuteNonQuery() != 1)
                                            throw new Exception(
                                                "No se pudo ocupar la mesa.");
                                    }
                                }
                                else
                                {
                                    throw new Exception(
                                        "Mesa no disponible.");
                                }
                            }

                            // ==========================
                            // DELIVERY Y RECOJO
                            // ==========================

                            else if (idPedidoSeleccionado.HasValue)
                            {
                                idPedido =
                                    idPedidoSeleccionado.Value;

                                using (SqlCommand cmd =
                                    new SqlCommand(@"
                                        SELECT COUNT(*)
                                        FROM dbo.PEDIDO
                                            WITH (UPDLOCK, HOLDLOCK)
                                        WHERE id_pedido = @Pedido
                                        AND id_sucursal = @Sucursal
                                        AND canal = @Canal
                                        AND estado IN
                                        (
                                            'pendiente',
                                            'en_preparacion',
                                            'listo',
                                            'entregado'
                                        );",
                                        conexion, transaccion))
                                {
                                    cmd.Parameters.Add(
                                        "@Pedido", SqlDbType.Int
                                    ).Value = idPedido;

                                    cmd.Parameters.Add(
                                        "@Sucursal", SqlDbType.Int
                                    ).Value = IdSucursal;

                                    cmd.Parameters.Add(
                                        "@Canal", SqlDbType.VarChar, 20
                                    ).Value = tipoPedidoSeleccionado;

                                    if (Convert.ToInt32(
                                        cmd.ExecuteScalar()) != 1)
                                    {
                                        throw new Exception(
                                            "El pedido ya no está activo.");
                                    }
                                }
                            }
                            else
                            {
                                idPedido = CrearPedido(conexion, transaccion,
                                    tipoPedidoSeleccionado, null, null);
                            }

                            // ==========================
                            // VALIDAR COMPROBANTE Y PAGO
                            // ==========================

                            if (TieneComprobante(
                                conexion, transaccion, idPedido))
                            {
                                throw new Exception(
                                    "El pedido ya tiene comprobante.");
                            }

                            using (SqlCommand cmd =
                                new SqlCommand(@"
                                    SELECT COUNT(*)
                                    FROM dbo.PAGO
                                    WHERE id_pedido = @Pedido;",
                                    conexion, transaccion))
                            {
                                cmd.Parameters.Add(
                                    "@Pedido", SqlDbType.Int
                                ).Value = idPedido;

                                if (Convert.ToInt32(
                                    cmd.ExecuteScalar()) > 0)
                                {
                                    throw new Exception(
                                        "El pedido ya registra un pago.");
                                }
                            }

                            // ==========================
                            // GUARDAR PLATOS Y BEBIDAS
                            // ==========================

                            foreach (LineaPedido producto in carrito)
                            {
                                int nuevasUnidades =
                                    producto.Cantidad -
                                    producto.CantidadGuardada;

                                if (nuevasUnidades <= 0)
                                    continue;

                                string tabla =
                                    producto.Tipo == "plato"
                                    ? "dbo.DETALLE_PEDIDO_PLATO"
                                    : "dbo.DETALLE_PEDIDO_COMBO";

                                string campoId =
                                    producto.Tipo == "plato"
                                    ? "id_plato"
                                    : "id_combo";

                                // Comprobar que el producto
                                // continúe disponible.
                                string tablaProducto =
                                    producto.Tipo == "plato"
                                    ? "dbo.PLATO"
                                    : "dbo.COMBO";

                                string campoPrecio =
                                    producto.Tipo == "plato"
                                    ? "precio"
                                    : "precio_combo";

                                using (SqlCommand cmd =
                                    new SqlCommand(
                                        "SELECT " + campoPrecio +
                                        " FROM " + tablaProducto +
                                        " WHERE " + campoId +
                                        " = @Producto" +
                                        " AND estado = 'activo';",
                                        conexion, transaccion))
                                {
                                    cmd.Parameters.Add(
                                        "@Producto", SqlDbType.Int
                                    ).Value = producto.IdProducto;

                                    object precioSQL =
                                        cmd.ExecuteScalar();

                                    if (precioSQL == null)
                                        throw new Exception(
                                            "Producto no disponible: " +
                                            producto.Nombre);

                                    if (producto.CantidadGuardada == 0 &&
                                        Convert.ToDecimal(precioSQL)
                                            != producto.Precio)
                                    {
                                        throw new Exception(
                                            "Cambió el precio de " +
                                            producto.Nombre +
                                            ". Vuelva a cargar el producto.");
                                    }
                                }

                                if (producto.CantidadGuardada > 0)
                                {
                                    // Producto ya guardado.
                                    // Sumar nuevas unidades.
                                    string actualizar =
                                        "UPDATE " + tabla +
                                        " SET cantidad = cantidad + @Nuevas" +
                                        " WHERE id_pedido = @Pedido" +
                                        " AND " + campoId + " = @Producto" +
                                        " AND cantidad = @Anterior;";

                                    using (SqlCommand cmd =
                                        new SqlCommand(
                                            actualizar,
                                            conexion, transaccion))
                                    {
                                        cmd.Parameters.Add(
                                            "@Pedido", SqlDbType.Int
                                        ).Value = idPedido;

                                        cmd.Parameters.Add(
                                            "@Producto", SqlDbType.Int
                                        ).Value = producto.IdProducto;

                                        cmd.Parameters.Add(
                                            "@Nuevas", SqlDbType.Int
                                        ).Value = nuevasUnidades;

                                        cmd.Parameters.Add(
                                            "@Anterior", SqlDbType.Int
                                        ).Value = producto.CantidadGuardada;

                                        if (cmd.ExecuteNonQuery() != 1)
                                        {
                                            throw new Exception(
                                                "La cantidad de " +
                                                producto.Nombre +
                                                " cambió en SQL.\n" +
                                                "Vuelva a cargar el pedido.");
                                        }
                                    }
                                }
                                else
                                {
                                    // Registrar producto nuevo.
                                    string insertar =
                                        "INSERT INTO " + tabla +
                                        " (id_pedido, " + campoId +
                                        ", cantidad, precio_unitario, descuento)" +
                                        " VALUES (@Pedido, @Producto," +
                                        " @Cantidad, @Precio, 0);";

                                    using (SqlCommand cmd =
                                        new SqlCommand(
                                            insertar,
                                            conexion, transaccion))
                                    {
                                        cmd.Parameters.Add(
                                            "@Pedido", SqlDbType.Int
                                        ).Value = idPedido;

                                        cmd.Parameters.Add(
                                            "@Producto", SqlDbType.Int
                                        ).Value = producto.IdProducto;

                                        cmd.Parameters.Add(
                                            "@Cantidad", SqlDbType.Int
                                        ).Value = nuevasUnidades;

                                        SqlParameter precio =
                                            cmd.Parameters.Add(
                                                "@Precio", SqlDbType.Decimal);

                                        precio.Precision = 10;
                                        precio.Scale = 2;
                                        precio.Value = producto.Precio;

                                        cmd.ExecuteNonQuery();
                                    }
                                }
                            }

                            // CONFIRMAR GUARDADO.
                            transaccion.Commit();
                        }
                        catch
                        {
                            transaccion.Rollback();
                            throw;
                        }
                    }

                    idPedidoSeleccionado = idPedido;

                    // Cargar lo que realmente
                    // quedó guardado en SQL.
                    CargarDetallePedido(
                        conexion, idPedido);
                }

                if (tipoPedidoSeleccionado == "salon")
                    CargarMesasDisponibles();

                // ACTUALIZAR TABLA INFERIOR.
                CargarTablaPedidos();

                MessageBox.Show(
                    "Pedido N.º " + idPedido +
                    " guardado correctamente.",
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al guardar pedido:\n" +
                    ex.Message,
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        // ==========================================
        // 28. CARGAR TABLA INFERIOR PEDIDOS
        // ==========================================

        private void CargarTablaPedidos()
        {
            if (dgvListaPedidos == null)
            {
                MessageBox.Show(
                    "No existe el control dgvPedidos.");
                return;
            }

            const string sql = @"
                SELECT
                    P.id_pedido,

                    CASE
                        WHEN P.canal = 'salon'
                            THEN 'Mesa ' +
                                CAST(M.[número] AS VARCHAR(10))
                        WHEN P.canal = 'delivery'
                            THEN 'Delivery'
                        WHEN P.canal = 'recojo'
                            THEN 'Recojo'
                        ELSE P.canal
                    END AS MesaCanal,

                    P.estado AS EstadoPedido,

                    (
                        ISNULL(DP.TotalPlatos, 0)
                        + ISNULL(DC.TotalCombos, 0)
                        - ISNULL(P.descuento_aplicado, 0)
                        + P.propina
                    ) AS Total,

                    P.fecha_hora AS Fecha

                FROM dbo.PEDIDO P

                LEFT JOIN dbo.MESA M
                    ON P.id_mesa = M.id_mesa

                OUTER APPLY
                (
                    SELECT
                        SUM(
                            cantidad * precio_unitario
                            - descuento
                        ) AS TotalPlatos
                    FROM dbo.DETALLE_PEDIDO_PLATO
                    WHERE id_pedido = P.id_pedido
                ) DP

                OUTER APPLY
                (
                    SELECT
                        SUM(
                            cantidad * precio_unitario
                            - descuento
                        ) AS TotalCombos
                    FROM dbo.DETALLE_PEDIDO_COMBO
                    WHERE id_pedido = P.id_pedido
                ) DC

                WHERE P.id_sucursal = @Sucursal

                -- Desde Gestión de Mesas, mostrar únicamente su pedido.
                AND (@PedidoFiltrado = 0 OR P.id_pedido = @PedidoFiltrado)

                AND P.estado IN
                (
                    'pendiente',
                    'en_preparacion',
                    'listo',
                    'entregado'
                )

                AND NOT EXISTS
                (
                    SELECT 1
                    FROM dbo.COMPROBANTE C
                    WHERE C.id_pedido = P.id_pedido
                )
AND NOT EXISTS
(
    SELECT 1
    FROM dbo.PAGO PG
    WHERE PG.id_pedido = P.id_pedido
)

                AND
                (
                    EXISTS
                    (
                        SELECT 1
                        FROM dbo.DETALLE_PEDIDO_PLATO D
                        WHERE D.id_pedido = P.id_pedido
                    )

                    OR EXISTS
                    (
                        SELECT 1
                        FROM dbo.DETALLE_PEDIDO_COMBO D
                        WHERE D.id_pedido = P.id_pedido
                    )
                )

                ORDER BY P.fecha_hora DESC,
                         P.id_pedido DESC;";

            try
            {
                DataTable datos = new DataTable();

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int
                    ).Value = IdSucursal;

                    // 0 = acceso normal a Pedidos: mostrar todos.
                    // -1 = mesa libre desde Gestión: aún sin pedido.
                    // >0 = mesa ocupada desde Gestión: solo su pedido.
                    int pedidoFiltrado = 0;
                    if (numeroMesaDesdeGestion.HasValue)
                    {
                        pedidoFiltrado = idPedidoSeleccionado.HasValue
                            ? idPedidoSeleccionado.Value : -1;
                    }

                    cmd.Parameters.Add(
                        "@PedidoFiltrado", SqlDbType.Int
                    ).Value = pedidoFiltrado;

                    using (SqlDataAdapter adaptador =
                        new SqlDataAdapter(cmd))
                    {
                        adaptador.Fill(datos);
                    }
                }

                if (dgvListaPedidos.Columns.Count != 6)
                {
                    MessageBox.Show(
                        "dgvPedidos debe tener seis columnas:\n" +
                        "N°, Mesa/Canal, Estado, Total, " +
                        "Fecha y Hora, Ver.");
                    return;
                }

                dgvListaPedidos.DataSource = null;
                dgvListaPedidos.Rows.Clear();

                foreach (DataRow dato in datos.Rows)
                {
                    int fila =
                        dgvListaPedidos.Rows.Add();

                    dgvListaPedidos.Rows[fila]
                        .Cells[0].Value =
                            dato["id_pedido"];

                    dgvListaPedidos.Rows[fila]
                        .Cells[1].Value =
                            dato["MesaCanal"];

                    dgvListaPedidos.Rows[fila]
                        .Cells[2].Value =
                            dato["EstadoPedido"];

                    dgvListaPedidos.Rows[fila]
                        .Cells[3].Value =
                            "S/ " +
                            Convert.ToDecimal(
                                dato["Total"]).ToString("N2");

                    dgvListaPedidos.Rows[fila]
                        .Cells[4].Value =
                            Convert.ToDateTime(
                                dato["Fecha"])
                            .ToString("dd/MM/yyyy HH:mm");

                    dgvListaPedidos.Rows[fila]
                        .Cells[5].Value = "VER";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar pedidos:\n" +
                    ex.Message);
            }
        }

        // ==========================================
        // 29. BOTÓN VER DE TABLA INFERIOR
        // ==========================================

        private void dgvPedidos_CellClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            if (dgvListaPedidos == null)
                return;

            if (e.RowIndex >= 0)
            {
                idPedidoElegidoEnTabla = null;
            }
            // Sexta columna: Ver.
            if (e.RowIndex < 0 || e.ColumnIndex != 5)
                return;

            if (CambiosPendientes())
            {
                DialogResult respuesta =
                    MessageBox.Show(
                        "Hay cambios sin guardar.\n" +
                        "¿Descartarlos y abrir otro pedido?",
                        "Abrir pedido",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                if (respuesta != DialogResult.Yes)
                    return;
            }

            try
            {
                int pedido = Convert.ToInt32(
                    dgvListaPedidos.Rows[e.RowIndex]
                        .Cells[0].Value);

                const string sql = @"
                    SELECT
                        P.canal,
                        P.id_mesa,
                        M.[número] AS NumeroMesa
                    FROM dbo.PEDIDO P
                    LEFT JOIN dbo.MESA M
                        ON P.id_mesa = M.id_mesa
                    WHERE P.id_pedido = @Pedido
                      AND P.id_sucursal = @Sucursal
                      AND P.estado IN
                      (
                        'pendiente',
                        'en_preparacion',
                        'listo',
                        'entregado'
                      )
                      AND NOT EXISTS
                      (
                        SELECT 1
                        FROM dbo.COMPROBANTE C
                        WHERE C.id_pedido = P.id_pedido
                      );";

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd =
                    new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add(
                        "@Pedido", SqlDbType.Int
                    ).Value = pedido;

                    cmd.Parameters.Add(
                        "@Sucursal", SqlDbType.Int
                    ).Value = IdSucursal;

                    conexion.Open();

                    using (SqlDataReader lector =
                        cmd.ExecuteReader())
                    {
                        if (!lector.Read())
                        {
                            MessageBox.Show(
                                "El pedido ya no está disponible.");
                            CargarTablaPedidos();
                            return;
                        }

                        tipoPedidoSeleccionado =
                            lector["canal"].ToString();

                        idMesaSeleccionada =
                            lector["id_mesa"] == DBNull.Value
                            ? (int?)null
                            : Convert.ToInt32(
                                lector["id_mesa"]);

                        numeroMesaSeleccionada =
                            lector["NumeroMesa"] == DBNull.Value
                            ? 0
                            : Convert.ToInt32(
                                lector["NumeroMesa"]);

                        idPedidoSeleccionado = pedido;
                    }

                    CargarDetallePedido(
                        conexion, pedido);

                    idPedidoElegidoEnTabla = pedido;

                }

                EstiloBotonSeleccion(btnTipoSalon,
                    tipoPedidoSeleccionado == "salon");
                EstiloBotonSeleccion(btnTipoDelivery,
                    tipoPedidoSeleccionado == "delivery");
                EstiloBotonSeleccion(btnTipoRecojo,
                    tipoPedidoSeleccionado == "recojo");

                ActualizarBotonCancelar();

                if (tipoPedidoSeleccionado == "salon")
                    CargarMesasDisponibles();
                else
                    DeshabilitarTodasLasMesas();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al abrir pedido:\n" +
                    ex.Message);
            }
        }

        // ==========================================
        // 30. ACTUALIZAR BOTÓN CANCELAR
        // ==========================================




        private void ActualizarBotonCancelar()
        {
            // ==========================================
            // 1. IDENTIFICAR SI SE PUEDE CANCELAR
            // ==========================================

            bool permitirCancelar =
                tipoPedidoSeleccionado == "delivery" ||
                tipoPedidoSeleccionado == "recojo";

            // ==========================================
            // 2. OBTENER EL BOTÓN EXISTENTE
            // ==========================================

            Guna2Button boton = btnCancelar as Guna2Button;

            if (boton == null)
            {
                MessageBox.Show(
                    "No se encontró el botón Cancelar.\n" +
                    "Verifique que su (Name) sea " +
                    "btnCancelarPedido.",
                    "Control no encontrado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // ==========================================
            // 3. ESTADO NORMAL
            // ==========================================

            boton.FillColor = permitirCancelar
                ? Color.Firebrick
                : Color.LightGray;

            boton.ForeColor = permitirCancelar
                ? Color.White
                : Color.DimGray;

            // ==========================================
            // 4. ESTADO DESHABILITADO
            // ==========================================

            boton.DisabledState.FillColor = Color.LightGray;
            boton.DisabledState.ForeColor = Color.DimGray;
            boton.DisabledState.BorderColor = Color.LightGray;

            // ==========================================
            // 5. HABILITAR O DESHABILITAR
            // ==========================================

            boton.Enabled = permitirCancelar;

            boton.Invalidate();
        }




        // ==========================================
        // 31. NUEVO PEDIDO
        // ==========================================


        private void NuevoPedido_Click(object sender, EventArgs e)
        {
            if (modoMesaFija)
            {
                MessageBox.Show(
                    "Esta ventana muestra el pedido de la mesa " +
                    numeroMesaSeleccionada +
                    ". Regrese a Gestión de Mesas para elegir otra.");
                return;
            }

            // 1. Confirmar si existen productos sin guardar.
            if (CambiosPendientes())
            {
                DialogResult respuesta = MessageBox.Show(
                    "Hay productos sin guardar.\n\n" +
                    "¿Desea descartarlos e iniciar otro pedido?",
                    "Nuevo pedido",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (respuesta != DialogResult.Yes)
                    return;
            }

            // 2. Limpiar carrito, pedido, mesa e importes.
            LimpiarSeleccion();

            // 3. Restablecer la modalidad.
            tipoPedidoSeleccionado = "";

            EstiloBotonSeleccion(btnTipoSalon, false);
            EstiloBotonSeleccion(btnTipoDelivery, false);
            EstiloBotonSeleccion(btnTipoRecojo, false);

            // 4. Deshabilitar todas las mesas.
            DeshabilitarTodasLasMesas();

            // 5. Limpiar el buscador y mostrar
            // nuevamente los 19 productos.
            if (reiniciarFiltroProductos != null)
            {
                reiniciarFiltroProductos();
            }

            // 6. Cancelar apagado.
            // Guardar y Generar Boleta disponibles.
            ActualizarBotonCancelar();

            // 7. Mostrar importes en cero.
            ActualizarPagos();

            // 8. Actualizar pedidos guardados.
            CargarTablaPedidos();
        }

        // ==========================================
        // 32. LIMPIAR PEDIDO
        // ==========================================

        private void LimpiarPedido_Click(
            object sender, EventArgs e)
        {
            if (idPedidoSeleccionado.HasValue)
            {
                // No borrar productos guardados.
                if (!CambiosPendientes())
                {
                    MessageBox.Show(
                        "El pedido ya está guardado.");
                    return;
                }

                DialogResult respuesta =
                    MessageBox.Show(
                        "¿Descartar productos " +
                        "aún no guardados?",
                        "Limpiar",
                        MessageBoxButtons.YesNo);

                if (respuesta != DialogResult.Yes)
                    return;

                try
                {
                    using (SqlConnection conexion =
                        new SqlConnection(cadenaConexion))
                    {
                        conexion.Open();

                        CargarDetallePedido(
                            conexion,
                            idPedidoSeleccionado.Value);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Error al recargar pedido:\n" +
                        ex.Message);
                }

                return;
            }

            carrito.Clear();
            MostrarCarrito();
        }

        // ==========================================
        // 33. CANCELAR DELIVERY O RECOJO
        // ==========================================

        private void CancelarPedido_Click(object sender, EventArgs e)
        {


            if (tipoPedidoSeleccionado != "delivery" &&
                tipoPedidoSeleccionado != "recojo")
            {
                MessageBox.Show(
                    "Solo se pueden cancelar pedidos Delivery o Recojo.",
                    "Cancelar pedido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Pedido que todavía no se ha guardado.
            if (!idPedidoSeleccionado.HasValue)
            {
                if (carrito.Count == 0)
                    return;

                DialogResult respuestaTemporal = MessageBox.Show(
                    "¿Descartar los productos sin guardar?",
                    "Cancelar pedido",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (respuestaTemporal == DialogResult.Yes)
                {
                    carrito.Clear();
                    MostrarCarrito();
                }

                return;
            }

            int idPedido = idPedidoSeleccionado.Value;

            DialogResult respuesta = MessageBox.Show(
                "¿Desea cancelar el pedido N.º " + idPedido + "?",
                "Confirmar cancelación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (respuesta != DialogResult.Yes)
                return;

            try
            {
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    using (SqlTransaction transaccion =
                        conexion.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ==========================================
                            // 1. VERIFICAR EL ESTADO REAL DEL PEDIDO
                            // ==========================================

                            const string consultar = @"
                        SELECT
                            P.canal,
                            P.estado,
                            P.id_sucursal,

                            (SELECT COUNT(*)
                             FROM dbo.PAGO PG
                             WHERE PG.id_pedido = P.id_pedido)
                                AS Pagos,

                            (SELECT COUNT(*)
                             FROM dbo.COMPROBANTE C
                             WHERE C.id_pedido = P.id_pedido)
                                AS Comprobantes

                        FROM dbo.PEDIDO P WITH (UPDLOCK, HOLDLOCK)
                        WHERE P.id_pedido = @Pedido;";

                            string canalBD;
                            string estadoBD;
                            int sucursalBD;
                            int pagos;
                            int comprobantes;

                            using (SqlCommand cmd =
                                new SqlCommand(consultar, conexion, transaccion))
                            {
                                cmd.Parameters.Add(
                                    "@Pedido", SqlDbType.Int
                                ).Value = idPedido;

                                using (SqlDataReader lector =
                                    cmd.ExecuteReader())
                                {
                                    if (!lector.Read())
                                    {
                                        throw new Exception(
                                            "El pedido ya no existe en SQL Server.");
                                    }

                                    canalBD = Convert.ToString(
                                        lector["canal"]
                                    ).Trim().ToLowerInvariant();

                                    estadoBD = Convert.ToString(
                                        lector["estado"]
                                    ).Trim().ToLowerInvariant();

                                    sucursalBD = Convert.ToInt32(
                                        lector["id_sucursal"]);

                                    pagos = Convert.ToInt32(
                                        lector["Pagos"]);

                                    comprobantes = Convert.ToInt32(
                                        lector["Comprobantes"]);
                                }
                            }

                            // ==========================================
                            // 2. VALIDACIONES
                            // ==========================================

                            if (sucursalBD != IdSucursal)
                            {
                                throw new Exception(
                                    "El pedido pertenece a otra sucursal.");
                            }

                            if (canalBD != tipoPedidoSeleccionado)
                            {
                                throw new Exception(
                                    "La modalidad del pedido no coincide. " +
                                    "Modalidad registrada: " + canalBD);
                            }

                            if (comprobantes > 0)
                            {
                                throw new Exception(
                                    "Este pedido ya tiene un comprobante " +
                                    "emitido. No puede cancelarse directamente.");
                            }

                            if (pagos > 0)
                            {
                                throw new Exception(
                                    "Este pedido ya registra un pago. " +
                                    "Primero debe gestionarse la anulación " +
                                    "del pago correspondiente.");
                            }

                            if (estadoBD != "pendiente" &&
                                estadoBD != "en_preparacion" &&
                                estadoBD != "listo")
                            {
                                throw new Exception(
                                    "El pedido está en estado '" +
                                    estadoBD + "' y no admite cancelación.");
                            }

                            // ==========================================
                            // 3. CANCELAR EL PEDIDO EN SQL SERVER
                            // ==========================================

                            const string sqlCancelar = @"
                        DECLARE @Actualizados TABLE
                        (
                            id_pedido INT NOT NULL
                        );

                        UPDATE dbo.PEDIDO
                        SET estado = 'cancelado'

                        OUTPUT INSERTED.id_pedido
                            INTO @Actualizados(id_pedido)

                        WHERE id_pedido = @Pedido
                          AND id_sucursal = @Sucursal
                          AND canal = @Canal

                          AND estado IN
                          (
                              'pendiente',
                              'en_preparacion',
                              'listo'
                          )

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
                          );

                        SELECT COUNT(*)
                        FROM @Actualizados;";

                            using (SqlCommand cmd =
                                new SqlCommand(
                                    sqlCancelar, conexion, transaccion))
                            {
                                cmd.Parameters.Add(
                                    "@Pedido", SqlDbType.Int
                                ).Value = idPedido;

                                cmd.Parameters.Add(
                                    "@Sucursal", SqlDbType.Int
                                ).Value = IdSucursal;

                                cmd.Parameters.Add(
                                    "@Canal", SqlDbType.VarChar, 20
                                ).Value = tipoPedidoSeleccionado;

                                int cantidad = Convert.ToInt32(
                                    cmd.ExecuteScalar());

                                if (cantidad != 1)
                                {
                                    throw new Exception(
                                        "No se actualizó el pedido. " +
                                        "Sus datos pudieron cambiar. " +
                                        "Actualice la lista.");
                                }
                            }

                            // ==========================================
                            // 4. VERIFICAR EL ESTADO FINAL
                            // ==========================================

                            using (SqlCommand cmd =
                                new SqlCommand(@"
                            SELECT COUNT(*)
                            FROM dbo.PEDIDO
                            WHERE id_pedido = @Pedido
                              AND estado = 'cancelado';",
                                    conexion, transaccion))
                            {
                                cmd.Parameters.Add(
                                    "@Pedido", SqlDbType.Int
                                ).Value = idPedido;

                                if (Convert.ToInt32(
                                    cmd.ExecuteScalar()) != 1)
                                {
                                    throw new Exception(
                                        "SQL Server no dejó el pedido " +
                                        "en estado cancelado. " +
                                        "Revise los triggers de dbo.PEDIDO.");
                                }
                            }

                            transaccion.Commit();
                        }
                        catch
                        {
                            try
                            {
                                transaccion.Rollback();
                            }
                            catch (Exception)
                            {
                                // Un trigger puede haber anulado
                                // la transacción previamente.
                            }

                            throw;
                        }
                    }
                }

                // ==========================================
                // 5. ACTUALIZAR LA INTERFAZ
                // ==========================================

                LimpiarSeleccion();
                CargarTablaPedidos();
                ActualizarBotonCancelar();

                MessageBox.Show(
                    "Pedido N.º " + idPedido +
                    " cancelado correctamente.",
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo cancelar el pedido N.º " +
                    idPedido + ":\n" + ex.Message,
                    "Gestión de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // ==========================================
        // 34. GENERAR BOLETA
        // ==========================================


        private void BUT_GENERARBOLETA_Click(
            object sender, EventArgs e)
        {
            // ======================================
            // 1. VALIDAR PEDIDO SELECCIONADO
            // ======================================

            if (!idPedidoElegidoEnTabla.HasValue ||
                !idPedidoSeleccionado.HasValue ||
                idPedidoElegidoEnTabla.Value !=
                idPedidoSeleccionado.Value)
            {
                MessageBox.Show(
                    "Seleccione primero un pedido " +
                    "de la tabla presionando VER.",
                    "Generar Boleta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // ======================================
            // 2. VERIFICAR CAMBIOS SIN GUARDAR
            // ======================================

            if (CambiosPendientes())
            {
                MessageBox.Show(
                    "Guarde los productos nuevos " +
                    "antes de generar la boleta.",
                    "Generar Boleta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            int pedido = idPedidoElegidoEnTabla.Value;

            try
            {
                // ==================================
                // 3. COMPROBAR EL PEDIDO EN SQL
                // ==================================

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    if (TieneComprobante(
                        conexion, null, pedido))
                    {
                        MessageBox.Show(
                            "Este pedido ya tiene comprobante.",
                            "Generar Boleta",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        return;
                    }
                }

                // ==================================
                // 4. ABRIR GENERAR BOLETA
                // ==================================

                using (Generar_Boleta ventana =
                    new Generar_Boleta())
                {
                    // Enviar el ID del pedido.
                    ventana.Tag = pedido;

                    // Mostrar la ventana.
                    ventana.ShowDialog(this);
                }

                // ==================================
                // 5. CONSULTAR ESTADO ACTUALIZADO
                // ==================================

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                {
                    conexion.Open();

                    string estadoActual = "";

                    string sql = @"
                SELECT estado
                FROM dbo.PEDIDO
                WHERE id_pedido = @Pedido
                  AND id_sucursal = @Sucursal;";

                    using (SqlCommand cmd =
                        new SqlCommand(sql, conexion))
                    {
                        cmd.Parameters.Add(
                            "@Pedido", SqlDbType.Int).Value =
                            pedido;

                        cmd.Parameters.Add(
                            "@Sucursal", SqlDbType.Int).Value =
                            IdSucursal;

                        object resultado = cmd.ExecuteScalar();

                        if (resultado != null)
                        {
                            estadoActual = resultado.ToString()
                                .Trim()
                                .ToLower();
                        }
                    }

                    // ==================================
                    // 6. SI ESTÁ COBRADO, LIMPIAR
                    // ==================================

                    if (estadoActual == "cobrado" ||
                        estadoActual == "cancelado" ||
                        estadoActual == "" ||
                        TieneComprobante(
                            conexion, null, pedido))
                    {
                        LimpiarSeleccion();
                    }
                    else
                    {
                        // El pedido sigue pendiente.
                        // Recuperar sus productos.
                        CargarDetallePedido(
                            conexion, pedido);
                    }
                }

                // ==================================
                // 7. ACTUALIZAR TABLA INFERIOR
                // ==================================

                CargarTablaPedidos();

                // ==================================
                // 8. ACTUALIZAR MESAS
                // ==================================

                if (tipoPedidoSeleccionado == "salon")
                {
                    CargarMesasDisponibles();
                }

                ActualizarBotonCancelar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al generar boleta:\n" +
                    ex.Message,
                    "Gestión de Pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // ==========================================
        // 35. RELOJ Y FECHA
        // ==========================================

        private void ActualizarReloj()
        {
            DateTime ahora = DateTime.Now;

            lblHora.Text =
                ahora.ToString("HH:mm:ss");

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

        private void timerReloj_Tick(
            object sender, EventArgs e)
        {
            ActualizarReloj();
        }

        // ==========================================
        // 36. CERRAR SISTEMA
        // ==========================================

        private void but_Cerrar_Click(
            object sender, EventArgs e)
        {
            Application.Exit();
        }

        // ==========================================
        // 37. NAVEGACIÓN ENTRE FORMULARIOS
        // ==========================================

        // PEDIDOS
        private void guna2Button2_Click(
            object sender, EventArgs e)
        {
            BringToFront();
        }

        // MESAS
        private void guna2Button1_Click(
            object sender, EventArgs e)
        {
            new FRM_MESAS().Show();
            Hide();
        }

        // COCINA
        private void guna2Button5_Click(
            object sender, EventArgs e)
        {
            new FRM_COCINA().Show();
            Hide();
        }

        // EMPLEADOS
        private void guna2Button3_Click(
            object sender, EventArgs e)
        {
            new FRM_EMPLEADOS().Show();
            Hide();
        }

        // MENÚ
        private void guna2Button9_Click(
            object sender, EventArgs e)
        {
            new FRM_MENU().Show();
            Hide();
        }

        // INVENTARIO
        private void guna2Button8_Click(
            object sender, EventArgs e)
        {
            new FRM_INVENTARIO().Show();
            Hide();
        }

        // PROMOCIONES
        private void guna2Button7_Click(
            object sender, EventArgs e)
        {
            new FRM_PROMOCIONES().Show();
            Hide();
        }

        // REPORTES
        private void guna2Button6_Click(
            object sender, EventArgs e)
        {
            new FRM_REPORTES().Show();
            Hide();
        }
        private void ConfigurarColumnaEliminar()
        {
            DataGridView dgv = dgvDetallePedido;

            dgv.AllowUserToAddRows = false;
            dgv.RowHeadersVisible = false;

            // Conservar las primeras cuatro columnas.
            if (dgv.Columns.Count < 5)
                return;

            // Si Eliminar ya es un botón,
            // no necesitamos reemplazarlo.
            if (!(dgv.Columns[4] is DataGridViewButtonColumn))
            {
                int ancho = dgv.Columns[4].Width;

                // Eliminar solamente la quinta columna.
                dgv.Columns.RemoveAt(4);

                DataGridViewButtonColumn columna =
                    new DataGridViewButtonColumn();

                columna.Name = "colEliminar";
                columna.HeaderText = "Eliminar";
                columna.Width = ancho;

                dgv.Columns.Insert(4, columna);
            }

            DataGridViewButtonColumn boton =
                (DataGridViewButtonColumn)dgv.Columns[4];

            boton.Text = "X";
            boton.UseColumnTextForButtonValue = true;

            boton.DefaultCellStyle.BackColor = Color.White;
            boton.DefaultCellStyle.ForeColor = Color.Firebrick;
            boton.DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;
        }



        private void ConfigurarFiltroProductos()
        {
            // ==========================================
            // 1. BUSCAR LOS CONTROLES DEL DISEÑADOR
            // ==========================================

            Control[] buscar =
                this.Controls.Find("txtBuscarProducto", true);

            Control[] platos =
                this.Controls.Find("btnCategoriaPlatos", true);

            Control[] agregados =
                this.Controls.Find("btnCategoriaAgregados", true);

            Control[] bebidas =
                this.Controls.Find("btnCategoriaBebidas", true);

            // ==========================================
            // 2. COMPROBAR CONTROLES FALTANTES
            // ==========================================

            List<string> faltantes = new List<string>();

            if (buscar.Length == 0)
                faltantes.Add("txtBuscarProducto");

            if (platos.Length == 0)
                faltantes.Add("btnCategoriaPlatos");

            if (agregados.Length == 0)
                faltantes.Add("btnCategoriaAgregados");

            if (bebidas.Length == 0)
                faltantes.Add("btnCategoriaBebidas");

            if (faltantes.Count > 0)
            {
                MessageBox.Show(
                    "No se encontraron estos controles:\n\n" +
                    string.Join("\n", faltantes) +
                    "\n\nRevise la propiedad (Name) " +
                    "en FRMS_PEDIDOS [Diseño].",
                    "Configuración del filtro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // Referencias locales.
            // Evitan duplicar nombres del Designer.
            Control txtBuscar = buscar[0];
            Control btnPlatos = platos[0];
            Control btnAgregados = agregados[0];
            Control btnBebidas = bebidas[0];

            // ==========================================
            // 3. IDENTIFICAR LOS 19 PRODUCTOS
            // ==========================================

            // Formato:
            // Nombre del botón +, nombre, categoría.

            string[,] productos =
            {
        // ==============================
        // PLATOS
        // ==============================

        {
            "btnAgregarBBQ5",
            "Alitas BBQ 5 alitas",
            "platos"
        },

        {
            "btnAgregarAcevichadas5",
            "Alitas Acevichadas 5 alitas",
            "platos"
        },

        {
            "btnAgregarBBQ4",
            "Alitas BBQ 4 alitas",
            "platos"
        },

        {
            "btnAgregarAcevichadas4",
            "Alitas Acevichadas 4 alitas",
            "platos"
        },

        {
            "btnAgregarSalchialitas",
            "Salchialitas",
            "platos"
        },

        {
            "btnAgregarLomoCarne",
            "Lomo de carne",
            "platos"
        },

        {
            "btnAgregarLomoPollo",
            "Lomo de pollo",
            "platos"
        },

        {
            "btnAgregarMostrito",
            "Mostrito",
            "platos"
        },

        {
            "btnAgregarMilanesaPollo",
            "Milanesa de pollo",
            "platos"
        },

        {
            "btnAgregarTallarinVerde",
            "Tallarín verde",
            "platos"
        },

        {
            "btnAgregarTallarinSaltado",
            "Tallarín saltado",
            "platos"
        },

        // ==============================
        // AGREGADOS
        // ==============================

        {
            "btnAgregarHuevo",
            "Huevo",
            "agregados"
        },

        {
            "btnAgregarPlatano",
            "Plátano",
            "agregados"
        },

        // ==============================
        // BEBIDAS
        // ==============================

        {
            "btnAgregarMaracuya1L",
            "Jarra de maracuyá 1 litro",
            "bebidas"
        },

        {
            "btnAgregarMaracuyaMedioL",
            "Jarra de maracuyá medio litro",
            "bebidas"
        },

        {
            "btnAgregarCocaCola",
            "Coca-Cola personal",
            "bebidas"
        },

        {
            "btnAgregarIncaKola",
            "Inca Kola personal",
            "bebidas"
        },

        {
            "btnAgregarSprite",
            "Sprite personal",
            "bebidas"
        },

        {
            "btnAgregarFanta",
            "Fanta personal",
            "bebidas"
        }
    };

            // ==========================================
            // 4. GUARDAR LAS TARJETAS EXISTENTES
            // ==========================================

            List<Control> tarjetas =
                new List<Control>();

            Dictionary<Control, string> nombres =
                new Dictionary<Control, string>();

            Dictionary<Control, string> categorias =
                new Dictionary<Control, string>();

            Dictionary<Control, Point> posiciones =
                new Dictionary<Control, Point>();

            int cantidadProductos = productos.GetLength(0);

            for (int i = 0; i < cantidadProductos; i++)
            {
                string nombreBoton = productos[i, 0];

                Control[] encontrado =
                    this.Controls.Find(nombreBoton, true);

                if (encontrado.Length == 0)
                    continue;

                Control botonAgregar = encontrado[0];

                // Identificar el panel de la tarjeta.
                // En tu diseño actual el botón +
                // está dentro de su tarjeta.
                Control tarjeta = botonAgregar.Parent;

                if (tarjeta == null)
                    continue;

                // Evitar tarjetas repetidas.
                if (tarjetas.Contains(tarjeta))
                    continue;

                tarjetas.Add(tarjeta);

                // Guardar información del producto.
                nombres[tarjeta] = productos[i, 1];

                categorias[tarjeta] = productos[i, 2];

                // Guardar posición inicial de la tarjeta.
                posiciones[tarjeta] = tarjeta.Location;
            }

            // ==========================================
            // 5. VALIDAR LAS TARJETAS
            // ==========================================

            if (tarjetas.Count == 0)
            {
                MessageBox.Show(
                    "No se encontraron las tarjetas.\n" +
                    "Revise los nombres de los botones +.",
                    "Filtro de productos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // ==========================================
            // 6. NORMALIZAR TEXTO DE BÚSQUEDA
            // ==========================================

            // Permite buscar con o sin tildes.
            Func<string, string> normalizar = texto =>
            {
                return (texto ?? "")
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("á", "a")
                    .Replace("é", "e")
                    .Replace("í", "i")
                    .Replace("ó", "o")
                    .Replace("ú", "u")
                    .Replace("ü", "u");
            };

            // ==========================================
            // 7. CATEGORÍA SELECCIONADA
            // ==========================================

            // Al iniciar, mostrar todos.
            string categoriaActual = "todos";

            // ==========================================
            // 8. FUNCIÓN GENERAL PARA FILTRAR
            // ==========================================

            // Primero declaramos la variable y
            // después le asignamos la función.
            // Así evitamos el error CS0165.

            Action aplicarFiltro = null;

            aplicarFiltro = () =>
            {
                string textoBusqueda =
                    normalizar(txtBuscar.Text);

                // ======================================
                // 8.1. MOSTRAR U OCULTAR TARJETAS
                // ======================================

                foreach (Control tarjeta in tarjetas)
                {
                    string nombre =
                        normalizar(nombres[tarjeta]);

                    string categoria =
                        categorias[tarjeta];

                    // Buscar por nombre del producto.
                    bool coincideNombre =
                        string.IsNullOrEmpty(textoBusqueda) ||
                        nombre.Contains(textoBusqueda);

                    // Buscar por categoría.
                    bool coincideCategoria =
                        categoriaActual == "todos" ||
                        categoriaActual == categoria;

                    // Mostrar la tarjeta completa.
                    tarjeta.Visible =
                        coincideNombre && coincideCategoria;
                }

                // ======================================
                // 8.2. REORGANIZAR TARJETAS VISIBLES
                // ======================================

                var grupos =
                    tarjetas.GroupBy(t => t.Parent);

                foreach (var grupo in grupos)
                {
                    Control contenedor = grupo.Key;

                    if (contenedor == null)
                        continue;

                    // FlowLayoutPanel reorganiza
                    // automáticamente sus controles.
                    if (contenedor is FlowLayoutPanel)
                        continue;

                    List<Control> ordenadas =
                        grupo.OrderBy(
                            t => posiciones[t].X).ToList();

                    if (ordenadas.Count == 0)
                        continue;

                    // Recuperar la posición inicial.
                    int inicioX =
                        ordenadas.Min(t => posiciones[t].X);

                    int siguienteX = inicioX;

                    ScrollableControl panel =
                        contenedor as ScrollableControl;

                    // Reiniciar desplazamiento horizontal.
                    if (panel != null && panel.AutoScroll)
                    {
                        panel.AutoScrollPosition = Point.Empty;
                    }

                    // Colocar las tarjetas visibles
                    // una al lado de la otra.
                    foreach (Control tarjeta in ordenadas)
                    {
                        if (!tarjeta.Visible)
                            continue;

                        tarjeta.Location = new Point(
                            siguienteX,
                            posiciones[tarjeta].Y);

                        siguienteX += tarjeta.Width + 6;
                    }

                    // Actualizar desplazamiento del panel.
                    if (panel != null && panel.AutoScroll)
                    {
                        panel.AutoScrollMinSize = new Size(
                            siguienteX + 10,
                            0);

                        panel.AutoScrollPosition = Point.Empty;
                    }
                }

                // ======================================
                // 8.3. RESALTAR CATEGORÍA SELECCIONADA
                // ======================================

                Guna2Button botonPlatos =
                    btnPlatos as Guna2Button;

                Guna2Button botonAgregados =
                    btnAgregados as Guna2Button;

                Guna2Button botonBebidas =
                    btnBebidas as Guna2Button;

                EstiloCategoria(botonPlatos, categoriaActual == "platos");
                EstiloCategoria(botonAgregados, categoriaActual == "agregados");
                EstiloCategoria(botonBebidas, categoriaActual == "bebidas");
            };

            // ==========================================
            // 9. REINICIAR EL FILTRO DESDE NUEVO PEDIDO
            // ==========================================

            // IMPORTANTE:
            // Este bloque debe quedar DESPUÉS de
            // la función completa aplicarFiltro.

            reiniciarFiltroProductos = () =>
            {
                // Restablecer todas las categorías.
                categoriaActual = "todos";

                // Limpiar el buscador.
                txtBuscar.Text = "";

                // Mostrar nuevamente todos los productos.
                aplicarFiltro();
            };

            // ==========================================
            // 10. BOTÓN PLATOS
            // ==========================================

            btnPlatos.Click += (sender, e) =>
            {
                // Pulsar nuevamente muestra todos.
                categoriaActual =
                    categoriaActual == "platos"
                    ? "todos"
                    : "platos";

                aplicarFiltro();
            };

            // ==========================================
            // 11. BOTÓN AGREGADOS
            // ==========================================

            btnAgregados.Click += (sender, e) =>
            {
                categoriaActual =
                    categoriaActual == "agregados"
                    ? "todos"
                    : "agregados";

                aplicarFiltro();
            };

            // ==========================================
            // 12. BOTÓN BEBIDAS
            // ==========================================

            btnBebidas.Click += (sender, e) =>
            {
                categoriaActual =
                    categoriaActual == "bebidas"
                    ? "todos"
                    : "bebidas";

                aplicarFiltro();
            };

            // ==========================================
            // 13. BUSCADOR EN TIEMPO REAL
            // ==========================================

            txtBuscar.TextChanged += (sender, e) =>
            {
                aplicarFiltro();
            };

            // ==========================================
            // 14. MOSTRAR TODOS LOS PRODUCTOS AL INICIAR
            // ==========================================

            aplicarFiltro();
        }




        private void ActualizarPagos()
        {
            // ==========================================
            // 1. CALCULAR SUBTOTAL DE LOS PRODUCTOS
            // ==========================================

            decimal subtotal = 0;

            foreach (var producto in carrito)
            {
                subtotal += producto.Subtotal;
            }

            // ==========================================
            // 2. APLICAR DESCUENTO
            // ==========================================

            decimal descuento = descuentoGeneral;

            decimal totalProductos =
                Math.Max(0, subtotal - descuento);

            // ==========================================
            // 3. CALCULAR IGV INCLUIDO (18 %)
            // ==========================================

            decimal igv = Math.Round(
                totalProductos * 18m / 118m,
                2);

            // ==========================================
            // 4. CALCULAR TOTAL A PAGAR
            // ==========================================

            decimal total =
                totalProductos + propinaActual;

            // ==========================================
            // 5. MOSTRAR RESULTADOS EN EL FORMULARIO
            // ==========================================

            MostrarMonto("lblSubtotal", subtotal);

            MostrarMonto("lblDescuento", descuento);

            MostrarMonto("lblIGV", igv);

            MostrarMonto("lblTotalPagar", total);
        }

        private void but_nuevopedido_Click(object sender, EventArgs e)
        {
            NuevoPedido_Click(sender, e);
        }

        private void BUT_GUARDAR_Click(object sender, EventArgs e)
        {
            btnGuardarPedido_Click(sender, e);
        }

        private void EstiloCategoria(Guna2Button boton, bool seleccionado)
        {
            if (boton == null) return;
            boton.FillColor = Color.Gold;
            boton.ForeColor = Color.Black;
            boton.BorderRadius = 8;
            boton.BorderThickness = seleccionado ? 3 : 1;
            boton.BorderColor = seleccionado
                ? Color.OrangeRed : Color.LightGray;
        }

        private void EstiloBotonSeleccion(Guna2Button boton, bool seleccionado)
        {
            if (boton == null) return;
            boton.FillColor = Color.Gold;
            boton.ForeColor = Color.Black;
            boton.BorderRadius = 8;
            boton.BorderThickness = seleccionado ? 3 : 1;
            boton.BorderColor = seleccionado
                ? Color.Goldenrod : Color.LightGray;
        }

        private void btnLimpiarPedido_Click(object sender, EventArgs e)
        {
            if (modoMesaFija)
                return;



            // ==========================================
            // BOTÓN LIMPIAR - VENTA DE PEDIDOS
            // RESTAURANTE AL ESTILO PIURA
            // ==========================================

            // 1. Comprobar si hay algo que limpiar.
            bool hayProductos = carrito.Count > 0;
            bool hayPedidoSeleccionado =
                idPedidoSeleccionado.HasValue;

            if (!hayProductos && !hayPedidoSeleccionado)
            {
                MessageBox.Show(
                    "No hay productos para limpiar.",
                    "Limpiar pedido",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // ==========================================
            // 2. CONFIRMAR SI HAY CAMBIOS SIN GUARDAR
            // ==========================================

            if (CambiosPendientes())
            {
                DialogResult respuesta = MessageBox.Show(
                    "Existen productos que todavía no " +
                    "se han guardado.\n\n" +
                    "¿Desea descartarlos y limpiar la pantalla?",
                    "Confirmar limpieza",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (respuesta != DialogResult.Yes)
                    return;
            }

            // ==========================================
            // 3. LIMPIAR SOLO LA INTERFAZ
            // ==========================================

            // Este método limpia el carrito, pedido,
            // mesa seleccionada y montos.
            // NO elimina registros de SQL Server.

            LimpiarSeleccion();

            // ==========================================
            // 4. RESTABLECER BUSCADOR Y CATEGORÍAS
            // ==========================================

            if (reiniciarFiltroProductos != null)
            {
                reiniciarFiltroProductos();
            }

            // ==========================================
            // 5. RESTABLECER LAS MESAS
            // ==========================================

            if (tipoPedidoSeleccionado == "salon")
            {
                // Mantener la modalidad Salón.
                // Actualizar estados reales de las mesas.
                CargarMesasDisponibles();
            }
            else
            {
                // Delivery y Recojo no utilizan mesas.
                DeshabilitarTodasLasMesas();
            }

            // ==========================================
            // 6. ACTUALIZAR IMPORTES Y BOTONES
            // ==========================================

            ActualizarPagos();
            ActualizarBotonCancelar();

            // ==========================================
            // 7. ACTUALIZAR TABLA INFERIOR
            // ==========================================

            CargarTablaPedidos();

            MessageBox.Show(
                "La pantalla de pedidos se limpió " +
                "correctamente.\n\n" +
                "Los pedidos guardados en SQL Server " +
                "se conservaron.",
                "Gestión de pedidos",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void dgvDetallePedido_CellContentClick(
            object sender,
            DataGridViewCellEventArgs e)
        {
            // Verificar que se pulsó una fila y la columna X.
            if (e.RowIndex < 0 || e.ColumnIndex != 4)
                return;

            if (e.RowIndex >= dgvDetallePedido.Rows.Count)
                return;

            // Identificar el producto seleccionado.
            string referencia = Convert.ToString(
                dgvDetallePedido.Rows[e.RowIndex].Tag);

            LineaPedido producto = carrito.FirstOrDefault(p =>
                p.Tipo + ":" + p.IdProducto == referencia);

            if (producto == null)
                return;

            // ========================================
            // CASO 1: PRODUCTO TODAVÍA NO GUARDADO
            // ========================================

            if (producto.CantidadGuardada == 0)
            {
                carrito.Remove(producto);
                MostrarCarrito();

                MessageBox.Show(
                    "Producto eliminado del carrito.",
                    "Venta de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // ========================================
            // CASO 2: PRODUCTO GUARDADO EN SQL SERVER
            // ========================================

            if (!idPedidoSeleccionado.HasValue)
            {
                MessageBox.Show(
                    "No se encontró el pedido guardado.",
                    "Eliminar producto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            DialogResult respuesta = MessageBox.Show(
                "¿Desea eliminar el producto:\n\n" +
                producto.Nombre + "\n\n" +
                "Se eliminará del pedido registrado en SQL Server.",
                "Confirmar eliminación",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (respuesta != DialogResult.Yes)
                return;

            try
            {
                EliminarProductoGuardado(
                    idPedidoSeleccionado.Value,
                    producto.Tipo,
                    producto.IdProducto);

                // Solo quitarlo del carrito tras confirmar SQL.
                carrito.Remove(producto);

                MostrarCarrito();
                CargarTablaPedidos();

                MessageBox.Show(
                    "Producto eliminado correctamente.",
                    "Venta de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo eliminar el producto:\n" +
                    ex.Message,
                    "Venta de pedidos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        private void EliminarProductoGuardado(
            int idPedido,
            string tipo,
            int idProducto)
        {
            if (tipo != "plato" && tipo != "combo")
                throw new Exception("Tipo de producto no válido.");

            string tabla = tipo == "plato"
                ? "dbo.DETALLE_PEDIDO_PLATO"
                : "dbo.DETALLE_PEDIDO_COMBO";

            string campo = tipo == "plato"
                ? "id_plato"
                : "id_combo";

            using (SqlConnection conexion =
                new SqlConnection(cadenaConexion))
            {
                conexion.Open();

                using (SqlTransaction transaccion =
                    conexion.BeginTransaction(
                        IsolationLevel.Serializable))
                {
                    try
                    {
                        // ======================================
                        // 1. COMPROBAR PEDIDO
                        // ======================================

                        const string consultar = @"
                    SELECT
                        P.estado,

                        (SELECT COUNT(*)
                         FROM dbo.PAGO PG
                         WHERE PG.id_pedido = P.id_pedido)
                            AS Pagos,

                        (SELECT COUNT(*)
                         FROM dbo.COMPROBANTE C
                         WHERE C.id_pedido = P.id_pedido)
                            AS Comprobantes

                    FROM dbo.PEDIDO P
                        WITH (UPDLOCK, HOLDLOCK)

                    WHERE P.id_pedido = @Pedido
                      AND P.id_sucursal = @Sucursal
                      AND P.canal = @Canal;";

                        string estado;
                        int pagos;
                        int comprobantes;

                        using (SqlCommand cmd =
                            new SqlCommand(
                                consultar, conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int
                            ).Value = idPedido;

                            cmd.Parameters.Add(
                                "@Sucursal", SqlDbType.Int
                            ).Value = IdSucursal;

                            cmd.Parameters.Add(
                                "@Canal", SqlDbType.VarChar, 20
                            ).Value = tipoPedidoSeleccionado;

                            using (SqlDataReader lector =
                                cmd.ExecuteReader())
                            {
                                if (!lector.Read())
                                {
                                    throw new Exception(
                                        "El pedido no existe o no coincide " +
                                        "con la modalidad seleccionada.");
                                }

                                estado = Convert.ToString(
                                    lector["estado"]
                                ).Trim().ToLowerInvariant();

                                pagos = Convert.ToInt32(
                                    lector["Pagos"]);

                                comprobantes = Convert.ToInt32(
                                    lector["Comprobantes"]);
                            }
                        }

                        if (pagos > 0 || comprobantes > 0)
                        {
                            throw new Exception(
                                "El pedido ya registra un pago o " +
                                "comprobante. No puede modificarse.");
                        }

                        // Evitar cambiar pedidos que ya entraron
                        // a preparación, entrega o cierre.
                        if (estado != "pendiente")
                        {
                            throw new Exception(
                                "Solo se pueden eliminar productos " +
                                "de pedidos en estado pendiente. " +
                                "Estado actual: " + estado);
                        }

                        // ======================================
                        // 2. EVITAR DEJAR UN PEDIDO VACÍO
                        // ======================================

                        const string contar = @"
                    SELECT
                        (SELECT COUNT(*)
                         FROM dbo.DETALLE_PEDIDO_PLATO
                         WHERE id_pedido = @Pedido)
                        +
                        (SELECT COUNT(*)
                         FROM dbo.DETALLE_PEDIDO_COMBO
                         WHERE id_pedido = @Pedido);";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                contar, conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int
                            ).Value = idPedido;

                            int lineas = Convert.ToInt32(
                                cmd.ExecuteScalar());

                            if (lineas <= 1)
                            {
                                throw new Exception(
                                    "No se puede eliminar el último " +
                                    "producto guardado.\n" +
                                    "Primero debe cancelarse o gestionarse " +
                                    "el pedido completo.");
                            }
                        }

                        // ======================================
                        // 3. ELIMINAR EL PRODUCTO GUARDADO
                        // ======================================

                        string sqlEliminar = @"
                    DECLARE @Eliminados TABLE
                    (
                        id_producto INT
                    );

                    DELETE FROM " + tabla + @"

                    OUTPUT DELETED." + campo + @"
                    INTO @Eliminados(id_producto)

                    WHERE id_pedido = @Pedido
                      AND " + campo + @" = @Producto;

                    SELECT COUNT(*)
                    FROM @Eliminados;";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                sqlEliminar, conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int
                            ).Value = idPedido;

                            cmd.Parameters.Add(
                                "@Producto", SqlDbType.Int
                            ).Value = idProducto;

                            int eliminados = Convert.ToInt32(
                                cmd.ExecuteScalar());

                            if (eliminados != 1)
                            {
                                throw new Exception(
                                    "No se pudo eliminar exactamente " +
                                    "una línea del pedido.");
                            }
                        }

                        // ======================================
                        // 4. CONFIRMAR CAMBIOS
                        // ======================================

                        transaccion.Commit();
                    }
                    catch
                    {
                        try
                        {
                            transaccion.Rollback();
                        }
                        catch
                        {
                            // Es posible que un trigger
                            // ya haya anulado la transacción.
                        }

                        throw;
                    }
                }
            }
        }

        private void But_generarboleta_Click_1(object sender, EventArgs e)
        {
            BUT_GENERARBOLETA_Click(sender, e);
        }

        private void AplicarEstiloMesa(
            Guna2Button boton,
            string estado,
            bool seleccionada)
        {
            // Conservar el diseño amarillo.
            boton.FillColor = Color.Gold;
            boton.ForeColor = Color.Black;
            boton.BorderRadius = 10;

            estado = estado.Trim().ToLower();

            // Mesa seleccionada: borde NEGRO.
            if (seleccionada && estado == "libre")
            {
                boton.BorderThickness = 4;
                boton.BorderColor = Color.Black;
                return;
            }

            // Mesa ocupada: borde ROJO.
            if (estado == "ocupada")
            {
                boton.BorderThickness = 3;
                boton.BorderColor = Color.IndianRed;
                return;
            }

            // Mesa disponible: borde GOLD.
            if (estado == "libre")
            {
                boton.BorderThickness = 2;
                boton.BorderColor = Color.Gold;
                return;
            }

            // Otros estados.
            boton.BorderThickness = 2;
            boton.BorderColor = Color.Gray;
        }

        private void ButtonINICIO_Click(object sender, EventArgs e)
        {
            FRM_INICIO ventana = new FRM_INICIO();

            ventana.Show();
            this.Hide();
        }

        private void guna2PictureBox9_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button2_Click_1(object sender, EventArgs e)
        {

        }
    }
}
