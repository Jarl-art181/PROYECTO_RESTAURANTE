using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class RESERVAR_MESA : Form
    {
        private const int IdSucursal = 1;
        private const int DuracionReservaMinutos = 120;

        private readonly string cadenaConexion =
            @"Server=LAPTOP-GH63852S\SQLEXPRESS;
              Database=AL_ESTILO_PIURA;
              Integrated Security=True;";

        public RESERVAR_MESA()
        {
            InitializeComponent();

            Text = "Nueva Reserva";
            StartPosition = FormStartPosition.CenterParent;

            // El calendario debe ser Guna2DateTimePicker.
            // Su FillColor debe ser White en el diseñador, NO Transparent.
            dtpFechaReserva.Format = DateTimePickerFormat.Short;
            dtpFechaReserva.MinDate = DateTime.Today;
            dtpFechaReserva.Value = DateTime.Today;

            cmbMesa.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHoraReserva.DropDownStyle = ComboBoxStyle.DropDownList;

            // En el Designer, el control "Personas" se llama guna2ComboBox3.
            guna2ComboBox3.DropDownStyle = ComboBoxStyle.DropDownList;

            // Cargar únicamente horarios válidos.
            CargarHorariosDisponibles();


            // =====================================
            // CONFIGURAR CANTIDAD DE PERSONAS
            // MÁXIMO 4 POR MESA
            // =====================================

            guna2ComboBox3.Items.Clear();

            for (int i = 1; i <= 4; i++)
            {
                guna2ComboBox3.Items.Add(i.ToString());
            }

            // Seleccionar 2 personas inicialmente.
            guna2ComboBox3.SelectedItem = "2";

            // No permitir escribir otros valores.
            guna2ComboBox3.DropDownStyle =
                ComboBoxStyle.DropDownList;


            txtTelefono.MaxLength = 9;
            txtCliente.MaxLength = 100;
            txtObservacion.MaxLength = 250;
            txtObservacion.PlaceholderText = "Observación opcional...";

            // Cuando cambian la fecha, hora o personas, consultar mesas.
            dtpFechaReserva.ValueChanged -= FechaReserva_ValueChanged;
            dtpFechaReserva.ValueChanged += FechaReserva_ValueChanged;

            cmbHoraReserva.SelectedIndexChanged -= DatosReserva_Changed;
            cmbHoraReserva.SelectedIndexChanged += DatosReserva_Changed;

            guna2ComboBox3.SelectedIndexChanged -= DatosReserva_Changed;
            guna2ComboBox3.SelectedIndexChanged += DatosReserva_Changed;

            // Un evento por botón. Quitar los eventos antiguos del Designer.
            btnGuardarReserva.Click -= btnGuardarReserva_Click;
            btnGuardarReserva.Click -= btnCancelarReserva_Click;
            btnGuardarReserva.Click += btnGuardarReserva_Click;

            btnCancelarReserva.Click -= btnCancelarReserva_Click_1;
            btnCancelarReserva.Click -= btnCancelarReserva_Click;
            btnCancelarReserva.Click += btnCancelarReserva_Click;

            btnCerrarReserva.Click -= guna2Button1_Click;
            btnCerrarReserva.Click -= btnCancelarReserva_Click;
            btnCerrarReserva.Click += btnCancelarReserva_Click;

            CargarMesasDisponibles();
        }
        private void FechaReserva_ValueChanged(
    object sender, EventArgs e)
        {
            CargarHorariosDisponibles();
        }


        private void CargarHorariosDisponibles()
        {
            cmbHoraReserva.Items.Clear();

            DateTime fechaSeleccionada =
                dtpFechaReserva.Value.Date;

            DateTime ahora = DateTime.Now;

            // Horario de atención: 12:00 a 21:30.
            for (int hora = 12; hora <= 21; hora++)
            {
                for (int minuto = 0; minuto <= 30; minuto += 30)
                {
                    DateTime horario =
                        fechaSeleccionada.AddHours(hora)
                                          .AddMinutes(minuto);

                    // Mostrar solo horarios futuros.
                    if (horario > ahora)
                    {
                        cmbHoraReserva.Items.Add(
                            horario.ToString("HH:mm"));
                    }
                }
            }

            // Seleccionar el primer horario disponible.
            if (cmbHoraReserva.Items.Count > 0)
            {
                cmbHoraReserva.SelectedIndex = 0;
            }
            else
            {
                cmbHoraReserva.SelectedIndex = -1;
            }

            CargarMesasDisponibles();
        }


        // 1. CONVERTIR FECHA Y HORA EN UNA SOLA FECHA.
        private DateTime? ObtenerFechaHora()
        {
            TimeSpan hora;
            if (!TimeSpan.TryParse(cmbHoraReserva.Text, out hora))
                return null;

            return dtpFechaReserva.Value.Date.Add(hora);
        }

        // 2. ACTUALIZAR MESAS CUANDO CAMBIA LA RESERVA.
        private void DatosReserva_Changed(object sender, EventArgs e)
        {
            CargarMesasDisponibles();
        }

        // 3. MOSTRAR MESAS LIBRES, CON CAPACIDAD Y SIN CRUCES.
        private void CargarMesasDisponibles()
        {
            cmbMesa.DataSource = null;
            cmbMesa.Items.Clear();

            DateTime? inicio = ObtenerFechaHora();
            int personas;
            if (!inicio.HasValue ||
                !int.TryParse(guna2ComboBox3.Text, out personas) ||
                personas < 1 || inicio.Value <= DateTime.Now)
                return;

            DateTime fin = inicio.Value.AddMinutes(DuracionReservaMinutos);

            const string sql = @"
                SELECT M.id_mesa,
                       'Mesa ' + CAST(M.[número] AS VARCHAR(10)) AS NombreMesa
                FROM dbo.MESA M
                WHERE M.id_sucursal = @Sucursal
                  AND LOWER(LTRIM(RTRIM(M.estado))) = 'libre'
                  AND M.capacidad >= @Personas
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.RESERVA_MESA R
                      WHERE R.id_mesa = M.id_mesa
                        AND R.estado = 'confirmada'
                        AND R.fecha_hora < @Fin
                        AND DATEADD(MINUTE, 120, R.fecha_hora) > @Inicio
                  )
                ORDER BY M.[número];";

            try
            {
                DataTable tabla = new DataTable();
                using (SqlConnection conexion = new SqlConnection(cadenaConexion))
                using (SqlCommand cmd = new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                    cmd.Parameters.Add("@Personas", SqlDbType.Int).Value = personas;
                    cmd.Parameters.Add("@Inicio", SqlDbType.DateTime2).Value = inicio.Value;
                    cmd.Parameters.Add("@Fin", SqlDbType.DateTime2).Value = fin;

                    using (SqlDataAdapter adaptador = new SqlDataAdapter(cmd))
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
                    "No se pudieron cargar las mesas:\n" + ex.Message,
                    "Nueva Reserva", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 4. VALIDAR Y GUARDAR RESERVA.
        private void btnGuardarReserva_Click(object sender, EventArgs e)
        {
            string cliente = txtCliente.Text.Trim();
            string telefono = txtTelefono.Text.Trim();
            string observacion = txtObservacion.Text.Trim();

            if (cliente.Length < 3 || cliente.Length > 100)
            {
                MessageBox.Show("Ingrese un nombre de cliente válido (3 a 100 caracteres).");
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
                MessageBox.Show("El teléfono debe tener exactamente 9 dígitos.");
                txtTelefono.Focus();
                return;
            }

            foreach (char caracter in telefono)
            {
                if (caracter < '0' || caracter > '9')
                {
                    MessageBox.Show("El teléfono debe contener únicamente números.");
                    txtTelefono.Focus();
                    return;
                }
            }

            int personas;

            if (!int.TryParse(guna2ComboBox3.Text, out personas)
                || personas < 1 || personas > 4)
            {
                MessageBox.Show(
                    "Solo se permiten reservas de 1 a 4 personas por mesa.",
                    "Nueva Reserva",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DateTime? inicio = ObtenerFechaHora();
            if (!inicio.HasValue || inicio.Value <= DateTime.Now)
            {
                MessageBox.Show("Seleccione una fecha y hora futuras.");
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

            int idMesa = Convert.ToInt32(cmbMesa.SelectedValue);
            DateTime fin = inicio.Value.AddMinutes(DuracionReservaMinutos);
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
                            // Bloquear la mesa para validar su estado actualizado.
                            const string sqlMesa = @"
                                SELECT capacidad
                                FROM dbo.MESA WITH (UPDLOCK, HOLDLOCK)
                                WHERE id_mesa = @Mesa
                                  AND id_sucursal = @Sucursal
                                  AND LOWER(LTRIM(RTRIM(estado))) = 'libre';";

                            int capacidad;
                            using (SqlCommand cmd = new SqlCommand(sqlMesa, conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                                object resultado = cmd.ExecuteScalar();
                                if (resultado == null || resultado == DBNull.Value)
                                    throw new Exception("La mesa ya no está disponible.");

                                capacidad = Convert.ToInt32(resultado);
                            }

                            if (personas > capacidad)
                                throw new Exception("La cantidad de personas supera la capacidad de la mesa.");

                            // Evitar doble reserva en horas que se superponen.
                            const string sqlConflicto = @"
                                SELECT COUNT(*)
                                FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                                WHERE id_mesa = @Mesa
                                  AND estado = 'confirmada'
                                  AND fecha_hora < @Fin
                                  AND DATEADD(MINUTE, 120, fecha_hora) > @Inicio;";

                            using (SqlCommand cmd = new SqlCommand(sqlConflicto, conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                cmd.Parameters.Add("@Inicio", SqlDbType.DateTime2).Value = inicio.Value;
                                cmd.Parameters.Add("@Fin", SqlDbType.DateTime2).Value = fin;

                                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                    throw new Exception("Esta mesa ya tiene una reserva en ese horario.");
                            }

                            const string sqlGuardar = @"
                                INSERT INTO dbo.RESERVA_MESA
                                    (id_mesa, nombre_cliente, telefono,
                                     fecha_hora, personas, observacion, estado)
                                VALUES
                                    (@Mesa, @Cliente, @Telefono,
                                     @Fecha, @Personas, @Observacion, 'confirmada');
                                SELECT CAST(SCOPE_IDENTITY() AS INT);";

                            using (SqlCommand cmd = new SqlCommand(sqlGuardar, conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                cmd.Parameters.Add("@Cliente", SqlDbType.NVarChar, 100).Value = cliente;
                                cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 9).Value = telefono;
                                cmd.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = inicio.Value;
                                cmd.Parameters.Add("@Personas", SqlDbType.Int).Value = personas;
                                cmd.Parameters.Add("@Observacion", SqlDbType.NVarChar, 250).Value =
                                    observacion.Length == 0 ? (object)DBNull.Value : observacion;

                                idReserva = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            transaccion.Commit();
                        }
                        catch
                        {
                            try { transaccion.Rollback(); }
                            catch { }
                            throw;
                        }
                    }
                }

                MessageBox.Show(
                    "Reserva N.º " + idReserva + " registrada correctamente.",
                    "Nueva Reserva", MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error al guardar la reserva:\n" + ex.Message,
                    "Nueva Reserva", MessageBoxButtons.OK, MessageBoxIcon.Error);

                CargarMesasDisponibles();
            }
        }

        // 5. CANCELAR O CERRAR SIN GUARDAR.
        private void btnCancelarReserva_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // Estos eventos siguen asociados en RESERVAR_MESA.Designer.cs.
        // Se conservan para no generar errores al compilar.
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
