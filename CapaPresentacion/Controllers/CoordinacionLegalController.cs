using System.Web.Mvc;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,Administrador")]
    public class CoordinacionLegalController : Controller
    {
        // GET: /CoordinacionLegal/RevisarLegal
        public ActionResult RevisarLegal()
        {
            return RedirectToAction("RevisarLegalizacion", "SolicitudAOCR");
        }

        // GET: /CoordinacionLegal/GenerarCertificados
        public ActionResult GenerarCertificados()
        {
            ViewBag.Title = "Generar Certificados";
            return View();
        }
    }
}
