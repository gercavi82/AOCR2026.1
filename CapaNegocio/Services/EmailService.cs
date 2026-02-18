using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CapaNegocio.Services
{
    public class EmailSendResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        public static EmailSendResult Ok()
        {
            return new EmailSendResult { Success = true };
        }

        public static EmailSendResult Fail(string error)
        {
            return new EmailSendResult { Success = false, Error = error };
        }
    }

    public interface IEmailService
    {
        void EnviarConAdjunto(string para, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre);
        Task<EmailSendResult> EnviarAsync(string para, string nombrePara, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre);
    }

    public class EmailService : IEmailService
    {
        private static readonly Regex EmailRegex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);

        public void EnviarConAdjunto(string para, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre)
        {
            if (string.IsNullOrWhiteSpace(para) || !EmailRegex.IsMatch(para))
                throw new ArgumentException("Correo destino inválido.");

            if (string.IsNullOrWhiteSpace(asunto))
                throw new ArgumentException("Asunto requerido.");

            if (adjuntoBytes == null || adjuntoBytes.Length == 0)
                throw new ArgumentException("Adjunto vacío.");

            adjuntoNombre = string.IsNullOrWhiteSpace(adjuntoNombre) ? "documento.pdf" : adjuntoNombre;

            // ✅ Producción: usa config + credenciales seguras (no hardcode)
            var host = ConfigurationManager.AppSettings["SmtpHost"];
            var portStr = ConfigurationManager.AppSettings["SmtpPort"];
            var user = ConfigurationManager.AppSettings["SmtpUser"];
            var pass = ConfigurationManager.AppSettings["SmtpPass"]; // ideal: variable de entorno / secret manager
            var from = ConfigurationManager.AppSettings["MailFrom"];
            var enableSslStr = ConfigurationManager.AppSettings["SmtpEnableSsl"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
                throw new Exception("SMTP no configurado (SmtpHost/MailFrom).");

            int port = 587;
            int.TryParse(portStr, out port);

            bool enableSsl = true;
            bool.TryParse(enableSslStr, out enableSsl);

            using (var msg = new MailMessage())
            {
                msg.From = new MailAddress(from, "AOCR");
                msg.To.Add(para);
                msg.Subject = asunto;
                msg.IsBodyHtml = true;
                msg.Body = html ?? "";

                // Adjuntar PDF desde memoria (sin tocar disco)
                using (var ms = new System.IO.MemoryStream(adjuntoBytes))
                using (var attachment = new Attachment(ms, adjuntoNombre, "application/pdf"))
                {
                    msg.Attachments.Add(attachment);

                    using (var smtp = new SmtpClient(host, port))
                    {
                        smtp.EnableSsl = enableSsl;

                        // Si tu servidor requiere auth
                        if (!string.IsNullOrWhiteSpace(user))
                            smtp.Credentials = new NetworkCredential(user, pass);

                        smtp.Send(msg);
                    }
                }
            }
        }

        public Task<EmailSendResult> EnviarAsync(string para, string nombrePara, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre)
        {
            try
            {
                // Si hay adjunto, reutiliza el flujo existente
                if (adjuntoBytes != null && adjuntoBytes.Length > 0)
                {
                    CapaNegocio.LogBL.RegistrarInfo($"EMAIL_ENVIAR | para={para} | adjunto={adjuntoNombre} | bytes={adjuntoBytes.Length}", "EmailService");
                    EnviarConAdjunto(para, asunto, html, adjuntoBytes, adjuntoNombre);
                    CapaNegocio.LogBL.RegistrarInfo($"EMAIL_OK | para={para} | adjunto={adjuntoNombre}", "EmailService");
                    return Task.FromResult(EmailSendResult.Ok());
                }

                if (string.IsNullOrWhiteSpace(para) || !EmailRegex.IsMatch(para))
                    return Task.FromResult(EmailSendResult.Fail("Correo destino inválido"));

                if (string.IsNullOrWhiteSpace(asunto))
                    return Task.FromResult(EmailSendResult.Fail("Asunto requerido"));

                var host = ConfigurationManager.AppSettings["SmtpHost"];
                var portStr = ConfigurationManager.AppSettings["SmtpPort"];
                var user = ConfigurationManager.AppSettings["SmtpUser"];
                var pass = ConfigurationManager.AppSettings["SmtpPass"];
                var from = ConfigurationManager.AppSettings["MailFrom"];
                var enableSslStr = ConfigurationManager.AppSettings["SmtpEnableSsl"];

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
                    return Task.FromResult(EmailSendResult.Fail("SMTP no configurado (SmtpHost/MailFrom)."));

                int port = 587;
                int.TryParse(portStr, out port);

                bool enableSsl = true;
                bool.TryParse(enableSslStr, out enableSsl);

                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(from, "AOCR");
                    msg.To.Add(para);
                    msg.Subject = asunto;
                    msg.IsBodyHtml = true;
                    msg.Body = html ?? "";

                    using (var smtp = new SmtpClient(host, port))
                    {
                        smtp.EnableSsl = enableSsl;
                        if (!string.IsNullOrWhiteSpace(user))
                            smtp.Credentials = new NetworkCredential(user, pass);

                        CapaNegocio.LogBL.RegistrarInfo($"EMAIL_ENVIAR | para={para} | adjunto=none", "EmailService");
                        smtp.Send(msg);
                        CapaNegocio.LogBL.RegistrarInfo($"EMAIL_OK | para={para} | adjunto=none", "EmailService");
                    }
                }

                return Task.FromResult(EmailSendResult.Ok());
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"EMAIL_ERROR | para={para}", ex.Message, "EmailService");
                return Task.FromResult(EmailSendResult.Fail(ex.Message));
            }
        }
    }
}
