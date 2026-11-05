using System;
using System.IO;
using System.Text;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Registro de diagnóstico (archivo de texto local).
    // %LOCALAPPDATA%\AlEstiloPiurano\registro.log
    //
    // Sirve para investigar errores recuperables (timeouts de
    // SQL Server, reintentos del monitor de reservas, Cocina)
    // sin interrumpir al usuario con ventanas repetidas.
    // Nunca escribe contraseñas ni datos personales.
    // ==========================================================
    internal static class Registro
    {
        private static readonly object candado = new object();
        private const long TamanoMaximo = 2 * 1024 * 1024;   // 2 MB

        public static string Ruta
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AlEstiloPiurano", "registro.log");
            }
        }

        public static void Info(string origen, string mensaje)
        {
            Escribir("INFO ", origen, mensaje);
        }

        public static void Error(string origen, Exception ex)
        {
            Escribir("ERROR", origen, Describir(ex));
        }

        public static string Describir(Exception ex)
        {
            if (ex == null) return "";
            StringBuilder sb = new StringBuilder();
            Exception actual = ex;
            while (actual != null)
            {
                if (sb.Length > 0) sb.Append(" → ");
                sb.Append(actual.GetType().Name).Append(": ").Append(actual.Message);

                System.Data.SqlClient.SqlException sql = actual as System.Data.SqlClient.SqlException;
                if (sql != null) sb.Append(" [SQL ").Append(sql.Number).Append("]");

                actual = actual.InnerException;
            }
            return sb.ToString().Replace("\r", " ").Replace("\n", " ");
        }

        // true si el error es transitorio (vale la pena reintentar).
        public static bool EsTransitorio(Exception ex)
        {
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                System.Data.SqlClient.SqlException sql = e as System.Data.SqlClient.SqlException;
                if (sql != null)
                {
                    // -2 timeout, 53/258/2 red o inicio de sesión, 1205 interbloqueo,
                    // 701 sin memoria, 8645 espera de memoria, 4060/18456 no son transitorios.
                    switch (sql.Number)
                    {
                        case -2: case 2: case 53: case 258: case 233: case 64: case 10054:
                        case 1205: case 701: case 8645: case 8651: case 40613:
                            return true;
                    }
                }
                if (e is TimeoutException) return true;
            }
            return false;
        }

        private static void Escribir(string nivel, string origen, string mensaje)
        {
            try
            {
                lock (candado)
                {
                    string ruta = Ruta;
                    Directory.CreateDirectory(Path.GetDirectoryName(ruta));

                    FileInfo info = new FileInfo(ruta);
                    if (info.Exists && info.Length > TamanoMaximo)
                    {
                        string anterior = ruta + ".1";
                        if (File.Exists(anterior)) File.Delete(anterior);
                        File.Move(ruta, anterior);
                    }

                    File.AppendAllText(ruta,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + nivel + "  [" +
                        origen + "]  " + mensaje + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // El registro nunca debe detener la aplicación.
            }
        }
    }
}
