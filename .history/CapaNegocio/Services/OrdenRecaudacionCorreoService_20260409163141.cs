using System;
using System.Collections.Generic;
using CapaDatos.Entidades;
using CapaDatos.Services;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public class OrdenRecaudacionCorreoService
    {
        private readonly IEmailQueueService _emailQueueService;
        private readonly ILoggingService _logger;
        private readonly NotificacionDestinatarioPolicyService _policyService;

        public OrdenRecaudacionCorreoService()
            : this(new EmailQueueService(), new NotificacionDestinatarioPolicyService())
        {
        }

        public OrdenRecaudacionCorreoService(IEmailQueueService emailQueueService, NotificacionDestinatarioPolicyService policyService)
        {
            _emailQueueService = emailQueueService;
            _policyService = policyService;
            _logger = LoggingServiceFactory.Create();
        }

        public ResultadoOperacion NotificarEvento(
            OrdenRecaudacion orden,
            string evento,
            string emailDestino = null,
            string nombreDestino = null,
            byte[] adjuntoPdf = null,
            string nombreAdjunto = null,
            string observacion = null)
        {
            try
            {
                if (orden == null)
                {
                    return ResultadoOperacion.Error("No existe orden para notificar.");
                }

                var plantilla = ConstruirPlantilla(orden, evento);
                if (plantilla == null)
                {
                    return ResultadoOperacion.Ok(null, "Evento de orden sin plantilla de correo configurada.");
                }

                if (!string.IsNullOrWhiteSpace(emailDestino))
                {
                    orden.Correo = emailDestino;
                }

                if (!string.IsNullOrWhiteSpace(nombreDestino))
                {
                    orden.NombreContribuyente = nombreDestino;
                    orden.Compania = nombreDestino;
                }

                var destinatarios = _policyService.ResolverDestinatarios(orden, plantilla.GruposDestinatarios);
                if (destinatarios.Count == 0)
                {
                    return ResultadoOperacion.Ok(null, "Evento de orden sin destinatarios resolubles.");
                }

                foreach (var destinatario in destinatarios)
                {
                    var item = new EmailQueueItem
                    {
                        Para = destinatario.Email,
                        ParaNombre = destinatario.Nombre,
                        Asunto = plantilla.Asunto,
                        Cuerpo = ConstruirCuerpoHtml(destinatario.Nombre, plantilla, orden, observacion),
                        Estado = "PENDIENTE",
                        OrdenId = orden.Id,
                        TipoNotificacion = "OR_" + (evento ?? string.Empty).Trim().ToUpperInvariant(),
                        EsHtml = true,
                        AdjuntoContenido = adjuntoPdf,
                        AdjuntoNombre = adjuntoPdf != null ? (nombreAdjunto ?? (orden.NumeroOrden ?? "orden") + ".pdf") : null,
                        AdjuntoMimeType = adjuntoPdf != null ? "application/pdf" : null
                    };

                    _emailQueueService.EncolarAsync(item).GetAwaiter().GetResult();
                }

                return ResultadoOperacion.Ok(destinatarios.Count, "Correos de orden encolados correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("OrdenRecaudacionCorreoService.NotificarEvento: " + ex.Message);
                return ResultadoOperacion.Error("No fue posible encolar correos del evento de orden.");
            }
        }

        private static PlantillaOrdenCorreo ConstruirPlantilla(OrdenRecaudacion orden, string evento)
        {
            switch ((evento ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "ORDEN_CREADA":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Nueva Orden de recaudacion - " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Orden de recaudacion generada",
                        Mensaje = "Se genero una nueva orden de recaudacion asociada a su tramite. Revise el detalle y proceda con el pago correspondiente.",
                        GruposDestinatarios = new[] { NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante }
                    };
                case "PAGO_REGISTRADO":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Pago registrado - Orden " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Pago registrado",
                        Mensaje = "El pago de la orden fue registrado y queda pendiente de validacion por el area financiera.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoFinanciero
                        }
                    };
                case "PAGO_VALIDADO":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Pago validado - Orden " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Pago validado",
                        Mensaje = "El pago de la orden fue validado correctamente y el tramite financiero puede continuar.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoFinanciero
                        }
                    };
                case "FACTURA_GENERADA":
                    return new PlantillaOrdenCorreo
                    {
                        Asunto = "Factura generada - Orden " + (orden.NumeroOrden ?? ("#" + orden.Id)),
                        Titulo = "Factura generada",
                        Mensaje = "La factura asociada a la orden fue generada y queda disponible para su consulta.",
                        GruposDestinatarios = new[]
                        {
                            NotificacionDestinatarioPolicyService.GrupoOperadorSolicitante,
                            NotificacionDestinatarioPolicyService.GrupoFinanciero
                        }
                    };
                default:
                    return null;
            }
        }

        private static string ConstruirCuerpoHtml(string nombreDestino, PlantillaOrdenCorreo plantilla, OrdenRecaudacion orden, string observacion)
        {
            var model = new EmailTemplateModel
            {
                Titulo = plantilla.Titulo,
                NombreDestinatario = string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino,
                MensajePrincipal = plantilla.Mensaje,
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Orden", orden.NumeroOrden ?? ("#" + orden.Id)),
                    new EmailFieldItem("Contribuyente", orden.NombreContribuyente ?? orden.Compania ?? "Contribuyente"),
                    new EmailFieldItem("Estado", orden.Estado ?? "PENDIENTE"),
                    new EmailFieldItem("Total", "$" + string.Format("{0:N2}", orden.Total ?? 0m))
                },
                Observaciones = observacion,
                TextoCierre = "Puede revisar el detalle desde el sistema AOCR.",
                Footer = "Este es un mensaje automatico del workflow financiero AOCR."
            };

            return EmailTemplateRenderer.Render(model);
        }

        private sealed class PlantillaOrdenCorreo
        {
            public string Asunto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public string[] GruposDestinatarios { get; set; }
        }
    }
}