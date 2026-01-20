using System;
using System.Web.Mvc;
using CapaDatos.DAOs;


namespace CapaPresentacion.Filters
{
    public class RequireOrdenRecaudacionAttribute : ActionFilterAttribute
    {
        public bool RequiereGenerada { get; set; } = false;

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;
            if (session == null || session["CodigoUsuario"] == null)
            {
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            int usuarioId = Convert.ToInt32(session["CodigoUsuario"]);

            var dao = new OrdenRecaudacionDAO();
            bool ok = RequiereGenerada
                ? dao.ExisteORGeneradaOPagada(usuarioId)
                : dao.ExisteORMinima(usuarioId);

            if (!ok)
            {
                filterContext.Result = new RedirectResult("~/OrdenRecaudacion/Nueva");
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
