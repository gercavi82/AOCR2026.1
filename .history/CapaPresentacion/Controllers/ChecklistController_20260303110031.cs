using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class ChecklistController : Controller
    {
        // GET: Checklist
        public ActionResult Index()
        {
            try
            {
                // TODO: Implement listing via ChecklistDAO when ObtenerTodos is available
                return View(new List<Checklist>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar checklists: " + ex.Message;
                return View(new List<Checklist>());
            }
        }

        // GET: Checklist/Crear
        public ActionResult Crear()
        {
            return View(new Checklist());
        }

        // POST: Checklist/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Checklist checklist)
        {
            try
            {
                // TODO: Wire to ChecklistBL.Insertar when Checklist-level CRUD is available
                TempData["Success"] = "Checklist creado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear checklist: " + ex.Message;
                return View(checklist);
            }
        }

        // GET: Checklist/Editar/5
        public ActionResult Editar(int id)
        {
            try
            {
                // TODO: Implement ObtenerPorId
                return View(new Checklist { CodigoChecklist = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar checklist: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Checklist/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Checklist checklist)
        {
            try
            {
                TempData["Success"] = "Checklist actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar checklist: " + ex.Message;
                return View(checklist);
            }
        }

        // GET: Checklist/Detalle/5
        public ActionResult Detalle(int id)
        {
            try
            {
                // TODO: Load real data
                return View(new Checklist { CodigoChecklist = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar checklist: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}