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
    }
}
