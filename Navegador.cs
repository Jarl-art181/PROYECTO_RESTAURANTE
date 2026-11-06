using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Formularios que necesitan recargar sus datos cuando
    // vuelven a mostrarse (después de estar ocultos).
    // ==========================================================
    internal interface IActualizableAlMostrar
    {
        void ActualizarAlMostrar();
    }

    // ==========================================================
    // NAVEGACIÓN CENTRALIZADA
    // ----------------------------------------------------------
    // Antes: cada botón del menú hacía
    //     new FRM_X().Show(); this.Hide();
    // y el formulario anterior quedaba oculto en memoria para
    // siempre (+115 a +250 MB por apertura).
    //
    // Ahora:
    //  * Existe como máximo UNA instancia de cada módulo.
    //  * Los módulos operativos (Inicio, Pedidos, Mesas y
    //    Cocina) se reutilizan: se ocultan al salir y se
    //    actualizan al volver.
    //  * Los demás módulos se cierran y liberan al salir.
    //  * El formulario principal nunca se cierra, porque
    //    mantiene viva la aplicación (Application.Run).
    //  * Los formularios modales (Generar_Boleta,
    //    RESERVAR_MESA) no pasan por aquí: siguen usando
    //    ShowDialog dentro de un bloque using.
    // ==========================================================
    internal static class Navegador
    {
        // Módulos que conservan su instancia al ocultarse.
        // Tras optimizar las imágenes cada módulo ocupa solo 6-11 MB,
        // así que se reutilizan todos: volver a uno ya visitado tarda
        // ~150 ms en lugar de recrear 200-290 controles (0,6-0,9 s).
        // Todos se liberan al cerrar sesión.
        private static readonly HashSet<Type> reutilizables =
            new HashSet<Type>
            {
                typeof(FRM_INICIO),
                typeof(FRMS_PEDIDOS),
                typeof(FRM_MESAS),
                typeof(FRM_COCINA),
                typeof(FRM_EMPLEADOS),
                typeof(FRM_MENU),
                typeof(FRM_INVENTARIO),
                typeof(FRM_CLIENTES),
                typeof(FRM_PROMOCIONES),
                typeof(FRM_REPORTES)
            };

        // Una instancia como máximo por tipo de formulario.
        private static readonly Dictionary<Type, Form> instancias =
            new Dictionary<Type, Form>();

        private static Form principal;

        // Evita reaccionar a los cierres que hace el propio
        // Navegador (solo interesan los cierres del usuario).
        private static bool cerrandoDesdeNavegador;

        // Ventana de inicio de sesión (formulario de Application.Run).
        private static Form ventanaLogin;

        // ------------------------------------------------------
        // Registrar el formulario principal de los módulos (Inicio).
        // ------------------------------------------------------
        public static void EstablecerPrincipal(Form formulario)
        {
            if (formulario == null)
                throw new ArgumentNullException("formulario");

            principal = formulario;
            Registrar(formulario);
            MostrarUsuarioEnCabecera(formulario);
        }

        // ------------------------------------------------------
        // Tras un inicio de sesión correcto: abrir Inicio y ocultar
        // la ventana de acceso (que sigue siendo la principal de la
        // aplicación y el destino de los avisos de reservas).
        // ------------------------------------------------------
        public static void IniciarAplicacion(Form login)
        {
            ventanaLogin = login;

            FRM_INICIO inicio = new FRM_INICIO();
            EstablecerPrincipal(inicio);

            inicio.Show();
            inicio.BringToFront();
            inicio.Activate();
            login.Hide();

            // Una sola instancia del monitor durante toda la aplicación.
            ControlReservasAutomaticas.Iniciar(login);
        }

        // ------------------------------------------------------
        // CERRAR SESIÓN: cierra y libera TODOS los módulos (incluido
        // Inicio), borra la sesión y vuelve a la ventana de acceso.
        // ------------------------------------------------------
        public static void CerrarSesion()
        {
            List<Form> abiertos = new List<Form>(instancias.Values);
            Form inicio = principal;
            principal = null;   // ya no se protege: también se libera

            cerrandoDesdeNavegador = true;
            try
            {
                // Las ventanas modales abiertas (boleta, reservas) se cierran primero.
                foreach (Form f in new List<Form>(Application.OpenForms.Cast<Form>()))
                {
                    if (f.Modal && !f.IsDisposed)
                        f.Close();
                }

                foreach (Form f in abiertos)
                {
                    if (f != null && !f.IsDisposed)
                        f.Close();
                }

                if (inicio != null && !inicio.IsDisposed)
                    inicio.Close();
            }
            finally
            {
                cerrandoDesdeNavegador = false;
            }

            instancias.Clear();
            Registro.Info("Sesion", "Cierre de sesión del empleado " + SesionActual.IdEmpleado + ".");
            SesionActual.CerrarSesion();

            if (ventanaLogin != null && !ventanaLogin.IsDisposed)
            {
                ventanaLogin.Show();
                ventanaLogin.BringToFront();
                ventanaLogin.Activate();
            }
            else
            {
                Application.Exit();
            }
        }

        // Nombre y cargo reales en la cabecera (los mismos controles
        // del diseño: guna2HtmlLabel12, guna2HtmlLabel13, guna2HtmlLabel1).
        private static void MostrarUsuarioEnCabecera(Form formulario)
        {
            if (!SesionActual.Iniciada || formulario == null)
                return;

            AsignarTexto(formulario, "guna2HtmlLabel12", SesionActual.NombreCorto);
            AsignarTexto(formulario, "guna2HtmlLabel13", SesionActual.Cargo);
            AsignarTexto(formulario, "guna2HtmlLabel1", (SesionActual.Cargo ?? "").ToUpperInvariant() + " ");
        }

        private static void AsignarTexto(Form formulario, string nombre, string texto)
        {
            Control[] encontrados = formulario.Controls.Find(nombre, true);
            if (encontrados.Length > 0 && texto != null)
                encontrados[0].Text = texto;
        }

        // ------------------------------------------------------
        // Ir a un módulo sin parámetros.
        // ------------------------------------------------------
        public static void Ir<T>(Form actual) where T : Form, new()
        {
            Form existente = Buscar(typeof(T));

            // Pedidos abierto para una mesa concreta no se
            // reutiliza como Pedidos general: se descarta.
            if (existente != null && EsPedidosDeMesa(existente))
            {
                if (!PuedeDescartar(existente))
                    return;

                Descartar(existente);

                if (existente == actual)
                    actual = null;

                existente = null;
            }

            if (existente != null)
            {
                Cambiar(actual, existente, true);
                return;
            }

            T nuevo = new T();
            Registrar(nuevo);
            Cambiar(actual, nuevo, false);
        }

        // ------------------------------------------------------
        // Abrir Pedidos con una mesa fija (desde Gestión de
        // Mesas o desde el aviso de reservas).
        // Siempre crea un contexto nuevo para esa mesa.
        // ------------------------------------------------------
        public static void AbrirPedidosConMesa(Form actual, int numeroMesa)
        {
            if (actual == null)
                actual = FormularioVisible();

            Form existente = Buscar(typeof(FRMS_PEDIDOS));

            if (existente != null)
            {
                if (!PuedeDescartar(existente))
                    return;

                Descartar(existente);

                if (existente == actual)
                    actual = null;
            }

            FRMS_PEDIDOS nuevo = new FRMS_PEDIDOS(numeroMesa);
            Registrar(nuevo);
            Cambiar(actual, nuevo, false);
        }

        // ======================================================
        // MÉTODOS INTERNOS
        // ======================================================

        private static Form Buscar(Type tipo)
        {
            Form formulario;

            if (instancias.TryGetValue(tipo, out formulario))
            {
                if (formulario != null && !formulario.IsDisposed)
                    return formulario;

                instancias.Remove(tipo);
            }

            return null;
        }

        private static void Registrar(Form formulario)
        {
            Type tipo = formulario.GetType();

            Form anterior;
            if (instancias.TryGetValue(tipo, out anterior) &&
                anterior == formulario)
            {
                return;
            }

            instancias[tipo] = formulario;
            formulario.FormClosed += Formulario_FormClosed;
            formulario.VisibleChanged += AlternarReloj;
            MostrarUsuarioEnCabecera(formulario);
        }

        // El reloj de la cabecera (timerReloj, creado por el diseñador en
        // todos los módulos) solo trabaja mientras la ventana está visible:
        // los módulos ocultos y reutilizados no consumen temporizadores.
        private static void AlternarReloj(object sender, EventArgs e)
        {
            Form formulario = sender as Form;
            if (formulario == null || formulario.IsDisposed)
                return;

            System.Reflection.FieldInfo campo = formulario.GetType().GetField(
                "timerReloj",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Timer reloj = campo == null ? null : campo.GetValue(formulario) as Timer;
            if (reloj != null)
                reloj.Enabled = formulario.Visible;
        }

        private static void Cambiar(
            Form actual, Form destino, bool reutilizado)
        {
            if (destino == actual)
            {
                // Pulsar el módulo en el que ya se está:
                // solo actualizar sus datos.
                ActualizarSiCorresponde(destino);
                return;
            }

            // Mostrar primero el destino y después retirar el
            // actual, para no dejar la pantalla vacía.
            if (destino.WindowState == FormWindowState.Minimized)
                destino.WindowState = FormWindowState.Maximized;

            destino.Show();
            destino.BringToFront();
            destino.Activate();

            if (reutilizado)
                ActualizarSiCorresponde(destino);

            if (actual != null && !actual.IsDisposed)
                Salir(actual);
        }

        private static void ActualizarSiCorresponde(Form formulario)
        {
            IActualizableAlMostrar actualizable =
                formulario as IActualizableAlMostrar;

            if (actualizable == null)
                return;

            try
            {
                actualizable.ActualizarAlMostrar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No se pudieron actualizar los datos:\n" + ex.Message,
                    formulario.Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        // Salir del formulario actual: ocultar si se reutiliza,
        // cerrar y liberar en caso contrario.
        private static void Salir(Form formulario)
        {
            if (formulario == principal ||
                reutilizables.Contains(formulario.GetType()))
            {
                formulario.Hide();
                return;
            }

            Descartar(formulario);
        }

        private static void Descartar(Form formulario)
        {
            if (formulario == principal || formulario.IsDisposed)
                return;

            cerrandoDesdeNavegador = true;
            try
            {
                // Close() de un formulario abierto con Show()
                // también lo libera (Dispose).
                formulario.Close();
            }
            finally
            {
                cerrandoDesdeNavegador = false;
            }
        }

        private static bool EsPedidosDeMesa(Form formulario)
        {
            FRMS_PEDIDOS pedidos = formulario as FRMS_PEDIDOS;
            return pedidos != null && pedidos.EsVistaDeMesa;
        }

        // Evitar perder productos sin guardar al descartar Pedidos.
        private static bool PuedeDescartar(Form formulario)
        {
            FRMS_PEDIDOS pedidos = formulario as FRMS_PEDIDOS;

            if (pedidos == null || !pedidos.TieneCambiosSinGuardar())
                return true;

            DialogResult respuesta = MessageBox.Show(
                "En Pedidos hay productos que todavía no se guardaron.\n\n" +
                "¿Desea descartarlos y continuar?",
                "Productos sin guardar",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (respuesta == DialogResult.Yes)
                return true;

            // Llevar al usuario a Pedidos para que pueda guardar.
            if (!pedidos.Visible)
            {
                Form visible = FormularioVisible();
                pedidos.Show();
                pedidos.BringToFront();

                if (visible != null && visible != pedidos)
                    Salir(visible);
            }

            return false;
        }

        private static Form FormularioVisible()
        {
            foreach (Form formulario in instancias.Values)
            {
                if (formulario != null &&
                    !formulario.IsDisposed &&
                    formulario.Visible)
                {
                    return formulario;
                }
            }

            return null;
        }

        private static void Formulario_FormClosed(
            object sender, FormClosedEventArgs e)
        {
            Form formulario = sender as Form;

            if (formulario == null)
                return;

            formulario.FormClosed -= Formulario_FormClosed;

            Form registrado;
            if (instancias.TryGetValue(formulario.GetType(), out registrado) &&
                registrado == formulario)
            {
                instancias.Remove(formulario.GetType());
            }

            if (cerrandoDesdeNavegador ||
                e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

            // El usuario cerró Inicio (por ejemplo con Alt+F4): se trata
            // como "Cerrar sesión" para no dejar la aplicación invisible.
            if (formulario == principal)
            {
                // Diferido: se ejecuta cuando Inicio terminó de cerrarse.
                if (ventanaLogin != null && !ventanaLogin.IsDisposed)
                    ventanaLogin.BeginInvoke((Action)CerrarSesion);
                else
                    Application.Exit();
                return;
            }

            // Si cerró otra ventana y no queda ninguna visible, volver
            // a mostrar Inicio.

            if (principal != null &&
                !principal.IsDisposed &&
                FormularioVisible() == null)
            {
                principal.Show();
                principal.BringToFront();
                ActualizarSiCorresponde(principal);
            }
        }
    }
}
