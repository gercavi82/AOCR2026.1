using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;
using CapaDatos;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaModelo;
using CapaDatos.Constants;
using CapaPresentacion.Models;
using CapaPresentacion.Helpers;
using CapaNegocio;
using CapaNegocio.Integraciones.As400Sync;
using CapaNegocio.Helpers;
using CapaUtilidades;
using CapaDatos.Services;
using Newtonsoft.Json;
using Npgsql;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class SolicitudAOCRController : Controller
    {
        private readonly SolicitudBL _solicitudBL = new SolicitudBL();
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBL = new SolicitudEstadoTransitionBL();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();
        private readonly DocumentoDAO _documentoDAO = new DocumentoDAO();

        private readonly AeronaveSolicitudDAO _aeronaveSolDAO = new AeronaveSolicitudDAO();
        private readonly PagoDAO _pagoDAO = new PagoDAO();

        private static readonly HashSet<string> ExtensionesPermitidasDocumentos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png"
        };

        private const int TamanoMaximoDocumentoMb = 10;

        public ActionResult Index() => View();

        // Obtener solicitudes del usuario actual en formato JSON
        [HttpGet]
        public JsonResult ObtenerMisSolicitudes()
        {
            try
            {
                int codigoUsuario;
                if (!TryObtenerUsuarioActualId(out codigoUsuario))
                    return Json(new { success = true, data = new List<object>(), message = "Sesion expirada" }, JsonRequestBehavior.AllowGet);

                var solicitudes = _solicitudDAO.ObtenerPorUsuario(codigoUsuario);
                var companiaActiva = ObtenerCompaniaActivaCodigo();
                if (!string.IsNullOrWhiteSpace(companiaActiva))
                {
                    solicitudes = FiltrarSolicitudesPorCompaniaActiva(solicitudes, companiaActiva);
                }

                var resultado = solicitudes.Select(s => new
                {
                    id = s.CodigoSolicitud,
                    fecha = (s.FechaSolicitud ?? s.CreatedAt ?? DateTime.Now).ToString("dd/MM/yyyy"),
                    tipo = ObtenerTipoSolicitud(s.TipoSolicitud),
                    comp = s.NombreOperador ?? s.RazonSocial ?? "Sin Compañía",
                    insp = ObtenerNombreInspector(s),
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

        [HttpGet]
        public JsonResult ObtenerCompaniasDisponibles(int take = 5000)
        {
            try
            {
                if (take <= 0) take = 200;
                if (take > 10000) take = 10000;

                var data = CargarCatalogoCompanias(take);
                return Json(new { success = true, data }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "No se pudo cargar el catálogo de compañías: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult GuardarFlota(GuardarFlotaRequest request)
        {
            try
            {
                if (request == null || request.CodigoSolicitud <= 0)
                {
                    return Json(new { success = false, message = "Solicitud inválida para guardar flota." });
                }

                var usuarioId = ObtenerUsuarioActualId();
                if (usuarioId <= 0)
                {
                    return Json(new { success = false, message = "Sesión expirada." });
                }

                var solicitud = _solicitudDAO.ObtenerPorId(request.CodigoSolicitud);
                if (solicitud == null)
                {
                    return Json(new { success = false, message = "La solicitud no existe." });
                }

                if (!EsAdmin() && solicitud.CodigoUsuario != usuarioId)
                {
                    return Json(new { success = false, message = "No tiene permisos para guardar la flota de esta solicitud." });
                }

                var companiaActiva = ObtenerCompaniaActivaCodigo();
                if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(solicitud, companiaActiva))
                {
                    return Json(new { success = false, message = "La solicitud no corresponde a la compañía activa seleccionada." });
                }

                var aeronaves = (request.Aeronaves ?? new List<AeronaveSolicitud>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                    .ToList();

                if (!aeronaves.Any())
                {
                    return Json(new { success = false, message = "Debe ingresar al menos una aeronave válida." });
                }

                var usuarioCorreo = Session["Correo"]?.ToString() ?? "sistema";
                _aeronaveSolDAO.ReemplazarPorSolicitud(request.CodigoSolicitud, aeronaves, usuarioCorreo);

                return Json(new { success = true, message = "Flota guardada correctamente.", total = aeronaves.Count });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al guardar flota: " + ex.Message });
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

        private string ObtenerNombreInspector(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "Sin Asignar";
            }

            var nombreAs400 = (solicitud.TecnicoResponsableNombre ?? string.Empty).Trim();
            var cedulaAs400 = (solicitud.TecnicoResponsableCedula ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nombreAs400))
            {
                return string.IsNullOrWhiteSpace(cedulaAs400)
                    ? nombreAs400
                    : nombreAs400 + " - " + cedulaAs400;
            }

            if (!solicitud.CodigoTecnico.HasValue || solicitud.CodigoTecnico.Value == 0)
            {
                return "Sin Asignar";
            }

            try
            {
                var tecnico = UsuarioDAO.ObtenerPorId(solicitud.CodigoTecnico.Value);
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

            var norm = EstadoSolicitud.Normalizar(estado);

            if (norm == EstadoSolicitud.Pendiente || norm == EstadoSolicitud.SolicitudCreada)
                return "Pendiente";
            if (norm == EstadoSolicitud.DocumentacionPendiente)
                return "Documentación Pendiente";
            if (norm == EstadoSolicitud.Observada)
                return "Observada";
            if (norm == EstadoSolicitud.Subsanada)
                return "Subsanada";
            if (norm == EstadoSolicitud.AceptacionDocumental || norm == EstadoSolicitud.DocumentacionCompleta)
                return "Documentación Aceptada";
            if (norm == EstadoSolicitud.EnInspeccion || norm == EstadoSolicitud.InspeccionProgramada)
                return "En Inspección";
            if (norm == EstadoSolicitud.InspeccionRealizada)
                return "Inspección Realizada";
            if (norm == EstadoSolicitud.AOCR_EnElaboracion)
                return "AOCR en Elaboración";
            if (norm == EstadoSolicitud.AOCR_EnRevision)
                return "AOCR en Revisión";
            if (norm == EstadoSolicitud.AOCR_Validado)
                return "Validado por Jefatura";
            if (norm == EstadoSolicitud.AOCR_Legalizado)
                return "Legalizado";
            if (norm == EstadoSolicitud.AOCR_EmitidoRecibido || norm == EstadoSolicitud.CertificadoEmitido)
                return "AOCR Emitido";
            if (norm == EstadoSolicitud.Rechazada)
                return "Rechazada";
            if (norm == EstadoSolicitud.Anulada)
                return "Anulada";
            if (norm == EstadoSolicitud.EnRevision)
                return "En Revisión";

            return estado;
        }

        private string ObtenerCategoria(string estado)
        {
            if (string.IsNullOrEmpty(estado)) return "tramite";

            var norm = EstadoSolicitud.Normalizar(estado);

            // Observadas / Rechazadas
            if (norm == EstadoSolicitud.Observada || norm == EstadoSolicitud.Rechazada)
                return "observado";

            // Finalizadas / Aprobadas
            if (norm == EstadoSolicitud.AOCR_EmitidoRecibido ||
                norm == EstadoSolicitud.CertificadoEmitido ||
                norm == EstadoSolicitud.Aprobada ||
                norm == EstadoSolicitud.Anulada)
                return "aprobado";

            // Todo lo demás es trámite en curso
            return "tramite";
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

                int usuarioId;
                if (!TryObtenerUsuarioActualId(out usuarioId))
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioEmisionAOCR] Usuario ID es 0 o inválido");
                    return Content("<div class='alert alert-danger m-3'><i class='fas fa-exclamation-circle'></i> Error: Sesión expirada. Por favor, inicie sesión nuevamente.</div>");
                }

                var companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                var companiaActivaNombre = ObtenerCompaniaActivaNombre();

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
                    var codigoSesion = (Session["CodigoUsuario"] ?? string.Empty).ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(codigoSesion))
                    {
                        try
                        {
                            var usuarioPorCodigo = UsuarioDAO.ObtenerPorNombreUsuario(codigoSesion);
                            if (usuarioPorCodigo != null && usuarioPorCodigo.Id > 0)
                            {
                                vm.Usuario = usuarioPorCodigo;
                                usuarioId = usuarioPorCodigo.Id;
                                Session["IdUsuario"] = usuarioPorCodigo.Id;
                            }
                        }
                        catch (Exception exCodigo)
                        {
                            System.Diagnostics.Debug.WriteLine("[FormularioEmisionAOCR] Error resolviendo usuario por CodigoUsuario: " + exCodigo.Message);
                        }
                    }
                }
                
                if (vm.Usuario == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioEmisionAOCR] Usuario no encontrado para ID: {usuarioId}");
                    
                    // Crear un usuario temporal para no bloquear el formulario
                    vm.Usuario = new Usuario
                    {
                        CodigoUsuario = (Session["CodigoUsuario"] ?? usuarioId.ToString()).ToString(),
                        NombreCompleto = (Session["NombreUsuario"] ?? "Usuario Temporal").ToString(),
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

                    if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(vm.Solicitud, companiaActivaCodigo))
                        return Content("<div class='alert alert-danger m-3'><i class='fas fa-lock'></i> Error: La solicitud no corresponde a la compañía activa.</div>");

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

                    vm.Solicitud.CorreoRepresentanteTecnico = !string.IsNullOrWhiteSpace(vm.Solicitud.CorreoRepresentanteTecnico)
                        ? vm.Solicitud.CorreoRepresentanteTecnico
                        : (vm.Usuario?.Email ?? string.Empty);

                    vm.Solicitud.NombreComercial = !string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial)
                        ? vm.Solicitud.NombreComercial
                        : (vm.Solicitud.NombreOperador ?? string.Empty);
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
                        RepresentanteLegal = string.Empty,
                        CorreoRepresentanteTecnico = vm.Usuario != null ? vm.Usuario.Email : "",
                        Ruc = string.Empty,
                        CedulaRepresentante = string.Empty,
                        NombreComercial = !string.IsNullOrWhiteSpace(companiaActivaNombre)
                            ? companiaActivaNombre
                            : (!string.IsNullOrWhiteSpace(companiaActivaCodigo)
                                ? companiaActivaCodigo
                                : (vm.Usuario != null ? vm.Usuario.EmpresaCodigo : "")),
                        NombreOperador = !string.IsNullOrWhiteSpace(companiaActivaNombre)
                            ? companiaActivaNombre
                            : (!string.IsNullOrWhiteSpace(companiaActivaCodigo)
                                ? companiaActivaCodigo
                                : (vm.Usuario != null ? vm.Usuario.EmpresaCodigo : "")),
                        CompaniasSeleccionadas = companiaActivaCodigo
                    };

                    vm.Aeronaves = new List<AeronaveSolicitud>();
                    vm.DocumentosExistentes = new List<Documento>();
                }

                var usarDatosUsuarioActual = !oid.HasValue || oid.Value <= 0 ||
                    (vm.Solicitud != null && vm.Solicitud.CodigoUsuario == usuarioId);

                var nombreRepresentanteUsuario = usarDatosUsuarioActual
                    ? ObtenerNombreRepresentanteTecnicoActual(usuarioId, vm.Usuario)
                    : string.Empty;
                var identificacionUsuario = ObtenerIdentificacionUsuarioActual(usuarioId, vm.Usuario);
                var identificacionVista = !string.IsNullOrWhiteSpace(identificacionUsuario)
                    ? identificacionUsuario
                    : (usarDatosUsuarioActual
                        ? NormalizarIdentificacion(vm.Solicitud != null ? (vm.Solicitud.CedulaRepresentante ?? vm.Solicitud.Ruc) : null)
                        : string.Empty);
                var nombreRepresentanteVista = !string.IsNullOrWhiteSpace(nombreRepresentanteUsuario)
                    ? nombreRepresentanteUsuario
                    : FormatearNombreCompleto(vm.Solicitud != null ? vm.Solicitud.RepresentanteLegal : null, null);

                var companiaSeleccionadaCodigo = ResolverCompaniaSeleccionadaUnica(
                    companiaActivaCodigo,
                    vm.Solicitud != null ? vm.Solicitud.CompaniasSeleccionadas : null,
                    vm.Usuario != null ? vm.Usuario.EmpresaCodigo : null);
                var companiaSeleccionadaNombre = ResolverNombreCompaniaSeleccionada(
                    companiaSeleccionadaCodigo,
                    companiaActivaCodigo,
                    companiaActivaNombre,
                    vm.Solicitud != null ? vm.Solicitud.NombreOperador : null);

                vm.NombreRepresentanteTecnico = nombreRepresentanteVista;
                vm.IdentificacionUsuario = identificacionVista;
                vm.CompaniaActivaCodigo = companiaSeleccionadaCodigo;
                vm.CompaniaActivaNombre = companiaSeleccionadaNombre;

                if (!string.IsNullOrWhiteSpace(vm.NombreRepresentanteTecnico))
                {
                    vm.Solicitud.RepresentanteLegal = vm.NombreRepresentanteTecnico;
                }

                if (!string.IsNullOrWhiteSpace(identificacionVista))
                {
                    vm.Solicitud.CedulaRepresentante = identificacionVista;
                    vm.Solicitud.Ruc = identificacionVista;
                }

                vm.Solicitud.CompaniasSeleccionadas = companiaSeleccionadaCodigo;

                vm.CompaniasDisponibles = ConstruirCompaniaActivaView(companiaSeleccionadaCodigo, companiaSeleccionadaNombre);

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
        [ValidateAntiForgeryToken]
        public ActionResult TestJson()
        {
#if !DEBUG
            return HttpNotFound();
#else
            try
            {
                return Json(new { success = true, mensaje = "Endpoint JSON funcionando correctamente", timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error en test: " + ex.Message });
            }
#endif
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TestSession()
        {
#if !DEBUG
            return HttpNotFound();
#else
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
#endif
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TestFormularioCompleto(SolicitudAOCRViewModel vm)
        {
#if !DEBUG
            return HttpNotFound();
#else
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
#endif
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // ValidateAntiForgeryToken no funciona con JSON, usar ValidateJsonAntiForgeryToken si está disponible
        // o implementar validación manual del token en el header
        public ActionResult FormularioCompleto(SolicitudAOCRViewModel vm)
        {
            try
            {
                // Log de entrada para debugging
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Iniciando con vm: {vm}");

                // Si viene vmJson (multipart/form-data), usarlo como fuente principal del payload.
                // Nota: el ViewModel inicializa Solicitud por defecto, por lo que verificar solo null no es suficiente.
                if (Request != null && Request.Form != null)
                {
                    var vmJson = Request.Form["vmJson"];
                    if (!string.IsNullOrWhiteSpace(vmJson))
                    {
                        try
                        {
                            var vmDesdeJson = JsonConvert.DeserializeObject<SolicitudAOCRViewModel>(vmJson);
                            if (vmDesdeJson != null)
                            {
                                vm = vmDesdeJson;
                            }
                        }
                        catch (Exception exJson)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Error parseando vmJson: {exJson.Message}");
                        }
                    }
                }

                int usuarioId;
                if (!TryObtenerUsuarioActualId(out usuarioId))
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Sesión expirada");
                    return JsonConEstado(new { success = false, mensaje = "Sesión expirada." }, 401);
                }

                string usuarioCorreo = Session["Correo"]?.ToString() ?? "sistema";
                var companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                var companiaActivaNombre = ObtenerCompaniaActivaNombre();

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

                Usuario usuarioActual = null;
                try
                {
                    usuarioActual = UsuarioDAO.ObtenerPorId(usuarioId);
                }
                catch (Exception exUsuario)
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Error obteniendo usuario actual: " + exUsuario.Message);
                }

                var nombreRepresentanteUsuario = ObtenerNombreRepresentanteTecnicoActual(usuarioId, usuarioActual);
                var identificacionUsuario = ObtenerIdentificacionUsuarioActual(usuarioId, usuarioActual);

                var companiaSeleccionadaCodigo = ResolverCompaniaSeleccionadaUnica(
                    companiaActivaCodigo,
                    vm.Solicitud.CompaniasSeleccionadas,
                    usuarioActual != null ? usuarioActual.EmpresaCodigo : null);

                if (string.IsNullOrWhiteSpace(companiaSeleccionadaCodigo))
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "No existe una compañía activa seleccionada para este trámite."
                    }, JsonRequestBehavior.AllowGet);
                }

                var companiaSeleccionadaNombre = ResolverNombreCompaniaSeleccionada(
                    companiaSeleccionadaCodigo,
                    companiaActivaCodigo,
                    companiaActivaNombre,
                    vm.Solicitud.NombreOperador);

                // Normalización de campos para mantener compatibilidad con estructura actual.
                vm.Solicitud.CorreoRepresentanteTecnico = string.IsNullOrWhiteSpace(vm.Solicitud.CorreoRepresentanteTecnico)
                    ? vm.Solicitud.Email
                    : vm.Solicitud.CorreoRepresentanteTecnico;
                vm.Solicitud.CompaniasSeleccionadas = companiaSeleccionadaCodigo;
                vm.Solicitud.CodigoOaci = NormalizarCodigoOaci(!string.IsNullOrWhiteSpace(vm.Solicitud.CodigoOaci)
                    ? vm.Solicitud.CodigoOaci
                    : companiaSeleccionadaCodigo);
                vm.Solicitud.RazonSocial = !string.IsNullOrWhiteSpace(vm.Solicitud.RazonSocial)
                    ? vm.Solicitud.RazonSocial.Trim()
                    : (!string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial)
                        ? vm.Solicitud.NombreComercial.Trim()
                        : (!string.IsNullOrWhiteSpace(vm.Solicitud.NombreOperador)
                            ? vm.Solicitud.NombreOperador.Trim()
                            : string.Empty));
                vm.Solicitud.NombreComercial = string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial)
                    ? (!string.IsNullOrWhiteSpace(companiaSeleccionadaNombre) ? companiaSeleccionadaNombre : companiaSeleccionadaCodigo)
                    : vm.Solicitud.NombreComercial;
                vm.Solicitud.ResumenOperacionesEae = string.IsNullOrWhiteSpace(vm.Solicitud.ResumenOperacionesEae)
                    ? vm.Solicitud.DescripcionOperacion
                    : vm.Solicitud.ResumenOperacionesEae;
                System.Diagnostics.Debug.WriteLine(
                    $"[FormularioCompleto] Campos compañía => RazonSocial:'{vm.Solicitud.RazonSocial}', NombreComercial:'{vm.Solicitud.NombreComercial}', NombreOperador:'{vm.Solicitud.NombreOperador}'");

                if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreOperador))
                {
                    vm.Solicitud.NombreOperador = !string.IsNullOrWhiteSpace(companiaSeleccionadaNombre)
                        ? companiaSeleccionadaNombre
                        : companiaSeleccionadaCodigo;
                }

                if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreOperador))
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] NombreOperador vacío: '{vm.Solicitud.NombreOperador}'");
                    return Json(new { success = false, mensaje = "Nombre del operador es obligatorio." }, JsonRequestBehavior.AllowGet);
                }

                if (string.IsNullOrWhiteSpace(vm.Solicitud.RazonSocial))
                    return Json(new { success = false, mensaje = "La razón social de la compañía es obligatoria." }, JsonRequestBehavior.AllowGet);

                if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial))
                    return Json(new { success = false, mensaje = "El nombre comercial de la compañía es obligatorio." }, JsonRequestBehavior.AllowGet);

                if (!string.IsNullOrWhiteSpace(vm.Solicitud.CorreoRepresentanteTecnico) &&
                    !Regex.IsMatch(vm.Solicitud.CorreoRepresentanteTecnico.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return Json(new { success = false, mensaje = "El correo del Representante Técnico no tiene un formato válido." }, JsonRequestBehavior.AllowGet);
                }

                if (string.IsNullOrWhiteSpace(vm.Solicitud.CodigoOaci))
                {
                    return Json(new { success = false, mensaje = "El código OACI es obligatorio." }, JsonRequestBehavior.AllowGet);
                }

                if (!EsTelefonoNumericoValido(vm.Solicitud.Telefono))
                {
                    return Json(new { success = false, mensaje = "El teléfono debe contener solo números y tener entre 6 y 15 dígitos." }, JsonRequestBehavior.AllowGet);
                }

                if (!string.IsNullOrWhiteSpace(vm.Solicitud.ResumenOperacionesEae) && vm.Solicitud.ResumenOperacionesEae.Length > 2000)
                    return Json(new { success = false, mensaje = "El resumen de operaciones EAE no puede superar 2000 caracteres." }, JsonRequestBehavior.AllowGet);

                if (ContieneValorLista(vm.Solicitud.AprobacionesEspeciales, "OTROS") &&
                    string.IsNullOrWhiteSpace(vm.Solicitud.AprobacionesEspecialesOtros))
                {
                    return Json(new { success = false, mensaje = "Debe detallar las aprobaciones especiales en el campo OTROS." }, JsonRequestBehavior.AllowGet);
                }

                if (ContieneValorLista(vm.Solicitud.AeropuertosEcuador, "OTROS") &&
                    string.IsNullOrWhiteSpace(vm.Solicitud.AeropuertosEcuadorOtros))
                {
                    return Json(new { success = false, mensaje = "Debe detallar el aeropuerto cuando selecciona OTROS." }, JsonRequestBehavior.AllowGet);
                }

                // Dueño si es nuevo / seguridad si edita
                SolicitudAOCR actual = null;
                var esNuevaSolicitud = vm.Solicitud.CodigoSolicitud <= 0;
                var solicitudPerteneceUsuarioActual = vm.Solicitud.CodigoSolicitud <= 0;
                if (vm.Solicitud.CodigoSolicitud <= 0)
                {
                    vm.Solicitud.CodigoUsuario = usuarioId;
                    vm.Solicitud.TipoSolicitud = 1;
                }
                else
                {
                    actual = _solicitudDAO.ObtenerPorId(vm.Solicitud.CodigoSolicitud);
                    if (actual == null)
                        return Json(new { success = false, mensaje = "Solicitud no encontrada." }, JsonRequestBehavior.AllowGet);

                    if (!EsAdmin() && actual.CodigoUsuario != usuarioId)
                        return Json(new { success = false, mensaje = "No tiene permisos para modificar esta solicitud." }, JsonRequestBehavior.AllowGet);

                    if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(actual, companiaActivaCodigo))
                        return Json(new { success = false, mensaje = "La solicitud no corresponde a la compañía activa." }, JsonRequestBehavior.AllowGet);

                    vm.Solicitud.CodigoUsuario = actual.CodigoUsuario;
                    solicitudPerteneceUsuarioActual = actual.CodigoUsuario == usuarioId;
                }

                var identificacionFormulario = NormalizarIdentificacion(vm.Solicitud.CedulaRepresentante ?? vm.Solicitud.Ruc);
                var identificacionActual = NormalizarIdentificacion(actual != null ? (actual.CedulaRepresentante ?? actual.Ruc) : null);
                var identificacionFinal = solicitudPerteneceUsuarioActual
                    ? (!string.IsNullOrWhiteSpace(identificacionUsuario) ? identificacionUsuario : identificacionFormulario)
                    : (!string.IsNullOrWhiteSpace(identificacionFormulario) ? identificacionFormulario : identificacionActual);

                if (string.IsNullOrWhiteSpace(identificacionFinal))
                {
                    return Json(new
                    {
                        success = false,
                        mensaje = "No se encontró cédula/RUC para el representante técnico. Verifique su información en el perfil de usuario."
                    }, JsonRequestBehavior.AllowGet);
                }

                vm.Solicitud.CedulaRepresentante = identificacionFinal;
                vm.Solicitud.Ruc = identificacionFinal;

                var nombreRepresentanteFormulario = FormatearNombreCompleto(vm.Solicitud.RepresentanteLegal, null);
                var nombreRepresentanteActual = FormatearNombreCompleto(actual != null ? actual.RepresentanteLegal : null, null);
                var nombreRepresentanteFinal = solicitudPerteneceUsuarioActual
                    ? (!string.IsNullOrWhiteSpace(nombreRepresentanteUsuario) ? nombreRepresentanteUsuario : nombreRepresentanteFormulario)
                    : (!string.IsNullOrWhiteSpace(nombreRepresentanteFormulario) ? nombreRepresentanteFormulario : nombreRepresentanteActual);

                if (string.IsNullOrWhiteSpace(nombreRepresentanteFinal))
                {
                    nombreRepresentanteFinal = FormatearNombreCompleto(usuarioActual != null ? usuarioActual.NombreCompleto : null,
                        usuarioActual != null ? usuarioActual.ApellidoUsuario : null);
                }

                if (!string.IsNullOrWhiteSpace(nombreRepresentanteFinal))
                {
                    vm.Solicitud.RepresentanteLegal = nombreRepresentanteFinal;
                }

                int idFinal;
                try
                {
                    idFinal = GuardarFormularioCompletoAtomico(vm, usuarioId, usuarioCorreo);
                }
                catch (ApplicationException exApp)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Error de negocio: {exApp.Message}");
                    return JsonConEstado(new { success = false, mensaje = exApp.Message }, 400);
                }
                catch (PostgresException exPg)
                {
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Error PostgreSQL {exPg.SqlState}: {exPg.MessageText}");
                    return JsonConEstado(new
                    {
                        success = false,
                        mensaje = ObtenerMensajeErrorBaseDatos(exPg),
                        sqlState = exPg.SqlState
                    }, 500);
                }

                MarcarSubsanadaDespuesDeGuardar(actual, idFinal, usuarioId);

                if (esNuevaSolicitud)
                {
                    try
                    {
                        NotificarSolicitanteSolicitudCreada(vm.Solicitud, idFinal);
                    }
                    catch (Exception exCorreo)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Error enviando correo de solicitud creada: " + exCorreo.Message);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Exito total. Retornando JSON con ID: {idFinal}");
                return Json(new { success = true, mensaje = "Solicitud AOCR registrada correctamente.", id = idFinal }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Excepcion: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] StackTrace: {ex.StackTrace}");
                return JsonConEstado(new { success = false, mensaje = "Error crítico: " + ex.Message }, 500);
            }
        }

        private int GuardarFormularioCompletoAtomico(SolicitudAOCRViewModel vm, int usuarioId, string usuarioCorreo)
        {
            var opciones = new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TransactionManager.MaximumTimeout
            };

            var rutasFisicasCreadas = new List<string>();
            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.Required, opciones, TransactionScopeAsyncFlowOption.Enabled))
                {
                    string mensajeOut;
                    bool exito;

                    if (vm.Solicitud.CodigoSolicitud > 0)
                        exito = _solicitudBL.Actualizar(vm.Solicitud, usuarioId, out mensajeOut, true);
                    else
                        exito = _solicitudBL.Crear(vm.Solicitud, usuarioId, out mensajeOut);

                    if (!exito)
                    {
                        throw new ApplicationException(string.IsNullOrWhiteSpace(mensajeOut)
                            ? "No se pudo guardar la solicitud."
                            : mensajeOut);
                    }

                    int idFinal = vm.Solicitud.CodigoSolicitud;
                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Solicitud guardada con ID: {idFinal}");

                    var aeronaves = (vm.Aeronaves ?? new List<AeronaveSolicitud>())
                        .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Guardando {aeronaves.Count} aeronaves");
                    _aeronaveSolDAO.ReemplazarPorSolicitud(idFinal, aeronaves, usuarioCorreo);

                    if (Request?.Files != null && Request.Files.Count > 0)
                    {
                        ProcesarArchivosRequest(Request.Files, idFinal, vm.DocumentosCarga, usuarioCorreo, rutasFisicasCreadas);
                    }

                    if (vm.ArchivosSubidos != null && vm.ArchivosSubidos.Count() > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FormularioCompleto] Procesando {vm.ArchivosSubidos.Count()} documentos");
                        ProcesarArchivos(vm.ArchivosSubidos, idFinal, rutasFisicasCreadas);
                    }

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

                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Guardando pago");
                        _pagoDAO.Insertar(pagoEnt, usuarioCorreo);
                    }

                    scope.Complete();
                    return idFinal;
                }
            }
            catch
            {
                LimpiarArchivosGuardados(rutasFisicasCreadas);
                throw;
            }
        }

        private void MarcarSubsanadaDespuesDeGuardar(SolicitudAOCR solicitudOriginal, int codigoSolicitud, int usuarioId)
        {
            if (solicitudOriginal == null || codigoSolicitud <= 0)
            {
                return;
            }

            var estadoAnterior = EstadoSolicitud.Normalizar(solicitudOriginal.Estado);
            if (!string.Equals(estadoAnterior, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            const string observacion = "Subsanación documental enviada por el operador.";
            string mensajeCambio;
            var cambioPersistido = CambiarEstadoConReglasAocr(codigoSolicitud, EstadoSolicitud.Subsanada, observacion, out mensajeCambio);
            if (!cambioPersistido)
            {
                return;
            }
        }

        private static string ObtenerMensajeErrorBaseDatos(PostgresException exPg)
        {
            switch (exPg.SqlState)
            {
                case "42703":
                    return "La estructura de base de datos de AOCR no coincide con el codigo desplegado (columna faltante).";
                case "23514":
                    return "Uno o mas datos no cumplen las reglas de validacion de la base de datos (constraint CHECK).";
                case "42P01":
                    return "Falta una tabla requerida para registrar la solicitud. Ejecute la migracion de AOCR.";
                default:
                    return "Se produjo un error de base de datos al guardar la solicitud AOCR.";
            }
        }

        private JsonResult JsonConEstado(object payload, int statusCode)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(payload, JsonRequestBehavior.AllowGet);
        }

        // =========================================================
        // Guardar archivos sin depender de nombres de propiedades exactas
        // =========================================================
        private void ProcesarArchivos(IEnumerable<HttpPostedFileBase> archivos, int solicitudId, IList<string> rutasFisicasGuardadas = null)
        {
            if (archivos == null) return;

            foreach (var file in archivos)
            {
                if (file != null && file.ContentLength > 0)
                {
                    var options = new FileUploadOptions
                    {
                        BasePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/Uploads/AOCR"),
                        Subfolder = solicitudId.ToString(),
                        AllowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                        AllowedContentTypes = new[] { "application/pdf", "image/jpeg", "image/png" },
                        MaxSizeMb = 10,
                        ValidateMagicBytes = true
                    };

                    string error;
                    FileUploadResult result;
                    if (!FileUploadService.TrySave(file, options, out result, out error))
                    {
                        throw new ApplicationException("No se pudo guardar el archivo '" + file.FileName + "': " + error);
                    }

                    string fileName = result.StoredName;
                    string rutaRelativa = "~/App_Data/Uploads/AOCR/" + solicitudId + "/" + fileName;
                    string rutaFisica = Path.Combine(options.BasePath, options.Subfolder, fileName);
                    if (rutasFisicasGuardadas != null)
                    {
                        rutasFisicasGuardadas.Add(rutaFisica);
                    }

                    var doc = new Documento();
                    doc.CodigoSolicitud = solicitudId;

                    // Estos nombres sí los usas tú: NombreArchivo y Estado (si existen)
                    SetIfExists(doc, "NombreArchivo", fileName);
                    SetIfExists(doc, "Estado", "Cargado");

                    // En DB existe ruta_guardada y fecha_carga; tu modelo puede llamarse diferente:
                    SetIfExists(doc, "RutaGuardada", rutaRelativa);
                    SetIfExists(doc, "RutaArchivo", rutaRelativa);   // por si tu clase antigua lo tenía así
                    SetIfExists(doc, "FechaCarga", DateTime.Now);
                    SetIfExists(doc, "FechaSubida", DateTime.Now);   // por si tu clase antigua lo tenía así

                    _documentoDAO.Crear(doc);
                }
            }
        }

        private void ProcesarArchivosRequest(
            HttpFileCollectionBase archivos,
            int solicitudId,
            IList<DocumentoCargaVM> metadatos,
            string usuarioRegistro,
            IList<string> rutasFisicasGuardadas = null)
        {
            if (archivos == null || archivos.Count <= 0)
            {
                return;
            }

            var metadatosLookup = (metadatos ?? new List<DocumentoCargaVM>())
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.InputId))
                .GroupBy(m => m.InputId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < archivos.Count; i++)
            {
                var file = archivos[i];
                if (file == null || file.ContentLength <= 0)
                {
                    continue;
                }

                var inputKey = (archivos.GetKey(i) ?? string.Empty).Trim();
                var extension = Path.GetExtension(file.FileName) ?? string.Empty;
                if (!ExtensionesPermitidasDocumentos.Contains(extension))
                {
                    throw new ApplicationException("Archivo con extensión no permitida: " + file.FileName);
                }

                if (file.ContentLength > TamanoMaximoDocumentoMb * 1024 * 1024)
                {
                    throw new ApplicationException("El archivo '" + file.FileName + "' supera el tamaño máximo permitido (" + TamanoMaximoDocumentoMb + " MB).");
                }

                var meta = metadatosLookup.ContainsKey(inputKey)
                    ? metadatosLookup[inputKey]
                    : null;

                var tipoDocumento = ResolverTipoDocumento(inputKey, meta);
                var concepto = meta != null ? meta.Concepto : null;

                var options = new FileUploadOptions
                {
                    BasePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/Uploads/AOCR"),
                    Subfolder = solicitudId + "/Documentos",
                    AllowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                    AllowedContentTypes = new[] { "application/pdf", "image/jpeg", "image/png" },
                    MaxSizeMb = TamanoMaximoDocumentoMb,
                    ValidateMagicBytes = true
                };

                string error;
                FileUploadResult result;
                if (!FileUploadService.TrySave(file, options, out result, out error))
                {
                    throw new ApplicationException("No se pudo guardar el archivo '" + file.FileName + "': " + error);
                }

                var rutaRelativa = "~/App_Data/Uploads/AOCR/" + solicitudId + "/Documentos/" + result.StoredName;
                var rutaFisica = Path.Combine(options.BasePath, options.Subfolder, result.StoredName);
                if (rutasFisicasGuardadas != null)
                {
                    rutasFisicasGuardadas.Add(rutaFisica);
                }
                var doc = new Documento
                {
                    CodigoSolicitud = solicitudId,
                    TipoDocumento = tipoDocumento,
                    NombreArchivo = result.StoredName,
                    RutaGuardada = rutaRelativa,
                    Extension = extension,
                    TamanoBytes = file.ContentLength,
                    Estado = "Cargado",
                    Validado = false,
                    FechaCarga = DateTime.Now,
                    Observaciones = concepto,
                    Version = 1,
                    UsuarioRegistro = string.IsNullOrWhiteSpace(usuarioRegistro) ? "sistema" : usuarioRegistro
                };

                _documentoDAO.Crear(doc);
            }
        }

        private static void LimpiarArchivosGuardados(IEnumerable<string> rutasFisicasGuardadas)
        {
            if (rutasFisicasGuardadas == null)
            {
                return;
            }

            foreach (var ruta in rutasFisicasGuardadas)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(ruta) && System.IO.File.Exists(ruta))
                    {
                        System.IO.File.Delete(ruta);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[FormularioCompleto] No se pudo limpiar archivo en rollback: " + ruta + " - " + ex.Message);
                }
            }
        }

        private static string ResolverTipoDocumento(string inputKey, DocumentoCargaVM meta)
        {
            if (!string.IsNullOrWhiteSpace(meta != null ? meta.TipoDocumento : null))
            {
                return meta.TipoDocumento.Trim();
            }

            if (string.IsNullOrWhiteSpace(inputKey))
            {
                return "OTRO";
            }

            switch (inputKey.Trim())
            {
                case "archivoAOC":
                    return "COPIA_AOC_VALIDA";
                case "archivoOpSpecs":
                    return "OPSPECS_ESPECIFICACIONES_OPERACIONALES";
                case "archivoManualOperaciones":
                    return "MANUAL_OPERACIONES";
                case "archivoPermisoOperacion":
                    return "PERMISO_OPERACION_CNAC";
                case "archivoCertificadoRuido":
                    return "CERTIFICADO_RUIDO_AERONAVES_EAE";
                case "archivoCertificadoAeronavegabilidad":
                    return "CERTIFICADO_AERONAVEGABILIDAD";
                case "archivoPoderRepresentante":
                    return "COPIA_CERTIFICADA_PODER_REPRESENTANTE_ECUADOR";
                case "archivoFacturaPago":
                    return "COMPROBANTE_PAGO";
                default:
                    return "OTRO";
            }
        }

        private static void SetIfExists(object obj, string prop, object value)
        {
            var pi = obj.GetType().GetProperty(prop);
            if (pi == null || !pi.CanWrite) return;
            pi.SetValue(obj, value, null);
        }

        private string ObtenerCompaniaActivaCodigo()
        {
            return CompaniaActivaSessionHelper.ObtenerCodigo(Session);
        }

        private string ObtenerCompaniaActivaNombre()
        {
            return CompaniaActivaSessionHelper.ObtenerNombre(Session);
        }

        private string ObtenerIdentificacionUsuarioActual(int usuarioId, Usuario usuario)
        {
            var identificacion = string.Empty;

            try
            {
                identificacion = UsuarioDAO.ObtenerIdentificacionPrincipal(usuarioId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error obteniendo identificación principal: " + ex.Message);
            }

            identificacion = NormalizarIdentificacion(identificacion);
            if (!string.IsNullOrWhiteSpace(identificacion))
            {
                return identificacion;
            }

            identificacion = NormalizarIdentificacion(usuario != null ? usuario.Ruc : null);
            if (!string.IsNullOrWhiteSpace(identificacion))
            {
                return identificacion;
            }

            var codigoUsuario = ObtenerCodigoUsuarioSesion(usuario);
            return ObtenerIdentificacionDesdeAs400(codigoUsuario);
        }

        private string ObtenerNombreRepresentanteTecnicoActual(int usuarioId, Usuario usuario)
        {
            var nombreDb = string.Empty;

            try
            {
                nombreDb = UsuarioDAO.ObtenerNombreCompletoPrincipal(usuarioId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error obteniendo nombre completo del usuario: " + ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(nombreDb))
            {
                return FormatearNombreCompleto(nombreDb, null);
            }

            var nombre = FormatearNombreCompleto(
                usuario != null ? usuario.NombreCompleto : null,
                usuario != null ? usuario.ApellidoUsuario : null);
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                return nombre;
            }

            return FormatearNombreCompleto((Session["NombreUsuario"] ?? string.Empty).ToString(), null);
        }

        private string ObtenerCodigoUsuarioSesion(Usuario usuario)
        {
            if (usuario != null && !string.IsNullOrWhiteSpace(usuario.CodigoUsuario))
            {
                return usuario.CodigoUsuario.Trim();
            }

            var codigoSesion = (Session["CodigoUsuario"] ?? string.Empty).ToString().Trim();
            if (!string.IsNullOrWhiteSpace(codigoSesion))
            {
                return codigoSesion;
            }

            return string.Empty;
        }

        private string ObtenerIdentificacionDesdeAs400(string codigoUsuario)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return string.Empty;
            }

            try
            {
                var as400Dao = new UsuarioAS400DAO();
                var cedula = NormalizarIdentificacion(as400Dao.ObtenerCedulaPorCodigoUsuario(codigoUsuario));
                if (!string.IsNullOrWhiteSpace(cedula))
                {
                    return cedula;
                }

                var ruc = NormalizarIdentificacion(as400Dao.ObtenerNumeroRucPorCodigoUsuario(codigoUsuario));
                if (!string.IsNullOrWhiteSpace(ruc))
                {
                    return ruc;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error obteniendo identificación desde AS400: " + ex.Message);
            }

            return string.Empty;
        }

        private static string NormalizarIdentificacion(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            var texto = (valor ?? string.Empty).Trim();
            var soloDigitos = new string(texto.Where(char.IsDigit).ToArray());
            if (soloDigitos.Length == 10 || soloDigitos.Length == 13)
            {
                return soloDigitos;
            }

            // Requisito funcional: en Solicitud AOCR solo se expone cédula o RUC válidos.
            return string.Empty;
        }

        private static string FormatearNombreCompleto(string nombres, string apellidos)
        {
            var nombresNorm = NormalizarEspacios(nombres);
            var apellidosNorm = NormalizarEspacios(apellidos);

            if (string.IsNullOrWhiteSpace(nombresNorm))
            {
                return apellidosNorm;
            }

            if (string.IsNullOrWhiteSpace(apellidosNorm))
            {
                return nombresNorm;
            }

            if (nombresNorm.EndsWith(apellidosNorm, StringComparison.OrdinalIgnoreCase))
            {
                return nombresNorm;
            }

            return NormalizarEspacios(nombresNorm + " " + apellidosNorm);
        }

        private static string NormalizarEspacios(string valor)
        {
            return string.Join(" ",
                (valor ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()));
        }

        private static string ObtenerPrimerCodigoCompania(string listaCompanias)
        {
            return (listaCompanias ?? string.Empty)
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => NormalizarCodigoCompania(x))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
        }

        private static string NormalizarCodigoCompania(string codigo)
        {
            return (codigo ?? string.Empty).Trim().ToUpperInvariant();
        }

        private static string NormalizarCodigoOaci(string codigo)
        {
            var valor = (codigo ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            return Regex.Replace(valor, "[^A-Z0-9]", string.Empty);
        }

        private static bool EsTelefonoNumericoValido(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
            {
                return false;
            }

            var soloDigitos = new string((telefono ?? string.Empty).Where(char.IsDigit).ToArray());
            return soloDigitos.Length >= 6 && soloDigitos.Length <= 15 && soloDigitos.Length == (telefono ?? string.Empty).Trim().Length;
        }

        private void NotificarSolicitanteSolicitudCreada(SolicitudAOCR solicitud, int codigoSolicitud)
        {
            if (solicitud == null || codigoSolicitud <= 0)
            {
                return;
            }

            var destinatario = FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, solicitud.Email);
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                return;
            }

            var numeroSolicitud = FirstNonEmpty(solicitud.NumeroSolicitud, "#" + codigoSolicitud);
            var operador = FirstNonEmpty(solicitud.NombreOperador, solicitud.RazonSocial, "Operador");
            var codigoOaci = FirstNonEmpty(solicitud.CodigoOaci, solicitud.CompaniasSeleccionadas, "No registrado");
            var fechaTexto = (solicitud.FechaSolicitud ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm");

            string enlaceDetalle;
            try
            {
                enlaceDetalle = Url.Action("Detalle", "SolicitudAOCR", new { id = codigoSolicitud }, Request != null && Request.Url != null ? Request.Url.Scheme : "http");
            }
            catch
            {
                enlaceDetalle = string.Empty;
            }

            var asunto = "AOCR - Solicitud registrada " + numeroSolicitud;
            var cuerpo = "<p>Estimado/a solicitante,</p>"
                + "<p>Su solicitud AOCR se registró correctamente en el sistema.</p>"
                + "<ul>"
                + "<li><strong>Número de solicitud:</strong> " + HttpUtility.HtmlEncode(numeroSolicitud) + "</li>"
                + "<li><strong>Operador:</strong> " + HttpUtility.HtmlEncode(operador) + "</li>"
                + "<li><strong>Código OACI:</strong> " + HttpUtility.HtmlEncode(codigoOaci) + "</li>"
                + "<li><strong>Fecha de registro:</strong> " + HttpUtility.HtmlEncode(fechaTexto) + "</li>"
                + "</ul>"
                + (!string.IsNullOrWhiteSpace(enlaceDetalle)
                    ? "<p>Puede revisar el detalle en el siguiente enlace: <a href=\"" + HttpUtility.HtmlAttributeEncode(enlaceDetalle) + "\">Ver solicitud</a>.</p>"
                    : string.Empty)
                + "<p>Atentamente,<br/>Dirección General de Aviación Civil</p>";

            var servicioCorreo = new EnviarCorreo();
            servicioCorreo.enviaMensajeCorreo(destinatario, asunto, cuerpo);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return null;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private string ResolverCompaniaSeleccionadaUnica(string companiaActivaCodigo, string companiasSolicitud, string empresaCodigoUsuario)
        {
            var codigoActivo = NormalizarCodigoCompania(companiaActivaCodigo);
            if (!string.IsNullOrWhiteSpace(codigoActivo))
            {
                return codigoActivo;
            }

            var codigoSolicitud = ObtenerPrimerCodigoCompania(companiasSolicitud);
            if (!string.IsNullOrWhiteSpace(codigoSolicitud))
            {
                return codigoSolicitud;
            }

            return ObtenerPrimerCodigoCompania(empresaCodigoUsuario);
        }

        private string ResolverNombreCompaniaSeleccionada(
            string companiaSeleccionadaCodigo,
            string companiaActivaCodigo,
            string companiaActivaNombre,
            string nombreSolicitudActual)
        {
            var codigo = NormalizarCodigoCompania(companiaSeleccionadaCodigo);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(companiaActivaNombre) &&
                string.Equals(codigo, NormalizarCodigoCompania(companiaActivaCodigo), StringComparison.OrdinalIgnoreCase))
            {
                return companiaActivaNombre.Trim();
            }

            if (!string.IsNullOrWhiteSpace(nombreSolicitudActual))
            {
                return nombreSolicitudActual.Trim();
            }

            try
            {
                var dao = new EmpresaAS400DAO();
                var empresa = dao.ObtenerEmpresaPorCodigo(codigo);
                if (empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre))
                {
                    return empresa.Nombre.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error resolviendo nombre de compañía activa: " + ex.Message);
            }

            return codigo;
        }

        private List<CompaniaCatalogoVM> ConstruirCompaniaActivaView(string companiaCodigo, string companiaNombre)
        {
            var codigo = NormalizarCodigoCompania(companiaCodigo);
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return new List<CompaniaCatalogoVM>();
            }

            return new List<CompaniaCatalogoVM>
            {
                new CompaniaCatalogoVM
                {
                    CodigoOaci = codigo,
                    Nombre = (companiaNombre ?? string.Empty).Trim(),
                    CodigoIata = string.Empty,
                    CodigoNumeroCia = string.Empty
                }
            };
        }

        private List<SolicitudAOCR> FiltrarSolicitudesPorCompaniaActiva(IEnumerable<SolicitudAOCR> solicitudes, string companiaActivaCodigo)
        {
            var lista = (solicitudes ?? Enumerable.Empty<SolicitudAOCR>()).ToList();
            if (string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                return lista;
            }

            return lista.Where(s => SolicitudCoincideConCompaniaActiva(s, companiaActivaCodigo)).ToList();
        }

        private bool SolicitudCoincideConCompaniaActiva(SolicitudAOCR solicitud, string companiaActivaCodigo)
        {
            if (solicitud == null || string.IsNullOrWhiteSpace(companiaActivaCodigo))
            {
                return true;
            }

            // Si no hay marca de compañías en el registro existente, no bloqueamos por compatibilidad legacy.
            if (string.IsNullOrWhiteSpace(solicitud.CompaniasSeleccionadas))
            {
                return true;
            }

            return ContieneValorLista(solicitud.CompaniasSeleccionadas, companiaActivaCodigo);
        }

        private static bool ContieneValorLista(string lista, string valor)
        {
            if (string.IsNullOrWhiteSpace(lista) || string.IsNullOrWhiteSpace(valor))
                return false;

            return lista
                .Split(',')
                .Select(x => (x ?? string.Empty).Trim())
                .Any(x => x.Equals(valor, StringComparison.OrdinalIgnoreCase));
        }

        // =========================================================
        // Resto de acciones (tu código igual)
        // =========================================================
        public ActionResult MisSolicitudes()
        {
            int codigoUsuario;
            if (!TryObtenerUsuarioActualId(out codigoUsuario))
                return RedirectToAction("Login", "Account");

            var solicitudes = _solicitudDAO.ObtenerPorUsuario(codigoUsuario);
            var companiaActiva = ObtenerCompaniaActivaCodigo();
            solicitudes = FiltrarSolicitudesPorCompaniaActiva(solicitudes, companiaActiva);

            return View(solicitudes);
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

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(idSolicitud, EstadoSolicitud.AceptacionDocumental, "Aprobado por inspector", out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("RevisarSolicitudes");
            }

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

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(idSolicitud, EstadoSolicitud.Observada, observacion ?? string.Empty, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("RevisarSolicitudes");
            }

            TempData["NotificacionTipo"] = "warning";
            TempData["NotificacionMensaje"] = "Solicitud marcada como observada.";

            try
            {
                if (!string.IsNullOrWhiteSpace(solicitud.Email))
                {
                    EmailHelper.EnviarEmail(
                        solicitud.Email,
                        "Observación a su Solicitud AOCR",
                        $"Estimado operador,<br><br>Su solicitud <strong>#{solicitud.CodigoSolicitud}</strong> ha sido <b>observada</b>.<br><br><b>Observación:</b> {observacion}<br><br>Por favor revise y actualice su información.<br><br>Saludos."
                    );
                }
            }
            catch
            {
                // Notificación por correo es auxiliar; no bloquear el flujo.
            }

            return RedirectToAction("RevisarSolicitudes");
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult RevisarDocumentoItem(int id, int codigoDocumento, string decision, string observacion)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "La solicitud no existe.";
                return RedirectToAction("Detalle", new { id });
            }

            var documento = _documentoDAO.ObtenerPorId(codigoDocumento);
            if (documento == null || documento.CodigoSolicitud != id)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "El documento no pertenece a la solicitud seleccionada.";
                return RedirectToAction("Detalle", new { id });
            }

            var decisionNorm = (decision ?? string.Empty).Trim().ToUpperInvariant();
            if (decisionNorm != "ACEPTADO" && decisionNorm != "DEVUELTO" && decisionNorm != "OBSERVADO")
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La decisión documental no es válida.";
                return RedirectToAction("Detalle", new { id });
            }

            var observacionNormalizada = (observacion ?? string.Empty).Trim();
            if ((decisionNorm == "DEVUELTO" || decisionNorm == "OBSERVADO") && string.IsNullOrWhiteSpace(observacionNormalizada))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Debe registrar una observación cuando el documento sea devuelto u observado.";
                return RedirectToAction("Detalle", new { id });
            }

            var estadoDocumento = decisionNorm == "ACEPTADO" ? "APROBADO" : "RECHAZADO";
            documento.Estado = estadoDocumento;
            documento.Validado = decisionNorm == "ACEPTADO";
            documento.Observaciones = observacionNormalizada;
            documento.FechaCarga = documento.FechaCarga ?? DateTime.Now;
            documento.UsuarioRegistro = (Session["CodigoUsuario"] ?? "sistema").ToString();

            if (!_documentoDAO.Actualizar(documento))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "No se pudo registrar la revisión del documento.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerUsuarioActualId();
            var usuarioRegistro = (Session["CodigoUsuario"] ?? User.Identity.Name ?? "sistema").ToString();
            var daoRevision = new RevisionDocumentalDAO();
            daoRevision.RegistrarRevision(id, codigoDocumento, decisionNorm, observacionNormalizada, usuarioId, usuarioRegistro);
            daoRevision.RegistrarEventoHistorial(
                id,
                codigoDocumento,
                "REVISION_DOCUMENTAL",
                "Documento " + (documento.TipoDocumento ?? "N/A") + " marcado como " + decisionNorm + ". " + observacionNormalizada,
                usuarioId,
                usuarioRegistro);

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "Revisión registrada para el documento seleccionado. Complete todos los documentos y luego cierre la revisión documental.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult FinalizarRevisionDocumental(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "La solicitud no existe.";
                return RedirectToAction("RevisarSolicitudes");
            }

            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            if (documentosRevision.Count == 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No existen documentos vigentes para cerrar la revisión.";
                return RedirectToAction("Detalle", new { id });
            }

            var daoRevision = new RevisionDocumentalDAO();
            var revisiones = daoRevision.ObtenerUltimasRevisionesPorSolicitud(id);

            var documentosSinDecision = documentosRevision
                .Where(d => !DocumentoTieneDecisionFinal(d, revisiones))
                .Select(d => ObtenerEtiquetaDocumento(d))
                .ToList();

            if (documentosSinDecision.Count > 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] =
                    "No se puede enviar la revisión documental. Faltan decisiones en: " +
                    string.Join(", ", documentosSinDecision) + ".";
                return RedirectToAction("Detalle", new { id });
            }

            var documentosSinObservacion = documentosRevision
                .Where(d => DocumentoRequiereObservacionPendiente(d, revisiones))
                .Select(d => ObtenerEtiquetaDocumento(d))
                .ToList();

            if (documentosSinObservacion.Count > 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] =
                    "No se puede enviar la revisión documental. Debe registrar observación en: " +
                    string.Join(", ", documentosSinObservacion) + ".";
                return RedirectToAction("Detalle", new { id });
            }

            var tieneDocumentosDevueltos = documentosRevision.Any(d =>
            {
                var decisionDoc = ObtenerDecisionRevisionDocumental(d, revisiones);
                return decisionDoc == "DEVUELTO" || decisionDoc == "OBSERVADO";
            });

            var estadoDestino = tieneDocumentosDevueltos
                ? EstadoSolicitud.Observada
                : EstadoSolicitud.AceptacionDocumental;

            var observacionCierre = tieneDocumentosDevueltos
                ? ConstruirResumenRevisionDocumental(documentosRevision, revisiones, true)
                : "Todos los documentos vigentes fueron aceptados por el inspector.";

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, estadoDestino, observacionCierre, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerUsuarioActualId();
            var usuarioRegistro = (Session["CodigoUsuario"] ?? User.Identity.Name ?? "sistema").ToString();
            daoRevision.RegistrarEventoHistorial(
                id,
                null,
                "REVISION_DOCUMENTAL_FINALIZADA",
                observacionCierre,
                usuarioId,
                usuarioRegistro);

            if (tieneDocumentosDevueltos)
            {
                try
                {
                    EnviarCorreoRevisionDocumentalDevuelta(solicitud, documentosRevision, revisiones);
                }
                catch
                {
                    // El correo es auxiliar; no bloquea el cierre de la revisión.
                }
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = tieneDocumentosDevueltos
                ? "La revisión documental fue cerrada y la solicitud se devolvió al operador con observaciones."
                : "La revisión documental fue cerrada y la solicitud avanzó a Aceptación Documental.";
            return RedirectToAction("Detalle", new { id });
        }

        [Authorize(Roles = "JefaturaTecnica")]
        public ActionResult RevisarPorJefatura()
        {
            var pendientes = _solicitudDAO.ObtenerPorEstados("ENVIADO_A_JEFATURA", EstadoSolicitud.AOCR_EnRevision, EstadoSolicitud.AOCR_EnElaboracion);
            return View(pendientes);
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPorJefatura(int id)
        {
            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_Validado, "Aprobado por Jefatura Técnica", out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
                return RedirectToAction("RevisarPorJefatura");
            }

            TempData["Exito"] = "La solicitud ha sido validada técnicamente.";
            return RedirectToAction("RevisarPorJefatura");
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica")]
        [ValidateAntiForgeryToken]
        public ActionResult ObservarPorJefatura(int id, string observaciones)
        {
            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.Observada, observaciones ?? string.Empty, out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
                return RedirectToAction("RevisarPorJefatura");
            }

            TempData["Exito"] = "Se ha enviado una observación a la solicitud.";
            return RedirectToAction("RevisarPorJefatura");
        }

        // =========================================================
        // GET: Subsanar — Vista enfocada de subsanación
        // =========================================================
        public ActionResult Subsanar(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            int usuarioId;
            if (!TryObtenerUsuarioActualId(out usuarioId))
                return RedirectToAction("Login", "Account");

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (!string.Equals(estadoActual, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en estado Observada.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!EsAdmin() && solicitud.CodigoUsuario != usuarioId)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "No tiene permisos para subsanar esta solicitud.";
                return RedirectToAction("Detalle", new { id });
            }

            var documentos = _documentoDAO.ObtenerPorSolicitud(id) ?? new List<Documento>();
            var historialDAO = new HistorialEstadoDAO();
            var historial = historialDAO.ObtenerPorSolicitud(id) ?? new List<HistorialEstado>();

            // Extraer nombre del inspector asignado
            var inspectorNombre = !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre)
                ? solicitud.TecnicoResponsableNombre
                : "Sin asignar";

            // Historial de observaciones (cambios a estado Observada)
            var historialObs = historial
                .Where(h => string.Equals(EstadoSolicitud.Normalizar(h.EstadoNuevo), EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(h.Observaciones))
                .OrderByDescending(h => h.FechaCambio)
                .Select(h => new HistorialObservacionVM
                {
                    Fecha = h.FechaCambio,
                    Observacion = h.Observaciones,
                    Usuario = h.NombreUsuario ?? "Inspector"
                })
                .ToList();

            var vm = new SubsanacionViewModel
            {
                CodigoSolicitud = solicitud.CodigoSolicitud,
                NumeroSolicitud = solicitud.NumeroSolicitud,
                Compania = !string.IsNullOrWhiteSpace(solicitud.NombreComercial)
                    ? solicitud.NombreComercial
                    : solicitud.NombreOperador,
                FechaSolicitud = solicitud.FechaSolicitud,
                Estado = estadoActual,
                InspectorNombre = inspectorNombre,
                ObservacionesInspector = solicitud.Observaciones,
                HistorialObservaciones = historialObs,
                DocumentosObservados = documentos.Select(d => new DocumentoSubsanacionVM
                {
                    CodigoDocumento = d.CodigoDocumento,
                    TipoDocumento = d.TipoDocumento,
                    NombreArchivo = d.NombreArchivo,
                    Estado = d.Estado,
                    Observaciones = d.Observaciones,
                    FechaCarga = d.FechaCarga
                }).ToList()
            };

            return View(vm);
        }

        // =========================================================
        // POST: SubsanarPost — Procesar corrección de documentos
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubsanarPost(int codigoSolicitud, string comentario)
        {
            int usuarioId;
            if (!TryObtenerUsuarioActualId(out usuarioId))
                return RedirectToAction("Login", "Account");

            var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
            if (solicitud == null) return HttpNotFound();

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (!string.Equals(estadoActual, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud ya no se encuentra en estado Observada.";
                return RedirectToAction("Detalle", new { id = codigoSolicitud });
            }

            if (!EsAdmin() && solicitud.CodigoUsuario != usuarioId)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "No tiene permisos para subsanar esta solicitud.";
                return RedirectToAction("Detalle", new { id = codigoSolicitud });
            }

            try
            {
                var archivosSubidos = 0;
                var usuarioRegistro = (Session["CodigoUsuario"] ?? usuarioId.ToString()).ToString();

                // Procesar cada archivo subido
                for (var i = 0; i < Request.Files.Count; i++)
                {
                    var file = Request.Files[i];
                    if (file == null || file.ContentLength <= 0) continue;

                    var key = Request.Files.GetKey(i) ?? string.Empty;
                    // key format: archivos_{codigoDocumento}
                    int docId;
                    var parts = key.Split('_');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out docId)) continue;

                    var extension = Path.GetExtension(file.FileName) ?? string.Empty;
                    if (!ExtensionesPermitidasDocumentos.Contains(extension))
                    {
                        TempData["NotificacionTipo"] = "error";
                        TempData["NotificacionMensaje"] = "Extensión no permitida: " + extension;
                        return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                    }

                    if (file.ContentLength > TamanoMaximoDocumentoMb * 1024 * 1024)
                    {
                        TempData["NotificacionTipo"] = "error";
                        TempData["NotificacionMensaje"] = "El archivo supera el límite de " + TamanoMaximoDocumentoMb + " MB.";
                        return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                    }

                    // Obtener documento original para preservar tipo
                    var docOriginal = _documentoDAO.ObtenerPorId(docId);
                    var tipoDoc = docOriginal != null ? docOriginal.TipoDocumento : "Documento Subsanado";

                    var options = new FileUploadOptions
                    {
                        BasePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/Uploads/AOCR"),
                        Subfolder = codigoSolicitud + "/Documentos",
                        AllowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" },
                        AllowedContentTypes = new[] { "application/pdf", "image/jpeg", "image/png",
                            "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                        MaxSizeMb = TamanoMaximoDocumentoMb,
                        ValidateMagicBytes = true
                    };

                    string error;
                    FileUploadResult result;
                    if (!FileUploadService.TrySave(file, options, out result, out error))
                    {
                        TempData["NotificacionTipo"] = "error";
                        TempData["NotificacionMensaje"] = "Error al guardar archivo: " + error;
                        return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                    }

                    var rutaRelativa = "~/App_Data/Uploads/AOCR/" + codigoSolicitud + "/Documentos/" + result.StoredName;
                    var versionAnterior = docOriginal != null && docOriginal.Version.HasValue ? docOriginal.Version.Value : 1;

                    var nuevoDoc = new Documento
                    {
                        CodigoSolicitud = codigoSolicitud,
                        TipoDocumento = tipoDoc,
                        NombreArchivo = result.StoredName,
                        RutaGuardada = rutaRelativa,
                        Extension = extension,
                        TamanoBytes = file.ContentLength,
                        Estado = "Subsanado",
                        Validado = false,
                        FechaCarga = DateTime.Now,
                        Observaciones = "Subsanación: " + (comentario ?? "").Trim(),
                        Version = versionAnterior + 1,
                        UsuarioRegistro = usuarioRegistro
                    };

                    _documentoDAO.Crear(nuevoDoc);
                    archivosSubidos++;
                }

                if (archivosSubidos == 0)
                {
                    TempData["NotificacionTipo"] = "warning";
                    TempData["NotificacionMensaje"] = "Debe subir al menos un documento corregido.";
                    return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                }

                // Cambiar estado a Subsanada
                var observacionCambio = "Subsanación documental enviada por el operador.";
                if (!string.IsNullOrWhiteSpace(comentario))
                    observacionCambio += " Comentario: " + comentario.Trim();

                string mensajeCambio;
                if (!CambiarEstadoConReglasAocr(codigoSolicitud, EstadoSolicitud.Subsanada, observacionCambio, out mensajeCambio))
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = string.IsNullOrWhiteSpace(mensajeCambio)
                        ? "No fue posible actualizar el estado de la solicitud."
                        : mensajeCambio;
                    return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                }

                // Enviar email al inspector si tiene correo
                try
                {
                    if (!string.IsNullOrWhiteSpace(solicitud.Email))
                    {
                        EmailHelper.EnviarEmail(
                            solicitud.Email,
                            "Subsanación Recibida - Solicitud AOCR #" + codigoSolicitud,
                            "La solicitud AOCR <strong>#" + codigoSolicitud + "</strong> ha sido subsanada por el operador.<br><br>" +
                            "<b>Documentos corregidos:</b> " + archivosSubidos + "<br>" +
                            (!string.IsNullOrWhiteSpace(comentario) ? "<b>Comentario:</b> " + comentario + "<br>" : "") +
                            "<br>Ingrese al sistema para revisar los documentos actualizados."
                        );
                    }
                }
                catch { /* email auxiliar */ }

                TempData["NotificacionTipo"] = "success";
                TempData["NotificacionMensaje"] = "Corrección enviada exitosamente. Se subieron " + archivosSubidos + " documento(s).";
                return RedirectToAction("Detalle", new { id = codigoSolicitud });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SubsanarPost] Error: " + ex.Message);
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "Error al procesar la subsanación: " + ex.Message;
                return RedirectToAction("Subsanar", new { id = codigoSolicitud });
            }
        }

        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var historialDAO = new HistorialEstadoDAO();
            ViewBag.HistorialEstados = historialDAO.ObtenerPorSolicitud(id);
            ViewBag.UsuarioActualId = ObtenerUsuarioActualId();

            var rtDao = new UsuarioInternoRTDAO();
            ViewBag.AsignacionActiva = rtDao.ObtenerAsignacionActiva(id);
            ViewBag.HistorialAsignaciones = rtDao.ObtenerHistorialAsignacion(id);
            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            var revisionesDocumentales = new RevisionDocumentalDAO().ObtenerUltimasRevisionesPorSolicitud(id);
            ViewBag.DocumentosSolicitud = documentosRevision;
            ViewBag.RevisionesDocumentales = revisionesDocumentales;
            ViewBag.PuedeFinalizarRevisionDocumental =
                documentosRevision.Count > 0 &&
                documentosRevision.All(d => DocumentoTieneDecisionFinal(d, revisionesDocumentales)) &&
                !documentosRevision.Any(d => DocumentoRequiereObservacionPendiente(d, revisionesDocumentales));

            return View(solicitud);
        }

        [Authorize(Roles = "CoordinacionLegal,DirectorGeneral,Administrador")]
        public ActionResult RevisarLegalizacion()
        {
            var lista = _solicitudDAO.ObtenerPorEstados("ENVIADO_A_LEGALIZACION", EstadoSolicitud.AOCR_Validado);
            return View(lista);
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string observacionLegal = "")
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null) return HttpNotFound();

                string mensajeCambio;
                if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_Legalizado, observacionLegal ?? "Legalizado por Coordinación Legal", out mensajeCambio))
                {
                    TempData["Error"] = mensajeCambio;
                    return RedirectToAction("RevisarLegalizacion");
                }

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

        [Authorize(Roles = "Inspector,CoordinadorInspecciones,Administrador")]
        public ActionResult MarcarPendienteAsignacionRT(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.PendienteAsignacionRT, "Documentación aceptada — pendiente de asignación de RT/Inspector", out mensajeCambio))
            {
                TempData["NotificacionMensaje"] = mensajeCambio;
                TempData["NotificacionTipo"] = "error";
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionMensaje"] = "Solicitud marcada como pendiente de asignación de RT/Inspector.";
            TempData["NotificacionTipo"] = "success";
            return RedirectToAction("Detalle", new { id });
        }

        [Authorize(Roles = "Inspector,Administrador")]
        public ActionResult SolicitarInspeccion(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.EnInspeccion, "Inspeccion solicitada", out mensajeCambio))
            {
                TempData["NotificacionMensaje"] = mensajeCambio;
                TempData["NotificacionTipo"] = "error";
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionMensaje"] = "Inspección solicitada correctamente.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "CoordinadorInspecciones,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarAocrEnElaboracion(int id, string observacion = "")
        {
            string mensajeInspeccion;
            if (!SolicitudTieneInspeccionSatisfactoria(id, out mensajeInspeccion))
            {
                TempData["Error"] = mensajeInspeccion;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EnElaboracion, observacion ?? "AOCR en elaboración", out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
            }
            else
            {
                TempData["Exito"] = "Solicitud enviada a elaboración de AOCR.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "JefaturaTecnica,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarAocrEnRevision(int id, string observacion = "")
        {
            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EnRevision, observacion ?? "AOCR en revisión", out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
            }
            else
            {
                TempData["Exito"] = "Solicitud enviada a revisión AOCR.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult EmitirAocr(int id, string observacion = "")
        {
            string mensajeInspeccion;
            if (!SolicitudTieneInspeccionSatisfactoria(id, out mensajeInspeccion))
            {
                TempData["Error"] = mensajeInspeccion;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EmitidoRecibido, observacion ?? "AOCR emitido/recibido", out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
            }
            else
            {
                TempData["Exito"] = "AOCR emitido y marcado como recibido.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        private bool SolicitudTieneInspeccionSatisfactoria(int codigoSolicitud, out string mensaje)
        {
            mensaje = string.Empty;
            var inspecciones = new InspeccionDAO().ListarPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();

            if (inspecciones.Count == 0)
            {
                mensaje = "No se puede avanzar porque la solicitud no tiene inspecciones registradas.";
                return false;
            }

            var existeSatisfactoria = inspecciones.Any(EsInspeccionSatisfactoria);
            if (!existeSatisfactoria)
            {
                mensaje = "No se puede avanzar a AOCR final sin una inspección satisfactoria (estado APROBADA/CERRADA o resultado satisfactorio).";
                return false;
            }

            return true;
        }

        private static bool EsInspeccionSatisfactoria(Inspeccion inspeccion)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var estado = (inspeccion.Estado ?? string.Empty).Trim().ToUpperInvariant();
            var resultado = (inspeccion.Resultado ?? string.Empty).Trim().ToUpperInvariant();
            var resultadoEvaluacion = (inspeccion.ResultadoEvaluacion ?? string.Empty).Trim().ToUpperInvariant();

            return estado == "APROBADA"
                   || estado == "RESULTADO_SATISFACTORIO"
                   || estado == "CERRADA"
                   || resultado == "APROBADO"
                   || resultado == "SATISFACTORIO"
                   || resultadoEvaluacion == "RESULTADO_SATISFACTORIO"
                   || resultadoEvaluacion == "SATISFACTORIO";
        }

        private bool CambiarEstadoConReglasAocr(int codigoSolicitud, string nuevoEstado, string observacion, out string mensaje)
        {
            var usuarioId = ObtenerUsuarioActualId();
            return _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                codigoSolicitud,
                nuevoEstado,
                observacion,
                usuarioId,
                UsuarioActualPuedeTransicionarAocr,
                out mensaje);
        }

        private bool UsuarioActualPuedeTransicionarAocr(string estadoDestino)
        {
            var destino = EstadoSolicitud.Normalizar(estadoDestino);

            if (User != null && User.IsInRole("Administrador"))
            {
                return true;
            }

            if (destino == EstadoSolicitud.Observada)
            {
                return User != null && (User.IsInRole("Inspector") || User.IsInRole("JefaturaTecnica") || User.IsInRole("CoordinacionLegal"));
            }

            if (destino == EstadoSolicitud.AceptacionDocumental)
            {
                return User != null && User.IsInRole("Inspector");
            }

            if (destino == EstadoSolicitud.PendienteAsignacionRT)
            {
                return User != null && (User.IsInRole("Inspector") || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.EnInspeccion || destino == EstadoSolicitud.AOCR_EnElaboracion)
            {
                return User != null && (User.IsInRole("Inspector") || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.AOCR_EnRevision || destino == EstadoSolicitud.AOCR_Validado)
            {
                return User != null && User.IsInRole("JefaturaTecnica");
            }

            if (destino == EstadoSolicitud.AOCR_Legalizado || destino == EstadoSolicitud.AOCR_EmitidoRecibido)
            {
                return User != null && (User.IsInRole("CoordinacionLegal") || User.IsInRole("DirectorGeneral"));
            }

            return false;
        }

        private List<Documento> ObtenerDocumentosVigentesParaRevision(int codigoSolicitud)
        {
            var documentos = _documentoDAO.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>();
            return documentos
                .Where(d => d != null && d.CodigoDocumento > 0)
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(d => d.Version ?? 0)
                    .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .ThenByDescending(d => d.CodigoDocumento)
                    .First())
                .OrderBy(d => ObtenerEtiquetaDocumento(d), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ObtenerClaveDocumentoRevision(Documento documento)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            var tipoDocumento = (documento.TipoDocumento ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(tipoDocumento))
            {
                return tipoDocumento.ToUpperInvariant();
            }

            return "__DOC_" + documento.CodigoDocumento;
        }

        private static string ObtenerEtiquetaDocumento(Documento documento)
        {
            if (documento == null)
            {
                return "Documento";
            }

            var etiqueta = string.IsNullOrWhiteSpace(documento.TipoDocumento)
                ? "Documento"
                : documento.TipoDocumento.Trim();

            if (!string.IsNullOrWhiteSpace(documento.NombreArchivo))
            {
                return etiqueta + " (" + documento.NombreArchivo.Trim() + ")";
            }

            return etiqueta;
        }

        private static string ObtenerDecisionRevisionDocumental(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revisionActual;
            if (revisiones != null &&
                revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual) &&
                revisionActual != null &&
                !string.IsNullOrWhiteSpace(revisionActual.Item1))
            {
                return revisionActual.Item1.Trim().ToUpperInvariant();
            }

            var estadoDocumento = (documento.Estado ?? string.Empty).Trim().ToUpperInvariant();
            if (estadoDocumento == "APROBADO")
            {
                return "ACEPTADO";
            }

            if (estadoDocumento == "RECHAZADO")
            {
                return "DEVUELTO";
            }

            return string.Empty;
        }

        private static string ObtenerObservacionRevisionDocumental(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revisionActual;
            if (revisiones != null &&
                revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual) &&
                revisionActual != null &&
                !string.IsNullOrWhiteSpace(revisionActual.Item2))
            {
                return revisionActual.Item2.Trim();
            }

            return (documento.Observaciones ?? string.Empty).Trim();
        }

        private static bool DocumentoTieneDecisionFinal(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
            return decision == "ACEPTADO" || decision == "DEVUELTO" || decision == "OBSERVADO";
        }

        private static bool DocumentoRequiereObservacionPendiente(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
            if (decision != "DEVUELTO" && decision != "OBSERVADO")
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(ObtenerObservacionRevisionDocumental(documento, revisiones));
        }

        private static string ConstruirResumenRevisionDocumental(IEnumerable<Documento> documentos, IDictionary<int, Tuple<string, string>> revisiones, bool soloDevueltos)
        {
            var items = (documentos ?? Enumerable.Empty<Documento>())
                .Select(d => new
                {
                    Documento = ObtenerEtiquetaDocumento(d),
                    Decision = ObtenerDecisionRevisionDocumental(d, revisiones),
                    Observacion = ObtenerObservacionRevisionDocumental(d, revisiones)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Decision))
                .Where(x => !soloDevueltos || x.Decision == "DEVUELTO" || x.Decision == "OBSERVADO")
                .Select(x => x.Documento + ": " + RevisionDocumentalDisplayHelper.GetVisibleStateLabel(x.Decision) + (string.IsNullOrWhiteSpace(x.Observacion) ? string.Empty : " - " + x.Observacion))
                .ToList();

            if (items.Count == 0)
            {
                return soloDevueltos
                    ? "La solicitud fue devuelta para subsanación documental."
                    : "La revisión documental fue cerrada.";
            }

            return string.Join(" | ", items);
        }

        private static void EnviarCorreoRevisionDocumentalDevuelta(
            SolicitudAOCR solicitud,
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (solicitud == null || string.IsNullOrWhiteSpace(solicitud.Email))
            {
                return;
            }

            var itemsDevueltos = (documentos ?? Enumerable.Empty<Documento>())
                .Select(d => new
                {
                    Documento = ObtenerEtiquetaDocumento(d),
                    Decision = ObtenerDecisionRevisionDocumental(d, revisiones),
                    Observacion = ObtenerObservacionRevisionDocumental(d, revisiones)
                })
                .Where(x => x.Decision == "DEVUELTO" || x.Decision == "OBSERVADO")
                .ToList();

            if (itemsDevueltos.Count == 0)
            {
                return;
            }

            var detalleHtml = string.Join(string.Empty, itemsDevueltos.Select(x =>
                "<li><strong>" + HttpUtility.HtmlEncode(x.Documento) + "</strong>: " +
                HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(x.Observacion) ? "Sin observación registrada." : x.Observacion) +
                " <em>(" + HttpUtility.HtmlEncode(RevisionDocumentalDisplayHelper.GetVisibleStateLabel(x.Decision)) + ")</em></li>"));

            var asunto = "Solicitud AOCR devuelta para subsanación";
            var cuerpo = "Estimado operador,<br><br>" +
                         "La solicitud AOCR <strong>#" + solicitud.CodigoSolicitud + "</strong> fue devuelta a su bandeja para que realice las correcciones correspondientes.<br><br>" +
                         "<strong>Documentos observados/devueltos:</strong><ul>" + detalleHtml + "</ul>" +
                         "Ingrese al sistema, actualice la documentación y reenvíe la solicitud para una nueva revisión.<br><br>Saludos.";

            EmailHelper.EnviarEmail(solicitud.Email, asunto, cuerpo);
        }

        private List<CompaniaCatalogoVM> CargarCatalogoCompanias(int take)
        {
            var catalogo = new List<CompaniaCatalogoVM>();

            try
            {
                var mirror = new MirrorReadService();
                var mirrorCompanias = mirror.ListarCompaniasActivas(take);
                if (mirrorCompanias != null && mirrorCompanias.Count > 0)
                {
                    catalogo = mirrorCompanias
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CodigoOaci))
                        .Select(c => new CompaniaCatalogoVM
                        {
                            CodigoOaci = (c.CodigoOaci ?? string.Empty).Trim(),
                            CodigoIata = (c.CodigoIata ?? string.Empty).Trim(),
                            CodigoNumeroCia = (c.CodigoNumeroCia ?? string.Empty).Trim(),
                            Nombre = (c.NombreCompania ?? string.Empty).Trim()
                        })
                        .OrderBy(c => c.Nombre)
                        .ToList();
                }
            }
            catch (Exception exMirror)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error catálogo mirror: " + exMirror.Message);
            }

            if (catalogo.Count == 0)
            {
                try
                {
                    var dao = new EmpresaAS400DAO();
                    catalogo = dao.ObtenerEmpresas()
                        .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CodigoOaci))
                        .Select(c => new CompaniaCatalogoVM
                        {
                            CodigoOaci = (c.CodigoOaci ?? string.Empty).Trim(),
                            CodigoIata = (c.CodigoIata ?? string.Empty).Trim(),
                            CodigoNumeroCia = (c.CodigoNumeroCia ?? string.Empty).Trim(),
                            Nombre = (c.Nombre ?? string.Empty).Trim()
                        })
                        .OrderBy(c => c.Nombre)
                        .ToList();
                }
                catch (Exception exAs400)
                {
                    System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error catálogo AS400: " + exAs400.Message);
                }
            }

            if (catalogo.Count > 0)
            {
                catalogo = catalogo
                    .GroupBy(c => c.CodigoOaci ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();
            }

            return catalogo;
        }

        public class GuardarFlotaRequest
        {
            public int CodigoSolicitud { get; set; }
            public List<AeronaveSolicitud> Aeronaves { get; set; }
        }

        private bool TryObtenerUsuarioActualId(out int idUsuario)
        {
            idUsuario = 0;

            var idSesion = Session["IdUsuario"] ?? Session["UserId"];
            if (idSesion != null && int.TryParse(idSesion.ToString(), out idUsuario) && idUsuario > 0)
            {
                Session["IdUsuario"] = idUsuario;
                Session["UserId"] = idUsuario;
                return true;
            }

            if (Session["CodigoUsuario"] != null)
            {
                var codigoSesion = Session["CodigoUsuario"].ToString();
                if (int.TryParse(codigoSesion, out idUsuario) && idUsuario > 0)
                {
                    Session["IdUsuario"] = idUsuario;
                    Session["UserId"] = idUsuario;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(codigoSesion))
                {
                    Usuario usuarioPorCodigo;
                    if (TryResolverUsuarioPorLogin(codigoSesion, out usuarioPorCodigo))
                    {
                        SincronizarSesionUsuario(usuarioPorCodigo, codigoSesion);
                        idUsuario = usuarioPorCodigo.Id;
                        return true;
                    }
                }
            }

            try
            {
                if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                {
                    var identidades = new List<string>
                    {
                        User.Identity.Name
                    };

                    if (HttpContext != null && HttpContext.User != null && HttpContext.User.Identity != null)
                    {
                        identidades.Add(HttpContext.User.Identity.Name);
                    }

                    if (Request != null && Request.LogonUserIdentity != null)
                    {
                        identidades.Add(Request.LogonUserIdentity.Name);
                    }

                    foreach (var identidad in identidades.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        Usuario usuarioPorIdentidad;
                        if (TryResolverUsuarioPorLogin(identidad, out usuarioPorIdentidad))
                        {
                            SincronizarSesionUsuario(usuarioPorIdentidad, identidad);
                            idUsuario = usuarioPorIdentidad.Id;
                            return true;
                        }
                    }
                }
            }
            catch (Exception exIdentity)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error resolviendo ID de usuario desde Identity.Name: " + exIdentity.Message);
            }

            return false;
        }

        private bool TryResolverUsuarioPorLogin(string loginInput, out Usuario usuario)
        {
            usuario = null;
            var candidatos = ExpandirCandidatosLogin(loginInput);

            foreach (var candidato in candidatos)
            {
                try
                {
                    usuario = UsuarioDAO.ObtenerPorNombreUsuario(candidato);
                    if (usuario != null && usuario.Id > 0)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error resolviendo usuario por login '" + candidato + "': " + ex.Message);
                }
            }

            return false;
        }

        private static List<string> ExpandirCandidatosLogin(string valor)
        {
            var candidatos = new List<string>();
            var bruto = (valor ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(bruto))
            {
                return candidatos;
            }

            candidatos.Add(bruto);

            if (bruto.Contains("\\"))
            {
                var afterSlash = bruto.Substring(bruto.LastIndexOf("\\", StringComparison.Ordinal) + 1).Trim();
                if (!string.IsNullOrWhiteSpace(afterSlash))
                {
                    candidatos.Add(afterSlash);
                }
            }

            if (bruto.Contains("/"))
            {
                var afterForwardSlash = bruto.Substring(bruto.LastIndexOf("/", StringComparison.Ordinal) + 1).Trim();
                if (!string.IsNullOrWhiteSpace(afterForwardSlash))
                {
                    candidatos.Add(afterForwardSlash);
                }
            }

            if (bruto.Contains("@"))
            {
                var localPart = bruto.Split('@')[0].Trim();
                if (!string.IsNullOrWhiteSpace(localPart))
                {
                    candidatos.Add(localPart);
                }
            }

            return candidatos
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void SincronizarSesionUsuario(Usuario usuario, string loginFallback)
        {
            if (usuario == null || usuario.Id <= 0)
            {
                return;
            }

            Session["IdUsuario"] = usuario.Id;
            Session["UserId"] = usuario.Id;
            Session["CodigoUsuario"] = !string.IsNullOrWhiteSpace(usuario.CodigoUsuario)
                ? usuario.CodigoUsuario.Trim()
                : (loginFallback ?? string.Empty).Trim();

            if (Session["NombreUsuario"] == null && !string.IsNullOrWhiteSpace(usuario.NombreCompleto))
            {
                Session["NombreUsuario"] = usuario.NombreCompleto.Trim();
            }

            if (Session["Correo"] == null && !string.IsNullOrWhiteSpace(usuario.Email))
            {
                Session["Correo"] = usuario.Email.Trim();
            }
        }

        private int ObtenerUsuarioActualId()
        {
            int idUsuario;
            if (TryObtenerUsuarioActualId(out idUsuario))
                return idUsuario;

            throw new InvalidOperationException("No se pudo obtener el ID del usuario actual.");
        }

        private bool EsAdmin()
        {
            var rol = (Session["Rol"] ?? "").ToString();
            return rol.Equals("ADMIN", StringComparison.OrdinalIgnoreCase) ||
                   rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase);
        }
    }
}
