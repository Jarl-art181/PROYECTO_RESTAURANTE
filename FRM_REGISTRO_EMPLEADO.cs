using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Ventana modal para registrar o editar un empleado.
    // Guarda EMPLEADO y su cargo en una sola transacción
    // (EmpleadoDatos), validando la autorización en SQL Server.
    // ==========================================================
    public partial class FRM_REGISTRO_EMPLEADO : Form
    {
        private readonly int idAdministrador;
        private readonly EmpleadoInfo original;

        // Id del empleado guardado (para seleccionarlo en la tabla).
        internal int IdEmpleadoGuardado { get; private set; }

        // Constructor requerido por el diseñador de Visual Studio.
        public FRM_REGISTRO_EMPLEADO()
        {
            InitializeComponent();
        }

        internal FRM_REGISTRO_EMPLEADO(int idAdministrador, EmpleadoInfo existente)
            : this()
        {
            this.idAdministrador = idAdministrador;
            this.original = existente;

            PrepararControles();

            if (existente == null)
                PrepararNuevo();
            else
                CargarExistente(existente);
        }

        // ------------------------------------------------------
        // CONFIGURACIÓN
        // ------------------------------------------------------
        private void PrepararControles()
        {
            cmbCargo.Items.Clear();
            cmbCargo.Items.AddRange(Cargos.Todos);

            cmbTurno.Items.Clear();
            foreach (string turno in Turnos.Valores)
                cmbTurno.Items.Add(Turnos.Mostrar(turno));

            cmbEstado.Items.Clear();
            cmbEstado.Items.Add("Activo");
            cmbEstado.Items.Add("Inactivo");

            List<SucursalInfo> sucursales = EmpleadoDatos.ListarSucursales(
                original != null ? (int?)original.IdSucursal : null);
            cmbSucursal.Items.Clear();
            foreach (SucursalInfo s in sucursales)
                cmbSucursal.Items.Add(s);

            // Solo dígitos en DNI y teléfono.
            txtDni.KeyPress += SoloDigitos;
            txtTelefono.KeyPress += SoloDigitos;

            cmbCargo.SelectedIndexChanged += (s, e) => MostrarDatosDelCargo();

            btnGuardarEmpleado.Click += btnGuardarEmpleado_Click;
            btnCancelarRegistro.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            // Acceso al sistema (FASE B). Al registrar se puede crear la
            // cuenta en la misma operación; al editar, la cuenta se
            // administra en Opciones › Cuenta de acceso.
            // Nunca se muestran contraseñas guardadas ni hashes.
            chkHabilitarAcceso.Checked = false;
            chkHabilitarAcceso.Enabled = original == null;
            txtUsuario.Enabled = false;
            txtUsuario.MaxLength = CuentaDatos.UsuarioMaximo;
            chkHabilitarAcceso.CheckedChanged += (s, e) => PrepararAcceso();
            cmbEstado.SelectedIndexChanged += (s, e) =>
            {
                if (original == null && cmbEstado.SelectedIndex == 1)
                    chkHabilitarAcceso.Checked = false;
            };
            lblNotaAcceso.Text = original == null
                ? "Marque la casilla para crear su cuenta: se generará una contraseña " +
                  "temporal que deberá cambiar en su primer ingreso."
                : "La cuenta se administra desde Gestión de Empleados › Opciones › Cuenta de acceso.";
        }

        private void PrepararNuevo()
        {
            Text = "Nuevo empleado";
            lblTituloRegistro.Text = "REGISTRO DE EMPLEADO";

            cmbEstado.SelectedIndex = 0;   // Activo
            SeleccionarSucursal(BaseDatos.IdSucursalActual);
            MostrarDatosDelCargo();
        }

        private void CargarExistente(EmpleadoInfo e)
        {
            Text = "Editar empleado";
            lblTituloRegistro.Text = "EDITAR EMPLEADO · " + e.NombreCompleto.ToUpper();
            lblSubtituloRegistro.Text =
                "Código E" + e.IdEmpleado.ToString("D4") +
                ". Los cambios no modifican su historial de pedidos ni cobros.";

            txtDni.Text = e.Dni;
            txtNombres.Text = e.Nombre;
            txtApellidos.Text = e.Apellidos;
            txtTelefono.Text = e.Telefono;

            cmbCargo.SelectedItem = Cargos.Todos.Contains(e.Cargo) ? e.Cargo : null;

            int turno = Array.IndexOf(Turnos.Valores, e.Turno);
            cmbTurno.SelectedIndex = turno;

            cmbEstado.SelectedIndex = e.Estado == "inactivo" ? 1 : 0;
            SeleccionarSucursal(e.IdSucursal);

            switch (e.Cargo)
            {
                case Cargos.Mesero:
                    txtDatoCargo1.Text = e.AreaAsignada;
                    break;
                case Cargos.Cocinero:
                    txtDatoCargo1.Text = e.Certificacion;
                    break;
                case Cargos.Repartidor:
                    txtDatoCargo1.Text = e.ZonaReparto;
                    txtDatoCargo2.Text = e.Vehiculo;
                    break;
            }

            chkHabilitarAcceso.Checked = e.AccesoHabilitado;
            txtUsuario.Text = e.TieneUsuario ? e.Usuario : "(sin cuenta)";

            MostrarDatosDelCargo();
        }

        // Al marcar "Habilitar cuenta de acceso" se sugiere un usuario.
        private void PrepararAcceso()
        {
            if (original != null)
                return;

            bool habilitar = chkHabilitarAcceso.Checked;
            txtUsuario.Enabled = habilitar;

            if (habilitar && txtUsuario.Text.Trim().Length == 0 &&
                txtNombres.Text.Trim().Length > 0 && txtApellidos.Text.Trim().Length > 0)
            {
                try { txtUsuario.Text = CuentaDatos.SugerirUsuario(txtNombres.Text, txtApellidos.Text); }
                catch (Exception ex) { Registro.Error("Cuentas", ex); }
            }

            if (habilitar)
                txtUsuario.Focus();
        }

        private void SeleccionarSucursal(int idSucursal)
        {
            for (int i = 0; i < cmbSucursal.Items.Count; i++)
            {
                SucursalInfo s = (SucursalInfo)cmbSucursal.Items[i];
                if (s.IdSucursal == idSucursal)
                {
                    cmbSucursal.SelectedIndex = i;
                    return;
                }
            }

            if (cmbSucursal.Items.Count > 0)
                cmbSucursal.SelectedIndex = 0;
        }

        // Cada cargo tiene sus propios datos en SQL Server.
        private void MostrarDatosDelCargo()
        {
            string cargo = cmbCargo.SelectedItem as string;
            txtPerfil.Text = cargo ?? "";

            string titulo1 = null;
            string titulo2 = null;

            switch (cargo)
            {
                case Cargos.Mesero: titulo1 = "Área asignada"; break;
                case Cargos.Cocinero: titulo1 = "Certificación"; break;
                case Cargos.Repartidor: titulo1 = "Zona de reparto"; titulo2 = "Vehículo"; break;
            }

            lblDatoCargo1.Visible = txtDatoCargo1.Visible = titulo1 != null;
            lblDatoCargo2.Visible = txtDatoCargo2.Visible = titulo2 != null;
            lblSinDatosCargo.Visible = titulo1 == null;

            if (titulo1 != null) lblDatoCargo1.Text = titulo1;
            if (titulo2 != null) lblDatoCargo2.Text = titulo2;

            lblSinDatosCargo.Text = cargo == null
                ? "Seleccione el cargo del empleado."
                : "Este cargo no requiere datos adicionales.";
        }

        private static void SoloDigitos(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
                e.Handled = true;
        }

        // ------------------------------------------------------
        // GUARDAR
        // ------------------------------------------------------
        private EmpleadoInfo LeerFormulario()
        {
            EmpleadoInfo e = new EmpleadoInfo
            {
                IdEmpleado = original != null ? original.IdEmpleado : 0,
                Dni = txtDni.Text,
                Nombre = txtNombres.Text,
                Apellidos = txtApellidos.Text,
                Telefono = txtTelefono.Text,
                Cargo = cmbCargo.SelectedItem as string,
                Turno = cmbTurno.SelectedIndex >= 0 ? Turnos.Valores[cmbTurno.SelectedIndex] : null,
                Estado = cmbEstado.SelectedIndex == 1 ? "inactivo" : (cmbEstado.SelectedIndex == 0 ? "activo" : null),
                IdSucursal = cmbSucursal.SelectedItem is SucursalInfo
                    ? ((SucursalInfo)cmbSucursal.SelectedItem).IdSucursal : 0
            };

            switch (e.Cargo)
            {
                case Cargos.Mesero:
                    e.AreaAsignada = txtDatoCargo1.Text;
                    break;
                case Cargos.Cocinero:
                    e.Certificacion = txtDatoCargo1.Text;
                    break;
                case Cargos.Repartidor:
                    e.ZonaReparto = txtDatoCargo1.Text;
                    e.Vehiculo = txtDatoCargo2.Text;
                    break;
            }

            return e;
        }

        private void btnGuardarEmpleado_Click(object sender, EventArgs e)
        {
            EmpleadoInfo datos = LeerFormulario();

            try
            {
                EmpleadoDatos.Validar(datos);

                // Confirmar cambios sensibles al editar.
                if (original != null)
                {
                    string aviso = "";
                    if (original.Cargo != datos.Cargo)
                        aviso += "• Cambiar el cargo de " + original.Cargo + " a " + datos.Cargo + ".\n";
                    if (original.Estado == "activo" && datos.Estado == "inactivo")
                        aviso += "• Desactivar al empleado (su historial se conserva).\n";

                    if (aviso.Length > 0 &&
                        MessageBox.Show(
                            "Se realizarán estos cambios:\n\n" + aviso + "\n¿Desea continuar?",
                            "Confirmar cambios",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question,
                            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    {
                        return;
                    }
                }

                btnGuardarEmpleado.Enabled = false;
                UseWaitCursor = true;

                if (original == null)
                {
                    string temporal = null;
                    string usuario = null;

                    if (chkHabilitarAcceso.Checked)
                    {
                        usuario = CuentaDatos.NormalizarUsuario(txtUsuario.Text);
                        IdEmpleadoGuardado = CuentaDatos.RegistrarConCuenta(
                            datos, usuario, idAdministrador, out temporal);
                    }
                    else
                    {
                        IdEmpleadoGuardado = EmpleadoDatos.Registrar(datos, idAdministrador);
                    }

                    MessageBox.Show(
                        "Empleado registrado correctamente.\n\n" +
                        "Código: E" + IdEmpleadoGuardado.ToString("D4") + "\n" +
                        "Nombre: " + datos.NombreCompleto + "\n" +
                        "Cargo: " + datos.Cargo + "\n" +
                        "Acceso al sistema: " + (usuario == null ? "sin cuenta" : "usuario " + usuario),
                        "Gestión de empleados",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    if (temporal != null)
                    {
                        VentanaCuentaAcceso.MostrarContrasenaTemporal(
                            this, datos.NombreCompleto, usuario, temporal, "Cuenta creada");
                    }
                }
                else
                {
                    EmpleadoDatos.Actualizar(datos, idAdministrador);
                    IdEmpleadoGuardado = datos.IdEmpleado;

                    MessageBox.Show(
                        "Datos de " + datos.NombreCompleto + " actualizados correctamente.",
                        "Gestión de empleados",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de empleados",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    "No se pudo guardar en SQL Server. No se registró ningún cambio.\n\n" + ex.Message,
                    "Gestión de empleados",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnGuardarEmpleado.Enabled = true;
                UseWaitCursor = false;
            }
        }
    }
}
