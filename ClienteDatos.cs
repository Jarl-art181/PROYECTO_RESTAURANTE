using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PROYECTO_RESTAURANTE
{
    internal sealed class ClienteInfo
    {
        public int IdCliente;
        public string Documento;
        public string Nombre;
        public string Apellidos;
        public string Telefono;
        public string Correo;
        public string Direccion;
        public string Distrito;
        public DateTime? FechaNacimiento;
        public string Comentario;
        public string Estado = "activo";      // activo / inactivo

        // Solo lectura (calculados en SQL).
        public DateTime FechaRegistro;
        public int PedidosTotales;
        public int PedidosRecientes;         // últimos 90 días, no cancelados
        public int SaldoPuntos;
        public DateTime? UltimoPedido;

        public string NombreCompleto { get { return (Nombre + " " + Apellidos).Trim(); } }
        public bool Activo { get { return Estado == "activo"; } }
        public bool Frecuente { get { return PedidosRecientes >= ClienteDatos.PedidosParaFrecuente; } }
        public string TipoDocumento { get { return ClienteDatos.TipoDocumento(Documento); } }
    }

    internal sealed class PedidoClienteInfo
    {
        public int IdPedido;
        public DateTime FechaHora;
        public string Canal;
        public string Estado;
        public decimal Total;
    }

    // ==========================================================
    // GESTIÓN DE CLIENTES (FASE C)
    // ----------------------------------------------------------
    // Reglas aprobadas:
    //  * "Eliminar" desactiva (estado = 'inactivo'); nunca se
    //    borran clientes: pedidos, canjes y puntos se conservan.
    //  * Cliente frecuente: 3 o más pedidos no cancelados en los
    //    últimos 90 días.
    //  * "Nombre completo": las 2 últimas palabras son los
    //    apellidos (con 2 palabras, una y una). Con coma se
    //    fuerza: "Apellidos, Nombres".
    //  * El tipo de documento se deduce del número (sin columna):
    //    8 dígitos = DNI, 9 = CE, 11 = RUC, con letras = PAS.
    //  * Escrituras en transacción Serializable, revalidando en
    //    SQL que quien opera es un Administrador activo.
    // ==========================================================
    internal static class ClienteDatos
    {
        public const int PedidosParaFrecuente = 3;
        public const int DiasFrecuencia = 90;

        private static readonly string SqlListar = @"
            SELECT C.id_cliente, C.documento, C.nombre, C.apellidos,
                   C.[teléfono] AS telefono, C.[dirección] AS direccion,
                   C.correo, C.distrito, C.fecha_nacimiento, C.comentario,
                   C.estado, C.fecha_registro,
                   ISNULL(S.saldo_puntos, 0) AS saldo_puntos,
                   (SELECT COUNT(*) FROM dbo.PEDIDO P
                     WHERE P.id_cliente = C.id_cliente AND P.estado <> 'cancelado') AS pedidos_totales,
                   (SELECT COUNT(*) FROM dbo.PEDIDO P
                     WHERE P.id_cliente = C.id_cliente AND P.estado <> 'cancelado'
                       AND P.fecha_hora >= DATEADD(DAY, -" + DiasFrecuencia + @", SYSDATETIME())) AS pedidos_recientes,
                   (SELECT MAX(P.fecha_hora) FROM dbo.PEDIDO P
                     WHERE P.id_cliente = C.id_cliente AND P.estado <> 'cancelado') AS ultimo_pedido
            FROM dbo.CLIENTE C
            LEFT JOIN dbo.V_CLIENTE_SALDO S ON S.id_cliente = C.id_cliente";

        // ------------------------------------------------------
        // CONSULTAS
        // ------------------------------------------------------
        public static List<ClienteInfo> Listar()
        {
            List<ClienteInfo> lista = new List<ClienteInfo>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(SqlListar + " ORDER BY C.apellidos, C.nombre;", conexion))
            {
                cmd.CommandTimeout = 20;
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        lista.Add(Leer(r));
                }
            }
            return lista;
        }

        public static ClienteInfo Obtener(int idCliente)
        {
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(SqlListar + " WHERE C.id_cliente = @Id;", conexion))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idCliente;
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                    return r.Read() ? Leer(r) : null;
            }
        }

        // Historial con el procedimiento existente usp_PedidosPorCliente.
        public static List<PedidoClienteInfo> Pedidos(int idCliente)
        {
            List<PedidoClienteInfo> lista = new List<PedidoClienteInfo>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand("dbo.usp_PedidosPorCliente", conexion))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 20;
                cmd.Parameters.Add("@id_cliente", SqlDbType.Int).Value = idCliente;
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new PedidoClienteInfo
                        {
                            IdPedido = Convert.ToInt32(r["id_pedido"]),
                            FechaHora = Convert.ToDateTime(r["fecha_hora"]),
                            Canal = Convert.ToString(r["canal"]),
                            Estado = Convert.ToString(r["estado"]),
                            Total = r["total"] == DBNull.Value ? 0m : Convert.ToDecimal(r["total"])
                        });
                    }
                }
            }
            return lista;
        }

        private static ClienteInfo Leer(SqlDataReader r)
        {
            Func<string, string> texto = c => r[c] == DBNull.Value ? "" : Convert.ToString(r[c]);
            return new ClienteInfo
            {
                IdCliente = Convert.ToInt32(r["id_cliente"]),
                Documento = texto("documento").Trim(),
                Nombre = texto("nombre"),
                Apellidos = texto("apellidos"),
                Telefono = texto("telefono"),
                Direccion = texto("direccion"),
                Correo = texto("correo"),
                Distrito = texto("distrito"),
                Comentario = texto("comentario"),
                FechaNacimiento = r["fecha_nacimiento"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["fecha_nacimiento"]),
                Estado = texto("estado").Trim().ToLowerInvariant(),
                FechaRegistro = Convert.ToDateTime(r["fecha_registro"]),
                SaldoPuntos = Convert.ToInt32(r["saldo_puntos"]),
                PedidosTotales = Convert.ToInt32(r["pedidos_totales"]),
                PedidosRecientes = Convert.ToInt32(r["pedidos_recientes"]),
                UltimoPedido = r["ultimo_pedido"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ultimo_pedido"])
            };
        }

        // ------------------------------------------------------
        // REGLAS DE FORMATO
        // ------------------------------------------------------
        public static string TipoDocumento(string documento)
        {
            documento = (documento ?? "").Trim();
            if (documento.Length == 0) return "";
            if (documento.All(char.IsDigit))
            {
                if (documento.Length == 8) return "DNI";
                if (documento.Length == 9) return "CE";
                if (documento.Length == 11) return "RUC";
                return "?";
            }
            return documento.All(char.IsLetterOrDigit) && documento.Length >= 6 && documento.Length <= 12
                ? "PAS" : "?";
        }

        // "María Fernanda Torres Castro" -> ("María Fernanda", "Torres Castro").
        // "Torres Castro, María Fernanda" -> igual (la coma manda).
        public static void SepararNombre(string completo, out string nombre, out string apellidos)
        {
            completo = Limpiar(completo);
            int coma = completo.IndexOf(',');
            if (coma >= 0)
            {
                apellidos = Limpiar(completo.Substring(0, coma));
                nombre = Limpiar(completo.Substring(coma + 1));
                return;
            }

            string[] partes = completo.Split(' ');
            int nApellidos = partes.Length >= 3 ? 2 : (partes.Length == 2 ? 1 : 0);
            nombre = string.Join(" ", partes.Take(partes.Length - nApellidos));
            apellidos = string.Join(" ", partes.Skip(partes.Length - nApellidos));
        }

        private static readonly Regex Correo = new Regex(
            @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$", RegexOptions.CultureInvariant);

        public static void Validar(ClienteInfo c)
        {
            c.Documento = (c.Documento ?? "").Trim().ToUpperInvariant();
            c.Nombre = Limpiar(c.Nombre);
            c.Apellidos = Limpiar(c.Apellidos);
            c.Telefono = (c.Telefono ?? "").Trim();

            string tipo = TipoDocumento(c.Documento);
            if (tipo == "")
                throw new ReglaNegocioException("Ingrese el número de documento.");
            if (tipo == "RUC" && !(c.Documento.StartsWith("10") || c.Documento.StartsWith("15") ||
                                   c.Documento.StartsWith("17") || c.Documento.StartsWith("20")))
                throw new ReglaNegocioException("El RUC debe empezar con 10, 15, 17 o 20.");
            if (tipo == "?")
            {
                throw new ReglaNegocioException(
                    "Documento no válido: DNI de 8 dígitos, carné de extranjería de 9, " +
                    "RUC de 11, o pasaporte de 6 a 12 letras y números.");
            }

            if (c.Nombre.Length == 0 || c.Apellidos.Length == 0)
                throw new ReglaNegocioException(
                    "Escriba nombres y apellidos (por ejemplo: María Torres Castro).");
            ValidarNombre(c.Nombre, "Los nombres");
            ValidarNombre(c.Apellidos, "Los apellidos");

            if (c.Telefono.Length > 0 &&
                (!c.Telefono.All(char.IsDigit) || (c.Telefono.Length != 9 && c.Telefono.Length != 7)))
                throw new ReglaNegocioException("El teléfono debe tener 9 dígitos (celular) o 7 (fijo), o dejarse vacío.");

            c.Correo = Opcional(c.Correo, 100, "El correo");
            if (c.Correo != null && !Correo.IsMatch(c.Correo))
                throw new ReglaNegocioException("El correo no tiene un formato válido (ejemplo: nombre@correo.com).");

            c.Direccion = Opcional(c.Direccion, 255, "La dirección");
            c.Distrito = Opcional(c.Distrito, 100, "El distrito");
            c.Comentario = Opcional(c.Comentario, 255, "El comentario");

            if (c.FechaNacimiento.HasValue)
            {
                DateTime f = c.FechaNacimiento.Value.Date;
                if (f > DateTime.Today)
                    throw new ReglaNegocioException("La fecha de nacimiento no puede ser futura.");
                if (f < DateTime.Today.AddYears(-120))
                    throw new ReglaNegocioException("La fecha de nacimiento no es válida.");
            }

            if (c.Estado != "activo" && c.Estado != "inactivo")
                c.Estado = "activo";
        }

        // dd/mm/aaaa; vacío = sin fecha.
        public static DateTime? LeerFecha(string texto)
        {
            texto = (texto ?? "").Trim();
            if (texto.Length == 0)
                return null;

            DateTime fecha;
            if (DateTime.TryParseExact(texto, new[] { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                return fecha.Date;

            throw new ReglaNegocioException("Escriba la fecha de nacimiento como dd/mm/aaaa (por ejemplo 15/06/1998).");
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
            foreach (char ch in valor)
            {
                if (!char.IsLetter(ch) && ch != ' ' && ch != '\'' && ch != '-' && ch != '.')
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
        // ESCRITURAS
        // ------------------------------------------------------
        public static int Registrar(ClienteInfo c, int idOperador)
        {
            Validar(c);
            return EmpleadoDatos.Ejecutar((conexion, tx) => Registrar(conexion, tx, c, idOperador));
        }

        internal static int Registrar(SqlConnection conexion, SqlTransaction tx, ClienteInfo c, int idOperador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idOperador);
            VerificarDocumentoLibre(conexion, tx, c.Documento, null);

            int id;
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO dbo.CLIENTE
                    (documento, nombre, apellidos, [teléfono], [dirección], fecha_registro,
                     correo, fecha_nacimiento, distrito, comentario, estado)
                VALUES
                    (@Documento, @Nombre, @Apellidos, @Telefono, @Direccion, CAST(SYSDATETIME() AS DATE),
                     @Correo, @FechaNac, @Distrito, @Comentario, 'activo');
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conexion, tx))
            {
                Parametros(cmd, c);
                id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            Registro.Info("Clientes", "Empleado " + idOperador + " registró el cliente " + id + ".");
            return id;
        }

        public static void Actualizar(ClienteInfo c, int idOperador)
        {
            Validar(c);
            EmpleadoDatos.Ejecutar((conexion, tx) => { Actualizar(conexion, tx, c, idOperador); return 0; });
        }

        internal static void Actualizar(SqlConnection conexion, SqlTransaction tx, ClienteInfo c, int idOperador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idOperador);
            Bloquear(conexion, tx, c.IdCliente);
            VerificarDocumentoLibre(conexion, tx, c.Documento, c.IdCliente);

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.CLIENTE
                   SET documento = @Documento, nombre = @Nombre, apellidos = @Apellidos,
                       [teléfono] = @Telefono, [dirección] = @Direccion,
                       correo = @Correo, fecha_nacimiento = @FechaNac,
                       distrito = @Distrito, comentario = @Comentario
                 WHERE id_cliente = @Id;", conexion, tx))
            {
                Parametros(cmd, c);
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = c.IdCliente;
                if (cmd.ExecuteNonQuery() != 1)
                    throw new ReglaNegocioException("El cliente ya no existe.");
            }

            Registro.Info("Clientes", "Empleado " + idOperador + " actualizó el cliente " + c.IdCliente + ".");
        }

        // Desactivar ("Eliminar") o reactivar. Nunca borra.
        public static void CambiarEstado(int idCliente, bool activo, int idOperador)
        {
            EmpleadoDatos.Ejecutar((conexion, tx) =>
            {
                CambiarEstado(conexion, tx, idCliente, activo, idOperador);
                return 0;
            });
        }

        internal static void CambiarEstado(SqlConnection conexion, SqlTransaction tx, int idCliente, bool activo, int idOperador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idOperador);
            Bloquear(conexion, tx, idCliente);

            using (SqlCommand cmd = new SqlCommand(
                "UPDATE dbo.CLIENTE SET estado = @Estado WHERE id_cliente = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = activo ? "activo" : "inactivo";
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idCliente;
                cmd.ExecuteNonQuery();
            }

            Registro.Info("Clientes", "Empleado " + idOperador + (activo ? " reactivó" : " desactivó") +
                " el cliente " + idCliente + ".");
        }

        private static void Bloquear(SqlConnection conexion, SqlTransaction tx, int idCliente)
        {
            using (SqlCommand cmd = new SqlCommand(
                "SELECT id_cliente FROM dbo.CLIENTE WITH (UPDLOCK, HOLDLOCK) WHERE id_cliente = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idCliente;
                if (cmd.ExecuteScalar() == null)
                    throw new ReglaNegocioException("El cliente ya no existe.");
            }
        }

        private static void VerificarDocumentoLibre(SqlConnection conexion, SqlTransaction tx, string documento, int? idActual)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) nombre + ' ' + apellidos + CASE WHEN estado = 'inactivo' THEN ' (inactivo)' ELSE '' END
                FROM dbo.CLIENTE WITH (UPDLOCK, HOLDLOCK)
                WHERE documento = @Documento AND (@Id IS NULL OR id_cliente <> @Id);", conexion, tx))
            {
                cmd.Parameters.Add("@Documento", SqlDbType.VarChar, 20).Value = documento;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idActual.HasValue ? (object)idActual.Value : DBNull.Value;
                object existente = cmd.ExecuteScalar();
                if (existente != null)
                    throw new ReglaNegocioException("Ya existe un cliente con el documento " + documento + ": " + existente + ".");
            }
        }

        private static void Parametros(SqlCommand cmd, ClienteInfo c)
        {
            Func<string, object> v = s => string.IsNullOrEmpty(s) ? (object)DBNull.Value : s;
            cmd.Parameters.Add("@Documento", SqlDbType.VarChar, 20).Value = c.Documento;
            cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = c.Nombre;
            cmd.Parameters.Add("@Apellidos", SqlDbType.VarChar, 100).Value = c.Apellidos;
            cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 20).Value = v(c.Telefono);
            cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 255).Value = v(c.Direccion);
            cmd.Parameters.Add("@Correo", SqlDbType.VarChar, 100).Value = v(c.Correo);
            cmd.Parameters.Add("@FechaNac", SqlDbType.Date).Value =
                c.FechaNacimiento.HasValue ? (object)c.FechaNacimiento.Value : DBNull.Value;
            cmd.Parameters.Add("@Distrito", SqlDbType.VarChar, 100).Value = v(c.Distrito);
            cmd.Parameters.Add("@Comentario", SqlDbType.VarChar, 255).Value = v(c.Comentario);
        }
    }
}
