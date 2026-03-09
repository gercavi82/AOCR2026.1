using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
using Newtonsoft.Json;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class SolicitudAOCRController : Controller
    {
        private readonly SolicitudBL _solicitudBL = new SolicitudBL();
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

                    vm.Solicitud.CedulaRepresentante = !string.IsNullOrWhiteSpace(vm.Solicitud.CedulaRepresentante)
                        ? vm.Solicitud.CedulaRepresentante
                        : (vm.Usuario?.Ruc ?? vm.Usuario?.CodigoUsuario ?? string.Empty);

                    vm.Solicitud.NombreComercial = !string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial)
                        ? vm.Solicitud.NombreComercial
                        : (vm.Solicitud.NombreOperador ?? string.Empty);

                    if (!string.IsNullOrWhiteSpace(companiaActivaCodigo))
                    {
                        vm.Solicitud.CompaniasSeleccionadas = AsegurarCompaniaEnLista(vm.Solicitud.CompaniasSeleccionadas, companiaActivaCodigo);
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
                        CorreoRepresentanteTecnico = vm.Usuario != null ? vm.Usuario.Email : "",

                        // Si no hay RUC/cédula en BD, usar código de usuario como fallback seguro.
                        Ruc = vm.Usuario != null ? (vm.Usuario.Ruc ?? vm.Usuario.CodigoUsuario) : "",
                        CedulaRepresentante = vm.Usuario != null ? (vm.Usuario.Ruc ?? vm.Usuario.CodigoUsuario) : "",
                        NombreComercial = !string.IsNullOrWhiteSpace(companiaActivaCodigo)
                            ? companiaActivaCodigo
                            : (vm.Usuario != null ? vm.Usuario.EmpresaCodigo : ""),
                        NombreOperador = !string.IsNullOrWhiteSpace(companiaActivaNombre)
                            ? companiaActivaNombre
                            : (vm.Usuario != null ? vm.Usuario.EmpresaCodigo : ""),
                        CompaniasSeleccionadas = companiaActivaCodigo
                    };

                    vm.Aeronaves = new List<AeronaveSolicitud>();
                    vm.DocumentosExistentes = new List<Documento>();
                }

                vm.CompaniasDisponibles = CargarCatalogoCompanias(5000);

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

                // Si viene multipart/form-data y el model binder no armó VM, intentar reconstruir desde JSON.
                if ((vm == null || vm.Solicitud == null) && Request != null && Request.Form != null)
                {
                    var vmJson = Request.Form["vmJson"];
                    if (!string.IsNullOrWhiteSpace(vmJson))
                    {
                        try
                        {
                            vm = JsonConvert.DeserializeObject<SolicitudAOCRViewModel>(vmJson);
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
                    return Json(new { success = false, mensaje = "Sesión expirada." }, JsonRequestBehavior.AllowGet);
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

                // Normalización de campos para mantener compatibilidad con estructura actual.
                vm.Solicitud.CorreoRepresentanteTecnico = string.IsNullOrWhiteSpace(vm.Solicitud.CorreoRepresentanteTecnico)
                    ? vm.Solicitud.Email
                    : vm.Solicitud.CorreoRepresentanteTecnico;
                vm.Solicitud.NombreComercial = string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial)
                    ? vm.Solicitud.NombreOperador
                    : vm.Solicitud.NombreComercial;
                vm.Solicitud.ResumenOperacionesEae = string.IsNullOrWhiteSpace(vm.Solicitud.ResumenOperacionesEae)
                    ? vm.Solicitud.DescripcionOperacion
                    : vm.Solicitud.ResumenOperacionesEae;

                if (!string.IsNullOrWhiteSpace(companiaActivaCodigo))
                {
                    vm.Solicitud.CompaniasSeleccionadas = AsegurarCompaniaEnLista(vm.Solicitud.CompaniasSeleccionadas, companiaActivaCodigo);
                    if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreComercial))
                    {
                        vm.Solicitud.NombreComercial = companiaActivaCodigo;
                    }
                    if (string.IsNullOrWhiteSpace(vm.Solicitud.NombreOperador) && !string.IsNullOrWhiteSpace(companiaActivaNombre))
                    {
                        vm.Solicitud.NombreOperador = companiaActivaNombre;
                    }
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

                if (!string.IsNullOrWhiteSpace(vm.Solicitud.CedulaRepresentante))
                {
                    var identificacion = new string(vm.Solicitud.CedulaRepresentante.Where(char.IsDigit).ToArray());
                    if (identificacion.Length != 10 && identificacion.Length != 13)
                    {
                        return Json(new { success = false, mensaje = "La identificación del Representante Técnico debe tener 10 (cédula) o 13 (RUC) dígitos." }, JsonRequestBehavior.AllowGet);
                    }

                    vm.Solicitud.CedulaRepresentante = identificacion;
                    vm.Solicitud.Ruc = identificacion;
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

                    if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(actual, companiaActivaCodigo))
                        return Json(new { success = false, mensaje = "La solicitud no corresponde a la compañía activa." }, JsonRequestBehavior.AllowGet);

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

                // 3) Documentos (desde multipart o colección clásica)
                if (Request?.Files != null && Request.Files.Count > 0)
                {
                    ProcesarArchivosRequest(Request.Files, idFinal, vm.DocumentosCarga, usuarioCorreo);
                }

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
                        continue;
                    }

                    string fileName = result.StoredName;
                    string rutaRelativa = "~/App_Data/Uploads/AOCR/" + solicitudId + "/" + fileName;

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

        private void ProcesarArchivosRequest(
            HttpFileCollectionBase archivos,
            int solicitudId,
            IList<DocumentoCargaVM> metadatos,
            string usuarioRegistro)
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
                    continue;
                }

                if (file.ContentLength > TamanoMaximoDocumentoMb * 1024 * 1024)
                {
                    continue;
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
                    continue;
                }

                var rutaRelativa = "~/App_Data/Uploads/AOCR/" + solicitudId + "/Documentos/" + result.StoredName;
                var doc = new Documento
                {
                    CodigoSolicitud = solicitudId,
                    TipoDocumento = tipoDocumento,
                    NombreArchivo = result.StoredName,
                    RutaGuardada = rutaRelativa,
                    Extension = extension,
                    TamanoBytes = file.ContentLength,
                    Estado = "PENDIENTE",
                    Validado = false,
                    FechaCarga = DateTime.Now,
                    Observaciones = concepto,
                    Version = 1,
                    UsuarioRegistro = string.IsNullOrWhiteSpace(usuarioRegistro) ? "sistema" : usuarioRegistro
                };

                _documentoDAO.Crear(doc);
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

        private string AsegurarCompaniaEnLista(string listaCompanias, string companiaCodigo)
        {
            var codigo = (companiaCodigo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return (listaCompanias ?? string.Empty).Trim();
            }

            var elementos = (listaCompanias ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!elementos.Contains(codigo.ToUpperInvariant(), StringComparer.OrdinalIgnoreCase))
            {
                elementos.Add(codigo.ToUpperInvariant());
            }

            return string.Join(",", elementos);
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

            if (Session["IdUsuario"] != null && int.TryParse(Session["IdUsuario"].ToString(), out idUsuario) && idUsuario > 0)
            {
                return true;
            }

            if (Session["CodigoUsuario"] != null)
            {
                var codigoSesion = Session["CodigoUsuario"].ToString();
                if (int.TryParse(codigoSesion, out idUsuario) && idUsuario > 0)
                {
                    Session["IdUsuario"] = idUsuario;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(codigoSesion))
                {
                    try
                    {
                        var usuario = UsuarioDAO.ObtenerPorNombreUsuario(codigoSesion.Trim());
                        if (usuario != null && usuario.Id > 0)
                        {
                            idUsuario = usuario.Id;
                            Session["IdUsuario"] = usuario.Id;
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error resolviendo ID de usuario desde CódigoUsuario: " + ex.Message);
                    }
                }
            }

            return false;
        }

        private int ObtenerUsuarioActualId()
        {
            int idUsuario;
            if (TryObtenerUsuarioActualId(out idUsuario))
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
