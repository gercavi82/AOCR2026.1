using System;
using System.Web.Mvc;
using CapaNegocio;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    public class DireccionController : Controller
    {
        private readonly DireccionBL _bl = new DireccionBL();

        // ============================================================
        // LISTADO
        // ============================================================
        public ActionResult Index()
        {
            var lista = _bl.ObtenerTodos();
            return View(lista);
        }

        // ============================================================
        // DETALLE
        // ============================================================
        public ActionResult Detalle(int id)
        {
            var direccion = _bl.ObtenerPorId(id);
            if (direccion == null)
                return HttpNotFound("Dirección no encontrada");

            return View(direccion);
        }

        // ============================================================
        // CREAR
        // ============================================================
        public ActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Crear(Direccion d)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(d);

                _bl.Crear(d, User.Identity.Name);

                TempData["msg"] = "Dirección creada correctamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(d);
            }
        }

        // ============================================================
        // EDITAR
        // ============================================================
        public ActionResult Editar(int id)
        {
            var direccion = _bl.ObtenerPorId(id);
            if (direccion == null)
                return HttpNotFound("Dirección no encontrada");

            return View(direccion);
        }

        [HttpPost]
        public ActionResult Editar(Direccion d)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View(d);

                _bl.Actualizar(d, User.Identity.Name);

                TempData["msg"] = "Dirección actualizada correctamente";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(d);
            }
        }

        // ============================================================
        // ELIMINAR
        // ============================================================
        public ActionResult Eliminar(int id)
        {
            var direccion = _bl.ObtenerPorId(id);
            if (direccion == null)
                return HttpNotFound("Dirección no encontrada");

            return View(direccion);
        }

        [HttpPost]
        public ActionResult ConfirmarEliminar(int id)
        {
            try
            {
                _bl.Eliminar(id, User.Identity.Name);
                TempData["msg"] = "Dirección eliminada correctamente";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
                return RedirectToAction("Eliminar", new { id });
            }
        }

        // ============================================================
        // APROBAR SOLICITUDES - DIRECCIÓN
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult AprobarSolicitudes()
        {
            var solicitudesPendientes = SolicitudAOCRBL.ListarPorEstado("VALIDADO_TECNICAMENTE");
            return View(solicitudesPendientes);
        }

        // ============================================================
        // VALIDACIÓN FINAL
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult ValidacionFinal(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || solicitud.Estado != "VALIDADO_TECNICAMENTE")
                return HttpNotFound("Solicitud no encontrada o no está lista para validación final");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Direccion")]
        [ValidateAntiForgeryToken]
        public ActionResult ValidacionFinal(int id, bool aprobada, string observaciones, string condicionesEspeciales, int vigencia)
        {
            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
                if (solicitud == null || solicitud.Estado != "VALIDADO_TECNICAMENTE")
                    return HttpNotFound("Solicitud no encontrada o no está lista para validación final");

                int userId = ObtenerUsuarioActualId();

                if (aprobada)
                {
                    // Cambiar estado a aprobado por dirección
                    string mensaje;
                    SolicitudAOCRBL.CambiarEstado(id, "APROBADO_POR_DIRECCION", userId, observaciones ?? "Aprobado por Dirección", out mensaje);

                    TempData["success"] = "Solicitud aprobada correctamente. Pasará a legalización.";
                    return RedirectToAction("Legalizar", new { id });
                }
                else
                {
                    // Rechazar solicitud
                    string mensaje;
                    SolicitudAOCRBL.CambiarEstado(id, "RECHAZADO_POR_DIRECCION", userId, observaciones ?? "Rechazado por Dirección", out mensaje);

                    TempData["error"] = "Solicitud rechazada.";
                    return RedirectToAction("AprobarSolicitudes");
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error al procesar la validación: " + ex.Message;
                return RedirectToAction("ValidacionFinal", new { id });
            }
        }

        // ============================================================
        // LEGALIZAR CERTIFICADO
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult Legalizar(int id)
        {
            var solicitud = SolicitudAOCRBL.ObtenerPorId(id);
            if (solicitud == null || solicitud.Estado != "APROBADO_POR_DIRECCION")
                return HttpNotFound("Solicitud no encontrada o no está lista para legalización");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Direccion")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string firmaDirector, string selloOficial)
        {
            try
            {
                var solicitud = new SolicitudAOCRBL().ObtenerPorId(id);
                if (solicitud == null || solicitud.Estado != "APROBADO_POR_DIRECCION")
                    return HttpNotFound("Solicitud no encontrada o no está lista para legalización");

                int userId = ObtenerUsuarioActualId();

                // Cambiar estado a legalizado
                string mensaje;
                SolicitudAOCRBL.CambiarEstado(id, "LEGALIZADO", userId, "Certificado legalizado y firmado", out mensaje);

                TempData["success"] = "Certificado legalizado correctamente.";
                return RedirectToAction("EmitirAOCR", new { id });
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error al legalizar el certificado: " + ex.Message;
                return RedirectToAction("Legalizar", new { id });
            }
        }

        // ============================================================
        // EMITIR CERTIFICADO AOCR
        // ============================================================
        [Authorize(Roles = "Direccion")]
        public ActionResult EmitirAOCR(int id)
        {
            var solicitud = new SolicitudAOCRBL().ObtenerPorId(id);
            if (solicitud == null || solicitud.Estado != "LEGALIZADO")
                return HttpNotFound("Solicitud no encontrada o no está lista para emisión");

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Direccion")]
        [ValidateAntiForgeryToken]
        public ActionResult EmitirAOCRConfirm(int id)
        {
            try
            {
                var solicitud = new SolicitudAOCRBL().ObtenerPorId(id);
                if (solicitud == null || solicitud.Estado != "LEGALIZADO")
                    return HttpNotFound("Solicitud no encontrada o no está lista para emisión");

                int userId = ObtenerUsuarioActualId();

                // Cambiar estado a emitido
                string mensaje;
                SolicitudAOCRBL.CambiarEstado(id, "CERTIFICADO_EMITIDO", userId, "Certificado AOCR emitido", out mensaje);

                TempData["success"] = "Certificado AOCR emitido correctamente.";
                return RedirectToAction("AprobarSolicitudes");
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error al emitir el certificado: " + ex.Message;
                return RedirectToAction("EmitirAOCR", new { id });
            }
        }

        private int ObtenerUsuarioActualId()
        {
            if (Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out int idUsuario))
                return idUsuario;

            throw new Exception("No se pudo obtener el ID del usuario actual.");
        }
    }
}
