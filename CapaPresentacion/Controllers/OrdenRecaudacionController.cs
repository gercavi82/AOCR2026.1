using System;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaPresentacion.Models.ViewModels;
using System.Threading.Tasks;
using System.Linq;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly IOrdenRecaudacionDAO _dao;

        public OrdenRecaudacionController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        [HttpGet]
        public ActionResult Nueva()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Debe iniciar sesión para acceder a esta función";
                    return RedirectToAction("Login", "Account");
                }

                // Verificar si ya tiene orden BORRADOR
                if (_dao.ExisteORMinima(idUsuario))
                {
                    TempData["Advertencia"] = "Ya tiene una orden en estado BORRADOR. Debe completarla antes de crear una nueva.";
                    return RedirectToAction("MisOrdenes", "Orden");
                }

                return View(new OrdenRecaudacionViewModel());
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Crear(OrdenRecaudacionViewModel model)
        {
            try
            {
                // Validar modelo
                if (!ModelState.IsValid)
                {
                    var errores = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    
                    return Json(new { 
                        success = false, 
                        mensaje = "Corrija los siguientes errores:",
                        errores = errores
                    });
                }

                // Obtener usuario
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    return Json(new { 
                        success = false, 
                        mensaje = "Su sesión ha expirado. Por favor, inicie sesión nuevamente."
                    });
                }

                // Validaciones de negocio
                if (model.Estaciones < 0 || model.Estaciones > 50)
                {
                    return Json(new { 
                        success = false, 
                        mensaje = "El número de estaciones debe estar entre 0 y 50."
                    });
                }

                if (model.Dias < 0 || model.Dias > 30)
                {
                    return Json(new { 
                        success = false, 
                        mensaje = "El número de días debe estar entre 0 y 30."
                    });
                }

                // Crear orden
                int ordenId = await Task.Run(() => 
                    _dao.InsertarOrdenAOCR(
                        idUsuario,
                        model.CodigoSolicitud ?? 0,
                        model.ConceptoPrincipalCodigo,
                        model.Estaciones,
                        model.Dias,
                        model.Observacion ?? ""
                    ));

                if (ordenId > 0)
                {
                    return Json(new { 
                        success = true, 
                        ordenId = ordenId,
                        mensaje = "Orden creada exitosamente",
                        redireccion = Url.Action("Detalle", "Orden", new { id = ordenId })
                    });
                }

                return Json(new { 
                    success = false, 
                    mensaje = "No se pudo crear la orden. Intente nuevamente." 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    mensaje = ex.Message
                });
            }
        }

        private int ObtenerIdUsuario()
        {
            if (Session["IdUsuario"] != null && 
                int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            return 0;
        }
    }
}