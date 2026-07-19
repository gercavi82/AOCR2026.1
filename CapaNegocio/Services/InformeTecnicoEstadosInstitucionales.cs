using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CapaNegocio.Services
{
    public static class InformeTecnicoEstadosInstitucionales
    {
        public static readonly HashSet<string> EnviadosDireccion =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ENVIADO_A_DIRDAC",
                "ENVIADO_A_DIRECCION",
                "PENDIENTE_REVISION_DIRDAC",
                "PENDIENTE_REVISION_DIRECCION",
                "PENDIENTE_REVISION_INSTITUCIONAL",
                "PENDIENTE_FIRMA_DIRECCION",
                "PENDIENTE_FIRMA_INSTITUCIONAL",
                "PENDIENTE_REVISION_INFORME_DCAV",
                "PENDIENTE_REVISION_INFORME_DIRDAC"
            };

        public static readonly HashSet<string> PendientesRevisionDireccion =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ENVIADO_A_DIRDAC",
                "ENVIADO_A_DIRECCION",
                "PENDIENTE_REVISION_DIRDAC",
                "PENDIENTE_REVISION_DIRECCION",
                "PENDIENTE_REVISION_INSTITUCIONAL",
                "PENDIENTE_FIRMA_DIRECCION",
                "PENDIENTE_FIRMA_INSTITUCIONAL",
                "FIRMADO_INSPECTOR",
                "FIRMADO_POR_INSPECTOR",
                "INFORME_TECNICO_FIRMADO_INSPECTOR"
                ,"PENDIENTE_REVISION_INFORME_DCAV"
                ,"PENDIENTE_REVISION_INFORME_DIRDAC"
            };

        public static readonly HashSet<string> Cerrados =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "APROBADO_DIRECCION",
                "APROBADO_DIRDAC",
                "FIRMADO_DIRECCION",
                "FIRMADO_DIRDAC",
                "FIRMADO_FINAL",
                "DEVUELTO",
                "DEVUELTO_DIRECCION",
                "RECHAZADO",
                "RECHAZADO_DIRDAC",
                "FINALIZADO",
                "CERRADO",
                "INFORME_TECNICO_APROBADO_DIRDAC"
            };

        public static bool PuedeRevisarDireccion(string estado)
        {
            var token = NormalizarToken(estado);
            return !string.IsNullOrWhiteSpace(token)
                && !Cerrados.Contains(token)
                && PendientesRevisionDireccion.Contains(token);
        }

        public static bool FueEnviadoDireccion(string estado)
        {
            var token = NormalizarToken(estado);
            return !string.IsNullOrWhiteSpace(token)
                && EnviadosDireccion.Contains(token);
        }

        public static string NormalizarToken(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return string.Empty;
            }

            var texto = estado.Trim().Normalize(NormalizationForm.FormD);
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
