using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CapaNegocio.Helpers
{
    /// <summary>
    /// Helper de contrasenas con PBKDF2 (compatibilidad legacy SHA256).
    /// Formato persistido: PBKDF2$sha256$iteraciones$saltBase64$hashBase64
    /// </summary>
    public static class PasswordHelper
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100000;
        private const string Prefix = "PBKDF2$sha256$";

        /// <summary>
        /// PBKDF2-SHA256 para .NET 4.6.2 donde Rfc2898DeriveBytes solo soporta SHA-1.
        /// </summary>
        private static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations, int keyLength)
        {
            const int hashLength = 32; // SHA-256 output
            int blocks = (keyLength + hashLength - 1) / hashLength;
            byte[] result = new byte[keyLength];

            using (var hmac = new HMACSHA256(password))
            {
                for (int blockIndex = 1; blockIndex <= blocks; blockIndex++)
                {
                    byte[] intBytes = new byte[4];
                    intBytes[0] = (byte)(blockIndex >> 24);
                    intBytes[1] = (byte)(blockIndex >> 16);
                    intBytes[2] = (byte)(blockIndex >> 8);
                    intBytes[3] = (byte)(blockIndex);

                    byte[] saltPlusInt = new byte[salt.Length + 4];
                    Buffer.BlockCopy(salt, 0, saltPlusInt, 0, salt.Length);
                    Buffer.BlockCopy(intBytes, 0, saltPlusInt, salt.Length, 4);

                    byte[] u = hmac.ComputeHash(saltPlusInt);
                    byte[] block = (byte[])u.Clone();

                    for (int j = 1; j < iterations; j++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int k = 0; k < hashLength; k++)
                            block[k] ^= u[k];
                    }

                    int offset = (blockIndex - 1) * hashLength;
                    int bytesToCopy = Math.Min(hashLength, keyLength - offset);
                    Buffer.BlockCopy(block, 0, result, offset, bytesToCopy);
                }
            }

            return result;
        }

        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("La contrasena no puede estar vacia");
            }

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] key = Pbkdf2Sha256(Encoding.UTF8.GetBytes(password), salt, Iterations, KeySize);

            return string.Format(
                "{0}{1}${2}${3}",
                Prefix,
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(key));
        }

        public static bool VerifyPassword(string password, string hash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            if (EsHashPbkdf2(hash))
            {
                return VerifyPbkdf2(password, hash);
            }

            // Compatibilidad con hashes legacy SHA256.
            return VerifyLegacySha256(password, hash);
        }

        public static bool NeedsRehash(string hash)
        {
            if (!EsHashPbkdf2(hash))
            {
                return true;
            }

            int iterations = ParseIterations(hash);
            return iterations > 0 && iterations < Iterations;
        }

        public static string GenerarPasswordAleatoria(int longitud = 12)
        {
            if (longitud < 8)
            {
                longitud = 8;
            }

            const string mayusculas = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string minusculas = "abcdefghijklmnopqrstuvwxyz";
            const string numeros = "0123456789";
            const string especiales = "!@#$%^&*";
            var todos = mayusculas + minusculas + numeros + especiales;

            var password = new char[longitud];
            password[0] = RandomChar(mayusculas);
            password[1] = RandomChar(minusculas);
            password[2] = RandomChar(numeros);
            password[3] = RandomChar(especiales);

            for (var i = 4; i < longitud; i++)
            {
                password[i] = RandomChar(todos);
            }

            Shuffle(password);
            return new string(password);
        }

        public static (bool esValida, string mensaje) ValidarFortaleza(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "La contrasena no puede estar vacia.");
            }

            if (password.Length < 8)
            {
                return (false, "La contrasena debe tener al menos 8 caracteres.");
            }

            if (password.Length > 128)
            {
                return (false, "La contrasena no puede exceder 128 caracteres.");
            }

            if (!password.Any(char.IsUpper))
            {
                return (false, "La contrasena debe contener al menos una letra mayuscula.");
            }

            if (!password.Any(char.IsLower))
            {
                return (false, "La contrasena debe contener al menos una letra minuscula.");
            }

            if (!password.Any(char.IsDigit))
            {
                return (false, "La contrasena debe contener al menos un numero.");
            }

            if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
            {
                return (false, "La contrasena debe contener al menos un caracter especial.");
            }

            return (true, "Contrasena valida.");
        }

        public static int CalcularNivelFortaleza(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return 0;
            }

            var puntuacion = 0;

            if (password.Length >= 8) puntuacion += 20;
            if (password.Length >= 12) puntuacion += 20;
            if (password.Length >= 16) puntuacion += 10;

            if (password.Any(char.IsUpper)) puntuacion += 15;
            if (password.Any(char.IsLower)) puntuacion += 15;
            if (password.Any(char.IsDigit)) puntuacion += 10;
            if (password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c))) puntuacion += 10;

            return Math.Min(puntuacion, 100);
        }

        public static string GenerarTokenRecuperacion(int longitud = 32)
        {
            if (longitud < 16)
            {
                longitud = 16;
            }

            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var token = new char[longitud];
            for (var i = 0; i < longitud; i++)
            {
                token[i] = RandomChar(caracteres);
            }

            return new string(token);
        }

        private static bool EsHashPbkdf2(string hash)
        {
            return !string.IsNullOrWhiteSpace(hash) &&
                   hash.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool VerifyPbkdf2(string password, string hash)
        {
            try
            {
                var parts = hash.Split('$');
                if (parts.Length != 5)
                {
                    return false;
                }

                var iterations = int.Parse(parts[2]);
                var salt = Convert.FromBase64String(parts[3]);
                var expected = Convert.FromBase64String(parts[4]);

                byte[] actual = Pbkdf2Sha256(Encoding.UTF8.GetBytes(password), salt, iterations, expected.Length);

                return SlowEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        private static bool VerifyLegacySha256(string password, string hash)
        {
            string calculated;
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                calculated = sb.ToString();
            }

            return string.Equals(calculated, hash, StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseIterations(string hash)
        {
            try
            {
                var parts = hash.Split('$');
                if (parts.Length < 3)
                {
                    return 0;
                }

                int it;
                return int.TryParse(parts[2], out it) ? it : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static bool SlowEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        private static char RandomChar(string alphabet)
        {
            if (string.IsNullOrEmpty(alphabet))
            {
                return 'A';
            }

            var index = NextSecureInt(alphabet.Length);
            return alphabet[index];
        }

        private static void Shuffle(char[] chars)
        {
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = NextSecureInt(i + 1);
                var tmp = chars[i];
                chars[i] = chars[j];
                chars[j] = tmp;
            }
        }

        private static int NextSecureInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
            {
                return 0;
            }

            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                var limite = uint.MaxValue - (uint.MaxValue % (uint)maxExclusive);
                uint valor;

                do
                {
                    rng.GetBytes(bytes);
                    valor = BitConverter.ToUInt32(bytes, 0);
                } while (valor >= limite);

                return (int)(valor % (uint)maxExclusive);
            }
        }
    }
}
