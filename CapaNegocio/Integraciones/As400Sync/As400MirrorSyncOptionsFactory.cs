using System;
using System.Configuration;
using System.Data.Odbc;
using CapaDatos.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public class As400MirrorSyncEnvironment
    {
        public string PostgresMirrorConnectionString { get; set; }
        public string As400OdbcConnectionString { get; set; }
        public SyncExecutionOptions Options { get; set; }
    }

    public static class As400MirrorSyncOptionsFactory
    {
        public static As400MirrorSyncEnvironment Create()
        {
            var secure = new SecureConfigurationService();
            var options = new SyncExecutionOptions
            {
                Enabled = ParseBool(AppSetting("Sync:Enabled"), false),
                BatchSize = ParseInt(AppSetting("Sync:BatchSize"), 1000),
                As400CommandTimeoutSeconds = ParseInt(AppSetting("Sync:As400TimeoutSeconds"), 60),
                PostgresCommandTimeoutSeconds = ParseInt(AppSetting("Sync:PostgresTimeoutSeconds"), 60),
                MaxRetries = ParseInt(AppSetting("Sync:MaxRetries"), 2),
                RetryBackoffMs = ParseInt(AppSetting("Sync:RetryBackoffMs"), 1500),
                DryRun = ParseBool(AppSetting("Sync:DryRun"), false),
                LockOwner = Environment.MachineName
            };

            var pgMirror = secure.GetConnectionString("PostgresMirror");
            if (string.IsNullOrWhiteSpace(pgMirror))
            {
                pgMirror = secure.GetConnectionString("PostgreSQL") ?? secure.GetConnectionString("AOCRConnection");
            }

            var as400Odbc = secure.GetConnectionString("As400Odbc");
            if (string.IsNullOrWhiteSpace(as400Odbc))
            {
                as400Odbc = BuildAs400OdbcFromSettings(secure, options.As400CommandTimeoutSeconds);
            }

            return new As400MirrorSyncEnvironment
            {
                PostgresMirrorConnectionString = pgMirror,
                As400OdbcConnectionString = as400Odbc,
                Options = options
            };
        }

        private static string BuildAs400OdbcFromSettings(ISecureConfigurationService secure, int commandTimeoutSeconds)
        {
            var creds = secure.GetAS400Credentials();
            if (creds == null || string.IsNullOrWhiteSpace(creds.Server))
            {
                return null;
            }

            var library = string.IsNullOrWhiteSpace(creds.Library) ? creds.Database : creds.Library;
            var builder = new OdbcConnectionStringBuilder();
            builder.Driver = AppSetting("Sync:As400OdbcDriver") ?? "iSeries Access ODBC Driver";
            builder["System"] = creds.Server;
            if (!string.IsNullOrWhiteSpace(library))
            {
                builder["Database"] = library;
            }
            if (!string.IsNullOrWhiteSpace(creds.UserId))
            {
                builder["Uid"] = creds.UserId;
            }
            if (!string.IsNullOrWhiteSpace(creds.Password))
            {
                builder["Pwd"] = creds.Password;
            }
            builder["Connection Timeout"] = ParseInt(AppSetting("Sync:As400ConnectionTimeoutSeconds"), 30).ToString();
            builder["Query Timeout"] = commandTimeoutSeconds.ToString();
            // CommitMode=0 → *NONE: sin control de compromiso para lecturas de mirror sync.
            // Evita SQL0913 (row/object in use) en tablas con commitment control activo como OPCAR5.
            // QueryOptimizeGoal=1 → first-row optimization: reduce el tiempo que el cursor mantiene locks.
            builder["CommitMode"] = "0";
            builder["QueryOptimizeGoal"] = "1";
            return builder.ConnectionString;
        }

        private static string AppSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private static bool ParseBool(string raw, bool fallback)
        {
            bool value;
            return bool.TryParse(raw, out value) ? value : fallback;
        }
    }
}
