using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using CapaDatos.Models;

namespace CapaDatos.Services
{
    public class EmailService
    {
        private readonly SmtpClient _smtpClient;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService()
        {
            var config = System.Configuration.ConfigurationManager.AppSettings;

            _smtpClient = new SmtpClient
            {
                Host = config["SmtpHost"] ?? "smtp.gmail.com",
                Port = Convert.ToInt32(config["SmtpPort"] ?? "587"),
                EnableSsl = Convert.ToBoolean(config["SmtpEnableSsl"] ?? "true"),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    config["SmtpUsername"],
                    config["SmtpPassword"]
                ),
                Timeout = 30000
            };

            _fromEmail = config["FromEmail"] ?? "sistema@dgac.gob.ec";
            _fromName = config["FromName"] ?? "Sistema AOCR - DGAC Ecuador";
        }

        public bool EnviarOrdenRecaudacion(OrdenRecaudacionModel orden, byte[] pdfAdjunto)
        {
            try
            {
                if (string.IsNullOrEmpty(orden.Contribuyente.Email))
                {
                    throw new Exception("El contribuyente no tiene email registrado");
                }

                var mail = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = $"Orden de Recaudación {orden.NumeroOrden} - DGAC Ecuador",
                    Body = CrearCuerpoEmail(orden),
                    IsBodyHtml = true,
                    Priority = MailPriority.High
                };

                mail.To.Add(orden.Contribuyente.Email);

                // Adjuntar PDF
                if (pdfAdjunto != null && pdfAdjunto.Length > 0)
                {
                    var attachment = new Attachment(new System.IO.MemoryStream(pdfAdjunto),
                        $"Orden_{orden.NumeroOrden}.pdf",
                        "application/pdf");
                    mail.Attachments.Add(attachment);
                }

                // Enviar copia al usuario que genera la orden
                var usuarioEmail = System.Configuration.ConfigurationManager.AppSettings["UsuarioEmail"];
                if (!string.IsNullOrEmpty(usuarioEmail))
                {
                    mail.Bcc.Add(usuarioEmail);
                }

                _smtpClient.Send(mail);

                return true;
            }
            catch (Exception ex)
            {
                // Log del error
                System.Diagnostics.Trace.TraceError($"Error enviando email: {ex.Message}");
                return false;
            }
        }

        private string CrearCuerpoEmail(OrdenRecaudacionModel orden)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
            sb.AppendLine(".header { background-color: #1B4F72; color: white; padding: 20px; text-align: center; }");
            sb.AppendLine(".content { padding: 20px; }");
            sb.AppendLine(".info-box { border: 1px solid #ddd; padding: 15px; margin: 10px 0; background-color: #f9f9f9; }");
            sb.AppendLine(".total-box { background-color: #e8f5e8; border-left: 4px solid #28a745; padding: 15px; }");
            sb.AppendLine(".footer { margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; color: #666; font-size: 12px; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("<div class='header'>");
            sb.AppendLine("<h1>Dirección General de Aviación Civil - Ecuador</h1>");
            sb.AppendLine("<h2>ORDEN DE RECAUDACIÓN</h2>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='content'>");
            sb.AppendLine("<p>Estimado/a contribuyente,</p>");
            sb.AppendLine($"<p>Se ha generado una orden de recaudación a su nombre con el número <strong>{orden.NumeroOrden}</strong>.</p>");

            sb.AppendLine("<div class='info-box'>");
            sb.AppendLine("<h3>Información de la Orden:</h3>");
            sb.AppendLine($"<p><strong>Número:</strong> {orden.NumeroOrden}</p>");
            sb.AppendLine($"<p><strong>Fecha:</strong> {orden.FechaOrden:dd/MM/yyyy}</p>");
            sb.AppendLine($"<p><strong>Concepto:</strong> {orden.Concepto}</p>");
            sb.AppendLine($"<p><strong>Estado:</strong> {orden.Estado}</p>");
            if (orden.FechaVencimiento.HasValue)
            {
                sb.AppendLine($"<p><strong>Fecha de Vencimiento:</strong> {orden.FechaVencimiento.Value:dd/MM/yyyy}</p>");
            }
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='info-box'>");
            sb.AppendLine("<h3>Detalles del Pago:</h3>");
            sb.AppendLine($"<p><strong>Monto Total:</strong> ${orden.MontoTotal:#,##0.00}</p>");
            sb.AppendLine($"<p><strong>Monto Pagado:</strong> ${orden.MontoPagado:#,##0.00}</p>");
            sb.AppendLine($"<p><strong>Saldo Pendiente:</strong> ${orden.SaldoPendiente:#,##0.00}</p>");

            if (!string.IsNullOrEmpty(orden.ReferenciaPago))
            {
                sb.AppendLine($"<p><strong>Referencia de Pago:</strong> {orden.ReferenciaPago}</p>");
            }
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='total-box'>");
            sb.AppendLine($"<h3>Total a Pagar: ${orden.SaldoPendiente:#,##0.00}</h3>");
            sb.AppendLine("</div>");

            sb.AppendLine("<p><strong>Instrucciones para el pago:</strong></p>");
            sb.AppendLine("<ol>");
            sb.AppendLine("<li>Descargue el PDF adjunto para los detalles completos</li>");
            sb.AppendLine("<li>Realice el pago en cualquier banco autorizado</li>");
            sb.AppendLine("<li>Conserve el comprobante de pago</li>");
            sb.AppendLine("<li>En caso de dudas, contacte al administrador del sistema</li>");
            sb.AppendLine("</ol>");

            sb.AppendLine("</div>");

            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>Este es un mensaje automático del Sistema AOCR (Administración de Órdenes de Recaudación)</p>");
            sb.AppendLine("<p>Dirección General de Aviación Civil - Ecuador</p>");
            sb.AppendLine($"<p>Fecha de envío: {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public bool EnviarNotificacionPago(OrdenRecaudacionModel orden)
        {
            try
            {
                var mail = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = $"Confirmación de Pago - Orden {orden.NumeroOrden}",
                    Body = $@"
                        <html>
                        <body>
                        <h2>Confirmación de Pago Registrado</h2>
                        <p>Se ha registrado un pago para la orden {orden.NumeroOrden}</p>
                        <p><strong>Contribuyente:</strong> {orden.Contribuyente.NombreRazonSocial}</p>
                        <p><strong>Monto Pagado:</strong> ${orden.MontoPagado:#,##0.00}</p>
                        <p><strong>Saldo Pendiente:</strong> ${orden.SaldoPendiente:#,##0.00}</p>
                        <p><strong>Referencia:</strong> {orden.ReferenciaPago}</p>
                        <p><strong>Fecha de Pago:</strong> {orden.FechaPago:dd/MM/yyyy HH:mm}</p>
                        </body>
                        </html>",
                    IsBodyHtml = true
                };

                // Enviar a administradores
                var adminEmails = System.Configuration.ConfigurationManager.AppSettings["AdminEmails"];
                if (!string.IsNullOrEmpty(adminEmails))
                {
                    foreach (var email in adminEmails.Split(';'))
                    {
                        if (!string.IsNullOrEmpty(email.Trim()))
                        {
                            mail.To.Add(email.Trim());
                        }
                    }
                }

                _smtpClient.Send(mail);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}