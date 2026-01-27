using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class AdministradorController : Controller
    {
        public ActionResult ConfiguracionSistema()
        {
            return View();
        }
    }
}
