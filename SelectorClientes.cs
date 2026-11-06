using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // Ventana común para elegir un cliente registrado (Generar Boleta y
    // Nueva reserva). Devuelve null si no se elige ninguno.
    internal static class SelectorClientes
    {
        public static ClienteInfo Elegir(IWin32Window dueno, List<ClienteInfo> clientes,
            string titulo, string mensaje, string textoCancelar)
        {
            using (Form f = new Form())
            {
                bool conMensaje = !string.IsNullOrEmpty(mensaje);
                int desplazamiento = conMensaje ? 44 : 0;

                f.Text = titulo;
                f.ClientSize = new Size(640, 470 + desplazamiento);
                f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Color.White;
                f.ShowInTaskbar = false;

                Guna2Panel cab = new Guna2Panel { Dock = DockStyle.Top, Height = 48, FillColor = Color.Gold };
                cab.Controls.Add(new Label
                {
                    Text = titulo.ToUpperInvariant(), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold), BackColor = Color.Transparent
                });

                Label aviso = new Label
                {
                    Text = mensaje ?? "", Visible = conMensaje, BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 9.5F), ForeColor = Color.Black
                };
                Guna2TextBox buscar = new Guna2TextBox
                {
                    PlaceholderText = "Nombre, documento o teléfono", BorderRadius = 8, BorderColor = Color.Goldenrod,
                    Font = new Font("Segoe UI", 10F), ForeColor = Color.Black
                };
                Guna2DataGridView tabla = new Guna2DataGridView
                {
                    ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
                    RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect
                };
                tabla.Columns.Add("doc", "DOCUMENTO");
                tabla.Columns.Add("nombre", "CLIENTE");
                tabla.Columns.Add("tel", "TELÉFONO");
                Guna2Button elegir = new Guna2Button { Text = "Seleccionar", BorderRadius = 10, FillColor = Color.Gold, ForeColor = Color.Black, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Name = "btnSeleccionar" };
                Guna2Button cancelar = new Guna2Button { Text = textoCancelar ?? "Cancelar", BorderRadius = 10, FillColor = Color.Gainsboro, ForeColor = Color.Black, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Name = "btnCancelar" };

                f.Controls.AddRange(new Control[] { aviso, buscar, tabla, elegir, cancelar, cab });
                aviso.Bounds = new Rectangle(20, 56, 600, 40);
                buscar.Bounds = new Rectangle(20, 62 + desplazamiento, 600, 38);
                tabla.Bounds = new Rectangle(20, 110 + desplazamiento, 600, 290);
                cancelar.Bounds = new Rectangle(276, 414 + desplazamiento, 160, 40);
                elegir.Bounds = new Rectangle(460, 414 + desplazamiento, 160, 40);

                EstiloTablas.Configurar(tabla, 34, 28);
                tabla.ThemeStyle.HeaderStyle.BackColor = Color.Gold;
                tabla.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
                EstiloTablas.ColumnaFija(tabla.Columns[0], 120, EstiloTablas.Centro);
                EstiloTablas.Columna(tabla.Columns[1], 60, 200, EstiloTablas.Izquierda, true);
                EstiloTablas.ColumnaFija(tabla.Columns[2], 110, EstiloTablas.Centro);

                Action llenar = () =>
                {
                    string t = MenuDatos.Normalizar(buscar.Text);
                    tabla.Rows.Clear();
                    foreach (ClienteInfo c in clientes.Where(c => t.Length == 0 ||
                        MenuDatos.Normalizar(c.NombreCompleto).Contains(t) || c.Documento.ToLowerInvariant().Contains(t) ||
                        (c.Telefono ?? "").Contains(t)))
                    {
                        int r = tabla.Rows.Add(c.Documento, c.NombreCompleto, string.IsNullOrEmpty(c.Telefono) ? "-" : c.Telefono);
                        tabla.Rows[r].Tag = c;
                    }
                };
                ClienteInfo elegido = null;
                Action aceptar = () =>
                {
                    if (tabla.CurrentRow == null) return;
                    elegido = (ClienteInfo)tabla.CurrentRow.Tag;
                    f.DialogResult = DialogResult.OK;
                };
                buscar.TextChanged += (s, e) => llenar();
                tabla.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) aceptar(); };
                elegir.Click += (s, e) => aceptar();
                cancelar.Click += (s, e) => f.Close();
                f.Shown += (s, e) => buscar.Focus();
                llenar();

                return f.ShowDialog(dueno) == DialogResult.OK ? elegido : null;
            }
        }
    }
}
