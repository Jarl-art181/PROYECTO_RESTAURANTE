
using Guna.Charts.WinForms;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    public partial class FRM_CLIENTES : Form
    {

       
        public FRM_CLIENTES()
        {
            
            InitializeComponent();
            // ventana grande 
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            ConfigurarTablaClientes();
        }

        // Tabla de clientes: solo lectura, sin fila vacía editable,
        // columnas proporcionales y texto legible (antes era blanco).
        private void ConfigurarTablaClientes()
        {
            DataGridView tabla = guna2DataGridView1;
            tabla.AllowUserToAddRows = false;
            tabla.AllowUserToDeleteRows = false;
            tabla.ReadOnly = true;
            tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            EstiloTablas.Configurar(tabla, 38, 30);
            // Columnas cortas con ancho exacto; nombre y correo se reparten el resto.
            EstiloTablas.ColumnaFija(Column_N, 50, EstiloTablas.Centro);
            EstiloTablas.ColumnaFija(Column_tipo, 66, EstiloTablas.Izquierda);
            EstiloTablas.ColumnaFija(Column_Documento, 100, EstiloTablas.Centro);
            EstiloTablas.Columna(Column_nombre, 55, 110, EstiloTablas.Izquierda, true);
            EstiloTablas.ColumnaFija(Columntelefono, 88, EstiloTablas.Centro);
            EstiloTablas.Columna(Columncorreo, 45, 90, EstiloTablas.Izquierda);
        }


        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
       
      
       
        
        private void guna2Button2_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRMS_PEDIDOS>(this);
        }

        private void ButtonINICIO_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INICIO>(this);
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_MESAS>(this);
        }

        private void guna2Button5_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_COCINA>(this);
        }

        private void guna2Button4_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_CLIENTES>(this);
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_EMPLEADOS>(this);
        }
    }
    }


        

     