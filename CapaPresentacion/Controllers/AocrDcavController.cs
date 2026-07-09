using System;
using System.Web.Mvc;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class AocrDcavController : Controller
    {
        private readonly AocrDcavRevisionService _service = new AocrDcavRevisionService();

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,Director de Certificaciones DCAV,DCAV,Administrador")]
        public ActionResult Revision()
        {
            var items = _service.ListarPendientes();
            return View("~/Views/AocrDcav/Revision.cshtml", items);
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,Director de Certificaciones DCAV,DCAV,Administrador")]
        public ActionResult Detalle(int solicitudId)
        {
            var item = _service.ObtenerDetalle(solicitudId);
            if (item == null)
            {
                return HttpNotFound("No existe expediente AOCR para revision DCAV.");
            }

            return View("~/Views/AocrDcav/Detalle.cshtml", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,Director de Certificaciones DCAV,DCAV,Administrador")]
        public ActionResult Aprobar(int solicitudId)
        {
            var result = _service.AprobarEnviarDirectorGeneral(
                solicitudId,
                ObtenerUsuarioActualId(),
                ObtenerRolActual(),
                "Aprobado por Director de Certificaciones DCAV.");

            if (!result.Ok)
            {
                TempData["Error"] = result.Mensaje;
                return RedirectToAction("Detalle", new { solicitudId });
            }

            TempData["Success"] = result.Mensaje;
            return RedirectToAction("Revision");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,Director de Certificaciones DCAV,DCAV,Administrador")]
        public ActionResult Devolver(int solicitudId, string destino, string observacion)
        {
            var result = _service.DevolverConObservaciones(
                solicitudId,
                destino,
                observacion,
                ObtenerUsuarioActualId(),
                ObtenerRolActual());

            if (!result.Ok)
            {
                TempData["Error"] = result.Mensaje;
                return RedirectToAction("Detalle", new { solicitudId });
            }

            TempData["Success"] = result.Mensaje;
            return RedirectToAction("Revision");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Inspector,InspectorTecnico,Tecnico,CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
        public ActionResult EnviarRevisionDcav(int solicitudId)
        {
            var result = _service.EnviarRevisionDcav(
                solicitudId,
                ObtenerUsuarioActualId(),
                ObtenerRolActual(),
                "Inspector finaliza revision de AOCR y Condiciones y envia a revision DCAV.");

            if (!result.Ok)
            {
                TempData["Error"] = result.Mensaje;
            }
            else
            {
                TempData["Success"] = result.Mensaje;
            }

            return RedirectToAction("DashboardInspeccion", "CoordinacionJefatura");
        }

        private int ObtenerUsuarioActualId()
        {
            object valor = Session != null ? (Session["IdUsuario"] ?? Session["UserId"] ?? Session["CodigoUsuario"]) : null;
            int id;
            return valor != null && int.TryParse(Convert.ToString(valor), out id) ? id : 0;
        }

        private string ObtenerRolActual()
        {
            var rolSesion = Session != null ? Convert.ToString(Session["RolActual"] ?? Session["SelectedRole"] ?? Session["Rol"]) : null;
            if (!string.IsNullOrWhiteSpace(rolSesion))
            {
                return rolSesion.Trim();
            }

            return User != null && User.IsInRole("DIRECTOR_CERTIFICACIONES_DCAV")
                ? "DIRECTOR_CERTIFICACIONES_DCAV"
                : (User != null && User.IsInRole("DirectorGeneral") ? "DirectorGeneral" : "DCAV");
        }
    }
}
