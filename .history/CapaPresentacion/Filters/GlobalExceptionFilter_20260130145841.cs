using System;
using System.Net;
using System.Web;
using System.Web.Mvc;
using CapaNegocio.Services;
using CapaDatos.Infrastructure;

namespace CapaPresentacion.Filters
{
    /// <summary>
    /// Filtro global para manejo centralizado de excepciones.
    /// No expone stacktrace ni detalles internos en producción.
    /// </summary>
    public class GlobalExceptionFilter : IExceptionFilter
    {
        private readonly ILoggingService _logger;

        public GlobalExceptionFilter()
        {
            _logger = LoggingServiceFactory.Create();
        }

        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.ExceptionHandled)
            {
                return;
            }

            var exception = filterContext.Exception;
            var controllerName = filterContext.RouteData.Values["controller"] as string ?? "Unknown";
            var actionName = filterContext.RouteData.Values["action"] as string ?? "Unknown";

            // Obtener contexto de correlación si existe
            var correlationId = HttpContext.Current?.Items["CorrelationId"] as string ?? Guid.NewGuid().ToString("N");
            var numeroOrden = HttpContext.Current?.Items["NumeroOrden"] as string;
            var codigoSolicitud = HttpContext.Current?.Items["CodigoSolicitud"] as string;

            // Determinar código de error y mensaje para usuario
            var errorInfo = ClassifyException(exception);

            // Log estructurado del error
            _logger.LogError(exception, new LogContext
            {
                CorrelationId = correlationId,
                Controller = controllerName,
                Action = actionName,
                NumeroOrden = numeroOrden,
                CodigoSolicitud = codigoSolicitud,
                ErrorCode = errorInfo.ErrorCode,
                UserId = filterContext.HttpContext?.User?.Identity?.Name
            });

            // Preparar respuesta según tipo de request
            if (IsAjaxRequest(filterContext.HttpContext.Request))
            {
                HandleAjaxError(filterContext, errorInfo, correlationId);
            }
            else
            {
                HandleStandardError(filterContext, errorInfo, correlationId);
            }

            filterContext.ExceptionHandled = true;
        }

        private ErrorInfo ClassifyException(Exception ex)
        {
            // Clasificar excepciones para dar mensajes apropiados
            if (ex is DataAccessException dataEx)
            {
                return new ErrorInfo
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    ErrorCode = dataEx.ErrorCode,
                    UserMessage = dataEx.Message, // Ya está sanitizado
                    ViewName = "Error"
                };
            }

            if (ex is UnauthorizedAccessException)
            {
                return new ErrorInfo
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    ErrorCode = "ACCESS_DENIED",
                    UserMessage = "No tiene permisos para realizar esta acción.",
                    ViewName = "AccessDenied"
                };
            }

            if (ex is HttpException httpEx)
            {
                var code = httpEx.GetHttpCode();
                if (code == 404)
                {
                    return new ErrorInfo
                    {
                        StatusCode = HttpStatusCode.NotFound,
                        ErrorCode = "NOT_FOUND",
                        UserMessage = "La página solicitada no existe.",
                        ViewName = "NotFound"
                    };
                }
            }

            if (ex is ArgumentException || ex is InvalidOperationException)
            {
                return new ErrorInfo
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    ErrorCode = "BAD_REQUEST",
                    UserMessage = "La solicitud no es válida. Verifique los datos ingresados.",
                    ViewName = "Error"
                };
            }

            if (ex is TimeoutException)
            {
                return new ErrorInfo
                {
                    StatusCode = HttpStatusCode.GatewayTimeout,
                    ErrorCode = "TIMEOUT",
                    UserMessage = "La operación tardó demasiado. Por favor, intente nuevamente.",
                    ViewName = "Error"
                };
            }

            // Error genérico - no exponer detalles
            return new ErrorInfo
            {
                StatusCode = HttpStatusCode.InternalServerError,
                ErrorCode = "INTERNAL_ERROR",
                UserMessage = "Ha ocurrido un error inesperado. Por favor, contacte al administrador.",
                ViewName = "Error"
            };
        }

        private void HandleAjaxError(ExceptionContext filterContext, ErrorInfo errorInfo, string correlationId)
        {
            filterContext.HttpContext.Response.StatusCode = (int)errorInfo.StatusCode;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;

            filterContext.Result = new JsonResult
            {
                Data = new
                {
                    success = false,
                    error = errorInfo.UserMessage,
                    errorCode = errorInfo.ErrorCode,
                    correlationId = correlationId
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        private void HandleStandardError(ExceptionContext filterContext, ErrorInfo errorInfo, string correlationId)
        {
            filterContext.HttpContext.Response.StatusCode = (int)errorInfo.StatusCode;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;

            var model = new ErrorViewModel
            {
                ErrorCode = errorInfo.ErrorCode,
                Message = errorInfo.UserMessage,
                CorrelationId = correlationId,
                StatusCode = (int)errorInfo.StatusCode,
                RequestedUrl = filterContext.HttpContext.Request.Url?.ToString()
            };

            filterContext.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/" + errorInfo.ViewName + ".cshtml",
                ViewData = new ViewDataDictionary<ErrorViewModel>(model)
            };
        }

        private bool IsAjaxRequest(HttpRequestBase request)
        {
            if (request == null)
            {
                return false;
            }

            var header = request.Headers["X-Requested-With"];
            if (!string.IsNullOrEmpty(header) && header.Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var acceptHeader = request.Headers["Accept"];
            if (!string.IsNullOrEmpty(acceptHeader) && acceptHeader.Contains("application/json"))
            {
                return true;
            }

            return false;
        }

        private class ErrorInfo
        {
            public HttpStatusCode StatusCode { get; set; }
            public string ErrorCode { get; set; }
            public string UserMessage { get; set; }
            public string ViewName { get; set; }
        }
    }

    /// <summary>
    /// ViewModel para páginas de error
    /// </summary>
    public class ErrorViewModel
    {
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public string CorrelationId { get; set; }
        public int StatusCode { get; set; }
        public string RequestedUrl { get; set; }
    }
}
