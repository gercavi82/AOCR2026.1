using System.Configuration;
using System.Globalization;
using CapaModelo.Common;

namespace CapaNegocio.Services
{
    public class Fr3ConfigurationProvider
    {
        public Fr3Configuration GetConfiguration()
        {
            var config = new Fr3Configuration
            {
                Mode = ParseMode(ConfigurationManager.AppSettings["AS400:Facturacion:Mode"] ?? "Legacy"),
                TransactionRequired = ParseBool(ConfigurationManager.AppSettings["AS400:Facturacion:TransactionRequired"], true),
                AutomaticRetryEnabled = ParseBool(ConfigurationManager.AppSettings["AS400:Facturacion:AutomaticRetryEnabled"], true),
                MaxIntentos = ParseInt(ConfigurationManager.AppSettings["AS400:Facturacion:MaxIntentos"], 5),
                BaseBackoffSeconds = ParseInt(ConfigurationManager.AppSettings["AS400:Facturacion:BaseBackoffSeconds"], 300), // 5 min
                LeaseDurationSeconds = ParseInt(ConfigurationManager.AppSettings["AS400:Facturacion:LeaseDurationSeconds"], 60)
            };

            // Regla 2: Impedir por diseño que Legacy y Outbox sean escritores simultáneos.
            // Si el modo es Outbox, es Outbox. Si es Legacy, es Legacy. No pueden convivir para el mismo evento en ejecución.
            return config;
        }

        private Fr3ProcessingMode ParseMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Fr3ProcessingMode.Legacy;
            if (value.Equals("Outbox", System.StringComparison.OrdinalIgnoreCase)) return Fr3ProcessingMode.Outbox;
            if (value.Equals("Disabled", System.StringComparison.OrdinalIgnoreCase)) return Fr3ProcessingMode.Disabled;
            return Fr3ProcessingMode.Legacy; // Default
        }

        private bool ParseBool(string value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            if (bool.TryParse(value, out bool result)) return result;
            return defaultValue;
        }

        private int ParseInt(string value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            if (int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out int result)) return result;
            return defaultValue;
        }
    }
}
