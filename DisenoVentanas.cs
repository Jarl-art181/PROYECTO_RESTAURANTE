using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // DISEÑO COMÚN DE LAS VENTANAS PRINCIPALES
    // ----------------------------------------------------------
    // Todas las ventanas principales se diseñaron sobre un lienzo
    // de 1540 x 941 (barra lateral negra + encabezado + contenido).
    // Adaptar() conserva ese diseño en cualquier pantalla:
    //  * escala proporcionalmente posiciones, tamaños y letras al
    //    espacio disponible (1366x768, 1920x1080, escalado de Windows);
    //  * la barra lateral ocupa todo el alto y los encabezados todo
    //    el ancho (sus elementos de la derecha siguen a la derecha);
    //  * el contenido sobrante se centra;
    //  * si la pantalla es tan pequeña que la letra quedaría ilegible,
    //    se fija una escala mínima y se habilitan barras de desplazamiento
    //    (ningún botón desaparece).
    // Se calcula siempre desde los valores originales (sin acumular
    // redondeos) y no cambia nombres, eventos ni datos de los controles.
    // ==========================================================
    internal static class DisenoVentanas
    {
        public static readonly Size Lienzo = new Size(1540, 941);
        public const float EscalaMinima = 0.75f;
        public const float EscalaMaxima = 1.50f;

        private sealed class Original
        {
            public Rectangle Limites;
            public Font Letra;
            public int AltoElemento;
            public Dictionary<string, object> Propiedades;
        }

        private sealed class TablaOriginal
        {
            public Font Celdas, Encabezados, Filas, Alternas;
            public int AltoEncabezado, AltoFila;
            public Dictionary<DataGridViewColumn, int> Anchos = new Dictionary<DataGridViewColumn, int>();
            public Dictionary<DataGridViewColumn, int> Minimos = new Dictionary<DataGridViewColumn, int>();
        }

        private sealed class Estado
        {
            public readonly Dictionary<Control, Original> Controles = new Dictionary<Control, Original>();
            public readonly Dictionary<DataGridView, TablaOriginal> Tablas = new Dictionary<DataGridView, TablaOriginal>();
            public bool Mostrado;
            public Size Ultimo;
            public float Escala = 1f;
            public Size LienzoReal = Lienzo;
            public Size? LienzoFijo;          // ventanas modales con su propio tamaño de diseño
        }

        private static readonly ConditionalWeakTable<Form, Estado> estados = new ConditionalWeakTable<Form, Estado>();

        // Letras compartidas: un número acotado de fuentes para todas las ventanas.
        private static readonly Dictionary<string, Font> letras = new Dictionary<string, Font>();

        private static readonly string[] PropiedadesGuna = { "ImageSize", "ImageOffset", "TextOffset", "BorderRadius" };

        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int indice);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr ventana);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr ventana, IntPtr hdc);

        private static float factorWindows;

        // Escalado de Windows (1 = 100 %, 1,25 = 125 %): píxeles físicos del
        // escritorio / píxeles que ve esta aplicación.
        public static float FactorWindows()
        {
            if (factorWindows > 0)
                return factorWindows;
            float f = 1f;
            try
            {
                IntPtr dc = GetDC(IntPtr.Zero);
                try
                {
                    int fisico = GetDeviceCaps(dc, 118);   // DESKTOPHORZRES
                    int logico = GetDeviceCaps(dc, 8);     // HORZRES
                    if (fisico > 0 && logico > 0) f = fisico / (float)logico;
                }
                finally { ReleaseDC(IntPtr.Zero, dc); }
            }
            catch { f = 1f; }
            factorWindows = Math.Max(1f, Math.Min(3f, f));
            return factorWindows;
        }

        public static float Escala(Form f)
        {
            Estado e;
            return f != null && estados.TryGetValue(f, out e) ? e.Escala : 1f;
        }

        public static void Adaptar(Form f)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || f == null)
                return;

            Estado e = estados.GetValue(f, x => new Estado());

            // Antes de que la ventana se maximice: un anclaje abajo o a la derecha
            // estiraría el control ANTES de registrar su tamaño de diseño.
            Desanclar(f);

            // Menú lateral común: todos los botones conectados y según el cargo.
            MenuLateral.Conectar(f);
            // Imágenes por función (reemplazables desde la carpeta Imagenes)
            // e íconos sobre botones centrados; antes de registrar el diseño.
            Imagenes.Preparar(f);
            // Solo desde que la ventana se muestra: así los Load/Shown propios
            // ya terminaron de preparar sus controles en coordenadas de diseño.
            f.Shown += (s, a) => { e.Mostrado = true; Aplicar(f, e); };
            f.VisibleChanged += (s, a) =>
            {
                if (!f.Visible) return;
                if (!f.TopLevel) e.Mostrado = true;
                Aplicar(f, e);
            };
            f.Resize += (s, a) => Aplicar(f, e);
        }

        // Ventana (modal) diseñada con otro tamaño que el lienzo común:
        // se escala respecto de su propio tamaño de diseño.
        public static void Adaptar(Form f, Size lienzo)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || f == null)
                return;
            estados.GetValue(f, x => new Estado()).LienzoFijo = lienzo;
            Adaptar(f);
        }

        private static void Aplicar(Form f, Estado e)
        {
            if (!e.Mostrado || f.IsDisposed || !f.IsHandleCreated || f.WindowState == FormWindowState.Minimized)
                return;

            Size cs = f.ClientSize;
            if (cs.Width < 300 || cs.Height < 200 || cs == e.Ultimo)
                return;

            if (e.Controles.Count == 0)
            {
                Capturar(f, e);
                // Lo que se quita después (tarjetas recreadas) no queda retenido.
                f.ControlRemoved += (s, a) => Olvidar(a.Control, e);
                e.LienzoReal = e.LienzoFijo ?? MedirLienzo(f, e);
            }
            else
            {
                List<Control> muertos = new List<Control>();
                foreach (Control c in e.Controles.Keys)
                    if (c.IsDisposed) muertos.Add(c);
                foreach (Control c in muertos)
                    e.Controles.Remove(c);
            }

            // Algunas ventanas se diseñaron un poco más grandes que el lienzo
            // (botones bajo 941 px): se mide el contenido real para que nada quede fuera.
            Size lienzo = e.LienzoReal;
            float kx = cs.Width / (float)lienzo.Width, ky = cs.Height / (float)lienzo.Height;
            float justa = Math.Min(kx, ky);
            // La aplicación no es "DPI aware": con escalado de Windows (125 %,
            // 150 %) el sistema ya amplía la ventana. El mínimo legible se mide
            // en pantalla: 0,75 visible equivale a 0,6 lógico al 125 %.
            float minima = EscalaMinima / FactorWindows();
            float k = Math.Max(minima, Math.Min(EscalaMaxima, justa));
            bool desplazar = justa < minima;

            e.Ultimo = cs;
            e.Escala = k;

            int ancho = Redondear(lienzo.Width * k), alto = Redondear(lienzo.Height * k);
            int areaAncho = Math.Max(cs.Width, ancho), areaAlto = Math.Max(cs.Height, alto);
            int sobrante = Math.Max(0, cs.Width - ancho);

            f.SuspendLayout();
            try
            {
                if (desplazar)
                {
                    f.AutoScroll = true;
                    f.AutoScrollMinSize = new Size(ancho, alto);
                }
                else if (f.AutoScroll)
                {
                    f.AutoScrollMinSize = Size.Empty;
                    f.AutoScroll = false;
                }

                foreach (Control c in f.Controls)
                {
                    Original o;
                    if (!e.Controles.TryGetValue(c, out o))
                        continue;

                    Rectangle r = Escalar(o.Limites, k);
                    bool fondo = o.Limites.Width >= Lienzo.Width * 0.95 && o.Limites.Height >= Lienzo.Height * 0.9;
                    bool lateral = !fondo && o.Limites.X <= 10 && o.Limites.Height >= Lienzo.Height - 60;
                    bool encabezado = !fondo && !lateral && o.Limites.Y < 100 && o.Limites.Right >= Lienzo.Width - 20;

                    if (fondo)
                    {
                        // Imagen o panel de fondo: cubre toda el área visible.
                        r = new Rectangle(r.X, r.Y, areaAncho - r.X + Redondear((o.Limites.Right - Lienzo.Width) * k),
                                          areaAlto - r.Y + Redondear((o.Limites.Bottom - Lienzo.Height) * k));
                    }
                    else if (lateral)
                        r.Height = Math.Max(r.Height, areaAlto - r.Y + Redondear((o.Limites.Bottom - Lienzo.Height) * k));
                    else if (encabezado)
                        r.Width = Math.Max(r.Width, areaAncho - r.X + Redondear((o.Limites.Right - Lienzo.Width) * k));
                    else
                        r.X += sobrante / 2;

                    Poner(c, o, r, k, e);
                    Hijos(c, o, r, k, e, encabezado);
                }
            }
            finally
            {
                f.ResumeLayout(true);
            }

            // Íconos dibujados encima de un botón: el texto no debe quedar debajo.
            foreach (Control c in new List<Control>(e.Controles.Keys))
            {
                Guna2Button b = c as Guna2Button;
                if (b != null && !b.IsDisposed && b.Parent != null && !string.IsNullOrEmpty(b.Text))
                    SepararTextoDeIcono(b);
            }
        }

        // Guna centra el texto completo (con los espacios que el diseño dejó
        // para el ícono). Al reducirse la letra, los espacios se achican más
        // que el ícono y este tapa el comienzo del texto: se desplaza el texto
        // (TextOffset, sin cambiar Text) y, si no cabe, se reduce la letra.
        private static void SepararTextoDeIcono(Guna2Button b)
        {
            PictureBox icono = null;
            foreach (Control h in b.Parent.Controls)
            {
                PictureBox p = h as PictureBox;
                if (p != null && p.Visible && p.Bounds.IntersectsWith(b.Bounds) && p.Left < b.Left + b.Width / 2 &&
                    p.Width < b.Width / 2)
                {
                    icono = p;
                    break;
                }
            }
            if (icono == null)
                return;

            string texto = b.Text;
            string nucleo = texto.TrimStart();
            using (Graphics g = b.CreateGraphics())
            {
                for (int intento = 0; intento < 10; intento++)
                {
                    float completo = TextRenderer.MeasureText(g, texto, b.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                    float visible = TextRenderer.MeasureText(g, nucleo, b.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                    float inicio = b.Left + (b.Width - completo) / 2f + b.TextOffset.X + (completo - visible);
                    float falta = icono.Right + 4 - inicio;
                    if (falta <= 0)
                        return;
                    float fin = inicio + falta + visible;
                    if (fin <= b.Right - 6 || b.Font.Size <= 6.5f)
                    {
                        b.TextOffset = new Point(b.TextOffset.X + (int)Math.Ceiling(falta), b.TextOffset.Y);
                        return;
                    }
                    b.Font = Letra(b.Font, (b.Font.Size - 0.25f) / b.Font.Size);
                }
            }
        }

        private static void Hijos(Control padre, Original op, Rectangle nuevoPadre, float k, Estado e, bool estirado)
        {
            foreach (Control c in padre.Controls)
            {
                Original o;
                if (c.IsDisposed || !e.Controles.TryGetValue(c, out o))
                    continue;

                Rectangle r = Escalar(o.Limites, k);
                // En un encabezado estirado, lo que estaba a la derecha sigue a la
                // derecha y lo que lo ocupaba casi entero (banner) se estira con él.
                if (estirado && o.Limites.Width >= op.Limites.Width * 0.9)
                    r.Width = nuevoPadre.Width - r.X - Redondear((op.Limites.Width - o.Limites.Right) * k);
                else if (estirado && o.Limites.X > op.Limites.Width * 0.55)
                    r.X = nuevoPadre.Width - Redondear((op.Limites.Width - o.Limites.X) * k);

                Poner(c, o, r, k, e);
                Hijos(c, o, r, k, e, false);
            }
        }

        private static void Poner(Control c, Original o, Rectangle r, float k, Estado e)
        {
            if (o.Letra != null)
                c.Font = Letra(o.Letra, k);

            ComboBox combo = c as ComboBox;
            if (combo != null && o.AltoElemento > 0 && combo.DrawMode != DrawMode.Normal)
                combo.ItemHeight = Math.Max(12, Redondear(o.AltoElemento * k));

            if (o.Propiedades != null)
            {
                foreach (KeyValuePair<string, object> p in o.Propiedades)
                {
                    PropertyInfo pi = c.GetType().GetProperty(p.Key);
                    if (pi == null || !pi.CanWrite) continue;
                    object v = p.Value;
                    if (v is Size) v = new Size(Redondear(((Size)v).Width * k), Redondear(((Size)v).Height * k));
                    else if (v is Point) v = new Point(Redondear(((Point)v).X * k), Redondear(((Point)v).Y * k));
                    else if (v is int) v = Redondear((int)v * k);
                    try { pi.SetValue(c, v, null); } catch { }
                }
            }

            DataGridView tabla = c as DataGridView;
            if (tabla != null)
                Tabla(tabla, k, e);

            if (c.Dock != DockStyle.None)
                return;
            if (c.AutoSize)
                c.Location = r.Location;
            else
                c.Bounds = r;

            // Botones: el texto (con sus espacios para el ícono) debe caber en
            // una sola línea; si el redondeo de la letra lo impide, se reduce.
            if (c is Guna2Button || c is Button)
                AjustarTextoBoton(c);
        }

        private static void AjustarTextoBoton(Control b)
        {
            string texto = b.Text;
            if (string.IsNullOrEmpty(texto) || b.Width < 20)
                return;
            Font letra = b.Font;
            // Guna deja unos 10 px de margen a cada lado (medido: el texto
            // se parte con menos de 20 px libres).
            int disponible = b.Width - 20;
            using (Graphics g = b.CreateGraphics())
            {
                for (int i = 0; i < 12; i++)
                {
                    float gdi = TextRenderer.MeasureText(g, texto, letra, Size.Empty, TextFormatFlags.SingleLine).Width;
                    float gdiPlus = g.MeasureString(texto, letra).Width;
                    if (Math.Max(gdi, gdiPlus) <= disponible || letra.Size <= 6.5f)
                        break;
                    letra = Letra(letra, (letra.Size - 0.25f) / letra.Size);
                }
            }
            if (!ReferenceEquals(letra, b.Font))
                b.Font = letra;
        }

        private static void Tabla(DataGridView t, float k, Estado e)
        {
            TablaOriginal o;
            if (!e.Tablas.TryGetValue(t, out o))
                return;

            if (o.Celdas != null) t.DefaultCellStyle.Font = Letra(o.Celdas, k);
            if (o.Encabezados != null) t.ColumnHeadersDefaultCellStyle.Font = Letra(o.Encabezados, k);
            if (o.Filas != null) t.RowsDefaultCellStyle.Font = Letra(o.Filas, k);
            if (o.Alternas != null) t.AlternatingRowsDefaultCellStyle.Font = Letra(o.Alternas, k);

            if (t.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize)
                t.ColumnHeadersHeight = Math.Max(18, Redondear(o.AltoEncabezado * k));
            t.RowTemplate.Height = Math.Max(16, Redondear(o.AltoFila * k));
            if (t.AutoSizeRowsMode == DataGridViewAutoSizeRowsMode.None)
                foreach (DataGridViewRow fila in t.Rows)
                    fila.Height = t.RowTemplate.Height;

            foreach (KeyValuePair<DataGridViewColumn, int> c in o.Anchos)
            {
                if (c.Key.DataGridView != t) continue;
                int minimo;
                if (o.Minimos.TryGetValue(c.Key, out minimo))
                    c.Key.MinimumWidth = Math.Max(2, Redondear(minimo * k));
                if (c.Key.InheritedAutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                    c.Key.Width = Math.Max(c.Key.MinimumWidth, Redondear(c.Value * k));
            }

            AjustarColumnas(t);

            Guna2DataGridView guna = t as Guna2DataGridView;
            if (guna != null)
            {
                guna.ThemeStyle.HeaderStyle.Height = t.ColumnHeadersHeight;
                guna.ThemeStyle.RowsStyle.Height = t.RowTemplate.Height;
                if (o.Encabezados != null) guna.ThemeStyle.HeaderStyle.Font = t.ColumnHeadersDefaultCellStyle.Font;
                if (o.Celdas != null) guna.ThemeStyle.RowsStyle.Font = t.DefaultCellStyle.Font;
                if (o.Alternas != null) guna.ThemeStyle.AlternatingRowsStyle.Font = t.AlternatingRowsDefaultCellStyle.Font;
            }
        }

        // Las columnas deben caber en el ancho visible (con barra vertical):
        // el redondeo de la escala no debe crear una barra horizontal.
        private static void AjustarColumnas(DataGridView t)
        {
            int fijas = 0, minimos = 0;
            foreach (DataGridViewColumn col in t.Columns)
            {
                if (!col.Visible) continue;
                if (col.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.Fill) minimos += col.MinimumWidth;
                else fijas += col.Width;
            }
            int disponible = (int)((t.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 3) * 0.92f);
            if (disponible > 50 && fijas + minimos > disponible)
            {
                float factor = disponible / (float)(fijas + minimos);
                foreach (DataGridViewColumn col in t.Columns)
                {
                    if (!col.Visible) continue;
                    if (col.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
                        col.MinimumWidth = Math.Max(10, (int)(col.MinimumWidth * factor));
                    else
                    {
                        col.MinimumWidth = Math.Max(10, (int)(col.MinimumWidth * factor));
                        col.Width = Math.Max(col.MinimumWidth, (int)(col.Width * factor));
                    }
                }
            }
        }

        private static void Capturar(Control padre, Estado e)
        {
            foreach (Control c in padre.Controls)
            {
                Original o = new Original
                {
                    Limites = c.Bounds,
                    Letra = c.Font,
                    AltoElemento = c is ComboBox ? ((ComboBox)c).ItemHeight : 0
                };

                foreach (string nombre in PropiedadesGuna)
                {
                    PropertyInfo pi = c.GetType().GetProperty(nombre);
                    if (pi == null || !pi.CanRead || !pi.CanWrite) continue;
                    object v = pi.GetValue(c, null);
                    if (!(v is Size || v is Point || v is int)) continue;
                    if (o.Propiedades == null) o.Propiedades = new Dictionary<string, object>();
                    o.Propiedades[nombre] = v;
                }
                e.Controles[c] = o;
                // Las posiciones las calcula la adaptación: un anclaje a la derecha
                // o abajo haría que WinForms moviera o estirara además el control.
                if (c.Dock == DockStyle.None && c.Anchor != (AnchorStyles.Top | AnchorStyles.Left))
                    c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                if (c.HasChildren || c is FlowLayoutPanel)
                    c.ControlRemoved += (s, a) => Olvidar(a.Control, e);

                DataGridView t = c as DataGridView;
                if (t != null)
                {
                    TablaOriginal to = new TablaOriginal
                    {
                        Celdas = t.DefaultCellStyle.Font,
                        Encabezados = t.ColumnHeadersDefaultCellStyle.Font,
                        Filas = t.RowsDefaultCellStyle.Font,
                        Alternas = t.AlternatingRowsDefaultCellStyle.Font,
                        AltoEncabezado = t.ColumnHeadersHeight,
                        AltoFila = t.RowTemplate.Height
                    };
                    foreach (DataGridViewColumn col in t.Columns)
                    {
                        to.Anchos[col] = col.Width;
                        to.Minimos[col] = col.MinimumWidth;
                    }
                    e.Tablas[t] = to;

                    // Si todas las columnas reparten el ancho (Fill), nunca hace
                    // falta desplazarse a los lados: WinForms mostraba una barra
                    // horizontal por 1 o 2 px al aparecer la barra vertical.
                    bool todasFill = t.Columns.Count > 0;
                    foreach (DataGridViewColumn col in t.Columns)
                        if (col.Visible && col.InheritedAutoSizeMode != DataGridViewAutoSizeColumnMode.Fill) todasFill = false;
                    if (todasFill && t.ScrollBars == ScrollBars.Both)
                        t.ScrollBars = ScrollBars.Vertical;

                    // Columna creada por código después de adaptar (p. ej. el botón
                    // Eliminar de Pedidos, que copia el ancho ya escalado de la que
                    // reemplaza): no se vuelve a escalar; se registra en la escala
                    // actual y se reajusta para que todo quepa sin barra horizontal.
                    DataGridView tabla = t;
                    t.ColumnAdded += (s, a) =>
                    {
                        if (to.Anchos.ContainsKey(a.Column)) return;
                        float k = e.Escala <= 0 ? 1f : e.Escala;
                        to.Anchos[a.Column] = Redondear(a.Column.Width / k);
                        to.Minimos[a.Column] = Redondear(a.Column.MinimumWidth / k);
                        AjustarColumnas(tabla);
                    };
                    t.ColumnRemoved += (s, a) => { to.Anchos.Remove(a.Column); to.Minimos.Remove(a.Column); };
                    continue;
                }

                // Las tarjetas del diseñador dentro de un FlowLayoutPanel también se
                // escalan: las que se crean después por código copian su tamaño
                // (plantilla) y quedan proporcionadas. Las que se recrean en cada
                // actualización usan Escala()/Fuente() al construirse.
                if (c is UserControl && !(c is Form))
                    continue;

                Capturar(c, e);
            }
        }

        // Contenido (sin fondo, barra lateral ni encabezado) visto en el diseño.
        private static Size MedirLienzo(Form f, Estado e)
        {
            int derecha = Lienzo.Width, abajo = Lienzo.Height;
            foreach (Control c in f.Controls)
            {
                Original o;
                if (!e.Controles.TryGetValue(c, out o) || !c.Visible)
                    continue;
                Rectangle r = o.Limites;
                bool fondo = r.Width >= Lienzo.Width * 0.95 && r.Height >= Lienzo.Height * 0.9;
                bool lateral = r.X <= 10 && r.Height >= Lienzo.Height - 60;
                bool encabezado = r.Y < 100 && r.Right >= Lienzo.Width - 20;
                if (fondo || lateral || encabezado)
                    continue;
                derecha = Math.Max(derecha, Math.Min(r.Right + 6, Lienzo.Width + 120));
                abajo = Math.Max(abajo, Math.Min(r.Bottom + 6, Lienzo.Height + 120));
            }
            return new Size(derecha, abajo);
        }

        private static void Desanclar(Control padre)
        {
            foreach (Control c in padre.Controls)
            {
                if (c.Dock == DockStyle.None && c.Anchor != (AnchorStyles.Top | AnchorStyles.Left))
                    c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                if (!(c is UserControl))
                    Desanclar(c);
            }
        }

        private static void Olvidar(Control c, Estado e)
        {
            e.Controles.Remove(c);
            DataGridView t = c as DataGridView;
            if (t != null) e.Tablas.Remove(t);
            foreach (Control h in c.Controls)
                Olvidar(h, e);
        }

        // Letra escalada y compartida para lo que una ventana dibuja por
        // código (tarjetas que se recrean): no crea una fuente por tarjeta.
        public static Font Fuente(Form f, string familia, float tamano, FontStyle estilo)
        {
            float k = Escala(f);
            string clave = "base|" + familia + "|" + tamano + "|" + (int)estilo;
            Font baseFuente;
            lock (letras)
            {
                if (!letras.TryGetValue(clave, out baseFuente))
                {
                    baseFuente = new Font(familia, tamano, estilo);
                    letras[clave] = baseFuente;
                }
            }
            return Math.Abs(k - 1f) < 0.001f ? baseFuente : Letra(baseFuente, k);
        }

        // Medida de diseño convertida a la escala actual de la ventana.
        public static int Medida(Form f, int valor)
        {
            return Redondear(valor * Escala(f));
        }

        private static Font Letra(Font original, float k)
        {
            float tam = (float)Math.Round(original.Size * k * 4f) / 4f;
            if (tam < 6f) tam = 6f;
            string clave = original.FontFamily.Name + "|" + tam + "|" + (int)original.Style + "|" + (int)original.Unit;
            lock (letras)
            {
                Font f;
                if (!letras.TryGetValue(clave, out f))
                {
                    f = new Font(original.FontFamily, tam, original.Style, original.Unit, original.GdiCharSet);
                    letras[clave] = f;
                }
                return f;
            }
        }

        private static Rectangle Escalar(Rectangle r, float k)
        {
            return new Rectangle(Redondear(r.X * k), Redondear(r.Y * k), Redondear(r.Width * k), Redondear(r.Height * k));
        }

        private static int Redondear(float v)
        {
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }

        // ======================================================
        // ENCABEZADO: hora, fecha, usuario y cargo de la sesión
        // ------------------------------------------------------
        // Reemplaza los textos fijos del diseño ("Torrez_AD",
        // "Adminitrador", "Vier. 18 Abr 2025", "22:47:00").
        // conReloj = false en las ventanas que ya tienen su reloj.
        // ======================================================
        private static readonly string[] Dias = { "Dom.", "Lun.", "Mar.", "Mié.", "Jue.", "Vier.", "Sáb." };
        private static readonly string[] Meses = { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };

        public static void Encabezado(Form f, bool conReloj)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || f == null)
                return;

            Control hora = Buscar(f, "lblHora"), fecha = Buscar(f, "lblFecha");
            List<Control> usuario = new List<Control>(), cargo = new List<Control>(), rol = new List<Control>();
            Recorrer(f, c =>
            {
                string t = (c.Text ?? "").Trim();
                if (t == "Torrez_AD") usuario.Add(c);
                else if (t == "Adminitrador" || t == "Admintrador" || t == "Administrador" && c.Top < 60 && c.Left > 900) cargo.Add(c);
                else if (t == "ADMINISTRADOR" && c.Top < 60) rol.Add(c);
            });

            Action datos = () =>
            {
                // Igual que el Navegador: nombre corto (p. ej. "Ana Seminario"), no el usuario de la cuenta.
                string nombre = SesionActual.Iniciada ? (SesionActual.NombreCorto ?? SesionActual.Usuario) : "-";
                string cargoTexto = SesionActual.Iniciada ? (SesionActual.Cargo ?? "") : "";
                foreach (Control c in usuario) c.Text = nombre;
                foreach (Control c in cargo) c.Text = cargoTexto;
                foreach (Control c in rol) c.Text = cargoTexto.ToUpperInvariant();
            };
            Action reloj = () =>
            {
                DateTime ahora = DateTime.Now;
                if (hora != null) hora.Text = ahora.ToString("HH:mm:ss");
                if (fecha != null) fecha.Text = Dias[(int)ahora.DayOfWeek] + " " + ahora.Day.ToString("00") + " " + Meses[ahora.Month - 1] + " " + ahora.Year;
            };

            datos();
            if (conReloj) reloj();
            f.VisibleChanged += (s, a) => { if (f.Visible) { datos(); if (conReloj) reloj(); } };

            if (!conReloj || (hora == null && fecha == null))
                return;

            Timer temporizador = new Timer { Interval = 1000 };
            temporizador.Tick += (s, a) => { if (f.Visible) reloj(); };
            temporizador.Start();
            EventHandler liberar = (s, a) => { temporizador.Stop(); temporizador.Dispose(); };
            f.FormClosed += (s, a) => liberar(s, a);
            f.Disposed += liberar;      // también si la ventana se destruye sin cerrarse
        }

        // ======================================================
        // ÍCONOS DE SÍMBOLO (fuente "Segoe MDL2 Assets" de Windows)
        // ------------------------------------------------------
        // Para tarjetas que heredaron un ícono de otro módulo. Si la
        // fuente no existe, se conserva la imagen del diseño.
        // ======================================================
        public static void PonerIcono(PictureBox destino, char glifo, Color color)
        {
            if (destino == null || destino.Width < 8 || destino.Height < 8)
                return;
            bool existe = false;
            foreach (FontFamily fam in FontFamily.Families)
                if (fam.Name == "Segoe MDL2 Assets") { existe = true; break; }
            if (!existe)
                return;

            int lado = Math.Min(destino.Width, destino.Height);
            Bitmap b = new Bitmap(destino.Width, destino.Height);
            using (Graphics g = Graphics.FromImage(b))
            using (Font fuente = new Font("Segoe MDL2 Assets", lado * 0.62f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush pincel = new SolidBrush(color))
            using (StringFormat centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glifo.ToString(), fuente, pincel, new RectangleF(0, 0, b.Width, b.Height), centro);
            }
            Image anterior = destino.Image;
            destino.Image = b;
            destino.SizeMode = PictureBoxSizeMode.Zoom;
            if (anterior != null) anterior.Dispose();
            destino.Disposed += (s, e) => b.Dispose();
        }

        // Imagen mucho más grande que su cuadro: se reemplaza por una copia
        // reducida (hasta 3 veces el cuadro, para que se vea nítida al escalar)
        // y se muestra sin deformarse. Ahorra la memoria de la imagen original.
        public static void ReducirImagen(PictureBox destino)
        {
            Image original = destino == null ? null : destino.Image;
            if (original == null || destino.Width < 4 || destino.Height < 4)
                return;
            int maxAncho = destino.Width * 3, maxAlto = destino.Height * 3;
            if (original.Width <= maxAncho && original.Height <= maxAlto)
            {
                destino.SizeMode = PictureBoxSizeMode.Zoom;
                return;
            }
            float f = Math.Min(maxAncho / (float)original.Width, maxAlto / (float)original.Height);
            Bitmap copia = new Bitmap(Math.Max(1, (int)(original.Width * f)), Math.Max(1, (int)(original.Height * f)));
            using (Graphics g = Graphics.FromImage(copia))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, new Rectangle(0, 0, copia.Width, copia.Height));
            }
            destino.Image = copia;
            destino.SizeMode = PictureBoxSizeMode.Zoom;
            original.Dispose();
            destino.Disposed += (s, e) => copia.Dispose();
        }

        // Etiquetas de una tarjeta centradas en su ancho (sin AutoSize).
        public static void CentrarEnTarjeta(Control tarjeta)
        {
            foreach (Control c in tarjeta.Controls)
            {
                Guna2HtmlLabel l = c as Guna2HtmlLabel;
                if (l != null)
                {
                    int alto = l.Height;
                    l.AutoSize = false;
                    l.TextAlignment = ContentAlignment.MiddleCenter;
                    l.Bounds = new Rectangle(4, l.Top, tarjeta.Width - 8, Math.Max(alto, l.Font.Height + 4));
                }
                else if (c is PictureBox)
                {
                    c.Left = (tarjeta.Width - c.Width) / 2;
                }
            }
        }

        private static Control Buscar(Control raiz, string nombre)
        {
            Control[] r = raiz.Controls.Find(nombre, true);
            return r.Length == 0 ? null : r[0];
        }

        private static void Recorrer(Control raiz, Action<Control> accion)
        {
            foreach (Control c in raiz.Controls)
            {
                accion(c);
                Recorrer(c, accion);
            }
        }
    }
}
