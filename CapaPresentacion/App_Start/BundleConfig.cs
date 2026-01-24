using System.Web.Optimization;

namespace CapaPresentacion
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            // En DEBUG puedes dejar false, en PROD true
            BundleTable.EnableOptimizations = !System.Web.HttpContext.Current.IsDebuggingEnabled;

            // ======================
            // CSS
            // ======================
            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/bootstrap.min.css",
                "~/Content/adminlte.min.css",
                "~/Content/site.css",
                "~/Content/fontawesome-all.min.css"
            ));

            bundles.Add(new StyleBundle("~/Content/plugins-css").Include(
                // DataTables Bootstrap 5 (local)
                "~/Content/DataTables/css/dataTables.bootstrap5.min.css",
                "~/Content/DataTables/css/responsive.bootstrap5.min.css",

                "~/Content/sweetalert2/sweetalert2.min.css",
                "~/Content/select2/css/select2.min.css",
                "~/Content/toastr/toastr.min.css"
            ));

            // ======================
            // JS (ORDEN CRÍTICO)
            // ======================
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                "~/Scripts/jquery-3.6.4.min.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                "~/Scripts/bootstrap.bundle.min.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                "~/Scripts/jquery.validate.min.js",
                "~/Scripts/jquery.validate.unobtrusive.min.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/datatables").Include(
                "~/Scripts/DataTables/jquery.dataTables.min.js",
                "~/Scripts/DataTables/dataTables.bootstrap5.min.js",
                "~/Scripts/DataTables/dataTables.responsive.min.js",
                "~/Scripts/DataTables/responsive.bootstrap5.min.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/plugins").Include(
                "~/Scripts/sweetalert2/sweetalert2.min.js",
                "~/Scripts/select2/select2.full.min.js",
                "~/Scripts/toastr/toastr.min.js",
                "~/Scripts/adminlte.min.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/app").Include(
                "~/Scripts/app/global.js",
                "~/Scripts/app/notifications.js",
                "~/Scripts/app/forms.js"
            ));
        }
    }
}
