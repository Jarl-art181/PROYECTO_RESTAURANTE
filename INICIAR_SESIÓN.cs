using Guna.UI2.WinForms;
using System;
using System.Drawing;
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
            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);
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

            LabHora.Text = DateTime.Now.ToString("hh:mm:ss tt");
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
