using Guna.UI2.WinForms;
using System;
using System.Reflection;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // CORRECCIONES DE LA LIBRERÍA GUNA UI2 (v2.0.4.8)
    // ----------------------------------------------------------
    // Guna2DateTimePicker crea internamente una ventana de
    // calendario (campo privado _dialog, un Form) que NUNCA libera
    // al desecharse el control: cada apertura de RESERVAR_MESA
    // dejaba 3 objetos GDI y 6 objetos USER sin liberar (medido).
    // Aquí se libera ese calendario cuando el control se desecha.
    // Si una versión futura de Guna cambia el campo, no hace nada.
    // ==========================================================
    internal static class CorreccionesGuna
    {
        private static readonly FieldInfo campoCalendario =
            typeof(Guna2DateTimePicker).GetField(
                "_dialog", BindingFlags.Instance | BindingFlags.NonPublic);

        // Registra la corrección en todos los Guna2DateTimePicker del control.
        public static void Aplicar(Control raiz)
        {
            if (raiz == null || campoCalendario == null)
                return;

            foreach (Control hijo in raiz.Controls)
            {
                Guna2DateTimePicker selector = hijo as Guna2DateTimePicker;
                if (selector != null)
                    selector.Disposed += LiberarCalendario;

                if (hijo.HasChildren)
                    Aplicar(hijo);
            }
        }

        private static void LiberarCalendario(object sender, EventArgs e)
        {
            try
            {
                IDisposable calendario = campoCalendario.GetValue(sender) as IDisposable;
                if (calendario != null)
                    calendario.Dispose();
            }
            catch (Exception ex)
            {
                Registro.Error("Guna", ex);
            }
        }
    }
}
