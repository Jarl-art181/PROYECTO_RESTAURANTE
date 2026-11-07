
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
    public partial class FRM_REPORTES : Form
    {

       
        public FRM_REPORTES()
        {
            
            InitializeComponent();
            // ventana grande 
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            // Diseño común: se adapta a la pantalla (1366x768 a 1920x1080 y escalado
            // de Windows), el encabezado muestra la sesión y la hora, y el menú
            // lateral queda conectado (MenuLateral).
            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);
        }


        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            // "Cerrar Sesión": libera los módulos y vuelve al inicio de sesión.
            Navegador.CerrarSesion();
        }
       
      
       
        
        private void guna2Button2_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRMS_PEDIDOS>(this);
        }
    }
    }


        

     