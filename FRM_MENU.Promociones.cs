using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // MENÚ · PROMOCIONES DEL PRODUCTO
    // ----------------------------------------------------------
    // Vista de consulta en el área de "Lista de productos": dos
    // pestañas (Productos / Promociones) en la barra amarilla. Muestra
    // las promociones del producto seleccionado y las de todo el pedido,
    // con el precio promocional estimado calculado por
    // PromocionDatos.Calcular (la misma lógica que se aplica al cobrar).
    // No crea ni edita promociones: «Gestionar» abre FRM_PROMOCIONES.
    // ==========================================================
    public partial class FRM_MENU
    {
        private Guna2DataGridView tablaPromociones;
        private Guna2Button btnVerProductos, btnVerPromociones, btnGestionarPromociones;
        private bool viendoPromociones;
        private List<PromocionInfo> promocionesLeidas;     // se vuelven a leer al recargar el catálogo

        private static readonly Color PestanaActiva = Color.Black;
        private static readonly Color TextoPestanaActiva = Color.Gold;

        private void CrearApartadoPromociones()
        {
            // Pestañas y acceso a Gestión de Promociones (barra de 779x40 del diseño).
            btnVerProductos = Pestana("Productos", new Rectangle(318, 5, 112, 30));
            btnVerPromociones = Pestana("Promociones del producto", new Rectangle(436, 5, 198, 30));
            btnGestionarPromociones = Pestana("Gestionar", new Rectangle(640, 5, 128, 30));
            btnGestionarPromociones.FillColor = Color.White;
            btnGestionarPromociones.BorderColor = Color.Black;
            btnGestionarPromociones.BorderThickness = 1;
            guna2Panel29.Controls.AddRange(new Control[] { btnVerProductos, btnVerPromociones, btnGestionarPromociones });

            btnVerProductos.Click += (s, e) => VerPromociones(false);
            btnVerPromociones.Click += (s, e) => VerPromociones(true);
            btnGestionarPromociones.Click += (s, e) => Navegador.Ir<FRM_PROMOCIONES>(this);
            guna2Panel19.Cursor = Cursors.Hand;                       // tarjeta "Promociones"
            guna2Panel19.Click += (s, e) => VerPromociones(true);
            foreach (Control c in guna2Panel19.Controls)
            {
                c.Cursor = Cursors.Hand;
                c.Click += (s, e) => VerPromociones(true);
            }

            // Tabla con el mismo estilo que la lista de productos.
            DataGridView origen = guna2DataGridView1;
            tablaPromociones = new Guna2DataGridView
            {
                Name = "tablaPromociones",
                Bounds = origen.Bounds,
                Visible = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = origen.Font,
                BackgroundColor = origen.BackgroundColor,
                GridColor = origen.GridColor,
                BorderStyle = origen.BorderStyle,
                CellBorderStyle = origen.CellBorderStyle,
                ColumnHeadersBorderStyle = origen.ColumnHeadersBorderStyle,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle(origen.ColumnHeadersDefaultCellStyle),
                DefaultCellStyle = new DataGridViewCellStyle(origen.DefaultCellStyle)
            };
            tablaPromociones.ThemeStyle.HeaderStyle.BackColor = guna2DataGridView1.ThemeStyle.HeaderStyle.BackColor;
            // Ocho columnas en el ancho de seis: encabezado un poco más pequeño.
            Font letraEncabezado = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold);
            tablaPromociones.ColumnHeadersDefaultCellStyle.Font = letraEncabezado;
            tablaPromociones.ThemeStyle.HeaderStyle.Font = letraEncabezado;
            Disposed += (s, e) => letraEncabezado.Dispose();
            tablaPromociones.ThemeStyle.BackColor = guna2DataGridView1.ThemeStyle.BackColor;
            tablaPromociones.ThemeStyle.GridColor = guna2DataGridView1.ThemeStyle.GridColor;
            tablaPromociones.ThemeStyle.RowsStyle.Font = guna2DataGridView1.ThemeStyle.RowsStyle.Font;
            tablaPromociones.ThemeStyle.RowsStyle.BackColor = guna2DataGridView1.ThemeStyle.RowsStyle.BackColor;
            tablaPromociones.ThemeStyle.AlternatingRowsStyle.BackColor = guna2DataGridView1.ThemeStyle.AlternatingRowsStyle.BackColor;

            string[] titulos = { "PROMOCIÓN", "DSCTO.", "INICIO", "VENCE", "ESTADO", "DÍAS", "PRECIO", "PROMO" };
            foreach (string t in titulos)
                tablaPromociones.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = t, Name = "colPromo" + tablaPromociones.Columns.Count });

            EstiloTablas.Configurar(tablaPromociones, 34, 30);
            EstiloTablas.Columna(tablaPromociones.Columns[0], 22, 120, EstiloTablas.Izquierda, true);
            EstiloTablas.Columna(tablaPromociones.Columns[1], 8, 60, EstiloTablas.Derecha);
            EstiloTablas.Columna(tablaPromociones.Columns[2], 10, 74, EstiloTablas.Centro);
            EstiloTablas.Columna(tablaPromociones.Columns[3], 10, 74, EstiloTablas.Centro);
            EstiloTablas.Columna(tablaPromociones.Columns[4], 15, 96, EstiloTablas.Izquierda, true);
            EstiloTablas.Columna(tablaPromociones.Columns[5], 12, 70, EstiloTablas.Izquierda, true);
            EstiloTablas.Columna(tablaPromociones.Columns[6], 10, 66, EstiloTablas.Derecha);
            EstiloTablas.Columna(tablaPromociones.Columns[7], 11, 76, EstiloTablas.Derecha, true);

            Controls.Add(tablaPromociones);
            tablaPromociones.BringToFront();
            ActualizarPestanas();
        }

        private static Guna2Button Pestana(string texto, Rectangle r)
        {
            return new Guna2Button
            {
                Text = texto,
                Bounds = r,
                BorderRadius = 8,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FillColor = Color.White,
                ForeColor = Color.Black,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
        }

        private void VerPromociones(bool ver)
        {
            viendoPromociones = ver;
            ActualizarApartadoPromociones();
        }

        private void ActualizarPestanas()
        {
            btnVerProductos.FillColor = viendoPromociones ? Color.White : PestanaActiva;
            btnVerProductos.ForeColor = viendoPromociones ? Color.Black : TextoPestanaActiva;
            btnVerPromociones.FillColor = viendoPromociones ? PestanaActiva : Color.White;
            btnVerPromociones.ForeColor = viendoPromociones ? TextoPestanaActiva : Color.Black;
            btnGestionarPromociones.Visible = SesionActual.Iniciada &&
                Permisos.PuedeAbrir(typeof(FRM_PROMOCIONES), SesionActual.Cargo);
        }

        // Llamado al mostrar un producto y al recargar el catálogo.
        private void ActualizarApartadoPromociones()
        {
            if (tablaPromociones == null)
                return;

            ActualizarPestanas();
            guna2DataGridView1.Visible = !viendoPromociones;
            tablaPromociones.Visible = viendoPromociones;
            guna2HtmlLabel34.Text = viendoPromociones ? "Promociones" : "Lista de productos";
            if (!viendoPromociones)
                return;

            // Mientras estaba oculta no se repartió el ancho: se vuelve a repartir.
            tablaPromociones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            tablaPromociones.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            tablaPromociones.Rows.Clear();
            ProductoMenu p = seleccionado;
            if (p == null)
            {
                Aviso("Seleccione un producto (tarjeta o lista) para ver sus promociones.");
                return;
            }

            List<PromocionInfo> todas;
            try
            {
                if (promocionesLeidas == null)
                    promocionesLeidas = PromocionDatos.Listar();
                todas = promocionesLeidas;
            }
            catch (Exception ex)
            {
                Registro.Error("Menu", ex);
                Aviso(ex is ReglaNegocioException ? ex.Message : "No se pudieron leer las promociones.");
                return;
            }

            DateTime hoy = DateTime.Today;
            // Del producto (por platos) y de todo el pedido; las inactivas o vencidas al final.
            var propias = todas.Where(x => x.PorPlatos && x.Platos.Contains(p.IdPlato))
                .Concat(todas.Where(x => !x.PorPlatos && x.Activa && hoy <= x.FechaFin.Date))
                .OrderBy(x => Orden(x.Situacion(hoy))).ThenBy(x => x.FechaFin)
                .ToList();

            guna2HtmlLabel34.Text = "Promociones · " + Recortar(p.Nombre, 26);
            if (propias.Count == 0)
            {
                Aviso("«" + p.Nombre + "» no tiene promociones asociadas.");
                return;
            }

            foreach (PromocionInfo x in propias)
            {
                string situacion = x.Situacion(hoy);
                tablaPromociones.Rows.Add(
                    x.PorPlatos ? x.Nombre : x.Nombre + " (todo el pedido)",
                    x.DescuentoTexto,
                    x.FechaInicio.ToString("dd/MM/yy"),
                    x.FechaFin.ToString("dd/MM/yy"),
                    situacion,
                    x.DiasTexto,
                    Soles(p.Precio),
                    PrecioPromocional(x, p));
                DataGridViewRow fila = tablaPromociones.Rows[tablaPromociones.Rows.Count - 1];
                fila.Cells[4].Style.ForeColor = situacion == "Vigente hoy" ? Color.ForestGreen
                    : situacion == "Inactiva" || situacion == "Vencida" ? Color.Firebrick : Color.DarkOrange;
                fila.Cells[7].Style.ForeColor = Color.FromArgb(0, 120, 0);
            }
            tablaPromociones.ClearSelection();
        }

        // Precio estimado de una unidad con la lógica de cobro (sin exigir vigencia).
        private static string PrecioPromocional(PromocionInfo x, ProductoMenu p)
        {
            if (!x.PorPlatos)
                return x.MontoMinimo.HasValue ? "Pedido ≥ " + Soles(x.MontoMinimo.Value) : "Según pedido";

            List<LineaDescuento> linea = new List<LineaDescuento> { new LineaDescuento("plato", p.IdPlato, 1, p.Precio) };
            ResultadoPromocion r = PromocionDatos.Calcular(x, linea, DateTime.Today, false);
            if (r.Aplicable)
                return Soles(p.Precio - r.DescuentoLineas);
            return x.MontoMinimo.HasValue ? "Pedido ≥ " + Soles(x.MontoMinimo.Value) : "-";
        }

        private static int Orden(string situacion)
        {
            switch (situacion)
            {
                case "Vigente hoy": return 0;
                case "Vigente (otro día)": return 1;
                case "Programada": return 2;
                case "Vencida": return 3;
                default: return 4;
            }
        }

        private void Aviso(string texto)
        {
            tablaPromociones.Rows.Add(texto, "", "", "", "", "", "", "");
            tablaPromociones.Rows[0].Cells[0].Style.ForeColor = Color.DimGray;
            tablaPromociones.ClearSelection();
        }

        private static string Soles(decimal v)
        {
            return "S/ " + v.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Recortar(string texto, int maximo)
        {
            texto = texto ?? "";
            return texto.Length <= maximo ? texto : texto.Substring(0, maximo - 1).TrimEnd() + "…";
        }
    }
}
