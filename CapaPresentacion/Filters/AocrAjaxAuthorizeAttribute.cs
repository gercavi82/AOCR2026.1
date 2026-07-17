using System;
using System.Web.Mvc;

namespace CapaPresentacion.Filters
{
    public class AocrAjaxAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsAjaxRequest())
            {
                filterContext.Result = new JsonResult
                {
                    Data = new
                    {
                        success = false,
                        code = filterContext.HttpContext.User.Identity.IsAuthenticated ? 403 : 401,
                        message = filterContext.HttpContext.User.Identity.IsAuthenticated ? "No tiene permisos para realizar esta acción." : "Sesión expirada o no autenticado.",
                        correlationId = Guid.NewGuid().ToString(),
                        data = (object)null
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                filterContext.HttpContext.Response.StatusCode = filterContext.HttpContext.User.Identity.IsAuthenticated ? 403 : 401;
            }
            else
            {
                base.HandleUnauthorizedRequest(filterContext);
            }
        }
    }
}
