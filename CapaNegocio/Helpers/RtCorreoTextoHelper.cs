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
            return GetAppSetting("Email:RT:AsuntoDesignacion", "Cuenta creada - Sistema AOCR (pendiente de aprobacion)");
        }

        public static string GetAsuntoDeclaracionAceptada()
        {
            return GetAppSetting("Email:RT:AsuntoDeclaracion", "Declaracion de responsabilidad aceptada - Sistema AOCR");
        }

        public static string GetAsuntoAceptacionRt()
        {
            return GetAppSetting("Email:RT:AsuntoAceptacion", "Designacion RT aprobada - Sus credenciales de acceso - Sistema AOCR");
        }

        public static string GetAsuntoDevolucionRt()
        {
            return GetAppSetting("Email:RT:AsuntoDevolucion", "Designacion RT devuelta - Sistema AOCR");
        }

        public static string GetTextoDesignacionPendiente(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoDesignacion",
                "Su cuenta en el Sistema AOCR ha sido creada exitosamente. Su solicitud de designacion como Responsable Tecnico (RT) se encuentra en proceso de revision y aprobacion por la DGAC.");

            return ReplaceTokens(texto, tokens);
        }

        public static string GetTextoDeclaracionAceptada(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoDeclaracion",
                "Hemos registrado la aceptacion de su declaracion de responsabilidad RT.");

            return ReplaceTokens(texto, tokens);
        }

        public static string GetTextoAceptacionRt(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoAceptacion",
                "Nos complace informarle que su designacion como Responsable Tecnico (RT) de la compania {COMPANIA} ha sido aprobada por la DGAC. En tal virtud, con su usuario podra continuar con los tramites en el Sistema AOCR.");

            return ReplaceTokens(texto, tokens);
        }

        public static string GetTextoDevolucionRt(IDictionary<string, string> tokens = null)
        {
            var texto = GetAppSetting(
                "Email:RT:TextoDevolucion",
                "Su designacion como Responsable Tecnico (RT) ha sido devuelta para correccion. Por favor revise los requisitos y vuelva a cargar la documentacion correspondiente en el Sistema AOCR.");

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
