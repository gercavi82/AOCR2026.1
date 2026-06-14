using System;
using System.Net;
using System.Net.Mail;
using CapaModelo.Common;

namespace CapaDatos.Services
{
    /// <summary>
    /// Servicio institucional centralizado de correo AOCR.
    /// Todo envío del sistema debe pasar por esta clase para garantizar remitente único.
    /// </summary>
    public class AocrEmailService
    {
        public const string CorreoNoReply = "no_reply@aviacioncivil.gob.ec";
        public const string IpSmtp = "mail.aviacioncivil.gob.ec";
        public const string AliasDefault = "Sistema AOCR";

        private const int DefaultTimeout = 30000;

        private readonly ISecureConfigurationService _config;
        private readonly ILoggingService _logger;

        public AocrEmailService()
            : this(new SecureConfigurationService(), LoggingServiceFactory.Create())
        {
        }

        public AocrEmailService(ISecureConfigurationService config, ILoggingService logger = null)
        {
            _config = config ?? new SecureConfigurationService();
            _logger = logger ?? LoggingServiceFactory.Create();
        }

        public string LastError { get; private set; }

        public bool EnviarMensajeCorreo(string correoPara, string asunto, string mensajeDetalle, string aliasCorreo = null)
        {
            return EnviarMensajeCorreoInterno(correoPara, asunto, mensajeDetalle, aliasCorreo, null, null, null);
        }

        public bool EnviarMensajeCorreoConAdjunto(
            string correoPara,
            string asunto,
            string mensajeDetalle,
            byte[] adjuntoBytes,
            string adjuntoNombre,
            string aliasCorreo = null,
            string mimeType = "application/pdf")
        {
            return EnviarMensajeCorreoInterno(correoPara, asunto, mensajeDetalle, aliasCorreo, adjuntoBytes, adjuntoNombre, mimeType);
        }

        public static string NormalizarRemitenteInstitucional(string remitenteSolicitado)
        {
            return CorreoNoReply;
        }

        public static string NormalizarAlias(string aliasCorreo)
        {
            return string.IsNullOrWhiteSpace(aliasCorreo) ? AliasDefault : aliasCorreo.Trim();
        }

        public static string ResolverAliasPorTipoNotificacion(string tipoNotificacion)
        {
            var tipo = (tipoNotificacion ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(tipo))
            {
                return AliasDefault;
            }

            if (tipo.IndexOf("OBSERV", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("DEVUELT", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("RECHAZ", StringComparison.Ordinal) >= 0)
            {
                return "AOCR - Solicitud observada";
            }

            if (tipo.IndexOf("APROB", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("VALID", StringComparison.Ordinal) >= 0)
            {
                return "AOCR - Documentación aprobada";
            }

            if (tipo.IndexOf("INSPECC", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("INSPECTOR", StringComparison.Ordinal) >= 0)
            {
                return "AOCR - Inspección requerida";
            }

            if (tipo.IndexOf("FIRMA", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("PENDIENTE_FIRMA", StringComparison.Ordinal) >= 0)
            {
                return "AOCR - Firma pendiente";
            }

            if (tipo.IndexOf("DOCUMENTO", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("EMIT", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("FACTURA", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("INFORME", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("LV_", StringComparison.Ordinal) >= 0)
            {
                return "AOCR - Documento emitido";
            }

            if (tipo.IndexOf("ORDEN", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("PAGO", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("FINAN", StringComparison.Ordinal) >= 0)
            {
                return "DGAC - Sistema AOCR";
            }

            if (tipo.IndexOf("AOCR", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("SOLICITUD", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("COORD", StringComparison.Ordinal) >= 0
                || tipo.IndexOf("DIRD", StringComparison.Ordinal) >= 0)
            {
                return "AOCR - Notificaciones";
            }

            return AliasDefault;
        }

        private bool EnviarMensajeCorreoInterno(
            string correoPara,
            string asunto,
            string mensajeDetalle,
            string aliasCorreo,
            byte[] adjuntoBytes,
            string adjuntoNombre,
            string mimeType)
        {
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
            LastError = null;

            try
            {
                if (string.IsNullOrWhiteSpace(correoPara))
                {
                    throw new ArgumentException("El destinatario del correo es obligatorio.");
                }

                if (string.IsNullOrWhiteSpace(asunto))
                {
                    throw new ArgumentException("El asunto del correo es obligatorio.");
                }

                if (string.IsNullOrWhiteSpace(mensajeDetalle))
                {
                    throw new ArgumentException("El cuerpo del correo es obligatorio.");
                }

                var aliasVisible = NormalizarAlias(aliasCorreo);
                var asuntoFinal = asunto.Trim();
                var cuerpoHtml = EmailTemplateRenderer.EnsureStandardLayout(
                    asuntoFinal,
                    mensajeDetalle,
                    null,
                    "Este es un mensaje automatico del workflow AOCR.");

                LogSmtpStart(correlationId, correoPara.Trim(), asuntoFinal, aliasVisible, adjuntoBytes != null && adjuntoBytes.Length > 0);

                using (var correo = new MailMessage())
                {
                    correo.From = new MailAddress(CorreoNoReply, aliasVisible);
                    correo.To.Add(correoPara.Trim());
                    correo.Subject = asuntoFinal;
                    correo.Body = cuerpoHtml;
                    correo.IsBodyHtml = true;
                    correo.Priority = MailPriority.Normal;

                    if (adjuntoBytes != null && adjuntoBytes.Length > 0)
                    {
                        var nombre = string.IsNullOrWhiteSpace(adjuntoNombre) ? "documento.pdf" : adjuntoNombre.Trim();
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
                    string.Format("[AOCR][EMAIL] Correo enviado. Para={0}; From={1}; Asunto={2}", correoPara.Trim(), CorreoNoReply, TruncateForLog(asuntoFinal, 80)),
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
                    AdditionalData =
                    {
                        { "Destinatario", correoPara ?? string.Empty },
                        { "From", CorreoNoReply },
                        { "LastError", LastError ?? string.Empty }
                    }
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
                    AdditionalData =
                    {
                        { "Destinatario", correoPara ?? string.Empty },
                        { "From", CorreoNoReply },
                        { "Asunto", asunto ?? string.Empty }
                    }
                });
                return false;
            }
        }

        private SmtpClient CreateSmtpClient()
        {
            var smtp = new SmtpClient(GetSmtpServer())
            {
                Timeout = DefaultTimeout,
                UseDefaultCredentials = false
            };

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
            catch
            {
            }

            return FirstNonEmpty(_config.GetAppSetting("SmtpServer"), _config.GetAppSetting("SmtpHost")) ?? IpSmtp;
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

        private void LogSmtpStart(string correlationId, string correoPara, string asunto, string aliasVisible, bool conAdjunto)
        {
            try
            {
                var creds = _config.GetEmailCredentials();
                _logger.LogInfo(
                    string.Format(
                        "[AOCR][SMTP_SEND_START] Para={0}; From={1}; Alias={2}; Host={3}; Port={4}; SSL={5}; Adjunto={6}; Asunto={7}",
                        correoPara,
                        CorreoNoReply,
                        aliasVisible,
                        GetSmtpServer(),
                        creds.SmtpPort,
                        creds.UseSsl,
                        conAdjunto,
                        TruncateForLog(asunto, 50)),
                    new LogContext { CorrelationId = correlationId });
            }
            catch
            {
                _logger.LogInfo(
                    string.Format("[AOCR][SMTP_SEND_START] Para={0}; From={1}; Asunto={2}", correoPara, CorreoNoReply, TruncateForLog(asunto, 50)),
                    new LogContext { CorrelationId = correlationId });
            }
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

        private static string TruncateForLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength) + "...";
        }
    }
}
