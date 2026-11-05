using System;
using System.Collections.Generic;
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
        private static readonly HashSet<Type> reutilizables =
            new HashSet<Type>
            {
                typeof(FRM_INICIO),
                typeof(FRMS_PEDIDOS),
                typeof(FRM_MESAS),
                typeof(FRM_COCINA)
            };

        // Una instancia como máximo por tipo de formulario.
        private static readonly Dictionary<Type, Form> instancias =
            new Dictionary<Type, Form>();

        private static Form principal;

        // Evita reaccionar a los cierres que hace el propio
        // Navegador (solo interesan los cierres del usuario).
        private static bool cerrandoDesdeNavegador;

        // ------------------------------------------------------
        // Registrar el formulario principal (Application.Run).
        // ------------------------------------------------------
        public static void EstablecerPrincipal(Form formulario)
        {
            if (formulario == null)
                throw new ArgumentNullException("formulario");

            principal = formulario;
            Registrar(formulario);
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

            // Si el usuario cerró una ventana (por ejemplo con
            // Alt+F4) y no queda ninguna visible, volver a mostrar
            // el principal para no dejar la aplicación invisible.
            if (cerrandoDesdeNavegador ||
                formulario == principal ||
                e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

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
