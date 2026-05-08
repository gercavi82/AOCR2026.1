using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace CapaPresentacion.Helpers
{
    public static class PdfFileNameHelper
    {
        private const int MaxFileNameLength = 180;
        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        public static string LimpiarNombreArchivo(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var normalized = texto.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);
            var pendingSeparator = false;

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSeparator && builder.Length > 0 && builder[builder.Length - 1] != '_')
                    {
                        builder.Append('_');
                    }

                    builder.Append(character);
                    pendingSeparator = false;
                    continue;
                }

                if (character == '_' || character == '-' || character == ' ' || character == '.'
                    || char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character)
                    || InvalidFileNameChars.Contains(character))
                {
                    pendingSeparator = builder.Length > 0;
                }
            }

            var cleaned = builder
                .ToString()
                .Normalize(NormalizationForm.FormC)
                .Trim('_');

            while (cleaned.Contains("__"))
            {
                cleaned = cleaned.Replace("__", "_");
            }

            if (cleaned.Length > MaxFileNameLength)
            {
                cleaned = cleaned.Substring(0, MaxFileNameLength).Trim('_');
            }

            return cleaned;
        }

        public static string CombinarSegmentos(params string[] values)
        {
            if (values == null || values.Length == 0)
            {
                return string.Empty;
            }

            var segments = values
                .Select(LimpiarNombreArchivo)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return segments.Count == 0 ? string.Empty : string.Join("_", segments);
        }

        public static string PrimerValorNoVacio(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        public static string CrearNombreListaVerificacionEae(string nombreEae, int codigoInspeccion, DateTime? fecha = null)
        {
            return CrearNombrePdf(
                "LV",
                "EAE",
                "129",
                nombreEae,
                "Inspeccion",
                codigoInspeccion.ToString(CultureInfo.InvariantCulture),
                FormatearFecha(fecha));
        }

        public static string CrearNombreInformeTecnico(string numeroSolicitud, int codigoInspeccion, DateTime? fecha = null, string sufijo = null)
        {
            return CrearNombrePdf(
                "Informe",
                "Tecnico",
                "AOCR",
                numeroSolicitud,
                "Inspeccion",
                codigoInspeccion.ToString(CultureInfo.InvariantCulture),
                FormatearFecha(fecha),
                sufijo);
        }

        public static string CrearNombreCertificadoAocr(string numeroSolicitud, string nombreEae, DateTime? fecha = null)
        {
            return CrearNombrePdf("Certificado", "AOCR", numeroSolicitud, nombreEae, FormatearFecha(fecha));
        }

        public static string CrearNombreReconocimientoAocr(string numeroSolicitud, string nombreEae, DateTime? fecha = null)
        {
            return CrearNombrePdf("Reconocimiento", "AOCR", numeroSolicitud, nombreEae, FormatearFecha(fecha));
        }

        public static string CrearNombreCondicionesLimitaciones(string numeroSolicitud, string nombreEae, DateTime? fecha = null)
        {
            return CrearNombrePdf("Condiciones", "Limitaciones", numeroSolicitud, nombreEae, FormatearFecha(fecha));
        }

        public static string CrearNombreAceptacionDocumental(string numeroSolicitud, string nombreEae, DateTime? fecha = null)
        {
            return CrearNombrePdf("Aceptacion", "Documental", "AOCR", numeroSolicitud, nombreEae, FormatearFecha(fecha));
        }

        public static string CrearNombreOrdenRecaudacion(string numeroOrden, string nombreEae, DateTime? fecha = null)
        {
            return CrearNombrePdf("Orden", "Recaudacion", numeroOrden, nombreEae, FormatearFecha(fecha));
        }

        public static string CrearNombreFactura(string numeroFactura, string nombreEae, DateTime? fecha = null)
        {
            return CrearNombrePdf("Factura", numeroFactura, nombreEae, FormatearFecha(fecha));
        }

        public static void AplicarContentDispositionPdf(HttpResponseBase response, bool descargar, string nombreArchivo)
        {
            if (response == null || string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return;
            }

            var disposition = (descargar ? "attachment" : "inline") + "; filename=\"" + nombreArchivo + "\"; filename*=UTF-8''" + Uri.EscapeDataString(nombreArchivo);
            response.AddHeader("Content-Disposition", disposition);
        }

        private static string CrearNombrePdf(params string[] segments)
        {
            var cleanedSegments = new List<string>();
            if (segments != null)
            {
                foreach (var segment in segments)
                {
                    var cleaned = LimpiarNombreArchivo(segment);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        cleanedSegments.Add(cleaned);
                    }
                }
            }

            var baseName = cleanedSegments.Count == 0
                ? "Documento_PDF"
                : string.Join("_", cleanedSegments);

            if (baseName.Length > MaxFileNameLength)
            {
                baseName = baseName.Substring(0, MaxFileNameLength).Trim('_');
            }

            return baseName + ".pdf";
        }

        private static string FormatearFecha(DateTime? fecha)
        {
            return (fecha ?? DateTime.Now).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }
    }
}