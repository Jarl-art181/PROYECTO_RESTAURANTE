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
            Disposed += (s, e) => { if (letraMostrar != null) letraMostrar.Dispose(); };
        }

        // ======================================================
        // DISTRIBUCIÓN DEL INICIO DE SESIÓN
        // ------------------------------------------------------
        // * Fondo: se dibuja "cubriendo" la ventana sin deformarse (antes
        //   se estiraba). Se recorta poco arriba (logo) y el resto abajo,
        //   donde queda la barra negra.
        // * Panel blanco: compacto, con espacios uniformes, entre la
        //   catedral y la estatua (no tapa el logo, la comida ni la estatua).
        // * Barra negra: todo el ancho y pegada abajo; a la izquierda el
        //   sistema, a la derecha fecha, hora y lugar.
        // Medidas de diseño (lienzo 1540x941) por la escala común.
        // ======================================================
        private Image fondo;
        private Font letraMostrar;
        private const float RecorteSuperiorFondo = 40f;        // px de la imagen original
        private const float CatedralDerecha = 885f / 1586f;    // fracción del ancho de la imagen
        private const float EstatuaIzquierda = 1340f / 1586f;
        private const int AnchoPanel = 447, AltoPanel = 676, AltoBarra = 64;

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

            // Centrado en el espacio libre entre la catedral y la estatua.
            RectangleF img = AreaFondo(cs);
            float libreIzq = img.X + img.Width * CatedralDerecha, libreDer = img.X + img.Width * EstatuaIzquierda;
            int x = (int)Math.Round((libreIzq + libreDer - ancho) / 2f);
            x = Math.Max(margen, Math.Min(cs.Width - ancho - margen, x));
            int y = Math.Max(margen, (altoUtil - alto) / 2);
            guna2Panel1.Bounds = new Rectangle(x, y, ancho, alto);

            // Interior: 26 | logo 256 | 16 | usuario 46 | 14 | contraseña 46 | 10 |
            //           mostrar 20 | 18 | botones 60 | 20 | Trujillo 120 | 24
            int bordeX = U(30, kp), campoX = U(80, kp), campoAncho = ancho - campoX - bordeX;
            int fy = 26;

            int logoAncho = U(360, kp), logoAlto = U(256, kp);
            Colocar(guna2PictureBox2, new Rectangle((ancho - logoAncho) / 2, U(fy, kp), logoAncho, logoAlto));
            fy += 256 + 16;

            Fila(guna2PictureBox4, txtUsuario, fy, kp, bordeX, campoX, campoAncho);
            fy += 46 + 14;
            Fila(guna2PictureBox5, txtContrasena, fy, kp, bordeX, campoX, campoAncho);
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
            chkMostrarContrasena.Location =new Point(campoX + U(2, kp), U(fy, kp) + (U(20, kp) - chkMostrarContrasena.Height) / 2);
            fy += 20 + 18;

            int altoBoton = U(60, kp), salir = U(60, kp), separacion = U(12, kp);
            int yBoton = U(fy, kp);
            But_iniciar.Bounds = new Rectangle(bordeX, yBoton, ancho - 2 * bordeX - salir - separacion, altoBoton);
            guna2Button2.Bounds = new Rectangle(ancho - bordeX - salir, yBoton, salir, altoBoton);
            int icono = U(38, kp);
            Colocar(guna2PictureBox6, new Rectangle(But_iniciar.Left + U(30, kp), yBoton + (altoBoton - icono) / 2, icono, icono));
            Colocar(guna2PictureBox7, new Rectangle(guna2Button2.Left + (salir - icono) / 2, yBoton + (altoBoton - icono) / 2, icono, icono));
            fy += 60 + 20;

            int fotoAncho = U(360, kp), fotoAlto = U(120, kp);
            Colocar(guna2PictureBox3, new Rectangle((ancho - fotoAncho) / 2, U(fy, kp), fotoAncho, fotoAlto));

            // El texto del botón empieza después de su ícono.
            int completo = TextRenderer.MeasureText(But_iniciar.Text, But_iniciar.Font).Width;
            int visible = TextRenderer.MeasureText(But_iniciar.Text.TrimStart(), But_iniciar.Font).Width;
            int inicioTexto = (But_iniciar.Width - completo) / 2 + (completo - visible);
            int finIcono = guna2PictureBox6.Right - But_iniciar.Left + U(10, kp);
            But_iniciar.TextOffset = new Point(Math.Max(0, finIcono - inicioTexto), 0);
        }

        // Ícono y campo en la misma fila, centrados en vertical.
        private static void Fila(PictureBox icono, Control campo, int fy, float k, int bordeX, int campoX, int campoAncho)
        {
            int y = U(fy, k), alto = U(46, k), lado = U(36, k);
            campo.Bounds = new Rectangle(campoX, y, campoAncho, alto);
            Colocar(icono, new Rectangle(bordeX + (campoX - bordeX - U(10, k) - lado) / 2, y + (alto - lado) / 2, lado, lado));
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
            int anchoHora = 0;
            string muestra = DateTime.Now.ToString("HH:mm:ss");
            for (char d = '0'; d <= '9'; d++)
            {
                char digito = d;
                string texto = new string(Array.ConvertAll(muestra.ToCharArray(), c => char.IsDigit(c) ? digito : c));
                anchoHora = Math.Max(anchoHora, Ancho(LabHora, texto));
            }
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

        private static int Ancho(Control etiqueta, string muestra)
        {
            return Math.Max(etiqueta.Width, TextRenderer.MeasureText(muestra, etiqueta.Font).Width + 4);
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
