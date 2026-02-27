using System;
using System.Collections.Generic;
using System.Linq;

namespace CapaNegocio.Integraciones.As400Sync
{
    public static class As400MirrorSyncJob
    {
        public static IList<SyncBatchResult> RunOnceAll()
        {
            var svc = As400MirrorSyncService.CreateDefault();
            return svc.RunAllEnabled();
        }

        public static SyncBatchResult RunOnceTable(string tableName)
        {
            var svc = As400MirrorSyncService.CreateDefault();
            return svc.RunTable(tableName);
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
