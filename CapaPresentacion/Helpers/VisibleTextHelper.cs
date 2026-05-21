using System;
using System.Web;

namespace CapaPresentacion.Helpers
{
    public static class VisibleTextHelper
    {
        public static string Normalize(string value)
        {
            var actual = (value ?? string.Empty).Trim();
            for (var i = 0; i < 3; i++)
            {
                var decoded = HttpUtility.HtmlDecode(actual) ?? string.Empty;
                if (string.Equals(decoded, actual, StringComparison.Ordinal))
                {
                    break;
                }

                actual = decoded;
            }

            return actual;
        }
    }
}