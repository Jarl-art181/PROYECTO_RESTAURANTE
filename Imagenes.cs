using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // IMÁGENES DEL SISTEMA (centralizadas y reemplazables)
    // ----------------------------------------------------------
    // Cada ícono, botón, encabezado y tarjeta con imagen tiene un
    // nombre de archivo según su FUNCIÓN (ver Mapa). Si existe
    //     <carpeta del programa>\Imagenes\<Carpeta>\<archivo>.png|.jpg
    // se muestra ese archivo; si no existe o está dañado, se muestra la
    // imagen predeterminada (la del diseño o la indicada en el mapa).
    // Nada se borra ni se sobrescribe: quitar el archivo restaura la
    // imagen original. Se vuelve a leer al mostrarse la ventana, así un
    // archivo nuevo aparece sin recompilar.
    //
    // Las fotos de productos usan la misma carpeta (Imagenes\Productos\
    // plato_<id_plato>.jpg) desde MenuDatos.FotoProducto, en Menú,
    // Pedidos e Inicio.
    //
    // Memoria: los archivos se leen sin bloquearlos y se reducen al doble
    // del tamaño del control; la imagen anterior se libera al cambiarla y
    // al cerrarse la ventana. Las predeterminadas compartidas se crean una
    // sola vez.
    // ==========================================================
    internal static class Imagenes
    {
        public const string Iconos = "Iconos", Botones = "Botones", Paneles = "Paneles", Productos = "Productos";
        private static readonly string[] Extensiones = { ".png", ".jpg", ".jpeg", ".bmp" };

        public static string Raiz
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Imagenes"); }
        }

        // Crea las carpetas vacías (si se puede); nunca toca archivos.
        public static void AsegurarCarpetas()
        {
            try
            {
                foreach (string c in new[] { Iconos, Botones, Paneles, Productos })
                    Directory.CreateDirectory(Path.Combine(Raiz, c));
            }
            catch (Exception ex) { Registro.Error("Imagenes", ex); }
        }

        // Ruta del archivo personalizado (cualquier extensión aceptada) o null.
        public static string Buscar(string carpeta, string nombre)
        {
            try
            {
                string baseRuta = Path.Combine(Raiz, carpeta, nombre);
                foreach (string ext in Extensiones)
                    if (File.Exists(baseRuta + ext))
                        return baseRuta + ext;
            }
            catch (Exception ex) { Registro.Error("Imagenes", ex); }
            return null;
        }

        public static long Version(string ruta)
        {
            try { return ruta == null ? 0 : File.GetLastWriteTimeUtc(ruta).Ticks ^ new FileInfo(ruta).Length; }
            catch { return 0; }
        }

        // Lee un archivo sin bloquearlo y lo reduce (conservando la proporción)
        // para que no supere "maximo". null si no es una imagen válida.
        public static Image Cargar(string ruta, Size maximo)
        {
            if (ruta == null) return null;
            try
            {
                using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(ruta)))
                using (Image original = Image.FromStream(ms))
                {
                    double k = 1.0;
                    if (maximo.Width > 0 && maximo.Height > 0)
                        k = Math.Min(1.0, Math.Min((double)maximo.Width / original.Width, (double)maximo.Height / original.Height));
                    int w = Math.Max(1, (int)Math.Round(original.Width * k)), h = Math.Max(1, (int)Math.Round(original.Height * k));
                    Bitmap copia = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(copia))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(original, 0, 0, w, h);
                    }
                    return copia;
                }
            }
            catch (Exception ex)
            {
                Registro.Info("Imagenes", "No se pudo leer " + ruta + ": " + ex.Message + " (se usa la imagen predeterminada).");
                return null;
            }
        }

        // ======================================================
        // MAPA: qué archivo corresponde a cada control (por función)
        // ======================================================
        internal sealed class Entrada
        {
            public string Ventana;          // "*" = todas las ventanas de módulo
            public string Control;
            public string Carpeta;
            public string Archivo;
            public string Uso;
            public string Predeterminada = "la del diseño";
            public Func<Image> Origen;      // predeterminada distinta de la del diseño
        }

        private static readonly Dictionary<string, Image> compartidas = new Dictionary<string, Image>();

        // Imagen del .resx de otra ventana (mismo estilo), creada una sola vez.
        private static Func<Image> De(Type ventana, string control)
        {
            return () => Compartida(ventana.Name + "." + control, () =>
            {
                ComponentResourceManager r = new ComponentResourceManager(ventana);
                return r.GetObject(control + ".Image") as Image;
            });
        }

        // Pictograma (silueta) de otra imagen, en el color indicado o en el de una imagen de referencia.
        private static Func<Image> Silueta(Func<Image> fuente, Func<Color> color, string clave)
        {
            return () => Compartida("silueta." + clave, () =>
            {
                Image f = fuente();
                return f == null ? null : Pictograma(f, color());
            });
        }

        // Silueta del dibujo CLARO de un ícono blanco sobre fondo oscuro.
        private static Func<Image> SiluetaClara(Func<Image> fuente, Color color, string clave)
        {
            return () => Compartida("silueta_clara." + clave, () =>
            {
                Image f = fuente();
                if (f == null) return null;
                using (Bitmap o = new Bitmap(f))
                {
                    Bitmap r = new Bitmap(o.Width, o.Height, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < o.Height; y++)
                        for (int x = 0; x < o.Width; x++)
                        {
                            Color c = o.GetPixel(x, y);
                            double luz = (0.3 * c.R + 0.59 * c.G + 0.11 * c.B) / 255.0;
                            double a = Math.Max(0.0, Math.Min(1.0, (luz - 0.35) / 0.5)) * c.A / 255.0;
                            r.SetPixel(x, y, Color.FromArgb((int)(a * 255), color));
                        }
                    return Recortar(r);
                }
            });
        }

        private static Func<Image> Glifo(char glifo, string clave)
        {
            return () => Compartida("glifo." + clave, () =>
            {
                Bitmap b = new Bitmap(96, 96);
                using (Graphics g = Graphics.FromImage(b))
                using (Font f = new Font("Segoe MDL2 Assets", 60f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (StringFormat c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.DrawString(glifo.ToString(), f, Brushes.Black, new RectangleF(0, 0, 96, 96), c);
                }
                return b;
            });
        }

        private static Image Compartida(string clave, Func<Image> crear)
        {
            lock (compartidas)
            {
                Image im;
                if (compartidas.TryGetValue(clave, out im)) return im;
                try { im = crear(); }
                catch (Exception ex) { Registro.Error("Imagenes", ex); im = null; }
                compartidas[clave] = im;
                return im;
            }
        }

        private static Func<Color> ColorDe(Type ventana, string control)
        {
            return () => { Image i = De(ventana, control)(); return i == null ? Color.Black : ColorPictograma(i); };
        }

        private static readonly List<Entrada> mapa = CrearMapa();

        public static IList<Entrada> Mapa { get { return mapa.AsReadOnly(); } }

        private static List<Entrada> CrearMapa()
        {
            List<Entrada> m = new List<Entrada>();
            Action<string, string, string, string, string> e = (v, c, carpeta, archivo, uso) =>
                m.Add(new Entrada { Ventana = v, Control = c, Carpeta = carpeta, Archivo = archivo, Uso = uso });
            Action<string, string, string, string, string, string, Func<Image>> d = (v, c, carpeta, archivo, uso, pred, origen) =>
                m.Add(new Entrada { Ventana = v, Control = c, Carpeta = carpeta, Archivo = archivo, Uso = uso, Predeterminada = pred, Origen = origen });

            // ---- Comunes a todas las ventanas de módulo ----
            e("*", "pictureBox1", Iconos, "inicio", "Menú lateral: Inicio");
            e("*", "pictureBox2", Iconos, "mesas", "Menú lateral: Mesas");
            e("*", "pictureBox3", Iconos, "pedidos", "Menú lateral: Pedidos");
            e("*", "pictureBox6", Iconos, "cocina", "Menú lateral: Cocina");
            e("*", "pictureBox5", Iconos, "clientes", "Menú lateral: Clientes");
            e("*", "pictureBox4", Iconos, "empleados", "Menú lateral: Empleados");
            e("*", "pictureBox10", Iconos, "menu", "Menú lateral: Menú");
            e("*", "pictureBox9", Iconos, "inventario", "Menú lateral: Inventario");
            e("*", "pictureBox8", Iconos, "promociones", "Menú lateral: Promociones");
            e("*", "pictureBox7", Iconos, "reportes", "Menú lateral: Reportes");
            e("*", "guna2PictureCerrar", Iconos, "cerrar_sesion", "Menú lateral: Cerrar sesión");
            e("*", "guna2PictureBox7", Iconos, "sistema", "Barra superior: Sistema de gestión");
            e("*", "guna2PictureBox6", Iconos, "usuario", "Barra superior: usuario de la sesión");
            e("*", "guna2PictureBox10", Iconos, "reloj", "Barra superior: fecha y hora");

            Type inicio = typeof(FRM_INICIO), pedidos = typeof(FRMS_PEDIDOS), clientes = typeof(FRM_CLIENTES),
                 mesas = typeof(FRM_MESAS), boleta = typeof(Generar_Boleta);

            // ---- Inicio ----
            e("FRM_INICIO", "guna2PictureBox1", Paneles, "banner_inicio", "Inicio: banner superior");
            d("FRM_INICIO", "guna2PictureBox4", Paneles, "inicio_ventas", "Inicio: tarjeta Ventas totales",
                "gráfico de ventas (antes tenía una mesa)", Silueta(De(inicio, "guna2PictureBox2"), ColorDe(inicio, "guna2PictureBox4"), "inicio_ventas"));
            d("FRM_INICIO", "guna2PictureBox2", Paneles, "inicio_pendientes", "Inicio: tarjeta Pedidos pendientes",
                "lista con reloj (antes tenía un gráfico)", Silueta(De(inicio, "guna2PictureBox3"), ColorDe(inicio, "guna2PictureBox2"), "inicio_pendientes"));
            d("FRM_INICIO", "guna2PictureBox3", Paneles, "inicio_mesas_ocupadas", "Inicio: tarjeta Mesas ocupadas",
                "mesa (antes tenía una lista con reloj)", Silueta(De(inicio, "guna2PictureBox4"), ColorDe(inicio, "guna2PictureBox3"), "inicio_mesas"));
            d("FRM_INICIO", "guna2PictureBox5", Paneles, "inicio_cobrados", "Inicio: tarjeta Pedidos cobrados",
                "recibo cobrado (antes tenía una caja)", Silueta(De(boleta, "guna2PictureBox1"), ColorDe(inicio, "guna2PictureBox5"), "inicio_cobrados"));

            // ---- Mesas ----
            d("FRM_MESAS", "guna2PictureBox1", Paneles, "encabezado_mesas", "Mesas: ícono del encabezado",
                "mesa (antes: el de Pedidos)", De(pedidos, "guna2PictureBox25"));
            e("FRM_MESAS", "guna2PictureBox2", Paneles, "mesas_totales", "Mesas: tarjeta Mesas totales");
            e("FRM_MESAS", "guna2PictureBox3", Paneles, "mesas_libres", "Mesas: tarjeta Mesas libres");
            e("FRM_MESAS", "guna2PictureBox4", Paneles, "mesas_ocupadas", "Mesas: tarjeta Mesas ocupadas");
            e("FRM_MESAS", "guna2PictureBox5", Paneles, "mesas_reservadas", "Mesas: tarjeta Mesas reservadas");
            foreach (string c in new[] { "guna2PictureBox12", "guna2PictureBox13", "guna2PictureBox14", "guna2PictureBox15", "guna2PictureBox16",
                                         "guna2PictureBox17", "guna2PictureBox18", "guna2PictureBox19", "guna2PictureBox20", "guna2PictureBox21", "guna2PictureBox22" })
                e("FRM_MESAS", c, Paneles, "mesa", "Mesas: foto de cada mesa y del detalle");
            e("FRM_MESAS", "guna2PictureBox9", Botones, "nueva_reserva", "Mesas: botón Nueva reserva");
            e("FRM_MESAS", "guna2PictureBox11", Botones, "buscar", "Mesas: buscar");
            e("FRM_MESAS", "guna2PictureBox23", Botones, "detalle_pedido", "Mesas: botón Detalle pedido");
            e("FRM_MESAS", "guna2PictureBox24", Botones, "reservar", "Mesas: botón Reservar");
            e("FRM_MESAS", "guna2PictureBox25", Botones, "liberar_mesa", "Mesas: botón Liberar mesa");

            // ---- Pedidos ----
            e("FRMS_PEDIDOS", "guna2PictureBox1", Paneles, "encabezado_pedidos", "Pedidos: ícono del encabezado");
            e("FRMS_PEDIDOS", "guna2PictureBox44", Botones, "nuevo_pedido", "Pedidos: botón Nuevo pedido");
            d("FRMS_PEDIDOS", "guna2PictureBox42", Botones, "guardar", "Pedidos: botón Guardar",
                "disquete (antes: el de Generar recibo)", De(clientes, "guna2PictureBox11"));
            d("FRMS_PEDIDOS", "guna2PictureBox41", Botones, "limpiar", "Pedidos: botón Limpiar",
                "escoba, como en Clientes", De(clientes, "guna2PictureBox4"));
            e("FRMS_PEDIDOS", "guna2PictureBox43", Botones, "cancelar", "Pedidos: botón Cancelar");
            e("FRMS_PEDIDOS", "guna2PictureBox45", Botones, "generar_recibo", "Pedidos: botón Generar recibo");
            e("FRMS_PEDIDOS", "guna2PictureBox28", Iconos, "categoria_platos", "Filtro Platos");
            e("FRMS_PEDIDOS", "guna2PictureBox29", Iconos, "categoria_agregados", "Filtro Agregados");
            e("FRMS_PEDIDOS", "guna2PictureBox30", Iconos, "categoria_bebidas", "Filtro Bebidas");
            e("FRMS_PEDIDOS", "guna2PictureBox25", Iconos, "tipo_salon", "Tipo de pedido / filtro: Salón");
            e("FRMS_PEDIDOS", "guna2PictureBox26", Iconos, "tipo_delivery", "Tipo de pedido / filtro: Delivery");
            e("FRMS_PEDIDOS", "guna2PictureBox27", Iconos, "tipo_recojo", "Tipo de pedido / filtro: Recojo");
            for (int i = 31; i <= 40; i++)
                e("FRMS_PEDIDOS", "guna2PictureBox" + i, Iconos, "mesa_pedido", "Pedidos: botones de mesa (1 a 10)");

            // ---- Cocina ----
            e("FRM_COCINA", "guna2PictureBox1", Paneles, "encabezado_cocina", "Cocina: ícono del encabezado");
            e("FRM_COCINA", "guna2PictureBox28", Iconos, "filtro_todos", "Filtro Todos");
            e("FRM_COCINA", "guna2PictureBox29", Iconos, "tipo_salon", "Tipo de pedido / filtro: Salón");
            e("FRM_COCINA", "guna2PictureBox30", Iconos, "tipo_delivery", "Tipo de pedido / filtro: Delivery");
            e("FRM_COCINA", "guna2PictureBox31", Iconos, "tipo_recojo", "Tipo de pedido / filtro: Recojo");
            e("FRM_COCINA", "guna2PictureBox11", Botones, "buscar", "Cocina: buscar");
            e("FRM_COCINA", "guna2PictureBox3", Paneles, "cocina_por_iniciar", "Cocina: tarjeta Por iniciar");
            e("FRM_COCINA", "guna2PictureBox2", Paneles, "cocina_en_proceso", "Cocina: tarjeta En proceso");
            e("FRM_COCINA", "guna2PictureBox4", Paneles, "cocina_listos", "Cocina: tarjeta Listos para entregar");

            // ---- Clientes ----
            e("FRM_CLIENTES", "guna2PictureBox1", Paneles, "encabezado_clientes", "Clientes: ícono del encabezado");
            e("FRM_CLIENTES", "guna2PictureBox30", Botones, "nuevo", "Botón Nuevo (cliente)");
            e("FRM_CLIENTES", "guna2PictureBox2", Botones, "editar", "Botón Editar");
            e("FRM_CLIENTES", "guna2PictureBox3", Botones, "eliminar", "Botón Eliminar");
            e("FRM_CLIENTES", "guna2PictureBox4", Botones, "limpiar", "Botón Limpiar");
            e("FRM_CLIENTES", "guna2PictureBox5", Botones, "buscar", "Botón Buscar");
            e("FRM_CLIENTES", "guna2PictureBox11", Botones, "guardar", "Botón Guardar");
            e("FRM_CLIENTES", "guna2PictureBox9", Botones, "cancelar", "Botón Cancelar");
            e("FRM_CLIENTES", "guna2PictureBox12", Paneles, "clientes_total", "Clientes: tarjeta Total de clientes");
            e("FRM_CLIENTES", "guna2PictureBox13", Paneles, "clientes_activos", "Clientes: tarjeta Clientes activos");
            e("FRM_CLIENTES", "guna2PictureBox14", Paneles, "clientes_inactivos", "Clientes: tarjeta Clientes inactivos");
            e("FRM_CLIENTES", "guna2PictureBox15", Paneles, "clientes_frecuentes", "Clientes: tarjeta Clientes frecuentes");
            e("FRM_CLIENTES", "guna2PictureBox16", Paneles, "cliente_foto", "Clientes: foto del detalle del cliente");

            // ---- Empleados ----
            e("FRM_EMPLEADOS", "guna2PictureBox1", Paneles, "encabezado_empleados", "Empleados: ícono del encabezado");
            e("FRM_EMPLEADOS", "guna2PictureBox30", Botones, "nuevo", "Botón Nuevo (empleado)");
            e("FRM_EMPLEADOS", "guna2PictureBox11", Botones, "buscar", "Empleados: buscar");
            e("FRM_EMPLEADOS", "guna2PictureBox2", Paneles, "empleados_total", "Empleados: tarjeta Total empleados");
            e("FRM_EMPLEADOS", "guna2PictureBox3", Paneles, "empleados_administradores", "Empleados: tarjeta Administradores");
            e("FRM_EMPLEADOS", "guna2PictureBox4", Paneles, "empleados_cocineros", "Empleados: tarjeta Cocineros");
            e("FRM_EMPLEADOS", "guna2PictureBox5", Paneles, "empleados_meseros", "Empleados: tarjeta Meseros");
            e("FRM_EMPLEADOS", "guna2PictureBox9", Paneles, "empleado_foto", "Empleados: foto del detalle del empleado");

            // ---- Menú ----
            d("FRM_MENU", "guna2PictureBox1", Paneles, "encabezado_menu", "Menú: ícono del encabezado",
                "carta del menú lateral (antes: el de Pedidos)", SiluetaClara(De(typeof(FRM_MENU), "pictureBox10"), Color.Black, "menu"));
            e("FRM_MENU", "guna2PictureBox28", Botones, "nuevo_producto", "Menú: botón Nuevo producto");
            e("FRM_MENU", "guna2PictureBox27", Iconos, "filtro_todos", "Filtro Todos");
            d("FRM_MENU", "guna2PictureBox26", Iconos, "categoria_platos", "Filtro Platos", "plato, como en Pedidos (antes: mesa)", De(pedidos, "guna2PictureBox28"));
            d("FRM_MENU", "guna2PictureBox25", Iconos, "categoria_bebidas", "Filtro Bebidas", "bebida, como en Pedidos (antes: moto)", De(pedidos, "guna2PictureBox30"));
            d("FRM_MENU", "guna2PictureBox31", Iconos, "categoria_agregados", "Filtro Agregados", "agregados, como en Pedidos (antes: bolsa)", De(pedidos, "guna2PictureBox29"));
            e("FRM_MENU", "guna2PictureBox30", Botones, "guardar", "Botón Guardar");
            e("FRM_MENU", "guna2PictureBox32", Botones, "editar", "Botón Editar");
            e("FRM_MENU", "guna2PictureBox33", Botones, "eliminar", "Botón Eliminar");
            e("FRM_MENU", "guna2PictureBox37", Paneles, "menu_total", "Menú: tarjeta Total productos");
            e("FRM_MENU", "guna2PictureBox34", Paneles, "menu_platos", "Menú: tarjeta Platos activos");
            e("FRM_MENU", "guna2PictureBox36", Paneles, "menu_bebidas", "Menú: tarjeta Bebidas");
            e("FRM_MENU", "guna2PictureBox35", Paneles, "menu_promociones", "Menú: tarjeta Promociones");

            // ---- Inventario ----
            d("FRM_INVENTARIO", "guna2PictureBox1", Paneles, "encabezado_inventario", "Inventario: ícono del encabezado",
                "almacén del menú lateral (antes: el de Pedidos)", SiluetaClara(De(typeof(FRM_INVENTARIO), "pictureBox9"), Color.Black, "inventario"));
            d("FRM_INVENTARIO", "guna2PictureBox28", Botones, "nuevo", "Inventario: botón Nuevo insumo", "documento con +, como Nuevo pedido (antes: bolsa)", De(pedidos, "guna2PictureBox44"));
            e("FRM_INVENTARIO", "guna2PictureBox27", Iconos, "filtro_todos", "Filtro Todos");
            d("FRM_INVENTARIO", "guna2PictureBox26", Iconos, "categoria_platos", "Filtro Platos", "plato, como en Pedidos (antes: mesa)", De(pedidos, "guna2PictureBox28"));
            d("FRM_INVENTARIO", "guna2PictureBox25", Iconos, "categoria_bebidas", "Filtro Bebidas", "bebida, como en Pedidos (antes: moto)", De(pedidos, "guna2PictureBox30"));
            d("FRM_INVENTARIO", "guna2PictureBox31", Iconos, "categoria_agregados", "Filtro Agregados", "agregados, como en Pedidos (antes: bolsa)", De(pedidos, "guna2PictureBox29"));
            d("FRM_INVENTARIO", "guna2PictureBox30", Botones, "registrar_entrada", "Inventario: botón Registrar entrada", "flecha de ingreso (antes: bolsa)", Glifo('', "entrada"));
            d("FRM_INVENTARIO", "guna2PictureBox29", Botones, "registrar_salida", "Inventario: botón Registrar salida", "flecha de salida (antes: bolsa)", Glifo('', "salida"));
            e("FRM_INVENTARIO", "guna2PictureBox38", Botones, "guardar", "Botón Guardar");
            d("FRM_INVENTARIO", "guna2Button16", Botones, "editar", "Inventario: botón Editar", "lápiz (antes sin ícono)", Glifo('', "editar"));
            d("FRM_INVENTARIO", "guna2Button17", Botones, "eliminar", "Inventario: botón Eliminar", "papelera (antes sin ícono)", Glifo('', "eliminar"));
            e("FRM_INVENTARIO", "guna2PictureBox37", Paneles, "inventario_total", "Inventario: tarjeta Total insumos");
            e("FRM_INVENTARIO", "guna2PictureBox34", Paneles, "inventario_stock_adecuado", "Inventario: tarjeta Stock adecuado");
            e("FRM_INVENTARIO", "guna2PictureBox36", Paneles, "inventario_stock_bajo", "Inventario: tarjeta Stock bajo");
            e("FRM_INVENTARIO", "guna2PictureBox35", Paneles, "inventario_proveedores", "Inventario: tarjeta Proveedores");

            // ---- Promociones y Reportes ----
            e("FRM_PROMOCIONES", "guna2PictureBox1", Paneles, "encabezado_promociones", "Promociones: ícono del encabezado");
            e("FRM_REPORTES", "guna2PictureBox1", Paneles, "encabezado_reportes", "Reportes: ícono del encabezado");

            // ---- Reservar mesa ----
            d("RESERVAR_MESA", "guna2PictureBox1", Paneles, "encabezado_reservas", "Reservar mesa: ícono del encabezado",
                "calendario, como Nueva reserva (antes: recibo)", De(mesas, "guna2PictureBox9"));
            d("RESERVAR_MESA", "guna2PictureBox43", Botones, "guardar", "Reservar mesa: botón Guardar", "disquete (antes: una X)", De(clientes, "guna2PictureBox11"));
            e("RESERVAR_MESA", "guna2PictureBox2", Botones, "cancelar", "Reservar mesa: botón Cancelar");

            // ---- Generar recibo ----
            e("Generar_Boleta", "guna2PictureBox1", Paneles, "encabezado_recibo", "Generar recibo: ícono del encabezado");
            e("Generar_Boleta", "btnVistaPreviaBoleta", Botones, "vista_previa", "Generar recibo: botón Vista previa");
            e("Generar_Boleta", "btnImprimirBoleta", Botones, "imprimir", "Generar recibo: botón Imprimir");
            e("Generar_Boleta", "btnCancelarBoleta", Botones, "cancelar", "Generar recibo: botón Cancelar");
            e("Generar_Boleta", "btnRegistrarCobro", Botones, "cobrar", "Generar recibo: botón Cobrar");
            return m;
        }

        // ======================================================
        // APLICACIÓN A UNA VENTANA
        // ======================================================
        private sealed class Estado
        {
            public Image Original;          // la predeterminada (no se libera aquí)
            public Image Propia;            // la del archivo (se libera al cambiar)
            public Image Encuadrada;        // predeterminada reutilizada, encuadrada para este control
            public string Ruta;
            public long Version;
        }

        private static readonly ConditionalWeakTable<Control, Estado> estados = new ConditionalWeakTable<Control, Estado>();
        private static readonly ConditionalWeakTable<Form, object> preparadas = new ConditionalWeakTable<Form, object>();

        // Llamado al construir la ventana (antes de la adaptación de tamaño)
        // y de nuevo cada vez que se muestra (archivos nuevos o quitados).
        public static void Preparar(Form f)
        {
            if (f == null || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;
            object marca;
            if (!preparadas.TryGetValue(f, out marca))
            {
                preparadas.Add(f, new object());
                AsegurarCarpetas();
                AlinearIconosDeBotones(f);
                f.VisibleChanged += (s, e) => { if (f.Visible) Aplicar(f); };
            }
            Aplicar(f);
        }

        public static void Aplicar(Form f)
        {
            string ventana = f.GetType().Name;
            Dictionary<string, Control> porNombre = new Dictionary<string, Control>();
            Recorrer(f, c => { if (!string.IsNullOrEmpty(c.Name) && !porNombre.ContainsKey(c.Name)) porNombre[c.Name] = c; });

            foreach (Entrada en in mapa)
            {
                if (en.Ventana != ventana && en.Ventana != "*")
                    continue;
                Control c;
                if (!porNombre.TryGetValue(en.Control, out c))
                    continue;
                // Las comunes solo en su lugar real: menú lateral y barra superior negra.
                if (en.Ventana == "*" && !EsComun(c, en))
                    continue;
                try { AplicarEntrada(c, en); }
                catch (Exception ex) { Registro.Error("Imagenes", ex); }
            }
        }

        // Menú lateral: su contenedor tiene el botón de Inicio. Barra superior:
        // guna2Panel2 de una ventana de módulo (con menú lateral). El inicio de
        // sesión tiene su propia barra y no usa estas imágenes.
        private static bool EsComun(Control c, Entrada en)
        {
            Form f = c.FindForm();
            if (c.Parent == null || f == null || f is INICIAR_SESIÓN)
                return false;
            bool menuLateral = f.Controls.Find("ButtonINICIO", true).Length > 0;
            if (en.Control.StartsWith("guna2PictureBox"))
                return menuLateral && c.Parent.Name == "guna2Panel2";
            return c.Parent.Controls.ContainsKey("ButtonINICIO");
        }

        private static void AplicarEntrada(Control c, Entrada en)
        {
            Estado st = estados.GetValue(c, x => new Estado());
            if (st.Original == null)
            {
                Image diseno = ImagenDe(c);
                Image pred = en.Origen != null ? en.Origen() : null;
                if (pred != null && c is PictureBox)
                {
                    // Con la proporción del cuadro: sin deformar ni dejar franjas. Sobre
                    // un cuadro transparente (tarjetas) la silueta va sin fondo; en los
                    // demás, sobre el color de fondo del ícono que reemplaza.
                    Color fondo = c.BackColor.A == 0 && EsSilueta(pred) ? Color.Transparent : FondoDe(diseno);
                    pred = Encuadrar(pred, new Size(Math.Max(32, c.Width * 2), Math.Max(32, c.Height * 2)), fondo);
                    st.Encuadrada = pred;
                }
                st.Original = pred ?? diseno;
                if (pred != null)
                    PonerImagen(c, pred);
                c.Disposed += (s, e) =>
                {
                    if (st.Propia != null) { st.Propia.Dispose(); st.Propia = null; }
                    if (st.Encuadrada != null) { st.Encuadrada.Dispose(); st.Encuadrada = null; }
                };
            }

            string ruta = Buscar(en.Carpeta, en.Archivo);
            long version = Version(ruta);
            if (ruta == st.Ruta && version == st.Version)
                return;                                       // sin cambios

            Image nueva = null;
            if (ruta != null)
            {
                Size maximo = new Size(Math.Max(64, c.Width * 2), Math.Max(64, c.Height * 2));
                if (c is PictureBox && ((PictureBox)c).SizeMode != PictureBoxSizeMode.Zoom && en.Carpeta == Paneles && c.Width > 600)
                    maximo = new Size(c.Width * 2, c.Height * 2);
                nueva = Cargar(ruta, maximo);
                // En un cuadro (ícono, tarjeta): se encuadra con la proporción del
                // cuadro sobre el color de fondo de la imagen original, así nunca
                // se deforma ni aparecen franjas, cualquiera sea su tamaño.
                if (nueva != null && c is PictureBox && c.Width <= 600)
                {
                    Image encuadrada = Encuadrar(nueva, maximo, FondoDe(st.Original));
                    nueva.Dispose();
                    nueva = encuadrada;
                }
            }

            Image anterior = st.Propia;
            PonerImagen(c, nueva ?? st.Original);
            st.Propia = nueva;
            st.Ruta = ruta;
            st.Version = version;
            if (anterior != null && anterior != nueva)
                anterior.Dispose();
            if (nueva != null)
                Registro.Info("Imagenes", NombreVentana(c) + "." + c.Name + " usa " + ruta);
        }

        private static Bitmap Encuadrar(Image im, Size caja, Color fondo)
        {
            Bitmap b = new Bitmap(Math.Max(1, caja.Width), Math.Max(1, caja.Height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(fondo);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float k = Math.Min((float)b.Width / im.Width, (float)b.Height / im.Height);
                float w = im.Width * k, h = im.Height * k;
                g.DrawImage(im, (b.Width - w) / 2f, (b.Height - h) / 2f, w, h);
            }
            return b;
        }

        // Silueta = esquinas transparentes (pictogramas generados).
        private static bool EsSilueta(Image im)
        {
            try
            {
                using (Bitmap o = new Bitmap(im))
                    return o.GetPixel(0, 0).A == 0 && o.GetPixel(o.Width - 1, o.Height - 1).A == 0;
            }
            catch { return false; }
        }

        // Color de la esquina de la imagen original (fondo del ícono); transparente si no hay.
        private static Color FondoDe(Image original)
        {
            try
            {
                using (Bitmap o = new Bitmap(original))
                    return o.GetPixel(1, 1);
            }
            catch { return Color.Transparent; }
        }

        private static string NombreVentana(Control c) { Form x = c.FindForm(); return x == null ? "?" : x.GetType().Name; }

        private static Image ImagenDe(Control c)
        {
            if (c is PictureBox) return ((PictureBox)c).Image;
            if (c is Guna2Button) return ((Guna2Button)c).Image;
            return c.BackgroundImage;
        }

        private static void PonerImagen(Control c, Image im)
        {
            PictureBox p = c as PictureBox;
            if (p != null)
            {
                p.Image = im;
                if (Deformada(p))
                    p.SizeMode = PictureBoxSizeMode.Zoom;
                return;
            }
            Guna2Button b = c as Guna2Button;
            if (b != null)
            {
                b.Image = im;
                // Texto con espacio reservado a la izquierda ("     Editar"):
                // el ícono ocupa ese espacio, separado del texto.
                if (b.Text.StartsWith("   "))
                {
                    b.ImageAlign = HorizontalAlignment.Left;
                    b.ImageOffset = new Point(8, 0);
                    b.ImageSize = new Size(22, 22);
                }
                return;
            }
            c.BackgroundImage = im;
        }

        // ======================================================
        // ÍCONOS SOBRE BOTONES: centrados y sin robar el clic
        // ======================================================
        // Para cada imagen colocada encima de un botón (mismo contenedor):
        //  * se muestra sin deformarse (Zoom);
        //  * si está descentrada en vertical (más de 2 px), se centra;
        //  * si no tiene su propio evento Click, el clic llega al botón.
        // No cambia el tamaño ni la posición de los botones.
        private static void AlinearIconosDeBotones(Form f)
        {
            Recorrer(f, c =>
            {
                PictureBox p = c as PictureBox;
                if (p == null || p.Parent == null || !Visible(p))
                    return;
                Control boton = p.Parent.Controls.Cast<Control>().FirstOrDefault(x => x != p && x is Guna2Button && Visible(x) &&
                    x.Bounds.Contains(new Point(p.Left + p.Width / 2, p.Top + p.Height / 2)) && p.Width < x.Width * 0.6);
                if (boton == null || TieneClic(p))
                    return;                                   // con clic propio (menú lateral, cerrar): ya está resuelto

                if (Deformada(p))
                {
                    p.SizeMode = PictureBoxSizeMode.Zoom;
                    // El espacio sobrante toma el color del botón (sin franjas blancas).
                    Guna2Button gb = (Guna2Button)boton;
                    if (p.BackColor.A == 255 && gb.FillColor.A == 255)
                        p.BackColor = gb.FillColor;
                }
                // Solo los íconos a la izquierda de un texto propio del botón; en los
                // botones con ícono arriba y texto abajo, el ícono va arriba a propósito.
                bool alLado = boton.Text.Trim().Length > 0 && p.Left + p.Width / 2 < boton.Left + boton.Width / 2;
                int arriba = boton.Top + (boton.Height - p.Height) / 2;
                if (alLado && Math.Abs(p.Top - arriba) > 2 && p.Height <= boton.Height)
                    p.Top = arriba;

                IButtonControl ib = (IButtonControl)boton;
                p.Click += (s, e) => { if (boton.Enabled && Visible(boton)) ib.PerformClick(); };
                p.Cursor = boton.Cursor;
            });
        }

        // Estirada más de un 15 % respecto de su proporción (si no, se deja como
        // está: cambiar el modo mostraría el fondo del cuadro arriba/abajo).
        private static bool Deformada(PictureBox p)
        {
            if (p.SizeMode != PictureBoxSizeMode.StretchImage || p.Image == null || p.Width < 2 || p.Height < 2)
                return false;
            double caja = (double)p.Width / p.Height, imagen = (double)p.Image.Width / p.Image.Height;
            return Math.Abs(caja / imagen - 1.0) > 0.15;
        }

        private static readonly MethodInfo getState = typeof(Control).GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly object claveClick = typeof(Control).GetField("EventClick", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        private static readonly PropertyInfo eventos = typeof(Component).GetProperty("Events", BindingFlags.Instance | BindingFlags.NonPublic);

        // Visible "propio" del control (Control.Visible es falso mientras la ventana no se muestra).
        private static bool Visible(Control c)
        {
            try { return (bool)getState.Invoke(c, new object[] { 2 }); }
            catch { return c.Visible; }
        }

        private static bool TieneClic(Control c)
        {
            EventHandlerList lista = (EventHandlerList)eventos.GetValue(c, null);
            return lista[claveClick] != null;
        }

        private static void Recorrer(Control raiz, Action<Control> accion)
        {
            foreach (Control c in raiz.Controls)
            {
                accion(c);
                Recorrer(c, accion);
            }
        }

        // ======================================================
        // PICTOGRAMAS: silueta de un ícono, en un color
        // ======================================================
        // Separa el dibujo del fondo (el color de las esquinas) y lo pinta del
        // color pedido sobre fondo transparente. Sirve para reutilizar los
        // íconos del sistema con el color de cada tarjeta.
        public static Bitmap Pictograma(Image fuente, Color color)
        {
            using (Bitmap o = new Bitmap(fuente))
            {
                // Distancia de color al fondo (no solo brillo: un dibujo amarillo
                // sobre amarillo claro también se separa bien).
                Color fondo = o.GetPixel(1, 1);
                double maximo = 1;
                for (int y = 0; y < o.Height; y += 2)
                    for (int x = 0; x < o.Width; x += 2)
                        maximo = Math.Max(maximo, Distancia(o.GetPixel(x, y), fondo));
                Bitmap r = new Bitmap(o.Width, o.Height, PixelFormat.Format32bppArgb);
                for (int y = 0; y < o.Height; y++)
                    for (int x = 0; x < o.Width; x++)
                    {
                        Color c = o.GetPixel(x, y);
                        double a = Distancia(c, fondo) / maximo;
                        a = Math.Max(0.0, Math.Min(1.0, (a - 0.2) / 0.5)) * c.A / 255.0;
                        r.SetPixel(x, y, Color.FromArgb((int)(a * 255), color));
                    }
                return Recortar(r);
            }
        }

        // Ajusta el pictograma a su dibujo con un margen uniforme del 8 %: así
        // todos los íconos de un mismo grupo se ven del mismo tamaño.
        private static Bitmap Recortar(Bitmap b)
        {
            int x0 = b.Width, y0 = b.Height, x1 = -1, y1 = -1;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    if (b.GetPixel(x, y).A > 24)
                    {
                        if (x < x0) x0 = x; if (y < y0) y0 = y;
                        if (x > x1) x1 = x; if (y > y1) y1 = y;
                    }
            if (x1 < 0) return b;
            int w = x1 - x0 + 1, h = y1 - y0 + 1, lado = Math.Max(w, h), margen = Math.Max(2, lado * 8 / 100);
            Bitmap r = new Bitmap(lado + 2 * margen, lado + 2 * margen, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(r))
                g.DrawImage(b, new Rectangle(margen + (lado - w) / 2, margen + (lado - h) / 2, w, h), new Rectangle(x0, y0, w, h), GraphicsUnit.Pixel);
            b.Dispose();
            return r;
        }

        // Color del dibujo de un ícono (el más alejado del fondo).
        public static Color ColorPictograma(Image fuente)
        {
            using (Bitmap o = new Bitmap(fuente))
            {
                Color fondo = o.GetPixel(1, 1), mejor = Color.Black;
                double distancia = -1;
                for (int y = 0; y < o.Height; y += 3)
                    for (int x = 0; x < o.Width; x += 3)
                    {
                        Color c = o.GetPixel(x, y);
                        double d = Math.Pow(c.R - fondo.R, 2) + Math.Pow(c.G - fondo.G, 2) + Math.Pow(c.B - fondo.B, 2);
                        if (c.A > 200 && d > distancia) { distancia = d; mejor = c; }
                    }
                return mejor;
            }
        }

        private static double Distancia(Color a, Color b)
        {
            return Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
        }
    }
}
