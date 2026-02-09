using System;
using System.Web.Mvc;
using System.Web.Routing;

namespace CapaPresentacion.Infrastructure
{
    /// <summary>
    /// Configuración de routing empresarial para el módulo de Órdenes de Recaudación
    /// Implementa redirecciones automáticas y URLs amigables
    /// </summary>
    public static class OrdenRecaudacionRoutes
    {
        /// <summary>
        /// Registra las rutas específicas del módulo de órdenes
        /// Debe llamarse desde RouteConfig.RegisterRoutes()
        /// </summary>
        public static void RegisterRoutes(RouteCollection routes)
        {
            // Ruta por defecto - Ir a Home que manejará autenticación y redirección
            routes.MapRoute(
                name: "HomeToOrdenDashboard",
                url: "",
                defaults: new { controller = "Home", action = "Index" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            // Dashboard empresarial principal
            routes.MapRoute(
                name: "DashboardOrdenes",
                url: "dashboard-ordenes",
                defaults: new { controller = "OrdenRecaudacionDashboardEmpresarial", action = "Index" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            // APIs del Dashboard
            routes.MapRoute(
                name: "DashboardOrdenesAPI",
                url: "api/dashboard-ordenes/{action}",
                defaults: new { controller = "OrdenRecaudacionDashboardEmpresarial" },
                constraints: new { action = @"^(ObtenerKPIs|ObtenerOrdenes|ObtenerMetricasMensuales|CambiarEstado|AccionRapida|LimpiarCache|HealthCheck)$" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            // Rutas específicas de órdenes con URLs amigables
            routes.MapRoute(
                name: "OrdenRecaudacionNew",
                url: "ordenes/nueva",
                defaults: new { controller = "OrdenRecaudacion", action = "Nueva" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            routes.MapRoute(
                name: "OrdenRecaudacionMy",
                url: "ordenes/mis-ordenes",
                defaults: new { controller = "OrdenRecaudacion", action = "Obligatoria" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            routes.MapRoute(
                name: "OrdenRecaudacionDetail",
                url: "ordenes/detalle/{id}",
                defaults: new { controller = "OrdenRecaudacion", action = "Detalle" },
                constraints: new { id = @"\d+" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            routes.MapRoute(
                name: "OrdenRecaudacionPDF",
                url: "ordenes/pdf/{id}",
                defaults: new { controller = "OrdenRecaudacion", action = "DescargarPdf" },
                constraints: new { id = @"\d+" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            // Rutas de administración (solo para admin/financiero)
            routes.MapRoute(
                name: "OrdenRecaudacionAdmin",
                url: "admin/ordenes/{action}/{id}",
                defaults: new { controller = "OrdenRecaudacion", action = "Index", id = UrlParameter.Optional },
                constraints: new { action = @"^(Index|Todas|Estadisticas|Reportes)$" },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );

            // Ruta genérica del controlador principal (mantener compatibilidad)
            routes.MapRoute(
                name: "OrdenRecaudacion",
                url: "OrdenRecaudacion/{action}/{id}",
                defaults: new { controller = "OrdenRecaudacion", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "CapaPresentacion.Controllers" }
            );
        }

        /// <summary>
        /// Configura restricciones de seguridad adicionales para las rutas
        /// </summary>
        public static void ConfigurarRestricciones()
        {
            // Configuraciones adicionales si son necesarias
            // Por ejemplo, restricciones por IP, horarios, etc.
        }
    }

    /// <summary>
    /// Atributo personalizado para rutas que requieren roles específicos
    /// </summary>
    public class RequireRoleRouteConstraint : IRouteConstraint
    {
        private readonly string[] _requiredRoles;

        public RequireRoleRouteConstraint(params string[] requiredRoles)
        {
            _requiredRoles = requiredRoles ?? new string[0];
        }

        public bool Match(System.Web.HttpContextBase httpContext, Route route, string parameterName, 
                         RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
                return false;

            if (_requiredRoles.Length == 0)
                return true;

            foreach (var role in _requiredRoles)
            {
                if (httpContext.User.IsInRole(role))
                    return true;
            }

            return false;
        }
    }
}