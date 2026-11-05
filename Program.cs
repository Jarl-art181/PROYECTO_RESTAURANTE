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

            // Conserva el comportamiento del repositorio: arranca en Inicio.
            // Si ya activaste INICIAR_SESIÓN, NO reemplaces Program.cs sin adaptar
            // esta parte al formulario que mantiene abierto el programa.
            FRM_INICIO ventanaPrincipal = new FRM_INICIO();
            ventanaPrincipal.Shown += (s, e) =>
                ControlReservasAutomaticas.Iniciar(ventanaPrincipal);

            try
            {
                Application.Run(ventanaPrincipal);
            }
            finally
            {
                ControlReservasAutomaticas.Detener();
            }
        }
    }
}
