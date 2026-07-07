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
using CapaPresentacion.Filters;
using CapaPresentacion.Models;
using CapaPresentacion.Models.ViewModels;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using CapaModelo;
using CapaNegocio.Services;
using CapaNegocio.Helpers;
using CapaNegocio.Integraciones.As400Sync;
using iTextSharp.text.pdf;
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
        private const string CodigoConceptoInspeccionExt = "INSPECCION_EXT";
        private const string TipoSolicitudInspeccionGenerada = "SOLICITUD_INSPECCION_EXT";
        private const string TipoSolicitudInspeccionFirmada = "SOLICITUD_INSPECCIONES_FIRMADA";
        private const string LogSolicitudInspeccionExt = "[SOLICITUD_INSPECCION_EXT]";
        private const string LogSolicitudInspeccionPdf = "[SOLICITUD_INSPECCION_PDF]";
        private const string MensajeSolicitudInspeccionPendiente = "Debe generar, firmar y cargar la Solicitud de Inspecciones antes de generar la orden.";
        private const string MensajeSolicitudInspeccionFirmadaFaltante = "No se puede generar la orden porque falta cargar la Solicitud de Inspecciones firmada.";
        private const string MensajeSolicitudInspeccionSoloLectura = "No se puede modificar la Solicitud de Inspecciones porque la orden ya fue generada.";
        private const string MensajeSolicitudInspeccionPreliminarBloqueada = "No se puede acceder a la Solicitud de Inspecciones preliminar porque la orden ya fue generada.";
        private const string MensajeSolicitudInspeccionSoloLecturaSinFirmado = "La orden ya no permite edición y no se encontró la solicitud firmada. Revise el expediente documental.";
        private const string MensajeSolicitudInspeccionYaGenerada = "La Solicitud de Inspecciones ya fue generada. Descárguela y cargue la versión firmada para continuar.";
        private const string MensajeSolicitudInspeccionPendienteCargaFirmada = "La solicitud ya fue generada. Descargue el PDF, fírmelo externamente y cargue la versión firmada para continuar. No se pueden agregar nuevas acciones, conceptos o inspecciones adicionales a esta solicitud.";
        private const string MensajeSolicitudInspeccionPendienteConReapertura = "La solicitud ya fue generada. Si necesita agregar nuevas acciones, debe rechazar la generación actual antes de cargar el PDF firmado.";
        private const string MensajeSolicitudInspeccionReaperturaExito = "La generación fue rechazada. Puede seguir agregando acciones, conceptos o inspecciones a la orden.";
        private const string AccionReaperturaPorAgregarAcciones = "REAPERTURA_POR_AGREGAR_ACCIONES";
        private const string MensajeSolicitudInspeccionGeneradaExito = "La Solicitud de Inspecciones fue generada correctamente. Descárguela, fírmela externamente y cargue la versión firmada para continuar. No se pueden agregar nuevas acciones, conceptos o inspecciones adicionales a esta solicitud.";
        private const string MensajeSolicitudInspeccionModoSoloLectura = "Documento disponible en modo solo lectura.";

        private OrdenRecaudacionDAO _ordenDAO;
        private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();
        private readonly AocrCompaniaContextService _companiaContextService = new AocrCompaniaContextService();
        private readonly AocrProcesoActivoService _procesoActivoService = new AocrProcesoActivoService();
        private readonly DocumentoDAO _documentoDao = new DocumentoDAO();
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
            var selectedRoleCookie = AuthTicketRoleDataHelper.ReadSelectedRoleFromCookie(Request != null ? Request.Cookies : null);
            var authTicket = System.Web.Security.FormsAuthentication.Decrypt(
                Request != null && Request.Cookies != null
                    ? Request.Cookies[System.Web.Security.FormsAuthentication.FormsCookieName]?.Value
                    : null);
            var authTicketRoleData = AuthTicketRoleDataHelper.Deserialize(authTicket != null ? authTicket.UserData : null);
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
            diagnostico.AppendLine($"Cookie['{AuthTicketRoleDataHelper.SelectedRoleCookieName}']: {selectedRoleCookie ?? "null"}");
            diagnostico.AppendLine($"FormsAuth.UserData: {(authTicket != null ? authTicket.UserData : "null")}");
            diagnostico.AppendLine($"FormsAuth.SelectedRole: {authTicketRoleData.SelectedRole ?? "null"}");
            diagnostico.AppendLine($"FormsAuth.Roles: {(authTicketRoleData.Roles != null && authTicketRoleData.Roles.Any() ? string.Join(", ", authTicketRoleData.Roles) : "null")}");
            
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

            var esAdministrador = User != null && User.IsInRole("Administrador");
            int? idUsuarioFiltro = esAdministrador ? (int?)null : idUsuario;

            CargarEstadosCombo(estado);
            CargarContinuidadOrdenUsuario(idUsuario);

            var ordenes = _dao.ListarPorUsuarioModel(idUsuarioFiltro, estado) ?? new List<OrdenRecaudacionModel>();
            if (!esAdministrador && idUsuario > 0)
            {
                ordenes = FiltrarOrdenesModelPorCompaniaActiva(ordenes, idUsuario);
            }

            // Estadï¿½sticas: tu view espera claves con mayï¿½scula
            var est = _dao.ObtenerEstadisticas(idUsuarioFiltro);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);

            return View(ordenes);
        }

        // GET: /OrdenRecaudacion/Obligatoria
        public ActionResult Obligatoria(string estado = null)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Obligatoria: Usuario no autenticado, redirigiendo a login");
                return RedirectToAction("Login", "Account");
            }

            System.Diagnostics.Debug.WriteLine($"Obligatoria: Usuario ID = {idUsuario}");

            var esAdministrador = User != null && User.IsInRole("Administrador");
            int? idUsuarioFiltro = esAdministrador ? (int?)null : idUsuario;

            CargarEstadosCombo(estado);
            CargarContinuidadOrdenUsuario(idUsuario);

            var ordenes = _dao.ListarPorUsuario(idUsuarioFiltro, estado) ?? new List<OrdenRecaudacion>();
            if (!esAdministrador && idUsuario > 0)
            {
                ordenes = FiltrarOrdenesPorCompaniaActiva(ordenes, idUsuario);
            }
            System.Diagnostics.Debug.WriteLine(string.Format("Obligatoria: Se encontraron {0} Órdenes", ordenes.Count));

            // Estadisticas
            var est = _dao.ObtenerEstadisticas(idUsuarioFiltro);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);

            return View(ordenes);
        }

        private void CargarContinuidadOrdenUsuario(int idUsuario)
        {
            OrdenRecaudacionModel ordenPendiente = null;
            var scope = RtCompaniaScope.FromSession(Session, idUsuario);
            if (!string.IsNullOrWhiteSpace(scope.CodigoCompania))
            {
                var entity = _procesoActivoService.ObtenerOrdenPendienteAccionPorCompania(
                    idUsuario,
                    scope.CodigoCompania,
                    scope.NombreCompania);
                if (entity != null && entity.Id > 0)
                {
                    ordenPendiente = _dao.ObtenerOrdenPorIdModel(entity.Id);
                }
            }
            else
            {
                ordenPendiente = _dao.ObtenerOrdenPendienteUsuarioAccion(idUsuario);
            }

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

            var scope = RtCompaniaScope.FromSession(Session, userId);
            scope.PublicarEnViewBag(this);

            if (!scope.TieneCompaniaActivaValida())
            {
                TempData["Error"] = "Debe seleccionar una compañía activa válida antes de crear una orden.";
                return RedirectToAction("SeleccionarCompania", "Account", new { returnUrl = Url.Action("Nueva", "OrdenRecaudacion") });
            }

            var bloqueoProceso = scope.EvaluarBloqueoNuevaOrden();
            if (bloqueoProceso.Bloqueado)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = bloqueoProceso.Mensaje;
                return RedirectToAction(bloqueoProceso.Action, bloqueoProceso.Controller, bloqueoProceso.RouteValues);
            }

            var ordenPendienteEntity = _procesoActivoService.ObtenerOrdenPendienteAccionPorCompania(
                userId,
                scope.CodigoCompania,
                scope.NombreCompania);
            if (ordenPendienteEntity != null)
            {
                var estadoPendiente = EstadoOrden.NormalizarEstado(ordenPendienteEntity.Estado);
                var requiereComprobante = estadoPendiente == EstadoOrden.Pendiente ||
                                          estadoPendiente == EstadoOrden.Generada ||
                                          estadoPendiente == EstadoOrden.Devuelta;
                TempData["OK"] = requiereComprobante
                    ? "Ya existe una orden pendiente de comprobante para esta compañía. Continúe con esa orden antes de crear otra."
                    : "Ya existe una orden en borrador para esta compañía. Continúe con esa orden antes de crear otra.";
                return RedirectToAction("Detalles", new { id = ordenPendienteEntity.Id, abrirPago = requiereComprobante });
            }

            var model = new CapaPresentacion.Models.OrdenRecaudacionNuevaVM();
            AplicarCompaniaActivaAlModelo(model, userId, scope);
            PrepararNuevaOrdenViewModel(model);
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
                    PrepararNuevaOrdenViewModel(model);
                    return View(model);
                }

                if (model != null)
                {
                    model.AeropuertosSolicitados = (model.AeropuertosSolicitados ?? string.Empty).Trim().ToUpperInvariant();
                }

                var scope = RtCompaniaScope.FromSession(Session, idUsuario);
                var bloqueoProceso = scope.EvaluarBloqueoNuevaOrden();
                if (bloqueoProceso.Bloqueado)
                {
                    TempData["NotificacionTipo"] = "warning";
                    TempData["NotificacionMensaje"] = bloqueoProceso.Mensaje;
                    return RedirectToAction(bloqueoProceso.Action, bloqueoProceso.Controller, bloqueoProceso.RouteValues);
                }

                var ordenPendiente = _procesoActivoService.ObtenerOrdenPendienteAccionPorCompania(
                    idUsuario,
                    scope.CodigoCompania,
                    scope.NombreCompania);
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

                if (!scope.TieneCompaniaActivaValida())
                {
                    ModelState.AddModelError("", _companiaContextService.ObtenerMensajeAccesoDenegadoCompania());
                    PrepararNuevaOrdenViewModel(model);
                    return View(model);
                }

                // PROCESO 1 — Crear orden: la fuente de verdad es la sesión del servidor.
                // No validar CompaniaId / token / hidden del formulario (pueden quedar vacíos o desactualizados).
                AplicarCompaniaActivaAlModelo(model, idUsuario, scope);
                var companiaActiva = ObtenerCompaniaActivaDesdeSesion(idUsuario);
                if (!companiaActiva.EsValida)
                {
                    ModelState.AddModelError("", _companiaContextService.ObtenerMensajeAccesoDenegadoCompania());
                    PrepararNuevaOrdenViewModel(model);
                    return View(model);
                }

                RegistrarTrazaOrdenPdf(
                    "NuevaOrdenGuardar",
                    idUsuario,
                    null,
                    companiaActiva.Codigo,
                    model?.CompaniaActivaCodigo,
                    model?.CompaniaActivaContextToken,
                    0,
                    companiaActiva.Codigo,
                    "BORRADOR_NUEVO");

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

                string errorConceptosObligatorios;
                if (!ValidarDetallesConceptosObligatoriosOrdenNueva(detalles, out errorConceptosObligatorios))
                {
                    ModelState.AddModelError("", errorConceptosObligatorios);
                    PrepararNuevaOrdenViewModel(model);
                    return View(model);
                }

                var requiereSolicitudInspeccion = detalles.Any(det =>
                {
                    var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
                    return EsConceptoInspeccionExt(concepto?.Codigo);
                });

                if (model.GenerarSolicitudInspeccionAlGuardar && !requiereSolicitudInspeccion)
                {
                    model.GenerarSolicitudInspeccionAlGuardar = false;
                    ModelState.AddModelError("", "La orden debe contener el concepto INSPECCION_EXT para generar la Solicitud de Inspecciones.");
                    PrepararNuevaOrdenViewModel(model, false);
                    return View(model);
                }

                if (model.GenerarSolicitudInspeccionAlGuardar && string.IsNullOrWhiteSpace(model.AeropuertosSolicitados))
                {
                    model.GenerarSolicitudInspeccionAlGuardar = false;
                    ModelState.AddModelError("AeropuertosSolicitados", "Debe ingresar los aeropuertos solicitados antes de generar la Solicitud de Inspecciones.");
                    PrepararNuevaOrdenViewModel(model, requiereSolicitudInspeccion);
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
                    PrepararNuevaOrdenViewModel(model, requiereSolicitudInspeccion);
                    return View(model);
                }
                model.Orden.RucCedula = ExtraerRucCedula(rucDesdeDb);

                model.Orden.Compania = _companiaContextService.FormatearTextoCompaniaOrden(companiaActiva.Codigo, companiaActiva.Nombre);
                model.Orden.NombreContribuyente = companiaActiva.Nombre;

                var codigoSolicitud = int.TryParse(model.Orden?.CodigoSolicitud?.ToString(), out int cs) ? (int?)cs : null;
                var numeroSolicitudGop = ObtenerNumeroSolicitudGop(codigoSolicitud);
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: idUsuario = {idUsuario}");

                var numeroOrden = await GenerarNumeroOrdenAsync(numeroSolicitudGop, codigoSolicitud);
                System.Diagnostics.Debug.WriteLine($"Controller Nueva: numeroOrden generado = {numeroOrden}; numeroSolicitudGop = {numeroSolicitudGop}");
                var lugarEmisionDb = ResolverLugarEmisionDesdeDb(codigoSolicitud, idUsuario);

                var orden = new OrdenRecaudacion
                {
                    NumeroOrden = numeroOrden,
                    CodigoUsuario = idUsuario,
                    CodigoSolicitud = codigoSolicitud,
                    LugarEmision = lugarEmisionDb,
                    Compania = _companiaContextService.FormatearTextoCompaniaOrden(companiaActiva.Codigo, companiaActiva.Nombre),
                    CompaniaCodigo = companiaActiva.Codigo,
                    NombreContribuyente = companiaActiva.Nombre,
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

                        if (EsConceptoInspeccionExt(detalle.ConceptoCodigo))
                        {
                            CapaNegocio.LogBL.RegistrarInfo(
                                $"{LogSolicitudInspeccionExt} OrdenId={ordenId} CodigoConcepto={CodigoConceptoInspeccionExt} Usuario={idUsuario} Resultado=Concepto agregado",
                                "OrdenRecaudacionController");
                        }
                    }

                    var codigoSolicitudEstadoCentral = codigoSolicitud;
                    if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
                    {
                        var solicitudAuto = ConstruirSolicitudAuto(
                            idUsuario,
                            usuarioActual,
                            companiaActiva.Nombre,
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

                        codigoSolicitudEstadoCentral = codigoSolicitudGenerado;
                    }

                    if (codigoSolicitudEstadoCentral.HasValue && codigoSolicitudEstadoCentral.Value > 0)
                    {
                        try
                        {
                            new AocrEstadoProcesoService().SincronizarDesdeFuentesActuales(
                                codigoSolicitudEstadoCentral.Value,
                                "GENERAR_ORDEN",
                                idUsuario,
                                "Solicitante",
                                "Orden de recaudacion creada o vinculada.",
                                ordenId);
                        }
                        catch
                        {
                        }
                    }

                    if (requiereSolicitudInspeccion && !string.IsNullOrWhiteSpace(model.AeropuertosSolicitados))
                    {
                        Session["SolicitudInspeccionAeropuertos_" + ordenId] = model.AeropuertosSolicitados.Trim();
                    }

                    if (model.GenerarSolicitudInspeccionAlGuardar)
                    {
                        TempData["OK"] = "Orden " + numeroOrden + " creada en borrador.";
                        TempData["AeropuertosGenerarSolicitudInspeccion"] = model.AeropuertosSolicitados.Trim();
                        return RedirectToAction("GenerarSolicitudInspeccion", new { id = ordenId });
                    }

                    TempData["OK"] = "Orden " + numeroOrden + " creada exitosamente.";
                    return RedirectToAction("Detalles", new { id = ordenId });
                }

                ModelState.AddModelError("", "Error al guardar la orden en la base de datos.");
                PrepararNuevaOrdenViewModel(model, requiereSolicitudInspeccion);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al crear orden: " + ex.ToString());
                ModelState.AddModelError("", "Error interno al crear la orden: " + ex.Message);
                PrepararNuevaOrdenViewModel(model);
                return View(model);
            }
        }

        private void PrepararNuevaOrdenViewModel(OrdenRecaudacionNuevaVM model, bool? tieneInspeccionExt = null)
        {
            if (model == null)
            {
                return;
            }

            var userId = GetUserId();
            if (userId > 0)
            {
                var scope = RtCompaniaScope.FromSession(Session, userId);
                AplicarCompaniaActivaAlModelo(model, userId, scope);
            }

            CargarConceptosNueva(model);
            ConfigurarConceptoObligatorioViewBag(model);
            model.SolicitudInspeccionPanel = BuildNuevaSolicitudInspeccionPanelViewModel(model, tieneInspeccionExt);
        }

        private CompaniaActivaInfo ObtenerCompaniaActivaDesdeSesion(int userId)
        {
            return _companiaContextService.ObtenerCompaniaActivaObligatoria(
                userId,
                CompaniaActivaSessionHelper.ObtenerCodigo(Session),
                CompaniaActivaSessionHelper.ObtenerNombre(Session));
        }

        private string ResolverCompaniaCodigoOrden(OrdenRecaudacionModel orden)
        {
            if (orden == null || orden.Id <= 0)
            {
                return string.Empty;
            }

            try
            {
                var entidad = _dao.ObtenerPorId(orden.Id);
                return _companiaContextService.ResolverCodigoCompaniaDesdeOrden(entidad);
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool ValidarAccesoOrdenRecaudacionDesdeBd(OrdenRecaudacionModel orden, int idUsuario, bool esAdmin, out string mensajeError)
        {
            mensajeError = null;
            if (orden == null || orden.Id <= 0)
            {
                mensajeError = "No se encontró la Orden de Recaudación solicitada.";
                return false;
            }

            if (string.Equals(EstadoOrden.NormalizarEstado(orden.Estado), EstadoOrden.Anulada, StringComparison.OrdinalIgnoreCase))
            {
                mensajeError = "No se puede generar el PDF porque la orden se encuentra anulada.";
                return false;
            }

            if (!esAdmin && orden.CodigoUsuario != idUsuario)
            {
                mensajeError = "No está autorizado para generar el PDF de esta orden de recaudación.";
                return false;
            }

            if (esAdmin)
            {
                return true;
            }

            OrdenRecaudacion ordenEntidad;
            try
            {
                ordenEntidad = _dao.ObtenerPorId(orden.Id);
            }
            catch
            {
                ordenEntidad = null;
            }

            if (ordenEntidad == null)
            {
                mensajeError = "No se encontró la Orden de Recaudación solicitada.";
                return false;
            }

            var codigoOrden = _companiaContextService.ResolverCodigoCompaniaDesdeOrden(ordenEntidad);
            if (!string.IsNullOrWhiteSpace(codigoOrden)
                && _companiaContextService.ValidarCompaniaPerteneceAlRt(idUsuario, codigoOrden))
            {
                return true;
            }

            if (_companiaContextService.OrdenPerteneceACompania(
                ordenEntidad,
                codigoOrden,
                FirstNonEmpty(ordenEntidad.Compania, ordenEntidad.NombreContribuyente),
                idUsuario))
            {
                return true;
            }

            mensajeError = "No está autorizado para generar el PDF de esta orden de recaudación.";
            return false;
        }

        private void RegistrarTrazaOrdenPdf(
            string accion,
            int usuarioId,
            OrdenRecaudacionModel orden,
            string companiaActivaCodigo,
            string companiaFormulario,
            string tokenFormulario,
            int ordenId,
            string companiaOrdenCodigo,
            string estadoOrden)
        {
            var ordenIdFinal = orden != null && orden.Id > 0 ? orden.Id : ordenId;
            var companiaOrden = !string.IsNullOrWhiteSpace(companiaOrdenCodigo)
                ? companiaOrdenCodigo
                : ResolverCompaniaCodigoOrden(orden);

            System.Diagnostics.Trace.TraceInformation(
                "[AOCR][ORDEN_PDF] Accion={0} Usuario={1} CompaniaActiva={2} CompaniaFormulario={3} TokenFormulario={4} OrdenId={5} CompaniaOrden={6} EstadoOrden={7}",
                accion ?? string.Empty,
                usuarioId,
                companiaActivaCodigo ?? string.Empty,
                string.IsNullOrWhiteSpace(companiaFormulario) ? "(ignorado)" : companiaFormulario,
                string.IsNullOrWhiteSpace(tokenFormulario) ? "(ignorado)" : "presente",
                ordenIdFinal,
                companiaOrden ?? string.Empty,
                estadoOrden ?? string.Empty);
        }

        private void AplicarCompaniaActivaAlModelo(OrdenRecaudacionNuevaVM model, int userId, RtCompaniaScope scope)
        {
            if (model == null || userId <= 0 || scope == null || !scope.TieneCompaniaActivaValida())
            {
                return;
            }

            var companiaActiva = ObtenerCompaniaActivaDesdeSesion(userId);
            if (!companiaActiva.EsValida)
            {
                return;
            }

            model.Orden = model.Orden ?? new OrdenRecaudacionNuevaVM.NuevaOrdenViewModel();
            model.Orden.Compania = _companiaContextService.FormatearTextoCompaniaOrden(companiaActiva.Codigo, companiaActiva.Nombre);
            model.Orden.NombreContribuyente = companiaActiva.Nombre;
            model.CompaniaActivaCodigo = companiaActiva.Codigo;
            model.CompaniaActivaContextToken = CompaniaActivaSessionHelper.GenerarTokenContexto(Session, userId);

            if (!string.IsNullOrWhiteSpace(companiaActiva.Ruc))
            {
                model.Orden.RucCedula = ExtraerRucCedula(companiaActiva.Ruc);
                model.RucCedula = model.Orden.RucCedula;
            }

            Usuario usuario = null;
            try
            {
                usuario = UsuarioDAO.ObtenerPorId(userId);
                if (string.IsNullOrWhiteSpace(model.Orden.RucCedula))
                {
                    var rucCedula = ResolverRucCedulaDesdeFuentes(userId, usuario);
                    if (!string.IsNullOrWhiteSpace(rucCedula))
                    {
                        model.Orden.RucCedula = ExtraerRucCedula(rucCedula);
                        model.RucCedula = model.Orden.RucCedula;
                    }
                }

                if (string.IsNullOrWhiteSpace(model.Orden.Correo) && !string.IsNullOrWhiteSpace(usuario?.Email))
                {
                    model.Orden.Correo = usuario.Email;
                }
            }
            catch
            {
                // ignorar prefill si falla
            }

            PrefillDesdeUltimaOrden(userId, model, companiaActiva.Codigo, companiaActiva.Nombre);

            if (string.IsNullOrWhiteSpace(model.Orden.RucCedula))
            {
                model.Orden.RucCedula = ExtraerRucCedula(ResolverRucCedulaDesdeFuentes(userId, usuario));
                model.RucCedula = model.Orden.RucCedula;
            }

            model.Orden.LugarEmision = ResolverLugarEmisionDesdeDb(model.Orden.CodigoSolicitud, userId);

            ViewBag.CompaniaActivaCodigo = companiaActiva.Codigo;
            ViewBag.CompaniaActivaNombre = companiaActiva.Nombre;
            ViewBag.CompaniaActivaAlerta = "Esta orden se generará para la compañía activa seleccionada: "
                + companiaActiva.Nombre
                + " ("
                + companiaActiva.Codigo
                + "). Verifique que la compañía sea correcta antes de guardar.";
        }

        private void ConfigurarConceptoObligatorioViewBag(OrdenRecaudacionNuevaVM model)
        {
            var conceptoObligatorio = (model?.Conceptos ?? new List<CapaPresentacion.Models.ConceptoOptionVM>())
                .FirstOrDefault(c => EsConceptoInspeccionExt(c.Codigo));

            ViewBag.ConceptoObligatorioCodigo = CodigoConceptoInspeccionExt;
            ViewBag.ConceptoObligatorioId = conceptoObligatorio?.Id ?? 0;
            ViewBag.ConceptoObligatorioEncontrado = conceptoObligatorio != null;
        }

        private bool ValidarDetallesConceptosObligatoriosOrdenNueva(List<DetalleOrdenRequest> detalles, out string mensajeError)
        {
            mensajeError = null;

            var conceptoObligatorioCatalogo = _conceptoDao.ObtenerPorCodigo(CodigoConceptoInspeccionExt);
            if (conceptoObligatorioCatalogo == null || !conceptoObligatorioCatalogo.Activo)
            {
                mensajeError = "No se encontró el concepto obligatorio INSPECCION_EXT. Verifique el catálogo de conceptos antes de continuar.";
                return false;
            }

            if (detalles == null || detalles.Count == 0)
            {
                mensajeError = "Debe agregar el concepto obligatorio INSPECCION_EXT para continuar.";
                return false;
            }

            var tieneInspeccionExt = false;
            var tieneOtroConcepto = false;

            foreach (var det in detalles)
            {
                var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
                if (concepto == null || det.Cantidad <= 0)
                {
                    continue;
                }

                if (EsConceptoInspeccionExt(concepto.Codigo))
                {
                    tieneInspeccionExt = true;
                }
                else
                {
                    tieneOtroConcepto = true;
                }
            }

            if (!tieneInspeccionExt)
            {
                mensajeError = "Debe agregar el concepto obligatorio INSPECCION_EXT para continuar.";
                return false;
            }

            if (!tieneOtroConcepto)
            {
                mensajeError = "Debe agregar al menos otra acción o concepto adicional para poder continuar con el proceso.";
                return false;
            }

            return true;
        }

        private bool ValidarConceptosOrdenInspeccionExt(OrdenRecaudacionModel orden, out string mensajeError)
        {
            mensajeError = null;

            var conceptoObligatorioCatalogo = _conceptoDao.ObtenerPorCodigo(CodigoConceptoInspeccionExt);
            if (conceptoObligatorioCatalogo == null || !conceptoObligatorioCatalogo.Activo)
            {
                mensajeError = "No se encontró el concepto obligatorio INSPECCION_EXT. Verifique el catálogo de conceptos antes de continuar.";
                return false;
            }

            var detalles = ObtenerDetallesOrdenParaValidacion(orden);
            if (detalles == null || detalles.Count == 0)
            {
                mensajeError = "Debe agregar el concepto obligatorio INSPECCION_EXT para continuar.";
                return false;
            }

            var tieneInspeccionExt = false;
            var tieneOtroConcepto = false;

            foreach (var det in detalles)
            {
                if (det.Cantidad <= 0)
                {
                    continue;
                }

                if (EsConceptoInspeccionExt(det.ConceptoCodigo))
                {
                    tieneInspeccionExt = true;
                }
                else
                {
                    tieneOtroConcepto = true;
                }
            }

            if (!tieneInspeccionExt)
            {
                mensajeError = "Debe agregar el concepto obligatorio INSPECCION_EXT para continuar.";
                return false;
            }

            if (!tieneOtroConcepto)
            {
                mensajeError = "Debe agregar al menos otra acción o concepto adicional para poder continuar con el proceso.";
                return false;
            }

            return true;
        }

        private List<CapaDatos.Models.OrdenDetalleModel> ObtenerDetallesOrdenParaValidacion(OrdenRecaudacionModel orden)
        {
            if (orden == null)
            {
                return new List<CapaDatos.Models.OrdenDetalleModel>();
            }

            var detalles = orden.Detalles ?? new List<CapaDatos.Models.OrdenDetalleModel>();
            if (detalles.Count == 0 && orden.Id > 0)
            {
                try
                {
                    detalles = (_dao.ObtenerDetallesPorOrdenId(orden.Id) ?? new List<DetalleOrden>())
                        .Select(d => new CapaDatos.Models.OrdenDetalleModel
                        {
                            OrdenId = d.OrdenId,
                            ConceptoId = d.ConceptoId ?? 0,
                            ConceptoCodigo = d.ConceptoCodigo,
                            ConceptoNombre = d.ConceptoNombre,
                            Cantidad = d.Cantidad
                        })
                        .ToList();
                }
                catch
                {
                    detalles = new List<CapaDatos.Models.OrdenDetalleModel>();
                }
            }

            return detalles;
        }

        private SolicitudInspeccionExtPanelViewModel BuildNuevaSolicitudInspeccionPanelViewModel(OrdenRecaudacionNuevaVM model, bool? tieneInspeccionExt = null)
        {
            var requiereSolicitudInspeccion = tieneInspeccionExt ?? true;
            return new SolicitudInspeccionExtPanelViewModel
            {
                OrdenId = 0,
                EstadoOrden = EstadoOrden.Borrador,
                TieneInspeccionExt = requiereSolicitudInspeccion,
                EstadoDocumentoSolicitudInspeccion = requiereSolicitudInspeccion ? "NO_GENERADO" : "NO_REQUERIDO",
                AeropuertosSolicitados = (model != null ? model.AeropuertosSolicitados : null) ?? string.Empty,
                TienePdfGenerado = false,
                TienePdfFirmado = false,
                PuedeEditarSolicitudInspeccionExt = requiereSolicitudInspeccion,
                PuedeAgregarAccionesOrden = requiereSolicitudInspeccion,
                PuedeGenerarSolicitud = requiereSolicitudInspeccion,
                PuedeDescargarSolicitud = false,
                PuedeSubirSolicitudFirmada = false,
                PuedeVerSolicitudFirmada = false,
                PuedeContinuarConOrden = !requiereSolicitudInspeccion,
                EsNuevaOrden = true,
                MostrarSoloLecturaSinFirmado = false,
                UrlGenerarSolicitud = string.Empty,
                UrlVerSolicitudFirmada = string.Empty,
                UrlDescargarSolicitudGenerada = string.Empty,
                UrlSubirSolicitudFirmada = string.Empty,
                ClaseEstadoCss = requiereSolicitudInspeccion ? "warning text-dark" : "secondary",
                MensajeEstado = "Este concepto requiere generar, firmar y cargar la Solicitud de Inspecciones.",
                MensajeSoloLectura = string.Empty
            };
        }

        private async Task<string> GenerarNumeroOrdenAsync(string numeroSolicitudGop = null, int? codigoSolicitud = null)
        {
            return await Task.FromResult(_ordenRecaudacionService.GenerarNumeroOrdenAocrVinculada(DateTime.Now.Year, numeroSolicitudGop, codigoSolicitud));
        }

        private string ObtenerNumeroSolicitudGop(int? codigoSolicitud)
        {
            if (!codigoSolicitud.HasValue || codigoSolicitud.Value <= 0)
            {
                return null;
            }

            try
            {
                var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud.Value);
                return solicitud != null ? solicitud.NumeroSolicitud : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[ORDEN_NUM][GOP_LOOKUP_ERROR] CodigoSolicitud=" + codigoSolicitud.Value + "; " + ex.Message);
                return null;
            }
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

            CompletarDatosOrdenParaVista(orden);

            System.Diagnostics.Debug.WriteLine($"Controller Detalles: ordenId = {id}, numeroOrden = {orden.NumeroOrden}");

            var pagos = new List<CapaDatos.Models.PagoModel>();
            var tieneComprobanteValido = false;
            var mensajeComprobante = "Debe registrar el comprobante antes de continuar.";
            FacturaPagoRegistroModel facturaPago = null;

            try
            {
                pagos = await _dao.ObtenerPagosPorOrdenAsync(id) ?? new List<CapaDatos.Models.PagoModel>();
                NormalizarMontosPagoDesfasados(pagos, orden.Total);
            }
            catch
            {
                pagos = new List<CapaDatos.Models.PagoModel>();
            }

            try
            {
                var comprobanteService = new ComprobanteService();
                tieneComprobanteValido = comprobanteService.ExisteComprobanteValido(id, out var msgComprobante);
                mensajeComprobante = msgComprobante;
            }
            catch
            {
                tieneComprobanteValido = false;
                mensajeComprobante = "Debe registrar el comprobante antes de continuar.";
            }

            try
            {
                facturaPago = _dao.ObtenerFacturaPagoPorOrden(id);
            }
            catch
            {
                facturaPago = null;
            }

            var panelSolicitudInspeccion = BuildSolicitudInspeccionPanelViewModel(orden, idUsuario, out var documentos);

            // Cargar lista de bancos desde P9
            ViewBag.ListaBancoPago = ToSelectList("OPCBAN");
            
            // Cargar mÃ©todos de pago desde P9
            ViewBag.ListaMetodoPago = ToSelectList("SOLFOR");

            return View(new OrdenRecaudacionDetallesViewModel
            {
                Orden = orden,
                Documentos = documentos,
                Pagos = pagos,
                FacturaPago = facturaPago,
                TieneComprobanteValido = tieneComprobanteValido,
                MensajeComprobante = mensajeComprobante,
                AbrirModalPago = abrirPago,
                SolicitudInspeccionPanel = panelSolicitudInspeccion
            });
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

            CompletarDatosOrdenParaVista(orden);
            if (!OrdenPermiteAgregarAccionesInspeccionExt(orden))
            {
                TempData["Error"] = MensajeSolicitudInspeccionPendienteCargaFirmada;
                return RedirectToAction("Detalles", new { id });
            }

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

            var ordenParaValidacion = _dao.ObtenerOrdenPorIdModel(model.Id);
            CompletarDatosOrdenParaVista(ordenParaValidacion);
            if (!OrdenPermiteAgregarAccionesInspeccionExt(ordenParaValidacion))
            {
                TempData["Error"] = MensajeSolicitudInspeccionPendienteCargaFirmada;
                return RedirectToAction("Detalles", new { id = model.Id });
            }

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

            if (id <= 0)
            {
                TempData["Error"] = "No se recibió el identificador de la Orden de Recaudación. Regrese al detalle de la orden e intente nuevamente.";
                return RedirectToAction("Index");
            }

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            var esAdmin = User != null && (User.IsInRole("Administrador") || User.IsInRole("Financiero"));

            RegistrarTrazaOrdenPdf(
                "GenerarOrden",
                idUsuario,
                orden,
                CompaniaActivaSessionHelper.ObtenerCodigo(Session),
                null,
                null,
                id,
                null,
                orden != null ? orden.Estado : null);

            if (!ValidarAccesoOrdenRecaudacionDesdeBd(orden, idUsuario, esAdmin, out var mensajeAcceso))
            {
                TempData["Error"] = mensajeAcceso;
                return orden == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

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

            if (!PuedeContinuarConSolicitudInspeccion(orden, out var documentoSolicitudFirmada, out var motivoBloqueoSolicitud))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} SolicitudId={orden.CodigoSolicitud} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={(documentoSolicitudFirmada != null ? documentoSolicitudFirmada.CodigoDocumento : 0)} TienePdfFirmado={(documentoSolicitudFirmada != null)} PuedeGenerarOrden=False Usuario={idUsuario} Resultado=Bloqueado MotivoBloqueo={FirstNonEmpty(motivoBloqueoSolicitud, "Solicitud firmada faltante")}",
                    "OrdenRecaudacionController");
                TempData["Error"] = MensajeSolicitudInspeccionPendiente;
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
                    TempData["OK"] = "Su Orden de Recaudación ha sido generada correctamente. Descargue el documento y cargue el comprobante de depósito o transferencia para que el área Financiera pueda realizar la revisión correspondiente.";
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
                        CustomSwitches = PdfBrandingHelper.BuildStandardRotativaSwitches(Server, "OrdenRecaudacionController.EnviarNotificacionOrdenGeneradaAsync")
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
                    "ORDEN_GENERADA_RT",
                    string.IsNullOrWhiteSpace(orden.Correo) ? null : orden.Correo,
                    string.IsNullOrWhiteSpace(orden.NombreContribuyente) ? orden.Compania : orden.NombreContribuyente,
                    pdfBytes != null && pdfBytes.Length > 0 ? pdfBytes : null,
                    pdfBytes != null && pdfBytes.Length > 0 ? nombreArchivo : null,
                    instruccionesPagoHtml);
                System.Diagnostics.Debug.WriteLine($"Resultado notificación ORDEN_GENERADA_RT: Exitoso={resultadoCorreoRt.Exitoso}, Mensaje={resultadoCorreoRt.Mensaje}");

                if (!resultadoCorreoRt.Exitoso)
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
        [AocrAuthorize(Modulo = "OrdenRecaudacion", Accion = "SubirComprobante", CodigoOrdenParameter = "id")]
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
        [AocrAuthorize(Modulo = "OrdenRecaudacion", Accion = "SubirComprobante", CodigoOrdenParameter = "id")]
        public ActionResult RegistrarPago(int id, string Monto, string NumeroFactura, string MetodoPago, string Banco, HttpPostedFileBase ComprobanteArchivo, string Observaciones)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            var estadoOrden = CapaDatos.Constants.EstadoOrden.NormalizarEstado(orden.Estado);
            if (OrdenRecaudacionOperativaHelper.EsOrdenCerradaPostAprobacionFinanciera(estadoOrden))
            {
                TempData["Error"] = OrdenRecaudacionOperativaHelper.MensajeBloqueoComprobante;
                return RedirectToAction("Detalles", new { id = id });
            }

            if (!OrdenRecaudacionOperativaHelper.PermiteSubirComprobante(estadoOrden))
            {
                TempData["Error"] = "Solo se puede cargar respaldo cuando la orden esté en GENERADA, PENDIENTE o DEVUELTA.";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (!PuedeContinuarConSolicitudInspeccion(orden, out var documentoSolicitudFirmada, out var motivoBloqueoSolicitud))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} SolicitudId={orden.CodigoSolicitud} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={(documentoSolicitudFirmada != null ? documentoSolicitudFirmada.CodigoDocumento : 0)} TienePdfFirmado={(documentoSolicitudFirmada != null)} PuedeGenerarOrden=False Usuario={idUsuario} Resultado=Bloqueado MotivoBloqueo={FirstNonEmpty(motivoBloqueoSolicitud, "Solicitud firmada faltante")}",
                    "OrdenRecaudacionController");
                TempData["Error"] = MensajeSolicitudInspeccionPendiente;
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

                    var pagoRegistrado = _dao.ObtenerUltimoPagoPorOrden(orden.Id);
                    var comprobanteId = pagoRegistrado != null && pagoRegistrado.Id > 0
                        ? pagoRegistrado.Id.ToString()
                        : "0";

                    var resultadoCorreoPago = _ordenCorreoService.NotificarEvento(
                        ordenEntidad,
                        "COMPROBANTE_CARGADO_FINANCIERO",
                        null,
                        null,
                        comprobanteAdjunto,
                        nombreAdjunto,
                        "Sistema AOCR - Dirección General de Aviación Civil",
                        comprobanteId);

                    if (!resultadoCorreoPago.Exitoso)
                    {
                        TempData["Warning"] = "El comprobante fue registrado, pero la notificación al área Financiera no se pudo encolar: "
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
            var esAdmin = User != null && User.IsInRole("Administrador");
            if (orden == null || (!esAdmin && orden.CodigoUsuario != idUsuario))
                return HttpNotFound();

            var estadoAnterior = (orden.Estado ?? string.Empty).Trim();
            if (OrdenRecaudacionOperativaHelper.EsOrdenCerradaPostAprobacionFinanciera(estadoAnterior))
            {
                TempData["Error"] = OrdenRecaudacionOperativaHelper.MensajeBloqueoEdicion;
                return RedirectToAction("Detalles", new { id = id });
            }

            if (estadoAnterior.Equals("FACTURADA", StringComparison.OrdinalIgnoreCase) ||
                estadoAnterior.Equals("COMPLETADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "No se pueden anular órdenes aprobadas o facturadas.";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.Equals(estadoAnterior, "ANULADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La orden ya está anulada";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe ingresar el motivo de la anulación.";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                var motivoLimpio = motivo.Trim();
                bool result = _dao.Anular(id, motivoLimpio);
                if (result)
                {
                    CapaNegocio.LogBL.RegistrarInfo(
                        string.Format(
                            "[AOCR][ORDEN] Orden anulada. OrdenId={0}; NumeroOrden={1}; EstadoAnterior={2}; Usuario={3}; Motivo={4}",
                            id,
                            FirstNonEmpty(orden.NumeroOrden, id.ToString()),
                            FirstNonEmpty(estadoAnterior, "N/A"),
                            idUsuario,
                            motivoLimpio),
                        "OrdenRecaudacionController");
                    TempData["OK"] = "Orden anulada correctamente";
                    return RedirectToAction("Detalles", new { id = id });
                }

                TempData["Error"] = "Error al anular la orden";
                return RedirectToAction("Detalles", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        /// <summary>
        /// Genera la Solicitud de Inspecciones usando solo el Id de la orden (datos desde BD).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public ActionResult GenerarSolicitudInspeccion(int id)
        {
            var aeropuertos = TempData["AeropuertosGenerarSolicitudInspeccion"] as string;
            if (string.IsNullOrWhiteSpace(aeropuertos))
            {
                aeropuertos = Session["SolicitudInspeccionAeropuertos_" + id] as string;
            }

            return EjecutarGenerarSolicitudInspeccion(id, aeropuertos, GetUserId());
        }

        /// <summary>
        /// Genera y registra la solicitud documental requerida por INSPECCION_EXT.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public ActionResult GenerarSolicitudInspeccion(int id, string aeropuertosSolicitados)
        {
            return EjecutarGenerarSolicitudInspeccion(id, aeropuertosSolicitados, GetUserId());
        }

        private ActionResult EjecutarGenerarSolicitudInspeccion(int id, string aeropuertosSolicitados, int idUsuario)
        {
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            aeropuertosSolicitados = (aeropuertosSolicitados ?? string.Empty).Trim().ToUpperInvariant();

            if (id <= 0)
            {
                TempData["Error"] = "No se recibió el identificador de la Orden de Recaudación. Regrese al detalle de la orden e intente nuevamente.";
                return RedirectToAction("Index");
            }

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            CompletarDatosOrdenParaVista(orden);

            var esAdmin = User != null && User.IsInRole("Administrador");
            var companiaActivaCodigo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            RegistrarTrazaOrdenPdf(
                "GenerarSolicitudInspeccion",
                idUsuario,
                orden,
                companiaActivaCodigo,
                null,
                null,
                id,
                ResolverCompaniaCodigoOrden(orden),
                orden != null ? orden.Estado : null);

            if (!ValidarAccesoOrdenRecaudacionDesdeBd(orden, idUsuario, esAdmin, out var mensajeAcceso))
            {
                TempData["Error"] = mensajeAcceso;
                return orden == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

            if (!ValidarOrdenSolicitudInspeccion(orden, idUsuario, permitirGestion: true, mensajeError: out var mensaje))
            {
                TempData["Error"] = mensaje;
                return RedirectToAction("Detalles", new { id });
            }

            if (!PuedeEditarSolicitudInspeccionExt(orden, idUsuario, out _, out var motivoBloqueoEdicion))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} EstadoOrden={FirstNonEmpty(orden != null ? orden.Estado : null, "N/A")} Accion=GenerarSolicitud Usuario={idUsuario} PuedeEditar=False MotivoBloqueo={FirstNonEmpty(motivoBloqueoEdicion, MensajeSolicitudInspeccionSoloLectura)} Resultado=Bloqueado",
                    "OrdenRecaudacionController");
                TempData["Error"] = MensajeSolicitudInspeccionSoloLectura;
                return RedirectToAction("Detalles", new { id });
            }

            if (string.IsNullOrWhiteSpace(aeropuertosSolicitados))
            {
                TempData["Error"] = "Debe ingresar los aeropuertos solicitados para generar la Solicitud de Inspecciones.";
                return RedirectToAction("Detalles", new { id });
            }

            var solicitudIdOrden = ObtenerCodigoSolicitudOrden(orden);
            var solicitudYaGenerada = solicitudIdOrden > 0
                ? ObtenerUltimoDocumentoSolicitudInspeccion(solicitudIdOrden, TipoSolicitudInspeccionGenerada, orden.Id)
                : null;
            if (solicitudYaGenerada != null)
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} SolicitudId={solicitudIdOrden} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={solicitudYaGenerada.CodigoDocumento} Usuario={idUsuario} Accion=GenerarSolicitud Resultado=Bloqueado MotivoBloqueo=PdfYaGenerado",
                    "OrdenRecaudacionController");
                TempData["Error"] = MensajeSolicitudInspeccionYaGenerada;
                return RedirectToAction("Detalles", new { id });
            }

            string errorConceptosOrden;
            if (!ValidarConceptosOrdenInspeccionExt(orden, out errorConceptosOrden))
            {
                TempData["Error"] = errorConceptosOrden;
                return RedirectToAction("Detalles", new { id });
            }

            int documentoId;
            string errorGeneracion;
            if (GenerarSolicitudInspeccionDocumento(orden, aeropuertosSolicitados, idUsuario, out documentoId, out errorGeneracion))
            {
                TempData["OK"] = MensajeSolicitudInspeccionGeneradaExito;
            }
            else
            {
                TempData["Error"] = "No fue posible generar la Solicitud de Inspecciones. " + (errorGeneracion ?? string.Empty);
            }

            return RedirectToAction("Detalles", new { id });
        }

        /// <summary>
        /// Invalida el PDF generado y reabre la orden para agregar acciones (solo si no hay firmado cargado).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public ActionResult RechazarGeneracionSolicitudInspeccion(int id, string motivo = null)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (!ValidarOrdenSolicitudInspeccion(orden, idUsuario, permitirGestion: true, mensajeError: out var mensaje))
            {
                TempData["Error"] = mensaje;
                return orden == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

            if (!PuedeEditarSolicitudInspeccionExt(orden, idUsuario, out var documentoFirmado, out var motivoBloqueoEdicion))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} EstadoOrden={FirstNonEmpty(orden != null ? orden.Estado : null, "N/A")} Accion={AccionReaperturaPorAgregarAcciones} Usuario={idUsuario} PuedeEditar=False MotivoBloqueo={FirstNonEmpty(motivoBloqueoEdicion, MensajeSolicitudInspeccionSoloLectura)} Resultado=Bloqueado",
                    "OrdenRecaudacionController");
                TempData["Error"] = FirstNonEmpty(motivoBloqueoEdicion, MensajeSolicitudInspeccionSoloLectura);
                return RedirectToAction("Detalles", new { id });
            }

            if (documentoFirmado != null)
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} SolicitudId={ObtenerCodigoSolicitudOrden(orden)} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={documentoFirmado.CodigoDocumento} Accion={AccionReaperturaPorAgregarAcciones} Usuario={idUsuario} Resultado=Bloqueado MotivoBloqueo=SolicitudFirmadaCargada",
                    "OrdenRecaudacionController");
                TempData["Error"] = "No se puede rechazar la generación porque ya existe una solicitud firmada cargada.";
                return RedirectToAction("Detalles", new { id });
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            var generado = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionGenerada, orden.Id);
            if (generado == null)
            {
                TempData["Error"] = "No existe una solicitud generada para rechazar.";
                return RedirectToAction("Detalles", new { id });
            }

            var estadoAnterior = "PENDIENTE_CARGA_FIRMADA";
            var usuarioNombre = User != null && User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name)
                ? User.Identity.Name
                : idUsuario.ToString();

            if (!_documentoDao.MarcarComoEliminado(generado.CodigoDocumento, usuarioNombre))
            {
                TempData["Error"] = "No fue posible invalidar el PDF generado. Intente nuevamente o contacte al administrador.";
                return RedirectToAction("Detalles", new { id });
            }

            Session.Remove("SolicitudInspeccionAeropuertos_" + orden.Id);

            CapaNegocio.LogBL.RegistrarInfo(
                $"{LogSolicitudInspeccionExt} OrdenId={id} SolicitudId={solicitudId} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={generado.CodigoDocumento} Usuario={idUsuario} Accion={AccionReaperturaPorAgregarAcciones} EstadoAnterior={estadoAnterior} EstadoNuevo=NO_GENERADO Motivo={FirstNonEmpty(motivo, "Rechazo por usuario para agregar acciones")} Resultado=Reabierto",
                "OrdenRecaudacionController");

            TempData["OK"] = MensajeSolicitudInspeccionReaperturaExito;
            return RedirectToAction("Detalles", new { id });
        }

        private bool GenerarSolicitudInspeccionDocumento(
            OrdenRecaudacionModel orden,
            string aeropuertosSolicitados,
            int idUsuario,
            out int documentoId,
            out string mensajeError)
        {
            documentoId = 0;
            mensajeError = null;

            if (orden == null || orden.Id <= 0)
            {
                mensajeError = "No se encontró la orden de recaudación.";
                return false;
            }

            if (!OrdenContieneInspeccionExt(orden))
            {
                mensajeError = "La orden no contiene el concepto INSPECCION_EXT.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(aeropuertosSolicitados))
            {
                mensajeError = "Debe ingresar los aeropuertos solicitados.";
                return false;
            }

            if (!OrdenPermiteEdicionSolicitudInspeccionExt(orden))
            {
                mensajeError = MensajeSolicitudInspeccionSoloLectura;
                return false;
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            if (solicitudId <= 0)
            {
                mensajeError = "La orden no tiene solicitud asociada.";
                return false;
            }

            var generadoExistente = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionGenerada, orden.Id);
            if (generadoExistente != null)
            {
                mensajeError = MensajeSolicitudInspeccionYaGenerada;
                return false;
            }

            if (!ValidarConceptosOrdenInspeccionExt(orden, out mensajeError))
            {
                return false;
            }

            try
            {
                var aeropuertos = aeropuertosSolicitados.Trim();
                var bytes = BuildSolicitudInspeccionPdfBytes(orden, aeropuertos, out var nombreArchivo, out var paginasGeneradas);
                var rutaGuardada = GuardarBytesAocr(bytes, "SolicitudesInspeccion", nombreArchivo);
                var rutaFisica = ResolverRutaArchivoRegistrado(rutaGuardada);
                var existeArchivo = !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica);
                var version = ObtenerSiguienteVersionDocumento(solicitudId, TipoSolicitudInspeccionGenerada, orden.Id);
                var pdfModel = BuildSolicitudInspeccionPdfModel(orden, aeropuertos);

                documentoId = _documentoDao.Crear(new Documento
                {
                    CodigoSolicitud = solicitudId,
                    TipoDocumento = TipoSolicitudInspeccionGenerada,
                    NombreArchivo = nombreArchivo,
                    RutaGuardada = rutaGuardada,
                    Extension = ".pdf",
                    TamanoBytes = bytes.LongLength,
                    Estado = "Cargado",
                    Validado = false,
                    FechaCarga = DateTime.Now,
                    Version = version,
                    UsuarioRegistro = User != null && User.Identity != null ? User.Identity.Name : "sistema",
                    Observaciones = $"OrdenId={orden.Id}; CodigoConcepto={CodigoConceptoInspeccionExt}; EstadoDocumento=GENERADO; Aeropuertos={aeropuertos}"
                });

                Session["SolicitudInspeccionAeropuertos_" + orden.Id] = aeropuertos;
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionPdf} OrdenId={orden.Id} NumeroOrden={FirstNonEmpty(orden.NumeroOrden, orden.Id.ToString())} NombreRT={pdfModel.NombreRT} Compania={pdfModel.NombreCompania} AeropuertosLength={(aeropuertos ?? string.Empty).Length} PaginasGeneradas={paginasGeneradas} RutaPdf={rutaGuardada} ExisteArchivo={existeArchivo} Resultado={(paginasGeneradas == 1 && existeArchivo ? "OK" : "REVISION")}",
                    "OrdenRecaudacionController");
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={orden.Id} SolicitudId={solicitudId} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={documentoId} EstadoDocumento=GENERADO RutaPdfGenerado={rutaGuardada} Usuario={idUsuario} Resultado=Generado",
                    "OrdenRecaudacionController");
                return true;
            }
            catch (Exception ex)
            {
                mensajeError = "Revise el log técnico para más detalle.";
                CapaNegocio.LogBL.RegistrarError(
                    $"{LogSolicitudInspeccionPdf} OrdenId={orden.Id} NumeroOrden={FirstNonEmpty(orden.NumeroOrden, orden.Id.ToString())} NombreRT={FirstNonEmpty(orden.NombreUsuario, User != null && User.Identity != null ? User.Identity.Name : null, "No aplica")} Compania={FirstNonEmpty(orden.Compania, orden.NombreContribuyente, "No aplica")} AeropuertosLength={(aeropuertosSolicitados ?? string.Empty).Trim().Length} PaginasGeneradas=0 RutaPdf=N/A ExisteArchivo=False Resultado=ERROR",
                    ex.ToString(),
                    "OrdenRecaudacionController");
                CapaNegocio.LogBL.RegistrarError(
                    $"{LogSolicitudInspeccionExt} OrdenId={orden.Id} CodigoConcepto={CodigoConceptoInspeccionExt} Usuario={idUsuario} Resultado=ErrorGeneracion",
                    ex.ToString(),
                    "OrdenRecaudacionController");
                return false;
            }
        }

        private int ObtenerNumeroPaginasPdf(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length == 0)
                {
                    return 0;
                }

                using (var reader = new PdfReader(bytes))
                {
                    return reader.NumberOfPages;
                }
            }
            catch
            {
                return 0;
            }
        }

        [HttpGet]
        [Authorize(Roles = "Solicitante,Administrador,Operador,Financiero,Inspector,Coordinador,CoordinadorInspecciones,Coordinacion,JefaturaTecnica,Direccion")]
        public ActionResult DescargarSolicitudInspeccion(int id, bool vistaPrevia = false)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (!ValidarOrdenSolicitudInspeccion(orden, idUsuario, permitirGestion: false, mensajeError: out var mensaje))
            {
                TempData["Error"] = mensaje;
                return orden == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

            if (!PuedeEditarSolicitudInspeccionExt(orden, idUsuario, out _, out var motivoBloqueoEdicion))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} EstadoOrden={FirstNonEmpty(orden != null ? orden.Estado : null, "N/A")} Accion=DescargarSolicitudPreliminar Usuario={idUsuario} PuedeEditar=False MotivoBloqueo={FirstNonEmpty(motivoBloqueoEdicion, MensajeSolicitudInspeccionPreliminarBloqueada)} Resultado=Bloqueado",
                    "OrdenRecaudacionController");
                TempData["Error"] = MensajeSolicitudInspeccionPreliminarBloqueada;
                return RedirectToAction("Detalles", new { id });
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            var documento = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionGenerada, id);
            if (documento == null || string.IsNullOrWhiteSpace(documento.RutaGuardada))
            {
                TempData["Error"] = "No existe una Solicitud de Inspecciones generada para descargar.";
                return RedirectToAction("Detalles", new { id });
            }

            try
            {
                var aeropuertos = ObtenerAeropuertosSolicitudInspeccion(orden, documento);
                var bytes = BuildSolicitudInspeccionPdfBytes(orden, aeropuertos, out var nombreArchivoActual, out var paginasGeneradas);
                RefrescarArchivoSolicitudInspeccion(documento, bytes);

                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionPdf} OrdenId={orden.Id} NumeroOrden={FirstNonEmpty(orden.NumeroOrden, orden.Id.ToString())} NombreRT={FirstNonEmpty(orden.NombreUsuario, User != null && User.Identity != null ? User.Identity.Name : null, "No aplica")} Compania={FirstNonEmpty(orden.Compania, orden.NombreContribuyente, "No aplica")} AeropuertosLength={(aeropuertos ?? string.Empty).Length} PaginasGeneradas={paginasGeneradas} RutaPdf={(documento.RutaGuardada ?? "N/A")} ExisteArchivo=True Resultado=REGENERADO_DESCARGA",
                    "OrdenRecaudacionController");

                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={orden.Id} SolicitudId={solicitudId} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={documento.CodigoDocumento} RutaPdfGenerado={(documento.RutaGuardada ?? "N/A")} Usuario={idUsuario} Resultado=RegeneradoDescarga",
                    "OrdenRecaudacionController");

                Response.Headers["X-Content-Type-Options"] = "nosniff";
                if (vistaPrevia)
                {
                    return File(bytes, "application/pdf");
                }

                return File(bytes, "application/pdf", string.IsNullOrWhiteSpace(nombreArchivoActual) ? documento.NombreArchivo : nombreArchivoActual);
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError(
                    $"{LogSolicitudInspeccionPdf} OrdenId={orden.Id} NumeroOrden={FirstNonEmpty(orden.NumeroOrden, orden.Id.ToString())} NombreRT={FirstNonEmpty(orden.NombreUsuario, User != null && User.Identity != null ? User.Identity.Name : null, "No aplica")} Compania={FirstNonEmpty(orden.Compania, orden.NombreContribuyente, "No aplica")} AeropuertosLength={ObtenerAeropuertosSolicitudInspeccion(orden, documento).Length} PaginasGeneradas=0 RutaPdf={(documento.RutaGuardada ?? "N/A")} ExisteArchivo=False Resultado=ERROR_REGENERAR_DESCARGA",
                    ex.ToString(),
                    "OrdenRecaudacionController");
            }

            return DescargarDocumentoSolicitudInspeccion(orden, documento, !vistaPrevia, "RutaPdfGenerado");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public ActionResult SubirSolicitudInspeccionFirmada(int id, HttpPostedFileBase archivoSolicitudFirmada)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (!ValidarOrdenSolicitudInspeccion(orden, idUsuario, permitirGestion: true, mensajeError: out var mensaje))
            {
                TempData["Error"] = mensaje;
                return orden == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

            if (!PuedeEditarSolicitudInspeccionExt(orden, idUsuario, out _, out var motivoBloqueoEdicion))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} EstadoOrden={FirstNonEmpty(orden != null ? orden.Estado : null, "N/A")} Accion=SubirSolicitudFirmada Usuario={idUsuario} PuedeEditar=False MotivoBloqueo={FirstNonEmpty(motivoBloqueoEdicion, MensajeSolicitudInspeccionSoloLectura)} Resultado=Bloqueado",
                    "OrdenRecaudacionController");
                TempData["Error"] = MensajeSolicitudInspeccionSoloLectura;
                return RedirectToAction("Detalles", new { id });
            }

            var solicitudIdOrden = ObtenerCodigoSolicitudOrden(orden);
            if (ObtenerUltimoDocumentoSolicitudInspeccion(solicitudIdOrden, TipoSolicitudInspeccionGenerada, id) == null)
            {
                TempData["Error"] = "Debe generar la Solicitud de Inspecciones antes de cargar el documento firmado.";
                return RedirectToAction("Detalles", new { id });
            }

            if (archivoSolicitudFirmada == null || archivoSolicitudFirmada.ContentLength <= 0)
            {
                TempData["Error"] = "Debe seleccionar la Solicitud de Inspecciones firmada en formato PDF.";
                return RedirectToAction("Detalles", new { id });
            }

            if (!FileStorageHelper.ValidatePdf(archivoSolicitudFirmada, out var fileError))
            {
                TempData["Error"] = fileError;
                return RedirectToAction("Detalles", new { id });
            }

            try
            {
                var solicitudId = ObtenerCodigoSolicitudOrden(orden);
                var rutaGuardada = FileStorageHelper.SavePdf(archivoSolicitudFirmada, "SolicitudesInspeccionFirmadas");
                var rutaFisica = ResolverRutaArchivoRegistrado(rutaGuardada);
                var hash = !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica)
                    ? FileStorageHelper.ComputeSha256(rutaFisica)
                    : string.Empty;
                var version = ObtenerSiguienteVersionDocumento(solicitudId, TipoSolicitudInspeccionFirmada, id);
                var nombreOriginal = Path.GetFileName(archivoSolicitudFirmada.FileName);

                var documentoId = _documentoDao.Crear(new Documento
                {
                    CodigoSolicitud = solicitudId,
                    TipoDocumento = TipoSolicitudInspeccionFirmada,
                    NombreArchivo = string.IsNullOrWhiteSpace(nombreOriginal) ? "Solicitud_Inspecciones_Firmada.pdf" : nombreOriginal,
                    RutaGuardada = rutaGuardada,
                    Extension = ".pdf",
                    TamanoBytes = archivoSolicitudFirmada.ContentLength,
                    Estado = "Cargado",
                    Validado = false,
                    FechaCarga = DateTime.Now,
                    Version = version,
                    UsuarioRegistro = User.Identity.Name,
                    Observaciones = $"OrdenId={id}; CodigoConcepto={CodigoConceptoInspeccionExt}; EstadoDocumento=CARGADO; HashArchivo={hash}"
                });

                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} SolicitudId={solicitudId} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={documentoId} EstadoDocumento=CARGADO RutaPdfFirmado={rutaGuardada} Usuario={idUsuario} Resultado=Cargado",
                    "OrdenRecaudacionController");

                TempData["OK"] = "Solicitud de Inspecciones firmada cargada correctamente.";
                return RedirectToAction("Detalles", new { id });
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} CodigoConcepto={CodigoConceptoInspeccionExt} Usuario={idUsuario} Resultado=ErrorCarga",
                    ex.ToString(),
                    "OrdenRecaudacionController");
                TempData["Error"] = "No fue posible guardar la Solicitud de Inspecciones firmada.";
                return RedirectToAction("Detalles", new { id });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Solicitante,Administrador,Operador,Financiero,Inspector,Coordinador,CoordinadorInspecciones,Coordinacion,JefaturaTecnica,Direccion")]
        public ActionResult VerSolicitudInspeccionFirmada(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorIdModel(id);
            if (!ValidarOrdenSolicitudInspeccion(orden, idUsuario, permitirGestion: false, mensajeError: out var mensaje))
            {
                TempData["Error"] = mensaje;
                return orden == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

            if (!PuedeVerSolicitudFirmada(orden, idUsuario, out var documento, out var motivoBloqueo))
            {
                CapaNegocio.LogBL.RegistrarInfo(
                    $"{LogSolicitudInspeccionExt} OrdenId={id} EstadoOrden={FirstNonEmpty(orden != null ? orden.Estado : null, "N/A")} Accion=VerSolicitudFirmada Usuario={idUsuario} PuedeVerFirmado=False MotivoBloqueo={FirstNonEmpty(motivoBloqueo, "Documento firmado no disponible")} Resultado=Bloqueado",
                    "OrdenRecaudacionController");
                TempData["Error"] = FirstNonEmpty(motivoBloqueo, "No existe una Solicitud de Inspecciones firmada cargada.");
                return RedirectToAction("Detalles", new { id });
            }

            CapaNegocio.LogBL.RegistrarInfo(
                $"{LogSolicitudInspeccionExt} OrdenId={id} EstadoOrden={FirstNonEmpty(orden != null ? orden.Estado : null, "N/A")} EstadoDocumento={FirstNonEmpty(documento != null ? documento.Estado : null, "N/A")} DocumentoId={(documento != null ? documento.CodigoDocumento : 0)} Accion=VerSolicitudFirmada Usuario={idUsuario} PuedeVerFirmado=True Resultado=Permitido",
                "OrdenRecaudacionController");

            return DescargarDocumentoSolicitudInspeccion(orden, documento, descargar: false, rutaLogLabel: "RutaPdfFirmado");
        }

        private ActionResult DescargarDocumentoSolicitudInspeccion(OrdenRecaudacionModel orden, Documento documento, bool descargar, string rutaLogLabel)
        {
            var rutaFisica = ResolverRutaArchivoRegistrado(documento.RutaGuardada);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                TempData["Error"] = "No se encontró el archivo solicitado.";
                return RedirectToAction("Detalles", new { id = orden.Id });
            }

            var nombre = string.IsNullOrWhiteSpace(documento.NombreArchivo)
                ? ConstruirNombrePdfSolicitudInspeccion(orden)
                : documento.NombreArchivo;
            if (!nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                nombre += ".pdf";
            }

            CapaNegocio.LogBL.RegistrarInfo(
                $"{LogSolicitudInspeccionExt} OrdenId={orden.Id} SolicitudId={ObtenerCodigoSolicitudOrden(orden)} CodigoConcepto={CodigoConceptoInspeccionExt} DocumentoId={documento.CodigoDocumento} {rutaLogLabel}={documento.RutaGuardada} Usuario={GetUserId()} Resultado=Descargado",
                "OrdenRecaudacionController");

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, nombre);
            return File(System.IO.File.ReadAllBytes(rutaFisica), "application/pdf");
        }

        /// <summary>
        /// Genera y descarga el PDF de la orden usando únicamente el Id (datos desde BD).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "OrdenRecaudacion", Accion = "Descargar", CodigoOrdenParameter = "id")]
        [Authorize(Roles = "Solicitante,Administrador,Operador,Financiero,Inspector,Coordinador,CoordinadorInspecciones,Coordinacion,JefaturaTecnica,Direccion")]
        public ActionResult GenerarPdf(int id)
        {
            return DescargarPdf(id, vistaPrevia: false);
        }

        /// <summary>
        /// Descargar PDF de orden
        /// </summary>
        [HttpGet]
        [AocrAuthorize(Modulo = "OrdenRecaudacion", Accion = "Descargar", CodigoOrdenParameter = "id")]
        public ActionResult DescargarPdf(int id, bool vistaPrevia = false)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            if (id <= 0)
            {
                TempData["Error"] = "No se recibió el identificador de la Orden de Recaudación. Regrese al detalle de la orden e intente nuevamente.";
                return RedirectToAction("Index");
            }

            var ordenModel = _dao.ObtenerOrdenPorIdModel(id);
            var esFinanciero = User != null && (User.IsInRole("Financiero") || User.IsInRole("Administrador"));
            var esAdmin = esFinanciero || (User != null && User.IsInRole("Administrador"));

            RegistrarTrazaOrdenPdf(
                "DescargarPdf",
                idUsuario,
                ordenModel,
                CompaniaActivaSessionHelper.ObtenerCodigo(Session),
                null,
                null,
                id,
                null,
                ordenModel != null ? ordenModel.Estado : null);

            if (!ValidarAccesoOrdenRecaudacionDesdeBd(ordenModel, idUsuario, esAdmin, out var mensajeAcceso))
            {
                TempData["Error"] = mensajeAcceso;
                return ordenModel == null ? (ActionResult)HttpNotFound() : RedirectToAction("Detalles", new { id });
            }

            if (ordenModel == null)
                return HttpNotFound();

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
                    CustomSwitches = PdfBrandingHelper.BuildStandardRotativaSwitches(Server, "OrdenRecaudacionController.GenerarPdf")
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
        [AocrAuthorize(Modulo = "OrdenRecaudacion", Accion = "Descargar", CodigoOrdenParameter = "id")]
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

        private SolicitudInspeccionExtPanelViewModel BuildSolicitudInspeccionPanelViewModel(OrdenRecaudacionModel orden, int idUsuario, out List<CapaDatos.Models.DocumentoModel> documentos)
        {
            documentos = new List<CapaDatos.Models.DocumentoModel>();
            var documentosSolicitud = new List<Documento>();
            var requiere = OrdenContieneInspeccionExt(orden);
            Documento generado = null;
            Documento firmado = null;

            try
            {
                var solicitudId = ObtenerCodigoSolicitudOrden(orden);
                if (solicitudId > 0)
                {
                    documentosSolicitud = _documentoDao.ObtenerPorSolicitud(solicitudId) ?? new List<Documento>();
                    documentos = documentosSolicitud.Select(MapearDocumentoParaVista).ToList();
                    generado = ObtenerUltimoDocumentoSolicitudInspeccion(documentosSolicitud, TipoSolicitudInspeccionGenerada, orden != null ? (int?)orden.Id : null);
                    firmado = ObtenerUltimoDocumentoSolicitudInspeccion(documentosSolicitud, TipoSolicitudInspeccionFirmada, orden != null ? (int?)orden.Id : null);
                }
            }
            catch
            {
                documentos = new List<CapaDatos.Models.DocumentoModel>();
            }

            var estadoSolicitudInspeccion = requiere
                ? ResolverEstadoSolicitudInspeccion(documentosSolicitud, orden != null ? (int?)orden.Id : null, generado, firmado)
                : "NO_REQUERIDO";
            var puedeEditar = requiere && PuedeEditarSolicitudInspeccionExt(orden, idUsuario, out _, out _);
            var puedeContinuarConOrden = !requiere || PuedeContinuarConSolicitudInspeccion(orden, out _, out _);
            var puedeVerFirmada = requiere && PuedeVerSolicitudFirmada(orden, idUsuario, firmado, out _);
            var puedeAccederPanel = requiere && UsuarioPuedeAccederOrdenInspeccion(orden, idUsuario, permitirGestion: false);
            var puedeRechazarGeneracion = puedeEditar && generado != null && firmado == null;

            return new SolicitudInspeccionExtPanelViewModel
            {
                OrdenId = orden != null ? orden.Id : 0,
                EstadoOrden = orden != null ? EstadoOrden.NormalizarEstado(orden.Estado) : string.Empty,
                TieneInspeccionExt = requiere,
                EstadoDocumentoSolicitudInspeccion = estadoSolicitudInspeccion,
                AeropuertosSolicitados = requiere ? ObtenerAeropuertosSolicitudInspeccion(orden, generado ?? firmado) : string.Empty,
                TienePdfGenerado = generado != null,
                TienePdfFirmado = firmado != null,
                PuedeEditarSolicitudInspeccionExt = puedeEditar,
                PuedeAgregarAccionesOrden = puedeEditar && generado == null,
                PuedeGenerarSolicitud = puedeEditar && generado == null,
                PuedeDescargarSolicitud = generado != null && puedeAccederPanel,
                PuedeSubirSolicitudFirmada = puedeEditar && generado != null,
                PuedeVerSolicitudFirmada = puedeVerFirmada,
                PuedeRechazarGeneracionSolicitud = puedeRechazarGeneracion,
                PuedeContinuarConOrden = puedeContinuarConOrden,
                EsNuevaOrden = false,
                MostrarSoloLecturaSinFirmado = requiere && !puedeEditar && !puedeVerFirmada,
                UrlGenerarSolicitud = Url.Action("GenerarSolicitudInspeccion", "OrdenRecaudacion"),
                UrlVerSolicitudFirmada = puedeVerFirmada ? Url.Action("VerSolicitudInspeccionFirmada", "OrdenRecaudacion", new { id = orden.Id }) : string.Empty,
                UrlDescargarSolicitudGenerada = (generado != null && puedeAccederPanel) ? Url.Action("DescargarSolicitudInspeccion", "OrdenRecaudacion", new { id = orden.Id }) : string.Empty,
                UrlSubirSolicitudFirmada = Url.Action("SubirSolicitudInspeccionFirmada", "OrdenRecaudacion"),
                UrlRechazarGeneracionSolicitud = puedeRechazarGeneracion
                    ? Url.Action("RechazarGeneracionSolicitudInspeccion", "OrdenRecaudacion")
                    : string.Empty,
                ClaseEstadoCss = ResolverClaseEstadoSolicitudInspeccion(estadoSolicitudInspeccion),
                MensajeEstado = ResolverMensajeSolicitudInspeccionPanel(estadoSolicitudInspeccion, puedeEditar, puedeVerFirmada, puedeRechazarGeneracion),
                MensajeSoloLectura = requiere && !puedeEditar
                    ? (puedeVerFirmada ? MensajeSolicitudInspeccionModoSoloLectura : MensajeSolicitudInspeccionSoloLecturaSinFirmado)
                    : string.Empty
            };
        }

        private string ResolverClaseEstadoSolicitudInspeccion(string estadoSolicitudInspeccion)
        {
            switch ((estadoSolicitudInspeccion ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "VALIDADO":
                    return "success";
                case "CARGADO":
                    return "primary";
                case "REEMPLAZADO":
                    return "info";
                case "PENDIENTE_CARGA_FIRMADA":
                    return "warning";
                case "OBSERVADO":
                    return "danger";
                case "NO_GENERADO":
                    return "warning text-dark";
                default:
                    return "secondary";
            }
        }

        private string ResolverMensajeSolicitudInspeccionPanel(string estadoSolicitudInspeccion, bool puedeEditar, bool puedeVerFirmada, bool puedeRechazarGeneracion = false)
        {
            var estado = (estadoSolicitudInspeccion ?? string.Empty).Trim().ToUpperInvariant();
            if (!puedeEditar)
            {
                if (puedeVerFirmada)
                {
                    return estado == "VALIDADO"
                        ? "La solicitud firmada ya fue validada dentro del expediente documental."
                        : "La Solicitud firmada ya fue cargada y queda disponible para revisión documental del expediente.";
                }

                return MensajeSolicitudInspeccionSoloLecturaSinFirmado;
            }

            switch (estado)
            {
                case "CARGADO":
                    return "Solicitud firmada cargada correctamente. Ya puede generar la orden.";
                case "VALIDADO":
                    return "La solicitud firmada ya fue validada dentro del expediente documental.";
                case "PENDIENTE_CARGA_FIRMADA":
                    return puedeRechazarGeneracion
                        ? MensajeSolicitudInspeccionPendienteConReapertura
                        : MensajeSolicitudInspeccionPendienteCargaFirmada;
                case "OBSERVADO":
                    return "La solicitud firmada fue observada. Cargue una nueva versión firmada para reemplazar el documento observado.";
                case "REEMPLAZADO":
                    return "Existe una nueva versión firmada cargada en reemplazo de una versión previamente observada.";
                default:
                    return "Este concepto requiere generar, firmar y cargar la Solicitud de Inspecciones.";
            }
        }

        private CapaDatos.Models.DocumentoModel MapearDocumentoParaVista(Documento doc)
        {
            if (doc == null) return null;

            return new CapaDatos.Models.DocumentoModel
            {
                CodigoDocumento = doc.CodigoDocumento,
                CodigoSolicitud = doc.CodigoSolicitud,
                TipoDocumento = doc.TipoDocumento,
                NombreArchivo = doc.NombreArchivo,
                RutaGuardada = doc.RutaGuardada,
                Extension = doc.Extension,
                TamanoBytes = doc.TamanoBytes,
                Estado = doc.Estado,
                Validado = doc.Validado,
                FechaCarga = doc.FechaCarga,
                FechaValidacion = doc.FechaValidacion,
                ValidadoPor = doc.ValidadoPor,
                Observaciones = doc.Observaciones,
                Version = doc.Version,
                CreatedBy = doc.UsuarioRegistro
            };
        }

        private string ResolverEstadoSolicitudInspeccion(IEnumerable<Documento> documentos, int? ordenId, Documento generado, Documento firmado)
        {
            if (firmado != null)
            {
                var estado = (firmado.Estado ?? string.Empty).Trim().ToUpperInvariant();
                if (firmado.Validado == true || estado == "VALIDADO" || estado == "APROBADO")
                {
                    return "VALIDADO";
                }

                if (estado == "OBSERVADO" || estado == "RECHAZADO" || estado == "SUBSANACION")
                {
                    return "OBSERVADO";
                }

                var tieneVersionPreviaObservada = (documentos ?? Enumerable.Empty<Documento>())
                    .Where(d => string.Equals((d.TipoDocumento ?? string.Empty).Trim(), TipoSolicitudInspeccionFirmada, StringComparison.OrdinalIgnoreCase))
                    .Where(d => DocumentoPerteneceOrden(d, ordenId))
                    .Where(d => d.CodigoDocumento != firmado.CodigoDocumento)
                    .Any(EsDocumentoSolicitudInspeccionObservado);

                if (tieneVersionPreviaObservada)
                {
                    return "REEMPLAZADO";
                }

                if (DocumentoSolicitudInspeccionPermiteAvanzar(firmado))
                {
                    return "CARGADO";
                }

                return generado != null ? "PENDIENTE_CARGA_FIRMADA" : "NO_GENERADO";
            }

            if (generado != null)
            {
                return "PENDIENTE_CARGA_FIRMADA";
            }

            return "NO_GENERADO";
        }

        private bool EsDocumentoSolicitudInspeccionObservado(Documento documento)
        {
            var estado = documento != null ? (documento.Estado ?? string.Empty).Trim().ToUpperInvariant() : string.Empty;
            return estado == "OBSERVADO"
                || estado == "RECHAZADO"
                || estado == "SUBSANACION";
        }

        private bool ValidarOrdenSolicitudInspeccion(OrdenRecaudacionModel orden, int idUsuario, bool permitirGestion, out string mensajeError)
        {
            mensajeError = null;
            if (orden == null)
            {
                mensajeError = "No se encontró la orden.";
                return false;
            }

            if (!UsuarioPuedeAccederOrdenInspeccion(orden, idUsuario, permitirGestion))
            {
                mensajeError = "No tiene permisos para acceder a este documento.";
                return false;
            }

            if (!OrdenContieneInspeccionExt(orden))
            {
                mensajeError = "La orden no contiene el concepto INSPECCION_EXT.";
                return false;
            }

            if (ObtenerCodigoSolicitudOrden(orden) <= 0)
            {
                mensajeError = "La orden no está vinculada a una solicitud válida.";
                return false;
            }

            return true;
        }

        private bool UsuarioPuedeAccederOrdenInspeccion(OrdenRecaudacionModel orden, int idUsuario, bool permitirGestion)
        {
            if (orden == null || idUsuario <= 0) return false;

            var esPropietario = orden.CodigoUsuario == idUsuario;
            var esAdmin = User != null && User.IsInRole("Administrador");
            if (permitirGestion)
            {
                return esPropietario || esAdmin;
            }

            var esRolConsulta = User != null &&
                (User.IsInRole("Financiero") ||
                 User.IsInRole("Inspector") ||
                 User.IsInRole("Coordinador") ||
                 User.IsInRole("CoordinadorInspecciones") ||
                 User.IsInRole("JefaturaTecnica") ||
                 User.IsInRole("Direccion"));

            return esPropietario || esAdmin || esRolConsulta;
        }

        private bool OrdenContieneInspeccionExt(OrdenRecaudacionModel orden)
        {
            if (orden == null) return false;

            var detalles = orden.Detalles ?? new List<CapaDatos.Models.OrdenDetalleModel>();
            if (detalles.Count == 0 && orden.Id > 0)
            {
                try
                {
                    detalles = (_dao.ObtenerDetallesPorOrdenId(orden.Id) ?? new List<DetalleOrden>())
                        .Select(d => new CapaDatos.Models.OrdenDetalleModel
                        {
                            OrdenId = d.OrdenId,
                            ConceptoId = d.ConceptoId ?? 0,
                            ConceptoCodigo = d.ConceptoCodigo,
                            ConceptoNombre = d.ConceptoNombre
                        })
                        .ToList();
                }
                catch
                {
                    detalles = new List<CapaDatos.Models.OrdenDetalleModel>();
                }
            }

            return detalles.Any(d => EsConceptoInspeccionExt(d.ConceptoCodigo));
        }

        private bool EsConceptoInspeccionExt(string codigo)
        {
            return string.Equals((codigo ?? string.Empty).Trim(), CodigoConceptoInspeccionExt, StringComparison.OrdinalIgnoreCase);
        }

        private bool OrdenPermiteAgregarAccionesInspeccionExt(OrdenRecaudacionModel orden)
        {
            if (orden == null || !OrdenContieneInspeccionExt(orden))
            {
                return true;
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            if (solicitudId <= 0)
            {
                return true;
            }

            var generado = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionGenerada, orden.Id);
            return generado == null;
        }

        private int ObtenerCodigoSolicitudOrden(OrdenRecaudacionModel orden)
        {
            if (orden == null || string.IsNullOrWhiteSpace(orden.CodigoSolicitud)) return 0;
            return int.TryParse(orden.CodigoSolicitud.Trim(), out var solicitudId) ? solicitudId : _dao.ObtenerCodigoSolicitudPorNumero(orden.CodigoSolicitud);
        }

        private Documento ObtenerUltimoDocumentoSolicitudInspeccion(int solicitudId, string tipoDocumento, int? ordenId = null)
        {
            if (solicitudId <= 0) return null;
            var documentos = _documentoDao.ObtenerPorSolicitud(solicitudId) ?? new List<Documento>();
            return ObtenerUltimoDocumentoSolicitudInspeccion(documentos, tipoDocumento, ordenId);
        }

        private Documento ObtenerUltimoDocumentoSolicitudInspeccion(IEnumerable<Documento> documentos, string tipoDocumento, int? ordenId = null)
        {
            return (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => string.Equals((d.TipoDocumento ?? string.Empty).Trim(), tipoDocumento, StringComparison.OrdinalIgnoreCase))
                .Where(d => !string.Equals((d.Estado ?? string.Empty).Trim(), "ELIMINADO", StringComparison.OrdinalIgnoreCase))
                .Where(d => DocumentoPerteneceOrden(d, ordenId))
                .OrderByDescending(d => d.Version ?? 0)
                .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                .ThenByDescending(d => d.CodigoDocumento)
                .FirstOrDefault();
        }

        private bool DocumentoPerteneceOrden(Documento documento, int? ordenId)
        {
            if (!ordenId.HasValue || ordenId.Value <= 0)
            {
                return true;
            }

            var observaciones = documento != null ? (documento.Observaciones ?? string.Empty) : string.Empty;
            return observaciones.IndexOf("OrdenId=" + ordenId.Value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool DocumentoPerteneceConceptoInspeccionExt(Documento documento)
        {
            var observaciones = documento != null ? (documento.Observaciones ?? string.Empty) : string.Empty;
            return observaciones.IndexOf("CodigoConcepto=" + CodigoConceptoInspeccionExt, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool OrdenPermiteEdicionSolicitudInspeccionExt(OrdenRecaudacionModel orden)
        {
            if (orden == null)
            {
                return false;
            }

            var estadoNormalizado = EstadoOrden.NormalizarEstado(orden.Estado);
            var estadoOriginal = (orden.Estado ?? string.Empty).Trim();
            return string.Equals(estadoNormalizado, EstadoOrden.Borrador, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoOriginal, "PENDIENTE_GENERACION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoOriginal, "EN_CREACION", StringComparison.OrdinalIgnoreCase);
        }

        private bool DocumentoSolicitudInspeccionEstaFinalizado(Documento documento)
        {
            if (documento == null)
            {
                return false;
            }

            var estado = (documento.Estado ?? string.Empty).Trim().ToUpperInvariant();
            return documento.Validado == true || estado == "VALIDADO" || estado == "APROBADO";
        }

        private bool PuedeEditarSolicitudInspeccionExt(OrdenRecaudacionModel orden, int idUsuario, out Documento documentoFirmado, out string motivoBloqueo)
        {
            documentoFirmado = null;
            motivoBloqueo = null;

            if (orden == null)
            {
                motivoBloqueo = "Orden no encontrada";
                return false;
            }

            if (!OrdenContieneInspeccionExt(orden))
            {
                motivoBloqueo = "La orden no contiene el concepto INSPECCION_EXT.";
                return false;
            }

            if (!OrdenPermiteEdicionSolicitudInspeccionExt(orden))
            {
                motivoBloqueo = MensajeSolicitudInspeccionSoloLectura;
                return false;
            }

            if (!UsuarioPuedeAccederOrdenInspeccion(orden, idUsuario, permitirGestion: true))
            {
                motivoBloqueo = "No tiene permisos para modificar la Solicitud de Inspecciones.";
                return false;
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            if (solicitudId <= 0)
            {
                motivoBloqueo = "La orden no está vinculada a una solicitud válida.";
                return false;
            }

            documentoFirmado = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionFirmada, orden.Id);
            if (DocumentoSolicitudInspeccionEstaFinalizado(documentoFirmado))
            {
                motivoBloqueo = "No se puede modificar la Solicitud de Inspecciones porque el documento ya fue validado.";
                return false;
            }

            return true;
        }

        private bool PuedeVerSolicitudFirmada(OrdenRecaudacionModel orden, int idUsuario, out Documento documentoFirmado, out string motivoBloqueo)
        {
            documentoFirmado = null;
            motivoBloqueo = null;

            if (orden == null)
            {
                motivoBloqueo = "Orden no encontrada";
                return false;
            }

            if (!OrdenContieneInspeccionExt(orden))
            {
                motivoBloqueo = "La orden no contiene el concepto INSPECCION_EXT.";
                return false;
            }

            if (!UsuarioPuedeAccederOrdenInspeccion(orden, idUsuario, permitirGestion: false))
            {
                motivoBloqueo = "No tiene permisos para visualizar la Solicitud de Inspecciones firmada.";
                return false;
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            if (solicitudId <= 0)
            {
                motivoBloqueo = "La orden no está vinculada a una solicitud válida.";
                return false;
            }

            documentoFirmado = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionFirmada, orden.Id);
            return PuedeVerSolicitudFirmada(orden, idUsuario, documentoFirmado, out motivoBloqueo);
        }

        private bool PuedeVerSolicitudFirmada(OrdenRecaudacionModel orden, int idUsuario, Documento documentoFirmado, out string motivoBloqueo)
        {
            motivoBloqueo = null;

            if (orden == null)
            {
                motivoBloqueo = "Orden no encontrada";
                return false;
            }

            if (!UsuarioPuedeAccederOrdenInspeccion(orden, idUsuario, permitirGestion: false))
            {
                motivoBloqueo = "No tiene permisos para visualizar la Solicitud de Inspecciones firmada.";
                return false;
            }

            if (documentoFirmado == null)
            {
                motivoBloqueo = "No existe una Solicitud de Inspecciones firmada cargada.";
                return false;
            }

            if (!DocumentoPerteneceOrden(documentoFirmado, orden.Id) || !DocumentoPerteneceConceptoInspeccionExt(documentoFirmado))
            {
                motivoBloqueo = "La solicitud firmada no pertenece a la orden actual.";
                return false;
            }

            if (!ArchivoDocumentoExiste(documentoFirmado))
            {
                motivoBloqueo = "No se encontró el archivo físico de la Solicitud de Inspecciones firmada.";
                return false;
            }

            return true;
        }

        private bool DocumentoSolicitudInspeccionPermiteAvanzar(Documento documento)
        {
            if (documento == null)
            {
                return false;
            }

            if (!DocumentoPerteneceConceptoInspeccionExt(documento))
            {
                return false;
            }

            if (!ArchivoDocumentoExiste(documento))
            {
                return false;
            }

            var estado = (documento.Estado ?? string.Empty).Trim().ToUpperInvariant();
            return documento.Validado == true
                || estado == "VALIDADO"
                || estado == "APROBADO"
                || estado == "CARGADO";
        }

        private bool ArchivoDocumentoExiste(Documento documento)
        {
            if (documento == null || string.IsNullOrWhiteSpace(documento.RutaGuardada))
            {
                return false;
            }

            var rutaFisica = ResolverRutaArchivoRegistrado(documento.RutaGuardada);
            return !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica);
        }

        private int ObtenerSiguienteVersionDocumento(int solicitudId, string tipoDocumento, int? ordenId = null)
        {
            try
            {
                var documentos = _documentoDao.ObtenerPorSolicitud(solicitudId) ?? new List<Documento>();
                var ultimaVersion = documentos
                    .Where(d => string.Equals((d.TipoDocumento ?? string.Empty).Trim(), tipoDocumento, StringComparison.OrdinalIgnoreCase))
                    .Where(d => DocumentoPerteneceOrden(d, ordenId))
                    .Select(d => d.Version ?? 0)
                    .DefaultIfEmpty(0)
                    .Max();
                return ultimaVersion + 1;
            }
            catch
            {
                return 1;
            }
        }

        private bool ExisteSolicitudInspeccionFirmada(OrdenRecaudacionModel orden)
        {
            return PuedeContinuarConSolicitudInspeccion(orden, out _, out _);
        }

        private bool PuedeContinuarConSolicitudInspeccion(OrdenRecaudacionModel orden, out Documento documentoFirmado, out string motivoBloqueo)
        {
            documentoFirmado = null;
            motivoBloqueo = null;

            if (orden == null)
            {
                motivoBloqueo = "Orden no encontrada";
                return false;
            }

            if (!OrdenContieneInspeccionExt(orden))
            {
                return true;
            }

            var solicitudId = ObtenerCodigoSolicitudOrden(orden);
            if (solicitudId <= 0)
            {
                motivoBloqueo = "Solicitud asociada inválida";
                return false;
            }

            documentoFirmado = ObtenerUltimoDocumentoSolicitudInspeccion(solicitudId, TipoSolicitudInspeccionFirmada, orden.Id);
            if (documentoFirmado == null)
            {
                motivoBloqueo = MensajeSolicitudInspeccionFirmadaFaltante;
                return false;
            }

            if (!DocumentoPerteneceOrden(documentoFirmado, orden.Id))
            {
                motivoBloqueo = "La solicitud firmada no pertenece a la orden actual";
                return false;
            }

            if (!DocumentoPerteneceConceptoInspeccionExt(documentoFirmado))
            {
                motivoBloqueo = "La solicitud firmada no está asociada al concepto INSPECCION_EXT";
                return false;
            }

            if (!ArchivoDocumentoExiste(documentoFirmado))
            {
                motivoBloqueo = "El archivo de la solicitud firmada no existe físicamente";
                return false;
            }

            if (!DocumentoSolicitudInspeccionPermiteAvanzar(documentoFirmado))
            {
                motivoBloqueo = MensajeSolicitudInspeccionFirmadaFaltante;
                return false;
            }

            return true;
        }

        private CapaPresentacion.Models.ViewModels.SolicitudInspeccionPdfViewModel BuildSolicitudInspeccionPdfModel(OrdenRecaudacionModel orden, string aeropuertosSolicitados)
        {
            CompletarDatosOrdenParaVista(orden);
            Usuario usuario = null;
            try
            {
                usuario = UsuarioDAO.ObtenerPorId(orden.CodigoUsuario);
            }
            catch
            {
                usuario = null;
            }

            return new CapaPresentacion.Models.ViewModels.SolicitudInspeccionPdfViewModel
            {
                OrdenId = orden.Id,
                SolicitudId = ObtenerCodigoSolicitudOrden(orden),
                NombreRT = FirstNonEmpty(usuario?.NombreCompleto, orden.NombreUsuario, User?.Identity?.Name, "No aplica"),
                NombreCompania = FirstNonEmpty(orden.Compania, orden.NombreContribuyente, "No aplica"),
                AeropuertosSolicitados = FirstNonEmpty(aeropuertosSolicitados, "No aplica"),
                FechaSolicitud = DateTime.Now,
                LugarEmision = FirstNonEmpty(orden.LugarEmision, "Quito"),
                CorreoRT = FirstNonEmpty(usuario?.Email, orden.Correo, "No aplica"),
                TelefonoRT = FirstNonEmpty(orden.Telefono, "No aplica"),
                RucCedula = FirstNonEmpty(orden.RucCedula, "No aplica"),
                CodigoConcepto = CodigoConceptoInspeccionExt,
                NumeroOrden = FirstNonEmpty(orden.NumeroOrden, orden.Id.ToString()),
                TextoResolucion = "Resolución 066-2010 (01 de julio de 2010), Art. 14"
            };
        }

        private byte[] BuildSolicitudInspeccionPdfBytes(OrdenRecaudacionModel orden, string aeropuertosSolicitados, out string nombreArchivo, out int paginasGeneradas)
        {
            var aeropuertos = string.IsNullOrWhiteSpace(aeropuertosSolicitados) ? "No aplica" : aeropuertosSolicitados.Trim();
            var pdfModel = BuildSolicitudInspeccionPdfModel(orden, aeropuertos);
            nombreArchivo = ConstruirNombrePdfSolicitudInspeccion(orden);

            var pdf = new PartialViewAsPdf("SolicitudInspeccionesPdf", pdfModel)
            {
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(0, 0, 0, 0),
                CustomSwitches = PdfBrandingHelper.StandardRotativaSwitchesInlineBranding
            };

            var bytes = pdf.BuildFile(ControllerContext);
            bytes = PdfBrandingHelper.ApplyLetterheadBackground(
                bytes,
                Server,
                "OrdenRecaudacionController.BuildSolicitudInspeccionPdfBytes");

            paginasGeneradas = ObtenerNumeroPaginasPdf(bytes);
            return bytes;
        }

        private string ObtenerAeropuertosSolicitudInspeccion(OrdenRecaudacionModel orden, Documento documento)
        {
            var sessionKey = orden != null ? "SolicitudInspeccionAeropuertos_" + orden.Id : null;
            var aeropuertosSesion = !string.IsNullOrWhiteSpace(sessionKey) ? (Session[sessionKey] as string) : null;
            if (!string.IsNullOrWhiteSpace(aeropuertosSesion))
            {
                return aeropuertosSesion.Trim();
            }

            var observaciones = documento != null ? (documento.Observaciones ?? string.Empty) : string.Empty;
            const string marker = "Aeropuertos=";
            var start = observaciones.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                start += marker.Length;
                var end = observaciones.IndexOf(';', start);
                var value = end >= 0 ? observaciones.Substring(start, end - start) : observaciones.Substring(start);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return "No aplica";
        }

        private void RefrescarArchivoSolicitudInspeccion(Documento documento, byte[] bytes)
        {
            if (documento == null || bytes == null || bytes.Length == 0 || string.IsNullOrWhiteSpace(documento.RutaGuardada))
            {
                return;
            }

            try
            {
                var rutaFisica = ResolverRutaArchivoRegistrado(documento.RutaGuardada);
                if (!string.IsNullOrWhiteSpace(rutaFisica))
                {
                    var directorio = Path.GetDirectoryName(rutaFisica);
                    if (!string.IsNullOrWhiteSpace(directorio))
                    {
                        Directory.CreateDirectory(directorio);
                    }

                    System.IO.File.WriteAllBytes(rutaFisica, bytes);
                }
            }
            catch
            {
            }
        }

        private string ConstruirNombrePdfSolicitudInspeccion(OrdenRecaudacionModel orden)
        {
            var numeroOrden = orden != null ? FirstNonEmpty(orden.NumeroOrden, orden.Id.ToString()) : string.Empty;
            var compania = orden != null ? FirstNonEmpty(orden.Compania, orden.RucCedula) : string.Empty;
            var baseName = PdfFileNameHelper.LimpiarNombreArchivo(
                PdfFileNameHelper.CombinarSegmentos("Solicitud", "Inspecciones", numeroOrden, compania, DateTime.Now.ToString("yyyyMMddHHmmss")));

            return (string.IsNullOrWhiteSpace(baseName) ? "Solicitud_Inspecciones" : baseName) + ".pdf";
        }

        private string GuardarBytesAocr(byte[] bytes, string folderRelative, string nombreArchivo)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException("El PDF generado no tiene contenido.");
            }

            var safeFileName = PdfFileNameHelper.LimpiarNombreArchivo(Path.GetFileNameWithoutExtension(nombreArchivo));
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                safeFileName = "Solicitud_Inspecciones";
            }

            safeFileName = safeFileName + ".pdf";
            var normalizedFolder = (folderRelative ?? string.Empty).Trim('~', '/', '\\');
            var basePath = FileStorageHelper.GetPhysicalBasePath(FileStorageHelper.BasePathStorage);
            var targetFolder = string.IsNullOrWhiteSpace(normalizedFolder)
                ? basePath
                : Path.Combine(basePath, normalizedFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(targetFolder);

            var fullPath = Path.Combine(targetFolder, safeFileName);
            System.IO.File.WriteAllBytes(fullPath, bytes);

            var baseVirtual = FileStorageHelper.NormalizeStoredPath(FileStorageHelper.BasePathStorage).TrimEnd('/');
            return FileStorageHelper.NormalizeStoredPath(baseVirtual + "/" + normalizedFolder.Replace("\\", "/").Trim('/') + "/" + safeFileName);
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
        public ActionResult DebugOrdenNumero()
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

        private List<OrdenRecaudacion> FiltrarOrdenesPorCompaniaActiva(List<OrdenRecaudacion> ordenes, int idUsuario)
        {
            var codigo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            var nombre = CompaniaActivaSessionHelper.ObtenerNombre(Session);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                var totalCompanias = (new UsuarioCompaniaRTDAO().ObtenerCompaniasAsignadas(idUsuario, true) ?? new List<UsuarioCompaniaRT>()).Count;
                if (totalCompanias > 1)
                {
                    return new List<OrdenRecaudacion>();
                }

                return ordenes ?? new List<OrdenRecaudacion>();
            }

            return _companiaContextService
                .FiltrarOrdenesPorCompania(ordenes, codigo, nombre, idUsuario)
                .ToList();
        }

        private List<OrdenRecaudacionModel> FiltrarOrdenesModelPorCompaniaActiva(List<OrdenRecaudacionModel> ordenes, int idUsuario)
        {
            var codigo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            var nombre = CompaniaActivaSessionHelper.ObtenerNombre(Session);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return ordenes ?? new List<OrdenRecaudacionModel>();
            }

            var filtradas = _companiaContextService.FiltrarOrdenesPorCompania(
                (ordenes ?? new List<OrdenRecaudacionModel>()).Select(o => _dao.ObtenerOrdenPorId(o.Id)).Where(o => o != null),
                codigo,
                nombre,
                idUsuario);

            var ids = new HashSet<int>(filtradas.Select(o => o.Id));
            return (ordenes ?? new List<OrdenRecaudacionModel>()).Where(o => ids.Contains(o.Id)).ToList();
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

        private void PrefillDesdeUltimaOrden(int userId, CapaPresentacion.Models.OrdenRecaudacionNuevaVM model, string companiaCodigo = null, string companiaNombre = null)
        {
            if (userId <= 0 || model?.Orden == null) return;

            try
            {
                var codigo = (companiaCodigo ?? CompaniaActivaSessionHelper.ObtenerCodigo(Session) ?? string.Empty).Trim();
                var nombre = (companiaNombre ?? CompaniaActivaSessionHelper.ObtenerNombre(Session) ?? string.Empty).Trim();
                var ordenesUsuario = _dao.ListarPorUsuario(userId, null) ?? new List<OrdenRecaudacion>();
                var ordenesCompania = string.IsNullOrWhiteSpace(codigo)
                    ? ordenesUsuario
                    : _companiaContextService.FiltrarOrdenesPorCompania(ordenesUsuario, codigo, nombre, userId);

                var ultimaOrden = ordenesCompania
                    .OrderByDescending(o => o.FechaCreacion)
                    .FirstOrDefault();
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
                var codigoCompaniaActiva = (CompaniaActivaSessionHelper.ObtenerCodigo(Session) ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(codigoCompaniaActiva))
                {
                    return string.Empty;
                }

                var nombreSesion = (CompaniaActivaSessionHelper.ObtenerNombre(Session) ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(nombreSesion))
                {
                    return nombreSesion;
                }

                var userId = GetUserId();
                if (userId > 0)
                {
                    var nombre = _companiaContextService.ResolverNombreCompaniaAsignada(userId, codigoCompaniaActiva);
                    if (!string.IsNullOrWhiteSpace(nombre))
                    {
                        return nombre;
                    }
                }

                return codigoCompaniaActiva;
            }
            catch
            {
                return string.Empty;
            }
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

                        return string.IsNullOrWhiteSpace(codigoCompania);
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
                    AgregarCandidato(
                        _companiaContextService.ResolverRucCompaniaAsignada(userId, codigoCompaniaActiva),
                        true,
                        "compania_activa.asignacion.usuoid");

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
                        .Where(s => string.IsNullOrWhiteSpace(codigoCompaniaActiva) || CompaniaCoincide(s, codigoCompaniaActiva))
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
                new SelectListItem { Text = "Todos los estados", Value = "" },
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
        [AocrAuthorize(Modulo = "Financiero", Accion = "AprobarPago", CodigoOrdenParameter = "ordenId")]
        public ActionResult ValidarPago(int ordenId, int pagoId)
        {
            try
            {
                string usuario = User.Identity.Name ?? "SISTEMA";
                var userId = GetUserId();

                var resultado = new FinancieroAprobacionPagoOrchestrator().AprobarPagoCompleto(
                    ordenId,
                    pagoId,
                    usuario,
                    userId);

                if (resultado.Exito)
                {
                    TempData["Success"] = resultado.Idempotente
                        ? "El pago ya estaba aprobado. Solicitud AOCR habilitada para el RT."
                        : "Pago aprobado. Solicitud AOCR habilitada y orden cerrada para el RT.";

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
                        (string.IsNullOrWhiteSpace(resultado.Error) ? "" : " Detalle: " + resultado.Error);
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
        [AocrAuthorize(Modulo = "Financiero", Accion = "RechazarPago", CodigoOrdenParameter = "ordenId")]
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
