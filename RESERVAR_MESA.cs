using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // Nueva reserva. Se abre desde el botón superior «Nueva reserva»
    // (el usuario elige la mesa) o desde «Reservar» del panel de la mesa
    // (la mesa llega preseleccionada). Ambos usan esta misma ventana,
    // las mismas reglas (ReservaReglas) y el mismo guardado.
    public partial class RESERVAR_MESA : Form
    {
        private const int IdSucursal = 1;

        private readonly string cadenaConexion =
            BaseDatos.CadenaConexion;

        // Mesa elegida desde Gestión de Mesas (0 = ninguna).
        private readonly int numeroMesaInicial;

        // Datos leídos una vez de SQL (se vuelven a leer si el guardado falla).
        private DataTable mesas;
        private readonly List<KeyValuePair<int, DateTime>> reservasConfirmadas =
            new List<KeyValuePair<int, DateTime>>();

        // Evita que los cambios internos de los ComboBox disparen recargas en cadena.
        private bool cargando;
        // La primera carga no muestra avisos (aún no hay elección del usuario).
        private bool iniciado;
        // Última mesa que el usuario tenía elegida (se conserva entre cambios).
        private int idMesaElegida;

        public RESERVAR_MESA() : this(0)
        {
        }

        public RESERVAR_MESA(int numeroMesa)
        {
            InitializeComponent();

            // Evita la fuga de GDI/USER del calendario interno de Guna2DateTimePicker.
            CorreccionesGuna.Aplicar(this);

            numeroMesaInicial = numeroMesa;
            Text = "Nueva reserva";
            StartPosition = FormStartPosition.CenterParent;

            // Hoy o una fecha posterior (hasta 30 días).
            dtpFechaReserva.Format = DateTimePickerFormat.Short;
            dtpFechaReserva.Value = DateTime.Today;
            dtpFechaReserva.MaxDate = DateTime.Today.AddDays(ReservaReglas.DiasAnticipacionMaxima);
            dtpFechaReserva.MinDate = DateTime.Today;
            dtpFechaReserva.Enabled = true;
            cmbHoraReserva.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbHoraReserva.Enabled = true;

            cmbMesa.DropDownStyle = ComboBoxStyle.DropDownList;
            guna2ComboBox3.DropDownStyle = ComboBoxStyle.DropDownList;
            guna2ComboBox3.Items.Clear();
            for (int i = 1; i <= ReservaReglas.MaxPersonasPorMesa; i++)
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

            // Cambios del usuario: fecha y personas recalculan horarios y mesas;
            // la hora solo recalcula las mesas. La mesa elegida se conserva.
            dtpFechaReserva.ValueChanged += (s, e) => { if (!cargando) { CargarHorarios(); CargarMesas(); } };
            guna2ComboBox3.SelectedIndexChanged += (s, e) => { if (!cargando) { CargarHorarios(); CargarMesas(); } };
            cmbHoraReserva.SelectedIndexChanged += (s, e) => { if (!cargando) CargarMesas(); };
            cmbMesa.SelectionChangeCommitted += (s, e) => { idMesaElegida = IdMesaSeleccionada(); };

            LeerDatos();
            if (numeroMesaInicial > 0)
                idMesaElegida = IdMesaPorNumero(numeroMesaInicial);
            CargarHorarios();
            CargarMesas();
            iniciado = true;

            Shown += (sender, e) =>
            {
                if (mesas == null)
                    return;

                if (cmbHoraReserva.Items.Count == 0)
                {
                    MessageBox.Show(
                        "No quedan horarios disponibles para hoy.\n\n" +
                        "Horario de atención: " + ReservaReglas.TextoHorario() + ".\n" +
                        "Elija una fecha posterior para reservar.",
                        "Horario de reservas", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (numeroMesaInicial > 0 && IdMesaSeleccionada() == 0)
                {
                    MessageBox.Show(
                        "La Mesa " + numeroMesaInicial + " no está disponible en el horario propuesto (" +
                        cmbHoraReserva.Text + ").\n\n" + MotivoNoDisponible(idMesaElegida) +
                        "\n\nElija otro horario o fecha y la mesa volverá a seleccionarse si queda libre.",
                        "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
        }

        // Número de la mesa con que se abrió la ventana (0 desde «Nueva reserva»).
        public int NumeroMesaInicial
        {
            get { return numeroMesaInicial; }
        }

        // ======================================================
        // DATOS
        // ======================================================

        private void LeerDatos()
        {
            const string sql = @"
                SELECT M.id_mesa, M.[número] AS numero, M.capacidad,
                       LOWER(LTRIM(RTRIM(M.estado))) AS estado,
                       CASE WHEN EXISTS (
                           SELECT 1 FROM dbo.PEDIDO P
                           WHERE P.id_mesa = M.id_mesa AND P.canal = 'salon'
                             AND P.estado IN ('pendiente','en_preparacion','listo','entregado'))
                            THEN 1 ELSE 0 END AS con_pedido
                FROM dbo.MESA M
                WHERE M.id_sucursal = @Sucursal
                ORDER BY M.[número];

                SELECT R.id_mesa, R.fecha_hora
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE M.id_sucursal = @Sucursal
                  AND R.estado = 'confirmada'
                  AND R.fecha_hora >= DATEADD(MINUTE, -@Duracion, CONVERT(datetime2, CONVERT(date, SYSDATETIME())));";

            try
            {
                DataSet ds = new DataSet();
                using (SqlConnection conexion = new SqlConnection(cadenaConexion))
                using (SqlCommand comando = new SqlCommand(sql, conexion))
                {
                    comando.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                    comando.Parameters.Add("@Duracion", SqlDbType.Int).Value = ReservaReglas.DuracionReservaMinutos;
                    using (SqlDataAdapter adaptador = new SqlDataAdapter(comando))
                    {
                        adaptador.Fill(ds);
                    }
                }

                mesas = ds.Tables[0];
                reservasConfirmadas.Clear();
                foreach (DataRow r in ds.Tables[1].Rows)
                {
                    reservasConfirmadas.Add(new KeyValuePair<int, DateTime>(
                        Convert.ToInt32(r["id_mesa"]), Convert.ToDateTime(r["fecha_hora"])));
                }
            }
            catch (Exception ex)
            {
                mesas = null;
                btnGuardarReserva.Enabled = false;
                MessageBox.Show("Error al consultar las mesas: " + ex.Message,
                    "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int IdMesaPorNumero(int numero)
        {
            if (mesas == null) return 0;
            foreach (DataRow r in mesas.Rows)
                if (Convert.ToInt32(r["numero"]) == numero)
                    return Convert.ToInt32(r["id_mesa"]);
            return 0;
        }

        private DataRow FilaMesa(int idMesa)
        {
            if (mesas == null) return null;
            foreach (DataRow r in mesas.Rows)
                if (Convert.ToInt32(r["id_mesa"]) == idMesa)
                    return r;
            return null;
        }

        private int Personas()
        {
            int personas;
            return int.TryParse(guna2ComboBox3.Text, out personas) ? personas : 0;
        }

        // Motivo por el que una mesa no admite la reserva (null = disponible).
        private string Motivo(DataRow mesa, DateTime fechaHora, int personas, DateTime ahora)
        {
            if (mesa == null)
                return "La mesa no existe en esta sucursal.";
            if (personas < 1 || personas > ReservaReglas.MaxPersonasPorMesa)
                return "Seleccione entre 1 y " + ReservaReglas.MaxPersonasPorMesa + " personas.";
            if (Convert.ToInt32(mesa["capacidad"]) < personas)
                return "Tiene capacidad para " + mesa["capacidad"] + " personas.";

            string estado = Convert.ToString(mesa["estado"]);
            if (estado != "libre" && estado != "ocupada" && estado != "reservada")
                return "Está en estado «" + estado + "».";

            int idMesa = Convert.ToInt32(mesa["id_mesa"]);
            foreach (KeyValuePair<int, DateTime> r in reservasConfirmadas)
            {
                if (r.Key == idMesa && ReservaReglas.EnConflicto(r.Value, fechaHora))
                    return "Ya tiene una reserva confirmada el " + r.Value.ToString("dd/MM/yyyy") +
                           " a las " + r.Value.ToString("HH:mm") + ".";
            }

            if (ReservaReglas.RequiereMesaLibreAhora(fechaHora, ahora) &&
                (estado != "libre" || Convert.ToInt32(mesa["con_pedido"]) == 1))
                return "Ahora está " + (Convert.ToInt32(mesa["con_pedido"]) == 1 ? "ocupada por un pedido en curso" : estado) +
                       " y la reserva empieza en menos de " + (ReservaReglas.DuracionReservaMinutos / 60) + " horas.";

            return null;
        }

        private string MotivoNoDisponible(int idMesa)
        {
            DateTime? fecha = ObtenerFechaHora();
            if (!fecha.HasValue) return "Elija una hora.";
            return Motivo(FilaMesa(idMesa), fecha.Value, Personas(), DateTime.Now) ?? "";
        }

        // ======================================================
        // HORARIOS Y MESAS
        // ======================================================

        // Horarios de 11:00 a 22:00 cada 30 minutos; hoy solo los futuros.
        // Solo se ofrecen horas en que alguna mesa admite la reserva.
        // La hora elegida se conserva si sigue siendo válida.
        private void CargarHorarios()
        {
            string horaAnterior = cmbHoraReserva.SelectedItem as string;
            DateTime ahora = DateTime.Now;
            int personas = Personas();

            cargando = true;
            try
            {
                cmbHoraReserva.Items.Clear();
                if (mesas != null)
                {
                    foreach (DateTime hora in ReservaReglas.Horarios(dtpFechaReserva.Value, ahora))
                    {
                        bool hayMesa = false;
                        foreach (DataRow m in mesas.Rows)
                        {
                            if (Motivo(m, hora, personas, ahora) == null) { hayMesa = true; break; }
                        }
                        if (hayMesa)
                            cmbHoraReserva.Items.Add(hora.ToString("HH:mm"));
                    }
                }

                int indice = horaAnterior == null ? -1 : cmbHoraReserva.Items.IndexOf(horaAnterior);

                // Desde el panel de la mesa: proponer la primera hora en que esa mesa está libre.
                if (indice < 0 && horaAnterior == null && idMesaElegida > 0)
                {
                    for (int i = 0; i < cmbHoraReserva.Items.Count; i++)
                    {
                        DateTime h = dtpFechaReserva.Value.Date.Add(TimeSpan.Parse((string)cmbHoraReserva.Items[i]));
                        if (Motivo(FilaMesa(idMesaElegida), h, personas, ahora) == null) { indice = i; break; }
                    }
                }

                if (indice < 0 && cmbHoraReserva.Items.Count > 0)
                {
                    indice = 0;
                    if (iniciado && horaAnterior != null)
                    {
                        MessageBox.Show(
                            "Las " + horaAnterior + " no están disponibles para el " +
                            dtpFechaReserva.Value.ToString("dd/MM/yyyy") + " con " + personas +
                            " persona(s).\nSe propone la hora más próxima: " + cmbHoraReserva.Items[0] + ".",
                            "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                cmbHoraReserva.SelectedIndex = indice;
            }
            finally
            {
                cargando = false;
            }

            btnGuardarReserva.Enabled = mesas != null && cmbHoraReserva.Items.Count > 0;

            if (iniciado && cmbHoraReserva.Items.Count == 0)
            {
                MessageBox.Show(
                    "No hay horarios disponibles para el " + dtpFechaReserva.Value.ToString("dd/MM/yyyy") +
                    " con " + personas + " persona(s).\n\nHorario de atención: " + ReservaReglas.TextoHorario() +
                    ".\nElija otra fecha o cantidad de personas.",
                    "Horario de reservas", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private DateTime? ObtenerFechaHora()
        {
            TimeSpan hora;
            string texto = cmbHoraReserva.SelectedItem as string;
            if (texto == null || !TimeSpan.TryParse(texto, out hora))
                return null;
            return dtpFechaReserva.Value.Date.Add(hora);
        }

        private int IdMesaSeleccionada()
        {
            if (cmbMesa.SelectedIndex < 0 || cmbMesa.SelectedValue == null)
                return 0;
            int id;
            return int.TryParse(cmbMesa.SelectedValue.ToString(), out id) ? id : 0;
        }

        // Mesas que admiten la reserva en la fecha y hora elegidas.
        // Si la mesa elegida deja de estar disponible se informa el motivo
        // (no se borra en silencio) y vuelve a seleccionarse cuando esté libre.
        private void CargarMesas()
        {
            DateTime? fechaHora = ObtenerFechaHora();
            int personas = Personas();
            DateTime ahora = DateTime.Now;

            DataTable disponibles = new DataTable();
            disponibles.Columns.Add("id_mesa", typeof(int));
            disponibles.Columns.Add("NombreMesa", typeof(string));

            if (mesas != null && fechaHora.HasValue)
            {
                foreach (DataRow m in mesas.Rows)
                {
                    if (Motivo(m, fechaHora.Value, personas, ahora) == null)
                        disponibles.Rows.Add(Convert.ToInt32(m["id_mesa"]),
                            "Mesa " + m["numero"] + " (" + m["capacidad"] + " pers.)");
                }
            }

            cargando = true;
            try
            {
                cmbMesa.DataSource = null;
                cmbMesa.Items.Clear();
                cmbMesa.DisplayMember = "NombreMesa";
                cmbMesa.ValueMember = "id_mesa";
                cmbMesa.DataSource = disponibles;
                cmbMesa.SelectedIndex = -1;

                if (idMesaElegida > 0)
                {
                    for (int i = 0; i < disponibles.Rows.Count; i++)
                    {
                        if (Convert.ToInt32(disponibles.Rows[i]["id_mesa"]) == idMesaElegida)
                        {
                            cmbMesa.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            finally
            {
                cargando = false;
            }

            if (iniciado && idMesaElegida > 0 && IdMesaSeleccionada() == 0 && fechaHora.HasValue)
            {
                DataRow fila = FilaMesa(idMesaElegida);
                MessageBox.Show(
                    "La Mesa " + (fila == null ? "?" : fila["numero"].ToString()) + " no está disponible el " +
                    fechaHora.Value.ToString("dd/MM/yyyy 'a las' HH:mm") + " para " + personas + " persona(s).\n\n" +
                    MotivoNoDisponible(idMesaElegida) +
                    "\n\nLa mesa se conserva como preferida: elija otro horario o seleccione otra mesa.",
                    "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ======================================================
        // GUARDAR
        // ======================================================

        private void btnGuardarReserva_Click(object sender, EventArgs e)
        {
            string cliente = txtCliente.Text.Trim();
            string telefono = txtTelefono.Text.Trim();
            string observacion = txtObservacion.Text.Trim();
            int personas = Personas();

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
            if (personas < 1 || personas > ReservaReglas.MaxPersonasPorMesa)
            {
                MessageBox.Show("Seleccione entre 1 y " + ReservaReglas.MaxPersonasPorMesa + " personas.");
                return;
            }
            int idMesa = IdMesaSeleccionada();
            if (idMesa == 0)
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
            if (!fechaReserva.HasValue || !ReservaReglas.HorarioValido(fechaReserva.Value, DateTime.Now))
            {
                MessageBox.Show("Elija un horario válido: " + ReservaReglas.TextoHorario() +
                    ", cada " + ReservaReglas.IntervaloMinutos + " minutos y posterior a la hora actual.");
                CargarHorarios();
                CargarMesas();
                return;
            }

            // Mismo teléfono con otra reserva cercana: puede ser un grupo que
            // necesita dos mesas, así que se pregunta en lugar de bloquear.
            string otraReserva = ReservaCercanaDelTelefono(telefono, fechaReserva.Value);
            if (otraReserva != null &&
                MessageBox.Show("Este teléfono ya tiene una reserva confirmada:\n" + otraReserva +
                    "\n\n¿Registrar otra mesa para el mismo cliente?", "Posible reserva duplicada",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            // ¿Quién reserva es un cliente registrado? Solo por teléfono exacto
            // y con confirmación; nunca por nombres parecidos ni creando clientes.
            int? idCliente;
            if (!ElegirClienteRegistrado(telefono, out idCliente))
                return;

            int idReserva;
            bool apartada;
            try
            {
                idReserva = Registrar(idMesa, cliente, telefono, fechaReserva.Value, personas,
                    observacion, idCliente, out apartada);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo guardar la reserva: " + ex.Message,
                    "Nueva reserva", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LeerDatos();
                CargarHorarios();
                CargarMesas();
                return;
            }

            DataRow fila = FilaMesa(idMesa);
            MessageBox.Show("Reserva N.º " + idReserva + " registrada:\n" +
                "Mesa " + (fila == null ? "" : fila["numero"].ToString()) + ", " +
                fechaReserva.Value.ToString("dd/MM/yyyy 'a las' HH:mm") + ".\n" +
                "Tolerancia de llegada: " + ReservaReglas.ToleranciaLlegadaMinutos + " minutos." +
                (apartada ? "" : "\nLa mesa se apartará automáticamente " + ReservaReglas.ApartadoMinutos +
                                 " minutos antes de la hora reservada."),
                "Reserva registrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        // Devuelve false si el usuario cancela el guardado.
        private bool ElegirClienteRegistrado(string telefono, out int? idCliente)
        {
            idCliente = null;
            List<ClienteInfo> candidatos;
            try
            {
                candidatos = ClienteDatos.BuscarPorTelefono(telefono);
            }
            catch (Exception ex)
            {
                Registro.Error("Reservas", ex);
                return true; // sin vínculo: la reserva no depende de Clientes
            }

            if (candidatos.Count == 0)
                return true;

            if (candidatos.Count == 1)
            {
                ClienteInfo c = candidatos[0];
                DialogResult r = MessageBox.Show(
                    "El teléfono " + telefono + " pertenece al cliente registrado:\n\n" +
                    c.NombreCompleto + " (" + c.TipoDocumento + " " + Enmascarar(c.Documento) + ")\n\n" +
                    "¿La reserva es de esta persona?\n\n" +
                    "Sí: vincular la reserva a su historial.\nNo: registrar la reserva sin vincular.",
                    "Cliente registrado", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                    return false;
                if (r == DialogResult.Yes)
                    idCliente = c.IdCliente;
                return true;
            }

            ClienteInfo elegido = SelectorClientes.Elegir(this, candidatos, "Cliente de la reserva",
                "Varios clientes registrados tienen el teléfono " + telefono + ". Elija quién reserva " +
                "o pulse «No es ninguno» para registrar la reserva sin vincular.", "No es ninguno");
            if (elegido != null)
                idCliente = elegido.IdCliente;
            return true;
        }

        private static string Enmascarar(string documento)
        {
            documento = (documento ?? "").Trim();
            return documento.Length <= 4 ? documento : new string('*', documento.Length - 4) + documento.Substring(documento.Length - 4);
        }

        private string ReservaCercanaDelTelefono(string telefono, DateTime fechaHora)
        {
            const string sql = @"
                SELECT TOP (1) 'Mesa ' + CAST(M.[número] AS varchar(10)) + ', ' +
                       CONVERT(varchar(10), R.fecha_hora, 103) + ' ' + CONVERT(varchar(5), R.fecha_hora, 108) +
                       ' (' + R.nombre_cliente + ')'
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE R.telefono = @Telefono AND R.estado = 'confirmada'
                  AND ABS(DATEDIFF(MINUTE, R.fecha_hora, @Fecha)) < @Duracion
                ORDER BY R.fecha_hora;";
            try
            {
                using (SqlConnection conexion = new SqlConnection(cadenaConexion))
                using (SqlCommand cmd = new SqlCommand(sql, conexion))
                {
                    cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 9).Value = telefono;
                    cmd.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = fechaHora;
                    cmd.Parameters.Add("@Duracion", SqlDbType.Int).Value = ReservaReglas.DuracionReservaMinutos;
                    conexion.Open();
                    object r = cmd.ExecuteScalar();
                    return r == null || r == DBNull.Value ? null : Convert.ToString(r);
                }
            }
            catch
            {
                return null; // el guardado vuelve a validar los choques de mesa
            }
        }

        // Guardado único de reservas: valida de nuevo en SQL (hora del
        // servidor, horario, choque por mesa y fecha) y bloquea la mesa
        // para que dos equipos no registren la misma reserva a la vez.
        internal int Registrar(int idMesa, string cliente, string telefono, DateTime fechaReserva,
            int personas, string observacion, int? idCliente, out bool apartada)
        {
            apartada = false;
            using (SqlConnection conexion = new SqlConnection(cadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction transaccion = conexion.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        DateTime ahora;
                        using (SqlCommand cmd = new SqlCommand("SELECT SYSDATETIME();", conexion, transaccion))
                            ahora = Convert.ToDateTime(cmd.ExecuteScalar());

                        if (!ReservaReglas.HorarioValido(fechaReserva, ahora))
                            throw new Exception("La hora elegida ya pasó o queda fuera del horario de atención (" +
                                ReservaReglas.TextoHorario() + ").");

                        // Bloquear la mesa (orden MESA -> RESERVA, igual que el monitor).
                        int capacidad;
                        string estadoMesa;
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT capacidad, LOWER(LTRIM(RTRIM(estado)))
                            FROM dbo.MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_mesa = @Mesa AND id_sucursal = @Sucursal;", conexion, transaccion))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (!rd.Read()) throw new Exception("La mesa no existe.");
                                capacidad = rd.GetInt32(0);
                                estadoMesa = rd.GetString(1);
                            }
                        }
                        if (personas > capacidad)
                            throw new Exception("La mesa no tiene capacidad suficiente.");

                        if (ReservaReglas.RequiereMesaLibreAhora(fechaReserva, ahora))
                        {
                            using (SqlCommand cmd = new SqlCommand(@"
                                SELECT COUNT(*) FROM dbo.PEDIDO WITH (UPDLOCK, HOLDLOCK)
                                WHERE id_mesa = @Mesa AND canal = 'salon'
                                  AND estado IN ('pendiente','en_preparacion','listo','entregado');", conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                if (estadoMesa != "libre" || Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                                    throw new Exception("La mesa ya no está libre para ese horario.");
                            }
                        }

                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT TOP (1) fecha_hora FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_mesa = @Mesa AND estado = 'confirmada'
                              AND ABS(DATEDIFF(MINUTE, fecha_hora, @Fecha)) < @Duracion;", conexion, transaccion))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            cmd.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = fechaReserva;
                            cmd.Parameters.Add("@Duracion", SqlDbType.Int).Value = ReservaReglas.DuracionReservaMinutos;
                            object otra = cmd.ExecuteScalar();
                            if (otra != null && otra != DBNull.Value)
                                throw new Exception("La mesa ya tiene una reserva confirmada a las " +
                                    Convert.ToDateTime(otra).ToString("HH:mm") + " de ese día.");
                        }

                        if (idCliente.HasValue)
                        {
                            using (SqlCommand cmd = new SqlCommand(@"
                                SELECT COUNT(*) FROM dbo.CLIENTE WITH (UPDLOCK, HOLDLOCK)
                                WHERE id_cliente = @IdCliente AND estado = 'activo';", conexion, transaccion))
                            {
                                cmd.Parameters.Add("@IdCliente", SqlDbType.Int).Value = idCliente.Value;
                                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                                    throw new Exception("El cliente registrado elegido ya no está activo.");
                            }
                        }

                        // Se registra la fecha y hora elegidas por el cliente, NO SYSDATETIME().
                        int idReserva;
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO dbo.RESERVA_MESA
                                (id_mesa, nombre_cliente, telefono, fecha_hora, personas, observacion, estado, id_cliente)
                            VALUES
                                (@Mesa, @Cliente, @Telefono, @Fecha, @Personas, @Observacion, 'confirmada', @IdCliente);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);", conexion, transaccion))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            cmd.Parameters.Add("@Cliente", SqlDbType.NVarChar, 100).Value = cliente;
                            cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 9).Value = telefono;
                            cmd.Parameters.Add("@Personas", SqlDbType.Int).Value = personas;
                            cmd.Parameters.Add("@Fecha", SqlDbType.DateTime2).Value = fechaReserva;
                            cmd.Parameters.Add("@Observacion", SqlDbType.NVarChar, 250).Value =
                                string.IsNullOrEmpty(observacion) ? (object)DBNull.Value : observacion;
                            cmd.Parameters.Add("@IdCliente", SqlDbType.Int).Value =
                                idCliente.HasValue ? (object)idCliente.Value : DBNull.Value;
                            idReserva = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // Reserva cercana: la mesa queda apartada desde ahora.
                        if (ReservaReglas.SeApartaAlRegistrar(fechaReserva, ahora))
                        {
                            using (SqlCommand cmd = new SqlCommand(@"
                                UPDATE dbo.MESA SET estado = 'reservada'
                                WHERE id_mesa = @Mesa AND id_sucursal = @Sucursal
                                  AND LOWER(LTRIM(RTRIM(estado))) = 'libre';", conexion, transaccion))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                                if (cmd.ExecuteNonQuery() != 1)
                                    throw new Exception("No se pudo apartar la mesa.");
                            }
                            apartada = true;
                        }

                        transaccion.Commit();
                        Registro.Info("Reservas", "Reserva " + idReserva + " registrada (mesa id " + idMesa +
                            ", " + fechaReserva.ToString("dd/MM/yyyy HH:mm") + ").");
                        return idReserva;
                    }
                    catch
                    {
                        try { transaccion.Rollback(); } catch { }
                        throw;
                    }
                }
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
