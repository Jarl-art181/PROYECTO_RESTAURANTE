using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // DIÁLOGO "DESACTIVAR O ELIMINAR" (Empleados, Menú, Inventario)
    // ----------------------------------------------------------
    // Muestra claramente las dos operaciones:
    //   * Desactivar / Reactivar: conserva el registro y su historial.
    //   * Eliminar definitivamente: solo sin historial (y si la base
    //     de datos lo tiene habilitado); pide una segunda confirmación.
    // ==========================================================
    internal static class DialogoEliminar
    {
        public enum Resultado { Cancelar, CambiarEstado, Eliminar }

        public static Resultado Mostrar(IWin32Window dueno, string modulo, string tipo, string nombre,
                                        bool activo, IList<string> historial, string efectoDesactivar)
        {
            bool sinHistorial = historial == null || historial.Count == 0;
            bool puedeEliminar = sinHistorial && EliminacionSegura.Habilitada;

            using (Form v = new Form())
            {
                v.Text = modulo;
                v.FormBorderStyle = FormBorderStyle.FixedDialog;
                v.MaximizeBox = false;
                v.MinimizeBox = false;
                v.StartPosition = FormStartPosition.CenterParent;
                v.ShowInTaskbar = false;
                v.BackColor = Color.White;
                v.Font = new Font("Segoe UI", 10F);
                v.ClientSize = new Size(560, 420);

                Guna2Panel cabecera = new Guna2Panel { Dock = DockStyle.Top, Height = 54, FillColor = Color.Gold };
                cabecera.Controls.Add(new Label
                {
                    Text = activo ? "DESACTIVAR O ELIMINAR" : "REACTIVAR O ELIMINAR",
                    Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
                });

                Label lblNombre = new Label
                {
                    Text = tipo + ": " + nombre + "   ·   " + (activo ? "Activo" : "Inactivo"),
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Location = new Point(24, 68), Size = new Size(512, 26), AutoEllipsis = true
                };

                string textoEstado = activo
                    ? "DESACTIVAR: " + efectoDesactivar + " Se conservan sus datos e historial y puede reactivarse."
                    : "REACTIVAR: vuelve a estar disponible con todos sus datos.";
                Label lblEstado = new Label
                {
                    Text = textoEstado, ForeColor = Color.FromArgb(40, 40, 40),
                    Location = new Point(24, 100), Size = new Size(512, 64)
                };

                string textoEliminar;
                if (!sinHistorial)
                    textoEliminar = "ELIMINAR DEFINITIVAMENTE: no disponible, porque tiene historial que debe conservarse:\n• " +
                                    string.Join("\n• ", historial);
                else if (!EliminacionSegura.Habilitada)
                    textoEliminar = "ELIMINAR DEFINITIVAMENTE: no tiene historial, pero " +
                                    EliminacionSegura.MotivoDeshabilitada.Substring(0, 1).ToLower() +
                                    EliminacionSegura.MotivoDeshabilitada.Substring(1);
                else
                    textoEliminar = "ELIMINAR DEFINITIVAMENTE: no tiene historial relacionado, por lo que puede " +
                                    "borrarse de la base de datos. Esta acción no se puede deshacer.";
                Label lblEliminar = new Label
                {
                    Text = textoEliminar,
                    ForeColor = sinHistorial ? Color.FromArgb(40, 40, 40) : Color.Firebrick,
                    Location = new Point(24, 170), Size = new Size(512, 170)
                };

                Guna2Button btnCancelar = Boton("Cancelar", Color.Gainsboro, Color.Black, new Point(24, 356), 140);
                Guna2Button btnEstado = Boton(activo ? "Desactivar" : "Reactivar", Color.Gold, Color.Black, new Point(176, 356), 150);
                Guna2Button btnEliminar = Boton("Eliminar definitivamente", Color.Firebrick, Color.White, new Point(338, 356), 198);
                btnEliminar.Enabled = puedeEliminar;

                Resultado resultado = Resultado.Cancelar;
                btnCancelar.Click += (s, e) => v.Close();
                btnEstado.Click += (s, e) => { resultado = Resultado.CambiarEstado; v.Close(); };
                btnEliminar.Click += (s, e) =>
                {
                    if (MessageBox.Show(v,
                            "¿Eliminar definitivamente " + tipo.ToLower() + " \"" + nombre + "\"?\n\n" +
                            "Se borrará de la base de datos y NO se podrá recuperar.",
                            "Confirmar eliminación definitiva", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                            MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                    resultado = Resultado.Eliminar;
                    v.Close();
                };

                v.Controls.AddRange(new Control[] { lblNombre, lblEstado, lblEliminar, btnCancelar, btnEstado, btnEliminar, cabecera });
                v.CancelButton = null;
                v.KeyPreview = true;
                v.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) v.Close(); };
                v.Shown += (s, e) => btnCancelar.Focus();
                v.ShowDialog(dueno);
                return resultado;
            }
        }

        private static Guna2Button Boton(string texto, Color fondo, Color letra, Point lugar, int ancho)
        {
            return new Guna2Button
            {
                Text = texto, FillColor = fondo, ForeColor = letra, BorderRadius = 10,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = lugar, Size = new Size(ancho, 42),
                DisabledState = { FillColor = Color.FromArgb(225, 225, 225), ForeColor = Color.FromArgb(150, 150, 150) }
            };
        }
    }
}
