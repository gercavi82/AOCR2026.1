using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapaDatos.Constants;
using CapaDatos.Services;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Encolado idempotente de correos por evento de flujo AOCR.
    /// </summary>
    public sealed class AocrEmailFlujoService
    {
        private readonly IEmailQueueService _emailQueue;

        public AocrEmailFlujoService()
            : this(new EmailQueueService())
        {
        }

        public AocrEmailFlujoService(IEmailQueueService emailQueue)
        {
            _emailQueue = emailQueue ?? new EmailQueueService();
        }

        public async Task<bool> EncolarSiNoDuplicadoAsync(
            string eventoFlujo,
            int? codigoSolicitud,
            int? codigoOrden,
            string destinatario,
            string asunto,
            string cuerpoHtml,
            string correlationId = null)
        {
            if (string.IsNullOrWhiteSpace(destinatario) || string.IsNullOrWhiteSpace(asunto))
            {
                return false;
            }

            var eventKey = SolicitudAocrCorreoService.BuildAocrEventKey(
                eventoFlujo,
                codigoSolicitud.GetValueOrDefault(),
                null,
                correlationId,
                destinatario);

            if (await _emailQueue.ExisteNotificacionAsync(eventoFlujo, eventKey, codigoSolicitud).ConfigureAwait(false))
            {
                return false;
            }

            var item = new EmailQueueItem
            {
                Para = destinatario.Trim(),
                Asunto = asunto.Trim(),
                Cuerpo = cuerpoHtml ?? string.Empty,
                EsHtml = true,
                SolicitudId = codigoSolicitud,
                OrdenId = codigoOrden,
                EventKey = eventKey,
                TipoNotificacion = eventoFlujo,
                CorrelationId = correlationId,
                Estado = EstadoEmail.Pendiente,
                FechaCreacion = DateTime.UtcNow,
                MaxIntentos = 5
            };

            await _emailQueue.EncolarAsync(item).ConfigureAwait(false);
            return true;
        }

        public static IReadOnlyList<string> EventosFlujoInstitucionales { get; } = new[]
        {
            "ORDEN_GENERADA",
            "PAGO_OBSERVADO",
            "PAGO_APROBADO",
            "SOLICITUD_AOCR_HABILITADA",
            "DOCUMENTACION_ENVIADA",
            "DOCUMENTACION_OBSERVADA",
            "DOCUMENTACION_SUBSANADA",
            "INSPECTOR_ASIGNADO",
            "SOLICITUD_INSPECCION_GENERADA",
            "SOLICITUD_INSPECCION_FIRMADA",
            "LV_FINALIZADA",
            "LV_FIRMADA",
            "INFORME_TECNICO_GENERADO",
            "INFORME_TECNICO_FIRMADO",
            "INFORME_TECNICO_SATISFACTORIO",
            "INFORME_TECNICO_NO_SATISFACTORIO",
            "NC_GENERADA",
            "SUBSANACION_REQUERIDA",
            "NUEVA_INSPECCION_REQUERIDA",
            "AOCR_GENERADO",
            "AOCR_ENVIADO_COORDINACION",
            "AOCR_ENVIADO_DIRDAC",
            "AOCR_FIRMADO",
            "CONDICIONES_FIRMADAS",
            "DOCUMENTOS_FINALES_DISPONIBLES",
            "TRAMITE_CERRADO"
        };
    }
}
