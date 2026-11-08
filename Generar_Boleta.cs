using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZXing;

namespace PROYECTO_RESTAURANTE
{
    public partial class Generar_Boleta : Form
    {



        // Número que muestran la vista previa, el QR y la impresión:
        // "PEDIDO-000021" antes del cobro y el número del recibo
        // guardado (R001-00000001) después del cobro o al reimprimir.
        private string numeroBoletaDemo = "";

        // ==================================================
        // RECIBO INTERNO Y DATOS DEL CLIENTE (aprobado)
        // ==================================================
        // Se guarda en dbo.COMPROBANTE con la serie interna R001 (no es
        // la serie B001 de una boleta electrónica SUNAT). Un recibo por
        // pedido (UQ_COMPROBANTE_PEDIDO) y número único por serie.
        private const string SerieRecibo = "R001";
        // SUNAT exige DNI/CE y nombre en boletas electrónicas > S/ 700
        // (R.S. 303-2018). Aquí solo se avisa: el recibo es interno.
        private const decimal MontoIdentificacionSunat = 700m;
        private const string NotaInterna = "Recibo interno · No es comprobante electrónico SUNAT";

        // Número del pedido (formato PEDIDO-000019): va bajo el código de barras.
        // El número interno del recibo (R001-...) se muestra aparte, bajo el título.
        private string CodigoPedido { get { return "PEDIDO-" + idPedido.ToString("D6"); } }

        // Importes con el mismo formato en todo el recibo (S/ 1,234.50).
        private static readonly System.Globalization.CultureInfo Peru = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
        private static string Monto(decimal v) { return v.ToString("N2", Peru); }

        private string numeroRecibo;               // R001-00000001
        private int? idClienteSeleccionado;        // cliente registrado (se vincula al pedido)

        // Datos guardados en el pedido: quién reservó (reserva de origen) y
        // el cliente registrado del pedido. Se ofrecen como comprador, pero
        // el comprador del recibo puede ser otra persona.
        private int? idReservaPedido;
        private string contactoPedido, telefonoPedido;
        private int? idClientePedido;
        private string documentoClientePedido, nombreClientePedido, telefonoClientePedido;
        private string documentoClienteRegistrado;
        private bool cargandoCliente;
        private string formaPagoGuardada;          // reimpresión
        private string cajeroRecibo;               // quien cobró
        private Guna2CheckBox chkIncluirCliente;
        private Guna2Button btnBuscarCliente;
        private Label lblPrevCliente;
        private Label lblPrevDocumento;
        private Dictionary<Control, int> topsVistaPrevia;
        private readonly ToolTip consejosBoleta = new ToolTip();

        // true: abrir un pedido ya cobrado solo para reimprimir su recibo.
        public bool ModoReimpresion { get; set; }
        private readonly string cadenaConexion =
            BaseDatos.CadenaConexion;

        // Datos del pedido seleccionado
        private int idPedido = 0;
        private DateTime fechaPedido;
        private string tipoPedido = "";
        private string numeroMesa = "";
        private string atendidoPor = "";
        private string estadoPedido = "";

        // Totales: los precios de los productos incluyen IGV
        private decimal subtotal = 0m;
        private decimal descuentoGeneral = 0m;
        private decimal descuentoTotal = 0m;
        // Nombre de la promoción del pedido (solo para mostrar; el importe
        // del descuento ya está guardado en el pedido).
        private string promocionPedido;
        private decimal propina = 0m;
        private decimal igvIncluido = 0m;
        private decimal total = 0m;

        private readonly List<LineaBoleta> productos = new List<LineaBoleta>();

        private class LineaBoleta
        {
            public int Cantidad { get; set; }
            public string Descripcion { get; set; }
            public decimal PrecioUnitario { get; set; }
            public decimal DescuentoLinea { get; set; }
            public decimal Importe
            {
                // Descuento POR UNIDAD (igual que V_PEDIDO_TOTAL y RN-G).
                get { return Cantidad * (PrecioUnitario - DescuentoLinea); }
            }
        }


        public Generar_Boleta()
        {
            InitializeComponent();
            // Evitar ejecutar configuraciones en el diseñador.
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            // Orden de la ventana (coordenadas de diseño) y adaptación a la
            // pantalla: antes de conectar Shown, así se escala antes de cargar.
            OrganizarVentana();
            AjustarAPantalla();
            DisenoVentanas.Adaptar(this, new Size(AnchoDiseno, AltoDiseno));
            Shown += (s, e) => RecentrarTodo();
            // =====================================
            // CONFIGURAR FORMAS DE PAGO
            // =====================================

            rbPagoYape.CheckedChanged -=
                rbPagoTarjeta_CheckedChanged;

            rbPagoMixto.CheckedChanged -=
                rbPagoTarjeta_CheckedChanged;

            rbPagoTransferencia.CheckedChanged -=
                rbPagoTarjeta_CheckedChanged;

            rbPagoYape.CheckedChanged +=
                rbPagoTarjeta_CheckedChanged;

            rbPagoMixto.CheckedChanged +=
                rbPagoTarjeta_CheckedChanged;

            rbPagoTransferencia.CheckedChanged +=
                rbPagoTarjeta_CheckedChanged;

            // =====================================
            // CONFIGURAR COPIAS
            // =====================================

            numCopiasBoleta.Minimum = 1;
            numCopiasBoleta.Value = 1;

            // =====================================
            // TABLAS: proporciones y textos legibles
            // =====================================

            AjustarTablasBoleta();

            // =====================================
            // CONFIGURACIÓN ORIGINAL
            // =====================================



            this.Shown += Generar_Boleta_Shown;

            ConfigurarMetodosPagoMixto();
            // El Designer ya conecta btnRegistrarCobro_Click.
            // Quitar y volver a agregar deja UNA sola conexión,
            // aunque el Designer cambie en el futuro.
            btnRegistrarCobro.Click -= btnRegistrarCobro_Click;
            btnRegistrarCobro.Click += btnRegistrarCobro_Click;

            // El texto "Cobrar" está sobre el botón.
            // También debe responder al clic (una sola vez).
            guna2HtmlLabel22.Click -= btnRegistrarCobro_Click;
            guna2HtmlLabel22.Click += btnRegistrarCobro_Click;

            CrearDatosCliente();
            FormClosed += (s, e) => consejosBoleta.Dispose();
        }

        // ==================================================
        // DATOS DEL CLIENTE (opcionales)
        // ==================================================
        // Usa los campos del diseño (txtDNICliente, txtNombreCliente,
        // txtCelularCliente) y agrega la casilla y el botón Buscar.
        private void CrearDatosCliente()
        {
            Control panel = txtDNICliente.Parent;
            guna2HtmlLabel9.Text = "Datos del cliente";

            chkIncluirCliente = new Guna2CheckBox
            {
                Text = "Incluir en el recibo",
                AutoSize = true,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(220, 196)
            };
            chkIncluirCliente.CheckedState.FillColor = Color.Goldenrod;
            chkIncluirCliente.CheckedState.BorderColor = Color.Goldenrod;
            chkIncluirCliente.UncheckedState.BorderColor = Color.Goldenrod;
            chkIncluirCliente.UncheckedState.BorderThickness = 1;
            panel.Controls.Add(chkIncluirCliente);
            chkIncluirCliente.BringToFront();
            consejosBoleta.SetToolTip(chkIncluirCliente,
                "Incluir datos del cliente en el recibo (opcional). Sin marcar, el recibo sale sin nombre.");

            int derecha = txtDNICliente.Right;
            txtDNICliente.Width = 232;
            btnBuscarCliente = new Guna2Button
            {
                Text = "Buscar",
                BorderRadius = 10,
                FillColor = Color.Gold,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            panel.Controls.Add(btnBuscarCliente);
            btnBuscarCliente.Bounds = new Rectangle(txtDNICliente.Right + 8, txtDNICliente.Top,
                derecha - txtDNICliente.Right - 8, txtDNICliente.Height);
            consejosBoleta.SetToolTip(btnBuscarCliente, "Buscar un cliente registrado en Gestión de Clientes.");
            EstiloBotones.AplicarA(btnBuscarCliente);

            txtDNICliente.PlaceholderText = "DNI o CE (opcional)";
            txtDNICliente.MaxLength = 12;
            txtNombreCliente.PlaceholderText = "Nombre completo del cliente";
            txtNombreCliente.MaxLength = 120;
            txtCelularCliente.MaxLength = 20;

            txtDNICliente.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsLetterOrDigit(e.KeyChar))
                    e.Handled = true;
            };
            txtDNICliente.TextChanged += (s, e) => { if (!cargandoCliente) BuscarClientePorDocumento(); };
            txtNombreCliente.TextChanged += (s, e) =>
            {
                if (cargandoCliente) return;
                if (txtNombreCliente.Text.Trim().Length > 0 && !chkIncluirCliente.Checked)
                    chkIncluirCliente.Checked = true;      // escribir el nombre = incluirlo
                MostrarClienteEnVistaPrevia();
            };
            chkIncluirCliente.CheckedChanged += (s, e) => MostrarClienteEnVistaPrevia();
            btnBuscarCliente.Click += (s, e) => SeleccionarClienteRegistrado();
        }

        // Recupera los datos ya conocidos del pedido (cliente registrado o
        // quien reservó) para no volver a escribirlos. La casilla queda SIN
        // marcar: solo se incluyen si el comprador acepta figurar en el
        // recibo. Se pueden corregir o reemplazar por otro comprador.
        private void PrecargarComprador()
        {
            string origen = idReservaPedido.HasValue ? "reserva N.º " + idReservaPedido.Value : "pedido";

            if (idClientePedido.HasValue)
            {
                AsignarCliente(idClientePedido.Value, documentoClientePedido, nombreClientePedido,
                    string.IsNullOrEmpty(telefonoClientePedido) ? telefonoPedido : telefonoClientePedido, false);
            }
            else if (!string.IsNullOrEmpty(contactoPedido))
            {
                cargandoCliente = true;
                try
                {
                    txtNombreCliente.Text = contactoPedido;
                    txtCelularCliente.Text = telefonoPedido ?? "";
                    txtDNICliente.Text = "";
                    chkIncluirCliente.Checked = false;
                }
                finally
                {
                    cargandoCliente = false;
                }
            }
            else
            {
                return;
            }

            // El título no cambia (la casilla está en la misma fila); el origen
            // de los datos se indica en la ayuda de la casilla y del nombre.
            string ayuda = "Datos recuperados de" + (idReservaPedido.HasValue ? " la " : "l ") + origen +
                ". Marque «Incluir en el recibo» si el comprador acepta figurar en él. " +
                "Puede corregirlos, completar el documento o buscar otro comprador.";
            consejosBoleta.SetToolTip(chkIncluirCliente, ayuda);
            consejosBoleta.SetToolTip(txtNombreCliente, ayuda);
            MostrarClienteEnVistaPrevia();
        }

        private bool IncluirCliente()
        {
            return chkIncluirCliente != null && chkIncluirCliente.Checked &&
                   txtNombreCliente.Text.Trim().Length > 0;
        }

        // "DNI", "CE", "RUC" o "Doc." según el número (misma regla que Clientes).
        private static string EtiquetaDocumento(string documento)
        {
            switch (ClienteDatos.TipoDocumento(documento))
            {
                case "DNI": return "DNI";
                case "CE": return "CE";
                case "RUC": return "RUC";
                default: return "Doc.";
            }
        }

        // Al escribir un documento de un cliente registrado, completa sus datos.
        private void BuscarClientePorDocumento()
        {
            string doc = txtDNICliente.Text.Trim().ToUpperInvariant();

            if (idClienteSeleccionado.HasValue && doc != documentoClienteRegistrado)
            {
                idClienteSeleccionado = null;      // cambió el documento: ya no es ese cliente
                documentoClienteRegistrado = null;
                consejosBoleta.SetToolTip(txtNombreCliente, "");
            }

            if (doc.Length >= 8 && !idClienteSeleccionado.HasValue)
            {
                try
                {
                    using (SqlConnection cx = new SqlConnection(cadenaConexion))
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP (1) id_cliente, nombre + ' ' + apellidos, ISNULL([teléfono], '')
                        FROM dbo.CLIENTE WHERE documento = @Doc AND estado = 'activo';", cx))
                    {
                        cmd.CommandTimeout = 10;
                        cmd.Parameters.Add("@Doc", SqlDbType.VarChar, 20).Value = doc;
                        cx.Open();
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                                AsignarCliente(r.GetInt32(0), doc, r.GetString(1), r.GetString(2));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Registro.Error("Boleta", ex);
                }
            }
            MostrarClienteEnVistaPrevia();
        }

        private void AsignarCliente(int idCliente, string documento, string nombre, string telefono, bool incluir = true)
        {
            cargandoCliente = true;
            try
            {
                idClienteSeleccionado = idCliente;
                documentoClienteRegistrado = (documento ?? "").Trim().ToUpperInvariant();
                txtDNICliente.Text = documentoClienteRegistrado;
                txtNombreCliente.Text = nombre;
                if (!string.IsNullOrEmpty(telefono)) txtCelularCliente.Text = telefono;
                chkIncluirCliente.Checked = incluir;
                consejosBoleta.SetToolTip(txtNombreCliente, "Cliente registrado (C" + idCliente.ToString("D4") + "): el pedido quedará asociado a su historial.");
            }
            finally
            {
                cargandoCliente = false;
            }
            MostrarClienteEnVistaPrevia();
        }

        // Ventana para elegir un cliente registrado (Gestión de Clientes).
        private void SeleccionarClienteRegistrado()
        {
            List<ClienteInfo> clientes;
            try
            {
                clientes = ClienteDatos.Listar().Where(c => c.Activo).ToList();
            }
            catch (Exception ex)
            {
                Registro.Error("Boleta", ex);
                MessageBox.Show(this, "No se pudieron cargar los clientes:\n" + ex.Message, "Buscar cliente",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ClienteInfo elegido = SelectorClientes.Elegir(this, clientes, "Buscar cliente registrado", null, "Cancelar");
            if (elegido != null)
                AsignarCliente(elegido.IdCliente, elegido.Documento, elegido.NombreCompleto, elegido.Telefono);
        }

        // Renglones "Cliente" y documento en la vista previa: se insertan
        // debajo de los datos del pedido y desplazan lo que sigue.
        private void MostrarClienteEnVistaPrevia()
        {
            Control referencia = lblBoletaAtendidoPor;
            Control padre = referencia.Parent;
            if (padre == null) return;

            if (lblPrevCliente == null)
            {
                Font f = lblBoletaFecha.Font;
                lblPrevCliente = new Label { AutoSize = true, BackColor = Color.Transparent, Font = f, ForeColor = Color.Black };
                lblPrevDocumento = new Label { AutoSize = true, BackColor = Color.Transparent, Font = f, ForeColor = Color.Black };
                topsVistaPrevia = new Dictionary<Control, int>();
                foreach (Control c in padre.Controls)
                    topsVistaPrevia[c] = c.Top;
                padre.Controls.Add(lblPrevCliente);
                padre.Controls.Add(lblPrevDocumento);
            }

            int baseY = Math.Max(referencia.Bottom, guna2HtmlLabel33.Bottom) + 4;
            int izquierda = guna2HtmlLabel33.Left;
            int ancho = Math.Max(120, padre.ClientSize.Width - izquierda - 24);

            bool mostrar = IncluirCliente();
            string doc = txtDNICliente.Text.Trim();
            int alto = 0;
            padre.SuspendLayout();
            if (mostrar)
            {
                lblPrevCliente.MaximumSize = new Size(ancho, 0);
                lblPrevCliente.Text = "Cliente: " + txtNombreCliente.Text.Trim();
                lblPrevCliente.Location = new Point(izquierda, baseY);
                alto = lblPrevCliente.PreferredSize.Height + 2;
                lblPrevDocumento.Visible = doc.Length > 0;
                if (doc.Length > 0)
                {
                    lblPrevDocumento.Text = EtiquetaDocumento(doc) + ": " + doc;
                    lblPrevDocumento.Location = new Point(izquierda, baseY + alto);
                    alto += lblPrevDocumento.PreferredSize.Height + 2;
                }
                alto += 4;
            }
            lblPrevCliente.Visible = mostrar;
            if (!mostrar) lblPrevDocumento.Visible = false;

            foreach (KeyValuePair<Control, int> par in topsVistaPrevia)
            {
                if (par.Key.IsDisposed) continue;
                par.Key.Top = par.Value >= baseY - 2 ? par.Value + alto : par.Value;
            }
            padre.ResumeLayout(true);
        }




        // Una sola vez: detalle del pedido (izquierda) y altura del
        // encabezado de la vista previa (antes 15 px, texto recortado).
        private void AjustarTablasBoleta()
        {
            if (dgvDetallePedido.Columns.Count >= 4)
            {
                DataGridView t = dgvDetallePedido;
                EstiloTablas.Configurar(t, 30, 26);
                EstiloTablas.Columna(t.Columns[0], 12, 46, EstiloTablas.Centro);
                EstiloTablas.Columna(t.Columns[1], 46, 120, EstiloTablas.Izquierda, true);
                EstiloTablas.Columna(t.Columns[2], 20, 66, EstiloTablas.Derecha);
                EstiloTablas.Columna(t.Columns[3], 22, 72, EstiloTablas.Derecha);
            }

            dgvBoletaDetalle.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvBoletaDetalle.ColumnHeadersHeight = 24;
            dgvBoletaDetalle.ThemeStyle.HeaderStyle.Height = 24;
        }

        // ==============================================
        // PAGO MIXTO: efectivo fijo + segundo metodo
        // ==============================================


        private void ActualizarImporteMixto()
        {
            bool esMixto = rbPagoMixto.Checked;
            bool esEfectivo = rbPagoEfectivo.Checked;

            // ============================================
            // 1. APARTADO CAMBIO
            // SIEMPRE VISIBLE
            // ============================================

            lblTituloCambio.Visible = true;
            lblMontoRecibido.Visible = true;
            numMontoRecibido.Visible = true;
            lblVuelto.Visible = true;
            txtVueltoBoleta.Visible = true;
            btnCalcularVuelto.Visible = true;
           

            // Habilitar solamente cuando se elige Efectivo.
            numMontoRecibido.Enabled = esEfectivo;
            txtVueltoBoleta.Enabled = esEfectivo;
            btnCalcularVuelto.Enabled = esEfectivo;

            // Colores del apartado Cambio.
            lblTituloCambio.ForeColor =
                esEfectivo ? Color.Black : Color.Gray;

            lblMontoRecibido.ForeColor =
                esEfectivo ? Color.Black : Color.Gray;

            lblVuelto.ForeColor =
                esEfectivo ? Color.Black : Color.Gray;

            btnCalcularVuelto.FillColor =
                esEfectivo ? Color.Gold : Color.Gainsboro;

            btnCalcularVuelto.ForeColor =
                esEfectivo ? Color.Black : Color.Gray;

            // ============================================
            // 2. APARTADO PAGO MIXTO
            // SIEMPRE VISIBLE
            // ============================================

            lblTituloPagoMixto.Visible = true;
            lblTituloPago1.Visible = true;
            lblTituloPago2.Visible = true;

            lblMetodoPago1.Visible = true;
            lblMontoPago1.Visible = true;

            txtMetodoPago1.Visible = true;
            numMontoPago1.Visible = true;

            // ESTE ES EL COMBO DE LOS MÉTODOS DE PAGO.
            cmbMetodoPago2.Visible = true;

            // Monto automático del segundo pago.
            txtMontoPago2.Visible = true;

            // ============================================
            // 3. HABILITAR O DESHABILITAR PAGO MIXTO
            // ============================================

            txtMetodoPago1.Enabled = esMixto;
            numMontoPago1.Enabled = esMixto;

            // Permitir elegir Yape, Plin,
            // Tarjeta o Transferencia cuando es Mixto.
            cmbMetodoPago2.Enabled = esMixto;

            // El monto 2 es automático.
            txtMontoPago2.Enabled = false;

            // ============================================
            // 4. CAMBIAR COLORES
            // ============================================

            lblTituloPagoMixto.ForeColor =
                esMixto ? Color.Black : Color.Gray;

            lblTituloPago1.ForeColor =
                esMixto ? Color.Black : Color.Gray;

            lblTituloPago2.ForeColor =
                esMixto ? Color.Black : Color.Gray;

            lblMetodoPago1.ForeColor =
                esMixto ? Color.Black : Color.Gray;

            lblMontoPago1.ForeColor =
                esMixto ? Color.Black : Color.Gray;

            cmbMetodoPago2.FillColor =
                esMixto ? Color.White : Color.Gainsboro;

            cmbMetodoPago2.ForeColor =
                esMixto ? Color.Black : Color.Gray;

            // ============================================
            // 5. TIPO DE TARJETA
            // ============================================

            // El ComboBox de Pago 2 siempre permanece visible.
            cmbMetodoPago2.Visible = true;

            // Solo se habilita cuando seleccionas Mixto.
            cmbMetodoPago2.Enabled = esMixto;



            // ============================================
            // 6. VERIFICAR SI ESTÁ SELECCIONADO MIXTO
            // ============================================

            if (!esMixto)
            {
                return;
            }

            // ============================================
            // 7. PAGO 1: EFECTIVO FIJO
            // ============================================

            txtMetodoPago1.Text = "Efectivo";

            decimal efectivo = numMontoPago1.Value;

            // ============================================
            // 8. CALCULAR EL SEGUNDO PAGO
            // ============================================

            decimal restante = total - efectivo;

            if (restante < 0)
            {
                restante = 0;
            }

            // txtMontoPago2 es Guna2NumericUpDown.
            if (txtMontoPago2.Maximum < restante)
            {
                txtMontoPago2.Maximum = restante;
            }

            txtMontoPago2.Value = restante;

            // ============================================
            // 9. ACTUALIZAR FORMA DE PAGO
            // ============================================

            if (cmbMetodoPago2.SelectedIndex >= 0)
            {
                lblBoletaFormaPago.Text =
                    "Efectivo + " + cmbMetodoPago2.Text;
            }
            else
            {
                lblBoletaFormaPago.Text = "Mixto";
            }
        }



        private void ConfigurarMetodosPagoMixto()
        {
            // PAGO 1: SIEMPRE EFECTIVO
            txtMetodoPago1.Text = "Efectivo";
            txtMetodoPago1.ReadOnly = true;

            numMontoPago1.DecimalPlaces = 2;
            numMontoPago1.Minimum = 0;
            numMontoPago1.Maximum = 1000000;

            // PAGO 2: TODOS LOS MÉTODOS MENOS EFECTIVO
            cmbMetodoPago2.Items.Clear();

            cmbMetodoPago2.Items.Add("Yape");
            cmbMetodoPago2.Items.Add("Plin");
            cmbMetodoPago2.Items.Add("Tarjeta");
            cmbMetodoPago2.Items.Add("Transferencia");

            cmbMetodoPago2.DropDownStyle =
                ComboBoxStyle.DropDownList;

            cmbMetodoPago2.SelectedIndex = -1;

            // MONTO DEL SEGUNDO PAGO: AUTOMÁTICO
            txtMontoPago2.DecimalPlaces = 2;
            txtMontoPago2.Minimum = 0;
            txtMontoPago2.Maximum = 1000000;
            txtMontoPago2.Enabled = false;
            txtMontoPago2.Value = 0;

            // EVENTOS: CONECTAR UNA SOLA VEZ
            rbPagoMixto.CheckedChanged +=
                (s, e) => ActualizarImporteMixto();

            rbPagoEfectivo.CheckedChanged +=
                (s, e) => ActualizarImporteMixto();

            rbPagoTarjeta.CheckedChanged +=
                (s, e) => ActualizarImporteMixto();

            rbPagoYape.CheckedChanged +=
                (s, e) => ActualizarImporteMixto();

            rbPagoTransferencia.CheckedChanged +=
                (s, e) => ActualizarImporteMixto();

            numMontoPago1.ValueChanged +=
                (s, e) => ActualizarImporteMixto();

            cmbMetodoPago2.SelectedIndexChanged +=
                (s, e) => ActualizarImporteMixto();

            ActualizarImporteMixto();
        }



        private void Generar_Boleta_Shown(object sender, EventArgs e)
        {
            // Cargar impresoras al mostrar la ventana,
            // no mientras Visual Studio diseña el formulario.
            CargarImpresoras();
            // 1. Verificar que se haya recibido un pedido.
            if (this.Tag == null)
            {
                MessageBox.Show(
                    "No se recibió ningún pedido.",
                    "Generar recibo de venta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                this.Close();
                return;
            }

            // 2. Recibir el número del pedido.
            if (!int.TryParse(this.Tag.ToString(), out idPedido)
                || idPedido <= 0)
            {
                MessageBox.Show(
                    "El número del pedido no es válido.",
                    "Generar recibo de venta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                this.Close();
                return;
            }

            // 3. Cargar los datos desde SQL Server.
            try
            {

                CargarPedidoSQL();
                if (!ModoReimpresion)
                    PrecargarComprador();

                // Crear numeración ficticia.
                GenerarBoletaPrueba();

                // Mostrar datos del pedido.
                MostrarPedido();

                // Calcular Pago 2 utilizando el total real del pedido.
                ActualizarImporteMixto();

                // Generar los códigos.
                GenerarCodigos();

                if (rbPagoEfectivo.Checked)
                    rbPagoEfectivo_CheckedChanged(rbPagoEfectivo, EventArgs.Empty);

                if (ModoReimpresion)
                    PrepararReimpresion();
                MostrarClienteEnVistaPrevia();

                MessageBox.Show(
                    "Pedido N.º " + idPedido +
                    " cargado correctamente.",
                    "Generar recibo de venta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar el pedido:\n" + ex.Message,
                    "Generar recibo de venta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                this.Close();
            }
        }




        private void RegistrarCobroSQL(
            List<KeyValuePair<string, decimal>> pagos)
        {
            // Identidad: empleado autenticado en INICIAR_SESIÓN.
            if (!SesionActual.IdEmpleado.HasValue)
                throw new Exception(
                    "Debe iniciar sesión para cobrar.");

            // Cargo autorizado (decisión aprobada: Administrador y Cajero).
            // Se vuelve a comprobar en SQL dentro de la transacción.
            if (!Permisos.PuedeCobrar(SesionActual.Cargo))
                throw new Exception(
                    "Su cargo (" + SesionActual.Cargo + ") no está autorizado para cobrar.\n" +
                    "Solo pueden cobrar un Administrador o un Cajero.");

            int idCajero = SesionActual.IdEmpleado.Value;

            // Comprador (opcional): distinto de quien cobra.
            string nombreCliente = "";
            string documentoCliente = "";
            int? clienteVinculado = null;
            if (IncluirCliente())
            {
                nombreCliente = txtNombreCliente.Text.Trim();
                while (nombreCliente.Contains("  ")) nombreCliente = nombreCliente.Replace("  ", " ");
                documentoCliente = txtDNICliente.Text.Trim().ToUpperInvariant();
                if (nombreCliente.Length < 3)
                    throw new Exception("Escriba el nombre completo del cliente o desmarque \"Incluir en el recibo\".");
                if (documentoCliente.Length > 0 && ClienteDatos.TipoDocumento(documentoCliente) == "?")
                    throw new Exception("El documento del cliente no es válido: DNI de 8 dígitos, CE de 9, RUC de 11 o pasaporte.");
                if (idClienteSeleccionado.HasValue && documentoCliente == documentoClienteRegistrado)
                    clienteVinculado = idClienteSeleccionado;
            }

            decimal suma = 0;

            foreach (KeyValuePair<string, decimal> pago in pagos)
            {
                if (pago.Value <= 0)
                    throw new Exception("Hay un importe inválido.");

                suma += pago.Value;
            }

            if (suma != total)
                throw new Exception(
                    "La suma de los pagos no coincide con el total.");

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
                        // 1. Verificar al empleado que cobra: activo, con
                        //    cuenta habilitada, de esta sucursal y con cargo
                        //    CAJERO o ADMINISTRADOR (igual que TR_PAGO_RN_E).
                        using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM dbo.EMPLEADO E
                    WHERE E.id_empleado = @Cajero
                      AND E.id_sucursal = 1
                      AND LOWER(LTRIM(RTRIM(E.estado))) = 'activo'
                      AND E.acceso_habilitado = 1
                      AND (EXISTS (SELECT 1 FROM dbo.CAJERO C WHERE C.id_empleado = E.id_empleado)
                           OR EXISTS (SELECT 1 FROM dbo.ADMINISTRADOR A WHERE A.id_empleado = E.id_empleado));",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Cajero", SqlDbType.Int).Value =
                                idCajero;

                            if (Convert.ToInt32(
                                cmd.ExecuteScalar()) != 1)
                            {
                                throw new Exception(
                                    "El empleado de la sesión no está autorizado para cobrar " +
                                    "(debe ser Administrador o Cajero activo, con cuenta habilitada).");
                            }
                        }

                        // 2. Bloquear y verificar el pedido.
                        decimal totalSQL;

                        using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        CAST(
                            ISNULL((
                                SELECT SUM(
                                    cantidad * (precio_unitario
                                    - ISNULL(descuento, 0)))
                                FROM dbo.DETALLE_PEDIDO_PLATO
                                WHERE id_pedido = P.id_pedido
                            ), 0)
                            +
                            ISNULL((
                                SELECT SUM(
                                    cantidad * (precio_unitario
                                    - ISNULL(descuento, 0)))
                                FROM dbo.DETALLE_PEDIDO_COMBO
                                WHERE id_pedido = P.id_pedido
                            ), 0)
                            -
                            ISNULL(P.descuento_aplicado, 0)
                            +
                            ISNULL(P.propina, 0)
                        AS DECIMAL(18,2))
                    FROM dbo.PEDIDO P WITH (UPDLOCK, HOLDLOCK)
                    WHERE P.id_pedido = @Pedido
                      AND P.id_sucursal = 1
                      AND P.estado = 'entregado';",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;

                            object resultado = cmd.ExecuteScalar();

                            // Regla de SQL (TR_PEDIDO_VALIDACIONES): solo un
                            // pedido 'entregado' puede pasar a 'cobrado'.
                            if (resultado == null ||
                                resultado == DBNull.Value)
                            {
                                throw new Exception(
                                    "El pedido debe estar Entregado " +
                                    "y no debe estar cobrado.");
                            }

                            totalSQL = Convert.ToDecimal(resultado);
                        }

                        if (totalSQL != total || totalSQL != suma)
                        {
                            throw new Exception(
                                "El total cambió. Vuelva a cargar el pedido.");
                        }

                        // 3. Evitar cualquier cobro duplicado.
                        using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        (SELECT COUNT(*) FROM dbo.PAGO
                         WHERE id_pedido = @Pedido)
                        +
                        (SELECT COUNT(*) FROM dbo.COMPROBANTE
                         WHERE id_pedido = @Pedido)
                        +
                        (SELECT COUNT(*) FROM dbo.VENTA_INTERNA
                         WHERE id_pedido = @Pedido);",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;

                            if (Convert.ToInt32(
                                cmd.ExecuteScalar()) > 0)
                            {
                                throw new Exception(
                                    "Este pedido ya tiene un pago o venta registrada.");
                            }
                        }

                        // 4. Registrar cada método de pago.
                        foreach (KeyValuePair<string, decimal> pago in pagos)
                        {
                            InsertarPago(
                                conexion, transaccion,
                                idCajero, pago.Key, pago.Value);
                        }

                        // 4a. Comprador autorizado: se registra (o se vincula si su
                        //     documento ya existe), en esta misma transacción.
                        if (!clienteVinculado.HasValue && compradorPorRegistrar != null)
                            clienteVinculado = RegistrarCompradorEnCobro(conexion, transaccion, idCajero);

                        // 4b. Cliente registrado elegido: debe seguir activo.
                        if (clienteVinculado.HasValue)
                        {
                            using (SqlCommand cmd = new SqlCommand(
                                "SELECT COUNT(*) FROM dbo.CLIENTE WHERE id_cliente = @Cliente AND estado = 'activo';",
                                conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Cliente", SqlDbType.Int).Value = clienteVinculado.Value;
                                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                                    throw new Exception("El cliente seleccionado ya no está activo. Elija otro o quite el cliente.");
                            }
                        }

                        // 4c. Recibo interno: serie R001 y correlativo único.
                        string numero;
                        using (SqlCommand cmd = new SqlCommand(@"
                    SELECT ISNULL(MAX(CAST(numero AS INT)), 0) + 1
                    FROM dbo.COMPROBANTE WITH (UPDLOCK, HOLDLOCK)
                    WHERE serie = @Serie;", conexion, transaccion))
                        {
                            cmd.Parameters.Add("@Serie", SqlDbType.VarChar, 4).Value = SerieRecibo;
                            int siguiente = Convert.ToInt32(cmd.ExecuteScalar());
                            if (siguiente > 99999999)
                                throw new Exception("Se agotó la numeración de recibos.");
                            numero = siguiente.ToString("D8");
                        }
                        using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO dbo.COMPROBANTE
                        (tipo, serie, numero, fecha_emision, identificacion_cliente, nombre_cliente, estado, id_pedido)
                    VALUES
                        ('B', @Serie, @Numero, CONVERT(DATE, SYSDATETIME()), @Documento, @Nombre, 'emitido', @Pedido);",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add("@Serie", SqlDbType.VarChar, 4).Value = SerieRecibo;
                            cmd.Parameters.Add("@Numero", SqlDbType.VarChar, 8).Value = numero;
                            cmd.Parameters.Add("@Documento", SqlDbType.VarChar, 20).Value =
                                documentoCliente.Length == 0 ? (object)DBNull.Value : documentoCliente;
                            cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 255).Value =
                                nombreCliente.Length == 0 ? (object)DBNull.Value : nombreCliente;
                            cmd.Parameters.Add("@Pedido", SqlDbType.Int).Value = idPedido;
                            cmd.ExecuteNonQuery();
                        }

                        // 5. Marcar el pedido como cobrado (y vincular al
                        // cliente registrado, si se eligió uno).
                        // El trigger verifica la suma de pagos.
                        using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE dbo.PEDIDO
                    SET estado = 'cobrado',
                        id_cliente = COALESCE(@Cliente, id_cliente)
                    WHERE id_pedido = @Pedido
                      AND id_sucursal = 1
                      AND estado = 'entregado';",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;
                            cmd.Parameters.Add("@Cliente", SqlDbType.Int).Value =
                                clienteVinculado.HasValue ? (object)clienteVinculado.Value : DBNull.Value;
                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                throw new Exception(
                                    "No se pudo cerrar el pedido.");
                            }

                            numeroRecibo = SerieRecibo + "-" + numero;
                        }

                        // 6. Salón: la mesa queda libre en la MISMA transacción
                        //    (antes nadie la liberaba y quedaba 'ocupada' sin
                        //    pedido, bloqueando nuevos pedidos). Solo si no tiene
                        //    otro pedido activo. No toca reservas.
                        using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE M
                       SET estado = 'libre'
                      FROM dbo.MESA M
                      INNER JOIN dbo.PEDIDO P ON P.id_mesa = M.id_mesa
                     WHERE P.id_pedido = @Pedido
                       AND P.canal = 'salon'
                       AND M.estado = 'ocupada'
                       AND NOT EXISTS
                       (
                           SELECT 1 FROM dbo.PEDIDO O
                           WHERE O.id_mesa = M.id_mesa
                             AND O.id_pedido <> P.id_pedido
                             AND O.estado IN ('pendiente','en_preparacion','listo','entregado')
                       );",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;
                            cmd.ExecuteNonQuery();
                        }

                        transaccion.Commit();
                        if (compradorPorRegistrar != null && clienteVinculado.HasValue)
                        {
                            clienteRegistradoAlCobrar = clienteVinculado;
                            idClienteSeleccionado = clienteVinculado;
                            documentoClienteRegistrado = documentoCliente;
                        }
                        Registro.Info("Cobro", "Pedido " + idPedido + " cobrado por el empleado " +
                            idCajero + " (" + SesionActual.Cargo + "), " + pagos.Count + " pago(s).");
                    }
                    catch
                    {
                        try
                        {
                            transaccion.Rollback();
                        }
                        catch
                        {
                            // SQL Server pudo finalizar la transacción.
                        }

                        throw;
                    }
                }
            }
        }

        private void InsertarPago(
            SqlConnection conexion,
            SqlTransaction transaccion,
            int idCajero,
            string metodo,
            decimal importe)
        {
            const string sql = @"
        INSERT INTO dbo.PAGO
        (
            fecha_hora,
            [método],
            importe,
            nro_referencia,
            id_pedido,
            id_cajero,
            id_cierre
        )
        VALUES
        (
            SYSDATETIME(),
            @Metodo,
            @Importe,
            NULL,
            @Pedido,
            @Cajero,
            NULL
        );";

            using (SqlCommand cmd =
                new SqlCommand(sql, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Metodo", SqlDbType.VarChar, 20
                ).Value = metodo;

                SqlParameter monto = cmd.Parameters.Add(
                    "@Importe", SqlDbType.Decimal);

                monto.Precision = 18;
                monto.Scale = 2;
                monto.Value = importe;

                cmd.Parameters.Add(
                    "@Pedido", SqlDbType.Int).Value = idPedido;

                cmd.Parameters.Add(
                    "@Cajero", SqlDbType.Int).Value = idCajero;

                cmd.ExecuteNonQuery();
            }
        }


        // ==================================================
        // CARGAR EL PEDIDO Y SUS PRODUCTOS DESDE SQL SERVER
        // ==================================================
        private void CargarPedidoSQL()
        {
            const string sqlEncabezado = @"
                SELECT P.id_pedido, P.fecha_hora, P.canal, P.estado,
                       ISNULL(P.descuento_aplicado, 0) AS DescuentoGeneral,
                       ISNULL(P.propina, 0) AS Propina,
                       PR.nombre AS Promocion,
                       M.[número] AS NumeroMesa,
                       LTRIM(RTRIM(ISNULL(E.nombre, '') + ' ' +
                                    ISNULL(E.apellidos, ''))) AS NombreMesero,
                       P.id_reserva, P.nombre_contacto, P.[teléfono_contacto] AS TelefonoContacto,
                       CASE WHEN C.estado = 'activo' THEN C.id_cliente END AS IdClienteActivo,
                       C.documento AS DocCliente, C.[teléfono] AS TelCliente,
                       LTRIM(RTRIM(ISNULL(C.nombre, '') + ' ' + ISNULL(C.apellidos, ''))) AS NombreClienteReg
                FROM dbo.PEDIDO P
                LEFT JOIN dbo.CLIENTE C ON C.id_cliente = P.id_cliente
                LEFT JOIN dbo.MESA M ON M.id_mesa = P.id_mesa
                LEFT JOIN dbo.EMPLEADO E ON E.id_empleado = P.id_mesero
                LEFT JOIN dbo.PROMOCION PR ON PR.id_promocion = P.id_promocion
                WHERE P.id_pedido = @Pedido;";

            const string sqlDetalle = @"
                SELECT D.cantidad AS Cantidad, P.nombre AS Descripcion,
                       D.precio_unitario AS Precio,
                       ISNULL(D.descuento, 0) AS Descuento
                FROM dbo.DETALLE_PEDIDO_PLATO D
                INNER JOIN dbo.PLATO P ON P.id_plato = D.id_plato
                WHERE D.id_pedido = @Pedido
                UNION ALL
                SELECT D.cantidad, C.nombre, D.precio_unitario,
                       ISNULL(D.descuento, 0)
                FROM dbo.DETALLE_PEDIDO_COMBO D
                INNER JOIN dbo.COMBO C ON C.id_combo = D.id_combo
                WHERE D.id_pedido = @Pedido;";

            using (SqlConnection conexion = new SqlConnection(cadenaConexion))
            {
                conexion.Open();
                using (SqlCommand cmd = new SqlCommand(sqlEncabezado, conexion))
                {
                    cmd.Parameters.Add("@Pedido", SqlDbType.Int).Value = idPedido;
                    using (SqlDataReader lector = cmd.ExecuteReader())
                    {
                        if (!lector.Read())
                            throw new Exception("El pedido no existe en SQL Server.");

                        string estado = Convert.ToString(lector["estado"])
                            .Trim().ToLowerInvariant();
                        if (estado == "cancelado")
                            throw new Exception("El pedido está cancelado.");
                        estadoPedido = estado;

                        fechaPedido = Convert.ToDateTime(lector["fecha_hora"]);
                        tipoPedido = Convert.ToString(lector["canal"]);
                        numeroMesa = lector["NumeroMesa"] == DBNull.Value
                            ? "No aplica" : Convert.ToString(lector["NumeroMesa"]);
                        atendidoPor = Convert.ToString(lector["NombreMesero"]);
                        if (string.IsNullOrWhiteSpace(atendidoPor))
                            atendidoPor = "No asignado";
                        descuentoGeneral = Convert.ToDecimal(lector["DescuentoGeneral"]);
                        propina = Convert.ToDecimal(lector["Propina"]);
                        promocionPedido = lector["Promocion"] == DBNull.Value ? null : Convert.ToString(lector["Promocion"]);

                        Func<string, string> texto = c => lector[c] == DBNull.Value ? null : Convert.ToString(lector[c]).Trim();
                        idReservaPedido = lector["id_reserva"] == DBNull.Value ? (int?)null : Convert.ToInt32(lector["id_reserva"]);
                        contactoPedido = texto("nombre_contacto");
                        telefonoPedido = texto("TelefonoContacto");
                        idClientePedido = lector["IdClienteActivo"] == DBNull.Value ? (int?)null : Convert.ToInt32(lector["IdClienteActivo"]);
                        documentoClientePedido = texto("DocCliente");
                        nombreClientePedido = texto("NombreClienteReg");
                        telefonoClientePedido = texto("TelCliente");
                    }
                }

                // Reimpresión: solo pedidos cobrados; se leen los datos guardados.
                if (ModoReimpresion)
                {
                    if (estadoPedido != "cobrado")
                        throw new Exception("Solo se pueden reimprimir recibos de pedidos cobrados.");
                    CargarDatosGuardados(conexion);
                }
                // Impedir preparar una nueva boleta para un pedido ya cobrado.
                else using (SqlCommand cmd = new SqlCommand(@"
                    SELECT (SELECT COUNT(*) FROM dbo.COMPROBANTE
                            WHERE id_pedido = @Pedido)
                         + (SELECT COUNT(*) FROM dbo.PAGO
                            WHERE id_pedido = @Pedido);", conexion))
                {
                    cmd.Parameters.Add("@Pedido", SqlDbType.Int).Value = idPedido;
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                        throw new Exception("El pedido ya tiene un pago o comprobante.");
                }

                productos.Clear();
                using (SqlCommand cmd = new SqlCommand(sqlDetalle, conexion))
                {
                    cmd.Parameters.Add("@Pedido", SqlDbType.Int).Value = idPedido;
                    using (SqlDataReader lector = cmd.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            productos.Add(new LineaBoleta
                            {
                                Cantidad = Convert.ToInt32(lector["Cantidad"]),
                                Descripcion = Convert.ToString(lector["Descripcion"]),
                                PrecioUnitario = Convert.ToDecimal(lector["Precio"]),
                                DescuentoLinea = Convert.ToDecimal(lector["Descuento"])
                            });
                        }
                    }
                }
            }

            if (productos.Count == 0)
                throw new Exception("El pedido no tiene productos guardados.");

            subtotal = 0m;
            descuentoTotal = descuentoGeneral;
            foreach (LineaBoleta linea in productos)
            {
                if (linea.Cantidad <= 0 || linea.Importe < 0)
                    throw new Exception("Hay una línea con cantidad o descuento inválido.");
                subtotal += linea.Cantidad * linea.PrecioUnitario;
                descuentoTotal += linea.Cantidad * linea.DescuentoLinea;
            }
            decimal importeConIGV = subtotal - descuentoTotal;
            if (importeConIGV < 0)
                throw new Exception("Los descuentos superan el subtotal.");

            igvIncluido = Math.Round(importeConIGV * 18m / 118m,
                2, MidpointRounding.AwayFromZero);
            total = importeConIGV + propina;
        }


        // "Descuento:" o "Desc. <promoción>:" (recortado para no salirse).
        private string TextoDescuento()
        {
            if (string.IsNullOrEmpty(promocionPedido))
                return "Descuento:";
            string nombre = promocionPedido.Length > 18 ? promocionPedido.Substring(0, 17) + "…" : promocionPedido;
            return "Desc. " + nombre + ":";
        }

        // Recibo guardado (número y comprador), pagos y cajero de un pedido cobrado.
        private void CargarDatosGuardados(SqlConnection conexion)
        {
            numeroRecibo = null;
            string nombre = null, documento = null;
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) serie, numero, nombre_cliente, identificacion_cliente
                FROM dbo.COMPROBANTE WHERE id_pedido = @Pedido;", conexion))
            {
                cmd.Parameters.Add("@Pedido", SqlDbType.Int).Value = idPedido;
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        numeroRecibo = r.GetString(0).Trim() + "-" + r.GetString(1).Trim();
                        nombre = r.IsDBNull(2) ? null : r.GetString(2);
                        documento = r.IsDBNull(3) ? null : r.GetString(3);
                    }
                }
            }

            List<string> metodos = new List<string>();
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT PG.[método], LTRIM(RTRIM(E.nombre + ' ' + E.apellidos))
                FROM dbo.PAGO PG JOIN dbo.EMPLEADO E ON E.id_empleado = PG.id_cajero
                WHERE PG.id_pedido = @Pedido ORDER BY PG.id_pago;", conexion))
            {
                cmd.Parameters.Add("@Pedido", SqlDbType.Int).Value = idPedido;
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string m = r.GetString(0).Trim();
                        metodos.Add(m.Length == 0 ? m : char.ToUpperInvariant(m[0]) + m.Substring(1));
                        cajeroRecibo = r.GetString(1);
                    }
                }
            }
            formaPagoGuardada = metodos.Count == 0 ? "-" : string.Join(" + ", metodos);

            numeroBoletaDemo = numeroRecibo ?? ("PEDIDO-" + idPedido.ToString("D6"));

            cargandoCliente = true;
            try
            {
                txtNombreCliente.Text = nombre ?? "";
                txtDNICliente.Text = documento ?? "";
                chkIncluirCliente.Checked = !string.IsNullOrEmpty(nombre);
            }
            finally
            {
                cargandoCliente = false;
            }
        }

        // Tras el cobro (o al reimprimir) los datos del recibo no cambian.
        private void BloquearDatosCliente()
        {
            txtDNICliente.ReadOnly = true;
            txtNombreCliente.ReadOnly = true;
            txtCelularCliente.ReadOnly = true;
            chkIncluirCliente.Enabled = false;
            btnBuscarCliente.Enabled = false;
        }

        // Ventana en modo reimpresión: sin cobrar de nuevo.
        private void PrepararReimpresion()
        {
            Text = "Reimprimir recibo";
            guna2HtmlLabel1.Text = "REIMPRIMIR RECIBO DE VENTA";
            BloquearDatosCliente();
            btnRegistrarCobro.Enabled = false;
            guna2HtmlLabel22.Enabled = false;
            foreach (RadioButton rb in new RadioButton[] { rbPagoEfectivo, rbPagoYape, rbPagoTarjeta, rbPagoTransferencia, rbPagoPlin, rbPagoMixto })
                rb.Enabled = false;
            lblBoletaFormaPago.Text = formaPagoGuardada;
            btnImprimirBoleta.Enabled = true;
            ActualizarVistaPrevia();
        }

        // Elegir un pedido cobrado y abrir su recibo para reimprimirlo.
        internal static void ReimprimirRecibo(IWin32Window dueno)
        {
            if (!SesionActual.Iniciada || !Permisos.PuedeCobrar(SesionActual.Cargo))
            {
                MessageBox.Show(dueno, "Solo un Administrador o Cajero puede reimprimir recibos.", "Reimprimir recibo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DataTable datos = new DataTable();
            try
            {
                using (SqlConnection cx = new SqlConnection(BaseDatos.CadenaConexion))
                using (SqlDataAdapter da = new SqlDataAdapter(@"
                    SELECT TOP (200) P.id_pedido, P.fecha_hora, P.canal, V.total,
                           ISNULL(C.serie + '-' + C.numero, '-') AS recibo,
                           ISNULL(C.nombre_cliente, '') AS cliente
                    FROM dbo.PEDIDO P
                    JOIN dbo.V_PEDIDO_TOTAL V ON V.id_pedido = P.id_pedido
                    LEFT JOIN dbo.COMPROBANTE C ON C.id_pedido = P.id_pedido
                    WHERE P.estado = 'cobrado' AND P.id_sucursal = @Sucursal
                    ORDER BY P.fecha_hora DESC;", cx))
                {
                    da.SelectCommand.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                    da.Fill(datos);
                }
            }
            catch (Exception ex)
            {
                Registro.Error("Boleta", ex);
                MessageBox.Show(dueno, "No se pudieron cargar los pedidos cobrados:\n" + ex.Message, "Reimprimir recibo",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int? elegido = null;
            using (Form f = new Form())
            {
                f.Text = "Reimprimir recibo";
                f.ClientSize = new Size(720, 480);
                f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Color.White;
                f.ShowInTaskbar = false;

                Guna2Panel cab = new Guna2Panel { Dock = DockStyle.Top, Height = 48, FillColor = Color.Gold };
                cab.Controls.Add(new Label
                {
                    Text = "REIMPRIMIR RECIBO", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold), BackColor = Color.Transparent
                });
                Guna2TextBox buscar = new Guna2TextBox
                {
                    PlaceholderText = "N.º de pedido, recibo o cliente", BorderRadius = 8, BorderColor = Color.Goldenrod,
                    Font = new Font("Segoe UI", 10F), ForeColor = Color.Black
                };
                Guna2DataGridView tabla = new Guna2DataGridView
                {
                    ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
                    RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };
                foreach (string[] c in new[] { new[] { "pedido", "PEDIDO" }, new[] { "fecha", "FECHA" }, new[] { "canal", "TIPO" },
                         new[] { "total", "TOTAL S/" }, new[] { "recibo", "RECIBO" }, new[] { "cliente", "CLIENTE" } })
                    tabla.Columns.Add(c[0], c[1]);
                Guna2Button abrir = new Guna2Button { Text = "Abrir recibo", BorderRadius = 10, FillColor = Color.Gold, ForeColor = Color.Black, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                Guna2Button cerrar = new Guna2Button { Text = "Cerrar", BorderRadius = 10, FillColor = Color.Gainsboro, ForeColor = Color.Black, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                f.Controls.AddRange(new Control[] { buscar, tabla, abrir, cerrar, cab });
                buscar.Bounds = new Rectangle(20, 62, 680, 38);
                tabla.Bounds = new Rectangle(20, 110, 680, 300);
                cerrar.Bounds = new Rectangle(356, 424, 160, 40);
                abrir.Bounds = new Rectangle(540, 424, 160, 40);

                EstiloTablas.Configurar(tabla, 34, 28);
                tabla.ThemeStyle.HeaderStyle.BackColor = Color.Gold;
                tabla.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
                EstiloTablas.ColumnaFija(tabla.Columns[0], 70, EstiloTablas.Centro);
                EstiloTablas.ColumnaFija(tabla.Columns[1], 120, EstiloTablas.Centro);
                EstiloTablas.ColumnaFija(tabla.Columns[2], 80, EstiloTablas.Izquierda);
                EstiloTablas.ColumnaFija(tabla.Columns[3], 84, EstiloTablas.Derecha);
                EstiloTablas.ColumnaFija(tabla.Columns[4], 120, EstiloTablas.Centro);
                EstiloTablas.Columna(tabla.Columns[5], 40, 120, EstiloTablas.Izquierda, true);

                Action llenar = () =>
                {
                    string t = MenuDatos.Normalizar(buscar.Text);
                    tabla.Rows.Clear();
                    foreach (DataRow d in datos.Rows)
                    {
                        string pedido = Convert.ToString(d["id_pedido"]);
                        string recibo = Convert.ToString(d["recibo"]);
                        string cliente = Convert.ToString(d["cliente"]);
                        if (t.Length > 0 && !pedido.Contains(t) && !recibo.ToLowerInvariant().Contains(t) &&
                            !MenuDatos.Normalizar(cliente).Contains(t))
                            continue;
                        int r = tabla.Rows.Add(pedido, Convert.ToDateTime(d["fecha_hora"]).ToString("dd/MM/yy HH:mm"),
                            Convert.ToString(d["canal"]), Convert.ToDecimal(d["total"]).ToString("N2"), recibo,
                            cliente.Length == 0 ? "-" : cliente);
                        tabla.Rows[r].Tag = Convert.ToInt32(d["id_pedido"]);
                    }
                };
                Action aceptar = () =>
                {
                    if (tabla.CurrentRow == null) return;
                    elegido = (int)tabla.CurrentRow.Tag;
                    f.DialogResult = DialogResult.OK;
                };
                buscar.TextChanged += (s, e) => llenar();
                tabla.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) aceptar(); };
                abrir.Click += (s, e) => aceptar();
                cerrar.Click += (s, e) => f.Close();
                f.Shown += (s, e) => buscar.Focus();
                llenar();
                f.ShowDialog(dueno);
            }

            if (elegido.HasValue)
            {
                using (Generar_Boleta ventana = new Generar_Boleta())
                {
                    ventana.ModoReimpresion = true;
                    ventana.Tag = elegido.Value;
                    ventana.ShowDialog(dueno);
                }
            }
        }

        private void MostrarPedido()
        {
            string canal = (tipoPedido ?? "")
                .Trim().ToLower();

            bool esSalon = canal == "salon" ||
                           canal == "salón";

            bool esDelivery = canal == "delivery";

            // Datos generales.
            lblValorPedido.Text = idPedido.ToString();
            lblValorTipo.Text = FormatearCanal(tipoPedido);
            lblValorFecha.Text =
                fechaPedido.ToString("dd/MM/yyyy HH:mm");

            // Mostrar los datos correspondientes.
            if (esSalon)
            {
                guna2HtmlLabel6.Text = "Mesa:";
                lblValorMesa.Text = numeroMesa;

                guna2HtmlLabel8.Visible = true;
                lblValorAtendido.Visible = true;
                lblValorAtendido.Text = atendidoPor;
            }
            else if (esDelivery)
            {
                guna2HtmlLabel6.Text = "Entrega:";
                lblValorMesa.Text = "A domicilio";

                guna2HtmlLabel8.Visible = false;
                lblValorAtendido.Visible = false;
            }
            else
            {
                guna2HtmlLabel6.Text = "Retiro:";
                lblValorMesa.Text = "En el local";

                guna2HtmlLabel8.Visible = false;
                lblValorAtendido.Visible = false;
            }

            // Calcular la base imponible sin IGV.
            decimal baseSinIGV =
                subtotal - descuentoTotal - igvIncluido;

            // Cambiar el título del subtotal.
            guna2HtmlLabel82.Text = "Base sin IGV:";

            // Mostrar los importes.
            lblSubtotalBoleta.Text = FormatoSoles(baseSinIGV);

            lblDescuentoBoleta.Text =
                FormatoSoles(descuentoTotal);
            guna2HtmlLabel83.Text = TextoDescuento();

            lblIGVBoleta.Text =
                FormatoSoles(igvIncluido);

            lblTotalBoleta.Text =
                FormatoSoles(total);


            // Mostrar los productos.
            dgvDetallePedido.DataSource = null;
            dgvDetallePedido.AllowUserToAddRows = false;
            dgvDetallePedido.ReadOnly = true;
            dgvDetallePedido.Rows.Clear();

            foreach (LineaBoleta linea in productos)
            {
                dgvDetallePedido.Rows.Add(
                    linea.Cantidad,
                    linea.Descripcion,
                    linea.PrecioUnitario.ToString("N2"),
                    linea.Importe.ToString("N2"));
            }

            dgvDetallePedido.ClearSelection();

            ActualizarVistaPrevia();
        }

        private string FormatoSoles(decimal importe)
        {
            return "S/ " + importe.ToString("N2",
                System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
        }

        private string FormatearCanal(string canal)
        {
            switch ((canal ?? "").Trim().ToLowerInvariant())
            {
                case "salon":
                case "salón": return "Salón";
                case "delivery": return "Delivery";
                case "recojo": return "Recojo";
                default: return canal ?? "";
            }
        }

        private void CargarImpresoras()
        {
            // Limpiar la lista de impresoras.
            cmbImpresoraBoleta.Items.Clear();

            // Buscar las impresoras instaladas.
            foreach (string impresora in PrinterSettings.InstalledPrinters)
            {
                cmbImpresoraBoleta.Items.Add(impresora);
            }

            // Identificar la impresora predeterminada.
            PrinterSettings configuracion = new PrinterSettings();

            string impresoraPredeterminada =
                configuracion.PrinterName;

            int posicion = cmbImpresoraBoleta.Items.IndexOf(
                impresoraPredeterminada);

            if (posicion >= 0)
            {
                cmbImpresoraBoleta.SelectedIndex = posicion;
            }
            else if (cmbImpresoraBoleta.Items.Count > 0)
            {
                cmbImpresoraBoleta.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show(
                    "No se encontraron impresoras instaladas.",
                    "Impresoras",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }


        private void btnCancelarBoleta_Click(object sender, EventArgs e)
        {
            // Cerrar la ventana Generar Boleta.
            this.Close();
        }


        private void numMontoRecibido_ValueChanged(object sender, EventArgs e)
        {
            if (!rbPagoEfectivo.Checked)
            {
                txtVueltoBoleta.Text = "S/ 0.00";
                return;
            }

            decimal montoRecibido = numMontoRecibido.Value;

            decimal vuelto = montoRecibido - total;

            if (vuelto < 0)
            {
                vuelto = 0;
            }

            txtVueltoBoleta.Text = "S/ " + vuelto.ToString("N2");
        }

        private void btnVistaPreviaBoleta_Click(object sender, EventArgs e)
        {
            if (idPedido <= 0 || productos.Count == 0)
            {
                MessageBox.Show("Primero cargue un pedido con productos.",
                    "Vista previa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ActualizarVistaPrevia();
        }

        private void btnVistaPreviaBoleta_Click_1(object sender, EventArgs e)
        {
            // Comprobar el pedido seleccionado.
            if (idPedido <= 0 || productos.Count == 0)
            {
                MessageBox.Show(
                    "Primero debe seleccionar un pedido.",
                    "Vista previa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                // Actualizar los datos del formulario.
                ActualizarVistaPrevia();

                // Preparar el documento para impresión.
                using (PrintDocument documento =
                    CrearDocumentoImpresion())
                {
                    // Abrir la vista previa de Windows.
                    using (PrintPreviewDialog vista =
                        new PrintPreviewDialog())
                    {
                        vista.Document = documento;

                        vista.Width = 900;
                        vista.Height = 700;

                        vista.StartPosition =
                            FormStartPosition.CenterScreen;

                        vista.ShowDialog(this);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudo mostrar la vista previa:\n" +
                    ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }




        private void ActualizarVistaPrevia()
        {




            // ======================================
            // 1. GENERAR NÚMERO DE BOLETA DE PRUEBA
            // ======================================

            // Generar una sola vez por apertura.
            if (string.IsNullOrEmpty(numeroBoletaDemo))
            {
                GenerarBoletaPrueba();
            }

            // Mostrar número ficticio.
            lblBoletaNumero.Text =
                numeroBoletaDemo;

            // ======================================
            // 2. INFORMACIÓN DEL PEDIDO
            // ======================================

            lblBoletaFecha.Text =
                fechaPedido.ToString("dd/MM/yyyy");

            lblBoletaHora.Text =
                fechaPedido.ToString("HH:mm");


            string canalActual = (tipoPedido ?? "")
                .Trim().ToLower();

            bool esSalon = canalActual == "salon" ||
                           canalActual == "salón";

            bool esDelivery = canalActual == "delivery";

            if (esSalon)
            {
                // Conservar la presentación original de Salón.
                guna2HtmlLabel26.Text = "Mesa:";
                lblBoletaMesa.Location = new Point(DisenoVentanas.Medida(this, 86), DisenoVentanas.Medida(this, 108));
                lblBoletaMesa.Text = numeroMesa;

                guna2HtmlLabel25.Visible = true;
                guna2HtmlLabel31.Visible = true;
                lblBoletaCanal.Visible = true;

                lblBoletaCanal.Text = "Salón";

                guna2HtmlLabel33.Text = "Atendido por:";
                lblBoletaAtendidoPor.Text = atendidoPor;
            }
            else
            {
                // Delivery y Recojo no tienen mesa.
                guna2HtmlLabel26.Text = "Tipo de pedido:";

                lblBoletaMesa.Location = new Point(DisenoVentanas.Medida(this, 145), DisenoVentanas.Medida(this, 108));
                lblBoletaMesa.Text =
                    FormatearCanal(tipoPedido);

                // Ocultar la separación Mesa | Canal.
                guna2HtmlLabel25.Visible = false;
                guna2HtmlLabel31.Visible = false;
                lblBoletaCanal.Visible = false;

                if (esDelivery)
                {
                    guna2HtmlLabel33.Text = "Entrega:";
                    lblBoletaAtendidoPor.Text =
                        "A domicilio";
                }
                else
                {
                    guna2HtmlLabel33.Text = "Retiro:";
                    lblBoletaAtendidoPor.Text =
                        "En el local";
                }
            }


            // ======================================
            // 3. IMPORTES DEL PEDIDO
            // ======================================

            decimal baseSinIGV =
                          subtotal - descuentoTotal - igvIncluido;

            // Títulos correctos.
            guna2HtmlLabel51.Text = "Base imponible:";
            guna2HtmlLabel36.Text = "IGV (18%):";              // igual que la impresión

            // Mostrar los importes (mismo orden que la impresión).
            lblPrevSubtotal.Text = FormatoSoles(subtotal);
            lblPrevDescuentoTitulo.Text = TextoDescuento();
            lblPrevDescuento.Text = FormatoSoles(descuentoTotal);
            lblBoletaSubtotal.Text = FormatoSoles(baseSinIGV);
            lblBoletaIGV.Text = FormatoSoles(igvIncluido);
            lblBoletaTotal.Text = FormatoSoles(total);




            // ======================================
            // 4. TÍTULOS DE LA BOLETA DEMOSTRATIVA
            // ======================================

            guna2HtmlLabel20.Text =
                ModoReimpresion ? "RECIBO DE VENTA (REIMPRESIÓN)" : "RECIBO DE VENTA";
            consejosBoleta.SetToolTip(guna2HtmlLabel20, NotaInterna);

            guna2HtmlLabel56.Text = "¡Gracias por su compra!";
            lblPrevNota.Text = NotaInterna;
            lblPrevCaja.Text = "Caja: " + (cajeroRecibo ?? SesionActual.NombreCompleto ?? "");

            // ======================================
            // 5. CONFIGURAR TABLA
            // ======================================

            dgvBoletaDetalle.DataSource = null;

            dgvBoletaDetalle.ReadOnly = true;

            dgvBoletaDetalle.AllowUserToAddRows = false;

            dgvBoletaDetalle.RowHeadersVisible = false;

            // Crear columnas si no existen.
            if (dgvBoletaDetalle.Columns.Count == 0)
            {
                dgvBoletaDetalle.Columns.Add(
                    "colCantBoleta", "Cant.");

                dgvBoletaDetalle.Columns.Add(
                    "colDescripcionBoleta", "Descripción");

                dgvBoletaDetalle.Columns.Add(
                    "colPrecioBoleta", "P. Unit.");

                dgvBoletaDetalle.Columns.Add(
                    "colImporteBoleta", "Importe");
            }

            // ======================================
            // 6. CONFIGURAR ANCHO DE COLUMNAS
            // ======================================

            dgvBoletaDetalle.AutoSizeColumnsMode =
                DataGridViewAutoSizeColumnsMode.None;

            // Cantidad (50 px: el encabezado "Cant." ya no se corta).
            dgvBoletaDetalle.Columns[0].Width = DisenoVentanas.Medida(this, 50);

            // Descripción.
            dgvBoletaDetalle.Columns[1].MinimumWidth = DisenoVentanas.Medida(this, 100);

            dgvBoletaDetalle.Columns[1].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;

            // Precio unitario.
            dgvBoletaDetalle.Columns[2].Width = DisenoVentanas.Medida(this, 70);

            // Importe.
            dgvBoletaDetalle.Columns[3].Width = DisenoVentanas.Medida(this, 75);

            // ======================================
            // 7. AJUSTAR TEXTO
            // ======================================

            // Permitir descripciones de varias líneas.
            dgvBoletaDetalle.Columns[1]
                .DefaultCellStyle.WrapMode =
                DataGridViewTriState.True;

            dgvBoletaDetalle.AutoSizeRowsMode =
                DataGridViewAutoSizeRowsMode.AllCells;

            // Alinear cantidades.
            dgvBoletaDetalle.Columns[0]
                .DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleCenter;

            // Alinear precios.
            dgvBoletaDetalle.Columns[2]
                .DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleRight;

            dgvBoletaDetalle.Columns[3]
                .DefaultCellStyle.Alignment =
                DataGridViewContentAlignment.MiddleRight;

            // ======================================
            // 8. MOSTRAR PRODUCTOS DEL PEDIDO
            // ======================================

            dgvBoletaDetalle.Rows.Clear();
            dgvBoletaDetalle.Columns[3].HeaderText = "Importe";       // igual que la impresión

            foreach (LineaBoleta linea in productos)
            {
                dgvBoletaDetalle.Rows.Add(
                    linea.Cantidad,
                    linea.Descripcion,
                    Monto(linea.PrecioUnitario),
                    Monto(linea.Importe)
                );
            }

            // ======================================
            // 9. AJUSTAR ALTURA DE LA TABLA
            // ======================================

            dgvBoletaDetalle.AutoResizeRows(
                DataGridViewAutoSizeRowsMode.AllCells);

            int altoTabla =
                dgvBoletaDetalle.ColumnHeadersHeight + 8;

            foreach (DataGridViewRow fila
                in dgvBoletaDetalle.Rows)
            {
                if (!fila.IsNewRow)
                {
                    altoTabla += fila.Height;
                }
            }

            dgvBoletaDetalle.Height = altoTabla;

            dgvBoletaDetalle.ClearSelection();

            // ======================================
            // 10. CÓDIGO FINAL DE LA BOLETA
            // ======================================

            // Número real del pedido bajo el código de barras.
            lblBoletaCodigoFinal.Text = CodigoPedido;
        }




        private void GenerarBoletaPrueba()
        {
            // Sin números ficticios: antes del cobro se identifica por el
            // pedido; el número real del recibo se asigna al cobrar.
            if (numeroBoletaDemo == "")
            {
                numeroBoletaDemo = numeroRecibo ?? ("PEDIDO-" + idPedido.ToString("D6"));
            }
        }



        private void rbPagoEfectivo_CheckedChanged(object sender, EventArgs e)
        {
            if (rbPagoEfectivo.Checked)
            {
                // Mostrar método de pago.
                lblBoletaFormaPago.Text = "Efectivo";

                // Habilitar monto recibido.
                numMontoRecibido.Enabled = true;

                // Limpiar el vuelto anterior.
                txtVueltoBoleta.Text = "S/ 0.00";
            }
        }

        private void rbPagoTarjeta_CheckedChanged(object sender, EventArgs e)
        {
            string metodoPago = "";

            if (rbPagoTarjeta.Checked)
            {
                metodoPago = "Tarjeta";
            }
            else if (rbPagoYape.Checked)
            {
                metodoPago = "Yape";
            }
            else if (rbPagoPlin.Checked)
            {
                metodoPago = "Plin";
            }
            else if (rbPagoTransferencia.Checked)
            {
                metodoPago = "Transferencia";
            }
            else
            {
                return;
            }

            // Mostrar la forma de pago en la boleta.
            lblBoletaFormaPago.Text = metodoPago;

            // Deshabilitar monto recibido.
            numMontoRecibido.Enabled = false;

            // Reiniciar monto y vuelto.
            numMontoRecibido.Value = 0;
            txtVueltoBoleta.Text = "S/ 0.00";
        }


        private bool ValidarPago()
        {
            // 1. Verificar que exista un pedido.
            if (idPedido <= 0)
            {
                MessageBox.Show(
                    "Primero debe seleccionar un pedido.",
                    "Validación de pago",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            // 1b. Antes de pedir datos de pago: estado del pedido y permiso.
            //     (Regla de SQL: solo un pedido 'entregado' puede cobrarse.)
            if (estadoPedido != "entregado")
            {
                MessageBox.Show(
                    "El pedido N.º " + idPedido + " está '" + estadoPedido + "'.\n\n" +
                    "Solo se puede cobrar un pedido ENTREGADO. Márquelo como " +
                    "entregado en Cocina y vuelva a abrir el recibo.",
                    "Validación de pago",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            if (!Permisos.PuedeCobrar(SesionActual.Cargo))
            {
                MessageBox.Show(
                    SesionActual.Iniciada
                        ? "Su cargo (" + SesionActual.Cargo + ") no está autorizado para cobrar.\n" +
                          "Solo pueden cobrar un Administrador o un Cajero."
                        : "Debe iniciar sesión para cobrar.",
                    "Validación de pago",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            // 2. Verificar el importe total.
            if (total <= 0)
            {
                MessageBox.Show(
                    "El pedido no tiene un total válido.",
                    "Validación de pago",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            // 3. Verificar la forma de pago.
            bool metodoSeleccionado =
    rbPagoEfectivo.Checked ||
    rbPagoTarjeta.Checked ||
    rbPagoYape.Checked ||
    rbPagoPlin.Checked ||
    rbPagoTransferencia.Checked ||
    rbPagoMixto.Checked;
            if (!metodoSeleccionado)
            {
                MessageBox.Show(
                    "Seleccione una forma de pago.",
                    "Validación de pago",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return false;
            }

            // 4. Validar el pago en efectivo.
            if (rbPagoEfectivo.Checked)
            {
                decimal montoRecibido =
                    numMontoRecibido.Value;

                if (montoRecibido < total)
                {
                    decimal faltante =
                        total - montoRecibido;

                    MessageBox.Show(
                        "El dinero recibido es insuficiente.\n" +
                        "Faltan: S/ " + faltante.ToString("N2"),
                        "Validación de pago",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return false;
                }
            }
            if (rbPagoMixto.Checked)
            {
                return ValidarPagoMixto();
            }
            // Todas las validaciones son correctas.
            return true;

        }


        private bool ValidarPagoMixto()
        {
            if (total <= 0)
            {
                MessageBox.Show("El total del pedido no es valido.",
                    "Pago mixto", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            decimal efectivo = numMontoPago1.Value;
            decimal segundoPago = total - efectivo;

            // Los dos pagos deben tener importe mayor que cero.
            if (efectivo <= 0m || segundoPago <= 0m)
            {
                MessageBox.Show(
                    "El efectivo debe ser mayor que cero y menor " +
                    "que el total del pedido.",
                    "Pago mixto", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            // El segundo metodo nunca puede ser Efectivo.
            if (cmbMetodoPago2.SelectedIndex == -1 ||
                cmbMetodoPago2.Text == "Efectivo")
            {
                MessageBox.Show(
                    "Seleccione Yape, Plin, Tarjeta o Transferencia " +
                    "para el segundo pago.",
                    "Pago mixto", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (cmbMetodoPago2.Text == "Tarjeta" &&
                cmbMetodoPago2.SelectedIndex == -1)
            {
                MessageBox.Show("Seleccione Debito o Credito.",
                    "Pago mixto", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }


        private void btnImprimirBoleta_Click(
            object sender, EventArgs e)
        {
            try
            {
                // No imprimir como pagado sin verificar SQL.
                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand cmd = new SqlCommand(@"
            SELECT COUNT(*)
            FROM dbo.PEDIDO P
            WHERE P.id_pedido = @Pedido
              AND P.estado = 'cobrado'
              AND (
                  EXISTS (
                      SELECT 1 FROM dbo.PAGO PG
                      WHERE PG.id_pedido = P.id_pedido
                  )
                  OR EXISTS (
                      SELECT 1 FROM dbo.VENTA_INTERNA V
                      WHERE V.id_pedido = P.id_pedido
                  )
              );", conexion))
                {
                    cmd.Parameters.Add(
                        "@Pedido", SqlDbType.Int).Value = idPedido;

                    conexion.Open();

                    if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                    {
                        MessageBox.Show(
                            "Primero registre el cobro del pedido.");
                        return;
                    }
                }

                using (PrintDocument documento =
                    CrearDocumentoImpresion())
                {
                    documento.Print();
                }

                MessageBox.Show(
                    "Recibo enviado a la impresora.\n" +
                    "No se ha registrado un segundo cobro.",
                    "Impresión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al imprimir:\n" + ex.Message);
            }
        }




        private PrintDocument CrearDocumentoImpresion()
        {
            // Verificar la impresora seleccionada.
            if (cmbImpresoraBoleta.SelectedItem == null)
            {
                throw new Exception(
                    "Primero seleccione una impresora.");
            }

            PrintDocument documento = new PrintDocument();

            // Utilizar la impresora elegida.
            documento.PrinterSettings.PrinterName =
                cmbImpresoraBoleta.SelectedItem.ToString();

            if (!documento.PrinterSettings.IsValid)
            {
                documento.Dispose();

                throw new Exception(
                    "La impresora seleccionada no está disponible.");
            }

            // Contar productos.
            int cantidadProductos = 0;

            foreach (DataGridViewRow fila in dgvDetallePedido.Rows)
            {
                if (!fila.IsNewRow)
                {
                    cantidadProductos++;
                }
            }

            // Papel térmico de 80 mm.
            // Las medidas están en centésimas de pulgada.
            // Alto según el contenido (sin desperdiciar papel): base del recibo,
            // cada producto con holgura para nombres en dos líneas, y los datos
            // opcionales (cliente, mesa, reimpresión).
            int alto = 760 + cantidadProductos * 36
                     + (IncluirCliente() ? 60 : 0)
                     + (ModoReimpresion ? 20 : 0)
                     + 20;

            documento.DefaultPageSettings.PaperSize =
                new PaperSize("Ticket 80 mm", 315, alto);

            documento.DefaultPageSettings.Margins =
                new Margins(8, 8, 8, 8);

            // Cantidad de copias.
            documento.PrinterSettings.Copies =
                (short)Math.Max(1, (int)numCopiasBoleta.Value);

            // Conectar la función que dibuja el ticket.
            documento.PrintPage += Documento_PrintPage;

            return documento;
        }



        // ==================================================
        // IMPRESIÓN (papel térmico de 80 mm)
        // --------------------------------------------------
        // Mismo contenido y orden que la vista previa. Todo se
        // centra respecto al ancho del papel; importes a la
        // derecha con el mismo formato (S/ 0.00); los textos
        // largos pasan a otra línea; QR y barras sin deformar.
        // ==================================================
        private void Documento_PrintPage(
            object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            // Columna de impresión centrada en el papel (72 mm útiles en 80 mm).
            float papel = e.PageBounds.Width;
            float ancho = Math.Min(272f, papel - 32f);
            float x = (papel - ancho) / 2f - e.PageSettings.HardMarginX;
            float y = Math.Max(10f, e.MarginBounds.Top - e.PageSettings.HardMarginY);

            using (Font empresa = new Font("Arial", 11, FontStyle.Bold))
            using (Font titulo = new Font("Arial", 10.5f, FontStyle.Bold))
            using (Font negrita = new Font("Arial", 8.5f, FontStyle.Bold))
            using (Font texto = new Font("Arial", 8.5f, FontStyle.Regular))
            using (Font pequeño = new Font("Arial", 7.5f, FontStyle.Regular))
            using (Font nota = new Font("Arial", 6.5f, FontStyle.Regular))
            using (Font totalFont = new Font("Arial", 12, FontStyle.Bold))
            using (StringFormat centro = new StringFormat { Alignment = StringAlignment.Center })
            using (StringFormat derecha = new StringFormat { Alignment = StringAlignment.Far })
            {
                // Texto centrado (varias líneas si hace falta).
                Action<string, Font, float> centrado = (t, f, despues) =>
                {
                    float h = g.MeasureString(t, f, (int)ancho, centro).Height;
                    g.DrawString(t, f, Brushes.Black, new RectangleF(x, y, ancho, h + 2), centro);
                    y += h + despues;
                };
                // Texto a la izquierda (varias líneas si hace falta).
                Action<string, Font, float> izquierda = (t, f, despues) =>
                {
                    float h = g.MeasureString(t, f, (int)ancho).Height;
                    g.DrawString(t, f, Brushes.Black, new RectangleF(x, y, ancho, h + 2));
                    y += h + despues;
                };
                // Concepto a la izquierda e importe a la derecha.
                Action<string, string, Font> fila = (concepto, importe, f) =>
                {
                    float anchoImporte = Math.Max(g.MeasureString(importe, f).Width + 4, 80f);
                    float h = Math.Max(g.MeasureString(concepto, f, (int)(ancho - anchoImporte - 6)).Height, f.GetHeight(g));
                    g.DrawString(concepto, f, Brushes.Black, new RectangleF(x, y, ancho - anchoImporte - 6, h + 2));
                    g.DrawString(importe, f, Brushes.Black, new RectangleF(x + ancho - anchoImporte, y, anchoImporte, h + 2), derecha);
                    y += h + 4;
                };
                Action<float> linea = espacio =>
                {
                    y += espacio;
                    using (Pen p = new Pen(Color.Black, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                        g.DrawLine(p, x, y, x + ancho, y);
                    y += espacio;
                };

                // ---------- 1. Logotipo y empresa ----------
                if (guna2PictureBox7.Image != null)
                {
                    Image logo = guna2PictureBox7.Image;
                    float anchoLogo = 150f, altoLogo = anchoLogo * logo.Height / logo.Width;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(logo, x + (ancho - anchoLogo) / 2, y, anchoLogo, altoLogo);
                    y += altoLogo + 6;
                }
                centrado("AL ESTILO PIURANO S.A.C.", empresa, 2);
                centrado("RUC: 20601234567", texto, 1);
                centrado("Av. Principal 123, Trujillo - Perú", pequeño, 1);
                centrado("Tel.: 987 654 321", pequeño, 2);
                linea(6);

                // ---------- 2. Título y número interno ----------
                centrado("RECIBO DE VENTA", titulo, 1);
                centrado(numeroBoletaDemo, negrita, 1);
                if (ModoReimpresion)
                    centrado("REIMPRESIÓN", negrita, 1);
                linea(6);

                // ---------- 3. Datos del pedido y del cliente ----------
                izquierda("Fecha: " + fechaPedido.ToString("dd/MM/yyyy") + "   " + fechaPedido.ToString("HH:mm"), texto, 2);
                string canalTicket = (tipoPedido ?? "").Trim().ToLower();
                if (canalTicket == "salon" || canalTicket == "salón")
                {
                    izquierda("Mesa: " + numeroMesa + "  ·  Salón", texto, 2);
                    izquierda("Atendido por: " + atendidoPor, texto, 2);
                }
                else
                {
                    izquierda("Tipo de pedido: " + FormatearCanal(tipoPedido), texto, 2);
                    izquierda(canalTicket == "delivery" ? "Entrega: A domicilio" : "Retiro: En el local", texto, 2);
                }
                if (IncluirCliente())
                {
                    izquierda("Cliente: " + txtNombreCliente.Text.Trim(), texto, 2);
                    if (!string.IsNullOrWhiteSpace(txtDNICliente.Text))
                        izquierda(EtiquetaDocumento(txtDNICliente.Text.Trim()) + ": " + txtDNICliente.Text.Trim(), texto, 2);
                }
                linea(5);

                // ---------- 4. Detalle: Cant. | Descripción | P. Unit. | Importe ----------
                float colCant = g.MeasureString("Cant.", negrita).Width + 6f, colPrecio = 52f, colImporte = 58f;
                float colDesc = ancho - colCant - colPrecio - colImporte - 6f;
                g.DrawString("Cant.", negrita, Brushes.Black, new RectangleF(x, y, colCant, 16), centro);
                g.DrawString("Descripción", negrita, Brushes.Black, x + colCant, y);
                g.DrawString("P. Unit.", negrita, Brushes.Black, new RectangleF(x + colCant + colDesc, y, colPrecio, 16), derecha);
                g.DrawString("Importe", negrita, Brushes.Black, new RectangleF(x + ancho - colImporte, y, colImporte, 16), derecha);
                y += negrita.GetHeight(g) + 5;

                foreach (LineaBoleta l in productos)
                {
                    string descripcion = l.Descripcion ?? "";
                    float h = Math.Max(g.MeasureString(descripcion, texto, (int)colDesc).Height, texto.GetHeight(g));
                    g.DrawString(l.Cantidad.ToString(), texto, Brushes.Black, new RectangleF(x, y, colCant, h), centro);
                    g.DrawString(descripcion, texto, Brushes.Black, new RectangleF(x + colCant, y, colDesc, h + 2));
                    g.DrawString(Monto(l.PrecioUnitario), texto, Brushes.Black, new RectangleF(x + colCant + colDesc, y, colPrecio, h), derecha);
                    g.DrawString(Monto(l.Importe), texto, Brushes.Black, new RectangleF(x + ancho - colImporte, y, colImporte, h), derecha);
                    y += h + 4;
                }
                linea(5);

                // ---------- 5. Importes (mismos cálculos de siempre) ----------
                decimal baseSinIGV = subtotal - descuentoTotal - igvIncluido;
                fila("Subtotal (con IGV):", FormatoSoles(subtotal), texto);
                fila(TextoDescuento(), FormatoSoles(descuentoTotal), texto);
                fila("Base imponible:", FormatoSoles(baseSinIGV), texto);
                fila("IGV (18%):", FormatoSoles(igvIncluido), texto);
                linea(5);
                fila("TOTAL", FormatoSoles(total), totalFont);
                linea(5);

                // ---------- 6. Pago ----------
                izquierda("Forma de pago: " + lblBoletaFormaPago.Text, texto, 2);
                izquierda("Caja: " + (cajeroRecibo ?? SesionActual.NombreCompleto ?? ""), pequeño, 8);

                // ---------- 7. QR, código de barras y número del pedido ----------
                if (picQRBoleta.Image != null)
                {
                    float qr = 120f;
                    Codigo(g, picQRBoleta.Image, new RectangleF(x + (ancho - qr) / 2, y, qr, qr));
                    y += qr + 8;
                }
                if (picCodigoBarrasBoleta.Image != null)
                {
                    Image barras = picCodigoBarrasBoleta.Image;
                    float anchoBarras = Math.Min(220f, ancho);
                    float altoBarras = anchoBarras * barras.Height / barras.Width;     // sin deformar
                    Codigo(g, barras, new RectangleF(x + (ancho - anchoBarras) / 2, y, anchoBarras, altoBarras));
                    y += altoBarras + 3;
                }
                centrado(CodigoPedido, negrita, 10);

                // ---------- 8. Agradecimientos y nota discreta ----------
                centrado("¡Gracias por su compra!", titulo, 2);
                centrado("¡Gracias por su preferencia!", texto, 1);
                centrado("¡Sabor que te encanta!", texto, 8);
                centrado(NotaInterna, nota, 0);

                e.HasMorePages = false;
            }
        }


        // QR y barras nítidos y sin el borde gris que deja el escalado.
        private static void Codigo(Graphics g, Image imagen, RectangleF destino)
        {
            var antes = g.InterpolationMode;
            var desfase = g.PixelOffsetMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            using (System.Drawing.Imaging.ImageAttributes atributos = new System.Drawing.Imaging.ImageAttributes())
            {
                atributos.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                g.DrawImage(imagen, new[] { destino.Location, new PointF(destino.Right, destino.Top), new PointF(destino.Left, destino.Bottom) },
                    new RectangleF(0, 0, imagen.Width, imagen.Height), GraphicsUnit.Pixel, atributos);
            }
            g.InterpolationMode = antes;
            g.PixelOffsetMode = desfase;
        }

        private void GenerarCodigos()
        {
            // ======================================
            // 1. VERIFICAR EL PEDIDO
            // ======================================

            if (idPedido <= 0 || total <= 0)
            {
                throw new Exception(
                    "No hay un pedido válido para generar códigos.");
            }

            // ======================================
            // 2. MISMO NÚMERO DE LA BOLETA DEMO
            // ======================================

            // La vista previa, el QR, el código de barras y
            // la impresión utilizan la misma numeración.
            if (string.IsNullOrEmpty(numeroBoletaDemo))
            {
                GenerarBoletaPrueba();
            }

            string codigoPedido = CodigoPedido;

            // ======================================
            // 3. INFORMACIÓN DEL CÓDIGO QR
            // ======================================

            string datosQR =
                "AL ESTILO PIURANO" +
    "\nRECIBO: " + (numeroRecibo ?? "pendiente de cobro") +
    "\nPEDIDO: " + CodigoPedido +
    "\nFECHA: " +
    fechaPedido.ToString("dd/MM/yyyy HH:mm") +
    "\nTOTAL: S/ " +
    total.ToString("0.00",
        System.Globalization.CultureInfo.InvariantCulture);

            // ======================================
            // 4. GENERAR CÓDIGO QR
            // ======================================

            ZXing.BarcodeWriter generadorQR =
                new ZXing.BarcodeWriter();

            generadorQR.Format =
                ZXing.BarcodeFormat.QR_CODE;

            generadorQR.Options =
                new ZXing.Common.EncodingOptions
                {
                    Width = 300,
                    Height = 300,
                    Margin = 2
                };

            Image imagenQR = generadorQR.Write(datosQR);

            // Mostrar QR en el formulario.
            picQRBoleta.Image = imagenQR;

            picQRBoleta.SizeMode =
                PictureBoxSizeMode.Zoom;

            // ======================================
            // 5. GENERAR CÓDIGO DE BARRAS
            // ======================================

            ZXing.BarcodeWriter generadorBarras =
                new ZXing.BarcodeWriter();

            generadorBarras.Format =
                ZXing.BarcodeFormat.CODE_128;

            generadorBarras.Options =
                new ZXing.Common.EncodingOptions
                {
                    Width = 500,
                    Height = 100,
                    Margin = 10,
                    PureBarcode = true
                };

            Image imagenBarras =
                generadorBarras.Write(codigoPedido);

            // Mostrar código de barras.
            picCodigoBarrasBoleta.Image =
                imagenBarras;

            picCodigoBarrasBoleta.SizeMode =
                PictureBoxSizeMode.Zoom;

            // ======================================
            // 6. MOSTRAR IDENTIFICADOR
            // ======================================

            lblBoletaCodigoFinal.Text = CodigoPedido;
        }


        // FUNCIONALIDAD FUTURA: escribe registros reales en SQL Server.
        // NO se llama desde la impresión demostrativa.
        private int RegistrarPagoSQL(
            SqlConnection conexion,
            SqlTransaction transaccion,
            int idCajero)
        {
            // 1. Validar los datos.
            if (idPedido <= 0)
            {
                throw new Exception(
                    "No existe un pedido seleccionado.");
            }

            if (idCajero <= 0)
            {
                throw new Exception(
                    "No se identificó al cajero.");
            }

            if (total <= 0)
            {
                throw new Exception(
                    "El total del pedido no es válido.");
            }

            // 2. Identificar el método de pago.
            string metodo = "";

            if (rbPagoEfectivo.Checked)
            {
                metodo = "Efectivo";

                if (numMontoRecibido.Value < total)
                {
                    throw new Exception(
                        "El dinero recibido es insuficiente.");
                }
            }
            else if (rbPagoTarjeta.Checked)
            {
                metodo = "Tarjeta";
            }
            else if (rbPagoYape.Checked)
            {
                metodo = "Yape";
            }
            else if (rbPagoMixto.Checked)
            {
                metodo = "Plin";
            }
            else if (rbPagoMixto.Checked)
            {
                // Esta rutina guarda un UNICO pago.
                // Mixto necesita dos importes en RegistrarCobroSQL.
                throw new InvalidOperationException(
                    "Pago mixto requiere registrar ambos medios " +
                    "mediante RegistrarCobroSQL.");
            }
            else if (rbPagoTransferencia.Checked)
            {
                metodo = "Transferencia";
            }
            else
            {
                throw new Exception(
                    "Seleccione una forma de pago.");
            }

            // 3. Registrar el pago.
            // Esta consulta debe ejecutarse dentro
            // de una transacción SQL activa.

            const string sql = @"
        IF NOT EXISTS
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
        AND EXISTS
        (
            SELECT 1
            FROM dbo.PEDIDO
            WHERE id_pedido = @Pedido
              AND estado <> 'cancelado'
        )
        BEGIN
            INSERT INTO dbo.PAGO
            (
                fecha_hora,
                [método],
                importe,
                nro_referencia,
                id_pedido,
                id_cajero,
                id_cierre
            )
            VALUES
            (
                SYSDATETIME(),
                @Metodo,
                @Importe,
                NULL,
                @Pedido,
                @Cajero,
                NULL
            );

            SELECT CAST(SCOPE_IDENTITY() AS INT);
        END";

            using (SqlCommand cmd =
                new SqlCommand(sql, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Pedido", SqlDbType.Int).Value = idPedido;

                cmd.Parameters.Add(
                    "@Cajero", SqlDbType.Int).Value = idCajero;

                cmd.Parameters.Add(
                    "@Metodo", SqlDbType.VarChar, 20).Value =
                    metodo;

                SqlParameter parametroImporte =
                    cmd.Parameters.Add(
                        "@Importe", SqlDbType.Decimal);

                parametroImporte.Precision = 18;
                parametroImporte.Scale = 2;
                parametroImporte.Value = total;

                object resultado = cmd.ExecuteScalar();

                if (resultado == null ||
                    resultado == DBNull.Value)
                {
                    throw new Exception(
                        "No se registró el pago. " +
                        "Verifique que el pedido exista, " +
                        "esté activo y no esté cobrado.");
                }

                return Convert.ToInt32(resultado);
            }
        }


        // FUNCIONALIDAD FUTURA: escribe PAGO y COMPROBANTE.
        // NO se llama desde la impresión demostrativa.
        private string GenerarBoletaSQL(int idCajero)
        {
            // ==========================================
            // 1. VALIDACIONES INICIALES
            // ==========================================

            if (idPedido <= 0)
            {
                throw new Exception(
                    "No hay un pedido seleccionado.");
            }

            if (idCajero <= 0)
            {
                throw new Exception(
                    "Debe identificarse al cajero.");
            }

            // Esta serie debe estar configurada
            // para el emisor real.
            string serie = "B001";

            using (SqlConnection conexion =
                new SqlConnection(cadenaConexion))
            {
                conexion.Open();

                // Una sola transacción para ambos registros.
                using (SqlTransaction transaccion =
                    conexion.BeginTransaction(
                        IsolationLevel.Serializable))
                {
                    try
                    {
                        // ==================================
                        // 2. COMPROBAR PEDIDO Y TOTAL
                        // ==================================

                        const string sqlPedido = @"
                    SELECT
                        ISNULL((
                            SELECT SUM(
                                cantidad * (precio_unitario
                                - ISNULL(descuento, 0)))
                            FROM dbo.DETALLE_PEDIDO_PLATO
                            WHERE id_pedido = P.id_pedido
                        ), 0)
                        +
                        ISNULL((
                            SELECT SUM(
                                cantidad * (precio_unitario
                                - ISNULL(descuento, 0)))
                            FROM dbo.DETALLE_PEDIDO_COMBO
                            WHERE id_pedido = P.id_pedido
                        ), 0)
                        -
                        ISNULL(P.descuento_aplicado, 0)
                        +
                        ISNULL(P.propina, 0)
                    FROM dbo.PEDIDO P
                        WITH (UPDLOCK, HOLDLOCK)
                    WHERE P.id_pedido = @Pedido
                      AND P.estado <> 'cancelado';";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                sqlPedido, conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int
                            ).Value = idPedido;

                            object resultado = cmd.ExecuteScalar();

                            if (resultado == null ||
                                resultado == DBNull.Value)
                            {
                                throw new Exception(
                                    "El pedido no está disponible.");
                            }

                            decimal totalSQL =
                                Convert.ToDecimal(resultado);

                            if (totalSQL != total)
                            {
                                throw new Exception(
                                    "El total del pedido cambió. " +
                                    "Vuelva a cargar los datos.");
                            }
                        }

                        // ==================================
                        // 3. VERIFICAR EL CAJERO
                        // ==================================

                        using (SqlCommand cmd =
                            new SqlCommand(@"
                        SELECT COUNT(*)
                        FROM dbo.EMPLEADO
                        WHERE id_empleado = @Cajero;",
                                conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Cajero", SqlDbType.Int
                            ).Value = idCajero;

                            if (Convert.ToInt32(
                                cmd.ExecuteScalar()) != 1)
                            {
                                throw new Exception(
                                    "El empleado del cajero no existe.");
                            }
                        }

                        // ==================================
                        // 4. EVITAR REGISTROS DUPLICADOS
                        // ==================================

                        using (SqlCommand cmd =
                            new SqlCommand(@"
                        SELECT
                            (SELECT COUNT(*)
                             FROM dbo.PAGO
                             WHERE id_pedido = @Pedido)
                            +
                            (SELECT COUNT(*)
                             FROM dbo.COMPROBANTE
                             WHERE id_pedido = @Pedido);",
                                conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int
                            ).Value = idPedido;

                            if (Convert.ToInt32(
                                cmd.ExecuteScalar()) > 0)
                            {
                                throw new Exception(
                                    "Este pedido ya registra " +
                                    "un pago o comprobante.");
                            }
                        }

                        // ==================================
                        // 5. BLOQUEAR EL CORRELATIVO
                        // ==================================

                        using (SqlCommand cmd =
                            new SqlCommand(@"
                        DECLARE @Resultado INT;

                        EXEC @Resultado = sp_getapplock
                            @Resource = @Recurso,
                            @LockMode = 'Exclusive',
                            @LockOwner = 'Transaction',
                            @LockTimeout = 10000;

                        SELECT @Resultado;",
                                conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Recurso", SqlDbType.NVarChar, 255
                            ).Value = "COMPROBANTE_B001";

                            int bloqueo = Convert.ToInt32(
                                cmd.ExecuteScalar());

                            if (bloqueo < 0)
                            {
                                throw new Exception(
                                    "No se pudo reservar " +
                                    "la numeración del comprobante.");
                            }
                        }

                        // ==================================
                        // 6. OBTENER EL SIGUIENTE NÚMERO
                        // ==================================

                        int siguienteNumero;

                        using (SqlCommand cmd =
                            new SqlCommand(@"
                        SELECT
                            ISNULL(MAX(
                                TRY_CONVERT(INT, numero)
                            ), 0) + 1
                        FROM dbo.COMPROBANTE
                        WHERE tipo = 'B'
                          AND serie = @Serie;",
                                conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Serie", SqlDbType.VarChar, 4
                            ).Value = serie;

                            siguienteNumero = Convert.ToInt32(
                                cmd.ExecuteScalar());
                        }

                        if (siguienteNumero > 99999999)
                        {
                            throw new Exception(
                                "Se agotó el correlativo de la serie.");
                        }

                        string numero =
                            siguienteNumero.ToString("D8");

                        // ==================================
                        // 7. REGISTRAR PAGO
                        // ==================================

                        RegistrarPagoSQL(
                            conexion,
                            transaccion,
                            idCajero);

                        // ==================================
                        // 8. REGISTRAR COMPROBANTE PENDIENTE
                        // ==================================

                        const string sqlComprobante = @"
                    INSERT INTO dbo.COMPROBANTE
                    (
                        tipo,
                        serie,
                        numero,
                        fecha_emision,
                        identificacion_cliente,
                        nombre_cliente,
                        estado,
                        id_pedido
                    )
                    VALUES
                    (
                        @Tipo,
                        @Serie,
                        @Numero,
                        CONVERT(DATE, SYSDATETIME()),
                        @DNI,
                        @Cliente,
                        @Estado,
                        @Pedido
                    );";

                        using (SqlCommand cmd =
                            new SqlCommand(
                                sqlComprobante,
                                conexion,
                                transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Tipo", SqlDbType.Char, 1
                            ).Value = "B";

                            cmd.Parameters.Add(
                                "@Serie", SqlDbType.VarChar, 4
                            ).Value = serie;

                            cmd.Parameters.Add(
                                "@Numero", SqlDbType.VarChar, 8
                            ).Value = numero;

                            string dni = txtDNICliente.Text.Trim();
                            string nombre = txtNombreCliente.Text.Trim();

                            cmd.Parameters.Add(
                                "@DNI", SqlDbType.VarChar, 20
                            ).Value =
                                dni.Length == 0
                                ? (object)DBNull.Value
                                : dni;

                            cmd.Parameters.Add(
                                "@Cliente", SqlDbType.VarChar, 255
                            ).Value =
                                nombre.Length == 0
                                ? (object)DBNull.Value
                                : nombre;

                            cmd.Parameters.Add(
                                "@Estado", SqlDbType.VarChar, 20
                            ).Value = "PENDIENTE";

                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int
                            ).Value = idPedido;

                            cmd.ExecuteNonQuery();
                        }

                        // ==================================
                        // 9. CONFIRMAR LA TRANSACCIÓN
                        // ==================================

                        transaccion.Commit();

                        return serie + "-" + numero;
                    }
                    catch
                    {
                        // Si algo falla, no guardar
                        // el pago ni el comprobante.
                        transaccion.Rollback();
                        throw;
                    }
                }
            }
        }


        private bool RegistrarVentaInterna()
        {
            string sql = @"
        SET NOCOUNT ON;

        DECLARE @Estado VARCHAR(30);
        DECLARE @YaRegistrada BIT = 0;

        -- Bloquear el pedido mientras se registra.
        SELECT @Estado =
            LOWER(LTRIM(RTRIM(estado)))
        FROM dbo.PEDIDO WITH (UPDLOCK, HOLDLOCK)
        WHERE id_pedido = @Pedido
          AND id_sucursal = @Sucursal;

        IF @Estado IS NULL
            THROW 51001,
                'El pedido no existe.', 1;

        IF @Estado = 'cancelado'
            THROW 51002,
                'No se puede cobrar un pedido cancelado.', 1;

        -- Evitar duplicar pagos existentes.
        IF EXISTS (
            SELECT 1 FROM dbo.PAGO
            WHERE id_pedido = @Pedido
        )
        OR EXISTS (
            SELECT 1 FROM dbo.COMPROBANTE
            WHERE id_pedido = @Pedido
        )
            THROW 51003,
                'El pedido ya tiene pago o comprobante.', 1;

        -- Comprobar si existe una venta interna.
        IF EXISTS (
            SELECT 1
            FROM dbo.VENTA_INTERNA
            WHERE id_pedido = @Pedido
        )
            SET @YaRegistrada = 1;

        IF @Estado NOT IN (
            'pendiente',
            'en_preparacion',
            'listo',
            'entregado',
            'cobrado'
        )
            THROW 51004,
                'El estado del pedido no permite cobrar.', 1;

        IF @Estado = 'cobrado'
           AND @YaRegistrada = 0
            THROW 51005,
                'El pedido ya figura como cobrado.', 1;

        -- Registrar solo si no estaba contabilizado.
        IF @YaRegistrada = 0
        BEGIN
            INSERT INTO dbo.VENTA_INTERNA
                (id_pedido, importe)
            VALUES
                (@Pedido, @Importe);
        END;

        -- Actualizar el estado del pedido.
        UPDATE dbo.PEDIDO
        SET estado = 'cobrado'
        WHERE id_pedido = @Pedido
          AND id_sucursal = @Sucursal;

        IF @@ROWCOUNT <> 1
            THROW 51006,
                'No se pudo actualizar el pedido.', 1;

        -- 1: venta nueva.
        -- 0: venta ya existente.
        SELECT CASE
            WHEN @YaRegistrada = 1 THEN 0
            ELSE 1
        END;";

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
                        using (SqlCommand cmd =
                            new SqlCommand(
                                sql, conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido",
                                SqlDbType.Int).Value = idPedido;

                            cmd.Parameters.Add(
                                "@Sucursal",
                                SqlDbType.Int).Value = 1;

                            SqlParameter importe =
                                cmd.Parameters.Add(
                                    "@Importe",
                                    SqlDbType.Decimal);

                            importe.Precision = 12;
                            importe.Scale = 2;
                            importe.Value = total;

                            int resultado = Convert.ToInt32(
                                cmd.ExecuteScalar());

                            transaccion.Commit();

                            return resultado == 1;
                        }
                    }
                    catch
                    {
                        try
                        {
                            transaccion.Rollback();
                        }
                        catch
                        {
                            // La transacción pudo finalizar
                            // por un error de SQL Server.
                        }

                        throw;
                    }
                }
            }
        }

        private void btnCerrarBoleta_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnRegistrarCobro_Click(object sender, EventArgs e)
        {
            // Validar el pedido y la forma de pago.
            if (!ValidarPago())
                return;

            List<KeyValuePair<string, decimal>> pagos =
                new List<KeyValuePair<string, decimal>>();

            try
            {
                // ======================================
                // 1. PAGO MIXTO
                // ======================================
                if (rbPagoMixto.Checked)
                {
                    decimal efectivo = numMontoPago1.Value;
                    decimal segundoPago = total - efectivo;

                    string metodo2 = cmbMetodoPago2.Text.Trim();

                    if (efectivo <= 0 || segundoPago <= 0)
                    {
                        MessageBox.Show(
                            "Los dos pagos deben ser mayores que cero.");
                        return;
                    }

                    if (metodo2 != "Yape" &&
                        metodo2 != "Plin" &&
                        metodo2 != "Tarjeta" &&
                        metodo2 != "Transferencia")
                    {
                        MessageBox.Show(
                            "Seleccione el segundo método de pago.");
                        return;
                    }

                    pagos.Add(
                        new KeyValuePair<string, decimal>(
                            "Efectivo", efectivo));

                    pagos.Add(
                        new KeyValuePair<string, decimal>(
                            metodo2, segundoPago));
                }
                else
                {
                    // ======================================
                    // 2. PAGO CON UN SOLO MÉTODO
                    // ======================================
                    string metodo = "";

                    if (rbPagoEfectivo.Checked)
                        metodo = "Efectivo";

                    else if (rbPagoYape.Checked)
                        metodo = "Yape";

                    else if (rbPagoTarjeta.Checked)
                        metodo = "Tarjeta";

                    else if (rbPagoTransferencia.Checked)
                        metodo = "Transferencia";

                   
                    else if (rbPagoPlin.Checked)
                        metodo = "Plin";

                    if (metodo == "")
                    {
                        MessageBox.Show(
                            "Seleccione una forma de pago.");
                        return;
                    }

                    pagos.Add(
                        new KeyValuePair<string, decimal>(
                            metodo, total));
                }

                // Comprador con documento y aún no registrado: se pide su
                // autorización para guardarlo en Gestión de Clientes.
                if (!AutorizarRegistroComprador())
                    return;

                // ======================================
                // 3. CONFIRMAR EL COBRO
                // ======================================
                bool conIdentificacion = IncluirCliente() && txtDNICliente.Text.Trim().Length > 0;
                if (total > MontoIdentificacionSunat && !conIdentificacion &&
                    MessageBox.Show(
                        "El total supera S/ " + MontoIdentificacionSunat.ToString("N2") + ".\n\n" +
                        "En una boleta electrónica SUNAT sería obligatorio registrar el DNI o CE y el nombre del cliente. " +
                        "Este recibo es un documento interno.\n\n¿Desea cobrar sin los datos del cliente?",
                        "Datos del cliente", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;

                DialogResult respuesta = MessageBox.Show(
                    "Pedido N.º " + idPedido +
                    "\nTotal: S/ " + total.ToString("N2") +
                    (IncluirCliente() ? "\nCliente: " + txtNombreCliente.Text.Trim() : "") +
                    "\n\n¿Desea confirmar el cobro?",
                    "Confirmar cobro",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (respuesta != DialogResult.Yes)
                    return;

                // Evitar doble clic mientras se procesa.
                btnRegistrarCobro.Enabled = false;
                guna2HtmlLabel22.Enabled = false;

                // ======================================
                // 4. GUARDAR EN SQL SERVER
                // ======================================
                RegistrarCobroSQL(pagos);

                // Actualizar el texto del comprobante.
                if (rbPagoMixto.Checked)
                {
                    lblBoletaFormaPago.Text =
                        "Efectivo + " + cmbMetodoPago2.Text;
                }
                else
                {
                    lblBoletaFormaPago.Text = pagos[0].Key;
                }

                // Número real del recibo en la vista previa, QR e impresión.
                numeroBoletaDemo = numeroRecibo;
                cajeroRecibo = SesionActual.NombreCompleto;
                GenerarCodigos();
                ActualizarVistaPrevia();
                BloquearDatosCliente();

                // El pedido ya se cobró.
                // Permitir imprimir, sin volver a cobrar.
                btnImprimirBoleta.Enabled = true;

                MessageBox.Show(
                    "Cobro registrado correctamente.\n\n" +
                    "Recibo " + numeroRecibo + "\n" +
                    "Pedido N.º " + idPedido +
                    "\nTotal: S/ " + total.ToString("N2") +
                    (clienteRegistradoAlCobrar.HasValue
                        ? "\n\nCliente vinculado en Gestión de Clientes: " + txtNombreCliente.Text.Trim()
                        : "") +
                    "\n\nYa puede imprimir el recibo.",
                    "Cobro exitoso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // Si falló el registro, permitir reintentar.
                btnRegistrarCobro.Enabled = true;
                guna2HtmlLabel22.Enabled = true;

                MessageBox.Show(
                    "No se pudo registrar el cobro:\n\n" +
                    ex.Message,
                    "Error de cobro",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
