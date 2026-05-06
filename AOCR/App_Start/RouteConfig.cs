using System.Web.Mvc;
using System.Web.Routing;

namespace AOCR
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "LegacyInspeccionesDetalle",
                url: "inspecciones/detalle/{id}",
                defaults: new { controller = "Inspeccion", action = "Detalle", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "LegacyPagosDetalle",
                url: "pagos/detalle/{id}",
                defaults: new { controller = "Pago", action = "Ver", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "LegacyHallazgosDetalle",
                url: "hallazgos/detalle/{id}",
                defaults: new { controller = "Inspeccion", action = "VerHallazgo", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
