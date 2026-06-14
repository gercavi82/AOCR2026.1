using System;
using System.Web;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Normaliza textos del formulario AOCR que pudieron quedar con entidades HTML
    /// por codificaciones repetidas (&amp;quot;, &#225;, etc.).
    /// </summary>
    public static class FormularioEmisionTextHelper
    {
        public static string NormalizarTextoPlano(string valor)
        {
            if (string.IsNullOrEmpty(valor))
            {
                return valor ?? string.Empty;
            }

            var actual = valor;
            for (var intento = 0; intento < 6; intento++)
            {
                var decodificado = HttpUtility.HtmlDecode(actual);
                if (string.Equals(decodificado, actual, StringComparison.Ordinal))
                {
                    break;
                }

                actual = decodificado;
            }

            return actual;
        }
    }
}
