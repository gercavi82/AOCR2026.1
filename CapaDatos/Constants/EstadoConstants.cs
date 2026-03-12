using System;
using System.Collections.Generic;

namespace CapaDatos.Constants
{
    public static class EstadoOrden
    {
        public const string Borrador = "BORRADOR";
        public const string Generada = "GENERADA";
        public const string Pendiente = "PENDIENTE";
        public const string Completada = "COMPLETADA";
        public const string Facturada = "FACTURADA";
        public const string Pagada = "PAGADA";
        public const string Anulada = "ANULADA";

        public static bool PermiteEditar(string estado)
        {
            return string.Equals(estado, Borrador, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PermiteCambiarEstado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Borrador, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Generada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Pendiente, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsEstadoFinal(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Pagada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Anulada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Completada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Facturada, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsPagado(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Pagada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Completada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Facturada, StringComparison.OrdinalIgnoreCase);
        }

        public static string ObtenerColorBadge(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return "secondary";

            switch (estado.ToUpperInvariant())
            {
                case Borrador:
                    return "secondary";
                case Pendiente:
                case Generada:
                    return "warning";
                case Facturada:
                case Completada:
                case Pagada:
                    return "success";
                case Anulada:
                    return "danger";
                default:
                    return "info";
            }
        }

        public static string[] ObtenerTodos()
        {
            return new[]
            {
                Borrador,
                Generada,
                Pendiente,
                Completada,
                Facturada,
                Pagada,
                Anulada
            };
        }
    }

    public static class EstadoPago
    {
        public const string Pendiente = "PENDIENTE";
        public const string Validado = "VALIDADO";
        public const string Aprobado = "APROBADO";
        public const string Rechazado = "RECHAZADO";
        public const string Anulado = "ANULADO";

        public static bool EsEstadoFinal(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado)) return false;

            return string.Equals(estado, Validado, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Aprobado, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Rechazado, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(estado, Anulado, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class EstadoEmail
    {
        public const string Pendiente = "Pendiente";
        public const string Enviado = "Enviado";
        public const string Fallido = "Fallido";
        public const string Procesando = "Procesando";
        public const string Cancelado = "Cancelado";
    }

    public static class EstadoDocumento
    {
        public const string Pendiente = "PENDIENTE";
        public const string Aprobado = "APROBADO";
        public const string Rechazado = "RECHAZADO";
        public const string Revision = "REVISION";
    }

    public static class EstadoSolicitud
    {
        // Legacy
        public const string Pendiente = "Pendiente";
        public const string EnRevision = "En Revision";
        public const string DocumentacionCompleta = "Documentacion Completa";
        public const string PagoPendiente = "Pago Pendiente";
        public const string PagoValidado = "Pago Validado";
        public const string InspeccionProgramada = "Inspeccion Programada";
        public const string InspeccionRealizada = "Inspeccion Realizada";
        public const string Aprobada = "Aprobada";
        public const string Rechazada = "Rechazada";
        public const string CertificadoEmitido = "Certificado Emitido";
        public const string Anulada = "Anulada";

        // BPMN AOCR target
        public const string SolicitudCreada = "Solicitud Creada";
        public const string DocumentacionPendiente = "Documentacion Pendiente";
        public const string Observada = "Observada";
        public const string Subsanada = "Subsanada";
        public const string AceptacionDocumental = "Aceptacion Documental";
        public const string PendienteAsignacionRT = "Pendiente Asignacion RT";
        public const string EnInspeccion = "En Inspeccion";
        public const string AOCR_EnElaboracion = "AOCR En Elaboracion";
        public const string AOCR_EnRevision = "AOCR En Revision";
        public const string AOCR_Validado = "AOCR Validado";
        public const string AOCR_Legalizado = "AOCR Legalizado";
        public const string AOCR_EmitidoRecibido = "AOCR Emitido/Recibido";

        private static readonly string[] EstadosValidos =
        {
            // Legacy
            Pendiente,
            EnRevision,
            DocumentacionCompleta,
            PagoPendiente,
            PagoValidado,
            InspeccionProgramada,
            InspeccionRealizada,
            Aprobada,
            Rechazada,
            CertificadoEmitido,
            Anulada,

            // BPMN AOCR
            SolicitudCreada,
            DocumentacionPendiente,
            Observada,
            Subsanada,
            AceptacionDocumental,
            PendienteAsignacionRT,
            EnInspeccion,
            AOCR_EnElaboracion,
            AOCR_EnRevision,
            AOCR_Validado,
            AOCR_Legalizado,
            AOCR_EmitidoRecibido
        };

        private static readonly Dictionary<string, string[]> Transiciones = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { SolicitudCreada, new[] { DocumentacionPendiente, Observada } },
            { DocumentacionPendiente, new[] { Observada, AceptacionDocumental } },
            { Observada, new[] { Subsanada } },
            { Subsanada, new[] { DocumentacionPendiente, AceptacionDocumental } },
            { AceptacionDocumental, new[] { PendienteAsignacionRT, EnInspeccion } },
            { PendienteAsignacionRT, new[] { EnInspeccion } },
            { EnInspeccion, new[] { AOCR_EnElaboracion } },
            { AOCR_EnElaboracion, new[] { AOCR_EnRevision } },
            { AOCR_EnRevision, new[] { AOCR_Validado, Observada } },
            { AOCR_Validado, new[] { AOCR_Legalizado, Observada } },
            { AOCR_Legalizado, new[] { AOCR_EmitidoRecibido } },
            { AOCR_EmitidoRecibido, Array.Empty<string>() }
        };

        public static string Normalizar(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return Pendiente;
            }

            var trimmed = estado.Trim();
            var upper = trimmed
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U");

            switch (upper)
            {
                case "BORRADOR":
                case "PENDIENTE":
                case "SOLICITUD_CREADA":
                    return Pendiente;
                case "DOCUMENTACION_PENDIENTE":
                case "ENVIADO":
                case "PREPARANDO":
                    return EnRevision;
                case "DOCUMENTOS_COMPLETOS":
                case "DOCUMENTACION_COMPLETA":
                    return DocumentacionCompleta;
                case "ACEPTACION_DOCUMENTAL":
                case "APROBADO_POR_INSPECTOR":
                    return AceptacionDocumental;
                case "PENDIENTE_ASIGNACION_RT":
                case "PENDIENTE ASIGNACION RT":
                case "PENDIENTE_ASIGNACION_TECNICA":
                case "PENDIENTE ASIGNACION TECNICA":
                case "PENDIENTE_ASIGNACION":
                    return PendienteAsignacionRT;
                case "PAGO_PENDIENTE":
                    return PagoPendiente;
                case "PAGO_VALIDADO":
                case "PAGO_VALIDADO_ADMIN":
                    return PagoValidado;
                case "INSPECCION_ASIGNADA":
                case "ENVIADO_A_INSPECTOR":
                case "EN_INSPECCION":
                case "INSPECCION_PROGRAMADA":
                case "INSPECCION_A_PROGRAMAR":
                    return EnInspeccion;
                case "INSPECCION_REALIZADA":
                case "INSPECCION_COMPLETADA":
                    return InspeccionRealizada;
                case "AOCR_EN_ELABORACION":
                    return AOCR_EnElaboracion;
                case "AOCR_EN_REVISION":
                case "ENVIADO_A_JEFATURA":
                    return AOCR_EnRevision;
                case "APROBADO":
                case "APROBADO_POR_DIRECCION":
                    return Aprobada;
                case "VALIDADO_TECNICAMENTE":
                    return AOCR_Validado;
                case "LEGALIZADO":
                    return AOCR_Legalizado;
                case "RECHAZADO":
                case "OBSERVADO":
                case "OBSERVADO_JEFATURA":
                case "RECHAZADO_POR_DIRECCION":
                    return Observada;
                case "SUBSANADO":
                case "SUBSANADA":
                    return Subsanada;
                case "CERTIFICADO_LEGALIZADO":
                case "CERTIFICADO_EMITIDO":
                case "AOCR_EMITIDO":
                case "AOCR_ENTREGADO":
                case "AOCR_EMITIDO_RECIBIDO":
                    return AOCR_EmitidoRecibido;
                default:
                    foreach (var valido in EstadosValidos)
                    {
                        if (string.Equals(valido, trimmed, StringComparison.OrdinalIgnoreCase))
                        {
                            return valido;
                        }
                    }

                    return Pendiente;
            }
        }

        public static bool PermiteEdicion(string estado)
        {
            var actual = Normalizar(estado);
            return string.Equals(actual, Pendiente, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, DocumentacionPendiente, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Observada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Subsanada, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsEstadoValido(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return false;
            }

            var normalized = Normalizar(estado);
            foreach (var valido in EstadosValidos)
            {
                if (string.Equals(normalized, valido, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool EsTransicionValida(string estadoActual, string estadoDestino)
        {
            var actual = Normalizar(estadoActual);
            var destino = Normalizar(estadoDestino);

            if (!Transiciones.ContainsKey(actual))
            {
                return false;
            }

            foreach (var permitido in Transiciones[actual])
            {
                if (string.Equals(permitido, destino, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
