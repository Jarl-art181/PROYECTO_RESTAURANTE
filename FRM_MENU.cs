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
    // GESTIÓN DE MENÚ (FASE D)
    // Catálogo real de dbo.PLATO con los controles del diseño
    // (nombres sin cambiar):
    //   flowProductos (tarjetas) · guna2DataGridView1 (lista)
    //   txtBuscarProducto · guna2Button10/11/12/13 Todos/Platos/Bebidas/Agregados
    //   guna2Button24 Nuevo producto · guna2Button15 Guardar
    //   guna2Button16 Editar/Cancelar · guna2Button17 Eliminar/Reactivar
    //   guna2Button14 Cambiar imagen · guna2PictureBox29 imagen
    //   guna2TextBox1 nombre · guna2ComboBox1 categoría · guna2TextBox4 precio
    //   guna2TextBox3 stock (no aplica) · guna2TextBox2 estado · guna2TextBox5 descripción
    //   lblVentasHoy / guna2HtmlLabel26 / 30 / 28: total, platos activos,
    //   bebidas activas y promociones vigentes.
    // Los cambios se ven en Pedidos al volver a abrirlo.
    // ==========================================================
    public partial class FRM_MENU : Form, IActualizableAlMostrar
    {
        // Botón "+" de cada tarjeta del diseño -> id_plato real.
        private static readonly Dictionary<string, int> BotonesDiseno = new Dictionary<string, int>
        {
            { "guna2CircleButton2", 11 }, { "guna2CircleButton5", 12 }, { "guna2CircleButton4", 13 },
            { "guna2CircleButton3", 14 }, { "guna2CircleButton6", 15 }, { "guna2CircleButton9", 16 },
            { "guna2CircleButton1", 17 }, { "guna2CircleButton7", 18 }, { "guna2CircleButton8", 19 },
            { "guna2CircleButton11", 20 }, { "guna2CircleButton10", 21 }, { "guna2CircleButton13", 22 },
            { "guna2CircleButton12", 23 }, { "guna2CircleButton18", 24 }, { "guna2CircleButton19", 25 },
            { "guna2CircleButton15", 26 }, { "guna2CircleButton14", 27 }, { "guna2CircleButton16", 28 },
            { "guna2CircleButton17", 29 }
        };

        private const string TextoEditar = "         Editar";
        private const string TextoCancelar = "         Cancelar";
        private const string TextoEliminar = "         Eliminar";
        private const string TextoReactivar = "         Reactivar";

        private TarjetasMenu catalogo;
        private List<ProductoMenu> productos = new List<ProductoMenu>();
        private ProductoMenu seleccionado;
        private bool editando;
        private bool esNuevo;
        private string fotoPendiente;      // archivo copiado aún no guardado en SQL
        private Image vistaPendiente;
        private string grupoActual = "todos";
        private Dictionary<int, List<string>> advertencias = new Dictionary<int, List<string>>();

        public FRM_MENU()
        {
            InitializeComponent();
            // ventana grande
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;

            ConfigurarTablaProductos();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;

            catalogo = new TarjetasMenu(flowProductos, BotonesDiseno, true);
            catalogo.Pulsado += Seleccionar;
            catalogo.Seleccionado += Seleccionar;

            ConfigurarControles();
            MostrarProducto(null);

            Shown += (s, e) => CargarCatalogo(null);
            FormClosed += (s, e) =>
            {
                DescartarFotoPendiente();
                catalogo.Dispose();
            };

            // Íconos heredados de Gestión de Mesas (mesa) reemplazados por uno
            // propio de cada indicador y botón.
            DisenoVentanas.PonerIcono(guna2PictureBox37, '\uE8FD', Color.Black);   // lista: total de productos
            DisenoVentanas.PonerIcono(guna2PictureBox34, '\uED56', Color.Black);   // plato: platos activos
            DisenoVentanas.PonerIcono(guna2PictureBox36, '\uEC32', Color.Black);   // taza: bebidas
            DisenoVentanas.PonerIcono(guna2PictureBox35, '\uE8EC', Color.Black);   // etiqueta: promociones
            DisenoVentanas.PonerIcono(guna2PictureBox30, '\uE74E', Color.Black);   // guardar
            DisenoVentanas.PonerIcono(guna2PictureBox32, '\uE70F', Color.Black);   // editar
            DisenoVentanas.PonerIcono(guna2PictureBox33, '\uE74D', Color.Black);   // eliminar

            // Ícono de la barra (imagen de 1069x1014 en un cuadro de 58x38):
            // copia reducida y sin deformar.
            DisenoVentanas.ReducirImagen(guna2PictureBox28);

            // Diseño común: se adapta a la pantalla (1366x768 a 1920x1080 y escalado
            // de Windows) y el encabezado muestra la sesión y la hora.
            DisenoVentanas.Adaptar(this);
            DisenoVentanas.Encabezado(this, true);
        }

        public void ActualizarAlMostrar()
        {
            if (!editando)
                CargarCatalogo(seleccionado != null ? (int?)seleccionado.IdPlato : null);
        }

        // Tabla de productos: solo lectura, sin fila vacía editable,
        // columnas proporcionales e importes alineados a la derecha.
        private void ConfigurarTablaProductos()
        {
            DataGridView tabla = guna2DataGridView1;
            tabla.AllowUserToAddRows = false;
            tabla.AllowUserToDeleteRows = false;
            tabla.ReadOnly = true;
            tabla.MultiSelect = false;
            tabla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            EstiloTablas.Configurar(tabla, 34, 30);
            EstiloTablas.Columna(Column_N, 6, 44, EstiloTablas.Centro);
            EstiloTablas.Columna(ColumnProducto, 34, 180, EstiloTablas.Izquierda, true);
            EstiloTablas.Columna(ColumnCategoria, 18, 100, EstiloTablas.Izquierda);
            EstiloTablas.Columna(Columnprecio, 14, 90, EstiloTablas.Derecha);
            EstiloTablas.Columna(Column_Estado, 16, 84, EstiloTablas.Izquierda);
            EstiloTablas.Columna(ColumnSTOCK, 12, 70, EstiloTablas.Derecha);
        }

        private void ConfigurarControles()
        {
            guna2HtmlLabel34.Text = "Lista de productos";

            guna2TextBox1.PlaceholderText = "Ej.: Alitas BBQ (5 alitas)";
            guna2TextBox1.MaxLength = 100;
            guna2TextBox4.PlaceholderText = "Ej.: 15.00";
            guna2TextBox4.MaxLength = 8;
            guna2TextBox4.KeyPress += (s, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != ',')
                    e.Handled = true;
            };
            guna2TextBox5.PlaceholderText = "Descripción que verá el personal";
            guna2TextBox5.MaxLength = 255;
            guna2TextBox5.Multiline = true;

            // Los platos se preparan al momento: el stock se controla
            // por insumos en Inventario.
            guna2TextBox3.PlaceholderText = "";
            guna2TextBox3.ReadOnly = true;
            guna2TextBox3.TabStop = false;
            guna2TextBox2.PlaceholderText = "";
            guna2TextBox2.ReadOnly = true;
            guna2TextBox2.TabStop = false;

            guna2ComboBox1.DropDownStyle = ComboBoxStyle.DropDownList;

            txtBuscarProducto.TextChanged += (s, e) => AplicarFiltro();

            guna2Button10.Click += (s, e) => CambiarGrupo("todos");
            guna2Button11.Click += (s, e) => CambiarGrupo(MenuDatos.Platos);
            guna2Button12.Click += (s, e) => CambiarGrupo(MenuDatos.Bebidas);
            guna2Button13.Click += (s, e) => CambiarGrupo(MenuDatos.Agregados);

            guna2Button24.Click += (s, e) => NuevoProducto();
            guna2Button15.Click += (s, e) => Guardar();
            guna2Button16.Click += (s, e) =>
            {
                if (editando) CancelarEdicion();
                else Editar();
            };
            guna2Button17.Click += (s, e) => CambiarEstado();
            guna2Button14.Click += (s, e) => CambiarImagen();

            guna2DataGridView1.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    Seleccionar(guna2DataGridView1.Rows[e.RowIndex].Tag as ProductoMenu);
            };
        }

        // ======================================================
        // CARGA
        // ======================================================
        private void CargarCatalogo(int? seleccionar)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                productos = MenuDatos.Listar();
                CargarCategorias();
                // Platos cuyos insumos están en o bajo su stock mínimo.
                // Es solo un aviso: si falla (p. ej. base sin la migración
                // de la Fase E), el menú se muestra igual.
                try { advertencias = InventarioDatos.PlatosConInsumoBajo(); }
                catch (Exception ex) { Registro.Error("Menu", ex); advertencias = new Dictionary<int, List<string>>(); }
                catalogo.Advertencias = advertencias;
                catalogo.Promociones = EtiquetasPromocion();
                catalogo.Cargar(productos);

                guna2HtmlLabel28.Text = MenuDatos.PromocionesVigentes().ToString("D2");
            }
            catch (Exception ex)
            {
                Registro.Error("Menu", ex);
                MessageBox.Show("No se pudo cargar el menú:\n" + ex.Message, "Gestión de menú",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            lblVentasHoy.Text = productos.Count.ToString("D2");
            guna2HtmlLabel26.Text = productos.Count(p => p.Activo && p.Grupo == MenuDatos.Platos).ToString("D2");
            guna2HtmlLabel30.Text = productos.Count(p => p.Activo && p.Grupo == MenuDatos.Bebidas).ToString("D2");

            AplicarFiltro();

            ProductoMenu elegido = seleccionar.HasValue
                ? productos.FirstOrDefault(p => p.IdPlato == seleccionar.Value)
                : null;
            MostrarProducto(elegido);
        }

        // Promociones por plato vigentes hoy (solo etiqueta). Si falla o la
        // base no tiene la migración de la Fase F, el menú se muestra igual.
        internal static Dictionary<int, string> EtiquetasPromocion()
        {
            try { return PromocionDatos.EtiquetasVigentes(DateTime.Today); }
            catch (Exception ex) { Registro.Error("Promociones", ex); return new Dictionary<int, string>(); }
        }

        private void CargarCategorias()
        {
            guna2ComboBox1.Items.Clear();
            foreach (CategoriaInfo c in MenuDatos.ListarCategorias())
                guna2ComboBox1.Items.Add(c);
        }

        private void CambiarGrupo(string grupo)
        {
            grupoActual = grupo;
            AplicarFiltro();
        }

        private void AplicarFiltro()
        {
            if (catalogo == null)
                return;

            catalogo.Filtrar(txtBuscarProducto.Text, grupoActual);

            foreach (KeyValuePair<Guna2Button, string> b in new Dictionary<Guna2Button, string>
                     {
                         { guna2Button10, "todos" }, { guna2Button11, MenuDatos.Platos },
                         { guna2Button12, MenuDatos.Bebidas }, { guna2Button13, MenuDatos.Agregados }
                     })
            {
                b.Key.FillColor = b.Value == grupoActual ? Color.Orange : Color.Gold;
            }

            string buscado = MenuDatos.Normalizar(txtBuscarProducto.Text);
            DataGridView tabla = guna2DataGridView1;
            tabla.SuspendLayout();
            tabla.Rows.Clear();
            foreach (ProductoMenu p in productos
                .Where(p => (grupoActual == "todos" || p.Grupo == grupoActual) &&
                            (buscado.Length == 0 || MenuDatos.Normalizar(p.Nombre).Contains(buscado) ||
                             MenuDatos.Normalizar(p.Categoria).Contains(buscado)))
                .OrderBy(p => p.Categoria).ThenBy(p => p.Nombre))
            {
                int fila = tabla.Rows.Add(p.IdPlato.ToString(), p.Nombre, p.Categoria,
                    p.Precio.ToString("0.00", CultureInfo.InvariantCulture),
                    p.Activo ? "Activo" : "Inactivo", "No aplica");
                tabla.Rows[fila].Tag = p;
                if (!p.Activo)
                    tabla.Rows[fila].DefaultCellStyle.ForeColor = Color.Gray;
            }
            tabla.ResumeLayout();
            tabla.ClearSelection();
            MarcarFila();
        }

        private void MarcarFila()
        {
            if (seleccionado == null)
                return;
            foreach (DataGridViewRow r in guna2DataGridView1.Rows)
            {
                if (((ProductoMenu)r.Tag).IdPlato == seleccionado.IdPlato)
                {
                    r.Selected = true;
                    guna2DataGridView1.CurrentCell = r.Cells[1];
                    break;
                }
            }
        }

        // ======================================================
        // DETALLE
        // ======================================================
        private void Seleccionar(ProductoMenu p)
        {
            if (p == null)
                return;

            if (editando)
            {
                if (MessageBox.Show("Hay cambios sin guardar en el producto. ¿Descartarlos?", "Gestión de menú",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                CancelarEdicion();
            }

            MostrarProducto(productos.FirstOrDefault(x => x.IdPlato == p.IdPlato) ?? p);
            MarcarFila();
        }

        private void MostrarProducto(ProductoMenu p)
        {
            seleccionado = p;
            editando = false;
            esNuevo = false;

            guna2TextBox1.Text = p == null ? "" : p.Nombre;
            guna2TextBox4.Text = p == null ? "" : p.Precio.ToString("0.00", CultureInfo.InvariantCulture);
            guna2TextBox5.Text = p == null ? "" : p.Descripcion;
            guna2TextBox3.Text = p == null ? "" : "No aplica";
            guna2TextBox2.Text = p == null ? "" : (p.Activo ? "Activo" : "Inactivo");
            guna2TextBox2.ForeColor = p != null && !p.Activo ? Color.Firebrick : Color.ForestGreen;
            SeleccionarCategoria(p == null ? 0 : p.IdCategoria);
            List<string> bajos;
            guna2HtmlLabel7.Text = p == null ? "Seleccione un producto"
                : (advertencias.TryGetValue(p.IdPlato, out bajos)
                    ? "Bajo stock: " + string.Join(", ", bajos) : "Código " + p.IdPlato);
            guna2HtmlLabel7.ForeColor = p != null && advertencias.ContainsKey(p.IdPlato) ? Color.DarkOrange : SystemColors.ControlDarkDark;

            MostrarImagen(p);
            ActualizarModo();
        }

        private void MostrarImagen(ProductoMenu p)
        {
            Image anterior = guna2PictureBox29.Image;
            Image nueva = null;

            if (vistaPendiente != null)
                nueva = new Bitmap(vistaPendiente);
            else if (p != null)
            {
                Image tarjeta = catalogo.ImagenActual(p.IdPlato);
                nueva = MenuDatos.CargarFoto(p.Foto) ?? (tarjeta == null ? null : new Bitmap(tarjeta));
            }

            guna2PictureBox29.Image = nueva;
            if (anterior != null)
                anterior.Dispose();
        }

        private void SeleccionarCategoria(int idCategoria)
        {
            guna2ComboBox1.SelectedIndex = -1;
            for (int i = 0; i < guna2ComboBox1.Items.Count; i++)
            {
                if (((CategoriaInfo)guna2ComboBox1.Items[i]).IdCategoria == idCategoria)
                {
                    guna2ComboBox1.SelectedIndex = i;
                    return;
                }
            }
        }

        private void ActualizarModo()
        {
            bool hay = seleccionado != null;

            guna2TextBox1.ReadOnly = !editando;
            guna2TextBox4.ReadOnly = !editando;
            guna2TextBox5.ReadOnly = !editando;
            guna2ComboBox1.Enabled = editando;

            guna2Button15.Enabled = editando;
            guna2Button16.Enabled = editando || hay;
            guna2Button16.Text = editando ? TextoCancelar : TextoEditar;
            guna2Button17.Enabled = hay && !editando;
            guna2Button17.Text = hay && !seleccionado.Activo ? TextoReactivar : TextoEliminar;
            guna2Button14.Enabled = editando || hay;
        }

        // ======================================================
        // OPERACIONES
        // ======================================================
        private void NuevoProducto()
        {
            if (Operador() == null)
                return;
            if (editando && !Seleccionar_Descartar())
                return;

            MostrarProducto(null);
            editando = true;
            esNuevo = true;
            guna2HtmlLabel7.Text = "Nuevo producto";
            guna2TextBox2.Text = "Activo";
            guna2TextBox3.Text = "No aplica";
            ActualizarModo();
            guna2TextBox1.Focus();
        }

        private bool Seleccionar_Descartar()
        {
            if (MessageBox.Show("Hay cambios sin guardar. ¿Descartarlos?", "Gestión de menú",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return false;
            CancelarEdicion();
            return true;
        }

        private void Editar()
        {
            if (seleccionado == null || Operador() == null)
                return;
            editando = true;
            esNuevo = false;
            ActualizarModo();
            guna2TextBox1.Focus();
        }

        private void CancelarEdicion()
        {
            DescartarFotoPendiente();
            ProductoMenu p = esNuevo ? null : seleccionado;
            editando = false;
            MostrarProducto(p);
        }

        private void CambiarImagen()
        {
            if (Operador() == null)
                return;
            if (!editando)
            {
                if (seleccionado == null)
                    return;
                Editar();
            }

            using (OpenFileDialog dialogo = new OpenFileDialog())
            {
                dialogo.Title = "Elegir imagen del producto";
                dialogo.Filter = "Imágenes (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";
                if (dialogo.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    string archivo = MenuDatos.GuardarFoto(dialogo.FileName);
                    DescartarFotoPendiente();
                    fotoPendiente = archivo;
                    vistaPendiente = MenuDatos.CargarFoto(archivo);
                    MostrarImagen(seleccionado);
                }
                catch (ReglaNegocioException ex)
                {
                    MessageBox.Show(ex.Message, "Cambiar imagen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    Registro.Error("Menu", ex);
                    MessageBox.Show("No se pudo usar la imagen:\n" + ex.Message, "Cambiar imagen",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void Guardar()
        {
            int? operador = Operador();
            if (!operador.HasValue || !editando)
                return;

            try
            {
                CategoriaInfo categoria = guna2ComboBox1.SelectedItem as CategoriaInfo;
                ProductoMenu p = new ProductoMenu
                {
                    IdPlato = esNuevo ? 0 : seleccionado.IdPlato,
                    Nombre = guna2TextBox1.Text,
                    Descripcion = guna2TextBox5.Text,
                    Precio = MenuDatos.LeerPrecio(guna2TextBox4.Text),
                    IdCategoria = categoria == null ? 0 : categoria.IdCategoria,
                    Categoria = categoria == null ? null : categoria.Nombre,
                    Foto = fotoPendiente ?? (esNuevo ? null : seleccionado.Foto),
                    Estado = esNuevo ? "activo" : seleccionado.Estado
                };
                MenuDatos.Validar(p);

                string aviso = "";
                if (!esNuevo && p.Precio != seleccionado.Precio)
                {
                    aviso = "\n\nEl precio cambia de " + seleccionado.PrecioTexto + " a " + p.PrecioTexto +
                            ". Las ventas ya registradas conservan su precio.";
                    if (MessageBox.Show("¿Guardar los cambios de " + p.Nombre + "?" + aviso, "Confirmar",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                }

                Cursor = Cursors.WaitCursor;
                guna2Button15.Enabled = false;

                int id;
                if (esNuevo)
                    id = MenuDatos.Registrar(p, operador.Value);
                else
                {
                    MenuDatos.Actualizar(p, operador.Value);
                    id = p.IdPlato;
                }

                fotoPendiente = null;          // ya pertenece al producto
                LiberarVistaPendiente();
                editando = false;

                MessageBox.Show((esNuevo ? "Producto registrado" : "Producto actualizado") +
                    " correctamente.\n\n" + p.Nombre + " · " + p.PrecioTexto + " · " + p.Categoria +
                    "\n\nYa está disponible en Pedidos.", "Gestión de menú",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                CargarCatalogo(id);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de menú", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Menu", ex);
                MessageBox.Show("No se pudo guardar en SQL Server. No se registró ningún cambio.\n\n" + ex.Message,
                    "Gestión de menú", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                ActualizarModo();
            }
        }

        // "Eliminar" = desactivar (o reactivar si ya está inactivo).
        private void CambiarEstado()
        {
            ProductoMenu p = seleccionado;
            if (p == null)
                return;

            int? operador = Operador();
            if (!operador.HasValue)
                return;

            bool activar = !p.Activo;
            string pregunta = activar
                ? "¿Reactivar " + p.Nombre + "? Volverá a aparecer en Pedidos."
                : "¿Desactivar " + p.Nombre + "?\n\nDejará de aparecer en Pedidos. No se borra: " +
                  "sus ventas, recetas y promociones se conservan y puede reactivarse.";

            if (MessageBox.Show(pregunta, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            try
            {
                MenuDatos.CambiarEstado(p.IdPlato, activar, operador.Value);
                CargarCatalogo(p.IdPlato);
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(ex.Message, "Gestión de menú", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Menu", ex);
                MessageBox.Show("No se pudo cambiar el estado:\n" + ex.Message, "Gestión de menú",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int? Operador()
        {
            if (!SesionActual.IdEmpleado.HasValue || !Permisos.PuedeGestionarMenu(SesionActual.Cargo))
            {
                MessageBox.Show("Su perfil no puede modificar el menú.", "Gestión de menú",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            return SesionActual.IdEmpleado.Value;
        }

        // Foto copiada pero no guardada: se borra para no dejar archivos sueltos.
        private void DescartarFotoPendiente()
        {
            LiberarVistaPendiente();
            if (fotoPendiente == null)
                return;
            try { File.Delete(Path.Combine(MenuDatos.CarpetaFotos, fotoPendiente)); }
            catch (Exception ex) { Registro.Error("Menu", ex); }
            fotoPendiente = null;
        }

        private void LiberarVistaPendiente()
        {
            if (vistaPendiente != null)
            {
                vistaPendiente.Dispose();
                vistaPendiente = null;
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

        private void guna2HtmlLabel34_Click(object sender, EventArgs e)
        {
        }

        private void guna2Button8_Click(object sender, EventArgs e)
        {
            Navegador.Ir<FRM_INVENTARIO>(this);
        }

        private void guna2PictureBox28_Click(object sender, EventArgs e)
        {

        }
    }
}
