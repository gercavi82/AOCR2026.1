using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapaPresentacion.Models.Validation
{
    /// <summary>
    /// Valida identificación Ecuador: Cédula (10) o RUC (13).
    /// - Cédula (10 dígitos): valida módulo 10.
    /// - RUC Persona Natural (13 dígitos, 3º dígito 0-5): primeros 10 cédula y últimos 3 != "000".
    /// - RUC Sociedad Privada (13 dígitos, 3º dígito 9): módulo 11 (coeficientes 4,3,2,7,6,5,4,3,2).
    /// - RUC Entidad Pública (13 dígitos, 3º dígito 6): módulo 11 (coeficientes 3,2,7,6,5,4,3,2).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class RucCedulaValidationAttribute : ValidationAttribute
    {
        public bool PermitirVacio { get; set; } = false;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var s = value as string;

            if (string.IsNullOrWhiteSpace(s))
            {
                return PermitirVacio
                    ? ValidationResult.Success
                    : new ValidationResult(ErrorMessage ?? "RUC/Cédula es requerido.");
            }

            s = new string(s.Where(char.IsDigit).ToArray());

            if (s.Length == 10)
            {
                return ValidarCedula(s)
                    ? ValidationResult.Success
                    : new ValidationResult(ErrorMessage ?? "Cédula inválida.");
            }

            if (s.Length == 13)
            {
                return ValidarRuc(s)
                    ? ValidationResult.Success
                    : new ValidationResult(ErrorMessage ?? "RUC inválido.");
            }

            return new ValidationResult(ErrorMessage ?? "Debe tener 10 (cédula) o 13 (RUC) dígitos.");
        }

        public static bool EsRucCedulaValido(string identificacion)
        {
            if (string.IsNullOrWhiteSpace(identificacion)) return false;
            var s = new string(identificacion.Where(char.IsDigit).ToArray());

            if (s.Length == 10) return ValidarCedula(s);
            if (s.Length == 13) return ValidarRuc(s);
            return false;
        }

        private static bool ValidarCedula(string ced)
        {
            if (!Regex.IsMatch(ced, @"^\d{10}$")) return false;

            int provincia = int.Parse(ced.Substring(0, 2));
            int tercer = ced[2] - '0';

            // Provincia: 01 a 24 o 30 (extranjeros)
            if ((provincia < 1 || provincia > 24) && provincia != 30) return false;
            if (tercer >= 6) return false; // Persona natural: 0..5

            int suma = 0;
            for (int i = 0; i < 9; i++)
            {
                int d = ced[i] - '0';
                if (i % 2 == 0) // Posiciones impares (0,2,4,6,8)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                suma += d;
            }

            int verif = ced[9] - '0';
            int decena = ((suma + 9) / 10) * 10;
            int dig = decena - suma;
            if (dig == 10) dig = 0;

            return dig == verif;
        }

        private static bool ValidarRuc(string ruc)
        {
            if (!Regex.IsMatch(ruc, @"^\d{13}$")) return false;

            int provincia = int.Parse(ruc.Substring(0, 2));
            if ((provincia < 1 || provincia > 24) && provincia != 30) return false;

            int tercer = ruc[2] - '0';

            // 1) Persona Natural (3º dígito < 6)
            if (tercer < 6)
            {
                if (!ValidarCedula(ruc.Substring(0, 10))) return false;
                return ruc.Substring(10, 3) != "000";
            }

            // 2) Sociedad Privada o Extranjera sin Cédula (3º dígito == 9)
            if (tercer == 9)
            {
                if (ruc.Substring(10, 3) == "000") return false;
                int[] coef = { 4, 3, 2, 7, 6, 5, 4, 3, 2 };
                int suma = 0;
                for (int i = 0; i < 9; i++)
                {
                    suma += (ruc[i] - '0') * coef[i];
                }
                int residuo = suma % 11;
                int digitoVerificador = (residuo == 0) ? 0 : 11 - residuo;
                return digitoVerificador == (ruc[9] - '0');
            }

            // 3) Entidad Pública (3º dígito == 6)
            if (tercer == 6)
            {
                if (ruc.Substring(9, 4) == "0000") return false;
                int[] coef = { 3, 2, 7, 6, 5, 4, 3, 2 };
                int suma = 0;
                for (int i = 0; i < 8; i++)
                {
                    suma += (ruc[i] - '0') * coef[i];
                }
                int residuo = suma % 11;
                int digitoVerificador = (residuo == 0) ? 0 : 11 - residuo;
                return digitoVerificador == (ruc[8] - '0');
            }

            return false;
        }
    }
}
