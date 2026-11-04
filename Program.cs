
using System;
using System.Windows.Forms;

namespace PROYECTO_RESTAURANTE
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Inicio de sesión desactivado temporalmente.
            // Application.Run(new INICIAR_SESIÓN());

            // Abrir directamente la ventana principal.
            Application.Run(new FRM_INICIO());
        }
    }
}
