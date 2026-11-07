using Guna.Charts.WinForms;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GESTIÓN DE REPORTES (FASE G)
    // ----------------------------------------------------------
    //  * Solo Administrador (Permisos + verificación al mostrarse):
    //    contiene información financiera.
    //  * Datos reales de SQL Server con consultas de solo lectura
    //    (ReporteDatos). Ventas = pedidos cobrados; los pagos
    //    mixtos se cuentan una sola vez por pago.
    //  * Filtros: período (hoy, semana, mes, rango), canal, método
    //    de pago, estado y agrupación por día / semana / mes.
    //  * Exportación de todos los reportes a Excel (.xlsx) y PDF.
    //  * El diseño conserva la barra lateral y el encabezado del
    //    diseñador; el contenido se construye aquí (como Promociones).
    // ==========================================================
    public partial class FRM_REPORTES : Form, IActualizableAlMostrar
    {
        private static readonly Color Dorado = Color.Gold;
        private static readonly Color Naranja = Color.Orange;
        private static readonly CultureInfo Peru = CultureInfo.GetCultureInfo("es-PE");

        private sealed class Reporte
        {
            public string Titulo;
            public Func<FiltroReporte, DataTable> Datos;
            public string Grafico;            // "barras" o "dona"
            public string ColumnaEtiqueta;    // columna que nombra cada barra / porción
            public string ColumnaValor;       // columna numérica del gráfico
        }

        private readonly List<Reporte> reportes = new List<Reporte>();
        private readonly List<Guna2Button> botonesReporte = new List<Guna2Button>();
        private Reporte actual;
        private DataTable tablaActual;
        private string periodo = "mes";

        private Guna2Button btnHoy, btnSemana, btnMes, btnRango, btnGenerar, btnExcel, btnPdf;
        private Guna2DateTimePicker dtDesde, dtHasta;
        private Guna2ComboBox cmbCanal, cmbMetodo, cmbEstado, cmbAgrupar;
        private Label lblVentas, lblPedidos, lblTicket, lblPropinas, lblDescuentos, lblMixtos;
        private Label lblTituloReporte, lblNota, lblFiltros;
        private Guna2DataGridView tabla;
        private GunaChart grafico;

        public FRM_REPORTES()
        {
            InitializeComponent();
            // ventana grande
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            // Encabezado propio (antes decía "GESTIÓN DE MESAS").
            guna2HtmlLabel3.Text = "GESTIÓN DE REPORTES";
            guna2HtmlLabel4.Text = "Ventas, ingresos, pedidos, clientes y reservas con datos reales";
            DisenoVentanas.PonerIcono(guna2PictureBox1, '', Color.Black);
            Text = "Gestión de reportes";

            DefinirReportes();
            CrearFiltros();
            CrearTarjetas();
            CrearCuerpo();
            ElegirPeriodo("mes");

            // Libera el calendario interno de los Guna2DateTimePicker (fuga de GDI/USER de Guna).
            CorreccionesGuna.Aplicar(this);

            Shown += (s, e) => { if (Autorizado()) Generar(); };

            // Diseño común: se adapta a la pantalla (1366x768 a 1920x1080 y escalado
            // de Windows) y el encabezado muestra la sesión y la hora.
            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);
        }

        // El Navegador reutiliza la ventana: al volver, datos frescos.
        public void ActualizarAlMostrar()
        {
            if (Autorizado())
                Generar();
        }

        // Información financiera: solo Administrador (además de Permisos).
        private bool Autorizado()
        {
            bool ok = SesionActual.Iniciada && SesionActual.Cargo == Cargos.Administrador;
            foreach (Control c in new Control[] { btnGenerar, btnExcel, btnPdf, tabla })
                c.Enabled = ok;
            if (!ok)
            {
                lblNota.Text = "Solo un Administrador puede consultar los reportes financieros.";
                tabla.DataSource = null;
                grafico.Datasets.Clear();
                grafico.Update();
            }
            return ok;
        }

        // ======================================================
        // REPORTES DISPONIBLES
        // ======================================================
        private void DefinirReportes()
        {
            reportes.Add(new Reporte { Titulo = "Ventas por período", Grafico = "barras", ColumnaEtiqueta = "Período", ColumnaValor = "Ventas (S/)",
                Datos = f => ReporteDatos.VentasPorPeriodo(f, cmbAgrupar == null ? "dia" : Agrupacion()) });
            reportes.Add(new Reporte { Titulo = "Ventas por canal", Grafico = "dona", ColumnaEtiqueta = "Canal", ColumnaValor = "Ventas (S/)", Datos = ReporteDatos.VentasPorCanal });
            reportes.Add(new Reporte { Titulo = "Ingresos por método de pago", Grafico = "dona", ColumnaEtiqueta = "Método", ColumnaValor = "Ingresos (S/)", Datos = IngresosPorMetodo });
            reportes.Add(new Reporte { Titulo = "Pagos mixtos", Grafico = "barras", ColumnaEtiqueta = "Pedido", ColumnaValor = "Total (S/)", Datos = ReporteDatos.PagosMixtos });
            reportes.Add(new Reporte { Titulo = "Pedidos por estado", Grafico = "barras", ColumnaEtiqueta = "Estado", ColumnaValor = "Pedidos", Datos = ReporteDatos.PedidosPorEstado });
            reportes.Add(new Reporte { Titulo = "Productos más vendidos", Grafico = "barras", ColumnaEtiqueta = "Producto", ColumnaValor = "Unidades", Datos = ReporteDatos.ProductosMasVendidos });
            reportes.Add(new Reporte { Titulo = "Cobros por empleado", Grafico = "barras", ColumnaEtiqueta = "Empleado", ColumnaValor = "Ingresos cobrados (S/)", Datos = ReporteDatos.CobrosPorEmpleado });
            reportes.Add(new Reporte { Titulo = "Promociones y descuentos", Grafico = "barras", ColumnaEtiqueta = "Promoción", ColumnaValor = "Descuento otorgado (S/)", Datos = ReporteDatos.Promociones });
            reportes.Add(new Reporte { Titulo = "Clientes frecuentes", Grafico = "barras", ColumnaEtiqueta = "Cliente", ColumnaValor = "Compras", Datos = ReporteDatos.ClientesFrecuentes });
            reportes.Add(new Reporte { Titulo = "Reservas", Grafico = "dona", ColumnaEtiqueta = "Estado", ColumnaValor = "Reservas", Datos = ReporteDatos.Reservas });
        }

        // Nombre del método legible (Efectivo, Yape...) en lugar del valor guardado.
        private static DataTable IngresosPorMetodo(FiltroReporte f)
        {
            DataTable t = ReporteDatos.IngresosPorMetodo(f);
            DataTable r = new DataTable();
            r.Columns.Add("Método", typeof(string));
            foreach (DataColumn c in t.Columns)
                if (c.ColumnName != "metodo") r.Columns.Add(c.ColumnName, c.DataType);
            foreach (DataRow fila in t.Rows)
            {
                DataRow n = r.NewRow();
                n["Método"] = ReporteDatos.NombreMetodo(Convert.ToString(fila["metodo"]));
                foreach (DataColumn c in t.Columns)
                    if (c.ColumnName != "metodo") n[c.ColumnName] = fila[c];
                r.Rows.Add(n);
            }
            return r;
        }

        // ======================================================
        // CONSTRUCCIÓN DE LA VENTANA (coordenadas del lienzo 1540x941)
        // ======================================================
        private Guna2Button Boton(string texto, Color color, Rectangle r)
        {
            Guna2Button b = new Guna2Button
            {
                Text = texto, Bounds = r, BorderRadius = 10, FillColor = color, ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand
            };
            b.HoverState.FillColor = Color.FromArgb(Math.Max(0, color.R - 20), Math.Max(0, color.G - 20), Math.Max(0, color.B - 20));
            Controls.Add(b);
            return b;
        }

        private Guna2ComboBox Combo(Rectangle r, string[] opciones)
        {
            Guna2ComboBox c = new Guna2ComboBox
            {
                Bounds = r, BorderRadius = 10, BorderColor = Color.Goldenrod, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F), ForeColor = Color.Black, ItemHeight = 30, BackColor = Color.Transparent
            };
            c.Items.AddRange(opciones);
            c.SelectedIndex = 0;
            Controls.Add(c);
            return c;
        }

        private Guna2DateTimePicker Fecha(Rectangle r)
        {
            Guna2DateTimePicker d = new Guna2DateTimePicker
            {
                Bounds = r, BorderRadius = 10, FillColor = Color.White, BorderColor = Color.Goldenrod, BorderThickness = 1,
                Format = DateTimePickerFormat.Short, Font = new Font("Segoe UI", 10F), ForeColor = Color.Black,
                Value = DateTime.Today, MaxDate = DateTime.Today.AddYears(1), MinDate = new DateTime(2020, 1, 1)
            };
            Controls.Add(d);
            return d;
        }

        private void Etiqueta(string texto, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = texto, Location = new Point(x, y), AutoSize = true, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.DimGray
            });
        }

        private void CrearFiltros()
        {
            // Fila 1: período, fechas, generar y exportar.
            btnHoy = Boton("Hoy", Dorado, new Rectangle(351, 146, 100, 40));
            btnSemana = Boton("Esta semana", Dorado, new Rectangle(457, 146, 124, 40));
            btnMes = Boton("Este mes", Dorado, new Rectangle(587, 146, 112, 40));
            btnRango = Boton("Rango", Dorado, new Rectangle(705, 146, 96, 40));
            dtDesde = Fecha(new Rectangle(817, 146, 160, 40));
            dtHasta = Fecha(new Rectangle(983, 146, 160, 40));
            btnGenerar = Boton("Generar", Naranja, new Rectangle(1153, 146, 120, 40));
            btnExcel = Boton("Excel", Color.FromArgb(120, 200, 120), new Rectangle(1281, 146, 120, 40));
            btnPdf = Boton("PDF", Color.FromArgb(240, 120, 110), new Rectangle(1409, 146, 120, 40));

            // Fila 2: filtros.
            Etiqueta("Canal", 351, 192);
            cmbCanal = Combo(new Rectangle(351, 210, 200, 36), new[] { "Todos los canales", "Salón", "Delivery", "Recojo" });
            Etiqueta("Método de pago", 563, 192);
            cmbMetodo = Combo(new Rectangle(563, 210, 200, 36), new[] { "Todos los métodos", "Efectivo", "Yape", "Plin", "Tarjeta", "Transferencia" });
            Etiqueta("Estado del pedido", 775, 192);
            cmbEstado = Combo(new Rectangle(775, 210, 200, 36), new[] { "Todos los estados", "Pendiente", "En preparación", "Listo", "Entregado", "Cobrado", "Cancelado" });
            Etiqueta("Agrupar ventas por", 987, 192);
            cmbAgrupar = Combo(new Rectangle(987, 210, 200, 36), new[] { "Día", "Semana", "Mes" });
            lblFiltros = new Label
            {
                Location = new Point(1199, 212), Size = new Size(330, 34), BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = Color.DimGray, Text = ""
            };
            Controls.Add(lblFiltros);

            btnHoy.Click += (s, e) => { ElegirPeriodo("hoy"); Generar(); };
            btnSemana.Click += (s, e) => { ElegirPeriodo("semana"); Generar(); };
            btnMes.Click += (s, e) => { ElegirPeriodo("mes"); Generar(); };
            btnRango.Click += (s, e) => { ElegirPeriodo("rango"); dtDesde.Focus(); };
            btnGenerar.Click += (s, e) => Generar();
            btnExcel.Click += (s, e) => Exportar("xlsx");
            btnPdf.Click += (s, e) => Exportar("pdf");
            dtDesde.ValueChanged += (s, e) => { if (periodo != "rango") ElegirPeriodo("rango"); };
            dtHasta.ValueChanged += (s, e) => { if (periodo != "rango") ElegirPeriodo("rango"); };
            foreach (Guna2ComboBox c in new[] { cmbCanal, cmbMetodo, cmbEstado, cmbAgrupar })
                c.SelectedIndexChanged += (s, e) => { if (Visible) Generar(); };
        }

        private void CrearTarjetas()
        {
            string[] titulos = { "Ventas cobradas", "Pedidos cobrados", "Ticket promedio", "Propinas", "Descuentos otorgados", "Pagos mixtos" };
            Color[] colores = { Color.SpringGreen, Color.LightSteelBlue, Color.Khaki, Color.PaleTurquoise, Color.PaleVioletRed, Color.NavajoWhite };
            Label[] valores = new Label[6];
            for (int i = 0; i < 6; i++)
            {
                Guna2Panel p = new Guna2Panel { Bounds = new Rectangle(351 + i * 198, 258, 188, 72), BorderRadius = 12, FillColor = colores[i], BackColor = Color.Transparent };
                p.Controls.Add(new Label
                {
                    Text = titulos[i], Location = new Point(12, 6), AutoSize = true, BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
                });
                valores[i] = new Label
                {
                    Text = "-", Location = new Point(12, 30), Size = new Size(170, 32), BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 15F, FontStyle.Bold | FontStyle.Italic)
                };
                p.Controls.Add(valores[i]);
                Controls.Add(p);
            }
            lblVentas = valores[0]; lblPedidos = valores[1]; lblTicket = valores[2];
            lblPropinas = valores[3]; lblDescuentos = valores[4]; lblMixtos = valores[5];
        }

        private void CrearCuerpo()
        {
            // Lista de reportes (izquierda).
            Guna2Panel lista = new Guna2Panel { Bounds = new Rectangle(351, 340, 210, 595), BorderRadius = 12, FillColor = Color.WhiteSmoke, BackColor = Color.Transparent };
            Controls.Add(lista);
            for (int i = 0; i < reportes.Count; i++)
            {
                Reporte r = reportes[i];
                Guna2Button b = new Guna2Button
                {
                    Text = r.Titulo, Bounds = new Rectangle(8, 8 + i * 58, 194, 52), BorderRadius = 10,
                    FillColor = Color.White, ForeColor = Color.Black, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    TextAlign = HorizontalAlignment.Left, TextOffset = new Point(4, 0), Cursor = Cursors.Hand
                };
                b.HoverState.FillColor = Color.LemonChiffon;
                b.Click += (s, e) => Mostrar(r);
                lista.Controls.Add(b);
                botonesReporte.Add(b);
            }

            // Tabla (centro).
            Guna2Panel cabecera = new Guna2Panel { Bounds = new Rectangle(571, 340, 560, 40), BorderRadius = 12, FillColor = Dorado, BackColor = Color.Transparent };
            lblTituloReporte = new Label
            {
                Location = new Point(16, 9), AutoSize = true, BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold), Text = "Reporte"
            };
            cabecera.Controls.Add(lblTituloReporte);
            Controls.Add(cabecera);

            tabla = new Guna2DataGridView
            {
                Bounds = new Rectangle(571, 386, 560, 520),
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true, MultiSelect = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            Controls.Add(tabla);
            EstiloTablas.Configurar(tabla, 36, 30);
            tabla.ThemeStyle.HeaderStyle.BackColor = Dorado;
            tabla.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
            tabla.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            tabla.ColumnHeadersHeight = 44;
            tabla.ThemeStyle.HeaderStyle.Height = 44;
            tabla.CellDoubleClick += (s, e) => VerHistorialCliente(e.RowIndex);

            lblNota = new Label
            {
                Location = new Point(571, 910), Size = new Size(560, 24), BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = Color.DimGray
            };
            Controls.Add(lblNota);

            // Gráfico (derecha).
            Guna2Panel marco = new Guna2Panel { Bounds = new Rectangle(1141, 340, 388, 595), BorderRadius = 12, FillColor = Color.White, BorderColor = Color.Gainsboro, BorderThickness = 1, BackColor = Color.Transparent };
            grafico = new GunaChart { Bounds = new Rectangle(8, 8, 372, 579), BackColor = Color.White };
            grafico.Legend.Display = true;
            grafico.Title.Display = false;
            marco.Controls.Add(grafico);
            Controls.Add(marco);
        }

        // ======================================================
        // FILTROS
        // ======================================================
        private void ElegirPeriodo(string p)
        {
            periodo = p;
            DateTime hoy = DateTime.Today;
            if (p == "hoy") { dtDesde.Value = hoy; dtHasta.Value = hoy; }
            else if (p == "semana") { dtDesde.Value = hoy.AddDays(-(((int)hoy.DayOfWeek + 6) % 7)); dtHasta.Value = hoy; }
            else if (p == "mes") { dtDesde.Value = new DateTime(hoy.Year, hoy.Month, 1); dtHasta.Value = hoy; }
            periodo = p;   // ValueChanged pudo cambiarlo a "rango"
            foreach (Guna2Button b in new[] { btnHoy, btnSemana, btnMes, btnRango })
                b.FillColor = Dorado;
            Guna2Button activo = p == "hoy" ? btnHoy : p == "semana" ? btnSemana : p == "mes" ? btnMes : btnRango;
            activo.FillColor = Naranja;
        }

        private string Agrupacion()
        {
            switch (cmbAgrupar.SelectedIndex) { case 1: return "semana"; case 2: return "mes"; default: return "dia"; }
        }

        private FiltroReporte Filtro()
        {
            string[] canales = { null, "salon", "delivery", "recojo" };
            string[] metodos = { null, "efectivo", "yape", "plin", "tarjeta", "transferencia" };
            string[] estados = { null, "pendiente", "en_preparacion", "listo", "entregado", "cobrado", "cancelado" };
            DateTime d = dtDesde.Value.Date, h = dtHasta.Value.Date;
            if (h < d) { DateTime x = d; d = h; h = x; }
            return new FiltroReporte
            {
                Desde = d, Hasta = h,
                Canal = canales[Math.Max(0, cmbCanal.SelectedIndex)],
                Metodo = metodos[Math.Max(0, cmbMetodo.SelectedIndex)],
                Estado = estados[Math.Max(0, cmbEstado.SelectedIndex)]
            };
        }

        // ======================================================
        // GENERAR Y MOSTRAR
        // ======================================================
        private void Generar()
        {
            if (!Autorizado())
                return;
            FiltroReporte f = Filtro();
            lblFiltros.Text = f.Descripcion();
            try
            {
                Cursor = Cursors.WaitCursor;
                ResumenReporte r = ReporteDatos.Resumen(f);
                lblVentas.Text = "S/ " + r.Ventas.ToString("N2", Peru);
                lblPedidos.Text = r.PedidosCobrados.ToString("D2");
                lblTicket.Text = "S/ " + r.TicketPromedio.ToString("N2", Peru);
                lblPropinas.Text = "S/ " + r.Propinas.ToString("N2", Peru);
                lblDescuentos.Text = "S/ " + r.Descuentos.ToString("N2", Peru);
                lblMixtos.Text = r.PagosMixtos.ToString("D2");
                Mostrar(actual ?? reportes[0]);
                lblNota.Text = "Ventas = pedidos cobrados. " + r.PedidosPendientes +
                               " pedido(s) del período aún no se cobran y no se cuentan como venta.";
            }
            catch (Exception ex)
            {
                Registro.Error("Reportes", ex);
                MessageBox.Show("No se pudo generar el reporte:\n" + ex.Message, "Gestión de reportes",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void Mostrar(Reporte r)
        {
            if (!Autorizado())
                return;
            actual = r;
            for (int i = 0; i < reportes.Count; i++)
                botonesReporte[i].FillColor = reportes[i] == r ? Dorado : Color.White;
            lblTituloReporte.Text = r.Titulo;

            try
            {
                tablaActual = r.Datos(Filtro());
            }
            catch (Exception ex)
            {
                Registro.Error("Reportes", ex);
                MessageBox.Show("No se pudo consultar \"" + r.Titulo + "\":\n" + ex.Message, "Gestión de reportes",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            tabla.DataSource = null;
            tabla.Columns.Clear();
            tabla.AutoGenerateColumns = true;
            tabla.DataSource = tablaActual;
            foreach (DataGridViewColumn c in tabla.Columns)
            {
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                Type tipo = tablaActual.Columns[c.DataPropertyName].DataType;
                bool numero = tipo == typeof(decimal) || tipo == typeof(int) || tipo == typeof(long) || tipo == typeof(double);
                if (tipo == typeof(decimal)) { c.DefaultCellStyle.Format = "N2"; c.DefaultCellStyle.FormatProvider = Peru; }
                c.DefaultCellStyle.Alignment = numero ? DataGridViewContentAlignment.MiddleRight : DataGridViewContentAlignment.MiddleLeft;
                c.HeaderCell.Style.Alignment = c.DefaultCellStyle.Alignment;
                c.MinimumWidth = 50;
                c.FillWeight = numero ? 70 : 130;
            }
            tabla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            if (r.Titulo == "Clientes frecuentes")
                lblNota.Text = "Doble clic en un cliente para ver su historial de compras.";

            DibujarGrafico(r, tablaActual);
        }

        private static readonly Color[] Paleta =
        {
            Color.Gold, Color.CornflowerBlue, Color.LightCoral, Color.MediumSeaGreen, Color.Orange,
            Color.Plum, Color.SkyBlue, Color.Khaki, Color.LightSalmon, Color.MediumAquamarine
        };

        private void DibujarGrafico(Reporte r, DataTable t)
        {
            grafico.Datasets.Clear();
            if (t != null && t.Rows.Count > 0 && t.Columns.Contains(r.ColumnaEtiqueta) && t.Columns.Contains(r.ColumnaValor))
            {
                IEnumerable<DataRow> filas = t.Rows.Cast<DataRow>().Take(10);
                if (r.Grafico == "dona")
                {
                    GunaDoughnutDataset d = new GunaDoughnutDataset { Label = r.ColumnaValor };
                    int i = 0;
                    foreach (DataRow f in filas)
                    {
                        d.DataPoints.Add(new LPoint { Label = Convert.ToString(f[r.ColumnaEtiqueta]), Y = Convert.ToDouble(f[r.ColumnaValor]) });
                        d.FillColors.Add(Paleta[i++ % Paleta.Length]);
                    }
                    d.TargetChart = grafico;
                    grafico.Datasets.Add(d);
                    grafico.XAxes.Display = false;
                    grafico.YAxes.Display = false;
                    grafico.Legend.Display = true;
                }
                else
                {
                    GunaBarDataset b = new GunaBarDataset { Label = r.ColumnaValor };
                    foreach (DataRow f in filas)
                    {
                        string etiqueta = Convert.ToString(f[r.ColumnaEtiqueta]);
                        if (etiqueta.Length > 16) etiqueta = etiqueta.Substring(0, 15) + "…";
                        b.DataPoints.Add(new LPoint { Label = etiqueta, Y = Convert.ToDouble(f[r.ColumnaValor]) });
                    }
                    b.FillColors.Add(Color.Gold);
                    b.TargetChart = grafico;
                    grafico.Datasets.Add(b);
                    grafico.XAxes.Display = true;
                    grafico.YAxes.Display = true;
                    grafico.Legend.Display = false;
                }
            }
            grafico.Update();
        }

        // Historial de compras de un cliente (reporte de clientes frecuentes).
        private void VerHistorialCliente(int fila)
        {
            if (fila < 0 || actual == null || actual.Titulo != "Clientes frecuentes" || tablaActual == null)
                return;
            string documento = Convert.ToString(tablaActual.Rows[fila]["Documento"]);
            ClienteInfo c = ClienteDatos.Listar().FirstOrDefault(x => x.Documento == documento);
            if (c == null)
                return;
            DataTable h = ReporteDatos.HistorialCliente(c.IdCliente);
            using (Form v = new Form())
            {
                v.Text = "Historial de compras";
                v.ClientSize = new Size(620, 420);
                v.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                v.StartPosition = FormStartPosition.CenterParent;
                v.BackColor = Color.White;
                v.ShowInTaskbar = false;
                Guna2Panel cab = new Guna2Panel { Dock = DockStyle.Top, Height = 48, FillColor = Dorado };
                cab.Controls.Add(new Label
                {
                    Text = c.NombreCompleto.ToUpperInvariant() + " · " + h.Rows.Count + " pedido(s)", Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11F, FontStyle.Bold), BackColor = Color.Transparent
                });
                Guna2DataGridView g = new Guna2DataGridView
                {
                    Bounds = new Rectangle(14, 60, 592, 346), ReadOnly = true, AllowUserToAddRows = false,
                    RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, DataSource = h
                };
                v.Controls.Add(g);
                v.Controls.Add(cab);
                EstiloTablas.Configurar(g, 34, 28);
                g.ThemeStyle.HeaderStyle.BackColor = Dorado;
                v.Shown += (s, e) => { foreach (DataGridViewColumn col in g.Columns) if (col.ValueType == typeof(decimal)) col.DefaultCellStyle.Format = "N2"; };
                v.ShowDialog(this);
            }
        }

        // ======================================================
        // EXPORTAR (todos los reportes con los filtros actuales)
        // ======================================================
        private void Exportar(string tipo)
        {
            if (!Autorizado())
                return;
            FiltroReporte f = Filtro();
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "Exportar reportes";
                d.Filter = tipo == "pdf" ? "Documento PDF (*.pdf)|*.pdf" : "Libro de Excel (*.xlsx)|*.xlsx";
                d.FileName = "Reportes_AlEstiloPiurano_" + f.Desde.ToString("yyyyMMdd") + "_" + f.Hasta.ToString("yyyyMMdd") + "." + tipo;
                d.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (d.ShowDialog(this) != DialogResult.OK)
                    return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    ExportarA(d.FileName, tipo, f);
                }
                catch (Exception ex)
                {
                    Registro.Error("Reportes", ex);
                    MessageBox.Show("No se pudo exportar:\n" + ex.Message, "Gestión de reportes", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
                if (MessageBox.Show("Reportes exportados en:\n" + d.FileName + "\n\n¿Abrir el archivo?", "Gestión de reportes",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    try { Process.Start(new ProcessStartInfo(d.FileName) { UseShellExecute = true }); } catch { }
                }
            }
        }

        // También lo usan las pruebas automáticas (sin diálogos).
        internal void ExportarA(string ruta, string tipo, FiltroReporte f)
        {
            ResumenReporte r = ReporteDatos.Resumen(f);
            DataTable resumen = new DataTable();
            resumen.Columns.Add("Indicador", typeof(string));
            resumen.Columns.Add("Valor", typeof(string));
            resumen.Rows.Add("Ventas cobradas (S/)", r.Ventas.ToString("N2", Peru));
            resumen.Rows.Add("Pedidos cobrados", r.PedidosCobrados.ToString());
            resumen.Rows.Add("Ticket promedio (S/)", r.TicketPromedio.ToString("N2", Peru));
            resumen.Rows.Add("Propinas (S/)", r.Propinas.ToString("N2", Peru));
            resumen.Rows.Add("Descuentos otorgados (S/)", r.Descuentos.ToString("N2", Peru));
            resumen.Rows.Add("Pedidos con pago mixto", r.PagosMixtos.ToString());
            resumen.Rows.Add("Pedidos sin cobrar (no son venta)", r.PedidosPendientes.ToString());
            resumen.Rows.Add("Reservas del período", r.Reservas.ToString());

            List<SeccionReporte> secciones = new List<SeccionReporte> { new SeccionReporte { Titulo = "Resumen", Tabla = resumen } };
            foreach (Reporte rep in reportes)
                secciones.Add(new SeccionReporte { Titulo = rep.Titulo, Tabla = rep.Datos(f) });

            string titulo = "Reporte de gestión";
            string filtros = "Período: " + f.Descripcion() + " · Generado por " + (SesionActual.NombreCompleto ?? "-");
            if (tipo == "pdf")
                ExportadorReportes.Pdf(ruta, titulo, filtros, secciones);
            else
                ExportadorReportes.Excel(ruta, titulo, filtros, secciones);
            Registro.Info("Reportes", "Exportados (" + tipo + ") por empleado " + SesionActual.IdEmpleado + ": " + f.Descripcion());
        }

        // ======================================================
        // EVENTOS DEL DISEÑADOR (no borrar)
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
