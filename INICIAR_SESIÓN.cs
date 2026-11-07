using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // INICIO DE SESIÓN (formulario principal de la aplicación)
    // ----------------------------------------------------------
    // Program.cs ejecuta esta ventana. Tras autenticar:
    //   1. Se identifica al empleado real (Autenticacion).
    //   2. Se exige empleado activo, cuenta habilitada y cargo.
    //   3. Si la clave es temporal, se obliga a cambiarla.
    //   4. Se registra SesionActual y se abre Inicio (Navegador).
    // Al cerrar sesión, el Navegador cierra los módulos y vuelve aquí.
    // ==========================================================
    public partial class INICIAR_SESIÓN : Form
    {
        public INICIAR_SESIÓN()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            timer1.Start();
            txtContrasena.UseSystemPasswordChar = true;
            chkMostrarContrasena.Checked = false;

            // Enter en usuario pasa a contraseña; Enter en contraseña inicia sesión.
            txtUsuario.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; txtContrasena.Focus(); }
            };
            txtContrasena.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; But_iniciar_Click(But_iniciar, EventArgs.Empty); }
            };

            Shown += (s, e) => PrepararParaIngreso();
            VisibleChanged += (s, e) =>
            {
                timer1.Enabled = Visible;
                if (Visible) PrepararParaIngreso();
            };

            // Diseño común: se adapta a la pantalla (1366x768 a 1920x1080 y escalado
            // de Windows) y el encabezado muestra la sesión y la hora.
            // El ícono del tenedor pertenece a la barra inferior (antes quedaba
            // suelto en la ventana y no seguía a la barra).
            Point tenedor = guna2PictureBox8.Location;
            Controls.Remove(guna2PictureBox8);
            guna2Panel2.Controls.Add(guna2PictureBox8);
            guna2PictureBox8.Location = new Point(tenedor.X - guna2Panel2.Left, tenedor.Y - guna2Panel2.Top);
            PrepararFondo();
            PrepararControles();

            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);

            // Después de la adaptación común: fondo, panel y barra propios del inicio.
            Shown += (s, e) => Distribuir();
            VisibleChanged += (s, e) => { if (Visible) Distribuir(); };
            Resize += (s, e) => Distribuir();
            // La fecha cambia de largo al cambiar de día: la barra se reacomoda.
            labFecha.SizeChanged += (s, e) =>
            {
                if (Visible && fondo != null && guna2Panel2.Width > 300) DistribuirBarra(DisenoVentanas.Escala(this));
            };
            Disposed += (s, e) =>
            {
                if (letraMostrar != null) letraMostrar.Dispose();
                if (letraBoton != null) letraBoton.Dispose();
                foreach (Image i in new[] { guna2Panel1.BackgroundImage, respaldo.BackgroundImage, guna2PictureBox4.Image,
                                            guna2PictureBox5.Image, siluetaUsuario, siluetaCandado })
                    if (i != null) i.Dispose();
            };
        }

        // ======================================================
        // DISTRIBUCIÓN DEL INICIO DE SESIÓN
        // ------------------------------------------------------
        // * Fondo: se dibuja "cubriendo" la ventana sin deformarse (antes
        //   se estiraba). Se recorta poco arriba (logo) y el resto abajo,
        //   donde queda la barra negra.
        // * Panel blanco: centrado en el espacio libre entre el logotipo grande
        //   del fondo y la estatua (el logotipo del fondo queda visible) y en el
        //   alto libre sobre la barra; sombra suave y filo dorado inferior.
        // * Barra negra: todo el ancho y pegada abajo; a la izquierda el
        //   sistema, a la derecha fecha, hora y lugar.
        // Medidas de diseño (lienzo 1540x941) por la escala común.
        // ======================================================
        private Image fondo;
        private Font letraMostrar, letraBoton;
        private Panel respaldo;                       // sombra y filo dorado detrás del panel blanco
        private Image siluetaUsuario, siluetaCandado;
        private static readonly Color Oro = Color.FromArgb(255, 204, 0);
        private static readonly Color GrisZona = Color.FromArgb(232, 232, 232);
        private const float FinLogoFondo = 742f / 1586f;    // fin del tenedor del logotipo grande (fracción)
        private const float InicioEstatua = 1330f / 1586f;  // inicio de la estatua (fracción)
        private const float RecorteSuperiorFondo = 40f;        // px de la imagen original
        private const int AnchoPanel = 447, AltoPanel = 710, AltoBarra = 64;

        private void PrepararFondo()
        {
            fondo = guna2PictureBox1.Image;
            guna2PictureBox1.Image = null;          // se dibuja en Paint: sin copias en memoria
            guna2PictureBox1.Paint += PintarFondo;
            guna2PictureBox1.Disposed += (s, e) =>
            {
                if (fondo != null) { fondo.Dispose(); fondo = null; }
            };
        }

        // Una sola vez: íconos como siluetas (sin cuadros de fondo), textos de
        // ayuda, casilla y botones con su ícono. Los eventos no cambian.
        private void PrepararControles()
        {
            siluetaUsuario = Silueta(guna2PictureBox4.Image);
            siluetaCandado = Silueta(guna2PictureBox5.Image);
            foreach (Guna2PictureBox zona in new[] { guna2PictureBox4, guna2PictureBox5 })
            {
                Image original = zona.Image;
                zona.Image = null;
                if (original != null) original.Dispose();
                zona.BackColor = Color.White;            // el panel es blanco
                zona.FillColor = Color.Transparent;
                zona.BorderRadius = 0;
                zona.SizeMode = PictureBoxSizeMode.Normal;
                zona.Cursor = Cursors.IBeam;
            }
            guna2PictureBox4.Click += (s, e) => txtUsuario.Focus();
            guna2PictureBox5.Click += (s, e) => txtContrasena.Focus();

            foreach (Guna2TextBox t in new[] { txtUsuario, txtContrasena })
            {
                t.CustomizableEdges.TopLeft = false;
                t.CustomizableEdges.BottomLeft = false;
                t.FillColor = Color.FromArgb(250, 250, 250);
                t.BorderColor = Color.FromArgb(210, 210, 210);
                t.PlaceholderForeColor = Color.FromArgb(120, 120, 120);
                t.TextOffset = new Point(6, 0);
            }
            txtUsuario.PlaceholderText = "Usuario";
            txtContrasena.PlaceholderText = "Contraseña";

            chkMostrarContrasena.UncheckedState.FillColor = Color.White;
            chkMostrarContrasena.UncheckedState.BorderColor = Color.FromArgb(120, 120, 120);
            chkMostrarContrasena.UncheckedState.BorderThickness = 1;
            chkMostrarContrasena.UncheckedState.BorderRadius = 2;
            chkMostrarContrasena.CheckedState.FillColor = Color.FromArgb(40, 40, 40);
            chkMostrarContrasena.CheckedState.BorderColor = Color.FromArgb(40, 40, 40);
            chkMostrarContrasena.CheckedState.BorderRadius = 2;
            chkMostrarContrasena.ForeColor = Color.FromArgb(45, 45, 45);

            // Botones: el ícono va dentro del propio botón (antes era una imagen
            // superpuesta con su propio fondo); texto e ícono centrados juntos.
            Image entrar = Silueta(guna2PictureBox6.Image), salir = Silueta(guna2PictureBox7.Image);
            guna2PictureBox6.Visible = false;
            guna2PictureBox7.Visible = false;
            But_iniciar.Text = "INICIAR SESIÓN";
            But_iniciar.TextOffset = Point.Empty;
            But_iniciar.Image = entrar;
            But_iniciar.ImageAlign = HorizontalAlignment.Center;
            But_iniciar.FillColor = Oro;
            guna2Button2.Image = salir;
            guna2Button2.ImageAlign = HorizontalAlignment.Center;
            guna2Button2.FillColor = Color.FromArgb(222, 222, 222);
            Disposed += (s, e) => { entrar.Dispose(); salir.Dispose(); };

            // Barra: línea dorada superior.
            guna2Panel2.FillColor = Color.Black;
            guna2Panel2.CustomBorderColor = Oro;
            guna2Panel2.CustomBorderThickness = new Padding(0, 2, 0, 0);

            // Respaldo (sombra y filo dorado) entre el fondo y el panel blanco.
            respaldo = new Panel { Name = "pnlRespaldoLogin", BackColor = Color.Black, Bounds = new Rectangle(0, 0, 10, 10) };
            Controls.Add(respaldo);
            Controls.SetChildIndex(respaldo, Controls.GetChildIndex(guna2PictureBox1));
            guna2PictureBox1.SendToBack();
        }

        // Ícono original convertido en silueta negra sobre fondo transparente.
        private static Bitmap Silueta(Image original)
        {
            if (original == null) return null;
            using (Bitmap o = new Bitmap(original))
            {
                Bitmap r = new Bitmap(o.Width, o.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                for (int y = 0; y < o.Height; y++)
                    for (int x = 0; x < o.Width; x++)
                    {
                        Color c = o.GetPixel(x, y);
                        float lum = 0.3f * c.R + 0.59f * c.G + 0.11f * c.B;
                        int alfa = (int)Math.Max(0, Math.Min(255, (150 - lum) * 255 / 110)) * c.A / 255;
                        r.SetPixel(x, y, Color.FromArgb(alfa, 0, 0, 0));
                    }
                return r;
            }
        }

        // Dónde queda la imagen completa al cubrir un área (puede salirse por los bordes).
        private RectangleF AreaFondo(Size destino)
        {
            float esc = Math.Max(destino.Width / (float)fondo.Width, destino.Height / (float)fondo.Height);
            float ancho = fondo.Width * esc, alto = fondo.Height * esc;
            float sobraAlto = alto - destino.Height;
            return new RectangleF((destino.Width - ancho) / 2f, -Math.Min(sobraAlto, RecorteSuperiorFondo * esc), ancho, alto);
        }

        private void PintarFondo(object sender, PaintEventArgs e)
        {
            if (fondo == null) return;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(fondo, AreaFondo(guna2PictureBox1.ClientSize));
        }

        private static int U(float valor, float k)
        {
            return (int)Math.Round(valor * k, MidpointRounding.AwayFromZero);
        }

        private void Distribuir()
        {
            float k = DisenoVentanas.Escala(this);
            Size cs = ClientSize;
            if (fondo == null || !Visible || !IsHandleCreated || WindowState == FormWindowState.Minimized ||
                cs.Width < 300 || cs.Height < 200 || k <= 0f)
                return;

            SuspendLayout();
            try
            {
                guna2PictureBox1.Bounds = new Rectangle(0, 0, cs.Width, cs.Height);
                guna2PictureBox1.Invalidate();

                int altoBarra = Math.Max(40, U(AltoBarra, k));
                guna2Panel2.Bounds = new Rectangle(0, cs.Height - altoBarra, cs.Width, altoBarra);
                DistribuirBarra(k);

                DistribuirPanel(k, cs, guna2Panel2.Top);
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void DistribuirPanel(float k, Size cs, int altoUtil)
        {
            // Si la ventana es baja, el panel se reduce un poco para no tocar la barra.
            int margen = U(14, k);
            float kp = Math.Min(k, (altoUtil - 2 * margen) / (float)AltoPanel);
            int ancho = U(AnchoPanel, kp), alto = U(AltoPanel, kp);

            // Centrado en el espacio libre entre el logotipo grande del fondo y la
            // estatua (así el logotipo del fondo se ve completo) y en el alto libre
            // entre el borde superior y la barra. Se calcula con el encuadre real
            // del fondo: vale para cualquier resolución.
            RectangleF img = AreaFondo(cs);
            float finLogo = img.X + img.Width * FinLogoFondo, estatua = img.X + img.Width * InicioEstatua;
            int x = (int)Math.Round((finLogo + estatua - ancho) / 2f);
            x = Math.Max(x, (int)Math.Ceiling(finLogo + cs.Width * 0.02f));
            x = Math.Max(margen, Math.Min(cs.Width - ancho - margen, x));
            int y = Math.Max(margen, (altoUtil - alto) / 2);
            int radio = U(34, kp);
            guna2Panel1.BorderRadius = radio;
            guna2Panel1.Bounds = new Rectangle(x, y, ancho, alto);
            Respaldo(cs, img, radio, U(6, kp), U(12, kp));

            // Interior (márgenes iguales: 30 a los lados, 26 arriba y abajo):
            //   26 | logo 275 | 22 | usuario 46 | 12 | contraseña 46 | 10 |
            //   mostrar 22 | 16 | botones 58 | 22 | Trujillo 129 | 26
            int bordeX = U(30, kp), contenido = ancho - 2 * bordeX;
            int fy = 26;

            Colocar(guna2PictureBox2, new Rectangle(bordeX, U(fy, kp), contenido, U(275, kp)));
            fy += 275 + 22;

            Fila(guna2PictureBox4, txtUsuario, siluetaUsuario, fy, kp, bordeX, contenido);
            fy += 46 + 12;
            Fila(guna2PictureBox5, txtContrasena, siluetaCandado, fy, kp, bordeX, contenido);
            fy += 46 + 10;

            // "Mostrar contraseña" legible también en pantallas pequeñas (mínimo 8,5 pt).
            float tamMostrar = (float)Math.Round(Math.Max(8.5f, 10f * kp) * 4f) / 4f;
            if (letraMostrar == null || Math.Abs(letraMostrar.Size - tamMostrar) > 0.01f)
            {
                Font anterior = letraMostrar;
                letraMostrar = new Font("Segoe UI", tamMostrar);
                chkMostrarContrasena.Font = letraMostrar;
                if (anterior != null) anterior.Dispose();
            }
            chkMostrarContrasena.Font = letraMostrar;   // la adaptación común pudo reemplazarla
            chkMostrarContrasena.Location = new Point(bordeX + U(2, kp), U(fy, kp) + (U(22, kp) - chkMostrarContrasena.Height) / 2);
            fy += 22 + 16;

            int altoBoton = U(58, kp), salir = U(62, kp), separacion = U(14, kp);
            int yBoton = U(fy, kp);
            But_iniciar.Bounds = new Rectangle(bordeX, yBoton, contenido - salir - separacion, altoBoton);
            guna2Button2.Bounds = new Rectangle(ancho - bordeX - salir, yBoton, salir, altoBoton);
            foreach (Guna2Button bt in new[] { But_iniciar, guna2Button2 })
            {
                bt.BorderRadius = U(12, kp);
                bt.ImageSize = new Size(U(26, kp), U(26, kp));
            }
            float tamBoton = (float)Math.Round(Math.Max(9f, 14f * kp) * 4f) / 4f;
            if (letraBoton == null || Math.Abs(letraBoton.Size - tamBoton) > 0.01f)
            {
                Font anterior = letraBoton;
                letraBoton = new Font("Segoe UI", tamBoton, FontStyle.Bold | FontStyle.Italic);
                if (anterior != null) anterior.Dispose();
            }
            But_iniciar.Font = letraBoton;
            But_iniciar.TextOffset = new Point(U(6, kp), 0);
            fy += 58 + 22;

            Colocar(guna2PictureBox3, new Rectangle(bordeX, U(fy, kp), contenido, U(129, kp)));
        }

        // Respaldo detrás del panel: fondo + sombra suave + filo dorado inferior.
        // Las esquinas redondeadas del panel muestran ese mismo respaldo.
        private void Respaldo(Size cs, RectangleF img, int radio, int filo, int sombra)
        {
            Rectangle p = guna2Panel1.Bounds;
            if (p.Width < 4 || p.Height < 4) return;
            Rectangle r = Rectangle.Inflate(p, sombra, sombra);
            r.Height += filo;
            r.Intersect(new Rectangle(0, 0, cs.Width, guna2Panel2.Top));

            Bitmap capa = new Bitmap(r.Width, r.Height);
            using (Graphics g = Graphics.FromImage(capa))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(fondo, new RectangleF(img.X - r.X, img.Y - r.Y, img.Width, img.Height));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                RectangleF local = new RectangleF(p.X - r.X, p.Y - r.Y, p.Width, p.Height);
                for (int i = sombra; i >= 1; i--)
                    using (GraphicsPath s = Redondo(new RectangleF(local.X - i, local.Y - i + filo, local.Width + 2 * i, local.Height + 2 * i), radio + i))
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(Math.Max(1, 90 / sombra), 0, 0, 0)))
                        g.FillPath(sb, s);
                using (GraphicsPath s = Redondo(new RectangleF(local.X, local.Y + filo, local.Width, local.Height), radio))
                using (SolidBrush sb = new SolidBrush(Oro))
                    g.FillPath(sb, s);
            }
            Image anterior = respaldo.BackgroundImage;
            respaldo.BackgroundImageLayout = ImageLayout.None;
            respaldo.BackgroundImage = capa;
            respaldo.Bounds = r;
            if (anterior != null) anterior.Dispose();

            Bitmap esquinas = capa.Clone(new Rectangle(p.X - r.X, p.Y - r.Y, Math.Min(p.Width, r.Right - p.X), Math.Min(p.Height, r.Bottom - p.Y)), capa.PixelFormat);
            anterior = guna2Panel1.BackgroundImage;
            guna2Panel1.BackgroundImageLayout = ImageLayout.None;
            guna2Panel1.BackgroundImage = esquinas;
            if (anterior != null) anterior.Dispose();
        }

        private static GraphicsPath Redondo(RectangleF r, float radio)
        {
            GraphicsPath g = new GraphicsPath();
            float d = Math.Max(1, radio * 2);
            g.AddArc(r.X, r.Y, d, d, 180, 90);
            g.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            g.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            g.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            g.CloseFigure();
            return g;
        }

        private static GraphicsPath IzquierdaRedondeada(RectangleF r, float radio)
        {
            GraphicsPath g = new GraphicsPath();
            float d = Math.Max(1, radio * 2);
            g.AddArc(r.X, r.Y, d, d, 180, 90);
            g.AddLine(r.X + radio, r.Y, r.Right, r.Y);
            g.AddLine(r.Right, r.Bottom, r.X + radio, r.Bottom);
            g.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            g.CloseFigure();
            return g;
        }

        // Campo con su zona de ícono gris a la izquierda (como un solo control).
        private static void Fila(Guna2PictureBox zona, Guna2TextBox campo, Image silueta, int fy, float k, int x, int ancho)
        {
            int y = U(fy, k), alto = U(46, k), anchoZona = U(48, k), radio = U(10, k);
            campo.BorderRadius = radio;
            zona.Bounds = new Rectangle(x, y, anchoZona, alto);
            campo.Bounds = new Rectangle(x + anchoZona, y, ancho - anchoZona, alto);

            // Ícono a la mitad del alto, centrado en la zona.
            int lado = Math.Max(8, alto / 2);
            // La zona (gris, redondeada a la izquierda, con el mismo borde del campo)
            // se dibuja en su imagen: así se ve igual con cualquier escala.
            Bitmap icono = new Bitmap(anchoZona, alto);
            using (Graphics g = Graphics.FromImage(icono))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                using (GraphicsPath forma = IzquierdaRedondeada(new RectangleF(0.5f, 0.5f, anchoZona + radio, alto - 1.5f), radio))
                using (SolidBrush relleno = new SolidBrush(GrisZona))
                using (Pen borde = new Pen(campo.BorderColor, 1f))
                {
                    g.FillPath(relleno, forma);
                    g.DrawPath(borde, forma);
                }
            }
            if (silueta != null)
                using (Graphics g = Graphics.FromImage(icono))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    float f = Math.Min(lado / (float)silueta.Width, lado / (float)silueta.Height);
                    float w = silueta.Width * f, h = silueta.Height * f;
                    g.DrawImage(silueta, (anchoZona - w) / 2, (alto - h) / 2, w, h);
                }
            Image anterior = zona.Image;
            zona.Image = icono;
            if (anterior != null) anterior.Dispose();
        }

        // Imagen sin deformarse dentro de su cuadro.
        private static void Colocar(PictureBox foto, Rectangle r)
        {
            foto.SizeMode = PictureBoxSizeMode.Zoom;
            foto.Bounds = r;
        }

        private void DistribuirBarra(float k)
        {
            int alto = guna2Panel2.Height, margen = U(28, k), lado = Math.Min(U(34, k), alto - 8);
            int junto = U(8, k), separado = U(16, k);

            // Izquierda: tenedor, nombre | sistema.
            int x = margen;
            x = EnBarra(guna2PictureBox8, x, lado, lado) + junto;
            x = EnBarra(guna2HtmlLabel2, x, guna2HtmlLabel2.Width, 0) + separado;
            x = EnBarra(guna2HtmlLabel3, x, guna2HtmlLabel3.Width, 0) + separado;
            EnBarra(guna2HtmlLabel1, x, guna2HtmlLabel1.Width, 0);

            // Derecha (alineada al borde): reloj fecha | reloj hora | tenedor lugar.
            // La hora tiene un ancho fijo (el de sus dígitos más anchos) para que
            // nada se mueva cada segundo; la fecha se reacomoda al cambiar de día.
            int anchoFecha = labFecha.Width;
            // Ancho medido con la propia etiqueta (la medición de TextRenderer
            // no coincide con la letra que dibuja la etiqueta HTML).
            int anchoHora = 0;
            string actual = LabHora.Text;
            foreach (string muestra in new[] { "00:00:00", "88:88:88", "44:44:44", "22:22:22" })
            {
                LabHora.Text = muestra;
                anchoHora = Math.Max(anchoHora, LabHora.PreferredSize.Width);
            }
            LabHora.Text = actual;
            int anchoLugar = guna2HtmlLabel5.Width;
            int total = lado + junto + anchoFecha + separado + guna2HtmlLabel7.Width + separado +
                        lado + junto + anchoHora + separado + guna2HtmlLabel4.Width + separado +
                        lado + junto + anchoLugar;
            x = guna2Panel2.Width - margen - total;
            x = EnBarra(guna2PictureBox11, x, lado, lado) + junto;
            x = EnBarra(labFecha, x, anchoFecha, 0) + separado;
            x = EnBarra(guna2HtmlLabel7, x, guna2HtmlLabel7.Width, 0) + separado;
            x = EnBarra(guna2PictureBox10, x, lado, lado) + junto;
            x = EnBarra(LabHora, x, anchoHora, 0) + separado;
            x = EnBarra(guna2HtmlLabel4, x, guna2HtmlLabel4.Width, 0) + separado;
            x = EnBarra(guna2PictureBox9, x, lado, lado) + junto;
            EnBarra(guna2HtmlLabel5, x, anchoLugar, 0);
        }

        // Coloca en la barra, centrado en vertical; devuelve el borde derecho de su lugar.
        private int EnBarra(Control c, int x, int ancho, int altoImagen)
        {
            if (altoImagen > 0)
                Colocar((PictureBox)c, new Rectangle(x, (guna2Panel2.Height - altoImagen) / 2, ancho, altoImagen));
            else
                c.Location = new Point(x, (guna2Panel2.Height - c.Height) / 2);
            return x + ancho;
        }

        // Deja la ventana limpia (al iniciar y al volver tras cerrar sesión).
        internal void PrepararParaIngreso()
        {
            txtContrasena.Text = "";
            chkMostrarContrasena.Checked = false;
            txtContrasena.UseSystemPasswordChar = true;
            But_iniciar.Enabled = true;

            if (txtUsuario.Text.Length == 0)
                txtUsuario.Focus();
            else
                txtContrasena.Focus();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void guna2PictureBox7_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            labFecha.Text = DateTime.Now.ToString(
                 "dddd, dd 'de' MMMM 'de' yyyy",
                 new CultureInfo("es-ES"));

            LabHora.Text = DateTime.Now.ToString("HH:mm:ss");   // 24 h: el sufijo "a. m." no se dibujaba
        }

        private void But_iniciar_Click(object sender, EventArgs e)
        {
            string usuario = txtUsuario.Text.Trim();
            string contrasena = txtContrasena.Text;

            if (usuario == "" || contrasena == "")
            {
                MessageBox.Show(
                    "Ingrese su usuario y contraseña.",
                    "Iniciar sesión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            ResultadoAutenticacion resultado;
            But_iniciar.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                resultado = Autenticacion.Autenticar(usuario, contrasena);
            }
            catch (Exception ex)
            {
                Registro.Error("Sesion", ex);
                MessageBox.Show(
                    "No fue posible iniciar sesión: SQL Server no respondió.\n" +
                    "Inténtelo de nuevo en unos segundos.\n\nDetalle: " + ex.Message,
                    "Error de acceso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor = Cursors.Default;
                But_iniciar.Enabled = true;
                txtContrasena.Text = "";
            }

            if (!resultado.Correcto)
            {
                MessageBox.Show(
                    resultado.Mensaje,
                    "Acceso denegado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtContrasena.Focus();
                return;
            }

            // Contraseña temporal: cambio obligatorio antes de entrar.
            if (resultado.DebeCambiarContrasena &&
                !SolicitarCambioObligatorio(resultado, contrasena))
            {
                return;
            }

            SesionActual.IniciarSesion(
                resultado.IdEmpleado,
                resultado.Usuario,
                resultado.NombreCompleto,
                resultado.Cargo,
                resultado.IdSucursal);

            Navegador.IniciarAplicacion(this);
        }

        // ------------------------------------------------------
        // Cambio obligatorio de contraseña temporal
        // ------------------------------------------------------
        private bool SolicitarCambioObligatorio(ResultadoAutenticacion r, string actual)
        {
            using (Form ventana = new Form())
            {
                ventana.Text = "Cambiar contraseña temporal";
                ventana.ClientSize = new Size(440, 330);
                ventana.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                ventana.StartPosition = FormStartPosition.CenterParent;
                ventana.BackColor = Color.White;
                ventana.ShowInTaskbar = false;

                Guna2Panel encabezado = new Guna2Panel { Dock = DockStyle.Top, Height = 56, FillColor = Color.Gold };
                Label titulo = new Label
                {
                    Text = "NUEVA CONTRASEÑA",
                    Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                encabezado.Controls.Add(titulo);

                Label nota = new Label
                {
                    Text = "Hola " + r.NombreCompleto + ". Su contraseña es temporal: " +
                           "elija una nueva (mínimo " + Contrasenas.LongitudMinima + " caracteres, con letras y números).",
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.DimGray,
                    Location = new Point(30, 66),
                    Size = new Size(380, 40)
                };

                Guna2TextBox txtNueva = Caja(140, "Nueva contraseña");
                Guna2TextBox txtRepetir = Caja(196, "Repetir contraseña");
                Label lblError = new Label { ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 9F), Location = new Point(30, 240), Size = new Size(380, 34) };

                Guna2Button btnGuardar = new Guna2Button
                {
                    Text = "Guardar y entrar",
                    Location = new Point(230, 280),
                    Size = new Size(180, 38),
                    BorderRadius = 10,
                    FillColor = Color.Gold,
                    ForeColor = Color.Black,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
                };
                Guna2Button btnCancelar = new Guna2Button
                {
                    Text = "Cancelar",
                    Location = new Point(30, 280),
                    Size = new Size(180, 38),
                    BorderRadius = 10,
                    FillColor = Color.Gainsboro,
                    ForeColor = Color.Black,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold)
                };

                bool cambiado = false;
                btnGuardar.Click += (s, e) =>
                {
                    if (txtNueva.Text != txtRepetir.Text)
                    {
                        lblError.Text = "Las contraseñas no coinciden.";
                        return;
                    }
                    try
                    {
                        string error = Autenticacion.CambiarContrasena(r.IdEmpleado, r.Usuario, actual, txtNueva.Text);
                        if (error != null) { lblError.Text = error; return; }
                        cambiado = true;
                        ventana.DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        Registro.Error("Sesion", ex);
                        lblError.Text = "SQL Server no respondió. Inténtelo de nuevo.";
                    }
                };
                btnCancelar.Click += (s, e) => ventana.DialogResult = DialogResult.Cancel;

                ventana.Controls.AddRange(new Control[] { nota, txtNueva, txtRepetir, lblError, btnGuardar, btnCancelar, encabezado });
                ventana.Shown += (s, e) => txtNueva.Focus();
                ventana.ShowDialog(this);

                txtNueva.Text = "";
                txtRepetir.Text = "";

                if (cambiado)
                {
                    MessageBox.Show("Contraseña actualizada correctamente.", "Iniciar sesión",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return cambiado;
            }
        }

        private static Guna2TextBox Caja(int y, string marcador)
        {
            return new Guna2TextBox
            {
                Location = new Point(30, y),
                Size = new Size(380, 38),
                BorderRadius = 8,
                BorderColor = Color.Goldenrod,
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.Black,
                PlaceholderText = marcador,
                UseSystemPasswordChar = true,
                MaxLength = 128
            };
        }

        private void guna2CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            // Mostrar u ocultar la contraseña.
            txtContrasena.UseSystemPasswordChar = !chkMostrarContrasena.Checked;
        }
    }
}
