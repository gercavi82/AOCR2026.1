using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OperadorController : Controller
    {
        // Compatibilidad: la bandeja del operador converge en SolicitudAOCR/Index.
        public ActionResult Index()
        {
            return RedirectToAction("Index", "SolicitudAOCR");
        }

        // Compatibilidad: conservar alias legacy para enlaces antiguos.
        public ActionResult Modificacion()
        {
            return RedirectToAction("Index", "SolicitudAOCR", new { tipoSolicitud = 3, abrirModal = true });
        }

        // Compatibilidad: antigua ruta de acceso rápido al alta desde el menú lateral.
        public ActionResult RegistrarAeronave()
        {
            return RedirectToAction("Index", "SolicitudAOCR", new { tipoSolicitud = 1, abrirModal = true });
        }
    }
}
