using System.Web.Mvc;
using System.Web.Routing;

namespace CapaPresentacion
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Ruta personalizada para validaciones de Dirección (Opcional pero recomendado)
            routes.MapRoute(
                name: "ValidacionDireccion",
                url: "Direccion/Validacion/{codigoSolicitud}",
                defaults: new { controller = "Direccion", action = "ValidacionFinal", codigoSolicitud = UrlParameter.Optional }
            );

            // Ruta por defecto (Cubre /Tecnico/ListaChequeo/5 y similares)
            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}