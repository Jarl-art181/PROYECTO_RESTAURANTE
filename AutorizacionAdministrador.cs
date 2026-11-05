using Guna.UI2.WinForms;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Identifica al Administrador que autoriza una operación.
    //
    // * Si hay una sesión iniciada por un Administrador válido
    //   (FASE 2B/2C), se usa esa identidad.
    // * Mientras tanto, solicita usuario y contraseña de un
    //   Administrador y los comprueba contra SQL Server.
    //
    // No existen identificadores fijos ni claves en el código.
    // Cada operación vuelve a validar el id en SQL Server
    // (EmpleadoDatos.VerificarAdministrador).
    // ==========================================================
    internal static class AutorizacionAdministrador
    {
        public static int? Solicitar(IWin32Window propietario)
        {
            if (SesionActual.IdEmpleado.HasValue)
            {
                string error;
                if (EsAdministradorValido(SesionActual.IdEmpleado.Value, out error))
                    return SesionActual.IdEmpleado.Value;
            }

            return MostrarDialogo(propietario);
        }

        // Comprueba credenciales. Devuelve el id o null con el motivo.
        internal static int? Validar(string usuario, string contrasena, out string error)
        {
            error = null;
            usuario = (usuario ?? "").Trim();

            if (usuario.Length == 0 || string.IsNullOrEmpty(contrasena))
            {
                error = "Ingrese usuario y contraseña.";
                return null;
            }

            const string sql = @"
                SELECT E.id_empleado, E.[contraseña] AS clave, E.estado,
                       E.acceso_habilitado, E.bloqueo_hasta,
                       SYSDATETIME() AS ahora,
                       CASE WHEN A.id_empleado IS NULL THEN 0 ELSE 1 END AS es_admin
                FROM dbo.EMPLEADO E
                LEFT JOIN dbo.ADMINISTRADOR A ON A.id_empleado = E.id_empleado
                WHERE E.usuario = @Usuario;";

            using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
            using (SqlCommand cmd = new SqlCommand(sql, conexion))
            {
                cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 50).Value = usuario;
                conexion.Open();

                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    // Mensaje genérico: no revelar si el usuario existe.
                    if (!r.Read())
                    {
                        error = "Usuario o contraseña incorrectos.";
                        return null;
                    }

                    if (r["bloqueo_hasta"] != DBNull.Value &&
                        Convert.ToDateTime(r["bloqueo_hasta"]) > Convert.ToDateTime(r["ahora"]))
                    {
                        error = "La cuenta está bloqueada temporalmente.";
                        return null;
                    }

                    string guardada = r["clave"] == DBNull.Value ? null : Convert.ToString(r["clave"]);
                    if (!Contrasenas.Verificar(contrasena, guardada))
                    {
                        error = "Usuario o contraseña incorrectos.";
                        return null;
                    }

                    if (Convert.ToString(r["estado"]).Trim().ToLowerInvariant() != "activo" ||
                        !Convert.ToBoolean(r["acceso_habilitado"]))
                    {
                        error = "La cuenta no está habilitada.";
                        return null;
                    }

                    if (Convert.ToInt32(r["es_admin"]) != 1)
                    {
                        error = "Esta operación requiere un usuario Administrador.";
                        return null;
                    }

                    return Convert.ToInt32(r["id_empleado"]);
                }
            }
        }

        internal static bool EsAdministradorValido(int idEmpleado, out string error)
        {
            error = null;
            try
            {
                using (SqlConnection conexion = new SqlConnection(BaseDatos.CadenaConexion))
                {
                    conexion.Open();
                    EmpleadoDatos.VerificarAdministrador(conexion, null, idEmpleado);
                    return true;
                }
            }
            catch (ReglaNegocioException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------
        // Ventana de autorización (estilo amarillo/blanco Guna UI2)
        // ------------------------------------------------------
        private static int? MostrarDialogo(IWin32Window propietario)
        {
            int? autorizado = null;

            using (Form ventana = new Form())
            {
                ventana.Text = "Autorización de administrador";
                ventana.ClientSize = new Size(420, 300);
                ventana.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                ventana.StartPosition = FormStartPosition.CenterParent;
                ventana.BackColor = Color.White;
                ventana.ShowInTaskbar = false;
                ventana.MaximizeBox = false;
                ventana.MinimizeBox = false;

                Guna2Panel encabezado = new Guna2Panel();
                encabezado.Dock = DockStyle.Top;
                encabezado.Height = 56;
                encabezado.FillColor = Color.Gold;

                Label titulo = new Label();
                titulo.Text = "AUTORIZACIÓN DE ADMINISTRADOR";
                titulo.Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold);
                titulo.ForeColor = Color.Black;
                titulo.BackColor = Color.Transparent;
                titulo.AutoSize = false;
                titulo.TextAlign = ContentAlignment.MiddleCenter;
                titulo.Dock = DockStyle.Fill;
                encabezado.Controls.Add(titulo);

                Label nota = new Label();
                nota.Text = "Gestión de Empleados requiere las credenciales\nde un Administrador activo.";
                nota.Font = new Font("Segoe UI", 9F);
                nota.ForeColor = Color.DimGray;
                nota.Location = new Point(30, 66);
                nota.Size = new Size(360, 36);

                Label lblUsuario = CrearEtiqueta("Usuario:", 110);
                Guna2TextBox txtUsuario = CrearCaja(132);
                txtUsuario.MaxLength = 50;

                Label lblClave = CrearEtiqueta("Contraseña:", 172);
                Guna2TextBox txtClave = CrearCaja(194);
                txtClave.UseSystemPasswordChar = true;
                txtClave.MaxLength = 128;

                Label lblError = new Label();
                lblError.ForeColor = Color.Firebrick;
                lblError.Font = new Font("Segoe UI", 9F);
                lblError.Location = new Point(30, 232);
                lblError.Size = new Size(360, 18);

                Guna2Button btnAutorizar = CrearBoton("Autorizar", 220, Color.Gold);
                Guna2Button btnCancelar = CrearBoton("Cancelar", 30, Color.Gainsboro);

                Action intentar = () =>
                {
                    string error;
                    try
                    {
                        int? id = Validar(txtUsuario.Text, txtClave.Text, out error);
                        if (id.HasValue)
                        {
                            autorizado = id;
                            ventana.DialogResult = DialogResult.OK;
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        error = "No se pudo comprobar con SQL Server: " + ex.Message;
                    }

                    lblError.Text = error;
                    txtClave.Text = "";
                    txtClave.Focus();
                };

                btnAutorizar.Click += (s, e) => intentar();
                btnCancelar.Click += (s, e) => ventana.DialogResult = DialogResult.Cancel;
                txtClave.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        intentar();
                    }
                };

                ventana.Controls.AddRange(new Control[]
                {
                    nota, lblUsuario, txtUsuario, lblClave, txtClave,
                    lblError, btnAutorizar, btnCancelar, encabezado
                });

                ventana.Shown += (s, e) => txtUsuario.Focus();
                ventana.ShowDialog(propietario);

                // Borrar la contraseña de la memoria del control.
                txtClave.Text = "";
            }

            return autorizado;
        }

        private static Label CrearEtiqueta(string texto, int y)
        {
            Label etiqueta = new Label();
            etiqueta.Text = texto;
            etiqueta.Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            etiqueta.Location = new Point(30, y);
            etiqueta.AutoSize = true;
            return etiqueta;
        }

        private static Guna2TextBox CrearCaja(int y)
        {
            Guna2TextBox caja = new Guna2TextBox();
            caja.Location = new Point(30, y);
            caja.Size = new Size(360, 34);
            caja.BorderRadius = 8;
            caja.BorderColor = Color.Goldenrod;
            caja.FocusedState.BorderColor = Color.DarkGoldenrod;
            caja.Font = new Font("Segoe UI", 10F);
            caja.ForeColor = Color.Black;
            return caja;
        }

        private static Guna2Button CrearBoton(string texto, int x, Color fondo)
        {
            Guna2Button boton = new Guna2Button();
            boton.Text = texto;
            boton.Location = new Point(x, 254);
            boton.Size = new Size(170, 36);
            boton.BorderRadius = 10;
            boton.FillColor = fondo;
            boton.ForeColor = Color.Black;
            boton.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            return boton;
        }
    }
}
