using System.Web.Mvc;
using System.Web.Routing;

namespace CapaPresentacion
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("favicon.ico");
            routes.IgnoreRoute("Content/imagenes/favicon.ico");
            routes.IgnoreRoute(".well-known/{*pathInfo}");

            // RUTA ESPECÍFICA para el Dashboard - debe ir ANTES de la ruta por defecto
            routes.MapRoute(
                name: "Dashboard",
                url: "Dashboard",
                defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional }
            );

            // RUTA para Detalle de Orden - también específica
            routes.MapRoute(
                name: "OrdenDetalle",
                url: "Orden/Detalle/{id}",
                defaults: new { controller = "Orden", action = "Detalle", id = UrlParameter.Optional }
            );

            // RUTA POR DEFECTO - debe ir ÚLTIMA
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}