using System;
using System.Diagnostics;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Implementación de logging usando NLog (o fallback a Debug)
    /// </summary>
    public class NLogLoggerService : ILoggerService
    {
        private readonly string _loggerName;
        private readonly bool _useDebugFallback;

        public NLogLoggerService(string loggerName)
        {
            _loggerName = loggerName ?? "AOCR";
            
            // Verificar si NLog está disponible
            _useDebugFallback = !IsNLogAvailable();
        }

        private bool IsNLogAvailable()
        {
            try
            {
                var type = Type.GetType("NLog.LogManager, NLog");
                return type != null;
            }
            catch
            {
                return false;
            }
        }

        public void LogDebug(string message, params object[] args)
        {
            if (_useDebugFallback)
            {
                Debug.WriteLine($"[DEBUG] [{_loggerName}] {string.Format(message, args)}");
                return;
            }

            try
            {
                var logger = GetNLogLogger();
                logger?.GetType().GetMethod("Debug", new[] { typeof(string), typeof(object[]) })
                    ?.Invoke(logger, new object[] { message, args });
            }
            catch
            {
                Debug.WriteLine($"[DEBUG] [{_loggerName}] {string.Format(message, args)}");
            }
        }

        public void LogInfo(string message, params object[] args)
        {
            if (_useDebugFallback)
            {
                Debug.WriteLine($"[INFO] [{_loggerName}] {string.Format(message, args)}");
                return;
            }

            try
            {
                var logger = GetNLogLogger();
                logger?.GetType().GetMethod("Info", new[] { typeof(string), typeof(object[]) })
                    ?.Invoke(logger, new object[] { message, args });
            }
            catch
            {
                Debug.WriteLine($"[INFO] [{_loggerName}] {string.Format(message, args)}");
            }
        }

        public void LogWarning(string message, params object[] args)
        {
            if (_useDebugFallback)
            {
                Debug.WriteLine($"[WARN] [{_loggerName}] {string.Format(message, args)}");
                return;
            }

            try
            {
                var logger = GetNLogLogger();
                logger?.GetType().GetMethod("Warn", new[] { typeof(string), typeof(object[]) })
                    ?.Invoke(logger, new object[] { message, args });
            }
            catch
            {
                Debug.WriteLine($"[WARN] [{_loggerName}] {string.Format(message, args)}");
            }
        }

        public void LogError(string message, params object[] args)
        {
            if (_useDebugFallback)
            {
                Debug.WriteLine($"[ERROR] [{_loggerName}] {string.Format(message, args)}");
                return;
            }

            try
            {
                var logger = GetNLogLogger();
                logger?.GetType().GetMethod("Error", new[] { typeof(string), typeof(object[]) })
                    ?.Invoke(logger, new object[] { message, args });
            }
            catch
            {
                Debug.WriteLine($"[ERROR] [{_loggerName}] {string.Format(message, args)}");
            }
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            if (_useDebugFallback)
            {
                Debug.WriteLine($"[ERROR] [{_loggerName}] {string.Format(message, args)} - Exception: {exception}");
                return;
            }

            try
            {
                var logger = GetNLogLogger();
                logger?.GetType().GetMethod("Error", new[] { typeof(Exception), typeof(string), typeof(object[]) })
                    ?.Invoke(logger, new object[] { exception, message, args });
            }
            catch
            {
                Debug.WriteLine($"[ERROR] [{_loggerName}] {string.Format(message, args)} - Exception: {exception}");
            }
        }

        public void LogFatal(Exception exception, string message, params object[] args)
        {
            if (_useDebugFallback)
            {
                Debug.WriteLine($"[FATAL] [{_loggerName}] {string.Format(message, args)} - Exception: {exception}");
                return;
            }

            try
            {
                var logger = GetNLogLogger();
                logger?.GetType().GetMethod("Fatal", new[] { typeof(Exception), typeof(string), typeof(object[]) })
                    ?.Invoke(logger, new object[] { exception, message, args });
            }
            catch
            {
                Debug.WriteLine($"[FATAL] [{_loggerName}] {string.Format(message, args)} - Exception: {exception}");
            }
        }

        private object GetNLogLogger()
        {
            try
            {
                var logManagerType = Type.GetType("NLog.LogManager, NLog");
                var method = logManagerType?.GetMethod("GetLogger", new[] { typeof(string) });
                return method?.Invoke(null, new object[] { _loggerName });
            }
            catch
            {
                return null;
            }
        }
    }
}
