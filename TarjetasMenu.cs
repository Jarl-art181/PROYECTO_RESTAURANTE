using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // TARJETAS DEL CATÁLOGO (Menú y Pedidos)
    // ----------------------------------------------------------
    // Las 19 tarjetas del diseño se conservan (con sus imágenes);
    // cada una se asocia a su producto real por el nombre de su
    // botón "+". Al cargar el catálogo desde SQL:
    //  * nombre, precio y descripción salen de dbo.PLATO,
    //  * si el producto tiene foto propia, reemplaza la del diseño,
    //  * los productos nuevos reciben una tarjeta con el mismo
    //    estilo (se crea por código en el mismo FlowLayoutPanel),
    //  * los inactivos se ocultan (Pedidos) o se muestran
    //    atenuados (Menú).
    // ==========================================================
    internal sealed class TarjetasMenu : IDisposable
    {
        private sealed class Tarjeta
        {
            public Control Panel;
            public Guna2HtmlLabel Nombre;
            public Guna2HtmlLabel Precio;
            public Label Descripcion;
            public Guna2PictureBox Imagen;
            public Image ImagenDiseno;
            public string DescripcionDiseno;
            public Control Boton;
            public ProductoMenu Producto;
            public string FotoMostrada;
            public Image FotoCargada;
        }

        private readonly FlowLayoutPanel flow;
        private readonly bool mostrarInactivos;
        private readonly List<Tarjeta> tarjetas = new List<Tarjeta>();
        private readonly Dictionary<int, Tarjeta> porId = new Dictionary<int, Tarjeta>();
        private Tarjeta plantilla;
        private Image sinFoto;

        // "+" de una tarjeta.
        public event Action<ProductoMenu> Pulsado;
        // Clic en cualquier otra parte de la tarjeta.
        public event Action<ProductoMenu> Seleccionado;

        public FlowLayoutPanel Contenedor { get { return flow; } }

        public TarjetasMenu(FlowLayoutPanel flow, IDictionary<string, int> botonesDiseno, bool mostrarInactivos)
        {
            this.flow = flow;
            this.mostrarInactivos = mostrarInactivos;

            foreach (KeyValuePair<string, int> par in botonesDiseno)
            {
                Control[] encontrados = flow.Controls.Find(par.Key, true);
                if (encontrados.Length == 0 || encontrados[0].Parent == null)
                    continue;

                Tarjeta t = Analizar(encontrados[0].Parent);
                if (t == null)
                    continue;

                // Producto mínimo hasta cargar el catálogo de SQL.
                t.Producto = new ProductoMenu { IdPlato = par.Value, Nombre = t.Nombre.Text, Estado = "activo" };
                Registrar(t);
                if (plantilla == null)
                    plantilla = t;
            }
        }

        public ProductoMenu Producto(int idPlato)
        {
            Tarjeta t;
            return porId.TryGetValue(idPlato, out t) ? t.Producto : null;
        }

        // Imagen que muestra la tarjeta (diseño o foto propia).
        public Image ImagenActual(int idPlato)
        {
            Tarjeta t;
            return porId.TryGetValue(idPlato, out t) && t.Imagen != null ? t.Imagen.Image : null;
        }

        // Partes de una tarjeta por su tipo de control.
        private static Tarjeta Analizar(Control panel)
        {
            Tarjeta t = new Tarjeta { Panel = panel };
            List<Guna2HtmlLabel> etiquetas = new List<Guna2HtmlLabel>();
            foreach (Control c in panel.Controls)
            {
                if (c is Guna2CircleButton) t.Boton = c;
                else if (c is Guna2PictureBox) t.Imagen = (Guna2PictureBox)c;
                else if (c is Guna2HtmlLabel) etiquetas.Add((Guna2HtmlLabel)c);
                else if (c is Label) t.Descripcion = (Label)c;
            }

            // Nombre (11,25 pt) y precio (15,75 pt): el precio es el más grande.
            if (t.Boton == null || etiquetas.Count != 2)
                return null;
            etiquetas.Sort((a, b) => a.Font.Size.CompareTo(b.Font.Size));
            t.Nombre = etiquetas[0];
            t.Precio = etiquetas[1];
            if (t.Imagen != null)
                t.ImagenDiseno = t.Imagen.Image;
            if (t.Descripcion != null)
                t.DescripcionDiseno = t.Descripcion.Text;
            return t;
        }

        private void Registrar(Tarjeta t)
        {
            tarjetas.Add(t);
            porId[t.Producto.IdPlato] = t;

            t.Boton.Click += (s, e) => { if (Pulsado != null) Pulsado(t.Producto); };

            EventHandler seleccionar = (s, e) => { if (Seleccionado != null) Seleccionado(t.Producto); };
            t.Panel.Click += seleccionar;
            foreach (Control c in t.Panel.Controls)
            {
                if (c != t.Boton)
                {
                    c.Click += seleccionar;
                    c.Cursor = Cursors.Hand;
                }
            }
        }

        // ------------------------------------------------------
        // CARGAR EL CATÁLOGO REAL
        // ------------------------------------------------------
        public void Cargar(IList<ProductoMenu> productos)
        {
            flow.SuspendLayout();
            try
            {
                HashSet<int> vistos = new HashSet<int>();
                foreach (ProductoMenu p in productos)
                {
                    vistos.Add(p.IdPlato);
                    Tarjeta t;
                    if (!porId.TryGetValue(p.IdPlato, out t))
                    {
                        t = CrearTarjeta(p);
                        if (t == null)
                            continue;
                    }
                    t.Producto = p;
                    Pintar(t);
                }

                // Tarjetas del diseño cuyo producto ya no existe.
                foreach (Tarjeta t in tarjetas)
                {
                    if (!vistos.Contains(t.Producto.IdPlato))
                        t.Producto.Estado = "inexistente";
                }

                // Orden: activos primero; platos, agregados, bebidas; luego por código.
                Dictionary<string, int> ordenGrupo = new Dictionary<string, int>
                {
                    { MenuDatos.Platos, 0 }, { MenuDatos.Agregados, 1 }, { MenuDatos.Bebidas, 2 }
                };
                int indice = 0;
                foreach (Tarjeta t in tarjetas
                    .OrderBy(x => x.Producto.Activo ? 0 : 1)
                    .ThenBy(x => x.Producto.Categoria == null ? 9 : ordenGrupo[x.Producto.Grupo])
                    .ThenBy(x => x.Producto.IdPlato))
                {
                    flow.Controls.SetChildIndex(t.Panel, indice++);
                }
            }
            finally
            {
                flow.ResumeLayout(true);
            }
        }

        private void Pintar(Tarjeta t)
        {
            ProductoMenu p = t.Producto;
            bool activo = p.Activo;

            t.Nombre.Text = p.Nombre;
            t.Precio.Text = p.PrecioTexto;
            if (t.Descripcion != null)
            {
                // Sin descripción en SQL se conserva el texto del diseño.
                string descripcion = string.IsNullOrEmpty(p.Descripcion) ? (t.DescripcionDiseno ?? "") : p.Descripcion;
                t.Descripcion.Text = (activo ? "" : "INACTIVO · ") + descripcion;
            }

            Color texto = activo ? SystemColors.ControlText : Color.Gray;
            t.Nombre.ForeColor = texto;
            t.Precio.ForeColor = texto;
            if (t.Descripcion != null)
                t.Descripcion.ForeColor = activo ? SystemColors.ControlText : Color.Firebrick;

            Guna2Panel panel = t.Panel as Guna2Panel;
            if (panel != null)
                panel.BorderColor = activo ? Color.Gold : Color.Silver;

            // Foto propia (si existe) o la imagen del diseño.
            if (t.Imagen != null && t.FotoMostrada != p.Foto)
            {
                Image nueva = MenuDatos.CargarFoto(p.Foto);
                Image anterior = t.FotoCargada;

                t.Imagen.Image = nueva ?? t.ImagenDiseno ?? SinFoto();
                t.FotoCargada = nueva;
                t.FotoMostrada = p.Foto;

                if (anterior != null)
                    anterior.Dispose();
            }
        }

        // Tarjeta nueva con el mismo estilo que las del diseño.
        private Tarjeta CrearTarjeta(ProductoMenu p)
        {
            if (plantilla == null)
                return null;

            Guna2Panel modelo = plantilla.Panel as Guna2Panel;
            Guna2Panel panel = new Guna2Panel
            {
                Size = plantilla.Panel.Size,
                Margin = plantilla.Panel.Margin,
                BackColor = plantilla.Panel.BackColor
            };
            if (modelo != null)
            {
                panel.BorderRadius = modelo.BorderRadius;
                panel.BorderColor = modelo.BorderColor;
                panel.BorderThickness = modelo.BorderThickness;
                panel.FillColor = modelo.FillColor;
            }

            Guna2PictureBox imagen = null;
            if (plantilla.Imagen != null)
            {
                imagen = new Guna2PictureBox
                {
                    Bounds = plantilla.Imagen.Bounds,
                    SizeMode = plantilla.Imagen.SizeMode,
                    BackColor = plantilla.Imagen.BackColor,
                    BorderRadius = plantilla.Imagen.BorderRadius,
                    FillColor = plantilla.Imagen.FillColor
                };
            }

            Guna2HtmlLabel nombre = CopiarEtiqueta(plantilla.Nombre);
            Guna2HtmlLabel precio = CopiarEtiqueta(plantilla.Precio);

            Label descripcion = null;
            if (plantilla.Descripcion != null)
            {
                descripcion = new Label
                {
                    Bounds = plantilla.Descripcion.Bounds,
                    Font = plantilla.Descripcion.Font,
                    BackColor = plantilla.Descripcion.BackColor,
                    AutoSize = plantilla.Descripcion.AutoSize,
                    MaximumSize = plantilla.Descripcion.MaximumSize
                };
            }

            Guna2CircleButton boton = new Guna2CircleButton
            {
                Bounds = plantilla.Boton.Bounds,
                Text = plantilla.Boton.Text,
                Font = plantilla.Boton.Font,
                ForeColor = plantilla.Boton.ForeColor,
                BackColor = plantilla.Boton.BackColor,
                Cursor = Cursors.Hand
            };
            Guna2CircleButton botonModelo = plantilla.Boton as Guna2CircleButton;
            if (botonModelo != null)
                boton.FillColor = botonModelo.FillColor;

            foreach (Control c in new Control[] { boton, descripcion, nombre, precio, imagen })
            {
                if (c != null)
                    panel.Controls.Add(c);
            }

            flow.Controls.Add(panel);

            Tarjeta t = new Tarjeta
            {
                Panel = panel,
                Nombre = nombre,
                Precio = precio,
                Descripcion = descripcion,
                Imagen = imagen,
                ImagenDiseno = null,
                Boton = boton,
                Producto = p,
                FotoMostrada = "\0"      // fuerza la carga de foto/marcador
            };
            Registrar(t);
            return t;
        }

        private static Guna2HtmlLabel CopiarEtiqueta(Guna2HtmlLabel m)
        {
            return new Guna2HtmlLabel
            {
                Location = m.Location,
                AutoSize = m.AutoSize,
                Size = m.Size,
                MaximumSize = m.MaximumSize,
                Font = m.Font,
                ForeColor = m.ForeColor,
                BackColor = m.BackColor
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

        // ------------------------------------------------------
        // FILTRO: texto (sin tildes) y grupo (todos/platos/...).
        // ------------------------------------------------------
        public int Filtrar(string texto, string grupo)
        {
            string buscado = MenuDatos.Normalizar(texto);
            int visibles = 0;

            flow.SuspendLayout();
            foreach (Tarjeta t in tarjetas)
            {
                ProductoMenu p = t.Producto;
                bool existe = p.Estado != "inexistente";
                bool visible = existe &&
                    (mostrarInactivos || p.Activo) &&
                    (string.IsNullOrEmpty(grupo) || grupo == "todos" || p.Categoria == null || p.Grupo == grupo) &&
                    (buscado.Length == 0 || MenuDatos.Normalizar(p.Nombre).Contains(buscado) ||
                     MenuDatos.Normalizar(p.Categoria).Contains(buscado));

                t.Panel.Visible = visible;
                if (visible) visibles++;
            }
            flow.ResumeLayout(true);
            flow.AutoScrollPosition = Point.Empty;
            return visibles;
        }

        public void Dispose()
        {
            foreach (Tarjeta t in tarjetas)
            {
                if (t.FotoCargada != null)
                {
                    if (t.Imagen != null && t.Imagen.Image == t.FotoCargada)
                        t.Imagen.Image = null;
                    t.FotoCargada.Dispose();
                    t.FotoCargada = null;
                }
            }
            if (sinFoto != null)
            {
                sinFoto.Dispose();
                sinFoto = null;
            }
        }
    }
}
