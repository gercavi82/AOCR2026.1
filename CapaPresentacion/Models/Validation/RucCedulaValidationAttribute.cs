using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapaPresentacion.Models.Validation
{
    /// <summary>
    /// Valida identificación Ecuador: Cédula (10) o RUC (13).
    /// - Cédula: valida dígito verificador (módulo 10).
    /// - RUC: valida que los primeros 10 cumplan cédula y 3 últimos no sean "000".
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
                return ValidarRucPersonaNatural(s)
                    ? ValidationResult.Success
                    : new ValidationResult(ErrorMessage ?? "RUC inválido.");
            }

            return new ValidationResult(ErrorMessage ?? "Debe tener 10 (cédula) o 13 (RUC) dígitos.");
        }

        private static bool ValidarCedula(string ced)
        {
            if (!Regex.IsMatch(ced, @"^\d{10}$")) return false;

            int provincia = int.Parse(ced.Substring(0, 2));
            int tercer = ced[2] - '0';
            if (provincia < 1 || provincia > 24) return false;
            if (tercer >= 6) return false; // persona natural: 0..5

            int suma = 0;
            for (int i = 0; i < 9; i++)
            {
                int d = ced[i] - '0';
                if (i % 2 == 0) // posiciones 0,2,4,6,8
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

        private static bool ValidarRucPersonaNatural(string ruc)
        {
            // RUC persona natural: primeros 10 = cédula válida, últimos 3 != 000
            if (!Regex.IsMatch(ruc, @"^\d{13}$")) return false;
            if (!ValidarCedula(ruc.Substring(0, 10))) return false;
            string ult3 = ruc.Substring(10, 3);
            return ult3 != "000";
        }
    }
}
