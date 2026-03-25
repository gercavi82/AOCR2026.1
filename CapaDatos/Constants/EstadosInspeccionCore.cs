using System;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Estados core de negocio para el flujo BPMN de inspeccion.
    /// No reemplazan los estados persistidos actuales; operan como capa logica de compatibilidad.
    /// </summary>
    public static class EstadosInspeccionCore
    {
        public const string BORRADOR = "BORRADOR";
        public const string EN_REVISION = "EN_REVISION";
        public const string CON_NC = "CON_NC";
        public const string SUBSANACION = "SUBSANACION";
        public const string REVALIDACION = "REVALIDACION";
        public const string APROBADA = "APROBADA";
        public const string RECHAZADA = "RECHAZADA";
        public const string CERRADA = "CERRADA";

        public static string ObtenerEstadoCore(string estadoPersistido, string resultadoPersistido = null)
        {
            var estado = EstadosInspeccion.NormalizarEstado(estadoPersistido);
            var resultado = (resultadoPersistido ?? string.Empty).Trim().ToUpperInvariant();

            switch (estado)
            {
                case EstadosInspeccion.SOLICITUD_INSPECCION_CREADA:
                case EstadosInspeccion.VERIFICACION_SOLICITUD:
                case EstadosInspeccion.ACEPTADA:
                case EstadosInspeccion.VIATICOS_REQUERIDOS:
                case EstadosInspeccion.PAGO_VALIDADO:
                    return BORRADOR;

                case EstadosInspeccion.EN_INSPECCION:
                case EstadosInspeccion.INFORME_ELABORADO:
                    return EN_REVISION;

                case EstadosInspeccion.OBSERVADA:
                case EstadosInspeccion.OBSERVACION_DOCUMENTAL:
                    return SUBSANACION;

                case EstadosInspeccion.SUBSANADA:
                    return REVALIDACION;

                case EstadosInspeccion.RESULTADO_SATISFACTORIO:
                    return APROBADA;

                case EstadosInspeccion.RESULTADO_NO_SATISFACTORIO:
                    return CON_NC;

                case EstadosInspeccion.CERRADA:
                    if (resultado == "RECHAZADO" || resultado == "NO_SATISFACTORIO" || resultado == EstadosInspeccion.RESULTADO_NO_SATISFACTORIO)
                    {
                        return RECHAZADA;
                    }

                    return CERRADA;

                default:
                    return BORRADOR;
            }
        }

        public static string ObtenerEstadoPersistidoDestino(string estadoCoreDestino, string estadoPersistidoActual)
        {
            var core = (estadoCoreDestino ?? string.Empty).Trim().ToUpperInvariant();
            var actual = EstadosInspeccion.NormalizarEstado(estadoPersistidoActual);

            switch (core)
            {
                case BORRADOR:
                    return actual == EstadosInspeccion.CERRADA ? EstadosInspeccion.ACEPTADA : actual;
                case EN_REVISION:
                    return EstadosInspeccion.EN_INSPECCION;
                case CON_NC:
                    return actual == EstadosInspeccion.INFORME_ELABORADO
                        ? EstadosInspeccion.RESULTADO_NO_SATISFACTORIO
                        : EstadosInspeccion.OBSERVADA;
                case SUBSANACION:
                    return EstadosInspeccion.OBSERVADA;
                case REVALIDACION:
                    return EstadosInspeccion.SUBSANADA;
                case APROBADA:
                    return EstadosInspeccion.RESULTADO_SATISFACTORIO;
                case RECHAZADA:
                    return EstadosInspeccion.RESULTADO_NO_SATISFACTORIO;
                case CERRADA:
                    return EstadosInspeccion.CERRADA;
                default:
                    return actual;
            }
        }
    }
}