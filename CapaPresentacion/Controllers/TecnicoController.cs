using System;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using CapaDatos.DAOs;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class TecnicoController : Controller
    {
        private const string VIEWS_TECNICO = "~/Views/Tecnico/";
        private readonly SolicitudBL _solicitudBL = new SolicitudBL(); // ✅ Agregado

        [Authorize(Roles = "Tecnico,Administrador")]
        public ActionResult Index()
        {
            var lista = TecnicoBL.ObtenerTodos();
            return View(VIEWS_TECNICO + "Index.cshtml", lista);
        }

        [Authorize(Roles = "Administrador")]
        public ActionResult Crear()
        {
            return View(VIEWS_TECNICO + "Crear.cshtml");
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Tecnico modelo)
        {
            if (!ModelState.IsValid)
                return View(VIEWS_TECNICO + "Crear.cshtml", modelo);

            string mensaje;
            bool ok = TecnicoBL.Insertar(modelo, out mensaje);

            if (!ok)
            {
                ViewBag.Error = mensaje;
                return View(VIEWS_TECNICO + "Crear.cshtml", modelo);
            }

            TempData["Success"] = "Técnico creado correctamente.";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrador")]
        public ActionResult Editar(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var modelo = TecnicoBL.ObtenerPorId(id);
            if (modelo == null)
            {
                TempData["Error"] = "Técnico no encontrado.";
                return RedirectToAction("Index");
            }

            return View(VIEWS_TECNICO + "Editar.cshtml", modelo);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Tecnico modelo)
        {
            if (!ModelState.IsValid)
                return View(VIEWS_TECNICO + "Editar.cshtml", modelo);

            string mensaje;
            bool ok = TecnicoBL.Actualizar(modelo, out mensaje);

            if (!ok)
            {
                ViewBag.Error = mensaje;
                return View(VIEWS_TECNICO + "Editar.cshtml", modelo);
            }

            TempData["Success"] = "Técnico actualizado correctamente.";
            return RedirectToAction("Index");
        }

        [Authorize(Roles = "Administrador")]
        public ActionResult Eliminar(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            string mensaje;
            bool ok = TecnicoBL.Eliminar(id, out mensaje);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Técnico eliminado correctamente."
                : mensaje;

            return RedirectToAction("Index");
        }

        [HttpGet]
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones")]
        public ActionResult AsignarInspector(int? solicitudId)
        {
            if (!solicitudId.HasValue || solicitudId.Value <= 0)
            {
                TempData["Error"] = "ID de solicitud no proporcionado o inválido.";
                return RedirectToAction("Index");
            }

            var solicitud = _solicitudBL.ObtenerDetalle(solicitudId.Value); // ✅ Actualizado
            if (solicitud == null)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Index");
            }

            var inspectores = UsuarioBL.ObtenerInspectores();
            ViewBag.Inspectores = new SelectList(inspectores, "CodigoUsuario", "NombreCompleto");

            return View(VIEWS_TECNICO + "AsignarInspector.cshtml", solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones")]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarInspector(int solicitudId, int inspectorPrincipal, int? inspectorApoyo,
                                            DateTime fechaInspeccion, string observaciones)
        {
            if (solicitudId <= 0)
            {
                TempData["Error"] = "Solicitud inválida.";
                return RedirectToAction("Index");
            }

            try
            {
                int usuarioId = Convert.ToInt32(Session["CodigoUsuario"]); // ✅ Usuario actual
                string mensaje;

                bool ok = _solicitudBL.AsignarTecnico(
                    solicitudId,
                    inspectorPrincipal,
                    fechaInspeccion,
                    observaciones,
                    usuarioId,
                    out mensaje
                );

                TempData[ok ? "Success" : "Error"] = mensaje;

                return ok
                    ? RedirectToAction("Index")
                    : RedirectToAction("AsignarInspector", new { solicitudId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones")]
        public ActionResult ListaChequeo(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                TempData["Error"] = "ID de solicitud no válido.";
                return RedirectToAction("Index");
            }

            var modelo = ChecklistBL.ObtenerPorSolicitud(id.Value);

            if (modelo == null)
            {
                TempData["Error"] = "No se encontró el checklist para esta solicitud.";
                return RedirectToAction("Index");
            }

            ViewBag.SolicitudId = id.Value;
            return View(VIEWS_TECNICO + "ListaChequeo.cshtml", modelo);
        }
    }
}
