using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // GESTIÓN DE INVENTARIO (FASE E)
    // Insumos reales de dbo.INSUMO con los controles del diseño
    // (nombres sin cambiar):
    //   flowProductos (tarjetas) · flowLayoutPanel1 (movimientos)
    //   txtBuscarProducto · guna2Button10/11/12/13 Todos/Platos/Bebidas/Agregados
    //     (insumos usados por platos de ese grupo, según RECETA_PLATO)
    //   guna2Button24 Nuevo insumo · guna2Button15 Registrar entrada
    //   guna2Button14 Registrar salida (merma)
    //   Tarjetas: total, stock adecuado, stock bajo y proveedores
    //     (clic en ellas filtra; "Proveedores" abre su registro)
    //   Detalle: guna2TextBox1 nombre · guna2ComboBox1 categoría
    //     guna2TextBox3 stock actual · guna2TextBox2 stock mínimo
    //     guna2TextBox4 último proveedor · guna2TextBox6 estado
    //     guna2TextBox5 descripción · guna2Button18/16/17 Guardar/Editar/Eliminar
    //     guna2Button19 Cambiar imagen · guna2PictureBox39
    // El stock solo cambia con compras (entradas) y mermas (salidas).
    // ==========================================================
    public partial class FRM_INVENTARIO : Form, IActualizableAlMostrar
    {
        private const string TextoEditar = "         Editar";
        private const string TextoCancelar = "         Cancelar";
        private const string TextoEliminar = "         Eliminar";
        private const string TextoReactivar = "         Reactivar";
        private const string SinCategoria = "(Sin categoría)";
        private const int MovimientosRecientes = 20;

        private TarjetasInventario tarjetas;
        private List<InsumoInfo> insumos = new List<InsumoInfo>();
        private InsumoInfo seleccionado;
        private bool editando;
        private string grupoActual = "todos";
        private string estadoActual = "todos";      // todos / adecuado / bajo
        private readonly List<Control> filasMovimiento = new List<Control>();

        public FRM_INVENTARIO()
        {
            InitializeComponent();
            // ventana grande
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            tarjetas = new TarjetasInventario(flowProductos);
            tarjetas.Seleccionado += Seleccionar;
            tarjetas.DobleClic += i => DialogosInventario.PlatosQueUsan(this, i);

            ConfigurarTextos();
            ConfigurarControles();
            MostrarInsumo(null);

            Shown += (s, e) => Cargar(null);
            FormClosed += (s, e) =>
            {
                tarjetas.Dispose();
                Image img = guna2PictureBox39.Image;
                guna2PictureBox39.Image = null;
                if (img != null) img.Dispose();
            };

            // Tarjetas de estadísticas: tenían el ícono de mesa (heredado de
            // Gestión de Mesas). Ícono propio de cada indicador.
            DisenoVentanas.PonerIcono(guna2PictureBox37, '\uE7B8', Color.Black);   // paquete: total de insumos
            DisenoVentanas.PonerIcono(guna2PictureBox34, '\uE73E', Color.Black);   // visto: stock adecuado
            DisenoVentanas.PonerIcono(guna2PictureBox36, '\uE7BA', Color.Black);   // advertencia: stock bajo
            DisenoVentanas.PonerIcono(guna2PictureBox35, '\uE719', Color.Black);   // tienda: proveedores
            DisenoVentanas.PonerIcono(guna2PictureBox38, '\uE74E', Color.Black);   // guardar (antes: mesa)

            // Diseño común: se adapta a la pantalla (1366x768 a 1920x1080 y escalado
            // de Windows) y el encabezado muestra la sesión y la hora.
            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);
        }

        public void ActualizarAlMostrar()
        {
            if (!editando)
                Cargar(seleccionado != null ? (int?)seleccionado.IdInsumo : null);
        }

        // ======================================================
        // CONFIGURACIÓN (textos del diseño heredados de Menú)
        // ======================================================
        private void ConfigurarTextos()
        {
            guna2HtmlLabel4.Text = "Insumos de cocina, compras, mermas y alertas de stock";
            guna2HtmlLabel138.Text = "Insumos de cocina";
            guna2HtmlLabel131.Text = "Movimientos recientes  ·  ver historial";
            guna2HtmlLabel131.Cursor = Cursors.Hand;
            guna2Button24.Text = "         Nuevo insumo";
            guna2Button14.Text = "          Registrar salida";
            guna2HtmlLabel6.Text = "Nombre del insumo";
            guna2HtmlLabel34.Text = "Detalle del insumo";
            guna2HtmlLabel5.Text = "Último proveedor";

            guna2HtmlLabel33.Text = "Total insumos";
            guna2HtmlLabel27.Text = "Stock adecuado";
            guna2HtmlLabel31.Text = "Stock bajo";
            guna2HtmlLabel29.Text = "Proveedores";

            txtBuscarProducto.PlaceholderText = "Buscar insumo o categoría...";
            guna2TextBox1.PlaceholderText = "Ej.: Corvina";
            guna2TextBox1.MaxLength = 100;
            guna2TextBox2.PlaceholderText = "Ej.: 5";
            guna2TextBox2.MaxLength = 12;
            guna2TextBox5.PlaceholderText = "Uso o características del insumo";
            guna2TextBox5.MaxLength = 255;
            guna2TextBox5.Multiline = true;
            foreach (Guna2TextBox t in new[] { guna2TextBox3, guna2TextBox4, guna2TextBox6 })
            {
                t.PlaceholderText = "";
                t.ReadOnly = true;
                t.TabStop = false;
            }
            guna2ComboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        private void ConfigurarControles()
        {
            txtBuscarProducto.TextChanged += (s, e) => AplicarFiltro();
            guna2Button10.Click += (s, e) => { grupoActual = "todos"; estadoActual = "todos"; AplicarFiltro(); };
            guna2Button11.Click += (s, e) => CambiarGrupo(MenuDatos.Platos);
            guna2Button12.Click += (s, e) => CambiarGrupo(MenuDatos.Bebidas);
            guna2Button13.Click += (s, e) => CambiarGrupo(MenuDatos.Agregados);

            ClicEnTarjeta(lblVentasHoy.Parent, () => { estadoActual = "todos"; AplicarFiltro(); });
            ClicEnTarjeta(guna2HtmlLabel26.Parent, () => { estadoActual = estadoActual == "adecuado" ? "todos" : "adecuado"; AplicarFiltro(); });
            ClicEnTarjeta(guna2HtmlLabel30.Parent, () => { estadoActual = estadoActual == "bajo" ? "todos" : "bajo"; AplicarFiltro(); });
            ClicEnTarjeta(guna2HtmlLabel28.Parent, AbrirProveedores);

            guna2HtmlLabel131.Click += (s, e) => DialogosInventario.Historial(this, insumos, seleccionado);

            guna2Button24.Click += (s, e) => NuevoInsumo();
            guna2Button15.Click += (s, e) => RegistrarEntrada();
            guna2Button14.Click += (s, e) => RegistrarSalida();
            guna2Button18.Click += (s, e) => Guardar();
            guna2Button16.Click += (s, e) => { if (editando) MostrarInsumo(seleccionado); else Editar(); };
            guna2Button17.Click += (s, e) => CambiarEstado();
            guna2Button19.Click += (s, e) => CambiarImagen();

            guna2TextBox2.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != ',')
                    e.Handled = true;
            };

            // Movimientos: la fila del diseño es el molde.
            flowLayoutPanel1.AutoScroll = true;
            FijarColumnas(guna2Panel31);
            filasMovimiento.Add(guna2Panel31);
            guna2Panel31.Visible = false;
        }

        // Fila de movimiento en dos renglones (seis datos no caben legibles en
        // una sola línea en pantallas chicas):
        //   1.º  TIPO   Insumo (+cantidad)                      Hoy   06/11
        //   2.º         Detalle (proveedor / motivo)   ·   Responsable
        // Orden de las etiquetas del diseño (por posición): tipo, insumo,
        // detalle, responsable, "Hoy/Ayer" y fecha.
        private static void FijarColumnas(Control fila)
        {
            Rectangle[] r =
            {
                new Rectangle(8, 4, 100, 27),      // tipo
                new Rectangle(112, 4, 420, 27),    // insumo y cantidad
                new Rectangle(112, 32, 400, 21),   // detalle
                new Rectangle(516, 32, 216, 21),   // responsable
                new Rectangle(540, 4, 70, 27),     // Hoy / Ayer
                new Rectangle(614, 4, 118, 27)     // fecha
            };
            Guna2HtmlLabel[] l = fila.Controls.OfType<Guna2HtmlLabel>().OrderBy(c => c.Left).ToArray();
            for (int i = 0; i < l.Length && i < r.Length; i++)
            {
                l[i].Tag = i;   // número de columna: el relleno ya no depende de la posición
                l[i].AutoSize = false;
                l[i].Bounds = r[i];
                l[i].TextAlignment = i >= 4 ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
                if (i == 2 || i == 3)
                    l[i].Font = new Font(l[i].Font.FontFamily, 8.5F, FontStyle.Italic);
            }
        }

        private static void ClicEnTarjeta(Control panel, Action accion)
        {
            if (panel == null) return;
            panel.Cursor = Cursors.Hand;
            panel.Click += (s, e) => accion();
            foreach (Control c in panel.Controls)
            {
                c.Cursor = Cursors.Hand;
                c.Click += (s, e) => accion();
            }
        }

        // ======================================================
        // CARGA Y FILTROS
        // ======================================================
        private void Cargar(int? seleccionar)
        {
            List<MovimientoInfo> movimientos = new List<MovimientoInfo>();
            int proveedores = 0;
            try
            {
                Cursor = Cursors.WaitCursor;
                insumos = InventarioDatos.Listar();
                movimientos = InventarioDatos.Historial(null, null, null, MovimientosRecientes);
                proveedores = InventarioDatos.ListarProveedores().Count;
                tarjetas.Cargar(insumos);
                CargarCategorias();
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de inventario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Inventario", ex);
                MessageBox.Show("No se pudo cargar el inventario:\n" + ex.Message, "Gestión de inventario",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            List<InsumoInfo> activos = insumos.Where(i => i.Activo).ToList();
            lblVentasHoy.Text = activos.Count.ToString("D2");
            guna2HtmlLabel26.Text = activos.Count(i => !i.BajoMinimo).ToString("D2");
            guna2HtmlLabel30.Text = activos.Count(i => i.BajoMinimo).ToString("D2");
            guna2HtmlLabel28.Text = proveedores.ToString("D2");

            MostrarMovimientos(movimientos);
            AplicarFiltro();

            InsumoInfo elegido = seleccionar.HasValue ? insumos.FirstOrDefault(i => i.IdInsumo == seleccionar.Value) : null;
            MostrarInsumo(elegido);
        }

        private void CargarCategorias()
        {
            guna2ComboBox1.Items.Clear();
            guna2ComboBox1.Items.Add(SinCategoria);
            foreach (string c in InventarioDatos.CategoriasSugeridas.Union(insumos.Select(i => i.Categoria).Where(c => c.Length > 0))
                     .Distinct().OrderBy(c => c))
                guna2ComboBox1.Items.Add(c);
        }

        private void CambiarGrupo(string grupo)
        {
            grupoActual = grupoActual == grupo ? "todos" : grupo;
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (tarjetas == null) return;
            string buscado = MenuDatos.Normalizar(txtBuscarProducto.Text);

            tarjetas.Filtrar(i =>
                (grupoActual == "todos" || i.Grupos.Contains(grupoActual)) &&
                (estadoActual == "todos" || (i.Activo && (estadoActual == "bajo") == i.BajoMinimo)) &&
                (buscado.Length == 0 ||
                 MenuDatos.Normalizar(i.Nombre).Contains(buscado) ||
                 MenuDatos.Normalizar(i.Categoria).Contains(buscado) ||
                 MenuDatos.Normalizar(i.Descripcion).Contains(buscado)));

            foreach (KeyValuePair<Guna2Button, string> b in new Dictionary<Guna2Button, string>
                     {
                         { guna2Button10, "todos" }, { guna2Button11, MenuDatos.Platos },
                         { guna2Button12, MenuDatos.Bebidas }, { guna2Button13, MenuDatos.Agregados }
                     })
                b.Key.FillColor = b.Value == grupoActual ? Color.Orange : Color.Gold;
        }

        // Lista de "Movimientos recientes" con la fila del diseño como molde.
        private void MostrarMovimientos(List<MovimientoInfo> movimientos)
        {
            flowLayoutPanel1.SuspendLayout();
            while (filasMovimiento.Count < Math.Max(1, movimientos.Count))
                filasMovimiento.Add(CrearFilaMovimiento());

            for (int k = 0; k < filasMovimiento.Count; k++)
            {
                Control fila = filasMovimiento[k];
                if (k >= movimientos.Count)
                {
                    fila.Visible = movimientos.Count == 0 && k == 0;
                    if (fila.Visible) LlenarFila(fila, null);
                    continue;
                }
                LlenarFila(fila, movimientos[k]);
                fila.Visible = true;
            }
            flowLayoutPanel1.ResumeLayout(true);
        }

        private static void LlenarFila(Control fila, MovimientoInfo m)
        {
            Guna2HtmlLabel[] l = fila.Controls.OfType<Guna2HtmlLabel>()
                .OrderBy(c => c.Tag is int ? (int)c.Tag : 100 + c.Left).ToArray();
            // tipo · insumo · detalle · responsable · fecha · relativo
            if (l.Length < 6) return;
            if (m == null)
            {
                l[0].Text = "";
                l[1].Text = "Sin movimientos registrados";
                for (int i = 2; i < l.Length; i++) l[i].Text = "";
                return;
            }
            l[0].Text = m.Tipo;
            l[0].ForeColor = m.Tipo == "ENTRADA" ? Color.FromArgb(0, 192, 0) : Color.Firebrick;
            l[1].Text = m.Insumo + " (" + (m.Tipo == "SALIDA" ? "-" : "+") + InventarioDatos.Cantidad(m.Cantidad) + " " + m.Unidad + ")";
            l[2].Text = m.Detalle;
            l[3].Text = m.Responsable;
            int dias = (DateTime.Today - m.Fecha.Date).Days;
            l[4].Text = dias == 0 ? "Hoy" : dias == 1 ? "Ayer" : "";
            l[5].Text = m.Fecha.ToString("dd/MM");
        }

        private Control CrearFilaMovimiento()
        {
            Guna2Panel modelo = guna2Panel31;
            Guna2Panel fila = new Guna2Panel
            {
                Size = modelo.Size, Margin = modelo.Margin, BackColor = modelo.BackColor,
                FillColor = modelo.FillColor, BorderRadius = modelo.BorderRadius,
                BorderColor = modelo.BorderColor, BorderThickness = modelo.BorderThickness
            };
            foreach (Guna2HtmlLabel m in modelo.Controls.OfType<Guna2HtmlLabel>())
            {
                fila.Controls.Add(new Guna2HtmlLabel
                {
                    Location = m.Location, AutoSize = false, Size = m.Size, TextAlignment = m.TextAlignment, Tag = m.Tag,
                    Font = m.Font, ForeColor = m.ForeColor, BackColor = m.BackColor
                });
            }
            flowLayoutPanel1.Controls.Add(fila);
            return fila;
        }

        // ======================================================
        // DETALLE
        // ======================================================
        private void Seleccionar(InsumoInfo i)
        {
            if (i == null) return;
            if (editando)
            {
                if (MessageBox.Show("Hay cambios sin guardar. ¿Descartarlos?", "Gestión de inventario",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
            }
            MostrarInsumo(i);
        }

        private void MostrarInsumo(InsumoInfo i)
        {
            seleccionado = i;
            editando = false;

            guna2TextBox1.Text = i == null ? "" : i.Nombre;
            guna2TextBox3.Text = i == null ? "" : i.StockTexto;
            guna2TextBox2.Text = i == null ? "" : InventarioDatos.Cantidad(i.StockMinimo);
            guna2TextBox4.Text = i == null ? "" : (i.UltimoProveedor ?? "Sin compras registradas");
            guna2TextBox6.Text = i == null ? "" : (i.Activo ? "Activo · " : "Inactivo · ") + i.EstadoStock;
            guna2TextBox6.ForeColor = i == null ? Color.Black : !i.Activo ? Color.Gray : i.BajoMinimo ? Color.DarkOrange : Color.ForestGreen;
            guna2TextBox5.Text = i == null ? "" : i.Descripcion;
            guna2HtmlLabel7.Text = i == null ? "Seleccione un insumo" : "Código " + i.IdInsumo + " · " + i.Unidad;

            guna2ComboBox1.SelectedIndex = -1;
            if (i != null)
            {
                string cat = i.Categoria.Length == 0 ? SinCategoria : i.Categoria;
                if (!guna2ComboBox1.Items.Contains(cat)) guna2ComboBox1.Items.Add(cat);
                guna2ComboBox1.SelectedItem = cat;
            }

            Image anterior = guna2PictureBox39.Image;
            Image nueva = null;
            if (i != null)
            {
                Image tarjeta = tarjetas.ImagenDe(i.IdInsumo);
                nueva = tarjeta == null ? null : new Bitmap(tarjeta);
            }
            guna2PictureBox39.Image = nueva;
            if (anterior != null) anterior.Dispose();

            ActualizarModo();
        }

        private void ActualizarModo()
        {
            bool hay = seleccionado != null;
            guna2TextBox1.ReadOnly = !editando;
            guna2TextBox2.ReadOnly = !editando;
            guna2TextBox5.ReadOnly = !editando;
            guna2ComboBox1.Enabled = editando;

            guna2Button18.Enabled = editando;
            guna2Button16.Enabled = hay;
            guna2Button16.Text = editando ? TextoCancelar : TextoEditar;
            guna2Button17.Enabled = hay && !editando;
            guna2Button17.Text = hay && !seleccionado.Activo ? TextoReactivar : TextoEliminar;
            guna2Button19.Enabled = hay;
        }

        // ======================================================
        // OPERACIONES (Administrador; SQL lo vuelve a validar)
        // ======================================================
        private int? Operador()
        {
            if (!SesionActual.IdEmpleado.HasValue || !Permisos.PuedeGestionarInventario(SesionActual.Cargo))
            {
                MessageBox.Show("Su perfil no puede modificar el inventario.", "Gestión de inventario",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return SesionActual.IdEmpleado.Value;
        }

        private void NuevoInsumo()
        {
            int? op = Operador();
            if (!op.HasValue) return;
            int? id = DialogosInventario.NuevoInsumo(this, insumos.Select(i => i.Categoria), op.Value);
            if (id.HasValue)
                Cargar(id);
        }

        private void RegistrarEntrada()
        {
            int? op = Operador();
            if (!op.HasValue) return;
            if (DialogosInventario.RegistrarEntrada(this, insumos, seleccionado, op.Value))
            {
                MessageBox.Show("Compra registrada. El stock se actualizó.", "Registrar entrada",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cargar(seleccionado != null ? (int?)seleccionado.IdInsumo : null);
            }
        }

        private void RegistrarSalida()
        {
            int? op = Operador();
            if (!op.HasValue) return;
            if (DialogosInventario.RegistrarSalida(this, insumos, seleccionado, op.Value))
            {
                MessageBox.Show("Merma registrada. El stock se actualizó.", "Registrar salida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cargar(seleccionado != null ? (int?)seleccionado.IdInsumo : null);
            }
        }

        private void AbrirProveedores()
        {
            int? op = Operador();
            if (!op.HasValue) return;
            DialogosInventario.Proveedores(this, op.Value);
            Cargar(seleccionado != null ? (int?)seleccionado.IdInsumo : null);
        }

        private void Editar()
        {
            if (seleccionado == null || Operador() == null) return;
            editando = true;
            ActualizarModo();
            guna2TextBox1.Focus();
        }

        private void Guardar()
        {
            int? op = Operador();
            if (!op.HasValue || !editando || seleccionado == null) return;

            try
            {
                string categoria = guna2ComboBox1.SelectedItem as string;
                InsumoInfo i = new InsumoInfo
                {
                    IdInsumo = seleccionado.IdInsumo,
                    Nombre = guna2TextBox1.Text,
                    Unidad = seleccionado.Unidad,
                    StockMinimo = InventarioDatos.LeerCantidad(guna2TextBox2.Text.Length == 0 ? "0" : guna2TextBox2.Text, "El stock mínimo", true),
                    Categoria = categoria == null || categoria == SinCategoria ? "" : categoria,
                    Descripcion = guna2TextBox5.Text
                };
                InventarioDatos.ActualizarInsumo(i, op.Value);
                editando = false;
                MessageBox.Show("Insumo actualizado.", "Gestión de inventario", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cargar(i.IdInsumo);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de inventario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Inventario", ex);
                MessageBox.Show("No se pudo guardar en SQL Server. No se registró ningún cambio.\n\n" + ex.Message,
                    "Gestión de inventario", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // "Eliminar" = desactivar (o reactivar). Nunca borra.
        private void CambiarEstado()
        {
            InsumoInfo i = seleccionado;
            if (i == null) return;
            int? op = Operador();
            if (!op.HasValue) return;

            bool activar = !i.Activo;
            string pregunta = activar
                ? "¿Reactivar el insumo " + i.Nombre + "?"
                : "¿Desactivar el insumo " + i.Nombre + "?\n\nNo se borra: sus compras, mermas y recetas se conservan. " +
                  "No podrá recibir entradas ni salidas mientras esté inactivo." +
                  (i.Platos.Count > 0 ? "\n\nLo usan: " + string.Join(", ", i.Platos) + "." : "");
            if (MessageBox.Show(pregunta, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            try
            {
                InventarioDatos.CambiarEstado(i.IdInsumo, activar, op.Value);
                Cargar(i.IdInsumo);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de inventario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Inventario", ex);
                MessageBox.Show("No se pudo cambiar el estado:\n" + ex.Message, "Gestión de inventario",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CambiarImagen()
        {
            if (seleccionado == null || Operador() == null) return;
            using (OpenFileDialog dialogo = new OpenFileDialog())
            {
                dialogo.Title = "Elegir imagen del insumo";
                dialogo.Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
                if (dialogo.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    TarjetasInventario.GuardarFoto(seleccionado.IdInsumo, dialogo.FileName);
                    Registro.Info("Inventario", "Empleado " + SesionActual.IdEmpleado + " cambió la imagen del insumo " + seleccionado.IdInsumo + ".");
                    Cargar(seleccionado.IdInsumo);
                }
                catch (ReglaNegocioException ex)
                {
                    MessageBox.Show(ex.Message, "Cambiar imagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    Registro.Error("Inventario", ex);
                    MessageBox.Show("No se pudo usar la imagen:\n" + ex.Message, "Cambiar imagen",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ======================================================
        // NAVEGACIÓN (eventos del diseñador)
        // ======================================================
        private void but_Cerrar_Click(object sender, EventArgs e)
        {
            // "Cerrar Sesión": libera los módulos y vuelve al inicio de sesión.
            Navegador.CerrarSesion();
        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRMS_PEDIDOS>(this);
        }

        private void guna2Button6_Click(object sender, EventArgs e)
        {
        }
    }
}
