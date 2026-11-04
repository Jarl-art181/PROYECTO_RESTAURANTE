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
using ZXing.Common;


namespace PROYECTO_RESTAURANTE
{
    public partial class RESERVAR_MESA : Form
    {
        private readonly string cadenaConexion =
             @"Server=LAPTOP-GH63852S\SQLEXPRESS;
              Database=AL_ESTILO_PIURA;
              Integrated Security=True;";

        public RESERVAR_MESA()
        {

            InitializeComponent();

            this.Text = "Nueva Reserva";
            this.StartPosition = FormStartPosition.CenterParent;

            // Solo fechas actuales o futuras.
            dtpFechaReserva.MinDate = DateTime.Today;
            dtpFechaReserva.Value = DateTime.Today;

            // Evitar que escriban valores no existentes.
            cmbMesa.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHoraReserva.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHoraReserva.DropDownStyle = ComboBoxStyle.DropDownList;

            // HORARIOS: de 12:00 a 21:30.
            cmbHoraReserva.Items.Clear();

            for (int hora = 12; hora <= 21; hora++)
            {
                cmbHoraReserva.Items.Add(
                    hora.ToString("00") + ":00");

                cmbHoraReserva.Items.Add(
                    hora.ToString("00") + ":30");
            }

            cmbHoraReserva.SelectedItem = "19:00";

            // CANTIDAD DE PERSONAS.
            cmbHoraReserva.Items.Clear();

            for (int i = 1; i <= 10; i++)
            {
                cmbHoraReserva.Items.Add(i.ToString());
            }

            cmbHoraReserva.SelectedItem = "2";

            txtTelefono.MaxLength = 9;

            // Conectar eventos una sola vez.
            dtpFechaReserva.ValueChanged -= DatosReserva_Changed;
            dtpFechaReserva.ValueChanged += DatosReserva_Changed;

            cmbHoraReserva.SelectedIndexChanged -= DatosReserva_Changed;
            cmbHoraReserva.SelectedIndexChanged += DatosReserva_Changed;

            cmbHoraReserva.SelectedIndexChanged -= DatosReserva_Changed;
            cmbHoraReserva.SelectedIndexChanged += DatosReserva_Changed;

            btnGuardarReserva.Click -= btnGuardarReserva_Click;
            btnGuardarReserva.Click += btnGuardarReserva_Click;

            btnGuardarReserva.Click -= btnCancelarReserva_Click;
            btnGuardarReserva.Click += btnCancelarReserva_Click;

            btnCerrarReserva.Click -= btnCancelarReserva_Click;
            btnCerrarReserva.Click += btnCancelarReserva_Click;

            CargarMesasDisponibles();

        }

        // ==========================================
        // 1. OBTENER FECHA Y HORA DE LA RESERVA
        // ==========================================

        private DateTime? ObtenerFechaHora()
        {
            TimeSpan hora;

            if (!TimeSpan.TryParse(
                cmbHoraReserva.Text, out hora))
            {
                return null;
            }

            return dtpFechaReserva.Value.Date.Add(hora);
        }


        // ==========================================
        // 2. ACTUALIZAR MESAS DISPONIBLES
        // ==========================================

        private void DatosReserva_Changed(
            object sender, EventArgs e)
        {
            CargarMesasDisponibles();
        }


        // ==========================================
        // 3. CARGAR MESAS DESDE SQL SERVER
        // ==========================================

        private void CargarMesasDisponibles()
        {
            cmbMesa.DataSource = null;
            cmbMesa.Items.Clear();

            DateTime? fechaReserva = ObtenerFechaHora();

            int personas;

            if (!fechaReserva.HasValue)
                return;

            if (!int.TryParse(
                cmbHoraReserva.Text, out personas))
                return;

            // No permitir reservas en el pasado.
            if (fechaReserva.Value <= DateTime.Now)
                return;

            DateTime inicio = fechaReserva.Value;
            DateTime fin = inicio.AddHours(2);

            string sql = @"
                SELECT
                    M.id_mesa,
                    'Mesa ' +
                    CAST(M.[número] AS VARCHAR(10))
                        AS NombreMesa

                FROM dbo.MESA M

                WHERE M.id_sucursal = @Sucursal
                  AND LOWER(LTRIM(RTRIM(M.estado))) =
                      'libre'

                  AND M.capacidad >= @Personas

                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.RESERVA_MESA R
                      WHERE R.id_mesa = M.id_mesa
                        AND R.estado = 'confirmada'
                        AND R.fecha_hora < @Fin
                        AND DATEADD(
                            MINUTE, 120, R.fecha_hora
                        ) > @Inicio
                  )

                ORDER BY M.[número];";

            try
            {
                DataTable tabla = new DataTable();

                using (SqlConnection conexion =
                    new SqlConnection(cadenaConexion))
                using (SqlCommand comando =
                    new SqlCommand(sql, conexion))
                {
                    comando.Parameters.Add(
                        "@Sucursal",
                        SqlDbType.Int).Value = 1;

                    comando.Parameters.Add(
                        "@Personas",
                        SqlDbType.Int).Value = personas;

                    comando.Parameters.Add(
                        "@Inicio",
                        SqlDbType.DateTime2).Value = inicio;

                    comando.Parameters.Add(
                        "@Fin",
                        SqlDbType.DateTime2).Value = fin;

                    using (SqlDataAdapter adaptador =
                        new SqlDataAdapter(comando))
                    {
                        adaptador.Fill(tabla);
                    }
                }

                cmbMesa.DisplayMember = "NombreMesa";
                cmbMesa.ValueMember = "id_mesa";
                cmbMesa.DataSource = tabla;

                cmbMesa.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al cargar mesas:\n" +
                    ex.Message,
                    "Nueva Reserva",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // ==========================================
        // 4. GUARDAR RESERVA
        // ==========================================

        private void btnGuardarReserva_Click(
            object sender, EventArgs e)
        {
            string cliente = txtCliente.Text.Trim();
            string telefono = txtTelefono.Text.Trim();
            string observacion =
                txtObservacion.Text.Trim();

            // VALIDAR CLIENTE
            if (cliente.Length < 3)
            {
                MessageBox.Show(
                    "Ingrese el nombre del cliente.");

                txtCliente.Focus();
                return;
            }

            foreach (char letra in cliente)
            {
                if (char.IsDigit(letra))
                {
                    MessageBox.Show(
                        "El nombre no puede contener números.");

                    txtCliente.Focus();
                    return;
                }
            }

            // VALIDAR TELÉFONO
            if (telefono.Length != 9)
            {
                MessageBox.Show(
                    "El teléfono debe tener 9 dígitos.");

                txtTelefono.Focus();
                return;
            }

            foreach (char numero in telefono)
            {
                if (!char.IsDigit(numero))
                {
                    MessageBox.Show(
                        "El teléfono solo debe contener números.");

                    txtTelefono.Focus();
                    return;
                }
            }

            // VALIDAR MESA
            if (cmbMesa.SelectedIndex < 0)
            {
                MessageBox.Show(
                    "Seleccione una mesa disponible.");
                return;
            }

            // VALIDAR PERSONAS
            int personas;

            if (!int.TryParse(
                cmbHoraReserva.Text, out personas) ||
                personas <= 0)
            {
                MessageBox.Show(
                    "Seleccione la cantidad de personas.");
                return;
            }

            // VALIDAR FECHA
            DateTime? fecha = ObtenerFechaHora();

            if (!fecha.HasValue ||
                fecha.Value <= DateTime.Now)
            {
                MessageBox.Show(
                    "Seleccione una fecha y hora futuras.");
                return;
            }

            if (observacion.Length > 250)
            {
                MessageBox.Show(
                    "La observación no debe superar " +
                    "los 250 caracteres.");
                return;
            }

            int idMesa =
                Convert.ToInt32(cmbMesa.SelectedValue);

            DateTime inicio = fecha.Value;
            DateTime fin = inicio.AddHours(2);

            try
            {
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
                            // ===========================
                            // VERIFICAR MESA REAL
                            // ===========================

                            string sqlValidar = @"
                                SELECT capacidad
                                FROM dbo.MESA WITH
                                    (UPDLOCK, HOLDLOCK)

                                WHERE id_mesa = @Mesa
                                  AND id_sucursal = @Sucursal
                                  AND LOWER(
                                      LTRIM(RTRIM(estado))
                                  ) = 'libre';";

                            int capacidad;

                            using (SqlCommand comando =
                                new SqlCommand(
                                    sqlValidar,
                                    conexion,
                                    transaccion))
                            {
                                comando.Parameters.Add(
                                    "@Mesa",
                                    SqlDbType.Int).Value = idMesa;

                                comando.Parameters.Add(
                                    "@Sucursal",
                                    SqlDbType.Int).Value = 1;

                                object resultado =
                                    comando.ExecuteScalar();

                                if (resultado == null)
                                {
                                    throw new Exception(
                                        "La mesa ya no está libre.");
                                }

                                capacidad =
                                    Convert.ToInt32(resultado);
                            }

                            if (personas > capacidad)
                            {
                                throw new Exception(
                                    "La cantidad de personas " +
                                    "supera la capacidad de la mesa.");
                            }

                            // ===========================
                            // VALIDAR RESERVAS EXISTENTES
                            // ===========================

                            string sqlConflicto = @"
                                SELECT COUNT(*)
                                FROM dbo.RESERVA_MESA WITH
                                    (UPDLOCK, HOLDLOCK)

                                WHERE id_mesa = @Mesa
                                  AND estado = 'confirmada'
                                  AND fecha_hora < @Fin
                                  AND DATEADD(
                                      MINUTE, 120, fecha_hora
                                  ) > @Inicio;";

                            using (SqlCommand comando =
                                new SqlCommand(
                                    sqlConflicto,
                                    conexion,
                                    transaccion))
                            {
                                comando.Parameters.Add(
                                    "@Mesa",
                                    SqlDbType.Int).Value = idMesa;

                                comando.Parameters.Add(
                                    "@Inicio",
                                    SqlDbType.DateTime2).Value = inicio;

                                comando.Parameters.Add(
                                    "@Fin",
                                    SqlDbType.DateTime2).Value = fin;

                                int existentes =
                                    Convert.ToInt32(
                                        comando.ExecuteScalar());

                                if (existentes > 0)
                                {
                                    throw new Exception(
                                        "Esta mesa ya tiene una reserva " +
                                        "en ese horario.");
                                }
                            }

                            // ===========================
                            // INSERTAR RESERVA
                            // ===========================

                            string sqlGuardar = @"
                                INSERT INTO dbo.RESERVA_MESA
                                (
                                    id_mesa,
                                    nombre_cliente,
                                    telefono,
                                    fecha_hora,
                                    personas,
                                    observacion,
                                    estado
                                )
                                VALUES
                                (
                                    @Mesa,
                                    @Cliente,
                                    @Telefono,
                                    @Fecha,
                                    @Personas,
                                    @Observacion,
                                    'confirmada'
                                );

                                SELECT CAST(
                                    SCOPE_IDENTITY() AS INT
                                );";

                            int idReserva;

                            using (SqlCommand comando =
                                new SqlCommand(
                                    sqlGuardar,
                                    conexion,
                                    transaccion))
                            {
                                comando.Parameters.Add(
                                    "@Mesa",
                                    SqlDbType.Int).Value = idMesa;

                                comando.Parameters.Add(
                                    "@Cliente",
                                    SqlDbType.NVarChar, 100).Value =
                                    cliente;

                                comando.Parameters.Add(
                                    "@Telefono",
                                    SqlDbType.VarChar, 9).Value =
                                    telefono;

                                comando.Parameters.Add(
                                    "@Fecha",
                                    SqlDbType.DateTime2).Value =
                                    inicio;

                                comando.Parameters.Add(
                                    "@Personas",
                                    SqlDbType.Int).Value =
                                    personas;

                                comando.Parameters.Add(
                                    "@Observacion",
                                    SqlDbType.NVarChar, 250).Value =
                                    observacion == ""
                                    ? (object)DBNull.Value
                                    : observacion;

                                idReserva = Convert.ToInt32(
                                    comando.ExecuteScalar());
                            }

                            transaccion.Commit();

                            MessageBox.Show(
                                "Reserva N.º " + idReserva +
                                " registrada correctamente.",
                                "Nueva Reserva",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);

                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        }
                        catch
                        {
                            try
                            {
                                transaccion.Rollback();
                            }
                            catch
                            {
                            }

                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al guardar la reserva:\n" +
                    ex.Message,
                    "Nueva Reserva",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                CargarMesasDisponibles();
            }
        }


        // ==========================================
        // 5. CANCELAR Y CERRAR
        // ==========================================

        private void btnCancelarReserva_Click(
            object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }




        private void btnCancelarBoleta_Click(object sender, EventArgs e)
        {
            this.Close();
        }


        private void numMontoRecibido_ValueChanged(object sender, EventArgs e)
        {
           
        }

      
        private void btnVistaPreviaBoleta_Click_1(object sender, EventArgs e)
        {
           
        }





        private void rbPagoEfectivo_CheckedChanged(object sender, EventArgs e)
        {
          
        }

        private void rbPagoTarjeta_CheckedChanged(object sender, EventArgs e)
        {
           
        }


       



       



     



     

        


        private void btnCerrarBoleta_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void guna2Panel3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnCancelarReserva_Click_1(object sender, EventArgs e)
        {
            Close();
        }
    }
}
