using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;

namespace CapaPresentacion.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class RequirePermissionAttribute : AuthorizeAttribute
    {
        private readonly string _codigoPermiso;

        public RequirePermissionAttribute(string codigoPermiso)
        {
            _codigoPermiso = (codigoPermiso ?? string.Empty).Trim();
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext == null || httpContext.User == null || !httpContext.User.Identity.IsAuthenticated)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_codigoPermiso))
            {
                return true;
            }

            var roles = ObtenerRoles(httpContext.User);
            var codigoUsuario = ObtenerCodigoUsuario(httpContext);

            return SeguridadBL.UsuarioTienePermiso(codigoUsuario, _codigoPermiso, roles);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                return;
            }

            var request = filterContext.HttpContext.Request;
            var esAjax = request.IsAjaxRequest() ||
                         (request.Headers["Accept"] ?? string.Empty).IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;

            if (esAjax)
            {
                filterContext.HttpContext.Response.StatusCode = 403;
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new JsonResult
                {
                    Data = new
                    {
                        ok = false,
                        message = "No tiene permisos para realizar esta accion.",
                        permission = _codigoPermiso
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                base.HandleUnauthorizedRequest(filterContext);
                return;
            }

            filterContext.Result = new RedirectToRouteResult(
                new System.Web.Routing.RouteValueDictionary(
                    new
                    {
                        controller = "Error",
                        action = "AccessDenied"
                    }));
        }

        private static List<string> ObtenerRoles(System.Security.Principal.IPrincipal user)
        {
            var catalogo = new[]
            {
                "Administrador",
                "Financiero",
                "CoordinadorFinanciero",
                "DirectorFinanciero",
                "CoordinacionLegal",
                "CoordinadorLegal",
                "Direccion",
                "JefaturaTecnica",
                "Solicitante",
                "Operador",
                "Inspector",
                "Tecnico"
            };

            return catalogo.Where(r => user.IsInRole(r)).ToList();
        }

        private static string ObtenerCodigoUsuario(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                return null;
            }

            var session = httpContext.Session;
            if (session != null)
            {
                var codigo = Convert.ToString(session["CodigoUsuario"]);
                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    return codigo.Trim();
                }
            }

            var identityName = httpContext.User != null && httpContext.User.Identity != null
                ? httpContext.User.Identity.Name
                : null;

            return string.IsNullOrWhiteSpace(identityName) ? null : identityName.Trim();
        }
    }
}
