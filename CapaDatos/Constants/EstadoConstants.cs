using System;
using System.Collections.Generic;

namespace CapaDatos.Constants
{
    public static class EstadoOrden
    {
        public const string Borrador = "BORRADOR";
        public const string Generada = "GENERADA";
        public const string Pendiente = "PENDIENTE";
        public const string Enviada = "ENVIADA";
        public const string EnRevisionFinanciera = "EN_REVISION_FINANCIERA";
        public const string Devuelta = "DEVUELTA";
        public const string Completada = "COMPLETADA";
        public const string Facturada = "FACTURADA";
        public const string Pagada = "PAGADA";
        public const string Anulada = "ANULADA";

        public static string NormalizarEstado(string estado)
        {
            var actual = (estado ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");

            switch (actual)
            {
                case "PROCESADA":
                case "EN_REVISION":
                case EnRevisionFinanciera:
                    return EnRevisionFinanciera;
                case Enviada:
                    return Enviada;
                case Devuelta:
                    return Devuelta;
                case Facturada:
                    return Facturada;
                case Completada:
                    return Completada;
                case Pagada:
                    return Pagada;
                case Anulada:
                    return Anulada;
                case Pendiente:
                    return Pendiente;
                case Generada:
                    return Generada;
                case Borrador:
                    return Borrador;
                default:
                    return actual;
            }
        }

        public static bool PermiteEditar(string estado)
        {
            return string.Equals(NormalizarEstado(estado), Borrador, StringComparison.OrdinalIgnoreCase);
        }

        public static bool PermiteCambiarEstado(string estado)
        {
            var actual = NormalizarEstado(estado);
            if (string.IsNullOrWhiteSpace(actual)) return false;

            return string.Equals(actual, Borrador, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Generada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Pendiente, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Enviada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, EnRevisionFinanciera, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Devuelta, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsEstadoFinal(string estado)
        {
            var actual = NormalizarEstado(estado);
            if (string.IsNullOrWhiteSpace(actual)) return false;

            return string.Equals(actual, Pagada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Anulada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Completada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Facturada, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EsPagado(string estado)
        {
            var actual = NormalizarEstado(estado);
            if (string.IsNullOrWhiteSpace(actual)) return false;

            return string.Equals(actual, Pagada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Completada, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(actual, Facturada, StringComparison.OrdinalIgnoreCase);
        }

        public static string ObtenerColorBadge(string estado)
        {
            var actual = NormalizarEstado(estado);
            if (string.IsNullOrWhiteSpace(actual)) return "secondary";

            switch (actual)
            {
                case Borrador:
                    return "secondary";
                case Pendiente:
                case Generada:
                    return "warning";
                case Enviada:
                    return "info";
                case EnRevisionFinanciera:
                    return "primary";
                case Devuelta:
                    return "danger";
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
                Enviada,
                EnRevisionFinanciera,
                Devuelta,
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
        public const string RequiereInspeccion = "Requiere Inspeccion";
        public const string GeneradoCondicionesLimitaciones = "Generado Condiciones y Limitaciones";
        public const string EnRevisionCoordinadorFinal = "En Revision Coordinador Final";
        public const string EnviadoDcav = "Enviado DCAV";
        public const string FirmadoDcav = "Firmado DCAV";
        public const string PendienteAsignacionRT = "Pendiente Asignacion RT";
        public const string FirmadoCoordinador = "Firmado Coordinador";
        public const string Finalizado = "Finalizado";
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
                Pendiente,
                EnRevision,
            DocumentacionPendiente,
            Observada,
            Subsanada,
            AceptacionDocumental,
            RequiereInspeccion,
            GeneradoCondicionesLimitaciones,
            EnRevisionCoordinadorFinal,
            EnviadoDcav,
            FirmadoDcav,
            PendienteAsignacionRT,
            FirmadoCoordinador,
            Finalizado,
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
            { Pendiente, new[] { EnRevision } },
            { EnRevision, new[] { Observada, AceptacionDocumental, EnInspeccion } },
            { DocumentacionPendiente, new[] { Observada, AceptacionDocumental } },
            { Observada, new[] { Subsanada } },
            { Subsanada, new[] { DocumentacionPendiente, AceptacionDocumental, EnInspeccion } },
            { AceptacionDocumental, new[] { RequiereInspeccion, GeneradoCondicionesLimitaciones, PendienteAsignacionRT, EnInspeccion, FirmadoCoordinador } },
            { RequiereInspeccion, new[] { PendienteAsignacionRT, EnInspeccion } },
            { GeneradoCondicionesLimitaciones, new[] { EnRevisionCoordinadorFinal } },
            { EnRevisionCoordinadorFinal, new[] { EnviadoDcav } },
            { EnviadoDcav, new[] { FirmadoDcav } },
            { FirmadoDcav, new[] { Finalizado } },
            { PendienteAsignacionRT, new[] { EnInspeccion } },
            { FirmadoCoordinador, new[] { PendienteAsignacionRT, RequiereInspeccion, GeneradoCondicionesLimitaciones } },
            { Finalizado, Array.Empty<string>() },
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
                case "EN_REVISION_DOCUMENTAL":
                case "EN REVISION DOCUMENTAL":
                case "PENDIENTE_REVISION_DOCUMENTAL":
                case "PENDIENTE REVISION DOCUMENTAL":
                case "PENDIENTE_CARGA_DOCUMENTAL_RT":
                case "PENDIENTE CARGA DOCUMENTAL RT":
                case "ENVIADO":
                case "PREPARANDO":
                case "ENVIADO_COORDINADOR":
                case "ENVIADO COORDINADOR":
                case "EN_REVISION_COORDINADOR":
                case "EN REVISION COORDINADOR":
                    return EnRevision;
                case "DOCUMENTOS_COMPLETOS":
                case "DOCUMENTACION_COMPLETA":
                    return DocumentacionCompleta;
                case "ACEPTACION_DOCUMENTAL":
                case "ACEPTADO_INSPECTOR":
                case "APROBADO_POR_INSPECTOR":
                case "DOCUMENTACION_ACEPTADA":
                case "DOCUMENTACION ACEPTADA":
                    return AceptacionDocumental;
                case "REQUIERE_INSPECCION":
                case "REQUIERE INSPECCION":
                    return RequiereInspeccion;
                case "GENERADO_CONDICIONES_LIMITACIONES":
                case "GENERADO CONDICIONES LIMITACIONES":
                    return GeneradoCondicionesLimitaciones;
                case "EN_REVISION_COORDINADOR_FINAL":
                case "EN REVISION COORDINADOR FINAL":
                    return EnRevisionCoordinadorFinal;
                case "ENVIADO_DCAV":
                case "ENVIADO DCAV":
                    return EnviadoDcav;
                case "FIRMADO_DCAV":
                case "FIRMADO DCAV":
                    return FirmadoDcav;
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
                case "INSPECTOR_ASIGNADO":
                case "EN_REVISION_INSPECTOR":
                case "EN REVISION INSPECTOR":
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
                case "EN REVISION AOCR":
                case "ENVIADO_A_JEFATURA":
                case "ENVIADO A JEFATURA":
                    return AOCR_EnRevision;
                case "APROBADO":
                case "APROBADO_POR_DIRECCION":
                    return Aprobada;
                case "VALIDADO":
                    return AOCR_Validado;
                case "VALIDADO_TECNICAMENTE":
                case "VALIDADO TECNICAMENTE":
                case "ENVIADO_A_LEGALIZACION":
                case "ENVIADO A LEGALIZACION":
                case "AOCR_VALIDADO":
                    return AOCR_Validado;
                case "LEGALIZADO":
                case "AOCR_LEGALIZADO":
                    return AOCR_Legalizado;
                case "RECHAZADO":
                case "OBSERVADO":
                case "DEVUELTO":
                case "DEVUELTA":
                case "DEVUELTO_CON_OBSERVACIONES":
                case "DEVUELTO_RT":
                case "DEVUELTO RT":
                case "OBSERVADO_JEFATURA":
                case "RECHAZADO_POR_DIRECCION":
                    return Observada;
                case "SUBSANADO":
                case "SUBSANADA":
                    return Subsanada;
                case "FIRMADO_COORDINADOR":
                case "AUTORIZACION_FIRMADA":
                case "FIRMADO COORDINADOR":
                    return FirmadoCoordinador;
                case "FINALIZADO":
                    return Finalizado;
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

        /// <summary>
        /// Clave técnica del estado para reglas de edición del formulario RT (FormularioEmisionAOCR).
        /// </summary>
        public static string NormalizarClaveEdicion(string estado)
        {
            return (estado ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace(" ", "_")
                .Replace("-", "_");
        }

        private static readonly HashSet<string> ClavesEditablesFormularioEmision = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BORRADOR",
            "NO_GENERADO",
            "PENDIENTE_SUBSANACION",
            "SUBSANACION_REQUERIDA",
            "EN_CARGA",
            "REGISTRO_INICIAL"
        };

        private static readonly HashSet<string> ClavesNoEditablesFormularioEmision = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EN_INSPECCION",
            "EN_REVISION_DOCUMENTAL",
            "ACEPTACION_DOCUMENTAL",
            "PENDIENTE_CARGA_FIRMADA",
            "SOLICITUD_FIRMADA",
            "ENVIADO_COORDINADOR",
            "EN_REVISION_COORDINADOR",
            "ASIGNADA_INSPECTOR",
            "AOCR_EN_ELABORACION",
            "FIRMADO_COORDINADOR",
            "AOCR_LEGALIZADO",
            "AUTORIZACION_FIRMADA",
            "CERRADA",
            "FINALIZADA",
            "CERRADO",
            "FINALIZADO"
        };

        private static readonly HashSet<string> EstadosNormalizadosNoEditablesFormularioEmision = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            EnInspeccion,
            EnRevision,
            AceptacionDocumental,
            RequiereInspeccion,
            GeneradoCondicionesLimitaciones,
            EnRevisionCoordinadorFinal,
            EnviadoDcav,
            FirmadoDcav,
            PendienteAsignacionRT,
            FirmadoCoordinador,
            Finalizado,
            AOCR_EnElaboracion,
            AOCR_EnRevision,
            AOCR_Validado,
            AOCR_Legalizado,
            AOCR_EmitidoRecibido,
            DocumentacionCompleta,
            PagoPendiente,
            PagoValidado,
            InspeccionProgramada,
            InspeccionRealizada,
            Aprobada,
            Rechazada,
            CertificadoEmitido,
            Anulada
        };

        /// <summary>
        /// Determina si el RT puede editar el formulario de emisión AOCR según el estado actual del trámite.
        /// </summary>
        public static bool PermiteEdicionFormularioEmision(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                return true;
            }

            var clave = NormalizarClaveEdicion(estado);
            if (ClavesEditablesFormularioEmision.Contains(clave))
            {
                return true;
            }

            if (PermiteEdicion(estado))
            {
                return true;
            }

            if (ClavesNoEditablesFormularioEmision.Contains(clave))
            {
                return false;
            }

            var actual = Normalizar(estado);
            return !EstadosNormalizadosNoEditablesFormularioEmision.Contains(actual);
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
