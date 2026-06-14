using System;
using System.Configuration;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Servicio para acceso seguro a configuraciones sensibles.
    /// Abstrae el origen de las credenciales (Web.config, variables de entorno, KeyVault).
    /// </summary>
    public interface ISecureConfigurationService
    {
        string GetConnectionString(string name);
        string GetAppSetting(string key);
        string GetSecret(string secretName);
        AS400Credentials GetAS400Credentials();
        EmailCredentials GetEmailCredentials();
    }

    /// <summary>
    /// Credenciales AS400
    /// </summary>
    public class AS400Credentials
    {
        public string Server { get; set; }
        public string Database { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string Library { get; set; }
    }

    /// <summary>
    /// Credenciales de Email
    /// </summary>
    public class EmailCredentials
    {
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSsl { get; set; }
        public string FromAddress { get; set; }
        public string FromName { get; set; }
    }

    /// <summary>
    /// Implementación que prioriza variables de entorno sobre Web.config
    /// </summary>
    public class SecureConfigurationService : ISecureConfigurationService
    {
        private const string EnvPrefix = "AOCR_";

        public string GetConnectionString(string name)
        {
            // 1. Buscar en variable de entorno
            var envKey = EnvPrefix + "CONNSTR_" + name.ToUpperInvariant();
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            // 2. Fallback a Web.config
            var configValue = ConfigurationManager.ConnectionStrings[name];
            return configValue != null ? configValue.ConnectionString : null;
        }

        public string GetAppSetting(string key)
        {
            // 1. Buscar en variable de entorno
            var envKey = EnvPrefix + key.ToUpperInvariant().Replace(":", "_").Replace(".", "_");
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            // 2. Fallback a Web.config
            return ConfigurationManager.AppSettings[key];
        }

        public string GetSecret(string secretName)
        {
            // Buscar exclusivamente en variables de entorno (no en Web.config)
            var envKey = EnvPrefix + "SECRET_" + secretName.ToUpperInvariant();
            var envValue = Environment.GetEnvironmentVariable(envKey);

            if (string.IsNullOrWhiteSpace(envValue))
            {
                throw new ConfigurationErrorsException(
                    string.Format("Secret '{0}' no encontrado. Configure la variable de entorno '{1}'.",
                        secretName, envKey));
            }

            return envValue;
        }

        public AS400Credentials GetAS400Credentials()
        {
            return new AS400Credentials
            {
                Server = GetConfigOrEnv("AS400:Server", "AOCR_AS400_SERVER"),
                Database = GetConfigOrEnv("AS400:Database", "AOCR_AS400_DATABASE"),
                UserId = GetConfigOrEnv("AS400:UserId", "AOCR_AS400_USERID"),
                Password = GetSecretOrEnv("AS400:Password", "AOCR_AS400_PASSWORD"),
                Library = GetConfigOrEnv("AS400:Library", "AOCR_AS400_LIBRARY")
            };
        }

        public EmailCredentials GetEmailCredentials()
        {
            var portStr = GetConfigOrEnv("Email:SmtpPort", "AOCR_EMAIL_PORT");
            var useSslStr = GetConfigOrEnv("Email:UseSsl", "AOCR_EMAIL_USESSL");

            return new EmailCredentials
            {
                SmtpServer = FirstNonEmpty(
                    GetConfigOrEnv("Email:SmtpServer", "AOCR_EMAIL_SERVER"),
                    GetConfigOrEnv("SmtpServer", "AOCR_SMTP_SERVER"),
                    "mail.aviacioncivil.gob.ec"),
                SmtpPort = int.TryParse(portStr, out int port) ? port : 25,
                Username = GetConfigOrEnv("Email:Username", "AOCR_EMAIL_USERNAME"),
                Password = GetSecretOrEnv("Email:Password", "AOCR_EMAIL_PASSWORD"),
                UseSsl = bool.TryParse(useSslStr, out bool ssl) ? ssl : false,
                FromAddress = FirstNonEmpty(
                    GetConfigOrEnv("Email:FromAddress", "AOCR_EMAIL_FROM"),
                    "no_reply@aviacioncivil.gob.ec"),
                FromName = FirstNonEmpty(
                    GetConfigOrEnv("Email:FromName", "AOCR_EMAIL_FROMNAME"),
                    "Sistema AOCR")
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private string GetConfigOrEnv(string configKey, string envKey)
        {
            // Prioridad: variable de entorno > appSettings
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            return ConfigurationManager.AppSettings[configKey] ?? string.Empty;
        }

        private string GetSecretOrEnv(string configKey, string envKey)
        {
            // Para secretos, SOLO usar variables de entorno en producción
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            // En desarrollo, permitir fallback a config (con warning)
            var configValue = ConfigurationManager.AppSettings[configKey];
            if (!string.IsNullOrWhiteSpace(configValue))
            {
                System.Diagnostics.Debug.WriteLine(
                    string.Format("⚠️ ADVERTENCIA: Secret '{0}' leído desde config. Use variable de entorno '{1}' en producción.",
                        configKey, envKey));
                return configValue;
            }

            return string.Empty;
        }
    }
}
