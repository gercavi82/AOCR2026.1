using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace CapaNegocio.Services
{
    public interface IEmailService
    {
        void EnviarConAdjunto(string para, string asunto, string html, byte[] adjuntoBytes, string adjuntoNombre);
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
    }
}
