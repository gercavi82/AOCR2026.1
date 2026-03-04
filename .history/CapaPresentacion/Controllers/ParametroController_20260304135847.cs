using System;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ParametroController : Controller
    {
        // GET: Parametro
        public ActionResult Index()
        {
            try
            {
                var lista = ParametroBL.ListarTodos();
                return View(lista ?? new List<Parametro>());
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar parámetros: " + ex.Message;
                return View(new List<Parametro>());
            }
        }

        // GET: Parametro/Crear
        public ActionResult Crear()
        {
            return View(new Parametro { Activo = true });
        }

        // POST: Parametro/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Parametro parametro)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string mensaje;
                bool ok = ParametroBL.Crear(parametro, codigoUsuario, out mensaje);

                if (ok)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction("Index");
                }

                TempData["Error"] = mensaje;
                return View(parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear parámetro: " + ex.Message;
                return View(parametro);
            }
        }

        // GET: Parametro/Editar/5
        public ActionResult Editar(int id)
        {
            try
            {
                var parametro = ParametroBL.ObtenerPorId(id);
                if (parametro == null)
                {
                    TempData["Error"] = "Parámetro no encontrado.";
                    return RedirectToAction("Index");
                }
                return View(parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar parámetro: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Parametro/Editar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Parametro parametro)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string mensaje;
                bool ok = ParametroBL.Actualizar(parametro, codigoUsuario, out mensaje);

                if (ok)
                {
                    TempData["Success"] = mensaje;
                    return RedirectToAction("Index");
                }

                TempData["Error"] = mensaje;
                return View(parametro);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar parámetro: " + ex.Message;
                return View(parametro);
            }
        }

        // POST: Parametro/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string mensaje;
                bool ok = ParametroBL.EliminarSoft(id, codigoUsuario, out mensaje);

                return Json(new { success = ok, mensaje });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error al eliminar: " + ex.Message });
            }
        }

        private int ObtenerCodigoUsuario()
        {
            if (Session["IdUsuario"] != null &&
                int.TryParse(Session["IdUsuario"].ToString(), out int id))
            {
                return id;
            }
            return 0;
        }
    }
}
