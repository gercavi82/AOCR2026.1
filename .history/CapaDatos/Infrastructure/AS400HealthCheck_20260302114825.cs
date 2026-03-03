using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Globalization;
using CapaDatos.Infrastructure;
using CapaDatos.Services;

namespace CapaDatos.Infrastructure
{
    /// <summary>
    /// Health check estructurado para conexiones AS400/DB2.
    /// Provee información detallada de estado para dashboards y monitoreo.
    /// </summary>
    public class AS400HealthCheck
    {
        private readonly ISecureConfigurationService _configService;
        private static readonly CircuitBreaker _circuitBreaker =
            CircuitBreakerRegistry.GetOrCreate("AS400_HEALTH", maxFailures: 5, openDurationSeconds: 120);

        public AS400HealthCheck(ISecureConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException("configService");
        }

        public AS400HealthCheck() : this(new SecureConfigurationService())
        {
        }

        /// <summary>
        /// Ejecuta health check completo del sistema AS400.
        /// </summary>
        public AS400HealthResult CheckHealth()
        {
            var result = new AS400HealthResult
            {
                Timestamp = DateTime.UtcNow,
                CheckedBy = "AS400HealthCheck"
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // 1. Verificar configuración
                var creds = _configService.GetAS400Credentials();
                result.ServerConfigured = !string.IsNullOrWhiteSpace(creds.Server);
                result.DatabaseConfigured = !string.IsNullOrWhiteSpace(creds.Library) || !string.IsNullOrWhiteSpace(creds.Database);
                result.Server = creds.Server ?? "(no configurado)";
                result.Database = creds.Library ?? creds.Database ?? "(no configurado)";

                if (!result.ServerConfigured)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Message = "Servidor AS400 no configurado en Web.config/variables de entorno.";
                    return result;
                }

                // 2. Verificar facturación habilitada
                var facturacionEnabled = _configService.GetAppSetting("AS400:Facturacion:Enabled");
                result.FacturacionEnabled = string.Equals(facturacionEnabled, "true", StringComparison.OrdinalIgnoreCase);

                // 3. Verificar circuit breaker
                result.CircuitBreaker = _circuitBreaker.GetStatus();
                if (!_circuitBreaker.IsAvailable)
                {
                    result.Status = HealthStatus.Unhealthy;
                    result.Message = string.Format(
                        "Circuit breaker abierto. {0} fallos consecutivos. Reintentar en {1:0}s.",
                        result.CircuitBreaker.FailureCount,
                        result.CircuitBreaker.RemainingOpenSeconds);
                    return result;
                }

                // 4. Intentar conexión
                result.ConnectionResult = TestConnection(creds);
                sw.Stop();
                result.ResponseTimeMs = sw.ElapsedMilliseconds;

                if (result.ConnectionResult.Success)
                {
                    result.Status = HealthStatus.Healthy;
                    result.Message = string.Format(
                        "Conexión exitosa. Respuesta en {0}ms.",
                        result.ResponseTimeMs);

                    // 5. Verificar tablas FR3 si la conexión funciona
                    if (result.FacturacionEnabled)
                    {
                        result.TableChecks = VerifyFr3Tables(creds);
                    }
                }
                else
                {
                    result.Status = HealthStatus.Unhealthy;
                    result.Message = result.ConnectionResult.Error;
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result.ResponseTimeMs = sw.ElapsedMilliseconds;
                result.Status = HealthStatus.Unhealthy;
                result.Message = "Error inesperado: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Health check rápido (solo conectividad, sin verificar tablas).
        /// </summary>
        public bool IsHealthy(out string message)
        {
            message = null;

            if (!_circuitBreaker.IsAvailable)
            {
                message = "Circuit breaker abierto para AS400.";
                return false;
            }

            try
            {
                var creds = _configService.GetAS400Credentials();
                var connectionResult = TestConnection(creds);
                message = connectionResult.Success ? "OK" : connectionResult.Error;
                return connectionResult.Success;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private ConnectionTestResult TestConnection(AS400Credentials creds)
        {
            var testResult = new ConnectionTestResult();

            try
            {
                using (var dao = new TestableAS400DAO(_configService))
                {
                    string testMessage;
                    testResult.Success = dao.ExternalTestConnection(out testMessage);
                    testResult.Error = testMessage;

                    if (testResult.Success)
                    {
                        testResult.DriverInfo = dao.GetDriverInfo();
                    }
                }
            }
            catch (Exception ex)
            {
                testResult.Success = false;
                testResult.Error = ex.Message;
            }

            return testResult;
        }

        private List<TableCheckResult> VerifyFr3Tables(AS400Credentials creds)
        {
            var results = new List<TableCheckResult>();
            var library = (creds.Library ?? creds.Database ?? "DGACDAT").Trim().ToUpperInvariant();
            var tables = new[] { "OPCAR5", "OPCAR6", "OPSARC" };

            try
            {
                using (var dao = new TestableAS400DAO(_configService))
                {
                    foreach (var table in tables)
                    {
                        var check = new TableCheckResult { TableName = table };
                        try
                        {
                            check.Exists = dao.TableExists(library, table);
                            if (check.Exists)
                            {
                                check.ColumnCount = dao.GetColumnCount(library, table);
                                check.RowCount = dao.GetApproximateRowCount(library, table);
                            }
                        }
                        catch (Exception ex)
                        {
                            check.Error = ex.Message;
                        }
                        results.Add(check);
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new TableCheckResult { TableName = "ERROR", Error = ex.Message });
            }

            return results;
        }

        /// <summary>
        /// DAO interno solo para health checks (no expone operaciones de negocio).
        /// </summary>
        private class TestableAS400DAO : AS400BaseDAO
        {
            public TestableAS400DAO(ISecureConfigurationService configService) : base(configService, 15) { }

            public bool ExternalTestConnection(out string message)
            {
                return TryTestConnection(out message);
            }

            public string GetDriverInfo()
            {
                try
                {
                    return ExecuteWithConnection<string>(conn =>
                    {
                        return string.Format("Driver: {0}, ServerVersion: {1}",
                            conn.Driver ?? "N/A",
                            conn.ServerVersion ?? "N/A");
                    });
                }
                catch
                {
                    return "No disponible";
                }
            }

            public bool TableExists(string schema, string table)
            {
                return ExecuteWithConnection<bool>(conn =>
                {
                    var sql = @"SELECT COUNT(*) FROM QSYS2.SYSTABLES 
                                WHERE TABLE_SCHEMA = ? AND TABLE_NAME = ?";
                    using (var cmd = CreateCommand(conn, sql))
                    {
                        AddParameter(cmd, schema, OdbcType.VarChar);
                        AddParameter(cmd, table, OdbcType.VarChar);
                        var count = cmd.ExecuteScalar();
                        return count != null && Convert.ToInt32(count) > 0;
                    }
                });
            }

            public int GetColumnCount(string schema, string table)
            {
                return ExecuteWithConnection<int>(conn =>
                {
                    var sql = @"SELECT COUNT(*) FROM QSYS2.SYSCOLUMNS 
                                WHERE TABLE_SCHEMA = ? AND TABLE_NAME = ?";
                    using (var cmd = CreateCommand(conn, sql))
                    {
                        AddParameter(cmd, schema, OdbcType.VarChar);
                        AddParameter(cmd, table, OdbcType.VarChar);
                        var count = cmd.ExecuteScalar();
                        return count != null ? Convert.ToInt32(count) : 0;
                    }
                });
            }

            public long GetApproximateRowCount(string schema, string table)
            {
                try
                {
                    return ExecuteWithConnection<long>(conn =>
                    {
                        var sql = string.Format(
                            "SELECT COUNT(*) FROM {0}.{1} FETCH FIRST 1 ROWS ONLY",
                            schema, table);
                        using (var cmd = CreateCommand(conn, sql))
                        {
                            var result = cmd.ExecuteScalar();
                            return result != null ? Convert.ToInt64(result) : -1;
                        }
                    });
                }
                catch
                {
                    return -1; // No se pudo contar
                }
            }
        }
    }

    #region Result Types

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }

    public class AS400HealthResult
    {
        public HealthStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public string CheckedBy { get; set; }
        public long ResponseTimeMs { get; set; }

        // Configuración
        public string Server { get; set; }
        public string Database { get; set; }
        public bool ServerConfigured { get; set; }
        public bool DatabaseConfigured { get; set; }
        public bool FacturacionEnabled { get; set; }

        // Conexión
        public ConnectionTestResult ConnectionResult { get; set; }

        // Circuit Breaker
        public CircuitBreakerStatus CircuitBreaker { get; set; }

        // Tablas FR3
        public List<TableCheckResult> TableChecks { get; set; }

        public string StatusBadge
        {
            get
            {
                switch (Status)
                {
                    case HealthStatus.Healthy: return "success";
                    case HealthStatus.Degraded: return "warning";
                    case HealthStatus.Unhealthy: return "danger";
                    default: return "secondary";
                }
            }
        }

        public string StatusIcon
        {
            get
            {
                switch (Status)
                {
                    case HealthStatus.Healthy: return "fa-circle-check";
                    case HealthStatus.Degraded: return "fa-triangle-exclamation";
                    case HealthStatus.Unhealthy: return "fa-circle-xmark";
                    default: return "fa-question-circle";
                }
            }
        }
    }

    public class ConnectionTestResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string DriverInfo { get; set; }
    }

    public class TableCheckResult
    {
        public string TableName { get; set; }
        public bool Exists { get; set; }
        public int ColumnCount { get; set; }
        public long RowCount { get; set; }
        public string Error { get; set; }

        public string StatusBadge
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Error)) return "danger";
                return Exists ? "success" : "warning";
            }
        }
    }

    #endregion
}
