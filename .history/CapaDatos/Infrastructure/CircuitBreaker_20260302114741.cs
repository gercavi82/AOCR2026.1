using System;
using System.Threading;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Estado del Circuit Breaker
    /// </summary>
    public enum CircuitBreakerState
    {
        Closed,     // Normal — permite llamadas
        Open,       // Abierto — bloquea llamadas
        HalfOpen    // Semi-abierto — permite una llamada de prueba
    }

    /// <summary>
    /// Circuit Breaker pattern para proteger conexiones AS400/DB2.
    /// Previene cascadas de fallos cuando el sistema remoto está caído.
    /// Thread-safe mediante Interlocked operations.
    /// </summary>
    public class CircuitBreaker
    {
        #region Configuración

        private readonly int _maxFailures;
        private readonly TimeSpan _openDuration;
        private readonly string _name;

        #endregion

        #region Estado interno (thread-safe)

        private int _failureCount;
        private long _lastFailureTicks;
        private int _state; // 0=Closed, 1=Open, 2=HalfOpen

        private static readonly object _lock = new object();

        #endregion

        #region Constructor

        /// <summary>
        /// Crea un nuevo Circuit Breaker.
        /// </summary>
        /// <param name="name">Nombre descriptivo (para logging)</param>
        /// <param name="maxFailures">Fallos consecutivos antes de abrir el circuito (default: 3)</param>
        /// <param name="openDurationSeconds">Segundos que el circuito permanece abierto (default: 60)</param>
        public CircuitBreaker(string name, int maxFailures = 3, int openDurationSeconds = 60)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name");

            _name = name;
            _maxFailures = maxFailures > 0 ? maxFailures : 3;
            _openDuration = TimeSpan.FromSeconds(openDurationSeconds > 0 ? openDurationSeconds : 60);
            _failureCount = 0;
            _lastFailureTicks = 0;
            _state = (int)CircuitBreakerState.Closed;
        }

        #endregion

        #region Propiedades

        public string Name { get { return _name; } }

        public CircuitBreakerState State
        {
            get
            {
                var currentState = (CircuitBreakerState)Interlocked.CompareExchange(ref _state, 0, 0);

                if (currentState == CircuitBreakerState.Open && HasOpenDurationElapsed())
                {
                    // Transicionar a HalfOpen automáticamente
                    Interlocked.CompareExchange(ref _state, (int)CircuitBreakerState.HalfOpen, (int)CircuitBreakerState.Open);
                    return CircuitBreakerState.HalfOpen;
                }

                return currentState;
            }
        }

        public int FailureCount
        {
            get { return Interlocked.CompareExchange(ref _failureCount, 0, 0); }
        }

        public int MaxFailures { get { return _maxFailures; } }

        public TimeSpan OpenDuration { get { return _openDuration; } }

        public DateTime? LastFailureTime
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastFailureTicks);
                return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;
            }
        }

        public bool IsAvailable
        {
            get { return State != CircuitBreakerState.Open; }
        }

        #endregion

        #region Ejecución protegida

        /// <summary>
        /// Ejecuta una acción protegida por el circuit breaker.
        /// Si el circuito está abierto, lanza CircuitBreakerOpenException.
        /// </summary>
        public T Execute<T>(Func<T> action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            EnsureCircuitAllowsExecution();

            try
            {
                var result = action();
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }

        /// <summary>
        /// Ejecuta una acción sin retorno protegida por el circuit breaker.
        /// </summary>
        public void Execute(Action action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            EnsureCircuitAllowsExecution();

            try
            {
                action();
                OnSuccess();
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }

        /// <summary>
        /// Intenta ejecutar una acción. Retorna false si el circuito está abierto o la acción falla.
        /// No lanza excepciones.
        /// </summary>
        public bool TryExecute<T>(Func<T> action, out T result, out string error)
        {
            result = default(T);
            error = null;

            var currentState = State;
            if (currentState == CircuitBreakerState.Open)
            {
                error = string.Format(
                    "Circuit breaker '{0}' está abierto. Reintentar después de {1:0} segundos.",
                    _name,
                    RemainingOpenSeconds());
                return false;
            }

            try
            {
                result = action();
                OnSuccess();
                return true;
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                error = ex.Message;
                return false;
            }
        }

        #endregion

        #region Retry con backoff exponencial

        /// <summary>
        /// Ejecuta con reintentos y backoff exponencial.
        /// Respeta el circuit breaker en cada intento.
        /// </summary>
        /// <param name="action">Acción a ejecutar</param>
        /// <param name="maxRetries">Máximo de reintentos (default: 3)</param>
        /// <param name="baseDelayMs">Delay base en ms (default: 500)</param>
        /// <param name="isTransient">Función para determinar si el error es transitorio (default: siempre true)</param>
        public T ExecuteWithRetry<T>(
            Func<T> action,
            int maxRetries = 3,
            int baseDelayMs = 500,
            Func<Exception, bool> isTransient = null)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            var transientCheck = isTransient ?? IsTransientDefault;
            Exception lastException = null;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return Execute(action);
                }
                catch (CircuitBreakerOpenException)
                {
                    throw; // No reintentar si el circuito está abierto
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt >= maxRetries || !transientCheck(ex))
                    {
                        throw;
                    }

                    // Backoff exponencial: 500ms, 1000ms, 2000ms, ...
                    var delayMs = baseDelayMs * (int)Math.Pow(2, attempt);
                    var jitter = new Random().Next(0, delayMs / 4); // ±25% jitter
                    Thread.Sleep(delayMs + jitter);
                }
            }

            throw lastException ?? new InvalidOperationException("Retry agotado sin excepción capturada.");
        }

        #endregion

        #region Reset manual

        /// <summary>
        /// Resetea el circuit breaker a estado cerrado.
        /// Usar después de confirmar que el sistema remoto está disponible.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _failureCount, 0);
            Interlocked.Exchange(ref _state, (int)CircuitBreakerState.Closed);
            Interlocked.Exchange(ref _lastFailureTicks, 0);
        }

        /// <summary>
        /// Fuerza la apertura del circuit breaker.
        /// Útil para mantenimiento programado.
        /// </summary>
        public void ForceOpen()
        {
            Interlocked.Exchange(ref _state, (int)CircuitBreakerState.Open);
            Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);
        }

        #endregion

        #region Estado para diagnóstico

        /// <summary>
        /// Obtiene estado detallado para health checks y dashboards.
        /// </summary>
        public CircuitBreakerStatus GetStatus()
        {
            return new CircuitBreakerStatus
            {
                Name = _name,
                State = State,
                FailureCount = FailureCount,
                MaxFailures = _maxFailures,
                OpenDurationSeconds = (int)_openDuration.TotalSeconds,
                LastFailureTime = LastFailureTime,
                RemainingOpenSeconds = State == CircuitBreakerState.Open ? RemainingOpenSeconds() : 0,
                IsAvailable = IsAvailable
            };
        }

        #endregion

        #region Métodos privados

        private void EnsureCircuitAllowsExecution()
        {
            var currentState = State;
            if (currentState == CircuitBreakerState.Open)
            {
                throw new CircuitBreakerOpenException(
                    string.Format(
                        "Circuit breaker '{0}' está abierto. {1} fallos consecutivos detectados. Reintentar en {2:0}s.",
                        _name,
                        _failureCount,
                        RemainingOpenSeconds()),
                    _name);
            }
        }

        private void OnSuccess()
        {
            var currentState = (CircuitBreakerState)Interlocked.CompareExchange(ref _state, 0, 0);

            if (currentState == CircuitBreakerState.HalfOpen)
            {
                // Éxito en HalfOpen → cerrar circuito
                Interlocked.Exchange(ref _failureCount, 0);
                Interlocked.Exchange(ref _state, (int)CircuitBreakerState.Closed);
            }
            else if (currentState == CircuitBreakerState.Closed)
            {
                // Reset contador de fallos en estado normal
                Interlocked.Exchange(ref _failureCount, 0);
            }
        }

        private void OnFailure(Exception ex)
        {
            Interlocked.Exchange(ref _lastFailureTicks, DateTime.UtcNow.Ticks);
            var newCount = Interlocked.Increment(ref _failureCount);

            var currentState = (CircuitBreakerState)Interlocked.CompareExchange(ref _state, 0, 0);

            if (currentState == CircuitBreakerState.HalfOpen)
            {
                // Fallo en HalfOpen → volver a abrir
                Interlocked.Exchange(ref _state, (int)CircuitBreakerState.Open);
            }
            else if (currentState == CircuitBreakerState.Closed && newCount >= _maxFailures)
            {
                // Umbral alcanzado → abrir circuito
                Interlocked.Exchange(ref _state, (int)CircuitBreakerState.Open);
            }
        }

        private bool HasOpenDurationElapsed()
        {
            var ticks = Interlocked.Read(ref _lastFailureTicks);
            if (ticks == 0) return true;

            var elapsed = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            return elapsed >= _openDuration;
        }

        private double RemainingOpenSeconds()
        {
            var ticks = Interlocked.Read(ref _lastFailureTicks);
            if (ticks == 0) return 0;

            var elapsed = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            var remaining = _openDuration - elapsed;
            return remaining.TotalSeconds > 0 ? remaining.TotalSeconds : 0;
        }

        private static bool IsTransientDefault(Exception ex)
        {
            if (ex == null) return false;

            var msg = ex.Message.ToUpperInvariant();
            return msg.Contains("TIMEOUT") ||
                   msg.Contains("CONNECTION") ||
                   msg.Contains("COMMUNICATION") ||
                   msg.Contains("NETWORK") ||
                   msg.Contains("TRANSPORT") ||
                   msg.Contains("UNAVAILABLE") ||
                   msg.Contains("DEADLOCK");
        }

        #endregion
    }

    #region Tipos de soporte

    /// <summary>
    /// Estado detallado del circuit breaker para diagnóstico.
    /// </summary>
    public class CircuitBreakerStatus
    {
        public string Name { get; set; }
        public CircuitBreakerState State { get; set; }
        public int FailureCount { get; set; }
        public int MaxFailures { get; set; }
        public int OpenDurationSeconds { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public double RemainingOpenSeconds { get; set; }
        public bool IsAvailable { get; set; }

        public string StateDescription
        {
            get
            {
                switch (State)
                {
                    case CircuitBreakerState.Closed: return "Cerrado (Normal)";
                    case CircuitBreakerState.Open: return "Abierto (Bloqueado)";
                    case CircuitBreakerState.HalfOpen: return "Semi-Abierto (Prueba)";
                    default: return "Desconocido";
                }
            }
        }

        public string StateBadge
        {
            get
            {
                switch (State)
                {
                    case CircuitBreakerState.Closed: return "success";
                    case CircuitBreakerState.Open: return "danger";
                    case CircuitBreakerState.HalfOpen: return "warning";
                    default: return "secondary";
                }
            }
        }
    }

    /// <summary>
    /// Excepción lanzada cuando el circuit breaker está abierto.
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public string CircuitBreakerName { get; private set; }

        public CircuitBreakerOpenException(string message, string circuitBreakerName)
            : base(message)
        {
            CircuitBreakerName = circuitBreakerName;
        }
    }

    #endregion

    #region Singleton Registry

    /// <summary>
    /// Registro global de circuit breakers.
    /// Permite acceder al mismo circuit breaker desde múltiples DAOs.
    /// </summary>
    public static class CircuitBreakerRegistry
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CircuitBreaker> _breakers
            = new System.Collections.Concurrent.ConcurrentDictionary<string, CircuitBreaker>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Obtiene o crea un circuit breaker por nombre.
        /// </summary>
        public static CircuitBreaker GetOrCreate(string name, int maxFailures = 3, int openDurationSeconds = 60)
        {
            return _breakers.GetOrAdd(name, n => new CircuitBreaker(n, maxFailures, openDurationSeconds));
        }

        /// <summary>
        /// Obtiene todos los circuit breakers registrados (para dashboard).
        /// </summary>
        public static System.Collections.Generic.List<CircuitBreakerStatus> GetAllStatuses()
        {
            var statuses = new System.Collections.Generic.List<CircuitBreakerStatus>();
            foreach (var kvp in _breakers)
            {
                statuses.Add(kvp.Value.GetStatus());
            }
            return statuses;
        }

        /// <summary>
        /// Resetea un circuit breaker específico.
        /// </summary>
        public static bool TryReset(string name)
        {
            CircuitBreaker breaker;
            if (_breakers.TryGetValue(name, out breaker))
            {
                breaker.Reset();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Resetea todos los circuit breakers.
        /// </summary>
        public static void ResetAll()
        {
            foreach (var kvp in _breakers)
            {
                kvp.Value.Reset();
            }
        }
    }

    #endregion
}
