using System;
using System.Collections.Generic;
using System.Linq;
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

    // ==========================================================
    // PERMISOS POR PERFIL (única fuente de reglas de acceso)
    // ----------------------------------------------------------
    //  Administrador: todos los módulos y operaciones.
    //  Cajero:        Inicio, Pedidos y Mesas (cobros y comprobantes).
    //  Mesero:        Inicio, Pedidos y Mesas (incluye reservas).
    //  Cocinero:      Inicio y Cocina.
    //  Repartidor:    Inicio y Pedidos (entregas).
    // Las operaciones sensibles se vuelven a validar en SQL Server.
    // ==========================================================
    internal static class Permisos
    {
        private static readonly Dictionary<Type, string> nombresModulo = new Dictionary<Type, string>
        {
            { typeof(FRM_INICIO), "Inicio" },
            { typeof(FRMS_PEDIDOS), "Pedidos" },
            { typeof(FRM_MESAS), "Mesas y reservas" },
            { typeof(FRM_COCINA), "Cocina" },
            { typeof(FRM_EMPLEADOS), "Empleados y cuentas" },
            { typeof(FRM_MENU), "Menú" },
            { typeof(FRM_INVENTARIO), "Inventario" },
            { typeof(FRM_CLIENTES), "Clientes" },
            { typeof(FRM_PROMOCIONES), "Promociones" },
            { typeof(FRM_REPORTES), "Reportes" }
        };

        private static readonly Dictionary<string, HashSet<Type>> modulosPorCargo =
            new Dictionary<string, HashSet<Type>>
            {
                { Cargos.Administrador, new HashSet<Type>(nombresModulo.Keys) },
                { Cargos.Cajero, new HashSet<Type> { typeof(FRM_INICIO), typeof(FRMS_PEDIDOS), typeof(FRM_MESAS) } },
                { Cargos.Mesero, new HashSet<Type> { typeof(FRM_INICIO), typeof(FRMS_PEDIDOS), typeof(FRM_MESAS) } },
                { Cargos.Cocinero, new HashSet<Type> { typeof(FRM_INICIO), typeof(FRM_COCINA) } },
                { Cargos.Repartidor, new HashSet<Type> { typeof(FRM_INICIO), typeof(FRMS_PEDIDOS) } }
            };

        public static bool PuedeAbrir(Type modulo, string cargo)
        {
            HashSet<Type> permitidos;
            return cargo != null && modulosPorCargo.TryGetValue(cargo, out permitidos) &&
                   permitidos.Contains(modulo);
        }

        public static string NombreModulo(Type modulo)
        {
            string nombre;
            return nombresModulo.TryGetValue(modulo, out nombre) ? nombre : modulo.Name;
        }

        // Texto para mostrar los módulos de un cargo (cuentas de acceso).
        public static string DescribirModulos(string cargo)
        {
            HashSet<Type> permitidos;
            if (cargo == null || !modulosPorCargo.TryGetValue(cargo, out permitidos))
                return "Ninguno (sin cargo)";

            return string.Join(", ", nombresModulo.Where(m => permitidos.Contains(m.Key)).Select(m => m.Value)) +
                   (PuedeCobrar(cargo) ? ". Puede cobrar." : ". No puede cobrar.");
        }

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

        // Crear cuentas, habilitarlas, bloquearlas y restablecer contraseñas.
        public static bool PuedeGestionarCuentas(string cargo)
        {
            return cargo == Cargos.Administrador;
        }

        // Crear, editar, desactivar productos y cambiar precios (módulo Menú).
        public static bool PuedeGestionarMenu(string cargo)
        {
            return cargo == Cargos.Administrador;
        }

        // Elegir una promoción vigente al registrar un pedido.
        public static bool PuedeAplicarPromociones(string cargo)
        {
            return cargo == Cargos.Administrador || cargo == Cargos.Cajero || cargo == Cargos.Mesero;
        }

        // Registrar, editar, activar y desactivar promociones.
        public static bool PuedeGestionarPromociones(string cargo)
        {
            return cargo == Cargos.Administrador;
        }

        // Insumos, compras, mermas y proveedores (módulo Inventario).
        public static bool PuedeGestionarInventario(string cargo)
        {
            return cargo == Cargos.Administrador;
        }

        // Registrar, editar y desactivar clientes (módulo Clientes).
        public static bool PuedeGestionarClientes(string cargo)
        {
            return cargo == Cargos.Administrador;
        }

        // Perfiles que atienden la llegada de comensales con reserva.
        public static bool AtiendeReservas(string cargo)
        {
            return cargo == Cargos.Administrador || cargo == Cargos.Cajero || cargo == Cargos.Mesero;
        }

        // ------------------------------------------------------
        // Comprueba en SQL Server que la sesión sigue siendo válida:
        // mismo empleado, activo, cuenta habilitada, sin bloqueo,
        // misma sucursal y mismo cargo que al iniciar sesión.
        // Devuelve null si es válida o el motivo si no lo es.
        // Si SQL Server no responde lanza la excepción (quien llama
        // decide; la navegación no se bloquea por un corte de red).
        // ------------------------------------------------------
        public static string VerificarSesionVigente()
        {
            if (!SesionActual.Iniciada || !SesionActual.IdEmpleado.HasValue)
                return "No hay una sesión iniciada.";

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT E.estado, E.acceso_habilitado, E.id_sucursal,
                       CASE WHEN E.bloqueo_hasta > SYSDATETIME() THEN 1 ELSE 0 END AS bloqueado,
                       " + Autenticacion.SqlCargo + @" AS cargo
                FROM dbo.EMPLEADO E
                WHERE E.id_empleado = @Id;", conexion))
            {
                cmd.CommandTimeout = 10;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = SesionActual.IdEmpleado.Value;
                conexion.Open();

                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                        return "Su empleado ya no existe en el sistema.";
                    if (Convert.ToString(r["estado"]).Trim() != "activo")
                        return "Su empleado fue desactivado.";
                    if (!Convert.ToBoolean(r["acceso_habilitado"]))
                        return "Su cuenta de acceso fue deshabilitada.";
                    if (Convert.ToInt32(r["bloqueado"]) == 1)
                        return "Su cuenta fue bloqueada.";
                    if (Convert.ToInt32(r["id_sucursal"]) != SesionActual.IdSucursal)
                        return "Su sucursal asignada cambió.";
                    if (Convert.ToString(r["cargo"]) != SesionActual.Cargo)
                        return "Su cargo cambió.";
                }
            }

            return null;
        }
    }

    internal static class Autenticacion
    {
        public const int IntentosMaximos = 5;
        public const int MinutosBloqueo = 15;

        // Bloqueo manual del Administrador: se guarda en la columna
        // existente bloqueo_hasta con esta fecha (no caduca sola).
        // Los bloqueos por intentos fallidos usan +15 minutos.
        public static readonly DateTime BloqueoManual = new DateTime(9999, 12, 31);

        public static bool EsBloqueoManual(DateTime bloqueoHasta)
        {
            return bloqueoHasta.Year >= 9999;
        }

        internal const string SqlCargo = @"
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
                            if (EsBloqueoManual(bloqueo.Value))
                                return Fallo("La cuenta está bloqueada por el Administrador. Consulte con el Administrador.");
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
