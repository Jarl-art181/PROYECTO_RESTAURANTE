using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // VENTANA "CUENTA DE ACCESO" (Gestión de Empleados › Opciones)
    // ----------------------------------------------------------
    // Consulta el estado de la cuenta de un empleado y permite:
    // crear la cuenta, habilitar/deshabilitar el acceso,
    // bloquear/desbloquear y restablecer la contraseña.
    // Se construye por código (mismo estilo que el diálogo de
    // cambio de contraseña), sin archivos .Designer ni .resx.
    // Nunca muestra hashes ni contraseñas guardadas.
    // ==========================================================
    internal sealed class VentanaCuentaAcceso : Form
    {
        private readonly int idAdministrador;
        private EmpleadoInfo empleado;

        private static readonly string[] Campos =
        {
            "Empleado", "Cargo", "Sucursal", "Estado laboral",
            "Usuario", "Acceso", "Bloqueo", "Contraseña"
        };
        private readonly TableLayoutPanel tablaDatos = new TableLayoutPanel();
        private readonly Label[] valores = new Label[Campos.Length];
        private readonly Label lblPermisos = new Label();
        private readonly Label lblUsuario = new Label();
        private readonly Guna2TextBox txtUsuario = new Guna2TextBox();
        private readonly Guna2Button btnCrear = Boton("Crear cuenta", Color.Gold);
        private readonly Guna2Button btnAcceso = Boton("", Color.Gold);
        private readonly Guna2Button btnBloqueo = Boton("", Color.Gold);
        private readonly Guna2Button btnRestablecer = Boton("Restablecer contraseña", Color.Gold);
        private readonly Guna2Button btnCerrar = Boton("Cerrar", Color.Gainsboro);

        // Indica a Gestión de Empleados que debe recargar la lista.
        public bool HuboCambios { get; private set; }

        internal VentanaCuentaAcceso(EmpleadoInfo empleado, int idAdministrador)
        {
            this.empleado = empleado;
            this.idAdministrador = idAdministrador;

            Text = "Cuenta de acceso";
            ClientSize = new Size(560, 500);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 10F);

            Guna2Panel encabezado = new Guna2Panel { Dock = DockStyle.Top, Height = 56, FillColor = Color.Gold };
            encabezado.Controls.Add(new Label
            {
                Text = "CUENTA DE ACCESO",
                Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            tablaDatos.Location = new Point(24, 70);
            tablaDatos.Size = new Size(512, 184);
            tablaDatos.ColumnCount = 2;
            tablaDatos.RowCount = Campos.Length;
            tablaDatos.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
            tablaDatos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < Campos.Length; i++)
            {
                tablaDatos.RowStyles.Add(new RowStyle(SizeType.Absolute, 23F));
                tablaDatos.Controls.Add(new Label
                {
                    Text = Campos[i] + ":",
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    AutoSize = true,
                    Margin = new Padding(0, 2, 0, 0)
                }, 0, i);
                valores[i] = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
                tablaDatos.Controls.Add(valores[i], 1, i);
            }

            lblPermisos.Location = new Point(24, 262);
            lblPermisos.Size = new Size(512, 62);
            lblPermisos.Font = new Font("Segoe UI", 9F);
            lblPermisos.ForeColor = Color.DimGray;

            lblUsuario.Text = "Usuario:";
            lblUsuario.Location = new Point(24, 345);
            lblUsuario.AutoSize = true;

            txtUsuario.Location = new Point(100, 336);
            txtUsuario.Size = new Size(240, 38);
            txtUsuario.BorderRadius = 8;
            txtUsuario.BorderColor = Color.Goldenrod;
            txtUsuario.ForeColor = Color.Black;
            txtUsuario.Font = new Font("Segoe UI", 10F);
            txtUsuario.MaxLength = CuentaDatos.UsuarioMaximo;
            txtUsuario.PlaceholderText = "p. ej. lquispe";

            btnCrear.Location = new Point(356, 336);
            btnCrear.Size = new Size(180, 38);

            btnAcceso.Bounds = new Rectangle(24, 336, 172, 38);
            btnBloqueo.Bounds = new Rectangle(208, 336, 128, 38);
            btnRestablecer.Bounds = new Rectangle(348, 336, 188, 38);

            btnCerrar.Location = new Point(356, 440);
            btnCerrar.Size = new Size(180, 38);

            btnCrear.Click += (s, e) => CrearCuenta();
            btnAcceso.Click += (s, e) => CambiarAcceso();
            btnBloqueo.Click += (s, e) => CambiarBloqueo();
            btnRestablecer.Click += (s, e) => Restablecer();
            btnCerrar.Click += (s, e) => Close();
            CancelButton = btnCerrar;

            Controls.AddRange(new Control[]
            {
                tablaDatos, lblPermisos, lblUsuario, txtUsuario, btnCrear,
                btnAcceso, btnBloqueo, btnRestablecer, btnCerrar, encabezado
            });

            // Guna2TextBox reajusta su posición al cambiar la fuente:
            // se fija de nuevo una vez agregado al formulario.
            txtUsuario.Bounds = new Rectangle(100, 336, 240, 38);

            Mostrar();

            if (!empleado.TieneUsuario && empleado.Estado == "activo")
            {
                try { txtUsuario.Text = CuentaDatos.SugerirUsuario(empleado.Nombre, empleado.Apellidos); }
                catch (Exception ex) { Registro.Error("Cuentas", ex); }
            }
        }

        // ------------------------------------------------------
        // ESTADO
        // ------------------------------------------------------
        private void Mostrar()
        {
            EmpleadoInfo e = empleado;

            string[] texto =
            {
                e.NombreCompleto + "  (E" + e.IdEmpleado.ToString("D4") + ")",
                string.IsNullOrEmpty(e.Cargo) ? "Sin cargo" : e.Cargo,
                e.Sucursal,
                e.Estado == "activo" ? "Activo" : "Inactivo",
                e.TieneUsuario ? e.Usuario : "Sin cuenta",
                !e.TieneUsuario ? "-" : e.AccesoHabilitado ? "Habilitado" : "Deshabilitado",
                TextoBloqueo(e),
                !e.TieneUsuario ? "-" : e.DebeCambiarContrasena
                    ? "Temporal (debe cambiarla al ingresar)" : "Definida por el empleado"
            };
            for (int i = 0; i < texto.Length; i++)
                valores[i].Text = texto[i];

            valores[3].ForeColor = e.Estado == "activo" ? Color.ForestGreen : Color.Firebrick;
            valores[5].ForeColor = e.AccesoHabilitado ? Color.ForestGreen : Color.Firebrick;
            valores[6].ForeColor = e.Bloqueada ? Color.Firebrick : Color.Black;

            lblPermisos.Text = "Permisos del perfil: " + Permisos.DescribirModulos(e.Cargo) +
                (e.Estado != "activo" ? "\nEl empleado está inactivo: no puede iniciar sesión." : "");

            bool sinCuenta = !e.TieneUsuario;
            lblUsuario.Visible = txtUsuario.Visible = btnCrear.Visible = sinCuenta;
            btnCrear.Enabled = sinCuenta && e.Estado == "activo" && !string.IsNullOrEmpty(e.Cargo);

            btnAcceso.Visible = btnBloqueo.Visible = btnRestablecer.Visible = !sinCuenta;
            btnAcceso.Text = e.AccesoHabilitado ? "Deshabilitar acceso" : "Habilitar acceso";
            btnBloqueo.Text = e.Bloqueada ? "Desbloquear" : "Bloquear";
        }

        internal static string TextoBloqueo(EmpleadoInfo e)
        {
            if (!e.TieneUsuario) return "-";
            if (!e.BloqueoHasta.HasValue) return "Sin bloqueo";
            return Autenticacion.EsBloqueoManual(e.BloqueoHasta.Value)
                ? "Bloqueada por el Administrador"
                : "Bloqueada por intentos fallidos hasta las " + e.BloqueoHasta.Value.ToString("HH:mm");
        }

        // Resumen corto para el panel de Gestión de Empleados.
        internal static string Resumen(EmpleadoInfo e)
        {
            if (!e.TieneUsuario) return "sin cuenta";
            if (e.Bloqueada) return "cuenta " + e.Usuario + " bloqueada";
            return "cuenta " + e.Usuario + (e.AccesoHabilitado ? "" : " deshabilitada");
        }

        private void Recargar()
        {
            HuboCambios = true;
            EmpleadoInfo nuevo = EmpleadoDatos.Obtener(empleado.IdEmpleado);
            if (nuevo != null)
                empleado = nuevo;
            Mostrar();
        }

        // ------------------------------------------------------
        // OPERACIONES
        // ------------------------------------------------------
        private void CrearCuenta()
        {
            Ejecutar(() =>
            {
                string temporal = CuentaDatos.CrearCuenta(empleado.IdEmpleado, txtUsuario.Text, idAdministrador);
                Recargar();
                MostrarContrasenaTemporal(this, empleado.NombreCompleto, empleado.Usuario, temporal, "Cuenta creada");
            });
        }

        private void CambiarAcceso()
        {
            bool habilitar = !empleado.AccesoHabilitado;
            if (!Confirmar(habilitar
                    ? "¿Habilitar el acceso de " + empleado.NombreCompleto + "?"
                    : "¿Deshabilitar el acceso de " + empleado.NombreCompleto + "?\n\n" +
                      "No podrá iniciar sesión. Sus datos e historial se conservan."))
                return;

            Ejecutar(() =>
            {
                CuentaDatos.CambiarAcceso(empleado.IdEmpleado, habilitar, idAdministrador);
                Recargar();
            });
        }

        private void CambiarBloqueo()
        {
            bool bloquear = !empleado.Bloqueada;
            if (!Confirmar(bloquear
                    ? "¿Bloquear la cuenta de " + empleado.NombreCompleto + "?\n\n" +
                      "No podrá iniciar sesión hasta que un Administrador la desbloquee."
                    : "¿Desbloquear la cuenta de " + empleado.NombreCompleto + "?"))
                return;

            Ejecutar(() =>
            {
                CuentaDatos.CambiarBloqueo(empleado.IdEmpleado, bloquear, idAdministrador);
                Recargar();
            });
        }

        private void Restablecer()
        {
            bool propia = empleado.IdEmpleado == idAdministrador;
            if (!Confirmar("¿Restablecer la contraseña de " + empleado.NombreCompleto + "?\n\n" +
                           "La contraseña actual dejará de funcionar y se generará una temporal" +
                           (propia ? ".\nEs su propia cuenta: deberá usarla en su próximo ingreso." : ".")))
                return;

            Ejecutar(() =>
            {
                string temporal = CuentaDatos.RestablecerContrasena(empleado.IdEmpleado, idAdministrador);
                Recargar();
                MostrarContrasenaTemporal(this, empleado.NombreCompleto, empleado.Usuario, temporal, "Contraseña restablecida");
            });
        }

        private bool Confirmar(string mensaje)
        {
            return MessageBox.Show(this, mensaje, "Cuenta de acceso",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void Ejecutar(Action accion)
        {
            UseWaitCursor = true;
            try
            {
                accion();
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(this, ex.Message, "Cuenta de acceso",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Cuentas", ex);
                MessageBox.Show(this, "No se pudo completar la operación. No se guardó ningún cambio.\n\n" +
                    ex.Message, "Cuenta de acceso", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        // ------------------------------------------------------
        // Muestra la contraseña temporal UNA sola vez.
        // ------------------------------------------------------
        internal static void MostrarContrasenaTemporal(
            IWin32Window propietario, string nombre, string usuario, string temporal, string titulo)
        {
            using (Form ventana = new Form())
            {
                ventana.Text = titulo;
                ventana.ClientSize = new Size(460, 300);
                ventana.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                ventana.StartPosition = FormStartPosition.CenterParent;
                ventana.BackColor = Color.White;
                ventana.ShowInTaskbar = false;

                Guna2Panel encabezado = new Guna2Panel { Dock = DockStyle.Top, Height = 50, FillColor = Color.Gold };
                encabezado.Controls.Add(new Label
                {
                    Text = "CONTRASEÑA TEMPORAL",
                    Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                });

                Label datos = new Label
                {
                    Text = nombre + "\nUsuario: " + usuario,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Location = new Point(24, 62),
                    Size = new Size(412, 44)
                };

                Guna2TextBox clave = new Guna2TextBox
                {
                    Text = temporal,
                    ReadOnly = true,
                    Location = new Point(24, 112),
                    Size = new Size(280, 44),
                    BorderRadius = 8,
                    BorderColor = Color.Goldenrod,
                    Font = new Font("Consolas", 16F, FontStyle.Bold),
                    ForeColor = Color.Black,
                    TextAlign = HorizontalAlignment.Center
                };

                Guna2Button copiar = Boton("Copiar", Color.Gold);
                copiar.Location = new Point(316, 115);
                copiar.Size = new Size(120, 38);
                copiar.Click += (s, e) =>
                {
                    try { Clipboard.SetText(temporal); copiar.Text = "Copiada"; }
                    catch (Exception ex) { Registro.Error("Cuentas", ex); }
                };

                Label nota = new Label
                {
                    Text = "Entréguela personalmente al empleado. No se volverá a mostrar ni " +
                           "se guarda en el sistema. Al ingresar deberá cambiarla por una propia.",
                    Font = new Font("Segoe UI", 9F),
                    ForeColor = Color.DimGray,
                    Location = new Point(24, 168),
                    Size = new Size(412, 54)
                };

                Guna2Button aceptar = Boton("Entendido", Color.Gold);
                aceptar.Location = new Point(256, 240);
                aceptar.Size = new Size(180, 38);
                aceptar.Click += (s, e) => ventana.Close();
                ventana.AcceptButton = aceptar;

                ventana.Controls.AddRange(new Control[] { datos, clave, copiar, nota, aceptar, encabezado });
                ventana.ShowDialog(propietario);
                clave.Text = "";
            }
        }

        private static Guna2Button Boton(string texto, Color relleno)
        {
            return new Guna2Button
            {
                Text = texto,
                Size = new Size(160, 38),
                BorderRadius = 10,
                FillColor = relleno,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
        }
    }
}
