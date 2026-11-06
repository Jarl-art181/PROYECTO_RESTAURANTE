using System;
using System.Security.Cryptography;
using System.Text;

namespace PROYECTO_RESTAURANTE
{
    // ==========================================================
    // CONTRASEÑAS
    // ----------------------------------------------------------
    // Formato nuevo (se guarda en la misma columna [contraseña]):
    //     PBKDF2$<iteraciones>$<sal Base64>$<hash Base64>
    //   PBKDF2-HMAC-SHA256, sal aleatoria de 16 bytes, 100 000
    //   iteraciones (Rfc2898DeriveBytes, incluido en .NET 4.7.2).
    //
    // Compatibilidad: se siguen aceptando los hashes SHA-256 en
    // hexadecimal anteriores; tras un inicio de sesión correcto,
    // la contraseña se vuelve a guardar en formato PBKDF2.
    // Nunca se guardan ni se registran contraseñas en texto plano.
    // ==========================================================
    internal static class Contrasenas
    {
        private const string Prefijo = "PBKDF2";
        private const int Iteraciones = 100000;
        private const int BytesSal = 16;
        private const int BytesHash = 32;

        public const int LongitudMinima = 8;

        public static string GenerarHash(string contrasena)
        {
            if (contrasena == null)
                throw new ArgumentNullException("contrasena");

            byte[] sal = new byte[BytesSal];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                rng.GetBytes(sal);

            byte[] hash = Derivar(contrasena, sal, Iteraciones);

            return Prefijo + "$" + Iteraciones + "$" +
                   Convert.ToBase64String(sal) + "$" +
                   Convert.ToBase64String(hash);
        }

        public static bool Verificar(string ingresada, string guardada)
        {
            if (string.IsNullOrEmpty(ingresada) || string.IsNullOrEmpty(guardada))
                return false;

            if (guardada.StartsWith(Prefijo + "$", StringComparison.Ordinal))
            {
                string[] partes = guardada.Split('$');
                int iteraciones;
                if (partes.Length != 4 || !int.TryParse(partes[1], out iteraciones) || iteraciones < 1000)
                    return false;

                try
                {
                    byte[] sal = Convert.FromBase64String(partes[2]);
                    byte[] esperado = Convert.FromBase64String(partes[3]);
                    byte[] calculado = Derivar(ingresada, sal, iteraciones, esperado.Length);
                    return IgualesTiempoConstante(calculado, esperado);
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            if (EsSha256Antiguo(guardada))
            {
                byte[] calculado = Encoding.ASCII.GetBytes(CalcularSha256(ingresada));
                byte[] esperado = Encoding.ASCII.GetBytes(guardada.ToUpperInvariant());
                return IgualesTiempoConstante(calculado, esperado);
            }

            // Formato desconocido: nunca se acepta.
            return false;
        }

        // true si conviene volver a guardar en el formato nuevo.
        public static bool NecesitaActualizar(string guardada)
        {
            return EsSha256Antiguo(guardada);
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

        // Política mínima: 8 caracteres, al menos una letra y un número.
        public static string ValidarPolitica(string contrasena, string usuario)
        {
            if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinima)
                return "La contraseña debe tener al menos " + LongitudMinima + " caracteres.";

            bool letra = false, numero = false;
            foreach (char c in contrasena)
            {
                if (char.IsLetter(c)) letra = true;
                if (char.IsDigit(c)) numero = true;
            }

            if (!letra || !numero)
                return "La contraseña debe combinar letras y números.";

            if (!string.IsNullOrEmpty(usuario) &&
                contrasena.IndexOf(usuario, StringComparison.OrdinalIgnoreCase) >= 0)
                return "La contraseña no debe contener el nombre de usuario.";

            return null;
        }

        // Clave temporal criptográficamente segura (se muestra una sola vez).
        // Sin caracteres ambiguos (0/O, 1/l/I) para dictarla sin errores.
        public static string GenerarTemporal()
        {
            const string letras = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
            const string digitos = "23456789";
            const string todos = letras + digitos;

            char[] clave = new char[10];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < clave.Length; i++)
                {
                    string origen = i == 0 ? letras : (i == 1 ? digitos : todos);
                    clave[i] = origen[Aleatorio(rng, origen.Length)];
                }

                // Mezclar (Fisher-Yates).
                for (int i = clave.Length - 1; i > 0; i--)
                {
                    int j = Aleatorio(rng, i + 1);
                    char t = clave[i]; clave[i] = clave[j]; clave[j] = t;
                }
            }

            return new string(clave);
        }

        private static int Aleatorio(RandomNumberGenerator rng, int maximo)
        {
            // Rechazo para evitar sesgo de módulo.
            byte[] b = new byte[4];
            uint limite = uint.MaxValue - (uint.MaxValue % (uint)maximo);
            uint valor;
            do
            {
                rng.GetBytes(b);
                valor = BitConverter.ToUInt32(b, 0);
            } while (valor >= limite);
            return (int)(valor % (uint)maximo);
        }

        private static byte[] Derivar(string contrasena, byte[] sal, int iteraciones, int bytes = BytesHash)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(
                contrasena, sal, iteraciones, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(bytes);
            }
        }

        private static string CalcularSha256(string texto)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] resultado = sha.ComputeHash(Encoding.UTF8.GetBytes(texto));
                return BitConverter.ToString(resultado).Replace("-", "");
            }
        }

        private static bool IgualesTiempoConstante(byte[] a, byte[] b)
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
