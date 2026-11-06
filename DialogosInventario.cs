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
    // VENTANAS DE INVENTARIO (construidas por código, mismo
    // estilo amarillo/blanco/negro que "Cuenta de acceso").
    // Sin archivos .Designer ni .resx.
    // ==========================================================
    internal static class DialogosInventario
    {
        // ------------------------------------------------------
        // ESTILO COMÚN
        // ------------------------------------------------------
        private static Form Ventana(string titulo, int ancho, int alto)
        {
            Form f = new Form
            {
                Text = titulo,
                ClientSize = new Size(ancho, alto),
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Color.White,
                ShowInTaskbar = false,
                Font = new Font("Segoe UI", 10F)
            };
            Guna2Panel encabezado = new Guna2Panel { Dock = DockStyle.Top, Height = 52, FillColor = Color.Gold };
            encabezado.Controls.Add(new Label
            {
                Text = titulo.ToUpperInvariant(),
                Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            f.Controls.Add(encabezado);
            return f;
        }

        private static Label Etiqueta(Form f, string texto, int x, int y, int ancho = 0)
        {
            Label l = new Label { Text = texto, Location = new Point(x, y), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            if (ancho > 0) { l.AutoSize = false; l.Size = new Size(ancho, 20); } else l.AutoSize = true;
            f.Controls.Add(l);
            return l;
        }

        private static Label Nota(Form f, string texto, int x, int y, int ancho, int alto)
        {
            Label l = new Label { Text = texto, Location = new Point(x, y), Size = new Size(ancho, alto), Font = new Font("Segoe UI", 9F), ForeColor = Color.DimGray };
            f.Controls.Add(l);
            return l;
        }

        private static Guna2TextBox Caja(Form f, int x, int y, int ancho, int maximo, int alto = 36)
        {
            Guna2TextBox t = new Guna2TextBox
            {
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.Black,
                BorderRadius = 8,
                BorderColor = Color.Goldenrod,
                MaxLength = maximo
            };
            f.Controls.Add(t);
            // Guna reajusta la posición al cambiar la fuente: se fija al final.
            t.Bounds = new Rectangle(x, y, ancho, alto);
            return t;
        }

        private static ComboBox Combo(Form f, int x, int y, int ancho, bool editable)
        {
            ComboBox c = new ComboBox
            {
                Location = new Point(x, y + 4),
                Width = ancho,
                Font = new Font("Segoe UI", 10F),
                DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList
            };
            f.Controls.Add(c);
            return c;
        }

        private static Guna2Button Boton(Form f, string texto, int x, int y, int ancho, Color relleno)
        {
            Guna2Button b = new Guna2Button
            {
                Text = texto,
                Location = new Point(x, y),
                Size = new Size(ancho, 38),
                BorderRadius = 10,
                FillColor = relleno,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            f.Controls.Add(b);
            return b;
        }

        private static Guna2DataGridView Tabla(Form f, int x, int y, int ancho, int alto)
        {
            Guna2DataGridView t = new Guna2DataGridView
            {
                Location = new Point(x, y),
                Size = new Size(ancho, alto),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            f.Controls.Add(t);
            return t;
        }

        private static void EstiloTabla(Guna2DataGridView t)
        {
            EstiloTablas.Configurar(t, 34, 28);
            t.ThemeStyle.HeaderStyle.BackColor = Color.Gold;
            t.ThemeStyle.HeaderStyle.ForeColor = Color.Black;
        }

        private static bool Ejecutar(IWin32Window dueno, string titulo, Action accion)
        {
            try
            {
                accion();
                return true;
            }
            catch (ReglaNegocioException ex)
            {
                MessageBox.Show(dueno, ex.Message, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Registro.Error("Inventario", ex);
                MessageBox.Show(dueno, "No se pudo completar la operación. No se guardó ningún cambio.\n\n" + ex.Message,
                    titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return false;
        }

        private static DateTimePicker Fecha(Form f, int x, int y)
        {
            DateTimePicker d = new DateTimePicker
            {
                Location = new Point(x, y + 4),
                Width = 150,
                Format = DateTimePickerFormat.Short,
                MaxDate = DateTime.Today,
                Value = DateTime.Today,
                Font = new Font("Segoe UI", 10F)
            };
            f.Controls.Add(d);
            return d;
        }

        // ------------------------------------------------------
        // NUEVO INSUMO (nace con stock 0)
        // ------------------------------------------------------
        public static int? NuevoInsumo(IWin32Window dueno, IEnumerable<string> categoriasExistentes, int idOperador)
        {
            using (Form f = Ventana("Nuevo insumo", 480, 470))
            {
                Etiqueta(f, "Nombre del insumo", 24, 68);
                Guna2TextBox nombre = Caja(f, 24, 90, 430, 100);
                Etiqueta(f, "Unidad de medida", 24, 138);
                ComboBox unidad = Combo(f, 24, 158, 200, true);
                unidad.Items.AddRange(InventarioDatos.UnidadesSugeridas);
                unidad.MaxLength = 20;
                Etiqueta(f, "Stock mínimo", 254, 138);
                Guna2TextBox minimo = Caja(f, 254, 158, 200, 12);
                Etiqueta(f, "Categoría", 24, 206);
                ComboBox categoria = Combo(f, 24, 226, 430, true);
                categoria.Items.AddRange(InventarioDatos.CategoriasSugeridas.Union(categoriasExistentes.Where(c => c.Length > 0)).Distinct().OrderBy(c => c).ToArray());
                categoria.MaxLength = 50;
                Etiqueta(f, "Descripción", 24, 274);
                Guna2TextBox descripcion = Caja(f, 24, 296, 430, 255, 60);
                descripcion.Multiline = true;
                Nota(f, "El insumo empieza con stock 0. Para cargar existencias use \"Registrar entrada\" " +
                        "(compra a proveedor).", 24, 364, 430, 36);

                Guna2Button cancelar = Boton(f, "Cancelar", 24, 412, 200, Color.Gainsboro);
                Guna2Button guardar = Boton(f, "Guardar", 254, 412, 200, Color.Gold);
                cancelar.Click += (s, e) => f.Close();

                int? id = null;
                guardar.Click += (s, e) => Ejecutar(f, "Nuevo insumo", () =>
                {
                    InsumoInfo i = new InsumoInfo
                    {
                        Nombre = nombre.Text,
                        Unidad = unidad.Text,
                        Categoria = categoria.Text,
                        Descripcion = descripcion.Text,
                        StockMinimo = InventarioDatos.LeerCantidad(minimo.Text.Length == 0 ? "0" : minimo.Text, "El stock mínimo", true)
                    };
                    id = InventarioDatos.RegistrarInsumo(i, idOperador);
                    f.DialogResult = DialogResult.OK;
                });

                f.Shown += (s, e) => nombre.Focus();
                f.ShowDialog(dueno);
                return id;
            }
        }

        // ------------------------------------------------------
        // REGISTRAR ENTRADA = COMPRA A PROVEEDOR
        // ------------------------------------------------------
        public static bool RegistrarEntrada(IWin32Window dueno, IList<InsumoInfo> insumos, InsumoInfo preseleccion, int idAdministrador)
        {
            List<InsumoInfo> activos = insumos.Where(i => i.Activo).OrderBy(i => i.Nombre).ToList();
            if (activos.Count == 0)
            {
                MessageBox.Show(dueno, "No hay insumos activos. Registre uno con \"Nuevo insumo\".", "Registrar entrada",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            Dictionary<string, InsumoInfo> porTexto = activos.ToDictionary(i => i.Nombre + " (" + i.Unidad + ")");

            using (Form f = Ventana("Registrar entrada (compra)", 720, 560))
            {
                Etiqueta(f, "Proveedor", 24, 66);
                ComboBox proveedor = Combo(f, 24, 86, 420, false);
                Guna2Button proveedores = Boton(f, "Proveedores...", 456, 86, 240, Color.Gold);
                Etiqueta(f, "N.º de factura o boleta", 24, 132);
                Guna2TextBox documento = Caja(f, 24, 152, 250, 20);
                Etiqueta(f, "Fecha de la compra", 300, 132);
                DateTimePicker fecha = Fecha(f, 300, 152);

                Etiqueta(f, "Insumos comprados", 24, 200);
                Guna2DataGridView tabla = Tabla(f, 24, 222, 672, 220);
                tabla.EditMode = DataGridViewEditMode.EditOnEnter;
                DataGridViewComboBoxColumn colInsumo = new DataGridViewComboBoxColumn { HeaderText = "INSUMO", FlatStyle = FlatStyle.Flat };
                colInsumo.Items.AddRange(porTexto.Keys.ToArray());
                tabla.Columns.Add(colInsumo);
                tabla.Columns.Add("cantidad", "CANTIDAD");
                tabla.Columns.Add("costo", "COSTO UNIT. S/");
                tabla.Columns.Add("subtotal", "SUBTOTAL S/");
                tabla.Columns[3].ReadOnly = true;
                EstiloTabla(tabla);
                tabla.ReadOnly = false;
                tabla.Columns[3].ReadOnly = true;
                EstiloTablas.Columna(tabla.Columns[0], 50, 200, EstiloTablas.Izquierda);
                EstiloTablas.Columna(tabla.Columns[1], 16, 90, EstiloTablas.Derecha);
                EstiloTablas.Columna(tabla.Columns[2], 18, 110, EstiloTablas.Derecha);
                EstiloTablas.Columna(tabla.Columns[3], 16, 100, EstiloTablas.Derecha);

                Guna2Button agregar = Boton(f, "+ Agregar insumo", 24, 452, 180, Color.Gold);
                Guna2Button quitar = Boton(f, "Quitar línea", 214, 452, 140, Color.Gainsboro);
                Label total = new Label { Location = new Point(400, 458), Size = new Size(296, 28), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
                f.Controls.Add(total);

                Guna2Button cancelar = Boton(f, "Cancelar", 300, 504, 190, Color.Gainsboro);
                Guna2Button guardar = Boton(f, "Registrar compra", 506, 504, 190, Color.Gold);

                Action cargarProveedores = () =>
                {
                    object anterior = proveedor.SelectedItem;
                    proveedor.Items.Clear();
                    foreach (ProveedorInfo p in InventarioDatos.ListarProveedores()) proveedor.Items.Add(p);
                    if (anterior != null)
                        foreach (ProveedorInfo p in proveedor.Items) if (p.IdProveedor == ((ProveedorInfo)anterior).IdProveedor) proveedor.SelectedItem = p;
                    if (proveedor.SelectedIndex < 0 && proveedor.Items.Count == 1) proveedor.SelectedIndex = 0;
                };
                cargarProveedores();
                proveedores.Click += (s, e) => { Proveedores(f, idAdministrador); cargarProveedores(); };

                Action recalcular = () =>
                {
                    decimal suma = 0;
                    foreach (DataGridViewRow r in tabla.Rows)
                    {
                        decimal c, k;
                        bool okc = decimal.TryParse(Convert.ToString(r.Cells[1].Value).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out c);
                        bool okk = decimal.TryParse(Convert.ToString(r.Cells[2].Value).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out k);
                        r.Cells[3].Value = okc && okk ? (c * k).ToString("0.00", CultureInfo.InvariantCulture) : "";
                        if (okc && okk) suma += c * k;
                    }
                    total.Text = "Total: S/ " + suma.ToString("0.00", CultureInfo.InvariantCulture);
                };
                tabla.CellEndEdit += (s, e) => recalcular();
                agregar.Click += (s, e) => { tabla.Rows.Add(); recalcular(); };
                quitar.Click += (s, e) => { if (tabla.CurrentRow != null) tabla.Rows.Remove(tabla.CurrentRow); recalcular(); };
                cancelar.Click += (s, e) => f.Close();

                int fila = tabla.Rows.Add();
                if (preseleccion != null && preseleccion.Activo)
                    tabla.Rows[fila].Cells[0].Value = porTexto.First(p => p.Value.IdInsumo == preseleccion.IdInsumo).Key;
                recalcular();

                bool registrado = false;
                guardar.Click += (s, e) => Ejecutar(f, "Registrar entrada", () =>
                {
                    tabla.EndEdit();
                    ProveedorInfo prov = proveedor.SelectedItem as ProveedorInfo;
                    if (prov == null) throw new ReglaNegocioException("Seleccione el proveedor (o regístrelo con \"Proveedores...\").");

                    List<LineaCompra> lineas = new List<LineaCompra>();
                    foreach (DataGridViewRow r in tabla.Rows)
                    {
                        string texto = Convert.ToString(r.Cells[0].Value);
                        if (texto.Length == 0 && Convert.ToString(r.Cells[1].Value).Length == 0) continue;
                        InsumoInfo ins;
                        if (!porTexto.TryGetValue(texto, out ins)) throw new ReglaNegocioException("Elija el insumo de la línea " + (r.Index + 1) + ".");
                        lineas.Add(new LineaCompra
                        {
                            IdInsumo = ins.IdInsumo,
                            Cantidad = InventarioDatos.LeerCantidad(Convert.ToString(r.Cells[1].Value), "La cantidad de " + ins.Nombre, false),
                            CostoUnitario = InventarioDatos.LeerImporte(Convert.ToString(r.Cells[2].Value), "El costo de " + ins.Nombre)
                        });
                    }

                    InventarioDatos.RegistrarCompra(prov.IdProveedor, documento.Text, fecha.Value, lineas, idAdministrador);
                    registrado = true;
                    f.DialogResult = DialogResult.OK;
                });

                f.ShowDialog(dueno);
                return registrado;
            }
        }

        // ------------------------------------------------------
        // REGISTRAR SALIDA = MERMA (solo pérdidas)
        // ------------------------------------------------------
        public static bool RegistrarSalida(IWin32Window dueno, IList<InsumoInfo> insumos, InsumoInfo preseleccion, int idOperador)
        {
            List<InsumoInfo> conStock = insumos.Where(i => i.Activo && i.StockActual > 0).OrderBy(i => i.Nombre).ToList();
            if (conStock.Count == 0)
            {
                MessageBox.Show(dueno, "No hay insumos activos con stock para dar de baja.", "Registrar salida",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            using (Form f = Ventana("Registrar salida (merma)", 480, 500))
            {
                Etiqueta(f, "Insumo", 24, 66);
                ComboBox insumo = Combo(f, 24, 86, 430, false);
                foreach (InsumoInfo i in conStock) insumo.Items.Add(i.Nombre + "  ·  hay " + i.StockTexto);
                Etiqueta(f, "Cantidad perdida", 24, 132);
                Guna2TextBox cantidad = Caja(f, 24, 152, 200, 12);
                Label unidad = Etiqueta(f, "", 232, 160);
                unidad.Font = new Font("Segoe UI", 10F);
                Etiqueta(f, "Fecha", 300, 132);
                DateTimePicker fecha = Fecha(f, 300, 152);
                Etiqueta(f, "Motivo de la pérdida", 24, 200);
                ComboBox motivo = Combo(f, 24, 220, 430, false);
                motivo.Items.AddRange(InventarioDatos.MotivosMerma);
                Etiqueta(f, "Detalle (opcional; obligatorio en \"Otra pérdida\")", 24, 266);
                Guna2TextBox detalle = Caja(f, 24, 288, 430, 200);
                Nota(f, "Solo pérdidas: deterioro, vencimiento, cadena de frío o roturas. El consumo normal " +
                        "de cocina NO se registra aquí (no hay todavía un registro para distinguirlo).", 24, 336, 430, 54);

                Guna2Button cancelar = Boton(f, "Cancelar", 24, 440, 200, Color.Gainsboro);
                Guna2Button guardar = Boton(f, "Registrar merma", 254, 440, 200, Color.Gold);
                cancelar.Click += (s, e) => f.Close();

                insumo.SelectedIndexChanged += (s, e) =>
                {
                    unidad.Text = insumo.SelectedIndex >= 0 ? conStock[insumo.SelectedIndex].Unidad : "";
                };
                int pre = preseleccion == null ? -1 : conStock.FindIndex(i => i.IdInsumo == preseleccion.IdInsumo);
                insumo.SelectedIndex = pre >= 0 ? pre : 0;

                bool registrado = false;
                guardar.Click += (s, e) => Ejecutar(f, "Registrar salida", () =>
                {
                    if (motivo.SelectedIndex < 0) throw new ReglaNegocioException("Seleccione el motivo de la pérdida.");
                    InsumoInfo i = conStock[insumo.SelectedIndex];
                    decimal c = InventarioDatos.LeerCantidad(cantidad.Text, "La cantidad", false);
                    if (MessageBox.Show(f, "¿Dar de baja " + InventarioDatos.Cantidad(c) + " " + i.Unidad + " de " + i.Nombre +
                            " por \"" + motivo.Text + "\"?\n\nQuedarán " + InventarioDatos.Cantidad(i.StockActual - c) + " " + i.Unidad + ".",
                            "Confirmar merma", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                        return;
                    InventarioDatos.RegistrarMerma(i.IdInsumo, c, motivo.Text, detalle.Text, fecha.Value, idOperador);
                    registrado = true;
                    f.DialogResult = DialogResult.OK;
                });

                f.Shown += (s, e) => cantidad.Focus();
                f.ShowDialog(dueno);
                return registrado;
            }
        }

        // ------------------------------------------------------
        // PROVEEDORES: registro y edición
        // ------------------------------------------------------
        public static void Proveedores(IWin32Window dueno, int idOperador)
        {
            using (Form f = Ventana("Proveedores", 760, 540))
            {
                Guna2DataGridView tabla = Tabla(f, 24, 66, 712, 220);
                tabla.Columns.Add("ruc", "RUC");
                tabla.Columns.Add("nombre", "RAZÓN SOCIAL");
                tabla.Columns.Add("telefono", "TELÉFONO");
                tabla.Columns.Add("direccion", "DIRECCIÓN");
                tabla.ReadOnly = true;
                EstiloTabla(tabla);
                EstiloTablas.ColumnaFija(tabla.Columns[0], 110, EstiloTablas.Centro);
                EstiloTablas.Columna(tabla.Columns[1], 40, 160, EstiloTablas.Izquierda);
                EstiloTablas.ColumnaFija(tabla.Columns[2], 110, EstiloTablas.Centro);
                EstiloTablas.Columna(tabla.Columns[3], 40, 160, EstiloTablas.Izquierda);

                Label titulo = Etiqueta(f, "Nuevo proveedor", 24, 298, 400);
                titulo.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
                Etiqueta(f, "RUC", 24, 326);
                Guna2TextBox ruc = Caja(f, 24, 346, 200, 11);
                ruc.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
                Etiqueta(f, "Razón social", 244, 326);
                Guna2TextBox nombre = Caja(f, 244, 346, 492, 100);
                Etiqueta(f, "Teléfono", 24, 392);
                Guna2TextBox telefono = Caja(f, 24, 412, 200, 20);
                Etiqueta(f, "Dirección", 244, 392);
                Guna2TextBox direccion = Caja(f, 244, 412, 492, 255);

                Guna2Button nuevo = Boton(f, "Nuevo", 24, 480, 160, Color.Gainsboro);
                Guna2Button cerrar = Boton(f, "Cerrar", 400, 480, 160, Color.Gainsboro);
                Guna2Button guardar = Boton(f, "Guardar", 576, 480, 160, Color.Gold);

                List<ProveedorInfo> lista = new List<ProveedorInfo>();
                int idEditando = 0;

                Action limpiar = () =>
                {
                    idEditando = 0;
                    titulo.Text = "Nuevo proveedor";
                    ruc.Text = nombre.Text = telefono.Text = direccion.Text = "";
                    tabla.ClearSelection();
                };
                Action<int> cargar = seleccionar =>
                {
                    lista = InventarioDatos.ListarProveedores();
                    tabla.Rows.Clear();
                    foreach (ProveedorInfo p in lista)
                    {
                        int r = tabla.Rows.Add(p.Ruc, p.Nombre, p.Telefono.Length == 0 ? "-" : p.Telefono, p.Direccion.Length == 0 ? "-" : p.Direccion);
                        tabla.Rows[r].Tag = p;
                    }
                    tabla.ClearSelection();
                };

                tabla.CellClick += (s, e) =>
                {
                    if (e.RowIndex < 0) return;
                    ProveedorInfo p = (ProveedorInfo)tabla.Rows[e.RowIndex].Tag;
                    idEditando = p.IdProveedor;
                    titulo.Text = "Editando: " + p.Nombre;
                    ruc.Text = p.Ruc; nombre.Text = p.Nombre; telefono.Text = p.Telefono; direccion.Text = p.Direccion;
                };
                nuevo.Click += (s, e) => { limpiar(); ruc.Focus(); };
                cerrar.Click += (s, e) => f.Close();
                guardar.Click += (s, e) => Ejecutar(f, "Proveedores", () =>
                {
                    InventarioDatos.GuardarProveedor(new ProveedorInfo
                    {
                        IdProveedor = idEditando, Ruc = ruc.Text, Nombre = nombre.Text, Telefono = telefono.Text, Direccion = direccion.Text
                    }, idOperador);
                    MessageBox.Show(f, "Proveedor guardado.", "Proveedores", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    cargar(0);
                    limpiar();
                });

                Ejecutar(dueno, "Proveedores", () => cargar(0));
                f.ShowDialog(dueno);
            }
        }

        // ------------------------------------------------------
        // HISTORIAL DE MOVIMIENTOS (compras y mermas)
        // ------------------------------------------------------
        public static void Historial(IWin32Window dueno, IList<InsumoInfo> insumos, InsumoInfo preseleccion)
        {
            using (Form f = Ventana("Historial de movimientos", 900, 560))
            {
                Etiqueta(f, "Insumo", 24, 66);
                ComboBox insumo = Combo(f, 24, 86, 280, false);
                insumo.Items.Add("Todos los insumos");
                List<InsumoInfo> ordenados = insumos.OrderBy(i => i.Nombre).ToList();
                foreach (InsumoInfo i in ordenados) insumo.Items.Add(i.Nombre);
                Etiqueta(f, "Desde", 324, 66);
                DateTimePicker desde = Fecha(f, 324, 86);
                desde.Value = DateTime.Today.AddMonths(-3);
                Etiqueta(f, "Hasta", 490, 66);
                DateTimePicker hasta = Fecha(f, 490, 86);
                Guna2Button buscar = Boton(f, "Buscar", 660, 86, 216, Color.Gold);

                Guna2DataGridView tabla = Tabla(f, 24, 136, 852, 370);
                foreach (string[] c in new[] { new[] { "fecha", "FECHA" }, new[] { "tipo", "TIPO" }, new[] { "insumo", "INSUMO" },
                         new[] { "cantidad", "CANTIDAD" }, new[] { "detalle", "DETALLE" }, new[] { "importe", "IMPORTE S/" }, new[] { "responsable", "RESPONSABLE" } })
                    tabla.Columns.Add(c[0], c[1]);
                tabla.ReadOnly = true;
                EstiloTabla(tabla);
                EstiloTablas.ColumnaFija(tabla.Columns[0], 92, EstiloTablas.Centro);
                EstiloTablas.ColumnaFija(tabla.Columns[1], 84, EstiloTablas.Izquierda);
                EstiloTablas.Columna(tabla.Columns[2], 16, 110, EstiloTablas.Izquierda);
                EstiloTablas.ColumnaFija(tabla.Columns[3], 96, EstiloTablas.Derecha);
                EstiloTablas.Columna(tabla.Columns[4], 34, 180, EstiloTablas.Izquierda, true);
                EstiloTablas.ColumnaFija(tabla.Columns[5], 92, EstiloTablas.Derecha);
                EstiloTablas.Columna(tabla.Columns[6], 16, 110, EstiloTablas.Izquierda);

                Label resumen = new Label { Location = new Point(24, 514), Size = new Size(852, 26), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                f.Controls.Add(resumen);

                Action consultar = () =>
                {
                    int? id = insumo.SelectedIndex > 0 ? (int?)ordenados[insumo.SelectedIndex - 1].IdInsumo : null;
                    List<MovimientoInfo> movs = InventarioDatos.Historial(id, desde.Value, hasta.Value, 2000);
                    tabla.Rows.Clear();
                    foreach (MovimientoInfo m in movs)
                    {
                        int r = tabla.Rows.Add(m.Fecha.ToString("dd/MM/yyyy"), m.Tipo, m.Insumo,
                            (m.Tipo == "SALIDA" ? "-" : "+") + InventarioDatos.Cantidad(m.Cantidad) + " " + m.Unidad,
                            m.Detalle, m.Importe.HasValue ? m.Importe.Value.ToString("0.00", CultureInfo.InvariantCulture) : "-", m.Responsable);
                        tabla.Rows[r].Cells[1].Style.ForeColor = m.Tipo == "ENTRADA" ? Color.FromArgb(0, 150, 0) : Color.Firebrick;
                    }
                    decimal compras = movs.Where(m => m.Importe.HasValue).Sum(m => m.Importe.Value);
                    resumen.Text = movs.Count + " movimiento(s) · " + movs.Count(m => m.Tipo == "ENTRADA") + " entradas (S/ " +
                        compras.ToString("0.00", CultureInfo.InvariantCulture) + ") · " + movs.Count(m => m.Tipo == "SALIDA") + " mermas";
                };

                int pre = preseleccion == null ? -1 : ordenados.FindIndex(i => i.IdInsumo == preseleccion.IdInsumo);
                insumo.SelectedIndex = pre >= 0 ? pre + 1 : 0;
                buscar.Click += (s, e) => Ejecutar(f, "Historial", consultar);
                f.Shown += (s, e) => Ejecutar(f, "Historial", consultar);
                f.ShowDialog(dueno);
            }
        }

        // ------------------------------------------------------
        // PLATOS QUE USAN UN INSUMO (RECETA_PLATO)
        // ------------------------------------------------------
        public static void PlatosQueUsan(IWin32Window dueno, InsumoInfo insumo)
        {
            List<string[]> platos = null;
            if (!Ejecutar(dueno, "Platos que lo usan", () => platos = InventarioDatos.PlatosQueUsan(insumo.IdInsumo)))
                return;

            using (Form f = Ventana("Platos que usan " + insumo.Nombre, 560, 380))
            {
                Guna2DataGridView tabla = Tabla(f, 24, 66, 512, 250);
                tabla.Columns.Add("plato", "PLATO");
                tabla.Columns.Add("cantidad", "RECETA");
                tabla.Columns.Add("estado", "ESTADO");
                tabla.ReadOnly = true;
                EstiloTabla(tabla);
                EstiloTablas.Columna(tabla.Columns[0], 50, 200, EstiloTablas.Izquierda);
                EstiloTablas.Columna(tabla.Columns[1], 34, 150, EstiloTablas.Izquierda);
                EstiloTablas.ColumnaFija(tabla.Columns[2], 80, EstiloTablas.Izquierda);
                foreach (string[] p in platos) tabla.Rows.Add(p[0], p[1], p[2]);

                Nota(f, platos.Count == 0
                        ? "Ningún plato tiene registrada una receta con este insumo."
                        : "Stock actual: " + insumo.StockTexto + " · mínimo " + InventarioDatos.Cantidad(insumo.StockMinimo) + " " + insumo.Unidad +
                          (insumo.BajoMinimo ? "  ·  ALERTA: stock bajo" : ""), 24, 324, 512, 40);
                f.ShowDialog(dueno);
            }
        }
    }
}
