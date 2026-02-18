using System;
using System.Net.Mail;

namespace CapaDatos.Services
{
    /// <summary>
    /// Servicio de envío de correos refactorizado.
    /// Usa configuración segura y logging estructurado.
    /// </summary>
    public class EnviarCorreo
    {
        private readonly ISecureConfigurationService _config;
        private readonly ILoggingService _logger;
        private readonly IEmailQueueService _queueService;

        // Configuración por defecto (fallback)
        private const string DefaultSmtpServer = "172.20.16.21";
        private const string DefaultFromAddress = "no_reply@aviacioncivil.gob.ec";
        private const int DefaultTimeout = 30000; // 30 segundos

        #region Constructores

        /// <summary>
        /// Constructor con inyección de dependencias (recomendado)
        /// </summary>
        public EnviarCorreo(ISecureConfigurationService config, IEmailQueueService queueService = null)
        {
            _config = config;
            _queueService = queueService;
            _logger = LoggingServiceFactory.Create();
        }

        /// <summary>
        /// Constructor legacy para compatibilidad (usar solo si es necesario)
        /// </summary>
        public EnviarCorreo()
        {
            _config = new SecureConfigurationService();
            _logger = LoggingServiceFactory.Create();
        }

        #endregion

        #region Métodos Públicos - Envío Directo (Legacy)

        /// <summary>
        /// Envía correo de forma síncrona (método legacy - mantiene firma original)
        /// </summary>
        public bool enviaMensajeCorreo(string coreoPara, string asunto, string mensajeDetalle)
        {
            return enviaMensajeCorreoDesde(GetDefaultFromAddress(), coreoPara, asunto, mensajeDetalle);
        }

        /// <summary>
        /// Envía correo con remitente personalizado (método legacy - mantiene firma original)
        /// </summary>
        public bool enviaMensajeCorreoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle)
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

            try
            {
                // Validar parámetros
                if (string.IsNullOrWhiteSpace(coreoPara))
                {
                    _logger.LogWarning("Intento de envío sin destinatario", 
                        new LogContext { CorrelationId = correlationId });
                    return false;
                }

                if (string.IsNullOrWhiteSpace(asunto))
                {
                    asunto = "Notificación - Sistema AOCR";
                }

                _logger.LogInfo(
                    string.Format("Enviando correo a {0}, Asunto: {1}", coreoPara, TruncateForLog(asunto, 50)),
                    new LogContext { CorrelationId = correlationId });

                using (var correo = new MailMessage())
                {
                    correo.From = new MailAddress(coreoDesde ?? GetDefaultFromAddress());
                    correo.To.Add(coreoPara);
                    correo.Subject = asunto;
                    correo.Body = mensajeDetalle;
                    correo.IsBodyHtml = true;
                    correo.Priority = MailPriority.Normal;

                    using (var smtp = CreateSmtpClient())
                    {
                        smtp.Send(correo);
                    }
                }

                _logger.LogInfo(
                    string.Format("Correo enviado exitosamente a {0}", coreoPara),
                    new LogContext { CorrelationId = correlationId });

                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = "SMTP_ERROR",
                    AdditionalData = { { "Destinatario", coreoPara }, { "StatusCode", ex.StatusCode.ToString() } }
                });
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = "EMAIL_ERROR",
                    AdditionalData = { { "Destinatario", coreoPara } }
                });
                return false;
            }
        }

        /// <summary>
        /// Envía correo con adjunto (método legacy para adjuntos)
        /// </summary>
        public bool enviaMensajeCorreoConAdjunto(string coreoPara, string asunto, string mensajeDetalle, byte[] adjuntoBytes, string adjuntoNombre, string mimeType = "application/pdf")
        {
            return enviaMensajeCorreoConAdjuntoDesde(GetDefaultFromAddress(), coreoPara, asunto, mensajeDetalle, adjuntoBytes, adjuntoNombre, mimeType);
        }

        /// <summary>
        /// Envía correo con adjunto y remitente personalizado
        /// </summary>
        public bool enviaMensajeCorreoConAdjuntoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle, byte[] adjuntoBytes, string adjuntoNombre, string mimeType = "application/pdf")
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

            try
            {
                if (string.IsNullOrWhiteSpace(coreoPara))
                {
                    _logger.LogWarning("Intento de envío con adjunto sin destinatario",
                        new LogContext { CorrelationId = correlationId });
                    return false;
                }

                if (string.IsNullOrWhiteSpace(asunto))
                    asunto = "Notificación - Sistema AOCR";

                using (var correo = new MailMessage())
                {
                    correo.From = new MailAddress(coreoDesde ?? GetDefaultFromAddress());
                    correo.To.Add(coreoPara);
                    correo.Subject = asunto;
                    correo.Body = mensajeDetalle;
                    correo.IsBodyHtml = true;
                    correo.Priority = MailPriority.Normal;

                    if (adjuntoBytes != null && adjuntoBytes.Length > 0)
                    {
                        var nombre = string.IsNullOrWhiteSpace(adjuntoNombre) ? "documento.pdf" : adjuntoNombre;
                        var stream = new System.IO.MemoryStream(adjuntoBytes);
                        var attachment = new Attachment(stream, nombre, mimeType ?? "application/octet-stream");
                        correo.Attachments.Add(attachment);
                    }

                    using (var smtp = CreateSmtpClient())
                    {
                        smtp.Send(correo);
                    }
                }

                _logger.LogInfo(
                    string.Format("Correo con adjunto enviado exitosamente a {0}", coreoPara),
                    new LogContext { CorrelationId = correlationId });

                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = "SMTP_ERROR_ADJUNTO",
                    AdditionalData = { { "Destinatario", coreoPara }, { "StatusCode", ex.StatusCode.ToString() } }
                });
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = "EMAIL_ADJUNTO_ERROR",
                    AdditionalData = { { "Destinatario", coreoPara } }
                });
                return false;
            }
        }

        #endregion

        #region Métodos Públicos - Envío Encolado (Recomendado)

        /// <summary>
        /// Encola correo para envío asíncrono (NO BLOQUEA)
        /// </summary>
        public bool EnviarEncolado(string coreoPara, string asunto, string mensajeDetalle, 
            int? ordenId = null, string tipoNotificacion = null)
        {
            return EnviarEncoladoDesde(GetDefaultFromAddress(), coreoPara, asunto, mensajeDetalle, ordenId, tipoNotificacion);
        }

        /// <summary>
        /// Encola correo con remitente personalizado (NO BLOQUEA)
        /// </summary>
        public bool EnviarEncoladoDesde(string coreoDesde, string coreoPara, string asunto, string mensajeDetalle,
            int? ordenId = null, string tipoNotificacion = null)
        {
            if (_queueService == null)
            {
                // Fallback a envío directo si no hay cola configurada
                _logger.LogWarning("Cola no disponible, usando envío directo");
                return enviaMensajeCorreoDesde(coreoDesde, coreoPara, asunto, mensajeDetalle);
            }

            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);

            try
            {
                var item = new EmailQueueItem
                {
                    Para = coreoPara,
                    Asunto = asunto,
                    Cuerpo = mensajeDetalle,
                    EsHtml = true,
                    MaxIntentos = 3,
                    CorrelationId = correlationId,
                    SolicitudId = ordenId,
                    TipoNotificacion = tipoNotificacion
                };

                var queueId = _queueService.EncolarAsync(item).Result;

                _logger.LogInfo(
                    string.Format("Correo encolado: ID={0}, Para={1}", queueId, coreoPara),
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

        #endregion

        #region Métodos Privados

        private SmtpClient CreateSmtpClient()
        {
            var server = GetSmtpServer();
            var smtp = new SmtpClient(server);
            smtp.Timeout = DefaultTimeout;

            // Si hay credenciales configuradas, usarlas
            try
            {
                var creds = _config.GetEmailCredentials();
                if (!string.IsNullOrEmpty(creds.Username) && !string.IsNullOrEmpty(creds.Password))
                {
                    smtp.Credentials = new System.Net.NetworkCredential(creds.Username, creds.Password);
                    smtp.EnableSsl = creds.UseSsl;
                    smtp.Port = creds.SmtpPort;
                }
            }
            catch
            {
                // Usar configuración sin autenticación (relay interno)
            }

            return smtp;
        }

        private string GetSmtpServer()
        {
            try
            {
                var creds = _config.GetEmailCredentials();
                if (!string.IsNullOrEmpty(creds.SmtpServer))
                {
                    return creds.SmtpServer;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo SmtpServer desde config segura: {ex.Message}");
                // Continuar con fallback
            }

            // Fallback a servidor por defecto
            return _config.GetAppSetting("SmtpServer") ?? DefaultSmtpServer;
        }

        private string GetDefaultFromAddress()
        {
            try
            {
                var creds = _config.GetEmailCredentials();
                if (!string.IsNullOrEmpty(creds.FromAddress))
                {
                    return creds.FromAddress;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo FromAddress desde config segura: {ex.Message}");
                // Continuar con fallback
            }

            return _config.GetAppSetting("EmailFrom") ?? DefaultFromAddress;
        }

        private string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }
            return text.Substring(0, maxLength) + "...";
        }

        #endregion
    }
}
