using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Configuration;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Models;
using CapaNegocio;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;
using System.Threading.Tasks;
using CapaPresentacion.Models;
using CapaModelo;
using CapaNegocio.Services;
// Alias para evitar ambig�edad
using EmailSvc = CapaDatos.Services.EmailService;
using SecureConfig = CapaDatos.Services.SecureConfigurationService;
using DetalleOrden = CapaDatos.Entidades.DetalleOrden;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private OrdenRecaudacionDAO _ordenDAO;
        private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();
        private readonly OrdenRecaudacionBL _bl = new OrdenRecaudacionBL();
        private readonly ConceptoDAO _conceptoDao = new ConceptoDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly IOrdenRecaudacionOrchestrator _orchestrator;

        public OrdenRecaudacionController(IOrdenRecaudacionOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public OrdenRecaudacionController()
        {
            try
            {
                _ordenDAO = new OrdenRecaudacionDAO();
                System.Diagnostics.Debug.WriteLine("OrdenRecaudacionController inicializado correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR en constructor OrdenRecaudacionController: " + ex.Message);
                _ordenDAO = null;
            }

            // Inicializar orquestador con dependencias m�nimas para evitar NRE en Nueva()
            _orchestrator = new OrdenRecaudacionOrchestrator(
                new OrdenRecaudacionDAO(),
                new PagoDAO(),
                null,
                null,
                new CapaNegocio.Services.EmailService(),
                null
            );
        }

        // ? Para confirmar conexi�n real a DB (�til en producci�n)
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

            var ordenes = _dao.ListarPorUsuarioModel(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();

            // Estad�sticas: tu view espera claves con may�scula
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

            var ordenes = _dao.ListarPorUsuario(idUsuario, null) ?? new List<OrdenRecaudacion>();
            System.Diagnostics.Debug.WriteLine(string.Format("Obligatoria: Se encontraron {0} �rdenes", ordenes.Count));

            // Estad�sticas
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
        /// Crear nueva orden - acepta OrdenRecaudacionNuevaVM desde la vista
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult Nueva(OrdenRecaudacionNuevaVM model)
        {
            try
            {
                // Parsear detalles del JSON
                var detalles = new List<DetalleOrdenRequest>();
                if (!string.IsNullOrWhiteSpace(model.DetallesJson))
                {
                    var serializer = new JavaScriptSerializer();
                    var detallesRaw = serializer.Deserialize<List<Dictionary<string, object>>>(model.DetallesJson);
                    if (detallesRaw != null)
                    {
                        foreach (var d in detallesRaw)
                        {
                            var conceptoId = d.ContainsKey("ConceptoId") ? Convert.ToInt32(d["ConceptoId"]) : 0;
                            var cantidad = d.ContainsKey("Cantidad") ? Convert.ToInt32(d["Cantidad"]) : 1;

                            // Obtener precio del concepto
                            var concepto = _conceptoDao.ObtenerPorId(conceptoId);
                            var precioUnitario = concepto?.ValorBase ?? 0m;

                            detalles.Add(new DetalleOrdenRequest
                            {
                                ConceptoId = conceptoId,
                                Cantidad = cantidad,
                                PrecioUnitario = precioUnitario,
                                Subtotal = cantidad * precioUnitario
                            });
                        }
                    }
                }

                if (detalles.Count == 0)
                {
                    ModelState.AddModelError("", "Debe agregar al menos un concepto a la orden.");
                    CargarConceptosNueva(model);
                    return View(model);
                }

                // Calcular totales
                decimal subtotal = 0m, admin = 0m;
                foreach (var det in detalles)
                {
                    var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
                    var porcentajeAdmin = concepto?.PorcentajeAdmin ?? 0m;
                    subtotal += det.Subtotal;
                    admin += det.Subtotal * (porcentajeAdmin / 100m);
                }
                var total = subtotal + admin;

                // Crear la entidad OrdenRecaudacion
                var idUsuario = GetUserId();
                if (idUsuario <= 0)
                {
                    ModelState.AddModelError("", "Usuario no autenticado.");
                    CargarConceptosNueva(model);
                    return View(model);
                }

                var numeroOrden = GenerarNumeroOrden();

                var orden = new OrdenRecaudacion
                {
                    NumeroOrden = numeroOrden,
                    CodigoUsuario = idUsuario.ToString(),
                    CodigoSolicitud = model.Orden?.CodigoSolicitud?.ToString(),
                    LugarEmision = model.Orden?.LugarEmision ?? "Quito",
                    Compania = model.Orden?.Compania,
                    NombreContribuyente = model.Orden?.Compania,
                    RucCedula = model.Orden?.RucCedula,
                    RucContribuyente = model.Orden?.RucCedula,
                    Correo = model.Orden?.Correo,
                    EmailContribuyente = model.Orden?.Correo,
                    Telefono = model.Orden?.Telefono,
                    Observacion = model.Orden?.Observacion,
                    Observaciones = model.Orden?.Observacion,
                    Subtotal = subtotal,
                    Admin = admin,
                    Total = total,
                    Estado = "BORRADOR",
                    FechaCreacion = DateTime.Now,
                    UsuarioCreacion = User.Identity.Name,
                    Activo = true
                };

                // Insertar orden
                var ordenId = _dao.Insertar(orden);

                if (ordenId > 0)
                {
                    // Insertar detalles
                    foreach (var det in detalles)
                    {
                        var detalle = new DetalleOrden
                        {
                            OrdenId = ordenId,
                            ConceptoId = det.ConceptoId,
                            Cantidad = det.Cantidad,
                            PrecioUnitario = det.PrecioUnitario,
                            Subtotal = det.Subtotal
                        };
                        _dao.CrearDetalleAsync(detalle).Wait();
                    }

                    TempData["OK"] = "Orden " + numeroOrden + " creada exitosamente.";
                    return RedirectToAction("Detalles", new { id = ordenId });
                }

                ModelState.AddModelError("", "Error al guardar la orden en la base de datos.");
                CargarConceptosNueva(model);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al crear orden: " + ex.ToString());
                ModelState.AddModelError("", "Error interno al crear la orden: " + ex.Message);
                CargarConceptosNueva(model);
                return View(model);
            }
        }

        private string GenerarNumeroOrden()
        {
            var fecha = DateTime.Now;
            var consecutivo = _dao.ObtenerConsecutivoDiarioAsync(fecha).Result + 1;
            return string.Format("OR-{0:yyyyMMdd}-{1:D4}", fecha, consecutivo);
        }

        // GET: /OrdenRecaudacion/Detalles/5
        public ActionResult Detalles(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorIdModel(id);
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

            var orden = _dao.ObtenerOrdenPorIdModel(id);
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

            var ordenExistente = _dao.ObtenerOrdenPorIdModel(model.Id);
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

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return Json(new { success = false, message = "Orden no encontrada" });

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "La orden ya est� anulada" });

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

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden generar �rdenes en estado BORRADOR";
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

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "GENERADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden enviar �rdenes en estado GENERADA";
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

            try
            {
                AsegurarConceptosBasicos();

                var conceptos = _conceptoDao.ObtenerConceptos(true);
                model.Conceptos = conceptos.Select(c => new CapaPresentacion.Models.ConceptoOptionVM
                {
                    Id = c.Id,
                    Codigo = c.Codigo,
                    Nombre = c.Nombre,
                    Valor = c.ValorBase,
                    PorcentajeAdmin = c.PorcentajeAdmin,
                    Label = string.Format("{0} - {1} (${2})", c.Codigo, c.Nombre, c.ValorBase.ToString("0.00"))
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CargarConceptosNueva: Error cargando conceptos - " + ex.Message);
                model.Conceptos = new List<CapaPresentacion.Models.ConceptoOptionVM>();
                ModelState.AddModelError("", "No se pudieron cargar los conceptos. Verifique la conexi�n a la base de datos.");
            }

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CargarConceptosNueva: Error cargando solicitudes - " + ex.Message);
                model.Solicitudes = new List<CapaPresentacion.Models.OrdenRecaudacionNuevaVM.SolicitudOptionVM>();
            }
        }

        private void AsegurarConceptosBasicos()
        {
            var conceptos = new List<CapaDatos.Models.ConceptoModel>
            {
                new CapaDatos.Models.ConceptoModel { Codigo = "EMI_AOCR", Nombre = "Emisi�n AOCR", TipoCalculo = "FIJO", ValorBase = 3300m, PorcentajeAdmin = 0m, Activo = true, Orden = 1, Descripcion = "Emisi�n AOCR", PorEstacion = false, PorDia = false, EsViatico = false },
                new CapaDatos.Models.ConceptoModel { Codigo = "REN_AOCR", Nombre = "Renovaci�n AOCR", TipoCalculo = "FIJO", ValorBase = 3300m, PorcentajeAdmin = 0m, Activo = true, Orden = 2, Descripcion = "Renovaci�n AOCR", PorEstacion = false, PorDia = false, EsViatico = false },
                new CapaDatos.Models.ConceptoModel { Codigo = "MOD_AOCR_INC", Nombre = "Modificaci�n AOCR (Inclusi�n aeronaves distinto modelo y tipo)", TipoCalculo = "FIJO", ValorBase = 1600m, PorcentajeAdmin = 0m, Activo = true, Orden = 3, Descripcion = "Modificaci�n AOCR (Inclusi�n aeronaves distinto modelo y tipo)", PorEstacion = false, PorDia = false, EsViatico = false },
                new CapaDatos.Models.ConceptoModel { Codigo = "MOD_AOCR_SIN_INC", Nombre = "Modificaci�n AOCR (Que no implique incremento de aeronaves)", TipoCalculo = "FIJO", ValorBase = 80m, PorcentajeAdmin = 0m, Activo = true, Orden = 4, Descripcion = "Modificaci�n AOCR (Que no implique incremento de aeronaves)", PorEstacion = false, PorDia = false, EsViatico = false },
                new CapaDatos.Models.ConceptoModel { Codigo = "INSPECCION_EXT", Nombre = "Inspecci�n requerida por el Operador A�reo Extranjero", TipoCalculo = "POR_ESTACION", ValorBase = 500m, PorcentajeAdmin = 0m, Activo = true, Orden = 5, Descripcion = "Inspecci�n requerida por el Operador A�reo Extranjero (por estaci�n)", PorEstacion = true, PorDia = false, EsViatico = false },
                new CapaDatos.Models.ConceptoModel { Codigo = "VIATICOS_INSPECTOR", Nombre = "Vi�ticos a Sres. Inspectores", TipoCalculo = "POR_DIA", ValorBase = 80m, PorcentajeAdmin = 8m, Activo = true, Orden = 6, Descripcion = "Vi�ticos por d�a (m�s 8% de gastos administrativos)", PorEstacion = false, PorDia = true, EsViatico = true }
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

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            var estadoOrden = (orden.Estado ?? "").Trim();
            if (!estadoOrden.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) &&
                !estadoOrden.Equals("GENERADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se puede subir comprobante cuando la orden est� en GENERADA o PENDIENTE.";
                return RedirectToAction("Detalles", new { id = id });
            }

            decimal montoValue;
            var montoRaw = (Monto ?? Request["Monto"] ?? "").Trim();
            if (!decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out montoValue) &&
                !decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out montoValue))
            {
                TempData["Error"] = "Monto inv�lido";
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
                TempData["Error"] = "Debe seleccionar un m�todo de pago";
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
                        TempData["Error"] = "El comprobante supera el tama�o m�ximo permitido (10MB).";
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

                var pago = new CapaModelo.PagoModel
                {
                    NumeroFactura = NumeroFactura,
                    Monto = montoValue,
                    Moneda = "USD",
                    MetodoPago = MetodoPago,
                    // ? Debe coincidir con chk_estado_pago (case-sensitive)
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
                TempData["Error"] = "La orden no est� vinculada a una solicitud v�lida para registrar el pago.";
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
                            EnviarNotificacionAFinanciero(orden, pago, financieroEmail, comprobanteRuta);
                        }
                    }
                    catch
                    {
                        // No bloquear el flujo si el email falla
                    }

                    TempData["OK"] = "Comprobante enviado. La orden est� en revisi�n financiera.";
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

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            var estadoOrden = (orden.Estado ?? "").Trim();
            if (estadoOrden.Equals("FACTURADA", StringComparison.OrdinalIgnoreCase) ||
                estadoOrden.Equals("COMPLETADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "No se pueden anular �rdenes aprobadas o facturadas.";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La orden ya est� anulada";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe proporcionar un motivo para la anulaci�n";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // TODO: Aqu� se deber�a guardar el motivo de la anulaci�n en la base de datos
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
                System.Diagnostics.Debug.WriteLine("GetUserId: No se encontr� ID de usuario en la sesi�n");
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

            ViewBag.Estados = items; // ? IEnumerable<SelectListItem> real
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
            try
            {
            ViewBag.Conceptos = _conceptoDao.ObtenerConceptos(true);
            }
            catch
            {
                ViewBag.Conceptos = new List<CapaModelo.ConceptoModel>();
            }
            
            ViewBag.Contribuyentes = new List<object>();
        }

        // M�todo helper con tipo correcto:
        private void EnviarNotificacionAFinanciero(OrdenRecaudacionModel orden, CapaModelo.PagoModel pago, string emailFinanciero, string comprobanteRuta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(emailFinanciero)) return;

                var config = new SecureConfig();
                var emailSvc = new EmailSvc(config);

                var asunto = string.Format("Nueva Orden de Pago - {0}", orden.NumeroOrden);
                var cuerpo = string.Format(@"
                    <h2>Nueva Orden Pendiente de Revisi�n</h2>
                    <p><strong>N�mero de Orden:</strong> {0}</p>
                    <p><strong>Contribuyente:</strong> {1}</p>
                    <p><strong>Monto:</strong> ${2:N2}</p>
                    <p><strong>M�todo de Pago:</strong> {3}</p>",
                    orden.NumeroOrden,
                    orden.NombreContribuyente,
                    pago.Monto,
                    pago.MetodoPago);

                byte[] adjunto = null;
                string nombreAdjunto = null;
                if (!string.IsNullOrWhiteSpace(comprobanteRuta))
                {
                    var rutaFisica = Server.MapPath(comprobanteRuta);
                    if (System.IO.File.Exists(rutaFisica))
                    {
                        adjunto = System.IO.File.ReadAllBytes(rutaFisica);
                        nombreAdjunto = Path.GetFileName(rutaFisica);
                    }
                }

                emailSvc.EnviarAsync(emailFinanciero, "Financiero", asunto, cuerpo, adjunto, nombreAdjunto).Wait();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error enviando email: " + ex.Message);
            }
        }
    }
}







