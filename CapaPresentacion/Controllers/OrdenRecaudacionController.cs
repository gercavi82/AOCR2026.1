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
using CapaPresentacion.Helpers;
using CapaModelo;
using CapaNegocio.Services;
using CapaNegocio.Integraciones.As400Sync;
using CapaNegocio.Helpers;
using Rotativa;
// Alias para evitar ambigï¿½edad
using EmailSvc = CapaDatos.Services.EmailService;
using SecureConfig = CapaDatos.Services.SecureConfigurationService;
using DetalleOrden = CapaDatos.Entidades.DetalleOrden;
using CapaDatos.Constants;
using OrdenRecaudacionModel = CapaDatos.Models.OrdenRecaudacionModel;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly OrdenRecaudacionCorreoService _ordenCorreoService = new OrdenRecaudacionCorreoService();
        private readonly CapaNegocio.Services.OrdenRecaudacionService _ordenRecaudacionService = new CapaNegocio.Services.OrdenRecaudacionService();

        private static readonly Dictionary<string, string> TablasCiudadPermitidas =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "aocr_tbsolicitud", "codigo_solicitud = @id" },
                { "usuario", "idusuario = @id" }
            };

        private static readonly object MirrorSyncLock = new object();
        private static readonly Dictionary<string, DateTime> LastOnDemandSyncUtc =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly bool EnableAs400RuntimeFallback = AppFlagEnabled("AS400:RuntimeFallbackEnabled", false);
        private static readonly bool EnableOnDemandMirrorRefresh = AppFlagEnabled("Sync:OnDemandFromRequestEnabled", false);

        private OrdenRecaudacionDAO _ordenDAO;
        private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();
        private readonly OrdenRecaudacionBL _bl = new OrdenRecaudacionBL();
        private readonly ConceptoDAO _conceptoDao = new ConceptoDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly BancoP9DAO _bancoDao = new BancoP9DAO(new SecureConfig());
        private readonly ParametroDAO _parametroDao = new ParametroDAO();
        private readonly MirrorReadService _mirrorReadService = new MirrorReadService();
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

            try
            {
                _orchestrator = new OrdenRecaudacionOrchestrator(
                    new OrdenRecaudacionDAO(),
                    new PagoDAO(),
                    null,
                    null,
                    null,
                    null
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR Orchestrator: " + ex.Message);
                throw;
            }
        }

        // ? Para confirmar conexió real a DB (útil en producció)
        [Authorize(Roles = "Administrador,Financiero")]
        public JsonResult DbPing()
        {
            return Json(new { ok = _dao.Ping() }, JsonRequestBehavior.AllowGet);
        }

        // Diagnóstico de sesión (solo Administradores)
        [Authorize(Roles = "Administrador")]
        public ActionResult DiagnosticoSesion()
        {
            var diagnostico = new System.Text.StringBuilder();
            diagnostico.AppendLine("=== DIAGNÃ“STICO DE SESIÃ“N ===\n");
            diagnostico.AppendLine($"Fecha/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n");
            
            // Usuario autenticado
            diagnostico.AppendLine($"User.Identity.IsAuthenticated: {User?.Identity?.IsAuthenticated ?? false}");
            diagnostico.AppendLine($"User.Identity.Name: {User?.Identity?.Name ?? "null"}");
            
            // Roles
            var principal = User as System.Security.Principal.GenericPrincipal;
            if (principal != null && principal.IsInRole("Solicitante"))
                diagnostico.AppendLine("âœ“ Usuario TIENE rol 'Solicitante'");
            else
                diagnostico.AppendLine("âœ— Usuario NO tiene rol 'Solicitante'");
                
            if (principal != null && principal.IsInRole("Administrador"))
                diagnostico.AppendLine("âœ“ Usuario TIENE rol 'Administrador'");
            else
                diagnostico.AppendLine("âœ— Usuario NO tiene rol 'Administrador'");
                
            if (principal != null && principal.IsInRole("Operador"))
                diagnostico.AppendLine("âœ“ Usuario TIENE rol 'Operador'");
            else
                diagnostico.AppendLine("âœ— Usuario NO tiene rol 'Operador'");
            
            var allRoles = new List<string>();
            if (principal != null) {
                if (principal.IsInRole("Solicitante")) allRoles.Add("Solicitante");
                if (principal.IsInRole("Administrador")) allRoles.Add("Administrador");
                if (principal.IsInRole("Operador")) allRoles.Add("Operador");
                if (principal.IsInRole("Financiero")) allRoles.Add("Financiero");
                if (principal.IsInRole("Inspector")) allRoles.Add("Inspector");
                if (principal.IsInRole("Tecnico")) allRoles.Add("Tecnico");
                if (principal.IsInRole("CoordinacionLegal")) allRoles.Add("CoordinacionLegal");
                if (principal.IsInRole("Direccion")) allRoles.Add("Direccion");
                if (principal.IsInRole("JefaturaTecnica")) allRoles.Add("JefaturaTecnica");
            }
            diagnostico.AppendLine($"\nTodos los roles: {string.Join(", ", allRoles)}");
            
            // Sesió
            diagnostico.AppendLine($"\nSession['IdUsuario']: {Session["IdUsuario"]?.ToString() ?? "null"}");
            diagnostico.AppendLine($"Session['UserId']: {Session["UserId"]?.ToString() ?? "null"}");
            diagnostico.AppendLine($"Session['Correo']: {Session["Correo"]?.ToString() ?? "null"}");
            diagnostico.AppendLine($"Session['Rol']: {Session["Rol"]?.ToString() ?? "null"}");
            
            // Intentar acceso a Nueva
            diagnostico.AppendLine($"\nÂ¿Puede acceder a Nueva?: {(principal != null && (principal.IsInRole("Solicitante") || principal.IsInRole("Administrador") || principal.IsInRole("Operador")) ? "SÃ" : "NO")}");
            
            ViewBag.Diagnostico = diagnostico.ToString();
            return Content("<pre>" + diagnostico.ToString() + "</pre>", "text/html");
        }

        // Diagnostico de tarifas - Ver valores actuales y parseo
        [Authorize(Roles = "Administrador")]
        public ActionResult DiagnosticoTarifas()
        {
            var diag = new System.Text.StringBuilder();
            diag.AppendLine("<html><head><meta charset='utf-8'><style>");
            diag.AppendLine("body { font-family: Consolas, monospace; padding: 20px; }");
            diag.AppendLine("table { border-collapse: collapse; width: 100%; margin-top: 20px; }");
            diag.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            diag.AppendLine("th { background-color: #4CAF50; color: white; }");
            diag.AppendLine(".error { background-color: #ffcccc; }");
            diag.AppendLine(".success { background-color: #ccffcc; }");
            diag.AppendLine(".warning { background-color: #ffffcc; }");
            diag.AppendLine("pre { background: #f4f4f4; padding: 10px; border-radius: 4px; }");
            diag.AppendLine("</style></head><body>");
            diag.AppendLine("<h1>Diagnostico de Tarifas</h1>");
            diag.AppendLine($"<p><strong>Fecha/Hora:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            
            var tarifas = new[] {
                "TARIFA_EMI_AOCR",
                "TARIFA_REN_AOCR",
                "TARIFA_MOD_AOCR_INC",
                "TARIFA_MOD_AOCR_SIN_INC",
                "TARIFA_INSPECCION_EXT",
                "TARIFA_VIATICOS_INSPECTOR",
                "PORCENTAJE_ADMIN_VIATICOS"
            };

            var valoresDB = new Dictionary<string, string>();
            try
            {
                var cs = System.Configuration.ConfigurationManager.ConnectionStrings["PostgreSQL"].ConnectionString;
                using (var conn = new Npgsql.NpgsqlConnection(cs))
                {
                    conn.Open();
                    foreach (var clave in tarifas)
                    {
                        var cmd = new Npgsql.NpgsqlCommand(
                            "SELECT valor FROM aocr_tbparametro WHERE clave = @clave AND deletedat IS NULL ORDER BY codigoparametro DESC LIMIT 1", conn);
                        cmd.Parameters.AddWithValue("@clave", clave);
                        var resultado = cmd.ExecuteScalar();
                        valoresDB[clave] = resultado?.ToString() ?? "[NULL]";
                    }
                }
            }
            catch (Exception exDB)
            {
                diag.AppendLine($"<div class='error'><strong>Error BD:</strong> {exDB.Message}</div>");
            }
            
            diag.AppendLine("<table>");
            diag.AppendLine("<tr><th>Clave</th><th>Valor DB</th><th>Long</th><th>Hex</th><th>Parseado</th><th>Estado</th></tr>");
            
            foreach (var claveTarifa in tarifas)
            {
                try
                {
                    string valorBD = valoresDB.ContainsKey(claveTarifa) ? valoresDB[claveTarifa] : "[NO_ENCONTRADO]";
                    
                    if (valorBD == "[NULL]" || valorBD == "[NO_ENCONTRADO]")
                    {
                        diag.AppendLine($"<tr class='warning'><td>{claveTarifa}</td><td colspan='5'>Parametro no existe o NULL</td></tr>");
                        continue;
                    }
                    
                    var longitud = valorBD.Length;
                    var bytes = System.Text.Encoding.UTF8.GetBytes(valorBD);
                    var hex = string.Join(" ", bytes.Take(30).Select(b => b.ToString("X2")));
                    
                    var limpio = valorBD.Trim().Replace("$", "").Replace("USD", "").Replace(" ", "").Replace("_", "");
                    
                    decimal valorParse = 0m;
                    var exito = false;
                    var metodo = "";
                    
                    if (decimal.TryParse(limpio, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out valorParse))
                    {
                        exito = true;
                        metodo = "InvariantCulture";
                    }
                    else if (decimal.TryParse(limpio, System.Globalization.NumberStyles.Any,
                        new System.Globalization.CultureInfo("es-ES"), out valorParse))
                    {
                        exito = true;
                        metodo = "es-ES";
                    }
                    else
                    {
                        var conPunto = limpio.Replace(",", ".");
                        if (decimal.TryParse(conPunto, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out valorParse))
                        {
                            exito = true;
                            metodo = "Coma2Punto";
                        }
                    }
                    
                    var css = exito ? "success" : "error";
                    var mostrar = exito ? valorParse.ToString("0.00") : "N/A";
                    var estado = exito ? $"OK ({metodo})" : $"ERROR (Limpio: {limpio})";
                    
                    diag.AppendLine($"<tr class='{css}'><td>{claveTarifa}</td><td><code>{valorBD}</code></td><td>{longitud}</td><td style='font-size:10px;'>{hex}</td><td>{mostrar}</td><td>{estado}</td></tr>");
                }
                catch (Exception ex)
                {
                    diag.AppendLine($"<tr class='error'><td>{claveTarifa}</td><td colspan='5'>Excepcion: {ex.Message}</td></tr>");
                }
            }
            
            diag.AppendLine("</table>");
            
            diag.AppendLine("<h3>Comandos SQL para corregir</h3>");
            diag.AppendLine("<pre>");
            foreach (var clave in tarifas)
            {
                var valorDef = "0";
                if (clave.Contains("EMI")) valorDef = "3300";
                else if (clave.Contains("REN")) valorDef = "3300";
                else if (clave.Contains("MOD") && clave.Contains("INC")) valorDef = "1600";
                else if (clave.Contains("MOD") && clave.Contains("SIN")) valorDef = "80";
                else if (clave.Contains("INSPECCION")) valorDef = "500";
                else if (clave.Contains("VIATICOS")) valorDef = "100";
                else if (clave.Contains("PORCENTAJE")) valorDef = "10";
                
                diag.AppendLine($"UPDATE aocr_tbparametro SET valor = '{valorDef}', updatedat = NOW() WHERE clave = '{clave}' AND deletedat IS NULL;");
            }
            diag.AppendLine("</pre>");
            diag.AppendLine("<p><strong>Nota:</strong> Formato: <code>1234.56</code> o <code>1234,56</code></p>");
            diag.AppendLine("</body></html>");
            
            return Content(diag.ToString(), "text/html");
        }

        public ActionResult Index(string estado)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            CargarEstadosCombo(estado);
            CargarContinuidadOrdenUsuario(idUsuario);

            var ordenes = _dao.ListarPorUsuarioModel(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();

            // Estadï¿½sticas: tu view espera claves con mayï¿½scula
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
            CargarContinuidadOrdenUsuario(idUsuario);

            var ordenes = _dao.ListarPorUsuario(idUsuario, null) ?? new List<OrdenRecaudacion>();
            System.Diagnostics.Debug.WriteLine(string.Format("Obligatoria: Se encontraron {0} Órdenes", ordenes.Count));

            // Estadisticas
            var est = _dao.ObtenerEstadisticas(idUsuario);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);

            return View(ordenes);
        }

        private void CargarContinuidadOrdenUsuario(int idUsuario)
        {
            var ordenPendiente = _dao.ObtenerOrdenPendienteUsuarioAccion(idUsuario);
            var estadoPendiente = EstadoOrden.NormalizarEstado(ordenPendiente != null ? ordenPendiente.Estado : null);
            var requiereComprobante = estadoPendiente == EstadoOrden.Pendiente ||
                                      estadoPendiente == EstadoOrden.Generada ||
                                      estadoPendiente == EstadoOrden.Devuelta;

            ViewBag.OrdenPendienteAccionId = ordenPendiente != null ? ordenPendiente.Id : 0;
            ViewBag.OrdenPendienteNumero = ordenPendiente != null ? ordenPendiente.NumeroOrden : string.Empty;
            ViewBag.OrdenPendienteRequiereComprobante = requiereComprobante;
            ViewBag.TieneOrdenBorrador = estadoPendiente == EstadoOrden.Borrador;
        }

        // GET: /OrdenRecaudacion/Nueva
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public ActionResult Nueva()
        {
            var userId = GetUserId();
            if (userId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var ordenPendiente = _dao.ObtenerOrdenPendienteUsuarioAccion(userId);
            if (ordenPendiente != null)
            {
                var estadoPendiente = EstadoOrden.NormalizarEstado(ordenPendiente.Estado);
                var requiereComprobante = estadoPendiente == EstadoOrden.Pendiente ||
                                          estadoPendiente == EstadoOrden.Generada ||
                                          estadoPendiente == EstadoOrden.Devuelta;
                TempData["OK"] = requiereComprobante
                    ? "Ya existe una orden pendiente de comprobante. Continúe con esa orden antes de crear otra."
                    : "Ya existe una orden en borrador. Continúe con esa orden antes de crear otra.";
                return RedirectToAction("Detalles", new { id = ordenPendiente.Id, abrirPago = requiereComprobante });
            }

            var model = new CapaPresentacion.Models.OrdenRecaudacionNuevaVM();
            CargarConceptosNueva(model);
            Usuario usuario = null;
            // Prefill bÃ¡sico desde usuario/empresa (editables)
            try
            {
                usuario = UsuarioDAO.ObtenerPorId(userId);
                var empresaNombre = ObtenerNombreCompaniaActiva(usuario);

                if (!string.IsNullOrWhiteSpace(empresaNombre))
                    model.Orden.Compania = empresaNombre;

                var rucCedula = ResolverRucCedulaDesdeFuentes(userId, usuario);
                if (!string.IsNullOrWhiteSpace(rucCedula))
                    model.Orden.RucCedula = ExtraerRucCedula(rucCedula);

                if (!string.IsNullOrWhiteSpace(usuario?.Email))
                    model.Orden.Correo = usuario.Email;
            }
            catch
            {
                // ignorar prefill si falla
            }

            // Completar campos faltantes con la última orden registrada del usuario.
            PrefillDesdeUltimaOrden(userId, model);
            if (string.IsNullOrWhiteSpace(model.Orden.RucCedula))
            {
                model.Orden.RucCedula = ExtraerRucCedula(ResolverRucCedulaDesdeFuentes(userId, usuario));
            }

            var nombreCompaniaActiva = ObtenerNombreCompaniaActiva(usuario);
            if (!string.IsNullOrWhiteSpace(nombreCompaniaActiva))
            {
                model.Orden.Compania = nombreCompaniaActiva;
            }

            model.Orden.LugarEmision = ResolverLugarEmisionDesdeDb(model.Orden.CodigoSolicitud, userId);
            return View(model);
        }

        /// <summary>
        /// Crear nueva orden - acepta OrdenRecaudacionNuevaVM desde la vista
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public async Task<ActionResult> Nueva(OrdenRecaudacionNuevaVM model)
        {
            try
            {
                var idUsuario = GetUserId();
                if (idUsuario <= 0)
                {
                    ModelState.AddModelError("", "Usuario no autenticado.");
                    CargarConceptosNueva(model);
                    return View(model);
                }

                var ordenPendiente = _dao.ObtenerOrdenPendienteUsuarioAccion(idUsuario);
                if (ordenPendiente != null)
                {
                    var estadoPendiente = EstadoOrden.NormalizarEstado(ordenPendiente.Estado);
                    var requiereComprobante = estadoPendiente == EstadoOrden.Pendiente ||
                                              estadoPendiente == EstadoOrden.Generada ||
                                              estadoPendiente == EstadoOrden.Devuelta;
                    TempData["Error"] = requiereComprobante
                        ? "Ya existe una orden pendiente de comprobante para este usuario. Cargue el respaldo y continúe con la orden existente."
                        : "Ya existe una orden en borrador para este usuario. Complete la orden existente antes de crear otra.";
                    return RedirectToAction("Detalles", new { id = ordenPendiente.Id, abrirPago = requiereComprobante });
                }

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
                var usuarioActual = UsuarioDAO.ObtenerPorId(idUsuario);
                var rucDesdeDb = ResolverRucCedulaDesdeFuentes(idUsuario, usuarioActual);
                if (string.IsNullOrWhiteSpace(rucDesdeDb))
                {
                    ModelState.AddModelError("Orden.RucCedula", "No se encontró RUC/Cédula del contribuyente en base de datos.");
                    CargarConceptosNueva(model);
                    return View(model);
                }
                model.Orden.RucCedula = ExtraerRucCedula(rucDesdeDb);

                var nombreCompaniaActiva = ObtenerNombreCompaniaActiva(usuarioActual);
                if (string.IsNullOrWhiteSpace(nombreCompaniaActiva))
                {
                    ModelState.AddModelError("Orden.Compania", "No se encontró la compañía activa de la sesión. Seleccione una compañía activa e intente nuevamente.");
                    CargarConceptosNueva(model);
                    return View(model);
                }
                model.Orden.Compania = nombreCompaniaActiva;

                System.Diagnostics.Debug.WriteLine($"Controller Nueva: idUsuario = {idUsuario}");

                var numeroOrden = await GenerarNumeroOrdenAsync();
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: numeroOrden generado = {numeroOrden}");
                var codigoSolicitud = int.TryParse(model.Orden?.CodigoSolicitud?.ToString(), out int cs) ? (int?)cs : null;
                var lugarEmisionDb = ResolverLugarEmisionDesdeDb(codigoSolicitud, idUsuario);

                var orden = new OrdenRecaudacion
                {
                    NumeroOrden = numeroOrden,
                    CodigoUsuario = idUsuario,
                    CodigoSolicitud = codigoSolicitud,
                    LugarEmision = lugarEmisionDb,
                    Compania = nombreCompaniaActiva,
                    NombreContribuyente = nombreCompaniaActiva,
                    RucCedula = model.Orden?.RucCedula,
                    RucContribuyente = model.Orden?.RucCedula,
                    Correo = model.Orden?.Correo,
                    // EmailContribuyente eliminado, usar solo Correo
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
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: DespuÃ©s de insertar, ordenId = {ordenId}");

                if (ordenId > 0)
                {
                    // Insertar detalles
                    foreach (var det in detalles)
                    {
                        // Obtener el concepto para tener el porcentaje de administració
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

                    if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
                    {
                        var solicitudAuto = ConstruirSolicitudAuto(
                            idUsuario,
                            usuarioActual,
                            nombreCompaniaActiva,
                            model.Orden?.RucCedula,
                            model.Orden?.Correo,
                            model.Orden?.Telefono,
                            lugarEmisionDb);

                        var codigoSolicitudGenerado = _dao.CrearSolicitudYVincularOrden(ordenId, solicitudAuto);
                        if (codigoSolicitudGenerado <= 0)
                        {
                            TempData["Error"] = "La orden se creó, pero no se pudo generar y vincular la solicitud asociada.";
                            return RedirectToAction("Detalles", new { id = ordenId });
                        }
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
            return await Task.FromResult(_ordenRecaudacionService.GenerarNumeroOrdenAocr(DateTime.Now.Year));
        }

        // GET: /OrdenRecaudacion/Detalles/5
        public async Task<ActionResult> Detalles(int id, bool abrirPago = false)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = await _dao.ObtenerOrdenPorIdModelAsync(id);
            var esAdmin = User != null && (User.IsInRole("Administrador") || User.IsInRole("Financiero"));
            if (orden == null || (!esAdmin && orden.CodigoUsuario != idUsuario))
                return HttpNotFound();

            ViewBag.AbrirModalPago = abrirPago;

            CompletarDatosOrdenParaVista(orden);

            System.Diagnostics.Debug.WriteLine($"Controller Detalles: ordenId = {id}, numeroOrden = {orden.NumeroOrden}");

            try
            {
                var pagos = await _dao.ObtenerPagosPorOrdenAsync(id);
                NormalizarMontosPagoDesfasados(pagos, orden.Total);
                ViewBag.Pagos = pagos;
            }
            catch
            {
                ViewBag.Pagos = null;
            }

            try
            {
                var comprobanteService = new ComprobanteService();
                ViewBag.TieneComprobanteValido = comprobanteService.ExisteComprobanteValido(id, out var msgComprobante);
                ViewBag.MensajeComprobante = msgComprobante;
            }
            catch
            {
                ViewBag.TieneComprobanteValido = false;
                ViewBag.MensajeComprobante = "Debe registrar el comprobante antes de continuar.";
            }

            try
            {
                ViewBag.FacturaPago = _dao.ObtenerFacturaPagoPorOrden(id);
            }
            catch
            {
                ViewBag.FacturaPago = null;
            }

            // Cargar lista de bancos desde P9
            ViewBag.ListaBancoPago = ToSelectList("OPCBAN");
            
            // Cargar mÃ©todos de pago desde P9
            ViewBag.ListaMetodoPago = ToSelectList("SOLFOR");

            return View(orden);
        }

        private void CompletarDatosOrdenParaVista(OrdenRecaudacionModel orden)
        {
            if (orden == null) return;

            SolicitudAOCR solicitud = null;
            Usuario usuario = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(orden.CodigoSolicitud) &&
                    int.TryParse(orden.CodigoSolicitud, out int codigoSolicitud) &&
                    codigoSolicitud > 0)
                {
                    solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
                }
            }
            catch
            {
                // no bloquear render por fallo de solicitud
            }

            try
            {
                if (orden.CodigoUsuario > 0)
                {
                    usuario = UsuarioDAO.ObtenerPorId(orden.CodigoUsuario);
                }
            }
            catch
            {
                // no bloquear render por fallo de usuario
            }

            if (string.IsNullOrWhiteSpace(orden.Compania))
            {
                orden.Compania = !string.IsNullOrWhiteSpace(solicitud?.RazonSocial)
                    ? solicitud.RazonSocial
                    : (!string.IsNullOrWhiteSpace(solicitud?.NombreOperador)
                        ? solicitud.NombreOperador
                        : usuario?.NombreCompleto);
            }

            if (string.IsNullOrWhiteSpace(orden.RucCedula))
            {
                orden.RucCedula = !string.IsNullOrWhiteSpace(solicitud?.Ruc)
                    ? solicitud.Ruc
                    : ExtraerRucCedula(usuario?.CodigoUsuario ?? usuario?.NombreUsuario);
            }

            if (string.IsNullOrWhiteSpace(orden.Correo))
            {
                orden.Correo = !string.IsNullOrWhiteSpace(solicitud?.Email)
                    ? solicitud.Email
                    : usuario?.Email;
            }

            if (string.IsNullOrWhiteSpace(orden.Telefono))
            {
                orden.Telefono = solicitud?.Telefono;
            }

            if (string.IsNullOrWhiteSpace(orden.LugarEmision))
            {
                orden.LugarEmision = !string.IsNullOrWhiteSpace(solicitud?.Ciudad)
                    ? solicitud.Ciudad
                    : "Quito";
            }

            if (string.IsNullOrWhiteSpace(orden.NombreUsuario))
            {
                orden.NombreUsuario = !string.IsNullOrWhiteSpace(usuario?.NombreCompleto)
                    ? usuario.NombreCompleto
                    : usuario?.NombreUsuario;
            }

            if (string.IsNullOrWhiteSpace(orden.NumeroSolicitud) && !string.IsNullOrWhiteSpace(solicitud?.NumeroSolicitud))
            {
                orden.NumeroSolicitud = solicitud.NumeroSolicitud;
            }
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

            int codigoSolicitud;
            int.TryParse((orden.CodigoSolicitud ?? string.Empty).Trim(), out codigoSolicitud);
            orden.LugarEmision = ResolverLugarEmisionDesdeDb(
                codigoSolicitud > 0 ? (int?)codigoSolicitud : null,
                orden.CodigoUsuario,
                orden.LugarEmision);

            return View(orden);
        }

        // POST: /OrdenRecaudacion/Editar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(OrdenRecaudacionModel model, string accion)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                var errores = string.Join(" | ", ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => x.Key + ": " + (e.ErrorMessage ?? e.Exception?.Message ?? "Error de validación"))));
                System.Diagnostics.Debug.WriteLine("OrdenRecaudacion/Editar POST ModelState inválido: " + errores);
                var ordenVista = _dao.ObtenerOrdenPorIdModel(model.Id);
                if (ordenVista != null)
                {
                    ordenVista.Compania = model.Compania;
                    ordenVista.NombreContribuyente = model.NombreContribuyente;
                    ordenVista.RucCedula = model.RucCedula;
                    ordenVista.Correo = model.Correo;
                    ordenVista.Telefono = model.Telefono;
                    ordenVista.Observacion = model.Observacion;
                    return View(ordenVista);
                }

                return View(model);
            }

            var ordenExistente = _dao.ObtenerPorId(model.Id);
            if (ordenExistente == null || ordenExistente.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((ordenExistente.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(403);

            try
            {
                // Actualizar los campos editables
                var codigoSolicitudExistente = ordenExistente.CodigoSolicitud;
                ordenExistente.LugarEmision = ResolverLugarEmisionDesdeDb(
                    codigoSolicitudExistente > 0 ? codigoSolicitudExistente : null,
                    ordenExistente.CodigoUsuario ?? idUsuario,
                    ordenExistente.LugarEmision);
                ordenExistente.Compania = !string.IsNullOrWhiteSpace(model.Compania)
                    ? model.Compania
                    : model.NombreContribuyente;
                ordenExistente.RucCedula = model.RucCedula;
                ordenExistente.Correo = model.Correo;
                ordenExistente.Telefono = model.Telefono;
                ordenExistente.Observacion = model.Observacion;

                bool result = _dao.ActualizarOrden(ordenExistente);
                System.Diagnostics.Debug.WriteLine($"OrdenRecaudacion/Editar POST resultado update: id={model.Id}, updated={result}");
                if (result)
                {
                    var accionNormalizada = (accion ?? string.Empty).Trim().ToUpperInvariant();
                    var solicitarGeneracion = string.Equals(accionNormalizada, "GENERAR", StringComparison.OrdinalIgnoreCase);

                    if (solicitarGeneracion)
                    {
                        if ((ordenExistente.Total ?? 0m) <= 0m)
                        {
                            TempData["Error"] = "La orden se actualizó, pero no se puede generar sin valores en el detalle.";
                            return RedirectToAction("Detalles", new { id = model.Id });
                        }

                        string errEstado;
                        var cambioEstado = _dao.CambiarEstadoOrden(model.Id, "PENDIENTE", out errEstado);
                        if (!cambioEstado)
                        {
                            // Fallback de compatibilidad con estados legacy.
                            cambioEstado = _dao.CambiarEstadoOrden(model.Id, "GENERADA", out errEstado);
                        }

                        if (!cambioEstado)
                        {
                            TempData["Error"] = "La orden se actualizó, pero no se pudo cambiar el estado. " + (errEstado ?? string.Empty);
                            return RedirectToAction("Detalles", new { id = model.Id });
                        }

                        TempData["OK"] = "Orden actualizada y generada correctamente (pendiente de pago).";
                        return RedirectToAction("Detalles", new { id = model.Id });
                    }

                    TempData["OK"] = "Orden actualizada correctamente";
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Error al actualizar la orden");
                    var ordenReload = _dao.ObtenerOrdenPorIdModel(model.Id);
                    return View(ordenReload ?? model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error interno: " + ex.Message);
                var ordenReload = _dao.ObtenerOrdenPorIdModel(model.Id);
                return View(ordenReload ?? model);
            }
        }

        // POST: /OrdenRecaudacion/AnularAjax/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public JsonResult AnularAjax(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return Json(new { success = false, message = "Usuario no autenticado" });

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null)
                return Json(new { success = false, message = "Orden no encontrada" });

            var esAdmin = User != null && User.IsInRole("Administrador");
            if (!esAdmin && orden.CodigoUsuario != idUsuario)
                return Json(new { success = false, message = "No tiene permisos para anular esta orden" });

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "La orden ya estï¿½ anulada" });

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
            var esAdmin = User != null && (User.IsInRole("Administrador") || User.IsInRole("Financiero"));
            if (orden == null || (!esAdmin && orden.CodigoUsuario != idUsuario))
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden generar ï¿½rdenes en estado BORRADOR";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (orden.Total <= 0)
            {
                TempData["Error"] = "No se puede generar una orden sin conceptos";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                var result = await _dao.CambiarEstadoOrdenAsync(id, "PENDIENTE");
                if (!result)
                {
                    // Fallback legacy
                    result = await _dao.CambiarEstadoOrdenAsync(id, "GENERADA");
                }

                if (result)
                {
                    TempData["OK"] = "Orden generada correctamente (pendiente de pago).";
                    // Notificar al contribuyente con comprobante PDF
                    await EnviarNotificacionOrdenGeneradaAsync(orden);
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

        private Task EnviarNotificacionOrdenGeneradaAsync(OrdenRecaudacionModel orden)
        {
            try
            {
                if (orden == null) return Task.CompletedTask;

                var pdfModel = BuildOrdenRecaudacionPdfModel(orden);
                pdfModel.LeyendaBancos = OrdenRecaudacionPagoHelper.ConstruirLeyendaHtml();
                var nombreArchivo = ConstruirNombrePdfOrdenRecaudacion(orden);
                byte[] pdfBytes = null;

                try
                {
                    var pdf = new PartialViewAsPdf("OrdenRecaudacionPDF", pdfModel)
                    {
                        PageSize = Rotativa.Options.Size.A4,
                        PageOrientation = Rotativa.Options.Orientation.Portrait,
                        PageMargins = new Rotativa.Options.Margins(0, 0, 0, 0),
                        CustomSwitches = PdfBrandingHelper.StandardRotativaSwitches
                    };
                    pdfBytes = pdf.BuildFile(ControllerContext);
                    System.Diagnostics.Debug.WriteLine($"PDF generado para notificación, tamaño: {(pdfBytes != null ? pdfBytes.Length : 0)} bytes");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error al generar PDF para notificación: " + ex.Message);
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"ADVERTENCIA: El PDF de la orden no se generó correctamente, el correo se enviará sin adjunto. Orden: {orden.NumeroOrden}");
                }

                int codigoSolicitudInt;
                if (!string.IsNullOrWhiteSpace(orden.CodigoSolicitud) && int.TryParse(orden.CodigoSolicitud, out codigoSolicitudInt) && codigoSolicitudInt > 0)
                {
                }
                else if (!string.IsNullOrWhiteSpace(orden.RucCedula))
                {
                    codigoSolicitudInt = _dao.ObtenerCodigoSolicitudPorRuc(orden.RucCedula);
                }
                else
                {
                    codigoSolicitudInt = 0;
                }

                var ordenEntidad = new OrdenRecaudacion
                {
                    Id = orden.Id,
                    CodigoSolicitud = codigoSolicitudInt > 0 ? (int?)codigoSolicitudInt : null,
                    NumeroOrden = orden.NumeroOrden,
                    Estado = orden.Estado,
                    Total = orden.Total,
                    Correo = orden.Correo,
                    RucCedula = orden.RucCedula,
                    Compania = orden.Compania,
                    NombreContribuyente = orden.NombreContribuyente
                };

                var instruccionesPagoHtml = ConstruirInstruccionesPagoCorreoHtml(orden);
                var resultadoCorreoRt = _ordenCorreoService.NotificarEvento(
                    ordenEntidad,
                    "ORDEN_CREADA",
                    string.IsNullOrWhiteSpace(orden.Correo) ? null : orden.Correo,
                    string.IsNullOrWhiteSpace(orden.NombreContribuyente) ? orden.Compania : orden.NombreContribuyente,
                    pdfBytes != null && pdfBytes.Length > 0 ? pdfBytes : null,
                    pdfBytes != null && pdfBytes.Length > 0 ? nombreArchivo : null,
                    instruccionesPagoHtml);
                System.Diagnostics.Debug.WriteLine($"Resultado notificación ORDEN_CREADA: Exitoso={resultadoCorreoRt.Exitoso}, Mensaje={resultadoCorreoRt.Mensaje}");

                var resultadoCorreo = _ordenCorreoService.NotificarEvento(
                    ordenEntidad,
                    "ORDEN_RECAUDACION_GENERADA_FINANCIERO",
                    null,
                    null,
                    pdfBytes != null && pdfBytes.Length > 0 ? pdfBytes : null,
                    pdfBytes != null && pdfBytes.Length > 0 ? nombreArchivo : null,
                    pdfBytes != null && pdfBytes.Length > 0
                        ? "Orden de recaudación generada y remitida a Financiero con comprobante adjunto."
                        : "Orden de recaudación generada y remitida a Financiero sin adjunto por falla de PDF.");
                System.Diagnostics.Debug.WriteLine($"Resultado notificación ORDEN_RECAUDACION_GENERADA_FINANCIERO: Exitoso={resultadoCorreo.Exitoso}, Mensaje={resultadoCorreo.Mensaje}");
                if (!resultadoCorreo.Exitoso)
                {
                    TempData["Warning"] = "La orden fue generada, pero la notificación al área Financiera no se pudo encolar: "
                        + (resultadoCorreo.Mensaje ?? "Error no especificado.");
                }
                else if (!resultadoCorreoRt.Exitoso)
                {
                    TempData["Warning"] = "La orden fue generada, pero la notificación al RT no se pudo encolar: "
                        + (resultadoCorreoRt.Mensaje ?? "Error no especificado.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error enviando notificación de orden generada: " + ex.Message);
            }
            return Task.CompletedTask;
        }

        private static string ConstruirInstruccionesPagoCorreoHtml(OrdenRecaudacionModel orden)
        {
            var monto = orden != null ? orden.Total : 0m;
            return @"<div style='margin:0 0 18px 0; padding:16px 18px; background-color:#f8fbfd; border:1px solid #d9e7f1; border-radius:8px;'>"
                + "<p style='margin:0 0 12px 0; font-size:14px; color:#16364a; line-height:1.55;'><strong>Instrucciones para el pago</strong><br>"
                + "El valor a cancelar para esta orden es <strong>$" + monto.ToString("N2") + "</strong>. Realice el pago en una de las cuentas habilitadas por la DGAC y luego registre el comprobante en el módulo de Orden de Recaudación.</p>"
                + OrdenRecaudacionPagoHelper.ConstruirLeyendaHtml()
                + "<p style='margin:12px 0 0 0; font-size:13px; color:#3a4f5e; line-height:1.55;'>El trámite continuará únicamente cuando el pago sea validado por el área Financiera.</p>"
                + "</div>";
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

            try
            {
                AsegurarConceptosBasicos();
                var conceptos = _conceptoDao.ObtenerConceptos(true);
                // Filtrar conceptos únicos por CÓdigo
                var conceptosUnicos = conceptos
                    .GroupBy(c => c.Codigo)
                    .Select(g => g.First())
                    .ToList();
                model.Conceptos = conceptosUnicos.Select(c => new CapaPresentacion.Models.ConceptoOptionVM
                {
                    Id = c.Id,
                    Codigo = c.Codigo,
                    Nombre = c.Nombre,
                    Valor = c.ValorBase,
                    PorcentajeAdmin = c.PorcentajeAdmin,
                    Label = string.Format("{0} - {1} (${2})", c.Codigo, c.Nombre, c.ValorBase.ToString("0.00"))
                }).ToList();
            }
            catch (Exception)
            {
                model.Conceptos = new List<CapaPresentacion.Models.ConceptoOptionVM>();
                ModelState.AddModelError("", "No se pudieron cargar los conceptos. Verifique la conexión a la base de datos.");
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
                        Label = s.NumeroSolicitud,
                        Ruc = s.Ruc,
                        Correo = s.Email,
                        Telefono = s.Telefono,
                        Compania = string.IsNullOrWhiteSpace(s.RazonSocial) ? s.NombreOperador : s.RazonSocial
                    }).ToList();
            }
            catch (Exception)
            {
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
                if (parametro != null && parametro.Activo && !string.IsNullOrWhiteSpace(parametro.Valor))
                {
                    if (TryParseDecimalConfig(parametro.Valor, out decimal valor))
                    {
                        return valor;
                    }

                    System.Diagnostics.Debug.WriteLine($"No se pudo parsear '{clave}': valor='{parametro.Valor}'. Usando valor por defecto: {valorPorDefecto}");
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

        private bool TryParseDecimalConfig(string valorTexto, out decimal valor)
        {
            valor = 0m;
            if (string.IsNullOrWhiteSpace(valorTexto)) return false;

            var limpio = valorTexto.Trim()
                .Replace("$", "")
                .Replace("USD", "")
                .Replace(" ", "")
                .Replace("_", "");

            var ultimoPunto = limpio.LastIndexOf('.');
            var ultimaComa = limpio.LastIndexOf(',');

            if (ultimoPunto >= 0 && ultimaComa >= 0)
            {
                // Si ambos existen, el separador decimal es el último.
                if (ultimaComa > ultimoPunto)
                {
                    limpio = limpio.Replace(".", "");
                    limpio = limpio.Replace(",", ".");
                }
                else
                {
                    limpio = limpio.Replace(",", "");
                }

                return decimal.TryParse(
                    limpio,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out valor);
            }

            if (ultimaComa >= 0)
            {
                var decimales = limpio.Length - ultimaComa - 1;
                if (decimales <= 2)
                {
                    limpio = limpio.Replace(".", "");
                    limpio = limpio.Replace(",", ".");
                }
                else
                {
                    limpio = limpio.Replace(",", "");
                }

                return decimal.TryParse(
                    limpio,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out valor);
            }

            if (ultimoPunto >= 0)
            {
                var decimales = limpio.Length - ultimoPunto - 1;
                if (decimales > 2)
                {
                    limpio = limpio.Replace(".", "");
                }

                return decimal.TryParse(
                    limpio,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out valor);
            }

            return decimal.TryParse(
                limpio,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out valor);
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
                    Descripcion = "Emisión AOCR", 
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
                    Descripcion = "Renovación AOCR", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "MOD_AOCR_INC", 
                    Nombre = "Modificación AOCR (Inclusión aeronaves distinto modelo y tipo)", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_MOD_AOCR_INC", 1600m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_MOD", 0m), 
                    Activo = true, 
                    Orden = 3, 
                    Descripcion = "Modificación AOCR (Inclusión aeronaves distinto modelo y tipo)", 
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
                    Descripcion = "Modificación AOCR (Que no implique incremento de aeronaves)", 
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
                    Descripcion = "Inspección requerida por el Operador Aéreo Extranjero (por estación)", 
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
                    Descripcion = "Viáticos por día (más 8% de gastos administrativos)", 
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

            var estadoOrden = CapaDatos.Constants.EstadoOrden.NormalizarEstado(orden.Estado);
            if (!estadoOrden.Equals(CapaDatos.Constants.EstadoOrden.Pendiente, StringComparison.OrdinalIgnoreCase) &&
                !estadoOrden.Equals(CapaDatos.Constants.EstadoOrden.Generada, StringComparison.OrdinalIgnoreCase) &&
                !estadoOrden.Equals(CapaDatos.Constants.EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se puede cargar respaldo cuando la orden esté en GENERADA, PENDIENTE o DEVUELTA.";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (ComprobanteArchivo == null || ComprobanteArchivo.ContentLength <= 0)
            {
                TempData["Error"] = estadoOrden.Equals(CapaDatos.Constants.EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase)
                    ? "Debe adjuntar el respaldo actualizado antes de reenviar a Financiero."
                    : "Debe adjuntar el respaldo de pago antes de enviar a Financiero.";
                return RedirectToAction("Detalles", new { id = id });
            }

            decimal montoValue;
            var montoRaw = (Monto ?? Request["Monto"] ?? "").Trim();
            if (!TryParseDecimalConfig(montoRaw, out montoValue))
            {
                TempData["Error"] = "Monto inválido";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (montoValue <= 0)
            {
                TempData["Error"] = "El monto debe ser mayor a cero";
                return RedirectToAction("Detalles", new { id = id });
            }

            var pagosExistentes = _dao.ObtenerPagosPorOrden(id) ?? new List<CapaDatos.Models.PagoModel>();
            var totalPagadoValidado = pagosExistentes
                .Where(p =>
                {
                    var estadoPago = (p.Estado ?? string.Empty).Trim().ToUpperInvariant();
                    return estadoPago == "APROBADO" || estadoPago == "VALIDADO";
                })
                .Sum(p => p.Monto);
            var saldoPendienteReal = Math.Max(orden.Total - totalPagadoValidado, 0m);
            if (saldoPendienteReal > 0m && montoValue > saldoPendienteReal)
            {
                TempData["Error"] = "El monto no puede exceder el saldo pendiente de $" + saldoPendienteReal.ToString("#,##0.00", new System.Globalization.CultureInfo("es-EC")) + ".";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(NumeroFactura))
            {
                // Generar número de factura único automáticamente
                NumeroFactura = $"PAG-{id}-{DateTime.Now:yyyyMMddHHmmss}";
            }

            if (string.IsNullOrWhiteSpace(MetodoPago))
            {
                TempData["Error"] = "Debe seleccionar un método de pago";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // Guardar comprobante si existe via helper central (FileStorageHelper)
                string comprobanteRuta = null;
                string savedVirtualPath = null;
                if (ComprobanteArchivo != null && ComprobanteArchivo.ContentLength > 0)
                {
                    // Validación centralizada
                    if (!CapaNegocio.Helpers.FileStorageHelper.ValidateFile(ComprobanteArchivo, out var fileError))
                    {
                        TempData["Error"] = fileError;
                        return RedirectToAction("Detalles", new { id = id });
                    }

                    try
                    {
                        // Guardar en carpeta controlada bajo App_Data
                        savedVirtualPath = CapaNegocio.Helpers.FileStorageHelper.SaveFile(ComprobanteArchivo, "Comprobantes");
                        comprobanteRuta = savedVirtualPath;
                        CapaNegocio.LogBL.RegistrarInfo($"Comprobante guardado: Orden={orden.NumeroOrden} Ruta={savedVirtualPath}", "OrdenRecaudacionController");
                    }
                    catch (Exception exSave)
                    {
                        CapaNegocio.LogBL.RegistrarError($"Error guardando archivo comprobante Orden={orden.NumeroOrden}", exSave.ToString(), "OrdenRecaudacionController");
                        TempData["Error"] = "Error guardando el comprobante. Intente nuevamente.";
                        return RedirectToAction("Detalles", new { id = id });
                    }
                }

                var pago = new CapaDatos.Models.PagoModel
                {
                    NumeroFactura = NumeroFactura,
                    Monto = montoValue,
                    Moneda = "USD",
                    MetodoPago = MetodoPago,
                    Banco = Banco,
                    // âœ… Debe coincidir con chk_estado_pago (case-sensitive)
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
                TempData["Error"] = "La orden no está vinculada a una solicitud válida para registrar el pago.";
                return RedirectToAction("Detalles", new { id = id });
            }

                // Registrar pago + actualizar estado en una transacció atÓmica en BD
                string pagoErr;
                bool transOk = _dao.RegistrarPagoYActualizarEstadoTransaccional(orden.Id, codigoSolicitud, pago, "PROCESADA", out pagoErr);
                if (!transOk)
                {
                    // Si guardamos archivo y la BD fallÓ, borrarlo para no dejar archivos huÃ©rfanos
                    if (!string.IsNullOrWhiteSpace(savedVirtualPath))
                    {
                        CapaNegocio.Helpers.FileStorageHelper.DeleteFile(savedVirtualPath);
                        CapaNegocio.LogBL.RegistrarInfo($"Archivo eliminado por fallo transacció: Orden={orden.NumeroOrden} Ruta={savedVirtualPath}", "OrdenRecaudacionController");
                    }

                    CapaNegocio.LogBL.RegistrarError($"Error registrando pago/transacción Orden={orden.NumeroOrden} CodigoSolicitud={codigoSolicitud}", pagoErr ?? "n/a", "OrdenRecaudacionController");
                    TempData["Error"] = "No se pudo registrar el pago en la base de datos. " + (string.IsNullOrWhiteSpace(pagoErr) ? "" : ("Detalle: " + pagoErr));
                    return RedirectToAction("Detalles", new { id = id });
                }

                try
                {
                    byte[] comprobanteAdjunto = null;
                    string nombreAdjunto = null;
                    if (!string.IsNullOrWhiteSpace(comprobanteRuta))
                    {
                        var rutaFisica = Server.MapPath(comprobanteRuta);
                        if (System.IO.File.Exists(rutaFisica))
                        {
                            comprobanteAdjunto = System.IO.File.ReadAllBytes(rutaFisica);
                            nombreAdjunto = Path.GetFileName(rutaFisica);
                        }
                    }

                    var ordenEntidad = new OrdenRecaudacion
                    {
                        Id = orden.Id,
                        CodigoSolicitud = codigoSolicitud > 0 ? (int?)codigoSolicitud : null,
                        NumeroOrden = orden.NumeroOrden,
                        Estado = "PROCESADA",
                        Total = orden.Total,
                        Correo = orden.Correo,
                        RucCedula = orden.RucCedula,
                        Compania = orden.Compania,
                        NombreContribuyente = orden.NombreContribuyente
                    };

                    var resultadoCorreoPago = _ordenCorreoService.NotificarEvento(
                        ordenEntidad,
                        "PAGO_REGISTRADO",
                        string.IsNullOrWhiteSpace(orden.Correo) ? null : orden.Correo,
                        string.IsNullOrWhiteSpace(orden.NombreContribuyente) ? orden.Compania : orden.NombreContribuyente,
                        comprobanteAdjunto,
                        nombreAdjunto,
                        "Se registró un comprobante de pago para la orden y queda pendiente la validación del área Financiera.");

                    if (!resultadoCorreoPago.Exitoso)
                    {
                        TempData["Warning"] = "El pago fue registrado, pero la notificación no se pudo encolar: "
                            + (resultadoCorreoPago.Mensaje ?? "Error no especificado.");
                    }
                }
                catch
                {
                    // No bloquear el flujo si la notificación falla
                }

                    TempData["OK"] = estadoOrden.Equals(CapaDatos.Constants.EstadoOrden.Devuelta, StringComparison.OrdinalIgnoreCase)
                        ? "Respaldo actualizado reenviado. La orden vuelve a revisión financiera."
                        : "Comprobante enviado. La orden está en revisión financiera.";
                    return RedirectToAction("Detalles", new { id = id });
                }
            catch (Exception ex)
            {
                var numeroOrden = orden != null ? orden.NumeroOrden : "n/a";
                var codigoSol = orden != null ? orden.CodigoSolicitud : "n/a";
                CapaNegocio.LogBL.RegistrarError("Error registrando comprobante Orden=" + numeroOrden + " CodigoSolicitud=" + codigoSol, ex.ToString(), "OrdenRecaudacionController");
                TempData["Error"] = "Error interno al procesar el pago. Por favor contacte al administrador.";
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
                // TODO: AquÃ­ se debera guardar el motivo de la anulacin en la base de datos
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
        public ActionResult DescargarPdf(int id, bool vistaPrevia = false)
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
                var nombreArchivo = ConstruirNombrePdfOrdenRecaudacion(ordenModel);

                var pdf = new PartialViewAsPdf("OrdenRecaudacionPDF", pdfModel)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageMargins = new Rotativa.Options.Margins(0, 0, 0, 0),
                    CustomSwitches = PdfBrandingHelper.StandardRotativaSwitches
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreArchivo);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al generar PDF: " + ex.Message);
                TempData["ErrorMessage"] = "Error al generar el PDF.";
                return RedirectToAction("Detalles", new { id });
            }
        }

        [HttpGet]
        public ActionResult DescargarFactura(int id)
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
                var factura = _dao.ObtenerFacturaPagoPorOrden(id);
                if (factura == null || string.IsNullOrWhiteSpace(factura.FilePath))
                {
                    TempData["Error"] = "La factura aún no está disponible para descarga.";
                    return RedirectToAction("Detalles", new { id });
                }

                var rutaFisica = ResolverRutaArchivoRegistrado(factura.FilePath);
                if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
                {
                    TempData["Error"] = "No se encontró el archivo de factura registrado.";
                    return RedirectToAction("Detalles", new { id });
                }

                var nombreArchivo = EsPdfFactura(factura, rutaFisica)
                    ? ConstruirNombrePdfFactura(ordenModel, factura)
                    : (!string.IsNullOrWhiteSpace(factura.FileName)
                        ? factura.FileName
                        : Path.GetFileName(rutaFisica));
                var contentType = !string.IsNullOrWhiteSpace(factura.ContentType)
                    ? factura.ContentType
                    : MimeMapping.GetMimeMapping(nombreArchivo);

                return File(System.IO.File.ReadAllBytes(rutaFisica), contentType, nombreArchivo);
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError(
                    $"Error descargando factura de orden {id}",
                    ex.ToString(),
                    "OrdenRecaudacionController");
                TempData["Error"] = "No fue posible descargar la factura.";
                return RedirectToAction("Detalles", new { id });
            }
        }

        private string ResolverRutaArchivoRegistrado(string rutaArchivo)
        {
            if (string.IsNullOrWhiteSpace(rutaArchivo))
            {
                return null;
            }

            if (Path.IsPathRooted(rutaArchivo))
            {
                return Path.GetFullPath(rutaArchivo);
            }

            if (rutaArchivo.StartsWith("~"))
            {
                return Server.MapPath(rutaArchivo);
            }

            var basePath = FileStorageHelper.GetPhysicalBasePath(FileStorageHelper.BasePathStorage);
            return Path.Combine(basePath, rutaArchivo.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar));
        }

        private string ConstruirNombrePdfOrdenRecaudacion(OrdenRecaudacionModel ordenModel)
        {
            var numeroOrden = ordenModel != null && !string.IsNullOrWhiteSpace(ordenModel.NumeroOrden)
                ? ordenModel.NumeroOrden
                : (ordenModel != null ? ordenModel.Id.ToString() : string.Empty);
            var nombreOperador = ordenModel == null
                ? string.Empty
                : PdfFileNameHelper.PrimerValorNoVacio(
                    PdfFileNameHelper.CombinarSegmentos(ordenModel.RucCedula, ordenModel.Compania),
                    PdfFileNameHelper.CombinarSegmentos(ordenModel.RucCedula, ordenModel.NombreContribuyente),
                    ordenModel.Compania,
                    ordenModel.NombreContribuyente,
                    ordenModel.RucCedula);

            return PdfFileNameHelper.CrearNombreOrdenRecaudacion(numeroOrden, nombreOperador, ordenModel != null ? (DateTime?)ordenModel.FechaCreacion : (DateTime?)null);
        }

        private string ConstruirNombrePdfFactura(OrdenRecaudacionModel ordenModel, FacturaPagoRegistroModel factura)
        {
            var numeroFactura = factura != null && !string.IsNullOrWhiteSpace(factura.NumeroFactura)
                ? factura.NumeroFactura
                : (ordenModel != null && !string.IsNullOrWhiteSpace(ordenModel.NumeroOrden)
                    ? ordenModel.NumeroOrden
                    : (ordenModel != null ? ordenModel.Id.ToString() : string.Empty));
            var nombreOperador = ordenModel == null
                ? string.Empty
                : PdfFileNameHelper.PrimerValorNoVacio(
                    PdfFileNameHelper.CombinarSegmentos(ordenModel.RucCedula, ordenModel.Compania),
                    PdfFileNameHelper.CombinarSegmentos(ordenModel.RucCedula, ordenModel.NombreContribuyente),
                    ordenModel.Compania,
                    ordenModel.NombreContribuyente,
                    ordenModel.RucCedula);

            return PdfFileNameHelper.CrearNombreFactura(numeroFactura, nombreOperador, factura != null ? (DateTime?)factura.FechaEmision : (DateTime?)ordenModel.FechaCreacion);
        }

        private bool EsPdfFactura(FacturaPagoRegistroModel factura, string rutaFisica)
        {
            if (factura != null && string.Equals(factura.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(Path.GetExtension(rutaFisica), ".pdf", StringComparison.OrdinalIgnoreCase);
        }


        private CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFModel BuildOrdenRecaudacionPdfModel(OrdenRecaudacionModel ordenModel)
        {
            if (ordenModel == null)
            {
                return new CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFModel();
            }

            // Reutiliza los mismos fallbacks de la vista de Detalles para evitar campos vacios en el PDF.
            CompletarDatosOrdenParaVista(ordenModel);

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

            var detallesPdf = new List<CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFDetalleModel>();
            foreach (var d in detalles)
            {
                var subtotal = d.Subtotal > 0m
                    ? d.Subtotal
                    : Math.Round(d.Cantidad * d.ValorUnitario, 2, MidpointRounding.AwayFromZero);

                var porcentajeAdmin = NormalizarPorcentajeAdminPdf(d.PorcentajeAdmin);
                var adminCalculado = Math.Round(subtotal * (porcentajeAdmin / 100m), 2, MidpointRounding.AwayFromZero);
                var admin = adminCalculado;

                if (d.Admin > 0m)
                {
                    // Si no existe porcentaje confiable, conservar el valor guardado.
                    if (adminCalculado <= 0m)
                    {
                        admin = d.Admin;
                    }
                    else
                    {
                        // Si el valor guardado no cuadra (ej: 640 vs 6.40), prevalece el calculado.
                        admin = Math.Abs(d.Admin - adminCalculado) > 0.01m ? adminCalculado : d.Admin;
                    }
                }

                var totalLinea = d.TotalLinea;
                var totalEsperado = Math.Round(subtotal + admin, 2, MidpointRounding.AwayFromZero);
                if (totalLinea <= 0m || Math.Abs(totalLinea - totalEsperado) > 0.01m)
                {
                    totalLinea = totalEsperado;
                }

                detallesPdf.Add(new CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFDetalleModel
                {
                    Concepto = FirstNonEmpty(d.Descripcion, d.ConceptoNombre, d.ConceptoCodigo, "Concepto no especificado"),
                    Subtotal = subtotal,
                    Admin = admin,
                    TotalLinea = totalLinea
                });
            }

            var solicitud = ObtenerSolicitudParaPdf(ordenModel);
            var ultimoPago = _dao.ObtenerUltimoPagoPorOrden(ordenModel.Id);
            var bancoPago = ultimoPago?.BancoOrigen ?? ultimoPago?.MetodoPago;
            var numeroComp = ultimoPago?.NumeroComprobante ?? ultimoPago?.NumeroFactura;

            var referenciaSolicitud = FirstNonEmpty(solicitud?.NumeroSolicitud, ordenModel.CodigoSolicitud, "N/A");
            var pdfModel = new CapaPresentacion.Models.ViewModels.OrdenRecaudacionPDFModel
            {
                NumeroOrden = ordenModel.NumeroOrden,
                FechaEmision = ordenModel.FechaCreacion != default(DateTime) ? ordenModel.FechaCreacion : DateTime.Now,
                LugarEmision = FirstNonEmpty(solicitud?.Ciudad, ordenModel.LugarEmision, "Quito"),
                NombreCompania = FirstNonEmpty(solicitud?.RazonSocial, solicitud?.NombreOperador, ordenModel.Compania, ordenModel.NombreContribuyente, "No especificado"),
                Ruc = FirstNonEmpty(solicitud?.Ruc, ordenModel.RucCedula, "No especificado"),
                Email = FirstNonEmpty(solicitud?.Email, ordenModel.Correo, "No especificado"),
                Telefono = FirstNonEmpty(solicitud?.Telefono, ordenModel.Telefono, "No especificado"),
                Banco = string.IsNullOrWhiteSpace(bancoPago) ? "No especificado" : bancoPago,
                NumeroComprobante = string.IsNullOrWhiteSpace(numeroComp) ? "No registrado" : numeroComp,
                ConceptoPrincipal = FirstNonEmpty(detallesPdf.FirstOrDefault()?.Concepto, solicitud?.DescripcionOperacion, "Inspección y Certificación AOCR"),
                Referencia = $"Orden de recaudación {ordenModel.NumeroOrden} - Solicitud {referenciaSolicitud}",
                Detalles = detallesPdf
            };

            if (pdfModel.Detalles.Count == 0)
            {
                pdfModel.ValorBase = ordenModel.Subtotal != 0m
                    ? ordenModel.Subtotal
                    : (ordenModel.Total != 0m ? ordenModel.Total : 0m);
            }

            pdfModel.CalcularTotales();
            return pdfModel;
        }

        private CapaModelo.SolicitudAOCR ObtenerSolicitudParaPdf(OrdenRecaudacionModel ordenModel)
        {
            if (ordenModel == null)
            {
                return null;
            }

            var solicitudDAO = new CapaDatos.DAOs.SolicitudDAO();
            int codigoSolicitudInt;

            if (!string.IsNullOrWhiteSpace(ordenModel.CodigoSolicitud) &&
                int.TryParse(ordenModel.CodigoSolicitud, out codigoSolicitudInt) &&
                codigoSolicitudInt > 0)
            {
                return solicitudDAO.ObtenerPorId(codigoSolicitudInt);
            }

            if (!string.IsNullOrWhiteSpace(ordenModel.CodigoSolicitud))
            {
                codigoSolicitudInt = _dao.ObtenerCodigoSolicitudPorNumero(ordenModel.CodigoSolicitud);
                if (codigoSolicitudInt > 0)
                {
                    return solicitudDAO.ObtenerPorId(codigoSolicitudInt);
                }
            }

            if (!string.IsNullOrWhiteSpace(ordenModel.RucCedula))
            {
                codigoSolicitudInt = _dao.ObtenerCodigoSolicitudPorRuc(ordenModel.RucCedula);
                if (codigoSolicitudInt > 0)
                {
                    return solicitudDAO.ObtenerPorId(codigoSolicitudInt);
                }
            }

            return null;
        }

        private decimal NormalizarPorcentajeAdminPdf(decimal porcentaje)
        {
            if (porcentaje > 100m && porcentaje <= 10000m)
            {
                return porcentaje / 100m;
            }

            if (porcentaje > 0m && porcentaje <= 1m)
            {
                return porcentaje * 100m;
            }

            return porcentaje;
        }

        private string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private string ResolverNombreCompaniaDesdeFuentes(string codigoCompania)
        {
            if (string.IsNullOrWhiteSpace(codigoCompania))
            {
                return string.Empty;
            }

            var codigo = codigoCompania.Trim().ToUpperInvariant();

            try
            {
                var empresaMirror = _mirrorReadService.ObtenerCompaniaPorCodigo(codigo);
                if (empresaMirror != null && !string.IsNullOrWhiteSpace(empresaMirror.NombreCompania))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverNombreCompaniaDesdeFuentes[mirror]: codigo={codigo}, nombre={empresaMirror.NombreCompania.Trim()}");
                    return empresaMirror.NombreCompania.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverNombreCompaniaDesdeFuentes[mirror]: codigo={codigo}, error={ex.GetType().FullName}, msg={ex.Message}");
            }

            if (!EnableAs400RuntimeFallback)
            {
                return string.Empty;
            }

            try
            {
                var daoEmpresa = new EmpresaAS400DAO(new SecureConfig());
                var empresa = daoEmpresa.ObtenerEmpresaPorCodigo(codigo);
                if (empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverNombreCompaniaDesdeFuentes[as400]: codigo={codigo}, nombre={empresa.Nombre.Trim()}");
                    return empresa.Nombre.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverNombreCompaniaDesdeFuentes[as400]: codigo={codigo}, error={ex.GetType().FullName}, msg={ex.Message}");
            }

            return string.Empty;
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
                var numeroOrden = _ordenRecaudacionService.GenerarNumeroOrdenInstitucional(DateTime.Now.Year);
                
                result.AppendLine($"Generated: {numeroOrden}");
                result.AppendLine($"Anio: {DateTime.Now.Year}");
                
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
            
            // Agregar opció por defecto
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
                System.Diagnostics.Debug.WriteLine("GetUserId: No se encontró ID de usuario en la sesión");
            }
            return id;
        }

        private void PrefillDesdeUltimaOrden(int userId, CapaPresentacion.Models.OrdenRecaudacionNuevaVM model)
        {
            if (userId <= 0 || model?.Orden == null) return;

            try
            {
                var ultimaOrden = _dao.ListarPorUsuario(userId, null).FirstOrDefault();
                if (ultimaOrden == null) return;

                if (string.IsNullOrWhiteSpace(model.Orden.RucCedula) && !string.IsNullOrWhiteSpace(ultimaOrden.RucCedula))
                    model.Orden.RucCedula = ultimaOrden.RucCedula;

                if (string.IsNullOrWhiteSpace(model.Orden.Correo) && !string.IsNullOrWhiteSpace(ultimaOrden.Correo))
                    model.Orden.Correo = ultimaOrden.Correo;

                if (string.IsNullOrWhiteSpace(model.Orden.Telefono) && !string.IsNullOrWhiteSpace(ultimaOrden.Telefono))
                    model.Orden.Telefono = ultimaOrden.Telefono;
            }
            catch
            {
                // Ignorar errores de prefill por historial para no bloquear el formulario.
            }
        }

        private string ObtenerNombreCompaniaActiva(Usuario usuario)
        {
            try
            {
                var nombreSesion = (CompaniaActivaSessionHelper.ObtenerNombre(Session) ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(nombreSesion))
                {
                    return nombreSesion;
                }

                var codigoCompaniaActiva = (CompaniaActivaSessionHelper.ObtenerCodigo(Session) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(codigoCompaniaActiva))
                {
                    codigoCompaniaActiva = (usuario != null ? usuario.EmpresaCodigo : string.Empty) ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(codigoCompaniaActiva))
                {
                    var nombre = ResolverNombreCompaniaDesdeFuentes(codigoCompaniaActiva);
                    if (!string.IsNullOrWhiteSpace(nombre))
                    {
                        return nombre;
                    }
                }
            }
            catch
            {
                // no-op
            }

            return string.Empty;
        }

        private string ResolverLugarEmisionDesdeDb(int? codigoSolicitud, int codigoUsuario, string fallback = null)
        {
            try
            {
                string ciudadSolicitudFallback = null;
                string codCiudadSolicitud = null;

                if (codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
                {
                    var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud.Value);
                    if (solicitud != null)
                    {
                        codCiudadSolicitud = FirstNonEmpty(
                            NormalizarCodigoCiudad(solicitud.CodCiudad),
                            NormalizarCodigoCiudad(ObtenerCodCiudadSolicitudDesdePostgres(codigoSolicitud.Value)),
                            NormalizarCodigoCiudad(solicitud.Ciudad));

                        System.Diagnostics.Debug.WriteLine(
                            $"ResolverLugarEmisionDesdeDb: solicitud={codigoSolicitud.Value}, codCiudad={codCiudadSolicitud ?? "(null)"}, ciudadSolicitud={solicitud.Ciudad ?? "(null)"}");

                        var lugarDesdeMirror = ResolverEstacionDesdeMirror(codCiudadSolicitud);
                        if (!string.IsNullOrWhiteSpace(lugarDesdeMirror))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"ResolverLugarEmisionDesdeDb: estación mirror por solicitud={lugarDesdeMirror}");
                            return lugarDesdeMirror;
                        }

                        if (EnableOnDemandMirrorRefresh)
                        {
                            // Refresco opcional y controlado. Por defecto está desactivado para no acoplar el request web al proceso de sync.
                            TryRefreshUbicacionMirrorOnDemand("solicitud", codCiudadSolicitud);
                        }

                        lugarDesdeMirror = ResolverEstacionDesdeMirror(codCiudadSolicitud);
                        if (!string.IsNullOrWhiteSpace(lugarDesdeMirror))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"ResolverLugarEmisionDesdeDb: estación mirror tras refresh por solicitud={lugarDesdeMirror}");
                            return lugarDesdeMirror;
                        }

                        if (EnableAs400RuntimeFallback)
                        {
                            var lugarDesdeAs400 = ResolverEstacionDesdeAs400(codCiudadSolicitud);
                            if (!string.IsNullOrWhiteSpace(lugarDesdeAs400))
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"ResolverLugarEmisionDesdeDb: estación AS400 por solicitud={lugarDesdeAs400}");
                                return lugarDesdeAs400;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(solicitud.Ciudad))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"ResolverLugarEmisionDesdeDb: ciudad solicitud disponible (fallback diferido)={solicitud.Ciudad.Trim()}");
                            ciudadSolicitudFallback = solicitud.Ciudad.Trim();
                        }
                    }
                }

                if (codigoUsuario > 0)
                {
                    var codCiudadUsuario = FirstNonEmpty(
                        NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdePostgres(codigoUsuario)),
                        NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdeMirror(codigoUsuario)),
                        EnableAs400RuntimeFallback
                            ? NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdeAs400(codigoUsuario))
                            : null);

                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverLugarEmisionDesdeDb: usuario={codigoUsuario}, codCiudadUsuario={codCiudadUsuario ?? "(null)"}");

                    var lugarUsuarioDesdeMirror = ResolverEstacionDesdeMirror(codCiudadUsuario);
                    if (!string.IsNullOrWhiteSpace(lugarUsuarioDesdeMirror))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ResolverLugarEmisionDesdeDb: estación mirror por usuario={lugarUsuarioDesdeMirror}");
                        return lugarUsuarioDesdeMirror;
                    }

                    if (EnableOnDemandMirrorRefresh)
                    {
                        TryRefreshUbicacionMirrorOnDemand("usuario", codCiudadUsuario);
                    }

                    lugarUsuarioDesdeMirror = ResolverEstacionDesdeMirror(codCiudadUsuario);
                    if (!string.IsNullOrWhiteSpace(lugarUsuarioDesdeMirror))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ResolverLugarEmisionDesdeDb: estación mirror tras refresh por usuario={lugarUsuarioDesdeMirror}");
                        return lugarUsuarioDesdeMirror;
                    }

                    if (EnableAs400RuntimeFallback)
                    {
                        var lugarUsuarioDesdeAs400 = ResolverEstacionDesdeAs400(codCiudadUsuario);
                        if (!string.IsNullOrWhiteSpace(lugarUsuarioDesdeAs400))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"ResolverLugarEmisionDesdeDb: estación AS400 por usuario={lugarUsuarioDesdeAs400}");
                            return lugarUsuarioDesdeAs400;
                        }
                    }

                    var solicitudesUsuario = _solicitudDao.ObtenerPorUsuario(codigoUsuario) ?? Enumerable.Empty<SolicitudAOCR>();
                    var solicitudConCiudad = solicitudesUsuario
                        .FirstOrDefault(s => s != null && !string.IsNullOrWhiteSpace(s.Ciudad));

                    if (solicitudConCiudad != null && !string.IsNullOrWhiteSpace(solicitudConCiudad.Ciudad))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ResolverLugarEmisionDesdeDb: fallback ciudad última solicitud usuario={solicitudConCiudad.Ciudad.Trim()}");
                        return solicitudConCiudad.Ciudad.Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(ciudadSolicitudFallback))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverLugarEmisionDesdeDb: fallback ciudad solicitud final={ciudadSolicitudFallback}");
                    return ciudadSolicitudFallback;
                }

                if (!string.IsNullOrWhiteSpace(codCiudadSolicitud))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverLugarEmisionDesdeDb: fallback código ciudad solicitud={codCiudadSolicitud}");
                    return codCiudadSolicitud;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverLugarEmisionDesdeDb: error={ex.GetType().FullName}, msg={ex.Message}, solicitud={(codigoSolicitud.HasValue ? codigoSolicitud.Value.ToString() : "null")}, usuario={codigoUsuario}, fallbackEntrada={fallback ?? "(null)"}");
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverLugarEmisionDesdeDb: fallback final valor previo={fallback.Trim()}");
                return fallback.Trim();
            }

            System.Diagnostics.Debug.WriteLine("ResolverLugarEmisionDesdeDb: fallback final Quito");
            return "Quito";
        }

        private void TryRefreshUbicacionMirrorOnDemand(string origen, string codCiudad)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(codCiudad))
                {
                    return;
                }

                var enabled = string.Equals(
                    ConfigurationManager.AppSettings["Sync:Enabled"],
                    "true",
                    StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ConfigurationManager.AppSettings["Sync:Enabled"], "1", StringComparison.OrdinalIgnoreCase);

                if (!enabled)
                {
                    return;
                }

                // Evitar ejecutar sync por cada request: cooldown por tabla.
                if (!CanRunOnDemandSync("OPUARC01", TimeSpan.FromMinutes(5)) &&
                    !CanRunOnDemandSync("OIDAR2", TimeSpan.FromMinutes(5)))
                {
                    return;
                }

                var res1 = As400MirrorSyncJob.RunOnceTable("OPUARC01");
                var res2 = As400MirrorSyncJob.RunOnceTable("OIDAR2");

                MarkOnDemandSyncExecuted("OPUARC01");
                MarkOnDemandSyncExecuted("OIDAR2");

                System.Diagnostics.Debug.WriteLine(
                    $"TryRefreshUbicacionMirrorOnDemand: origen={origen}, codCiudad={codCiudad}, OPUARC01={res1?.Status ?? "N/A"}, OIDAR2={res2?.Status ?? "N/A"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"TryRefreshUbicacionMirrorOnDemand: origen={origen}, codCiudad={codCiudad}, error={ex.GetType().FullName}, msg={ex.Message}");
            }
        }

        private static bool CanRunOnDemandSync(string tableName, TimeSpan cooldown)
        {
            var now = DateTime.UtcNow;
            lock (MirrorSyncLock)
            {
                DateTime lastRun;
                if (!LastOnDemandSyncUtc.TryGetValue(tableName, out lastRun))
                {
                    return true;
                }

                return (now - lastRun) >= cooldown;
            }
        }

        private static void MarkOnDemandSyncExecuted(string tableName)
        {
            lock (MirrorSyncLock)
            {
                LastOnDemandSyncUtc[tableName] = DateTime.UtcNow;
            }
        }

        private static string ResolverEstacionDesdeAs400(string codCiudad)
        {
            var codCiudadNormalizado = NormalizarCodigoCiudad(codCiudad);
            if (string.IsNullOrWhiteSpace(codCiudadNormalizado))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverEstacionDesdeAs400: codCiudad inválido o vacío ({codCiudad ?? "(null)"}).");
                return null;
            }

            try
            {
                var daoUbicacion = CD_UbicacionUsuario.Instancia;

                var ubicacionUsuario = daoUbicacion.UbicacionUsuarioPorCiudad(codCiudadNormalizado);
                if (!string.IsNullOrWhiteSpace(ubicacionUsuario?.Estacion))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverEstacionDesdeAs400: estación por OPUARC01 para codCiudad={codCiudadNormalizado}, estacion={ubicacionUsuario.Estacion.Trim()}");
                    return ubicacionUsuario.Estacion.Trim();
                }

                var ubicacionAeropuerto = daoUbicacion.UbicacionAeropuertoUsuarioPorCiudad(codCiudadNormalizado);
                if (!string.IsNullOrWhiteSpace(ubicacionAeropuerto?.Estacion))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverEstacionDesdeAs400: estación por OIDAR2 para codCiudad={codCiudadNormalizado}, estacion={ubicacionAeropuerto.Estacion.Trim()}");
                    return ubicacionAeropuerto.Estacion.Trim();
                }

                System.Diagnostics.Debug.WriteLine(
                    $"ResolverEstacionDesdeAs400: sin filas útiles para codCiudad={codCiudadNormalizado}. Se aplicará fallback de ciudad.");
            }
            catch (IBM.Data.DB2.iSeries.iDB2ConversionException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverEstacionDesdeAs400: iDB2ConversionException codCiudad={codCiudadNormalizado}, msg={ex.Message}. Se aplicará fallback de ciudad.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverEstacionDesdeAs400: error inesperado en AS400 para codCiudad={codCiudadNormalizado}, error={ex.GetType().FullName}, msg={ex.Message}. Se aplicará fallback de ciudad.");
            }

            return null;
        }

        private string ResolverEstacionDesdeMirror(string codCiudad)
        {
            var codCiudadNormalizado = NormalizarCodigoCiudad(codCiudad);
            if (string.IsNullOrWhiteSpace(codCiudadNormalizado))
            {
                return null;
            }

            try
            {
                var estacion = _mirrorReadService.ObtenerEstacionPorCodigoCiudad(codCiudadNormalizado);
                if (!string.IsNullOrWhiteSpace(estacion))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ResolverEstacionDesdeMirror: codCiudad={codCiudadNormalizado}, estacion={estacion.Trim()}");
                    return estacion.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverEstacionDesdeMirror: error codCiudad={codCiudadNormalizado}, error={ex.GetType().FullName}, msg={ex.Message}");
            }

            return null;
        }

        private static string NormalizarCodigoCiudad(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }

            var codigo = valor.Trim().ToUpperInvariant();
            if (codigo.Length < 2 || codigo.Length > 10)
            {
                return null;
            }

            if (!codigo.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-'))
            {
                return null;
            }

            return codigo;
        }

        private string ObtenerCodCiudadSolicitudDesdePostgres(int codigoSolicitud)
        {
            return ObtenerCodCiudadDesdePostgres(
                "aocr_tbsolicitud",
                "codigo_solicitud = @id",
                cmd => cmd.Parameters.AddWithValue("@id", codigoSolicitud));
        }

        private string ObtenerCodCiudadUsuarioDesdePostgres(int codigoUsuario)
        {
            return ObtenerCodCiudadDesdePostgres(
                "usuario",
                "idusuario = @id",
                cmd => cmd.Parameters.AddWithValue("@id", codigoUsuario));
        }

        private string ObtenerCodCiudadUsuarioDesdeMirror(int codigoUsuario)
        {
            if (codigoUsuario <= 0)
            {
                return null;
            }

            try
            {
                var usuario = UsuarioDAO.ObtenerPorId(codigoUsuario);
                var claves = new List<string>();

                void Agregar(string valor)
                {
                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        claves.Add(valor.Trim());
                    }
                }

                Agregar(usuario?.CodigoUsuario);
                Agregar(usuario?.NombreUsuario);
                Agregar(ExtraerRucCedula(usuario?.CodigoUsuario));
                Agregar(ExtraerRucCedula(usuario?.NombreUsuario));

                var solicitudesUsuario = _solicitudDao.ObtenerPorUsuario(codigoUsuario) ?? Enumerable.Empty<SolicitudAOCR>();
                var solicitudConDatos = solicitudesUsuario.FirstOrDefault();
                if (solicitudConDatos != null)
                {
                    Agregar(solicitudConDatos.Ruc);
                    Agregar(solicitudConDatos.CedulaRepresentante);
                    Agregar(ExtraerRucCedula(solicitudConDatos.Ruc));
                    Agregar(ExtraerRucCedula(solicitudConDatos.CedulaRepresentante));
                }

                var codCiudad = _mirrorReadService.ObtenerCodigoCiudadPorClavesUsuario(
                    claves
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(codCiudad))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ObtenerCodCiudadUsuarioDesdeMirror: usuario={codigoUsuario}, codCiudad={codCiudad}");
                    return codCiudad.Trim().ToUpperInvariant();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ObtenerCodCiudadUsuarioDesdeMirror: usuario={codigoUsuario}, error={ex.GetType().FullName}, msg={ex.Message}");
            }

            return null;
        }

        private string ObtenerCodCiudadUsuarioDesdeAs400(int codigoUsuario)
        {
            if (codigoUsuario <= 0)
            {
                return null;
            }

            try
            {
                var codCiudadMirror = ObtenerCodCiudadUsuarioDesdeMirror(codigoUsuario);
                if (!string.IsNullOrWhiteSpace(codCiudadMirror))
                {
                    return codCiudadMirror;
                }

                var usuario = UsuarioDAO.ObtenerPorId(codigoUsuario);
                if (usuario == null)
                {
                    return null;
                }

                var as400Dao = new UsuarioAS400DAO(new SecureConfig());
                var candidatos = new List<string>();

                void AgregarCandidato(string valor)
                {
                    if (!string.IsNullOrWhiteSpace(valor))
                    {
                        candidatos.Add(valor.Trim());
                    }
                }

                AgregarCandidato(usuario.CodigoUsuario);
                AgregarCandidato(usuario.NombreUsuario);
                AgregarCandidato(ExtraerRucCedula(usuario.CodigoUsuario));
                AgregarCandidato(ExtraerRucCedula(usuario.NombreUsuario));

                var solicitudesUsuario = _solicitudDao.ObtenerPorUsuario(codigoUsuario) ?? Enumerable.Empty<SolicitudAOCR>();
                var ultimaSolicitud = solicitudesUsuario.FirstOrDefault();
                if (ultimaSolicitud != null)
                {
                    AgregarCandidato(ultimaSolicitud.Ruc);
                    AgregarCandidato(ultimaSolicitud.CedulaRepresentante);
                    AgregarCandidato(ExtraerRucCedula(ultimaSolicitud.Ruc));
                    AgregarCandidato(ExtraerRucCedula(ultimaSolicitud.CedulaRepresentante));
                }

                foreach (var candidato in candidatos
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var codCiudad = as400Dao.ObtenerCodigoCiudadPorCodigoUsuario(candidato);
                    if (!string.IsNullOrWhiteSpace(codCiudad))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ObtenerCodCiudadUsuarioDesdeAs400: match candidato={candidato}, codCiudad={codCiudad}");
                        return codCiudad.Trim();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ObtenerCodCiudadUsuarioDesdeAs400: usuario={codigoUsuario}, error={ex.GetType().FullName}, msg={ex.Message}");
                return null;
            }
        }

        private string ObtenerCodCiudadDesdePostgres(string tabla, string whereClause, Action<Npgsql.NpgsqlCommand> bindParams)
        {
            try
            {
                if (!TablaYWherePermitidos(tabla, whereClause))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"ObtenerCodCiudadDesdePostgres: acceso rechazado para tabla='{tabla}', where='{whereClause}'.");
                    return null;
                }

                var cs = ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                         ?? ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString;
                if (string.IsNullOrWhiteSpace(cs))
                {
                    return null;
                }

                using (var cn = new Npgsql.NpgsqlConnection(cs))
                {
                    cn.Open();
                    var columnas = ObtenerColumnasTablaPostgres(cn, tabla);
                    if (columnas.Count == 0)
                    {
                        return null;
                    }

                    var candidatas = new[]
                    {
                        "cod_ciudad",
                        "codigo_ciudad",
                        "ciudad_codigo",
                        "codigociudad",
                        "codigo_ciudad_adic",
                        "codigo_ciudad_adicional",
                        "usuco5",
                        "ciudad"
                    };
                    var disponibles = candidatas
                        .Where(c => columnas.Contains(c) && EsIdentificadorSeguro(c))
                        .ToList();
                    if (disponibles.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ObtenerCodCiudadDesdePostgres: tabla={tabla}, sin columnas candidatas de ciudad.");
                        return null;
                    }

                    var expr = string.Join(", ", disponibles.Select(c => string.Format("NULLIF(BTRIM(CAST({0} AS TEXT)), '')", c)));
                    var sql = string.Format("SELECT COALESCE({0}) FROM {1} WHERE {2} LIMIT 1", expr, tabla, whereClause);

                    using (var cmd = new Npgsql.NpgsqlCommand(sql, cn))
                    {
                        bindParams?.Invoke(cmd);
                        var value = cmd.ExecuteScalar();
                        return value == null || value == DBNull.Value
                            ? null
                            : value.ToString().Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ObtenerCodCiudadDesdePostgres: " + ex.Message);
                return null;
            }
        }

        private static bool TablaYWherePermitidos(string tabla, string whereClause)
        {
            if (string.IsNullOrWhiteSpace(tabla) || string.IsNullOrWhiteSpace(whereClause))
            {
                return false;
            }

            if (!EsIdentificadorSeguro(tabla))
            {
                return false;
            }

            string whereEsperado;
            return TablasCiudadPermitidas.TryGetValue(tabla.Trim(), out whereEsperado)
                   && string.Equals(whereClause.Trim(), whereEsperado, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsIdentificadorSeguro(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (var ch in value)
            {
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static HashSet<string> ObtenerColumnasTablaPostgres(Npgsql.NpgsqlConnection cn, string tabla)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_name = @tabla
                  AND table_schema NOT IN ('pg_catalog', 'information_schema')";

            using (var cmd = new Npgsql.NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!rd.IsDBNull(0))
                        {
                            columnas.Add(rd.GetString(0));
                        }
                    }
                }
            }

            return columnas;
        }

        private SolicitudAOCR ConstruirSolicitudAuto(
            int userId,
            Usuario usuario = null,
            string empresaNombreOverride = null,
            string rucCedulaOverride = null,
            string correoOverride = null,
            string telefonoOverride = null,
            string ciudadOverride = null)
        {
            if (userId <= 0) return null;

            usuario = usuario ?? UsuarioDAO.ObtenerPorId(userId);
            var blSolicitud = new SolicitudBL();
            var year = DateTime.Now.Year;
            var numero = blSolicitud.GenerarNumeroSolicitud(year);
            var empresaNombre = (empresaNombreOverride ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(empresaNombre))
            {
                try
                {
                    var codigoCompaniaActiva = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
                    if (string.IsNullOrWhiteSpace(codigoCompaniaActiva))
                    {
                        codigoCompaniaActiva = usuario != null ? usuario.EmpresaCodigo : string.Empty;
                    }

                    if (!string.IsNullOrWhiteSpace(codigoCompaniaActiva))
                    {
                        empresaNombre = ResolverNombreCompaniaDesdeFuentes(codigoCompaniaActiva);
                    }
                }
                catch
                {
                    empresaNombre = "";
                }
            }

            var rucCedula = string.IsNullOrWhiteSpace(rucCedulaOverride)
                ? ResolverRucCedulaDesdeFuentes(userId, usuario)
                : rucCedulaOverride.Trim();
            var codCiudad = FirstNonEmpty(
                NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdePostgres(userId)),
                NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdeMirror(userId)),
                EnableAs400RuntimeFallback
                    ? NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdeAs400(userId))
                    : null);
            if (string.IsNullOrWhiteSpace(codCiudad))
            {
                codCiudad = null;
            }
            var ciudadResolvida = string.IsNullOrWhiteSpace(ciudadOverride)
                ? FirstNonEmpty(
                    ResolverEstacionDesdeMirror(codCiudad),
                    EnableAs400RuntimeFallback ? ResolverEstacionDesdeAs400(codCiudad) : null,
                    codCiudad,
                    "Quito")
                : ciudadOverride.Trim();

            var solicitud = new SolicitudAOCR
            {
                NumeroSolicitud = numero,
                FechaSolicitud = DateTime.Now,
                TipoSolicitud = 1,
                Estado = EstadoSolicitud.Pendiente,
                CodigoUsuario = userId,
                NombreOperador = !string.IsNullOrWhiteSpace(empresaNombre)
                    ? empresaNombre
                    : (usuario?.NombreCompleto ?? usuario?.NombreUsuario ?? ""),
                Ruc = rucCedula,
                RazonSocial = empresaNombre,
                Email = !string.IsNullOrWhiteSpace(correoOverride) ? correoOverride.Trim() : (usuario?.Email ?? ""),
                Telefono = !string.IsNullOrWhiteSpace(telefonoOverride) ? telefonoOverride.Trim() : "",
                Direccion = "",
                Ciudad = ciudadResolvida,
                CodCiudad = codCiudad
            };

            return solicitud;
        }

        private string ResolverRucCedulaDesdeFuentes(int userId, Usuario usuario = null)
        {
            var candidatos = new List<string>();
            var codigoCompaniaActiva = (CompaniaActivaSessionHelper.ObtenerCodigo(Session) ?? string.Empty).Trim();
            var nombreCompaniaActiva = (CompaniaActivaSessionHelper.ObtenerNombre(Session) ?? string.Empty).Trim();

            void AgregarCandidato(string valor, bool desdeDb = false, string origen = null)
            {
                var normalizado = desdeDb
                    ? NormalizarIdentificacionDesdeDb(valor)
                    : ExtraerRucCedula(valor);

                if (!string.IsNullOrWhiteSpace(normalizado)
                    && !candidatos.Any(c => string.Equals(c, normalizado, StringComparison.OrdinalIgnoreCase)))
                {
                    candidatos.Add(normalizado);
                    if (!string.IsNullOrWhiteSpace(origen))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"ResolverRucCedulaDesdeFuentes[{origen}]: userId={userId}, candidato={normalizado}");
                    }
                }
            }

            void LogPasoError(string paso, Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ResolverRucCedulaDesdeFuentes[{paso}]: userId={userId}, error={ex.GetType().FullName}, msg={ex.Message}");
            }

            bool CompaniaCoincide(SolicitudAOCR solicitud, string codigoCompania)
            {
                if (solicitud == null || string.IsNullOrWhiteSpace(codigoCompania))
                {
                    return false;
                }

                var codigo = codigoCompania.Trim();
                if (!string.IsNullOrWhiteSpace(solicitud.CodigoOaci) &&
                    string.Equals((solicitud.CodigoOaci ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var lista = solicitud.CompaniasSeleccionadas ?? string.Empty;
                if (string.IsNullOrWhiteSpace(lista))
                {
                    if (string.IsNullOrWhiteSpace(solicitud.CodigoOaci))
                    {
                        // Compatibilidad con solicitudes legacy sin marca explícita de compañía.
                        if (!string.IsNullOrWhiteSpace(nombreCompaniaActiva))
                        {
                            var nombreSolicitud = FirstNonEmpty(
                                solicitud.NombreComercial,
                                solicitud.NombreOperador,
                                solicitud.RazonSocial);

                            if (!string.IsNullOrWhiteSpace(nombreSolicitud) &&
                                string.Equals(nombreSolicitud.Trim(), nombreCompaniaActiva, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }

                        return true;
                    }

                    return false;
                }

                return lista
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => (x ?? string.Empty).Trim())
                    .Any(x => x.Equals(codigo, StringComparison.OrdinalIgnoreCase));
            }

            // 1) Prioridad funcional: RUC/Cédula de la compañía activa seleccionada.
            try
            {
                if (userId > 0 && (!string.IsNullOrWhiteSpace(codigoCompaniaActiva) || !string.IsNullOrWhiteSpace(nombreCompaniaActiva)))
                {
                    var solicitudesUsuario = _solicitudDao.ObtenerPorUsuario(userId) ?? Enumerable.Empty<SolicitudAOCR>();
                    var solicitudCompaniaActiva = solicitudesUsuario
                        .Where(s => CompaniaCoincide(s, codigoCompaniaActiva))
                        .FirstOrDefault(s =>
                            s != null && (
                                !string.IsNullOrWhiteSpace(NormalizarIdentificacionDesdeDb(s.Ruc)) ||
                                !string.IsNullOrWhiteSpace(NormalizarIdentificacionDesdeDb(s.CedulaRepresentante))));

                    if (solicitudCompaniaActiva != null)
                    {
                        AgregarCandidato(solicitudCompaniaActiva.Ruc, true, "compania_activa.solicitud.ruc");
                        AgregarCandidato(solicitudCompaniaActiva.CedulaRepresentante, true, "compania_activa.solicitud.cedula");
                    }

                    // Si no existe una solicitud del usuario para la compañía activa,
                    // buscar identificación reciente por compañía (global), priorizando al usuario actual.
                    var identificacionCompania = _solicitudDao.ObtenerIdentificacionRecientePorCompania(
                        codigoCompaniaActiva,
                        nombreCompaniaActiva,
                        userId);
                    AgregarCandidato(identificacionCompania, true, "compania_activa.global");

                    // Respaldo desde mirror FR3 (AS400 replicado): opcc08(codigo_oaci_cia) + opcru1(ruc).
                    var rucCompaniaMirror = _mirrorReadService.ObtenerRucCompaniaPorCodigo(
                        codigoCompaniaActiva,
                        nombreCompaniaActiva);
                    AgregarCandidato(rucCompaniaMirror, true, "compania_activa.mirror_fr3");
                }
            }
            catch (Exception ex)
            {
                LogPasoError("compania_activa", ex);
            }

            // 2) Fuente principal de respaldo: identificación registrada del usuario en AOCR.
            //    Prioriza cédulaidentificacion y luego identificación tributaria/RUC según esquema.
            try
            {
                if (userId > 0)
                {
                    var identificacionPrincipal = UsuarioDAO.ObtenerIdentificacionPrincipal(userId);
                    AgregarCandidato(identificacionPrincipal, true, "usuario.registro");
                }
            }
            catch (Exception ex)
            {
                LogPasoError("usuario.registro", ex);
            }

            try
            {
                if (userId > 0)
                {
                    var solicitudesUsuario = _solicitudDao.ObtenerPorUsuario(userId) ?? Enumerable.Empty<SolicitudAOCR>();
                    var solicitudConRuc = solicitudesUsuario
                        .FirstOrDefault(s =>
                            s != null && (
                                !string.IsNullOrWhiteSpace(NormalizarIdentificacionDesdeDb(s.Ruc)) ||
                                !string.IsNullOrWhiteSpace(NormalizarIdentificacionDesdeDb(s.CedulaRepresentante))));

                    if (solicitudConRuc != null)
                    {
                        AgregarCandidato(solicitudConRuc.Ruc, true, "solicitud.ruc");
                        AgregarCandidato(solicitudConRuc.CedulaRepresentante, true, "solicitud.cedula");
                    }
                }
            }
            catch (Exception ex)
            {
                LogPasoError("solicitud", ex);
            }

            try
            {
                if (userId > 0)
                {
                    var rtDao = new RTDao();
                    var solicitudRt = rtDao.GetSolicitudByUsuario(userId);
                    if (solicitudRt != null && solicitudRt.CompaniaId > 0)
                    {
                        var companiaRt = rtDao.GetCompaniaById(solicitudRt.CompaniaId);
                        if (companiaRt != null)
                        {
                            AgregarCandidato(companiaRt.Ruc, true, "rt.compania");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogPasoError("rt", ex);
            }

            try
            {
                if (userId > 0)
                {
                    var ordenesUsuario = _dao.ListarPorUsuario(userId, null) ?? Enumerable.Empty<OrdenRecaudacion>();
                    var ultimaOrden = ordenesUsuario
                        .FirstOrDefault(o => o != null && !string.IsNullOrWhiteSpace(o.RucCedula));

                    if (ultimaOrden != null)
                    {
                        AgregarCandidato(ultimaOrden.RucCedula, true, "orden");
                    }
                }
            }
            catch (Exception ex)
            {
                LogPasoError("orden", ex);
            }

            try
            {
                if (usuario == null && userId > 0)
                {
                    usuario = UsuarioDAO.ObtenerPorId(userId);
                }

                if (usuario != null)
                {
                    AgregarCandidato(usuario.Ruc, true, "usuario.ruc");
                    AgregarCandidato(usuario.CodigoUsuario, false, "usuario.codigo");
                    AgregarCandidato(usuario.NombreUsuario, false, "usuario.nombre");
                }
            }
            catch (Exception ex)
            {
                LogPasoError("usuario", ex);
            }

            try
            {
                if (usuario == null && userId > 0)
                {
                    usuario = UsuarioDAO.ObtenerPorId(userId);
                }

                if (usuario != null)
                {
                    var claves = new[]
                    {
                        usuario.CodigoUsuario,
                        usuario.NombreUsuario,
                        ExtraerRucCedula(usuario.CodigoUsuario),
                        ExtraerRucCedula(usuario.NombreUsuario)
                    }
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                    var identificacionMirror = _mirrorReadService.ObtenerIdentificacionPorClavesUsuario(claves);
                    if (identificacionMirror != null)
                    {
                        AgregarCandidato(identificacionMirror.Ruc, true, "mirror.ruc");
                        AgregarCandidato(identificacionMirror.Cedula, true, "mirror.cedula");

                        System.Diagnostics.Debug.WriteLine(
                            $"ResolverRucCedulaDesdeFuentes[mirror]: userId={userId}, ruc={identificacionMirror.Ruc ?? "(null)"}, cedula={identificacionMirror.Cedula ?? "(null)"}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogPasoError("mirror", ex);
            }

            if (EnableAs400RuntimeFallback)
            {
                try
                {
                    if (usuario == null && userId > 0)
                    {
                        usuario = UsuarioDAO.ObtenerPorId(userId);
                    }

                    if (usuario != null)
                    {
                        var as400Dao = new UsuarioAS400DAO(new SecureConfig());
                        var claves = new[] { usuario.CodigoUsuario, usuario.NombreUsuario }
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Select(v => v.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        foreach (var clave in claves)
                        {
                            var ruc = as400Dao.ObtenerNumeroRucPorCodigoUsuario(clave);
                            var cedula = as400Dao.ObtenerCedulaPorCodigoUsuario(clave);

                            AgregarCandidato(ruc, true, "as400.ruc");
                            AgregarCandidato(cedula, true, "as400.cedula");

                            System.Diagnostics.Debug.WriteLine(
                                $"ResolverRucCedulaDesdeFuentes[as400]: userId={userId}, clave={clave}, ruc={ruc ?? "(null)"}, cedula={cedula ?? "(null)"}");
                        }

                        if (claves.Count == 0)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"ResolverRucCedulaDesdeFuentes[as400]: userId={userId}, sin claves de consulta para AS400.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogPasoError("as400", ex);
                }
            }

            var resultado = candidatos.FirstOrDefault() ?? string.Empty;
            System.Diagnostics.Debug.WriteLine(
                $"ResolverRucCedulaDesdeFuentes: userId={userId}, totalCandidatos={candidatos.Count}, resultado={resultado}, candidatos={string.Join("|", candidatos.Take(5))}");
            return resultado;
        }

        private static bool AppFlagEnabled(string key, bool defaultValue)
        {
            try
            {
                var raw = ConfigurationManager.AppSettings[key];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return defaultValue;
                }

                return string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return defaultValue;
            }
        }

        private string NormalizarIdentificacionDesdeDb(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return "";
            }

            var texto = valor.Trim();
            var soloDigitos = new string(texto.Where(char.IsDigit).ToArray());

            // Formato local preferido (cédula/RUC EC)
            if (soloDigitos.Length == 10 || soloDigitos.Length == 13)
            {
                return soloDigitos;
            }

            // Fallback para identificaciones no-EC guardadas en DB/AS400
            // (ej. pasaporte, tax-id extranjero, etc.)
            var compacto = new string(texto.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '/').ToArray());
            if (compacto.Length >= 5 && compacto.Length <= 20)
            {
                return compacto.ToUpperInvariant();
            }

            return "";
        }

        private string ExtraerRucCedula(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return "";
            var limpio = new string(valor.Where(char.IsDigit).ToArray());
            if (limpio.Length == 10 || limpio.Length == 13)
                return limpio;
            return "";
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

        private Task CargarViewBagsParaNueva()
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
            return Task.CompletedTask;
        }

        // Mï¿½todo helper con tipo correcto:
        private void EnviarNotificacionAFinanciero(OrdenRecaudacionModel orden, CapaDatos.Models.PagoModel pago, string emailFinanciero, string comprobanteRuta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(emailFinanciero)) return;

                CapaNegocio.LogBL.RegistrarInfo($"Notificando financiero: Orden={orden.NumeroOrden} CodigoSolicitud={orden.CodigoSolicitud}", "OrdenRecaudacionController");

                var config = new SecureConfig();
                var emailSvc = new EmailSvc(config);

                var asunto = string.Format("Nueva Orden Pendiente de Revisión - {0}", orden.NumeroOrden);
                var cuerpo = string.Format(@"
                    <h2>Nueva Orden Pendiente de Revisión</h2>
                    <p><strong>Número de Orden:</strong> {0}</p>
                    <p><strong>Contribuyente:</strong> {1}</p>
                    <p><strong>Monto:</strong> ${2:N2}</p>
                    <p><strong>Método de Pago:</strong> {3}</p>",
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
                CapaNegocio.LogBL.RegistrarError($"Error enviando notificació a financiero Orden={orden?.NumeroOrden} CodigoSolicitud={orden?.CodigoSolicitud}", ex.ToString(), "OrdenRecaudacionController");
            }
        }

        /// <summary>
        /// Validar un pago específico
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Financiero")]
        public ActionResult ValidarPago(int ordenId, int pagoId)
        {
            try
            {
                string usuario = User.Identity.Name ?? "SISTEMA";

                // Usa la transacción completa: actualiza pago → actualiza orden → actualiza solicitud
                string err;
                var resultado = _dao.ActualizarPagoYEstadoTransaccional(
                    ordenId,
                    pagoId,
                    CapaDatos.Constants.EstadoPago.Validado,
                    usuario,
                    "Pago validado por " + usuario,
                    CapaDatos.Constants.EstadoOrden.Facturada,
                    out err);

                if (resultado)
                {
                    new AocrPostPagoWorkflowService().ProcesarPagoAprobado(ordenId, usuario);
                    TempData["Success"] = "Pago validado correctamente. Orden actualizada a FACTURADA.";

                    // Intentar notificación por email (no bloqueante)
                    try
                    {
                        var ordenActualizada = _dao.ObtenerOrdenPorId(ordenId);
                        if (ordenActualizada != null)
                        {
                            var pdf = new CapaPresentacion.Services.PdfGeneratorService()
                                          .GenerarOrdenRecaudacionPDF(ordenActualizada);
                            new EmailSvc().EnviarFacturaGenerada(ordenActualizada, pdf);
                        }
                    }
                    catch (Exception exNotif)
                    {
                        CapaNegocio.LogBL.RegistrarAdvertencia(
                            $"ValidarPago: email/pdf no crítico. OrdenId={ordenId}. {exNotif.Message}",
                            "OrdenRecaudacionController");
                    }
                }
                else
                {
                    TempData["Error"] = "No se pudo validar el pago." +
                        (string.IsNullOrWhiteSpace(err) ? "" : " Detalle: " + err);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al validar pago: " + ex.Message;
                CapaNegocio.LogBL.RegistrarError(
                    $"ValidarPago: excepcion. OrdenId={ordenId} PagoId={pagoId}",
                    ex.ToString(),
                    "OrdenRecaudacionController");
            }

            return RedirectToAction("Detalles", new { id = ordenId });
        }

        /// <summary>
        /// Rechazar un pago específico
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                var resultado = _dao.ActualizarPagoEstadoPorId(
                    ordenId,
                    pagoId,
                    CapaDatos.Constants.EstadoPago.Rechazado,
                    usuario,
                    motivo);
                
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
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO(new SecureConfig());
                var resultado = bancoPDao.ProbarConexionAS400();
                
                if (resultado.StartsWith("OK"))
                {
                    TempData["OK"] = $"Conexió AS400 exitosa: {resultado}";
                }
                else
                {
                    TempData["Error"] = $"Error en conexió AS400: {resultado}";
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
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO(new SecureConfig());
                var resultado = bancoPDao.VerificarDriverODBC();
                
                if (resultado.StartsWith("âœ…"))
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
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO(new SecureConfig());
                var resultado = bancoPDao.ListarDriversODBC();
                return Content(resultado, "text/plain");
            }
            catch (Exception ex)
            {
                return Content($"Error listando drivers: {ex.Message}", "text/plain");
            }
        }

        private static void NormalizarMontosPagoDesfasados(List<CapaDatos.Models.PagoModel> pagos, decimal totalOrden)
        {
            if (pagos == null || pagos.Count == 0 || totalOrden <= 0m)
            {
                return;
            }

            foreach (var pago in pagos)
            {
                if (pago == null || pago.Monto <= totalOrden * 10m)
                {
                    continue;
                }

                var montoNormalizado = Math.Round(pago.Monto / 100m, 2);
                if (montoNormalizado > 0m && montoNormalizado <= Math.Round(totalOrden * 1.01m, 2))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"OrdenRecaudacion/Detalles: monto de pago normalizado por desfase x100. PagoId={pago.CodigoPago}, original={pago.Monto}, normalizado={montoNormalizado}");
                    pago.Monto = montoNormalizado;
                }
            }
        }
    }
}
