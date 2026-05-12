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
                string smtpServer = ConfigurationManager.AppSettings["SmtpServer"] ?? "172.20.16.21";
                int puerto = int.TryParse(ConfigurationManager.AppSettings["Email:SmtpPort"], out int p) ? p : 25;
                bool useSsl = bool.TryParse(ConfigurationManager.AppSettings["Email:UseSsl"], out bool ssl) && ssl;
                string remitente = ConfigurationManager.AppSettings["EmailFrom"] ?? "aocr@aviacioncivil.gob.ec";
                string nombreRemitente = ConfigurationManager.AppSettings["EmailFromName"] ?? "Sistema AOCR";
                string usuario = ConfigurationManager.AppSettings["Email:Username"] ?? "";
                string password = ConfigurationManager.AppSettings["Email:Password"] ?? "";

                var correo = new MailMessage();
                correo.From = new MailAddress(remitente, nombreRemitente);
                correo.To.Add(destinatario);
                correo.Subject = asunto;
                correo.Body = cuerpoHtml;
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
            string asunto = "Recordatorio: Certificado " + numeroCertificado + " proximo a vencer";

            var model = new EmailTemplateModel
            {
                Titulo = "Recordatorio de vencimiento",
                NombreDestinatario = nombreOperador,
                MensajePrincipal = "Su certificado esta proximo a vencer. Le recomendamos iniciar el proceso de renovacion lo antes posible.",
                Resumen = new List<EmailFieldItem>
                {
                    new EmailFieldItem("Numero de Certificado", numeroCertificado),
                    new EmailFieldItem("Dias restantes", diasRestantes + " dias")
                },
                TextoCierre = "Puede gestionar la renovacion desde el sistema AOCR.",
                Footer = "Este es un correo automatico, por favor no responder."
            };

            string cuerpo = EmailTemplateRenderer.Render(model);
            return EnviarEmail(destinatario, asunto, cuerpo);
        }
    }
}
