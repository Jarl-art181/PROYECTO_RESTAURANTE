
using Guna.Charts.WinForms;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GESTIÓN DE EMPLEADOS (FASE 2A)
    // Conecta con SQL Server los controles que ya existían en el
    // diseño: tabla, buscador, filtros, tarjetas, gráfico, panel
    // de información y botón "Nuevo empleado".
    //
    // Controles del diseñador utilizados (nombres sin cambiar):
    //   guna2Button10        -> Nuevo empleado
    //   guna2DataGridView1   -> tabla de empleados
    //   txtBuscarProducto    -> buscador ("Buscar empleado...")
    //   guna2ComboBox2       -> filtro por cargo
    //   guna2ComboBox1       -> filtro por estado
    //   lblVentasHoy, guna2HtmlLabel6/8/10 -> tarjetas
    //   GunaDona + guna2Panel10 -> distribución por cargo
    //   guna2Panel8          -> información del empleado
    // ==========================================================
    public partial class FRM_EMPLEADOS : Form, IActualizableAlMostrar
    {
        private const string TodosLosCargos = "Todos los cargos";
        private const string TodosLosEstados = "Todos los estados";

        private List<EmpleadoInfo> empleados = new List<EmpleadoInfo>();
        private readonly ContextMenuStrip menuAcciones = new ContextMenuStrip();
        private EmpleadoInfo empleadoMenu;

        // Administrador que autorizó operaciones en esta ventana.
        // Se vuelve a validar en SQL Server en cada operación.
        private int? idAdministradorAutorizado;

        public FRM_EMPLEADOS()
        {
            InitializeComponent();
            // ventana grande
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            guna2HtmlLabel4.Text =
                "Registro de trabajadores, cargos, turnos y estado laboral";

            ConfigurarTabla();
            ConfigurarFiltros();
            ConfigurarMenuAcciones();
            OrganizarPaneles();
            LimpiarDetalle();

            // Diseño común: se adapta a la pantalla y el encabezado muestra
            // la hora, la fecha y el usuario de la sesión.
            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);

            guna2Button10.Click += btnNuevoEmpleado_Click;
            FormClosed += (s, e) => menuAcciones.Dispose();

            Shown += (s, e) => CargarEmpleados(null);
        }

        // El Navegador reutiliza esta ventana: al volver, datos frescos de SQL.
        public void ActualizarAlMostrar()
        {
            CargarEmpleados(null);
        }

        // ======================================================
        // CONFIGURACIÓN
        // ======================================================
        private void ConfigurarTabla()
        {
            DataGridView tabla = guna2DataGridView1;

            tabla.Rows.Clear();
            tabla.ReadOnly = true;
            tabla.AllowUserToAddRows = false;
            tabla.AllowUserToDeleteRows = false;
            tabla.MultiSelect = false;
            tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            ColumnAccion.Text = "Opciones";
            ColumnAccion.UseColumnTextForButtonValue = true;

            // Visual: proporciones, textos legibles y nombres en dos líneas.
            EstiloTablas.Configurar(tabla, 36, 34);
            // Celdas a 10 pt (antes 11,25 pt): con 8 columnas en 719 px
            // el texto se cortaba. Misma familia de letra del diseño.
            tabla.DefaultCellStyle.Font = new Font("Microsoft Sans Serif", 10F);
            tabla.AlternatingRowsDefaultCellStyle.Font = tabla.DefaultCellStyle.Font;
            ((Guna2DataGridView)tabla).ThemeStyle.RowsStyle.Font = tabla.DefaultCellStyle.Font;

            // Columnas cortas con ancho exacto (medido con la letra real);
            // el nombre ocupa el resto y pasa a dos líneas si es largo.
            EstiloTablas.ColumnaFija(Column_codi, 62, EstiloTablas.Centro);
            EstiloTablas.Columna(Column_nombres, 100, 156, EstiloTablas.Izquierda, true);
            EstiloTablas.ColumnaFija(Column1_DNI, 80, EstiloTablas.Centro);
            EstiloTablas.ColumnaFija(Column_cargo, 108, EstiloTablas.Izquierda);
            EstiloTablas.ColumnaFija(Column_telefono, 88, EstiloTablas.Centro);
            EstiloTablas.ColumnaFija(Column_turno, 82, EstiloTablas.Izquierda);
            EstiloTablas.ColumnaFija(Column_Estado, 68, EstiloTablas.Izquierda);
            EstiloTablas.ColumnaFija(ColumnAccion, 86, EstiloTablas.Centro);

            tabla.SelectionChanged += (s, e) => MostrarDetalle(EmpleadoSeleccionado());
            tabla.CellContentClick += Tabla_CellContentClick;
            tabla.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0) EditarEmpleado(EmpleadoDeFila(e.RowIndex));
            };
        }

        // ------------------------------------------------------
        // DISTRIBUCIÓN (complementa al diseñador)
        // ------------------------------------------------------
        // Quinta fila de la leyenda (Repartidores): el diseño traía 4.
        private Guna2CircleButton puntoRepartidores;
        private Guna2HtmlLabel lblRepartidores, lblCantidadRepartidores, lblPorcentajeRepartidores;

        private void OrganizarPaneles()
        {
            // Leyenda: los puntos de color son marcas, sin texto.
            Guna2CircleButton[] puntos = { guna2CircleButton4, guna2CircleButton5, guna2CircleButton6, guna2CircleButton1 };
            Color[] colores = { Color.CornflowerBlue, Color.LightCoral, Color.MediumSpringGreen, Color.Khaki };
            for (int i = 0; i < puntos.Length; i++)
            {
                puntos[i].Text = "";
                puntos[i].FillColor = colores[i];
                puntos[i].TabStop = false;
            }

            puntoRepartidores = new Guna2CircleButton
            {
                Size = guna2CircleButton1.Size,
                Location = new Point(guna2CircleButton1.Left, guna2CircleButton1.Top + 27),
                FillColor = Color.Plum,
                BackColor = Color.White,
                BorderColor = Color.White,
                TabStop = false,
                Text = ""
            };
            puntoRepartidores.ShadowDecoration.Mode = Guna.UI2.WinForms.Enums.ShadowMode.Circle;
            lblRepartidores = CopiarEtiqueta(guna2HtmlLabel34, "Repartidores");
            lblCantidadRepartidores = CopiarEtiqueta(lblCantidadTotal, "00");
            lblPorcentajeRepartidores = CopiarEtiqueta(lblPorcentajeTotal, "0%");
            guna2Panel10.Controls.AddRange(new Control[] { puntoRepartidores, lblRepartidores, lblCantidadRepartidores, lblPorcentajeRepartidores });

            // Información del empleado: valores sin salirse del panel
            // (texto completo al pasar el mouse).
            ToolTip ayuda = new ToolTip { AutoPopDelay = 10000 };
            foreach (Guna2HtmlLabel valor in new[] { guna2HtmlLabel57, guna2HtmlLabel56, guna2HtmlLabel26,
                     guna2HtmlLabel25, guna2HtmlLabel30, guna2HtmlLabel29 })
            {
                valor.AutoSize = false;
                valor.Size = new Size(guna2Panel8.Width - valor.Left - 10, 22);
                Guna2HtmlLabel v = valor;
                v.TextChanged += (s, e) => ayuda.SetToolTip(v, v.Text.Length > 22 ? v.Text : "");
            }
            Disposed += (s, e) => ayuda.Dispose();
        }

        private static Guna2HtmlLabel CopiarEtiqueta(Guna2HtmlLabel modelo, string texto)
        {
            return new Guna2HtmlLabel
            {
                BackColor = Color.Transparent,
                Font = modelo.Font,
                ForeColor = modelo.ForeColor,
                Location = new Point(modelo.Left, modelo.Top + 27),
                Text = texto
            };
        }

        private void ConfigurarFiltros()
        {
            guna2ComboBox2.Items.Clear();
            guna2ComboBox2.Items.Add(TodosLosCargos);
            guna2ComboBox2.Items.AddRange(Cargos.Todos);
            guna2ComboBox2.SelectedIndex = 0;

            guna2ComboBox1.Items.Clear();
            guna2ComboBox1.Items.Add(TodosLosEstados);
            guna2ComboBox1.Items.Add("Activos");
            guna2ComboBox1.Items.Add("Inactivos");
            guna2ComboBox1.SelectedIndex = 0;

            guna2ComboBox2.SelectedIndexChanged += (s, e) => MostrarTabla(null);
            guna2ComboBox1.SelectedIndexChanged += (s, e) => MostrarTabla(null);
            txtBuscarProducto.TextChanged += (s, e) => MostrarTabla(null);
        }

        private void ConfigurarMenuAcciones()
        {
            menuAcciones.Font = new Font("Segoe UI", 10F);
            menuAcciones.Items.Add("Editar datos y cargo...", null, (s, e) => EditarEmpleado(empleadoMenu));
            menuAcciones.Items.Add("Desactivar empleado", null, (s, e) => CambiarEstado(empleadoMenu));
            menuAcciones.Items.Add(new ToolStripSeparator());
            menuAcciones.Items.Add("Cuenta de acceso...", null, (s, e) => AbrirCuenta(empleadoMenu));
        }

        // ======================================================
        // CARGA DE DATOS
        // ======================================================
        private void CargarEmpleados(int? seleccionar)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                empleados = EmpleadoDatos.Listar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudieron cargar los empleados:\n" + ex.Message,
                    "Gestión de empleados",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                empleados = new List<EmpleadoInfo>();
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            MostrarEstadisticas();
            MostrarTabla(seleccionar);
        }

        private IEnumerable<EmpleadoInfo> Filtrados()
        {
            string cargo = guna2ComboBox2.SelectedItem as string;
            int estado = guna2ComboBox1.SelectedIndex;   // 0 todos, 1 activos, 2 inactivos
            string busqueda = (txtBuscarProducto.Text ?? "").Trim().ToLowerInvariant();

            return empleados.Where(e =>
                (cargo == null || cargo == TodosLosCargos || e.Cargo == cargo) &&
                (estado <= 0 || (estado == 1) == (e.Estado == "activo")) &&
                (busqueda.Length == 0 ||
                 e.NombreCompleto.ToLowerInvariant().Contains(busqueda) ||
                 e.Dni.Contains(busqueda) ||
                 Codigo(e).ToLowerInvariant().Contains(busqueda)));
        }

        private void MostrarTabla(int? seleccionar)
        {
            DataGridView tabla = guna2DataGridView1;
            int? anterior = seleccionar ?? (EmpleadoSeleccionado() != null
                ? (int?)EmpleadoSeleccionado().IdEmpleado : null);

            tabla.SuspendLayout();
            tabla.Rows.Clear();

            foreach (EmpleadoInfo e in Filtrados())
            {
                int fila = tabla.Rows.Add(
                    Codigo(e),
                    e.NombreCompleto,
                    e.Dni,
                    string.IsNullOrEmpty(e.Cargo) ? "Sin cargo" : e.Cargo,
                    string.IsNullOrEmpty(e.Telefono) ? "-" : e.Telefono,
                    Turnos.Mostrar(e.Turno),
                    e.Estado == "activo" ? "Activo" : "Inactivo");

                DataGridViewRow row = tabla.Rows[fila];
                row.Tag = e;

                if (e.Estado != "activo")
                    row.DefaultCellStyle.ForeColor = Color.Gray;
            }

            tabla.ResumeLayout();
            tabla.ClearSelection();

            // Volver a seleccionar el empleado anterior o el primero.
            DataGridViewRow elegida = tabla.Rows.Cast<DataGridViewRow>()
                .FirstOrDefault(r => anterior.HasValue && ((EmpleadoInfo)r.Tag).IdEmpleado == anterior.Value)
                ?? tabla.Rows.Cast<DataGridViewRow>().FirstOrDefault();

            if (elegida != null)
            {
                elegida.Selected = true;
                tabla.CurrentCell = elegida.Cells[1];
            }

            MostrarDetalle(EmpleadoSeleccionado());
        }

        // Tarjetas y gráfico: solo personal ACTIVO.
        private void MostrarEstadisticas()
        {
            List<EmpleadoInfo> activos = empleados.Where(e => e.Estado == "activo").ToList();
            int total = activos.Count;

            Func<string, int> contar = cargo => activos.Count(e => e.Cargo == cargo);
            int administradores = contar(Cargos.Administrador);
            int cocineros = contar(Cargos.Cocinero);
            int meseros = contar(Cargos.Mesero);
            int cajeros = contar(Cargos.Cajero);
            int repartidores = contar(Cargos.Repartidor);

            // Tarjetas superiores.
            lblVentasHoy.Text = total.ToString("D2");
            guna2HtmlLabel6.Text = administradores.ToString("D2");
            guna2HtmlLabel8.Text = cocineros.ToString("D2");
            guna2HtmlLabel10.Text = meseros.ToString("D2");

            // Leyenda de "Distribución por cargo": los cinco cargos.
            guna2HtmlLabel50.Text = "Distribución por cargo (activos)";
            guna2HtmlLabel51.Text = "Administradores";
            guna2HtmlLabel54.Text = "Cocineros";
            guna2HtmlLabel33.Text = "Meseros";
            guna2HtmlLabel34.Text = "Cajeros";

            MostrarFilaLeyenda(lblCantidadSalon, lblPorcentajeSalon, administradores, total);
            MostrarFilaLeyenda(lblCantidadDelivery, lblPorcentajeDelivery, cocineros, total);
            MostrarFilaLeyenda(lblCantidadRecojo, lblPorcentajeRecojo, meseros, total);
            MostrarFilaLeyenda(lblCantidadTotal, lblPorcentajeTotal, cajeros, total);
            MostrarFilaLeyenda(lblCantidadRepartidores, lblPorcentajeRepartidores, repartidores, total);

            // Gráfico con los cinco cargos (colores de la leyenda).
            GunaDona.Datasets.Clear();
            if (total > 0)
            {
                GunaDoughnutDataset datos = new GunaDoughnutDataset();
                datos.Label = "Empleados activos";

                AgregarPunto(datos, "Administradores", administradores, Color.CornflowerBlue);
                AgregarPunto(datos, "Cocineros", cocineros, Color.LightCoral);
                AgregarPunto(datos, "Meseros", meseros, Color.MediumSpringGreen);
                AgregarPunto(datos, "Cajeros", cajeros, Color.Khaki);
                AgregarPunto(datos, "Repartidores", repartidores, Color.Plum);

                datos.TargetChart = GunaDona;
                GunaDona.Datasets.Add(datos);
            }

            GunaDona.Legend.Display = false;
            GunaDona.Update();
        }

        private static void AgregarPunto(GunaDoughnutDataset datos, string texto, int valor, Color color)
        {
            datos.DataPoints.Add(new LPoint { Label = texto, Y = valor });
            datos.FillColors.Add(color);
        }

        private static void MostrarFilaLeyenda(Control cantidad, Control porcentaje, int valor, int total)
        {
            cantidad.Text = valor.ToString("D2");
            porcentaje.Text = total == 0 ? "0%" : Math.Round(valor * 100.0 / total).ToString("0") + "%";
        }

        // Panel "Información del Empleado".
        private void MostrarDetalle(EmpleadoInfo e)
        {
            if (e == null)
            {
                LimpiarDetalle();
                return;
            }

            guna2HtmlLabel57.Text = e.Nombre;
            guna2HtmlLabel56.Text = e.Apellidos;
            guna2HtmlLabel26.Text = string.IsNullOrEmpty(e.Cargo) ? "Sin cargo" : e.Cargo;
            guna2HtmlLabel25.Text = string.IsNullOrEmpty(e.Telefono) ? "-" : e.Telefono;
            guna2HtmlLabel30.Text = Turnos.Mostrar(e.Turno);
            guna2HtmlLabel29.Text = (e.Estado == "activo" ? "Activo" : "Inactivo") +
                                    " · " + VentanaCuentaAcceso.Resumen(e);
            guna2HtmlLabel29.ForeColor = e.Estado == "activo" ? Color.ForestGreen : Color.Firebrick;
        }

        private void LimpiarDetalle()
        {
            guna2HtmlLabel57.Text = "-";
            guna2HtmlLabel56.Text = "-";
            guna2HtmlLabel26.Text = "-";
            guna2HtmlLabel25.Text = "-";
            guna2HtmlLabel30.Text = "-";
            guna2HtmlLabel29.Text = "-";
            guna2HtmlLabel29.ForeColor = Color.Black;
        }

        private static string Codigo(EmpleadoInfo e)
        {
            return "E" + e.IdEmpleado.ToString("D4");
        }

        private EmpleadoInfo EmpleadoSeleccionado()
        {
            DataGridViewRow fila = guna2DataGridView1.CurrentRow;
            return fila == null ? null : fila.Tag as EmpleadoInfo;
        }

        private EmpleadoInfo EmpleadoDeFila(int indice)
        {
            if (indice < 0 || indice >= guna2DataGridView1.Rows.Count)
                return null;
            return guna2DataGridView1.Rows[indice].Tag as EmpleadoInfo;
        }

        // ======================================================
        // OPERACIONES (requieren Administrador)
        // ======================================================
        private int? ObtenerAutorizacion()
        {
            if (idAdministradorAutorizado.HasValue)
            {
                string error;
                if (AutorizacionAdministrador.EsAdministradorValido(idAdministradorAutorizado.Value, out error))
                    return idAdministradorAutorizado;

                idAdministradorAutorizado = null;
            }

            idAdministradorAutorizado = AutorizacionAdministrador.Solicitar(this);
            return idAdministradorAutorizado;
        }

        private void btnNuevoEmpleado_Click(object sender, EventArgs e)
        {
            int? admin = ObtenerAutorizacion();
            if (!admin.HasValue)
                return;

            AbrirRegistro(admin.Value, null);
        }

        private void EditarEmpleado(EmpleadoInfo empleado)
        {
            if (empleado == null)
                return;

            int? admin = ObtenerAutorizacion();
            if (!admin.HasValue)
                return;

            AbrirRegistro(admin.Value, empleado);
        }

        private void AbrirRegistro(int admin, EmpleadoInfo empleado)
        {
            try
            {
                using (FRM_REGISTRO_EMPLEADO ventana = new FRM_REGISTRO_EMPLEADO(admin, empleado))
                {
                    if (ventana.ShowDialog(this) == DialogResult.OK)
                        CargarEmpleados(ventana.IdEmpleadoGuardado);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo abrir el registro de empleados:\n" + ex.Message,
                    "Gestión de empleados",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Opciones › Cuenta de acceso (crear, habilitar, bloquear, restablecer).
        private void AbrirCuenta(EmpleadoInfo empleado)
        {
            if (empleado == null)
                return;

            int? admin = ObtenerAutorizacion();
            if (!admin.HasValue)
                return;

            try
            {
                // Datos frescos: otro Administrador pudo cambiar la cuenta.
                EmpleadoInfo actual = EmpleadoDatos.Obtener(empleado.IdEmpleado) ?? empleado;

                using (VentanaCuentaAcceso ventana = new VentanaCuentaAcceso(actual, admin.Value))
                {
                    ventana.ShowDialog(this);
                    if (ventana.HuboCambios)
                        CargarEmpleados(empleado.IdEmpleado);
                }
            }
            catch (Exception ex)
            {
                Registro.Error("Cuentas", ex);
                MessageBox.Show(
                    "No se pudo abrir la cuenta de acceso:\n" + ex.Message,
                    "Gestión de empleados",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CambiarEstado(EmpleadoInfo empleado)
        {
            if (empleado == null)
                return;

            bool activar = empleado.Estado != "activo";
            string accion = activar ? "activar" : "desactivar";

            if (MessageBox.Show(
                    "¿Desea " + accion + " a " + empleado.NombreCompleto + "?" +
                    (activar ? "" : "\n\nNo se borrará ningún dato: su historial de pedidos y cobros se conserva."),
                    "Confirmar",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            int? admin = ObtenerAutorizacion();
            if (!admin.HasValue)
                return;

            try
            {
                EmpleadoDatos.CambiarEstado(empleado.IdEmpleado, activar, admin.Value);
                CargarEmpleados(empleado.IdEmpleado);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de empleados",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo cambiar el estado:\n" + ex.Message,
                    "Gestión de empleados", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Tabla_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || guna2DataGridView1.Columns[e.ColumnIndex] != ColumnAccion)
                return;

            empleadoMenu = EmpleadoDeFila(e.RowIndex);
            if (empleadoMenu == null)
                return;

            menuAcciones.Items[1].Text = empleadoMenu.Estado == "activo"
                ? "Desactivar empleado"
                : "Activar empleado";

            Rectangle celda = guna2DataGridView1.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            menuAcciones.Show(guna2DataGridView1, new Point(celda.Left, celda.Bottom));
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

        private void guna2Button9_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_MENU>(this);
        }
    }
}
