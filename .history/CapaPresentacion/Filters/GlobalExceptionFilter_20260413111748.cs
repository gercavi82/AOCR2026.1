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
    /// No expone stacktrace ni detalles internos en producciÃ³n.
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

            // Fallback log directo a archivo para depuraciÃ³n local
            try
            {
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs", "UnhandledExceptions.log");
                var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {controllerName}/{actionName}\n{exception}\n\n";
                System.IO.File.AppendAllText(logPath, entry);
            }
            catch
            {
                // No bloquear el flujo si el log falla
            }

            // Obtener contexto de correlaciÃ³n si existe
            var correlationId = HttpContext.Current?.Items["CorrelationId"] as string ?? Guid.NewGuid().ToString("N");
            var numeroOrden = HttpContext.Current?.Items["NumeroOrden"] as string;
            var codigoSolicitud = HttpContext.Current?.Items["CodigoSolicitud"] as string;
            if (exception is HttpAntiForgeryException)
            {
                if (IsAjaxRequest(filterContext.HttpContext.Request))
                {
                    HandleAjaxAntiForgeryError(filterContext);
                }
                else
                {
                    HandleStandardAntiForgeryError(filterContext);
                }
                filterContext.ExceptionHandled = true;
                return;
            }

            // Determinar cÃ³digo de error y mensaje para usuario
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

            // Preparar respuesta segÃºn tipo de request
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
                    UserMessage = dataEx.Message, // Ya estÃ¡ sanitizado
                    ViewName = "Error"
                };
            }

            if (ex is UnauthorizedAccessException)
            {
                return new ErrorInfo
                {
                    StatusCode = HttpStatusCode.Forbidden,
                    ErrorCode = "ACCESS_DENIED",
                    UserMessage = "No tiene permisos para realizar esta acciÃ³n.",
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
                        UserMessage = "La pÃ¡gina solicitada no existe.",
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
                    UserMessage = "La solicitud no es vÃ¡lida. Verifique los datos ingresados.",
                    ViewName = "Error"
                };
            }

            if (ex is TimeoutException)
            {
                return new ErrorInfo
                {
                    StatusCode = HttpStatusCode.GatewayTimeout,
                    ErrorCode = "TIMEOUT",
                    UserMessage = "La operaciÃ³n tardÃ³ demasiado. Por favor, intente nuevamente.",
                    ViewName = "Error"
                };
            }

            // Error genÃ©rico - no exponer detalles
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

            // DIAGNÓSTICO: cuando customErrors="Off", incluir detalles de la excepción
            var userMessage = errorInfo.UserMessage;
            var customErrorsSection = System.Configuration.ConfigurationManager.GetSection("system.web/customErrors")
                as System.Web.Configuration.CustomErrorsSection;
            if (customErrorsSection != null && customErrorsSection.Mode == System.Web.Configuration.CustomErrorsMode.Off)
            {
                var ex = filterContext.Exception;
                userMessage = ex != null
                    ? ex.GetType().FullName + ": " + ex.Message + "\n\n" + ex.StackTrace
                    : userMessage;
            }

            var model = new ErrorViewModel
            {
                ErrorCode = errorInfo.ErrorCode,
                Message = userMessage,
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

        private void HandleAjaxAntiForgeryError(ExceptionContext filterContext)
        {
            TryExpireAntiForgeryCookie(filterContext.HttpContext);
            filterContext.HttpContext.Response.StatusCode = 400;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new JsonResult
            {
                Data = new
                {
                    success = false,
                    error = "La sesion expiro o el formulario perdio validez. Recargue la pagina e intente nuevamente.",
                    errorCode = "ANTI_FORGERY_INVALID"
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }
        private void HandleStandardAntiForgeryError(ExceptionContext filterContext)
        {
            TryExpireAntiForgeryCookie(filterContext.HttpContext);
            var request = filterContext.HttpContext.Request;
            var url = request != null ? request.Url : null;
            var isLoginRoute =
                string.Equals(filterContext.RouteData.Values["controller"] as string, "Account", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(filterContext.RouteData.Values["action"] as string, "Login", StringComparison.OrdinalIgnoreCase);
            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary
                {
                    { "controller", "Account" },
                    { "action", "Login" },
                    { "returnUrl", isLoginRoute ? null : (url != null ? (url.PathAndQuery ?? string.Empty) : null) },
                    { "af", "1" }
                });
        }
        private void TryExpireAntiForgeryCookie(HttpContextBase context)
        {
            try
            {
                if (context == null || context.Response == null || context.Request == null)
                {
                    return;
                }
                var cookieName = System.Web.Helpers.AntiForgeryConfig.CookieName;
                var expired = new HttpCookie(cookieName)
                {
                    Value = string.Empty,
                    Expires = DateTime.UtcNow.AddDays(-1),
                    HttpOnly = true,
                    Secure = context.Request.IsSecureConnection,
                    Path = "/"
                };
                context.Response.Cookies.Add(expired);
            }
            catch
            {
                // best-effort
            }
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
    /// ViewModel para pÃ¡ginas de error
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



