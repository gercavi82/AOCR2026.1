using System.Net;
using System.Text;

namespace CapaModelo.Common
{
    public static class EmailTemplateRenderer
    {
        public static string Render(EmailTemplateModel model)
        {
            if (model == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(4096);

            sb.Append(@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0; padding:0; font-family:Arial,Helvetica,sans-serif; background-color:#f0f3f7; -webkit-text-size-adjust:100%;'>
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background-color:#f0f3f7; padding:24px 0;'>
<tr><td align='center'>
<table role='presentation' width='680' cellpadding='0' cellspacing='0' style='max-width:680px; width:100%; background-color:#ffffff; border:1px solid #d6dfe8; border-radius:12px; overflow:hidden;'>
");

            // ── HEADER ──
            sb.Append(@"<tr><td style='background:linear-gradient(135deg,#143b57 0%,#1b6f8a 100%); padding:22px 28px;'>
<table role='presentation' width='100%' cellpadding='0' cellspacing='0'>
<tr><td style='font-size:11px; letter-spacing:0.1em; text-transform:uppercase; color:rgba(255,255,255,0.82); font-family:Arial,Helvetica,sans-serif;'>SISTEMA AOCR DGAC</td></tr>
<tr><td style='padding-top:8px; font-size:20px; font-weight:bold; color:#ffffff; font-family:Arial,Helvetica,sans-serif;'>");
            sb.Append(Encode(model.Titulo ?? "Notificacion AOCR"));
            sb.Append(@"</td></tr>
</table>
</td></tr>
");

            // ── BODY ──
            sb.Append("<tr><td style='padding:28px 28px 12px 28px;'>");

            // Saludo
            sb.Append("<p style='margin:0 0 14px 0; font-size:14px; color:#243746;'>Estimado/a <strong>");
            sb.Append(Encode(string.IsNullOrWhiteSpace(model.NombreDestinatario) ? "Usuario AOCR" : model.NombreDestinatario));
            sb.Append("</strong>,</p>");

            // Mensaje principal
            if (!string.IsNullOrWhiteSpace(model.MensajePrincipal))
            {
                sb.Append("<p style='margin:0 0 18px 0; font-size:14px; color:#3a4f5e; line-height:1.55;'>");
                sb.Append(Encode(model.MensajePrincipal));
                sb.Append("</p>");
            }

            // Tabla resumen
            if (model.Resumen != null && model.Resumen.Count > 0)
            {
                sb.Append(@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse; margin:0 0 18px 0;'>");
                foreach (var item in model.Resumen)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style='padding:10px 12px; border:1px solid #e4edf4; background-color:#f8fbfd; font-weight:bold; font-size:13px; color:#243746; width:40%; font-family:Arial,Helvetica,sans-serif;'>");
                    sb.Append(Encode(item.Label ?? string.Empty));
                    sb.Append("</td>");
                    sb.Append("<td style='padding:10px 12px; border:1px solid #e4edf4; font-size:13px; color:#3a4f5e; font-family:Arial,Helvetica,sans-serif;'>");
                    sb.Append(Encode(item.Value ?? string.Empty));
                    sb.Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }

            // Contenido HTML extra (credenciales, listas, etc.)
            if (!string.IsNullOrWhiteSpace(model.ContenidoHtmlExtra))
            {
                sb.Append(model.ContenidoHtmlExtra);
            }

            // Observaciones
            if (!string.IsNullOrWhiteSpace(model.Observaciones))
            {
                sb.Append(@"<div style='margin:0 0 18px 0; padding:12px 14px; background-color:#fef9e7; border-left:4px solid #e2b93b; border-radius:4px; font-size:13px; color:#5a4e1a; line-height:1.5;'>");
                sb.Append("<strong>Observaciones:</strong> ");
                sb.Append(Encode(model.Observaciones));
                sb.Append("</div>");
            }

            // Enlace
            if (!string.IsNullOrWhiteSpace(model.EnlaceUrl))
            {
                var textoEnlace = string.IsNullOrWhiteSpace(model.EnlaceTexto) ? "Abrir expediente" : model.EnlaceTexto;
                sb.Append("<p style='margin:0 0 18px 0;'><a href=\"");
                sb.Append(Encode(model.EnlaceUrl));
                sb.Append("\" style='display:inline-block; padding:10px 22px; background-color:#1b6f8a; color:#ffffff; text-decoration:none; border-radius:6px; font-size:13px; font-weight:bold; font-family:Arial,Helvetica,sans-serif;'>");
                sb.Append(Encode(textoEnlace));
                sb.Append("</a></p>");
            }

            // Cierre
            if (!string.IsNullOrWhiteSpace(model.TextoCierre))
            {
                sb.Append("<p style='margin:0 0 0 0; font-size:13px; color:#617588; line-height:1.5;'>");
                sb.Append(Encode(model.TextoCierre));
                sb.Append("</p>");
            }

            sb.Append("</td></tr>");

            // ── FOOTER ──
            sb.Append(@"<tr><td style='padding:18px 28px; border-top:1px solid #e8ecf1;'>
<p style='margin:0; font-size:11px; color:#8a96a3; font-family:Arial,Helvetica,sans-serif;'>");
            sb.Append(Encode(string.IsNullOrWhiteSpace(model.Footer) ? "Este es un mensaje automatico del sistema AOCR." : model.Footer));
            sb.Append(@"</p>
</td></tr>
");

            // ── CLOSE ──
            sb.Append(@"</table>
</td></tr>
</table>
</body>
</html>");

            return sb.ToString();
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
