using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OperadorController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        // Si ya tienes Modificacion en el menú:
        public ActionResult Modificacion()
        {
            return View();
        }

        // Ruta solicitada por el sidebar anterior; redirige a solicitudes para evitar 404
        public ActionResult RegistrarAeronave()
        {
            // Redirige a creación de solicitud AOCR, ajusta si existe otra pantalla específica
            return RedirectToAction("Index", "SolicitudAOCR");
        }
    }
}
