using System;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Interfaz para servicios de logging profesional
    /// </summary>
    public interface ILoggerService
    {
        void LogDebug(string message, params object[] args);
        void LogInfo(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogError(Exception exception, string message, params object[] args);
        void LogFatal(Exception exception, string message, params object[] args);
    }
}
