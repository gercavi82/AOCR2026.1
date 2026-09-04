using System;

namespace CapaModelo
{
    /// <summary>
    /// AC-10: Catálogo canónico de estados independientes para Condiciones y Limitaciones (CL).
    /// Segregados de solicitud, inspección, LV, Informe Técnico y AOCR.
    /// </summary>
    public static class AocrEstadoCl
    {
        public const string ClNoGenerada = "CL_NO_GENERADA";
        public const string ClBorrador = "CL_BORRADOR";
        public const string ClPendienteCoordinador = "CL_PENDIENTE_COORDINADOR";
        public const string ClDevueltaInspector = "CL_DEVUELTA_INSPECTOR";
        public const string ClPendienteDircav = "CL_PENDIENTE_DIRCAV";
        public const string ClDevueltaCoordinador = "CL_DEVUELTA_COORDINADOR";
        public const string ClPendienteFirmaDircav = "CL_PENDIENTE_FIRMA_DIRCAV";
        public const string ClFirmadaDircav = "CL_FIRMADA_DIRCAV";
        public const string ClAnulada = "CL_ANULADA";
        public const string ClReemplazada = "CL_REEMPLAZADA";

        public static bool EsEstadoValido(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;
            var e = estado.Trim().ToUpperInvariant();
            return e == ClNoGenerada
                || e == ClBorrador
                || e == ClPendienteCoordinador
                || e == ClDevueltaInspector
                || e == ClPendienteDircav
                || e == ClDevueltaCoordinador
                || e == ClPendienteFirmaDircav
                || e == ClFirmadaDircav
                || e == ClAnulada
                || e == ClReemplazada;
        }

        public static string ObtenerEtiqueta(string estado)
        {
            switch ((estado ?? string.Empty).Trim().ToUpperInvariant())
            {
                case ClNoGenerada: return "No Generada";
                case ClBorrador: return "Borrador Inspector";
                case ClPendienteCoordinador: return "Pendiente Revisión Coordinación";
                case ClDevueltaInspector: return "Devuelta al Inspector";
                case ClPendienteDircav: return "Pendiente Revisión DIRCAV";
                case ClDevueltaCoordinador: return "Devuelta a Coordinación";
                case ClPendienteFirmaDircav: return "Pendiente Firma DIRCAV";
                case ClFirmadaDircav: return "Firmada por DIRCAV";
                case ClAnulada: return "Anulada";
                case ClReemplazada: return "Reemplazada";
                default: return "No Generada";
            }
        }

        public static string ObtenerBadgeCss(string estado)
        {
            switch ((estado ?? string.Empty).Trim().ToUpperInvariant())
            {
                case ClFirmadaDircav: return "badge badge-success";
                case ClPendienteCoordinador:
                case ClPendienteDircav:
                case ClPendienteFirmaDircav: return "badge badge-warning";
                case ClDevueltaInspector:
                case ClDevueltaCoordinador: return "badge badge-danger";
                case ClBorrador: return "badge badge-info";
                default: return "badge badge-secondary";
            }
        }
    }
}
