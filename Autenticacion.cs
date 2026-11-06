using System;
using System.Data;
using System.Data.SqlClient;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // AUTENTICACIÓN Y PERMISOS
    // ----------------------------------------------------------
    // * Valida usuario y contraseña contra EMPLEADO.
    // * Exige empleado ACTIVO, cuenta HABILITADA y un cargo.
    // * 5 intentos fallidos => bloqueo temporal de 15 minutos.
    // * Migra los hashes SHA-256 antiguos a PBKDF2 al entrar.
    // * Mensajes genéricos: no revela si el usuario existe.
    // ==========================================================
    internal sealed class ResultadoAutenticacion
    {
        public bool Correcto;
        public string Mensaje;
        public int IdEmpleado;
        public string Usuario;
        public string NombreCompleto;
        public string Cargo;
        public int IdSucursal;
        public bool DebeCambiarContrasena;
    }

    internal static class Permisos
    {
        // Perfiles autorizados a registrar cobros (decisión aprobada).
        // Coincide con el trigger TR_PAGO_RN_E (CAJERO o ADMINISTRADOR).
        public static bool PuedeCobrar(string cargo)
        {
            return cargo == Cargos.Administrador || cargo == Cargos.Cajero;
        }

        public static bool PuedeAdministrarEmpleados(string cargo)
        {
            return cargo == Cargos.Administrador;
        }

        // Perfiles que atienden la llegada de comensales con reserva.
        public static bool AtiendeReservas(string cargo)
        {
            return cargo == Cargos.Administrador || cargo == Cargos.Cajero || cargo == Cargos.Mesero;
        }
    }

    internal static class Autenticacion
    {
        public const int IntentosMaximos = 5;
        public const int MinutosBloqueo = 15;

        private const string SqlCargo = @"
            CASE
                WHEN EXISTS (SELECT 1 FROM dbo.ADMINISTRADOR A WHERE A.id_empleado = E.id_empleado) THEN 'Administrador'
                WHEN EXISTS (SELECT 1 FROM dbo.CAJERO C        WHERE C.id_empleado = E.id_empleado) THEN 'Cajero'
                WHEN EXISTS (SELECT 1 FROM dbo.MESERO M        WHERE M.id_empleado = E.id_empleado) THEN 'Mesero'
                WHEN EXISTS (SELECT 1 FROM dbo.COCINERO K      WHERE K.id_empleado = E.id_empleado) THEN 'Cocinero'
                WHEN EXISTS (SELECT 1 FROM dbo.REPARTIDOR R    WHERE R.id_empleado = E.id_empleado) THEN 'Repartidor'
                ELSE ''
            END";

        public static ResultadoAutenticacion Autenticar(string usuario, string contrasena)
        {
            usuario = (usuario ?? "").Trim();

            if (usuario.Length == 0 || string.IsNullOrEmpty(contrasena))
                return Fallo("Ingrese su usuario y contraseña.");

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        int id = 0, intentos = 0, sucursal = 0;
                        string guardada = null, estado = null, nombre = null, cargo = null;
                        bool acceso = false, debeCambiar = false, existe;
                        DateTime? bloqueo = null;
                        DateTime ahora = DateTime.Now;

                        using (SqlCommand cmd = new SqlCommand(@"
                            SELECT E.id_empleado, E.[contraseña] AS clave, E.estado,
                                   E.acceso_habilitado, E.debe_cambiar_contrasena,
                                   E.intentos_fallidos, E.bloqueo_hasta, E.id_sucursal,
                                   LTRIM(RTRIM(E.nombre + ' ' + E.apellidos)) AS nombre,
                                   " + SqlCargo + @" AS cargo,
                                   SYSDATETIME() AS ahora
                            FROM dbo.EMPLEADO E WITH (UPDLOCK, ROWLOCK)
                            WHERE E.usuario = @Usuario;", conexion, tx))
                        {
                            cmd.CommandTimeout = 15;
                            cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 50).Value = usuario;
                            using (SqlDataReader r = cmd.ExecuteReader())
                            {
                                existe = r.Read();
                                if (existe)
                                {
                                    id = Convert.ToInt32(r["id_empleado"]);
                                    guardada = r["clave"] == DBNull.Value ? null : Convert.ToString(r["clave"]);
                                    estado = Convert.ToString(r["estado"]).Trim().ToLowerInvariant();
                                    acceso = Convert.ToBoolean(r["acceso_habilitado"]);
                                    debeCambiar = Convert.ToBoolean(r["debe_cambiar_contrasena"]);
                                    intentos = Convert.ToInt32(r["intentos_fallidos"]);
                                    bloqueo = r["bloqueo_hasta"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["bloqueo_hasta"]);
                                    sucursal = Convert.ToInt32(r["id_sucursal"]);
                                    nombre = Convert.ToString(r["nombre"]);
                                    cargo = Convert.ToString(r["cargo"]);
                                    ahora = Convert.ToDateTime(r["ahora"]);
                                }
                            }
                        }

                        // Usuario inexistente: mismo mensaje que clave errónea.
                        if (!existe)
                        {
                            tx.Commit();
                            Registro.Info("Sesion", "Intento fallido (usuario no reconocido).");
                            return Fallo("Usuario o contraseña incorrectos.");
                        }

                        if (bloqueo.HasValue && bloqueo.Value > ahora)
                        {
                            tx.Commit();
                            return Fallo("La cuenta está bloqueada temporalmente por intentos fallidos.\n" +
                                         "Inténtelo de nuevo después de las " + bloqueo.Value.ToString("HH:mm") + ".");
                        }

                        if (!Contrasenas.Verificar(contrasena, guardada))
                        {
                            intentos++;
                            bool bloquear = intentos >= IntentosMaximos;

                            using (SqlCommand cmd = new SqlCommand(@"
                                UPDATE dbo.EMPLEADO
                                   SET intentos_fallidos = @Intentos,
                                       bloqueo_hasta = CASE WHEN @Bloquear = 1
                                                            THEN DATEADD(MINUTE, @Minutos, SYSDATETIME())
                                                            ELSE bloqueo_hasta END
                                 WHERE id_empleado = @Id;", conexion, tx))
                            {
                                cmd.Parameters.Add("@Intentos", SqlDbType.Int).Value = bloquear ? 0 : intentos;
                                cmd.Parameters.Add("@Bloquear", SqlDbType.Bit).Value = bloquear;
                                cmd.Parameters.Add("@Minutos", SqlDbType.Int).Value = MinutosBloqueo;
                                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                                cmd.ExecuteNonQuery();
                            }

                            tx.Commit();
                            Registro.Info("Sesion", "Contraseña incorrecta para el empleado " + id +
                                (bloquear ? " (cuenta bloqueada " + MinutosBloqueo + " min)." : "."));

                            return Fallo(bloquear
                                ? "Usuario o contraseña incorrectos.\n\nPor seguridad, la cuenta quedó bloqueada " +
                                  MinutosBloqueo + " minutos."
                                : "Usuario o contraseña incorrectos.");
                        }

                        // Contraseña correcta: comprobar estado y cuenta.
                        if (estado != "activo")
                        {
                            tx.Commit();
                            return Fallo("El empleado está inactivo. Consulte con el Administrador.");
                        }

                        if (!acceso)
                        {
                            tx.Commit();
                            return Fallo("La cuenta de acceso está deshabilitada. Consulte con el Administrador.");
                        }

                        if (string.IsNullOrEmpty(cargo))
                        {
                            tx.Commit();
                            return Fallo("El empleado no tiene un cargo asignado. Consulte con el Administrador.");
                        }

                        // Restablecer contadores y migrar el hash antiguo.
                        bool migrar = Contrasenas.NecesitaActualizar(guardada);
                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE dbo.EMPLEADO
                               SET intentos_fallidos = 0,
                                   bloqueo_hasta = NULL" +
                                   (migrar ? ", [contraseña] = @Hash" : "") + @"
                             WHERE id_empleado = @Id;", conexion, tx))
                        {
                            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                            if (migrar)
                                cmd.Parameters.Add("@Hash", SqlDbType.VarChar, 255).Value = Contrasenas.GenerarHash(contrasena);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();
                        Registro.Info("Sesion", "Inicio de sesión del empleado " + id + " (" + cargo + ")" +
                            (migrar ? "; contraseña migrada a PBKDF2." : "."));

                        return new ResultadoAutenticacion
                        {
                            Correcto = true,
                            IdEmpleado = id,
                            Usuario = usuario,
                            NombreCompleto = nombre,
                            Cargo = cargo,
                            IdSucursal = sucursal,
                            DebeCambiarContrasena = debeCambiar
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

        // Cambio de contraseña del propio empleado (obligatorio si es temporal).
        public static string CambiarContrasena(int idEmpleado, string usuario, string actual, string nueva)
        {
            string politica = Contrasenas.ValidarPolitica(nueva, usuario);
            if (politica != null)
                return politica;

            if (nueva == actual)
                return "La nueva contraseña debe ser distinta de la actual.";

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.ReadCommitted))
                {
                    string guardada;
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT [contraseña] FROM dbo.EMPLEADO WITH (UPDLOCK, ROWLOCK)
                        WHERE id_empleado = @Id AND estado = 'activo' AND acceso_habilitado = 1;", conexion, tx))
                    {
                        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                        object valor = cmd.ExecuteScalar();
                        guardada = valor == null || valor == DBNull.Value ? null : Convert.ToString(valor);
                    }

                    if (!Contrasenas.Verificar(actual, guardada))
                    {
                        tx.Rollback();
                        return "La contraseña actual no es correcta.";
                    }

                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE dbo.EMPLEADO
                           SET [contraseña] = @Hash, debe_cambiar_contrasena = 0,
                               intentos_fallidos = 0, bloqueo_hasta = NULL
                         WHERE id_empleado = @Id;", conexion, tx))
                    {
                        cmd.Parameters.Add("@Hash", SqlDbType.VarChar, 255).Value = Contrasenas.GenerarHash(nueva);
                        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }

            Registro.Info("Sesion", "El empleado " + idEmpleado + " cambió su contraseña.");
            return null;
        }

        private static ResultadoAutenticacion Fallo(string mensaje)
        {
            return new ResultadoAutenticacion { Correcto = false, Mensaje = mensaje };
        }
    }
}
