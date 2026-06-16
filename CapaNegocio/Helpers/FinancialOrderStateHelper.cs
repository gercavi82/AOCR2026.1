using System;
using CapaDatos.Constants;

namespace CapaNegocio.Helpers
{
    public static class FinancialOrderStateHelper
    {
        public const string PendientesFinanciero = "PENDIENTES_FINANCIERO";

        public static string NormalizarEstadoDashboard(string estado)
        {
            var actual = EstadoOrden.NormalizarEstado(estado);
            if (actual == EstadoOrden.Pendiente || actual == EstadoOrden.Generada)
            {
                return EstadoOrden.Generada;
            }

            return actual;
        }

        public static string NormalizarFiltro(string estado)
        {
            var actual = (estado ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(actual))
            {
                return "TODAS";
            }

            switch (actual)
            {
                case "TODAS":
                    return "TODAS";
                case "PENDIENTES_FINANCIERO":
                case "PENDIENTES":
                case "PAGOS_PENDIENTES":
                    return PendientesFinanciero;
                case "PROCESADA":
                case "EN_REVISION":
                case "EN_REVISION_FINANCIERA":
                case "PAGOS_CARGADOS":
                    return EstadoOrden.EnRevisionFinanciera;
                case "PENDIENTE":
                case "GENERADA":
                    return EstadoOrden.Generada;
                default:
                    return NormalizarEstadoDashboard(actual);
            }
        }

        public static string ResolverEstadoOperativo(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada)
        {
            if (EsObservada(estadoOrden, estadoPago))
            {
                return EstadoOrden.Devuelta;
            }

            if (EsAprobadaOFacturada(estadoOrden, estadoPago, tieneFacturaRegistrada))
            {
                return EstadoOrden.Facturada;
            }

            return NormalizarEstadoDashboard(estadoOrden);
        }

        public static bool CoincideFiltro(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada, string estadoFiltro)
        {
            return CoincideFiltro(estadoOrden, estadoPago, tieneFacturaRegistrada, estadoFiltro, false);
        }

        public static bool CoincideFiltro(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada, string estadoFiltro, bool tieneComprobanteValido)
        {
            var filtro = NormalizarFiltro(estadoFiltro);
            if (string.Equals(filtro, "TODAS", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(filtro, PendientesFinanciero, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(filtro, EstadoOrden.EnRevisionFinanciera, StringComparison.OrdinalIgnoreCase))
            {
                return EsPendienteGestion(estadoOrden, estadoPago, tieneFacturaRegistrada, tieneComprobanteValido);
            }

            if (string.Equals(filtro, EstadoOrden.Facturada, StringComparison.OrdinalIgnoreCase))
            {
                return EsAprobadaOFacturada(estadoOrden, estadoPago, tieneFacturaRegistrada);
            }

            if (string.Equals(filtro, EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase))
            {
                return EsObservada(estadoOrden, estadoPago);
            }

            return string.Equals(
                ResolverEstadoOperativo(estadoOrden, estadoPago, tieneFacturaRegistrada),
                filtro,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Orden generada/enviada por RT aún sin comprobante de depósito o transferencia.
        /// </summary>
        public static bool EsOrdenRtPendienteComprobante(string estadoOrden)
        {
            var actual = NormalizarEstadoDashboard(estadoOrden);
            return string.Equals(actual, EstadoOrden.Generada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Pendiente, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Enviada, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Oculta de bandeja/contador financiero órdenes RT sin comprobante o en revisión sin respaldo válido.
        /// </summary>
        public static bool DebeOcultarDeBandejaFinanciera(string estadoOrden, bool tieneComprobanteValido)
        {
            if (EsOrdenRtPendienteComprobante(estadoOrden))
            {
                return true;
            }

            var actual = NormalizarEstadoDashboard(estadoOrden);
            return string.Equals(actual, EstadoOrden.EnRevisionFinanciera, StringComparison.OrdinalIgnoreCase)
                && !tieneComprobanteValido;
        }

        public static bool EsPendienteGestion(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada)
        {
            return EsPendienteGestion(estadoOrden, estadoPago, tieneFacturaRegistrada, false);
        }

        /// <summary>
        /// Pago pendiente de revisión financiera: solo con comprobante cargado y estado EN_REVISION_FINANCIERA.
        /// </summary>
        public static bool EsPendienteGestion(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada, bool tieneComprobanteValido)
        {
            if (!tieneComprobanteValido ||
                EsAprobadaOFacturada(estadoOrden, estadoPago, tieneFacturaRegistrada) ||
                EsObservada(estadoOrden, estadoPago))
            {
                return false;
            }

            var actual = NormalizarEstadoDashboard(estadoOrden);
            return string.Equals(actual, EstadoOrden.EnRevisionFinanciera, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsObservada(string estadoOrden, string estadoPago)
        {
            return string.Equals(NormalizarEstadoDashboard(estadoOrden), EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizarPago(estadoPago), EstadoPago.Rechazado, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsAprobadaOFacturada(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada)
        {
            if (EsObservada(estadoOrden, estadoPago))
            {
                return false;
            }

            var actual = NormalizarEstadoDashboard(estadoOrden);
            return tieneFacturaRegistrada
                || string.Equals(actual, EstadoOrden.Facturada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.OrdenCerradaPorSolicitud, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.OrdenInactiva, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Pagada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Completada, StringComparison.OrdinalIgnoreCase)
                || EstadoPago.EsPagoAprobadoFinancieramente(estadoPago);
        }

        public static bool TieneFacturaRegistrada(string numeroFactura, string fr3Estado, string fr3Numero)
        {
            return !string.IsNullOrWhiteSpace(numeroFactura)
                || !string.IsNullOrWhiteSpace(fr3Estado)
                || !string.IsNullOrWhiteSpace(fr3Numero);
        }

        public static bool EsHistorialFinanciero(string estadoOrden, string estadoPago, bool tieneFacturaRegistrada)
        {
            var actual = NormalizarEstadoDashboard(estadoOrden);
            return EsAprobadaOFacturada(estadoOrden, estadoPago, tieneFacturaRegistrada)
                || EsObservada(estadoOrden, estadoPago)
                || string.Equals(actual, EstadoOrden.EnRevisionFinanciera, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Anulada, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizarPago(string estadoPago)
        {
            return (estadoPago ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
        }
    }
}
