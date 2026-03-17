using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Configuration;
using System.Text.RegularExpressions;
using CapaDatos.Models;
using System.Threading.Tasks;
using CapaNegocio.Services;

namespace CapaDatos.Services
{
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

        public async Task<EmailSendResult> EnviarAsync(string para, string paraNombre, string asunto, string cuerpo,
            byte[] adjunto = null, string adjuntoNombre = null)
        {
            var creds = _config.GetEmailCredentials();

            try
            {
                using (var client = new SmtpClient(creds.SmtpServer, creds.SmtpPort))
                {
                    client.EnableSsl = creds.UseSsl;
                    client.Credentials = new NetworkCredential(creds.Username, creds.Password);
                    client.Timeout = 30000; // 30 segundos

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
            catch (SmtpException ex)
            {
                _logger.LogError(ex, new LogContext { ErrorCode = "SMTP_ERROR" });
                return new EmailSendResult
                {
                    Success = false,
                    Error = "Error SMTP: " + ex.StatusCode
                };
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
    }

    /// <summary>
    /// Facade que encola correos en lugar de enviarlos directamente
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

                _logger.LogInfo(string.Format("Email encolado con ID {0} para {1}", id, para));

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
    }

    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string MessageId { get; set; }
        public string Error { get; set; }
    }

    public class EmailQueueItem
    {
        public string Para { get; set; }
        public string ParaNombre { get; set; }
        public string Asunto { get; set; }
        public string Cuerpo { get; set; }
        public bool EsHtml { get; set; }
        public string AdjuntoNombre { get; set; }
        public byte[] AdjuntoContenido { get; set; }
        public int MaxIntentos { get; set; }
    }
}
