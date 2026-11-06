using System;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // CUENTAS DE ACCESO DE LOS EMPLEADOS (FASE B)
    // ----------------------------------------------------------
    // Usa solo columnas existentes de EMPLEADO: usuario,
    // [contraseña], acceso_habilitado, debe_cambiar_contrasena,
    // intentos_fallidos y bloqueo_hasta. Sin cambios de esquema.
    //
    // Reglas:
    //  * Solo un Administrador activo, con acceso y sin bloqueo
    //    puede operar (se valida en SQL en cada operación).
    //  * Usuario único (índice UX_EMPLEADO_USUARIO + comprobación).
    //  * Contraseña temporal aleatoria guardada con PBKDF2 y
    //    cambio obligatorio en el primer ingreso.
    //  * La contraseña temporal se devuelve una sola vez para
    //    mostrarla; nunca se guarda en claro ni en el registro.
    //  * Nunca queda el sistema sin un Administrador con acceso.
    //  * Un Administrador no puede deshabilitar ni bloquear su
    //    propia cuenta.
    // ==========================================================
    internal static class CuentaDatos
    {
        public const int UsuarioMinimo = 4;
        public const int UsuarioMaximo = 30;

        // ------------------------------------------------------
        // Usuario: minúsculas, dígitos, punto o guion bajo;
        // empieza con letra. Se guarda en minúsculas.
        // ------------------------------------------------------
        public static string NormalizarUsuario(string usuario)
        {
            usuario = (usuario ?? "").Trim().ToLowerInvariant();

            if (usuario.Length < UsuarioMinimo || usuario.Length > UsuarioMaximo)
            {
                throw new ReglaNegocioException(
                    "El usuario debe tener entre " + UsuarioMinimo + " y " + UsuarioMaximo + " caracteres.");
            }

            if (usuario[0] < 'a' || usuario[0] > 'z')
                throw new ReglaNegocioException("El usuario debe empezar con una letra.");

            foreach (char c in usuario)
            {
                bool valido = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_';
                if (!valido)
                {
                    throw new ReglaNegocioException(
                        "El usuario solo puede contener letras sin tilde, números, punto o guion bajo.");
                }
            }

            return usuario;
        }

        // Inicial del nombre + primer apellido (p. ej. "lquispe"),
        // sin tildes; si existe, se agrega un número.
        public static string SugerirUsuario(string nombre, string apellidos)
        {
            string inicial = SinTildes(nombre).Where(char.IsLetter).Take(1)
                .Aggregate("", (a, c) => a + c);
            string apellido = SinTildes((apellidos ?? "").Trim().Split(' ').FirstOrDefault())
                .Where(char.IsLetter).Aggregate("", (a, c) => a + c);

            string baseUsuario = (inicial + apellido).ToLowerInvariant();
            if (baseUsuario.Length > UsuarioMaximo - 2)
                baseUsuario = baseUsuario.Substring(0, UsuarioMaximo - 2);
            while (baseUsuario.Length < UsuarioMinimo)
                baseUsuario += "1";

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.EMPLEADO WHERE usuario = @Usuario;", conexion))
            {
                SqlParameter p = cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 50);
                conexion.Open();

                for (int n = 1; n < 100; n++)
                {
                    string candidato = n == 1 ? baseUsuario : baseUsuario + n;
                    p.Value = candidato;
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                        return candidato;
                }
            }

            return baseUsuario;
        }

        private static string SinTildes(string texto)
        {
            string descompuesto = (texto ?? "").Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // ------------------------------------------------------
        // CREAR CUENTA (empleado activo, con cargo y sin cuenta).
        // Devuelve la contraseña temporal para mostrarla una vez.
        // ------------------------------------------------------
        public static string CrearCuenta(int idEmpleado, string usuario, int idAdministrador)
        {
            usuario = NormalizarUsuario(usuario);
            return EmpleadoDatos.Ejecutar((conexion, tx) =>
                CrearCuenta(conexion, tx, idEmpleado, usuario, idAdministrador));
        }

        internal static string CrearCuenta(
            SqlConnection conexion, SqlTransaction tx,
            int idEmpleado, string usuario, int idAdministrador)
        {
            usuario = NormalizarUsuario(usuario);
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idAdministrador);
            EmpleadoInfo e = EmpleadoDatos.ObtenerBloqueado(conexion, tx, idEmpleado);

            if (e.Estado != "activo")
                throw new ReglaNegocioException("Solo se puede crear una cuenta para un empleado activo.");
            if (string.IsNullOrEmpty(e.Cargo))
                throw new ReglaNegocioException("El empleado no tiene un cargo asignado.");
            if (e.TieneUsuario)
                throw new ReglaNegocioException("El empleado ya tiene una cuenta (" + e.Usuario + ").");

            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) nombre + ' ' + apellidos
                FROM dbo.EMPLEADO WITH (UPDLOCK, HOLDLOCK)
                WHERE usuario = @Usuario;", conexion, tx))
            {
                cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 50).Value = usuario;
                if (cmd.ExecuteScalar() != null)
                    throw new ReglaNegocioException("El usuario \"" + usuario + "\" ya está en uso. Elija otro.");
            }

            string temporal = Contrasenas.GenerarTemporal();

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.EMPLEADO
                   SET usuario = @Usuario,
                       [contraseña] = @Hash,
                       acceso_habilitado = 1,
                       debe_cambiar_contrasena = 1,
                       intentos_fallidos = 0,
                       bloqueo_hasta = NULL
                 WHERE id_empleado = @Id AND usuario IS NULL;", conexion, tx))
            {
                cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 50).Value = usuario;
                cmd.Parameters.Add("@Hash", SqlDbType.VarChar, 255).Value = Contrasenas.GenerarHash(temporal);
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;

                if (cmd.ExecuteNonQuery() != 1)
                    throw new ReglaNegocioException("No se pudo crear la cuenta: el empleado cambió. Actualice la lista.");
            }

            Registro.Info("Cuentas", "Administrador " + idAdministrador + " creó la cuenta \"" + usuario +
                "\" del empleado " + idEmpleado + ".");
            return temporal;
        }

        // ------------------------------------------------------
        // REGISTRAR EMPLEADO + CUENTA en una sola transacción:
        // si falla la cuenta, tampoco se registra el empleado.
        // ------------------------------------------------------
        public static int RegistrarConCuenta(
            EmpleadoInfo e, string usuario, int idAdministrador, out string temporal)
        {
            EmpleadoDatos.Validar(e);
            usuario = NormalizarUsuario(usuario);

            if (e.Estado != "activo")
                throw new ReglaNegocioException("Solo un empleado activo puede tener acceso al sistema.");

            string clave = null;
            int id = EmpleadoDatos.Ejecutar((conexion, tx) =>
            {
                int nuevo = EmpleadoDatos.Registrar(conexion, tx, e, idAdministrador);
                clave = CrearCuenta(conexion, tx, nuevo, usuario, idAdministrador);
                return nuevo;
            });

            temporal = clave;
            return id;
        }

        // ------------------------------------------------------
        // HABILITAR / DESHABILITAR ACCESO
        // ------------------------------------------------------
        public static void CambiarAcceso(int idEmpleado, bool habilitar, int idAdministrador)
        {
            EmpleadoDatos.Ejecutar((conexion, tx) =>
            {
                CambiarAcceso(conexion, tx, idEmpleado, habilitar, idAdministrador);
                return 0;
            });
        }

        internal static void CambiarAcceso(
            SqlConnection conexion, SqlTransaction tx,
            int idEmpleado, bool habilitar, int idAdministrador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idAdministrador);
            EmpleadoInfo e = ObtenerConCuenta(conexion, tx, idEmpleado);

            if (habilitar)
            {
                if (e.Estado != "activo")
                    throw new ReglaNegocioException("Active primero al empleado para habilitar su cuenta.");
                if (string.IsNullOrEmpty(e.Cargo))
                    throw new ReglaNegocioException("El empleado no tiene un cargo asignado.");
            }
            else
            {
                ProtegerAdministradores(conexion, tx, e, idAdministrador, "deshabilitar");
            }

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.EMPLEADO SET acceso_habilitado = @Acceso
                 WHERE id_empleado = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Acceso", SqlDbType.Bit).Value = habilitar;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                cmd.ExecuteNonQuery();
            }

            Registro.Info("Cuentas", "Administrador " + idAdministrador + (habilitar ? " habilitó" : " deshabilitó") +
                " la cuenta del empleado " + idEmpleado + ".");
        }

        // ------------------------------------------------------
        // BLOQUEAR (manual, sin caducidad) / DESBLOQUEAR (manual
        // o por intentos fallidos).
        // ------------------------------------------------------
        public static void CambiarBloqueo(int idEmpleado, bool bloquear, int idAdministrador)
        {
            EmpleadoDatos.Ejecutar((conexion, tx) =>
            {
                CambiarBloqueo(conexion, tx, idEmpleado, bloquear, idAdministrador);
                return 0;
            });
        }

        internal static void CambiarBloqueo(
            SqlConnection conexion, SqlTransaction tx,
            int idEmpleado, bool bloquear, int idAdministrador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idAdministrador);
            EmpleadoInfo e = ObtenerConCuenta(conexion, tx, idEmpleado);

            if (bloquear)
                ProtegerAdministradores(conexion, tx, e, idAdministrador, "bloquear");

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.EMPLEADO
                   SET bloqueo_hasta = @Bloqueo, intentos_fallidos = 0
                 WHERE id_empleado = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Bloqueo", SqlDbType.DateTime2).Value =
                    bloquear ? (object)Autenticacion.BloqueoManual : DBNull.Value;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                cmd.ExecuteNonQuery();
            }

            Registro.Info("Cuentas", "Administrador " + idAdministrador + (bloquear ? " bloqueó" : " desbloqueó") +
                " la cuenta del empleado " + idEmpleado + ".");
        }

        // ------------------------------------------------------
        // RESTABLECER CONTRASEÑA: nueva temporal + cambio
        // obligatorio. Quita el bloqueo por intentos fallidos,
        // pero mantiene un bloqueo manual del Administrador.
        // ------------------------------------------------------
        public static string RestablecerContrasena(int idEmpleado, int idAdministrador)
        {
            return EmpleadoDatos.Ejecutar((conexion, tx) =>
                RestablecerContrasena(conexion, tx, idEmpleado, idAdministrador));
        }

        internal static string RestablecerContrasena(
            SqlConnection conexion, SqlTransaction tx, int idEmpleado, int idAdministrador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idAdministrador);
            ObtenerConCuenta(conexion, tx, idEmpleado);

            string temporal = Contrasenas.GenerarTemporal();

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.EMPLEADO
                   SET [contraseña] = @Hash,
                       debe_cambiar_contrasena = 1,
                       intentos_fallidos = 0,
                       bloqueo_hasta = CASE WHEN bloqueo_hasta >= '99990101' THEN bloqueo_hasta END
                 WHERE id_empleado = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Hash", SqlDbType.VarChar, 255).Value = Contrasenas.GenerarHash(temporal);
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                cmd.ExecuteNonQuery();
            }

            Registro.Info("Cuentas", "Administrador " + idAdministrador +
                " restableció la contraseña del empleado " + idEmpleado + ".");
            return temporal;
        }

        // ------------------------------------------------------
        // AUXILIARES
        // ------------------------------------------------------
        private static EmpleadoInfo ObtenerConCuenta(
            SqlConnection conexion, SqlTransaction tx, int idEmpleado)
        {
            EmpleadoInfo e = EmpleadoDatos.ObtenerBloqueado(conexion, tx, idEmpleado);
            if (!e.TieneUsuario)
                throw new ReglaNegocioException("El empleado no tiene una cuenta de acceso. Créela primero.");
            return e;
        }

        // Nadie se deshabilita/bloquea a sí mismo y siempre queda
        // otro Administrador activo con acceso y sin bloqueo.
        private static void ProtegerAdministradores(
            SqlConnection conexion, SqlTransaction tx,
            EmpleadoInfo e, int idAdministrador, string accion)
        {
            if (e.IdEmpleado == idAdministrador)
                throw new ReglaNegocioException("No puede " + accion + " su propia cuenta.");

            if (e.Cargo == Cargos.Administrador)
                EmpleadoDatos.VerificarOtroAdministrador(conexion, tx, e.IdEmpleado);
        }
    }
}
