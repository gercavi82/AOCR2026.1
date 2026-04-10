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
                        OrdenId = solicitud.CodigoSolicitud,
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
                        Asunto = "AOCR - Solicitud aprobada por Direccion #" + solicitud.CodigoSolicitud,
                        Titulo = "Solicitud aprobada por Direccion",
                        Mensaje = "La solicitud AOCR fue aprobada por Direccion y pasa al tramo de legalizacion institucional.",
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
                        Mensaje = "La solicitud AOCR fue legalizada y el certificado queda habilitado para su emision institucional.",
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