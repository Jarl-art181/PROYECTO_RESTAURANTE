
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
    public partial class FRM_PROMOCIONES : Form
    {

       
        public FRM_PROMOCIONES()
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
            Navegador.Ir<FRMS_PEDIDOS>(this);
        }
    }
    }


        

     