using System;
using System.Configuration;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.UI;
using System.Web.Helpers;

namespace CapaPresentacion.Infrastructure
{
    /// <summary>
    /// Filtro de acción para logging empresarial y manejo de errores
    /// Intercepta todas las acciones del módulo de órdenes
    /// </summary>
    public class OrdenRecaudacionLoggingFilter : ActionFilterAttribute
    {
        private readonly bool _logActions;
        private readonly bool _logErrors;

        public OrdenRecaudacionLoggingFilter()
        {
            _logActions = ConfigurationManager.AppSettings["LogOrdenActions"] == "true";
            _logErrors = true; // Siempre log de errores
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (_logActions)
            {
                var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
                var actionName = filterContext.ActionDescriptor.ActionName;
                var userName = filterContext.HttpContext.User?.Identity?.Name ?? "Anonymous";
                var userId = filterContext.HttpContext.Session?["CodigoUsuario"]?.ToString() ?? "N/A";

                System.Diagnostics.Debug.WriteLine($"[ORDEN_LOG] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Usuario: {userName} (ID: {userId}) - Acción: {controllerName}.{actionName}");
            }

            base.OnActionExecuting(filterContext);
        }

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext.Exception != null && _logErrors)
            {
                var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
                var actionName = filterContext.ActionDescriptor.ActionName;
                var userName = filterContext.HttpContext.User?.Identity?.Name ?? "Anonymous";
                var error = filterContext.Exception.Message;

                System.Diagnostics.Debug.WriteLine($"[ORDEN_ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Usuario: {userName} - Error en: {controllerName}.{actionName} - {error}");

                // En producción, aquí iría un logger como NLog o Serilog
                // _logger.LogError(filterContext.Exception, "Error en acción de orden de recaudación");

                // Manejar error de manera elegante
                if (!filterContext.ExceptionHandled)
                {
                    filterContext.ExceptionHandled = true;
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, message = "Error interno del servidor" },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
            }

            base.OnActionExecuted(filterContext);
        }
    }

    /// <summary>
    /// Filtro de autorización específico para órdenes de recaudación
    /// Implementa validaciones adicionales de seguridad
    /// </summary>
    public class OrdenRecaudacionAuthorizationFilter : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(System.Web.HttpContextBase httpContext)
        {
            // Validación base de autorización
            if (!base.AuthorizeCore(httpContext))
                return false;

            // Validaciones adicionales específicas del módulo
            var userId = httpContext.Session?["CodigoUsuario"];
            if (userId == null)
                return false;

            // Verificar que el usuario tenga roles válidos para órdenes
            var user = httpContext.User;
            if (!user.IsInRole("Solicitante") && !user.IsInRole("Financiero") && !user.IsInRole("Administrador"))
                return false;

            return true;
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, message = "No autorizado", requiresLogin = true },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }

    /// <summary>
    /// Filtro para validación de AntiForgeryToken en requests AJAX
    /// Mejora la seguridad CSRF
    /// </summary>
    public class ValidateAntiForgeryTokenAjaxAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                try
                {
                    // Para AJAX aceptar tokens estándar y legacy en headers.
                    var request = filterContext.HttpContext.Request;
                    var headerToken =
                        request.Headers["RequestVerificationToken"] ??
                        request.Headers["__RequestVerificationToken"] ??
                        request.Headers["X-CSRF-TOKEN"];

                    var formTokenFromForm = request.Form["__RequestVerificationToken"];
                    if (string.IsNullOrWhiteSpace(headerToken) && string.IsNullOrWhiteSpace(formTokenFromForm))
                    {
                        var url = request != null && request.Url != null
                            ? request.Url.ToString()
                            : "N/A";
                        var user = filterContext != null &&
                                   filterContext.HttpContext != null &&
                                   filterContext.HttpContext.User != null &&
                                   filterContext.HttpContext.User.Identity != null &&
                                   filterContext.HttpContext.User.Identity.IsAuthenticated
                            ? filterContext.HttpContext.User.Identity.Name
                            : "ANON";
                        CapaNegocio.LogBL.RegistrarAdvertencia(
                            string.Format("CSRF rechazado: token faltante. Metodo={0}, Url={1}, User={2}",
                                request != null ? request.HttpMethod : "N/A",
                                url,
                                user),
                            "ValidateAntiForgeryTokenAjax");

                        filterContext.HttpContext.Response.StatusCode = 400;
                        filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                        filterContext.Result = new JsonResult
                        {
                            Data = new { ok = false, message = "Token CSRF requerido" },
                            JsonRequestBehavior = JsonRequestBehavior.AllowGet
                        };
                        return;
                    }

                    // Admite formato combinado "cookieToken:formToken".
                    string cookieToken = null;
                    string formToken = null;

                    if (!string.IsNullOrWhiteSpace(headerToken))
                    {
                        var parts = headerToken.Split(':');
                        if (parts.Length == 2)
                        {
                            cookieToken = parts[0];
                            formToken = parts[1];
                        }
                        else
                        {
                            formToken = headerToken;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(formToken) && !string.IsNullOrWhiteSpace(formTokenFromForm))
                    {
                        formToken = formTokenFromForm;
                    }

                    if (string.IsNullOrWhiteSpace(cookieToken))
                    {
                        var antiCookie = request.Cookies[AntiForgeryConfig.CookieName] ??
                                         request.Cookies["__RequestVerificationToken"];
                        cookieToken = antiCookie != null ? antiCookie.Value : null;
                    }

                    if (!string.IsNullOrWhiteSpace(cookieToken) && !string.IsNullOrWhiteSpace(formToken))
                    {
                        AntiForgery.Validate(cookieToken, formToken);
                    }
                    else if (!string.IsNullOrWhiteSpace(formTokenFromForm))
                    {
                        AntiForgery.Validate();
                    }
                    else
                    {
                        throw new System.Web.Mvc.HttpAntiForgeryException("Token CSRF incompleto.");
                    }
                }
                catch (System.Web.Mvc.HttpAntiForgeryException)
                {
                    var request = filterContext != null && filterContext.HttpContext != null
                        ? filterContext.HttpContext.Request
                        : null;
                    var url = request != null && request.Url != null
                        ? request.Url.ToString()
                        : "N/A";
                    var user = filterContext != null &&
                               filterContext.HttpContext != null &&
                               filterContext.HttpContext.User != null &&
                               filterContext.HttpContext.User.Identity != null &&
                               filterContext.HttpContext.User.Identity.IsAuthenticated
                        ? filterContext.HttpContext.User.Identity.Name
                        : "ANON";
                    CapaNegocio.LogBL.RegistrarAdvertencia(
                        string.Format("CSRF rechazado: token invalido/expirado. Metodo={0}, Url={1}, User={2}",
                            request != null ? request.HttpMethod : "N/A",
                            url,
                            user),
                        "ValidateAntiForgeryTokenAjax");

                    filterContext.HttpContext.Response.StatusCode = 400;
                    filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                    filterContext.Result = new JsonResult
                    {
                        Data = new { ok = false, message = "Token CSRF invalido o expirado" },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
            }
            else
            {
                // Para requests normales, usar validación estándar
                try
                {
                    System.Web.Helpers.AntiForgery.Validate();
                }
                catch (System.Web.Mvc.HttpAntiForgeryException)
                {
                    var request = filterContext != null && filterContext.HttpContext != null
                        ? filterContext.HttpContext.Request
                        : null;
                    var url = request != null && request.Url != null
                        ? request.Url.ToString()
                        : "N/A";
                    var user = filterContext != null &&
                               filterContext.HttpContext != null &&
                               filterContext.HttpContext.User != null &&
                               filterContext.HttpContext.User.Identity != null &&
                               filterContext.HttpContext.User.Identity.IsAuthenticated
                        ? filterContext.HttpContext.User.Identity.Name
                        : "ANON";

                    CapaNegocio.LogBL.RegistrarAdvertencia(
                        string.Format("CSRF rechazado (no-AJAX): token invalido/expirado. Metodo={0}, Url={1}, User={2}",
                            request != null ? request.HttpMethod : "N/A",
                            url,
                            user),
                        "ValidateAntiForgeryTokenAjax");

                    filterContext.HttpContext.Response.StatusCode = 400;
                    filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                    filterContext.Result = new JsonResult
                    {
                        Data = new { ok = false, message = "Token CSRF invalido o expirado" },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Filtro de cache inteligente para mejorar rendimiento
    /// </summary>
    public class SmartCacheAttribute : OutputCacheAttribute
    {
        public SmartCacheAttribute(int durationSeconds = 60, bool varyByUser = true)
        {
            Duration = durationSeconds;
            if (varyByUser)
            {
                VaryByCustom = "user";
            }
            VaryByParam = "*";
            Location = OutputCacheLocation.ServerAndClient;
        }
    }

    /// <summary>
    /// Helper para manejo de errores empresarial
    /// </summary>
    public static class ErrorHandlingHelper
    {
        public static JsonResult HandleError(Exception ex, string action = "")
        {
            // Log detallado para desarrollo
            System.Diagnostics.Debug.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - Acción: {action} - Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");

            // En producción, mensaje genérico al usuario
            var isProduction = ConfigurationManager.AppSettings["Environment"] == "Production";
            var mensaje = isProduction 
                ? "Ha ocurrido un error interno. Por favor, inténtelo nuevamente." 
                : ex.Message;

            return new JsonResult
            {
                Data = new
                {
                    success = false,
                    message = mensaje,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    action = action
                },
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
        }

        public static void LogInfo(string message, string component = "OrdenRecaudacion")
        {
            System.Diagnostics.Debug.WriteLine($"[{component}_INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        public static void LogWarning(string message, string component = "OrdenRecaudacion")
        {
            System.Diagnostics.Debug.WriteLine($"[{component}_WARN] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }
    }
}
