using System;

namespace CapaPresentacion.Helpers
{
    public static class DashboardInspeccionBadgeHelper
    {
        public static DashboardInspeccionBadgeInfo GetBadgeEstado(string estado, string contexto = null)
        {
            var scope = (contexto ?? string.Empty).Trim().ToUpperInvariant();
            var value = (estado ?? string.Empty).Trim().ToUpperInvariant();

            switch (scope)
            {
                case "GENERAL":
                    return MapearGeneral(value);
                case "INSPECCION":
                    return MapearInspeccion(value);
                case "DOCUMENTO":
                    return MapearDocumento(value);
                case "FIRMA":
                    return MapearFirma(value);
                case "NC":
                    return MapearNc(value);
                default:
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-neutral", string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Trim());
            }
        }

        private static DashboardInspeccionBadgeInfo MapearGeneral(string estado)
        {
            switch (estado)
            {
                case "PENDIENTE":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-gray", "Pendiente");
                case "EN_VERIFICACION":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-blue", "En verificación");
                case "EN_PROCESO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-yellow", "En proceso");
                case "OBSERVADO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-red", "Observado");
                case "FINALIZADO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green", "Finalizado");
                default:
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-neutral", string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Replace("_", " "));
            }
        }

        private static DashboardInspeccionBadgeInfo MapearInspeccion(string estado)
        {
            switch (estado)
            {
                case "ASIGNADA":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-blue", "Asignada");
                case "EN_PROCESO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-yellow", "En proceso");
                case "OBSERVADA":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-red", "Observada");
                case "SUBSANADA":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-orange", "Subsanada");
                case "FINALIZADA":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green", "Finalizada");
                default:
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-neutral", string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Replace("_", " "));
            }
        }

        private static DashboardInspeccionBadgeInfo MapearDocumento(string estado)
        {
            switch (estado)
            {
                case "PENDIENTE":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-gray", "Pendiente");
                case "EN_REVISION":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-blue", "En revisión");
                case "OBSERVADO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-red", "Observado");
                case "APROBADO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green-soft", "Aprobado");
                case "FIRMADO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green", "Firmado");
                default:
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-neutral", string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Replace("_", " "));
            }
        }

        private static DashboardInspeccionBadgeInfo MapearFirma(string estado)
        {
            switch (estado)
            {
                case "PENDIENTE_FIRMA_INSPECTOR":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-orange", "Pendiente inspector");
                case "PENDIENTE_FIRMA_DIRDAC":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green", "Pendiente DIRDAC");
                default:
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-neutral", string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Replace("_", " "));
            }
        }

        private static DashboardInspeccionBadgeInfo MapearNc(string estado)
        {
            switch (estado)
            {
                case "NC_ABIERTA":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-red", "NC abierta");
                case "EN_PROCESO":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-yellow", "En proceso");
                case "CERRADA":
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green", "Cerrada");
                default:
                    return new DashboardInspeccionBadgeInfo("dash-badge dash-badge-neutral", string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Replace("_", " "));
            }
        }

        public static DashboardInspeccionBadgeInfo GetBooleanBadge(bool valor)
        {
            return valor
                ? new DashboardInspeccionBadgeInfo("dash-badge dash-badge-green-soft", "Sí")
                : new DashboardInspeccionBadgeInfo("dash-badge dash-badge-gray", "No");
        }
    }

    public class DashboardInspeccionBadgeInfo
    {
        public DashboardInspeccionBadgeInfo(string cssClass, string text)
        {
            CssClass = cssClass;
            Text = text;
        }

        public string CssClass { get; private set; }
        public string Text { get; private set; }
    }
}