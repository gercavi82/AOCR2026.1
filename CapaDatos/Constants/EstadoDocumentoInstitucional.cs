using System;
using System.Collections.Generic;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Catálogo institucional de estados documentales AOCR y equivalencias legacy.
    /// </summary>
    public static class EstadoDocumentoInstitucional
    {
        public const string PendienteRevision = "PENDIENTE_REVISION";
        public const string Aceptado = "ACEPTADO";
        public const string Observado = "OBSERVADO";
        public const string DevueltoInspector = "DEVUELTO_INSPECTOR";
        public const string PendienteSubsanacion = "PENDIENTE_SUBSANACION";
        public const string SubsanadoRt = "SUBSANADO_RT";
        public const string EnRevisionInspector = "EN_REVISION_INSPECTOR";
        public const string Rechazado = "RECHAZADO";
        public const string Bloqueado = "BLOQUEADO";
        public const string VersionAnterior = "VERSION_ANTERIOR";
        public const string PendienteRevisionSubsanacion = "PENDIENTE_REVISION_SUBSANACION";

        public const string DevueltoPorRolInspector = "INSPECTOR";

        private static readonly HashSet<string> EstadosSubsanablesRt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DevueltoInspector,
            Observado,
            PendienteSubsanacion,
            PendienteRevisionSubsanacion,
            "RECHAZADO",
            "SUBSANACION"
        };

        private static readonly HashSet<string> EstadosRevisablesInspector = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PendienteRevision,
            PendienteRevisionSubsanacion,
            SubsanadoRt,
            EnRevisionInspector
        };

        public static string Normalizar(string estado)
        {
            var actual = (estado ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(actual))
            {
                return PendienteRevision;
            }

            switch (actual)
            {
                case "PENDIENTE":
                case "PENDIENTE_REVISION":
                case "PENDIENTE_REVISION_SUBSANACION":
                    return actual == "PENDIENTE_REVISION_SUBSANACION"
                        ? PendienteRevisionSubsanacion
                        : PendienteRevision;
                case "APROBADO":
                case "VALIDADO":
                case "ACEPTADO":
                    return Aceptado;
                case "OBSERVADO":
                    return Observado;
                case "DEVUELTO":
                case "DEVUELTO_INSPECTOR":
                    return DevueltoInspector;
                case "PENDIENTE_SUBSANACION":
                case "SUBSANACION":
                    return PendienteSubsanacion;
                case "SUBSANADO_RT":
                case "SUBSANADO":
                case "DOCUMENTACION_SUBSANADA":
                case "DOCUMENTOS_SUBSANADOS":
                    return SubsanadoRt;
                case "EN_REVISION_INSPECTOR":
                case "PENDIENTE_REVISION_INSPECTOR":
                    return EnRevisionInspector;
                case "RECHAZADO":
                    return Rechazado;
                case "BLOQUEADO":
                    return Bloqueado;
                case "VERSION_ANTERIOR":
                case "SUPERSEDED":
                    return VersionAnterior;
                default:
                    return actual;
            }
        }

        public static string NormalizarDecisionRevision(string decision)
        {
            var actual = (decision ?? string.Empty).Trim().ToUpperInvariant();
            switch (actual)
            {
                case "ACEPTADO":
                case "APROBADO":
                    return "ACEPTADO";
                case "DEVUELTO":
                case "RECHAZADO":
                    return "DEVUELTO";
                case "OBSERVADO":
                    return "OBSERVADO";
                case "PENDIENTE":
                case "PENDIENTE_REVISION":
                case "":
                    return "PENDIENTE";
                default:
                    return actual;
            }
        }

        public static bool EsEstadoSubsanableRt(string estadoDocumento)
        {
            var normalizado = Normalizar(estadoDocumento);
            return EstadosSubsanablesRt.Contains(normalizado);
        }

        public static bool EsEstadoRevisablePorInspector(string estadoDocumento)
        {
            var normalizado = Normalizar(estadoDocumento);
            return EstadosRevisablesInspector.Contains(normalizado);
        }

        public static bool DecisionIndicaDevolucionInspector(string decisionRevision)
        {
            var decision = NormalizarDecisionRevision(decisionRevision);
            return decision == "DEVUELTO" || decision == "OBSERVADO";
        }

        public static string ResolverEstadoTrasDecisionInspector(string decision)
        {
            var decisionNorm = NormalizarDecisionRevision(decision);
            if (decisionNorm == "ACEPTADO")
            {
                return Aceptado;
            }

            if (decisionNorm == "OBSERVADO")
            {
                return Observado;
            }

            if (decisionNorm == "DEVUELTO")
            {
                return DevueltoInspector;
            }

            return PendienteRevision;
        }

        public static string ResolverEstadoTrasSubsanacionRt()
        {
            return PendienteRevisionSubsanacion;
        }

        public static string ResolverEstadoVersionAnterior()
        {
            return VersionAnterior;
        }
    }
}
