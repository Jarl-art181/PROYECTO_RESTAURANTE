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



        private Random aleatorio = new Random();
        private string numeroBoletaDemo = "";
        private readonly string cadenaConexion =
            BaseDatos.CadenaConexion;

        // Datos del pedido seleccionado
        private int idPedido = 0;
        private DateTime fechaPedido;
        private string tipoPedido = "";
        private string numeroMesa = "";
        private string atendidoPor = "";

        // Totales: los precios de los productos incluyen IGV
        private decimal subtotal = 0m;
        private decimal descuentoGeneral = 0m;
        private decimal descuentoTotal = 0m;
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
                get { return Cantidad * PrecioUnitario - DescuentoLinea; }
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
                    "Generar Boleta",
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
                    "Generar Boleta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                this.Close();
                return;
            }

            // 3. Cargar los datos desde SQL Server.
            try
            {

                CargarPedidoSQL();

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

                MessageBox.Show(
                    "Pedido N.º " + idPedido +
                    " cargado correctamente.",
                    "Generar Boleta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar el pedido:\n" + ex.Message,
                    "Generar Boleta",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                this.Close();
            }
        }




        private void RegistrarCobroSQL(
            List<KeyValuePair<string, decimal>> pagos)
        {
            if (!SesionActual.IdEmpleado.HasValue)
                throw new Exception(
                    "Debe iniciar sesión como cajero para cobrar.");

            int idCajero = SesionActual.IdEmpleado.Value;

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
                        // 1. Verificar que el empleado sea cajero activo.
                        using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*)
                    FROM dbo.CAJERO C
                    INNER JOIN dbo.EMPLEADO E
                        ON C.id_empleado = E.id_empleado
                    WHERE C.id_empleado = @Cajero
                      AND E.id_sucursal = 1
                      AND LOWER(LTRIM(RTRIM(E.estado)))
                          = 'activo';",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Cajero", SqlDbType.Int).Value =
                                idCajero;

                            if (Convert.ToInt32(
                                cmd.ExecuteScalar()) != 1)
                            {
                                throw new Exception(
                                    "El empleado no es un cajero activo.");
                            }
                        }

                        // 2. Bloquear y verificar el pedido.
                        decimal totalSQL;

                        using (SqlCommand cmd = new SqlCommand(@"
                    SELECT
                        CAST(
                            ISNULL((
                                SELECT SUM(
                                    cantidad * precio_unitario
                                    - ISNULL(descuento, 0))
                                FROM dbo.DETALLE_PEDIDO_PLATO
                                WHERE id_pedido = P.id_pedido
                            ), 0)
                            +
                            ISNULL((
                                SELECT SUM(
                                    cantidad * precio_unitario
                                    - ISNULL(descuento, 0))
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
                      AND P.estado IN ('listo', 'entregado');",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;

                            object resultado = cmd.ExecuteScalar();

                            if (resultado == null ||
                                resultado == DBNull.Value)
                            {
                                throw new Exception(
                                    "El pedido debe estar Listo o Entregado " +
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

                        // 5. Marcar el pedido como cobrado.
                        // El trigger verifica la suma de pagos.
                        using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE dbo.PEDIDO
                    SET estado = 'cobrado'
                    WHERE id_pedido = @Pedido
                      AND id_sucursal = 1
                      AND estado IN ('listo', 'entregado');",
                            conexion, transaccion))
                        {
                            cmd.Parameters.Add(
                                "@Pedido", SqlDbType.Int).Value =
                                idPedido;

                            if (cmd.ExecuteNonQuery() != 1)
                            {
                                throw new Exception(
                                    "No se pudo cerrar el pedido.");
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
                       M.[número] AS NumeroMesa,
                       LTRIM(RTRIM(ISNULL(E.nombre, '') + ' ' +
                                    ISNULL(E.apellidos, ''))) AS NombreMesero
                FROM dbo.PEDIDO P
                LEFT JOIN dbo.MESA M ON M.id_mesa = P.id_mesa
                LEFT JOIN dbo.EMPLEADO E ON E.id_empleado = P.id_mesero
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

                        fechaPedido = Convert.ToDateTime(lector["fecha_hora"]);
                        tipoPedido = Convert.ToString(lector["canal"]);
                        numeroMesa = lector["NumeroMesa"] == DBNull.Value
                            ? "No aplica" : Convert.ToString(lector["NumeroMesa"]);
                        atendidoPor = Convert.ToString(lector["NombreMesero"]);
                        if (string.IsNullOrWhiteSpace(atendidoPor))
                            atendidoPor = "No asignado";
                        descuentoGeneral = Convert.ToDecimal(lector["DescuentoGeneral"]);
                        propina = Convert.ToDecimal(lector["Propina"]);
                    }
                }

                // Impedir preparar una nueva boleta para un pedido ya cobrado.
                using (SqlCommand cmd = new SqlCommand(@"
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
                descuentoTotal += linea.DescuentoLinea;
            }
            decimal importeConIGV = subtotal - descuentoTotal;
            if (importeConIGV < 0)
                throw new Exception("Los descuentos superan el subtotal.");

            igvIncluido = Math.Round(importeConIGV * 18m / 118m,
                2, MidpointRounding.AwayFromZero);
            total = importeConIGV + propina;
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
                lblBoletaMesa.Location = new Point(86, 108);
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

                lblBoletaMesa.Location = new Point(145, 108);
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
            guna2HtmlLabel36.Text = "IGV incluido (18%):";

            // Mostrar los importes.
            lblBoletaSubtotal.Text = FormatoSoles(baseSinIGV);
            lblBoletaIGV.Text = FormatoSoles(igvIncluido);
            lblBoletaTotal.Text = FormatoSoles(total);




            // ======================================
            // 4. TÍTULOS DE LA BOLETA DEMOSTRATIVA
            // ======================================

            guna2HtmlLabel20.Text =
                "BOLETA DE VENTA";

            guna2HtmlLabel56.Text = "¡Gracias por su compra!";

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
            dgvBoletaDetalle.Columns[0].Width = 50;

            // Descripción.
            dgvBoletaDetalle.Columns[1].MinimumWidth = 100;

            dgvBoletaDetalle.Columns[1].AutoSizeMode =
                DataGridViewAutoSizeColumnMode.Fill;

            // Precio unitario.
            dgvBoletaDetalle.Columns[2].Width = 70;

            // Importe.
            dgvBoletaDetalle.Columns[3].Width = 75;

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

            foreach (LineaBoleta linea in productos)
            {
                dgvBoletaDetalle.Rows.Add(
                    linea.Cantidad,
                    linea.Descripcion,
                    linea.PrecioUnitario.ToString("N2"),
                    linea.Importe.ToString("N2")
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

            // El mismo número usado en la cabecera.
            lblBoletaCodigoFinal.Text =
                numeroBoletaDemo;
        }




        private void GenerarBoletaPrueba()
        {
            if (numeroBoletaDemo == "")
            {
                int numero = aleatorio.Next(1, 99999999);

                numeroBoletaDemo =
                    "B001-" + numero.ToString("D8");
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
            int alto = Math.Max(
                980,
                790 + cantidadProductos * 55);

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



        private void Documento_PrintPage(
            object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;

            // ======================================
            // 1. ANCHO SEGURO PARA IMPRESORA POS-80
            // ======================================

            float x = e.MarginBounds.Left + 4;
            float y = e.MarginBounds.Top;

            // No utilizar todo el ancho del papel.
            float ancho = Math.Min(
                240f, e.MarginBounds.Width - 26f);

            using (Font titulo = new Font(
                "Arial", 11, FontStyle.Bold))
            using (Font negrita = new Font(
                "Arial", 9, FontStyle.Bold))
            using (Font texto = new Font(
                "Arial", 8, FontStyle.Regular))
            using (Font pequeño = new Font(
                "Arial", 7.5f, FontStyle.Regular))
            using (Font totalFont = new Font(
                "Arial", 11, FontStyle.Bold))
            using (StringFormat centro = new StringFormat())
            using (StringFormat derecha = new StringFormat())
            {
                centro.Alignment = StringAlignment.Center;
                derecha.Alignment = StringAlignment.Far;

                // ======================================
                // 2. LOGO DEL RESTAURANTE
                // ======================================

                if (guna2PictureBox7.Image != null)
                {
                    Image logo = guna2PictureBox7.Image;

                    float anchoLogo = 150f;

                    float altoLogo =
                        anchoLogo * logo.Height / logo.Width;

                    float posicionLogo =
                        x + (ancho - anchoLogo) / 2;

                    g.InterpolationMode =
                        System.Drawing.Drawing2D.InterpolationMode
                        .HighQualityBicubic;

                    g.DrawImage(
                        logo,
                        posicionLogo,
                        y,
                        anchoLogo,
                        altoLogo);

                    y += altoLogo + 6;
                }

                // ======================================
                // 3. INFORMACIÓN DE LA EMPRESA
                // ======================================

                g.DrawString(
                    "AL ESTILO PIURANO S.A.C.",
                    titulo,
                    Brushes.Black,
                    new RectangleF(x, y, ancho, 25),
                    centro);

                y += 23;

                g.DrawString(
                    "RUC : 20601234567",
                    texto,
                    Brushes.Black,
                    new RectangleF(x, y, ancho, 18),
                    centro);

                y += 17;

                g.DrawString(
                    "Dirección : Av. Principal 123",
                    pequeño,
                    Brushes.Black,
                    new RectangleF(x, y, ancho, 18),
                    centro);

                y += 17;

                g.DrawString(
                    "Tel. : 987 654 321",
                    pequeño,
                    Brushes.Black,
                    new RectangleF(x, y, ancho, 18),
                    centro);

                y += 23;

                g.DrawLine(
                    Pens.Black, x, y, x + ancho, y);

                y += 10;

                // ======================================
                // 4. BOLETA DEMOSTRATIVA
                // ======================================

                g.DrawString(
                   "RECIBO DE VENTA",
                    negrita,
                    Brushes.Black,
                    new RectangleF(x, y, ancho, 22),
                    centro);

                y += 22;



                g.DrawString(
                    numeroBoletaDemo,
                    negrita,
                    Brushes.Black,
                    new RectangleF(x, y, ancho, 22),
                    centro);

                y += 28;


                // ======================================
                // 5. DATOS DEL PEDIDO
                // ======================================

                // Número de pedido
                g.DrawString(
                    "Pedido: " + idPedido,
                    texto, Brushes.Black, x, y);

                y += 18;

                // Fecha y hora
                g.DrawString(
                    "Fecha: " +
                    fechaPedido.ToString("dd/MM/yyyy HH:mm"),
                    texto, Brushes.Black, x, y);

                y += 18;

                // Modalidad
                g.DrawString(
                    "Tipo: " + FormatearCanal(tipoPedido),
                    texto, Brushes.Black, x, y);

                y += 18;

                // Identificar el tipo de pedido.
                string canalTicket = (tipoPedido ?? "")
                    .Trim().ToLower();

                // SOLO PARA SALÓN
                if (canalTicket == "salon" ||
                    canalTicket == "salón")
                {
                    g.DrawString(
                        "Mesa: " + numeroMesa,
                        texto, Brushes.Black, x, y);

                    y += 18;

                    string nombreAtencionTicket =
                        "Atendido por: " + atendidoPor;

                    float altoAtencionTicket = g.MeasureString(
                        nombreAtencionTicket,
                        pequeño,
                        (int)ancho).Height;

                    g.DrawString(
                        nombreAtencionTicket,
                        pequeño,
                        Brushes.Black,
                        new RectangleF(
                            x, y, ancho, altoAtencionTicket + 3));

                    y += altoAtencionTicket + 8;
                }
                // SOLO PARA DELIVERY
                else if (canalTicket == "delivery")
                {
                    g.DrawString(
                        "Entrega: A domicilio",
                        texto, Brushes.Black, x, y);

                    y += 20;
                }
                // SOLO PARA RECOJO
                else if (canalTicket == "recojo")
                {
                    g.DrawString(
                        "Retiro: En el local",
                        texto, Brushes.Black, x, y);

                    y += 20;
                }


                // ======================================
                // 6. DATOS DEL CLIENTE
                // ======================================

                if (!string.IsNullOrWhiteSpace(
                    txtNombreCliente.Text))
                {
                    string cliente =
                        "Cliente: " + txtNombreCliente.Text;

                    float altoCliente = g.MeasureString(
                        cliente, pequeño, (int)ancho).Height;

                    g.DrawString(
                        cliente,
                        pequeño,
                        Brushes.Black,
                        new RectangleF(
                            x, y, ancho, altoCliente + 3));

                    y += altoCliente + 4;
                }

                if (!string.IsNullOrWhiteSpace(
                    txtDNICliente.Text))
                {
                    g.DrawString(
                        "DNI: " + txtDNICliente.Text,
                        pequeño, Brushes.Black, x, y);

                    y += 18;
                }

                // ======================================
                // 7. ENCABEZADO DE PRODUCTOS
                // ======================================

                g.DrawLine(
                    Pens.Black, x, y, x + ancho, y);

                y += 9;

                g.DrawString(
                    "Cant.",
                    pequeño, Brushes.Black, x, y);

                g.DrawString(
                    "Producto",
                    negrita, Brushes.Black, x + 27, y);

                g.DrawString(
                    "Importe",
                    pequeño,
                    Brushes.Black,
                    new RectangleF(
                        x + ancho - 66,
                        y,
                        58,
                        20),
                    derecha);

                y += 22;

                // ======================================
                // 8. DETALLE DE PRODUCTOS
                // ======================================

                foreach (LineaBoleta linea in productos)
                {
                    string cantidad =
                        linea.Cantidad.ToString();

                    string descripcion =
                        linea.Descripcion ?? "";

                    string importe =
                        linea.Importe.ToString("N2");

                    // Reservar espacio para el importe.
                    float anchoDescripcion = ancho - 95f;

                    // Calcular la altura del nombre.
                    float altoDescripcion = g.MeasureString(
                        descripcion,
                        pequeño,
                        (int)anchoDescripcion).Height;

                    float altoFila = Math.Max(
                        20f, altoDescripcion + 4f);

                    // Cantidad.
                    g.DrawString(
                        cantidad,
                        pequeño,
                        Brushes.Black,
                        x,
                        y);

                    // Descripción en varias líneas.
                    g.DrawString(
                        descripcion,
                        pequeño,
                        Brushes.Black,
                        new RectangleF(
                            x + 27,
                            y,
                            anchoDescripcion,
                            altoFila));

                    // Importe dentro del papel.
                    g.DrawString(
                        importe,
                        pequeño,
                        Brushes.Black,
                        new RectangleF(
                            x + ancho - 66,
                            y,
                            58,
                            altoFila),
                        derecha);

                    y += altoFila + 5;
                }


                // ======================================
                // 9. TOTALES ALINEADOS
                // ======================================

                y += 5;

                g.DrawLine(
                    Pens.Black, x, y, x + ancho, y);

                y += 12;

                // 1. Subtotal original de productos.
                g.DrawString(
                    "Subtotal (con IGV):",
                    texto, Brushes.Black, x, y);

                g.DrawString(
                    FormatoSoles(subtotal),
                    texto,
                    Brushes.Black,
                    new RectangleF(
                        x + ancho - 105, y, 97, 20),
                    derecha);

                y += 21;

                // 2. Descuentos aplicados.
                g.DrawString(
                    "Descuento:",
                    texto, Brushes.Black, x, y);

                g.DrawString(
                    FormatoSoles(descuentoTotal),
                    texto,
                    Brushes.Black,
                    new RectangleF(
                        x + ancho - 105, y, 97, 20),
                    derecha);

                y += 21;

                // 3. Separar el IGV del importe neto.
                decimal baseSinIGV =
                    subtotal - descuentoTotal - igvIncluido;

                g.DrawString(
                    "Base sin IGV:",
                    texto, Brushes.Black, x, y);

                g.DrawString(
                    FormatoSoles(baseSinIGV),
                    texto,
                    Brushes.Black,
                    new RectangleF(
                        x + ancho - 105, y, 97, 20),
                    derecha);

                y += 21;

                // 4. Mostrar el IGV incluido.
                g.DrawString(
                    "IGV (18%):",
                    texto, Brushes.Black, x, y);

                g.DrawString(
                    FormatoSoles(igvIncluido),
                    texto,
                    Brushes.Black,
                    new RectangleF(
                        x + ancho - 105, y, 97, 20),
                    derecha);

                y += 25;

                g.DrawLine(
                    Pens.Black, x, y, x + ancho, y);

                y += 12;

                // ======================================
                // 10. TOTAL A PAGAR
                // ======================================

                g.DrawString(
                    "TOTAL:",
                    totalFont, Brushes.Black, x, y);

                g.DrawString(
                    FormatoSoles(total),
                    totalFont,
                    Brushes.Black,
                    new RectangleF(
                        x + ancho - 108,
                        y,
                        100,
                        28),
                    derecha);

                y += 35;

                // ======================================
                // 11. FORMA DE PAGO
                // ======================================

                g.DrawString(
                    "Pago: " + lblBoletaFormaPago.Text,
                    texto, Brushes.Black, x, y);

                y += 26;

                // ======================================
                // 12. CÓDIGO QR
                // ======================================

                if (picQRBoleta.Image != null)
                {
                    float tamañoQR = 115f;

                    float posicionQR =
                        x + (ancho - tamañoQR) / 2;

                    g.InterpolationMode =
                        System.Drawing.Drawing2D.InterpolationMode
                        .NearestNeighbor;

                    g.DrawImage(
                        picQRBoleta.Image,
                        posicionQR,
                        y,
                        tamañoQR,
                        tamañoQR);

                    y += tamañoQR + 10;
                }

                // ======================================
                // 13. CÓDIGO DE BARRAS
                // ======================================

                if (picCodigoBarrasBoleta.Image != null)
                {
                    float anchoBarras = 195f;
                    float altoBarras = 55f;

                    float posicionBarras =
                        x + (ancho - anchoBarras) / 2;

                    g.DrawImage(
                        picCodigoBarrasBoleta.Image,
                        posicionBarras,
                        y,
                        anchoBarras,
                        altoBarras);

                    y += altoBarras + 8;
                }

                g.DrawString(
                    numeroBoletaDemo,
                    pequeño,
                    Brushes.Black,
                    new RectangleF(
                        x, y, ancho, 20),
                    centro);

                y += 24;

                // ======================================
                // 14. MENSAJE FINAL
                // ======================================

                g.DrawLine(
                    Pens.Black, x, y, x + ancho, y);

                y += 12;

                g.DrawString(
                    "¡Gracias por su preferencia!",
                    negrita,
                    Brushes.Black,
                    new RectangleF(
                        x, y, ancho, 24),
                    centro);

                y += 23;

                g.DrawString(
                    "¡Sabor que te encanta!",
                    pequeño,
                    Brushes.Black,
                    new RectangleF(
                        x, y, ancho, 20),
                    centro);

                e.HasMorePages = false;
            }
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

            string codigoPedido = numeroBoletaDemo;

            // ======================================
            // 3. INFORMACIÓN DEL CÓDIGO QR
            // ======================================

            string datosQR =
                "AL ESTILO PIURANO" +
    "\nRECIBO: " + numeroBoletaDemo +
    "\nPEDIDO: " + idPedido +
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

            lblBoletaCodigoFinal.Text =
                numeroBoletaDemo;
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
                                cantidad * precio_unitario
                                - ISNULL(descuento, 0))
                            FROM dbo.DETALLE_PEDIDO_PLATO
                            WHERE id_pedido = P.id_pedido
                        ), 0)
                        +
                        ISNULL((
                            SELECT SUM(
                                cantidad * precio_unitario
                                - ISNULL(descuento, 0))
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

                // ======================================
                // 3. CONFIRMAR EL COBRO
                // ======================================
                DialogResult respuesta = MessageBox.Show(
                    "Pedido N.º " + idPedido +
                    "\nTotal: S/ " + total.ToString("N2") +
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

                // El pedido ya se cobró.
                // Permitir imprimir, sin volver a cobrar.
                btnImprimirBoleta.Enabled = true;

                MessageBox.Show(
                    "Cobro registrado correctamente.\n\n" +
                    "Pedido N.º " + idPedido +
                    "\nTotal: S/ " + total.ToString("N2") +
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
