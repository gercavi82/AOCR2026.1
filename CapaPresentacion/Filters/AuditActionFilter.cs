using System;
using System.Web;
using System.Web.Mvc;
using CapaNegocio.Services;

namespace CapaPresentacion.Filters
{
    /// <summary>
    /// Filtro para auditoría automática de acciones
    /// </summary>
    public class AuditActionFilter : ActionFilterAttribute
    {
        private readonly ILoggingService _logger;

        public AuditActionFilter()
        {
            _logger = LoggingServiceFactory.Create();
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            // Generar CorrelationId si no existe
            if (HttpContext.Current.Items["CorrelationId"] == null)
            {
                HttpContext.Current.Items["CorrelationId"] = Guid.NewGuid().ToString("N").Substring(0, 12);
            }

            // Log de inicio de acción para acciones que modifican datos
            if (filterContext.HttpContext.Request.HttpMethod == "POST" ||
                filterContext.HttpContext.Request.HttpMethod == "PUT" ||
                filterContext.HttpContext.Request.HttpMethod == "DELETE")
            {
                var context = BuildLogContext(filterContext);
                _logger.LogInfo(string.Format("Iniciando acción: {0}/{1}",
                    context.Controller, context.Action), context);
            }

            base.OnActionExecuting(filterContext);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            // Log de finalización para acciones que modifican datos
            if (filterContext.HttpContext.Request.HttpMethod == "POST" ||
                filterContext.HttpContext.Request.HttpMethod == "PUT" ||
                filterContext.HttpContext.Request.HttpMethod == "DELETE")
            {
                var context = BuildLogContext(filterContext);

                if (filterContext.Exception != null)
                {
                    _logger.LogError(string.Format("Error en acción: {0}/{1}",
                        context.Controller, context.Action), context);
                }
                else
                {
                    _logger.LogInfo(string.Format("Acción completada: {0}/{1}",
                        context.Controller, context.Action), context);
                }
            }

            base.OnActionExecuted(filterContext);
        }

        private LogContext BuildLogContext(ControllerContext filterContext)
        {
            return new LogContext
            {
                CorrelationId = HttpContext.Current.Items["CorrelationId"] as string,
                Controller = filterContext.RouteData.Values["controller"] as string,
                Action = filterContext.RouteData.Values["action"] as string,
                UserId = filterContext.HttpContext.User?.Identity?.Name,
                NumeroOrden = HttpContext.Current.Items["NumeroOrden"] as string,
                CodigoSolicitud = HttpContext.Current.Items["CodigoSolicitud"] as string
            };
        }
    }

    /// <summary>
    /// Atributo para marcar acciones que requieren auditoría detallada
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class AuditAttribute : ActionFilterAttribute
    {
        public string EntityType { get; set; }
        public string Action { get; set; }

        private readonly ILoggingService _logger;

        public AuditAttribute()
        {
            _logger = LoggingServiceFactory.Create();
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext.Exception == null && !string.IsNullOrEmpty(EntityType))
            {
                var entityId = 0;

                // Intentar obtener ID de la entidad desde RouteData o parámetros
                var idParam = filterContext.RouteData.Values["id"];
                if (idParam != null)
                {
                    int.TryParse(idParam.ToString(), out entityId);
                }

                var context = new LogContext
                {
                    CorrelationId = HttpContext.Current.Items["CorrelationId"] as string,
                    UserId = filterContext.HttpContext.User?.Identity?.Name,
                    NumeroOrden = HttpContext.Current.Items["NumeroOrden"] as string,
                    CodigoSolicitud = HttpContext.Current.Items["CodigoSolicitud"] as string
                };

                _logger.LogAudit(Action ?? filterContext.ActionDescriptor.ActionName, EntityType, entityId, context);
            }

            base.OnActionExecuted(filterContext);
        }
    }
}
