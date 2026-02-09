using System;
using System.Web.Mvc;
using System.Web.Security;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            // Verificar autenticación - si no está autenticado, ir a Login
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            // Verificación de seguridad de sesión - si la sesión expiró, ir a Login
            if (Session["NombreUsuario"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // 🎯 REDIRECCIÓN EMPRESARIAL: Ir al Dashboard de Órdenes de Recaudación
            return RedirectToAction("Index", "OrdenRecaudacionDashboardEmpresarial");
        }

        public ActionResult Salir()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            if (Request.Cookies[FormsAuthentication.FormsCookieName] != null)
            {
                var cookie = new System.Web.HttpCookie(FormsAuthentication.FormsCookieName)
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(cookie);
            }

            return RedirectToAction("Login", "Account");
        }
    }
}