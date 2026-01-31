using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CapaDatos.Services
{
    /// <summary>
    /// Resultado de envío de correo - DEFINIR SOLO AQUÍ
    /// </summary>
    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string MessageId { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Interface para servicio de email
    /// </summary>
    public interface IEmailService
    {
        Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null);
    }

    /// <summary>
    /// Servicio de envío de correos
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly ISecureConfigurationService _config;
        private readonly ILoggingService _logger;

        public EmailService(ISecureConfigurationService config)
        {
            _config = config;
            _logger = LoggingServiceFactory.Create();
        }

        /// <summary>Constructor sin parámetros para compatibilidad</summary>
        public EmailService() : this(new SecureConfigurationService()) { }

        public async Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null)
        {
            var creds = _config.GetEmailCredentials();

            try
            {
                using (var client = new SmtpClient(creds.SmtpServer, creds.SmtpPort))
                {
                    client.EnableSsl = creds.UseSsl;
                    if (!string.IsNullOrEmpty(creds.Username))
                    {
                        client.Credentials = new NetworkCredential(creds.Username, creds.Password);
                    }
                    client.Timeout = 30000;

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(creds.FromAddress, creds.FromName);
                        message.To.Add(new MailAddress(para, paraNombre));
                        message.Subject = asunto;
                        message.Body = cuerpo;
                        message.IsBodyHtml = true;

                        if (adjunto != null && adjunto.Length > 0 && !string.IsNullOrEmpty(adjuntoNombre))
                        {
                            var stream = new System.IO.MemoryStream(adjunto);
                            var attachment = new Attachment(stream, adjuntoNombre, "application/octet-stream");
                            message.Attachments.Add(attachment);
                        }

                        await client.SendMailAsync(message);

                        return new EmailSendResult
                        {
                            Success = true,
                            MessageId = Guid.NewGuid().ToString()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "EMAIL_ERROR" });
                return new EmailSendResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        // ============================================================
        // Métodos de compatibilidad usados en la capa de presentación
        // ============================================================
        public void EnviarFacturaGenerada(object orden, byte[] pdfBytes)
        {
            _logger.LogInfo("EnviarFacturaGenerada ejecutado", new LogContext { ErrorCode = "EMAIL_FACTURA" });
        }

        public void EnviarNotificacionRechazo(object orden, string motivo)
        {
            _logger.LogInfo("EnviarNotificacionRechazo ejecutado", new LogContext { ErrorCode = "EMAIL_RECHAZO", AdditionalData = new System.Collections.Generic.Dictionary<string, object> { ["Motivo"] = motivo } });
        }
    }

    /// <summary>
    /// Facade que encola correos
    /// </summary>
    public class QueuedEmailService : IEmailService
    {
        private readonly IEmailQueueService _queueService;
        private readonly ILoggingService _logger;

        public QueuedEmailService(IEmailQueueService queueService)
        {
            _queueService = queueService;
            _logger = LoggingServiceFactory.Create();
        }

        public async Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null)
        {
            try
            {
                var item = new EmailQueueItem
                {
                    Para = para,
                    ParaNombre = paraNombre,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    EsHtml = true,
                    AdjuntoNombre = adjuntoNombre,
                    AdjuntoContenido = adjunto,
                    MaxIntentos = 3
                };

                var id = await _queueService.EncolarAsync(item);

                return new EmailSendResult
                {
                    Success = true,
                    MessageId = "QUEUED-" + id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "QUEUE_ERROR" });
                return new EmailSendResult
                {
                    Success = false,
                    Error = "Error al encolar: " + ex.Message
                };
            }
        }

        // Nota: los métodos de compatibilidad están en EmailService.
    }
}
