
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
    public partial class FRM_COCINA : Form
    {

       
        public FRM_COCINA()
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

        private void guna2Button4_Click(object sender, EventArgs e)
        {
            FRM_CLIENTES ventana = new FRM_CLIENTES();

            ventana.Show();
            this.Hide();
        }
    }
    }


        

     