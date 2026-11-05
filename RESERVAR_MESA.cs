using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class RESERVAR_MESA : Form
    {
        private const int IdSucursal = 1;
        private const int AnticipacionMaximaMinutos = 120;
        private const int ToleranciaLlegadaMinutos = 30;
        private static readonly TimeSpan HoraApertura = new TimeSpan(12, 0, 0);
        private static readonly TimeSpan HoraCierre = new TimeSpan(21, 30, 0);

        private readonly string cadenaConexion =
            @"Server=LAPTOP-GH63852S\SQLEXPRESS;
              Database=AL_ESTILO_PIURA;
              Integrated Security=True;";

        public RESERVAR_MESA()
        {
            InitializeComponent();
            Text = "Nueva reserva";
            StartPosition = FormStartPosition.CenterParent;

            // Solo reservas para hoy; el cliente elige la hora.
            dtpFechaReserva.Format = DateTimePickerFormat.Short;
            dtpFechaReserva.Value = DateTime.Today;
            dtpFechaReserva.Enabled = false;
            cmbHoraReserva.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHoraReserva.Enabled = true;

            cmbMesa.DropDownStyle = ComboBoxStyle.DropDownList;
            guna2ComboBox3.DropDownStyle = ComboBoxStyle.DropDownList;
            guna2ComboBox3.Items.Clear();
            for (int i = 1; i <= 4; i++)
            {
                guna2ComboBox3.Items.Add(i.ToString());
            }
            guna2ComboBox3.SelectedItem = "2";

            txtCliente.MaxLength = 100;
            txtTelefono.MaxLength = 9;
            txtObservacion.MaxLength = 250;
            txtObservacion.PlaceholderText = "Observación opcional...";

            // En el Designer no está vinculado el botón Guardar.
            btnGuardarReserva.Click -= btnGuardarReserva_Click;
            btnGuardarReserva.Click += btnGuardarReserva_Click;

            // Desvincular el manejador heredado del Designer antes de agregar uno solo.
            btnCancelarReserva.Click -= btnCancelarReserva_Click_1;
            btnCancelarReserva.Click -= btnCancelarReserva_Click;
            btnCancelarReserva.Click += btnCancelarReserva_Click;

            // El botón de cierre conserva el manejador del Designer.
            guna2ComboBox3.SelectedIndexChanged -= Personas_Changed;
            guna2ComboBox3.SelectedIndexChanged += Personas_Changed;
            cmbHoraReserva.SelectedIndexChanged -= Personas_Changed;
            cmbHoraReserva.SelectedIndexChanged += Personas_Changed;

            CargarHorariosDisponibles();
            CargarMesasDisponibles();
            this.Shown += (sender, e) =>
            {
                if (cmbHoraReserva.Items.Count == 0)
                {
                    btnGuardarReserva.Enabled = false;

                    MessageBox.Show(
                        "No hay horarios disponibles para reservar hoy.\n\n" +
                        "El restaurante atiende hasta las 9:30 p. m.\n" +
                        "Las reservas deben respetar el horario de cierre.",
                        "Horario de reservas",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            };

        }

        // Franja de hoy: entre apertura y cierre menos 30 minutos de tolerancia.
        // Solamente ofrece horas posteriores a ahora y no más de 2 horas adelante.
        private void CargarHorariosDisponibles()
        {
            cmbHoraReserva.Items.Clear();
            DateTime ahora = DateTime.Now;
            DateTime apertura = DateTime.Today.Add(HoraApertura);
            DateTime ultimaHoraReserva = DateTime.Today.Add(HoraCierre)
                .AddMinutes(-ToleranciaLlegadaMinutos);

            for (DateTime hora = apertura;
                 hora <= ultimaHoraReserva;
                 hora = hora.AddMinutes(30))
            {
                if (hora > ahora &&
                    hora <= ahora.AddMinutes(AnticipacionMaximaMinutos))
                    cmbHoraReserva.Items.Add(hora.ToString("HH:mm"));
            }

            cmbHoraReserva.SelectedIndex = cmbHoraReserva.Items.Count > 0 ? 0 : -1;
            btnGuardarReserva.Enabled = cmbHoraReserva.Items.Count > 0;
        }

        private DateTime? ObtenerFechaHora()
        {
            TimeSpan hora;
            if (!TimeSpan.TryParse(cmbHoraReserva.Text, out hora))
                return null;
            return DateTime.Today.Add(hora);
        }

        private void Personas_Changed(object sender, EventArgs e)
        {
            CargarMesasDisponibles();
        }

        private void CargarMesasDisponibles()
        {
            cmbMesa.DataSource = null;
            cmbMesa.Items.Clear();

            int personas;
            if (!int.TryParse(guna2ComboBox3.Text, out personas)
                || personas < 1 || personas > 4 || !ObtenerFechaHora().HasValue)
            {
                return;
            }

            const string sql = @"
                SELECT M.id_mesa,
                       'Mesa ' + CAST(M.[número] AS VARCHAR(10)) AS NombreMesa
                FROM dbo.MESA M
                WHERE M.id_sucursal = @Sucursal
                  AND LOWER(LTRIM(RTRIM(M.estado))) = 'libre'
                  AND M.capacidad >= @Personas
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.RESERVA_MESA R
                      WHERE R.id_mesa = M.id_mesa
                        AND R.estado = 'confirmada'
                  )
                ORDER BY M.[número];";

            try
            {
                DataTable tabla = new DataTable();
                using (SqlConnection conexion = new SqlConnection(cadenaConexion))
                using (SqlCommand comando = new SqlCommand(sql, conexion))
                {
                    comando.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                    comando.Parameters.Add("@Personas", SqlDbType.Int).Value = personas;
                    using (SqlDataAdapter adaptador = new SqlDataAdapter(comando))
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
                MessageBox.Show("Error al consultar las mesas: " + ex.Message,
                    "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnGuardarReserva_Click(object sender, EventArgs e)
        {
            string cliente = txtCliente.Text.Trim();
            string telefono = txtTelefono.Text.Trim();
            string observacion = txtObservacion.Text.Trim();
            int personas;

            if (cliente.Length < 3 || cliente.Length > 100)
            {
                MessageBox.Show("Ingrese un nombre válido de 3 a 100 caracteres.");
                txtCliente.Focus();
                return;
            }
            foreach (char caracter in cliente)
            {
                if (char.IsDigit(caracter))
                {
                    MessageBox.Show("El nombre del cliente no debe contener números.");
                    txtCliente.Focus();
                    return;
                }
            }
            if (telefono.Length != 9)
            {
                MessageBox.Show("El teléfono debe tener 9 dígitos.");
                txtTelefono.Focus();
                return;
            }
            foreach (char caracter in telefono)
            {
                if (caracter < '0' || caracter > '9')
                {
                    MessageBox.Show("El teléfono solo debe contener números.");
                    txtTelefono.Focus();
                    return;
                }
            }
            if (!int.TryParse(guna2ComboBox3.Text, out personas)
                || personas < 1 || personas > 4)
            {
                MessageBox.Show("Seleccione entre 1 y 4 personas.");
                return;
            }
            if (cmbMesa.SelectedIndex < 0 || cmbMesa.SelectedValue == null)
            {
                MessageBox.Show("Seleccione una mesa disponible.");
                return;
            }
            if (observacion.Length > 250)
            {
                MessageBox.Show("La observación no puede superar 250 caracteres.");
                return;
            }

            DateTime? fechaReserva = ObtenerFechaHora();
            DateTime ahora = DateTime.Now;
            DateTime ultimaHora = DateTime.Today.Add(HoraCierre)
                .AddMinutes(-ToleranciaLlegadaMinutos);
            if (!fechaReserva.HasValue || fechaReserva.Value <= ahora ||
                fechaReserva.Value > ahora.AddMinutes(AnticipacionMaximaMinutos) ||
                fechaReserva.Value.TimeOfDay < HoraApertura ||
                fechaReserva.Value > ultimaHora)
            {
                MessageBox.Show("Elija un horario válido de hoy, dentro de las " +
                    "próximas 2 horas y del horario de atención.");
                CargarHorariosDisponibles();
                CargarMesasDisponibles();
                return;
            }

            int idMesa = Convert.ToInt32(cmbMesa.SelectedValue);
            int idReserva = 0;

            try
            {
                using (SqlConnection conexion = new SqlConnection(cadenaConexion))
                {
                    conexion.Open();
                    using (SqlTransaction transaccion =
                        conexion.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // Revalidar con la hora del servidor SQL; el equipo puede tener otra hora.
                            const string validarFechaSQL = @"
                                SELECT CASE WHEN
                                    CONVERT(date, @Fecha) = CONVERT(date, SYSDATETIME())
                                    AND @Fecha > SYSDATETIME()
                                    AND @Fecha <= DATEADD(MINUTE, 120, SYSDATETIME())
                                    AND CONVERT(time, @Fecha) >= '12:00:00'
                                    AND CONVERT(time, @Fecha) <= '21:00:00'
                                    THEN 1 ELSE 0 END;";
                            using (SqlCommand cmd = new SqlCommand(validarFechaSQL, conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = fechaReserva.Value;
                                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                                    throw new Exception("La hora seleccionada ya no está disponible o queda fuera del horario del local.");
                            }

                            // Bloquear la mesa: no admitir dos reservas a la vez.
                            const string sqlMesa = @"
                                SELECT capacidad FROM dbo.MESA WITH (UPDLOCK, HOLDLOCK)
                                WHERE id_mesa = @Mesa AND id_sucursal = @Sucursal
                                  AND LOWER(LTRIM(RTRIM(estado))) = 'libre';";
                            int capacidad;
                            using (SqlCommand comando = new SqlCommand(sqlMesa, conexion, transaccion))
                            {
                                comando.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                comando.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                                object resultado = comando.ExecuteScalar();
                                if (resultado == null || resultado == DBNull.Value)
                                    throw new Exception("La mesa ya no está libre.");
                                capacidad = Convert.ToInt32(resultado);
                            }
                            if (personas > capacidad)
                                throw new Exception("La mesa no tiene capacidad suficiente.");

                            const string sqlConflicto = @"
                                SELECT COUNT(*) FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                                WHERE id_mesa = @Mesa AND estado = 'confirmada';";
                            using (SqlCommand comando =
                                new SqlCommand(sqlConflicto, conexion, transaccion))
                            {
                                comando.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                if (Convert.ToInt32(comando.ExecuteScalar()) > 0)
                                    throw new Exception("La mesa ya tiene una reserva activa.");
                            }

                            // Registrar la fecha/hora elegida por el cliente, NO SYSDATETIME().
                            const string sqlInsertar = @"
                                INSERT INTO dbo.RESERVA_MESA
                                    (id_mesa, nombre_cliente, telefono,
                                     fecha_hora, personas, observacion, estado)
                                VALUES
                                    (@Mesa, @Cliente, @Telefono,
                                     @Fecha, @Personas, @Observacion, 'confirmada');
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";
                            using (SqlCommand comando =
                                new SqlCommand(sqlInsertar, conexion, transaccion))
                            {
                                comando.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                comando.Parameters.Add("@Cliente", SqlDbType.NVarChar, 100).Value = cliente;
                                comando.Parameters.Add("@Telefono", SqlDbType.VarChar, 9).Value = telefono;
                                comando.Parameters.Add("@Personas", SqlDbType.Int).Value = personas;
                                comando.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = fechaReserva.Value;
                                comando.Parameters.Add("@Observacion", SqlDbType.NVarChar, 250).Value =
                                    observacion.Length == 0 ? (object)DBNull.Value : observacion;
                                idReserva = Convert.ToInt32(comando.ExecuteScalar());
                            }

                            const string sqlOcupar = @"
                                UPDATE dbo.MESA SET estado = 'reservada'
                                WHERE id_mesa = @Mesa AND id_sucursal = @Sucursal
                                  AND LOWER(LTRIM(RTRIM(estado))) = 'libre';";
                            using (SqlCommand comando =
                                new SqlCommand(sqlOcupar, conexion, transaccion))
                            {
                                comando.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                comando.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                                if (comando.ExecuteNonQuery() != 1)
                                    throw new Exception("No se pudo apartar la mesa.");
                            }

                            transaccion.Commit();
                        }
                        catch
                        {
                            try { transaccion.Rollback(); } catch { }
                            throw;
                        }
                    }
                }

                MessageBox.Show("Reserva N.º " + idReserva +
                    " registrada para las " + fechaReserva.Value.ToString("HH:mm") +
                    ".\nTolerancia de llegada: 30 minutos.", "Reserva registrada",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo guardar la reserva: " + ex.Message,
                    "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CargarMesasDisponibles();
            }
        }

        private void btnCancelarReserva_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // Eventos referidos por RESERVAR_MESA.Designer.cs: NO borrarlos.
        private void btnCerrarBoleta_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void guna2Button1_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void btnCancelarReserva_Click_1(object sender, EventArgs e)
        {
            Close();
        }
        private void guna2Panel3_Paint(object sender, PaintEventArgs e)
        {
        }
    }
}
