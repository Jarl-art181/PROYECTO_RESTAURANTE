using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GENERAR RECIBO DE VENTA · ORGANIZACIÓN DE LA VENTANA
    // ----------------------------------------------------------
    // Se ordena en coordenadas de diseño (1326x913) ANTES de la
    // adaptación común, que luego escala todo a la pantalla:
    //   * Título centrado: es un recibo interno R001, no una boleta
    //     electrónica SUNAT.
    //   * Tres paneles con el mismo margen, la misma altura y los
    //     subtítulos a la misma altura.
    //   * Recibo centrado en la vista previa; importes alineados a la
    //     derecha y total destacado.
    //   * Botones: Vista previa | Imprimir arriba, Cancelar | Cobrar abajo.
    // No cambia cálculos, pagos ni datos: solo posición y presentación.
    // ==========================================================
    public partial class Generar_Boleta
    {
        private const int AnchoDiseno = 1326, AltoDiseno = 913, AltoCabecera = 93, Margen = 12;
        private readonly List<Image> iconosBotones = new List<Image>();
        private readonly List<Font> letrasPropias = new List<Font>();

        private Font Propia(Font f)
        {
            letrasPropias.Add(f);
            return f;
        }

        private void OrganizarVentana()
        {
            Text = "Generar recibo de venta";

            // ---------- Cabecera: título y subtítulo centrados ----------
            guna2Panel1.Bounds = new Rectangle(0, 0, AnchoDiseno, AltoCabecera);
            guna2HtmlLabel1.Text = "GENERAR RECIBO DE VENTA";
            guna2HtmlLabel2.Text = "Recibo interno R001 · Revise el pedido, elija la forma de pago y confirme el cobro";
            Centrada(guna2HtmlLabel1, new Rectangle(120, 8, AnchoDiseno - 240, 40));
            Centrada(guna2HtmlLabel2, new Rectangle(120, 50, AnchoDiseno - 240, 32));
            int dx = AnchoDiseno - 75 - guna2Button1.Left;
            guna2Button1.Left += dx;
            btnCerrarBoleta.Left += dx;

            // ---------- Cuerpo: tres paneles con margen uniforme ----------
            guna2Panel2.Bounds = new Rectangle(0, AltoCabecera, AnchoDiseno, AltoDiseno - AltoCabecera);
            int alto = AltoDiseno - AltoCabecera - 2 * Margen;
            guna2Panel3.Bounds = new Rectangle(Margen, Margen, 372, alto);
            guna2Panel6.Bounds = new Rectangle(guna2Panel3.Right + Margen, Margen, 530, alto);
            guna2Panel7.Bounds = new Rectangle(guna2Panel6.Right + Margen, Margen, AnchoDiseno - guna2Panel6.Right - 2 * Margen, alto);

            Subtitulo(guna2PictureBox2, guna2HtmlLabel3, "Información del pedido");
            Subtitulo(guna2PictureBox5, guna2HtmlLabel13, "Vista previa del recibo");
            Subtitulo(guna2PictureBox8, guna2HtmlLabel28, "Impresión");

            OrganizarInformacion();
            OrganizarVistaPrevia(alto);
            OrganizarBotones(alto);
        }

        // Etiqueta de tamaño automático centrada en su contenedor; se vuelve a
        // centrar si cambia su texto o su letra (escala). Con tamaño fijo, la
        // etiqueta HTML de Guna recorta el texto cuando la escala es pequeña.
        private readonly List<Control> centradas = new List<Control>();

        private void Centrada(Guna2HtmlLabel etiqueta, Rectangle r)
        {
            etiqueta.AutoSize = true;
            etiqueta.Top = r.Y + Math.Max(0, (r.Height - etiqueta.Height) / 2);
            Recentrar(etiqueta);
            etiqueta.SizeChanged += (s, e) => Recentrar(etiqueta);
            centradas.Add(etiqueta);
        }

        private static void Recentrar(Control c)
        {
            if (c.Parent != null)
                c.Left = Math.Max(0, (c.Parent.ClientSize.Width - c.Width) / 2);
        }

        // Tras la adaptación a la pantalla (todas las letras ya escaladas).
        private void RecentrarTodo()
        {
            foreach (Control c in centradas)
                Recentrar(c);

            // El margen del recibo no se escala: se recalcula con el ancho real
            // visible de la vista previa (sin la barra de desplazamiento).
            int libre = flowLayoutPanel1.ClientSize.Width - guna2Panel8.Width;
            if (flowLayoutPanel1.VerticalScroll.Visible == false)
                libre -= SystemInformation.VerticalScrollBarWidth;
            flowLayoutPanel1.SuspendLayout();
            foreach (Control c in flowLayoutPanel1.Controls)
                c.Margin = new Padding(Math.Max(0, libre / 2), c.Margin.Top, 0, c.Margin.Bottom);
            flowLayoutPanel1.ResumeLayout(true);
        }

        private static void Derecha(Guna2HtmlLabel etiqueta, int derecha, int ancho)
        {
            int y = etiqueta.Top, h = etiqueta.Height;
            etiqueta.AutoSize = false;
            etiqueta.Bounds = new Rectangle(derecha - ancho, y, ancho, h);
            etiqueta.TextAlignment = ContentAlignment.MiddleRight;
        }

        private static void Subtitulo(PictureBox icono, Guna2HtmlLabel titulo, string texto)
        {
            icono.Bounds = new Rectangle(12, 10, 28, 32);
            icono.SizeMode = PictureBoxSizeMode.Zoom;
            titulo.Text = texto;
            titulo.Location = new Point(48, 13);
        }

        // Panel izquierdo: valores con la misma letra y alineados con sus títulos;
        // importes alineados a la derecha.
        private void OrganizarInformacion()
        {
            Font valor = Propia(new Font("Microsoft Sans Serif", 11.25F));
            foreach (Guna2HtmlLabel l in new[] { lblValorPedido, lblValorTipo, lblValorMesa, lblValorFecha, lblValorAtendido })
            {
                l.Font = valor;
                l.Top += 2;
                l.Left = 176;                 // a la derecha del título más largo ("Número de pedido:")
            }

            // Iconos y títulos de las secciones con la misma sangría.
            foreach (PictureBox p in new[] { guna2PictureBox3, guna2PictureBox4 })
            {
                p.Left = 12;
                p.SizeMode = PictureBoxSizeMode.Zoom;
            }
            guna2HtmlLabel9.Left = 48;
            guna2HtmlLabel10.Left = 48;

            int derecha = guna2Panel4.Width - 12;
            foreach (Guna2HtmlLabel l in new[] { lblSubtotalBoleta, lblDescuentoBoleta, lblIGVBoleta })
                Derecha(l, derecha, 150);
            Derecha(lblTotalBoleta, guna2Panel5.Width - 12, 170);
            lblTotalBoleta.Font = Propia(new Font(lblTotalBoleta.Font, FontStyle.Bold));
            guna2HtmlLabel12.Font = Propia(new Font(guna2HtmlLabel12.Font, FontStyle.Bold));
        }

        // Vista previa: recibo centrado, encabezado y pie centrados,
        // importes a la derecha y total destacado.
        private void OrganizarVistaPrevia(int alto)
        {
            int anchoRecibo = guna2Panel8.Width;                     // 411
            int anchoArea = 452;
            flowLayoutPanel1.Bounds = new Rectangle((guna2Panel6.Width - anchoArea) / 2, 50, anchoArea, alto - 50 - Margen);
            int libre = anchoArea - SystemInformation.VerticalScrollBarWidth - anchoRecibo;
            foreach (Control c in flowLayoutPanel1.Controls)
                c.Margin = new Padding(Math.Max(0, libre / 2), 3, 0, 3);

            guna2PictureBox7.Left = (anchoRecibo - guna2PictureBox7.Width) / 2;
            // Cajas un poco más altas que el texto: con la letra mínima no se recorta.
            foreach (Guna2HtmlLabel l in new[] { guna2HtmlLabel14, guna2HtmlLabel15, guna2HtmlLabel16, guna2HtmlLabel17,
                                                 guna2HtmlLabel20, lblBoletaNumero })
                Centrada(l, new Rectangle(0, l.Top - 2, anchoRecibo, l.Height + 4));
            dgvBoletaDetalle.ScrollBars = ScrollBars.None;           // la tabla crece con sus filas

            OrganizarTotalesRecibo(anchoRecibo);
            OrganizarPieRecibo(anchoRecibo);
        }

        // ------------------------------------------------------
        // RECIBO: totales y pie, en el mismo orden que la impresión
        // (Documento_PrintPage). Solo posiciones y textos fijos.
        // ------------------------------------------------------
        private Guna2HtmlLabel lblPrevSubtotalTitulo, lblPrevSubtotal, lblPrevDescuentoTitulo, lblPrevDescuento,
                               lblPrevCaja, lblPrevNota;

        private Guna2HtmlLabel EtiquetaRecibo(Control padre, Font letra, Point lugar)
        {
            Guna2HtmlLabel l = new Guna2HtmlLabel { AutoSize = true, BackColor = Color.Transparent, Font = letra, ForeColor = Color.Black, Location = lugar };
            padre.Controls.Add(l);
            return l;
        }

        // Subtotal | Descuento | Base imponible | IGV | ---- | TOTAL
        private void OrganizarTotalesRecibo(int anchoRecibo)
        {
            int derecha = anchoRecibo - 10, x = 13;
            Font titulo = guna2HtmlLabel51.Font, valor = lblBoletaSubtotal.Font;

            guna2HtmlLabel38.Top = 3;
            lblPrevSubtotalTitulo = EtiquetaRecibo(guna2Panel10, titulo, new Point(x, 27));
            lblPrevSubtotalTitulo.Text = "Subtotal (con IGV):";
            lblPrevSubtotal = EtiquetaRecibo(guna2Panel10, valor, new Point(0, 27));
            lblPrevSubtotal.AutoSize = false;
            lblPrevSubtotal.Height = lblBoletaSubtotal.Height;
            Derecha(lblPrevSubtotal, derecha, 150);

            lblPrevDescuentoTitulo = EtiquetaRecibo(guna2Panel10, titulo, new Point(x, 49));
            lblPrevDescuentoTitulo.Text = "Descuento:";
            lblPrevDescuento = EtiquetaRecibo(guna2Panel10, valor, new Point(0, 49));
            lblPrevDescuento.AutoSize = false;
            lblPrevDescuento.Height = lblBoletaSubtotal.Height;
            Derecha(lblPrevDescuento, derecha, 150);

            guna2HtmlLabel51.Location = new Point(x, 71);
            lblBoletaSubtotal.Top = 71;
            Derecha(lblBoletaSubtotal, derecha, 150);
            guna2HtmlLabel36.Location = new Point(x, 93);
            lblBoletaIGV.Top = 93;
            Derecha(lblBoletaIGV, derecha, 150);

            guna2HtmlLabel40.Top = 115;
            guna2HtmlLabel52.Text = "TOTAL";
            guna2HtmlLabel52.Font = Propia(new Font(guna2HtmlLabel52.Font, FontStyle.Bold));
            guna2HtmlLabel52.Location = new Point(x - 2, 134);
            lblBoletaTotal.Top = 134;
            lblBoletaTotal.Height = guna2HtmlLabel52.Height;
            Derecha(lblBoletaTotal, derecha, 180);
            lblBoletaTotal.Font = guna2HtmlLabel52.Font;
            guna2HtmlLabel54.Top = 165;
            guna2Panel10.Height = 190;
        }

        // Forma de pago | Caja | QR | código de barras | PEDIDO-000000 |
        // ¡Gracias por su compra! | ¡Gracias por su preferencia! |
        // ¡Sabor que te encanta! | nota discreta de recibo interno.
        private void OrganizarPieRecibo(int anchoRecibo)
        {
            guna2HtmlLabel50.Text = "Forma de pago:";
            guna2HtmlLabel50.Location = new Point(13, 8);
            lblBoletaFormaPago.Location = new Point(guna2HtmlLabel50.Right + 6, 7);
            lblPrevCaja = EtiquetaRecibo(guna2Panel11, Propia(new Font("Microsoft Sans Serif", 9.75F)), new Point(13, 32));

            int qr = 160;
            picQRBoleta.SizeMode = PictureBoxSizeMode.Zoom;
            picQRBoleta.Bounds = new Rectangle((anchoRecibo - qr) / 2, 62, qr, qr);
            picCodigoBarrasBoleta.SizeMode = PictureBoxSizeMode.Zoom;       // 5:1, sin deformar
            picCodigoBarrasBoleta.Bounds = new Rectangle((anchoRecibo - 300) / 2, 232, 300, 60);

            lblBoletaCodigoFinal.Font = Propia(new Font(lblBoletaCodigoFinal.Font, FontStyle.Bold));
            guna2HtmlLabel56.Text = "¡Gracias por su compra!";
            guna2HtmlLabel56.Font = Propia(new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold));
            lblPrevNota = EtiquetaRecibo(guna2Panel11, Propia(new Font("Microsoft Sans Serif", 7.5F)), new Point(0, 0));
            lblPrevNota.ForeColor = Color.DimGray;

            int y = 298;
            foreach (Guna2HtmlLabel l in new[] { lblBoletaCodigoFinal, guna2HtmlLabel56, guna2HtmlLabel34, guna2HtmlLabel46, lblPrevNota })
            {
                Centrada(l, new Rectangle(0, y, anchoRecibo, l.Height));
                y += l.Height + (l == lblBoletaCodigoFinal ? 12 : 4);
            }
            guna2Panel11.Height = y + 10;
        }

        // Botones con su texto e ícono propios (las etiquetas superpuestas
        // del diseño se ocultan; sus eventos se conservan).
        private void OrganizarBotones(int alto)
        {
            int x1 = 21, ancho = (guna2Panel7.Width - 2 * x1 - 12) / 2, x2 = x1 + ancho + 12;
            int altoBoton = 58, fila2 = alto - Margen - altoBoton, fila1 = fila2 - 10 - altoBoton;

            // Secciones del panel derecho con aire entre ellas (antes "Forma de
            // Pago" quedaba pegada a la impresora).
            HashSet<Control> botones = new HashSet<Control> { btnVistaPreviaBoleta, btnImprimirBoleta, btnCancelarBoleta,
                btnRegistrarCobro, guna2HtmlLabel23, guna2HtmlLabel47, guna2HtmlLabel49, guna2HtmlLabel22 };
            foreach (Control c in guna2Panel7.Controls)
            {
                if (botones.Contains(c)) continue;
                int t = c.Top;
                c.Top += t >= 515 ? 22 : t >= 370 ? 18 : t >= 210 ? 14 : t >= 100 ? 10 : 0;
            }

            // Impresora y copias: misma letra y cada campo bajo su título.
            guna2HtmlLabel27.Font = guna2HtmlLabel55.Font;
            guna2HtmlLabel27.Top = guna2HtmlLabel55.Top;
            cmbImpresoraBoleta.Left = guna2HtmlLabel27.Left;
            numCopiasBoleta.Left = guna2HtmlLabel55.Left;
            lblMetodoPago1.Text = "Método:";
            guna2HtmlLabel30.Text = "Método:";
            txtVueltoBoleta.PlaceholderText = "S/ 0.00";

            Boton(btnVistaPreviaBoleta, guna2HtmlLabel23, "Vista previa", '', new Rectangle(x1, fila1, ancho, altoBoton), Color.Khaki, Color.Black);
            Boton(btnImprimirBoleta, guna2HtmlLabel47, "Imprimir", '', new Rectangle(x2, fila1, ancho, altoBoton), Color.Khaki, Color.Black);
            Boton(btnCancelarBoleta, guna2HtmlLabel49, "Cancelar", '', new Rectangle(x1, fila2, ancho, altoBoton), Color.FromArgb(210, 35, 35), Color.White);
            Boton(btnRegistrarCobro, guna2HtmlLabel22, "Cobrar", '', new Rectangle(x2, fila2, ancho, altoBoton), Color.Gold, Color.Black);

            // Observaciones: justo encima de los botones.
            txtObservacionesBoleta.Height = fila1 - 12 - txtObservacionesBoleta.Top;
            Disposed += (s, e) =>
            {
                foreach (Image i in iconosBotones) i.Dispose();
                foreach (Font f in letrasPropias) f.Dispose();
            };
        }

        private void Boton(Guna2Button b, Guna2HtmlLabel etiqueta, string texto, char glifo, Rectangle r, Color fondo, Color letra)
        {
            b.Bounds = r;
            b.Text = texto;
            b.Font = Propia(new Font("Segoe UI", 12F, FontStyle.Bold));
            b.FillColor = fondo;
            b.ForeColor = letra;
            b.DisabledState.FillColor = Color.FromArgb(225, 225, 225);
            b.DisabledState.ForeColor = Color.FromArgb(140, 140, 140);
            b.BorderRadius = 12;
            etiqueta.Visible = false;

            Bitmap icono = new Bitmap(28, 28);
            using (Graphics g = Graphics.FromImage(icono))
            using (Font f = new Font("Segoe MDL2 Assets", 20F, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush pincel = new SolidBrush(letra))
            using (StringFormat centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glifo.ToString(), f, pincel, new RectangleF(0, 0, 28, 28), centro);
            }
            iconosBotones.Add(icono);
            b.Image = icono;
            b.ImageSize = new Size(22, 22);
            b.ImageAlign = HorizontalAlignment.Center;      // ícono junto al texto, ambos centrados
        }

        // La ventana es modal: se dimensiona para caber en la pantalla
        // (1366x768, 1920x1080, 100 % y 125 %); la adaptación común escala el contenido.
        private void AjustarAPantalla()
        {
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            // Bordes y barra de título reales (en el constructor aún no hay ventana).
            Size bordes = SizeFromClientSize(new Size(200, 200)) - new Size(200, 200);
            float k = Math.Min(1f, Math.Min((area.Width - bordes.Width - 16) / (float)AnchoDiseno,
                                            (area.Height - bordes.Height - 4) / (float)AltoDiseno));
            ClientSize = new Size((int)Math.Round(AnchoDiseno * k), (int)Math.Round(AltoDiseno * k));
        }
    }
}
