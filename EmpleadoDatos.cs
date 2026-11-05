using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Error de una regla del restaurante: el mensaje se muestra
    // tal cual al usuario.
    // ==========================================================
    internal sealed class ReglaNegocioException : Exception
    {
        public ReglaNegocioException(string mensaje) : base(mensaje) { }
    }

    // ==========================================================
    // Cargos = perfiles. Cada uno corresponde a una tabla de
    // subtipo existente en SQL Server (regla ISA disjunta).
    // ==========================================================
    internal static class Cargos
    {
        public const string Administrador = "Administrador";
        public const string Cajero = "Cajero";
        public const string Mesero = "Mesero";
        public const string Cocinero = "Cocinero";
        public const string Repartidor = "Repartidor";

        public static readonly string[] Todos =
        {
            Administrador, Cajero, Mesero, Cocinero, Repartidor
        };

        public static string Tabla(string cargo)
        {
            switch (cargo)
            {
                case Administrador: return "dbo.ADMINISTRADOR";
                case Cajero: return "dbo.CAJERO";
                case Mesero: return "dbo.MESERO";
                case Cocinero: return "dbo.COCINERO";
                case Repartidor: return "dbo.REPARTIDOR";
                default: throw new ReglaNegocioException("Cargo no válido.");
            }
        }
    }

    internal static class Turnos
    {
        // Valor en SQL -> texto visible.
        public static readonly string[] Valores = { "mañana", "tarde", "noche" };

        public static string Mostrar(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return "Sin asignar";
            return char.ToUpper(valor[0]) + valor.Substring(1);
        }
    }

    internal sealed class EmpleadoInfo
    {
        public int IdEmpleado;
        public string Dni;
        public string Nombre;
        public string Apellidos;
        public string Telefono;
        public string Cargo;
        public string Turno;          // mañana / tarde / noche
        public string Estado;         // activo / inactivo
        public int IdSucursal;

        // Datos propios del cargo.
        public string AreaAsignada;   // Mesero
        public string Certificacion;  // Cocinero
        public string ZonaReparto;    // Repartidor
        public string Vehiculo;       // Repartidor

        // Solo lectura (nunca se muestran contraseñas ni hashes).
        public string Sucursal;
        public bool AccesoHabilitado;
        public bool TieneUsuario;

        public string NombreCompleto
        {
            get { return (Nombre + " " + Apellidos).Trim(); }
        }
    }

    internal sealed class SucursalInfo
    {
        public int IdSucursal;
        public string Nombre;
        public override string ToString() { return Nombre; }
    }

    // ==========================================================
    // ACCESO A DATOS DE EMPLEADOS
    // Todas las escrituras:
    //   * se ejecutan en una transacción Serializable,
    //   * vuelven a comprobar en SQL que quien opera es un
    //     Administrador activo con acceso habilitado,
    //   * conservan el historial (nunca borran empleados).
    // ==========================================================
    internal static class EmpleadoDatos
    {
        private const string SqlListar = @"
            SELECT
                E.id_empleado, E.dni, E.nombre, E.apellidos,
                E.[teléfono] AS telefono, E.estado, E.turno,
                E.id_sucursal, S.nombre AS sucursal,
                E.acceso_habilitado,
                CAST(CASE WHEN E.usuario IS NULL THEN 0 ELSE 1 END AS BIT) AS tiene_usuario,
                CASE
                    WHEN A.id_empleado IS NOT NULL THEN 'Administrador'
                    WHEN C.id_empleado IS NOT NULL THEN 'Cajero'
                    WHEN M.id_empleado IS NOT NULL THEN 'Mesero'
                    WHEN K.id_empleado IS NOT NULL THEN 'Cocinero'
                    WHEN R.id_empleado IS NOT NULL THEN 'Repartidor'
                    ELSE ''
                END AS cargo,
                M.[área_asignada] AS area_asignada,
                K.[certificación] AS certificacion,
                R.zona_reparto,
                R.[vehículo] AS vehiculo
            FROM dbo.EMPLEADO E
            INNER JOIN dbo.SUCURSAL S ON S.id_sucursal = E.id_sucursal
            LEFT JOIN dbo.ADMINISTRADOR A ON A.id_empleado = E.id_empleado
            LEFT JOIN dbo.CAJERO        C ON C.id_empleado = E.id_empleado
            LEFT JOIN dbo.MESERO        M ON M.id_empleado = E.id_empleado
            LEFT JOIN dbo.COCINERO      K ON K.id_empleado = E.id_empleado
            LEFT JOIN dbo.REPARTIDOR    R ON R.id_empleado = E.id_empleado";

        // ------------------------------------------------------
        // CONSULTAS
        // ------------------------------------------------------
        public static List<EmpleadoInfo> Listar()
        {
            List<EmpleadoInfo> lista = new List<EmpleadoInfo>();

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(
                SqlListar + " ORDER BY E.apellidos, E.nombre;", conexion))
            {
                conexion.Open();
                using (SqlDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                        lista.Add(Leer(lector));
                }
            }

            return lista;
        }

        public static List<SucursalInfo> ListarSucursales(int? incluirId)
        {
            List<SucursalInfo> lista = new List<SucursalInfo>();

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT id_sucursal, nombre
                FROM dbo.SUCURSAL
                WHERE estado = 'activa' OR id_sucursal = @Incluir
                ORDER BY id_sucursal;", conexion))
            {
                cmd.Parameters.Add("@Incluir", SqlDbType.Int).Value =
                    incluirId.HasValue ? (object)incluirId.Value : DBNull.Value;

                conexion.Open();
                using (SqlDataReader lector = cmd.ExecuteReader())
                {
                    while (lector.Read())
                    {
                        lista.Add(new SucursalInfo
                        {
                            IdSucursal = Convert.ToInt32(lector["id_sucursal"]),
                            Nombre = Convert.ToString(lector["nombre"])
                        });
                    }
                }
            }

            return lista;
        }

        private static EmpleadoInfo Leer(SqlDataReader r)
        {
            return new EmpleadoInfo
            {
                IdEmpleado = Convert.ToInt32(r["id_empleado"]),
                Dni = Convert.ToString(r["dni"]).Trim(),
                Nombre = Convert.ToString(r["nombre"]),
                Apellidos = Convert.ToString(r["apellidos"]),
                Telefono = r["telefono"] == DBNull.Value ? "" : Convert.ToString(r["telefono"]),
                Estado = Convert.ToString(r["estado"]).Trim().ToLowerInvariant(),
                Turno = r["turno"] == DBNull.Value ? null : Convert.ToString(r["turno"]),
                IdSucursal = Convert.ToInt32(r["id_sucursal"]),
                Sucursal = Convert.ToString(r["sucursal"]),
                AccesoHabilitado = Convert.ToBoolean(r["acceso_habilitado"]),
                TieneUsuario = Convert.ToBoolean(r["tiene_usuario"]),
                Cargo = Convert.ToString(r["cargo"]),
                AreaAsignada = r["area_asignada"] == DBNull.Value ? "" : Convert.ToString(r["area_asignada"]),
                Certificacion = r["certificacion"] == DBNull.Value ? "" : Convert.ToString(r["certificacion"]),
                ZonaReparto = r["zona_reparto"] == DBNull.Value ? "" : Convert.ToString(r["zona_reparto"]),
                Vehiculo = r["vehiculo"] == DBNull.Value ? "" : Convert.ToString(r["vehiculo"])
            };
        }

        // ------------------------------------------------------
        // VALIDACIÓN DE DATOS (también la usa el formulario)
        // ------------------------------------------------------
        public static void Validar(EmpleadoInfo e)
        {
            e.Dni = (e.Dni ?? "").Trim();
            e.Nombre = Limpiar(e.Nombre);
            e.Apellidos = Limpiar(e.Apellidos);
            e.Telefono = (e.Telefono ?? "").Trim();

            if (e.Dni.Length != 8 || !e.Dni.All(char.IsDigit))
                throw new ReglaNegocioException("El DNI debe tener exactamente 8 dígitos.");

            ValidarNombre(e.Nombre, "Los nombres");
            ValidarNombre(e.Apellidos, "Los apellidos");

            if (e.Telefono.Length > 0 &&
                (e.Telefono.Length != 9 || !e.Telefono.All(char.IsDigit)))
                throw new ReglaNegocioException("El teléfono debe tener 9 dígitos (o dejarse vacío).");

            if (!Cargos.Todos.Contains(e.Cargo))
                throw new ReglaNegocioException("Seleccione el cargo.");

            if (!Turnos.Valores.Contains(e.Turno))
                throw new ReglaNegocioException("Seleccione el turno.");

            if (e.Estado != "activo" && e.Estado != "inactivo")
                throw new ReglaNegocioException("Seleccione el estado laboral.");

            if (e.IdSucursal <= 0)
                throw new ReglaNegocioException("Seleccione la sucursal.");

            e.AreaAsignada = Opcional(e.AreaAsignada, 100, "El área asignada");
            e.Certificacion = Opcional(e.Certificacion, 100, "La certificación");
            e.ZonaReparto = Opcional(e.ZonaReparto, 100, "La zona de reparto");
            e.Vehiculo = Opcional(e.Vehiculo, 100, "El vehículo");
        }

        private static string Limpiar(string texto)
        {
            texto = (texto ?? "").Trim();
            while (texto.Contains("  "))
                texto = texto.Replace("  ", " ");
            return texto;
        }

        private static void ValidarNombre(string valor, string campo)
        {
            if (valor.Length < 2 || valor.Length > 100)
                throw new ReglaNegocioException(campo + " deben tener entre 2 y 100 caracteres.");

            foreach (char c in valor)
            {
                if (!char.IsLetter(c) && c != ' ' && c != '\'' && c != '-' && c != '.')
                    throw new ReglaNegocioException(campo + " solo pueden contener letras.");
            }
        }

        private static string Opcional(string valor, int maximo, string campo)
        {
            valor = Limpiar(valor);
            if (valor.Length > maximo)
                throw new ReglaNegocioException(campo + " no puede superar " + maximo + " caracteres.");
            return valor.Length == 0 ? null : valor;
        }

        // ------------------------------------------------------
        // REGISTRAR
        // ------------------------------------------------------
        public static int Registrar(EmpleadoInfo e, int idAdministrador)
        {
            Validar(e);

            return Ejecutar((conexion, tx) =>
                Registrar(conexion, tx, e, idAdministrador));
        }

        internal static int Registrar(
            SqlConnection conexion, SqlTransaction tx,
            EmpleadoInfo e, int idAdministrador)
        {
            VerificarAdministrador(conexion, tx, idAdministrador);
            VerificarSucursal(conexion, tx, e.IdSucursal);
            VerificarDniLibre(conexion, tx, e.Dni, null);

            int id;
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO dbo.EMPLEADO
                    (dni, nombre, apellidos, [teléfono], usuario, [contraseña],
                     estado, id_sucursal, turno, acceso_habilitado)
                VALUES
                    (@Dni, @Nombre, @Apellidos, @Telefono, NULL, NULL,
                     @Estado, @Sucursal, @Turno, 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conexion, tx))
            {
                ParametrosEmpleado(cmd, e);
                id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            InsertarCargo(conexion, tx, id, e);
            return id;
        }

        // ------------------------------------------------------
        // ACTUALIZAR (datos personales, laborales y cargo)
        // ------------------------------------------------------
        public static void Actualizar(EmpleadoInfo e, int idAdministrador)
        {
            Validar(e);

            Ejecutar((conexion, tx) =>
            {
                Actualizar(conexion, tx, e, idAdministrador);
                return 0;
            });
        }

        internal static void Actualizar(
            SqlConnection conexion, SqlTransaction tx,
            EmpleadoInfo e, int idAdministrador)
        {
            VerificarAdministrador(conexion, tx, idAdministrador);

            EmpleadoInfo actual = ObtenerBloqueado(conexion, tx, e.IdEmpleado);
            VerificarSucursal(conexion, tx, e.IdSucursal, actual.IdSucursal);
            VerificarDniLibre(conexion, tx, e.Dni, e.IdEmpleado);

            bool cambiaCargo = actual.Cargo != e.Cargo;
            bool desactiva = actual.Estado == "activo" && e.Estado == "inactivo";
            bool esElMismo = e.IdEmpleado == idAdministrador;

            if (esElMismo && (desactiva || cambiaCargo))
            {
                throw new ReglaNegocioException(
                    "No puede desactivarse ni quitarse el cargo de Administrador a sí mismo.");
            }

            // No dejar el sistema sin administradores.
            if (actual.Cargo == Cargos.Administrador && (cambiaCargo || desactiva))
                VerificarOtroAdministrador(conexion, tx, e.IdEmpleado);

            // Un empleado sin cargo registrado (datos antiguos) solo recibe el nuevo.
            if (cambiaCargo && actual.Cargo.Length > 0)
            {
                if (TieneHistorialEnCargo(conexion, tx, e.IdEmpleado, actual.Cargo))
                {
                    throw new ReglaNegocioException(
                        "No se puede cambiar el cargo de este empleado porque tiene " +
                        "pedidos, cobros u operaciones asociadas a su función actual (" +
                        actual.Cargo + ").\n\nSu historial se conserva sin cambios.");
                }

                using (SqlCommand cmd = new SqlCommand(
                    "DELETE FROM " + Cargos.Tabla(actual.Cargo) +
                    " WHERE id_empleado = @Id;", conexion, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = e.IdEmpleado;
                    cmd.ExecuteNonQuery();
                }
            }

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.EMPLEADO
                   SET dni = @Dni,
                       nombre = @Nombre,
                       apellidos = @Apellidos,
                       [teléfono] = @Telefono,
                       estado = @Estado,
                       id_sucursal = @Sucursal,
                       turno = @Turno
                 WHERE id_empleado = @Id;", conexion, tx))
            {
                ParametrosEmpleado(cmd, e);
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = e.IdEmpleado;

                if (cmd.ExecuteNonQuery() != 1)
                    throw new ReglaNegocioException("El empleado ya no existe.");
            }

            if (cambiaCargo)
                InsertarCargo(conexion, tx, e.IdEmpleado, e);
            else
                ActualizarDatosCargo(conexion, tx, e);
        }

        // ------------------------------------------------------
        // ACTIVAR / DESACTIVAR (sin borrar historial)
        // ------------------------------------------------------
        public static void CambiarEstado(int idEmpleado, bool activo, int idAdministrador)
        {
            Ejecutar((conexion, tx) =>
            {
                CambiarEstado(conexion, tx, idEmpleado, activo, idAdministrador);
                return 0;
            });
        }

        internal static void CambiarEstado(
            SqlConnection conexion, SqlTransaction tx,
            int idEmpleado, bool activo, int idAdministrador)
        {
            VerificarAdministrador(conexion, tx, idAdministrador);
            EmpleadoInfo actual = ObtenerBloqueado(conexion, tx, idEmpleado);

            if (!activo)
            {
                if (idEmpleado == idAdministrador)
                    throw new ReglaNegocioException("No puede desactivar su propia cuenta de Administrador.");

                if (actual.Cargo == Cargos.Administrador && actual.Estado == "activo")
                    VerificarOtroAdministrador(conexion, tx, idEmpleado);
            }

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.EMPLEADO SET estado = @Estado
                 WHERE id_empleado = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = activo ? "activo" : "inactivo";
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------
        // AUTORIZACIÓN (se vuelve a comprobar en cada operación)
        // ------------------------------------------------------
        internal static void VerificarAdministrador(
            SqlConnection conexion, SqlTransaction tx, int idAdministrador)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM dbo.EMPLEADO E
                INNER JOIN dbo.ADMINISTRADOR A ON A.id_empleado = E.id_empleado
                WHERE E.id_empleado = @Id
                  AND E.estado = 'activo'
                  AND E.acceso_habilitado = 1;", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAdministrador;

                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                {
                    throw new ReglaNegocioException(
                        "Operación no autorizada: se requiere un Administrador " +
                        "activo con acceso habilitado.");
                }
            }
        }

        private static void VerificarOtroAdministrador(
            SqlConnection conexion, SqlTransaction tx, int idExcluido)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM dbo.EMPLEADO E WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.ADMINISTRADOR A ON A.id_empleado = E.id_empleado
                WHERE E.id_empleado <> @Id
                  AND E.estado = 'activo'
                  AND E.acceso_habilitado = 1;", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idExcluido;

                if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                {
                    throw new ReglaNegocioException(
                        "Debe quedar al menos un Administrador activo con acceso al sistema.");
                }
            }
        }

        // ------------------------------------------------------
        // AUXILIARES
        // ------------------------------------------------------
        private static EmpleadoInfo ObtenerBloqueado(
            SqlConnection conexion, SqlTransaction tx, int idEmpleado)
        {
            // Bloquear la fila para evitar cambios simultáneos.
            using (SqlCommand bloqueo = new SqlCommand(@"
                SELECT id_empleado FROM dbo.EMPLEADO WITH (UPDLOCK, HOLDLOCK)
                WHERE id_empleado = @Id;", conexion, tx))
            {
                bloqueo.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                if (bloqueo.ExecuteScalar() == null)
                    throw new ReglaNegocioException("El empleado ya no existe.");
            }

            using (SqlCommand cmd = new SqlCommand(
                SqlListar + " WHERE E.id_empleado = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                using (SqlDataReader lector = cmd.ExecuteReader())
                {
                    if (!lector.Read())
                        throw new ReglaNegocioException("El empleado ya no existe.");
                    return Leer(lector);
                }
            }
        }

        private static void VerificarDniLibre(
            SqlConnection conexion, SqlTransaction tx, string dni, int? idActual)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) nombre + ' ' + apellidos
                FROM dbo.EMPLEADO WITH (UPDLOCK, HOLDLOCK)
                WHERE dni = @Dni
                  AND (@Id IS NULL OR id_empleado <> @Id);", conexion, tx))
            {
                cmd.Parameters.Add("@Dni", SqlDbType.Char, 8).Value = dni;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value =
                    idActual.HasValue ? (object)idActual.Value : DBNull.Value;

                object existente = cmd.ExecuteScalar();
                if (existente != null)
                {
                    throw new ReglaNegocioException(
                        "Ya existe un empleado con el DNI " + dni + ": " + existente + ".");
                }
            }
        }

        private static void VerificarSucursal(
            SqlConnection conexion, SqlTransaction tx, int idSucursal, int? sucursalActual = null)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM dbo.SUCURSAL
                WHERE id_sucursal = @Id
                  AND (estado = 'activa' OR id_sucursal = @Actual);", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idSucursal;
                cmd.Parameters.Add("@Actual", SqlDbType.Int).Value =
                    sucursalActual.HasValue ? (object)sucursalActual.Value : DBNull.Value;

                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                    throw new ReglaNegocioException("La sucursal seleccionada no está disponible.");
            }
        }

        // Relaciones que impiden cambiar de cargo (se conservan).
        internal static bool TieneHistorialEnCargo(
            SqlConnection conexion, SqlTransaction tx, int idEmpleado, string cargo)
        {
            string sql;
            switch (cargo)
            {
                case Cargos.Mesero:
                    sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.PEDIDO WHERE id_mesero = @Id) THEN 1 ELSE 0 END;";
                    break;
                case Cargos.Cocinero:
                    sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.PEDIDO WHERE id_cocinero = @Id) THEN 1 ELSE 0 END;";
                    break;
                case Cargos.Repartidor:
                    sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.PEDIDO WHERE id_empleado_reparto = @Id) THEN 1 ELSE 0 END;";
                    break;
                case Cargos.Cajero:
                    sql = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.PAGO WHERE id_cajero = @Id)
                                         OR EXISTS (SELECT 1 FROM dbo.CIERRE_CAJA WHERE id_cajero = @Id)
                                    THEN 1 ELSE 0 END;";
                    break;
                case Cargos.Administrador:
                    sql = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.COMPRA WHERE id_administrador = @Id)
                                         OR EXISTS (SELECT 1 FROM dbo.PAGO WHERE id_cajero = @Id)
                                         OR EXISTS (SELECT 1 FROM dbo.CIERRE_CAJA WHERE id_cajero = @Id)
                                    THEN 1 ELSE 0 END;";
                    break;
                default:
                    return false;
            }

            using (SqlCommand cmd = new SqlCommand(sql, conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idEmpleado;
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        private static void InsertarCargo(
            SqlConnection conexion, SqlTransaction tx, int id, EmpleadoInfo e)
        {
            string sql;
            switch (e.Cargo)
            {
                case Cargos.Administrador:
                    sql = "INSERT INTO dbo.ADMINISTRADOR (id_empleado) VALUES (@Id);";
                    break;
                case Cargos.Cajero:
                    // El trigger copia el turno desde EMPLEADO (fuente de verdad).
                    sql = "INSERT INTO dbo.CAJERO (id_empleado, turno) VALUES (@Id, @Turno);";
                    break;
                case Cargos.Mesero:
                    sql = "INSERT INTO dbo.MESERO (id_empleado, [área_asignada]) VALUES (@Id, @Area);";
                    break;
                case Cargos.Cocinero:
                    sql = "INSERT INTO dbo.COCINERO (id_empleado, [certificación]) VALUES (@Id, @Certificacion);";
                    break;
                case Cargos.Repartidor:
                    sql = "INSERT INTO dbo.REPARTIDOR (id_empleado, zona_reparto, [vehículo]) VALUES (@Id, @Zona, @Vehiculo);";
                    break;
                default:
                    throw new ReglaNegocioException("Cargo no válido.");
            }

            using (SqlCommand cmd = new SqlCommand(sql, conexion, tx))
            {
                ParametrosCargo(cmd, id, e);
                cmd.ExecuteNonQuery();
            }
        }

        private static void ActualizarDatosCargo(
            SqlConnection conexion, SqlTransaction tx, EmpleadoInfo e)
        {
            string sql;
            switch (e.Cargo)
            {
                case Cargos.Mesero:
                    sql = "UPDATE dbo.MESERO SET [área_asignada] = @Area WHERE id_empleado = @Id;";
                    break;
                case Cargos.Cocinero:
                    sql = "UPDATE dbo.COCINERO SET [certificación] = @Certificacion WHERE id_empleado = @Id;";
                    break;
                case Cargos.Repartidor:
                    sql = "UPDATE dbo.REPARTIDOR SET zona_reparto = @Zona, [vehículo] = @Vehiculo WHERE id_empleado = @Id;";
                    break;
                default:
                    return; // Administrador y Cajero no tienen datos propios editables.
            }

            using (SqlCommand cmd = new SqlCommand(sql, conexion, tx))
            {
                ParametrosCargo(cmd, e.IdEmpleado, e);
                cmd.ExecuteNonQuery();
            }
        }

        private static void ParametrosEmpleado(SqlCommand cmd, EmpleadoInfo e)
        {
            cmd.Parameters.Add("@Dni", SqlDbType.Char, 8).Value = e.Dni;
            cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = e.Nombre;
            cmd.Parameters.Add("@Apellidos", SqlDbType.VarChar, 100).Value = e.Apellidos;
            cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 20).Value =
                string.IsNullOrEmpty(e.Telefono) ? (object)DBNull.Value : e.Telefono;
            cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = e.Estado;
            cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = e.IdSucursal;
            cmd.Parameters.Add("@Turno", SqlDbType.VarChar, 20).Value = e.Turno;
        }

        private static void ParametrosCargo(SqlCommand cmd, int id, EmpleadoInfo e)
        {
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            cmd.Parameters.Add("@Turno", SqlDbType.VarChar, 20).Value = e.Turno;
            cmd.Parameters.Add("@Area", SqlDbType.VarChar, 100).Value = (object)e.AreaAsignada ?? DBNull.Value;
            cmd.Parameters.Add("@Certificacion", SqlDbType.VarChar, 100).Value = (object)e.Certificacion ?? DBNull.Value;
            cmd.Parameters.Add("@Zona", SqlDbType.VarChar, 100).Value = (object)e.ZonaReparto ?? DBNull.Value;
            cmd.Parameters.Add("@Vehiculo", SqlDbType.VarChar, 100).Value = (object)e.Vehiculo ?? DBNull.Value;
        }

        // Ejecuta una operación en una transacción Serializable y
        // traduce los errores de SQL Server a mensajes claros.
        private static T Ejecutar<T>(Func<SqlConnection, SqlTransaction, T> operacion)
        {
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            {
                conexion.Open();
                using (SqlTransaction tx = conexion.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        T resultado = operacion(conexion, tx);
                        tx.Commit();
                        return resultado;
                    }
                    catch (SqlException ex)
                    {
                        Revertir(tx);
                        throw Traducir(ex);
                    }
                    catch
                    {
                        Revertir(tx);
                        throw;
                    }
                }
            }
        }

        private static void Revertir(SqlTransaction tx)
        {
            try { tx.Rollback(); }
            catch { /* Un trigger pudo haber revertido ya la transacción. */ }
        }

        internal static Exception Traducir(SqlException ex)
        {
            string m = ex.Message ?? "";

            if (ex.Number == 2627 || ex.Number == 2601)
            {
                if (m.Contains("UQ_EMPLEADO_DNI"))
                    return new ReglaNegocioException("Ya existe un empleado con ese DNI.");
                if (m.Contains("UX_EMPLEADO_USUARIO"))
                    return new ReglaNegocioException("Ese nombre de usuario ya está en uso.");
                return new ReglaNegocioException("El registro ya existe.");
            }

            if (ex.Number == 547)
            {
                return new ReglaNegocioException(
                    "SQL Server rechazó la operación para proteger datos relacionados " +
                    "o una regla de validación.\n\nDetalle: " + m);
            }

            if (ex.Number == 50000 && m.StartsWith("RN-"))
                return new ReglaNegocioException(m);

            return ex;
        }
    }
}
