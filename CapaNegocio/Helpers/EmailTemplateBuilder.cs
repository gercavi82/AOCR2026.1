using System;
using System.Net;
using System.Text;

namespace CapaNegocio.Helpers
{
    public static class EmailTemplateBuilder
    {
        public static string OrdenGenerada(
            string nombreSolicitante,
            string numeroOrden,
            string referenciaTramite,
            DateTime fechaAccion,
            string rolUnidad,
            decimal montoTotal,
            string observaciones)
        {
            var nombre = Html(nombreSolicitante, "Solicitante");
            var orden = Html(numeroOrden, "N/A");
            var tramite = Html(referenciaTramite, "N/A");
            var rol = Html(rolUnidad, "N/A");
            var obs = Html(observaciones, "Sin observaciones");
            var fecha = fechaAccion.ToString("yyyy-MM-dd HH:mm:ss");

            var sb = new StringBuilder();
            sb.Append("<html><body style='font-family:Arial,sans-serif;color:#222;'>");
            sb.Append("<h2 style='margin-bottom:8px;'>Orden de recaudación generada</h2>");
            sb.AppendFormat("<p>Estimado(a) <strong>{0}</strong>,</p>", nombre);
            sb.Append("<p>Su orden de recaudación fue generada correctamente.</p>");
            sb.Append("<table style='border-collapse:collapse;width:100%;max-width:760px;'>");
            Row(sb, "Número de Orden", orden);
            Row(sb, "Trámite / Solicitud", tramite);
            Row(sb, "Fecha y hora", Html(fecha, "N/A"));
            Row(sb, "Unidad / Rol", rol);
            Row(sb, "Monto total", Html(montoTotal.ToString("0.00"), "0.00"));
            sb.Append("</table>");
            sb.Append("<div style='margin-top:14px;padding:10px;border-left:4px solid #1d4ed8;background:#f8fbff;'>");
            sb.Append("<strong>Observaciones:</strong><br/>");
            sb.Append(obs.Replace("\n", "<br/>"));
            sb.Append("</div>");
            sb.Append("<p style='margin-top:18px;color:#666;'>No responda a este correo. Si requiere soporte, contacte a la mesa de ayuda AOCR.</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        public static string OrdenRechazada(
            string nombreSolicitante,
            string numeroOrden,
            string referenciaTramite,
            DateTime fechaAccion,
            string rolUnidad,
            string estadoFinal,
            string observaciones)
        {
            var nombre = Html(nombreSolicitante, "Solicitante");
            var orden = Html(numeroOrden, "N/A");
            var tramite = Html(referenciaTramite, "N/A");
            var rol = Html(rolUnidad, "N/A");
            var estado = Html(estadoFinal, "N/A");
            var obs = Html(observaciones, "Sin observaciones");
            var fecha = fechaAccion.ToString("yyyy-MM-dd HH:mm:ss");

            var sb = new StringBuilder();
            sb.Append("<html><body style='font-family:Arial,sans-serif;color:#222;'>");
            sb.Append("<h2 style='margin-bottom:8px;'>Notificación de orden rechazada/anulada</h2>");
            sb.AppendFormat("<p>Estimado(a) <strong>{0}</strong>,</p>", nombre);
            sb.Append("<p>Se informa que su orden/trámite fue actualizada con estado final negativo.</p>");
            sb.Append("<table style='border-collapse:collapse;width:100%;max-width:760px;'>");
            Row(sb, "Número de Orden", orden);
            Row(sb, "Trámite / Solicitud", tramite);
            Row(sb, "Fecha y hora de la acción", Html(fecha, "N/A"));
            Row(sb, "Unidad / Rol", rol);
            Row(sb, "Estado final", estado);
            sb.Append("</table>");
            sb.Append("<div style='margin-top:14px;padding:10px;border-left:4px solid #b91c1c;background:#fff5f5;'>");
            sb.Append("<strong>Observaciones / Motivo:</strong><br/>");
            sb.Append(obs.Replace("\n", "<br/>"));
            sb.Append("</div>");
            sb.Append("<p style='margin-top:18px;color:#666;'>No responda a este correo. Si requiere soporte, contacte a la mesa de ayuda AOCR.</p>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static void Row(StringBuilder sb, string label, string value)
        {
            sb.Append("<tr>");
            sb.AppendFormat("<td style='border:1px solid #ddd;padding:8px;background:#fafafa;width:260px;'><strong>{0}</strong></td>", Html(label, label));
            sb.AppendFormat("<td style='border:1px solid #ddd;padding:8px;'>{0}</td>", value);
            sb.Append("</tr>");
        }

        private static string Html(string value, string fallback)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            return WebUtility.HtmlEncode(safe);
        }
    }
}
