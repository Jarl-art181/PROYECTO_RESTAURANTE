using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PROYECTO_RESTAURANTE
{
    internal sealed class ProductoMenu
    {
        public int IdPlato;
        public string Nombre;
        public string Descripcion;
        public decimal Precio;
        public string Foto;            // nombre de archivo en la carpeta de fotos
        public string Estado = "activo";
        public int IdCategoria;
        public string Categoria;

        public bool Activo { get { return Estado == "activo"; } }
        public string Grupo { get { return MenuDatos.Grupo(Categoria); } }
        public string PrecioTexto { get { return "S/" + Precio.ToString("0.00", CultureInfo.InvariantCulture); } }
    }

    internal sealed class CategoriaInfo
    {
        public int IdCategoria;
        public string Nombre;
        public override string ToString() { return Nombre; }
    }

    // ==========================================================
    // CATÁLOGO DEL MENÚ (FASE D)
    // ----------------------------------------------------------
    // Fuente única de productos para Menú y Pedidos: dbo.PLATO
    // con su CATEGORIA. Sin cambios de esquema.
    //  * "Eliminar" desactiva (estado = 'inactivo'): los pedidos,
    //    recetas, combos y promociones que lo usan se conservan.
    //  * Cambiar el precio no altera ventas pasadas: cada detalle
    //    de pedido guarda su propio precio_unitario.
    //  * Las fotos se guardan redimensionadas en
    //    %ProgramData%\AlEstiloPiurano\Fotos y PLATO.foto guarda
    //    solo el nombre del archivo.
    //  * Grupos de los filtros: categoría "Bebidas" = bebidas,
    //    "Agregados" = agregados, el resto = platos.
    // ==========================================================
    internal static class MenuDatos
    {
        public const string Platos = "platos";
        public const string Bebidas = "bebidas";
        public const string Agregados = "agregados";

        public const decimal PrecioMaximo = 9999.99m;

        // Doble del tamaño en que se muestran (212 x 147) para nitidez.
        private const int AnchoFoto = 424;
        private const int AltoFoto = 294;

        public static string Grupo(string categoria)
        {
            string c = Normalizar(categoria);
            if (c == "bebidas") return Bebidas;
            if (c == "agregados") return Agregados;
            return Platos;
        }

        public static string Normalizar(string texto)
        {
            string d = (texto ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------
        // CONSULTAS
        // ------------------------------------------------------
        public static List<ProductoMenu> Listar()
        {
            List<ProductoMenu> lista = new List<ProductoMenu>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT P.id_plato, P.nombre, P.[descripción] AS descripcion, P.precio,
                       P.foto, P.estado, P.id_categoria, C.nombre AS categoria
                FROM dbo.PLATO P
                INNER JOIN dbo.CATEGORIA C ON C.id_categoria = P.id_categoria
                ORDER BY P.id_plato;", conexion))
            {
                cmd.CommandTimeout = 20;
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        lista.Add(new ProductoMenu
                        {
                            IdPlato = Convert.ToInt32(r["id_plato"]),
                            Nombre = Convert.ToString(r["nombre"]),
                            Descripcion = r["descripcion"] == DBNull.Value ? "" : Convert.ToString(r["descripcion"]),
                            Precio = Convert.ToDecimal(r["precio"]),
                            Foto = r["foto"] == DBNull.Value ? null : Convert.ToString(r["foto"]),
                            Estado = Convert.ToString(r["estado"]).Trim().ToLowerInvariant(),
                            IdCategoria = Convert.ToInt32(r["id_categoria"]),
                            Categoria = Convert.ToString(r["categoria"])
                        });
                    }
                }
            }
            return lista;
        }

        public static List<CategoriaInfo> ListarCategorias()
        {
            List<CategoriaInfo> lista = new List<CategoriaInfo>();
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(
                "SELECT id_categoria, nombre FROM dbo.CATEGORIA WHERE estado = 'activo' ORDER BY nombre;", conexion))
            {
                conexion.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        lista.Add(new CategoriaInfo { IdCategoria = Convert.ToInt32(r[0]), Nombre = Convert.ToString(r[1]) });
                }
            }
            return lista;
        }

        // Promociones activas cuyo periodo incluye hoy.
        public static int PromocionesVigentes()
        {
            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT COUNT(*) FROM dbo.PROMOCION
                WHERE estado = 'activo'
                  AND CAST(SYSDATETIME() AS DATE) BETWEEN fecha_inicio AND fecha_fin;", conexion))
            {
                conexion.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ------------------------------------------------------
        // VALIDACIÓN
        // ------------------------------------------------------
        public static decimal LeerPrecio(string texto)
        {
            texto = (texto ?? "").Trim().Replace("S/", "").Replace(" ", "").Replace(',', '.');
            decimal precio;
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out precio))
                throw new ReglaNegocioException("Escriba el precio como número, por ejemplo 15.00.");
            return precio;
        }

        public static void Validar(ProductoMenu p)
        {
            p.Nombre = Limpiar(p.Nombre);
            p.Descripcion = Limpiar(p.Descripcion);

            if (p.Nombre.Length < 2 || p.Nombre.Length > 100)
                throw new ReglaNegocioException("El nombre del producto debe tener entre 2 y 100 caracteres.");
            if (p.IdCategoria <= 0)
                throw new ReglaNegocioException("Seleccione la categoría.");
            if (p.Precio <= 0 || p.Precio > PrecioMaximo)
                throw new ReglaNegocioException("El precio debe ser mayor que 0 y como máximo S/" + PrecioMaximo.ToString("0.00", CultureInfo.InvariantCulture) + ".");
            if (decimal.Round(p.Precio, 2) != p.Precio)
                throw new ReglaNegocioException("El precio admite como máximo 2 decimales.");
            if (p.Descripcion.Length > 255)
                throw new ReglaNegocioException("La descripción no puede superar 255 caracteres.");
        }

        private static string Limpiar(string texto)
        {
            texto = (texto ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            while (texto.Contains("  "))
                texto = texto.Replace("  ", " ");
            return texto;
        }

        // ------------------------------------------------------
        // ESCRITURAS (transacción Serializable + Administrador)
        // ------------------------------------------------------
        public static int Registrar(ProductoMenu p, int idOperador)
        {
            Validar(p);
            return EmpleadoDatos.Ejecutar((conexion, tx) => Registrar(conexion, tx, p, idOperador));
        }

        internal static int Registrar(SqlConnection conexion, SqlTransaction tx, ProductoMenu p, int idOperador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idOperador);
            VerificarCategoria(conexion, tx, p.IdCategoria);
            VerificarNombreLibre(conexion, tx, p.Nombre, null);

            int id;
            using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO dbo.PLATO (nombre, [descripción], precio, foto, estado, id_categoria)
                VALUES (@Nombre, @Descripcion, @Precio, @Foto, 'activo', @Categoria);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", conexion, tx))
            {
                Parametros(cmd, p);
                id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            Registro.Info("Menu", "Empleado " + idOperador + " registró el producto " + id + " (" + p.Nombre + ", " + p.PrecioTexto + ").");
            return id;
        }

        public static void Actualizar(ProductoMenu p, int idOperador)
        {
            Validar(p);
            EmpleadoDatos.Ejecutar((conexion, tx) => { Actualizar(conexion, tx, p, idOperador); return 0; });
        }

        internal static void Actualizar(SqlConnection conexion, SqlTransaction tx, ProductoMenu p, int idOperador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idOperador);
            decimal anterior = Bloquear(conexion, tx, p.IdPlato);
            VerificarCategoria(conexion, tx, p.IdCategoria);
            VerificarNombreLibre(conexion, tx, p.Nombre, p.IdPlato);

            using (SqlCommand cmd = new SqlCommand(@"
                UPDATE dbo.PLATO
                   SET nombre = @Nombre, [descripción] = @Descripcion, precio = @Precio,
                       foto = @Foto, id_categoria = @Categoria
                 WHERE id_plato = @Id;", conexion, tx))
            {
                Parametros(cmd, p);
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = p.IdPlato;
                cmd.ExecuteNonQuery();
            }

            Registro.Info("Menu", "Empleado " + idOperador + " actualizó el producto " + p.IdPlato +
                (anterior != p.Precio ? " (precio S/" + anterior.ToString("0.00", CultureInfo.InvariantCulture) + " -> " + p.PrecioTexto + ")." : "."));
        }

        // Desactivar ("Eliminar") o reactivar. Nunca borra.
        public static void CambiarEstado(int idPlato, bool activo, int idOperador)
        {
            EmpleadoDatos.Ejecutar((conexion, tx) =>
            {
                CambiarEstado(conexion, tx, idPlato, activo, idOperador);
                return 0;
            });
        }

        internal static void CambiarEstado(SqlConnection conexion, SqlTransaction tx, int idPlato, bool activo, int idOperador)
        {
            EmpleadoDatos.VerificarAdministrador(conexion, tx, idOperador);
            Bloquear(conexion, tx, idPlato);

            if (activo)
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT C.estado FROM dbo.PLATO P JOIN dbo.CATEGORIA C ON C.id_categoria = P.id_categoria
                    WHERE P.id_plato = @Id;", conexion, tx))
                {
                    cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idPlato;
                    if (Convert.ToString(cmd.ExecuteScalar()) != "activo")
                        throw new ReglaNegocioException("La categoría del producto está inactiva. Cambie la categoría antes de reactivarlo.");
                }
            }

            using (SqlCommand cmd = new SqlCommand(
                "UPDATE dbo.PLATO SET estado = @Estado WHERE id_plato = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 20).Value = activo ? "activo" : "inactivo";
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idPlato;
                cmd.ExecuteNonQuery();
            }

            Registro.Info("Menu", "Empleado " + idOperador + (activo ? " reactivó" : " desactivó") + " el producto " + idPlato + ".");
        }

        private static decimal Bloquear(SqlConnection conexion, SqlTransaction tx, int idPlato)
        {
            using (SqlCommand cmd = new SqlCommand(
                "SELECT precio FROM dbo.PLATO WITH (UPDLOCK, HOLDLOCK) WHERE id_plato = @Id;", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idPlato;
                object precio = cmd.ExecuteScalar();
                if (precio == null)
                    throw new ReglaNegocioException("El producto ya no existe.");
                return Convert.ToDecimal(precio);
            }
        }

        private static void VerificarCategoria(SqlConnection conexion, SqlTransaction tx, int idCategoria)
        {
            using (SqlCommand cmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.CATEGORIA WHERE id_categoria = @Id AND estado = 'activo';", conexion, tx))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idCategoria;
                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                    throw new ReglaNegocioException("La categoría seleccionada no está disponible.");
            }
        }

        private static void VerificarNombreLibre(SqlConnection conexion, SqlTransaction tx, string nombre, int? idActual)
        {
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT TOP (1) CASE WHEN estado = 'inactivo' THEN ' (inactivo)' ELSE '' END
                FROM dbo.PLATO WITH (UPDLOCK, HOLDLOCK)
                WHERE nombre = @Nombre AND (@Id IS NULL OR id_plato <> @Id);", conexion, tx))
            {
                cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = nombre;
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idActual.HasValue ? (object)idActual.Value : DBNull.Value;
                object existe = cmd.ExecuteScalar();
                if (existe != null)
                    throw new ReglaNegocioException("Ya existe un producto llamado \"" + nombre + "\"" + existe + ".");
            }
        }

        private static void Parametros(SqlCommand cmd, ProductoMenu p)
        {
            cmd.Parameters.Add("@Nombre", SqlDbType.VarChar, 100).Value = p.Nombre;
            cmd.Parameters.Add("@Descripcion", SqlDbType.VarChar, 255).Value =
                string.IsNullOrEmpty(p.Descripcion) ? (object)DBNull.Value : p.Descripcion;
            SqlParameter precio = cmd.Parameters.Add("@Precio", SqlDbType.Decimal);
            precio.Precision = 10;
            precio.Scale = 2;
            precio.Value = p.Precio;
            cmd.Parameters.Add("@Foto", SqlDbType.VarChar, 255).Value =
                string.IsNullOrEmpty(p.Foto) ? (object)DBNull.Value : p.Foto;
            cmd.Parameters.Add("@Categoria", SqlDbType.Int).Value = p.IdCategoria;
        }

        // ------------------------------------------------------
        // FOTOS
        // ------------------------------------------------------
        public static string CarpetaFotos
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "AlEstiloPiurano", "Fotos");
            }
        }

        // Copia la imagen elegida, redimensionada, a la carpeta de
        // fotos. Devuelve el nombre del archivo para PLATO.foto.
        public static string GuardarFoto(string rutaOrigen)
        {
            Directory.CreateDirectory(CarpetaFotos);
            string nombre = "plato_" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".jpg";

            using (Image original = CargarSinBloquear(rutaOrigen))
            {
                if (original == null)
                    throw new ReglaNegocioException("El archivo elegido no es una imagen válida.");

                double escala = Math.Min(1.0, Math.Min((double)AnchoFoto / original.Width, (double)AltoFoto / original.Height));
                int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
                int alto = Math.Max(1, (int)Math.Round(original.Height * escala));

                using (Bitmap destino = new Bitmap(ancho, alto))
                using (Graphics g = Graphics.FromImage(destino))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(original, 0, 0, ancho, alto);

                    ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (EncoderParameters parametros = new EncoderParameters(1))
                    {
                        parametros.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
                        destino.Save(Path.Combine(CarpetaFotos, nombre), jpeg, parametros);
                    }
                }
            }

            return nombre;
        }

        // Carga una foto del catálogo (null si no existe o falla).
        public static Image CargarFoto(string nombre)
        {
            if (string.IsNullOrEmpty(nombre) || nombre.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;
            return CargarSinBloquear(Path.Combine(CarpetaFotos, nombre));
        }

        // Copia en memoria para no dejar el archivo bloqueado.
        private static Image CargarSinBloquear(string ruta)
        {
            try
            {
                if (!File.Exists(ruta))
                    return null;
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(ruta)))
                using (Image img = Image.FromStream(ms))
                    return new Bitmap(img);
            }
            catch (Exception ex)
            {
                Registro.Error("Menu", ex);
                return null;
            }
        }
    }
}
