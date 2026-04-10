using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public class InspeccionCorreoService
    {
        private readonly IEmailQueueService _emailQueueService;
        private readonly ILoggingService _logger;
        private readonly NotificacionDestinatarioPolicyService _policyService;

        public InspeccionCorreoService()
            : this(new EmailQueueService(), new NotificacionDestinatarioPolicyService())
        {
        }

        public InspeccionCorreoService(IEmailQueueService emailQueueService, NotificacionDestinatarioPolicyService policyService)
        {
            _emailQueueService = emailQueueService;
            _policyService = policyService;
            _logger = LoggingServiceFactory.Create();
        }

        public ResultadoOperacion NotificarEvento(Inspeccion inspeccion, SolicitudAOCR solicitud, string evento, string observacion)
        {
            try
            {
                if (inspeccion == null || solicitud == null)
                {
                    return ResultadoOperacion.Error("No existe contexto suficiente para notificar el evento de inspeccion.");
                }

                var plantilla = ConstruirPlantilla(inspeccion, solicitud, evento, observacion);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Ok(null, "Evento sin plantilla de correo configurada.");
                }

                var destinatarios = _policyService.ResolverDestinatarios(solicitud, inspeccion, plantilla.GruposDestinatarios);
                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "Evento sin destinatarios de correo resolubles.");
                }

                foreach (var destinatario in destinatarios)
                {
                    var item = new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, inspeccion, solicitud, observacion),
                        Estado = "PENDIENTE",
                        OrdenId = solicitud.CodigoSolicitud,
                        TipoNotificacion = "INSPECCION_" + (evento ?? string.Empty).Trim().ToUpperInvariant(),
                        EsHtml = true
                    };

                    _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                }

                return ResultadoOperacion.Ok(destinatarios.Count, "Correos de inspeccion encolados correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.NotificarEvento: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar correos del evento de inspeccion.");
            }
        }

        public ResultadoOperacion NotificarInformeTecnicoFirmadoFinal(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, byte[] pdfFirmado, string enlaceDocumento, string observacion)
        {
            try
            {
                if (inspeccion == null || solicitud == null)
                {
                    return ResultadoOperacion.Error("No existe contexto suficiente para notificar el informe técnico firmado.");
                }

                if (pdfFirmado == null || pdfFirmado.Length == 0)
                {
                    return ResultadoOperacion.Error("No existe un PDF firmado final para adjuntar.");
                }

                var plantilla = ConstruirPlantilla(inspeccion, solicitud, "INFORME_TECNICO_FIRMADO", observacion);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Error("No existe plantilla configurada para el informe técnico firmado.");
                }

                var destinatarios = _policyService.ResolverDestinatarios(
                    solicitud,
                    inspeccion,
                    NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                    NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                    NotificacionDestinatarioPolicyService.GrupoInspectorAsignado);

                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "No existen destinatarios resolubles para el informe técnico firmado.");
                }

                var servicioCorreo = new EnviarCorreo();
                var cuerpo = ConstruirCuerpoHtmlConEnlace(
                    plantilla,
                    inspeccion,
                    solicitud,
                    observacion,
                    enlaceDocumento,
                    null);
                var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
                var nombreAdjunto = string.Format("InformeTecnico_{0}_Firmado.pdf", numeroSolicitud.Replace("/", "_").Replace("\\", "_"));
                var enviados = 0;

                foreach (var destinatario in destinatarios)
                {
                    var html = cuerpo.Replace("@@DESTINATARIO@@", System.Web.HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(destinatario.Nombre) ? "Usuario AOCR" : destinatario.Nombre));
                    if (servicioCorreo.enviaMensajeCorreoConAdjunto(destinatario.Email, plantilla.Asunto, html, pdfFirmado, nombreAdjunto, "application/pdf"))
                    {
                        enviados++;
                    }
                }

                return enviados > 0
                    ? ResultadoOperacion.Ok(enviados, "Notificación final del informe técnico enviada correctamente.")
                    : ResultadoOperacion.Error("No fue posible enviar la notificación final del informe técnico.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("InspeccionCorreoService.NotificarInformeTecnicoFirmadoFinal: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible enviar la notificación final del informe técnico firmado.");
            }
        }

        private static PlantillaCorreoInspeccion ConstruirPlantilla(Inspeccion inspeccion, SolicitudAOCR solicitud, string evento, string observacion)
        {
            var eventoNormalizado = (evento ?? string.Empty).Trim().ToUpperInvariant();
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            switch (eventoNormalizado)
            {
                case "NC_GENERADAS":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - No conformidades registradas en inspeccion #" + inspeccion.CodigoInspeccion,
                        Titulo = "No conformidades registradas",
                        Mensaje = "Se registraron no conformidades que requieren validacion de coordinacion y subsanacion del RT.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                case "DOCUMENTOS_SUBSANADOS":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Documentacion subsanada en inspeccion #" + inspeccion.CodigoInspeccion,
                        Titulo = "Documentacion subsanada",
                        Mensaje = "El RT actualizo documentos asociados a no conformidades y el expediente requiere revalidacion.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                case "DEVOLUCION_INSPECCION":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Tramite de inspeccion devuelto #" + inspeccion.CodigoInspeccion,
                        Titulo = "Tramite devuelto para correccion",
                        Mensaje = "La inspeccion fue devuelta para correccion o para programar una nueva inspeccion, segun observaciones registradas.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion
                        }
                    };
                case "APROBACION_INSPECCION":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Inspeccion aprobada #" + inspeccion.CodigoInspeccion,
                        Titulo = "Inspeccion aprobada",
                        Mensaje = "La inspeccion fue aprobada y el expediente queda listo para el siguiente tramo institucional.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionLegal,
                            NotificacionDestinatarioPolicyService.GrupoDireccionFinal
                        }
                    };
                case "REVALIDACION_OK":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Revalidacion satisfactoria #" + inspeccion.CodigoInspeccion,
                        Titulo = "Revalidacion satisfactoria",
                        Mensaje = "La revalidacion de la inspeccion fue satisfactoria y el tramite puede continuar.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };
                case "REVALIDACION_RECHAZADA":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Revalidacion con observaciones #" + inspeccion.CodigoInspeccion,
                        Titulo = "Revalidacion con observaciones",
                        Mensaje = "La revalidacion mantiene observaciones pendientes y se requiere una nueva subsanacion o ajuste del tramite.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante
                        }
                    };
                case "PENDIENTE_FIRMA_DIRDAC":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Documento pendiente de firma " + numeroSolicitud,
                        Titulo = "Documento pendiente de firma DIRDAC",
                        Mensaje = "Se informa que existe un informe técnico pendiente de firma institucional por DIRDAC.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoDireccionFinal
                        }
                    };
                case "INFORME_TECNICO_FIRMADO":
                    return new PlantillaCorreoInspeccion
                    {
                        Asunto = "AOCR - Informe técnico firmado " + numeroSolicitud,
                        Titulo = "Informe técnico firmado",
                        Mensaje = "El informe técnico ya cuenta con las firmas institucionales requeridas y queda legalizado para el expediente AOCR.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoRepresentanteTecnico,
                            NotificacionDestinatarioPolicyService.GrupoCoordinacionInspeccion,
                            NotificacionDestinatarioPolicyService.GrupoInspectorAsignado
                        }
                    };
                default:
                    return null;
            }
        }

        private static string ConstruirCuerpoHtml(string nombreDestino, PlantillaCorreoInspeccion plantilla, Inspeccion inspeccion, SolicitudAOCR solicitud, string observacion)
        {
            return ConstruirCuerpoHtmlConEnlace(plantilla, inspeccion, solicitud, observacion, null, nombreDestino);
        }

        private static string ConstruirCuerpoHtmlConEnlace(PlantillaCorreoInspeccion plantilla, Inspeccion inspeccion, SolicitudAOCR solicitud, string observacion, string enlaceDocumento, string nombreDestino)
        {
            var operador = string.IsNullOrWhiteSpace(solicitud.NombreOperador) ? (solicitud.RazonSocial ?? "Operador") : solicitud.NombreOperador;
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);

            var model = new EmailTemplateModel
            {
                Titulo = plantilla.Titulo,
                NombreDestinatario = string.IsNullOrWhiteSpace(nombreDestino) ? "@@DESTINATARIO@@" : nombreDestino,
                MensajePrincipal = plantilla.Mensaje,
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Inspeccion", "#" + inspeccion.CodigoInspeccion),
                    new EmailFieldItem("Solicitud AOCR", numeroSolicitud),
                    new EmailFieldItem("Operador", operador),
                    new EmailFieldItem("Estado actual", inspeccion.Estado ?? "PENDIENTE")
                },
                Observaciones = observacion,
                EnlaceUrl = enlaceDocumento,
                EnlaceTexto = "Abrir expediente",
                TextoCierre = "Puede revisar el expediente desde el sistema AOCR en el detalle de inspeccion correspondiente.",
                Footer = "Este es un mensaje automatico del workflow de inspeccion AOCR."
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

        private sealed class PlantillaCorreoInspeccion
        {
            public string Asunto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public string[] GruposDestinatarios { get; set; }
        }
    }
}