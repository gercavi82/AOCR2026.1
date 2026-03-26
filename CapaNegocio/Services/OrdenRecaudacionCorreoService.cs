using System;
using CapaDatos.Entidades;
using CapaDatos.Services;

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
            var observacionHtml = string.IsNullOrWhiteSpace(observacion)
                ? string.Empty
                : "<p><strong>Observaciones:</strong> " + System.Web.HttpUtility.HtmlEncode(observacion) + "</p>";

            return string.Format(@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#f5f8fb; padding:24px;'>
    <div style='max-width:680px; margin:0 auto; background:#ffffff; border:1px solid #dce7ef; border-radius:16px; overflow:hidden;'>
        <div style='background:linear-gradient(135deg,#4b4d63 0%,#2f6d8d 100%); color:#ffffff; padding:20px 24px;'>
            <div style='font-size:12px; letter-spacing:0.08em; text-transform:uppercase; opacity:0.85;'>Sistema AOCR DGAC</div>
            <h2 style='margin:8px 0 0 0;'>{0}</h2>
        </div>
        <div style='padding:24px;'>
            <p>Estimado/a <strong>{1}</strong>,</p>
            <p>{2}</p>
            <table style='width:100%; border-collapse:collapse; margin:18px 0;'>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Orden</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{3}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Contribuyente</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{4}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Estado</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>{5}</td>
                </tr>
                <tr>
                    <td style='padding:10px; border:1px solid #e4edf4; background:#f8fbfd; font-weight:bold;'>Total</td>
                    <td style='padding:10px; border:1px solid #e4edf4;'>${6}</td>
                </tr>
            </table>
            {7}
            <p>Puede revisar el detalle desde el sistema AOCR.</p>
            <p style='margin-top:24px; color:#617588; font-size:12px;'>Este es un mensaje automatico del workflow financiero AOCR.</p>
        </div>
    </div>
</body>
</html>",
                plantilla.Titulo,
                System.Web.HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(nombreDestino) ? "Usuario AOCR" : nombreDestino),
                System.Web.HttpUtility.HtmlEncode(plantilla.Mensaje),
                System.Web.HttpUtility.HtmlEncode(orden.NumeroOrden ?? ("#" + orden.Id)),
                System.Web.HttpUtility.HtmlEncode(orden.NombreContribuyente ?? orden.Compania ?? "Contribuyente"),
                System.Web.HttpUtility.HtmlEncode(orden.Estado ?? "PENDIENTE"),
                System.Web.HttpUtility.HtmlEncode(string.Format("{0:N2}", orden.Total ?? 0m)),
                observacionHtml);
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