using System;
using System.Threading.Tasks;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio de notificaciones que encola correos sin bloquear
    /// </summary>
    public class NotificacionService
    {
        private readonly IEmailQueueService _queueService;
        private readonly ILoggingService _logger;

        public NotificacionService(IEmailQueueService queueService)
        {
            _queueService = queueService;
            _logger = LoggingServiceFactory.Create();
        }

        /// <summary>
        /// Encola notificación - NO BLOQUEA el request
        /// </summary>
        public async Task<OperationResult<int>> EnviarNotificacionAsync(EnviarNotificacionRequest request)
        {
            try
            {
                // Generar correlationId sin HttpContext
                var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

                var item = new EmailQueueItem
                {
                    Para = request.EmailDestino,
                    ParaNombre = request.NombreDestino,
                    Asunto = ObtenerAsunto(request),
                    Cuerpo = ObtenerCuerpo(request),
                    EsHtml = true,
                    AdjuntoNombre = request.AdjuntarPdf ? request.NombreAdjunto : null,
                    AdjuntoContenido = request.AdjuntarPdf ? request.AdjuntoPdf : null,
                    AdjuntoMimeType = request.AdjuntarPdf ? "application/pdf" : null,
                    MaxIntentos = 3,
                    CorrelationId = correlationId,
                    OrdenId = request.OrdenId,
                    TipoNotificacion = request.TipoNotificacion
                };

                var queueId = await _queueService.EncolarAsync(item);

                _logger.LogInfo(
                    string.Format("Notificación encolada: ID={0}, Tipo={1}, Para={2}",
                        queueId, request.TipoNotificacion, request.EmailDestino),
                    new LogContext
                    {
                        CorrelationId = correlationId,
                        NumeroOrden = item.NumeroOrden
                    });

                // Retorna inmediatamente - el correo se enviará en background
                return OperationResult<int>.Ok(queueId, "Notificación encolada para envío");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "NOTIF_QUEUE_ERROR" });
                return OperationResult<int>.Fail("Error al encolar notificación: " + ex.Message);
            }
        }

        private string ObtenerAsunto(EnviarNotificacionRequest request)
        {
            switch (request.TipoNotificacion?.ToUpperInvariant())
            {
                case "ORDEN_CREADA":
                    return "Nueva Orden de Recaudación - AOCR";
                case "PAGO_REGISTRADO":
                    return "Pago Registrado - Pendiente de Validación";
                case "PAGO_VALIDADO":
                    return "Pago Validado - Orden Procesada";
                case "PAGO_RECHAZADO":
                    return "Pago Rechazado - Acción Requerida";
                case "FACTURA_GENERADA":
                    return "Factura Generada - AOCR";
                default:
                    return "Notificación - Sistema AOCR";
            }
        }

        private string ObtenerCuerpo(EnviarNotificacionRequest request)
        {
            // Plantilla base HTML
            return string.Format(@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; padding: 20px;'>
    <div style='max-width: 600px; margin: 0 auto; border: 1px solid #ddd; padding: 20px;'>
        <h2 style='color: #1B4F72;'>Sistema AOCR</h2>
        <p>Estimado/a <strong>{0}</strong>,</p>
        <p>{1}</p>
        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'>
        <p style='font-size: 12px; color: #666;'>
            Este es un mensaje automático. Por favor no responda a este correo.
        </p>
    </div>
</body>
</html>",
                request.NombreDestino ?? "Usuario",
                ObtenerMensaje(request.TipoNotificacion));
        }

        private string ObtenerMensaje(string tipo)
        {
            switch (tipo?.ToUpperInvariant())
            {
                case "ORDEN_CREADA":
                    return "Se ha generado una nueva orden de recaudación. Por favor, realice el pago correspondiente.";
                case "PAGO_REGISTRADO":
                    return "Su pago ha sido registrado y está pendiente de validación por el área financiera.";
                case "PAGO_VALIDADO":
                    return "Su pago ha sido validado exitosamente. La factura será generada en breve.";
                case "PAGO_RECHAZADO":
                    return "Su pago ha sido rechazado. Por favor, verifique los datos y vuelva a intentar.";
                case "FACTURA_GENERADA":
                    return "Su factura ha sido generada. Puede descargarla desde el sistema.";
                default:
                    return "Tiene una nueva notificación en el sistema AOCR.";
            }
        }
    }
}
