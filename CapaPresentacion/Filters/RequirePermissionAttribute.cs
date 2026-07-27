using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaPresentacion.Helpers;

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

        public bool SoloAdministrador { get; set; }

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

            var roles = ObtenerRoles(httpContext);
            if (SoloAdministrador &&
                !roles.Any(r => r.Equals("Administrador", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
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
                var autenticado = filterContext.HttpContext.User != null &&
                                   filterContext.HttpContext.User.Identity != null &&
                                   filterContext.HttpContext.User.Identity.IsAuthenticated;
                filterContext.HttpContext.Response.StatusCode = autenticado ? 403 : 401;
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

            filterContext.HttpContext.Response.StatusCode = 403;
            filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new HttpStatusCodeResult(403, "No tiene permisos para realizar esta accion.");
        }

        private static List<string> ObtenerRoles(HttpContextBase httpContext)
        {
            var roles = new List<string>();
            var catalogo = new[]
            {
                "Administrador",
                "Financiero",
                "Coordinador",
                "CoordinadorInspecciones",
                "CoordinadorFinanciero",
                "DirectorFinanciero",
                "CoordinacionLegal",
                "CoordinadorLegal",
                "DirectorGeneral",
                "Direccion",
                "DIRDAC",
                "DCAV",
                "JefaturaTecnica",
                "Solicitante",
                "Operador",
                "Inspector",
                "Tecnico",
                "EvaluadorTecnico"
            };

            if (httpContext != null && httpContext.Session != null)
            {
                var rolSeleccionado = Convert.ToString(httpContext.Session["Rol"]);
                var rolesSesion = RoleGroupingHelper.ExtractRoles(httpContext.Session["Roles"], rolSeleccionado);
                var rolesRawSesion = RoleGroupingHelper.ExtractRoles(httpContext.Session["RolesRaw"], rolSeleccionado);

                roles.AddRange(rolesSesion);
                roles.AddRange(rolesRawSesion);
                roles.AddRange(RoleGroupingHelper.BuildUnifiedRoles(rolesRawSesion.Concat(rolesSesion)));
            }

            var user = httpContext != null ? httpContext.User : null;
            if (user != null)
            {
                roles.AddRange(catalogo.Where(user.IsInRole));
            }

            return roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
