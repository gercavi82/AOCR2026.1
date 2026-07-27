using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace CapaNegocio.Helpers
{
    public static class RtCorreoTextoHelper
    {
        public static string GetAsuntoDesignacionPendiente()
        {
            return GetAppSetting("Email:RT:AsuntoDesignacion", "Solicitud de cuenta - Sistema AOCR (pendiente de aceptación)");
        }

        public static string GetAsuntoDeclaracionAceptada()
        {
            return GetAppSetting("Email:RT:AsuntoDeclaracion", "Declaración de responsabilidad aceptada - Sistema AOCR");
        }

        public static string GetAsuntoAceptacionRt()
        {
            return GetAppSetting("Email:RT:AsuntoAceptacion", "Designación RT aprobada - Sus credenciales de acceso - Sistema AOCR");
        }

        public static string GetAsuntoDevolucionRt()
        {
            return GetAppSetting("Email:RT:AsuntoDevolucion", "Designación RT devuelta - Sistema AOCR");
        }

        public static string GetTextoDesignacionPendiente(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoDesignacion",
                "Su solicitud de cuenta en el Sistema AOCR ha sido registrada exitosamente. Su solicitud de designación como Responsable Técnico (RT) se encuentra pendiente de aceptación por la DGAC.");

            return ReplaceTokens(texto, tokens);
        }

        public static string GetTextoDeclaracionAceptada(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoDeclaracion",
                "Hemos registrado la aceptación de su declaración de responsabilidad RT.");

            return ReplaceTokens(texto, tokens);
        }

        public static string GetTextoAceptacionRt(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoAceptacion",
                "Nos complace informarle que su designación como Responsable Técnico (RT) de la compañía {COMPANIA} ha sido aprobada por la DGAC. En tal virtud, con su usuario podrá continuar con los trámites en el Sistema AOCR.");

            return ReplaceTokens(texto, tokens);
        }

        public static string GetTextoDevolucionRt(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoDevolucion",
                "Su designación como Responsable Técnico (RT) ha sido devuelta para corrección. Por favor revise los requisitos y vuelva a cargar la documentación correspondiente en el Sistema AOCR.");

            return ReplaceTokens(texto, tokens);
        }

        public static string ToHtmlParagraphs(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return string.Empty;
            }

            var normalized = NormalizeText(texto);
            var bloques = normalized
                .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            if (bloques.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(string.Empty, bloques.Select(ToParagraph));
        }

        private static string ToParagraph(string texto)
        {
            var encoded = HttpUtility.HtmlEncode(texto).Replace("\n", "<br/>");
            return "<p style='margin:0 0 12px 0; font-size:14px; color:#3a4f5e; line-height:1.55;'>" + encoded + "</p>";
        }

        private static string GetAppSetting(string key, string defaultValue)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : NormalizeText(value);
        }

        private static string ReplaceTokens(string texto, IDictionary<string, string> tokens)
        {
            if (string.IsNullOrWhiteSpace(texto) || tokens == null || tokens.Count == 0)
            {
                return texto;
            }

            var output = texto;
            foreach (var token in tokens)
            {
                var key = "{" + (token.Key ?? string.Empty).Trim().ToUpperInvariant() + "}";
                output = output.Replace(key, token.Value ?? string.Empty);
            }

            return output;
        }

        private static string NormalizeText(string texto)
        {
            if (texto == null)
            {
                return string.Empty;
            }

            return texto
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\r\n", "\n")
                .Trim();
        }
    }
}
