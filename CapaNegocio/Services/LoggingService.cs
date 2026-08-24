using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Text;
using System.Web;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Contexto de logging con información de correlación
    /// </summary>
    public class LogContext
    {
        public string CorrelationId { get; set; }
        public string NumeroOrden { get; set; }
        public string CodigoSolicitud { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public string UserId { get; set; }
        public string ErrorCode { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; }

        public LogContext()
        {
            AdditionalData = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Interface para servicio de logging
    /// </summary>
    public interface ILoggingService
    {
        void LogInfo(string message, LogContext context = null);
        void LogWarning(string message, LogContext context = null);
        void LogError(Exception ex, LogContext context = null);
        void LogError(string message, LogContext context = null);
        void LogDebug(string message, LogContext context = null);
        void LogAudit(string action, string entityType, int entityId, LogContext context = null);
        IDisposable BeginScope(LogContext context);
    }

    /// <summary>
    /// Implementación de logging estructurado
    /// Compatible con NLog/Serilog pero independiente
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly string _logDirectory;
        private readonly object _lockObject = new object();
        private static readonly string ApplicationName = "AOCR";

        public LoggingService(string logDirectory = null)
        {
            _logDirectory = logDirectory ?? ResolverRutaLogsConfigurada();
            try
            {
                EnsureDirectoryExists();
            }
            catch (Exception)
            {
                // Ruta configurada (ej. recurso de red) no disponible: fallback a App_Data\Logs local
                _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
                try
                {
                    EnsureDirectoryExists();
                }
                catch (UnauthorizedAccessException)
                {
                    // Fallback final: usar TEMP si App_Data tampoco tiene permisos de escritura
                    _logDirectory = Path.Combine(Path.GetTempPath(), "AOCR_Logs");
                    try { EnsureDirectoryExists(); } catch { /* logging no debe matar el arranque */ }
                }
                catch (Exception)
                {
                    // logging no debe impedir el arranque de la aplicación
                }
            }
        }

        private static string ResolverRutaLogsConfigurada()
        {
            try
            {
                var raw = ConfigurationManager.AppSettings["AOCR_LogPath"];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
            }
            catch
            {
                // Continuar con la ruta local por defecto
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public void LogInfo(string message, LogContext context = null)
        {
            WriteLog("INFO", message, null, context);
        }

        public void LogWarning(string message, LogContext context = null)
        {
            WriteLog("WARN", message, null, context);
        }

        public void LogError(Exception ex, LogContext context = null)
        {
            WriteLog("ERROR", ex.Message, ex, context);
        }

        public void LogError(string message, LogContext context = null)
        {
            WriteLog("ERROR", message, null, context);
        }

        public void LogDebug(string message, LogContext context = null)
        {
            #if DEBUG
            WriteLog("DEBUG", message, null, context);
            #endif
        }

        public void LogAudit(string action, string entityType, int entityId, LogContext context = null)
        {
            var auditMessage = string.Format("AUDIT: {0} | Entity: {1} | Id: {2}", action, entityType, entityId);
            WriteLog("AUDIT", auditMessage, null, context);

            // También escribir a archivo de auditoría separado
            WriteAuditLog(action, entityType, entityId, context);
        }

        public IDisposable BeginScope(LogContext context)
        {
            return new LogScope(context);
        }

        private void WriteLog(string level, string message, Exception ex, LogContext context)
        {
            try
            {
                var timestamp = DateTime.Now;
                var logEntry = BuildLogEntry(timestamp, level, message, ex, context);

                // Escribir a archivo
                var fileName = string.Format("{0}_{1:yyyyMMdd}.log", ApplicationName, timestamp);
                var filePath = Path.Combine(_logDirectory, fileName);

                lock (_lockObject)
                {
                    File.AppendAllText(filePath, logEntry + Environment.NewLine, Encoding.UTF8);
                }

                // También escribir a Debug output
                System.Diagnostics.Debug.WriteLine(logEntry);
            }
            catch
            {
                // Logging nunca debe matar la aplicación
                System.Diagnostics.Debug.WriteLine(string.Format("[AOCR-LOG-FAIL] {0}: {1}", level, message));
            }
        }

        private string BuildLogEntry(DateTime timestamp, string level, string message, Exception ex, LogContext context)
        {
            var sb = new StringBuilder();

            // Formato estructurado: timestamp | level | correlationId | message | context
            sb.AppendFormat("{0:yyyy-MM-dd HH:mm:ss.fff}", timestamp);
            sb.AppendFormat(" | {0,-5}", level);

            // Correlation ID
            var correlationId = context?.CorrelationId ?? GetCurrentCorrelationId();
            sb.AppendFormat(" | CID:{0}", correlationId ?? "N/A");

            // Número de orden / Código solicitud (si existen)
            if (!string.IsNullOrEmpty(context?.NumeroOrden))
            {
                sb.AppendFormat(" | ORD:{0}", context.NumeroOrden);
            }
            if (!string.IsNullOrEmpty(context?.CodigoSolicitud))
            {
                sb.AppendFormat(" | SOL:{0}", context.CodigoSolicitud);
            }

            // Usuario
            var userId = context?.UserId ?? GetCurrentUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                sb.AppendFormat(" | USR:{0}", userId);
            }

            // Controller/Action
            if (!string.IsNullOrEmpty(context?.Controller))
            {
                sb.AppendFormat(" | {0}/{1}", context.Controller, context.Action ?? "?");
            }

            // Error code
            if (!string.IsNullOrEmpty(context?.ErrorCode))
            {
                sb.AppendFormat(" | ERR:{0}", context.ErrorCode);
            }

            // Mensaje principal
            sb.AppendFormat(" | {0}", message);

            // Excepción (solo en ERROR)
            if (ex != null)
            {
                sb.AppendLine();
                sb.AppendFormat("    Exception: {0}", ex.GetType().Name);
                sb.AppendLine();
                sb.AppendFormat("    StackTrace: {0}", ex.StackTrace);

                if (ex.InnerException != null)
                {
                    sb.AppendLine();
                    sb.AppendFormat("    InnerException: {0} - {1}", ex.InnerException.GetType().Name, ex.InnerException.Message);
                }
            }

            // Datos adicionales
            if (context?.AdditionalData != null && context.AdditionalData.Count > 0)
            {
                sb.AppendLine();
                sb.Append("    Data: ");
                foreach (var kvp in context.AdditionalData)
                {
                    sb.AppendFormat("{0}={1}; ", kvp.Key, kvp.Value);
                }
            }

            return sb.ToString();
        }

        private void WriteAuditLog(string action, string entityType, int entityId, LogContext context)
        {
            try
            {
                var timestamp = DateTime.Now;
                var fileName = string.Format("{0}_Audit_{1:yyyyMMdd}.log", ApplicationName, timestamp);
                var filePath = Path.Combine(_logDirectory, fileName);

                var entry = string.Format(
                    "{0:yyyy-MM-dd HH:mm:ss} | {1} | {2} | {3} | {4} | {5} | {6}",
                    timestamp,
                    action,
                    entityType,
                    entityId,
                    context?.UserId ?? GetCurrentUserId() ?? "SYSTEM",
                    context?.NumeroOrden ?? "",
                    context?.CodigoSolicitud ?? "");

                lock (_lockObject)
                {
                    File.AppendAllText(filePath, entry + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Logging nunca debe matar la aplicación
            }
        }

        private string GetCurrentCorrelationId()
        {
            try
            {
                var current = HttpContext.Current;
                if (current == null)
                {
                    return null;
                }

                return current.Items["CorrelationId"] as string;
            }
            catch
            {
                return null;
            }
        }

        private string GetCurrentUserId()
        {
            try
            {
                var current = HttpContext.Current;
                if (current == null)
                {
                    return null;
                }

                return current.User != null && current.User.Identity != null && current.User.Identity.IsAuthenticated
                    ? current.User.Identity.Name
                    : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Scope para logging contextual
        /// </summary>
        private class LogScope : IDisposable
        {
            public LogScope(LogContext context) { }
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Factory para crear instancias de LoggingService
    /// </summary>
    public static class LoggingServiceFactory
    {
        private static ILoggingService _instance;
        private static readonly object _lock = new object();

        public static ILoggingService Create()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new LoggingService();
                    }
                }
            }
            return _instance;
        }

        public static void SetInstance(ILoggingService service)
        {
            lock (_lock)
            {
                _instance = service;
            }
        }
    }
}
