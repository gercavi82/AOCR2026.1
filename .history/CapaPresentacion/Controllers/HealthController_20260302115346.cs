using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Mvc;
using CapaDatos.Infrastructure;
using CapaDatos.Services;
using CapaNegocio.Services;
using Npgsql;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controller para health checks y monitoreo
    /// </summary>
    public class HealthController : Controller
    {
        private readonly ISecureConfigurationService _config;

        public HealthController()
        {
            _config = new SecureConfigurationService();
        }

        /// <summary>
        /// Health check básico - retorna 200 si la app responde
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            return Json(new { status = "healthy", timestamp = DateTime.UtcNow }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Health check detallado - verifica dependencias
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Details()
        {
            var checks = new Dictionary<string, object>();
            var overallHealthy = true;

            // 1. Verificar PostgreSQL
            try
            {
                var pgHealthy = CheckPostgreSQL();
                checks["postgresql"] = new { healthy = pgHealthy, message = pgHealthy ? "OK" : "Connection failed" };
                if (!pgHealthy) overallHealthy = false;
            }
            catch (Exception ex)
            {
                checks["postgresql"] = new { healthy = false, message = ex.Message };
                overallHealthy = false;
            }

            // 2. Verificar escritura en disco
            try
            {
                var diskHealthy = CheckDiskWrite();
                checks["disk"] = new { healthy = diskHealthy, message = diskHealthy ? "OK" : "Write failed" };
                if (!diskHealthy) overallHealthy = false;
            }
            catch (Exception ex)
            {
                checks["disk"] = new { healthy = false, message = ex.Message };
                overallHealthy = false;
            }

            // 3. Verificar memoria disponible
            try
            {
                var memInfo = CheckMemory();
                checks["memory"] = memInfo;
            }
            catch (Exception ex)
            {
                checks["memory"] = new { healthy = false, message = ex.Message };
            }

            // 4. Verificar cola de correos
            try
            {
                var queueInfo = CheckEmailQueue();
                checks["email_queue"] = queueInfo;
            }
            catch (Exception ex)
            {
                checks["email_queue"] = new { healthy = true, message = "Not available: " + ex.Message };
            }

            var result = new
            {
                status = overallHealthy ? "healthy" : "unhealthy",
                timestamp = DateTime.UtcNow,
                version = GetVersion(),
                checks = checks
            };

            Response.StatusCode = overallHealthy ? 200 : 503;
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Liveness probe - solo verifica que la app responde
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Live()
        {
            return Content("OK", "text/plain");
        }

        /// <summary>
        /// Readiness probe - verifica que puede atender requests
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Ready()
        {
            var ready = CheckPostgreSQL();
            Response.StatusCode = ready ? 200 : 503;
            return Content(ready ? "READY" : "NOT READY", "text/plain");
        }

        #region Private Methods

        private bool CheckPostgreSQL()
        {
            try
            {
                var connStr = _config.GetConnectionString("PostgreSQL");
                if (string.IsNullOrEmpty(connStr)) return false;

                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT 1", conn))
                    {
                        cmd.ExecuteScalar();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool CheckDiskWrite()
        {
            try
            {
                var testPath = Path.Combine(Server.MapPath("~/App_Data"), "health_check_" + Guid.NewGuid() + ".tmp");
                System.IO.File.WriteAllText(testPath, "test");
                System.IO.File.Delete(testPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private object CheckMemory()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            return new
            {
                healthy = true,
                working_set_mb = process.WorkingSet64 / 1024 / 1024,
                private_memory_mb = process.PrivateMemorySize64 / 1024 / 1024
            };
        }

        private object CheckEmailQueue()
        {
            try
            {
                var connStr = _config.GetConnectionString("PostgreSQL");
                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    var estadoCol = "status";
                    using (var cmdDetect = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM information_schema.columns WHERE table_name='email_queue' AND column_name='status'", conn))
                    {
                        var hasStatus = Convert.ToInt32(cmdDetect.ExecuteScalar()) > 0;
                        estadoCol = hasStatus ? "status" : "estado";
                    }

                    var sql = string.Format("SELECT {0}, COUNT(*) FROM email_queue GROUP BY {0}", estadoCol);
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        var stats = new Dictionary<string, int>();
                        while (reader.Read())
                        {
                            stats[reader.GetString(0)] = reader.GetInt32(1);
                        }
                        
                        var pendientes = stats.ContainsKey("PENDIENTE") ? stats["PENDIENTE"] : 0;
                        return new
                        {
                            healthy = pendientes < 100,
                            pending = pendientes,
                            stats = stats
                        };
                    }
                }
            }
            catch
            {
                return new { healthy = true, message = "Queue check not available" };
            }
        }

        private string GetVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        #endregion

        #region AS400 / DB2 Health Checks

        /// <summary>
        /// Health check completo del sistema AS400/DB2.
        /// Solo para Administrador y Financiero.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Administrador,Financiero")]
        public ActionResult As400()
        {
            try
            {
                var healthCheck = new AS400HealthCheck(_config);
                var result = healthCheck.CheckHealth();

                return Json(new
                {
                    status = result.Status.ToString().ToLower(),
                    status_badge = result.StatusBadge,
                    message = result.Message,
                    timestamp = result.Timestamp.ToString("o"),
                    response_time_ms = result.ResponseTimeMs,
                    server = result.Server,
                    database = result.Database,
                    server_configured = result.ServerConfigured,
                    database_configured = result.DatabaseConfigured,
                    facturacion_enabled = result.FacturacionEnabled,
                    connection = result.ConnectionResult != null ? new
                    {
                        success = result.ConnectionResult.Success,
                        error = result.ConnectionResult.Error,
                        driver_info = result.ConnectionResult.DriverInfo
                    } : null,
                    circuit_breaker = result.CircuitBreaker != null ? new
                    {
                        name = result.CircuitBreaker.Name,
                        state = result.CircuitBreaker.State.ToString().ToLower(),
                        state_description = result.CircuitBreaker.StateDescription,
                        state_badge = result.CircuitBreaker.StateBadge,
                        failure_count = result.CircuitBreaker.FailureCount,
                        max_failures = result.CircuitBreaker.MaxFailures,
                        remaining_open_seconds = Math.Round(result.CircuitBreaker.RemainingOpenSeconds, 1),
                        is_available = result.CircuitBreaker.IsAvailable
                    } : null,
                    tables = result.TableChecks
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    status = "unhealthy",
                    message = "Error: " + ex.Message,
                    timestamp = DateTime.UtcNow.ToString("o")
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Estado de todos los circuit breakers registrados.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public ActionResult CircuitBreakers()
        {
            var statuses = CircuitBreakerRegistry.GetAllStatuses();
            var result = new List<object>();

            foreach (var status in statuses)
            {
                result.Add(new
                {
                    name = status.Name,
                    state = status.State.ToString().ToLower(),
                    state_description = status.StateDescription,
                    state_badge = status.StateBadge,
                    failure_count = status.FailureCount,
                    max_failures = status.MaxFailures,
                    remaining_open_seconds = Math.Round(status.RemainingOpenSeconds, 1),
                    is_available = status.IsAvailable
                });
            }

            return Json(new { circuit_breakers = result, timestamp = DateTime.UtcNow.ToString("o") },
                JsonRequestBehavior.AllowGet);
        }

        /// <summary>
        /// Resetea un circuit breaker específico (Admin only).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult ResetCircuitBreaker(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Nombre requerido." });

            var ok = CircuitBreakerRegistry.TryReset(name);
            return Json(new
            {
                success = ok,
                message = ok ? "Circuit breaker reseteado." : "Circuit breaker no encontrado."
            });
        }

        /// <summary>
        /// Estadísticas de sincronización FR3 y cola de reintentos.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Administrador,Financiero")]
        public ActionResult SyncStats()
        {
            try
            {
                var syncService = new SyncLogService();
                var syncStats = syncService.ObtenerEstadisticas();

                var retryService = new Fr3RetryService();
                var retryStats = retryService.ObtenerEstadisticas();

                return Json(new
                {
                    sync = new
                    {
                        total = syncStats.Total,
                        completados = syncStats.Completados,
                        errores = syncStats.Errores,
                        reintentando = syncStats.Reintentando,
                        en_proceso = syncStats.EnProceso,
                        duracion_promedio_ms = syncStats.DuracionPromedioMs,
                        tasa_exito = syncStats.TasaExito,
                        ultimo_registro = syncStats.UltimoRegistro?.ToString("o")
                    },
                    retry_queue = new
                    {
                        pendientes = retryStats.Pendientes,
                        en_proceso = retryStats.EnProceso,
                        completados = retryStats.Completados,
                        fallidos = retryStats.Fallidos,
                        cancelados = retryStats.Cancelados,
                        total = retryStats.Total,
                        proximo_intento = retryStats.ProximoIntento?.ToString("o")
                    },
                    facturacion_enabled = FacturacionAS400Service.IsEnabled(),
                    timestamp = DateTime.UtcNow.ToString("o")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message, timestamp = DateTime.UtcNow.ToString("o") },
                    JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Procesar reintentos FR3 pendientes (Admin only).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessFr3Retries()
        {
            try
            {
                var service = new Fr3RetryService();
                var result = service.ProcesarPendientes(10);

                return Json(new
                {
                    success = true,
                    total_en_cola = result.TotalEnCola,
                    exitosos = result.Exitosos,
                    fallidos = result.Fallidos,
                    mensaje = result.Mensaje
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        #endregion
    }
}

