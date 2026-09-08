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
            bundles.Add(new StyleBundle("~/bundles/css")
                .Include("~/Content/bootstrap.min.css")
                .Include("~/Content/aocr-shell.css")
                .Include("~/Content/site.css")
                .Include("~/Content/aocr-institucional.css")
                .Include("~/Content/aocr-modals.css")
                .Include("~/Content/aocr-sidebar.css")
                .Include("~/Content/aocr-contrast.css")
            );

            bundles.Add(new StyleBundle("~/bundles/plugins-css").Include(
                "~/Content/plugins/sweetalert2/sweetalert2.min.css",
                "~/Content/plugins/select2/css/select2.min.css",
                "~/Content/plugins/toastr/toastr.min.css"
            ));

            bundles.Add(new StyleBundle("~/Content/auth")
                .Include("~/Content/bootstrap.min.css")
                .Include("~/Content/plugins/sweetalert2/sweetalert2.min.css")
            );

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

            bundles.Add(new ScriptBundle("~/bundles/plugins").Include(
                "~/Content/plugins/sweetalert2/sweetalert2.min.js",
                "~/Content/plugins/select2/js/select2.full.min.js",
                "~/Content/plugins/toastr/toastr.min.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/app").Include(
                "~/Scripts/app.js",
                "~/Scripts/site.js",
                "~/Scripts/aocr-sidebar.js",
                "~/Scripts/aocr-utils.js"
            ));

            bundles.Add(new ScriptBundle("~/bundles/auth").Include(
                "~/Scripts/jquery-3.6.4.min.js",
                "~/Scripts/bootstrap.bundle.min.js",
                "~/Content/plugins/sweetalert2/sweetalert2.min.js",
                "~/Scripts/aocr-utils.js"
            ));
        }
    }
}
