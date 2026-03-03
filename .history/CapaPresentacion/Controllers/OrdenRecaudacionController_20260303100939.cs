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

        // ? Para confirmar conexió real a DB (útil en producció)
        [Authorize(Roles = "Administrador,Financiero")]
        public JsonResult DbPing()
        {
            return Json(new { ok = _dao.Ping() }, JsonRequestBehavior.AllowGet);
        }

        // DiagnÓstico de sesió (SIN autorizació para debug)
        [AllowAnonymous]
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

            var ordenes = _dao.ListarPorUsuario(idUsuario, null) ?? new List<OrdenRecaudacion>();
            System.Diagnostics.Debug.WriteLine(string.Format("Obligatoria: Se encontraron {0} Órdenes", ordenes.Count));

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
            // Prefill bÃ¡sico desde usuario/empresa (editables)
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
                // Si falla la autogeneració, dejar el flujo normal con selecció
            }

            // Completar campos faltantes con la última orden registrada del usuario.
            PrefillDesdeUltimaOrden(userId, model);
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
                var codigoSolicitud = int.TryParse(model.Orden?.CodigoSolicitud?.ToString(), out int cs) ? (int?)cs : null;
                var lugarEmisionDb = ResolverLugarEmisionDesdeDb(codigoSolicitud, idUsuario);

                var orden = new OrdenRecaudacion
                {
                    NumeroOrden = numeroOrden,
                    CodigoUsuario = idUsuario,
                    CodigoSolicitud = codigoSolicitud,
                    LugarEmision = lugarEmisionDb,
                    Compania = model.Orden?.Compania,
                    NombreContribuyente = model.Orden?.Compania,
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

            CompletarDatosOrdenParaVista(orden);

            System.Diagnostics.Debug.WriteLine($"Controller Detalles: ordenId = {id}, numeroOrden = {orden.NumeroOrden}");

            try
            {
                ViewBag.Pagos = await _dao.ObtenerPagosPorOrdenAsync(id);
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
        public ActionResult Editar(OrdenRecaudacionModel model)
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
            if (orden == null || orden.CodigoUsuario != idUsuario)
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

        private async Task EnviarNotificacionOrdenGeneradaAsync(OrdenRecaudacionModel orden)
        {
            try
            {
                if (orden == null) return;

                var emailDestino = orden.Correo;
                if (string.IsNullOrWhiteSpace(emailDestino))
                {
                    CapaModelo.SolicitudAOCR solicitud = null;
                    int codigoSolicitudInt = 0;
                    if (!string.IsNullOrEmpty(orden.CodigoSolicitud) && int.TryParse(orden.CodigoSolicitud, out codigoSolicitudInt) && codigoSolicitudInt > 0)
                    {
                        var solicitudDAO = new CapaDatos.DAOs.SolicitudDAO();
                        solicitud = solicitudDAO.ObtenerPorId(codigoSolicitudInt);
                    }
                    else if (!string.IsNullOrWhiteSpace(orden.RucCedula))
                    {
                        codigoSolicitudInt = _dao.ObtenerCodigoSolicitudPorRuc(orden.RucCedula);
                        if (codigoSolicitudInt > 0)
                        {
                            var solicitudDAO = new CapaDatos.DAOs.SolicitudDAO();
                            solicitud = solicitudDAO.ObtenerPorId(codigoSolicitudInt);
                        }
                    }

                    emailDestino = solicitud?.Email;
                }

                if (string.IsNullOrWhiteSpace(emailDestino)) return;

                // Obtener lista de bancos (se mantiene para otros usos, pero la leyenda con cuentas
                // se toma del modelo PDF para asegurar consistencia entre correo y comprobante)
                var bancos = _bancoDao.ObtenerBancos();

                var pdfModel = BuildOrdenRecaudacionPdfModel(orden);
                pdfModel.LeyendaBancos = @"Para los servicios AEROPORTUARIOS y/o AERONAUTICOS, use las siguientes cuentas. Realice el pago con 72 horas de anticipación.<br><br>
<b>Banco Pichincha</b><br>
Cuenta Corriente: 2100310688<br>
Sublínea: 30200 (en depósitos)<br>
Titular: Dirección General de Aviación Civil<br>
RUC: 1768014410001<br>
En transferencias NO colocar sublínea<br><br>
<b>Banco Internacional</b><br>
Cuenta Corriente: 520608140<br>
Sublínea: 30200 (en depósitos)<br>
Titular: Dirección General de Aviación Civil<br>
RUC: 1768014410001<br>
En transferencias NO colocar sublínea<br><br>
<b>Banco Rumiñahui</b><br>
Cuenta Corriente: 8002531204<br>
Sublínea: 30200 (en depósitos)<br>
Titular: Dirección General de Aviación Civil<br>
RUC: 1768014410001<br>
En transferencias NO colocar sublínea<br>";

                // Usar la leyenda detallada (con números de cuenta) también en el cuerpo del correo
                string bancosHtml = "<ul style='margin:0;padding-left:18px;'>" + string.Join("", bancos.Select(b => $"<li>{b.Descripcion}</li>")) + "</ul>";
                string leyendaBancos = $"<p><strong>Puede realizar el pago de la orden en los siguientes bancos autorizados:</strong></p>{pdfModel.LeyendaBancos}";
                var nombreArchivo = "Comprobante_Orden_" + (orden.NumeroOrden ?? orden.Id.ToString()) + ".pdf";
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

                var asunto = $"Orden de recaudación generada - {orden.NumeroOrden}";
                var contribuyente = orden.NombreContribuyente ?? orden.Compania ?? "Contribuyente";
                var cuerpo = $@"
                    <h2>Orden de recaudación generada</h2>
                    <p>Estimado/a <strong>{contribuyente}</strong>,</p>
                    <p>Su orden de recaudación ha sido generada correctamente.</p>
                    <p><strong>Número de Orden:</strong> {orden.NumeroOrden}</p>
                    <p><strong>Monto Total:</strong> ${orden.Total:N2}</p>
                    <p>Se adjunta el comprobante en PDF.</p>
                    {leyendaBancos}
                ";

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"ADVERTENCIA: El PDF de la orden no se generó correctamente, no se adjuntará al correo. Orden: {orden.NumeroOrden}");
                }
                else
                {
                    var servicioCorreo = new CapaDatos.Services.EnviarCorreo();
                    var enviado = servicioCorreo.enviaMensajeCorreoConAdjunto(emailDestino, asunto, cuerpo, pdfBytes, nombreArchivo, "application/pdf");
                    System.Diagnostics.Debug.WriteLine($"Correo enviado con adjunto PDF: {enviado}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error enviando notificación de orden generada: " + ex.Message);
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
            catch (Exception ex)
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

            var estadoOrden = (orden.Estado ?? "").Trim();
            if (!estadoOrden.Equals("PENDIENTE", StringComparison.OrdinalIgnoreCase) &&
                !estadoOrden.Equals("GENERADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se puede subir comprobante cuando la orden esté en GENERADA o PENDIENTE.";
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

                    TempData["OK"] = "Comprobante enviado. La orden está en revisión financiera.";
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
                    PageMargins = new Rotativa.Options.Margins(0, 0, 0, 0),
                    CustomSwitches = PdfBrandingHelper.StandardRotativaSwitches
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
                ConceptoPrincipal = FirstNonEmpty(detallesPdf.FirstOrDefault()?.Concepto, solicitud?.DescripcionOperacion, "Inspeccion y Certificacion AOCR"),
                Referencia = $"Orden de recaudacion {ordenModel.NumeroOrden} - Solicitud {referenciaSolicitud}",
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

                if (string.IsNullOrWhiteSpace(model.Orden.Compania) && !string.IsNullOrWhiteSpace(ultimaOrden.Compania))
                    model.Orden.Compania = ultimaOrden.Compania;

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

        private string ResolverLugarEmisionDesdeDb(int? codigoSolicitud, int codigoUsuario, string fallback = null)
        {
            try
            {
                if (codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
                {
                    var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud.Value);
                    if (solicitud != null)
                    {
                        var codCiudad = FirstNonEmpty(
                            NormalizarCodigoCiudad(solicitud.CodCiudad),
                            NormalizarCodigoCiudad(ObtenerCodCiudadSolicitudDesdePostgres(codigoSolicitud.Value)),
                            NormalizarCodigoCiudad(solicitud.Ciudad));

                        var lugarDesdeAs400 = ResolverEstacionDesdeAs400(codCiudad);
                        if (!string.IsNullOrWhiteSpace(lugarDesdeAs400))
                        {
                            return lugarDesdeAs400;
                        }

                        if (!string.IsNullOrWhiteSpace(solicitud.Ciudad))
                        {
                            return solicitud.Ciudad.Trim();
                        }
                    }
                }

                if (codigoUsuario > 0)
                {
                    var codCiudadUsuario = FirstNonEmpty(
                        NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdePostgres(codigoUsuario)),
                        NormalizarCodigoCiudad(ObtenerCodCiudadUsuarioDesdeAs400(codigoUsuario)));

                    var lugarUsuarioDesdeAs400 = ResolverEstacionDesdeAs400(codCiudadUsuario);
                    if (!string.IsNullOrWhiteSpace(lugarUsuarioDesdeAs400))
                    {
                        return lugarUsuarioDesdeAs400;
                    }

                    var solicitudConCiudad = _solicitudDao
                        .ObtenerPorUsuario(codigoUsuario)
                        .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Ciudad));

                    if (solicitudConCiudad != null && !string.IsNullOrWhiteSpace(solicitudConCiudad.Ciudad))
                    {
                        return solicitudConCiudad.Ciudad.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ResolverLugarEmisionDesdeDb: " + ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback.Trim();
            }

            return "Quito";
        }

        private static string ResolverEstacionDesdeAs400(string codCiudad)
        {
            if (string.IsNullOrWhiteSpace(codCiudad))
            {
                return null;
            }

            try
            {
                var daoUbicacion = CD_UbicacionUsuario.Instancia;

                var ubicacionUsuario = daoUbicacion.UbicacionUsuarioPorCiudad(codCiudad);
                if (!string.IsNullOrWhiteSpace(ubicacionUsuario?.Estacion))
                {
                    return ubicacionUsuario.Estacion.Trim();
                }

                var ubicacionAeropuerto = daoUbicacion.UbicacionAeropuertoUsuarioPorCiudad(codCiudad);
                if (!string.IsNullOrWhiteSpace(ubicacionAeropuerto?.Estacion))
                {
                    return ubicacionAeropuerto.Estacion.Trim();
                }
            }
            catch
            {
                // Si AS400 no responde, conservar fallback de la app sin bloquear flujo.
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

        private string ObtenerCodCiudadUsuarioDesdeAs400(int codigoUsuario)
        {
            if (codigoUsuario <= 0)
            {
                return null;
            }

            try
            {
                var usuario = UsuarioDAO.ObtenerPorId(codigoUsuario);
                if (usuario == null || string.IsNullOrWhiteSpace(usuario.CodigoUsuario))
                {
                    return null;
                }

                var as400Dao = new UsuarioAS400DAO(new SecureConfig());
                return as400Dao.ObtenerCodigoCiudadPorCodigoUsuario(usuario.CodigoUsuario);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ObtenerCodCiudadUsuarioDesdeAs400: " + ex.Message);
                return null;
            }
        }

        private string ObtenerCodCiudadDesdePostgres(string tabla, string whereClause, Action<Npgsql.NpgsqlCommand> bindParams)
        {
            try
            {
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
                    var disponibles = candidatas.Where(c => columnas.Contains(c)).ToList();
                    if (disponibles.Count == 0)
                    {
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
                var comprobanteService = new ComprobanteService();
                if (!comprobanteService.ExisteComprobanteValido(ordenId, out var mensajeComprobante))
                {
                    TempData["Error"] = mensajeComprobante;
                    return RedirectToAction("Detalles", new { id = ordenId });
                }

                string usuario = User.Identity.Name ?? "SISTEMA";
                var resultado = _dao.ActualizarPagoEstadoPorId(
                    ordenId,
                    pagoId,
                    CapaDatos.Constants.EstadoPago.Validado,
                    usuario,
                    "Pago validado");
                
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
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO();
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
                var bancoPDao = new CapaDatos.DAOs.BancoP9DAO();
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
