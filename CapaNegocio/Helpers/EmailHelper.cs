using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using CapaModelo.Common;

namespace CapaNegocio.Helpers
{
    public static class EmailHelper
    {
        // ============================================================
        // MÉTODO GENERAL PARA ENVIAR CORREOS
        // Lee la configuración SMTP desde appSettings del Web.config
        // ============================================================
        public static bool EnviarEmail(string destinatario, string asunto, string cuerpoHtml)
        {
            try
            {
                string smtpServer = ConfigurationManager.AppSettings["SmtpServer"] ?? "mail.aviacioncivil.gob.ec";
                int puerto = int.TryParse(ConfigurationManager.AppSettings["Email:SmtpPort"], out int p) ? p : 25;
                bool useSsl = bool.TryParse(ConfigurationManager.AppSettings["Email:UseSsl"], out bool ssl) && ssl;
                string remitente = ConfigurationManager.AppSettings["EmailFrom"] ?? "no_reply@aviacioncivil.gob.ec";
                string nombreRemitente = ConfigurationManager.AppSettings["EmailFromName"] ?? "Sistema AOCR";
                string usuario = ConfigurationManager.AppSettings["Email:Username"] ?? "";
                string password = ConfigurationManager.AppSettings["Email:Password"] ?? "";

                var correo = new MailMessage();
                correo.From = new MailAddress(remitente, nombreRemitente);
                correo.To.Add(destinatario);
                correo.Subject = asunto;
                correo.Body = EmailTemplateRenderer.EnsureStandardLayout(
                    asunto,
                    cuerpoHtml,
                    null,
                    "Este es un mensaje automatico del workflow AOCR.");
                correo.IsBodyHtml = true;

                var cliente = new SmtpClient(smtpServer, puerto);
                cliente.EnableSsl = useSsl;

                if (!string.IsNullOrEmpty(usuario))
                    cliente.Credentials = new NetworkCredential(usuario, password);
                else
                    cliente.UseDefaultCredentials = false;

                cliente.Send(correo);
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al enviar correo: " + ex.Message, ex);
            }
        }

        // ============================================================
        // RECORDATORIO DE CERTIFICADO POR VENCER
        // ============================================================
        public static bool EnviarRecordatorioVencimiento(string destinatario, string nombreOperador, string numeroCertificado, int diasRestantes)
        {
            string asunto = "Recordatorio: Certificado " + numeroCertificado + " próximo a vencer";

            var model = new EmailTemplateModel
            {
                Titulo = "Recordatorio de vencimiento",
                NombreDestinatario = nombreOperador,
                MensajePrincipal = "Su certificado está próximo a vencer. Le recomendamos iniciar el proceso de renovación lo antes posible.",
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Número de certificado", numeroCertificado),
                    new EmailFieldItem("Días restantes", diasRestantes + " días")
                },
                TextoCierre = "Puede gestionar la renovación desde el sistema AOCR.",
                Footer = "Este es un correo automático, por favor no responder."
            };

            string cuerpo = EmailTemplateRenderer.Render(model);
            return EnviarEmail(destinatario, asunto, cuerpo);
        }
    }
}
