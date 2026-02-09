using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controlador principal que redirige al Dashboard de Órdenes
    /// mientras el dashboard general esté en desarrollo
    /// </summary>
    [Authorize]
    public class HomeController : Controller
    {
        /// <summary>
        /// Página principal - redirige a Dashboard Órdenes temporalmente
        /// </summary>
        public ActionResult Index()
        {
            // ? REDIRIGIR AL DASHBOARD DE ÓRDENES MIENTRAS 
            //    EL DASHBOARD GENERAL NO ESTÉ TERMINADO
            return RedirectToAction("Index", "OrdenRecaudacionDashboard");
        }

        /// <summary>
        /// Dashboard general (en desarrollo)
        /// </summary>
        public ActionResult Dashboard()
        {
            // TODO: Implementar dashboard general cuando esté listo
            ViewBag.Message = "Dashboard general en desarrollo";
            return View();
        }

        /// <summary>
        /// Información de la aplicación
        /// </summary>
        public ActionResult About()
        {
            ViewBag.Message = "Sistema AOCR - Órdenes de Recaudación";
            return View();
        }
    }
}
