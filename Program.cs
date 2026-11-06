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

            // Errores no controlados: se registran y se informan una vez,
            // sin cerrar la aplicación por un fallo recuperable.
            Application.ThreadException += (s, e) =>
            {
                Registro.Error("Aplicacion", e.Exception);
                MessageBox.Show(
                    "Ocurrió un error inesperado:\n" + e.Exception.Message +
                    "\n\nEl detalle quedó registrado en:\n" + Registro.Ruta,
                    "Al Estilo Piurano",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Registro.Error("Aplicacion", e.ExceptionObject as Exception);

            // Primero se exige iniciar sesión. Tras autenticar,
            // INICIAR_SESIÓN abre Inicio (Navegador.IniciarAplicacion)
            // y arranca UNA sola vez el monitor de reservas.
            // Cerrar la ventana de acceso termina la aplicación.
            INICIAR_SESIÓN acceso = new INICIAR_SESIÓN();

            try
            {
                Application.Run(acceso);
            }
            finally
            {
                ControlReservasAutomaticas.Detener();
            }
        }
    }
}
