using System;
using CapaModelo.Common;

namespace CapaDatos.Services
{
    /// <summary>
    /// Fachada legacy de envío de correos. Delega en AocrEmailService para garantizar remitente institucional.
    /// </summary>
    public class EnviarCorreo
    {
        private readonly AocrEmailService _emailService;
        private readonly IEmailQueueService _queueService;
        private readonly ILoggingService _logger;

        public string LastError { get; private set; }

        public EnviarCorreo(ISecureConfigurationService config, IEmailQueueService queueService = null)
        {
            _emailService = new AocrEmailService(config);
            _queueService = queueService;
            _logger = LoggingServiceFactory.Create();
        }

        public EnviarCorreo()
        {
            _emailService = new AocrEmailService();
            _logger = LoggingServiceFactory.Create();
        }

        public bool enviaMensajeCorreo(string coreoPara, string asunto, string mensajeDetalle)
        {
            return enviaMensajeCorreo(coreoPara, asunto, mensajeDetalle, null);
        }

        public bool enviaMensajeCorreo(string coreoPara, string asunto, string mensajeDetalle, string aliasCorreo)
        {
            LastError = null;
            var enviado = _emailService.EnviarMensajeCorreo(coreoPara, asunto, mensajeDetalle, aliasCorreo);
            if (!enviado)
            {
                LastError = _emailService.LastError;
            }

            return enviado;
        }

        /// <summary>
        /// Compatibilidad legacy: ignora el remitente solicitado y usa no_reply@aviacioncivil.gob.ec.
        /// </summary>
        public bool enviaMensajeCorreoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle)
        {
            return enviaMensajeCorreo(coreoPara, asunto, mensajeDetalle, null);
        }

        public bool enviaMensajeCorreoConAdjunto(string coreoPara, string asunto, string mensajeDetalle, byte[] adjuntoBytes, string adjuntoNombre, string mimeType = "application/pdf")
        {
            return enviaMensajeCorreoConAdjunto(coreoPara, asunto, mensajeDetalle, adjuntoBytes, adjuntoNombre, null, mimeType);
        }

        public bool enviaMensajeCorreoConAdjunto(string coreoPara, string asunto, string mensajeDetalle, byte[] adjuntoBytes, string adjuntoNombre, string aliasCorreo, string mimeType = "application/pdf")
        {
            LastError = null;
            var enviado = _emailService.EnviarMensajeCorreoConAdjunto(coreoPara, asunto, mensajeDetalle, adjuntoBytes, adjuntoNombre, aliasCorreo, mimeType);
            if (!enviado)
            {
                LastError = _emailService.LastError;
            }

            return enviado;
        }

        /// <summary>
        /// Compatibilidad legacy: ignora el remitente solicitado y usa no_reply@aviacioncivil.gob.ec.
        /// </summary>
        public bool enviaMensajeCorreoConAdjuntoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle, byte[] adjuntoBytes, string adjuntoNombre, string mimeType = "application/pdf")
        {
            return enviaMensajeCorreoConAdjunto(coreoPara, asunto, mensajeDetalle, adjuntoBytes, adjuntoNombre, null, mimeType);
        }

        public bool EnviarEncolado(string coreoPara, string asunto, string mensajeDetalle, int? ordenId = null, string tipoNotificacion = null)
        {
            return EnviarEncolado(coreoPara, asunto, mensajeDetalle, null, ordenId, tipoNotificacion);
        }

        public bool EnviarEncolado(string coreoPara, string asunto, string mensajeDetalle, string aliasCorreo, int? ordenId = null, string tipoNotificacion = null)
        {
            return EnviarEncoladoDesde(null, coreoPara, asunto, mensajeDetalle, aliasCorreo, ordenId, tipoNotificacion);
        }

        /// <summary>
        /// Compatibilidad legacy: ignora el remitente solicitado y fuerza remitente institucional en cola.
        /// </summary>
        public bool EnviarEncoladoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle, int? ordenId = null, string tipoNotificacion = null)
        {
            return EnviarEncoladoDesde(coreoDesde, coreoPara, asunto, mensajeDetalle, null, ordenId, tipoNotificacion);
        }

        public bool EnviarEncoladoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle, string aliasCorreo, int? ordenId = null, string tipoNotificacion = null)
        {
            if (_queueService == null)
            {
                _logger.LogWarning("Cola no disponible, usando envío directo institucional");
                return enviaMensajeCorreo(coreoPara, asunto, mensajeDetalle, aliasCorreo);
            }

            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

            try
            {
                var aliasFinal = AocrEmailService.NormalizarAlias(
                    string.IsNullOrWhiteSpace(aliasCorreo)
                        ? AocrEmailService.ResolverAliasPorTipoNotificacion(tipoNotificacion)
                        : aliasCorreo);

                var item = new EmailQueueItem
                {
                    Para = coreoPara,
                    Asunto = asunto,
                    Cuerpo = mensajeDetalle,
                    EsHtml = true,
                    MaxIntentos = 3,
                    CorrelationId = correlationId,
                    OrdenId = ordenId,
                    TipoNotificacion = tipoNotificacion,
                    Remitente = AocrEmailService.CorreoNoReply,
                    AliasRemitente = aliasFinal
                };

                var queueId = _queueService.EncolarAsync(item).Result;

                _logger.LogInfo(
                    string.Format("Correo encolado: ID={0}, Para={1}, From={2}", queueId, coreoPara, AocrEmailService.CorreoNoReply),
                    new LogContext { CorrelationId = correlationId });

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = "QUEUE_ERROR"
                });
                return false;
            }
        }
    }
}
