using System.Web.Mvc;
using CapaPresentacion.Filters;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,Administrador")]
    public class CoordinacionLegalController : Controller
    {
        // GET: /CoordinacionLegal/RevisarLegal
        [RequirePermission("LEGAL_REVISAR_SOLICITUD")]
        public ActionResult RevisarLegal()
        {
            return RedirectToAction("RevisarLegalizacion", "SolicitudAOCR");
        }

        // GET: /CoordinacionLegal/GenerarCertificados
        [RequirePermission("LEGAL_GENERAR_CERTIFICADO")]
        public ActionResult GenerarCertificados()
        {
            ViewBag.Title = "Generar Certificados";
            return View();
        }
    }
}
