using System.Web.Mvc;
using System.Web.Routing;
using CapaPresentacion.Infrastructure;

namespace CapaPresentacion
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // 🎯 RUTAS EMPRESARIALES DE ÓRDENES DE RECAUDACIÓN
            OrdenRecaudacionRoutes.RegisterRoutes(routes);

            // RUTA ESPECÍFICA para el Dashboard - debe ir ANTES de la ruta por defecto
            routes.MapRoute(
                name: "Dashboard",
                url: "Dashboard",
                defaults: new { controller = "OrdenRecaudacionDashboardEmpresarial", action = "Index", id = UrlParameter.Optional }
            );

            // RUTA para Detalle de Orden - también específica
            routes.MapRoute(
                name: "OrdenDetalle",
                url: "Orden/Detalle/{id}",
                defaults: new { controller = "Orden", action = "Detalle", id = UrlParameter.Optional }
            );

            // RUTA POR DEFECTO - debe ir ÚLTIMA - Usar Home como controlador por defecto
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}