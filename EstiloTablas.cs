using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // AJUSTE VISUAL COMÚN DE TABLAS (DataGridView / Guna2)
    // ----------------------------------------------------------
    // Se aplica UNA SOLA VEZ en el constructor de cada formulario,
    // antes de cargar datos: no reconstruye columnas ni se repite
    // en cada actualización.
    //
    //  * Columnas en modo Fill con pesos proporcionales.
    //  * MinimumWidth calculado para que el encabezado nunca se corte.
    //  * Texto negro legible en filas normales, alternas y
    //    seleccionadas (amarillo claro), conservando el encabezado.
    //  * Doble búfer para desplazarse sin parpadeos.
    // ==========================================================
    internal static class EstiloTablas
    {
        public static readonly Color FondoSeleccion = Color.FromArgb(255, 247, 207);

        public static void Configurar(
            DataGridView tabla, int altoEncabezado, int altoFila)
        {
            if (tabla == null)
                return;

            ActivarDobleBufer(tabla);

            tabla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            tabla.AllowUserToResizeRows = false;
            tabla.RowHeadersVisible = false;

            // Encabezado: altura fija, texto en una línea, sin cambiar sus colores.
            tabla.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            tabla.ColumnHeadersHeight = altoEncabezado;
            tabla.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            tabla.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;

            tabla.RowTemplate.Height = altoFila;
            tabla.RowTemplate.MinimumHeight = altoFila;

            // Texto siempre visible (antes había blanco sobre blanco).
            AplicarColoresFila(tabla.DefaultCellStyle);
            AplicarColoresFila(tabla.AlternatingRowsDefaultCellStyle);
            tabla.RowsDefaultCellStyle.ForeColor = Color.Black;
            tabla.RowsDefaultCellStyle.SelectionForeColor = Color.Black;
            tabla.RowsDefaultCellStyle.SelectionBackColor = FondoSeleccion;

            // Guna2DataGridView vuelve a aplicar su ThemeStyle:
            // se ajusta también para que no restablezca los colores.
            Guna2DataGridView guna = tabla as Guna2DataGridView;
            if (guna != null)
            {
                guna.ThemeStyle.HeaderStyle.Height = altoEncabezado;
                guna.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
                guna.ThemeStyle.RowsStyle.Height = altoFila;
                guna.ThemeStyle.RowsStyle.ForeColor = Color.Black;
                guna.ThemeStyle.RowsStyle.SelectionForeColor = Color.Black;
                guna.ThemeStyle.RowsStyle.SelectionBackColor = FondoSeleccion;
                guna.ThemeStyle.AlternatingRowsStyle.ForeColor = Color.Black;
                guna.ThemeStyle.AlternatingRowsStyle.SelectionForeColor = Color.Black;
                guna.ThemeStyle.AlternatingRowsStyle.SelectionBackColor = FondoSeleccion;
            }
        }

        // Proporción, ancho mínimo y alineación de una columna.
        public static void Columna(
            DataGridViewColumn columna,
            float peso,
            int anchoMinimo,
            DataGridViewContentAlignment alineacion,
            bool ajustarTexto = false)
        {
            if (columna == null)
                return;

            DataGridView tabla = columna.DataGridView;
            Font fuente = tabla != null
                ? (tabla.ColumnHeadersDefaultCellStyle.Font ?? tabla.Font)
                : SystemFonts.DefaultFont;

            // El encabezado completo siempre cabe.
            int anchoEncabezado =
                AnchoEncabezado(columna, fuente);

            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
            columna.FillWeight = peso;
            columna.MinimumWidth = Math.Max(anchoMinimo, anchoEncabezado);
            columna.Resizable = DataGridViewTriState.True;
            columna.SortMode = DataGridViewColumnSortMode.NotSortable;

            columna.DefaultCellStyle.Alignment = alineacion;
            columna.HeaderCell.Style.Alignment = alineacion;

            if (ajustarTexto)
            {
                columna.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

                // Solo se recalculan las filas visibles: rápido con muchos registros.
                if (tabla != null)
                    tabla.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            }
        }

        // Columna de ancho exacto (no participa del reparto Fill).
        // Útil en tablas con poco espacio, donde Fill recalcula los
        // pesos y puede provocar desplazamiento horizontal.
        public static void ColumnaFija(
            DataGridViewColumn columna,
            int ancho,
            DataGridViewContentAlignment alineacion)
        {
            if (columna == null)
                return;

            DataGridView tabla = columna.DataGridView;
            Font fuente = tabla != null
                ? (tabla.ColumnHeadersDefaultCellStyle.Font ?? tabla.Font)
                : SystemFonts.DefaultFont;

            int real = Math.Max(ancho, AnchoEncabezado(columna, fuente));

            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columna.MinimumWidth = real;
            columna.Width = real;
            columna.Resizable = DataGridViewTriState.True;
            columna.SortMode = DataGridViewColumnSortMode.NotSortable;
            columna.DefaultCellStyle.Alignment = alineacion;
            columna.HeaderCell.Style.Alignment = alineacion;
        }

        private static int AnchoEncabezado(DataGridViewColumn columna, Font fuente)
        {
            // Texto + márgenes y bordes de la celda de encabezado.
            return TextRenderer.MeasureText(columna.HeaderText ?? "", fuente).Width + 14;
        }

        private static void AplicarColoresFila(DataGridViewCellStyle estilo)
        {
            estilo.ForeColor = Color.Black;
            estilo.SelectionForeColor = Color.Black;
            estilo.SelectionBackColor = FondoSeleccion;
        }

        private static void ActivarDobleBufer(DataGridView tabla)
        {
            PropertyInfo propiedad = typeof(Control).GetProperty(
                "DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

            if (propiedad != null)
                propiedad.SetValue(tabla, true, null);
        }

        public const DataGridViewContentAlignment Izquierda = DataGridViewContentAlignment.MiddleLeft;
        public const DataGridViewContentAlignment Centro = DataGridViewContentAlignment.MiddleCenter;
        public const DataGridViewContentAlignment Derecha = DataGridViewContentAlignment.MiddleRight;
    }
}
