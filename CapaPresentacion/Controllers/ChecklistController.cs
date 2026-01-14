using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador,Inspector")]
    public class ChecklistController : Controller
    {
        // INDEX
        public ActionResult Index()
        {
            var lista = ChecklistBL.ObtenerTodos();
            return View(lista);
        }

        // CREAR (GET)
        public ActionResult Crear()
        {
            return View();
        }

        // CREAR (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Checklist modelo)
        {
            if (!ModelState.IsValid)
                return View(modelo);

            modelo.CreatedAt = DateTime.Now;
            modelo.CreatedBy = User.Identity.Name;

            string mensaje;
            bool ok = ChecklistBL.Insertar(modelo, out mensaje);

            if (ok)
            {
                TempData["Success"] = mensaje;
                return RedirectToAction("Index");
            }

            ViewBag.Error = mensaje;
            return View(modelo);
        }

        // EDITAR (GET)
        public ActionResult Editar(int id)
        {
            var checklist = ChecklistBL.ObtenerPorId(id);
            if (checklist == null)
            {
                TempData["Error"] = "Checklist no encontrado.";
                return RedirectToAction("Index");
            }

            return View(checklist);
        }

        // EDITAR (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Checklist modelo)
        {
            if (!ModelState.IsValid)
                return View(modelo);

            modelo.UpdatedAt = DateTime.Now;
            modelo.UpdatedBy = User.Identity.Name;

            string mensaje;
            bool ok = ChecklistBL.Actualizar(modelo, out mensaje);

            if (ok)
            {
                TempData["Success"] = mensaje;
                return RedirectToAction("Index");
            }

            ViewBag.Error = mensaje;
            return View(modelo);
        }

        // ELIMINAR LÓGICO (AJAX)
        [HttpPost]
        public JsonResult Eliminar(int id)
        {
            string usuario = User.Identity.Name;
            string mensaje;
            bool ok = ChecklistBL.EliminarLogico(id, usuario, out mensaje);

            return Json(new { success = ok, mensaje });
        }

        // DETALLE (GET)
        public ActionResult Detalle(int id)
        {
            var checklist = ChecklistBL.ObtenerPorId(id);
            if (checklist == null)
            {
                TempData["Error"] = "Checklist no encontrado.";
                return RedirectToAction("Index");
            }

            return View(checklist);
        }
    }
}
