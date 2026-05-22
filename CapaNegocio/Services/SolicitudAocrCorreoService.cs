using System;
using System.Collections.Generic;
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

        public ResultadoOperacion NotificarEvento(SolicitudAOCR solicitud, string evento, string observacion)
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

                var destinatarios = _policyService.ResolverDestinatarios(solicitud, null, plantilla.GruposDestinatarios);
                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "Evento de solicitud sin destinatarios resolubles.");
                }

                foreach (var destinatario in destinatarios)
                {
                    var item = new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, solicitud, observacion),
                        Estado = "PENDIENTE",
                        SolicitudId = solicitud.CodigoSolicitud,
                        TipoNotificacion = "SOLICITUD_" + (evento ?? string.Empty).Trim().ToUpperInvariant(),
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
                    return new PlantillaSolicitudCorreo
                    {
                        Asunto = "AOCR - Aceptación documental firmada #" + solicitud.CodigoSolicitud,
                        Titulo = "Aceptación documental firmada",
                        Mensaje = "La coordinación firmó la aceptación documental de la solicitud AOCR. " +
                                  "El documento final ya se encuentra disponible para su descarga desde el expediente.",
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
