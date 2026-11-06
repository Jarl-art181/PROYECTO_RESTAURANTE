using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // TARJETAS DE INSUMOS (Gestión de Inventario)
    // ----------------------------------------------------------
    // Las 20 tarjetas del diseño son de demostración (Arroz,
    // Pollo...). Se usan como "moldes": cada insumo real de
    // dbo.INSUMO ocupa una tarjeta (mismo tamaño, letras y
    // colores) y las sobrantes se ocultan. Si hay más insumos
    // que tarjetas, se crean otras con el mismo estilo.
    // Foto: %ProgramData%\AlEstiloPiurano\Fotos\insumo_<id>.jpg
    // (sin columna en SQL); si no hay, "Sin foto".
    // ==========================================================
    internal sealed class TarjetasInventario : IDisposable
    {
        private sealed class Tarjeta
        {
            public Control Panel;
            public Guna2PictureBox Imagen;
            public Guna2HtmlLabel Nombre, Titulo, Valor, Estado;
            public Label Descripcion;
            public InsumoInfo Insumo;
            public Image Foto;
        }

        private readonly FlowLayoutPanel flow;
        private readonly List<Tarjeta> tarjetas = new List<Tarjeta>();
        private Tarjeta plantilla;
        private Image sinFoto;

        public event Action<InsumoInfo> Seleccionado;
        public event Action<InsumoInfo> DobleClic;

        public TarjetasInventario(FlowLayoutPanel flow)
        {
            this.flow = flow;
            foreach (Control panel in flow.Controls.Cast<Control>().ToList())
            {
                Tarjeta t = Analizar(panel);
                if (t == null)
                    continue;
                Conectar(t);
                tarjetas.Add(t);
                if (plantilla == null)
                    plantilla = t;
            }
        }

        // Partes por tipo y letra: nombre 11,25 negrita, estado 12
        // negrita, "Stock actual" (izquierda) y valor (derecha) 9 negrita.
        private static Tarjeta Analizar(Control panel)
        {
            Tarjeta t = new Tarjeta { Panel = panel };
            List<Guna2HtmlLabel> pequenas = new List<Guna2HtmlLabel>();
            foreach (Control c in panel.Controls)
            {
                if (c is Guna2PictureBox) t.Imagen = (Guna2PictureBox)c;
                else if (c is Guna2HtmlLabel)
                {
                    Guna2HtmlLabel l = (Guna2HtmlLabel)c;
                    if (l.Font.Size >= 12f) t.Estado = l;
                    else if (l.Font.Size >= 11f) t.Nombre = l;
                    else pequenas.Add(l);
                }
                else if (c is Label) t.Descripcion = (Label)c;
            }
            if (t.Nombre == null || t.Estado == null || pequenas.Count != 2)
                return null;
            pequenas.Sort((a, b) => a.Left.CompareTo(b.Left));
            t.Titulo = pequenas[0];
            t.Valor = pequenas[1];
            return t;
        }

        private void Conectar(Tarjeta t)
        {
            EventHandler clic = (s, e) => { if (t.Insumo != null && Seleccionado != null) Seleccionado(t.Insumo); };
            EventHandler doble = (s, e) => { if (t.Insumo != null && DobleClic != null) DobleClic(t.Insumo); };
            t.Panel.Click += clic;
            t.Panel.DoubleClick += doble;
            t.Panel.Cursor = Cursors.Hand;
            foreach (Control c in t.Panel.Controls)
            {
                c.Click += clic;
                c.DoubleClick += doble;
                c.Cursor = Cursors.Hand;
            }
        }

        // ------------------------------------------------------
        // CARGAR INSUMOS REALES
        // ------------------------------------------------------
        public void Cargar(IList<InsumoInfo> insumos)
        {
            flow.SuspendLayout();
            try
            {
                List<InsumoInfo> orden = insumos
                    .OrderBy(i => i.Activo ? 0 : 1)
                    .ThenBy(i => i.Activo && i.BajoMinimo ? 0 : 1)   // alertas primero
                    .ThenBy(i => i.Nombre)
                    .ToList();

                while (tarjetas.Count < orden.Count && plantilla != null)
                    tarjetas.Add(Crear());

                for (int k = 0; k < tarjetas.Count; k++)
                {
                    Tarjeta t = tarjetas[k];
                    if (k < orden.Count)
                    {
                        t.Insumo = orden[k];
                        Pintar(t);
                    }
                    else
                    {
                        t.Insumo = null;
                        t.Panel.Visible = false;
                    }
                    flow.Controls.SetChildIndex(t.Panel, k);
                }
            }
            finally
            {
                flow.ResumeLayout(true);
            }
        }

        private void Pintar(Tarjeta t)
        {
            InsumoInfo i = t.Insumo;
            Control panel = t.Panel;

            t.Nombre.Text = i.Nombre;
            t.Titulo.Text = "Stock actual";
            t.Valor.Text = i.StockTexto;
            t.Valor.Left = panel.Width - 14 - t.Valor.Width;

            if (t.Descripcion != null)
            {
                string uso = i.Platos.Count == 0 ? "" : "Usado en: " + string.Join(", ", i.Platos);
                string texto = i.Descripcion;
                if (texto.Length == 0) texto = uso;
                else if (uso.Length > 0) texto += " · " + uso;
                if (texto.Length == 0) texto = i.Categoria.Length > 0 ? i.Categoria : "Sin descripción";
                t.Descripcion.Text = texto;
            }

            Color color;
            switch (i.EstadoStock)
            {
                case "Stock adecuado": color = Color.FromArgb(0, 192, 0); break;
                case "Stock bajo": color = Color.DarkOrange; break;
                case "Sin stock": color = Color.Firebrick; break;
                default: color = Color.Gray; break;
            }
            t.Estado.Text = i.EstadoStock;
            t.Estado.ForeColor = color;
            t.Estado.Left = panel.Width - 15 - t.Estado.Width;

            Color texto2 = i.Activo ? SystemColors.ControlText : Color.Gray;
            t.Nombre.ForeColor = texto2;
            t.Valor.ForeColor = texto2;
            if (t.Descripcion != null) t.Descripcion.ForeColor = texto2;

            Guna2Panel g = panel as Guna2Panel;
            if (g != null)
                g.BorderColor = !i.Activo ? Color.Silver : (i.BajoMinimo ? Color.DarkOrange : Color.Gold);

            if (t.Imagen != null)
            {
                Image anterior = t.Foto;
                t.Foto = CargarFoto(i.IdInsumo);
                t.Imagen.Image = t.Foto ?? SinFoto();
                if (anterior != null) anterior.Dispose();
            }

            panel.Visible = true;
        }

        public void Filtrar(Func<InsumoInfo, bool> condicion)
        {
            flow.SuspendLayout();
            foreach (Tarjeta t in tarjetas)
                t.Panel.Visible = t.Insumo != null && condicion(t.Insumo);
            flow.ResumeLayout(true);
            flow.AutoScrollPosition = Point.Empty;
        }

        public int Visibles { get { return tarjetas.Count(t => t.Panel.Visible); } }
        public int Total { get { return tarjetas.Count; } }

        public Image ImagenDe(int idInsumo)
        {
            Tarjeta t = tarjetas.FirstOrDefault(x => x.Insumo != null && x.Insumo.IdInsumo == idInsumo);
            return t == null || t.Imagen == null ? null : t.Imagen.Image;
        }

        // ------------------------------------------------------
        // FOTOS (convención de nombre; sin columna en SQL)
        // ------------------------------------------------------
        public static string ArchivoFoto(int idInsumo)
        {
            return Path.Combine(MenuDatos.CarpetaFotos, "insumo_" + idInsumo + ".jpg");
        }

        public static Image CargarFoto(int idInsumo)
        {
            return MenuDatos.CargarFoto("insumo_" + idInsumo + ".jpg");
        }

        // Copia la imagen elegida, redimensionada, como foto del insumo.
        public static void GuardarFoto(int idInsumo, string origen)
        {
            string temporal = MenuDatos.GuardarFoto(origen);
            string destino = ArchivoFoto(idInsumo);
            if (File.Exists(destino))
                File.Delete(destino);
            File.Move(Path.Combine(MenuDatos.CarpetaFotos, temporal), destino);
        }

        // ------------------------------------------------------
        // TARJETA NUEVA CON EL ESTILO DEL DISEÑO
        // ------------------------------------------------------
        private Tarjeta Crear()
        {
            Tarjeta m = plantilla;
            Guna2Panel modelo = m.Panel as Guna2Panel;
            Guna2Panel panel = new Guna2Panel { Size = m.Panel.Size, Margin = m.Panel.Margin, BackColor = m.Panel.BackColor };
            if (modelo != null)
            {
                panel.BorderRadius = modelo.BorderRadius;
                panel.BorderColor = modelo.BorderColor;
                panel.BorderThickness = modelo.BorderThickness;
                panel.FillColor = modelo.FillColor;
            }

            Tarjeta t = new Tarjeta { Panel = panel };
            if (m.Imagen != null)
            {
                t.Imagen = new Guna2PictureBox
                {
                    Bounds = m.Imagen.Bounds, SizeMode = m.Imagen.SizeMode,
                    BackColor = m.Imagen.BackColor, BorderRadius = m.Imagen.BorderRadius, FillColor = m.Imagen.FillColor
                };
                panel.Controls.Add(t.Imagen);
            }
            t.Nombre = Copiar(m.Nombre); t.Titulo = Copiar(m.Titulo); t.Valor = Copiar(m.Valor); t.Estado = Copiar(m.Estado);
            panel.Controls.AddRange(new Control[] { t.Nombre, t.Titulo, t.Valor, t.Estado });
            if (m.Descripcion != null)
            {
                t.Descripcion = new Label
                {
                    Bounds = m.Descripcion.Bounds, Font = m.Descripcion.Font, BackColor = m.Descripcion.BackColor,
                    AutoSize = m.Descripcion.AutoSize, MaximumSize = m.Descripcion.MaximumSize
                };
                panel.Controls.Add(t.Descripcion);
            }

            flow.Controls.Add(panel);
            Conectar(t);
            return t;
        }

        private static Guna2HtmlLabel Copiar(Guna2HtmlLabel m)
        {
            return new Guna2HtmlLabel
            {
                Location = m.Location, AutoSize = m.AutoSize, Size = m.Size, MaximumSize = m.MaximumSize,
                Font = m.Font, ForeColor = m.ForeColor, BackColor = m.BackColor
            };
        }

        private Image SinFoto()
        {
            if (sinFoto == null)
            {
                Size tam = plantilla != null && plantilla.Imagen != null ? plantilla.Imagen.Size : new Size(212, 147);
                Bitmap b = new Bitmap(tam.Width, tam.Height);
                using (Graphics g = Graphics.FromImage(b))
                using (Font f = new Font("Segoe UI", 11F, FontStyle.Italic))
                using (StringFormat centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.Clear(Color.FromArgb(250, 246, 225));
                    g.DrawString("Sin foto", f, Brushes.DarkGoldenrod, new RectangleF(0, 0, tam.Width, tam.Height), centro);
                }
                sinFoto = b;
            }
            return sinFoto;
        }

        public void Dispose()
        {
            foreach (Tarjeta t in tarjetas)
            {
                if (t.Foto != null)
                {
                    if (t.Imagen != null && t.Imagen.Image == t.Foto) t.Imagen.Image = null;
                    t.Foto.Dispose();
                    t.Foto = null;
                }
            }
            if (sinFoto != null) { sinFoto.Dispose(); sinFoto = null; }
        }
    }
}
