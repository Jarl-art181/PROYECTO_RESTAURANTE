using System.Configuration;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Cadena de conexión ÚNICA de la aplicación.
    // Se lee de App.config (connectionStrings → AL_ESTILO_PIURA);
    // si no existe, se usa la cadena original del proyecto.
    // Así se puede apuntar a una base de PRUEBAS sin tocar código.
    // ==========================================================
    internal static class BaseDatos
    {
        private const string Predeterminada =
            @"Server=LAPTOP-GH63852S\SQLEXPRESS;Database=AL_ESTILO_PIURA;" +
            @"Integrated Security=True;Application Name=AlEstiloPiurano;Connect Timeout=15;";

        private static readonly string cadena = Leer();

        public static string CadenaConexion
        {
            get { return cadena; }
        }

        public const int IdSucursalActual = 1;

        private static string Leer()
        {
            ConnectionStringSettings cfg =
                ConfigurationManager.ConnectionStrings["AL_ESTILO_PIURA"];

            return cfg != null && !string.IsNullOrWhiteSpace(cfg.ConnectionString)
                ? cfg.ConnectionString
                : Predeterminada;
        }
    }
}
