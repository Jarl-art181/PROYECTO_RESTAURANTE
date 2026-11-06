namespace PROYECTO_RESTAURANTE
{
    partial class FRM_REGISTRO_EMPLEADO
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        private void InitializeComponent()
        {
            this.pnlEncabezado = new Guna.UI2.WinForms.Guna2Panel();
            this.lblSubtituloRegistro = new System.Windows.Forms.Label();
            this.lblTituloRegistro = new System.Windows.Forms.Label();
            this.grpDatosPersonales = new Guna.UI2.WinForms.Guna2GroupBox();
            this.txtTelefono = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblTelefono = new System.Windows.Forms.Label();
            this.txtApellidos = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblApellidos = new System.Windows.Forms.Label();
            this.txtNombres = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblNombres = new System.Windows.Forms.Label();
            this.txtDni = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblTipoDocumento = new System.Windows.Forms.Label();
            this.cmbTipoDocumento = new Guna.UI2.WinForms.Guna2ComboBox();
            this.lblDni = new System.Windows.Forms.Label();
            this.grpInformacionLaboral = new Guna.UI2.WinForms.Guna2GroupBox();
            this.lblSinDatosCargo = new System.Windows.Forms.Label();
            this.txtDatoCargo2 = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblDatoCargo2 = new System.Windows.Forms.Label();
            this.txtDatoCargo1 = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblDatoCargo1 = new System.Windows.Forms.Label();
            this.cmbEstado = new Guna.UI2.WinForms.Guna2ComboBox();
            this.lblEstado = new System.Windows.Forms.Label();
            this.cmbSucursal = new Guna.UI2.WinForms.Guna2ComboBox();
            this.lblSucursal = new System.Windows.Forms.Label();
            this.cmbTurno = new Guna.UI2.WinForms.Guna2ComboBox();
            this.lblTurno = new System.Windows.Forms.Label();
            this.cmbCargo = new Guna.UI2.WinForms.Guna2ComboBox();
            this.lblCargo = new System.Windows.Forms.Label();
            this.grpAccesoSistema = new Guna.UI2.WinForms.Guna2GroupBox();
            this.lblNotaAcceso = new System.Windows.Forms.Label();
            this.txtPerfil = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblPerfil = new System.Windows.Forms.Label();
            this.txtUsuario = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblUsuario = new System.Windows.Forms.Label();
            this.chkHabilitarAcceso = new Guna.UI2.WinForms.Guna2CheckBox();
            this.btnGuardarEmpleado = new Guna.UI2.WinForms.Guna2Button();
            this.btnCancelarRegistro = new Guna.UI2.WinForms.Guna2Button();
            this.pnlEncabezado.SuspendLayout();
            this.grpDatosPersonales.SuspendLayout();
            this.grpInformacionLaboral.SuspendLayout();
            this.grpAccesoSistema.SuspendLayout();
            this.SuspendLayout();
            //
            // pnlEncabezado
            //
            this.pnlEncabezado.Controls.Add(this.lblSubtituloRegistro);
            this.pnlEncabezado.Controls.Add(this.lblTituloRegistro);
            this.pnlEncabezado.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlEncabezado.FillColor = System.Drawing.Color.Gold;
            this.pnlEncabezado.Location = new System.Drawing.Point(0, 0);
            this.pnlEncabezado.Name = "pnlEncabezado";
            this.pnlEncabezado.Size = new System.Drawing.Size(780, 64);
            this.pnlEncabezado.TabIndex = 0;
            //
            // lblSubtituloRegistro
            //
            this.lblSubtituloRegistro.BackColor = System.Drawing.Color.Transparent;
            this.lblSubtituloRegistro.Font = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Italic);
            this.lblSubtituloRegistro.Location = new System.Drawing.Point(22, 38);
            this.lblSubtituloRegistro.Name = "lblSubtituloRegistro";
            this.lblSubtituloRegistro.Size = new System.Drawing.Size(730, 20);
            this.lblSubtituloRegistro.TabIndex = 1;
            this.lblSubtituloRegistro.Text = "Complete los datos del trabajador. Los campos con * son obligatorios.";
            //
            // lblTituloRegistro
            //
            this.lblTituloRegistro.BackColor = System.Drawing.Color.Transparent;
            this.lblTituloRegistro.Font = new System.Drawing.Font("Arial", 15.75F, System.Drawing.FontStyle.Bold);
            this.lblTituloRegistro.Location = new System.Drawing.Point(20, 8);
            this.lblTituloRegistro.Name = "lblTituloRegistro";
            this.lblTituloRegistro.Size = new System.Drawing.Size(730, 28);
            this.lblTituloRegistro.TabIndex = 0;
            this.lblTituloRegistro.Text = "REGISTRO DE EMPLEADO";
            //
            // grpDatosPersonales
            //
            this.grpDatosPersonales.BorderColor = System.Drawing.Color.Goldenrod;
            this.grpDatosPersonales.BorderRadius = 8;
            this.grpDatosPersonales.Controls.Add(this.txtTelefono);
            this.grpDatosPersonales.Controls.Add(this.lblTelefono);
            this.grpDatosPersonales.Controls.Add(this.txtApellidos);
            this.grpDatosPersonales.Controls.Add(this.lblApellidos);
            this.grpDatosPersonales.Controls.Add(this.txtNombres);
            this.grpDatosPersonales.Controls.Add(this.lblNombres);
            this.grpDatosPersonales.Controls.Add(this.txtDni);
            this.grpDatosPersonales.Controls.Add(this.lblTipoDocumento);
            this.grpDatosPersonales.Controls.Add(this.cmbTipoDocumento);
            this.grpDatosPersonales.Controls.Add(this.lblDni);
            this.grpDatosPersonales.CustomBorderColor = System.Drawing.Color.Gold;
            this.grpDatosPersonales.Font = new System.Drawing.Font("Arial", 11F, System.Drawing.FontStyle.Bold);
            this.grpDatosPersonales.ForeColor = System.Drawing.Color.Black;
            this.grpDatosPersonales.Location = new System.Drawing.Point(20, 76);
            this.grpDatosPersonales.Name = "grpDatosPersonales";
            this.grpDatosPersonales.Size = new System.Drawing.Size(740, 124);
            this.grpDatosPersonales.TabIndex = 1;
            this.grpDatosPersonales.Text = "1. DATOS PERSONALES";
            //
            // lblTipoDocumento
            //
            this.lblTipoDocumento.AutoSize = true;
            this.lblTipoDocumento.BackColor = System.Drawing.Color.Transparent;
            this.lblTipoDocumento.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblTipoDocumento.Location = new System.Drawing.Point(18, 50);
            this.lblTipoDocumento.Name = "lblTipoDocumento";
            this.lblTipoDocumento.TabIndex = 0;
            this.lblTipoDocumento.Text = "Tipo de doc. *";
            //
            // cmbTipoDocumento
            //
            this.cmbTipoDocumento.BackColor = System.Drawing.Color.Transparent;
            this.cmbTipoDocumento.BorderColor = System.Drawing.Color.Goldenrod;
            this.cmbTipoDocumento.BorderRadius = 8;
            this.cmbTipoDocumento.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cmbTipoDocumento.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTipoDocumento.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbTipoDocumento.ForeColor = System.Drawing.Color.Black;
            this.cmbTipoDocumento.ItemHeight = 30;
            this.cmbTipoDocumento.Location = new System.Drawing.Point(18, 72);
            this.cmbTipoDocumento.Name = "cmbTipoDocumento";
            this.cmbTipoDocumento.Size = new System.Drawing.Size(110, 36);
            this.cmbTipoDocumento.TabIndex = 0;
            //
            // lblDni
            //
            this.lblDni.AutoSize = true;
            this.lblDni.BackColor = System.Drawing.Color.Transparent;
            this.lblDni.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblDni.Location = new System.Drawing.Point(139, 50);
            this.lblDni.Name = "lblDni";
            this.lblDni.TabIndex = 0;
            this.lblDni.Text = "DNI *";
            //
            // txtDni
            //
            this.txtDni.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtDni.BorderRadius = 8;
            this.txtDni.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtDni.ForeColor = System.Drawing.Color.Black;
            this.txtDni.Location = new System.Drawing.Point(139, 72);
            this.txtDni.MaxLength = 8;
            this.txtDni.Name = "txtDni";
            this.txtDni.PlaceholderText = "8 dígitos";
            this.txtDni.Size = new System.Drawing.Size(120, 36);
            this.txtDni.TabIndex = 1;
            //
            // lblNombres
            //
            this.lblNombres.AutoSize = true;
            this.lblNombres.BackColor = System.Drawing.Color.Transparent;
            this.lblNombres.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblNombres.Location = new System.Drawing.Point(270, 50);
            this.lblNombres.Name = "lblNombres";
            this.lblNombres.TabIndex = 2;
            this.lblNombres.Text = "Nombres *";
            //
            // txtNombres
            //
            this.txtNombres.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtNombres.BorderRadius = 8;
            this.txtNombres.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtNombres.ForeColor = System.Drawing.Color.Black;
            this.txtNombres.Location = new System.Drawing.Point(270, 72);
            this.txtNombres.MaxLength = 100;
            this.txtNombres.Name = "txtNombres";
            this.txtNombres.Size = new System.Drawing.Size(160, 36);
            this.txtNombres.TabIndex = 3;
            //
            // lblApellidos
            //
            this.lblApellidos.AutoSize = true;
            this.lblApellidos.BackColor = System.Drawing.Color.Transparent;
            this.lblApellidos.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblApellidos.Location = new System.Drawing.Point(441, 50);
            this.lblApellidos.Name = "lblApellidos";
            this.lblApellidos.TabIndex = 4;
            this.lblApellidos.Text = "Apellidos *";
            //
            // txtApellidos
            //
            this.txtApellidos.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtApellidos.BorderRadius = 8;
            this.txtApellidos.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtApellidos.ForeColor = System.Drawing.Color.Black;
            this.txtApellidos.Location = new System.Drawing.Point(441, 72);
            this.txtApellidos.MaxLength = 100;
            this.txtApellidos.Name = "txtApellidos";
            this.txtApellidos.Size = new System.Drawing.Size(160, 36);
            this.txtApellidos.TabIndex = 5;
            //
            // lblTelefono
            //
            this.lblTelefono.AutoSize = true;
            this.lblTelefono.BackColor = System.Drawing.Color.Transparent;
            this.lblTelefono.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblTelefono.Location = new System.Drawing.Point(612, 50);
            this.lblTelefono.Name = "lblTelefono";
            this.lblTelefono.TabIndex = 6;
            this.lblTelefono.Text = "Teléfono";
            //
            // txtTelefono
            //
            this.txtTelefono.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtTelefono.BorderRadius = 8;
            this.txtTelefono.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtTelefono.ForeColor = System.Drawing.Color.Black;
            this.txtTelefono.Location = new System.Drawing.Point(612, 72);
            this.txtTelefono.MaxLength = 9;
            this.txtTelefono.Name = "txtTelefono";
            this.txtTelefono.PlaceholderText = "9 dígitos";
            this.txtTelefono.Size = new System.Drawing.Size(110, 36);
            this.txtTelefono.TabIndex = 7;
            //
            // grpInformacionLaboral
            //
            this.grpInformacionLaboral.BorderColor = System.Drawing.Color.Goldenrod;
            this.grpInformacionLaboral.BorderRadius = 8;
            this.grpInformacionLaboral.Controls.Add(this.lblSinDatosCargo);
            this.grpInformacionLaboral.Controls.Add(this.txtDatoCargo2);
            this.grpInformacionLaboral.Controls.Add(this.lblDatoCargo2);
            this.grpInformacionLaboral.Controls.Add(this.txtDatoCargo1);
            this.grpInformacionLaboral.Controls.Add(this.lblDatoCargo1);
            this.grpInformacionLaboral.Controls.Add(this.cmbEstado);
            this.grpInformacionLaboral.Controls.Add(this.lblEstado);
            this.grpInformacionLaboral.Controls.Add(this.cmbSucursal);
            this.grpInformacionLaboral.Controls.Add(this.lblSucursal);
            this.grpInformacionLaboral.Controls.Add(this.cmbTurno);
            this.grpInformacionLaboral.Controls.Add(this.lblTurno);
            this.grpInformacionLaboral.Controls.Add(this.cmbCargo);
            this.grpInformacionLaboral.Controls.Add(this.lblCargo);
            this.grpInformacionLaboral.CustomBorderColor = System.Drawing.Color.Gold;
            this.grpInformacionLaboral.Font = new System.Drawing.Font("Arial", 11F, System.Drawing.FontStyle.Bold);
            this.grpInformacionLaboral.ForeColor = System.Drawing.Color.Black;
            this.grpInformacionLaboral.Location = new System.Drawing.Point(20, 212);
            this.grpInformacionLaboral.Name = "grpInformacionLaboral";
            this.grpInformacionLaboral.Size = new System.Drawing.Size(740, 192);
            this.grpInformacionLaboral.TabIndex = 2;
            this.grpInformacionLaboral.Text = "2. INFORMACIÓN LABORAL";
            //
            // lblCargo
            //
            this.lblCargo.AutoSize = true;
            this.lblCargo.BackColor = System.Drawing.Color.Transparent;
            this.lblCargo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblCargo.Location = new System.Drawing.Point(18, 50);
            this.lblCargo.Name = "lblCargo";
            this.lblCargo.TabIndex = 0;
            this.lblCargo.Text = "Cargo *";
            //
            // cmbCargo
            //
            this.cmbCargo.BackColor = System.Drawing.Color.Transparent;
            this.cmbCargo.BorderColor = System.Drawing.Color.Goldenrod;
            this.cmbCargo.BorderRadius = 8;
            this.cmbCargo.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cmbCargo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbCargo.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbCargo.ForeColor = System.Drawing.Color.Black;
            this.cmbCargo.ItemHeight = 30;
            this.cmbCargo.Location = new System.Drawing.Point(18, 72);
            this.cmbCargo.Name = "cmbCargo";
            this.cmbCargo.Size = new System.Drawing.Size(176, 36);
            this.cmbCargo.TabIndex = 1;
            //
            // lblTurno
            //
            this.lblTurno.AutoSize = true;
            this.lblTurno.BackColor = System.Drawing.Color.Transparent;
            this.lblTurno.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblTurno.Location = new System.Drawing.Point(208, 50);
            this.lblTurno.Name = "lblTurno";
            this.lblTurno.TabIndex = 2;
            this.lblTurno.Text = "Turno *";
            //
            // cmbTurno
            //
            this.cmbTurno.BackColor = System.Drawing.Color.Transparent;
            this.cmbTurno.BorderColor = System.Drawing.Color.Goldenrod;
            this.cmbTurno.BorderRadius = 8;
            this.cmbTurno.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cmbTurno.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTurno.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbTurno.ForeColor = System.Drawing.Color.Black;
            this.cmbTurno.ItemHeight = 30;
            this.cmbTurno.Location = new System.Drawing.Point(208, 72);
            this.cmbTurno.Name = "cmbTurno";
            this.cmbTurno.Size = new System.Drawing.Size(140, 36);
            this.cmbTurno.TabIndex = 3;
            //
            // lblSucursal
            //
            this.lblSucursal.AutoSize = true;
            this.lblSucursal.BackColor = System.Drawing.Color.Transparent;
            this.lblSucursal.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblSucursal.Location = new System.Drawing.Point(362, 50);
            this.lblSucursal.Name = "lblSucursal";
            this.lblSucursal.TabIndex = 4;
            this.lblSucursal.Text = "Sucursal *";
            //
            // cmbSucursal
            //
            this.cmbSucursal.BackColor = System.Drawing.Color.Transparent;
            this.cmbSucursal.BorderColor = System.Drawing.Color.Goldenrod;
            this.cmbSucursal.BorderRadius = 8;
            this.cmbSucursal.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cmbSucursal.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSucursal.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbSucursal.ForeColor = System.Drawing.Color.Black;
            this.cmbSucursal.ItemHeight = 30;
            this.cmbSucursal.Location = new System.Drawing.Point(362, 72);
            this.cmbSucursal.Name = "cmbSucursal";
            this.cmbSucursal.Size = new System.Drawing.Size(200, 36);
            this.cmbSucursal.TabIndex = 5;
            //
            // lblEstado
            //
            this.lblEstado.AutoSize = true;
            this.lblEstado.BackColor = System.Drawing.Color.Transparent;
            this.lblEstado.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblEstado.Location = new System.Drawing.Point(576, 50);
            this.lblEstado.Name = "lblEstado";
            this.lblEstado.TabIndex = 6;
            this.lblEstado.Text = "Estado laboral *";
            //
            // cmbEstado
            //
            this.cmbEstado.BackColor = System.Drawing.Color.Transparent;
            this.cmbEstado.BorderColor = System.Drawing.Color.Goldenrod;
            this.cmbEstado.BorderRadius = 8;
            this.cmbEstado.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.cmbEstado.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbEstado.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.cmbEstado.ForeColor = System.Drawing.Color.Black;
            this.cmbEstado.ItemHeight = 30;
            this.cmbEstado.Location = new System.Drawing.Point(576, 72);
            this.cmbEstado.Name = "cmbEstado";
            this.cmbEstado.Size = new System.Drawing.Size(146, 36);
            this.cmbEstado.TabIndex = 7;
            //
            // lblDatoCargo1
            //
            this.lblDatoCargo1.AutoSize = true;
            this.lblDatoCargo1.BackColor = System.Drawing.Color.Transparent;
            this.lblDatoCargo1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblDatoCargo1.Location = new System.Drawing.Point(18, 120);
            this.lblDatoCargo1.Name = "lblDatoCargo1";
            this.lblDatoCargo1.TabIndex = 8;
            this.lblDatoCargo1.Text = "Dato del cargo";
            //
            // txtDatoCargo1
            //
            this.txtDatoCargo1.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtDatoCargo1.BorderRadius = 8;
            this.txtDatoCargo1.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtDatoCargo1.ForeColor = System.Drawing.Color.Black;
            this.txtDatoCargo1.Location = new System.Drawing.Point(18, 142);
            this.txtDatoCargo1.MaxLength = 100;
            this.txtDatoCargo1.Name = "txtDatoCargo1";
            this.txtDatoCargo1.Size = new System.Drawing.Size(344, 36);
            this.txtDatoCargo1.TabIndex = 9;
            //
            // lblDatoCargo2
            //
            this.lblDatoCargo2.AutoSize = true;
            this.lblDatoCargo2.BackColor = System.Drawing.Color.Transparent;
            this.lblDatoCargo2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblDatoCargo2.Location = new System.Drawing.Point(378, 120);
            this.lblDatoCargo2.Name = "lblDatoCargo2";
            this.lblDatoCargo2.TabIndex = 10;
            this.lblDatoCargo2.Text = "Dato del cargo";
            //
            // txtDatoCargo2
            //
            this.txtDatoCargo2.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtDatoCargo2.BorderRadius = 8;
            this.txtDatoCargo2.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtDatoCargo2.ForeColor = System.Drawing.Color.Black;
            this.txtDatoCargo2.Location = new System.Drawing.Point(378, 142);
            this.txtDatoCargo2.MaxLength = 100;
            this.txtDatoCargo2.Name = "txtDatoCargo2";
            this.txtDatoCargo2.Size = new System.Drawing.Size(344, 36);
            this.txtDatoCargo2.TabIndex = 11;
            //
            // lblSinDatosCargo
            //
            this.lblSinDatosCargo.BackColor = System.Drawing.Color.Transparent;
            this.lblSinDatosCargo.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Italic);
            this.lblSinDatosCargo.ForeColor = System.Drawing.Color.DimGray;
            this.lblSinDatosCargo.Location = new System.Drawing.Point(18, 132);
            this.lblSinDatosCargo.Name = "lblSinDatosCargo";
            this.lblSinDatosCargo.Size = new System.Drawing.Size(700, 40);
            this.lblSinDatosCargo.TabIndex = 12;
            this.lblSinDatosCargo.Text = "Este cargo no requiere datos adicionales.";
            //
            // grpAccesoSistema
            //
            this.grpAccesoSistema.BorderColor = System.Drawing.Color.Goldenrod;
            this.grpAccesoSistema.BorderRadius = 8;
            this.grpAccesoSistema.Controls.Add(this.lblNotaAcceso);
            this.grpAccesoSistema.Controls.Add(this.txtPerfil);
            this.grpAccesoSistema.Controls.Add(this.lblPerfil);
            this.grpAccesoSistema.Controls.Add(this.txtUsuario);
            this.grpAccesoSistema.Controls.Add(this.lblUsuario);
            this.grpAccesoSistema.Controls.Add(this.chkHabilitarAcceso);
            this.grpAccesoSistema.CustomBorderColor = System.Drawing.Color.Gold;
            this.grpAccesoSistema.Font = new System.Drawing.Font("Arial", 11F, System.Drawing.FontStyle.Bold);
            this.grpAccesoSistema.ForeColor = System.Drawing.Color.Black;
            this.grpAccesoSistema.Location = new System.Drawing.Point(20, 416);
            this.grpAccesoSistema.Name = "grpAccesoSistema";
            this.grpAccesoSistema.Size = new System.Drawing.Size(740, 150);
            this.grpAccesoSistema.TabIndex = 3;
            this.grpAccesoSistema.Text = "3. ACCESO AL SISTEMA";
            //
            // chkHabilitarAcceso
            //
            this.chkHabilitarAcceso.AutoSize = true;
            this.chkHabilitarAcceso.BackColor = System.Drawing.Color.Transparent;
            this.chkHabilitarAcceso.CheckedState.BorderColor = System.Drawing.Color.Goldenrod;
            this.chkHabilitarAcceso.CheckedState.BorderRadius = 2;
            this.chkHabilitarAcceso.CheckedState.BorderThickness = 0;
            this.chkHabilitarAcceso.CheckedState.FillColor = System.Drawing.Color.Goldenrod;
            this.chkHabilitarAcceso.Enabled = false;
            this.chkHabilitarAcceso.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.chkHabilitarAcceso.Location = new System.Drawing.Point(18, 78);
            this.chkHabilitarAcceso.Name = "chkHabilitarAcceso";
            this.chkHabilitarAcceso.TabIndex = 0;
            this.chkHabilitarAcceso.Text = "Habilitar cuenta de acceso";
            this.chkHabilitarAcceso.UncheckedState.BorderColor = System.Drawing.Color.Goldenrod;
            this.chkHabilitarAcceso.UncheckedState.BorderRadius = 2;
            this.chkHabilitarAcceso.UncheckedState.BorderThickness = 1;
            this.chkHabilitarAcceso.UncheckedState.FillColor = System.Drawing.Color.White;
            this.chkHabilitarAcceso.UseVisualStyleBackColor = false;
            //
            // lblUsuario
            //
            this.lblUsuario.AutoSize = true;
            this.lblUsuario.BackColor = System.Drawing.Color.Transparent;
            this.lblUsuario.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblUsuario.Location = new System.Drawing.Point(300, 50);
            this.lblUsuario.Name = "lblUsuario";
            this.lblUsuario.TabIndex = 1;
            this.lblUsuario.Text = "Usuario";
            //
            // txtUsuario
            //
            this.txtUsuario.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtUsuario.BorderRadius = 8;
            this.txtUsuario.Enabled = false;
            this.txtUsuario.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtUsuario.ForeColor = System.Drawing.Color.Black;
            this.txtUsuario.Location = new System.Drawing.Point(300, 72);
            this.txtUsuario.MaxLength = 50;
            this.txtUsuario.Name = "txtUsuario";
            this.txtUsuario.Size = new System.Drawing.Size(200, 36);
            this.txtUsuario.TabIndex = 2;
            //
            // lblPerfil
            //
            this.lblPerfil.AutoSize = true;
            this.lblPerfil.BackColor = System.Drawing.Color.Transparent;
            this.lblPerfil.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblPerfil.Location = new System.Drawing.Point(522, 50);
            this.lblPerfil.Name = "lblPerfil";
            this.lblPerfil.TabIndex = 3;
            this.lblPerfil.Text = "Perfil de permisos";
            //
            // txtPerfil
            //
            this.txtPerfil.BorderColor = System.Drawing.Color.Goldenrod;
            this.txtPerfil.BorderRadius = 8;
            this.txtPerfil.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.txtPerfil.ForeColor = System.Drawing.Color.Black;
            this.txtPerfil.Location = new System.Drawing.Point(522, 72);
            this.txtPerfil.Name = "txtPerfil";
            this.txtPerfil.ReadOnly = true;
            this.txtPerfil.Size = new System.Drawing.Size(200, 36);
            this.txtPerfil.TabIndex = 4;
            //
            // lblNotaAcceso
            //
            this.lblNotaAcceso.BackColor = System.Drawing.Color.Transparent;
            this.lblNotaAcceso.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);
            this.lblNotaAcceso.ForeColor = System.Drawing.Color.DimGray;
            this.lblNotaAcceso.Location = new System.Drawing.Point(18, 114);
            this.lblNotaAcceso.Name = "lblNotaAcceso";
            this.lblNotaAcceso.Size = new System.Drawing.Size(704, 32);
            this.lblNotaAcceso.TabIndex = 5;
            this.lblNotaAcceso.Text = "El perfil de permisos corresponde al cargo.";
            //
            // btnGuardarEmpleado
            //
            this.btnGuardarEmpleado.BorderRadius = 10;
            this.btnGuardarEmpleado.FillColor = System.Drawing.Color.Gold;
            this.btnGuardarEmpleado.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnGuardarEmpleado.ForeColor = System.Drawing.Color.Black;
            this.btnGuardarEmpleado.Location = new System.Drawing.Point(570, 580);
            this.btnGuardarEmpleado.Name = "btnGuardarEmpleado";
            this.btnGuardarEmpleado.Size = new System.Drawing.Size(190, 44);
            this.btnGuardarEmpleado.TabIndex = 4;
            this.btnGuardarEmpleado.Text = "Guardar";
            //
            // btnCancelarRegistro
            //
            this.btnCancelarRegistro.BorderRadius = 10;
            this.btnCancelarRegistro.FillColor = System.Drawing.Color.Gainsboro;
            this.btnCancelarRegistro.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            this.btnCancelarRegistro.ForeColor = System.Drawing.Color.Black;
            this.btnCancelarRegistro.Location = new System.Drawing.Point(366, 580);
            this.btnCancelarRegistro.Name = "btnCancelarRegistro";
            this.btnCancelarRegistro.Size = new System.Drawing.Size(190, 44);
            this.btnCancelarRegistro.TabIndex = 5;
            this.btnCancelarRegistro.Text = "Cancelar";
            //
            // FRM_REGISTRO_EMPLEADO
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(780, 640);
            this.Controls.Add(this.btnCancelarRegistro);
            this.Controls.Add(this.btnGuardarEmpleado);
            this.Controls.Add(this.grpAccesoSistema);
            this.Controls.Add(this.grpInformacionLaboral);
            this.Controls.Add(this.grpDatosPersonales);
            this.Controls.Add(this.pnlEncabezado);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FRM_REGISTRO_EMPLEADO";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Gestión de empleados";
            this.pnlEncabezado.ResumeLayout(false);
            this.grpDatosPersonales.ResumeLayout(false);
            this.grpDatosPersonales.PerformLayout();
            this.grpInformacionLaboral.ResumeLayout(false);
            this.grpInformacionLaboral.PerformLayout();
            this.grpAccesoSistema.ResumeLayout(false);
            this.grpAccesoSistema.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private Guna.UI2.WinForms.Guna2Panel pnlEncabezado;
        private System.Windows.Forms.Label lblTituloRegistro;
        private System.Windows.Forms.Label lblSubtituloRegistro;
        private Guna.UI2.WinForms.Guna2GroupBox grpDatosPersonales;
        private System.Windows.Forms.Label lblDni;
        private Guna.UI2.WinForms.Guna2TextBox txtDni;
        private System.Windows.Forms.Label lblTipoDocumento;
        private Guna.UI2.WinForms.Guna2ComboBox cmbTipoDocumento;
        private System.Windows.Forms.Label lblNombres;
        private Guna.UI2.WinForms.Guna2TextBox txtNombres;
        private System.Windows.Forms.Label lblApellidos;
        private Guna.UI2.WinForms.Guna2TextBox txtApellidos;
        private System.Windows.Forms.Label lblTelefono;
        private Guna.UI2.WinForms.Guna2TextBox txtTelefono;
        private Guna.UI2.WinForms.Guna2GroupBox grpInformacionLaboral;
        private System.Windows.Forms.Label lblCargo;
        private Guna.UI2.WinForms.Guna2ComboBox cmbCargo;
        private System.Windows.Forms.Label lblTurno;
        private Guna.UI2.WinForms.Guna2ComboBox cmbTurno;
        private System.Windows.Forms.Label lblSucursal;
        private Guna.UI2.WinForms.Guna2ComboBox cmbSucursal;
        private System.Windows.Forms.Label lblEstado;
        private Guna.UI2.WinForms.Guna2ComboBox cmbEstado;
        private System.Windows.Forms.Label lblDatoCargo1;
        private Guna.UI2.WinForms.Guna2TextBox txtDatoCargo1;
        private System.Windows.Forms.Label lblDatoCargo2;
        private Guna.UI2.WinForms.Guna2TextBox txtDatoCargo2;
        private System.Windows.Forms.Label lblSinDatosCargo;
        private Guna.UI2.WinForms.Guna2GroupBox grpAccesoSistema;
        private Guna.UI2.WinForms.Guna2CheckBox chkHabilitarAcceso;
        private System.Windows.Forms.Label lblUsuario;
        private Guna.UI2.WinForms.Guna2TextBox txtUsuario;
        private System.Windows.Forms.Label lblPerfil;
        private Guna.UI2.WinForms.Guna2TextBox txtPerfil;
        private System.Windows.Forms.Label lblNotaAcceso;
        private Guna.UI2.WinForms.Guna2Button btnGuardarEmpleado;
        private Guna.UI2.WinForms.Guna2Button btnCancelarRegistro;
    }
}
