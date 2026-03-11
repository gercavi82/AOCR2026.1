using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using CapaDatos.DAOs;
using CapaDatos.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize] // No restringas aquí para no bloquear otras acciones por rol
    public class TecnicoController : Controller
    {
        // ✅ Según tu error, tu carpeta REAL parece ser: Views/Tecnico
        // Si NO es esa, cámbiala a la carpeta real (por ejemplo: "~/Views/Tecnico/")
        private const string VIEWS_TECNICO = "~/Views/Tecnico/";

        // =======================================================
        // LISTADO - Solicitudes pendientes de asignación
        // =======================================================
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones")]
        public ActionResult Index()
        {
            // Obtener solicitudes que necesitan asignación de inspector
            var lista = SolicitudAOCRBL.ObtenerPendientesAsignacion();
            return View(VIEWS_TECNICO + "Index.cshtml", lista);
        }

        // =======================================================
        // CREAR
        // =======================================================
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

        // =======================================================
        // EDITAR
        // =======================================================
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

        // =======================================================
        // ELIMINAR
        // =======================================================
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

        // =======================================================
        // ASIGNAR INSPECTOR (GET)
        // =======================================================
        [HttpGet]
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones")]
        public ActionResult AsignarInspector(int? solicitudId, string tipoInspector = "OPS")
        {
            if (!solicitudId.HasValue || solicitudId.Value <= 0)
            {
                TempData["Info"] = "Seleccione una solicitud pendiente para asignar inspector.";
                return RedirectToAction("Index");
            }

            var solicitud = SolicitudAOCRBL.ObtenerPorId(solicitudId.Value);
            if (solicitud == null)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Index");
            }

            var tipoInspectorNormalizado = NormalizarTipoInspector(tipoInspector);
            var inspectores = new List<CapaDatos.Models.InspectorAs400Record>();

            try
            {
                var inspectorAs400Dao = new InspectorAS400DAO(new SecureConfigurationService());
                inspectores = tipoInspectorNormalizado == "TODOS"
                    ? inspectorAs400Dao.ListarActivosPorTipos(new[] { "OPS", "AIR" })
                    : inspectorAs400Dao.ListarActivosPorTipo(tipoInspectorNormalizado);

                if (inspectores == null || inspectores.Count == 0)
                {
                    ViewBag.WarningInspectores = "No se encontraron inspectores en AS400 para el filtro seleccionado. Verifique estado/tipo en OPIAR2.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo cargar inspectores institucionales desde AS400: " + ex.Message;
            }

            ViewBag.TipoInspector = tipoInspectorNormalizado;
            ViewBag.TiposInspector = new SelectList(
                new List<SelectListItem>
                {
                    new SelectListItem { Value = "OPS", Text = "Operaciones (OPS)" },
                    new SelectListItem { Value = "AIR", Text = "Aeronavegabilidad (AIR)" },
                    new SelectListItem { Value = "TODOS", Text = "Todos (OPS + AIR)" }
                },
                "Value",
                "Text",
                tipoInspectorNormalizado);
            ViewBag.Inspectores = new SelectList(
                inspectores.Select(i => new
                {
                    Cedula = i.Cedula,
                    Etiqueta = i.EtiquetaLista
                }),
                "Cedula",
                "Etiqueta");

            return View(VIEWS_TECNICO + "AsignarInspector.cshtml", solicitud);
        }

        // =======================================================
        // ASIGNAR INSPECTOR (POST)
        // =======================================================
        [HttpPost]
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones")]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarInspector(
            int solicitudId,
            string inspectorPrincipal,
            string inspectorApoyo,
            DateTime fechaInspeccion,
            string observaciones,
            string tipoInspector = "OPS")
        {
            if (solicitudId <= 0)
            {
                TempData["Error"] = "Solicitud inválida.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(inspectorPrincipal))
            {
                TempData["Error"] = "Debe seleccionar un inspector principal activo.";
                return RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }

            try
            {
                string mensaje;
                bool ok = SolicitudAOCRBL.AsignarInspectores(
                    solicitudId,
                    inspectorPrincipal,
                    inspectorApoyo,
                    fechaInspeccion,
                    observaciones,
                    tipoInspector,
                    out mensaje
                );

                TempData[ok ? "Success" : "Error"] = mensaje;

                return ok
                    ? RedirectToAction("Index")
                    : RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Tecnico,Administrador,CoordinadorInspecciones,Inspector")]
        public JsonResult ListarInspectoresActivos(string tipoInspector = "OPS")
        {
            var tipoNormalizado = NormalizarTipoInspector(tipoInspector);
            var dao = new InspectorAS400DAO(new SecureConfigurationService());
            var data = tipoNormalizado == "TODOS"
                ? dao.ListarActivosPorTipos(new[] { "OPS", "AIR" })
                : dao.ListarActivosPorTipo(tipoNormalizado);

            var payload = data
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Cedula))
                .Select(x => new
                {
                    cedula = x.Cedula,
                    nombre = x.NombreCompleto,
                    tipo = x.Tipo,
                    etiqueta = x.EtiquetaLista
                })
                .ToList();

            return Json(new { success = true, tipo = tipoNormalizado, items = payload }, JsonRequestBehavior.AllowGet);
        }

        private static string NormalizarTipoInspector(string tipoInspector)
        {
            if (string.IsNullOrWhiteSpace(tipoInspector))
            {
                return "OPS";
            }

            var value = tipoInspector.Trim().ToUpperInvariant();
            if (value == "OPS" || value == "AIR" || value == "TODOS")
            {
                return value;
            }

            return "OPS";
        }

    }
}
