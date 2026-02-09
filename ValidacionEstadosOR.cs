using System;

namespace CapaNegocio.Helpers
{
    /// <summary>
    /// Validador empresarial de estados y transiciones
    /// Nivel: Producción - Sin romper sistema existente
    /// </summary>
    public static class ValidacionEstadosOR
    {
        // =============================
        // ESTADOS OFICIALES
        // =============================
        public const string BORRADOR = "BORRADOR";
        public const string PENDIENTE = "PENDIENTE";
        public const string PROCESADA = "PROCESADA";
        public const string FACTURADA = "FACTURADA";
        public const string COMPLETADA = "COMPLETADA";
        public const string ANULADA = "ANULADA";
        public const string RECHAZADA = "RECHAZADA";
        
        // Estados legacy para compatibilidad
        public const string GENERADA = "GENERADA";
        public const string ENVIADA = "ENVIADA";
        public const string PAGADA = "PAGADA";
        
        // Estados especiales
        public const string ORDEN_REQUERIDA = "Orden de Recaudación Requerida";

        /// <summary>
        /// Validar transición de estados con reglas empresariales
        /// </summary>
        public static void ValidarTransicion(string estadoActual, string estadoNuevo)
        {
            var actual = NormalizarEstado(estadoActual);
            var nuevo = NormalizarEstado(estadoNuevo);

            if (string.IsNullOrWhiteSpace(actual)) 
                throw new ArgumentException("Estado actual inválido");
            
            if (string.IsNullOrWhiteSpace(nuevo)) 
                throw new ArgumentException("Estado nuevo inválido");

            // =============================
            // REGLAS CRÍTICAS
            // =============================
            
            // Estados terminales
            if (actual == ANULADA) 
                throw new InvalidOperationException("Una orden ANULADA no puede cambiar de estado");
            
            if (actual == COMPLETADA && nuevo != ANULADA) 
                throw new InvalidOperationException("Una orden COMPLETADA solo puede anularse");

            // =============================
            // FLUJO PRINCIPAL VALIDADO
            // =============================
            switch (actual)
            {
                case BORRADOR:
                    if (nuevo != PENDIENTE && nuevo != GENERADA && nuevo != ANULADA)
                        throw new InvalidOperationException($"BORRADOR solo puede ir a PENDIENTE/GENERADA o ANULADA, no a {nuevo}");
                    break;

                case PENDIENTE:
                case GENERADA: // Compatibilidad legacy
                    if (nuevo != PROCESADA && nuevo != ENVIADA && nuevo != ANULADA)
                        throw new InvalidOperationException($"PENDIENTE/GENERADA solo puede ir a PROCESADA/ENVIADA o ANULADA, no a {nuevo}");
                    break;

                case PROCESADA:
                case ENVIADA: // Compatibilidad legacy
                    if (nuevo != FACTURADA && nuevo != RECHAZADA && nuevo != PENDIENTE && nuevo != ANULADA)
                        throw new InvalidOperationException($"PROCESADA/ENVIADA solo puede ir a FACTURADA, RECHAZADA, PENDIENTE o ANULADA, no a {nuevo}");
                    break;

                case FACTURADA:
                case PAGADA: // Compatibilidad legacy
                    if (nuevo != COMPLETADA && nuevo != ANULADA)
                        throw new InvalidOperationException($"FACTURADA/PAGADA solo puede ir a COMPLETADA o ANULADA, no a {nuevo}");
                    break;

                case RECHAZADA:
                    if (nuevo != PENDIENTE && nuevo != BORRADOR && nuevo != ANULADA)
                        throw new InvalidOperationException($"RECHAZADA solo puede regresar a PENDIENTE/BORRADOR o ANULADA, no a {nuevo}");
                    break;

                default:
                    // Estados especiales o futuros - permitir por compatibilidad
                    break;
            }
        }

        /// <summary>
        /// Verificar si un estado permite edición
        /// </summary>
        public static bool PermiteEdicion(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            return estadoNorm == BORRADOR;
        }

        /// <summary>
        /// Verificar si un estado permite generar/enviar
        /// </summary>
        public static bool PermiteGenerar(string estado, decimal total)
        {
            var estadoNorm = NormalizarEstado(estado);
            return (estadoNorm == BORRADOR || estadoNorm == GENERADA) && total > 0;
        }

        /// <summary>
        /// Verificar si un estado permite subir comprobante de pago
        /// </summary>
        public static bool PermiteSubirComprobante(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            return estadoNorm == PENDIENTE || estadoNorm == GENERADA || 
                   estadoNorm == ENVIADA || estadoNorm == PROCESADA;
        }

        /// <summary>
        /// Verificar si un estado permite anulación
        /// </summary>
        public static bool PermiteAnular(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            return estadoNorm != ANULADA && estadoNorm != COMPLETADA;
        }

        /// <summary>
        /// Obtener siguiente estado válido en el flujo normal
        /// </summary>
        public static string ObtenerSiguienteEstado(string estadoActual)
        {
            var actual = NormalizarEstado(estadoActual);
            
            switch (actual)
            {
                case BORRADOR: return PENDIENTE;
                case PENDIENTE: 
                case GENERADA: return PROCESADA;
                case PROCESADA:
                case ENVIADA: return FACTURADA;
                case FACTURADA:
                case PAGADA: return COMPLETADA;
                case COMPLETADA: return COMPLETADA; // Terminal
                case ANULADA: return ANULADA; // Terminal
                case RECHAZADA: return PENDIENTE;
                default: return PENDIENTE; // Estado seguro por defecto
            }
        }

        /// <summary>
        /// Determinar si un estado es considerado "pendiente"
        /// </summary>
        public static bool EsEstadoPendiente(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            return estadoNorm == BORRADOR || estadoNorm == PENDIENTE || 
                   estadoNorm == GENERADA || estadoNorm == PROCESADA || 
                   estadoNorm == ENVIADA;
        }

        /// <summary>
        /// Determinar si un estado es considerado "completado"
        /// </summary>
        public static bool EsEstadoCompletado(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            return estadoNorm == COMPLETADA || estadoNorm == FACTURADA || 
                   estadoNorm == PAGADA;
        }

        /// <summary>
        /// Obtener color CSS para badges de estado
        /// </summary>
        public static string ObtenerColorEstado(string estado)
        {
            var estadoNorm = NormalizarEstado(estado);
            
            switch (estadoNorm)
            {
                case BORRADOR: return "secondary";
                case PENDIENTE:
                case GENERADA: return "warning";
                case PROCESADA:
                case ENVIADA: return "info";
                case FACTURADA:
                case PAGADA:
                case COMPLETADA: return "success";
                case ANULADA:
                case RECHAZADA: return "danger";
                default: return "dark";
            }
        }

        /// <summary>
        /// Normalizar estado para comparaciones
        /// </summary>
        private static string NormalizarEstado(string estado)
        {
            return (estado ?? "").Trim().ToUpperInvariant();
        }
    }
}
