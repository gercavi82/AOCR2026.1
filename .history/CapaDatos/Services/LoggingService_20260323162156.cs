using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web;

namespace CapaDatos.Services
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
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly string _logDirectory;
        private readonly object _lockObject = new object();
        private static readonly string ApplicationName = "AOCR";

        public LoggingService(string logDirectory = null)
        {
            _logDirectory = logDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_logDirectory))
            {
                try 
                { 
                    Directory.CreateDirectory(_logDirectory); 
                } 
                catch (Exception ex) 
                { 
                    System.Diagnostics.Debug.WriteLine($"No se pudo crear directorio de logs {_logDirectory}: {ex.Message}");
                    // Continuar - los logs irán solo a Debug
                }
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
        }

        public IDisposable BeginScope(LogContext context)
        {
            return new LogScope(context);
        }

        private void WriteLog(string level, string message, Exception ex, LogContext context)
        {
            var timestamp = DateTime.Now;
            var logEntry = BuildLogEntry(timestamp, level, message, ex, context);

            var fileName = string.Format("{0}_{1:yyyyMMdd}.log", ApplicationName, timestamp);
            var filePath = Path.Combine(_logDirectory, fileName);

            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(filePath, logEntry + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception writeEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error escribiendo log a archivo {filePath}: {writeEx.Message}");
                // Continuar - el log ya se escribió a Debug
            }

            System.Diagnostics.Debug.WriteLine(logEntry);
        }

        private string BuildLogEntry(DateTime timestamp, string level, string message, Exception ex, LogContext context)
        {
            var sb = new StringBuilder();

            sb.AppendFormat("{0:yyyy-MM-dd HH:mm:ss.fff}", timestamp);
            sb.AppendFormat(" | {0,-5}", level);

            var correlationId = context?.CorrelationId ?? GetCurrentCorrelationId();
            sb.AppendFormat(" | CID:{0}", string.IsNullOrWhiteSpace(correlationId) ? "N/A" : correlationId);

            if (!string.IsNullOrEmpty(context?.NumeroOrden))
            {
                sb.AppendFormat(" | ORD:{0}", context.NumeroOrden);
            }

            if (!string.IsNullOrEmpty(context?.CodigoSolicitud))
            {
                sb.AppendFormat(" | SOL:{0}", context.CodigoSolicitud);
            }

            var userId = context?.UserId ?? GetCurrentUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                sb.AppendFormat(" | USR:{0}", userId);
            }

            var controller = context?.Controller ?? GetCurrentController();
            var action = context?.Action ?? GetCurrentAction();
            if (!string.IsNullOrEmpty(controller))
            {
                sb.AppendFormat(" | {0}/{1}", controller, string.IsNullOrEmpty(action) ? "?" : action);
            }

            if (!string.IsNullOrEmpty(context?.ErrorCode))
            {
                sb.AppendFormat(" | ERR:{0}", context.ErrorCode);
            }

            sb.AppendFormat(" | {0}", message);

            if (ex != null)
            {
                sb.AppendLine();
                sb.AppendFormat("    Exception: {0}", ex.GetType().Name);
                sb.AppendLine();
                sb.AppendFormat("    StackTrace: {0}", ex.StackTrace);
            }

            return sb.ToString();
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

        private string GetCurrentController()
        {
            try
            {
                var routeData = HttpContext.Current?.Request?.RequestContext?.RouteData;
                var controller = routeData != null ? routeData.Values["controller"] : null;
                return controller == null ? null : controller.ToString();
            }
            catch
            {
                return null;
            }
        }

        private string GetCurrentAction()
        {
            try
            {
                var routeData = HttpContext.Current?.Request?.RequestContext?.RouteData;
                var action = routeData != null ? routeData.Values["action"] : null;
                return action == null ? null : action.ToString();
            }
            catch
            {
                return null;
            }
        }

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
    }
}
