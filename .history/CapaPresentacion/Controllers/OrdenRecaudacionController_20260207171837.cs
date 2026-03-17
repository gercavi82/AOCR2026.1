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
using Rotativa;
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
        private readonly BancoP9DAO _bancoDao = new BancoP9DAO();
        private readonly ParametroDAO _parametroDao = new ParametroDAO();
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
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_nueva.txt");
            try
            {
                System.IO.File.AppendAllText(logPath, "\n=== Nueva() INICIADO " + DateTime.Now + " ===\n");
                var model = new CapaPresentacion.Models.OrdenRecaudacionNuevaVM();
                System.IO.File.AppendAllText(logPath, "Modelo creado, llamando a CargarConceptosNueva...\n");
                CargarConceptosNueva(model);
                System.IO.File.AppendAllText(logPath, "CargarConceptosNueva completado, retornando vista\n");
                return View(model);
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, "*** ERROR: " + ex.ToString() + "\n\n");
                ViewBag.ErrorMessage = ex.Message;
                ViewBag.ErrorStack = ex.StackTrace;
                throw;
            }
        }

        /// <summary>
        /// Crear nueva orden - acepta OrdenRecaudacionNuevaVM desde la vista
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador")]
        public async Task<ActionResult> Nueva(OrdenRecaudacionNuevaVM model)
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

                System.Diagnostics.Debug.WriteLine($"Controller Nueva: idUsuario = {idUsuario}");

                var numeroOrden = await GenerarNumeroOrdenAsync();
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: numeroOrden generado = {numeroOrden}");

                var orden = new OrdenRecaudacion
                {
                    NumeroOrden = numeroOrden,
                    CodigoUsuario = idUsuario,
                    CodigoSolicitud = int.TryParse(model.Orden?.CodigoSolicitud?.ToString(), out int cs) ? (int?)cs : null,
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
                    // NO asignar UsuarioCreacion porque sobrescribe CodigoUsuario
                    // UsuarioCreacion = User.Identity.Name,
                    Activo = true
                };

                System.Diagnostics.Debug.WriteLine($"Controller Nueva: Orden creada con CodigoUsuario = '{orden.CodigoUsuario}'");

                // Insertar orden
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: Antes de insertar orden con numero = {orden.NumeroOrden}");
                var ordenId = await _dao.InsertarAsync(orden);
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: Después de insertar, ordenId = {ordenId}");

                if (ordenId > 0)
                {
                    // Insertar detalles
                    foreach (var det in detalles)
                    {
                        // Obtener el concepto para tener el porcentaje de administración
                        var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
                        var porcentajeAdmin = concepto?.PorcentajeAdmin ?? 0m;
                        var adminLinea = det.Subtotal * (porcentajeAdmin / 100m);
                        var totalLinea = det.Subtotal + adminLinea;

                        var detalle = new DetalleOrden
                        {
                            OrdenId = ordenId,
                            ConceptoId = det.ConceptoId,
                            ConceptoCodigo = concepto?.Codigo,
                            ConceptoNombre = concepto?.Nombre,
                            Cantidad = det.Cantidad,
                            ValorUnitario = det.PrecioUnitario,
                            PorcentajeAdmin = porcentajeAdmin,
                            Subtotal = det.Subtotal,
                            Admin = adminLinea,
                            TotalLinea = totalLinea
                        };
                        await _dao.CrearDetalleAsync(detalle);
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

        private async Task<string> GenerarNumeroOrdenAsync()
        {
            var fecha = DateTime.Now;
            // Generar número único con timestamp de microsegundos para evitar duplicados
            var timestamp = fecha.ToString("yyyyMMddHHmmssfff"); // Agregamos milisegundos (fff)
            var consecutivo = await _dao.ObtenerConsecutivoDiarioAsync(fecha) + 1;
            var numeroOrden = string.Format("OR-{0}-{1}", timestamp, consecutivo);
            
            System.Diagnostics.Debug.WriteLine($"GenerarNumeroOrdenAsync: timestamp={timestamp}, consecutivo={consecutivo}, resultado={numeroOrden}");
            
            // Verificar que no exista ya este número (medida de seguridad adicional)
            int intentos = 0;
            var numeroFinal = numeroOrden;
            while (intentos < 10) // máximo 10 intentos
            {
                if (!_dao.ExisteNumeroOrden(numeroFinal))
                {
                    break;
                }
                
                // Si existe, agregar un sufijo adicional
                intentos++;
                numeroFinal = string.Format("OR-{0}-{1}-{2}", timestamp, consecutivo, intentos);
                System.Diagnostics.Debug.WriteLine($"GenerarNumeroOrdenAsync: Número duplicado, intentando={numeroFinal}");
            }
            
            return numeroFinal;
        }

        // GET: /OrdenRecaudacion/Detalles/5
        public async Task<ActionResult> Detalles(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            System.Diagnostics.Debug.WriteLine($"Controller Detalles: ordenId = {id}, numeroOrden = {orden.NumeroOrden}");

            try
            {
                ViewBag.Pagos = await _dao.ObtenerPagosPorOrdenAsync(id);
            }
            catch
            {
                ViewBag.Pagos = null;
            }

            // Cargar lista de bancos desde P9
            ViewBag.ListaBancoPago = ToSelectList("OPCBAN");
            
            // Cargar métodos de pago desde P9
            ViewBag.ListaMetodoPago = ToSelectList("SOLFOR");

            return View(orden);
        }

        // GET: /OrdenRecaudacion/Editar/5
        [Authorize(Roles = "Solicitante,Administrador")]
        public async Task<ActionResult> Editar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
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
        public async Task<ActionResult> Generar(int id)
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
                var result = await _dao.CambiarEstadoOrdenAsync(id, "PENDIENTE");
                if (!result)
                {
                    // Fallback legacy
                    result = await _dao.CambiarEstadoOrdenAsync(id, "GENERADA");
                }

                if (result)
                {
                    TempData["OK"] = "Orden generada correctamente (pendiente de pago).";
                    return RedirectToAction("Detalles", new { id = id });
                }

                TempData["Error"] = "No se pudo cambiar el estado de la orden.";
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
                System.Diagnostics.Debug.WriteLine("CargarConceptosNueva: Llamando AsegurarConceptosBasicos...");
                AsegurarConceptosBasicos();

                System.Diagnostics.Debug.WriteLine("CargarConceptosNueva: Obteniendo conceptos...");
                var conceptos = _conceptoDao.ObtenerConceptos(true);
                System.Diagnostics.Debug.WriteLine($"CargarConceptosNueva: {conceptos.Count} conceptos obtenidos");
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
                System.Diagnostics.Debug.WriteLine("*** ERROR en CargarConceptosNueva - Conceptos: " + ex.ToString());
                model.Conceptos = new List<CapaPresentacion.Models.ConceptoOptionVM>();
                ModelState.AddModelError("", "No se pudieron cargar los conceptos. Verifique la conexión a la base de datos.");
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("CargarConceptosNueva: Obteniendo solicitudes...");
                var userId = GetUserId();
                System.Diagnostics.Debug.WriteLine($"CargarConceptosNueva: userId = {userId}");
                var solicitudes = (User != null && User.IsInRole("Administrador"))
                    ? _solicitudDao.ObtenerTodos()
                    : _solicitudDao.ObtenerPorUsuario(userId);

                System.Diagnostics.Debug.WriteLine($"CargarConceptosNueva: {solicitudes?.Count ?? 0} solicitudes obtenidas");
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
                System.Diagnostics.Debug.WriteLine("*** ERROR en CargarConceptosNueva - Solicitudes: " + ex.ToString());
                model.Solicitudes = new List<CapaPresentacion.Models.OrdenRecaudacionNuevaVM.SolicitudOptionVM>();
            }
        }

        /// <summary>
        /// Obtiene valor de tarifa configurable desde BD, con fallback a valor por defecto
        /// </summary>
        private decimal ObtenerTarifaConfigurable(string clave, decimal valorPorDefecto)
        {
            try
            {
                var parametro = _parametroDao.ObtenerPorClave(clave);
                if (parametro != null && parametro.Activo && !string.IsNullOrEmpty(parametro.Valor))
                {
                    if (decimal.TryParse(parametro.Valor, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal valor))
                    {
                        return valor;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo tarifa '{clave}': {ex.Message}");
            }

            return valorPorDefecto;
        }

        /// <summary>
        /// Obtiene porcentaje configurable desde BD, con fallback a valor por defecto
        /// </summary>
        private decimal ObtenerPorcentajeConfigurable(string clave, decimal valorPorDefecto)
        {
            return ObtenerTarifaConfigurable(clave, valorPorDefecto);
        }

        private void AsegurarConceptosBasicos()
        {
            var conceptos = new List<CapaDatos.Models.ConceptoModel>
            {
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "EMI_AOCR", 
                    Nombre = "Emisión AOCR", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_EMI_AOCR", 0m), 
                    Activo = true, 
                    Orden = 1, 
                    Descripcion = "Emisi�n AOCR", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "REN_AOCR", 
                    Nombre = "Renovación AOCR", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_REN_AOCR", 3300m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_REN_AOCR", 0m), 
                    Activo = true, 
                    Orden = 2, 
                    Descripcion = "Renovaci�n AOCR", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "MOD_AOCR_INC", 
                    Nombre = "Modificación AOCR (Inclusi�n aeronaves distinto modelo y tipo)", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_MOD_AOCR_INC", 1600m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_MOD", 0m), 
                    Activo = true, 
                    Orden = 3, 
                    Descripcion = "Modificaci�n AOCR (Inclusi�n aeronaves distinto modelo y tipo)", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "MOD_AOCR_SIN_INC", 
                    Nombre = "Modificación AOCR (Que no implique incremento de aeronaves)", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_MOD_AOCR_SIN_INC", 80m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_MOD", 0m), 
                    Activo = true, 
                    Orden = 4, 
                    Descripcion = "Modificaci�n AOCR (Que no implique incremento de aeronaves)", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "INSPECCION_EXT", 
                    Nombre = "Inspección requerida por el Operador Aereo Extranjero", 
                    TipoCalculo = "POR_ESTACION", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_INSPECCION_EXT", 500m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_INSPECCION", 0m), 
                    Activo = true, 
                    Orden = 5, 
                    Descripcion = "Inspecci�n requerida por el Operador A�reo Extranjero (por estaci�n)", 
                    PorEstacion = true, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "VIATICOS_INSPECTOR", 
                    Nombre = "Viáticos a Sres. Inspectores", 
                    TipoCalculo = "POR_DIA", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_VIATICOS_INSPECTOR", 80m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_VIATICOS", 8m), 
                    Activo = true, 
                    Orden = 6, 
                    Descripcion = "Vi�ticos por d�a (m�s 8% de gastos administrativos)", 
                    PorEstacion = false, 
                    PorDia = true, 
                    EsViatico = true 
                }
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
        public ActionResult RegistrarPago(int id, string Monto, string NumeroFactura, string MetodoPago, string Banco, HttpPostedFileBase ComprobanteArchivo, string Observaciones)
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
                // Generar número de factura único automáticamente
                NumeroFactura = $"PAG-{id}-{DateTime.Now:yyyyMMddHHmmss}";
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
                    Banco = Banco,
                    // ✅ Debe coincidir con chk_estado_pago (case-sensitive)
                    Estado = CapaDatos.Constants.EstadoPago.Pendiente,
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
        /// Descargar PDF de orden
        /// </summary>
        [HttpGet]
        public ActionResult DescargarPdf(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var ordenModel = _dao.ObtenerOrdenPorIdModel(id);
            if (ordenModel == null)
                return HttpNotFound();

            var esFinanciero = User != null && (User.IsInRole("Financiero") || User.IsInRole("Administrador"));
            if (!esFinanciero && ordenModel.CodigoUsuario != idUsuario)
                return HttpNotFound();

            try
            {
                var pdfModel = BuildOrdenRecaudacionPdfModel(ordenModel);
                var nombreArchivo = "Orden_" + (ordenModel.NumeroOrden ?? id.ToString()) + ".pdf";

                return new PartialViewAsPdf("OrdenRecaudacionPDF", pdfModel)
                {
                    FileName = nombreArchivo,
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageMargins = new Rotativa.Options.Margins(20, 15, 20, 15),
                    CustomSwitches = "--disable-smart-shrinking --print-media-type"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al generar PDF: " + ex.Message);
                TempData["ErrorMessage"] = "Error al generar el PDF.";
                return RedirectToAction("Detalles", new { id });
            }
        }

        private CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFModel BuildOrdenRecaudacionPdfModel(OrdenRecaudacionModel ordenModel)
        {
            var detalles = ordenModel.Detalles ?? new List<CapaDatos.Models.OrdenDetalleModel>();
            if (detalles.Count == 0)
            {
                var detallesEnt = _dao.ObtenerDetallesPorOrdenId(ordenModel.Id);
                foreach (var d in detallesEnt)
                {
                    detalles.Add(new CapaDatos.Models.OrdenDetalleModel
                    {
                        Id = d.Id,
                        OrdenId = d.OrdenId,
                        ConceptoId = d.ConceptoId ?? 0,
                        ConceptoCodigo = d.ConceptoCodigo,
                        ConceptoNombre = d.ConceptoNombre,
                        Descripcion = d.Descripcion,
                        Cantidad = d.Cantidad,
                        ValorUnitario = d.ValorUnitario,
                        PorcentajeAdmin = d.PorcentajeAdmin,
                        Subtotal = d.Subtotal,
                        Admin = d.Admin,
                        TotalLinea = d.TotalLinea
                    });
                }
            }

            var estaciones = 0m;
            var dias = 0m;
            foreach (var d in detalles)
            {
                var codigo = (d.ConceptoCodigo ?? "").ToUpperInvariant();
                var nombre = (d.ConceptoNombre ?? "").ToUpperInvariant();
                if (codigo.Contains("INSP") || nombre.Contains("INSPECC"))
                {
                    estaciones += d.Cantidad;
                }
                if (codigo.Contains("VIAT") || nombre.Contains("VIATIC") || nombre.Contains("VIÁTIC"))
                {
                    dias += d.Cantidad;
                }
            }

            var conceptoPrincipal = detalles.Count > 0 ? detalles[0].ConceptoNombre : null;
            var valorBase = ordenModel.Subtotal != 0 ? ordenModel.Subtotal : (ordenModel.Total != 0 ? ordenModel.Total : detalles.Sum(d => d.Subtotal));

            CapaModelo.SolicitudAOCR solicitud = null;
            int codigoSolicitudInt = 0;
            if (!string.IsNullOrEmpty(ordenModel.CodigoSolicitud) && int.TryParse(ordenModel.CodigoSolicitud, out codigoSolicitudInt) && codigoSolicitudInt > 0)
            {
                var solicitudDAO = new CapaDatos.DAOs.SolicitudDAO();
                solicitud = solicitudDAO.ObtenerPorId(codigoSolicitudInt);
            }
            else if (!string.IsNullOrWhiteSpace(ordenModel.RucCedula))
            {
                codigoSolicitudInt = _dao.ObtenerCodigoSolicitudPorRuc(ordenModel.RucCedula);
                if (codigoSolicitudInt > 0)
                {
                    var solicitudDAO = new CapaDatos.DAOs.SolicitudDAO();
                    solicitud = solicitudDAO.ObtenerPorId(codigoSolicitudInt);
                }
            }

            var ultimoPago = _dao.ObtenerUltimoPagoPorOrden(ordenModel.Id);
            var bancoPago = ultimoPago?.BancoOrigen ?? ultimoPago?.MetodoPago;
            var numeroComp = ultimoPago?.NumeroComprobante ?? ultimoPago?.NumeroFactura;

            var pdfModel = new CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFModel
            {
                NumeroOrden = ordenModel.NumeroOrden,
                FechaEmision = ordenModel.FechaCreacion != default(DateTime) ? ordenModel.FechaCreacion : DateTime.Now,
                LugarEmision = solicitud?.Ciudad ?? ordenModel.LugarEmision ?? "Quito",
                NombreCompania = solicitud?.RazonSocial ?? ordenModel.NombreContribuyente ?? ordenModel.Compania ?? "Empresa no especificada",
                Ruc = solicitud?.Ruc ?? ordenModel.RucCedula ?? "RUC no especificado",
                Email = solicitud?.Email ?? ordenModel.Correo ?? "correo@empresa.com",
                Telefono = solicitud?.Telefono ?? ordenModel.Telefono ?? "Teléfono no especificado",
                Banco = string.IsNullOrWhiteSpace(bancoPago) ? "No especificado" : bancoPago,
                NumeroComprobante = string.IsNullOrWhiteSpace(numeroComp) ? "No registrado" : numeroComp,
                ConceptoPrincipal = conceptoPrincipal ?? solicitud?.DescripcionOperacion ?? "Inspección y Certificación AOCR",
                ValorBase = valorBase,
                Estaciones = (int)Math.Round(estaciones),
                Dias = (int)Math.Round(dias),
                Referencia = $"Orden de Recaudación {ordenModel.NumeroOrden} - Solicitud {solicitud?.NumeroSolicitud ?? "N/A"}"
            };

            pdfModel.CalcularTotales();
            return pdfModel;
        }

        /// <summary>
        /// Debug method to test order number generation and storage
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> DebugOrdenNumero()
        {
            var result = new System.Text.StringBuilder();
            result.AppendLine("=== DEBUG ORDER NUMBER GENERATION ===");
            
            try
            {
                var fecha = DateTime.Now;
                var timestamp = fecha.ToString("yyyyMMddHHmmss");
                var consecutivo = await _dao.ObtenerConsecutivoDiarioAsync(fecha) + 1;
                var numeroOrden = string.Format("OR-{0}-{1}", timestamp, consecutivo);
                
                result.AppendLine($"Generated: {numeroOrden}");
                result.AppendLine($"Timestamp: {timestamp}");
                result.AppendLine($"Consecutivo: {consecutivo}");
                
                // Test basic insertion and retrieval
                var testOrden = new OrdenRecaudacion 
                {
                    NumeroOrden = numeroOrden,
                    CodigoUsuario = 1, // Use hardcoded user for test
                    Estado = "DEBUG_TEST",
                    FechaCreacion = DateTime.Now,
                    Total = 0m,
                    Compania = "TEST",
                    LugarEmision = "TEST"
                };
                
                result.AppendLine($"Test order object created with NumeroOrden: {testOrden.NumeroOrden}");
                
                var testId = _dao.Insertar(testOrden);
                result.AppendLine($"Inserted with ID: {testId}");
                
                // Immediately retrieve to verify
                var retrieved = _dao.ObtenerPorId(testId);
                result.AppendLine($"Retrieved NumeroOrden: {retrieved?.NumeroOrden}");
                
                // Also test the model mapping
                var retrievedModel = _dao.ObtenerOrdenPorIdModel(testId);
                result.AppendLine($"Retrieved Model NumeroOrden: {retrievedModel?.NumeroOrden}");
                
            }
            catch (Exception ex)
            {
                result.AppendLine($"ERROR: {ex.Message}");
                result.AppendLine($"Stack: {ex.StackTrace}");
            }
            
            return Content(result.ToString(), "text/plain");
        }

        /// <summary>
        /// Convierte lista de valores P9 a SelectList
        /// </summary>
        private SelectList ToSelectList(string valueCampo)
        {
            var list = new List<SelectListItem>();
            
            try
            {
                var listValores = CapaDatos.DAOs.CD_ListaValor.Instancia.ListaValores(valueCampo);
                
                foreach (var item in listValores)
                {
                    list.Add(new SelectListItem
                    {
                        Text = item.Descripcion.Trim(),
                        Value = item.Codigo.Trim()
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ToSelectList: {ex.Message}");
            }
            
            // Agregar opción por defecto
            var seleccion = new SelectListItem
            {
                Value = "0",
                Text = "---SELECCIONAR...",
                Selected = true
            };
            list.Insert(0, seleccion);

            return new SelectList(list, "Value", "Text");
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

        /// <summary>
        /// Validar un pago específico
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Administrador,Financiero")]
        public ActionResult ValidarPago(int ordenId, int pagoId)
        {
            try
            {
                string usuario = User.Identity.Name ?? "SISTEMA";
                bool resultado = _dao.ActualizarUltimoPagoEstado(ordenId, CapaDatos.Constants.EstadoPago.Validado, usuario, "Pago validado");
                
                if (resultado)
                {
                    TempData["Success"] = "Pago validado correctamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo validar el pago";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al validar pago: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = ordenId });
        }

        /// <summary>
        /// Rechazar un pago específico
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Administrador,Financiero")]
        public ActionResult RechazarPago(int ordenId, int pagoId, string motivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    TempData["Error"] = "Debe proporcionar un motivo para el rechazo";
                    return RedirectToAction("Detalles", new { id = ordenId });
                }

                string usuario = User.Identity.Name ?? "SISTEMA";
                bool resultado = _dao.ActualizarUltimoPagoEstado(ordenId, CapaDatos.Constants.EstadoPago.Rechazado, usuario, motivo);
                
                if (resultado)
                {
                    TempData["Success"] = "Pago rechazado correctamente";
                }
                else
                {
                    TempData["Error"] = "No se pudo rechazar el pago";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al rechazar pago: " + ex.Message;
            }

            return RedirectToAction("Detalles", new { id = ordenId });
        }

        // GET: /OrdenRecaudacion/AgregarColumnaBanco
        [Authorize(Roles = "Administrador")]
        public ActionResult AgregarColumnaBanco()
        {
            try
            {
                var resultado = _dao.AgregarColumnaBancoTemporal();
                if (resultado)
                {
                    TempData["OK"] = "Columna banco agregada exitosamente a la tabla de pagos.";
                }
                else
                {
                    TempData["Error"] = "No se pudo agregar la columna banco. Verifique los logs.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error ejecutando comando: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }

        // GET: /OrdenRecaudacion/ProbarAS400
        [Authorize(Roles = "Administrador")]
        public ActionResult ProbarAS400()
        {
            try
            {
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO();
                var resultado = bancoPDao.ProbarConexionAS400();
                
                if (resultado.StartsWith("OK"))
                {
                    TempData["OK"] = $"Conexión AS400 exitosa: {resultado}";
                }
                else
                {
                    TempData["Error"] = $"Error en conexión AS400: {resultado}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error probando AS400: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }

        // GET: /OrdenRecaudacion/VerificarDriversODBC
        [Authorize(Roles = "Administrador")]
        public ActionResult VerificarDriversODBC()
        {
            try
            {
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO();
                var resultado = bancoPDao.VerificarDriverODBC();
                
                if (resultado.StartsWith("✅"))
                {
                    TempData["OK"] = resultado;
                }
                else
                {
                    TempData["Error"] = resultado;
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error verificando drivers: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }

        // GET: /OrdenRecaudacion/ListarDriversODBC
        [Authorize(Roles = "Administrador")]
        public ActionResult ListarDriversODBC()
        {
            try
            {
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO();
                var resultado = bancoPDao.ListarDriversODBC();
                return Content(resultado, "text/plain");
            }
            catch (Exception ex)
            {
                return Content($"Error listando drivers: {ex.Message}", "text/plain");
            }
        }
    }
}







