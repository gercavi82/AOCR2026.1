using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;
using CapaDatos;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaModelo;
using CapaDatos.Constants;
using CapaPresentacion.Models;
using CapaNegocio;
using CapaNegocio.Helpers;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class SolicitudAOCRController : Controller
    {
        private readonly SolicitudBL _solicitudBL = new SolicitudBL();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();
        private readonly DocumentoDAO _documentoDAO = new DocumentoDAO();
        private readonly HistorialEstadoDAO _historialDAO = new HistorialEstadoDAO();

        private readonly AeronaveSolicitudDAO _aeronaveSolDAO = new AeronaveSolicitudDAO();
        private readonly PagoDAO _pagoDAO = new PagoDAO();

        public ActionResult Index() => View();

        // Obtener solicitudes del usuario actual en formato JSON
        [HttpGet]
        public JsonResult ObtenerMisSolicitudes()
        {
            try
            {
                if (Session["CodigoUsuario"] == null && Session["IdUsuario"] != null)
                    Session["CodigoUsuario"] = Session["IdUsuario"];

                if (Session["CodigoUsuario"] == null)
                    return Json(new { success = true, data = new List<object>(), message = "Sesion expirada" }, JsonRequestBehavior.AllowGet);

                int codigoUsuario = Convert.ToInt32(Session["CodigoUsuario"]);
                var solicitudes = _solicitudDAO.ObtenerPorUsuario(codigoUsuario);

                var resultado = solicitudes.Select(s => new
                {
                    id = s.CodigoSolicitud,
                    fecha = (s.FechaSolicitud ?? s.CreatedAt ?? DateTime.Now).ToString("dd/MM/yyyy"),
                    tipo = ObtenerTipoSolicitud(s.TipoSolicitud),
                    comp = s.NombreOperador ?? s.RazonSocial ?? "Sin Compañía",
                    insp = ObtenerNombreInspector(s.CodigoTecnico),
                    st = ObtenerEstadoLegible(s.Estado),
                    cat = ObtenerCategoria(s.Estado),
                    viat = CalcularViaticos(s.CodigoSolicitud)
                }).ToList();

                return Json(new { success = true, data = resultado }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private string ObtenerTipoSolicitud(int? tipoSolicitud)
        {
            if (!tipoSolicitud.HasValue) return "EMISIÓN";
            
            switch (tipoSolicitud.Value)
            {
                case 1: return "EMISIÓN";
                case 2: return "RENOVACIÓN";
                case 3: return "MODIFICACIÓN";
                default: return "EMISIÓN";
            }
        }

        private string ObtenerNombreInspector(int? codigoTecnico)
        {
            if (!codigoTecnico.HasValue || codigoTecnico.Value == 0)
                return "Sin Asignar";

            try
            {
                var tecnico = UsuarioDAO.ObtenerPorId(codigoTecnico.Value);
                if (tecnico != null && !string.IsNullOrEmpty(tecnico.NombreCompleto))
                    return tecnico.NombreCompleto + " " + (tecnico.ApellidoUsuario ?? "");
                return "Sin Asignar";
            }
            catch
            {
                return "Sin Asignar";
            }
        }

        private decimal CalcularViaticos(int codigoSolicitud)
        {
            try
            {
                // Obtener inspecciones asociadas a la solicitud
                var inspeccionDAO = new InspeccionDAO();
                var inspecciones = inspeccionDAO.ListarPorSolicitud(codigoSolicitud);
                if (inspecciones == null || inspecciones.Count == 0)
                    return 0m;

                decimal total = 0m;
                foreach (var inspeccion in inspecciones)
                {
                    if (inspeccion.CodigoInspeccion > 0)
                    {
                        var viaticos = ViaticoDAO.ObtenerPorInspeccion(inspeccion.CodigoInspeccion);
                        total += viaticos?.Sum(v => v.Monto ?? 0) ?? 0m;
                    }
                }
                return total;
            }
            catch
            {
                return 0m;
            }
        }

        private string ObtenerEstadoLegible(string estado)
        {
            if (string.IsNullOrEmpty(estado)) return "Pendiente";
            
            switch (estado.ToUpper())
            {
                case "PENDIENTE": return "Pendiente";
                case "EN_REVISION": return "En Proceso";
                case "APROBADO": return "Aprobado";
                case "RECHAZADO": return "Observado";
                case "FINALIZADO": return "Finalizado";
                case "ENVIADO_A_INSPECTOR": return "En Trámite";
                case "ENVIADO_A_JEFATURA": return "En Jefatura";
                default: return estado;
            }
        }

        private string ObtenerCategoria(string estado)
        {
            if (string.IsNullOrEmpty(estado)) return "tramite";
            
            switch (estado.ToUpper())
            {
                case "APROBADO":
                case "FINALIZADO":
                    return "aprobado";
                case "RECHAZADO":
                case "OBSERVADO":
                    return "observado";
                default:
                    return "tramite";
            }
        }

        // =========================================================
        // GET: Carga el formulario parcial con datos de BD
        // =========================================================
        [HttpGet]
        public ActionResult FormularioEmisionAOCR(int? oid)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Iniciando con oid: {oid}");
                
                var vm = new SolicitudAOCRViewModel();

                // A veces el sistema guarda el id en IdUsuario en vez de CodigoUsuario.
                int usuarioId = 0;
                if (Session["CodigoUsuario"] != null)
                {
                    int.TryParse(Session["CodigoUsuario"].ToString(), out usuarioId);
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usuario desde CodigoUsuario: {usuarioId}");
                }
                else if (Session["IdUsuario"] != null)
                {
                    int.TryParse(Session["IdUsuario"].ToString(), out usuarioId);
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usuario desde IdUsuario: {usuarioId}");
                }

                if (usuarioId <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioEmisionAOCR] Usuario ID es 0 o inválido");
                    return Content("<div class='alert alert-danger m-3'><i class='fas fa-exclamation-circle'></i> Error: Sesión expirada. Por favor, inicie sesión nuevamente.</div>");
                }

                // 1) Cargar usuario logueado
                System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Intentando obtener usuario: {usuarioId}");
                
                try
                {
                    vm.Usuario = UsuarioDAO.ObtenerPorId(usuarioId);
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usuario obtenido: {(vm.Usuario != null ? vm.Usuario.NombreCompleto : "NULL")}");
                }
                catch (Exception userEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Error obteniendo usuario: {userEx.Message}");
                    vm.Usuario = null;
                }
                
                if (vm.Usuario == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usuario no encontrado para ID: {usuarioId}");
                    
                    // Crear un usuario temporal para no bloquear el formulario
                    vm.Usuario = new Usuario
                    {
                        CodigoUsuario = usuarioId.ToString(),
                        NombreCompleto = "Usuario Temporal",
                        Email = Session["Correo"]?.ToString() ?? "temp@ejemplo.com",
                        NombreUsuario = "temp_user"
                    };
                    
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usando usuario temporal");
                }

                System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usuario final: {vm.Usuario.NombreCompleto}");

                // 2) Si es edición
                if (oid.HasValue && oid.Value > 0)
                {
                    vm.Solicitud = _solicitudBL.ObtenerDetalle(oid.Value);
                    if (vm.Solicitud == null)
                        return Content("<div class='alert alert-danger m-3'><i class='fas fa-search'></i> Error: Solicitud no encontrada.</div>");

                    // Seguridad: si no es admin, solo su solicitud
                    if (!EsAdmin() && vm.Solicitud.CodigoUsuario != usuarioId)
                        return Content("<div class='alert alert-danger m-3'><i class='fas fa-lock'></i> Error: No tiene permisos para acceder a esta solicitud.</div>");

                    // Aeronaves (aocr_tbaeronave_solicitud)
                    vm.Aeronaves = _aeronaveSolDAO.ObtenerPorSolicitud(oid.Value) ?? new List<AeronaveSolicitud>();

                    // Documentos
                    vm.DocumentosExistentes = _documentoDAO.ObtenerPorSolicitud(oid.Value) ?? new List<Documento>();

                    // Pago/comprobante (aocr_tbpago)
                    var pago = _pagoDAO.ObtenerUltimoPorSolicitud(oid.Value);
                    if (pago != null)
                    {
                        vm.Banco = pago.MetodoPago;
                        vm.NumeroComprobante = pago.NumeroFactura;
                    }
                }
                else
                {
                    // NUEVO: precargar desde usuario
                    vm.Solicitud = new SolicitudAOCR
                    {
                        CodigoUsuario = usuarioId,
                        FechaSolicitud = DateTime.Now,
                        Estado = EstadoSolicitud.Pendiente,
                        Email = vm.Usuario != null ? vm.Usuario.Email : "",
                        RepresentanteLegal = vm.Usuario != null ? vm.Usuario.NombreCompleto : "",

                        // ✅ tu Usuario NO tiene NumeroRuc, así que usamos CodigoUsuario como fallback
                        // Si el RUC del usuario está en otra tabla/columna, luego lo mapeamos bien.
                        Ruc = vm.Usuario != null ? vm.Usuario.CodigoUsuario.ToString() : ""
                    };

                    vm.Aeronaves = new List<AeronaveSolicitud>();
                    vm.DocumentosExistentes = new List<Documento>();
                }

                return PartialView("_FormularioEmisionAOCR", vm);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Excepción: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] StackTrace: {ex.StackTrace}");
                
                // En lugar de devolver Content HTML que causa errores de parsing,
                // devolver un contenido HTML válido que no rompa el JavaScript
                return Content($@"
                    <div class='alert alert-danger m-3'>
                        <i class='fas fa-exclamation-triangle'></i> 
                        <strong>Error al cargar formulario:</strong><br/>
                        {HttpUtility.HtmlEncode(ex.Message)}
                        <br/><small class='text-muted'>Revisar logs del servidor para más detalles.</small>
                    </div>
                    <script>
                        console.error('Error en FormularioEmisionAOCR:', {HttpUtility.JavaScriptStringEncode(ex.Message)});
                    </script>");
            }
        }

        // =========================================================
        // POST: Guarda todo el formulario (Solicitud + Aeronaves + Docs + Pago)
        // =========================================================
        [HttpPost]
        public ActionResult TestJson()
        {
            try
            {
                return Json(new { success = true, mensaje = "Endpoint JSON funcionando correctamente", timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error en test: " + ex.Message });
            }
        }

        [HttpPost]
        public ActionResult TestSession()
        {
            try
            {
                var sessionInfo = new {
                    codigoUsuario = Session["CodigoUsuario"],
                    idUsuario = Session["IdUsuario"], 
                    correo = Session["Correo"],
                    sessionId = Session.SessionID,
                    sessionTimeout = Session.Timeout
                };
                
                return Json(new { 
                    success = true, 
                    mensaje = "Sesión verificada", 
                    data = sessionInfo 
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error verificando sesión: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult TestFormularioCompleto(SolicitudAOCRViewModel vm)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TestFormularioCompleto] Recibido ViewModel");
                
                if (vm == null)
                {
                    return Json(new { success = false, mensaje = "ViewModel es null" }, JsonRequestBehavior.AllowGet);
                }
                
                if (vm.Solicitud == null)
                {
                    return Json(new { success = false, mensaje = "vm.Solicitud es null" }, JsonRequestBehavior.AllowGet);
                }
                
                var info = new {
                    solicitudOk = vm.Solicitud != null,
                    nombreOperador = vm.Solicitud?.NombreOperador ?? "NULL",
                    aeronaves = vm.Aeronaves?.Count ?? 0,
                    banco = vm.Banco ?? "NULL",
                    numeroComprobante = vm.NumeroComprobante ?? "NULL"
                };
                
                return Json(new { 
                    success = true, 
                    mensaje = "Test ViewModel exitoso", 
                    data = info 
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TestFormularioCompleto] Excepción: {ex.Message}");
                return Json(new { success = false, mensaje = "Error en test: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        // ValidateAntiForgeryToken no funciona con JSON, usar ValidateJsonAntiForgeryToken si está disponible
        // o implementar validación manual del token en el header
        public ActionResult FormularioCompleto(SolicitudAOCRViewModel vm)
        {
            try
            {
                // Log de entrada para debugging
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Iniciando con vm: {vm}");

                if (Session["CodigoUsuario"] == null)
                {
                    if (Session["IdUsuario"] == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Sesión expirada");
                        return Json(new { success = false, mensaje = "Sesión expirada." }, JsonRequestBehavior.AllowGet);
                    }
                    Session["CodigoUsuario"] = Session["IdUsuario"];
                }

                int usuarioId = Convert.ToInt32(Session["CodigoUsuario"]);
                string usuarioCorreo = Session["Correo"]?.ToString() ?? "sistema";

                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Usuario: {usuarioId}");

                if (vm == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioCompleto] ViewModel es null");
                    return Json(new { success = false, mensaje = "ViewModel es null." }, JsonRequestBehavior.AllowGet);
                }

                if (vm.Solicitud == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioCompleto] vm.Solicitud es null");
                    return Json(new { success = false, mensaje = "Datos de solicitud incompletos." }, JsonRequestBehavior.AllowGet);
                }

                if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreOperador))
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] NombreOperador vacío: '{vm.Solicitud.NombreOperador}'");
                    return Json(new { success = false, mensaje = "Nombre del operador es obligatorio." }, JsonRequestBehavior.AllowGet);
                }

                // Dueño si es nuevo / seguridad si edita
                if (vm.Solicitud.CodigoSolicitud <= 0)
                {
                    vm.Solicitud.CodigoUsuario = usuarioId;
                    vm.Solicitud.TipoSolicitud = 1;
                }
                else
                {
                    var actual = _solicitudDAO.ObtenerPorId(vm.Solicitud.CodigoSolicitud);
                    if (actual == null)
                        return Json(new { success = false, mensaje = "Solicitud no encontrada." }, JsonRequestBehavior.AllowGet);

                    if (!EsAdmin() && actual.CodigoUsuario != usuarioId)
                        return Json(new { success = false, mensaje = "No tiene permisos para modificar esta solicitud." }, JsonRequestBehavior.AllowGet);

                    vm.Solicitud.CodigoUsuario = actual.CodigoUsuario;
                }

                // 1) Guardar Solicitud
                string mensajeOut;
                bool exito;

                if (vm.Solicitud.CodigoSolicitud > 0)
                    exito = _solicitudBL.Actualizar(vm.Solicitud, usuarioId, out mensajeOut, true);
                else
                    exito = _solicitudBL.Crear(vm.Solicitud, usuarioId, out mensajeOut);

                if (!exito)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Error al guardar solicitud: {mensajeOut}");
                    return Json(new { success = false, mensaje = mensajeOut }, JsonRequestBehavior.AllowGet);
                }

                int idFinal = vm.Solicitud.CodigoSolicitud;
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Solicitud guardada con ID: {idFinal}");

                // 2) Aeronaves (reemplazar)
                var aeronaves = (vm.Aeronaves ?? new List<AeronaveSolicitud>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Guardando {aeronaves.Count} aeronaves");
                _aeronaveSolDAO.ReemplazarPorSolicitud(idFinal, aeronaves, usuarioCorreo);

                // 3) Documentos (solo si ArchivosSubidos no es null)
                if (vm.ArchivosSubidos != null && vm.ArchivosSubidos.Count() > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Procesando {vm.ArchivosSubidos.Count()} documentos");
                    ProcesarArchivos(vm.ArchivosSubidos, idFinal);
                }

                // 4) Pago
                if (!string.IsNullOrWhiteSpace(vm.Banco) || !string.IsNullOrWhiteSpace(vm.NumeroComprobante))
                {
                    var pagoEnt = new CapaDatos.Entidades.Pago
                    {
                        CodigoSolicitud = idFinal,
                        MetodoPago = vm.Banco,
                        NumeroComprobante = vm.NumeroComprobante,
                        Estado = "REGISTRADO",
                        FechaPago = DateTime.Now
                    };
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Guardando pago");
                    _pagoDAO.Insertar(pagoEnt, usuarioCorreo);
                }

                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Exito total. Retornando JSON con ID: {idFinal}");
                return Json(new { success = true, mensaje = "Solicitud AOCR registrada correctamente.", id = idFinal }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Excepcion: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] StackTrace: {ex.StackTrace}");
                return Json(new { success = false, mensaje = "Error crítico: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // =========================================================
        // Guardar archivos sin depender de nombres de propiedades exactas
        // =========================================================
        private void ProcesarArchivos(IEnumerable<HttpPostedFileBase> archivos, int solicitudId)
        {
            if (archivos == null) return;

            string path = Server.MapPath("~/Uploads/AOCR/" + solicitudId);
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            foreach (var file in archivos)
            {
                if (file != null && file.ContentLength > 0)
                {
                    string fileName = System.IO.Path.GetFileName(file.FileName);
                    string rutaRelativa = "/Uploads/AOCR/" + solicitudId + "/" + fileName;

                    file.SaveAs(System.IO.Path.Combine(path, fileName));

                    var doc = new Documento();
                    doc.CodigoSolicitud = solicitudId;

                    // Estos nombres sí los usas tú: NombreArchivo y Estado (si existen)
                    SetIfExists(doc, "NombreArchivo", fileName);
                    SetIfExists(doc, "Estado", "PENDIENTE");

                    // En DB existe ruta_guardada y fecha_carga; tu modelo puede llamarse diferente:
                    SetIfExists(doc, "RutaGuardada", rutaRelativa);
                    SetIfExists(doc, "RutaArchivo", rutaRelativa);   // por si tu clase antigua lo tenía así
                    SetIfExists(doc, "FechaCarga", DateTime.Now);
                    SetIfExists(doc, "FechaSubida", DateTime.Now);   // por si tu clase antigua lo tenía así

                    _documentoDAO.Crear(doc);
                }
            }
        }

        private static void SetIfExists(object obj, string prop, object value)
        {
            var pi = obj.GetType().GetProperty(prop);
            if (pi == null || !pi.CanWrite) return;
            pi.SetValue(obj, value, null);
        }

        // =========================================================
        // Resto de acciones (tu código igual)
        // =========================================================
        public ActionResult MisSolicitudes()
        {
            if (Session["CodigoUsuario"] == null)
                return RedirectToAction("Login", "Account");

            return View(_solicitudDAO.ObtenerPorUsuario(Convert.ToInt32(Session["CodigoUsuario"])));
        }

        public ActionResult RevisarSolicitudes()
        {
            // Si no hay en ENVIADO_A_INSPECTOR, mostramos otros estados pendientes
            var pendientes = _solicitudDAO.ObtenerPendientesRevision();
            if (pendientes == null || pendientes.Count == 0)
            {
                pendientes = _solicitudDAO.ObtenerPorEstados(
                    "PENDIENTE",
                    "EN_REVISION",
                    "ENVIADO_A_INSPECTOR",
                    "ENVIADO_A_JEFATURA"
                );
            }
            return View("RevisarSolicitudes", pendientes);
        }

        [HttpPost]
        [Authorize(Roles = "Inspector")]
        [ValidateAntiForgeryToken]
        public ActionResult Aprobar(string id)
        {
            if (!int.TryParse(id, out int idSolicitud))
                return HttpNotFound();

            var solicitud = _solicitudDAO.ObtenerPorCodigo(idSolicitud);
            if (solicitud == null) return HttpNotFound();

            var stats = ChecklistDAO.ObtenerEstadisticasPorSolicitud(solicitud.CodigoSolicitud);

            bool incompleto =
                stats["Total"] == 0 ||
                stats["SinEvaluar"] > 0 ||
                stats["NoCumplen"] > 0;

            if (incompleto)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] =
                    $"No se puede aprobar. Checklist incompleto: Total={stats["Total"]}, SinEvaluar={stats["SinEvaluar"]}, NoCumplen={stats["NoCumplen"]}.";
                return RedirectToAction("RevisarSolicitudes");
            }

            _solicitudDAO.CambiarEstado(idSolicitud, "APROBADO_POR_INSPECTOR", ObtenerUsuarioActualId(), "Aprobado por inspector");

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "Solicitud aprobada correctamente.";
            return RedirectToAction("RevisarSolicitudes");
        }

        [HttpPost]
        [Authorize(Roles = "Inspector")]
        [ValidateAntiForgeryToken]
        public ActionResult Observar(string id, string observacion)
        {
            if (!int.TryParse(id, out int idSolicitud))
                return HttpNotFound();

            var solicitud = _solicitudDAO.ObtenerPorCodigo(idSolicitud);
            if (solicitud == null) return HttpNotFound();

            _solicitudDAO.CambiarEstado(idSolicitud, "OBSERVADO", ObtenerUsuarioActualId(), observacion ?? "");

            TempData["NotificacionTipo"] = "warning";
            TempData["NotificacionMensaje"] = "Solicitud marcada como observada.";

            if (!string.IsNullOrWhiteSpace(solicitud.Email))
            {
                EmailHelper.EnviarEmail(
                    solicitud.Email,
                    "Observación a su Solicitud AOCR",
                    $"Estimado operador,<br><br>Su solicitud <strong>#{solicitud.CodigoSolicitud}</strong> ha sido <b>observada</b>.<br><br><b>Observación:</b> {observacion}<br><br>Por favor revise y actualice su información.<br><br>Saludos."
                );
            }

            return RedirectToAction("RevisarSolicitudes");
        }

        [Authorize(Roles = "JefaturaTecnica")]
        public ActionResult RevisarPorJefatura()
        {
            var pendientes = _solicitudDAO.ObtenerPorEstado("ENVIADO_A_JEFATURA");
            return View(pendientes);
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPorJefatura(int id)
        {
            int userId = ObtenerUsuarioActualId();
            _solicitudDAO.CambiarEstado(id, "VALIDADO_TECNICAMENTE", userId, "Aprobado por Jefatura Técnica");
            TempData["Exito"] = "La solicitud ha sido validada técnicamente.";
            return RedirectToAction("RevisarPorJefatura");
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult ObservarPorJefatura(int id, string observaciones)
        {
            int userId = ObtenerUsuarioActualId();
            _solicitudDAO.CambiarEstado(id, "OBSERVADO_JEFATURA", userId, observaciones ?? "");
            TempData["Exito"] = "Se ha enviado una observación a la solicitud.";
            return RedirectToAction("RevisarPorJefatura");
        }

        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var historialDAO = new HistorialEstadoDAO();
            ViewBag.HistorialEstados = historialDAO.ObtenerPorSolicitud(id);

            return View(solicitud);
        }

        [Authorize(Roles = "CoordinacionLegal")]
        public ActionResult RevisarLegalizacion()
        {
            var lista = _solicitudDAO.ObtenerPorEstado("ENVIADO_A_LEGALIZACION");
            return View(lista);
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string observacionLegal = "")
        {
            try
            {
                int userId = ObtenerUsuarioActualId();

                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null) return HttpNotFound();

                string estadoAnterior = solicitud.Estado;

                _solicitudDAO.CambiarEstado(id, "LEGALIZADO", userId, observacionLegal ?? "Legalizado por Coordinación Legal");

                new HistorialEstadoDAO().RegistrarCambio(
                    id,
                    estadoAnterior,
                    "LEGALIZADO",
                    userId,
                    observacionLegal ?? "Legalizado por Coordinación Legal"
                );

                if (!string.IsNullOrWhiteSpace(solicitud.Email))
                {
                    EmailHelper.EnviarEmail(
                        solicitud.Email,
                        "AOCR Legalizado",
                        $"Estimado operador,<br><br>Su solicitud AOCR #{id} ha sido <strong>legalizada</strong>.<br><br><b>Observaciones:</b> {observacionLegal}<br><br>Gracias por su gestión."
                    );
                }

                TempData["Exito"] = "Solicitud legalizada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al legalizar: " + ex.Message;
            }

            return RedirectToAction("RevisarLegalizacion");
        }

        [Authorize(Roles = "Inspector,Administrador")]
        public ActionResult SolicitarInspeccion(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            _solicitudDAO.CambiarEstado(id, "ENVIADO_A_INSPECTOR", ObtenerUsuarioActualId(), "Inspección solicitada");

            TempData["NotificacionMensaje"] = "Inspección solicitada correctamente.";
            return RedirectToAction("Detalle", new { id });
        }

        // ================================================================
        // NUEVOS MÉTODOS PARA WORKFLOW COMPLETO AOCR (2025-01-05)
        // ================================================================

        /// <summary>
        /// Recepciona formalmente una solicitud (RECEPCIONADO)
        /// </summary>
        [Authorize(Roles = "Recepcion,Administrador")]
        [HttpPost]
        public ActionResult Recepcionar(int id)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                // Validar transición usando constantes
                if (!EstadosSolicitudAOCR.EsTransicionValida(solicitud.Estado, EstadosSolicitudAOCR.RECEPCIONADO))
                    return Json(new { success = false, message = "Transición de estado inválida" });

                solicitud.Estado = EstadosSolicitudAOCR.RECEPCIONADO;
                solicitud.FechaRecepcion = DateTime.Now;
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Registrar en historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = solicitud.Estado,
                    EstadoNuevo = EstadosSolicitudAOCR.RECEPCIONADO,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = "Solicitud recepcionada formalmente"
                });

                return Json(new { success = true, message = "Solicitud recepcionada correctamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Solicita subsanación de documentos (SUBSANACION)
        /// </summary>
        [Authorize(Roles = "TecnicoEvaluador,CoordinadorTecnico,Administrador")]
        [HttpPost]
        public ActionResult SolicitarSubsanacion(int id, string observaciones)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (!EstadosSolicitudAOCR.EsTransicionValida(solicitud.Estado, EstadosSolicitudAOCR.SUBSANACION))
                    return Json(new { success = false, message = "No se puede solicitar subsanación desde el estado actual" });

                var estadoAnterior = solicitud.Estado;
                solicitud.Estado = EstadosSolicitudAOCR.SUBSANACION;
                solicitud.FechaSolicitudSubsanacion = DateTime.Now;
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Insertar registro de subsanación
                var subsanacionDAO = new SubsanacionDAO();
                var subsanacion = new Subsanacion
                {
                    CodigoSolicitud = id,
                    FechaSolicitud = DateTime.Now,
                    Observaciones = observaciones,
                    CodigoUsuarioSolicitante = ObtenerUsuarioActualId(),
                    Estado = "PENDIENTE"
                };
                subsanacionDAO.Insertar(subsanacion);

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = EstadosSolicitudAOCR.SUBSANACION,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = "Subsanación solicitada: " + observaciones
                });

                return Json(new { success = true, message = "Subsanación solicitada. Se notificará al operador." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Marca como subsanado cuando el operador completa documentos (SUBSANADO)
        /// </summary>
        [Authorize(Roles = "Operador,Administrador")]
        [HttpPost]
        public ActionResult CompletarSubsanacion(int id, string respuesta)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (solicitud.Estado != EstadosSolicitudAOCR.SUBSANACION)
                    return Json(new { success = false, message = "La solicitud no está en estado de subsanación" });

                solicitud.Estado = EstadosSolicitudAOCR.SUBSANADO;
                solicitud.FechaSubsanacion = DateTime.Now;
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Actualizar registro de subsanación
                var subsanacionDAO = new SubsanacionDAO();
                var subsanacion = subsanacionDAO.ObtenerPendientePorSolicitud(id);
                if (subsanacion != null)
                {
                    subsanacion.FechaRespuesta = DateTime.Now;
                    subsanacion.Respuesta = respuesta;
                    subsanacion.CodigoUsuarioRespuesta = ObtenerUsuarioActualId();
                    subsanacion.Estado = "COMPLETADA";
                    subsanacionDAO.Actualizar(subsanacion);
                }

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = EstadosSolicitudAOCR.SUBSANACION,
                    EstadoNuevo = EstadosSolicitudAOCR.SUBSANADO,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = "Subsanación completada: " + respuesta
                });

                return Json(new { success = true, message = "Subsanación completada. La solicitud volverá a revisión." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Aprobación por Coordinador (EN_APROBACION_COORDINADOR → EN_APROBACION_DIRECTOR)
        /// </summary>
        [Authorize(Roles = "CoordinadorTecnico,CoordinadorLegal,CoordinadorFinanciero,Administrador")]
        [HttpPost]
        public ActionResult AprobarCoordinador(int id, string observaciones = "")
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (!EstadosSolicitudAOCR.EsTransicionValida(solicitud.Estado, EstadosSolicitudAOCR.EN_APROBACION_DIRECTOR))
                    return Json(new { success = false, message = "La solicitud no puede ser aprobada en su estado actual" });

                var estadoAnterior = solicitud.Estado;
                solicitud.Estado = EstadosSolicitudAOCR.EN_APROBACION_DIRECTOR;
                solicitud.FechaAprobacionCoordinador = DateTime.Now;
                solicitud.UsuarioAprobacionCoordinadorId = ObtenerUsuarioActualId();
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = EstadosSolicitudAOCR.EN_APROBACION_DIRECTOR,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = string.IsNullOrEmpty(observaciones) ? "Aprobado por Coordinador" : observaciones
                });

                return Json(new { success = true, message = "Solicitud enviada a aprobación del Director" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Aprobación final por Director (EN_APROBACION_DIRECTOR → APROBADO)
        /// </summary>
        [Authorize(Roles = "DirectorFinanciero,Administrador")]
        [HttpPost]
        public ActionResult AprobarDirector(int id, string observaciones = "")
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (solicitud.Estado != EstadosSolicitudAOCR.EN_APROBACION_DIRECTOR)
                    return Json(new { success = false, message = "La solicitud no está en estado de aprobación por Director" });

                solicitud.Estado = EstadosSolicitudAOCR.APROBADO;
                solicitud.FechaAprobacion = DateTime.Now;
                solicitud.UsuarioAprobacionDirectorId = ObtenerUsuarioActualId();
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = EstadosSolicitudAOCR.EN_APROBACION_DIRECTOR,
                    EstadoNuevo = EstadosSolicitudAOCR.APROBADO,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = string.IsNullOrEmpty(observaciones) ? "Aprobado por Director" : observaciones
                });

                return Json(new { success = true, message = "Solicitud aprobada. Proceda a emitir el certificado AOCR." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Emite el certificado AOCR (APROBADO → AOCR_EMITIDO)
        /// </summary>
        [Authorize(Roles = "Administrador")]
        [HttpPost]
        public ActionResult EmitirAOCR(int id, string numeroAOCR, string rutaPDF)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (solicitud.Estado != EstadosSolicitudAOCR.APROBADO)
                    return Json(new { success = false, message = "La solicitud no está aprobada para emisión" });

                solicitud.Estado = EstadosSolicitudAOCR.AOCR_EMITIDO;
                solicitud.FechaEmisionAOCR = DateTime.Now;
                solicitud.NumeroAOCR = numeroAOCR;
                solicitud.RutaArchivoPDFAOCR = rutaPDF;
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = EstadosSolicitudAOCR.APROBADO,
                    EstadoNuevo = EstadosSolicitudAOCR.AOCR_EMITIDO,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = $"Certificado AOCR emitido: {numeroAOCR}"
                });

                return Json(new { success = true, message = $"Certificado AOCR {numeroAOCR} emitido exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Registra la entrega física del certificado (AOCR_EMITIDO → AOCR_ENTREGADO)
        /// </summary>
        [Authorize(Roles = "Recepcion,Administrador")]
        [HttpPost]
        public ActionResult EntregarAOCR(int id, string observaciones = "")
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (solicitud.Estado != EstadosSolicitudAOCR.AOCR_EMITIDO)
                    return Json(new { success = false, message = "El certificado AOCR no ha sido emitido" });

                solicitud.Estado = EstadosSolicitudAOCR.AOCR_ENTREGADO;
                solicitud.FechaEntregaAOCR = DateTime.Now;
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = EstadosSolicitudAOCR.AOCR_EMITIDO,
                    EstadoNuevo = EstadosSolicitudAOCR.AOCR_ENTREGADO,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = string.IsNullOrEmpty(observaciones) ? "Certificado AOCR entregado al operador" : observaciones
                });

                return Json(new { success = true, message = "Certificado AOCR entregado. Proceso completado." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Rechaza una solicitud desde cualquier estado (excepto finales)
        /// </summary>
        [Authorize(Roles = "CoordinadorTecnico,CoordinadorLegal,CoordinadorFinanciero,DirectorFinanciero,Administrador")]
        [HttpPost]
        public ActionResult Rechazar(int id, string motivoRechazo)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null)
                    return Json(new { success = false, message = "Solicitud no encontrada" });

                if (EstadosSolicitudAOCR.EsEstadoFinal(solicitud.Estado))
                    return Json(new { success = false, message = "No se puede rechazar una solicitud en estado final" });

                var estadoAnterior = solicitud.Estado;
                solicitud.Estado = EstadosSolicitudAOCR.RECHAZADO;
                solicitud.UpdatedAt = DateTime.Now;
                solicitud.UpdatedBy = User.Identity.Name;

                _solicitudDAO.Actualizar(solicitud);

                // Historial
                _historialDAO.Insertar(new HistorialEstado
                {
                    CodigoSolicitud = id,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = EstadosSolicitudAOCR.RECHAZADO,
                    CodigoUsuario = ObtenerUsuarioActualId(),
                    FechaCambio = DateTime.Now,
                    Observaciones = "RECHAZADO: " + motivoRechazo
                });

                return Json(new { success = true, message = "Solicitud rechazada" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ================================================================
        // FIN MÉTODOS WORKFLOW COMPLETO
        // ================================================================

        private int ObtenerUsuarioActualId()
        {
            if (Session["CodigoUsuario"] != null && int.TryParse(Session["CodigoUsuario"].ToString(), out int idUsuario))
                return idUsuario;

            throw new Exception("No se pudo obtener el ID del usuario actual.");
        }

        private bool EsAdmin()
        {
            var rol = (Session["Rol"] ?? "").ToString();
            return rol.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                   rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase);
        }
    }
}

