using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaPresentacion.Models.ViewModels;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        // GET: OrdenRecaudacion
        public ActionResult Index(string estado = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
        {
            // TODO: Cargar lista de ordenes desde capa de negocio
            var ordenes = new List<OrdenRecaudacionViewModel>();
            
            ViewBag.FiltroEstado = estado;
            ViewBag.FiltroFechaDesde = fechaDesde;
            ViewBag.FiltroFechaHasta = fechaHasta;
            
            return View(ordenes);
        }

        // GET: OrdenRecaudacion/Nueva
        public ActionResult Nueva(string codigoSolicitud = null)
        {
            var model = new OrdenRecaudacionViewModel
            {
                CodigoSolicitud = codigoSolicitud,
                FechaCreacion = DateTime.Now,
                Estado = "Borrador",
                Estaciones = 0,
                Dias = 0
            };

            return View(model);
        }

        // POST: OrdenRecaudacion/Nueva
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Nueva(OrdenRecaudacionViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // TODO: Guardar orden en capa de negocio
                    int nuevoId = GuardarOrden(model);
                    
                    TempData["Exito"] = "Orden creada correctamente.";
                    return RedirectToAction("Detalles", new { id = nuevoId });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al guardar: " + ex.Message);
                }
            }

            return View(model);
        }

        // GET: OrdenRecaudacion/Editar/5
        public ActionResult Editar(int id)
        {
            var model = CargarOrden(id);
            
            if (model == null)
            {
                TempData["Error"] = "Orden no encontrada.";
                return RedirectToAction("Index");
            }

            // Verificar si puede editar
            if (model.Estado == "Pagada" || model.Estado == "Anulada")
            {
                TempData["Advertencia"] = "Esta orden no puede ser editada en su estado actual.";
                return RedirectToAction("Detalles", new { id = id });
            }

            return View(model);
        }

        // POST: OrdenRecaudacion/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(OrdenRecaudacionViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    ActualizarOrden(model);
                    TempData["Exito"] = "Orden actualizada correctamente.";
                    return RedirectToAction("Detalles", new { id = model.Id });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al actualizar: " + ex.Message);
                }
            }

            return View(model);
        }

        // GET: OrdenRecaudacion/Detalles/5
        public ActionResult Detalles(int id)
        {
            var model = CargarOrden(id);
            
            if (model == null)
            {
                TempData["Error"] = "Orden no encontrada.";
                return RedirectToAction("Index");
            }

            // Establecer acciones disponibles segun estado
            ViewBag.PuedeEditar = model.Estado == "Borrador" || model.Estado == "Generada";
            ViewBag.PuedeEnviar = model.Estado == "Generada";
            ViewBag.PuedeMarcarPagada = model.Estado == "Enviada";
            ViewBag.PuedeAnular = model.Estado == "Generada" || model.Estado == "Enviada";

            return View(model);
        }

        // POST: OrdenRecaudacion/CambiarEstado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstado(int id, string accion)
        {
            var orden = CargarOrden(id);
            if (orden == null)
            {
                return Json(new { success = false, mensaje = "Orden no encontrada." });
            }

            string nuevoEstado = null;
            string mensajeExito = "";

            switch (accion)
            {
                case "generar":
                    if (orden.Estado != "Borrador")
                    {
                        return Json(new { success = false, mensaje = "Solo se pueden generar ordenes en borrador." });
                    }
                    nuevoEstado = "Generada";
                    mensajeExito = "Orden generada correctamente.";
                    break;

                case "enviar":
                    if (orden.Estado != "Generada")
                    {
                        return Json(new { success = false, mensaje = "Solo se pueden enviar ordenes generadas." });
                    }
                    nuevoEstado = "Enviada";
                    mensajeExito = "Orden enviada correctamente.";
                    break;

                case "pagar":
                    if (orden.Estado != "Enviada")
                    {
                        return Json(new { success = false, mensaje = "Solo se pueden pagar ordenes enviadas." });
                    }
                    nuevoEstado = "Pagada";
                    mensajeExito = "Orden marcada como pagada.";
                    break;

                case "anular":
                    if (orden.Estado != "Generada" && orden.Estado != "Enviada")
                    {
                        return Json(new { success = false, mensaje = "Esta orden no puede ser anulada." });
                    }
                    nuevoEstado = "Anulada";
                    mensajeExito = "Orden anulada correctamente.";
                    break;

                default:
                    return Json(new { success = false, mensaje = "Accion no valida." });
            }

            try
            {
                ActualizarEstadoOrden(id, nuevoEstado);
                return Json(new { success = true, mensaje = mensajeExito });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error: " + ex.Message });
            }
        }

        // GET: OrdenRecaudacion/Obligatoria
        public ActionResult Obligatoria()
        {
            // Cargar ordenes urgentes (pendientes)
            var ordenes = CargarOrdenesPendientes();
            
            ViewBag.TotalPendientes = ordenes.Count;
            ViewBag.MontoTotalPendiente = ordenes.Sum(o => o.Total);

            return View(ordenes);
        }

        #region Metodos Auxiliares

        private OrdenRecaudacionViewModel CargarOrden(int id)
        {
            // TODO: Implementar carga desde capa de negocio
            return new OrdenRecaudacionViewModel
            {
                Id = id,
                CodigoSolicitud = "SOL-001",
                NumeroOrden = "ORD-" + id.ToString("D6"),
                FechaCreacion = DateTime.Now,
                Estado = "Borrador",
                Estaciones = 2,
                Dias = 5,
                ValorBase = 100.00m,
                Inspeccion = 50.00m,
                Viaticos = 25.00m,
                GastosAdmin = 10.00m,
                Subtotal = 175.00m,
                Admin = 17.50m,
                Total = 192.50m
            };
        }

        private List<OrdenRecaudacionViewModel> CargarOrdenesPendientes()
        {
            // TODO: Implementar carga desde capa de negocio
            return new List<OrdenRecaudacionViewModel>();
        }

        private int GuardarOrden(OrdenRecaudacionViewModel model)
        {
            // TODO: Implementar guardado en capa de negocio
            return 1;
        }

        private void ActualizarOrden(OrdenRecaudacionViewModel model)
        {
            // TODO: Implementar actualizacion en capa de negocio
        }

        private void ActualizarEstadoOrden(int id, string estado)
        {
            // TODO: Implementar actualizacion de estado en capa de negocio
        }

        #endregion
    }
}
