using System;
using System.Web.Helpers;
using System.Web.Mvc;

namespace CapaPresentacion.Filters
{
    /// <summary>
    /// Valida el token antiforgery recibido en el header HTTP "RequestVerificationToken"
    /// (o "X-Request-Verification-Token") contra la cookie antiforgery.
    /// Necesario para POST JSON/AJAX donde el token no viaja como campo de formulario.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class ValidateAntiForgeryTokenFromHeaderAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null) throw new ArgumentNullException("filterContext");

            var request = filterContext.HttpContext.Request;

            // 1) Intentar desde header (AJAX con JSON)
            string formToken = request.Headers["RequestVerificationToken"]
                            ?? request.Headers["X-Request-Verification-Token"]
                            ?? request.Headers["__RequestVerificationToken"];

            // 2) Fallback a campo de formulario (AJAX con FormData / form normal)
            if (string.IsNullOrEmpty(formToken))
            {
                try { formToken = request.Form != null ? request.Form["__RequestVerificationToken"] : null; }
                catch { formToken = null; }
            }

            string cookieToken = null;
            var cookie = request.Cookies[AntiForgeryConfig.CookieName];
            if (cookie != null) cookieToken = cookie.Value;

            AntiForgery.Validate(cookieToken, formToken);
        }
    }
}
