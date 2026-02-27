using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;
using CapaNegocio.Integraciones.As400Sync;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Dashboard y control del módulo de sincronización AS400 → PostgreSQL (mirror).
    /// Ruta base: /SyncAdmin
    /// Acceso: solo Administrador.  No afecta ninguna ruta/funcionalidad existente.
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class SyncAdminController : Controller
    {
        private readonly ILoggingService _logger;

        public SyncAdminController()
        {
            _logger = LoggingServiceFactory.Create();
        }

        // GET: /SyncAdmin/
        public ActionResult Index()
        {
            var vm = new SyncAdminVM();

            try
            {
                var reader = new MirrorReadService();
                vm.Watermarks = reader.ObtenerEstadoSync();
                vm.UltimosLotes = reader.ObtenerUltimosLotes(30);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SyncAdmin.Index: error al leer estado sync: " + ex.Message);
                vm.Watermarks = new List<MirrorSyncStatusDto>();
                vm.UltimosLotes = new List<SyncBatchResult>();
                TempData["SyncError"] = "No se pudo cargar el estado del sync: " + ex.Message +
                    ". Asegúrese de haber ejecutado los scripts SQL (001-003b) y que Sync:Enabled=true.";
            }

            vm.SyncHabilitado = ParseBool(ConfigurationManager.AppSettings["Sync:Enabled"]);
            vm.DryRun = ParseBool(ConfigurationManager.AppSettings["Sync:DryRun"]);
            vm.BatchSize = ParseInt(ConfigurationManager.AppSettings["Sync:BatchSize"], 1000);
            vm.Schedule = ConfigurationManager.AppSettings["Sync:Schedule"] ?? "No configurado";

            return View(vm);
        }

        // POST: /SyncAdmin/RunAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunAll()
        {
            try
            {
                if (!ParseBool(ConfigurationManager.AppSettings["Sync:Enabled"]))
                {
                    TempData["SyncError"] = "Sync:Enabled=false en web.config. Actívelo para ejecutar.";
                    return RedirectToAction("Index");
                }

                _logger.LogInfo("SyncAdmin.RunAll: iniciando ejecución manual por " + User.Identity.Name);
                var results = As400MirrorSyncJob.RunOnceAll();
                TempData["SyncOK"] = "Sync completado. " + As400MirrorSyncJob.Summarize(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    Controller = "SyncAdmin",
                    Action = "RunAll",
                    UserId = User.Identity.Name
                });
                TempData["SyncError"] = "Error ejecutando sync: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: /SyncAdmin/RunTable
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                TempData["SyncError"] = "Nombre de tabla no especificado.";
                return RedirectToAction("Index");
            }

            try
            {
                if (!ParseBool(ConfigurationManager.AppSettings["Sync:Enabled"]))
                {
                    TempData["SyncError"] = "Sync:Enabled=false. Actívelo en web.config primero.";
                    return RedirectToAction("Index");
                }

                _logger.LogInfo("SyncAdmin.RunTable: tabla=" + tableName + " usuario=" + User.Identity.Name);
                var result = As400MirrorSyncJob.RunOnceTable(tableName);
                TempData["SyncOK"] = string.Format(
                    "Tabla [{0}] sync: {1} | leidas={2} aplicadas={3} rechazadas={4} eliminadas={5}",
                    tableName, result.Status, result.RowsRead, result.RowsApplied, result.RowsRejected, result.RowsDeleted);

                if (!string.IsNullOrWhiteSpace(result.Error))
                    TempData["SyncError"] = result.Error;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, new LogContext
                {
                    Controller = "SyncAdmin",
                    Action = "RunTable",
                    UserId = User.Identity.Name,
                    AdditionalData = new Dictionary<string, object> { ["Table"] = tableName }
                });
                TempData["SyncError"] = "Error en tabla " + tableName + ": " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // GET: /SyncAdmin/Status (JSON para polling)
        [HttpGet]
        public JsonResult Status()
        {
            try
            {
                var reader = new MirrorReadService();
                var watermarks = reader.ObtenerEstadoSync();
                var lotes = reader.ObtenerUltimosLotes(5);

                return Json(new
                {
                    ok = true,
                    syncEnabled = ParseBool(ConfigurationManager.AppSettings["Sync:Enabled"]),
                    watermarks = watermarks.Select(w => new
                    {
                        tabla = w.Tabla,
                        estado = w.Estado,
                        ultimaSync = w.UltimaSync.HasValue ? w.UltimaSync.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-",
                        error = w.UltimoError
                    }),
                    ultimosLotes = lotes.Select(l => new
                    {
                        tabla = l.TableName,
                        estado = l.Status,
                        leidas = l.RowsRead,
                        aplicadas = l.RowsApplied,
                        rechazadas = l.RowsRejected,
                        duracionMs = l.Duration.TotalMilliseconds
                    })
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: /SyncAdmin/Fr3
        public ActionResult Fr3(string aeropuerto = null, string anio = null, int take = 50)
        {
            var vm = new SyncFr3VM
            {
                Aeropuerto = aeropuerto,
                Anio = anio ?? DateTime.Now.Year.ToString()
            };

            try
            {
                var reader = new MirrorReadService();
                vm.Registros = reader.ListarFr3Recientes(take, aeropuerto, anio ?? DateTime.Now.Year.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning("SyncAdmin.Fr3: " + ex.Message);
                vm.Registros = new List<MirrorFr3CabeceraDto>();
                TempData["SyncError"] = "No se pudo consultar el espejo de FR3: " + ex.Message;
            }

            return View(vm);
        }

        // ──────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────
        private static bool ParseBool(string v) =>
            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);

        private static int ParseInt(string v, int def)
        {
            return int.TryParse(v, out var r) ? r : def;
        }
    }

    // ──────────────────────────────────────
    // ViewModels ligeros (en mismo archivo para no dispersar)
    // ──────────────────────────────────────
    public class SyncAdminVM
    {
        public IList<MirrorSyncStatusDto> Watermarks { get; set; } = new List<MirrorSyncStatusDto>();
        public IList<SyncBatchResult> UltimosLotes { get; set; } = new List<SyncBatchResult>();
        public bool SyncHabilitado { get; set; }
        public bool DryRun { get; set; }
        public int BatchSize { get; set; }
        public string Schedule { get; set; }
    }

    public class SyncFr3VM
    {
        public string Aeropuerto { get; set; }
        public string Anio { get; set; }
        public IList<MirrorFr3CabeceraDto> Registros { get; set; } = new List<MirrorFr3CabeceraDto>();
    }
}
