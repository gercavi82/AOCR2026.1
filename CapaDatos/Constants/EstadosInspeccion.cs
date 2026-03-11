using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaDatos.Constants
{
    /// <summary>
    /// Estados del workflow de inspecciones AOCR (BPMN), con compatibilidad para estados legacy.
    /// </summary>
    public static class EstadosInspeccion
    {
        // ============================================
        // ESTADOS BPMN CANONICOS
        // ============================================
        public const string SOLICITUD_INSPECCION_CREADA = "SOLICITUD_INSPECCION_CREADA";
        public const string VERIFICACION_SOLICITUD = "VERIFICACION_SOLICITUD";
        public const string ACEPTADA = "ACEPTADA";
        public const string OBSERVADA = "OBSERVADA";
        public const string SUBSANADA = "SUBSANADA";
        public const string VIATICOS_REQUERIDOS = "VIATICOS_REQUERIDOS";
        public const string PAGO_VALIDADO = "PAGO_VALIDADO";
        public const string EN_INSPECCION = "EN_INSPECCION";
        public const string INFORME_ELABORADO = "INFORME_ELABORADO";
        public const string RESULTADO_SATISFACTORIO = "RESULTADO_SATISFACTORIO";
        public const string RESULTADO_NO_SATISFACTORIO = "RESULTADO_NO_SATISFACTORIO";
        public const string OBSERVACION_DOCUMENTAL = "OBSERVACION_DOCUMENTAL";
        public const string CERRADA = "CERRADA";

        // ============================================
        // ALIASES LEGACY (para no romper módulos previos)
        // ============================================
        public const string CREADA = SOLICITUD_INSPECCION_CREADA;
        public const string PROGRAMADA = VERIFICACION_SOLICITUD;
        public const string EN_CURSO = EN_INSPECCION;
        public const string APLAZADA = VIATICOS_REQUERIDOS;
        public const string FINALIZADA = INFORME_ELABORADO;
        public const string APROBADA = RESULTADO_SATISFACTORIO;
        public const string RECHAZADA = RESULTADO_NO_SATISFACTORIO;
        public const string CANCELADA = CERRADA;

        public static readonly string[] TodosLosEstados = new[]
        {
            SOLICITUD_INSPECCION_CREADA,
            VERIFICACION_SOLICITUD,
            ACEPTADA,
            OBSERVADA,
            SUBSANADA,
            VIATICOS_REQUERIDOS,
            PAGO_VALIDADO,
            EN_INSPECCION,
            INFORME_ELABORADO,
            RESULTADO_SATISFACTORIO,
            RESULTADO_NO_SATISFACTORIO,
            OBSERVACION_DOCUMENTAL,
            CERRADA
        };

        /// <summary>
        /// Transiciones canónicas permitidas según BPMN de inspecciones.
        /// </summary>
        public static readonly Dictionary<string, List<string>> TransicionesPermitidas =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { SOLICITUD_INSPECCION_CREADA, new List<string> { VERIFICACION_SOLICITUD } },
                { VERIFICACION_SOLICITUD, new List<string> { ACEPTADA, OBSERVADA, CERRADA } },
                { OBSERVADA, new List<string> { SUBSANADA, CERRADA } },
                { SUBSANADA, new List<string> { VERIFICACION_SOLICITUD } },
                { ACEPTADA, new List<string> { VIATICOS_REQUERIDOS, PAGO_VALIDADO, EN_INSPECCION } },
                { VIATICOS_REQUERIDOS, new List<string> { PAGO_VALIDADO, CERRADA } },
                { PAGO_VALIDADO, new List<string> { EN_INSPECCION } },
                { EN_INSPECCION, new List<string> { INFORME_ELABORADO, OBSERVADA } },
                { INFORME_ELABORADO, new List<string> { RESULTADO_SATISFACTORIO, RESULTADO_NO_SATISFACTORIO, OBSERVACION_DOCUMENTAL } },
                { OBSERVACION_DOCUMENTAL, new List<string> { SUBSANADA, CERRADA } },
                { RESULTADO_NO_SATISFACTORIO, new List<string> { OBSERVADA, CERRADA } },
                { RESULTADO_SATISFACTORIO, new List<string> { CERRADA } },
                { CERRADA, new List<string>() }
            };

        /// <summary>
        /// Normaliza cualquier estado (legacy/BPMN) al estado canónico BPMN.
        /// </summary>
        public static string NormalizarEstado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return SOLICITUD_INSPECCION_CREADA;
            }

            var value = estado.Trim().ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U");

            switch (value)
            {
                case "CREADA":
                case "SOLICITUD_INSPECCION_CREADA":
                    return SOLICITUD_INSPECCION_CREADA;
                case "PROGRAMADA":
                case "VERIFICACION_SOLICITUD":
                    return VERIFICACION_SOLICITUD;
                case "ACEPTADA":
                    return ACEPTADA;
                case "OBSERVADA":
                    return OBSERVADA;
                case "SUBSANADA":
                    return SUBSANADA;
                case "APLAZADA":
                case "VIATICOS_REQUERIDOS":
                    return VIATICOS_REQUERIDOS;
                case "PAGO_VALIDADO":
                    return PAGO_VALIDADO;
                case "EN_CURSO":
                case "EN_INSPECCION":
                case "EN_PROGRESO":
                    return EN_INSPECCION;
                case "FINALIZADA":
                case "INFORME_ELABORADO":
                    return INFORME_ELABORADO;
                case "APROBADA":
                case "RESULTADO_SATISFACTORIO":
                    return RESULTADO_SATISFACTORIO;
                case "RECHAZADA":
                case "RESULTADO_NO_SATISFACTORIO":
                    return RESULTADO_NO_SATISFACTORIO;
                case "OBSERVACION_DOCUMENTAL":
                    return OBSERVACION_DOCUMENTAL;
                case "CANCELADA":
                case "CERRADA":
                    return CERRADA;
                default:
                    return value;
            }
        }

        /// <summary>
        /// Mapea un estado canónico a un estado core legacy para compatibilidad
        /// cuando el constraint en BD aún no acepta estados BPMN extendidos.
        /// </summary>
        public static string MapearEstadoCoreCompat(string estadoCanonico)
        {
            var estado = NormalizarEstado(estadoCanonico);
            switch (estado)
            {
                case SOLICITUD_INSPECCION_CREADA:
                    return "CREADA";
                case VERIFICACION_SOLICITUD:
                case ACEPTADA:
                case SUBSANADA:
                case PAGO_VALIDADO:
                    return "PROGRAMADA";
                case OBSERVADA:
                case OBSERVACION_DOCUMENTAL:
                case RESULTADO_NO_SATISFACTORIO:
                    return "RECHAZADA";
                case VIATICOS_REQUERIDOS:
                    return "APLAZADA";
                case EN_INSPECCION:
                    return "EN_CURSO";
                case INFORME_ELABORADO:
                    return "FINALIZADA";
                case RESULTADO_SATISFACTORIO:
                    return "APROBADA";
                case CERRADA:
                    return "CERRADA";
                default:
                    return "CREADA";
            }
        }

        public static bool EsEstadoValido(string estado)
        {
            var normalized = NormalizarEstado(estado);
            return TodosLosEstados.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static bool EsTransicionValida(string estadoActual, string estadoDestino)
        {
            var actual = NormalizarEstado(estadoActual);
            var destino = NormalizarEstado(estadoDestino);

            if (!TransicionesPermitidas.ContainsKey(actual))
            {
                return false;
            }

            return TransicionesPermitidas[actual].Any(x => string.Equals(x, destino, StringComparison.OrdinalIgnoreCase));
        }

        public static List<string> ObtenerEstadosPermitidos(string estadoActual)
        {
            var actual = NormalizarEstado(estadoActual);
            if (!TransicionesPermitidas.ContainsKey(actual))
            {
                return new List<string>();
            }

            return new List<string>(TransicionesPermitidas[actual]);
        }

        public static bool EsEstadoFinal(string estado)
        {
            return string.Equals(NormalizarEstado(estado), CERRADA, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PermiteEdicion(string estado)
        {
            var normalized = NormalizarEstado(estado);
            return normalized == SOLICITUD_INSPECCION_CREADA ||
                   normalized == VERIFICACION_SOLICITUD ||
                   normalized == OBSERVADA ||
                   normalized == SUBSANADA ||
                   normalized == ACEPTADA;
        }

        public static bool PermiteSubirInforme(string estado)
        {
            var normalized = NormalizarEstado(estado);
            return normalized == EN_INSPECCION ||
                   normalized == INFORME_ELABORADO ||
                   normalized == RESULTADO_NO_SATISFACTORIO ||
                   normalized == OBSERVACION_DOCUMENTAL;
        }

        public static string ObtenerDescripcion(string estado)
        {
            switch (NormalizarEstado(estado))
            {
                case SOLICITUD_INSPECCION_CREADA:
                    return "Solicitud de inspección creada";
                case VERIFICACION_SOLICITUD:
                    return "Verificación de solicitud";
                case ACEPTADA:
                    return "Solicitud aceptada";
                case OBSERVADA:
                    return "Solicitud observada";
                case SUBSANADA:
                    return "Subsanada";
                case VIATICOS_REQUERIDOS:
                    return "Viáticos requeridos";
                case PAGO_VALIDADO:
                    return "Pago validado";
                case EN_INSPECCION:
                    return "En inspección";
                case INFORME_ELABORADO:
                    return "Informe elaborado";
                case RESULTADO_SATISFACTORIO:
                    return "Resultado satisfactorio";
                case RESULTADO_NO_SATISFACTORIO:
                    return "Resultado no satisfactorio";
                case OBSERVACION_DOCUMENTAL:
                    return "Observación documental";
                case CERRADA:
                    return "Cerrada";
                default:
                    return "Estado desconocido";
            }
        }

        public static string ObtenerColorBadge(string estado)
        {
            switch (NormalizarEstado(estado))
            {
                case SOLICITUD_INSPECCION_CREADA:
                    return "info";
                case VERIFICACION_SOLICITUD:
                case ACEPTADA:
                case PAGO_VALIDADO:
                    return "primary";
                case OBSERVADA:
                case OBSERVACION_DOCUMENTAL:
                case RESULTADO_NO_SATISFACTORIO:
                    return "danger";
                case SUBSANADA:
                    return "warning";
                case VIATICOS_REQUERIDOS:
                    return "secondary";
                case EN_INSPECCION:
                    return "warning";
                case INFORME_ELABORADO:
                case RESULTADO_SATISFACTORIO:
                    return "success";
                case CERRADA:
                    return "dark";
                default:
                    return "default";
            }
        }
    }
}
