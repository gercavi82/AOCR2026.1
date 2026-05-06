using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaPresentacion.Filters;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
    public class CoordinacionLegalController : Controller
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();

        // GET: /CoordinacionLegal/GenerarCertificados
        [RequirePermission("LEGAL_GENERAR_CERTIFICADO")]
        public ActionResult GenerarCertificados()
        {
            var solicitudes = _solicitudDao.ObtenerPorEstados(
                EstadoSolicitud.AOCR_Legalizado,
                EstadoSolicitud.AOCR_EmitidoRecibido,
                "LEGALIZADO",
                "CERTIFICADO_EMITIDO",
                "AOCR_EMITIDO");

            ViewBag.Title = "Generar Certificados";
            return View(solicitudes);
        }
    }
}
