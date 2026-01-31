using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        // GET: OrdenRecaudacion
        public ActionResult Index(string estado = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
        {
            var model = new OrdenRecaudacionIndexViewModel();
            
            // Cargar datos desde la capa de negocio
            model = CargarDatosIndex(estado, fechaDesde, fechaHasta);
            
            // Verificar permisos
            model.PuedeCrearOrden = UsuarioPuedeCrear();
            model.ExisteOrdenEnBorrador = ExisteOrdenEnBorrador();
            
            // Filtros activos
            model.FiltroEstado = estado;
            model.FiltroFechaDesde = fechaDesde;
            model.FiltroFechaHasta = fechaHasta;
            
            return View(model);
        }

        // GET: OrdenRecaudacion/Nueva
        public ActionResult Nueva()
        {
            // Verificar si ya existe borrador
            if (ExisteOrdenEnBorrador())
            {
                TempData["Advertencia"] = "Ya existe una orden en borrador. Complete o elimine el borrador existente antes de crear una nueva.";
                return RedirectToAction("Index");
            }

            var model = new OrdenRecaudacionFormViewModel
            {
                EsNuevo = true,
                Fecha = DateTime.Today,
                Estado = EstadoOrden.Borrador,
                PuedeGuardarBorrador = true,
                PuedeGenerar = true,
                PuedeEditar = true
            };

            return View(model);
        }

        // POST: OrdenRecaudacion/Nueva
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Nueva(OrdenRecaudacionFormViewModel model, string accion)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Determinar estado segun accion
                    EstadoOrden nuevoEstado = EstadoOrden.Borrador;
                    if (accion == "generar")
                    {
                        nuevoEstado = EstadoOrden.Generada;
                    }

                    // Guardar orden
                    int codigoOrden = GuardarOrden(model, nuevoEstado);

                    // Registrar evento
                    RegistrarEvento(codigoOrden, "Creacion", null, nuevoEstado);

                    if (nuevoEstado == EstadoOrden.Generada)
                    {
                        TempData["Exito"] = "Orden generada correctamente.";
                    }
                    else
                    {
                        TempData["Exito"] = "Borrador guardado correctamente.";
                    }

                    return RedirectToAction("Detalles", new { id = codigoOrden });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al guardar: " + ex.Message);
                }
            }

            model.EsNuevo = true;
            model.PuedeGuardarBorrador = true;
            model.PuedeGenerar = true;
            model.PuedeEditar = true;

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
            if (!EstadoOrdenHelper.PuedeEditar(model.Estado))
            {
                model.PuedeEditar = false;
                model.MensajeBloqueo = ObtenerMensajeBloqueo(model.Estado);
                TempData["Advertencia"] = model.MensajeBloqueo;
                return RedirectToAction("Detalles", new { id = id });
            }

            model.EsNuevo = false;
            model.PuedeEditar = true;
            model.PuedeGuardarBorrador = model.Estado == EstadoOrden.Borrador;
            model.PuedeGenerar = model.Estado == EstadoOrden.Borrador;

            return View(model);
        }

        // POST: OrdenRecaudacion/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(OrdenRecaudacionFormViewModel model, string accion)
        {
            // Verificar estado actual
            var ordenActual = CargarOrden(model.CodigoOrden);
            if (ordenActual == null)
            {
                TempData["Error"] = "Orden no encontrada.";
                return RedirectToAction("Index");
            }

            if (!EstadoOrdenHelper.PuedeEditar(ordenActual.Estado))
            {
                TempData["Error"] = ObtenerMensajeBloqueo(ordenActual.Estado);
                return RedirectToAction("Detalles", new { id = model.CodigoOrden });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    EstadoOrden nuevoEstado = ordenActual.Estado;
                    if (accion == "generar" && ordenActual.Estado == EstadoOrden.Borrador)
                    {
                        nuevoEstado = EstadoOrden.Generada;
                    }

                    ActualizarOrden(model, nuevoEstado);
                    
                    if (ordenActual.Estado != nuevoEstado)
                    {
                        RegistrarEvento(model.CodigoOrden, "Cambio de estado", ordenActual.Estado, nuevoEstado);
                    }
                    else
                    {
                        RegistrarEvento(model.CodigoOrden, "Edicion", null, null);
                    }

                    TempData["Exito"] = "Orden actualizada correctamente.";
                    return RedirectToAction("Detalles", new { id = model.CodigoOrden });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al actualizar: " + ex.Message);
                }
            }

            model.EsNuevo = false;
            model.PuedeEditar = true;
            model.PuedeGuardarBorrador = ordenActual.Estado == EstadoOrden.Borrador;
            model.PuedeGenerar = ordenActual.Estado == EstadoOrden.Borrador;

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

            // Cargar historial
            model.HistorialEventos = CargarHistorial(id);

            // Establecer acciones disponibles
            model.PuedeEditar = EstadoOrdenHelper.PuedeEditar(model.Estado);
            model.PuedeEnviar = EstadoOrdenHelper.PuedeEnviar(model.Estado);
            model.PuedeMarcarPagada = EstadoOrdenHelper.PuedeMarcarPagada(model.Estado);
            model.PuedeAnular = EstadoOrdenHelper.PuedeAnular(model.Estado);

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

            EstadoOrden? nuevoEstado = null;
            string mensajeExito = "";

            switch (accion)
            {
                case "enviar":
                    if (!EstadoOrdenHelper.PuedeEnviar(orden.Estado))
                    {
                        return Json(new { success = false, mensaje = "No se puede enviar esta orden." });
                    }
                    nuevoEstado = EstadoOrden.Enviada;
                    mensajeExito = "Orden enviada correctamente.";
                    break;

                case "pagar":
                    if (!EstadoOrdenHelper.PuedeMarcarPagada(orden.Estado))
                    {
                        return Json(new { success = false, mensaje = "No se puede marcar como pagada." });
                    }
                    nuevoEstado = EstadoOrden.Pagada;
                    mensajeExito = "Orden marcada como pagada.";
                    break;

                case "anular":
                    if (!EstadoOrdenHelper.PuedeAnular(orden.Estado))
                    {
                        return Json(new { success = false, mensaje = "No se puede anular esta orden." });
                    }
                    nuevoEstado = EstadoOrden.Anulada;
                    mensajeExito = "Orden anulada correctamente.";
                    break;

                default:
                    return Json(new { success = false, mensaje = "Accion no valida." });
            }

            try
            {
                if (nuevoEstado.HasValue)
                {
                    ActualizarEstadoOrden(id, nuevoEstado.Value);
                    RegistrarEvento(id, accion, orden.Estado, nuevoEstado.Value);
                }

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
            var model = new OrdenRecaudacionObligatoriaViewModel();
            
            // Cargar ordenes urgentes (vencidas o proximas a vencer)
            model.OrdenesUrgentes = CargarOrdenesUrgentes();
            model.TotalVencidas = model.OrdenesUrgentes.Count(o => o.EstaVencida);
            model.TotalProximasAVencer = model.OrdenesUrgentes.Count(o => !o.EstaVencida && o.DiasParaVencer <= 7);
            model.MontoTotalUrgente = model.OrdenesUrgentes.Sum(o => o.Monto);

            return View(model);
        }

        #region Metodos Auxiliares (compatibles con C# 5)

        private OrdenRecaudacionIndexViewModel CargarDatosIndex(string estado, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var model = new OrdenRecaudacionIndexViewModel();
            
            // TODO: Implementar carga desde capa de negocio
            // Ejemplo de datos de prueba
            model.TotalOrdenes = 50;
            model.OrdenesPendientes = 15;
            model.OrdenesPagadas = 30;
            model.OrdenesAnuladas = 3;
            model.OrdenesBorrador = 2;
            model.MontoTotalPendiente = 25000.00m;
            
            return model;
        }

        private OrdenRecaudacionFormViewModel CargarOrden(int id)
        {
            // TODO: Implementar carga desde capa de negocio
            return new OrdenRecaudacionFormViewModel
            {
                CodigoOrden = id,
                Cliente = "Cliente de prueba",
                Monto = 1000.00m,
                Concepto = "Concepto de prueba",
                Fecha = DateTime.Today,
                Estado = EstadoOrden.Borrador,
                EstadoDescripcion = EstadoOrdenHelper.ObtenerDescripcion(EstadoOrden.Borrador)
            };
        }

        private List<EventoOrdenViewModel> CargarHistorial(int id)
        {
            // TODO: Implementar carga desde capa de negocio
            return new List<EventoOrdenViewModel>();
        }

        private List<OrdenRecaudacionResumenViewModel> CargarOrdenesUrgentes()
        {
            // TODO: Implementar carga desde capa de negocio
            return new List<OrdenRecaudacionResumenViewModel>();
        }

        private int GuardarOrden(OrdenRecaudacionFormViewModel model, EstadoOrden estado)
        {
            // TODO: Implementar guardado en capa de negocio
            return 1;
        }

        private void ActualizarOrden(OrdenRecaudacionFormViewModel model, EstadoOrden estado)
        {
            // TODO: Implementar actualizacion en capa de negocio
        }

        private void ActualizarEstadoOrden(int id, EstadoOrden estado)
        {
            // TODO: Implementar actualizacion de estado en capa de negocio
        }

        private void RegistrarEvento(int codigoOrden, string accion, EstadoOrden? estadoAnterior, EstadoOrden? estadoNuevo)
        {
            // TODO: Implementar registro de evento en capa de negocio
        }

        private bool UsuarioPuedeCrear()
        {
            // TODO: Implementar verificacion de permisos
            return true;
        }

        private bool ExisteOrdenEnBorrador()
        {
            // TODO: Implementar verificacion
            return false;
        }

        private string ObtenerMensajeBloqueo(EstadoOrden estado)
        {
            switch (estado)
            {
                case EstadoOrden.Enviada:
                    return "Esta orden ya fue enviada y no puede ser editada. Puede anularla si es necesario.";
                case EstadoOrden.Pagada:
                    return "Esta orden ya fue pagada y no puede ser modificada.";
                case EstadoOrden.Anulada:
                    return "Esta orden fue anulada y no puede ser modificada.";
                default:
                    return "Esta orden no puede ser editada en su estado actual.";
            }
        }

        #endregion
    }
}
