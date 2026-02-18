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
using CapaPresentacion.Models.EmailTemplates;
using CapaPresentacion.Services;
using CapaDatos.Services;
using CapaModelo;
using CapaNegocio.Services;
using Rotativa;
// Alias para evitar ambigÔøΩedad
using EmailSvc = CapaDatos.Services.EmailService;
using SecureConfig = CapaDatos.Services.SecureConfigurationService;
using DetalleOrden = CapaDatos.Entidades.DetalleOrden;
using CapaDatos.Constants;
using CapaNegocio;

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
            var logPath = @"C:\AOCR\AOCR\AOCR05-01-2026\AOCR1\AOCR\debug_nueva.txt";
            try
            {
                System.IO.File.AppendAllText(logPath, $"\n=== Constructor() INICIADO {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===\n");
                System.IO.File.AppendAllText(logPath, "Creando OrdenRecaudacionDAO...\n");
                _ordenDAO = new OrdenRecaudacionDAO();
                System.IO.File.AppendAllText(logPath, "OrdenRecaudacionDAO creado OK\n");
                System.Diagnostics.Debug.WriteLine("OrdenRecaudacionController inicializado correctamente");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, "*** ERROR OrdenRecaudacionDAO: " + ex.ToString() + "\n");
                System.Diagnostics.Debug.WriteLine("ERROR en constructor OrdenRecaudacionController: " + ex.Message);
                _ordenDAO = null;
            }

            // Eliminar EmailService del orquestador, solo usar EnviarCorreo para notificaciones
            try
            {
                System.IO.File.AppendAllText(logPath, "Creando OrdenRecaudacionOrchestrator...\n");
                _orchestrator = new OrdenRecaudacionOrchestrator(
                    new OrdenRecaudacionDAO(),
                    new PagoDAO(),
                    null,
                    null,
                    null, // EmailService eliminado
                    null
                );
                System.IO.File.AppendAllText(logPath, "OrdenRecaudacionOrchestrator creado OK\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, "*** ERROR Orchestrator: " + ex.ToString() + "\n\n");
                throw;
            }
        }

        // ? Para confirmar conexi√≥n real a DB (√∫til en producci√≥n)
        [Authorize(Roles = "Administrador,Financiero")]
        public JsonResult DbPing()
        {
            return Json(new { ok = _dao.Ping() }, JsonRequestBehavior.AllowGet);
        }

        // Diagn√≥stico de sesi√≥n (SIN autorizaci√≥n para debug)
        [AllowAnonymous]
        public ActionResult DiagnosticoSesion()
        {
            var diagnostico = new System.Text.StringBuilder();
            diagnostico.AppendLine("=== DIAGN√ìSTICO DE SESI√ìN ===\n");
            diagnostico.AppendLine($"Fecha/Hora: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n");
            
            // Usuario autenticado
            diagnostico.AppendLine($"User.Identity.IsAuthenticated: {User?.Identity?.IsAuthenticated ?? false}");
            diagnostico.AppendLine($"User.Identity.Name: {User?.Identity?.Name ?? "null"}");
            
            // Roles
            var principal = User as System.Security.Principal.GenericPrincipal;
            if (principal != null && principal.IsInRole("Solicitante"))
                diagnostico.AppendLine("‚úì Usuario TIENE rol 'Solicitante'");
            else
                diagnostico.AppendLine("‚úó Usuario NO tiene rol 'Solicitante'");
                
            if (principal != null && principal.IsInRole("Administrador"))
                diagnostico.AppendLine("‚úì Usuario TIENE rol 'Administrador'");
            else
                diagnostico.AppendLine("‚úó Usuario NO tiene rol 'Administrador'");
                
            if (principal != null && principal.IsInRole("Operador"))
                diagnostico.AppendLine("‚úì Usuario TIENE rol 'Operador'");
            else
                diagnostico.AppendLine("‚úó Usuario NO tiene rol 'Operador'");
            
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
            
            // Sesi√≥n
            diagnostico.AppendLine($"\nSession['IdUsuario']: {Session["IdUsuario"]?.ToString() ?? "null"}");
            diagnostico.AppendLine($"Session['UserId']: {Session["UserId"]?.ToString() ?? "null"}");
            diagnostico.AppendLine($"Session['Correo']: {Session["Correo"]?.ToString() ?? "null"}");
            diagnostico.AppendLine($"Session['Rol']: {Session["Rol"]?.ToString() ?? "null"}");
            
            // Intentar acceso a Nueva
            diagnostico.AppendLine($"\n¬øPuede acceder a Nueva?: {(principal != null && (principal.IsInRole("Solicitante") || principal.IsInRole("Administrador") || principal.IsInRole("Operador")) ? "S√ç" : "NO")}");
            
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

            var ordenes = _dao.ListarPorUsuarioModel(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();

            // EstadÔøΩsticas: tu view espera claves con mayÔøΩscula
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
            System.Diagnostics.Debug.WriteLine(string.Format("Obligatoria: Se encontraron {0} √≥rdenes", ordenes.Count));

            // Estadisticas
            var est = _dao.ObtenerEstadisticas(idUsuario);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);
            ViewBag.TieneOrdenBorrador = ordenes.Any(o => string.Equals((o.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase));

            return View(ordenes);
        }

        // GET: /OrdenRecaudacion/Nueva
        [Authorize(Roles = "Solicitante,Administrador,Operador")]
        public ActionResult Nueva()
        {
            var model = new CapaPresentacion.Models.OrdenRecaudacionNuevaVM();
            CargarConceptosNueva(model);
            var userId = GetUserId();
            // Prefill b√°sico desde usuario/empresa (editables)
            try
            {
                var usuario = UsuarioDAO.ObtenerPorId(userId);
                var empresaNombre = "";
                if (!string.IsNullOrWhiteSpace(usuario?.EmpresaCodigo))
                {
                    var daoEmpresa = new EmpresaAS400DAO();
                    var empresa = daoEmpresa.ObtenerEmpresaPorCodigo(usuario.EmpresaCodigo);
                    empresaNombre = empresa?.Nombre ?? "";
                }

                if (!string.IsNullOrWhiteSpace(empresaNombre))
                    model.Orden.Compania = empresaNombre;
                else if (!string.IsNullOrWhiteSpace(usuario?.NombreCompleto))
                    model.Orden.Compania = usuario.NombreCompleto;

                var rucCedula = ExtraerRucCedula(usuario?.CodigoUsuario ?? usuario?.NombreUsuario);
                if (!string.IsNullOrWhiteSpace(rucCedula))
                    model.Orden.RucCedula = rucCedula;

                if (!string.IsNullOrWhiteSpace(usuario?.Email))
                    model.Orden.Correo = usuario.Email;
            }
            catch
            {
                // ignorar prefill si falla
            }

            try
            {
                var solicitudAuto = CrearSolicitudAuto(userId);
                if (solicitudAuto != null && solicitudAuto.CodigoSolicitud > 0)
                {
                    model.Solicitudes = new List<CapaPresentacion.Models.OrdenRecaudacionNuevaVM.SolicitudOptionVM>
                    {
                        new CapaPresentacion.Models.OrdenRecaudacionNuevaVM.SolicitudOptionVM
                        {
                            Id = solicitudAuto.CodigoSolicitud,
                            Numero = solicitudAuto.NumeroSolicitud,
                            Nombre = solicitudAuto.NombreOperador,
                            Label = solicitudAuto.NumeroSolicitud,
                            Ruc = solicitudAuto.Ruc,
                            Correo = solicitudAuto.Email,
                            Telefono = solicitudAuto.Telefono,
                            Compania = string.IsNullOrWhiteSpace(solicitudAuto.RazonSocial) ? solicitudAuto.NombreOperador : solicitudAuto.RazonSocial
                        }
                    };
                    model.Orden.CodigoSolicitud = solicitudAuto.CodigoSolicitud;
                    // Prefill de campos desde DB (editables)
                    var compania = !string.IsNullOrWhiteSpace(solicitudAuto.RazonSocial)
                        ? solicitudAuto.RazonSocial
                        : solicitudAuto.NombreOperador;
                    if (!string.IsNullOrWhiteSpace(compania)) model.Orden.Compania = compania;
                    if (!string.IsNullOrWhiteSpace(solicitudAuto.Ruc)) model.Orden.RucCedula = solicitudAuto.Ruc;
                    if (!string.IsNullOrWhiteSpace(solicitudAuto.Email)) model.Orden.Correo = solicitudAuto.Email;
                    if (!string.IsNullOrWhiteSpace(solicitudAuto.Telefono)) model.Orden.Telefono = solicitudAuto.Telefono;
                }
            }
            catch
            {
                // Si falla la autogeneraci√≥n, dejar el flujo normal con selecci√≥n
            }
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

                decimal subtotal = 0m, admin = 0m;

                foreach (var det in detalles)
                {
                    var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
                    var porcentajeAdmin = concepto?.PorcentajeAdmin ?? 0m;
                    subtotal += det.Subtotal;
                    admin += det.Subtotal * (porcentajeAdmin / 100m);
                }
                var total = subtotal + admin;

                if (total <= 0)
                {
                    ModelState.AddModelError("", "El total de la orden debe ser mayor a cero.");
                    CargarConceptosNueva(model);
                    return View(model);
                }

                var idUsuario = GetUserId();
                if (idUsuario <= 0)
                {
                    ModelState.AddModelError("", "Usuario no autenticado.");
                    CargarConceptosNueva(model);
                    return View(model);
                }

                var numeroOrden = await GenerarNumeroOrdenAsync();

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
                    Telefono = model.Orden?.Telefono,
                    Observacion = model.Orden?.Observacion,
                    Observaciones = model.Orden?.Observacion,
                    Subtotal = subtotal,
                    Admin = admin,
                    Total = total,
                    Estado = "GENERADA",
                    FechaCreacion = DateTime.Now,
                    Activo = true,
                    Detalles = new List<DetalleOrden>()
                };

                if (string.IsNullOrWhiteSpace(orden.Correo) && orden.CodigoSolicitud.HasValue && orden.CodigoSolicitud.Value > 0)
                {
                    var solicitud = _solicitudDao.ObtenerPorId(orden.CodigoSolicitud.Value);
                    if (!string.IsNullOrWhiteSpace(solicitud?.Email))
                    {
                        orden.Correo = solicitud.Email.Trim();
                    }
                }

                foreach (var det in detalles)
                {
                    var concepto = _conceptoDao.ObtenerPorId(det.ConceptoId);
                    var porcentajeAdmin = concepto?.PorcentajeAdmin ?? 0m;
                    var adminLinea = det.Subtotal * (porcentajeAdmin / 100m);
                    var totalLinea = det.Subtotal + adminLinea;

                    orden.Detalles.Add(new DetalleOrden
                    {
                        ConceptoId = det.ConceptoId,
                        ConceptoCodigo = concepto?.Codigo,
                        ConceptoNombre = concepto?.Nombre,
                        Cantidad = det.Cantidad,
                        ValorUnitario = det.PrecioUnitario,
                        PorcentajeAdmin = porcentajeAdmin,
                        Subtotal = det.Subtotal,
                        Admin = adminLinea,
                        TotalLinea = totalLinea
                    });
                }

                var generarResult = await _bl.GenerarOrdenEnUnPasoAsync(orden, "Solicitante");
                if (generarResult != null && generarResult.Success && generarResult.OrdenId > 0)
                {
                    TempData["OK"] = "Orden " + numeroOrden + " generada correctamente.";
                    return RedirectToAction("Detalles", new { id = generarResult.OrdenId });
                }

                ModelState.AddModelError("", "Error al guardar/generar la orden: " + (generarResult != null ? generarResult.Error : "Error desconocido"));
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
            // Generar n√∫mero √∫nico con timestamp de microsegundos para evitar duplicados
            var timestamp = fecha.ToString("yyyyMMddHHmmssfff"); // Agregamos milisegundos (fff)
            var consecutivo = await _dao.ObtenerConsecutivoDiarioAsync(fecha) + 1;
            var numeroOrden = string.Format("OR-{0}-{1}", timestamp, consecutivo);
            
            System.Diagnostics.Debug.WriteLine($"GenerarNumeroOrdenAsync: timestamp={timestamp}, consecutivo={consecutivo}, resultado={numeroOrden}");
            
            // Verificar que no exista ya este n√∫mero (medida de seguridad adicional)
            int intentos = 0;
            var numeroFinal = numeroOrden;
            while (intentos < 10) // m√°ximo 10 intentos
            {
                if (!_dao.ExisteNumeroOrden(numeroFinal))
                {
                    break;
                }
                
                // Si existe, agregar un sufijo adicional
                intentos++;
                numeroFinal = string.Format("OR-{0}-{1}-{2}", timestamp, consecutivo, intentos);
                System.Diagnostics.Debug.WriteLine($"GenerarNumeroOrdenAsync: N√∫mero duplicado, intentando={numeroFinal}");
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

            int solicitudIdDetalle;
            var correoDetalle = ObtenerEmailDestinoGeneracion(orden, out solicitudIdDetalle);
            if (!string.IsNullOrWhiteSpace(correoDetalle))
            {
                orden.Correo = correoDetalle;
            }

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
            
            // Cargar m√©todos de pago desde P9
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
                ordenExistente.Correo = string.IsNullOrWhiteSpace(model.Correo) ? ordenExistente.Correo : model.Correo.Trim();
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
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public JsonResult Anular(int id)
        {
            return Json(new
            {
                success = false,
                message = "Use el formulario de anulaciÛn con motivo (mÌnimo 10 caracteres)."
            });
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
                TempData["Error"] = "Solo se pueden generar Ûrdenes en estado BORRADOR";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (orden.Total <= 0)
            {
                TempData["Error"] = "No se puede generar una orden sin conceptos";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                int solicitudId;
                var emailDestino = ObtenerEmailDestinoGeneracion(orden, out solicitudId);
                if (string.IsNullOrWhiteSpace(emailDestino))
                {
                    TempData["Error"] = "No se puede generar la orden sin un correo de notificaciÛn (orden o solicitud).";
                    return RedirectToAction("Detalles", new { id = id });
                }

                // Persistir correo destino en la orden si venÌa vacÌo, para no perderlo al recargar.
                _dao.ActualizarCorreoOrdenSiVacio(id, emailDestino);
                orden.Correo = emailDestino;

                var result = await _dao.CambiarEstadoOrdenAsync(id, "GENERADA");
                if (!result)
                {
                    TempData["Error"] = "No se pudo cambiar el estado de la orden.";
                    return RedirectToAction("Detalles", new { id = id });
                }

                await EnviarNotificacionOrdenGeneradaAsync(orden, emailDestino, solicitudId > 0 ? (int?)solicitudId : null);
                TempData["OK"] = "Orden generada correctamente.";
                return RedirectToAction("Detalles", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        private string ObtenerEmailDestinoGeneracion(OrdenRecaudacionModel orden, out int solicitudId)
        {
            solicitudId = 0;
            if (orden == null) return null;

            var emailDestino = (orden.Correo ?? string.Empty).Trim();
            int.TryParse(orden.CodigoSolicitud ?? string.Empty, out solicitudId);

            if (string.IsNullOrWhiteSpace(emailDestino) && solicitudId > 0)
            {
                var solicitud = new SolicitudAOCRDAO().ObtenerPorId(solicitudId);
                emailDestino = (solicitud?.Email ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(emailDestino))
                {
                    orden.Correo = emailDestino;
                    if (orden.Id > 0)
                    {
                        _dao.ActualizarCorreoOrdenSiVacio(orden.Id, emailDestino);
                    }
                }
            }

            return emailDestino;
        }

        private async Task EnviarNotificacionOrdenGeneradaAsync(OrdenRecaudacionModel orden, string emailDestino = null, int? solicitudId = null)
        {
            await Task.Run(() =>
            {
                try
                {
                    if (orden == null) return;

                    var idSolicitud = solicitudId ?? 0;
                    var destino = emailDestino;
                    if (string.IsNullOrWhiteSpace(destino))
                    {
                        destino = ObtenerEmailDestinoGeneracion(orden, out var solIdTmp);
                        if (idSolicitud <= 0) idSolicitud = solIdTmp;
                    }

                    if (string.IsNullOrWhiteSpace(destino))
                    {
                        CapaNegocio.LogBL.RegistrarError(
                            "Orden generada sin correo destino",
                            "OrdenId=" + orden.Id + " NumeroOrden=" + orden.NumeroOrden + " CodigoSolicitud=" + (orden.CodigoSolicitud ?? "N/A"),
                            "OrdenRecaudacionController");
                        return;
                    }

                    var notify = new CapaNegocio.Services.RechazoAnulacionNotificacionService();
                    notify.NotificarOrdenGeneradaAsync(
                        orden.Id,
                        idSolicitud > 0 ? (int?)idSolicitud : null,
                        orden.NumeroOrden,
                        destino,
                        orden.NombreContribuyente ?? orden.Compania,
                        "Solicitante",
                        DateTime.Now,
                        orden.Total,
                        orden.Observacion);
                }
                catch (Exception ex)
                {
                    CapaNegocio.LogBL.RegistrarError("Error encolando notificaciÛn de orden generada", ex.ToString(), "OrdenRecaudacionController");
                }
            });
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
                TempData["Error"] = "Solo se pueden enviar ÔøΩrdenes en estado GENERADA";
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
                // Filtrar conceptos √∫nicos por C√≥digo
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
            catch (Exception ex)
            {
                model.Conceptos = new List<CapaPresentacion.Models.ConceptoOptionVM>();
                ModelState.AddModelError("", "No se pudieron cargar los conceptos. Verifique la conexi√≥n a la base de datos.");
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
            catch (Exception ex)
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
                if (parametro != null && parametro.Activo && !string.IsNullOrEmpty(parametro.Valor))
                {
                    // Limpiar el valor: remover espacios, s√≠mbolos de moneda, etc.
                    var valorLimpio = parametro.Valor.Trim()
                        .Replace("$", "")
                        .Replace("USD", "")
                        .Replace(" ", "")
                        .Replace("_", "");
                    
                    // Intentar con InvariantCulture (1234.56)
                    if (decimal.TryParse(valorLimpio, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal valor))
                    {
                        return valor;
                    }
                    
                    // Intentar con cultura espa√±ola (1234,56 o 1.234,56)
                    var culturaEspanol = new System.Globalization.CultureInfo("es-ES");
                    if (decimal.TryParse(valorLimpio, System.Globalization.NumberStyles.Any,
                        culturaEspanol, out valor))
                    {
                        return valor;
                    }
                    
                    // √öltimo intento: reemplazar coma por punto y parsear
                    valorLimpio = valorLimpio.Replace(",", ".");
                    if (decimal.TryParse(valorLimpio, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out valor))
                    {
                        return valor;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"‚ö† No se pudo parsear '{clave}': valor='{parametro.Valor}' (limpio: '{valorLimpio}'). Usando valor por defecto: {valorPorDefecto}");
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
                    Nombre = "EmisiÛn AOCR", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_EMI_AOCR", 3300m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_EMI_AOCR", 0m), 
                    Activo = true, 
                    Orden = 1, 
                    Descripcion = "EmisiÛn AOCR", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "REN_AOCR", 
                    Nombre = "RenovaciÛn AOCR", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_REN_AOCR", 3300m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_REN_AOCR", 0m), 
                    Activo = true, 
                    Orden = 2, 
                    Descripcion = "RenovaciÛn AOCR", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "MOD_AOCR_INC", 
                    Nombre = "ModificaciÛn AOCR (InclusiÛn aeronaves distinto modelo y tipo)", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_MOD_AOCR_INC", 1600m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_MOD", 0m), 
                    Activo = true, 
                    Orden = 3, 
                    Descripcion = "ModificaciÛn AOCR (InclusiÛn aeronaves distinto modelo y tipo)", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "MOD_AOCR_SIN_INC", 
                    Nombre = "ModificaciÛn AOCR (Que no implique incremento de aeronaves)", 
                    TipoCalculo = "FIJO", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_MOD_AOCR_SIN_INC", 80m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_MOD", 0m), 
                    Activo = true, 
                    Orden = 4, 
                    Descripcion = "ModificaciÛn AOCR (Que no implique incremento de aeronaves)", 
                    PorEstacion = false, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "INSPECCION_EXT", 
                    Nombre = "InspecciÛn requerida por el Operador AÈreo Extranjero", 
                    TipoCalculo = "POR_ESTACION", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_INSPECCION_EXT", 500m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_INSPECCION", 0m), 
                    Activo = true, 
                    Orden = 5, 
                    Descripcion = "InspecciÛn requerida por el Operador AÈreo Extranjero (por estaciÛn)", 
                    PorEstacion = true, 
                    PorDia = false, 
                    EsViatico = false 
                },
                new CapaDatos.Models.ConceptoModel { 
                    Codigo = "VIATICOS_INSPECTOR", 
                    Nombre = "Vi·ticos a Sres. Inspectores", 
                    TipoCalculo = "POR_DIA", 
                    ValorBase = ObtenerTarifaConfigurable("TARIFA_VIATICOS_INSPECTOR", 80m), 
                    PorcentajeAdmin = ObtenerPorcentajeConfigurable("PORCENTAJE_ADMIN_VIATICOS", 8m), 
                    Activo = true, 
                    Orden = 6, 
                    Descripcion = "Vi·ticos por dÌa (m·s 8% de gastos administrativos)", 
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
                TempData["Error"] = "Solo se puede subir comprobante cuando la orden estÔøΩ en GENERADA o PENDIENTE.";
                return RedirectToAction("Detalles", new { id = id });
            }

            decimal montoValue;
            var montoRaw = (Monto ?? Request["Monto"] ?? "").Trim();
            if (!decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out montoValue) &&
                !decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out montoValue))
            {
                TempData["Error"] = "Monto invÔøΩlido";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (montoValue <= 0)
            {
                TempData["Error"] = "El monto debe ser mayor a cero";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(NumeroFactura))
            {
                // Generar n√∫mero de factura √∫nico autom√°ticamente
                NumeroFactura = $"PAG-{id}-{DateTime.Now:yyyyMMddHHmmss}";
            }

            if (string.IsNullOrWhiteSpace(MetodoPago))
            {
                TempData["Error"] = "Debe seleccionar un mÔøΩtodo de pago";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // Guardar comprobante si existe via helper central (FileStorageHelper)
                string comprobanteRuta = null;
                string savedVirtualPath = null;
                if (ComprobanteArchivo != null && ComprobanteArchivo.ContentLength > 0)
                {
                    // Validaci√≥n centralizada
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
                    // ‚úÖ Debe coincidir con chk_estado_pago (case-sensitive)
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
                TempData["Error"] = "La orden no estÔøΩ vinculada a una solicitud vÔøΩlida para registrar el pago.";
                return RedirectToAction("Detalles", new { id = id });
            }

                // Registrar pago + actualizar estado en una transacci√≥n at√≥mica en BD
                string pagoErr;
                bool transOk = _dao.RegistrarPagoYActualizarEstadoTransaccional(orden.Id, codigoSolicitud, pago, "PROCESADA", out pagoErr);
                if (!transOk)
                {
                    // Si guardamos archivo y la BD fall√≥, borrarlo para no dejar archivos hu√©rfanos
                    if (!string.IsNullOrWhiteSpace(savedVirtualPath))
                    {
                        CapaNegocio.Helpers.FileStorageHelper.DeleteFile(savedVirtualPath);
                        CapaNegocio.LogBL.RegistrarInfo($"Archivo eliminado por fallo transacci√≥n: Orden={orden.NumeroOrden} Ruta={savedVirtualPath}", "OrdenRecaudacionController");
                    }

                    CapaNegocio.LogBL.RegistrarError($"Error registrando pago/transacci√≥n Orden={orden.NumeroOrden} CodigoSolicitud={codigoSolicitud}", pagoErr ?? "n/a", "OrdenRecaudacionController");
                    TempData["Error"] = "No se pudo registrar el pago en la base de datos. " + (string.IsNullOrWhiteSpace(pagoErr) ? "" : ("Detalle: " + pagoErr));
                    return RedirectToAction("Detalles", new { id = id });
                }                try
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

                try
                {
                    EnviarConfirmacionComprobanteASolicitante(orden, pago, comprobanteRuta);
                }
                catch
                {
                    // No bloquear el flujo si el email falla
                }

                TempData["OK"] = "Comprobante enviado. La orden esta en revision financiera.";
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

            var estadoAnterior = (orden.Estado ?? string.Empty).Trim().ToUpperInvariant();
            if (estadoAnterior.Equals("FACTURADA", StringComparison.OrdinalIgnoreCase) ||
                estadoAnterior.Equals("COMPLETADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "No se pueden anular Ûrdenes aprobadas o facturadas.";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (estadoAnterior.Equals("ANULADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La orden ya est· anulada";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 10)
            {
                TempData["Error"] = "Debe proporcionar un motivo de anulaciÛn de al menos 10 caracteres.";
                return RedirectToAction("Detalles", new { id = id });
            }

            var motivoFinal = motivo.Trim();
            var usuario = User?.Identity?.Name ?? "SISTEMA";
            var rol = User != null && User.IsInRole("Administrador") ? "Administrador" : "Solicitante";

            try
            {
                var result = _dao.CambiarEstado(id, "ANULADA", motivoFinal);
                if (!result)
                {
                    TempData["Error"] = "Error al anular la orden";
                    return RedirectToAction("Detalles", new { id = id });
                }

                try
                {
                    new CapaDatos.DAOs.OrdenEstadoHistorialDAO().RegistrarCambio(id, estadoAnterior, "ANULADA", motivoFinal, usuario, rol);
                }
                catch { }

                try
                {
                    int solicitudId;
                    int.TryParse(orden.CodigoSolicitud ?? string.Empty, out solicitudId);
                    new CapaNegocio.Services.RechazoAnulacionNotificacionService().NotificarOrdenAsync(
                        id,
                        solicitudId > 0 ? (int?)solicitudId : null,
                        orden.NumeroOrden,
                        orden.Correo,
                        orden.NombreContribuyente ?? orden.Compania,
                        motivoFinal,
                        rol,
                        "ANULADA",
                        DateTime.Now);
                }
                catch { }

                TempData["OK"] = "Orden anulada correctamente";
                return RedirectToAction("Detalles", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

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
            bool conceptoPrincipalEsInspeccion = false;
            if (detalles.Count > 0)
            {
                var codigo = (detalles[0].ConceptoCodigo ?? "").ToUpperInvariant();
                var nombre = (detalles[0].ConceptoNombre ?? "").ToUpperInvariant();
                conceptoPrincipalEsInspeccion = codigo.Contains("INSP") || nombre.Contains("INSPECC");
            }
            foreach (var d in detalles)
            {
                var codigo = (d.ConceptoCodigo ?? "").ToUpperInvariant();
                var nombre = (d.ConceptoNombre ?? "").ToUpperInvariant();
                if (codigo.Contains("INSP") || nombre.Contains("INSPECC"))
                {
                    estaciones += d.Cantidad;
                }
                if (codigo.Contains("VIAT") || nombre.Contains("VIATIC") || nombre.Contains("VI√ÅTIC"))
                {
                    dias += d.Cantidad;
                }
            }

            var conceptoPrincipal = detalles.Count > 0 ? detalles[0].ConceptoNombre : null;
            var valorBase = ordenModel.Subtotal != 0 ? ordenModel.Subtotal : (ordenModel.Total != 0 ? ordenModel.Total : detalles.Sum(d => d.Subtotal));

            // Si el concepto principal es inspecci√≥n, no mostrar la l√≠nea adicional de inspecciones
            if (conceptoPrincipalEsInspeccion)
            {
                estaciones = 0;
            }

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
                Telefono = solicitud?.Telefono ?? ordenModel.Telefono ?? "Tel√©fono no especificado",
                Banco = string.IsNullOrWhiteSpace(bancoPago) ? "No especificado" : bancoPago,
                NumeroComprobante = string.IsNullOrWhiteSpace(numeroComp) ? "No registrado" : numeroComp,
                ConceptoPrincipal = conceptoPrincipal ?? solicitud?.DescripcionOperacion ?? "Inspecci√≥n y Certificaci√≥n AOCR",
                ValorBase = valorBase,
                Estaciones = (int)Math.Round(estaciones),
                Dias = (int)Math.Round(dias),
                Referencia = $"Orden de Recaudaci√≥n {ordenModel.NumeroOrden} - Solicitud {solicitud?.NumeroSolicitud ?? "N/A"}"
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
            
            // Agregar opci√≥n por defecto
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
                System.Diagnostics.Debug.WriteLine("GetUserId: No se encontrÔøΩ ID de usuario en la sesiÔøΩn");
            }
            return id;
        }

        private SolicitudAOCR CrearSolicitudAuto(int userId)
        {
            if (userId <= 0) return null;

            var usuario = UsuarioDAO.ObtenerPorId(userId);
            var blSolicitud = new SolicitudBL();
            var year = DateTime.Now.Year;
            var numero = blSolicitud.GenerarNumeroSolicitud(year);
            var empresaNombre = "";
            try
            {
                if (!string.IsNullOrWhiteSpace(usuario?.EmpresaCodigo))
                {
                    var daoEmpresa = new EmpresaAS400DAO();
                    var empresa = daoEmpresa.ObtenerEmpresaPorCodigo(usuario.EmpresaCodigo);
                    empresaNombre = empresa?.Nombre ?? "";
                }
            }
            catch
            {
                empresaNombre = "";
            }

            var rucCedula = ExtraerRucCedula(usuario?.CodigoUsuario ?? usuario?.NombreUsuario);

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
                Email = usuario?.Email ?? "",
                Telefono = "",
                Direccion = ""
            };

            var id = _solicitudDao.InsertarConReturn(solicitud);
            if (id > 0)
            {
                solicitud.CodigoSolicitud = id;
                return solicitud;
            }

            return null;
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

        // Metodo helper con tipo correcto.
        private void EnviarNotificacionAFinanciero(OrdenRecaudacionModel orden, CapaDatos.Models.PagoModel pago, string emailFinanciero, string comprobanteRuta)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(emailFinanciero)) return;

                var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
                CapaNegocio.LogBL.RegistrarInfo($"Notificando financiero: Orden={orden?.NumeroOrden} CodigoSolicitud={orden?.CodigoSolicitud}", "OrdenRecaudacionController");

                var vm = new PagoRecibidoFinancieroEmailVM
                {
                    NumeroOrden = orden?.NumeroOrden ?? "N/A",
                    NumeroSolicitud = orden?.CodigoSolicitud?.ToString() ?? "N/A",
                    NombreContribuyente = orden?.NombreContribuyente ?? "Usuario",
                    Monto = pago?.Monto ?? 0m,
                    MetodoPago = pago?.MetodoPago ?? "N/A",
                    NumeroComprobante = pago?.NumeroFactura ?? "N/A",
                    FechaPago = pago?.FechaPago ?? DateTime.Now
                };

                var cuerpo = RazorViewRenderer.RenderPartialViewToString(ControllerContext, "EmailTemplates/PagoRecibidoFinanciero", vm);
                var asunto = string.Format("Nueva Orden Pendiente de RevisiÛn - {0}", orden?.NumeroOrden ?? "N/A");

                string rutaFisica = null;
                string nombreAdjunto = null;
                if (!string.IsNullOrWhiteSpace(comprobanteRuta))
                {
                    var ruta = Server.MapPath(comprobanteRuta);
                    if (System.IO.File.Exists(ruta))
                    {
                        rutaFisica = ruta;
                        nombreAdjunto = Path.GetFileName(ruta);
                    }
                }

                var cs = new SecureConfig().GetConnectionString("PostgreSQL")
                    ?? ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                    ?? ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                    ?? string.Empty;

                var queue = new EmailQueueService(cs);
                var item = new EmailQueueItem
                {
                    Para = emailFinanciero,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    SolicitudId = int.TryParse(orden?.CodigoSolicitud?.ToString(), out var s) ? (int?)s : null,
                    OrdenId = orden?.Id,
                    NumeroOrden = orden?.NumeroOrden,
                    TipoNotificacion = "PagoRecibido",
                    CorrelationId = correlationId,
                    AdjuntoRuta = rutaFisica,
                    AdjuntoNombre = nombreAdjunto,
                    AdjuntoMimeType = string.IsNullOrWhiteSpace(rutaFisica) ? null : "application/pdf",
                    MaxIntentos = 3
                };

                queue.EncolarAsync(item).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error enviando notificaciÛn a financiero Orden={orden?.NumeroOrden} CodigoSolicitud={orden?.CodigoSolicitud}", ex.ToString(), "OrdenRecaudacionController");
            }
        }
        private void EnviarConfirmacionComprobanteASolicitante(OrdenRecaudacionModel orden, CapaDatos.Models.PagoModel pago, string comprobanteRuta)
        {
            try
            {
                var emailDestino = orden?.Correo;

                if (string.IsNullOrWhiteSpace(emailDestino))
                {
                    int codigoSolicitud;
                    if (int.TryParse(orden?.CodigoSolicitud, out codigoSolicitud) && codigoSolicitud > 0)
                    {
                        var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
                        emailDestino = solicitud?.Email;
                    }
                }

                if (string.IsNullOrWhiteSpace(emailDestino)) return;

                var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
                var vm = new CapaPresentacion.Models.EmailTemplates.PagoRecibidoSolicitanteEmailVM
                {
                    NumeroOrden = orden?.NumeroOrden ?? "N/A",
                    NumeroSolicitud = orden?.CodigoSolicitud?.ToString() ?? "N/A",
                    NombreContribuyente = orden?.NombreContribuyente ?? "Usuario",
                    Monto = pago?.Monto ?? 0m,
                    MetodoPago = pago?.MetodoPago ?? "N/A",
                    NumeroComprobante = pago?.NumeroFactura ?? "N/A",
                    FechaPago = pago?.FechaPago ?? DateTime.Now
                };

                var cuerpo = RazorViewRenderer.RenderPartialViewToString(ControllerContext, "EmailTemplates/PagoRecibidoSolicitante", vm);
                var asunto = string.Format("Confirmacion de envio de comprobante - Orden {0}", orden?.NumeroOrden ?? "N/A");

                string rutaFisica = null;
                string nombreAdjunto = null;
                if (!string.IsNullOrWhiteSpace(comprobanteRuta))
                {
                    var ruta = Server.MapPath(comprobanteRuta);
                    if (System.IO.File.Exists(ruta))
                    {
                        rutaFisica = ruta;
                        nombreAdjunto = Path.GetFileName(ruta);
                    }
                }

                var cs = new SecureConfig().GetConnectionString("PostgreSQL")
                    ?? ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                    ?? ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                    ?? string.Empty;

                var queue = new EmailQueueService(cs);
                var item = new EmailQueueItem
                {
                    Para = emailDestino,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    SolicitudId = int.TryParse(orden?.CodigoSolicitud?.ToString(), out var s) ? (int?)s : null,
                    OrdenId = orden?.Id,
                    NumeroOrden = orden?.NumeroOrden,
                    TipoNotificacion = "ComprobanteRecibido",
                    CorrelationId = correlationId,
                    AdjuntoRuta = rutaFisica,
                    AdjuntoNombre = nombreAdjunto,
                    AdjuntoMimeType = string.IsNullOrWhiteSpace(rutaFisica) ? null : "application/pdf",
                    MaxIntentos = 3
                };

                queue.EncolarAsync(item).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error enviando confirmacion al solicitante Orden={orden?.NumeroOrden} CodigoSolicitud={orden?.CodigoSolicitud}", ex.ToString(), "OrdenRecaudacionController");
            }
        }
        /// <summary>
        /// Validar un pago espec√≠fico
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        /// Rechazar un pago espec√≠fico
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
                    TempData["OK"] = $"Conexi√≥n AS400 exitosa: {resultado}";
                }
                else
                {
                    TempData["Error"] = $"Error en conexi√≥n AS400: {resultado}";
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
                
                if (resultado.StartsWith("‚úÖ"))
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


















