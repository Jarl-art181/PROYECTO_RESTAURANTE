using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
   
    internal static class ControlReservasAutomaticas
    {
        private const int IdSucursal = 1;
        private const string CadenaConexion =
            @"Server=LAPTOP-GH63852S\SQLEXPRESS;Database=AL_ESTILO_PIURA;Integrated Security=True;";

        private static System.Threading.Timer temporizador;
        private static Form ventanaPrincipal;
        private static NotifyIcon notificador;
        private static int ejecutando;
        private static int errorNotificado;
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
        }

        public static void Iniciar(Form principal)
        {
            if (temporizador != null)
                return;

            ventanaPrincipal = principal;
            notificador = new NotifyIcon();
            notificador.Icon = SystemIcons.Information;
            notificador.Text = "Reservas - Al Estilo Piurano";
            notificador.Visible = true;

            // Primera comprobación a los 2 segundos y luego cada 10 segundos.
            temporizador = new System.Threading.Timer(RevisarReservas, null, 2000, 10000);
        }

        public static void Detener()
        {
            if (temporizador != null)
            {
                temporizador.Dispose();
                temporizador = null;
            }
            if (notificador != null)
            {
                notificador.Visible = false;
                notificador.Dispose();
                notificador = null;
            }
        }

        private static void RevisarReservas(object estado)
        {
            if (Interlocked.Exchange(ref ejecutando, 1) == 1)
                return;

            try
            {
                // Siempre liberar primero las reservas que YA hayan vencido.
                List<ReservaAviso> vencidas = LiberarReservasVencidas();
                List<ReservaAviso> avisos = MarcarYObtenerAvisos15();
                Interlocked.Exchange(ref errorNotificado, 0);

                if (vencidas.Count > 0 || avisos.Count > 0)
                {
                    EjecutarEnPantalla(delegate
                    {
                        if (vencidas.Count > 0)
                        {
                            NotificarMesasActualizadas();

                            foreach (ReservaAviso r in vencidas)
                            {
                                if (notificador != null)
                                {
                                    notificador.ShowBalloonTip(
                                        6000,
                                        "Mesa " + r.NumeroMesa + " liberada",
                                        "La reserva de " + r.Cliente + " venció después de 30 minutos. " +
                                        "La mesa está disponible.",
                                        ToolTipIcon.Info);
                                }
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
                // Evita mostrar el mismo error una vez cada 10 segundos.
                if (Interlocked.Exchange(ref errorNotificado, 1) == 0)
                {
                    string mensaje = ex.Message;
                    EjecutarEnPantalla(delegate
                    {
                        MessageBox.Show(
                            "No se pudieron comprobar las reservas automáticas.\n" + mensaje,
                            "Control de reservas",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    });
                }
            }
            finally
            {
                Interlocked.Exchange(ref ejecutando, 0);
            }
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

                    DialogResult respuesta = MessageBox.Show(
                        "¿LLEGÓ EL COMENSAL?\n\n" +
                        "Mesa: " + aviso.NumeroMesa + "\n" +
                        "Cliente: " + aviso.Cliente + "\n" +
                        "Hora reservada: " + aviso.FechaHora.ToString("HH:mm") + "\n\n" +
                        "Han pasado 15 minutos desde la hora reservada.\n" +
                        "Sí: confirma la llegada y permite abrir el pedido.\n" +
                        "No: conserva la reserva hasta los 30 minutos.",
                        "Confirmación de llegada",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);

                    if (respuesta == DialogResult.Yes)
                    {
                        try
                        {
                            // Confirmar llegada en SQL Server.
                            ConfirmarLlegada(aviso.IdReserva);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "No se pudo confirmar la llegada:\n" +
                                ex.Message);

                            continue;
                        }

                        try
                        {
                            // Abrir Pedidos con SALÓN y mesa seleccionada.
                            AbrirPedidosEnSalon(aviso.NumeroMesa);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                "La llegada quedó confirmada, pero " +
                                "no se pudo abrir Pedidos:\n" +
                                ex.Message);
                        }
                    }
                    // Si responde NO, no se cambia ningún estado.
                    // El monitor continuará vigilando la reserva hasta el minuto 30.
                }
            }
            finally
            {
                mostrandoPregunta = false;
            }
        }

        // Marca el aviso EN LA MISMA TABLA, de forma atómica.
        // Así no se repite al cambiar de formulario o reiniciar la aplicación.
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
                  AND LOWER(LTRIM(RTRIM(M.estado))) = 'reservada'
                  AND DATEADD(MINUTE, 15, R.fecha_hora) <= SYSDATETIME()
                  AND DATEADD(MINUTE, 30, R.fecha_hora) > SYSDATETIME();";

            List<ReservaAviso> avisos = new List<ReservaAviso>();
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, conexion))
            {
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
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

        private static List<ReservaAviso> LiberarReservasVencidas()
        {
            const string consulta = @"
                SELECT R.id_reserva, R.id_mesa
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE M.id_sucursal = @Sucursal
                  AND R.estado = 'confirmada'
                  AND LOWER(LTRIM(RTRIM(M.estado))) = 'reservada'
                  AND DATEADD(MINUTE, 30, R.fecha_hora) <= SYSDATETIME();";

            List<int[]> candidatos = new List<int[]>();
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(consulta, conexion))
            {
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                conexion.Open();
                using (SqlDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                        candidatos.Add(new int[] { Convert.ToInt32(lector[0]), Convert.ToInt32(lector[1]) });
                }
            }

            List<ReservaAviso> liberadas = new List<ReservaAviso>();
            foreach (int[] datos in candidatos)
            {
                ReservaAviso r = IntentarLiberarVencida(datos[0], datos[1]);
                if (r != null)
                    liberadas.Add(r);
            }
            return liberadas;
        }

        private static ReservaAviso IntentarLiberarVencida(int idReserva, int idMesa)
        {
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        // El orden MESA -> RESERVA coincide con guardar/cancelar/llegada.
                        int numero;
                        string estadoMesa;
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT [número], estado FROM dbo.MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_mesa = @Mesa AND id_sucursal = @Sucursal;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (!rd.Read()) return null;
                                numero = Convert.ToInt32(rd["número"]);
                                estadoMesa = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                            }
                        }

                        string cliente;
                        DateTime fechaHora;
                        string estadoReserva;
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT nombre_cliente, fecha_hora, estado
                            FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_reserva = @Reserva AND id_mesa = @Mesa;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (!rd.Read()) return null;
                                cliente = Convert.ToString(rd["nombre_cliente"]);
                                fechaHora = Convert.ToDateTime(rd["fecha_hora"]);
                                estadoReserva = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                            }
                        }

                        DateTime horaServidor;
                        using (SqlCommand cmd = new SqlCommand("SELECT SYSDATETIME();", conexion, tx))
                            horaServidor = Convert.ToDateTime(cmd.ExecuteScalar());

                        if (estadoMesa != "reservada" || estadoReserva != "confirmada" ||
                            horaServidor < fechaHora.AddMinutes(30))
                        {
                            tx.Rollback();
                            return null;
                        }

                        VerificarSinPedidoActivo(conexion, tx, idMesa);

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE dbo.RESERVA_MESA SET estado = 'cancelada'
                            WHERE id_reserva = @Reserva AND estado = 'confirmada';", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            if (cmd.ExecuteNonQuery() != 1)
                                throw new Exception("No se pudo vencer la reserva " + idReserva + ".");
                        }

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE dbo.MESA SET estado = 'libre'
                            WHERE id_mesa = @Mesa AND estado = 'reservada';", conexion, tx))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            if (cmd.ExecuteNonQuery() != 1)
                                throw new Exception("No se pudo liberar la mesa " + numero + ".");
                        }

                        tx.Commit();
                        return new ReservaAviso
                        {
                            IdReserva = idReserva,
                            IdMesa = idMesa,
                            NumeroMesa = numero,
                            Cliente = cliente,
                            FechaHora = fechaHora
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

        private static void VerificarSinPedidoActivo(SqlConnection conexion, SqlTransaction tx, int idMesa)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM dbo.PEDIDO WITH (UPDLOCK, HOLDLOCK)
                WHERE id_mesa = @Mesa AND canal = 'salon'
                  AND estado IN ('pendiente','en_preparacion','listo','entregado');", conexion, tx))
            {
                cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                    throw new Exception("La mesa tiene un pedido activo. No se puede liberar.");
            }
        }

        // Puede llamarse desde la pregunta de los 15 min o desde Abrir pedido.
        public static void ConfirmarLlegada(int idReserva)
        {
            int idMesa;
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT id_mesa FROM dbo.RESERVA_MESA WHERE id_reserva = @Reserva;", conexion))
            {
                cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                conexion.Open();
                object valor = cmd.ExecuteScalar();
                if (valor == null || valor == DBNull.Value)
                    throw new Exception("La reserva ya no existe.");
                idMesa = Convert.ToInt32(valor);
            }

            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        string estadoMesa;
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT estado FROM dbo.MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_mesa = @Mesa AND id_sucursal = @Sucursal;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                            object valor = cmd.ExecuteScalar();
                            if (valor == null) throw new Exception("No existe la mesa de la reserva.");
                            estadoMesa = Convert.ToString(valor).Trim().ToLowerInvariant();
                        }

                        DateTime fechaHora;
                        string estadoReserva;
                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT fecha_hora, estado FROM dbo.RESERVA_MESA WITH (UPDLOCK, HOLDLOCK)
                            WHERE id_reserva = @Reserva AND id_mesa = @Mesa;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            using (SqlDataReader rd = cmd.ExecuteReader())
                            {
                                if (!rd.Read()) throw new Exception("La reserva ya no existe.");
                                fechaHora = Convert.ToDateTime(rd["fecha_hora"]);
                                estadoReserva = Convert.ToString(rd["estado"]).Trim().ToLowerInvariant();
                            }
                        }

                        DateTime ahoraServidor;
                        using (SqlCommand cmd = new SqlCommand("SELECT SYSDATETIME();", conexion, tx))
                            ahoraServidor = Convert.ToDateTime(cmd.ExecuteScalar());

                        if (estadoReserva != "confirmada" || estadoMesa != "reservada")
                            throw new Exception("La reserva ya fue atendida o cancelada.");
                        if (ahoraServidor >= fechaHora.AddMinutes(30))
                            throw new Exception("La reserva superó los 30 minutos de tolerancia.");

                        VerificarSinPedidoActivo(conexion, tx, idMesa);

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE dbo.RESERVA_MESA SET estado = 'finalizada'
                            WHERE id_reserva = @Reserva AND estado = 'confirmada';", conexion, tx))
                        {
                            cmd.Parameters.Add("@Reserva", SqlDbType.Int).Value = idReserva;
                            if (cmd.ExecuteNonQuery() != 1) throw new Exception("No se pudo finalizar la reserva.");
                        }
                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE dbo.MESA SET estado = 'libre'
                            WHERE id_mesa = @Mesa AND estado = 'reservada';", conexion, tx))
                        {
                            cmd.Parameters.Add("@Mesa", SqlDbType.Int).Value = idMesa;
                            if (cmd.ExecuteNonQuery() != 1) throw new Exception("No se pudo preparar la mesa para el pedido.");
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
            int idReserva;
            using (SqlConnection conexion = new SqlConnection(CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (2) R.id_reserva
                FROM dbo.RESERVA_MESA R
                INNER JOIN dbo.MESA M ON M.id_mesa = R.id_mesa
                WHERE M.id_sucursal = @Sucursal AND M.[número] = @Numero
                  AND M.estado = 'reservada' AND R.estado = 'confirmada'
                ORDER BY R.fecha_hora DESC, R.id_reserva DESC;", conexion))
            {
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = IdSucursal;
                cmd.Parameters.Add("@Numero", SqlDbType.Int).Value = numeroMesa;
                conexion.Open();
                using (SqlDataReader rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) throw new Exception("Esta mesa ya no tiene reserva confirmada.");
                    idReserva = Convert.ToInt32(rd[0]);
                    if (rd.Read()) throw new Exception("Hay más de una reserva confirmada para la mesa.");
                }
            }
            ConfirmarLlegada(idReserva);
        }

       

    }
}
