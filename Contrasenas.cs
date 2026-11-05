using System;
using System.Security.Cryptography;
using System.Text;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // Verificación de contraseñas.
    // FASE 2A: compatible con el formato actual (SHA-256 en
    // hexadecimal, igual que INICIAR_SESIÓN).
    // FASE 2B: se añadirá PBKDF2 con sal y la migración automática.
    // ==========================================================
    internal static class Contrasenas
    {
        public static bool Verificar(string ingresada, string guardada)
        {
            if (string.IsNullOrEmpty(ingresada) ||
                string.IsNullOrEmpty(guardada))
            {
                return false;
            }

            if (EsSha256Antiguo(guardada))
            {
                return CompararSeguro(
                    CalcularSha256(ingresada),
                    guardada.ToUpperInvariant());
            }

            // Formato desconocido: nunca se acepta.
            return false;
        }

        public static bool EsSha256Antiguo(string guardada)
        {
            if (guardada == null || guardada.Length != 64)
                return false;

            foreach (char c in guardada)
            {
                bool hex = (c >= '0' && c <= '9') ||
                           (c >= 'a' && c <= 'f') ||
                           (c >= 'A' && c <= 'F');
                if (!hex)
                    return false;
            }

            return true;
        }

        private static string CalcularSha256(string texto)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] resultado = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(texto));

                return BitConverter.ToString(resultado)
                    .Replace("-", "");
            }
        }

        // Comparación en tiempo constante.
        private static bool CompararSeguro(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            int diferencia = 0;
            for (int i = 0; i < a.Length; i++)
                diferencia |= a[i] ^ b[i];

            return diferencia == 0;
        }
    }
}
