using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        }

        private static readonly ConditionalWeakTable<Form, Estado> estados = new ConditionalWeakTable<Form, Estado>();

        // Letras compartidas: un número acotado de fuentes para todas las ventanas.
        private static readonly Dictionary<string, Font> letras = new Dictionary<string, Font>();

        private static readonly string[] PropiedadesGuna = { "ImageSize", "ImageOffset", "TextOffset", "BorderRadius" };

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

        private static void Aplicar(Form f, Estado e)
        {
            if (!e.Mostrado || f.IsDisposed || !f.IsHandleCreated || f.WindowState == FormWindowState.Minimized)
                return;

            Size cs = f.ClientSize;
            if (cs.Width < 300 || cs.Height < 200 || cs == e.Ultimo)
                return;

            if (e.Controles.Count == 0)
                Capturar(f, e);

            float kx = cs.Width / (float)Lienzo.Width, ky = cs.Height / (float)Lienzo.Height;
            float justa = Math.Min(kx, ky);
            float k = Math.Max(EscalaMinima, Math.Min(EscalaMaxima, justa));
            bool desplazar = justa < EscalaMinima;

            e.Ultimo = cs;
            e.Escala = k;

            int ancho = Redondear(Lienzo.Width * k), alto = Redondear(Lienzo.Height * k);
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
                    bool lateral = o.Limites.X <= 10 && o.Limites.Height >= Lienzo.Height - 60;
                    bool encabezado = !lateral && o.Limites.Y < 140 && o.Limites.Right >= Lienzo.Width - 20;

                    if (lateral)
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
        }

        private static void Hijos(Control padre, Original op, Rectangle nuevoPadre, float k, Estado e, bool estirado)
        {
            foreach (Control c in padre.Controls)
            {
                Original o;
                if (!e.Controles.TryGetValue(c, out o))
                    continue;

                Rectangle r = Escalar(o.Limites, k);
                // En un encabezado estirado, lo que estaba a la derecha sigue a la derecha.
                if (estirado && o.Limites.X > op.Limites.Width * 0.55)
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
                    if (Math.Max(gdi, gdiPlus) <= disponible || letra.Size <= 7f)
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
                    continue;
                }

                // Las tarjetas de un FlowLayoutPanel se crean y recrean por código.
                if (c is FlowLayoutPanel || c is TableLayoutPanel || c is UserControl && !(c is Form))
                    continue;

                Capturar(c, e);
            }
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
                string nombre = SesionActual.Iniciada ? (SesionActual.Usuario ?? SesionActual.NombreCorto) : "-";
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
            f.FormClosed += (s, a) => { temporizador.Stop(); temporizador.Dispose(); };
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
