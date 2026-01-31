using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Configuration;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaDatos.Models;
using CapaNegocio;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;
using CapaDatos.Interfaces;
using System.Threading.Tasks;
using CapaPresentacion.Models;
using LoggingService = CapaNegocio.Services.ILoggingService;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();
        private readonly OrdenRecaudacionBL _bl = new OrdenRecaudacionBL();
        private readonly ConceptoDAO _conceptoDao = new ConceptoDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly IOrdenRecaudacionOrchestrator _orchestrator;

        // Constructor actualizado (solo orquestador; el resto usa DAOs locales)
        public OrdenRecaudacionController(IOrdenRecaudacionOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        // ✅ Para confirmar conexión real a DB (útil en producción)
        [Authorize(Roles = "Administrador,Financiero")]
        public JsonResult DbPing()
        {
            return Json(new { ok = _dao.Ping() }, JsonRequestBehavior.AllowGet);
        }

        // GET: /OrdenRecaudacion?estado=GENERADA
        public ActionResult Index(string estado)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            CargarEstadosCombo(estado);

            var ordenes = _dao.ListarPorUsuario(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();

            // Estadísticas: tu view espera claves con mayúscula
            var est = _dao.ObtenerEstadisticas(idUsuario);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);

            return View(ordenes);
        }

        // GET: /OrdenRecaudacion/Obligatoria
        public ActionResult Obligatoria()
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Obligatoria: Usuario no autenticado, redirigiendo a login");
                return RedirectToAction("Login", "Account");
            }

            System.Diagnostics.Debug.WriteLine($"Obligatoria: Usuario ID = {idUsuario}");

            CargarEstadosCombo(null);

            var ordenes = _dao.ListarPorUsuario(idUsuario, null) ?? new List<OrdenRecaudacionModel>();
            System.Diagnostics.Debug.WriteLine($"Obligatoria: Se encontraron {ordenes.Count} órdenes para el usuario {idUsuario}");

            // Estadísticas
            var est = _dao.ObtenerEstadisticas(idUsuario);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);
            ViewBag.TieneOrdenBorrador = ordenes.Any(o => string.Equals((o.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase));

            return View(ordenes);
        }

        // GET: /OrdenRecaudacion/Nueva
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult Nueva()
        {
            var model = new CapaPresentacion.Models.OrdenRecaudacionNuevaVM();
            CargarConceptosNueva(model);
            return View(model);
        }

        /// <summary>
        /// Crear nueva orden - refactorizado para usar orquestador
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador")]
        public async Task<ActionResult> Nueva(CrearOrdenViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await CargarViewBagsParaNueva();
                return View(model);
            }

            try
            {
                var request = new CrearOrdenRequest
                {
                    SolicitudId = model.SolicitudId,
                    ConceptoId = model.ConceptoId,
                    ContribuyenteId = model.ContribuyenteId,
                    Subtotal = model.Subtotal,
                    Iva = model.Iva,
                    Total = model.Total,
                    Observaciones = model.Observaciones,
                    UsuarioCreacion = User.Identity.Name
                };

                // Usar orquestador para crear orden
                var resultado = await _orchestrator.CrearOrdenAsync(request);

                if (resultado.Success)
                {
                    TempData["SuccessMessage"] = "Orden " + resultado.Data.NumeroOrden + " creada exitosamente.";
                    return RedirectToAction("Detalles", new { id = resultado.Data.OrdenId });
                }

                ModelState.AddModelError("", resultado.Message);
                await CargarViewBagsParaNueva();
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al crear orden: " + ex.Message);
                ModelState.AddModelError("", "Error interno al crear la orden.");
                await CargarViewBagsParaNueva();
                return View(model);
            }
        }

        // GET: /OrdenRecaudacion/Detalles/5
        public ActionResult Detalles(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            try
            {
                ViewBag.Pagos = _dao.ObtenerPagosPorOrden(id);
            }
            catch
            {
                ViewBag.Pagos = null;
            }

            return View(orden);
        }

        // GET: /OrdenRecaudacion/Editar/5
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult Editar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(403);

            return View(orden);
        }

        // POST: /OrdenRecaudacion/Editar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(OrdenRecaudacionModel model)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(model);

            var ordenExistente = _dao.ObtenerOrdenPorId(model.Id);
            if (ordenExistente == null || ordenExistente.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((ordenExistente.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(403);

            try
            {
                // Actualizar los campos editables
                ordenExistente.LugarEmision = model.LugarEmision;
                ordenExistente.Compania = model.Compania;
                ordenExistente.RucCedula = model.RucCedula;
                ordenExistente.NombreContribuyente = model.NombreContribuyente;
                ordenExistente.Correo = model.Correo;
                ordenExistente.Telefono = model.Telefono;
                ordenExistente.Observacion = model.Observacion;

                bool result = _dao.ActualizarOrden(ordenExistente);
                if (result)
                {
                    TempData["OK"] = "Orden actualizada correctamente";
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Error al actualizar la orden");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error interno: " + ex.Message);
                return View(model);
            }
        }

        // POST: /OrdenRecaudacion/Anular/5
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public JsonResult Anular(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return Json(new { success = false, message = "Usuario no autenticado" });

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return Json(new { success = false, message = "Orden no encontrada" });

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "La orden ya está anulada" });

            try
            {
                bool result = _dao.CambiarEstadoOrden(id, "ANULADA");
                return Json(new { success = result, message = result ? "Orden anulada correctamente" : "Error al anular la orden" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno: " + ex.Message });
            }
        }

        // POST: /OrdenRecaudacion/Generar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Generar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden generar órdenes en estado BORRADOR";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (orden.Total <= 0)
            {
                TempData["Error"] = "No se puede generar una orden sin conceptos";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                string err;
                var result = _dao.CambiarEstadoOrden(id, "PENDIENTE", out err);
                if (!result)
                {
                    // Fallback legacy
                    result = _dao.CambiarEstadoOrden(id, "GENERADA", out err);
                }

                if (result)
                {
                    TempData["OK"] = "Orden generada correctamente (pendiente de pago).";
                    return RedirectToAction("Detalles", new { id = id });
                }

                TempData["Error"] = "No se pudo cambiar el estado de la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                return RedirectToAction("Detalles", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // POST: /OrdenRecaudacion/Enviar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Enviar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "GENERADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden enviar órdenes en estado GENERADA";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                bool result = _dao.CambiarEstadoOrden(id, "ENVIADA");
                if (result)
                {
                    TempData["OK"] = "Orden enviada correctamente al contribuyente";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    TempData["Error"] = "Error al enviar la orden";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // POST: /OrdenRecaudacion/RegistrarPago/5
        [HttpGet]
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult RegistrarPago(int id)
        {
            if (id <= 0)
                return RedirectToAction("Index");

            TempData["Error"] = "Debe registrar el pago desde el detalle de la orden.";
            return RedirectToAction("Detalles", new { id = id });
        }

        private void CargarConceptosNueva(CapaPresentacion.Models.OrdenRecaudacionNuevaVM model)
        {
            if (model == null) return;
            AsegurarConceptosBasicos();

            var conceptos = _conceptoDao.ObtenerConceptos(true) ?? new List<ConceptoModel>();
            model.Conceptos = conceptos.Select(c => new CapaPresentacion.Models.ConceptoOptionVM
            {
                Id = c.Id,
                Codigo = c.Codigo,
                Nombre = c.Nombre,
                Valor = c.ValorBase,
                PorcentajeAdmin = c.PorcentajeAdmin,
                Label = string.Format("{0} - {1} (${2})", c.Codigo, c.Nombre, c.ValorBase.ToString("0.00"))
            }).ToList();

            try
            {
                var userId = GetUserId();
                var solicitudes = (User != null && User.IsInRole("Administrador"))
                    ? _solicitudDao.ObtenerTodos()
                    : _solicitudDao.ObtenerPorUsuario(userId);

                model.Solicitudes = (solicitudes ?? new List<CapaModelo.SolicitudAOCR>())
                    .Select(s => new CapaPresentacion.Models.OrdenRecaudacionNuevaVM.SolicitudOptionVM
                    {
                        Id = s.CodigoSolicitud,
                        Numero = s.NumeroSolicitud,
                        Nombre = s.NombreOperador,
                        Label = string.Format("{0} - {1}", s.NumeroSolicitud, s.NombreOperador),
                        Ruc = s.Ruc,
                        Correo = s.Email,
                        Telefono = s.Telefono,
                        Compania = string.IsNullOrWhiteSpace(s.RazonSocial) ? s.NombreOperador : s.RazonSocial
                    }).ToList();
            }
            catch
            {
                model.Solicitudes = new List<CapaPresentacion.Models.OrdenRecaudacionNuevaVM.SolicitudOptionVM>();
            }
        }

        private void AsegurarConceptosBasicos()
        {
            var conceptos = new List<ConceptoModel>
            {
                new ConceptoModel { Codigo = "EMI_AOCR", Nombre = "Emisión AOCR", TipoCalculo = "FIJO", ValorBase = 3300m, PorcentajeAdmin = 0m, Activo = true, Orden = 1, Descripcion = "Emisión AOCR", PorEstacion = false, PorDia = false, EsViatico = false },
                new ConceptoModel { Codigo = "REN_AOCR", Nombre = "Renovación AOCR", TipoCalculo = "FIJO", ValorBase = 3300m, PorcentajeAdmin = 0m, Activo = true, Orden = 2, Descripcion = "Renovación AOCR", PorEstacion = false, PorDia = false, EsViatico = false },
                new ConceptoModel { Codigo = "MOD_AOCR_INC", Nombre = "Modificación AOCR (Inclusión aeronaves distinto modelo y tipo)", TipoCalculo = "FIJO", ValorBase = 1600m, PorcentajeAdmin = 0m, Activo = true, Orden = 3, Descripcion = "Modificación AOCR (Inclusión aeronaves distinto modelo y tipo)", PorEstacion = false, PorDia = false, EsViatico = false },
                new ConceptoModel { Codigo = "MOD_AOCR_SIN_INC", Nombre = "Modificación AOCR (Que no implique incremento de aeronaves)", TipoCalculo = "FIJO", ValorBase = 80m, PorcentajeAdmin = 0m, Activo = true, Orden = 4, Descripcion = "Modificación AOCR (Que no implique incremento de aeronaves)", PorEstacion = false, PorDia = false, EsViatico = false },
                new ConceptoModel { Codigo = "INSPECCION_EXT", Nombre = "Inspección requerida por el Operador Aéreo Extranjero", TipoCalculo = "POR_ESTACION", ValorBase = 500m, PorcentajeAdmin = 0m, Activo = true, Orden = 5, Descripcion = "Inspección requerida por el Operador Aéreo Extranjero (por estación)", PorEstacion = true, PorDia = false, EsViatico = false },
                new ConceptoModel { Codigo = "VIATICOS_INSPECTOR", Nombre = "Viáticos a Sres. Inspectores", TipoCalculo = "POR_DIA", ValorBase = 80m, PorcentajeAdmin = 8m, Activo = true, Orden = 6, Descripcion = "Viáticos por día (más 8% de gastos administrativos)", PorEstacion = false, PorDia = true, EsViatico = true }
            };

            foreach (var c in conceptos)
            {
                _conceptoDao.Upsert(c);
            }
        }

        private class DetalleInput
        {
            public int ConceptoId { get; set; }
            public decimal Cantidad { get; set; }
        }

        private List<DetalleInput> ParseDetalles(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<DetalleInput>();

            try
            {
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<List<DetalleInput>>(json) ?? new List<DetalleInput>();
            }
            catch
            {
                return new List<DetalleInput>();
            }
        }

        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarPago(int id, string Monto, string NumeroFactura, string MetodoPago, HttpPostedFileBase ComprobanteArchivo, string Observaciones)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            var estadoOrden = (orden.Estado ?? "").Trim();
            if (!estadoOrden.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) &&
                !estadoOrden.Equals("GENERADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se puede subir comprobante cuando la orden está en GENERADA o PENDIENTE.";
                return RedirectToAction("Detalles", new { id = id });
            }

            decimal montoValue;
            var montoRaw = (Monto ?? Request["Monto"] ?? "").Trim();
            if (!decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out montoValue) &&
                !decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out montoValue))
            {
                TempData["Error"] = "Monto inválido";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (montoValue <= 0)
            {
                TempData["Error"] = "El monto debe ser mayor a cero";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(NumeroFactura))
            {
                NumeroFactura = null; // referencia opcional
            }

            if (string.IsNullOrWhiteSpace(MetodoPago))
            {
                TempData["Error"] = "Debe seleccionar un método de pago";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // Guardar comprobante si existe
                string comprobanteRuta = null;
                if (ComprobanteArchivo != null && ComprobanteArchivo.ContentLength > 0)
                {
                    var ext = Path.GetExtension(ComprobanteArchivo.FileName) ?? "";
                    ext = ext.ToLowerInvariant();
                    if (ext != ".pdf" && ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                    {
                        TempData["Error"] = "Formato de comprobante no permitido (PDF, JPG, PNG).";
                        return RedirectToAction("Detalles", new { id = id });
                    }

                    if (ComprobanteArchivo.ContentLength > (10 * 1024 * 1024))
                    {
                        TempData["Error"] = "El comprobante supera el tamaño máximo permitido (10MB).";
                        return RedirectToAction("Detalles", new { id = id });
                    }

                    var folderVirtual = "~/Content/documents/pagos";
                    var folderFisico = Server.MapPath(folderVirtual);
                    if (!Directory.Exists(folderFisico))
                        Directory.CreateDirectory(folderFisico);

                    var safeFile = $"pago_{id}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                    var fullPath = Path.Combine(folderFisico, safeFile);
                    ComprobanteArchivo.SaveAs(fullPath);
                    comprobanteRuta = VirtualPathUtility.ToAbsolute($"{folderVirtual}/{safeFile}");
                }

                var pago = new PagoModel
                {
                    NumeroFactura = NumeroFactura,
                    Monto = montoValue,
                    Moneda = "USD",
                    MetodoPago = MetodoPago,
                    // ✅ Debe coincidir con chk_estado_pago (case-sensitive)
                    Estado = "Pendiente",
                    FechaPago = DateTime.Now,
                    Observaciones = Observaciones,
                    ComprobanteRuta = comprobanteRuta
                };

            int codigoSolicitud;
            if (!int.TryParse(orden.CodigoSolicitud ?? "", out codigoSolicitud))
            {
                codigoSolicitud = 0;
            }

            if (codigoSolicitud <= 0 && !string.IsNullOrWhiteSpace(orden.CodigoSolicitud))
            {
                codigoSolicitud = _dao.ObtenerCodigoSolicitudPorNumero(orden.CodigoSolicitud);
            }

            if (codigoSolicitud <= 0 && _dao.ExisteSolicitud(orden.Id))
            {
                codigoSolicitud = orden.Id;
            }

            if (codigoSolicitud <= 0)
            {
                codigoSolicitud = _dao.ObtenerCodigoSolicitudPorRuc(orden.RucCedula);
                if (codigoSolicitud > 0)
                {
                    _dao.ActualizarCodigoSolicitudOrden(orden.Id, codigoSolicitud);
                }
            }

            if (codigoSolicitud <= 0 || !_dao.ExisteSolicitud(codigoSolicitud))
            {
                TempData["Error"] = "La orden no está vinculada a una solicitud válida para registrar el pago.";
                return RedirectToAction("Detalles", new { id = id });
            }

                string pagoErr;
                bool pagoOk = _dao.RegistrarPago(codigoSolicitud, pago, out pagoErr);
                if (!pagoOk)
                {
                    TempData["Error"] = "No se pudo registrar el pago en la base de datos. " + (string.IsNullOrWhiteSpace(pagoErr) ? "" : ("Detalle: " + pagoErr));
                    return RedirectToAction("Detalles", new { id = id });
                }

                // Cambiar estado de la orden a EN_REVISION_FINANCIERA
                bool result = _dao.CambiarEstadoOrden(id, "PROCESADA");
                if (result)
                {
                    try
                    {
                        var financieroEmail = ConfigurationManager.AppSettings["FinancieroEmail"];
                        if (!string.IsNullOrWhiteSpace(financieroEmail))
                        {
                            var emailSvc = new EmailService();
                            string comprobanteFisico = null;
                            if (!string.IsNullOrWhiteSpace(comprobanteRuta))
                            {
                                comprobanteFisico = Server.MapPath(comprobanteRuta);
                            }

                            emailSvc.EnviarNotificacionFinanciero(orden, pago, financieroEmail, comprobanteFisico);
                        }
                    }
                    catch
                    {
                        // No bloquear el flujo si el email falla
                    }

                    TempData["OK"] = "Comprobante enviado. La orden está en revisión financiera.";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    TempData["Error"] = "Error al registrar el pago";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // POST: /OrdenRecaudacion/Anular/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Anular(int id, string motivo)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            var estadoOrden = (orden.Estado ?? "").Trim();
            if (estadoOrden.Equals("FACTURADA", StringComparison.OrdinalIgnoreCase) ||
                estadoOrden.Equals("COMPLETADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "No se pueden anular órdenes aprobadas o facturadas.";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La orden ya está anulada";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe proporcionar un motivo para la anulación";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // TODO: Aquí se debería guardar el motivo de la anulación en la base de datos
                bool result = _dao.CambiarEstadoOrden(id, "ANULADA");
                if (result)
                {
                    TempData["OK"] = "Orden anulada correctamente";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    TempData["Error"] = "Error al anular la orden";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        /// <summary>
        /// Descargar PDF de orden - refactorizado
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> DescargarPdf(int id)
        {
            try
            {
                var request = new GenerarPdfRequest
                {
                    OrdenId = id,
                    TipoDocumento = "ORDEN",
                    IncluirDetalles = true
                };

                var resultado = await _orchestrator.GenerarPdfAsync(request);

                if (resultado.Success)
                {
                    return File(resultado.Data.ContenidoPdf, resultado.Data.ContentType, resultado.Data.NombreArchivo);
                }

                TempData["ErrorMessage"] = resultado.Message;
                return RedirectToAction("Detalles", new { id });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al generar PDF: " + ex.Message);
                TempData["ErrorMessage"] = "Error al generar el PDF.";
                return RedirectToAction("Detalles", new { id });
            }
        }

        private int GetUserId()
        {
            int id = 0;
            var v = Session["UserId"] ?? Session["IdUsuario"];
            if (v != null)
            {
                int.TryParse(v.ToString(), out id);
                System.Diagnostics.Debug.WriteLine($"GetUserId: Encontrado ID de usuario = {id} desde Session['{(Session["UserId"] != null ? "UserId" : "IdUsuario")}']");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GetUserId: No se encontró ID de usuario en la sesión");
            }
            return id;
        }

        private void CargarEstadosCombo(string estadoSeleccionado)
        {
            var selected = (estadoSeleccionado ?? "").Trim().ToUpperInvariant();

            var items = new List<SelectListItem>
            {
                new SelectListItem { Text = "TODAS", Value = "" },
                new SelectListItem { Text = "BORRADOR", Value = "BORRADOR" },
                new SelectListItem { Text = "PENDIENTE", Value = "PENDIENTE" },
                new SelectListItem { Text = "PROCESADA", Value = "PROCESADA" },
                new SelectListItem { Text = "FACTURADA", Value = "FACTURADA" },
                new SelectListItem { Text = "COMPLETADA", Value = "COMPLETADA" },
                new SelectListItem { Text = "ANULADA", Value = "ANULADA" }
            };

            foreach (var it in items)
                it.Selected = (!string.IsNullOrEmpty(selected) && it.Value == selected) ||
                              (string.IsNullOrEmpty(selected) && it.Value == "");

            ViewBag.Estados = items; // ✅ IEnumerable<SelectListItem> real
        }

        private Dictionary<string, object> MapearEstadisticasParaVista(Dictionary<string, object> d)
        {
            int total = GetInt(d, "total");
            int pagadas = GetInt(d, "pagada");
            decimal montoTotal = GetDec(d, "monto_total");
            decimal montoRecaudado = GetDec(d, "monto_recaudado");

            decimal saldoPendiente = montoTotal - montoRecaudado;
            if (saldoPendiente < 0) saldoPendiente = 0;

            return new Dictionary<string, object>
            {
                ["Total"] = total,
                ["Pagadas"] = pagadas,
                ["SaldoPendiente"] = saldoPendiente,
                ["MontoPagado"] = montoRecaudado
            };
        }

        private int GetInt(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0;
            int x; return int.TryParse(d[key].ToString(), out x) ? x : 0;
        }

        private decimal GetDec(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0m;
            decimal x; return decimal.TryParse(d[key].ToString(), out x) ? x : 0m;
        }

        private async Task CargarViewBagsParaNueva()
        {
            ViewBag.Conceptos = await _conceptoRepository.ObtenerActivosAsync();
            ViewBag.Contribuyentes = await _contribuyenteRepository.ObtenerTodosAsync();
        }
    }
}
