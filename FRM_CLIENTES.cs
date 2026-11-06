using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GESTIÓN DE CLIENTES (FASE C)
    // Conecta con SQL Server los controles que ya existían en el
    // diseño (nombres sin cambiar):
    //   guna2Button10 Nuevo · guna2Button11 Editar · guna2Button13 Eliminar
    //   guna2Button12 Limpiar · guna2Button14 Buscar · txtBuscarProducto
    //   guna2Button16 Guardar · guna2Button15 Cancelar
    //   guna2TextBox1 tipo doc. (automático) · 2 documento · 3 nombre completo
    //   guna2TextBox5 teléfono · 4 correo · 10 fecha nac. · 9 dirección
    //   guna2TextBox8 distrito · 7 comentario
    //   guna2DataGridView1 lista · guna2HtmlLabel30/35/41/38 tarjetas
    //   guna2HtmlLabel57..50 detalle del cliente
    // "Eliminar" desactiva (no borra); doble clic = historial de pedidos.
    // ==========================================================
    public partial class FRM_CLIENTES : Form, IActualizableAlMostrar
    {
        private const string TextoEliminar = "              Eliminar";
        private const string TextoReactivar = "              Reactivar";

        private List<ClienteInfo> clientes = new List<ClienteInfo>();
        private int? idEditando;

        public FRM_CLIENTES()
        {
            InitializeComponent();
            // ventana grande
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            ConfigurarTablaClientes();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            ConfigurarFormulario();
            ConfigurarBotones();
            LimpiarFormulario();
            LimpiarDetalle();

            Shown += (s, e) => CargarClientes(null);
        }

        // El Navegador reutiliza esta ventana: al volver, datos frescos.
        public void ActualizarAlMostrar()
        {
            CargarClientes(null);
        }

        // ======================================================
        // CONFIGURACIÓN
        // ======================================================

        // Tabla de clientes: solo lectura, sin fila vacía editable,
        // columnas proporcionales y texto legible (antes era blanco).
        private void ConfigurarTablaClientes()
        {
            DataGridView tabla = guna2DataGridView1;
            tabla.AllowUserToAddRows = false;
            tabla.AllowUserToDeleteRows = false;
            tabla.ReadOnly = true;
            tabla.MultiSelect = false;
            tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            EstiloTablas.Configurar(tabla, 38, 30);
            // Columnas cortas con ancho exacto; nombre y correo se reparten el resto.
            EstiloTablas.ColumnaFija(Column_N, 50, EstiloTablas.Centro);
            EstiloTablas.ColumnaFija(Column_tipo, 66, EstiloTablas.Izquierda);
            EstiloTablas.ColumnaFija(Column_Documento, 100, EstiloTablas.Centro);
            EstiloTablas.Columna(Column_nombre, 55, 110, EstiloTablas.Izquierda, true);
            EstiloTablas.ColumnaFija(Columntelefono, 88, EstiloTablas.Centro);
            EstiloTablas.Columna(Columncorreo, 45, 90, EstiloTablas.Izquierda);
        }

        private void ConfigurarFormulario()
        {
            guna2HtmlLabel5.Text = "Lista de clientes";

            // Tipo de documento: se deduce del número.
            guna2TextBox1.ReadOnly = true;
            guna2TextBox1.TabStop = false;
            guna2TextBox1.Text = "";

            guna2TextBox2.MaxLength = 12;
            guna2TextBox2.TextChanged += (s, e) => guna2TextBox1.Text = ClienteDatos.TipoDocumento(guna2TextBox2.Text);
            guna2TextBox2.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsLetterOrDigit(e.KeyChar))
                    e.Handled = true;
            };

            guna2TextBox3.MaxLength = 201;
            guna2TextBox3.PlaceholderText = "Nombres y apellidos";

            guna2TextBox5.MaxLength = 9;
            guna2TextBox5.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                    e.Handled = true;
            };

            guna2TextBox4.MaxLength = 100;
            guna2TextBox10.MaxLength = 10;
            guna2TextBox10.Text = "";
            guna2TextBox10.PlaceholderText = "dd/mm/aaaa";
            guna2TextBox9.MaxLength = 255;
            guna2TextBox8.MaxLength = 100;
            guna2TextBox7.MaxLength = 255;

            txtBuscarProducto.PlaceholderText = "Buscar por nombre, documento o teléfono ...";
        }

        private void ConfigurarBotones()
        {
            guna2Button10.Click += (s, e) => NuevoCliente();
            guna2Button11.Click += (s, e) => EditarCliente(ClienteSeleccionado());
            guna2Button13.Click += (s, e) => CambiarEstado(ClienteSeleccionado());
            guna2Button12.Click += (s, e) =>
            {
                txtBuscarProducto.Text = "";
                LimpiarFormulario();
            };
            guna2Button14.Click += (s, e) => Buscar();
            guna2Button16.Click += (s, e) => Guardar();
            guna2Button15.Click += (s, e) => LimpiarFormulario();

            txtBuscarProducto.TextChanged += (s, e) => MostrarTabla(null);
            txtBuscarProducto.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Buscar(); }
            };

            DataGridView tabla = guna2DataGridView1;
            tabla.SelectionChanged += (s, e) => MostrarDetalle(ClienteSeleccionado());
            tabla.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) MostrarHistorial(ClienteDeFila(e.RowIndex));
            };
        }

        // ======================================================
        // CARGA Y PRESENTACIÓN
        // ======================================================
        private void CargarClientes(int? seleccionar)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                clientes = ClienteDatos.Listar();
            }
            catch (Exception ex)
            {
                Registro.Error("Clientes", ex);
                MessageBox.Show("No se pudieron cargar los clientes:\n" + ex.Message,
                    "Gestión de clientes", MessageBoxButtons.OK, MessageBoxIcon.Error);
                clientes = new List<ClienteInfo>();
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            MostrarEstadisticas();
            MostrarTabla(seleccionar);
        }

        private IEnumerable<ClienteInfo> Filtrados()
        {
            string busqueda = (txtBuscarProducto.Text ?? "").Trim().ToLowerInvariant();
            if (busqueda.Length == 0)
                return clientes;

            return clientes.Where(c =>
                c.NombreCompleto.ToLowerInvariant().Contains(busqueda) ||
                c.Documento.ToLowerInvariant().Contains(busqueda) ||
                (c.Telefono ?? "").Contains(busqueda));
        }

        private void MostrarTabla(int? seleccionar)
        {
            DataGridView tabla = guna2DataGridView1;
            ClienteInfo anteriorCliente = ClienteSeleccionado();
            int? anterior = seleccionar ?? (anteriorCliente != null ? (int?)anteriorCliente.IdCliente : null);

            tabla.SuspendLayout();
            tabla.Rows.Clear();

            foreach (ClienteInfo c in Filtrados())
            {
                int fila = tabla.Rows.Add(
                    c.IdCliente.ToString(),
                    c.TipoDocumento,
                    c.Documento,
                    c.NombreCompleto + (c.Activo ? "" : " (inactivo)"),
                    string.IsNullOrEmpty(c.Telefono) ? "-" : c.Telefono,
                    string.IsNullOrEmpty(c.Correo) ? "-" : c.Correo);

                DataGridViewRow row = tabla.Rows[fila];
                row.Tag = c;
                if (!c.Activo)
                    row.DefaultCellStyle.ForeColor = Color.Gray;
            }

            tabla.ResumeLayout();
            tabla.ClearSelection();

            DataGridViewRow elegida = tabla.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(r => anterior.HasValue && ((ClienteInfo)r.Tag).IdCliente == anterior.Value)
                ?? tabla.Rows.Cast<DataGridViewRow>().FirstOrDefault();

            if (elegida != null)
            {
                elegida.Selected = true;
                tabla.CurrentCell = elegida.Cells[3];
            }

            MostrarDetalle(ClienteSeleccionado());
        }

        private void MostrarEstadisticas()
        {
            guna2HtmlLabel30.Text = clientes.Count.ToString("D2");
            guna2HtmlLabel35.Text = clientes.Count(c => c.Activo).ToString("D2");
            guna2HtmlLabel41.Text = clientes.Count(c => c.Activo && c.Frecuente).ToString("D2");
            guna2HtmlLabel38.Text = clientes.Count(c => !c.Activo).ToString("D2");
        }

        // Panel "Detalle del Cliente".
        private void MostrarDetalle(ClienteInfo c)
        {
            if (c == null)
            {
                LimpiarDetalle();
                return;
            }

            guna2HtmlLabel57.Text = c.NombreCompleto;
            guna2HtmlLabel56.Text = c.TipoDocumento + " - " + c.Documento;
            guna2HtmlLabel55.Text = Valor(c.Telefono);
            guna2HtmlLabel54.Text = Valor(c.Correo);
            guna2HtmlLabel53.Text = Valor(c.Direccion);
            guna2HtmlLabel52.Text = Valor(c.Distrito);
            guna2HtmlLabel51.Text = c.FechaNacimiento.HasValue ? c.FechaNacimiento.Value.ToString("dd/MM/yyyy") : "-";
            guna2HtmlLabel50.Text = Valor(c.Comentario);

            // Resumen en una segunda línea pequeña (no invade las etiquetas).
            guna2HtmlLabel14.Text = "Detalle del Cliente<br><span style='font-size:8pt; font-weight:normal'>" +
                (c.Activo ? (c.Frecuente ? "Cliente frecuente" : "Activo") : "Inactivo") +
                " · " + c.PedidosTotales + (c.PedidosTotales == 1 ? " pedido" : " pedidos") +
                " · " + c.SaldoPuntos + " pts</span>";

            guna2Button13.Text = c.Activo ? TextoEliminar : TextoReactivar;
        }

        private void LimpiarDetalle()
        {
            foreach (Control l in new Control[] { guna2HtmlLabel57, guna2HtmlLabel56, guna2HtmlLabel55,
                     guna2HtmlLabel54, guna2HtmlLabel53, guna2HtmlLabel52, guna2HtmlLabel51, guna2HtmlLabel50 })
                l.Text = "-";
            guna2HtmlLabel14.Text = "Detalle del Cliente";
            guna2Button13.Text = TextoEliminar;
        }

        private static string Valor(string texto)
        {
            return string.IsNullOrEmpty(texto) ? "-" : texto;
        }

        private ClienteInfo ClienteSeleccionado()
        {
            DataGridViewRow fila = guna2DataGridView1.CurrentRow;
            return fila == null ? null : fila.Tag as ClienteInfo;
        }

        private ClienteInfo ClienteDeFila(int indice)
        {
            if (indice < 0 || indice >= guna2DataGridView1.Rows.Count)
                return null;
            return guna2DataGridView1.Rows[indice].Tag as ClienteInfo;
        }

        // ======================================================
        // FORMULARIO DE REGISTRO / EDICIÓN
        // ======================================================
        private void LimpiarFormulario()
        {
            idEditando = null;
            foreach (Guna2TextBox t in new[] { guna2TextBox2, guna2TextBox3, guna2TextBox5, guna2TextBox4,
                     guna2TextBox10, guna2TextBox9, guna2TextBox8, guna2TextBox7, guna2TextBox1 })
                t.Text = "";
            guna2HtmlLabel6.Text = "Registro / Edición de cliente";
        }

        private void NuevoCliente()
        {
            LimpiarFormulario();
            guna2HtmlLabel6.Text = "Nuevo cliente";
            guna2TextBox2.Focus();
        }

        private void EditarCliente(ClienteInfo c)
        {
            if (c == null)
            {
                MessageBox.Show("Seleccione un cliente de la lista.", "Gestión de clientes",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LimpiarFormulario();
            idEditando = c.IdCliente;
            guna2HtmlLabel6.Text = "Editando: " + c.NombreCompleto;

            guna2TextBox2.Text = c.Documento;
            guna2TextBox3.Text = c.NombreCompleto;
            guna2TextBox5.Text = c.Telefono;
            guna2TextBox4.Text = c.Correo;
            guna2TextBox10.Text = c.FechaNacimiento.HasValue ? c.FechaNacimiento.Value.ToString("dd/MM/yyyy") : "";
            guna2TextBox9.Text = c.Direccion;
            guna2TextBox8.Text = c.Distrito;
            guna2TextBox7.Text = c.Comentario;
            guna2TextBox3.Focus();
        }

        private void Guardar()
        {
            int? operador = Operador();
            if (!operador.HasValue)
                return;

            try
            {
                string nombre, apellidos;
                ClienteDatos.SepararNombre(guna2TextBox3.Text, out nombre, out apellidos);

                ClienteInfo c = new ClienteInfo
                {
                    IdCliente = idEditando ?? 0,
                    Documento = guna2TextBox2.Text,
                    Nombre = nombre,
                    Apellidos = apellidos,
                    Telefono = guna2TextBox5.Text,
                    Correo = guna2TextBox4.Text,
                    FechaNacimiento = ClienteDatos.LeerFecha(guna2TextBox10.Text),
                    Direccion = guna2TextBox9.Text,
                    Distrito = guna2TextBox8.Text,
                    Comentario = guna2TextBox7.Text
                };

                ClienteDatos.Validar(c);

                guna2Button16.Enabled = false;
                Cursor = Cursors.WaitCursor;

                int id;
                if (idEditando.HasValue)
                {
                    ClienteDatos.Actualizar(c, operador.Value);
                    id = c.IdCliente;
                }
                else
                {
                    id = ClienteDatos.Registrar(c, operador.Value);
                }

                MessageBox.Show(
                    (idEditando.HasValue ? "Datos actualizados" : "Cliente registrado") + " correctamente.\n\n" +
                    "Nombres: " + c.Nombre + "\nApellidos: " + c.Apellidos + "\n" +
                    c.TipoDocumento + ": " + c.Documento,
                    "Gestión de clientes", MessageBoxButtons.OK, MessageBoxIcon.Information);

                LimpiarFormulario();
                txtBuscarProducto.Text = "";
                CargarClientes(id);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de clientes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Clientes", ex);
                MessageBox.Show("No se pudo guardar en SQL Server. No se registró ningún cambio.\n\n" + ex.Message,
                    "Gestión de clientes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                guna2Button16.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        // "Eliminar" = desactivar (o reactivar si ya está inactivo).
        private void CambiarEstado(ClienteInfo c)
        {
            if (c == null)
            {
                MessageBox.Show("Seleccione un cliente de la lista.", "Gestión de clientes",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool activar = !c.Activo;
            string pregunta = activar
                ? "¿Reactivar a " + c.NombreCompleto + "?"
                : "¿Desactivar a " + c.NombreCompleto + "?\n\nNo se borra ningún dato: sus " +
                  c.PedidosTotales + " pedido(s) y " + c.SaldoPuntos + " punto(s) se conservan, " +
                  "y puede reactivarse cuando lo necesite.";

            if (MessageBox.Show(pregunta, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            int? operador = Operador();
            if (!operador.HasValue)
                return;

            try
            {
                ClienteDatos.CambiarEstado(c.IdCliente, activar, operador.Value);
                if (idEditando == c.IdCliente)
                    LimpiarFormulario();
                CargarClientes(c.IdCliente);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de clientes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Clientes", ex);
                MessageBox.Show("No se pudo cambiar el estado:\n" + ex.Message, "Gestión de clientes",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Buscar()
        {
            MostrarTabla(null);
            if (guna2DataGridView1.Rows.Count == 0 && txtBuscarProducto.Text.Trim().Length > 0)
            {
                MessageBox.Show("No se encontraron clientes para \"" + txtBuscarProducto.Text.Trim() + "\".",
                    "Buscar cliente", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Empleado de la sesión con permiso sobre clientes; SQL lo
        // vuelve a validar (Administrador activo con acceso).
        private int? Operador()
        {
            if (!SesionActual.IdEmpleado.HasValue || !Permisos.PuedeGestionarClientes(SesionActual.Cargo))
            {
                MessageBox.Show("Su perfil no puede registrar ni modificar clientes.", "Gestión de clientes",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return SesionActual.IdEmpleado.Value;
        }

        // ======================================================
        // HISTORIAL DE PEDIDOS (doble clic en la lista)
        // ======================================================
        private void MostrarHistorial(ClienteInfo c)
        {
            if (c == null)
                return;

            List<PedidoClienteInfo> pedidos;
            try
            {
                pedidos = ClienteDatos.Pedidos(c.IdCliente);
            }
            catch (Exception ex)
            {
                Registro.Error("Clientes", ex);
                MessageBox.Show("No se pudo cargar el historial:\n" + ex.Message, "Historial de pedidos",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (Form ventana = new Form())
            {
                ventana.Text = "Historial de pedidos";
                ventana.ClientSize = new Size(620, 420);
                ventana.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                ventana.StartPosition = FormStartPosition.CenterParent;
                ventana.BackColor = Color.White;
                ventana.ShowInTaskbar = false;

                Guna2Panel encabezado = new Guna2Panel { Dock = DockStyle.Top, Height = 56, FillColor = Color.Gold };
                encabezado.Controls.Add(new Label
                {
                    Text = c.NombreCompleto.ToUpperInvariant() + "\n" + pedidos.Count + " pedido(s) · " + c.SaldoPuntos + " puntos",
                    Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                });

                Guna2DataGridView tabla = new Guna2DataGridView
                {
                    Location = new Point(16, 70),
                    Size = new Size(588, 330),
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };
                tabla.Columns.Add("pedido", "N.º");
                tabla.Columns.Add("fecha", "FECHA");
                tabla.Columns.Add("canal", "CANAL");
                tabla.Columns.Add("estado", "ESTADO");
                tabla.Columns.Add("total", "TOTAL");
                EstiloTablas.Configurar(tabla, 34, 28);
                tabla.ThemeStyle.HeaderStyle.BackColor = Color.Gold;
                tabla.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
                EstiloTablas.ColumnaFija(tabla.Columns[0], 70, EstiloTablas.Centro);
                EstiloTablas.ColumnaFija(tabla.Columns[1], 150, EstiloTablas.Centro);
                EstiloTablas.Columna(tabla.Columns[2], 50, 90, EstiloTablas.Izquierda);
                EstiloTablas.Columna(tabla.Columns[3], 50, 110, EstiloTablas.Izquierda);
                EstiloTablas.ColumnaFija(tabla.Columns[4], 100, EstiloTablas.Derecha);

                foreach (PedidoClienteInfo p in pedidos)
                {
                    tabla.Rows.Add(p.IdPedido.ToString("D4"), p.FechaHora.ToString("dd/MM/yyyy HH:mm"),
                        p.Canal, p.Estado, "S/ " + p.Total.ToString("0.00"));
                }

                ventana.Controls.Add(tabla);
                ventana.Controls.Add(encabezado);
                ventana.ShowDialog(this);
            }
        }

        // ======================================================
        // NAVEGACIÓN (eventos del diseñador)
        // ======================================================
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

        private void guna2Button5_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_COCINA>(this);
        }

        private void guna2Button4_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_CLIENTES>(this);
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_EMPLEADOS>(this);
        }
    }
}
