using System;
using System.Collections.Generic;
using System.Linq;
using CapaNegocio.Services;

namespace CapaNegocio.Integraciones.As400Sync
{
    public static class As400MirrorSyncJob
    {
        public static IList<SyncBatchResult> RunOnceAll()
        {
            var svc = As400MirrorSyncService.CreateDefault();
            var results = svc.RunAllEnabled();
            try
            {
                var opcar5Ok = results.Any(r =>
                    r != null &&
                    string.Equals(r.TableName, "OPCAR5", StringComparison.OrdinalIgnoreCase) &&
                    (r.Status == "OK" || r.Status == "OK_WITH_REJECTIONS"));
                if (opcar5Ok)
                {
                    var reader = new MirrorReadService();
                    reader.SincronizarFr3DesdeEspejo();
                }
            }
            catch (Exception ex)
            {
                LoggingServiceFactory.Create().LogError(ex, new LogContext { Controller = "As400MirrorSyncJob", Action = "RunOnceAll" });
            }
            return results;
        }

        public static SyncBatchResult RunOnceTable(string tableName)
        {
            var svc = As400MirrorSyncService.CreateDefault();
            var result = svc.RunTable(tableName);
            if (string.Equals(tableName, "OPCAR5", StringComparison.OrdinalIgnoreCase) &&
                result != null &&
                (result.Status == "OK" || result.Status == "OK_WITH_REJECTIONS"))
            {
                try
                {
                    var reader = new MirrorReadService();
                    reader.SincronizarFr3DesdeEspejo();
                }
                catch (Exception ex)
                {
                    LoggingServiceFactory.Create().LogError(ex, new LogContext { Controller = "As400MirrorSyncJob", Action = "RunOnceTable", AdditionalData = new Dictionary<string, object> { ["Table"] = tableName } });
                }
            }
            return result;
        }

        public static string Summarize(IList<SyncBatchResult> results)
        {
            if (results == null || results.Count == 0)
            {
                return "Sin ejecuciones.";
            }

            var ok = results.Count(r => r != null && (r.Status == "OK" || r.Status == "OK_WITH_REJECTIONS"));
            var error = results.Count(r => r != null && r.Status == "ERROR");
            var skipped = results.Count(r => r != null && r.Status == "SKIPPED");
            var totalRead = results.Where(r => r != null).Sum(r => r.RowsRead);
            var totalApplied = results.Where(r => r != null).Sum(r => r.RowsApplied);
            var totalRejected = results.Where(r => r != null).Sum(r => r.RowsRejected);
            var totalDeleted = results.Where(r => r != null).Sum(r => r.RowsDeleted);

            return string.Format(
                "MirrorSync resumen => OK:{0} ERROR:{1} SKIPPED:{2} read:{3} applied:{4} rejected:{5} deleted:{6}",
                ok, error, skipped, totalRead, totalApplied, totalRejected, totalDeleted);
        }
    }
}
