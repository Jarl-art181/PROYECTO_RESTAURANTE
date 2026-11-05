using System;
using System.Windows.Forms;
using System.Globalization;

using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;


namespace PROYECTO_RESTAURANTE
{
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
                 new CultureInfo("es-ES") );

            LabHora.Text = DateTime.Now.ToString("hh:mm:ss tt");
        }

        private void But_iniciar_Click(object sender, EventArgs e)
        {  // 1. Obtener los datos ingresados.
            string usuario = txtUsuario.Text.Trim();
            string contrasena = txtContrasena.Text;

            // 2. Validar campos vacíos.
            if (usuario == "" || contrasena == "")
            {
                MessageBox.Show(
                    "Ingrese su usuario y contraseña.",
                    "Iniciar sesión",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                // 3. Buscar al empleado en SQL Server.
                DataTable empleados = BuscarEmpleado(usuario);

                // 4. Comprobar que exista una cuenta activa.
                if (empleados.Rows.Count != 1)
                {
                    MessageBox.Show(
                        "Usuario o contraseña incorrectos.",
                        "Acceso denegado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                DataRow empleado = empleados.Rows[0];

                // 5. Obtener el hash almacenado.
                string hashGuardado =
                    Convert.ToString(empleado["contraseña"]);

                // 6. Verificar la contraseña.
                if (!VerificarContrasena(
                    contrasena, hashGuardado))
                {
                    MessageBox.Show(
                        "Usuario o contraseña incorrectos.",
                        "Acceso denegado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                // 7. Obtener el identificador real.
                int idEmpleado =
                    Convert.ToInt32(empleado["id_empleado"]);

                // 8. Guardar la sesión del empleado.
                SesionActual.IniciarSesion(idEmpleado);

                // 9. Abrir el sistema principal.
                FRM_INICIO ventana = new FRM_INICIO();

                ventana.FormClosed += (s, args) =>
                {
                    SesionActual.CerrarSesion();
                    this.Close();
                };

                ventana.Show();
                this.Hide();
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "No fue posible iniciar sesión. " +
                    "Verifique la conexión con SQL Server.",
                    "Error de acceso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private DataTable BuscarEmpleado(string usuario)
        {
            DataTable tabla = new DataTable();

            string conexionSQL =
                BaseDatos.CadenaConexion;

            string consulta = @"
        SELECT
            id_empleado,
            id_sucursal,
            usuario,
            [contraseña],
            estado
        FROM dbo.EMPLEADO
        WHERE usuario = @Usuario
          AND estado = 'activo';";

            using (SqlConnection conexion =
                new SqlConnection(conexionSQL))
            {
                using (SqlCommand comando =
                    new SqlCommand(consulta, conexion))
                {
                    comando.Parameters.Add(
                        "@Usuario", SqlDbType.VarChar, 100
                    ).Value = usuario;

                    using (SqlDataAdapter adaptador =
                        new SqlDataAdapter(comando))
                    {
                        adaptador.Fill(tabla);
                    }
                }
            }

            return tabla;
        }

        private bool VerificarContrasena(
            string contrasenaIngresada,
            string hashGuardado)
        {
            // Calcular SHA-256 de la contraseña ingresada.
            using (SHA256 sha = SHA256.Create())
            {
                byte[] datos = Encoding.UTF8.GetBytes(
                    contrasenaIngresada);

                byte[] resultado = sha.ComputeHash(datos);

                // Convertir el resultado a hexadecimal.
                string hashCalculado =
                    BitConverter.ToString(resultado)
                    .Replace("-", "");

                // Comparar con el valor guardado en SQL.
                return string.Equals(
                    hashCalculado,
                    hashGuardado,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private void guna2CheckBox1_CheckedChanged(object sender, EventArgs e)
        {

            if (chkMostrarContrasena.Checked)
            {
                // Mostrar la contraseña.
                txtContrasena.UseSystemPasswordChar = false;
            }
            else
            {
                // Ocultar la contraseña.
                txtContrasena.UseSystemPasswordChar = true;
            }
        }
    }
}
