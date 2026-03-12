using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

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
                string remitente = ConfigurationManager.AppSettings["EmailFrom"] ?? "no_reply@aviacioncivil.gob.ec";
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
            string asunto = $"⚠️ Recordatorio: Certificado {numeroCertificado} próximo a vencer";

            string cuerpo = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #ffc107; color: black; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background-color: #f5f5f5; }}
                        .warning {{ background-color: #fff3cd; padding: 15px; margin: 10px 0; border-left: 4px solid #ffc107; }}
                        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>Recordatorio Importante</h1>
                        </div>
                        <div class='content'>
                            <h2>Estimado/a {nombreOperador},</h2>
                            
                            <div class='warning'>
                                <h3>Su certificado está próximo a vencer</h3>
                                <p><strong>Número de Certificado:</strong> {numeroCertificado}</p>
                                <p><strong>Días restantes:</strong> {diasRestantes} días</p>
                            </div>
                            
                            <p>Le recomendamos iniciar el proceso de renovación lo antes posible.</p>
                        </div>
                        <div class='footer'>
                            <p>Este es un correo automático, por favor no responder.</p>
                            <p>&copy; 2024 Sistema AOCR. Todos los derechos reservados.</p>
                        </div>
                    </div>
                </body>
                </html>";

            return EnviarEmail(destinatario, asunto, cuerpo);
        }
    }
}
