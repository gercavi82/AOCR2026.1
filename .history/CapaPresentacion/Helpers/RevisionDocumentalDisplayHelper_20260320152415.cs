using System;

namespace CapaPresentacion.Helpers
{
    public static class RevisionDocumentalDisplayHelper
    {
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

        public static string GetVisibleStateLabel(string estado)
        {
            if (IsAcceptedState(estado))
            {
                return "ACEPTADO";
            }

            if (IsReturnedState(estado))
            {
                return "DEVUELTO";
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

        private static string Normalize(string estado)
        {
            return (estado ?? string.Empty).Trim().ToUpperInvariant();
        }
    }
}