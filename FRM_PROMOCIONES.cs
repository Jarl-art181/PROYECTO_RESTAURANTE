using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GESTIÓN DE PROMOCIONES (FASE F)
    // El diseño original solo tiene la cabecera y el panel
    // lateral; el contenido se construye por código con el mismo
    // estilo de Menú e Inventario (Guna UI2, amarillo, blanco y
    // negro). No se modifica el .Designer.cs ni el .resx.
    //
    //  * Buscador y filtros: Todas, Vigentes, Programadas, Vencidas, Inactivas.
    //  * Tarjetas: vigentes hoy, programadas, vencidas/inactivas,
    //    pedidos con promoción (y descuento otorgado).
    //  * Tabla de promociones y panel de registro/edición.
    //  * Activar/desactivar (nunca se borra: los pedidos que la
    //    usaron conservan sus importes).
    // Esta ventana NO aplica descuentos a pedidos.
    // ==========================================================
    public partial class FRM_PROMOCIONES : Form, IActualizableAlMostrar
    {
        private const string TextoEditar = "Editar";
        private const string TextoCancelar = "Cancelar";

        private static readonly Color Dorado = Color.Gold;
        private static readonly Color Naranja = Color.Orange;

        private List<PromocionInfo> promociones = new List<PromocionInfo>();
        private List<ProductoMenu> platos = new List<ProductoMenu>();
        private PromocionInfo seleccionada;
        private bool editando;
        private bool esNueva;
        private string filtro = "todas";

        // Controles creados por código.
        private Guna2TextBox txtBuscar;
        private readonly Dictionary<string, Guna2Button> botonesFiltro = new Dictionary<string, Guna2Button>();
        private Guna2Button btnNueva;
        private Label lblVigentes, lblProgramadas, lblVencidas, lblPedidos, lblOtorgado;
        private Guna2DataGridView tabla;
        private Label lblTituloDetalle, lblSituacion;
        private Guna2TextBox txtNombre, txtDescripcion, txtValor, txtMinimo;
        private Guna2ComboBox cmbTipo, cmbAlcance;
        private DateTimePicker dtInicio, dtFin;
        private readonly CheckBox[] chkDias = new CheckBox[7];
        private CheckedListBox lstPlatos;
        private Label lblPlatos;
        private Guna2Button btnGuardar, btnEditar, btnEstado;

        public FRM_PROMOCIONES()
        {
            InitializeComponent();
            // ventana grande
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            guna2HtmlLabel3.Text = "GESTIÓN DE PROMOCIONES";
            guna2HtmlLabel4.Text = "Descuentos por plato o por pedido, vigencia, días y monto mínimo";

            SuspendLayout();
            CrearBarra();
            CrearTarjetas();
            CrearTabla();
            CrearDetalle();
            ResumeLayout(true);

            MostrarPromocion(null);
            Shown += (s, e) => Cargar(null);
        }

        public void ActualizarAlMostrar()
        {
            if (!editando)
                Cargar(seleccionada != null ? (int?)seleccionada.IdPromocion : null);
        }

        // ======================================================
        // CONSTRUCCIÓN DE LA INTERFAZ
        // ======================================================
        private static Guna2Button Boton(string texto, Color relleno, Rectangle r)
        {
            return new Guna2Button
            {
                Text = texto,
                Bounds = r,
                BorderRadius = 10,
                FillColor = relleno,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private static Guna2TextBox Caja(Control padre, Rectangle r, int maximo, string marcador)
        {
            Guna2TextBox t = new Guna2TextBox
            {
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.Black,
                BorderRadius = 8,
                BorderColor = Color.Goldenrod,
                MaxLength = maximo,
                PlaceholderText = marcador
            };
            padre.Controls.Add(t);
            t.Bounds = r;   // después de la fuente: Guna reajusta la posición
            return t;
        }

        private static Label Etiqueta(Control padre, string texto, int x, int y)
        {
            Label l = new Label
            {
                Text = texto, Location = new Point(x, y), AutoSize = true,
                Font = new Font("Segoe UI", 9.75F, FontStyle.Bold), BackColor = Color.Transparent
            };
            padre.Controls.Add(l);
            return l;
        }

        private void CrearBarra()
        {
            txtBuscar = Caja(this, new Rectangle(356, 146, 300, 40), 100, "Buscar promoción o plato...");
            txtBuscar.TextChanged += (s, e) => AplicarFiltro();

            string[,] filtros = { { "todas", "Todas" }, { "vigentes", "Vigentes" }, { "programadas", "Programadas" },
                                  { "vencidas", "Vencidas" }, { "inactivas", "Inactivas" } };
            for (int i = 0; i < filtros.GetLength(0); i++)
            {
                string clave = filtros[i, 0];
                Guna2Button b = Boton(filtros[i, 1], Dorado, new Rectangle(668 + i * 124, 143, 118, 45));
                b.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                b.Click += (s, e) => { filtro = filtro == clave ? "todas" : clave; AplicarFiltro(); };
                botonesFiltro[clave] = b;
                Controls.Add(b);
            }

            btnNueva = Boton("Nueva promoción", Naranja, new Rectangle(1300, 138, 223, 54));
            btnNueva.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnNueva.Click += (s, e) => NuevaPromocion();
            Controls.Add(btnNueva);
        }

        private void CrearTarjetas()
        {
            lblVigentes = Tarjeta(356, Color.SpringGreen, "Vigentes hoy");
            lblProgramadas = Tarjeta(550, Color.LightSteelBlue, "Programadas");
            lblVencidas = Tarjeta(744, Color.Khaki, "Vencidas o inactivas");
            lblPedidos = Tarjeta(938, Color.PaleVioletRed, "Pedidos con promoción");
            lblOtorgado = new Label
            {
                Location = new Point(14, 52), Size = new Size(160, 18), BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.Black
            };
            lblPedidos.Parent.Controls.Add(lblOtorgado);
        }

        private Label Tarjeta(int x, Color color, string titulo)
        {
            Guna2Panel p = new Guna2Panel { Bounds = new Rectangle(x, 206, 186, 74), BorderRadius = 12, FillColor = color, BackColor = Color.Transparent };
            p.Controls.Add(new Label
            {
                Text = titulo, Location = new Point(14, 8), AutoSize = true, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            });
            Label valor = new Label
            {
                Text = "00", Location = new Point(14, 26), Size = new Size(160, 28), BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold | FontStyle.Italic)
            };
            p.Controls.Add(valor);
            Controls.Add(p);
            return valor;
        }

        private void CrearTabla()
        {
            Guna2Panel cabecera = new Guna2Panel { Bounds = new Rectangle(351, 292, 779, 40), BorderRadius = 12, FillColor = Dorado, BackColor = Color.Transparent };
            cabecera.Controls.Add(new Label
            {
                Text = "Lista de promociones", Location = new Point(22, 9), AutoSize = true, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            });
            Controls.Add(cabecera);

            tabla = new Guna2DataGridView
            {
                Bounds = new Rectangle(351, 338, 779, 600),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                MultiSelect = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
            };
            foreach (string[] c in new[] { new[] { "nombre", "PROMOCIÓN" }, new[] { "descuento", "DESCUENTO" }, new[] { "alcance", "APLICA A" },
                     new[] { "vigencia", "VIGENCIA" }, new[] { "dias", "DÍAS" }, new[] { "situacion", "SITUACIÓN" } })
                tabla.Columns.Add(c[0], c[1]);
            Controls.Add(tabla);

            EstiloTablas.Configurar(tabla, 36, 32);
            tabla.ThemeStyle.HeaderStyle.BackColor = Dorado;
            tabla.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
            EstiloTablas.Columna(tabla.Columns[0], 30, 150, EstiloTablas.Izquierda, true);
            EstiloTablas.ColumnaFija(tabla.Columns[1], 92, EstiloTablas.Derecha);
            EstiloTablas.Columna(tabla.Columns[2], 18, 120, EstiloTablas.Izquierda, true);
            EstiloTablas.ColumnaFija(tabla.Columns[3], 150, EstiloTablas.Centro);
            EstiloTablas.Columna(tabla.Columns[4], 14, 90, EstiloTablas.Izquierda, true);
            EstiloTablas.ColumnaFija(tabla.Columns[5], 118, EstiloTablas.Izquierda);

            tabla.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0) Seleccionar(tabla.Rows[e.RowIndex].Tag as PromocionInfo);
            };
        }

        private void CrearDetalle()
        {
            Guna2Panel cabecera = new Guna2Panel { Bounds = new Rectangle(1137, 206, 393, 46), BorderRadius = 12, FillColor = Dorado, BackColor = Color.Transparent };
            lblTituloDetalle = new Label
            {
                Text = "Detalle de la promoción", Location = new Point(20, 11), AutoSize = true, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12.5F, FontStyle.Bold)
            };
            cabecera.Controls.Add(lblTituloDetalle);
            Controls.Add(cabecera);

            Guna2Panel p = new Guna2Panel
            {
                Bounds = new Rectangle(1137, 258, 393, 680), BorderRadius = 12, FillColor = Color.WhiteSmoke, BackColor = Color.Transparent
            };
            Controls.Add(p);

            lblSituacion = new Label { Location = new Point(16, 8), Size = new Size(360, 20), BackColor = Color.Transparent, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            p.Controls.Add(lblSituacion);

            Etiqueta(p, "Nombre", 14, 32);
            txtNombre = Caja(p, new Rectangle(14, 52, 364, 36), 100, "Ej.: Martes de ceviche");
            Etiqueta(p, "Descripción", 14, 94);
            txtDescripcion = Caja(p, new Rectangle(14, 114, 364, 48), 255, "Condiciones visibles para el personal");
            txtDescripcion.Multiline = true;

            Etiqueta(p, "Tipo de descuento", 14, 168);
            cmbTipo = Combo(p, new Rectangle(14, 188, 176, 36), new[] { "Porcentaje (%)", "Monto fijo (S/)" });
            Etiqueta(p, "Valor", 202, 168);
            txtValor = Caja(p, new Rectangle(202, 188, 176, 36), 9, "Ej.: 10");
            txtValor.KeyPress += SoloDecimales;

            Etiqueta(p, "Aplica a", 14, 230);
            cmbAlcance = Combo(p, new Rectangle(14, 250, 176, 36), new[] { "Platos seleccionados", "Todo el pedido" });
            cmbAlcance.SelectedIndexChanged += (s, e) => ActualizarPlatosHabilitados();
            Etiqueta(p, "Compra mínima (S/)", 202, 230);
            txtMinimo = Caja(p, new Rectangle(202, 250, 176, 36), 9, "Opcional");
            txtMinimo.KeyPress += SoloDecimales;

            Etiqueta(p, "Inicio", 14, 292);
            dtInicio = Fecha(p, new Point(14, 312));
            Etiqueta(p, "Fin", 202, 292);
            dtFin = Fecha(p, new Point(202, 312));

            Etiqueta(p, "Días de la semana", 14, 346);
            string[] dias = { "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb", "Dom" };
            for (int i = 0; i < 7; i++)
            {
                chkDias[i] = new CheckBox
                {
                    Text = dias[i], Location = new Point(14 + i * 52, 366), Size = new Size(52, 24),
                    Font = new Font("Segoe UI", 9F), BackColor = Color.Transparent, Checked = true
                };
                p.Controls.Add(chkDias[i]);
            }

            lblPlatos = Etiqueta(p, "Platos participantes", 14, 396);
            lstPlatos = new CheckedListBox
            {
                Bounds = new Rectangle(14, 416, 364, 192), CheckOnClick = true, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F), IntegralHeight = false
            };
            p.Controls.Add(lstPlatos);

            btnGuardar = Boton("Guardar", Dorado, new Rectangle(4, 620, 126, 52));
            btnEditar = Boton(TextoEditar, Dorado, new Rectangle(134, 620, 126, 52));
            btnEstado = Boton("Desactivar", Dorado, new Rectangle(264, 620, 125, 52));
            btnGuardar.Click += (s, e) => Guardar();
            btnEditar.Click += (s, e) => { if (editando) CancelarEdicion(); else Editar(); };
            btnEstado.Click += (s, e) => CambiarEstado();
            p.Controls.AddRange(new Control[] { btnGuardar, btnEditar, btnEstado });
        }

        private static Guna2ComboBox Combo(Control padre, Rectangle r, string[] items)
        {
            Guna2ComboBox c = new Guna2ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = Color.Black,
                BorderRadius = 8,
                BorderColor = Color.Goldenrod
            };
            c.Items.AddRange(items);
            padre.Controls.Add(c);
            c.Bounds = r;
            return c;
        }

        private static DateTimePicker Fecha(Control padre, Point ubicacion)
        {
            DateTimePicker d = new DateTimePicker
            {
                Location = ubicacion, Width = 176, Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10F)
            };
            padre.Controls.Add(d);
            return d;
        }

        private static void SoloDecimales(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != ',')
                e.Handled = true;
        }

        // ======================================================
        // CARGA, INDICADORES Y FILTROS
        // ======================================================
        private void Cargar(int? seleccionar)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                promociones = PromocionDatos.Listar();
                platos = MenuDatos.Listar();
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de promociones", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Promociones", ex);
                MessageBox.Show("No se pudieron cargar las promociones:\n" + ex.Message, "Gestión de promociones",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            DateTime hoy = DateTime.Today;
            lblVigentes.Text = promociones.Count(p => p.Situacion(hoy) == "Vigente hoy").ToString("D2");
            lblProgramadas.Text = promociones.Count(p => p.Situacion(hoy) == "Programada").ToString("D2");
            lblVencidas.Text = promociones.Count(p => p.Situacion(hoy) == "Vencida" || p.Situacion(hoy) == "Inactiva").ToString("D2");
            lblPedidos.Text = promociones.Sum(p => p.PedidosConPromocion).ToString("D2");
            lblOtorgado.Text = "S/ " + promociones.Sum(p => p.DescuentoOtorgado).ToString("0.00", CultureInfo.InvariantCulture) + " descontados";

            AplicarFiltro();
            MostrarPromocion(seleccionar.HasValue ? promociones.FirstOrDefault(p => p.IdPromocion == seleccionar.Value) : null);
        }

        private bool PasaFiltro(PromocionInfo p, string buscado)
        {
            string s = p.Situacion(DateTime.Today);
            bool situacion =
                filtro == "todas" ||
                (filtro == "vigentes" && s.StartsWith("Vigente")) ||
                (filtro == "programadas" && s == "Programada") ||
                (filtro == "vencidas" && s == "Vencida") ||
                (filtro == "inactivas" && s == "Inactiva");
            return situacion && (buscado.Length == 0 ||
                MenuDatos.Normalizar(p.Nombre).Contains(buscado) ||
                MenuDatos.Normalizar(p.Descripcion).Contains(buscado) ||
                p.NombresPlatos.Any(n => MenuDatos.Normalizar(n).Contains(buscado)));
        }

        private void AplicarFiltro()
        {
            string buscado = MenuDatos.Normalizar(txtBuscar.Text);
            DateTime hoy = DateTime.Today;

            tabla.SuspendLayout();
            tabla.Rows.Clear();
            foreach (PromocionInfo p in promociones.Where(x => PasaFiltro(x, buscado)))
            {
                string s = p.Situacion(hoy);
                int fila = tabla.Rows.Add(p.Nombre, p.DescuentoTexto,
                    p.PorPlatos ? p.Platos.Count + " plato(s)" : p.AlcanceTexto,
                    p.FechaInicio.ToString("dd/MM/yy") + " - " + p.FechaFin.ToString("dd/MM/yy"),
                    p.DiasTexto, s);
                DataGridViewRow r = tabla.Rows[fila];
                r.Tag = p;
                r.Cells[5].Style.ForeColor = s == "Vigente hoy" ? Color.ForestGreen
                    : s == "Vigente (otro día)" ? Color.DarkOrange
                    : s == "Programada" ? Color.RoyalBlue : Color.Gray;
                if (s == "Inactiva" || s == "Vencida")
                    r.DefaultCellStyle.ForeColor = Color.Gray;
            }
            tabla.ResumeLayout();
            tabla.ClearSelection();
            if (seleccionada != null)
            {
                foreach (DataGridViewRow r in tabla.Rows)
                {
                    if (((PromocionInfo)r.Tag).IdPromocion == seleccionada.IdPromocion)
                    {
                        r.Selected = true;
                        break;
                    }
                }
            }

            foreach (KeyValuePair<string, Guna2Button> b in botonesFiltro)
                b.Value.FillColor = b.Key == filtro ? Naranja : Dorado;
        }

        // ======================================================
        // DETALLE
        // ======================================================
        private void Seleccionar(PromocionInfo p)
        {
            if (p == null) return;
            if (editando && MessageBox.Show("Hay cambios sin guardar. ¿Descartarlos?", "Gestión de promociones",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            MostrarPromocion(p);
        }

        private void MostrarPromocion(PromocionInfo p)
        {
            seleccionada = p;
            editando = false;
            esNueva = false;

            txtNombre.Text = p == null ? "" : p.Nombre;
            txtDescripcion.Text = p == null ? "" : p.Descripcion;
            cmbTipo.SelectedIndex = p == null ? -1 : (p.TipoDescuento == "porcentaje" ? 0 : 1);
            txtValor.Text = p == null ? "" : p.Valor.ToString("0.##", CultureInfo.InvariantCulture);
            cmbAlcance.SelectedIndex = p == null ? -1 : (p.PorPlatos ? 0 : 1);
            txtMinimo.Text = p == null || !p.MontoMinimo.HasValue ? "" : p.MontoMinimo.Value.ToString("0.00", CultureInfo.InvariantCulture);
            dtInicio.Value = p == null ? DateTime.Today : p.FechaInicio;
            dtFin.Value = p == null ? DateTime.Today : p.FechaFin;
            for (int i = 0; i < 7; i++)
                chkDias[i].Checked = p == null || PromocionDatos.DiaPermitido(p.DiasSemana, DiaDeIndice(i));

            LlenarPlatos(p == null ? new List<int>() : p.Platos);

            if (p == null)
            {
                lblTituloDetalle.Text = "Detalle de la promoción";
                lblSituacion.Text = "Seleccione una promoción o registre una nueva.";
                lblSituacion.ForeColor = Color.DimGray;
            }
            else
            {
                string s = p.Situacion(DateTime.Today);
                lblTituloDetalle.Text = "Promoción N.º " + p.IdPromocion;
                lblSituacion.Text = s + " · usada en " + p.PedidosConPromocion + " pedido(s)" +
                    (p.Alcance == "general" ? " · alcance general" : "");
                lblSituacion.ForeColor = s == "Vigente hoy" ? Color.ForestGreen : s == "Programada" ? Color.RoyalBlue : Color.DimGray;
            }
            ActualizarModo();
        }

        // Fecha de referencia cuyo día ISO es i + 1 (2024-01-01 fue lunes).
        private static DateTime DiaDeIndice(int i)
        {
            return new DateTime(2024, 1, 1).AddDays(i);
        }

        private void LlenarPlatos(List<int> marcados)
        {
            lstPlatos.BeginUpdate();
            lstPlatos.Items.Clear();
            IEnumerable<ProductoMenu> lista = platos
                .Where(x => x.Activo || marcados.Contains(x.IdPlato))
                .OrderBy(x => x.Categoria).ThenBy(x => x.Nombre);
            foreach (ProductoMenu x in lista)
                lstPlatos.Items.Add(new ItemPlato(x), marcados.Contains(x.IdPlato));
            lstPlatos.EndUpdate();
        }

        private sealed class ItemPlato
        {
            public readonly ProductoMenu Plato;
            public ItemPlato(ProductoMenu p) { Plato = p; }
            public override string ToString()
            {
                return Plato.Nombre + "  ·  " + Plato.PrecioTexto + (Plato.Activo ? "" : "  (inactivo)");
            }
        }

        private void ActualizarPlatosHabilitados()
        {
            bool porPlatos = cmbAlcance.SelectedIndex == 0;
            lstPlatos.Enabled = editando && porPlatos;
            lblPlatos.Text = porPlatos ? "Platos participantes" : "Platos participantes (no aplica: todo el pedido)";
        }

        private void ActualizarModo()
        {
            bool hay = seleccionada != null;
            foreach (Guna2TextBox t in new[] { txtNombre, txtDescripcion, txtValor, txtMinimo })
                t.ReadOnly = !editando;
            cmbTipo.Enabled = cmbAlcance.Enabled = dtInicio.Enabled = dtFin.Enabled = editando;
            foreach (CheckBox c in chkDias) c.Enabled = editando;
            ActualizarPlatosHabilitados();

            btnGuardar.Enabled = editando;
            btnEditar.Enabled = editando || hay;
            btnEditar.Text = editando ? TextoCancelar : TextoEditar;
            btnEstado.Enabled = hay && !editando;
            btnEstado.Text = hay && !seleccionada.Activa ? "Activar" : "Desactivar";
            btnNueva.Enabled = !editando;
        }

        // ======================================================
        // OPERACIONES
        // ======================================================
        private int? Operador()
        {
            if (!SesionActual.IdEmpleado.HasValue || !Permisos.PuedeGestionarPromociones(SesionActual.Cargo))
            {
                MessageBox.Show("Su perfil no puede modificar promociones.", "Gestión de promociones",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return SesionActual.IdEmpleado.Value;
        }

        private void NuevaPromocion()
        {
            if (Operador() == null) return;
            MostrarPromocion(null);
            editando = true;
            esNueva = true;
            cmbTipo.SelectedIndex = 0;
            cmbAlcance.SelectedIndex = 0;
            dtFin.Value = DateTime.Today.AddMonths(1);
            lblTituloDetalle.Text = "Nueva promoción";
            lblSituacion.Text = "Complete los datos y pulse Guardar.";
            ActualizarModo();
            txtNombre.Focus();
        }

        private void Editar()
        {
            if (seleccionada == null || Operador() == null) return;
            editando = true;
            esNueva = false;
            ActualizarModo();
            txtNombre.Focus();
        }

        private void CancelarEdicion()
        {
            MostrarPromocion(esNueva ? null : seleccionada);
        }

        private static decimal LeerImporte(string texto, string campo)
        {
            decimal v;
            if (!decimal.TryParse((texto ?? "").Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out v))
                throw new ReglaNegocioException(campo + ": escriba un número, por ejemplo 10 o 5.50.");
            return v;
        }

        private void Guardar()
        {
            int? op = Operador();
            if (!op.HasValue || !editando) return;

            try
            {
                if (cmbTipo.SelectedIndex < 0) throw new ReglaNegocioException("Seleccione el tipo de descuento.");
                if (cmbAlcance.SelectedIndex < 0) throw new ReglaNegocioException("Seleccione a qué se aplica la promoción.");

                List<int> dias = new List<int>();
                for (int i = 0; i < 7; i++) if (chkDias[i].Checked) dias.Add(i + 1);

                string alcance = cmbAlcance.SelectedIndex == 0 ? "platos"
                    : (!esNueva && seleccionada.Alcance == "general" ? "general" : "pedido");

                PromocionInfo p = new PromocionInfo
                {
                    IdPromocion = esNueva ? 0 : seleccionada.IdPromocion,
                    Nombre = txtNombre.Text,
                    Descripcion = txtDescripcion.Text,
                    TipoDescuento = cmbTipo.SelectedIndex == 0 ? "porcentaje" : "monto",
                    Valor = LeerImporte(txtValor.Text, "El valor del descuento"),
                    Alcance = alcance,
                    FechaInicio = dtInicio.Value.Date,
                    FechaFin = dtFin.Value.Date,
                    DiasSemana = PromocionDatos.NormalizarDias(dias),
                    MontoMinimo = txtMinimo.Text.Trim().Length == 0 ? (decimal?)null : LeerImporte(txtMinimo.Text, "La compra mínima"),
                    Platos = lstPlatos.CheckedItems.Cast<ItemPlato>().Select(x => x.Plato.IdPlato).ToList()
                };

                if (!esNueva && seleccionada.PedidosConPromocion > 0 &&
                    MessageBox.Show("Esta promoción ya se usó en " + seleccionada.PedidosConPromocion + " pedido(s).\n\n" +
                        "Los pedidos registrados conservan sus importes; los cambios solo valen para nuevos pedidos. ¿Continuar?",
                        "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;

                int id = PromocionDatos.Guardar(p, op.Value);
                editando = false;
                MessageBox.Show("Promoción guardada: " + p.Nombre + " (" + p.DescuentoTexto + ", " + p.DiasTexto + ").",
                    "Gestión de promociones", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cargar(id);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de promociones", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Promociones", ex);
                MessageBox.Show("No se pudo guardar en SQL Server. No se registró ningún cambio.\n\n" + ex.Message,
                    "Gestión de promociones", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CambiarEstado()
        {
            PromocionInfo p = seleccionada;
            if (p == null) return;
            int? op = Operador();
            if (!op.HasValue) return;

            bool activar = !p.Activa;
            if (MessageBox.Show((activar ? "¿Activar " : "¿Desactivar ") + "la promoción \"" + p.Nombre + "\"?" +
                    (activar ? "" : "\n\nNo se borra: los pedidos que la usaron conservan sus importes."),
                    "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            try
            {
                PromocionDatos.CambiarEstado(p.IdPromocion, activar, op.Value);
                Cargar(p.IdPromocion);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de promociones", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Promociones", ex);
                MessageBox.Show("No se pudo cambiar el estado:\n" + ex.Message, "Gestión de promociones",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }
}
