using System;
using System.Net;
using System.Net.Mail;
using CapaModelo.Common;

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
        public string LastError { get; private set; }

        // Configuración por defecto (fallback)
        private const string DefaultSmtpServer = "mail.aviacioncivil.gob.ec";
        private const string DefaultFromAddress = "sistema@dgac.gob.ec";
        private const string DefaultFromName = "aocr@aviacioncivil.gob.ec";
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
            LastError = null;

            try
            {
                // Validar parámetros
                if (string.IsNullOrWhiteSpace(coreoPara))
                {
                    LastError = "No se puede enviar el correo sin destinatario.";
                    _logger.LogWarning("Intento de envío sin destinatario", 
                        new LogContext { CorrelationId = correlationId });
                    return false;
                }

                if (string.IsNullOrWhiteSpace(asunto))
                {
                    asunto = "Notificación - Sistema AOCR";
                }

                var fromAddress = GetEffectiveFromAddress(coreoDesde);
                LogSmtpStart(correlationId, coreoPara, asunto, fromAddress, false);

                using (var correo = new MailMessage())
                {
                    correo.From = new MailAddress(fromAddress, GetDefaultFromName());
                    AddReplyToIfDifferent(correo, coreoDesde, fromAddress);
                    correo.To.Add(coreoPara);
                    correo.Subject = asunto;
                    correo.Body = EmailTemplateRenderer.EnsureStandardLayout(
                        asunto,
                        mensajeDetalle,
                        null,
                        "Este es un mensaje automatico del workflow AOCR.");
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
                LastError = BuildSmtpError(ex);
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = IsPermanentSmtpConfigurationError(ex) ? "ERROR_CONFIG_SMTP" : "SMTP_ERROR",
                    AdditionalData = { { "Destinatario", coreoPara }, { "StatusCode", ex.StatusCode.ToString() }, { "LastError", LastError } }
                });
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
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
            LastError = null;

            try
            {
                if (string.IsNullOrWhiteSpace(coreoPara))
                {
                    LastError = "No se puede enviar el correo sin destinatario.";
                    _logger.LogWarning("Intento de envío con adjunto sin destinatario",
                        new LogContext { CorrelationId = correlationId });
                    return false;
                }

                if (string.IsNullOrWhiteSpace(asunto))
                    asunto = "Notificación - Sistema AOCR";

                var fromAddress = GetEffectiveFromAddress(coreoDesde);
                LogSmtpStart(correlationId, coreoPara, asunto, fromAddress, true);

                using (var correo = new MailMessage())
                {
                    correo.From = new MailAddress(fromAddress, GetDefaultFromName());
                    AddReplyToIfDifferent(correo, coreoDesde, fromAddress);
                    correo.To.Add(coreoPara);
                    correo.Subject = asunto;
                    correo.Body = EmailTemplateRenderer.EnsureStandardLayout(
                        asunto,
                        mensajeDetalle,
                        null,
                        "Este es un mensaje automatico del workflow AOCR.");
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
                LastError = BuildSmtpError(ex);
                _logger.LogError(ex, new LogContext
                {
                    CorrelationId = correlationId,
                    ErrorCode = IsPermanentSmtpConfigurationError(ex) ? "ERROR_CONFIG_SMTP" : "SMTP_ERROR_ADJUNTO",
                    AdditionalData = { { "Destinatario", coreoPara }, { "StatusCode", ex.StatusCode.ToString() }, { "LastError", LastError } }
                });
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
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
                    OrdenId = ordenId,
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
            smtp.UseDefaultCredentials = false;

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

            try
            {
                var creds = _config.GetEmailCredentials();
                smtp.Port = creds.SmtpPort > 0 ? creds.SmtpPort : GetConfiguredSmtpPort();
                smtp.EnableSsl = creds.UseSsl;
                if (!string.IsNullOrEmpty(creds.Username) && !string.IsNullOrEmpty(creds.Password))
                {
                    smtp.Credentials = new NetworkCredential(creds.Username, creds.Password);
                }
            }
            catch
            {
                smtp.Port = GetConfiguredSmtpPort();
                smtp.EnableSsl = GetConfiguredSmtpSsl();
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

            var configured = FirstNonEmpty(
                _config.GetAppSetting("EmailFrom"),
                _config.GetAppSetting("FromEmail"),
                _config.GetAppSetting("MailFrom"));

            return string.IsNullOrWhiteSpace(configured) ? DefaultFromAddress : configured.Trim();
        }

        private string GetDefaultFromName()
        {
            try
            {
                var creds = _config.GetEmailCredentials();
                if (!string.IsNullOrWhiteSpace(creds.FromName))
                {
                    return creds.FromName.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo FromName desde config segura: {ex.Message}");
            }

            var configured = _config.GetAppSetting("EmailFromName");
            return string.IsNullOrWhiteSpace(configured) ? DefaultFromName : configured.Trim();
        }

        private string GetEffectiveFromAddress(string requestedFrom)
        {
            var creds = _config.GetEmailCredentials();
            var configuredFrom = FirstNonEmpty(
                creds != null ? creds.FromAddress : null,
                _config.GetAppSetting("EmailFrom"),
                _config.GetAppSetting("FromEmail"),
                _config.GetAppSetting("MailFrom"));

            if (!string.IsNullOrWhiteSpace(configuredFrom))
            {
                return configuredFrom.Trim();
            }

            if (creds != null && !string.IsNullOrWhiteSpace(creds.Username))
            {
                return creds.Username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(requestedFrom) &&
                !IsRejectedAviacionCivilNoAuthAddress(requestedFrom))
            {
                return requestedFrom.Trim();
            }

            return DefaultFromAddress;
        }

        private void AddReplyToIfDifferent(MailMessage correo, string requestedFrom, string effectiveFrom)
        {
            if (correo == null || string.IsNullOrWhiteSpace(requestedFrom))
            {
                return;
            }

            if (string.Equals(requestedFrom.Trim(), effectiveFrom ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                correo.ReplyToList.Add(new MailAddress(requestedFrom.Trim()));
            }
            catch
            {
            }
        }

        private void LogSmtpStart(string correlationId, string coreoPara, string asunto, string fromAddress, bool conAdjunto)
        {
            try
            {
                var creds = _config.GetEmailCredentials();
                _logger.LogInfo(
                    string.Format("[AOCR][SMTP_SEND_START] Para={0}; From={1}; Host={2}; Port={3}; SSL={4}; AuthConfigured={5}; Adjunto={6}; Asunto={7}",
                        coreoPara,
                        fromAddress,
                        GetSmtpServer(),
                        creds.SmtpPort,
                        creds.UseSsl,
                        !string.IsNullOrWhiteSpace(creds.Username) && !string.IsNullOrWhiteSpace(creds.Password),
                        conAdjunto,
                        TruncateForLog(asunto, 50)),
                    new LogContext { CorrelationId = correlationId });
            }
            catch
            {
                _logger.LogInfo(
                    string.Format("[AOCR][SMTP_SEND_START] Para={0}; From={1}; Asunto={2}", coreoPara, fromAddress, TruncateForLog(asunto, 50)),
                    new LogContext { CorrelationId = correlationId });
            }
        }

        private int GetConfiguredSmtpPort()
        {
            int port;
            return int.TryParse(FirstNonEmpty(_config.GetAppSetting("Email:SmtpPort"), _config.GetAppSetting("SmtpPort")), out port) && port > 0
                ? port
                : 25;
        }

        private bool GetConfiguredSmtpSsl()
        {
            bool ssl;
            return bool.TryParse(FirstNonEmpty(_config.GetAppSetting("Email:UseSsl"), _config.GetAppSetting("SmtpEnableSsl")), out ssl) && ssl;
        }

        private static string BuildSmtpError(SmtpException ex)
        {
            var detalle = string.Format("SMTP {0}: {1}", ex.StatusCode, ex.Message);
            return IsPermanentSmtpConfigurationError(ex)
                ? "ERROR_CONFIG_SMTP: " + detalle
                : detalle;
        }

        private static bool IsPermanentSmtpConfigurationError(SmtpException ex)
        {
            if (ex == null)
            {
                return false;
            }

            var detalle = ((ex.Message ?? string.Empty) + " " + ex.StatusCode).Trim();
            return detalle.IndexOf("5.7.1", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("MailboxNameNotAllowed", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("sender address rejected", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("not logged in", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("client was not authenticated", StringComparison.OrdinalIgnoreCase) >= 0
                || detalle.IndexOf("authentication", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRejectedAviacionCivilNoAuthAddress(string address)
        {
            return string.Equals((address ?? string.Empty).Trim(), "no_reply@aviacioncivil.gob.ec", StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
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
