using System;
using CapaDatos.Services;
using CapaNegocio.Helpers;

namespace CapaNegocio.Services
{
    public class RechazoAnulacionNotificacionService
    {
        private readonly IEmailQueueService _queue;

        public RechazoAnulacionNotificacionService()
        {
            var config = new SecureConfigurationService();
            var cs = config.GetConnectionString("PostgreSQL")
                ?? config.GetConnectionString("AOCRConnection");
            _queue = new EmailQueueService(cs ?? string.Empty);
        }

        public int NotificarOrdenGeneradaAsync(
            int ordenId,
            int? solicitudId,
            string numeroOrden,
            string emailSolicitante,
            string nombreSolicitante,
            string rolUnidad,
            DateTime fechaAccion,
            decimal montoTotal,
            string observaciones = null)
        {
            var eventKey = string.Format("ORDEN_{0}_GENERADA", ordenId);
            var asunto = string.Format("Orden {0} GENERADA", numeroOrden ?? "N/A");
            var referencia = solicitudId.HasValue ? solicitudId.Value.ToString() : "N/A";
            var body = EmailTemplateBuilder.OrdenGenerada(
                nombreSolicitante,
                numeroOrden,
                referencia,
                fechaAccion,
                rolUnidad,
                montoTotal,
                observaciones);

            if (string.IsNullOrWhiteSpace(emailSolicitante))
            {
                return _queue.RegistrarErrorEventoAsync(
                    eventKey,
                    asunto,
                    "No se encontró correo del solicitante para notificación de orden generada.",
                    solicitudId,
                    ordenId,
                    "ORDEN_GENERADA").GetAwaiter().GetResult();
            }

            var item = new EmailQueueItem
            {
                Para = emailSolicitante.Trim(),
                Asunto = asunto,
                Cuerpo = body,
                SolicitudId = solicitudId,
                OrdenId = ordenId,
                TipoNotificacion = "ORDEN_GENERADA",
                CorrelationId = eventKey,
                EventKey = eventKey,
                RolOrigen = rolUnidad,
                EstadoFinal = "GENERADA",
                MaxIntentos = 3,
                EsHtml = true
            };

            return _queue.EncolarIdempotenteAsync(item).GetAwaiter().GetResult();
        }

        public int NotificarOrdenAsync(
            int ordenId,
            int? solicitudId,
            string numeroOrden,
            string emailSolicitante,
            string nombreSolicitante,
            string observaciones,
            string rolUnidad,
            string estadoFinal,
            DateTime fechaAccion)
        {
            var estadoNorm = (estadoFinal ?? "N/A").Trim().ToUpperInvariant();
            var eventKey = string.Format("ORDEN_{0}_{1}", ordenId, estadoNorm);
            var asunto = string.Format("Orden {0} {1}", numeroOrden ?? "N/A", (estadoFinal ?? "ACTUALIZADA").Trim().ToUpperInvariant());
            var referencia = solicitudId.HasValue ? solicitudId.Value.ToString() : "N/A";
            var body = EmailTemplateBuilder.OrdenRechazada(
                nombreSolicitante,
                numeroOrden,
                referencia,
                fechaAccion,
                rolUnidad,
                estadoFinal,
                observaciones);

            if (string.IsNullOrWhiteSpace(emailSolicitante))
            {
                return _queue.RegistrarErrorEventoAsync(
                    eventKey,
                    asunto,
                    "No se encontró correo del solicitante para notificación de rechazo/anulación.",
                    solicitudId,
                    ordenId,
                    "ORDEN_RECHAZADA_ANULADA").GetAwaiter().GetResult();
            }

            var item = new EmailQueueItem
            {
                Para = emailSolicitante.Trim(),
                Asunto = asunto,
                Cuerpo = body,
                SolicitudId = solicitudId,
                OrdenId = ordenId,
                TipoNotificacion = "ORDEN_RECHAZADA_ANULADA",
                CorrelationId = eventKey,
                EventKey = eventKey,
                RolOrigen = rolUnidad,
                EstadoFinal = estadoFinal,
                MaxIntentos = 3,
                EsHtml = true
            };

            return _queue.EncolarIdempotenteAsync(item).GetAwaiter().GetResult();
        }

        public int NotificarSolicitudAsync(
            int solicitudId,
            string numeroSolicitud,
            string emailSolicitante,
            string nombreSolicitante,
            string observaciones,
            string rolUnidad,
            string estadoFinal,
            DateTime fechaAccion)
        {
            var eventKey = string.Format("SOLICITUD_{0}_ESTADO_{1}", solicitudId, (estadoFinal ?? "N/A").Trim().ToUpperInvariant());
            var asunto = string.Format("Solicitud {0} {1}", numeroSolicitud ?? solicitudId.ToString(), (estadoFinal ?? "ACTUALIZADA").Trim().ToUpperInvariant());
            var body = EmailTemplateBuilder.OrdenRechazada(
                nombreSolicitante,
                numeroSolicitud,
                solicitudId.ToString(),
                fechaAccion,
                rolUnidad,
                estadoFinal,
                observaciones);

            if (string.IsNullOrWhiteSpace(emailSolicitante))
            {
                return _queue.RegistrarErrorEventoAsync(
                    eventKey,
                    asunto,
                    "No se encontró correo del solicitante para notificación de rechazo/anulación en solicitud.",
                    solicitudId,
                    null,
                    "SOLICITUD_RECHAZADA_ANULADA").GetAwaiter().GetResult();
            }

            var item = new EmailQueueItem
            {
                Para = emailSolicitante.Trim(),
                Asunto = asunto,
                Cuerpo = body,
                SolicitudId = solicitudId,
                TipoNotificacion = "SOLICITUD_RECHAZADA_ANULADA",
                CorrelationId = eventKey,
                EventKey = eventKey,
                RolOrigen = rolUnidad,
                EstadoFinal = estadoFinal,
                MaxIntentos = 3,
                EsHtml = true
            };

            return _queue.EncolarIdempotenteAsync(item).GetAwaiter().GetResult();
        }
    }
}
