
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
    public partial class FRM_MENU : Form
    {

       
        public FRM_MENU()
        {
            
            InitializeComponent();
            // ventana grande 
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            ConfigurarTablaProductos();
        }

        // Tabla de productos: solo lectura, sin fila vacía editable,
        // columnas proporcionales e importes alineados a la derecha.
        private void ConfigurarTablaProductos()
        {
            DataGridView tabla = guna2DataGridView1;
            tabla.AllowUserToAddRows = false;
            tabla.AllowUserToDeleteRows = false;
            tabla.ReadOnly = true;
            tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            EstiloTablas.Configurar(tabla, 34, 30);
            EstiloTablas.Columna(Column_N, 6, 44, EstiloTablas.Centro);
            EstiloTablas.Columna(ColumnProducto, 34, 180, EstiloTablas.Izquierda, true);
            EstiloTablas.Columna(ColumnCategoria, 18, 100, EstiloTablas.Izquierda);
            EstiloTablas.Columna(Columnprecio, 14, 90, EstiloTablas.Derecha);
            EstiloTablas.Columna(Column_Estado, 16, 84, EstiloTablas.Izquierda);
            EstiloTablas.Columna(ColumnSTOCK, 12, 70, EstiloTablas.Derecha);
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

        private void guna2HtmlLabel34_Click(object sender, EventArgs e)
        {

        }

        private void guna2Button8_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INVENTARIO>(this);
        }
    }
    }


        

     