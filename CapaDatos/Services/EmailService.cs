using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Configuration;
using System.Text.RegularExpressions;
using CapaDatos.Models;

namespace CapaDatos.Services
{
    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly bool _smtpEnableSsl;
        private readonly string _fromEmail;
        private readonly string _fromName;

        // Opcional: cultura para formato moneda/fecha
        private static readonly CultureInfo EcCulture = new CultureInfo("es-EC");

        public EmailService()
        {
            _smtpHost = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
            _smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"] ?? "";
            _smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "";
            _smtpEnableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");
            _fromEmail = ConfigurationManager.AppSettings["FromEmail"] ?? "sistema@dgac.gob.ec";
            _fromName = ConfigurationManager.AppSettings["FromName"] ?? "Sistema AOCR - DGAC Ecuador";
        }

        /// <summary>
        /// Envía Orden de Recaudación al contribuyente. Adjunta PDF si existe.
        /// ccEmail: correo del funcionario/usuario (opcional).
        /// </summary>
        public bool EnviarOrdenRecaudacion(
            OrdenRecaudacionModel orden,
            byte[] pdfAdjunto = null,
            string ccEmail = null,
            string ccNombre = null,
            string bccEmail = null,
            string replyToEmail = null)
        {
            if (orden == null) throw new ArgumentNullException(nameof(orden));

            if (string.IsNullOrWhiteSpace(orden.Correo))
                throw new ArgumentException("La orden no tiene correo del contribuyente (aocr_or_orden.correo).");

            if (!EsEmailValido(orden.Correo))
                throw new ArgumentException("El correo del contribuyente no tiene un formato válido.");

            if (!string.IsNullOrWhiteSpace(ccEmail) && !EsEmailValido(ccEmail))
                throw new ArgumentException("El correo CC no tiene un formato válido.");

            if (!string.IsNullOrWhiteSpace(bccEmail) && !EsEmailValido(bccEmail))
                throw new ArgumentException("El correo BCC no tiene un formato válido.");

            if (!string.IsNullOrWhiteSpace(replyToEmail) && !EsEmailValido(replyToEmail))
                throw new ArgumentException("El Reply-To no tiene un formato válido.");

            // TLS 1.2 (seguro en .NET Framework)
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var asunto = $"Orden de Recaudación #{orden.NumeroOrden} - DGAC Ecuador";
            var cuerpo = GenerarCuerpoEmail(orden);

            using (var smtp = CrearSmtpClient())
            using (var msg = new MailMessage())
            {
                msg.From = new MailAddress(_fromEmail, _fromName);
                msg.Subject = asunto;
                msg.Body = cuerpo;
                msg.IsBodyHtml = true;
                msg.BodyEncoding = Encoding.UTF8;
                msg.SubjectEncoding = Encoding.UTF8;
                msg.Priority = MailPriority.High;

                var nombreDest = string.IsNullOrWhiteSpace(orden.NombreContribuyente) ? "Contribuyente" : orden.NombreContribuyente;
                msg.To.Add(new MailAddress(orden.Correo, nombreDest));

                if (!string.IsNullOrWhiteSpace(ccEmail))
                    msg.CC.Add(new MailAddress(ccEmail, string.IsNullOrWhiteSpace(ccNombre) ? "Usuario" : ccNombre));

                if (!string.IsNullOrWhiteSpace(bccEmail))
                    msg.Bcc.Add(new MailAddress(bccEmail));

                if (!string.IsNullOrWhiteSpace(replyToEmail))
                    msg.ReplyToList.Add(new MailAddress(replyToEmail));

                // Adjuntar PDF (si existe)
                System.IO.MemoryStream ms = null;
                try
                {
                    if (pdfAdjunto != null && pdfAdjunto.Length > 0)
                    {
                        ms = new System.IO.MemoryStream(pdfAdjunto);
                        var adj = new Attachment(ms, $"Orden_Recaudacion_{orden.NumeroOrden}.pdf", "application/pdf");
                        msg.Attachments.Add(adj);
                        ms = null; // el stream se liberará con msg/attachments
                    }

                    smtp.Send(msg);
                    return true;
                }
                catch (SmtpFailedRecipientException ex)
                {
                    TraceSeguro($"SMTP destinatario falló: {ex.FailedRecipient} - {ex.StatusCode}");
                    return false;
                }
                catch (SmtpException ex)
                {
                    TraceSeguro($"SMTP error: {ex.StatusCode}");
                    return false;
                }
                catch (Exception ex)
                {
                    TraceSeguro("EmailService error general.");
                    return false;
                }
                finally
                {
                    // Si por alguna razón no se adjuntó y quedó vivo
                    if (ms != null) ms.Dispose();
                }
            }
        }

        /// <summary>
        /// Notificación de pago registrada (basado en aocr_tbpago).
        /// Usa NumeroFactura o MetodoPago como referencia mostrada.
        /// </summary>
        public bool EnviarNotificacionPago(OrdenRecaudacionModel orden, PagoModel pago)
        {
            try
            {
                if (orden == null || pago == null) return false;
                if (string.IsNullOrWhiteSpace(orden.Correo) || !EsEmailValido(orden.Correo)) return false;

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                var asunto = $"Confirmación de Pago - Orden #{orden.NumeroOrden}";
                var referenciaMostrar = !string.IsNullOrWhiteSpace(pago.NumeroFactura)
                    ? pago.NumeroFactura
                    : (pago.MetodoPago ?? "N/D");

                var fechaPagoTxt = pago.FechaPago.HasValue
                    ? pago.FechaPago.Value.ToString("dd/MM/yyyy HH:mm", EcCulture)
                    : "N/D";

                var cuerpo = $@"
<html><body style='font-family: Arial, sans-serif;'>
<h2 style='color:#1B4F72;'>Confirmación de Pago Registrado</h2>
<p>Estimado/a <strong>{Html(orden.NombreContribuyente ?? "Contribuyente")}</strong>,</p>

<div style='background:#f8f9fa;padding:15px;border-left:4px solid #28a745;margin:20px 0;'>
  <p><strong>Número de Orden:</strong> {Html(orden.NumeroOrden)}</p>
  <p><strong>Fecha del Pago:</strong> {Html(fechaPagoTxt)}</p>
  <p><strong>Monto Pagado:</strong> {pago.Monto.ToString("C", EcCulture)}</p>
  <p><strong>Documento/Factura/Medio:</strong> {Html(referenciaMostrar)}</p>
  <p><strong>Estado:</strong> <span style='color:#28a745;'>{Html(pago.Estado ?? "")}</span></p>
</div>

<p>Este correo es automático. Por favor no responda.</p>
</body></html>";

                return EnviarEmail(orden.Correo, orden.NombreContribuyente, asunto, cuerpo);
            }
            catch
            {
                TraceSeguro("Error EnviarNotificacionPago.");
                return false;
            }
        }

        /// <summary>
        /// Notificación de anulación de orden.
        /// </summary>
        public bool EnviarNotificacionAnulacion(OrdenRecaudacionModel orden, string motivo)
        {
            try
            {
                if (orden == null || string.IsNullOrWhiteSpace(orden.Correo) || !EsEmailValido(orden.Correo)) return false;

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                var asunto = $"Anulación de Orden #{orden.NumeroOrden} - DGAC Ecuador";
                var cuerpo = $@"
<html><body style='font-family: Arial, sans-serif;'>
<h2 style='color:#dc3545;'>Notificación de Anulación de Orden</h2>
<p>Estimado/a <strong>{Html(orden.NombreContribuyente ?? "Contribuyente")}</strong>,</p>
<p>Su orden ha sido <strong>ANULADA</strong>.</p>

<div style='background:#fff3cd;padding:15px;border-left:4px solid #ffc107;margin:20px 0;'>
  <p><strong>Motivo:</strong> {Html(motivo ?? "")}</p>
</div>

<p style='font-size:12px;color:#666;'>Mensaje automático. No responder.</p>
</body></html>";

                return EnviarEmail(orden.Correo, orden.NombreContribuyente, asunto, cuerpo);
            }
            catch
            {
                TraceSeguro("Error EnviarNotificacionAnulacion.");
                return false;
            }
        }

        // ===================== Helpers internos =====================

        private bool EnviarEmail(string toEmail, string toName, string subject, string bodyHtml)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(toEmail) || !EsEmailValido(toEmail)) return false;

                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                using (var smtp = CrearSmtpClient())
                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(_fromEmail, _fromName);
                    msg.To.Add(new MailAddress(toEmail, string.IsNullOrWhiteSpace(toName) ? "Usuario" : toName));
                    msg.Subject = subject ?? "";
                    msg.Body = bodyHtml ?? "";
                    msg.IsBodyHtml = true;
                    msg.BodyEncoding = Encoding.UTF8;
                    msg.SubjectEncoding = Encoding.UTF8;

                    smtp.Send(msg);
                    return true;
                }
            }
            catch (SmtpException ex)
            {
                TraceSeguro($"SMTP error: {ex.StatusCode}");
                return false;
            }
            catch
            {
                TraceSeguro("EmailService error.");
                return false;
            }
        }

        private SmtpClient CrearSmtpClient()
        {
            var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpEnableSsl,
                Timeout = 30000,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            // Solo setear credenciales si están configuradas (evita errores raros en servidores internos)
            if (!string.IsNullOrWhiteSpace(_smtpUsername))
                smtp.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

            return smtp;
        }

        private string GenerarCuerpoEmail(OrdenRecaudacionModel orden)
        {
            var conceptoMostrar = "Servicios DGAC";

            if (orden.Detalles != null && orden.Detalles.Any())
                conceptoMostrar = orden.Detalles.First().Descripcion
                                  ?? orden.Detalles.First().ConceptoNombre
                                  ?? conceptoMostrar;

            var tablaDetalles = "";
            if (orden.Detalles != null && orden.Detalles.Any())
            {
                var sb = new StringBuilder();
                sb.AppendLine("<table style='width:100%;border-collapse:collapse;margin:15px 0;'>");
                sb.AppendLine("<thead><tr style='background:#1B4F72;color:white;'>");
                sb.AppendLine("<th style='padding:10px;'>Concepto</th>");
                sb.AppendLine("<th style='padding:10px;text-align:right;'>Cantidad</th>");
                sb.AppendLine("<th style='padding:10px;text-align:right;'>Valor Unit.</th>");
                sb.AppendLine("<th style='padding:10px;text-align:right;'>Subtotal</th>");
                sb.AppendLine("</tr></thead><tbody>");

                foreach (var d in orden.Detalles)
                {
                    sb.AppendLine("<tr style='border-bottom:1px solid #ddd;'>");
                    sb.AppendLine($"<td style='padding:10px;'>{Html(d.Descripcion ?? d.ConceptoNombre ?? "")}</td>");
                    sb.AppendLine($"<td style='padding:10px;text-align:right;'>{d.Cantidad}</td>");
                    sb.AppendLine($"<td style='padding:10px;text-align:right;'>{d.ValorUnitario.ToString("C", EcCulture)}</td>");
                    sb.AppendLine($"<td style='padding:10px;text-align:right;'>{d.Subtotal.ToString("C", EcCulture)}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
                tablaDetalles = sb.ToString();
            }

            var porcentajeAdmin = (orden.Subtotal > 0) ? (orden.Admin / orden.Subtotal * 100m) : 0m;

            return $@"
<html><body style='font-family: Arial, sans-serif; color:#333;'>
<div style='max-width:650px;margin:0 auto;'>
  <div style='background:#1B4F72;color:white;padding:16px;text-align:center;'>
    <h2 style='margin:0;'>Orden de Recaudación - DGAC</h2>
    <div>Sistema AOCR</div>
  </div>

  <div style='padding:16px;background:#f9f9f9;'>
    <p>Estimado/a <strong>{Html(orden.NombreContribuyente ?? "Contribuyente")}</strong>,</p>
    <p>Adjunto encontrará la Orden de Recaudación generada.</p>

    <div style='background:white;border:1px solid #ddd;padding:12px;margin:12px 0;'>
      <p><strong>Número de Orden:</strong> {Html(orden.NumeroOrden)}</p>
      <p><strong>Fecha:</strong> {orden.FechaCreacion.ToString("dd/MM/yyyy", EcCulture)}</p>
      <p><strong>Lugar:</strong> {Html(orden.LugarEmision ?? "")}</p>
      <p><strong>Concepto:</strong> {Html(conceptoMostrar)}</p>
      <p><strong>RUC/Cédula:</strong> {Html(orden.RucCedula ?? "")}</p>
      <p><strong>Compañía:</strong> {Html(orden.Compania ?? "")}</p>
    </div>

    {tablaDetalles}

    <div style='background:#e9f7fe;border-left:4px solid #1B4F72;padding:12px;margin:12px 0;'>
      <p><strong>Subtotal:</strong> {orden.Subtotal.ToString("C", EcCulture)}</p>
      <p><strong>Administración ({porcentajeAdmin:F1}%):</strong> {orden.Admin.ToString("C", EcCulture)}</p>
      <p style='font-size:1.1em;'><strong>TOTAL:</strong> {orden.Total.ToString("C", EcCulture)}</p>
    </div>

    <p style='font-size:12px;color:#666;'>Mensaje automático. No responder.</p>
  </div>
</div>
</body></html>";
        }

        private static string Html(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

        private static bool EsEmailValido(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            // Simple y suficiente para validación básica (sin sobre-rechazar)
            return Regex.IsMatch(email.Trim(),
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                RegexOptions.IgnoreCase);
        }

        private static void TraceSeguro(string msg)
        {
            // Nunca loguear usuario/clave SMTP ni correos completos si no es necesario.
            System.Diagnostics.Trace.TraceWarning("[EmailService] " + msg);
        }
    }
}
