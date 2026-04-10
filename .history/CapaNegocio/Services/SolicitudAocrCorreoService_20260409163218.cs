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
            var detalleHtml = string.IsNullOrWhiteSpace(observacion)
                ? string.Empty
                : "<p><strong>Detalle:</strong> " + System.Web.HttpUtility.HtmlEncode(observacion) + "</p>";

            return string.Format(@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f5f8fb; padding:24px;'>
    <div style='max-width:680px; margin:0 auto; background:#ffffff; border:1px solid #dce7ef; border-radius:16px; overflow:hidden;'>
        <div style='background:linear-gradient(135deg,#29455c 0%,#2f7f6b 100%); color:#ffffff; padding:20px 24px;'>
            <div style='font-size:12px; letter-spacing:0.08em; text-transform:uppercase; opacity:0.85;'>Sistema AOCR DGAC</div>
            <h2 style='margin:8px 0 0 0;'>{0}</h2>
        </div>
        <div style='padding:24px;'>
            <p>Estimado/a <strong>{1}</strong>,</p>
            <p>{2}</p>
            <table style='width:100%; border-collapse:collapse; margin:18px 0;'>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Solicitud AOCR</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>#{3}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Numero de solicitud</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{4}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Operador</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{5}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Estado actual</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{6}</td>
                </tr>
            </table>
            {7}
            <p>Puede revisar el expediente desde el sistema AOCR en el detalle de la solicitud correspondiente.</p>
            <p style='margin-top:24px; color:#617588; font-size:12px;'>Este es un mensaje automatico del workflow AOCR.</p>
        </div>
    </div>
</body>
</html>",
                plantilla.Titulo,
                System.Web.HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino),
                System.Web.HttpUtility.HtmlEncode(plantilla.Mensaje),
                solicitud.CodigoSolicitud,
                System.Web.HttpUtility.HtmlEncode(numeroSolicitud),
                System.Web.HttpUtility.HtmlEncode(operador),
                System.Web.HttpUtility.HtmlEncode(solicitud.Estado ?? "PENDIENTE"),
                detalleHtml);
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