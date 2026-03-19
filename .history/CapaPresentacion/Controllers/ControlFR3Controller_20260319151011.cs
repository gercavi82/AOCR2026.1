using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaNegocio;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controlador para gestión de Control FR3 (vuelos charter/especiales)
    /// </summary>
    [Authorize]
    public class ControlFR3Controller : Controller
    {
        private readonly ControlFR3BL _bl;
        private readonly ControlFR3DAO _dao;

        public ControlFR3Controller()
        {
            _bl = new ControlFR3BL();
            _dao = new ControlFR3DAO();
        }

        #region Vistas principales

        /// <summary>
        /// Lista de controles FR3
        /// </summary>
        [Authorize(Roles = "Administrador,Financiero,Operador")]
        public ActionResult Index(string aeropuerto, string anio, string estado)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            try
            {
                var controles = _bl.Listar(aeropuerto, anio, estado);
                
                // Estadísticas
                var stats = _bl.ObtenerEstadisticas(aeropuerto);
                ViewBag.Estadisticas = stats;
                ViewBag.TotalRegistros = stats.Values.Sum();
                
                // Filtros
                ViewBag.Aeropuerto = aeropuerto;
                ViewBag.Anio = anio ?? DateTime.Now.Year.ToString();
                ViewBag.Estado = estado;
                CargarAeropuertosCombo(aeropuerto);
                CargarEstadosCombo(estado);

                return View(controles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ControlFR3.Index: " + ex.Message);
                TempData["Error"] = "Error al cargar los controles FR3: " + ex.Message;
                return View(new List<ControlFR3>());
            }
        }

        /// <summary>
        /// Ver detalle de un control FR3
        /// </summary>
        [Authorize(Roles = "Administrador,Financiero,Operador")]
        public async Task<ActionResult> Detalles(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            try
            {
                var control = await _bl.ObtenerPorIdAsync(id);
                if (control == null)
                    return HttpNotFound("Control FR3 no encontrado.");

                // Cargar detalles
                ViewBag.Detalles = _bl.ObtenerDetalles(id);
                return View(control);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ControlFR3.Detalles: " + ex.Message);
                TempData["Error"] = "Error al obtener el detalle: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Formulario de nuevo control FR3
        /// </summary>
        [Authorize(Roles = "Administrador,Operador")]
        public ActionResult Nuevo()
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var model = new ControlFR3
            {
                Anio = DateTime.Now.Year.ToString(),
                NacInter = "N",
                Estado = "E",
                UsuarioCr = User.Identity.Name
            };

            CargarAeropuertosCombo(null);
            CargarTipoOperacionCombo(null);
            CargarFormaPagoCombo(null);
            CargarNacInterCombo("N");

            return View(model);
        }

        /// <summary>
        /// Crear nuevo control FR3
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Operador")]
        public async Task<ActionResult> Nuevo(ControlFR3 model)
        {
            try
            {
                if (!TryNormalizarTelefono(model))
                {
                    CargarAeropuertosCombo(model.Aeropuerto);
                    CargarTipoOperacionCombo(model.TipoOperacion);
                    CargarFormaPagoCombo(model.FormaPago);
                    CargarNacInterCombo(model.NacInter);
                    return View(model);
                }

                if (!model.EsValido())
                {
                    ModelState.AddModelError("", "Complete todos los campos obligatorios.");
                    CargarAeropuertosCombo(model.Aeropuerto);
                    CargarTipoOperacionCombo(model.TipoOperacion);
                    CargarFormaPagoCombo(model.FormaPago);
                    CargarNacInterCombo(model.NacInter);
                    return View(model);
                }

                model.UsuarioCr = User.Identity.Name;
                var id = await _bl.CrearAsync(model);

                if (id > 0)
                {
                    TempData["OK"] = string.Format("Control FR3 #{0} creado exitosamente (Sec. {1})", id, model.Secuencial);
                    return RedirectToAction("Detalles", new { id = id });
                }

                ModelState.AddModelError("", "Error al guardar el control FR3.");
                CargarAeropuertosCombo(model.Aeropuerto);
                CargarTipoOperacionCombo(model.TipoOperacion);
                CargarFormaPagoCombo(model.FormaPago);
                CargarNacInterCombo(model.NacInter);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en ControlFR3.Nuevo POST: " + ex.Message);
                ModelState.AddModelError("", "Error: " + ex.Message);
                CargarAeropuertosCombo(model.Aeropuerto);
                CargarTipoOperacionCombo(model.TipoOperacion);
                CargarFormaPagoCombo(model.FormaPago);
                CargarNacInterCombo(model.NacInter);
                return View(model);
            }
        }

        /// <summary>
        /// Formulario de edición de un control FR3
        /// </summary>
        [Authorize(Roles = "Administrador,Operador")]
        public async Task<ActionResult> Editar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            try
            {
                var control = await _bl.ObtenerPorIdAsync(id);
                if (control == null)
                    return HttpNotFound("Control FR3 no encontrado.");

                CargarAeropuertosCombo(control.Aeropuerto);
                CargarTipoOperacionCombo(control.TipoOperacion);
                CargarFormaPagoCombo(control.FormaPago);
                CargarNacInterCombo(control.NacInter);
                CargarEstadosCombo(control.Estado);

                return View(control);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el control: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// Actualizar control FR3
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Operador")]
        public async Task<ActionResult> Editar(ControlFR3 model)
        {
            try
            {
                if (!TryNormalizarTelefono(model))
                {
                    CargarAeropuertosCombo(model.Aeropuerto);
                    CargarTipoOperacionCombo(model.TipoOperacion);
                    CargarFormaPagoCombo(model.FormaPago);
                    CargarNacInterCombo(model.NacInter);
                    CargarEstadosCombo(model.Estado);
                    return View(model);
                }

                var resultado = await _bl.ActualizarAsync(model);

                if (resultado)
                {
                    TempData["OK"] = "Control FR3 actualizado correctamente.";
                    return RedirectToAction("Detalles", new { id = model.Id });
                }

                ModelState.AddModelError("", "No se pudo actualizar el control FR3.");
                CargarAeropuertosCombo(model.Aeropuerto);
                CargarTipoOperacionCombo(model.TipoOperacion);
                CargarFormaPagoCombo(model.FormaPago);
                CargarNacInterCombo(model.NacInter);
                return View(model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                CargarAeropuertosCombo(model.Aeropuerto);
                CargarTipoOperacionCombo(model.TipoOperacion);
                CargarFormaPagoCombo(model.FormaPago);
                CargarNacInterCombo(model.NacInter);
                return View(model);
            }
        }

        #endregion

        #region Acciones

        /// <summary>
        /// Cambiar estado de un control FR3
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Financiero")]
        public JsonResult CambiarEstado(int id, string nuevoEstado)
        {
            try
            {
                var resultado = _bl.CambiarEstado(id, nuevoEstado);
                return Json(new { success = resultado, message = resultado ? "Estado actualizado." : "No se pudo actualizar." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Eliminar (lógico) un control FR3
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public JsonResult Eliminar(int id)
        {
            try
            {
                var resultado = _bl.Eliminar(id);
                return Json(new { success = resultado, message = resultado ? "Control FR3 eliminado." : "No se pudo eliminar." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// API JSON: Listar controles FR3 (para DataTables / AJAX)
        /// </summary>
        [Authorize(Roles = "Administrador,Financiero,Operador")]
        public JsonResult ListarJson(string aeropuerto, string anio, string estado)
        {
            try
            {
                var controles = _bl.Listar(aeropuerto, anio, estado);
                var resultado = controles.Select(c => new
                {
                    c.Id,
                    c.Secuencial,
                    c.Aeropuerto,
                    c.Anio,
                    c.Matricula,
                    c.NombreCia,
                    c.Ruc,
                    c.Origen,
                    c.Destino,
                    c.GranTotal,
                    c.Estado,
                    c.FechaControlVuelo,
                    c.TipoOperacion,
                    c.NacInter,
                    FechaCreacion = c.FechaCreacion.ToString("yyyy-MM-dd HH:mm")
                });

                return Json(new { success = true, data = resultado }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// Diagnóstico de conexión
        /// </summary>
        [Authorize(Roles = "Administrador")]
        public JsonResult Ping()
        {
            return Json(new { ok = _dao.Ping() }, JsonRequestBehavior.AllowGet);
        }

        #endregion

        #region Helpers privados

        private int GetUserId()
        {
            int id = 0;
            var v = Session["UserId"] ?? Session["IdUsuario"];
            if (v != null)
            {
                int.TryParse(v.ToString(), out id);
            }
            return id;
        }

        private bool TryNormalizarTelefono(ControlFR3 model)
        {
            if (model == null)
            {
                return true;
            }

            var telefono = (model.Telefono ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(telefono))
            {
                model.Telefono = string.Empty;
                return true;
            }

            if (!telefono.All(char.IsDigit))
            {
                ModelState.AddModelError("Telefono", "El teléfono solo debe contener números.");
                return false;
            }

            if (telefono.Length > 15)
            {
                ModelState.AddModelError("Telefono", "El teléfono no puede tener más de 15 dígitos.");
                return false;
            }

            model.Telefono = telefono;
            return true;
        }

        private void CargarAeropuertosCombo(string seleccionado)
        {
            var aeropuertos = new List<SelectListItem>
            {
                new SelectListItem { Text = "-- Todos --", Value = "" },
                new SelectListItem { Text = "UIO - Quito", Value = "UIO" },
                new SelectListItem { Text = "GYE - Guayaquil", Value = "GYE" },
                new SelectListItem { Text = "CUE - Cuenca", Value = "CUE" },
                new SelectListItem { Text = "MEC - Manta", Value = "MEC" },
                new SelectListItem { Text = "LTX - Latacunga", Value = "LTX" },
                new SelectListItem { Text = "SCY - San Cristóbal", Value = "SCY" },
                new SelectListItem { Text = "GPS - Galápagos", Value = "GPS" }
            };

            if (!string.IsNullOrWhiteSpace(seleccionado))
            {
                foreach (var item in aeropuertos)
                    item.Selected = item.Value == seleccionado;
            }

            ViewBag.Aeropuertos = aeropuertos;
        }

        private void CargarEstadosCombo(string seleccionado)
        {
            var estados = new List<SelectListItem>
            {
                new SelectListItem { Text = "-- Todos --", Value = "" },
                new SelectListItem { Text = "Emitido", Value = "E" },
                new SelectListItem { Text = "Procesado", Value = "P" },
                new SelectListItem { Text = "Anulado", Value = "A" },
                new SelectListItem { Text = "Pagado", Value = "G" }
            };

            if (!string.IsNullOrWhiteSpace(seleccionado))
            {
                foreach (var item in estados)
                    item.Selected = item.Value == seleccionado;
            }

            ViewBag.Estados = estados;
        }

        private void CargarTipoOperacionCombo(string seleccionado)
        {
            var tipos = new List<SelectListItem>
            {
                new SelectListItem { Text = "-- Seleccione --", Value = "" },
                new SelectListItem { Text = "Charter Nacional", Value = "CHARTER_NAC" },
                new SelectListItem { Text = "Charter Internacional", Value = "CHARTER_INT" },
                new SelectListItem { Text = "Vuelo Especial", Value = "ESPECIAL" },
                new SelectListItem { Text = "Registro Aeronáutico", Value = "REG_AERO" }
            };

            if (!string.IsNullOrWhiteSpace(seleccionado))
            {
                foreach (var item in tipos)
                    item.Selected = item.Value == seleccionado;
            }

            ViewBag.TiposOperacion = tipos;
        }

        private void CargarFormaPagoCombo(string seleccionado)
        {
            var formas = new List<SelectListItem>
            {
                new SelectListItem { Text = "-- Seleccione --", Value = "" },
                new SelectListItem { Text = "Efectivo", Value = "EFECTIVO" },
                new SelectListItem { Text = "Transferencia", Value = "TRANSFERENCIA" },
                new SelectListItem { Text = "Cheque", Value = "CHEQUE" },
                new SelectListItem { Text = "Depósito", Value = "DEPOSITO" }
            };

            if (!string.IsNullOrWhiteSpace(seleccionado))
            {
                foreach (var item in formas)
                    item.Selected = item.Value == seleccionado;
            }

            ViewBag.FormasPago = formas;
        }

        private void CargarNacInterCombo(string seleccionado)
        {
            var opciones = new List<SelectListItem>
            {
                new SelectListItem { Text = "Nacional", Value = "N" },
                new SelectListItem { Text = "Internacional", Value = "I" }
            };

            if (!string.IsNullOrWhiteSpace(seleccionado))
            {
                foreach (var item in opciones)
                    item.Selected = item.Value == seleccionado;
            }

            ViewBag.NacInter = opciones;
        }

        #endregion
    }
}
