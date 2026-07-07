using System;
using System.Web.Mvc;
using CapaNegocio.Services;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class RecorridoTramiteController : Controller
    {
        private readonly AocrRecorridoTramiteService _recorridoService;

        public RecorridoTramiteController()
        {
            _recorridoService = new AocrRecorridoTramiteService();
        }

        [HttpGet]
        public ActionResult Ver(int solicitudId)
        {
            if (solicitudId <= 0)
            {
                return Content("<div class='alert alert-danger'>ID de solicitud inválido.</div>");
            }

            string rolActivo = Convert.ToString(Session["RolActivo"] ?? Session["Rol"] ?? "RT");
            int usuarioId = ObtenerUsuarioIdActual();
            string companiaActiva = Convert.ToString(Session["CompaniaActiva"] ?? string.Empty);

            if (!_recorridoService.PuedeVerRecorrido(solicitudId, rolActivo, usuarioId, companiaActiva))
            {
                return Content("<div class='alert alert-warning'><i class='fas fa-exclamation-triangle mr-2 me-2'></i>No tiene autorización para ver el recorrido de esta solicitud.</div>");
            }

            var recorrido = _recorridoService.ObtenerRecorrido(solicitudId, rolActivo, usuarioId, companiaActiva);
            var actual = _recorridoService.ObtenerResumenEstadoActual(solicitudId);

            ViewBag.EstadoActual = actual;
            return PartialView("_RecorridoCompleto", recorrido);
        }

        [HttpGet]
        public ActionResult VerPorOrden(int ordenId)
        {
            if (ordenId <= 0)
            {
                return Content("<div class='alert alert-danger'>ID de orden inválido.</div>");
            }

            int solicitudId = _recorridoService.ResolverSolicitudIdPorOrden(ordenId);
            if (solicitudId <= 0)
            {
                return Content("<div class='alert alert-info'><i class='fas fa-info-circle mr-2 me-2'></i>Esta orden no tiene un trámite AOCR vinculado.</div>");
            }

            return Ver(solicitudId);
        }

        [HttpGet]
        public ActionResult VerPorInspeccion(int inspeccionId)
        {
            if (inspeccionId <= 0)
            {
                return Content("<div class='alert alert-danger'>ID de inspección inválido.</div>");
            }

            int solicitudId = _recorridoService.ResolverSolicitudIdPorInspeccion(inspeccionId);
            if (solicitudId <= 0)
            {
                return Content("<div class='alert alert-info'><i class='fas fa-info-circle mr-2 me-2'></i>Esta inspección no tiene un trámite AOCR vinculado.</div>");
            }

            return Ver(solicitudId);
        }

        [HttpGet]
        public ActionResult VerPorInforme(int informeId)
        {
            if (informeId <= 0)
            {
                return Content("<div class='alert alert-danger'>ID de informe inválido.</div>");
            }

            int solicitudId = _recorridoService.ResolverSolicitudIdPorInforme(informeId);
            if (solicitudId <= 0)
            {
                return Content("<div class='alert alert-info'><i class='fas fa-info-circle mr-2 me-2'></i>Este informe no tiene un trámite AOCR vinculado.</div>");
            }

            return Ver(solicitudId);
        }

        private int ObtenerUsuarioIdActual()
        {
            var v = Session["UserId"] ?? Session["IdUsuario"];
            if (v == null) return 0;
            int id;
            return int.TryParse(v.ToString(), out id) ? id : 0;
        }
    }
}
