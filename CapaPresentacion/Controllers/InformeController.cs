using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class InformeController : Controller
    {
        // GET: Informe
        public ActionResult Index()
        {
            try
            {
                var lista = InformeBL.ObtenerTodos();
                return View(lista ?? new List<Informe>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar informes: " + ex.Message;
                return View(new List<Informe>());
            }
        }

        // GET: Informe/Crear
        public ActionResult Crear()
        {
            return View(new Informe());
        }

        // POST: Informe/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Informe informe)
        {
            try
            {
                string mensaje;
                bool ok = InformeBL.Insertar(informe, out mensaje);

                if (ok)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction("Index");
                }

                TempData["Error"] = mensaje;
                return View(informe);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear informe: " + ex.Message;
                return View(informe);
            }
        }

        // GET: Informe/Ver/5
        public ActionResult Ver(int id)
        {
            try
            {
                var informe = InformeBL.ObtenerPorId(id);
                if (informe == null)
                {
                    TempData["Error"] = "Informe no encontrado.";
                    return RedirectToAction("Index");
                }
                return View(informe);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar informe: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Informe/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            try
            {
                string mensaje;
                bool ok = InformeBL.Eliminar(id, out mensaje);

                return Json(new { success = ok, mensaje });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error: " + ex.Message });
            }
        }
    }
}
