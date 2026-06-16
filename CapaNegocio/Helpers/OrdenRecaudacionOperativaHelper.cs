using System;
using CapaDatos.Constants;

namespace CapaNegocio.Helpers
{
    /// <summary>
    /// Reglas operativas de la Orden de Recaudación tras aprobación financiera y durante el flujo AOCR.
    /// </summary>
    public static class OrdenRecaudacionOperativaHelper
    {
        public const string MensajeOrdenCerradaPostAprobacion =
            "Esta Orden de Recaudación fue aprobada por Financiero y quedó cerrada para el proceso AOCR actual. La Solicitud AOCR ya se encuentra habilitada para continuar el trámite.";

        public const string MensajeBloqueoEdicion =
            "Esta orden fue aprobada por Financiero y ya no puede modificarse.";

        public const string MensajeBloqueoComprobante =
            "No puede subir ni reemplazar comprobantes en una orden aprobada y cerrada.";

        public const string MensajeBloqueoNuevaOrdenProcesoActivo =
            "No puede generar una nueva Orden de Recaudación para esta compañía porque existe un proceso AOCR activo. Debe finalizar, cerrar o anular el proceso actual antes de iniciar una nueva orden.";

        public static bool EsOrdenCerradaPostAprobacionFinanciera(string estadoOrden)
        {
            return EstadoOrden.EsOrdenCerradaPostAprobacionFinanciera(estadoOrden);
        }

        public static bool PermiteEditarOrden(string estadoOrden)
        {
            return EstadoOrden.PermiteEditar(estadoOrden) && !EsOrdenCerradaPostAprobacionFinanciera(estadoOrden);
        }

        public static bool PermiteSubirComprobante(string estadoOrden)
        {
            if (EsOrdenCerradaPostAprobacionFinanciera(estadoOrden))
            {
                return false;
            }

            var actual = EstadoOrden.NormalizarEstado(estadoOrden);
            return string.Equals(actual, EstadoOrden.Pendiente, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Generada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PermiteAnularOrden(string estadoOrden)
        {
            if (EsOrdenCerradaPostAprobacionFinanciera(estadoOrden))
            {
                return false;
            }

            var actual = EstadoOrden.NormalizarEstado(estadoOrden);
            return !string.Equals(actual, EstadoOrden.Anulada, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PermiteReutilizarOrden(string estadoOrden)
        {
            return !EsOrdenCerradaPostAprobacionFinanciera(estadoOrden)
                && !string.Equals(EstadoOrden.NormalizarEstado(estadoOrden), EstadoOrden.Anulada, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolverEstadoOrdenPostAprobacion()
        {
            return EstadoOrden.ResolverEstadoPersistenciaPostAprobacionFinanciera();
        }

        public static string ResolverEstadoPagoPostAprobacion()
        {
            return EstadoPago.ResolverEstadoPersistenciaPostAprobacionFinanciera();
        }
    }
}
