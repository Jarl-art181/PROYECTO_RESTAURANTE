using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // MONITOR DE RESERVAS
    // ----------------------------------------------------------
    // * Se inicia UNA sola vez (Iniciar) y nunca ejecuta dos
    //   comprobaciones a la vez (Interlocked).
    // * Cada 10 s: vence reservas pasados 30 min y pregunta
    //   "¿Llegó el comensal?" a los 15 min (solo con sesión).
    // * Errores recuperables: se registran en el archivo de
    //   diagnóstico, se avisa con una notificación NO bloqueante
    //   y se reintenta con espera creciente (10 → 20 → 40 → 60 s).
    //   Si el fallo persiste se sigue avisando periódicamente.
    // * RESERVA_MESA y MESA se actualizan en la misma transacción
    //   e identificando la reserva por su id_reserva.
    // ==========================================================
    internal static class ControlReservasAutomaticas
    {
        private const int IdSucursal = 1;
        private const int IntervaloNormal = 10000;
        private const int IntervaloMaximo = 60000;
        private const int TiempoComando = 15;          // segundos (no se amplía)
        private const int ToleranciaMinutos = 30;
        private const int AvisoMinutos = 15;

        // Cadena centralizada en App.config (ver BaseDatos).
        private static string CadenaConexion { get { return BaseDatos.CadenaConexion; } }

        private static System.Threading.Timer temporizador;
        private static Form ventanaPrincipal;
        private static NotifyIcon notificador;
        private static int ejecutando;
        private static int fallosSeguidos;
        private static readonly Queue<ReservaAviso> colaAvisos = new Queue<ReservaAviso>();
        private static bool mostrandoPregunta;

        public static event EventHandler MesasActualizadas;

        private sealed class ReservaAviso
        {
            public int IdReserva;
            public int IdMesa;
            public int NumeroMesa;
            public string Cliente;
            public DateTime FechaHora;
            public bool MesaLiberada;
        }

        // Datos mínimos de una reserva confirmada (para la interfaz).
        internal sealed class ReservaVigente
        {
            public int IdReserva;
            public int NumeroMesa;
            public string Cliente;
            public DateTime FechaHora;
        }

        // ======================================================
        // CICLO DE VIDA
        // ======================================================
        public static void Iniciar(Form principal)
        {
            // Siempre se actualiza la ventana usada para mostrar avisos
            // (por ejemplo, tras cerrar sesión y volver a entrar).
            ventanaPrincipal = principal;

            if (temporizador != null)
                return;

            notificador = new NotifyIcon();
            notificador.Icon = SystemIcons.Information;
            notificador.Text = "Reservas - Al Estilo Piurano";
            notificador.Visible = true;

            fallosSeguidos = 0;
            temporizador = new System.Threading.Timer(RevisarReservas, null, 2000, Timeout.Infinite);
            Registro.Info("Reservas", "Monitor iniciado.");
        }

        public static void Detener()
        {
            System.Threading.Timer t = temporizador;
            temporizador = null;
            if (t != null)
                t.Dispose();

            if (notificador != null)
            {
                notificador.Visible = false;
                notificador.Dispose();
                notificador = null;
            }
        }

        // Programa la siguiente comprobación (un solo disparo cada vez:
        // nunca se acumulan ejecuciones aunque SQL tarde).
        private static void Programar(int milisegundos)
        {
            System.Threading.Timer t = temporizador;
            if (t != null)
            {
                try { t.Change(milisegundos, Timeout.Infinite); }
                catch (ObjectDisposedException) { }
            }
        }

        private static void RevisarReservas(object estado)
        {
            if (Interlocked.Exchange(ref ejecutando, 1) == 1)
                return;

            int siguiente = IntervaloNormal;

            try
            {
                List<ReservaAviso> vencidas = LiberarReservasVencidas();

                // Las preguntas solo tienen sentido con alguien en sesión.
                List<ReservaAviso> avisos = SesionActual.Iniciada && Permisos.AtiendeReservas(SesionActual.Cargo)
                    ? MarcarYObtenerAvisos15()
                    : new List<ReservaAviso>();

                if (fallosSeguidos > 0)
                {
                    Registro.Info("Reservas", "Comprobación restablecida tras " + fallosSeguidos + " intento(s) fallido(s).");
                    Notificar("Reservas automáticas", "La comprobación de reservas volvió a funcionar.", ToolTipIcon.Info);
                }
                fallosSeguidos = 0;

                if (vencidas.Count > 0 || avisos.Count > 0)
                {
                    EjecutarEnPantalla(delegate
                    {
                        if (vencidas.Count > 0)
                        {
                            NotificarMesasActualizadas();

                            foreach (ReservaAviso r in vencidas)
                            {
                                Notificar(
                                    "Reserva N.º " + r.IdReserva + " vencida",
                                    "La reserva de " + r.Cliente + " (Mesa " + r.NumeroMesa + ") superó los " +
                                    ToleranciaMinutos + " minutos de tolerancia." +
                                    (r.MesaLiberada ? " La mesa está disponible." : ""),
                                    ToolTipIcon.Info);
                            }
                        }

                        foreach (ReservaAviso aviso in avisos)
                            colaAvisos.Enqueue(aviso);

                        ProcesarColaDePreguntas();
                    });
                }
            }
            catch (Exception ex)
            {
                fallosSeguidos++;
                Registro.Error("Reservas", ex);

                siguiente = Math.Min(IntervaloMaximo, IntervaloNormal * (1 << Math.Min(fallosSeguidos, 3)));

                // Aviso no bloqueante: al primer fallo y luego cada ~6 intentos.
                if (fallosSeguidos == 1 || fallosSeguidos % 6 == 0)
                {
                    Notificar(
                        "Reservas automáticas: sin respuesta de SQL Server",
                        (Registro.EsTransitorio(ex)
                            ? "Error temporal (" + fallosSeguidos + " intento(s)). Se reintentará en " + (siguiente / 1000) + " s."
                            : "Error persistente: " + ex.Message) +
                        " Detalle en " + Registro.Ruta,
                        ToolTipIcon.Warning);
                }
            }
            finally
            {
                Interlocked.Exchange(ref ejecutando, 0);
                Programar(siguiente);
            }
        }

        private static void Notificar(string titulo, string texto, ToolTipIcon icono)
        {
            EjecutarEnPantalla(delegate
            {
                if (notificador != null)
                    notificador.ShowBalloonTip(6000, titulo, texto, icono);
            });
        }

        private static void EjecutarEnPantalla(Action accion)
        {
            Form p = ventanaPrincipal;
            if (p == null || p.IsDisposed || !p.IsHandleCreated)
                return;

            try
            {
                p.BeginInvoke(accion);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public static void NotificarMesasActualizadas()
        {
            EventHandler evento = MesasActualizadas;
            if (evento != null)
                evento(null, EventArgs.Empty);
        }

        private static void ProcesarColaDePreguntas()
        {
            if (mostrandoPregunta)
                return;

            mostrandoPregunta = true;
            try
            {
                while (colaAvisos.Count > 0)
                {
                    ReservaAviso aviso = colaAvisos.Dequeue();

                    // Si se cerró la sesión mientras esperaba, no preguntar.
                    if (!SesionActual.Iniciada || !Permisos.AtiendeReservas(SesionActual.Cargo))
                        continue;

                    DialogResult respuesta = MessageBox.Show(
                        "¿LLEGÓ EL COMENSAL?\n\n" +
                        "Reserva N.º " + aviso.IdReserva + "\n" +
                        "Mesa: " + aviso.NumeroMesa + "\n" +
                        "Cliente: " + aviso.Cliente + "\n" +
                        "Hora reservada: " + aviso.FechaHora.ToString("HH:mm") + "\n\n" +
                        "Han pasado " + AvisoMinutos + " minutos desde la hora reservada.\n" +
                        "Sí: confirma la llegada y permite abrir el pedido.\n" +
                        "No: conserva la reserva hasta los " + ToleranciaMinutos + " minutos.",
                        "Confirmación de llegada",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);

                    if (respuesta == DialogResult.Yes)
                    {
                        try
                        {
                            ConfirmarLlegada(aviso.IdReserva);
                        }
                        catch (Exception ex)
                        {
                            Registro.Error("Reservas", ex);
                            MessageBox.Show(
                                "No se pudo confirmar la llegada:\n" + ex.Message);
                            continue;
                        }

                        try
                        {
                            AbrirPedidosEnSalon(aviso.NumeroMesa);
                        }
                        catch (Exception ex)
                        {
                            Registro.Error("Reservas", ex);
                            MessageBox.Show(
                                "La llegada quedó confirmada, pero " +
                                "no se pudo abrir Pedidos:\n" + ex.Message);
                        }
                    }
                }
            }
            finally
            {
                mostrandoPregunta = false;
            }
        }

        // ======================================================
        // CONSULTAS DEL MONITOR
        // ======================================================

        // Marca el aviso en la misma tabla, de forma atómica, para que
        // no se repita al cambiar de formulario o reiniciar la aplicación.
        // Incluye reservas cuya mesa figura libre (datos anteriores a la
        // corrección), pero nunca mesas ocupadas por otro pedido.
        private static List<ReservaAviso> MarcarYObtenerAvisos15()
        {
            const string sql = @"
                UPDATE R
                SET aviso_15 = 1
                OUTPUT INSERTED.id_reserva, INSERTED.id_mesa,
                       M.[número], INSERTED.nombre_cliente, INSERTED.fecha_hora
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE M.id_sucursal = @Sucursal
                  AND R.estado = 'confirmada'
                  AND R.aviso_15 = 0
                  AND LOWER(LTRIM(RTRIM(M.estado))) IN ('reservada', 'libre')
                  AND DATEADD(MINUTE, @Aviso, R.fecha_hora) <= SYSDATETIME()
                  AND DATEADD(MINUTE, @Tolerancia, R.fecha_hora) > SYSDATETIME();";

            List<ReservaAviso> avisos = new List<ReservaAviso>();
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, conexion))
            {
                cmd.CommandTimeout = TiempoComando;
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                cmd.Parameters.Add("@Aviso", SqlDbType.Int).Value = AvisoMinutos;
                cmd.Parameters.Add("@Tolerancia", SqlDbType.Int).Value = ToleranciaMinutos;
                conexion.Open();
                using (SqlDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        avisos.Add(new ReservaAviso
                        {
                            IdReserva = Convert.ToInt32(lector[0]),
                            IdMesa = Convert.ToInt32(lector[1]),
                            NumeroMesa = Convert.ToInt32(lector[2]),
                            Cliente = Convert.ToString(lector[3]),
                            FechaHora = Convert.ToDateTime(lector[4])
                        });
                    }
                }
            }
            return avisos;
        }

        // Reservas confirmadas que superaron la tolerancia, sin importar
        // el estado de la mesa (antes solo se buscaban mesas 'reservada',
        // y una reserva con la mesa liberada quedaba confirmada para siempre).
        private static List<ReservaAviso> LiberarReservasVencidas()
        {
            const string consulta = @"
                SELECT R.id_reserva
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE M.id_sucursal = @Sucursal
                  AND R.estado = 'confirmada'
                  AND DATEADD(MINUTE, @Tolerancia, R.fecha_hora) <= SYSDATETIME();";

            List<int> candidatas = new List<int>();
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(consulta, conexion))
            {
                cmd.CommandTimeout = TiempoComando;
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                cmd.Parameters.Add("@Tolerancia", SqlDbType.Int).Value = ToleranciaMinutos;
                conexion.Open();
                using (SqlDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                        candidatas.Add(Convert.ToInt32(lector[0]));
                }
            }

            List<ReservaAviso> vencidas = new List<ReservaAviso>();
            foreach (int idReserva in candidatas)
            {
                ReservaAviso r = CerrarReserva(idReserva, "cancelada", true);
                if (r != null)
                {
                    Registro.Info("Reservas", "Reserva " + idReserva + " vencida (mesa " + r.NumeroMesa +
                        (r.MesaLiberada ? ", liberada)." : ", sin cambios en la mesa)."));
                    vencidas.Add(r);
                }
            }
            return vencidas;
        }

        // ======================================================
        // OPERACIÓN ÚNICA SOBRE UNA RESERVA (por id_reserva)
        // ------------------------------------------------------
        // Cambia el estado de UNA reserva confirmada y, en la misma
        // transacción, libera la mesa SOLO si estaba 'reservada', no
        // tiene pedido activo y no le queda otra reserva confirmada.
        // Nunca toca otras reservas de la misma mesa.
        // ======================================================
        private static ReservaAviso CerrarReserva(int idReserva, string nuevoEstado, bool soloSiVencida)
        {
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int idMesa = 0, numero = 0;
                        string estadoMesa = null, cliente = null, estadoReserva = null;
                        DateTime fechaHora = DateTime.MinValue, ahora = DateTime.MinValue;
                        bool existe;

                        // Orden de bloqueo MESA -> RESERVA (igual que reservar y confirmar llegada).
                        using (SqlCommand cmd = Comando(@"
                            SELECT M.id_mesa, M.[número], M.estado
                            FROM dbo.MESA M WITH (UPDLOCK, HOLDLOCK)
                            WHERE M.id_mesa = (SELECT id_mesa FROM dbo.RESERVA_MESA WHERE id_reserva = @Reserva)
                              AND M.id_sucursal = @Sucursal;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                existe = rd.Read();
                                if (existe)
                                {
                                    idMesa = Convert.ToInt32(rd["id_mesa"]);
                                    numero = Convert.ToInt32(rd["número"]);
                                    estadoMesa = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                                }
                            }
                        }
                        if (!existe) { tx.Rollback(); return null; }

                        using (SqlCommand cmd = Comando(@"
                            SELECT nombre_cliente, fecha_hora, estado, SYSDATETIME() AS ahora
                            FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_reserva = @Reserva;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                existe = rd.Read();
                                if (existe)
                                {
                                    cliente = Convert.ToString(rd["nombre_cliente"]);
                                    fechaHora = Convert.ToDateTime(rd["fecha_hora"]);
                                    estadoReserva = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                                    ahora = Convert.ToDateTime(rd["ahora"]);
                                }
                            }
                        }
                        if (!existe) { tx.Rollback(); return null; }

                        if (estadoReserva != "confirmada" ||
                            (soloSiVencida && ahora < fechaHora.AddMinutes(ToleranciaMinutos)))
                        {
                            tx.Rollback();
                            return null;
                        }

                        using (SqlCommand cmd = Comando(@"
                            UPDATE dbo.RESERVA_MESA SET estado = @Estado
                            WHERE id_reserva = @Reserva AND estado = 'confirmada';", conexion, tx))
                        {
                            cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = nuevoEstado;
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            if (cmd.ExecuteNonQuery() != 1)
                                throw new Exception("No se pudo actualizar la reserva " + idReserva + ".");
                        }

                        bool liberada = false;
                        if (estadoMesa == "reservada" &&
                            !TienePedidoActivo(conexion, tx, idMesa) &&
                            !TieneOtraReservaConfirmada(conexion, tx, idMesa, idReserva))
                        {
                            using (SqlCommand cmd = Comando(@"
                                UPDATE dbo.MESA SET estado = 'libre'
                                WHERE id_mesa = @Mesa AND estado = 'reservada';", conexion, tx))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                liberada = cmd.ExecuteNonQuery() == 1;
                            }
                        }

                        tx.Commit();
                        return new ReservaAviso
                        {
                            IdReserva = idReserva,
                            IdMesa = idMesa,
                            NumeroMesa = numero,
                            Cliente = cliente,
                            FechaHora = fechaHora,
                            MesaLiberada = liberada
                        };
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        private static SqlCommand Comando(string sql, SqlConnection conexion, SqlTransaction tx)
        {
            SqlCommand cmd = new SqlCommand(sql, conexion, tx);
            cmd.CommandTimeout = TiempoComando;
            return cmd;
        }

        private static bool TienePedidoActivo(SqlConnection conexion, SqlTransaction tx, int idMesa)
        {
            using (SqlCommand cmd = Comando(@"
                SELECT COUNT(*) FROM dbo.PEDIDO WITH (UPDLOCK, HOLDLOCK)
                WHERE id_mesa = @Mesa AND canal = 'salon'
                  AND estado IN ('pendiente','en_preparacion','listo','entregado');", conexion, tx))
            {
                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static bool TieneOtraReservaConfirmada(SqlConnection conexion, SqlTransaction tx, int idMesa, int idReserva)
        {
            using (SqlCommand cmd = Comando(@"
                SELECT COUNT(*) FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                WHERE id_mesa = @Mesa AND id_reserva <> @Reserva AND estado = 'confirmada';", conexion, tx))
            {
                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        // ======================================================
        // OPERACIONES PÚBLICAS (Gestión de Mesas, avisos)
        // ======================================================

        // Reserva confirmada de una mesa (la más próxima), o null.
        internal static ReservaVigente ObtenerReservaConfirmada(int numeroMesa)
        {
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) R.id_reserva, M.[número], R.nombre_cliente, R.fecha_hora
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE M.id_sucursal = @Sucursal AND M.[número] = @Numero
                  AND R.estado = 'confirmada'
                ORDER BY R.fecha_hora ASC, R.id_reserva ASC;", conexion))
            {
                cmd.CommandTimeout = TiempoComando;
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                cmd.Parameters.Add("@Numero", SqlDbType.Int).Value = numeroMesa;
                conexion.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                        return null;

                    return new ReservaVigente
                    {
                        IdReserva = Convert.ToInt32(rd[0]),
                        NumeroMesa = Convert.ToInt32(rd[1]),
                        Cliente = Convert.ToString(rd[2]),
                        FechaHora = Convert.ToDateTime(rd[3])
                    };
                }
            }
        }

        // Cancelación EXPLÍCITA de una reserva (queda en el historial).
        // Devuelve true si además se liberó la mesa.
        internal static bool CancelarReserva(int idReserva)
        {
            ReservaAviso r = CerrarReserva(idReserva, "cancelada", false);
            if (r == null)
                throw new Exception("La reserva N.º " + idReserva + " ya no está confirmada.");

            Registro.Info("Reservas", "Reserva " + idReserva + " cancelada desde Gestión de Mesas (mesa " +
                r.NumeroMesa + (r.MesaLiberada ? ", liberada)." : ", sin cambios en la mesa)."));
            NotificarMesasActualizadas();
            return r.MesaLiberada;
        }

        // Puede llamarse desde la pregunta de los 15 min o desde Abrir pedido.
        public static void ConfirmarLlegada(int idReserva)
        {
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        int idMesa;
                        string estadoMesa;
                        using (SqlCommand cmd = Comando(@"
                            SELECT M.id_mesa, M.estado FROM dbo.MESA M WITH (UPDLOCK, HOLDLOCK)
                            WHERE M.id_mesa = (SELECT id_mesa FROM dbo.RESERVA_MESA WHERE id_reserva = @Reserva)
                              AND M.id_sucursal = @Sucursal;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (!rd.Read()) throw new Exception("La reserva ya no existe.");
                                idMesa = Convert.ToInt32(rd["id_mesa"]);
                                estadoMesa = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                            }
                        }

                        DateTime fechaHora, ahoraServidor;
                        string estadoReserva;
                        using (SqlCommand cmd = Comando(@"
                            SELECT fecha_hora, estado, SYSDATETIME() AS ahora
                            FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_reserva = @Reserva AND id_mesa = @Mesa;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (!rd.Read()) throw new Exception("La reserva ya no existe.");
                                fechaHora = Convert.ToDateTime(rd["fecha_hora"]);
                                estadoReserva = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                                ahoraServidor = Convert.ToDateTime(rd["ahora"]);
                            }
                        }

                        if (estadoReserva != "confirmada")
                            throw new Exception("La reserva ya fue atendida o cancelada.");
                        if (estadoMesa != "reservada" && estadoMesa != "libre")
                            throw new Exception("La mesa está " + estadoMesa + ": no se puede confirmar la llegada.");
                        if (ahoraServidor >= fechaHora.AddMinutes(ToleranciaMinutos))
                            throw new Exception("La reserva superó los " + ToleranciaMinutos + " minutos de tolerancia.");
                        if (TienePedidoActivo(conexion, tx, idMesa))
                            throw new Exception("La mesa tiene un pedido activo.");

                        using (SqlCommand cmd = Comando(@"
                            UPDATE dbo.RESERVA_MESA SET estado = 'finalizada'
                            WHERE id_reserva = @Reserva AND estado = 'confirmada';", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            if (cmd.ExecuteNonQuery() != 1) throw new Exception("No se pudo finalizar la reserva.");
                        }

                        // La mesa queda libre para abrir el pedido del comensal.
                        if (estadoMesa == "reservada")
                        {
                            using (SqlCommand cmd = Comando(@"
                                UPDATE dbo.MESA SET estado = 'libre'
                                WHERE id_mesa = @Mesa AND estado = 'reservada';", conexion, tx))
                            {
                                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                                if (cmd.ExecuteNonQuery() != 1) throw new Exception("No se pudo preparar la mesa para el pedido.");
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
            Registro.Info("Reservas", "Llegada confirmada de la reserva " + idReserva + ".");
            NotificarMesasActualizadas();
        }

        public static void AbrirPedidosEnSalon(int numeroMesa)
        {
            if (numeroMesa < 1 || numeroMesa > 10)
            {
                throw new Exception(
                    "El número de mesa no es válido.");
            }

            // Obtener la ventana actual (puede ser null si el aviso
            // apareció sin ninguna ventana activa; el Navegador
            // busca entonces la ventana visible).
            Form ventanaActual = Form.ActiveForm;

            // Abrir Pedidos enviando el número de mesa, sin dejar
            // instancias anteriores ocultas en memoria.
            Navegador.AbrirPedidosConMesa(ventanaActual, numeroMesa);
        }

        public static void ConfirmarLlegadaPorNumeroMesa(int numeroMesa)
        {
            ReservaVigente reserva = ObtenerReservaConfirmada(numeroMesa);
            if (reserva == null)
                throw new Exception("Esta mesa ya no tiene reserva confirmada.");

            ConfirmarLlegada(reserva.IdReserva);
        }
    }
}
