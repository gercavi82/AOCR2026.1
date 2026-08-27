using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public class SolicitudAocrCorreoService
    {
        private readonly IEmailQueueService _emailQueueService;
        private readonly ILoggingService _logger;
        private readonly NotificacionDestinatarioPolicyService _policyService;

        public SolicitudAocrCorreoService()
            : this(new EmailQueueService(), new NotificacionDestinatarioPolicyService())
        {
        }

        public SolicitudAocrCorreoService(
            IEmailQueueService emailQueueService,
            NotificacionDestinatarioPolicyService policyService)
        {
            _emailQueueService = emailQueueService;
            _policyService = policyService;
            _logger = LoggingServiceFactory.Create();
        }

        public ResultadoOperacion NotificarEvento(
            SolicitudAOCR solicitud,
            string evento,
            string observacion,
            string emailDestino = null,
            string nombreDestino = null,
            int? codigoHistorial = null,
            string correlationId = null)
        {
            try
            {
                if (solicitud == null)
                {
                    return ResultadoOperacion.Error("No existe solicitud AOCR para notificar.");
                }

                var plantilla = ConstruirPlantilla(solicitud, evento);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Ok(null, "Evento de solicitud sin plantilla de correo configurada.");
                }

                var destinatarios = ResolverDestinatarios(solicitud, plantilla, emailDestino, nombreDestino);
                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "Evento de solicitud sin destinatarios resolubles.");
                }

                foreach (var destinatario in destinatarios)
                {
                    var tipoNotificacion = "SOLICITUD_" + (evento ?? string.Empty).Trim().ToUpperInvariant();
                    var eventKey = BuildAocrEventKey(evento, solicitud.CodigoSolicitud, codigoHistorial, correlationId, destinatario.Email);

                    if (!string.IsNullOrWhiteSpace(eventKey))
                    {
                        var existeEnCola = _emailQueueService
                            .ExisteNotificacionAsync(tipoNotificacion, eventKey, solicitud.CodigoSolicitud)
                            .GetAwaiter()
                            .GetResult();

                        if (existeEnCola)
                        {
                            continue;
                        }
                    }

                    var item = new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, solicitud, observacion),
                        Estado = "PENDIENTE",
                        SolicitudId = solicitud.CodigoSolicitud,
                        TipoNotificacion = tipoNotificacion,
                        EventKey = eventKey,
                        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
                        EsHtml = true
                    };

                    _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                }

                return ResultadoOperacion.Ok(destinatarios.Count, "Correos de solicitud AOCR encolados correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SolicitudAocrCorreoService.NotificarEvento: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar correos del evento de solicitud AOCR.");
            }
        }

        public ResultadoOperacion NotificarAceptacionInspeccion(SolicitudAOCR solicitud, Inspeccion inspeccion, string correlationId = null)
        {
            if (solicitud == null)
            {
                return ResultadoOperacion.Error("No existe solicitud AOCR para notificar la aceptación de inspección.");
            }

            try
            {
                var destinatariosRt = _policyService.ResolverDestinatarios(
                    solicitud,
                    inspeccion,
                    NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                    NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante);
                var destinatariosInspector = _policyService.ResolverDestinatarios(
                    solicitud,
                    inspeccion,
                    NotificacionDestinatarioPolicyService.GrupoInspectorAsignado);
                var nombreInspector = destinatariosInspector
                    .Select(d => d != null ? d.Nombre : null)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
                if (string.IsNullOrWhiteSpace(nombreInspector) && inspeccion != null)
                {
                    nombreInspector = inspeccion.InspectorPrincipalNombre;
                }
                if (string.IsNullOrWhiteSpace(nombreInspector))
                {
                    nombreInspector = solicitud.TecnicoResponsableNombre;
                }
                if (string.IsNullOrWhiteSpace(nombreInspector))
                {
                    nombreInspector = "Inspector designado";
                }

                var estaciones = ObtenerEstacionesNotificacion(solicitud, inspeccion);
                var total = 0;
                foreach (var destinatario in destinatariosRt)
                {
                    if (EncolarAceptacionInspeccion(
                        solicitud,
                        destinatario,
                        "ACEPTACION_INSPECCION_RT",
                        "AOCR - Aceptación de inspección " + ObtenerNumeroSolicitudVisible(solicitud),
                        "Notificación al Representante Técnico",
                        ConstruirContenidoAceptacionRt(solicitud, nombreInspector, estaciones),
                        correlationId))
                    {
                        total++;
                    }
                }

                foreach (var destinatario in destinatariosInspector)
                {
                    if (EncolarAceptacionInspeccion(
                        solicitud,
                        destinatario,
                        "DESIGNACION_INSPECTOR",
                        "AOCR - Designación de inspector " + ObtenerNumeroSolicitudVisible(solicitud),
                        "Designación para inspección AOCR",
                        ConstruirContenidoDesignacionInspector(solicitud, estaciones),
                        correlationId))
                    {
                        total++;
                    }
                }

                return ResultadoOperacion.Ok(total, "Notificaciones de aceptación y designación encoladas correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SolicitudAocrCorreoService.NotificarAceptacionInspeccion: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar las notificaciones de aceptación de inspección.");
            }
        }

        private bool EncolarAceptacionInspeccion(
            SolicitudAOCR solicitud,
            NotificacionDestinatario destinatario,
            string evento,
            string asunto,
            string titulo,
            string contenidoHtml,
            string correlationId)
        {
            if (destinatario == null || !CorreoInstitucionalService.EsCorreoValido(destinatario.Email))
            {
                return false;
            }

            var tipo = "SOLICITUD_" + evento;
            var eventKey = BuildAocrEventKey(evento, solicitud.CodigoSolicitud, null, correlationId, destinatario.Email);
            if (_emailQueueService.ExisteNotificacionAsync(tipo, eventKey, solicitud.CodigoSolicitud).GetAwaiter().GetResult())
            {
                return false;
            }

            var cuerpo = EmailTemplateRenderer.Render(new EmailTemplateModel
            {
                Titulo = titulo,
                NombreDestinatario = string.IsNullOrWhiteSpace(destinatario.Nombre) ? "Usuario AOCR" : destinatario.Nombre,
                ContenidoHtmlExtra = contenidoHtml,
                TextoCierre = "Puede revisar el expediente desde el sistema AOCR.",
                Footer = "Este es un mensaje automático del workflow AOCR."
            });
            _emailQueueService.EncolarAsync(new EmailQueueItem
            {
                Para = destinatario.Email,
                ParaNombre = destinatario.Nombre,
                Asunto = asunto,
                Cuerpo = cuerpo,
                Estado = "PENDIENTE",
                SolicitudId = solicitud.CodigoSolicitud,
                TipoNotificacion = tipo,
                EventKey = eventKey,
                CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
                EsHtml = true
            }).GetAwaiter().GetResult();
            return true;
        }

        private static string ConstruirContenidoAceptacionRt(SolicitudAOCR solicitud, string inspector, IList<Tuple<string, string>> estaciones)
        {
            var numero = WebUtility.HtmlEncode(ObtenerNumeroSolicitudVisible(solicitud));
            var operador = WebUtility.HtmlEncode(ObtenerOperadorVisible(solicitud));
            var nombreInspector = WebUtility.HtmlEncode(inspector);
            return "<p>En referencia a la solicitud <strong>" + numero + "</strong>, presentada por el Operador Aéreo Extranjero <strong>" + operador +
                   "</strong>, relacionada con el trámite de emisión de un AOCR, esta Dirección comunica a usted que el Inspector designado ha cumplido con la revisión de la información y documentación ingresada, determinando que la misma cumple con lo requerido por la normativa.</p>" +
                   "<p>Asimismo, se acepta la inspección solicitada y, para tal efecto, se designa al Inspector <strong>" + nombreInspector + "</strong>, quien la cumplirá conforme al siguiente detalle:</p>" +
                   ConstruirTablaEstaciones(estaciones) +
                   "<p>Es importante señalar que su Representada será responsable de cubrir todos los gastos de traslados aéreos y terrestres del Inspector designado, hacia y desde los aeropuertos involucrados.</p>";
        }

        private static string ConstruirContenidoDesignacionInspector(SolicitudAOCR solicitud, IList<Tuple<string, string>> estaciones)
        {
            var numero = WebUtility.HtmlEncode(ObtenerNumeroSolicitudVisible(solicitud));
            var operador = WebUtility.HtmlEncode(ObtenerOperadorVisible(solicitud));
            return "<p>En referencia a la solicitud <strong>" + numero + "</strong>, presentada por el Operador Aéreo Extranjero <strong>" + operador +
                   "</strong>, comunico a usted que ha sido designado para realizar la inspección solicitada conforme al siguiente detalle:</p>" +
                   ConstruirTablaEstaciones(estaciones) +
                   "<p>Agradeceré gestionar la comisión de servicios institucionales y, a su vez, coordinar con el Representante Técnico del EAE la logística para cumplir la inspección referida.</p>";
        }

        private static string ConstruirTablaEstaciones(IEnumerable<Tuple<string, string>> estaciones)
        {
            var sb = new StringBuilder();
            sb.Append("<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin:12px 0 18px 0;'>");
            sb.Append("<tr><th style='padding:9px;border:1px solid #ccd8e2;background:#f2f7fa;text-align:left;'>Estación</th><th style='padding:9px;border:1px solid #ccd8e2;background:#f2f7fa;text-align:left;'>Fecha</th></tr>");
            foreach (var item in estaciones ?? Enumerable.Empty<Tuple<string, string>>())
            {
                sb.Append("<tr><td style='padding:9px;border:1px solid #dbe4eb;'>").Append(WebUtility.HtmlEncode(item.Item1)).Append("</td>");
                sb.Append("<td style='padding:9px;border:1px solid #dbe4eb;'>").Append(WebUtility.HtmlEncode(item.Item2)).Append("</td></tr>");
            }
            sb.Append("</table>");
            return sb.ToString();
        }

        private static List<Tuple<string, string>> ObtenerEstacionesNotificacion(SolicitudAOCR solicitud, Inspeccion inspeccion)
        {
            var fecha = inspeccion != null && inspeccion.FechaProgramada.HasValue
                ? inspeccion.FechaProgramada.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : "---";
            var valores = new[] { solicitud.AeropuertosEcuador, solicitud.AeropuertosEcuadorOtros, inspeccion != null ? inspeccion.Lugar : null };
            var estaciones = valores
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .SelectMany(v => v.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim().ToUpperInvariant())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(v => Tuple.Create(v, fecha))
                .ToList();
            if (estaciones.Count == 0)
            {
                estaciones.Add(Tuple.Create("POR DEFINIR", fecha));
            }
            return estaciones;
        }

        private static string ObtenerOperadorVisible(SolicitudAOCR solicitud)
        {
            return !string.IsNullOrWhiteSpace(solicitud.NombreOperador)
                ? solicitud.NombreOperador.Trim()
                : (!string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial.Trim() : "Operador Aéreo Extranjero");
        }

        private List<NotificacionDestinatario> ResolverDestinatarios(
            SolicitudAOCR solicitud,
            PlantillaSolicitudCorreo plantilla,
            string emailDestino,
            string nombreDestino)
        {
            var correoNormalizado = (emailDestino ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(correoNormalizado))
            {
                if (!CorreoInstitucionalService.EsCorreoValido(correoNormalizado))
                {
                    return new List<NotificacionDestinatario>();
                }

                return new List<NotificacionDestinatario>
                {
                    new NotificacionDestinatario
                    {
                        Email = correoNormalizado,
                        Nombre = string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino.Trim()
                    }
                };
            }

            return _policyService.ResolverDestinatarios(solicitud, null, plantilla.GruposDestinatarios);
        }

        public static string BuildAocrEventKey(
            string evento,
            int solicitudId,
            int? codigoHistorial,
            string correlationId,
            string destinatario)
        {
            if (solicitudId <= 0 || string.IsNullOrWhiteSpace(destinatario))
            {
                return null;
            }

            var eventoNormalizado = NormalizarEventoParaEventKey(evento);
            if (string.IsNullOrWhiteSpace(eventoNormalizado))
            {
                return null;
            }

            var destinatarioNormalizado = destinatario.Trim().ToLowerInvariant();
            if (codigoHistorial.HasValue && codigoHistorial.Value > 0)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "AOCR:{0}:{1}:{2}:{3}",
                    eventoNormalizado,
                    solicitudId,
                    codigoHistorial.Value,
                    destinatarioNormalizado);
            }

            var correlationNormalizado = string.IsNullOrWhiteSpace(correlationId)
                ? null
                : correlationId.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(correlationNormalizado))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "AOCR:{0}:{1}:{2}:{3}",
                    eventoNormalizado,
                    solicitudId,
                    correlationNormalizado,
                    destinatarioNormalizado);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "AOCR:{0}:{1}:{2}",
                eventoNormalizado,
                solicitudId,
                destinatarioNormalizado);
        }

        private static bool EsEventoConIdempotenciaFuerteHabilitada(string evento)
        {
            var eventoNormalizado = (evento ?? string.Empty).Trim().ToUpperInvariant();
            return string.Equals(eventoNormalizado, "OBSERVADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventoNormalizado, "REVISION_DOCUMENTAL_OBSERVADA", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizarEventoParaEventKey(string evento)
        {
            var eventoNormalizado = (evento ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(eventoNormalizado))
            {
                return null;
            }

            if (string.Equals(eventoNormalizado, "OBSERVADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventoNormalizado, "REVISION_DOCUMENTAL_OBSERVADA", StringComparison.OrdinalIgnoreCase))
            {
                return "SOLICITUD_OBSERVADA";
            }

            return eventoNormalizado.StartsWith("SOLICITUD_", StringComparison.OrdinalIgnoreCase)
                ? eventoNormalizado
                : "SOLICITUD_" + eventoNormalizado;
        }

        private static PlantillaSolicitudCorreo ConstruirPlantilla(SolicitudAOCR solicitud, string evento)
        {
            switch ((evento ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "AOCR_APROBADO_DIRECCION":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Solicitud aprobada por Dirección #" + solicitud.CodigoSolicitud,
                        Titulo = "Solicitud aprobada por Dirección",
                        Mensaje = "La solicitud AOCR fue aprobada por Dirección y pasa al tramo de legalización institucional.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionLegal
                        }
                    };
                case "AOCR_LEGALIZADO":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Solicitud legalizada #" + solicitud.CodigoSolicitud,
                        Titulo = "AOCR legalizado",
                        Mensaje = "La solicitud AOCR fue legalizada y el certificado queda habilitado para su emisión institucional.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionLegal,
                            NotificacionDestinatarioPolicyService.GrupoDireccionFinal
                        }
                    };
                case "AOCR_EMITIDO_RECIBIDO":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Certificado emitido y entregado #" + solicitud.CodigoSolicitud,
                        Titulo = "AOCR emitido y entregado",
                        Mensaje = "El certificado AOCR fue emitido y marcado como recibido. El tramite queda completado en su tramo institucional final.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionLegal,
                            NotificacionDestinatarioPolicyService.GrupoDireccionFinal
                        }
                    };
                case "INSPECTOR_ASIGNADO":
                    var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Inspector asignado a solicitud " + numeroSolicitud,
                        Titulo = "Inspector asignado",
                        Mensaje = "Por medio del presente, se informa que ha sido asignado/a como Inspector a la solicitud " + numeroSolicitud + ".",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };

                // --- NUEVOS EVENTOS DEL FLUJO BPMN ---

                case "OBSERVADA":
                case "REVISION_DOCUMENTAL_OBSERVADA":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Observaciones en revisión documental #" + solicitud.CodigoSolicitud,
                        Titulo = "Observaciones en revisión documental",
                        Mensaje = "La solicitud AOCR presenta observaciones en la revisión documental. " +
                                  "Debe corregir los documentos indicados y reenviarlos para continuar con el trámite.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };

                case "SUBSANADA":
                case "CORRECCIONES_ENVIADAS_RT":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Correcciones documentales enviadas por RT #" + solicitud.CodigoSolicitud,
                        Titulo = "Correcciones documentales enviadas",
                        Mensaje = "El Representante Técnico ha enviado las correcciones documentales solicitadas. " +
                                  "Por favor, revise los documentos actualizados para continuar con el flujo de inspección.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };

                case "ACEPTACION_DOCUMENTAL":
                case "REVISION_DOCUMENTAL_APROBADA":
                case "INSPECCION_HABILITADA":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Revisión documental aprobada #" + solicitud.CodigoSolicitud,
                        Titulo = "Revisión documental aprobada",
                        Mensaje = "La revisión documental de la solicitud AOCR fue completada satisfactoriamente. " +
                                  "Todos los documentos han sido aprobados y se habilitó la ejecución de la inspección técnica.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };

                case "ACEPTACION_COORDINADOR_FIRMADA":
                case "REVISION_FINAL_COORDINACION_REGISTRADA":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Revisión final de Coordinación #" + solicitud.CodigoSolicitud,
                        Titulo = "Revisión final de Coordinación registrada",
                        Mensaje = "La Coordinación registró la revisión final de la solicitud AOCR. " +
                                  "Continúa el flujo institucional para la validación y firma final que corresponda.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };

                case "PENDIENTE_ASIGNACION_INSPECTOR":
                case "SOLICITUD_COMPLETADA":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Solicitud completada por RT, pendiente asignación de inspector #" + solicitud.CodigoSolicitud,
                        Titulo = "Solicitud AOCR completada",
                        Mensaje = "El Representante Técnico completó el llenado de la solicitud AOCR. " +
                                  "La solicitud se encuentra pendiente de asignación de inspector para continuar con el proceso de inspección.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };

                case "PAGO_APROBADO":
                case "SOLICITUD_HABILITADA":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Pago aprobado, solicitud habilitada #" + solicitud.CodigoSolicitud,
                        Titulo = "Solicitud AOCR habilitada",
                        Mensaje = "El pago de la orden de recaudación fue aprobado por Financiero. " +
                                  "La solicitud AOCR ya se encuentra disponible para que complete el formulario y adjunte la documentación requerida.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };

                case "DIRDAC_APROBO_INFORME":
                case "APROBADO_DIRDAC":
                case "CERTIFICADO_AOCR_HABILITADO":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Informe técnico aprobado por DIRDAC, certificado habilitado #" + solicitud.CodigoSolicitud,
                        Titulo = "Informe técnico aprobado por DIRDAC",
                        Mensaje = "El Informe Técnico fue aprobado por DIRDAC sin observaciones. " +
                                  "El Certificado AOCR se encuentra habilitado para su generación y firma por el Coordinador.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionLegal
                        }
                    };

                case "DIRDAC_DEVOLVIO_INFORME":
                case "DEVUELTO_DIRDAC":
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Informe técnico devuelto por DIRDAC #" + solicitud.CodigoSolicitud,
                        Titulo = "Informe técnico devuelto por DIRDAC",
                        Mensaje = "DIRDAC / Dirección devolvió el Informe Técnico con observaciones. " +
                                  "El Inspector y/o Coordinador deben revisar las observaciones indicadas y subsanar el informe antes de reenviarlo.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                default:
                    return null;
            }
        }

        private static string ConstruirCuerpoHtml(string nombreDestino, PlantillaSolicitudCorreo plantilla, SolicitudAOCR solicitud, string observacion)
        {
            var operador = string.IsNullOrWhiteSpace(solicitud.NombreOperador) ? (solicitud.RazonSocial ?? "Operador") : solicitud.NombreOperador;
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);

            var model = new EmailTemplateModel
            {
                Titulo = plantilla.Titulo,
                NombreDestinatario = string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino,
                MensajePrincipal = plantilla.Mensaje,
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Solicitud AOCR", "#" + solicitud.CodigoSolicitud),
                    new EmailFieldItem("Numero de solicitud", numeroSolicitud),
                    new EmailFieldItem("Operador", operador),
                    new EmailFieldItem("Estado actual", solicitud.Estado ?? "PENDIENTE")
                },
                Observaciones = observacion,
                TextoCierre = "Puede revisar el expediente desde el sistema AOCR en el detalle de la solicitud correspondiente.",
                Footer = "Este es un mensaje automatico del workflow AOCR."
            };

            return EmailTemplateRenderer.Render(model);
        }

        private static string ObtenerNumeroSolicitudVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "N/D";
            }

            return string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? "DGAC-GOP-2026-AOCR" + solicitud.CodigoSolicitud
                : solicitud.NumeroSolicitud.Trim();
        }

        private sealed class PlantillaSolicitudCorreo
        {
            public string Asunto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public string[] GruposDestinatarios { get; set; }
        }
    }
}
