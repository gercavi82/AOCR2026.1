using System;
using System.Globalization;
using System.Text;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class InformeTecnicoEstadoService
    {
        public bool EstaAprobadoPorDireccion(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            if (informe.FirmadoDirdac)
            {
                return true;
            }

            if (informe.FechaFirma2.HasValue && !string.IsNullOrWhiteSpace(informe.UsuarioFirma2))
            {
                return true;
            }

            return EstaAprobadoPorDireccion(informe.EstadoInforme);
        }

        public bool EstaAprobadoPorDireccion(string estado)
        {
            var token = NormalizarToken(estado);
            switch (token)
            {
                case "APROBADO_DIRECCION":
                case "APROBADO_DIRDAC":
                case "FIRMADO_DIRECCION":
                case "FIRMADO_FINAL":
                case "INFORME_TECNICO_APROBADO":
                case "INFORME_TECNICO_APROBADO_DIRECCION":
                    return true;
                default:
                    return false;
            }
        }

        private static string NormalizarToken(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            var texto = valor.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(texto.Length);
            foreach (var c in texto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .ToUpperInvariant()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("/", "_");
        }
    }
}
