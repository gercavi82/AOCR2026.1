using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace CapaPresentacion.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class SessionExpireAttribute : ActionFilterAttribute
    {
        public int TimeoutMinutes { get; set; } = 20;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var ctx = filterContext.HttpContext;

            if (ctx?.Session == null || ctx.Session["UserId"] == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary {
                        { "Controller", "Account" },
                        { "Action", "Login" },
                        { "ReturnUrl", ctx?.Request?.RawUrl }
                    });
                return;
            }

            if (ctx.Session["LastActivity"] is DateTime lastActivity)
            {
                if (DateTime.Now.Subtract(lastActivity).TotalMinutes > TimeoutMinutes)
                {
                    try { ctx.Session.Clear(); }
                    catch (Exception ex) { Trace.TraceWarning("SessionExpireAttribute: error al limpiar sesion: " + ex.Message); }

                    try { ctx.Session.Abandon(); }
                    catch (Exception ex) { Trace.TraceWarning("SessionExpireAttribute: error al abandonar sesion: " + ex.Message); }

                    filterContext.Result = new RedirectToRouteResult(
                        new RouteValueDictionary {
                            { "Controller", "Account" },
                            { "Action", "Login" },
                            { "timeout", "true" }
                        });
                    return;
                }
            }

            ctx.Session["LastActivity"] = DateTime.Now;
            base.OnActionExecuting(filterContext);
        }
    }
}
