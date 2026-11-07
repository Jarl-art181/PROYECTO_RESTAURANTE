using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // AUTORIZACIÓN PARA GUARDAR LOS DATOS DEL COMPRADOR
    // ----------------------------------------------------------
    // Ventana independiente (no cambia el diseño de Generar recibo).
    // Se muestra al cobrar cuando el comprador tiene documento y aún
    // no está en Gestión de Clientes:
    //   * Registrar y cobrar: solo con la autorización marcada.
    //   * Solo en este recibo: sus datos figuran únicamente en el recibo.
    //   * Cancelar: vuelve al recibo sin cobrar.
    // ==========================================================
    internal static class DialogoAutorizacionCliente
    {
        public enum Resultado { Cancelar, SoloRecibo, Registrar }

        public const string Titulo = "Datos del comprador";

        // motivo != null: los datos no permiten registrarlo (se explica y
        // solo se ofrece «Solo en este recibo»).
        public static Resultado Mostrar(IWin32Window dueno, ClienteInfo c, string motivo, out bool aceptaPromociones)
        {
            aceptaPromociones = false;
            bool puede = motivo == null;

            using (Form v = new Form())
            using (Font normal = new Font("Segoe UI", 10F))
            using (Font negrita = new Font("Segoe UI", 10F, FontStyle.Bold))
            using (Font titulo = new Font("Segoe UI", 13F, FontStyle.Bold))
            {
                v.Text = Titulo;
                v.FormBorderStyle = FormBorderStyle.FixedDialog;
                v.MaximizeBox = false;
                v.MinimizeBox = false;
                v.StartPosition = FormStartPosition.CenterParent;
                v.ShowInTaskbar = false;
                v.BackColor = Color.White;
                v.Font = normal;
                v.ClientSize = new Size(520, 400);

                Guna2Panel cabecera = new Guna2Panel { Dock = DockStyle.Top, Height = 54, FillColor = Color.Gold };
                cabecera.Controls.Add(new Label
                {
                    Text = "¿REGISTRAR AL COMPRADOR COMO CLIENTE?", Font = titulo, Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
                });

                string tipo = ClienteDatos.TipoDocumento(c.Documento);
                Label datos = new Label
                {
                    Font = negrita, Location = new Point(24, 68), Size = new Size(472, 70),
                    Text = "Nombres: " + c.Nombre + "\nApellidos: " + c.Apellidos + "\n" +
                           (tipo == "PAS" ? "Pasaporte" : tipo) + ": " + c.Documento +
                           (string.IsNullOrEmpty(c.Telefono) ? "" : "     Celular: " + c.Telefono)
                };

                Label nota = new Label
                {
                    Location = new Point(24, 144), Size = new Size(472, 58),
                    ForeColor = puede ? Color.FromArgb(60, 60, 60) : Color.Firebrick,
                    Text = puede
                        ? "No está en Gestión de Clientes. Si el cliente lo autoriza, se registrará y este pedido " +
                          "se sumará a su historial y puntos. Sin autorización, sus datos solo figuran en este recibo."
                        : "No se puede registrar en Gestión de Clientes: " + motivo + "\nSus datos figurarán solo en este recibo."
                };

                Guna2CheckBox autoriza = Casilla("El cliente autoriza guardar sus datos en Gestión de Clientes", new Point(24, 212));
                Guna2CheckBox promociones = Casilla("Acepta recibir promociones (opcional)", new Point(24, 246));
                autoriza.Enabled = puede;
                promociones.Enabled = false;

                Guna2Button cancelar = Boton("Cancelar", Color.Gainsboro, Color.Black, new Point(24, 336), 130);
                Guna2Button solo = Boton("Solo en este recibo", Color.Khaki, Color.Black, new Point(166, 336), 160);
                Guna2Button registrar = Boton("Registrar y cobrar", Color.Gold, Color.Black, new Point(338, 336), 158);
                registrar.Enabled = false;
                autoriza.CheckedChanged += (s, e) =>
                {
                    registrar.Enabled = autoriza.Checked;
                    promociones.Enabled = autoriza.Checked;
                    if (!autoriza.Checked) promociones.Checked = false;
                };

                Resultado r = Resultado.Cancelar;
                cancelar.Click += (s, e) => v.Close();
                solo.Click += (s, e) => { r = Resultado.SoloRecibo; v.Close(); };
                registrar.Click += (s, e) => { r = Resultado.Registrar; v.Close(); };

                v.Controls.AddRange(new Control[] { datos, nota, autoriza, promociones, cancelar, solo, registrar, cabecera });
                v.KeyPreview = true;
                v.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) v.Close(); };
                v.ShowDialog(dueno);

                aceptaPromociones = r == Resultado.Registrar && promociones.Checked;
                return r;
            }
        }

        private static Guna2CheckBox Casilla(string texto, Point lugar)
        {
            Guna2CheckBox c = new Guna2CheckBox
            {
                Text = texto, AutoSize = true, Location = lugar, BackColor = Color.Transparent,
                ForeColor = Color.Black
            };
            c.CheckedState.FillColor = Color.Goldenrod;
            c.CheckedState.BorderColor = Color.Goldenrod;
            c.UncheckedState.BorderColor = Color.Goldenrod;
            c.UncheckedState.BorderThickness = 1;
            c.UncheckedState.FillColor = Color.White;      // sin marcar: casilla blanca (no parece deshabilitada)
            return c;
        }

        private static Guna2Button Boton(string texto, Color fondo, Color letra, Point lugar, int ancho)
        {
            Guna2Button b = new Guna2Button
            {
                Text = texto, FillColor = fondo, ForeColor = letra, BorderRadius = 10,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold), Location = lugar, Size = new Size(ancho, 42)
            };
            b.DisabledState.FillColor = Color.FromArgb(225, 225, 225);
            b.DisabledState.ForeColor = Color.FromArgb(150, 150, 150);
            b.Disposed += (s, e) => b.Font.Dispose();
            return b;
        }
    }
}
