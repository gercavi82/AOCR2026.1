using System;
using System.Configuration;
using System.Diagnostics;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Npgsql;

namespace AOCR
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Inyectar configuración desde variables de entorno sobreescribiendo ConfigurationManager

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            AplicarMigracionesStartup();
        }

        private static void AplicarMigracionesStartup()
        {
            try
            {
                var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString;
                if (string.IsNullOrEmpty(cs)) return;

                using (var conn = new NpgsqlConnection(cs))
                {
                    conn.Open();

                    // Npgsql no soporta múltiples sentencias en un solo comando — ejecutar por separado
                    using (var cmd1 = new NpgsqlCommand(
                        "ALTER TABLE aocr_or_orden DROP CONSTRAINT IF EXISTS chk_estado;", conn))
                    {
                        cmd1.ExecuteNonQuery();
                    }

                    using (var cmd2 = new NpgsqlCommand(
                        "ALTER TABLE aocr_or_orden ADD CONSTRAINT chk_estado " +
                        "CHECK (estado IN ('BORRADOR','GENERADA','PENDIENTE','COMPLETADA','FACTURADA','PAGADA','ANULADA'));", conn))
                    {
                        cmd2.ExecuteNonQuery();
                    }

                    Trace.TraceInformation("[Startup] Migración chk_estado aplicada correctamente.");
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[Startup] Error aplicando migración chk_estado: {0}", ex.Message);
            }
        }
    }
}
