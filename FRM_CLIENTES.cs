
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

        }


        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
       
      
       
        
        private void guna2Button2_Click(object sender, EventArgs e)
        {
            FRMS_PEDIDOS pedidos = new FRMS_PEDIDOS();

            pedidos.Show(); 
            this.Hide();    
        }

        private void ButtonINICIO_Click(object sender, EventArgs e)
        {
            FRM_INICIO pedidos = new FRM_INICIO();

            pedidos.Show();
            this.Hide();
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            FRM_MESAS pedidos = new FRM_MESAS();

            pedidos.Show();
            this.Hide();
        }

        private void guna2Button5_Click(object sender, EventArgs e)
        {
            FRM_COCINA pedidos = new FRM_COCINA();

            pedidos.Show();
            this.Hide();
        }

        private void guna2Button4_Click(object sender, EventArgs e)
        {
            FRM_CLIENTES pedidos = new FRM_CLIENTES();

            pedidos.Show();
            this.Hide();
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
            FRM_EMPLEADOS pedidos = new FRM_EMPLEADOS();

            pedidos.Show();
            this.Hide();
        }
    }
    }


        

     