using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace PROYECTO_RESTAURANTE
{
    internal sealed class InsumoInfo
    {
        public int IdInsumo;
        public string Nombre;
        public string Unidad;
        public decimal StockActual;
        public decimal StockMinimo;
        public int IdSucursal;
        public string Estado = "activo";
        public string Categoria;
        public string Descripcion;

        // Solo lectura.
        public string UltimoProveedor;
        public List<string> Platos = new List<string>();   // platos activos que lo usan
        public HashSet<string> Grupos = new HashSet<string>(); // platos / bebidas / agregados
        public int Movimientos;                            // compras + mermas

        public bool Activo { get { return Estado == "activo"; } }
        public bool SinStock { get { return StockActual <= 0; } }
        public bool BajoMinimo { get { return StockActual <= StockMinimo; } }

        public string EstadoStock
        {
            get
            {
                if (!Activo) return "Inactivo";
                if (SinStock) return "Sin stock";
                if (BajoMinimo) return "Stock bajo";
                return "Stock adecuado";
            }
        }

        public string StockTexto { get { return InventarioDatos.Cantidad(StockActual) + " " + Unidad; } }
    }

    internal sealed class ProveedorInfo
    {
        public int IdProveedor;
        public string Nombre;
        public string Ruc;
        public string Telefono;
        public string Direccion;
        public override string ToString() { return Nombre + " (" + Ruc + ")"; }
    }

    internal sealed class LineaCompra
    {
        public int IdInsumo;
        public decimal Cantidad;
        public decimal CostoUnitario;
    }

    internal sealed class MovimientoInfo
    {
        public string Tipo;          // ENTRADA / SALIDA
        public DateTime Fecha;
        public int IdInsumo;
        public string Insumo;
        public decimal Cantidad;
        public string Unidad;
        public string Detalle;       // proveedor y documento, o motivo
        public string Responsable;
        public decimal? Importe;     // solo compras
        public int IdOrigen;         // id_compra o id_merma
    }

    // ==========================================================
    // INVENTARIO DE INSUMOS (FASE E)
    // ----------------------------------------------------------
    //  * Insumos de cocina (dbo.INSUMO) de la sucursal actual;
    //    los platos preparados (dbo.PLATO) son otra cosa.
    //  * El stock NUNCA se modifica directamente: lo mantienen los
    //    triggers existentes.
    //      ENTRADA = COMPRA + DETALLE_COMPRA (TR_DETALLE_COMPRA_STOCK)
    //      SALIDA  = MERMA, solo pérdidas      (TR_MERMA_STOCK)
    //    El consumo normal de cocina NO se registra como merma.
    //  * Todas las escrituras: transacción Serializable y
    //    Administrador activo validado en SQL.
    //  * "Eliminar" desactiva (estado = 'inactivo').
    // ==========================================================
    internal static class InventarioDatos
    {
        // Motivos de merma: solo pérdidas reales.
        public static readonly string[] MotivosMerma =
        {
            "Deterioro", "Vencimiento", "Pérdida de cadena de frío", "Rotura o derrame", "Otra pérdida"
        };

        public static readonly string[] UnidadesSugeridas = { "kg", "g", "L", "ml", "unidad", "paquete", "caja", "docena" };

        public static readonly string[] CategoriasSugeridas =
        {
            "Pescados y mariscos", "Carnes y aves", "Verduras", "Frutas", "Abarrotes",
            "Salsas y condimentos", "Bebidas", "Descartables", "Otros"
        };

        public const decimal CantidadMaxima = 9999999.999m;

        public static string Cantidad(decimal valor)
        {
            return valor.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------
        // CONSULTAS
        // ------------------------------------------------------
        // true si la base ya tiene las columnas de la Fase E.
        public static bool EsquemaListo()
        {
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(
                "SELECT CASE WHEN COL_LENGTH('dbo.INSUMO','estado') IS NOT NULL AND COL_LENGTH('dbo.INSUMO','categoria') IS NOT NULL " +
                "AND COL_LENGTH('dbo.INSUMO','descripcion') IS NOT NULL THEN 1 ELSE 0 END;", conexion))
            {
                conexion.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
            }
        }

        public static List<InsumoInfo> Listar()
        {
            if (!EsquemaListo())
                throw new ReglaNegocioException("La base de datos aún no tiene la actualización de Inventario " +
                    "(SQL\\FASEE_01_inventario.sql). Consulte con el Administrador.");

            List<InsumoInfo> lista = new List<InsumoInfo>();
            Dictionary<int, InsumoInfo> porId = new Dictionary<int, InsumoInfo>();

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            {
                conexion.Open();
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT I.id_insumo, I.nombre, I.unidad_medida, I.stock_actual, I.stock_minimo,
                           I.id_sucursal, I.estado, I.categoria, I.descripcion,
                           UP.proveedor,
                           (SELECT COUNT(*) FROM dbo.DETALLE_COMPRA D WHERE D.id_insumo = I.id_insumo)
                         + (SELECT COUNT(*) FROM dbo.MERMA M WHERE M.id_insumo = I.id_insumo) AS movimientos
                    FROM dbo.INSUMO I
                    OUTER APPLY (
                        SELECT TOP (1) P.nombre AS proveedor
                        FROM dbo.DETALLE_COMPRA D
                        JOIN dbo.COMPRA C ON C.id_compra = D.id_compra
                        JOIN dbo.PROVEEDOR P ON P.id_proveedor = C.id_proveedor
                        WHERE D.id_insumo = I.id_insumo
                        ORDER BY C.fecha DESC, C.id_compra DESC) UP
                    WHERE I.id_sucursal = @Sucursal
                    ORDER BY I.nombre;", conexion))
                {
                    cmd.CommandTimeout = 20;
                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            InsumoInfo i = Leer(r);
                            lista.Add(i);
                            porId[i.IdInsumo] = i;
                        }
                    }
                }

                // Platos activos que usan cada insumo (RECETA_PLATO).
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT R.id_insumo, P.nombre, C.nombre AS categoria
                    FROM dbo.RECETA_PLATO R
                    JOIN dbo.PLATO P ON P.id_plato = R.id_plato
                    JOIN dbo.CATEGORIA C ON C.id_categoria = P.id_categoria
                    WHERE P.estado = 'activo'
                    ORDER BY P.nombre;", conexion))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        InsumoInfo i;
                        if (porId.TryGetValue(r.GetInt32(0), out i))
                        {
                            i.Platos.Add(r.GetString(1));
                            i.Grupos.Add(MenuDatos.Grupo(r.GetString(2)));
                        }
                    }
                }
            }

            return lista;
        }

        public static InsumoInfo Obtener(int idInsumo)
        {
            return Listar().FirstOrDefault(i => i.IdInsumo == idInsumo);
        }

        private static InsumoInfo Leer(SqlDataReader r)
        {
            return new InsumoInfo
            {
                IdInsumo = Convert.ToInt32(r["id_insumo"]),
                Nombre = Convert.ToString(r["nombre"]),
                Unidad = Convert.ToString(r["unidad_medida"]),
                StockActual = Convert.ToDecimal(r["stock_actual"]),
                StockMinimo = Convert.ToDecimal(r["stock_minimo"]),
                IdSucursal = Convert.ToInt32(r["id_sucursal"]),
                Estado = Convert.ToString(r["estado"]).Trim().ToLowerInvariant(),
                Categoria = r["categoria"] == DBNull.Value ? "" : Convert.ToString(r["categoria"]),
                Descripcion = r["descripcion"] == DBNull.Value ? "" : Convert.ToString(r["descripcion"]),
                UltimoProveedor = r["proveedor"] == DBNull.Value ? null : Convert.ToString(r["proveedor"]),
                Movimientos = Convert.ToInt32(r["movimientos"])
            };
        }

        // Receta de los platos que usan un insumo (para consulta).
        public static List<string[]> PlatosQueUsan(int idInsumo)
        {
            List<string[]> lista = new List<string[]>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT P.nombre, R.cantidad, I.unidad_medida, P.estado
                FROM dbo.RECETA_PLATO R
                JOIN dbo.PLATO P ON P.id_plato = R.id_plato
                JOIN dbo.INSUMO I ON I.id_insumo = R.id_insumo
                WHERE R.id_insumo = @Id
                ORDER BY P.nombre;", conexion))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idInsumo;
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new[]
                        {
                            r.GetString(0), Cantidad(r.GetDecimal(1)) + " " + r.GetString(2) + " por plato",
                            r.GetString(3) == "activo" ? "Activo" : "Inactivo"
                        });
                    }
                }
            }
            return lista;
        }

        // id_plato -> insumos activos en o bajo su mínimo (para Menú).
        public static Dictionary<int, List<string>> PlatosConInsumoBajo()
        {
            Dictionary<int, List<string>> r = new Dictionary<int, List<string>>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT R.id_plato, I.nombre
                FROM dbo.RECETA_PLATO R
                JOIN dbo.INSUMO I ON I.id_insumo = R.id_insumo
                WHERE I.id_sucursal = @Sucursal
                  AND I.estado = 'activo'
                  AND I.stock_actual <= I.stock_minimo
                ORDER BY I.nombre;", conexion))
            {
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                conexion.Open();
                using (SqlDataReader d = cmd.ExecuteReader())
                {
                    while (d.Read())
                    {
                        List<string> l;
                        if (!r.TryGetValue(d.GetInt32(0), out l))
                            r[d.GetInt32(0)] = l = new List<string>();
                        l.Add(d.GetString(1));
                    }
                }
            }
            return r;
        }

        public static List<ProveedorInfo> ListarProveedores()
        {
            List<ProveedorInfo> lista = new List<ProveedorInfo>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT id_proveedor, nombre, ruc, [teléfono] AS telefono, [dirección] AS direccion
                FROM dbo.PROVEEDOR ORDER BY nombre;", conexion))
            {
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new ProveedorInfo
                        {
                            IdProveedor = Convert.ToInt32(r["id_proveedor"]),
                            Nombre = Convert.ToString(r["nombre"]),
                            Ruc = Convert.ToString(r["ruc"]).Trim(),
                            Telefono = r["telefono"] == DBNull.Value ? "" : Convert.ToString(r["telefono"]),
                            Direccion = r["direccion"] == DBNull.Value ? "" : Convert.ToString(r["direccion"])
                        });
                    }
                }
            }
            return lista;
        }

        // Historial: compras (entradas) y mermas (salidas), lo más reciente primero.
        public static List<MovimientoInfo> Historial(int? idInsumo, DateTime? desde, DateTime? hasta, int maximo)
        {
            List<MovimientoInfo> lista = new List<MovimientoInfo>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (@Max) * FROM (
                    SELECT 'ENTRADA' AS tipo, C.fecha, D.id_insumo, I.nombre AS insumo, D.cantidad,
                           I.unidad_medida AS unidad,
                           'Proveedor: ' + P.nombre + ' · Doc. ' + C.numero_documento AS detalle,
                           E.nombre + ' ' + E.apellidos AS responsable,
                           D.cantidad * D.costo_unitario AS importe, C.id_compra AS id_origen
                    FROM dbo.DETALLE_COMPRA D
                    JOIN dbo.COMPRA C ON C.id_compra = D.id_compra
                    JOIN dbo.PROVEEDOR P ON P.id_proveedor = C.id_proveedor
                    JOIN dbo.INSUMO I ON I.id_insumo = D.id_insumo
                    JOIN dbo.EMPLEADO E ON E.id_empleado = C.id_administrador
                    WHERE I.id_sucursal = @Sucursal
                    UNION ALL
                    SELECT 'SALIDA', M.fecha, M.id_insumo, I.nombre, M.cantidad, I.unidad_medida,
                           'Merma: ' + M.motivo, E.nombre + ' ' + E.apellidos, NULL, M.id_merma
                    FROM dbo.MERMA M
                    JOIN dbo.INSUMO I ON I.id_insumo = M.id_insumo
                    JOIN dbo.EMPLEADO E ON E.id_empleado = M.id_empleado
                    WHERE I.id_sucursal = @Sucursal
                ) X
                WHERE (@Insumo IS NULL OR X.id_insumo = @Insumo)
                  AND (@Desde IS NULL OR X.fecha >= @Desde)
                  AND (@Hasta IS NULL OR X.fecha <= @Hasta)
                ORDER BY X.fecha DESC, X.id_origen DESC, X.tipo;", conexion))
            {
                cmd.CommandTimeout = 20;
                cmd.Parameters.Add("@Max", SqlDbType.Int).Value = maximo;
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                cmd.Parameters.Add("@Insumo", SqlDbType.Int).Value = idInsumo.HasValue ? (object)idInsumo.Value : DBNull.Value;
                cmd.Parameters.Add("@Desde", SqlDbType.Date).Value = desde.HasValue ? (object)desde.Value.Date : DBNull.Value;
                cmd.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta.HasValue ? (object)hasta.Value.Date : DBNull.Value;
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new MovimientoInfo
                        {
                            Tipo = r.GetString(0),
                            Fecha = r.GetDateTime(1),
                            IdInsumo = r.GetInt32(2),
                            Insumo = r.GetString(3),
                            Cantidad = r.GetDecimal(4),
                            Unidad = r.GetString(5),
                            Detalle = r.GetString(6),
                            Responsable = r.GetString(7),
                            Importe = r.IsDBNull(8) ? (decimal?)null : r.GetDecimal(8),
                            IdOrigen = r.GetInt32(9)
                        });
                    }
                }
            }
            return lista;
        }

        // ------------------------------------------------------
        // VALIDACIÓN Y LECTURA DE NÚMEROS
        // ------------------------------------------------------
        public static decimal LeerCantidad(string texto, string campo, bool permitirCero)
        {
            texto = (texto ?? "").Trim().Replace(',', '.');
            decimal valor;
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor))
                throw new ReglaNegocioException(campo + ": escriba un número, por ejemplo 2.5.");
            if (valor < 0 || (!permitirCero && valor == 0))
                throw new ReglaNegocioException(campo + " debe ser mayor que 0.");
            if (valor > CantidadMaxima)
                throw new ReglaNegocioException(campo + " es demasiado grande.");
            if (decimal.Round(valor, 3) != valor)
                throw new ReglaNegocioException(campo + " admite como máximo 3 decimales.");
            return valor;
        }

        public static decimal LeerImporte(string texto, string campo)
        {
            texto = (texto ?? "").Trim().Replace("S/", "").Replace(',', '.');
            decimal valor;
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor) || valor < 0)
                throw new ReglaNegocioException(campo + ": escriba un importe válido, por ejemplo 22.50.");
            if (decimal.Round(valor, 2) != valor)
                throw new ReglaNegocioException(campo + " admite como máximo 2 decimales.");
            if (valor > 99999999.99m)
                throw new ReglaNegocioException(campo + " es demasiado grande.");
            return valor;
        }

        private static string Limpiar(string texto)
        {
            texto = (texto ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            while (texto.Contains("  "))
                texto = texto.Replace("  ", " ");
            return texto;
        }

        public static void Validar(InsumoInfo i)
        {
            i.Nombre = Limpiar(i.Nombre);
            i.Unidad = Limpiar(i.Unidad);
            i.Categoria = Limpiar(i.Categoria);
            i.Descripcion = Limpiar(i.Descripcion);

            if (i.Nombre.Length < 2 || i.Nombre.Length > 100)
                throw new ReglaNegocioException("El nombre del insumo debe tener entre 2 y 100 caracteres.");
            if (i.Unidad.Length == 0 || i.Unidad.Length > 20)
                throw new ReglaNegocioException("Indique la unidad de medida (kg, L, unidad...).");
            if (i.Categoria.Length > 50)
                throw new ReglaNegocioException("La categoría no puede superar 50 caracteres.");
            if (i.Descripcion.Length > 255)
                throw new ReglaNegocioException("La descripción no puede superar 255 caracteres.");
            if (i.StockMinimo < 0 || i.StockMinimo > CantidadMaxima || decimal.Round(i.StockMinimo, 3) != i.StockMinimo)
                throw new ReglaNegocioException("El stock mínimo debe ser 0 o mayor, con hasta 3 decimales.");
        }

        public static void Validar(ProveedorInfo p)
        {
            p.Nombre = Limpiar(p.Nombre);
            p.Ruc = (p.Ruc ?? "").Trim();
            p.Telefono = Limpiar(p.Telefono);
            p.Direccion = Limpiar(p.Direccion);

            if (p.Nombre.Length < 2 || p.Nombre.Length > 100)
                throw new ReglaNegocioException("La razón social debe tener entre 2 y 100 caracteres.");
            if (p.Ruc.Length != 11 || !p.Ruc.All(char.IsDigit))
                throw new ReglaNegocioException("El RUC debe tener 11 dígitos.");
            if (!(p.Ruc.StartsWith("10") || p.Ruc.StartsWith("15") || p.Ruc.StartsWith("17") || p.Ruc.StartsWith("20")))
                throw new ReglaNegocioException("El RUC debe empezar con 10, 15, 17 o 20.");
            if (p.Telefono.Length > 20 || p.Telefono.Any(c => !char.IsDigit(c) && c != '-' && c != ' ' && c != '+'))
                throw new ReglaNegocioException("El teléfono solo puede tener números, espacios o guiones (máximo 20).");
            if (p.Direccion.Length > 255)
                throw new ReglaNegocioException("La dirección no puede superar 255 caracteres.");
        }

        // ------------------------------------------------------
        // INSUMOS
        // ------------------------------------------------------
        public static int RegistrarInsumo(InsumoInfo i, int idOperador)
        {
            Validar(i);
            return Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);
                VerificarNombreLibre(cx, tx, i.Nombre, null);

                int id;
                // Nace con stock 0: el stock solo entra por compras.
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO dbo.INSUMO (nombre, unidad_medida, stock_actual, stock_minimo, id_sucursal, estado, categoria, descripcion)
                    VALUES (@Nombre, @Unidad, 0, @Minimo, @Sucursal, 'activo', @Categoria, @Descripcion);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", cx, tx))
                {
                    ParametrosInsumo(cmd, i);
                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                Registro.Info("Inventario", "Empleado " + idOperador + " registró el insumo " + id + " (" + i.Nombre + ").");
                return id;
            });
        }

        public static void ActualizarInsumo(InsumoInfo i, int idOperador)
        {
            Validar(i);
            Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);
                string unidadActual = null;
                int movimientos = 0;
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT I.unidad_medida,
                           (SELECT COUNT(*) FROM dbo.DETALLE_COMPRA WHERE id_insumo = I.id_insumo)
                         + (SELECT COUNT(*) FROM dbo.MERMA WHERE id_insumo = I.id_insumo)
                         + (SELECT COUNT(*) FROM dbo.RECETA_PLATO WHERE id_insumo = I.id_insumo)
                    FROM dbo.INSUMO I WITH (UPDLOCK, HOLDLOCK)
                    WHERE I.id_insumo = @Id AND I.id_sucursal = @Sucursal;", cx, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = i.IdInsumo;
                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            throw new ReglaNegocioException("El insumo ya no existe.");
                        unidadActual = r.GetString(0);
                        movimientos = r.GetInt32(1);
                    }
                }

                // La unidad no cambia si ya hay compras, mermas o recetas:
                // alteraría el significado de las cantidades registradas.
                if (movimientos > 0 && !string.Equals(unidadActual, i.Unidad, StringComparison.OrdinalIgnoreCase))
                    throw new ReglaNegocioException("No se puede cambiar la unidad (" + unidadActual +
                        ") porque el insumo ya tiene compras, mermas o recetas registradas.");

                VerificarNombreLibre(cx, tx, i.Nombre, i.IdInsumo);

                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE dbo.INSUMO
                       SET nombre = @Nombre, unidad_medida = @Unidad, stock_minimo = @Minimo,
                           categoria = @Categoria, descripcion = @Descripcion
                     WHERE id_insumo = @Id;", cx, tx))
                {
                    ParametrosInsumo(cmd, i);
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = i.IdInsumo;
                    cmd.ExecuteNonQuery();
                }
                Registro.Info("Inventario", "Empleado " + idOperador + " actualizó el insumo " + i.IdInsumo + ".");
                return 0;
            });
        }

        public static void CambiarEstado(int idInsumo, bool activo, int idOperador)
        {
            Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);
                using (SqlCommand cmd = new SqlCommand(@"
                    UPDATE dbo.INSUMO SET estado = @Estado
                    WHERE id_insumo = @Id AND id_sucursal = @Sucursal;", cx, tx))
                {
                    cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = activo ? "activo" : "inactivo";
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idInsumo;
                    cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                    if (cmd.ExecuteNonQuery() != 1)
                        throw new ReglaNegocioException("El insumo ya no existe.");
                }
                Registro.Info("Inventario", "Empleado " + idOperador + (activo ? " reactivó" : " desactivó") + " el insumo " + idInsumo + ".");
                return 0;
            });
        }

        private static void VerificarNombreLibre(SqlConnection cx, SqlTransaction tx, string nombre, int? idActual)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) CASE WHEN estado = 'inactivo' THEN ' (inactivo)' ELSE '' END
                FROM dbo.INSUMO WITH (UPDLOCK, HOLDLOCK)
                WHERE nombre = @Nombre AND id_sucursal = @Sucursal AND (@Id IS NULL OR id_insumo <> @Id);", cx, tx))
            {
                cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = nombre;
                cmd.Parameters.Add("@Sucursal", SqlDbType.Int).Value = BaseDatos.IdSucursalActual;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idActual.HasValue ? (object)idActual.Value : DBNull.Value;
                object existe = cmd.ExecuteScalar();
                if (existe != null)
                    throw new ReglaNegocioException("Ya existe el insumo \"" + nombre + "\"" + existe + " en esta sucursal.");
            }
        }

        private static void ParametrosInsumo(SqlCommand cmd, InsumoInfo i)
        {
            cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = i.Nombre;
            cmd.Parameters.Add("@Unidad", SqlDbType.VarChar, 20).Value = i.Unidad;
            SqlParameter min = cmd.Parameters.Add("@Minimo", SqlDbType.Decimal);
            min.Precision = 10; min.Scale = 3; min.Value = i.StockMinimo;
            cmd.Parameters.Add("@Categoria", SqlDbType.VarChar, 50).Value = string.IsNullOrEmpty(i.Categoria) ? (object)DBNull.Value : i.Categoria;
            cmd.Parameters.Add("@Descripcion", SqlDbType.VarChar, 255).Value = string.IsNullOrEmpty(i.Descripcion) ? (object)DBNull.Value : i.Descripcion;
        }

        // ------------------------------------------------------
        // ENTRADAS: COMPRA + DETALLE_COMPRA (el trigger suma stock)
        // ------------------------------------------------------
        public static int RegistrarCompra(int idProveedor, string documento, DateTime fecha,
                                          IList<LineaCompra> lineas, int idAdministrador)
        {
            documento = Limpiar(documento).ToUpperInvariant();
            if (idProveedor <= 0)
                throw new ReglaNegocioException("Seleccione el proveedor.");
            if (documento.Length < 3 || documento.Length > 20)
                throw new ReglaNegocioException("Escriba el número de la factura o boleta del proveedor (3 a 20 caracteres).");
            if (fecha.Date > DateTime.Today)
                throw new ReglaNegocioException("La fecha de la compra no puede ser futura.");
            if (lineas == null || lineas.Count == 0)
                throw new ReglaNegocioException("Agregue al menos un insumo a la compra.");
            if (lineas.Select(l => l.IdInsumo).Distinct().Count() != lineas.Count)
                throw new ReglaNegocioException("Un insumo aparece dos veces en la compra: sume sus cantidades en una sola línea.");
            foreach (LineaCompra l in lineas)
            {
                if (l.Cantidad <= 0 || decimal.Round(l.Cantidad, 3) != l.Cantidad || l.Cantidad > CantidadMaxima)
                    throw new ReglaNegocioException("Cada cantidad debe ser mayor que 0, con hasta 3 decimales.");
                if (l.CostoUnitario < 0 || decimal.Round(l.CostoUnitario, 2) != l.CostoUnitario)
                    throw new ReglaNegocioException("Cada costo unitario debe ser 0 o mayor, con hasta 2 decimales.");
            }

            return Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idAdministrador);

                using (SqlCommand cmd = new SqlCommand(
                    "SELECT COUNT(*) FROM dbo.PROVEEDOR WHERE id_proveedor = @Id;", cx, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idProveedor;
                    if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                        throw new ReglaNegocioException("El proveedor ya no existe.");
                }

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT COUNT(*) FROM dbo.COMPRA WITH (UPDLOCK, HOLDLOCK)
                    WHERE id_proveedor = @Prov AND numero_documento = @Doc;", cx, tx))
                {
                    cmd.Parameters.Add("@Prov", SqlDbType.Int).Value = idProveedor;
                    cmd.Parameters.Add("@Doc", SqlDbType.VarChar, 20).Value = documento;
                    if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                        throw new ReglaNegocioException("La compra con documento " + documento + " de este proveedor ya fue registrada.");
                }

                foreach (LineaCompra l in lineas)
                    VerificarInsumoActivo(cx, tx, l.IdInsumo);

                int idCompra;
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO dbo.COMPRA (fecha, numero_documento, id_proveedor, id_administrador)
                    VALUES (@Fecha, @Doc, @Prov, @Admin);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", cx, tx))
                {
                    cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fecha.Date;
                    cmd.Parameters.Add("@Doc", SqlDbType.VarChar, 20).Value = documento;
                    cmd.Parameters.Add("@Prov", SqlDbType.Int).Value = idProveedor;
                    cmd.Parameters.Add("@Admin", SqlDbType.Int).Value = idAdministrador;
                    idCompra = Convert.ToInt32(cmd.ExecuteScalar());
                }

                foreach (LineaCompra l in lineas)
                {
                    using (SqlCommand cmd = new SqlCommand(@"
                        INSERT INTO dbo.DETALLE_COMPRA (id_compra, id_insumo, cantidad, costo_unitario)
                        VALUES (@Compra, @Insumo, @Cantidad, @Costo);", cx, tx))
                    {
                        cmd.Parameters.Add("@Compra", SqlDbType.Int).Value = idCompra;
                        cmd.Parameters.Add("@Insumo", SqlDbType.Int).Value = l.IdInsumo;
                        SqlParameter c = cmd.Parameters.Add("@Cantidad", SqlDbType.Decimal); c.Precision = 10; c.Scale = 3; c.Value = l.Cantidad;
                        SqlParameter k = cmd.Parameters.Add("@Costo", SqlDbType.Decimal); k.Precision = 10; k.Scale = 2; k.Value = l.CostoUnitario;
                        cmd.ExecuteNonQuery();     // TR_DETALLE_COMPRA_STOCK suma el stock
                    }
                }

                Registro.Info("Inventario", "Administrador " + idAdministrador + " registró la compra " + idCompra +
                    " (" + documento + ", " + lineas.Count + " insumo(s)).");
                return idCompra;
            });
        }

        // ------------------------------------------------------
        // SALIDAS: MERMA (solo pérdidas; el trigger resta stock)
        // ------------------------------------------------------
        public static int RegistrarMerma(int idInsumo, decimal cantidad, string motivo, string detalle,
                                         DateTime fecha, int idOperador)
        {
            if (!MotivosMerma.Contains(motivo))
                throw new ReglaNegocioException("Seleccione el motivo de la merma.");
            detalle = Limpiar(detalle);
            if (motivo == "Otra pérdida" && detalle.Length < 3)
                throw new ReglaNegocioException("Describa la pérdida.");
            string texto = detalle.Length > 0 ? motivo + " - " + detalle : motivo;
            if (texto.Length > 255)
                throw new ReglaNegocioException("El detalle del motivo es demasiado largo.");
            if (cantidad <= 0 || decimal.Round(cantidad, 3) != cantidad)
                throw new ReglaNegocioException("La cantidad debe ser mayor que 0, con hasta 3 decimales.");
            if (fecha.Date > DateTime.Today)
                throw new ReglaNegocioException("La fecha de la merma no puede ser futura.");

            return Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);
                decimal stock = VerificarInsumoActivo(cx, tx, idInsumo);
                if (cantidad > stock)
                    throw new ReglaNegocioException("No hay stock suficiente: hay " + Cantidad(stock) +
                        " y se intenta dar de baja " + Cantidad(cantidad) + ".");

                int id;
                using (SqlCommand cmd = new SqlCommand(@"
                    INSERT INTO dbo.MERMA (fecha, cantidad, motivo, id_insumo, id_empleado)
                    VALUES (@Fecha, @Cantidad, @Motivo, @Insumo, @Empleado);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", cx, tx))
                {
                    cmd.Parameters.Add("@Fecha", SqlDbType.Date).Value = fecha.Date;
                    SqlParameter c = cmd.Parameters.Add("@Cantidad", SqlDbType.Decimal); c.Precision = 10; c.Scale = 3; c.Value = cantidad;
                    cmd.Parameters.Add("@Motivo", SqlDbType.VarChar, 255).Value = texto;
                    cmd.Parameters.Add("@Insumo", SqlDbType.Int).Value = idInsumo;
                    cmd.Parameters.Add("@Empleado", SqlDbType.Int).Value = idOperador;
                    id = Convert.ToInt32(cmd.ExecuteScalar());   // TR_MERMA_STOCK resta el stock
                }

                Registro.Info("Inventario", "Empleado " + idOperador + " registró la merma " + id + " del insumo " + idInsumo + ".");
                return id;
            });
        }

        // Bloquea el insumo y devuelve su stock; exige activo y de esta sucursal.
        private static decimal VerificarInsumoActivo(SqlConnection cx, SqlTransaction tx, int idInsumo)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT stock_actual, estado, id_sucursal, nombre FROM dbo.INSUMO WITH (UPDLOCK, HOLDLOCK)
                WHERE id_insumo = @Id;", cx, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idInsumo;
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read() || r.GetInt32(2) != BaseDatos.IdSucursalActual)
                        throw new ReglaNegocioException("El insumo no existe en esta sucursal.");
                    if (r.GetString(1) != "activo")
                        throw new ReglaNegocioException("El insumo \"" + r.GetString(3) + "\" está inactivo.");
                    return r.GetDecimal(0);
                }
            }
        }

        // ------------------------------------------------------
        // PROVEEDORES
        // ------------------------------------------------------
        public static int GuardarProveedor(ProveedorInfo p, int idOperador)
        {
            Validar(p);
            return Ejecutar((cx, tx) =>
            {
                EmpleadoDatos.VerificarAdministrador(cx, tx, idOperador);

                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT TOP (1) nombre FROM dbo.PROVEEDOR WITH (UPDLOCK, HOLDLOCK)
                    WHERE ruc = @Ruc AND id_proveedor <> @Id;", cx, tx))
                {
                    cmd.Parameters.Add("@Ruc", SqlDbType.Char, 11).Value = p.Ruc;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = p.IdProveedor;
                    object otro = cmd.ExecuteScalar();
                    if (otro != null)
                        throw new ReglaNegocioException("El RUC " + p.Ruc + " ya pertenece a " + otro + ".");
                }

                string sql = p.IdProveedor == 0
                    ? @"INSERT INTO dbo.PROVEEDOR (nombre, ruc, [teléfono], [dirección])
                        VALUES (@Nombre, @Ruc, @Telefono, @Direccion);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);"
                    : @"UPDATE dbo.PROVEEDOR SET nombre = @Nombre, ruc = @Ruc, [teléfono] = @Telefono, [dirección] = @Direccion
                        WHERE id_proveedor = @Id;
                        SELECT CASE WHEN @@ROWCOUNT = 1 THEN @Id ELSE 0 END;";

                int id;
                using (SqlCommand cmd = new SqlCommand(sql, cx, tx))
                {
                    cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = p.Nombre;
                    cmd.Parameters.Add("@Ruc", SqlDbType.Char, 11).Value = p.Ruc;
                    cmd.Parameters.Add("@Telefono", SqlDbType.VarChar, 20).Value = p.Telefono.Length == 0 ? (object)DBNull.Value : p.Telefono;
                    cmd.Parameters.Add("@Direccion", SqlDbType.VarChar, 255).Value = p.Direccion.Length == 0 ? (object)DBNull.Value : p.Direccion;
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = p.IdProveedor;
                    id = Convert.ToInt32(cmd.ExecuteScalar());
                }
                if (id == 0)
                    throw new ReglaNegocioException("El proveedor ya no existe.");

                Registro.Info("Inventario", "Empleado " + idOperador + (p.IdProveedor == 0 ? " registró" : " actualizó") + " el proveedor " + id + ".");
                return id;
            });
        }

        // Transacción Serializable; los errores de los triggers de
        // stock (RAISERROR 'COMPRA: ...' / 'MERMA: ...') se muestran tal cual.
        private static T Ejecutar<T>(Func<SqlConnection, SqlTransaction, T> operacion)
        {
            try
            {
                return EmpleadoDatos.Ejecutar(operacion);
            }
            catch (SqlException ex)
            {
                if (ex.Number == 50000 && (ex.Message.StartsWith("COMPRA:") || ex.Message.StartsWith("MERMA:")))
                    throw new ReglaNegocioException(ex.Message.Split('\n')[0]);
                if ((ex.Number == 2627 || ex.Number == 2601) && ex.Message.Contains("UQ_COMPRA_PROV_DOC"))
                    throw new ReglaNegocioException("Esa compra de este proveedor ya fue registrada.");
                throw;
            }
        }
    }
}
