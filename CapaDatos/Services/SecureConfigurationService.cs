using System;
using System.Configuration;

namespace CapaDatos.Services
{
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
    /// Interface para servicio de configuración segura
    /// </summary>
    public interface ISecureConfigurationService
    {
        string GetConnectionString(string name);
        string GetAppSetting(string key);
        AS400Credentials GetAS400Credentials();
        EmailCredentials GetEmailCredentials();
    }

    /// <summary>
    /// Implementación que prioriza variables de entorno sobre Web.config
    /// </summary>
    public class SecureConfigurationService : ISecureConfigurationService
    {
        private const string EnvPrefix = "AOCR_";

        public string GetConnectionString(string name)
        {
            var envKey = EnvPrefix + "CONNSTR_" + name.ToUpperInvariant();
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            var config = ConfigurationManager.ConnectionStrings[name];
            return config != null ? config.ConnectionString : null;
        }

        public string GetAppSetting(string key)
        {
            var envKey = EnvPrefix + key.ToUpperInvariant().Replace(":", "_");
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                return envValue;
            }

            return ConfigurationManager.AppSettings[key];
        }

        public AS400Credentials GetAS400Credentials()
        {
            return new AS400Credentials
            {
                Server = GetConfigOrEnv("AS400:Server", "AOCR_AS400_SERVER"),
                Database = GetConfigOrEnv("AS400:Database", "AOCR_AS400_DATABASE"),
                UserId = GetConfigOrEnv("AS400:UserId", "AOCR_AS400_USERID"),
                Password = GetConfigOrEnv("AS400:Password", "AOCR_AS400_PASSWORD"),
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
                    AocrEmailService.IpSmtp),
                SmtpPort = int.TryParse(portStr, out int port) ? port : 25,
                Username = GetConfigOrEnv("Email:Username", "AOCR_EMAIL_USERNAME"),
                Password = GetConfigOrEnv("Email:Password", "AOCR_EMAIL_PASSWORD"),
                UseSsl = bool.TryParse(useSslStr, out bool ssl) ? ssl : false,
                FromAddress = FirstNonEmpty(
                    GetConfigOrEnv("Email:FromAddress", "AOCR_EMAIL_FROM"),
                    AocrEmailService.CorreoNoReply),
                FromName = FirstNonEmpty(
                    GetConfigOrEnv("Email:FromName", "AOCR_EMAIL_FROMNAME"),
                    AocrEmailService.AliasDefault)
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
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(envValue)) return envValue;
            return ConfigurationManager.AppSettings[configKey] ?? string.Empty;
        }
    }
}
