using System;
using System.Web.Mvc;
using CapaNegocio;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class RolController : Controller
    {
        // ============================================================
        // LISTADO
        // ============================================================
        public ActionResult Index()
        {
            try
            {
                var roles = RolBL.ObtenerTodos();
                return View(roles);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar roles: " + ex.Message;
                return View(new System.Collections.Generic.List<Rol>());
            }
        }

        // ============================================================
        // CREAR (GET)
        // ============================================================
        public ActionResult Crear()
        {
            return View(new Rol { Activo = true });
        }

        // ============================================================
        // CREAR (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Rol modelo)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Complete todos los campos obligatorios.";
                    return View(modelo);
                }

                modelo.CreadoPor = User.Identity.Name;

                string mensaje;
                bool ok = RolBL.Insertar(modelo, out mensaje);

                if (!ok)
                {
                    TempData["Error"] = mensaje;
                    return View(modelo);
                }

                TempData["Success"] = "Rol creado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear rol: " + ex.Message;
                return View(modelo);
            }
        }

        // ============================================================
        // EDITAR (GET)
        // ============================================================
        public ActionResult Editar(int id)
        {
            try
            {
                var rol = RolBL.ObtenerPorId(id);

                if (rol == null)
                {
                    TempData["Error"] = "Rol no encontrado.";
                    return RedirectToAction("Index");
                }

                return View(rol);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar rol: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // EDITAR (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Rol modelo)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Complete correctamente los campos.";
                    return View(modelo);
                }

                modelo.ActualizadoPor = User.Identity.Name;

                string mensaje;
                bool ok = RolBL.Actualizar(modelo, out mensaje);

                if (!ok)
                {
                    TempData["Error"] = mensaje;
                    return View(modelo);
                }

                TempData["Success"] = "Rol actualizado correctamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar rol: " + ex.Message;
                return View(modelo);
            }
        }

        // ============================================================
        // CAMBIAR ESTADO
        // ============================================================
        public ActionResult CambiarEstado(int id, string estado)
        {
            try
            {
                bool activo = false;

                if (!string.IsNullOrWhiteSpace(estado))
                {
                    var e = estado.ToUpper();
                    activo = (e == "ACTIVO" || e == "1" || e == "TRUE" || e == "SI" || e == "S");
                }

                string mensaje;
                bool ok = RolBL.CambiarEstado(id, activo, out mensaje);

                TempData[ok ? "Success" : "Error"] =
                    ok ? "Estado actualizado correctamente." : mensaje;
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cambiar estado: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ============================================================
        // ELIMINAR
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            try
            {
                string mensaje;
                bool ok = RolBL.Eliminar(id, out mensaje);

                return Json(new { success = ok, mensaje = ok ? "Rol eliminado correctamente." : mensaje });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error al eliminar rol: " + ex.Message });
            }
        }
    }
}
