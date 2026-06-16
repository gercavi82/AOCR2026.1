using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaPresentacion.Helpers
{
    public static class RevisionDocumentalDisplayHelper
    {
        private sealed class TipoDocumentoOrdenDef
        {
            public TipoDocumentoOrdenDef(string codigo, string nombre, int prioridad, params string[] equivalencias)
            {
                Codigo = codigo;
                Nombre = nombre;
                Prioridad = prioridad;
                Equivalencias = equivalencias ?? new string[0];
            }

            public string Codigo { get; private set; }
            public string Nombre { get; private set; }
            public int Prioridad { get; private set; }
            public string[] Equivalencias { get; private set; }
        }

        private static readonly TipoDocumentoOrdenDef[] TiposDocumentoOrdenados = new[]
        {
            new TipoDocumentoOrdenDef("COMPROBANTE_PAGO", "Comprobante de pago", 1,
                "COMPROBANTE_PAGO", "COMPROBANTE_DE_PAGO"),
            new TipoDocumentoOrdenDef("SOLICITUD_INSPECCION_EXT", "Solicitud de inspecciones generada", 98,
                "SOLICITUD_INSPECCION_EXT", "SOLICITUD_DE_INSPECCIONES", "SOLICITUD_INSPECCIONES"),
            new TipoDocumentoOrdenDef("SOLICITUD_INSPECCIONES_FIRMADA", "Solicitud de inspecciones firmada", 2,
                "SOLICITUD_INSPECCIONES_FIRMADA", "SOLICITUD_DE_INSPECCIONES_FIRMADA"),
            new TipoDocumentoOrdenDef("COPIA_AOC_VALIDA", "Copia AOC válida", 3,
                "COPIA_AOC_VALIDA", "COPIA_AOC", "AOC", "AOC_VALIDA"),
            new TipoDocumentoOrdenDef("OPSPECS_ESPECIFICACIONES_OPERACIONALES", "OpSpecs / Especificaciones operacionales", 4,
                "OPSPECS_ESPECIFICACIONES_OPERACIONALES", "OPSPECS", "OP_SPECS", "ESPECIFICACIONES_OPERACIONALES"),
            new TipoDocumentoOrdenDef("MANUAL_OPERACIONES", "Manual de operaciones", 5,
                "MANUAL_OPERACIONES", "MANUAL_DE_OPERACIONES"),
            new TipoDocumentoOrdenDef("PERMISO_OPERACION_CNAC", "Permiso de operación CNAC", 6,
                "PERMISO_OPERACION_CNAC", "PERMISO_OPERACION"),
            new TipoDocumentoOrdenDef("COPIA_CERTIFICADA_PODER_REPRESENTANTE_ECUADOR", "Copia certificada del poder del representante en Ecuador", 7,
                "COPIA_CERTIFICADA_PODER_REPRESENTANTE_ECUADOR", "PODER_REPRESENTANTE_ECUADOR", "COPIA_CERTIFICADA_PODER_REPRESENTANTE", "PODER_REPRESENTANTE"),
            new TipoDocumentoOrdenDef("CERTIFICADO_AERONAVEGABILIDAD", "Certificado de aeronavegabilidad", 8,
                "CERTIFICADO_AERONAVEGABILIDAD"),
            new TipoDocumentoOrdenDef("CERTIFICADO_RUIDO_AERONAVES_EAE", "Certificado de ruido aeronaves EAE", 9,
                "CERTIFICADO_RUIDO_AERONAVES_EAE", "CERTIFICADO_RUIDO", "CERTIFICADO_RUIDO_AERONAVES")
        };

        public static bool ShouldIncludeInRevisionDocumental(string tipoDocumento)
        {
            var canonical = GetCanonicalDocumentType(tipoDocumento);
            return !EsDocumentoSoloConsulta(canonical);
        }

        public static bool EsDocumentoSoloConsulta(string tipoDocumento)
        {
            var canonical = GetCanonicalDocumentType(tipoDocumento);
            var normalized = NormalizeDocumentTypeKey(tipoDocumento);
            switch (canonical)
            {
                case "SOLICITUD_INSPECCION_EXT":
                case "SOLICITUD_INSPECCIONES_FIRMADA":
                case "COMPROBANTE_PAGO":
                case "ORDEN_RECAUDACION":
                case "FACTURA":
                case "COMPROBANTE_AS400":
                case "DOCUMENTO_GENERADO_SISTEMA":
                case "AOCR_GENERADO":
                case "AOCR_FIRMADO":
                case "CONDICIONES_LIMITACIONES":
                case "CONSTANCIA":
                    return true;
                default:
                    break;
            }

            switch (normalized)
            {
                case "SOLICITUD_INSPECCION_EXT":
                case "SOLICITUD_DE_INSPECCIONES":
                case "SOLICITUD_INSPECCIONES":
                case "SOLICITUD_INSPECCION_FIRMADA":
                case "SOLICITUD_INSPECCIONES_FIRMADA":
                case "SOLICITUD_DE_INSPECCIONES_FIRMADA":
                case "ORDEN_RECAUDACION":
                case "FACTURA":
                case "COMPROBANTE_AS400":
                case "COMPROBANTE_PAGO":
                case "COMPROBANTE_DE_PAGO":
                case "DOCUMENTO_GENERADO_SISTEMA":
                case "AOCR_GENERADO":
                case "AOCR_FIRMADO":
                case "CONDICIONES_LIMITACIONES":
                case "CONSTANCIA":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsAcceptedState(string estado)
        {
            var normalized = Normalize(estado);
            return normalized == "ACEPTADO" || normalized == "APROBADO" || normalized == "REVISADO";
        }

        public static bool IsReturnedState(string estado)
        {
            var normalized = Normalize(estado);
            return normalized == "DEVUELTO" || normalized == "RECHAZADO";
        }

        public static bool IsModificationRequestedState(string estado)
        {
            var normalized = Normalize(estado);
            return normalized == "OBSERVADO" || normalized == "MODIFICACION_SOLICITADA";
        }

        public static string GetVisibleStateLabel(string estado)
        {
            if (IsAcceptedState(estado))
            {
                return "ACEPTADO";
            }

            if (IsModificationRequestedState(estado))
            {
                return "MODIFICACION SOLICITADA";
            }

            if (IsReturnedState(estado))
            {
                return "RECHAZADO";
            }

            var normalized = Normalize(estado);
            return string.IsNullOrWhiteSpace(normalized) ? "PENDIENTE" : normalized;
        }

        public static string GetBadgeClass(string estado, bool highlightPendingAsDanger = false)
        {
            if (IsAcceptedState(estado))
            {
                return "badge bg-success";
            }

            if (IsModificationRequestedState(estado))
            {
                return "badge bg-warning text-dark";
            }

            if (IsReturnedState(estado))
            {
                return "badge bg-danger";
            }

            var normalized = Normalize(estado);
            if (normalized == "PENDIENTE")
            {
                return highlightPendingAsDanger ? "badge bg-danger" : "badge bg-secondary";
            }

            return "badge bg-secondary";
        }

        public static string GetCanonicalDocumentType(string tipoDocumento)
        {
            var normalized = NormalizeDocumentTypeKey(tipoDocumento);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "OTRO";
            }

            foreach (var def in TiposDocumentoOrdenados)
            {
                if (string.Equals(def.Codigo, normalized, StringComparison.OrdinalIgnoreCase)
                    || def.Equivalencias.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    return def.Codigo;
                }
            }

            return "OTRO";
        }

        public static string GetDocumentGroupKey(string tipoDocumento)
        {
            var canonical = GetCanonicalDocumentType(tipoDocumento);
            if (!string.Equals(canonical, "OTRO", StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }

            var normalized = NormalizeDocumentTypeKey(tipoDocumento);
            return string.IsNullOrWhiteSpace(normalized) ? "OTRO" : "OTRO_" + normalized;
        }

        public static int GetDocumentPriority(string tipoDocumento)
        {
            var canonical = GetCanonicalDocumentType(tipoDocumento);
            var def = TiposDocumentoOrdenados.FirstOrDefault(item => string.Equals(item.Codigo, canonical, StringComparison.OrdinalIgnoreCase));
            return def != null ? def.Prioridad : 99;
        }

        public static string GetDocumentDisplayName(string tipoDocumento)
        {
            var canonical = GetCanonicalDocumentType(tipoDocumento);
            var def = TiposDocumentoOrdenados.FirstOrDefault(item => string.Equals(item.Codigo, canonical, StringComparison.OrdinalIgnoreCase));
            if (def != null)
            {
                return def.Nombre;
            }

            return string.IsNullOrWhiteSpace(tipoDocumento) ? "Otro" : tipoDocumento.Trim();
        }

        private static string Normalize(string estado)
        {
            return (estado ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizeDocumentTypeKey(string value)
        {
            var normalized = Normalize(value)
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace("Ñ", "N")
                .Replace("/", "_")
                .Replace("-", "_")
                .Replace(" ", "_");

            while (normalized.Contains("__"))
            {
                normalized = normalized.Replace("__", "_");
            }

            return normalized.Trim('_');
        }
    }
}
