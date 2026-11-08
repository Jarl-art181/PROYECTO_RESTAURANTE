using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // ESTILO COMÚN DE BOTONES (todas las ventanas)
    // ----------------------------------------------------------
    // Cada botón con texto recibe el ícono de SU FUNCIÓN (reconocida por
    // el texto: Guardar, Editar, Nuevo..., no por el nombre del control):
    //  * ícono PNG del juego del sistema (Resources\Iconos), pintado del
    //    color del texto, a la izquierda y centrado junto con el texto;
    //  * la imagen que el diseño tenía encima del botón se oculta (dejaba
    //    fondos y rectángulos); su evento sigue en el botón;
    //  * colores por acción, como en Inventario: Guardar verde, Eliminar
    //    y Desactivar rojo, Registrar entrada azul, Cancelar gris; el
    //    texto pasa a blanco o negro según el fondo.
    // Personalizable: Imagenes\Botones\<icono>.png reemplaza el ícono (se
    // muestra con sus propios colores). No cambia tamaños ni posiciones.
    // ==========================================================
    internal static class EstiloBotones
    {
        internal sealed class Regla
        {
            public string Icono;           // nombre del archivo (sin extensión)
            public Color? Fondo;           // color por acción (null = el actual)
            public Func<Image> Respaldo;   // si no hay PNG del sistema (pictogramas propios)
        }

        public static readonly Color Verde = Color.FromArgb(34, 177, 76);
        public static readonly Color Rojo = Color.FromArgb(229, 57, 53);
        public static readonly Color Azul = Color.FromArgb(30, 136, 229);
        public static readonly Color Gris = Color.FromArgb(222, 222, 222);

        // Texto del botón (sin tildes, minúsculas) -> regla.
        private static readonly List<KeyValuePair<Func<string, bool>, Regla>> reglas = CrearReglas();

        private static List<KeyValuePair<Func<string, bool>, Regla>> CrearReglas()
        {
            var r = new List<KeyValuePair<Func<string, bool>, Regla>>();
            Action<Func<string, bool>, string, Color?> a = (cond, icono, fondo) =>
                r.Add(new KeyValuePair<Func<string, bool>, Regla>(cond, new Regla { Icono = icono, Fondo = fondo }));
            Func<string, Func<string, bool>> es = t => x => x == t;
            Func<string, Func<string, bool>> empieza = t => x => x.StartsWith(t);
            Type pedidos = typeof(FRMS_PEDIDOS);
            Action<string, string, string> picto = (texto, icono, control) =>
                r.Add(new KeyValuePair<Func<string, bool>, Regla>(es(texto), new Regla
                {
                    Icono = icono,
                    Respaldo = () => Imagenes.SiluetaDe(pedidos, control)
                }));

            a(es("nueva reserva"), "nueva_reserva", null);
            a(empieza("nuevo"), "nuevo", null);
            a(empieza("nueva"), "nuevo", null);
            a(es("guardar"), "guardar", Verde);
            a(es("editar"), "editar", null);
            a(es("eliminar"), "eliminar", Rojo);
            a(es("reactivar"), "visto", Verde);
            a(es("desactivar"), "desactivar", Rojo);
            a(es("buscar"), "buscar", null);
            a(es("limpiar"), "limpiar", null);
            a(es("cancelar"), "cancelar", Gris);
            a(es("imprimir"), "imprimir", null);
            a(es("cobrar"), "cobrar", null);
            a(es("vista previa"), "vista_previa", null);
            a(es("generar recibo"), "recibo", null);
            a(es("reimprimir recibo"), "imprimir", null);
            a(es("reservar"), "reservar", null);
            a(es("registrar entrada"), "entrada", Azul);
            a(es("registrar salida"), "salida", Rojo);
            a(es("detalle pedido"), "detalle", null);
            a(es("liberar mesa"), "liberar", null);
            a(es("cambiar imagen"), "imagen", null);
            a(es("calcular"), "calcular", null);
            a(es("generar"), "generar", null);
            a(es("excel"), "excel", null);
            a(es("pdf"), "pdf", null);
            a(es("gestionar"), "etiqueta", null);
            // Filtros
            a(es("todos"), "todos", null);
            a(es("todas"), "lista", null);
            a(es("libres"), "visto", null);
            a(es("ocupada"), "persona", null);
            a(es("reservada"), "reservar", null);
            a(es("vigentes"), "visto", null);
            a(es("programadas"), "reloj", null);
            a(es("vencidas"), "historial", null);
            a(es("inactivas"), "inactivo", null);
            a(x => x == "hoy" || x == "esta semana" || x == "este mes" || x == "rango", "calendario", null);
            picto("platos", "categoria_platos", "guna2PictureBox28");
            picto("agregados", "categoria_agregados", "guna2PictureBox29");
            picto("bebidas", "categoria_bebidas", "guna2PictureBox30");
            picto("salon", "tipo_salon", "guna2PictureBox25");
            picto("delivery", "tipo_delivery", "guna2PictureBox26");
            picto("recojo", "tipo_recojo", "guna2PictureBox27");
            // Lista de reportes
            a(es("ventas por periodo"), "grafico", null);
            a(es("ventas por canal"), "actividad", null);
            a(es("ingresos por metodo de pago"), "tarjeta", null);
            a(es("pagos mixtos"), "banco", null);
            a(es("pedidos por estado"), "visto", null);
            a(es("productos mas vendidos"), "plato", null);
            a(es("cobros por empleado"), "persona", null);
            a(es("promociones y descuentos"), "etiqueta", null);
            a(es("clientes frecuentes"), "personas", null);
            a(es("reservas"), "calendario", null);
            return r;
        }

        // Íconos usados (para la guía): nombre -> textos de botón.
        public static IEnumerable<string> NombresIconos { get { return reglas.Select(x => x.Value.Icono).Distinct(); } }

        public static string Normalizar(string texto)
        {
            string s = (texto ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder b = new StringBuilder();
            foreach (char c in s)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) b.Append(c);
            return string.Join(" ", b.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static Regla Buscar(string texto)
        {
            string t = Normalizar(texto);
            if (t.Length == 0) return null;
            foreach (var r in reglas)
                if (r.Key(t)) return r.Value;
            return null;
        }

        // ------------------------------------------------------
        // APLICAR A UNA VENTANA (antes de la adaptación de tamaño)
        // ------------------------------------------------------
        public static void Aplicar(Form f)
        {
            if (f is INICIAR_SESIÓN)
                return;                                   // diseño propio, ya aprobado
            foreach (Guna2Button b in Todos(f).OfType<Guna2Button>().ToList())
            {
                if (b.Parent == null || b.Parent.Controls.ContainsKey("ButtonINICIO"))
                    continue;                             // menú lateral
                Regla r = Buscar(b.Text);
                if (r == null)
                    continue;
                try { Estilizar(b, r); }
                catch (Exception ex) { Registro.Error("Estilo", ex); }
            }
            BuscadoresConLupa(f);
            TitulosDeSeccion(f);
            IconosDeTarjetas(f);
        }

        // ------------------------------------------------------
        // TARJETAS: íconos sin el recuadro de otro color que traían
        // ------------------------------------------------------
        // Si el fondo propio del ícono no coincide con el color de la tarjeta,
        // se deja solo el dibujo (con su mismo color) sobre fondo transparente.
        // Se crea una vez por ventana y control (compartido entre aperturas).
        private static void IconosDeTarjetas(Form f)
        {
            foreach (PictureBox p in Todos(f).OfType<PictureBox>().ToList())
            {
                Guna2Panel tarjeta = p.Parent as Guna2Panel;
                if (tarjeta == null || p.Image == null || !Imagenes.VisiblePropio(p) || p.Width > 160 || p.Height > 160 ||
                    tarjeta.Width > 420 || tarjeta.Height > 260 || tarjeta.FillColor.A < 255)
                    continue;
                // Ícono sobre un botón (Salón, Delivery, Recojo, mesas...): se deja como
                // está. Un cuadro transparente mostraría el fondo del panel, no el botón.
                Point centro = new Point(p.Left + p.Width / 2, p.Top + p.Height / 2);
                if (tarjeta.Controls.OfType<Guna2Button>().Any(b => b.Bounds.Contains(centro)))
                    continue;
                // El cuadro de la imagen sin color propio (era el "recuadro" en varias tarjetas).
                if (p.BackColor != Color.Transparent && Distancia(p.BackColor, tarjeta.FillColor) > 0)
                    p.BackColor = Color.Transparent;
                Guna2PictureBox cuadro = p as Guna2PictureBox;
                if (cuadro != null && cuadro.FillColor != Color.Transparent)
                    cuadro.FillColor = Color.Transparent;
                Color esquina;
                try { using (Bitmap b = new Bitmap(p.Image)) esquina = b.GetPixel(1, 1); }
                catch { continue; }
                if (esquina.A < 200 || Distancia(esquina, tarjeta.FillColor) < 3 || p.Name.StartsWith("picMesa"))
                    continue;
                Image original = p.Image;
                Image limpio = Imagenes.Compartir("tarjeta." + f.GetType().Name + "." + p.Name,
                    () => Imagenes.Pictograma(original, Imagenes.ColorPictograma(original)));
                if (limpio == null) continue;
                p.Image = limpio;
                p.BackColor = Color.Transparent;
                Guna2PictureBox gp = p as Guna2PictureBox;
                if (gp != null) gp.FillColor = Color.Transparent;
                if (p.SizeMode == PictureBoxSizeMode.StretchImage) p.SizeMode = PictureBoxSizeMode.Zoom;
                if (!ReferenceEquals(original, limpio)) original.Dispose();
            }
        }

        private static double Distancia(Color a, Color b)
        {
            return Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
        }

        // ------------------------------------------------------
        // TÍTULOS DE SECCIÓN (barras amarillas): ícono a la izquierda del título
        // ------------------------------------------------------
        private static readonly string[][] Titulos =
        {
            new[] { "insumos de cocina", "caja" }, new[] { "detalle del insumo", "detalle" },
            new[] { "movimientos recientes", "historial" }, new[] { "detalle del producto", "detalle" },
            new[] { "lista de promociones", "etiqueta" }, new[] { "detalle de la promocion", "detalle" },
            new[] { "detalle de la mesa", "detalle" }, new[] { "informacion del empleado", "persona" }
        };

        private static void TitulosDeSeccion(Form f)
        {
            foreach (Guna2Panel barra in Todos(f).OfType<Guna2Panel>().ToList())
            {
                Color c = barra.FillColor;
                if (!(c.R > 230 && c.G > 180 && c.B < 90) || barra.Height < 26 || barra.Height > 56 || barra.Width < 160)
                    continue;
                var hijos = barra.Controls.Cast<Control>().Where(Imagenes.VisiblePropio).ToList();
                if (hijos.Count != 1 || hijos[0] is PictureBox || hijos[0] is ButtonBase || hijos[0] is Guna2Button)
                    continue;
                Control titulo = hijos[0];
                string t = Normalizar(titulo.Text);
                string[] regla = Titulos.FirstOrDefault(r => t.StartsWith(r[0]));
                if (regla == null)
                    continue;
                int lado = Math.Max(16, (int)Math.Round(barra.Height * 0.58));
                int x = titulo.Left - lado - 6;
                if (x < 6)
                {
                    titulo.Left = 6 + lado + 6;               // hace lugar al ícono
                    x = 6;
                }
                PictureBox icono = new PictureBox
                {
                    Name = "picTitulo_" + regla[1],
                    Bounds = new Rectangle(x, (barra.Height - lado) / 2, lado, lado),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent,
                    Image = Icono(regla[1], Color.Black)
                };
                barra.Controls.Add(icono);
                icono.BringToFront();
            }
        }

        // Botón creado por código después de preparar la ventana.
        public static void AplicarA(Guna2Button b)
        {
            Regla r = b == null ? null : Buscar(b.Text);
            if (r != null)
                try { Estilizar(b, r); } catch (Exception ex) { Registro.Error("Estilo", ex); }
        }

        private static void Estilizar(Guna2Button b, Regla r)
        {
            // Imagen del diseño encima del botón: se oculta (el botón ya lleva su ícono).
            PictureBox encima = b.Parent.Controls.OfType<PictureBox>().FirstOrDefault(p => p.Bounds.IntersectsWith(b.Bounds) &&
                p.Width < b.Width * 0.6 && b.Bounds.Contains(new Point(p.Left + p.Width / 2, p.Top + p.Height / 2)));
            if (encima != null)
                encima.Visible = false;

            // Texto sin los espacios que reservaban lugar a la imagen superpuesta.
            QuitarEspacios(b);
            b.TextAlign = HorizontalAlignment.Center;
            b.TextOffset = Point.Empty;
            int lado = Math.Max(16, Math.Min(28, (int)Math.Round(b.Height * 0.46)));
            b.ImageSize = new Size(lado, lado);
            b.ImageAlign = HorizontalAlignment.Center;    // ícono y texto juntos, centrados
            b.ImageOffset = new Point(-3, 0);

            Color original = b.FillColor;
            Pintar(b, r, original);
            // Si el texto cambia en uso (Editar/Cancelar, Eliminar/Reactivar),
            // el ícono y el color siguen a la nueva función.
            b.TextChanged += (s, e) =>
            {
                QuitarEspacios(b);
                Regla nueva = Buscar(b.Text);
                if (nueva != null) Pintar(b, nueva, original);
            };
        }

        private static void Pintar(Guna2Button b, Regla r, Color original)
        {
            b.FillColor = r.Fondo ?? original;
            if (r.Fondo.HasValue)
                b.HoverState.FillColor = Color.Empty;     // Guna oscurece el color al pasar el mouse
            Color texto = Oscuro(b.FillColor) ? Color.White : Color.Black;
            b.ForeColor = texto;
            Image icono = Icono(r, texto);
            if (icono != null)
                b.Image = icono;
        }

        private static void QuitarEspacios(Guna2Button b)
        {
            string t = b.Text ?? "";
            string limpio = t.Trim();
            if (limpio != t && limpio.Length > 0)
                b.Text = limpio;
        }

        private static bool Oscuro(Color c)
        {
            if (c.A < 128) return false;
            double luz = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            return luz < 0.62;
        }

        // ------------------------------------------------------
        // ÍCONOS: personalizado > juego del sistema > pictograma propio
        // ------------------------------------------------------
        private static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

        public static Image Icono(Regla r, Color color)
        {
            string propia = Imagenes.Buscar(Imagenes.Botones, r.Icono);
            if (propia != null)
            {
                string clave = "archivo|" + propia + "|" + Imagenes.Version(propia);
                Image im;
                lock (cache)
                {
                    if (!cache.TryGetValue(clave, out im))
                    {
                        im = Imagenes.Cargar(propia, new Size(96, 96));
                        cache[clave] = im;
                    }
                }
                if (im != null) return im;
            }
            Image baseIcono = Imagenes.IconoSistema(r.Icono) ?? (r.Respaldo != null ? r.Respaldo() : null);
            return baseIcono == null ? null : Teñido(r.Icono, baseIcono, color);
        }

        public static Image Icono(string nombre, Color color)
        {
            return Icono(new Regla { Icono = nombre }, color);
        }

        // Mismo dibujo en otro color (conserva la transparencia). Compartido.
        private static Image Teñido(string nombre, Image fuente, Color color)
        {
            string clave = nombre + "|" + color.ToArgb();
            lock (cache)
            {
                Image im;
                if (cache.TryGetValue(clave, out im)) return im;
                Bitmap b = new Bitmap(fuente.Width, fuente.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(b))
                using (ImageAttributes at = new ImageAttributes())
                {
                    at.SetColorMatrix(new ColorMatrix(new[]
                    {
                        new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, 0, 0 },
                        new float[] { 0, 0, 0, 1, 0 },
                        new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 }
                    }));
                    g.DrawImage(fuente, new Rectangle(0, 0, b.Width, b.Height), 0, 0, fuente.Width, fuente.Height, GraphicsUnit.Pixel, at);
                }
                cache[clave] = b;
                return b;
            }
        }

        // ------------------------------------------------------
        // CUADROS DE BÚSQUEDA: lupa dentro del cuadro (si no tiene una al lado)
        // ------------------------------------------------------
        private static void BuscadoresConLupa(Form f)
        {
            foreach (Guna2TextBox t in Todos(f).OfType<Guna2TextBox>())
            {
                if (!Normalizar(t.PlaceholderText).StartsWith("buscar") || t.IconRight != null || t.IconLeft != null || t.Parent == null)
                    continue;
                bool lupaAlLado = t.Parent.Controls.Cast<Control>().Any(c => c != t && Imagenes.VisiblePropio(c) &&
                    (c is PictureBox || (c is Guna2Button && Normalizar(c.Text) == "buscar")) &&
                    Math.Abs((c.Top + c.Height / 2) - (t.Top + t.Height / 2)) < t.Height &&
                    (Math.Abs(c.Right - t.Left) < 70 || Math.Abs(c.Left - t.Right) < 70));
                if (lupaAlLado)
                    continue;
                int lado = Math.Max(14, Math.Min(22, t.Height / 2));
                t.IconRight = Icono("buscar", Color.FromArgb(90, 90, 90));
                t.IconRightSize = new Size(lado, lado);
                t.IconRightOffset = new Point(6, 0);
            }
        }

        private static IEnumerable<Control> Todos(Control c)
        {
            foreach (Control h in c.Controls)
            {
                yield return h;
                foreach (Control x in Todos(h)) yield return x;
            }
        }
    }
}
