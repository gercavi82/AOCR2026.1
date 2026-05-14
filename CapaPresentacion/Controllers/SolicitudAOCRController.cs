using System;
using System.IO;
using System.Linq;
using System.Text;
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
using CapaPresentacion.Filters;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models;
using CapaPresentacion.Helpers;
using CapaNegocio;
using CapaNegocio.Integraciones.As400Sync;
using CapaNegocio.Helpers;
using CapaUtilidades;
using CapaDatos.Services;
using CapaNegocio.Services;
using Newtonsoft.Json;
using Npgsql;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class SolicitudAOCRController : Controller
    {
        private readonly SolicitudBL _solicitudBL = new SolicitudBL();
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBL = new SolicitudAocrInfraBL();
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBL = new SolicitudEstadoTransitionBL();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();
        private readonly DocumentoDAO _documentoDAO = new DocumentoDAO();
        private readonly AocrFirmaDocumentoDAO _aocrFirmaDocumentoDao = new AocrFirmaDocumentoDAO();
        private readonly SolicitudAocrCorreoService _solicitudAocrCorreoService = new SolicitudAocrCorreoService();
        private readonly GeneracionAOCRService _generacionAocrService = new GeneracionAOCRService();

        private readonly AeronaveSolicitudDAO _aeronaveSolDAO = new AeronaveSolicitudDAO();
        private readonly PagoDAO _pagoDAO = new PagoDAO();
        private readonly OrdenRecaudacionDAO _ordenRecaudacionDAO = new OrdenRecaudacionDAO();
        private readonly InspeccionInformeDAO _inspeccionInformeDAO = new InspeccionInformeDAO();
        private readonly HallazgoDAO _hallazgoDAO = new HallazgoDAO();

        private static readonly HashSet<string> ExtensionesPermitidasDocumentos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png"
        };

        private static readonly IDictionary<string, string> DocumentoObligatorioEtiquetas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FACTURA", "Factura" },
            { "AOC", "Copia de AOC valida" },
            { "OPSPECS", "OpSpecs" },
            { "MANUAL_OPERACIONES", "Manual de Operaciones" },
            { "PERMISO_OPERACION", "Permiso de Operacion C.N.A.C" },
            { "CERTIFICADO_RUIDO", "Certificados de Ruido" },
            { "PODER_REPRESENTANTE", "Poder otorgado al representante legal" }
        };

        private static readonly IDictionary<string, string[]> DocumentoObligatorioTipos = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "FACTURA", new[] { "COMPROBANTE_PAGO", "FACTURA", "FACTURA_PAGO" } },
            { "AOC", new[] { "COPIA_AOC_VALIDA" } },
            { "OPSPECS", new[] { "OPSPECS_ESPECIFICACIONES_OPERACIONALES" } },
            { "MANUAL_OPERACIONES", new[] { "MANUAL_OPERACIONES" } },
            { "PERMISO_OPERACION", new[] { "PERMISO_OPERACION_CNAC" } },
            { "CERTIFICADO_RUIDO", new[] { "CERTIFICADO_RUIDO_AERONAVES_EAE" } },
            { "PODER_REPRESENTANTE", new[] { "COPIA_CERTIFICADA_PODER_REPRESENTANTE_ECUADOR" } }
        };

        private static readonly IDictionary<string, string> DocumentoObligatorioInputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "archivoFacturaPago", "FACTURA" },
            { "archivoAOC", "AOC" },
            { "archivoOpSpecs", "OPSPECS" },
            { "archivoManualOperaciones", "MANUAL_OPERACIONES" },
            { "archivoPermisoOperacion", "PERMISO_OPERACION" },
            { "archivoCertificadoRuido", "CERTIFICADO_RUIDO" },
            { "archivoPoderRepresentante", "PODER_REPRESENTANTE" }
        };

        private const int TamanoMaximoDocumentoMb = 10;
    private const string DocumentoTipoCondicionesLimitaciones = "CONDICIONES_LIMITACIONES";

        public ActionResult Index(int? tipoSolicitud = null, bool abrirModal = false)
        {
            ViewBag.TipoSolicitudInicial = NormalizarTipoSolicitud(tipoSolicitud);
            ViewBag.AbrirModalInicial = abrirModal;
            return View();
        }

        [HttpGet]
        public ActionResult CargarDocumentos()
        {
            TempData["Info"] = "Seleccione una solicitud para gestionar o cargar documentos desde su detalle.";
            return RedirectToAction("Index");
        }

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

        private static int NormalizarTipoSolicitud(int? tipoSolicitud)
        {
            switch (tipoSolicitud ?? 1)
            {
                case 1:
                case 2:
                case 3:
                    return tipoSolicitud ?? 1;
                default:
                    return 1;
            }
        }

        private static bool EsSolicitudModificacion(SolicitudAOCR solicitud)
        {
            return solicitud != null && solicitud.TipoSolicitud.GetValueOrDefault() == 3;
        }

        private static bool SolicitudModificacionTieneNuevoAeropuertoDeclarado(SolicitudAOCR solicitud)
        {
            if (!EsSolicitudModificacion(solicitud))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(solicitud.AeropuertosEcuador)
                || !string.IsNullOrWhiteSpace(solicitud.AeropuertosEcuadorOtros);
        }

        private static IEnumerable<KeyValuePair<string, string>> ObtenerDocumentosObligatoriosPorTipoSolicitud(int? tipoSolicitud)
        {
            var tipoNormalizado = NormalizarTipoSolicitud(tipoSolicitud);
            return DocumentoObligatorioEtiquetas.Where(item =>
                tipoNormalizado != 3 || !string.Equals(item.Key, "CERTIFICADO_RUIDO", StringComparison.OrdinalIgnoreCase));
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
                var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(codigoSolicitud);
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
            if (norm == EstadoSolicitud.FirmadoCoordinador)
                return "Aceptación firmada por coordinación";
            if (norm == EstadoSolicitud.Finalizado)
                return "Finalizado";
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
                norm == EstadoSolicitud.Finalizado ||
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
        public ActionResult FormularioEmisionAOCR(int? oid, int? tipoSolicitud = null)
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

                if ((!oid.HasValue || oid.Value <= 0) && !EsAdmin())
                {
                    var solicitudActiva = BuscarSolicitudActivaReutilizable(usuarioId, companiaActivaCodigo, tipoSolicitud);
                    if (solicitudActiva != null)
                    {
                        oid = solicitudActiva.CodigoSolicitud;
                        System.Diagnostics.Trace.TraceInformation(
                            "[SOLICITUD_AOCR] Reutilizando solicitud activa existente " + solicitudActiva.CodigoSolicitud +
                            " para usuario=" + usuarioId +
                            "; compania=" + (companiaActivaCodigo ?? string.Empty));
                    }
                }

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

                    // Guard: bloquear edición si el pago aún está pendiente de aprobación por Financiero
                    if (!EsAdmin() && !User.IsInRole("Financiero") && !User.IsInRole("CoordinadorFinanciero"))
                    {
                        var estadoNormGuard = EstadoSolicitud.Normalizar(vm.Solicitud.Estado ?? string.Empty);
                        if (estadoNormGuard == EstadoSolicitud.PagoPendiente)
                        {
                            return Content(
                                "<div class='alert alert-warning m-3'>" +
                                "<i class='fas fa-lock me-2'></i>" +
                                "<strong>Solicitud bloqueada.</strong><br/>" +
                                "La solicitud estará disponible cuando el pago sea aprobado por Financiero. " +
                                "Una vez que Financiero valide el comprobante de pago, recibirá una notificación y podrá continuar con el llenado de la solicitud." +
                                "</div>");
                        }
                    }

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
                    var tipoSolicitudInicial = NormalizarTipoSolicitud(tipoSolicitud);

                    // NUEVO: precargar desde usuario
                    vm.Solicitud = new SolicitudAOCR
                    {
                        CodigoUsuario = usuarioId,
                        TipoSolicitud = tipoSolicitudInicial,
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
        [ValidateAntiForgeryTokenFromHeader]
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
                vm.Solicitud.TipoSolicitud = NormalizarTipoSolicitud(vm.Solicitud.TipoSolicitud);
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

                if (vm.Solicitud.CodigoSolicitud <= 0)
                {
                    var solicitudActiva = BuscarSolicitudActivaReutilizable(usuarioId, companiaSeleccionadaCodigo, vm.Solicitud.TipoSolicitud);
                    if (solicitudActiva != null)
                    {
                        vm.Solicitud.CodigoSolicitud = solicitudActiva.CodigoSolicitud;
                        System.Diagnostics.Trace.TraceInformation(
                            "[SOLICITUD_AOCR] FormularioCompleto reutiliza solicitud=" + solicitudActiva.CodigoSolicitud +
                            " para usuario=" + usuarioId +
                            "; compania=" + (companiaSeleccionadaCodigo ?? string.Empty));
                    }
                }

                // Dueño si es nuevo / seguridad si edita
                SolicitudAOCR actual = null;
                var esNuevaSolicitud = vm.Solicitud.CodigoSolicitud <= 0;
                var solicitudPerteneceUsuarioActual = vm.Solicitud.CodigoSolicitud <= 0;
                var estadoActualNormalizado = string.Empty;
                var esBorradorLegacy = false;
                if (vm.Solicitud.CodigoSolicitud <= 0)
                {
                    vm.Solicitud.CodigoUsuario = usuarioId;
                    vm.Solicitud.TipoSolicitud = NormalizarTipoSolicitud(vm.Solicitud.TipoSolicitud);
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

                    // Guard POST: no permitir guardar si el pago está pendiente de aprobación
                    if (!EsAdmin() && !User.IsInRole("Financiero") && !User.IsInRole("CoordinadorFinanciero"))
                    {
                        var estadoNormPost = EstadoSolicitud.Normalizar(actual.Estado ?? string.Empty);
                        if (estadoNormPost == EstadoSolicitud.PagoPendiente)
                        {
                            return Json(new { success = false, mensaje = "La solicitud está bloqueada. El pago debe ser aprobado por Financiero antes de continuar." }, JsonRequestBehavior.AllowGet);
                        }
                    }

                    vm.Solicitud.CodigoUsuario = actual.CodigoUsuario;
                    solicitudPerteneceUsuarioActual = actual.CodigoUsuario == usuarioId;
                    estadoActualNormalizado = EstadoSolicitud.Normalizar(actual.Estado ?? string.Empty);
                    esBorradorLegacy = string.Equals((actual.Estado ?? string.Empty).Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase);
                    vm.Solicitud.Estado = actual.Estado;
                }

                var requiereEnvioCoordinador = esNuevaSolicitud
                    || string.Equals(estadoActualNormalizado, EstadoSolicitud.Pendiente, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoActualNormalizado, EstadoSolicitud.SolicitudCreada, StringComparison.OrdinalIgnoreCase)
                    || esBorradorLegacy;

                var documentosFaltantes = ObtenerDocumentosObligatoriosFaltantes(
                    actual != null ? (int?)actual.CodigoSolicitud : null,
                    Request != null ? Request.Files : null,
                    vm.Solicitud != null && vm.Solicitud.TipoSolicitud.HasValue ? vm.Solicitud.TipoSolicitud : (actual != null ? actual.TipoSolicitud : null));
                if (documentosFaltantes.Count > 0)
                {
                    return JsonConEstado(new
                    {
                        success = false,
                        mensaje = "Debe adjuntar todos los documentos obligatorios antes de enviar la solicitud. Faltan: " + string.Join(", ", documentosFaltantes) + "."
                    }, 400);
                }

                if (requiereEnvioCoordinador)
                {
                    vm.Solicitud.Estado = EstadoSolicitud.EnRevision;
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

                if (!esNuevaSolicitud && requiereEnvioCoordinador)
                {
                    try
                    {
                        new HistorialEstadoDAO().RegistrarCambio(
                            idFinal,
                            actual != null ? EstadoSolicitud.Normalizar(actual.Estado) : null,
                            EstadoSolicitud.EnRevision,
                            usuarioId,
                            "Solicitud formal enviada al coordinador para revisión documental.");
                    }
                    catch (Exception exHistorialEnvio)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Error registrando historial de envío documental: " + exHistorialEnvio.Message);
                    }
                }

                if (requiereEnvioCoordinador)
                {
                    try
                    {
                        var solicitudNotificacion = _solicitudDAO.ObtenerPorId(idFinal) ?? vm.Solicitud;
                        _solicitudAocrCorreoService.NotificarEvento(
                            solicitudNotificacion,
                            "SOLICITUD_COMPLETADA",
                            "Solicitud formal enviada al coordinador para revisión documental.");
                    }
                    catch (Exception exCorreoCoordinacion)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Error notificando envío a coordinación: " + exCorreoCoordinacion.Message);
                    }
                }

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

        /// <summary>
        /// Guarda el progreso parcial de una sección del formulario sin requerir documentos ni aeronaves.
        /// Acepta JSON con { seccion, solicitud: { CodigoSolicitud, ... campos de la sección } }.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryTokenFromHeader]
        public JsonResult GuardarProgreso()
        {
            try
            {
                int usuarioId;
                if (!this.TryGetSessionUserId(out usuarioId) && !TryObtenerUsuarioActualId(out usuarioId))
                {
                    return this.JsonContextMissing("Sesión expirada.");
                }

                string body;
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    body = reader.ReadToEnd();
                }

                if (string.IsNullOrWhiteSpace(body))
                {
                    return JsonEnvelope(false, "EMPTY_BODY", "Sin datos.", data: null);
                }

                GuardarProgresoPayload payload;
                try
                {
                    payload = JsonConvert.DeserializeObject<GuardarProgresoPayload>(body);
                }
                catch (Exception exJson)
                {
                    System.Diagnostics.Debug.WriteLine("[GuardarProgreso] JSON inválido: " + exJson.Message);
                    return JsonEnvelope(false, "INVALID_JSON", "Formato JSON inválido.", data: null);
                }

                if (payload == null || payload.Solicitud == null)
                {
                    return JsonEnvelope(false, "INVALID_PAYLOAD", "Datos inválidos.", data: null);
                }

                var sol = payload.Solicitud;
                if (sol == null)
                {
                    return JsonEnvelope(false, "INVALID_PAYLOAD", "No se pudo interpretar los datos de la solicitud.", data: null);
                }

                string seccion = !string.IsNullOrWhiteSpace(payload.Seccion) ? payload.Seccion.Trim() : "general";

                // Validaciones mínimas independientes de sección
                var companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                var companiaActivaNombre = ObtenerCompaniaActivaNombre();
                var companiaFinal = ResolverCompaniaSeleccionadaUnica(
                    companiaActivaCodigo, sol.CompaniasSeleccionadas, null);

                if (string.IsNullOrWhiteSpace(companiaFinal))
                {
                    return JsonEnvelope(false, "COMPANY_CONTEXT_MISSING", "No hay compañía activa seleccionada.", data: null);
                }

                sol.CompaniasSeleccionadas = companiaFinal;
                sol.TipoSolicitud = NormalizarTipoSolicitud(sol.TipoSolicitud);

                if (sol.CodigoSolicitud <= 0)
                {
                    var solicitudActiva = BuscarSolicitudActivaReutilizable(usuarioId, companiaFinal, sol.TipoSolicitud);
                    if (solicitudActiva != null)
                    {
                        sol.CodigoSolicitud = solicitudActiva.CodigoSolicitud;
                        System.Diagnostics.Trace.TraceInformation(
                            "[SOLICITUD_AOCR] GuardarProgreso reutiliza solicitud=" + solicitudActiva.CodigoSolicitud +
                            " para usuario=" + usuarioId +
                            "; seccion=" + seccion +
                            "; compania=" + (companiaFinal ?? string.Empty));
                    }
                }

                if (string.IsNullOrWhiteSpace(sol.NombreOperador))
                    sol.NombreOperador = !string.IsNullOrWhiteSpace(companiaActivaNombre) ? companiaActivaNombre : companiaFinal;

                if (string.IsNullOrWhiteSpace(sol.RazonSocial))
                    sol.RazonSocial = sol.NombreOperador;

                if (string.IsNullOrWhiteSpace(sol.NombreComercial))
                    sol.NombreComercial = sol.NombreOperador;

                int idFinal;
                string msg;

                if (sol.CodigoSolicitud <= 0)
                {
                    // Nueva solicitud
                    sol.CodigoUsuario = usuarioId;
                    sol.TipoSolicitud = NormalizarTipoSolicitud(sol.TipoSolicitud);
                    if (string.IsNullOrWhiteSpace(sol.NumeroSolicitud))
                        sol.NumeroSolicitud = "BORRADOR-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    sol.Estado = "BORRADOR";

                    bool ok = _solicitudBL.Crear(sol, usuarioId, out msg);
                    if (!ok)
                    {
                        return JsonEnvelope(false, "CREATE_FAILED", msg, data: null);
                    }
                    idFinal = sol.CodigoSolicitud;
                }
                else
                {
                    // Solicitud existente: verificar propiedad
                    var actual = _solicitudDAO.ObtenerPorId(sol.CodigoSolicitud);
                    if (actual == null)
                    {
                        return JsonEnvelope(false, "NOT_FOUND", "Solicitud no encontrada.", data: null);
                    }
                    if (!EsAdmin() && actual.CodigoUsuario != usuarioId)
                    {
                        return JsonEnvelope(false, "FORBIDDEN", "Sin permisos para modificar esta solicitud.", data: null);
                    }

                    AplicarCambiosGuardarProgreso(actual, sol, seccion);
                    bool ok = _solicitudBL.Actualizar(actual, usuarioId, out msg, EsAdmin());
                    if (!ok)
                    {
                        return JsonEnvelope(false, "UPDATE_FAILED", msg, data: null);
                    }
                    idFinal = actual.CodigoSolicitud;
                }

                return Json(new
                {
                    ok = true,
                    success = true,
                    code = "OK",
                    message = "Sección guardada correctamente.",
                    mensaje = "Sección guardada correctamente.",
                    id = idFinal,
                    seccion = seccion,
                    data = new
                    {
                        id = idFinal,
                        seccion = seccion
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[GuardarProgreso] Error: " + ex.Message);
                return JsonEnvelope(false, "INTERNAL_ERROR", "Error al guardar: " + ex.Message, data: null);
            }
        }

        private static void AplicarCambiosGuardarProgreso(SolicitudAOCR actual, SolicitudAOCR parcial, string seccion)
        {
            if (actual == null || parcial == null)
            {
                return;
            }

            actual.NombreOperador = parcial.NombreOperador;
            actual.RazonSocial = parcial.RazonSocial;
            actual.NombreComercial = parcial.NombreComercial;
            actual.CodigoOaci = parcial.CodigoOaci;
            actual.CompaniasSeleccionadas = parcial.CompaniasSeleccionadas;

            if (parcial.TipoSolicitud.HasValue)
            {
                actual.TipoSolicitud = parcial.TipoSolicitud;
            }

            if (!string.IsNullOrWhiteSpace(parcial.Estado))
            {
                actual.Estado = parcial.Estado;
            }

            var seccionNormalizada = (seccion ?? string.Empty).Trim().ToLowerInvariant();
            if (seccionNormalizada == "explotador")
            {
                actual.RepresentanteLegal = parcial.RepresentanteLegal;
                actual.CedulaRepresentante = parcial.CedulaRepresentante;
                actual.CorreoRepresentanteTecnico = parcial.CorreoRepresentanteTecnico;
                actual.Direccion = parcial.Direccion;
                actual.Telefono = parcial.Telefono;
                actual.Email = parcial.Email;
                actual.Ruc = parcial.Ruc;
                return;
            }

            if (seccionNormalizada == "operaciones")
            {
                actual.TipoOperacion = parcial.TipoOperacion;
                actual.DescripcionOperacion = parcial.DescripcionOperacion;
                actual.ResumenOperacionesEae = parcial.ResumenOperacionesEae;
                actual.NumeroAOC = parcial.NumeroAOC;
                actual.AprobacionesEspeciales = parcial.AprobacionesEspeciales;
                actual.AprobacionesEspecialesOtros = parcial.AprobacionesEspecialesOtros;
                actual.AeropuertosEcuador = parcial.AeropuertosEcuador;
                actual.AeropuertosEcuadorOtros = parcial.AeropuertosEcuadorOtros;
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

        private List<string> ObtenerDocumentosObligatoriosFaltantes(int? codigoSolicitud, HttpFileCollectionBase archivos, int? tipoSolicitud)
        {
            var cubiertos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var documentosExistentes = codigoSolicitud.HasValue && codigoSolicitud.Value > 0
                ? (_documentoDAO.ObtenerPorSolicitud(codigoSolicitud.Value) ?? new List<Documento>())
                : new List<Documento>();
            var documentosObligatorios = ObtenerDocumentosObligatoriosPorTipoSolicitud(tipoSolicitud).ToList();
            var clavesObligatorias = new HashSet<string>(documentosObligatorios.Select(item => item.Key), StringComparer.OrdinalIgnoreCase);

            foreach (var documento in documentosExistentes.Where(d => d != null && d.CodigoDocumento > 0))
            {
                foreach (var item in DocumentoObligatorioTipos)
                {
                    if (clavesObligatorias.Contains(item.Key)
                        && item.Value.Any(tipo => string.Equals(tipo, documento.TipoDocumento ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
                    {
                        cubiertos.Add(item.Key);
                    }
                }
            }

            if (archivos != null)
            {
                for (var i = 0; i < archivos.Count; i++)
                {
                    var archivo = archivos[i];
                    if (archivo == null || archivo.ContentLength <= 0)
                    {
                        continue;
                    }

                    var inputKey = (archivos.GetKey(i) ?? string.Empty).Trim();
                    string documentoObligatorio;
                    if (DocumentoObligatorioInputs.TryGetValue(inputKey, out documentoObligatorio)
                        && clavesObligatorias.Contains(documentoObligatorio))
                    {
                        cubiertos.Add(documentoObligatorio);
                    }
                }
            }

            return documentosObligatorios
                .Where(item => !cubiertos.Contains(item.Key))
                .Select(item => item.Value)
                .ToList();
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
                var cedula = NormalizarIdentificacion(_solicitudAocrInfraBL.ObtenerCedulaPorCodigoUsuario(codigoUsuario));
                if (!string.IsNullOrWhiteSpace(cedula))
                {
                    return cedula;
                }

                var ruc = NormalizarIdentificacion(_solicitudAocrInfraBL.ObtenerNumeroRucPorCodigoUsuario(codigoUsuario));
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
                var empresa = _solicitudAocrInfraBL.ObtenerEmpresaPorCodigo(codigo);
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

        private SolicitudAOCR BuscarSolicitudActivaReutilizable(int codigoUsuario, string companiaActivaCodigo, int? tipoSolicitud, int? excluirCodigoSolicitud = null)
        {
            if (codigoUsuario <= 0)
            {
                return null;
            }

            var tipoNormalizado = NormalizarTipoSolicitud(tipoSolicitud);
            return FiltrarSolicitudesPorCompaniaActiva(_solicitudDAO.ObtenerPorUsuario(codigoUsuario), companiaActivaCodigo)
                .Where(s => s != null && s.CodigoSolicitud > 0)
                .Where(s => !excluirCodigoSolicitud.HasValue || s.CodigoSolicitud != excluirCodigoSolicitud.Value)
                .Where(s => NormalizarTipoSolicitud(s.TipoSolicitud) == tipoNormalizado)
                .Where(EsSolicitudActivaReutilizable)
                .Select(s => new
                {
                    Solicitud = s,
                    TieneInspeccion = (_solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(s.CodigoSolicitud) ?? new List<Inspeccion>())
                        .Any(i => i != null && i.CodigoInspeccion > 0)
                })
                .OrderByDescending(x => x.TieneInspeccion)
                .ThenByDescending(x => x.Solicitud.CodigoSolicitud)
                .Select(x => x.Solicitud)
                .FirstOrDefault();
        }

        private static bool EsSolicitudActivaReutilizable(SolicitudAOCR solicitud)
        {
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return false;
            }

            switch (EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty))
            {
                case EstadoSolicitud.Pendiente:
                case EstadoSolicitud.EnRevision:
                case EstadoSolicitud.DocumentacionCompleta:
                case EstadoSolicitud.DocumentacionPendiente:
                case EstadoSolicitud.Observada:
                case EstadoSolicitud.Subsanada:
                case EstadoSolicitud.AceptacionDocumental:
                case EstadoSolicitud.RequiereInspeccion:
                case EstadoSolicitud.PagoPendiente:
                case EstadoSolicitud.PagoValidado:
                case EstadoSolicitud.PendienteAsignacionRT:
                case EstadoSolicitud.InspeccionProgramada:
                case EstadoSolicitud.InspeccionRealizada:
                case EstadoSolicitud.EnInspeccion:
                case EstadoSolicitud.GeneradoCondicionesLimitaciones:
                case EstadoSolicitud.EnRevisionCoordinadorFinal:
                case EstadoSolicitud.EnviadoDcav:
                case EstadoSolicitud.FirmadoDcav:
                case EstadoSolicitud.FirmadoCoordinador:
                case EstadoSolicitud.AOCR_EnElaboracion:
                case EstadoSolicitud.AOCR_EnRevision:
                case EstadoSolicitud.AOCR_Validado:
                case EstadoSolicitud.AOCR_Legalizado:
                    return true;
                default:
                    return false;
            }
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

        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Administrador")]
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
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Administrador")]
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
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Administrador")]
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
        [Authorize(Roles = "Inspector,Coordinador,CoordinadorInspecciones,Administrador")]
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

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, id, out redireccionProcesoCerrado))
            {
                return redireccionProcesoCerrado;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!SolicitudEstaEnEtapaRevisionDocumental(estadoSolicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            var documento = _documentoDAO.ObtenerPorId(codigoDocumento);
            if (documento == null || documento.CodigoSolicitud != id)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "El documento no pertenece a la solicitud seleccionada.";
                return RedirectToAction("Detalle", new { id });
            }

            var decisionNorm = NormalizarDecisionRevisionDocumental(decision);
            if (decisionNorm != "ACEPTADO" && decisionNorm != "DEVUELTO" && decisionNorm != "OBSERVADO")
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La decisión documental no es válida.";
                return RedirectToAction("Detalle", new { id });
            }

            var observacionNormalizada = (observacion ?? string.Empty).Trim();
            if (DecisionRevisionRequiereObservacion(decisionNorm) && string.IsNullOrWhiteSpace(observacionNormalizada))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Debe registrar una observación cuando el documento sea devuelto u observado.";
                return RedirectToAction("Detalle", new { id });
            }

            var estadoDocumento = decisionNorm == "ACEPTADO"
                ? "APROBADO"
                : (decisionNorm == "OBSERVADO" ? "OBSERVADO" : "RECHAZADO");
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
            _solicitudAocrInfraBL.RegistrarRevisionDocumental(id, codigoDocumento, decisionNorm, observacionNormalizada, usuarioId, usuarioRegistro);
            _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
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
        [Authorize(Roles = "Inspector,Coordinador,CoordinadorInspecciones,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult AccionMasivaRevisionDocumental(int id, string tipoAccion, string revisionesJson, string observacionCoordinador)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "La solicitud no existe.";
                return RedirectToAction("RevisarSolicitudes");
            }

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, id, out redireccionProcesoCerrado))
            {
                return redireccionProcesoCerrado;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!SolicitudEstaEnEtapaRevisionDocumental(estadoSolicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            var tipoAccionNorm = (tipoAccion ?? string.Empty).Trim().ToUpperInvariant();
            if (tipoAccionNorm != "APROBAR_TODOS" && tipoAccionNorm != "REGISTRAR_OBSERVACIONES")
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La acción masiva seleccionada no es válida.";
                return RedirectToAction("Detalle", new { id });
            }

            List<RevisionDocumentalMasivaItem> revisionesPayload;
            try
            {
                revisionesPayload = JsonConvert.DeserializeObject<List<RevisionDocumentalMasivaItem>>(revisionesJson ?? "[]")
                    ?? new List<RevisionDocumentalMasivaItem>();
            }
            catch
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "No fue posible leer el detalle de revisión documental masiva.";
                return RedirectToAction("Detalle", new { id });
            }

            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            if (documentosRevision.Count == 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No existen documentos vigentes para revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            var revisionesPorDocumento = revisionesPayload
                .Where(x => x != null && x.CodigoDocumento > 0)
                .GroupBy(x => x.CodigoDocumento)
                .ToDictionary(g => g.Key, g => g.First());

            var documentosSinDecision = new List<string>();
            var documentosSinObservacion = new List<string>();
            var hayDevueltosUObservados = false;
            var todosAceptados = true;

            foreach (var doc in documentosRevision)
            {
                RevisionDocumentalMasivaItem revision;
                if (!revisionesPorDocumento.TryGetValue(doc.CodigoDocumento, out revision))
                {
                    documentosSinDecision.Add(ObtenerEtiquetaDocumento(doc));
                    todosAceptados = false;
                    continue;
                }

                var decisionNorm = NormalizarDecisionRevisionDocumental(revision.Decision);
                revision.Decision = decisionNorm;
                revision.Observacion = (revision.Observacion ?? string.Empty).Trim();

                if (decisionNorm != "ACEPTADO" && decisionNorm != "DEVUELTO" && decisionNorm != "OBSERVADO")
                {
                    documentosSinDecision.Add(ObtenerEtiquetaDocumento(doc));
                    todosAceptados = false;
                    continue;
                }

                if (DecisionRevisionRequiereObservacion(decisionNorm))
                {
                    hayDevueltosUObservados = true;
                    todosAceptados = false;
                    if (string.IsNullOrWhiteSpace(revision.Observacion))
                    {
                        documentosSinObservacion.Add(ObtenerEtiquetaDocumento(doc));
                    }
                }
            }

            if (documentosSinDecision.Count > 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No se puede ejecutar la acción masiva. Faltan decisiones en: " + string.Join(", ", documentosSinDecision) + ".";
                return RedirectToAction("Detalle", new { id });
            }

            if (documentosSinObservacion.Count > 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No se puede ejecutar la acción masiva. Debe registrar observación en: " + string.Join(", ", documentosSinObservacion) + ".";
                return RedirectToAction("Detalle", new { id });
            }

            if (tipoAccionNorm == "APROBAR_TODOS" && !todosAceptados)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La aprobación masiva solo está disponible cuando todos los documentos están en Aceptado.";
                return RedirectToAction("Detalle", new { id });
            }

            if (tipoAccionNorm == "REGISTRAR_OBSERVACIONES" && !hayDevueltosUObservados)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Para registrar devolución/observaciones masivas debe existir al menos un documento observado o devuelto.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerUsuarioActualId();
            var usuarioRegistro = (Session["CodigoUsuario"] ?? User.Identity.Name ?? "sistema").ToString();

            foreach (var doc in documentosRevision)
            {
                var revision = revisionesPorDocumento[doc.CodigoDocumento];
                var estadoDocumento = revision.Decision == "ACEPTADO"
                    ? "APROBADO"
                    : (revision.Decision == "OBSERVADO" ? "OBSERVADO" : "RECHAZADO");

                doc.Estado = estadoDocumento;
                doc.Validado = revision.Decision == "ACEPTADO";
                doc.Observaciones = revision.Observacion;
                doc.FechaCarga = doc.FechaCarga ?? DateTime.Now;
                doc.UsuarioRegistro = (Session["CodigoUsuario"] ?? "sistema").ToString();

                if (!_documentoDAO.Actualizar(doc))
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = "No se pudo registrar la revisión masiva para todos los documentos.";
                    return RedirectToAction("Detalle", new { id });
                }

                _solicitudAocrInfraBL.RegistrarRevisionDocumental(id, doc.CodigoDocumento, revision.Decision, revision.Observacion, usuarioId, usuarioRegistro);
                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    id,
                    doc.CodigoDocumento,
                    "REVISION_DOCUMENTAL",
                    "Documento " + (doc.TipoDocumento ?? "N/A") + " marcado como " + revision.Decision + ". " + revision.Observacion,
                    usuarioId,
                    usuarioRegistro);
            }

            var revisionesResumen = revisionesPorDocumento
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => Tuple.Create(kvp.Value.Decision, kvp.Value.Observacion));

            var estadoDestino = tipoAccionNorm == "APROBAR_TODOS"
                ? EstadoSolicitud.AceptacionDocumental
                : EstadoSolicitud.Observada;

            var observacionCoordinadorLimpia = (observacionCoordinador ?? string.Empty).Trim();
            if (observacionCoordinadorLimpia.Length > 500)
            {
                observacionCoordinadorLimpia = observacionCoordinadorLimpia.Substring(0, 500);
            }

            var observacionBase = tipoAccionNorm == "APROBAR_TODOS"
                ? "Todos los documentos vigentes fueron aceptados por el inspector (acción masiva)."
                : ConstruirResumenRevisionDocumental(documentosRevision, revisionesResumen, true);

            var observacionCierre = string.IsNullOrWhiteSpace(observacionCoordinadorLimpia)
                ? observacionBase
                : observacionBase + " Observación para coordinación: " + observacionCoordinadorLimpia;

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, estadoDestino, observacionCierre, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                id,
                null,
                "REVISION_DOCUMENTAL_FINALIZADA",
                observacionCierre,
                usuarioId,
                usuarioRegistro);

            if (tipoAccionNorm == "REGISTRAR_OBSERVACIONES")
            {
                try
                {
                    var documentosYaNotificados = _solicitudAocrInfraBL.ObtenerDocumentosConEventoHistorial(id, "CORREO_DOCUMENTO_DEVUELTO_ENVIADO");
                    EnviarCorreoRevisionDocumentalDevuelta(solicitud, documentosRevision, revisionesResumen, documentosYaNotificados);
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        id,
                        null,
                        "CORREO_REVISION_FINAL_RESUMEN_ENVIADO",
                        "Correo final de resumen de revision documental con observaciones enviado.",
                        usuarioId,
                        usuarioRegistro);
                }
                catch
                {
                }
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = tipoAccionNorm == "APROBAR_TODOS"
                ? "Se aprobó masivamente la revisión documental y la solicitud avanzó a Aceptación Documental."
                : "Se registró la devolución/observación masiva y la solicitud fue devuelta al operador.";

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Coordinador,CoordinadorInspecciones,Administrador")]
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

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, id, out redireccionProcesoCerrado))
            {
                return redireccionProcesoCerrado;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!SolicitudEstaEnEtapaRevisionDocumental(estadoSolicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para cerrar la revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            if (documentosRevision.Count == 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No existen documentos vigentes para cerrar la revisión.";
                return RedirectToAction("Detalle", new { id });
            }

            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);

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
            _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
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
                    var documentosYaNotificados = _solicitudAocrInfraBL.ObtenerDocumentosConEventoHistorial(id, "CORREO_DOCUMENTO_DEVUELTO_ENVIADO");
                    EnviarCorreoRevisionDocumentalDevuelta(solicitud, documentosRevision, revisiones, documentosYaNotificados);
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        id,
                        null,
                        "CORREO_REVISION_FINAL_RESUMEN_ENVIADO",
                        "Correo final de resumen de revision documental con observaciones enviado.",
                        usuarioId,
                        usuarioRegistro);
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

        private class RevisionDocumentalMasivaItem
        {
            public int CodigoDocumento { get; set; }
            public string Decision { get; set; }
            public string Observacion { get; set; }
        }

        private bool UsuarioPuedeAsignarInspector()
        {
            return User.IsInRole("Administrador")
                || User.IsInRole("Coordinador")
                || User.IsInRole("CoordinadorInspecciones");
        }

        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult RevisarPorJefatura()
        {
            var pendientes = _solicitudDAO.ObtenerPorEstados("ENVIADO_A_JEFATURA", EstadoSolicitud.AOCR_EnRevision, EstadoSolicitud.AOCR_EnElaboracion);
            return View(pendientes);
        }

        [HttpPost]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPorJefatura(int id)
        {
            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_Validado, "Validado por Dirección / Jefatura", out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
                return RedirectToAction("RevisarPorJefatura");
            }

            TempData["Exito"] = "La solicitud ha sido validada institucionalmente.";
            return RedirectToAction("RevisarPorJefatura");
        }

        [HttpPost]
        [Authorize(Roles = "DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
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

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, id, out redireccionProcesoCerrado))
            {
                return redireccionProcesoCerrado;
            }

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

            var revisionesDocumentales = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            var documentos = ObtenerDocumentosVigentesParaRevision(id)
                .Where(d =>
                {
                    var decision = ObtenerDecisionRevisionDocumental(d, revisionesDocumentales);
                    return decision == "DEVUELTO" || decision == "OBSERVADO";
                })
                .ToList();
            var historial = _solicitudAocrInfraBL.ObtenerHistorialEstadosPorSolicitud(id);

            var inspectorNombre = ObtenerNombreInspector(solicitud);

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
                    Estado = ObtenerDecisionRevisionDocumental(d, revisionesDocumentales),
                    Observaciones = ObtenerObservacionRevisionDocumental(d, revisionesDocumentales),
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

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, codigoSolicitud, out redireccionProcesoCerrado))
            {
                return redireccionProcesoCerrado;
            }

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
                var documentosSubsanadosNotificacion = new List<Documento>();

                var revisionesDocumentales = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(codigoSolicitud);
                var documentosVigentes = ObtenerDocumentosVigentesParaRevision(codigoSolicitud);
                var documentosObservadosPendientes = documentosVigentes
                    .Where(d =>
                    {
                        var decision = ObtenerDecisionRevisionDocumental(d, revisionesDocumentales);
                        return decision == "DEVUELTO" || decision == "OBSERVADO";
                    })
                    .ToList();

                if (documentosObservadosPendientes.Count == 0)
                {
                    TempData["NotificacionTipo"] = "warning";
                    TempData["NotificacionMensaje"] = "No existen documentos observados/devueltos pendientes de subsanación.";
                    return RedirectToAction("Detalle", new { id = codigoSolicitud });
                }

                var documentosObservadosPorId = documentosObservadosPendientes.ToDictionary(d => d.CodigoDocumento, d => d);
                var archivosPorDocumento = new Dictionary<int, List<HttpPostedFileBase>>();

                for (var i = 0; i < Request.Files.Count; i++)
                {
                    var file = Request.Files[i];
                    if (file == null || file.ContentLength <= 0)
                    {
                        continue;
                    }

                    var key = Request.Files.GetKey(i) ?? string.Empty;
                    int docId;
                    var parts = key.Split('_');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out docId))
                    {
                        continue;
                    }

                    if (!documentosObservadosPorId.ContainsKey(docId))
                    {
                        TempData["NotificacionTipo"] = "error";
                        TempData["NotificacionMensaje"] = "El documento seleccionado no pertenece al bloque pendiente de subsanación.";
                        return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                    }

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

                    List<HttpPostedFileBase> listaArchivos;
                    if (!archivosPorDocumento.TryGetValue(docId, out listaArchivos))
                    {
                        listaArchivos = new List<HttpPostedFileBase>();
                        archivosPorDocumento[docId] = listaArchivos;
                    }

                    listaArchivos.Add(file);
                }

                if (archivosPorDocumento.Count == 0)
                {
                    TempData["NotificacionTipo"] = "warning";
                    TempData["NotificacionMensaje"] = "Debe subir al menos un documento corregido.";
                    return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                }

                var documentosFaltantesSubsanacion = documentosObservadosPendientes
                    .Where(d => !archivosPorDocumento.ContainsKey(d.CodigoDocumento))
                    .Select(ObtenerEtiquetaDocumento)
                    .ToList();

                if (documentosFaltantesSubsanacion.Count > 0)
                {
                    TempData["NotificacionTipo"] = "warning";
                    TempData["NotificacionMensaje"] = "Debe subsanar todos los documentos observados/devueltos. Faltan: " + string.Join(", ", documentosFaltantesSubsanacion) + ".";
                    return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                }

                foreach (var par in archivosPorDocumento)
                {
                    var docOriginal = documentosObservadosPorId[par.Key];
                    var tipoDoc = !string.IsNullOrWhiteSpace(docOriginal.TipoDocumento)
                        ? docOriginal.TipoDocumento
                        : "Documento Subsanado";

                    foreach (var file in par.Value)
                    {
                        var extension = Path.GetExtension(file.FileName) ?? string.Empty;
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
                        var versionAnterior = docOriginal.Version.HasValue ? docOriginal.Version.Value : 1;

                        var nuevoDoc = new Documento
                        {
                            CodigoSolicitud = codigoSolicitud,
                            TipoDocumento = tipoDoc,
                            NombreArchivo = result.StoredName,
                            RutaGuardada = rutaRelativa,
                            Extension = extension,
                            TamanoBytes = file.ContentLength,
                            Estado = "PENDIENTE_REVISION_SUBSANACION",
                            Validado = false,
                            FechaCarga = DateTime.Now,
                            Observaciones = "Subsanación: " + (comentario ?? "").Trim(),
                            Version = versionAnterior + 1,
                            UsuarioRegistro = usuarioRegistro
                        };

                        var codigoNuevoDocumento = _documentoDAO.Crear(nuevoDoc);
                        nuevoDoc.CodigoDocumento = codigoNuevoDocumento;
                        documentosSubsanadosNotificacion.Add(nuevoDoc);
                        _solicitudAocrInfraBL.RegistrarRevisionDocumental(
                            codigoSolicitud,
                            codigoNuevoDocumento,
                            "PENDIENTE_REVISION_SUBSANACION",
                            (comentario ?? string.Empty).Trim(),
                            usuarioId,
                            usuarioRegistro);
                        _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                            codigoSolicitud,
                            codigoNuevoDocumento,
                            "DOCUMENTO_SUBSANADO_POR_RT",
                            "Documento " + (tipoDoc ?? "N/A") + " subsanado por el RT. Documento original: " + docOriginal.CodigoDocumento + ".",
                            usuarioId,
                            usuarioRegistro);
                        archivosSubidos++;
                    }
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

                NotificarInspectorDocumentacionSubsanada(solicitud, documentosSubsanadosNotificacion, comentario, usuarioId, usuarioRegistro);

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

        private void NotificarInspectorDocumentacionSubsanada(
            SolicitudAOCR solicitud,
            IList<Documento> documentosSubsanados,
            string comentarioRt,
            int usuarioId,
            string usuarioRegistro)
        {
            if (solicitud == null || documentosSubsanados == null || documentosSubsanados.Count == 0)
            {
                return;
            }

            try
            {
                var inspeccion = ObtenerUltimaInspeccionVinculada(_solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud));
                var codigoInspector = inspeccion != null && inspeccion.CodigoInspector.HasValue
                    ? inspeccion.CodigoInspector.Value
                    : (solicitud.CodigoTecnico.HasValue ? solicitud.CodigoTecnico.Value : 0);

                if (codigoInspector <= 0)
                {
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        solicitud.CodigoSolicitud,
                        null,
                        "NOTIFICACION_SUBSANACION_INSPECTOR_OMITIDA",
                        "No se encontró inspector asignado para notificar la subsanación documental.",
                        usuarioId,
                        usuarioRegistro);
                    return;
                }

                var inspector = UsuarioDAO.ObtenerPorId(codigoInspector);
                var correoInspector = inspector != null ? (inspector.Email ?? string.Empty).Trim() : string.Empty;
                var nombreInspector = inspector != null ? FirstNonEmpty(inspector.NombreCompleto, inspector.NombreUsuario, "Inspector asignado") : "Inspector asignado";
                var numeroSolicitud = FirstNonEmpty(solicitud.NumeroSolicitud, "#" + solicitud.CodigoSolicitud);
                var operadora = FirstNonEmpty(solicitud.NombreComercial, solicitud.NombreOperador, solicitud.RazonSocial, "Operadora");
                var solicitante = UsuarioDAO.ObtenerPorId(solicitud.CodigoUsuario);
                var nombreRt = FirstNonEmpty(
                    solicitud.RepresentanteLegal,
                    solicitante != null ? solicitante.NombreCompleto : null,
                    solicitante != null ? solicitante.NombreUsuario : null,
                    "Representante Técnico");
                var fechaSubsanacion = DateTime.Now;
                var documentosYaNotificados = _solicitudAocrInfraBL.ObtenerDocumentosConEventoHistorial(
                    solicitud.CodigoSolicitud,
                    "NOTIFICACION_SUBSANACION_DOCUMENTO_INSPECTOR");
                var documentos = documentosSubsanados
                    .Where(d => d != null)
                    .GroupBy(d => d.CodigoDocumento)
                    .Select(g => g.First())
                    .Where(d => d.CodigoDocumento <= 0 || !documentosYaNotificados.Contains(d.CodigoDocumento))
                    .ToList();
                if (documentos.Count == 0)
                {
                    return;
                }
                var listaDocumentosTexto = string.Join(", ", documentos.Select(ObtenerEtiquetaDocumento));
                var eventKey = "DOCUMENTACION_SUBSANADA_RT_" + solicitud.CodigoSolicitud + "_" + codigoInspector + "_" +
                               string.Join("_", documentos.Select(d => d.CodigoDocumento + "V" + (d.Version ?? 0)));

                NotificacionBL.EnviarNotificacion(
                    codigoInspector,
                    "Documentación subsanada",
                    "El RT ha subsanado documentación observada de la Solicitud AOCR " + numeroSolicitud + ".",
                    "INFO",
                    Url.Action("Detalle", "SolicitudAOCR", new { id = solicitud.CodigoSolicitud }),
                    "AOCR",
                    solicitud.CodigoSolicitud,
                    "SOLICITUD_AOCR");

                if (!string.IsNullOrWhiteSpace(correoInspector))
                {
                    var asunto = "Solicitud AOCR " + numeroSolicitud + " - Documentación subsanada por el RT";
                    var cuerpo = ConstruirHtmlCorreoDocumentacionSubsanadaInspector(
                        nombreInspector,
                        nombreRt,
                        numeroSolicitud,
                        operadora,
                        documentos,
                        fechaSubsanacion,
                        comentarioRt);

                    var queue = new EmailQueueService();
                    queue.EncolarAsync(new EmailQueueItem
                    {
                        Para = correoInspector,
                        ParaNombre = nombreInspector,
                        Asunto = asunto,
                        Cuerpo = cuerpo,
                        EsHtml = true,
                        TipoNotificacion = "DOCUMENTACION_SUBSANADA_RT",
                        OrdenId = solicitud.CodigoSolicitud,
                        EventKey = eventKey,
                        MaxIntentos = 3
                    }).Wait();
                }

                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    solicitud.CodigoSolicitud,
                    null,
                    "NOTIFICACION_SUBSANACION_ENVIADA_INSPECTOR",
                    "Notificación de subsanación documental enviada al inspector asignado. Documentos: " + listaDocumentosTexto,
                    usuarioId,
                    usuarioRegistro);

                foreach (var documento in documentos)
                {
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        solicitud.CodigoSolicitud,
                        documento.CodigoDocumento,
                        "NOTIFICACION_SUBSANACION_DOCUMENTO_INSPECTOR",
                        "Documento subsanado incluido en notificación al inspector asignado.",
                        usuarioId,
                        usuarioRegistro);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SubsanarPost][NotificarInspector] " + ex.Message);
                try
                {
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        solicitud.CodigoSolicitud,
                        null,
                        "NOTIFICACION_SUBSANACION_INSPECTOR_ERROR",
                        "No se pudo encolar el correo al inspector. La subsanación no fue bloqueada. Error: " + ex.Message,
                        usuarioId,
                        usuarioRegistro);
                }
                catch
                {
                }
            }
        }

        private static string ConstruirHtmlCorreoDocumentacionSubsanadaInspector(
            string nombreInspector,
            string nombreRt,
            string numeroSolicitud,
            string operadora,
            IEnumerable<Documento> documentos,
            DateTime fechaSubsanacion,
            string comentarioRt)
        {
            var lista = string.Join(string.Empty, (documentos ?? Enumerable.Empty<Documento>())
                .Select(d => "<li>" + HttpUtility.HtmlEncode(ObtenerEtiquetaDocumento(d)) + "</li>"));

            if (string.IsNullOrWhiteSpace(lista))
            {
                lista = "<li>Documentación subsanada</li>";
            }

            return "Estimado/a " + HttpUtility.HtmlEncode(nombreInspector) + ",<br><br>" +
                   "Se informa que el Representante Técnico " + HttpUtility.HtmlEncode(nombreRt) +
                   " ha realizado la subsanación de documentación observada correspondiente a la Solicitud AOCR " +
                   HttpUtility.HtmlEncode(numeroSolicitud) + " de la operadora " + HttpUtility.HtmlEncode(operadora) + ".<br><br>" +
                   "<strong>Documentos subsanados:</strong><ul>" + lista + "</ul>" +
                   "<strong>Fecha de subsanación:</strong> " + fechaSubsanacion.ToString("dd/MM/yyyy HH:mm") + "<br>" +
                   "<strong>Observación del RT:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(comentarioRt) ? "Sin comentario adicional." : comentarioRt.Trim()) + "<br><br>" +
                   "Por favor, ingrese al sistema AOCR para revisar la documentación subsanada y continuar con el flujo correspondiente.<br><br>" +
                   "Atentamente,<br>Sistema AOCR<br>Dirección General de Aviación Civil";
        }

        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var procesoCerradoOperativamente = SolicitudEstaCerradaOperativamente(solicitud);
            var documentosObligatoriosFaltantes = procesoCerradoOperativamente
                ? new List<string>()
                : ObtenerDocumentosObligatoriosFaltantes(id, null, solicitud.TipoSolicitud);

            ViewBag.HistorialEstados = _solicitudAocrInfraBL.ObtenerHistorialEstadosPorSolicitud(id);
            ViewBag.UsuarioActualId = ObtenerUsuarioActualId();
            ViewBag.ProcesoCerradoOperativamente = procesoCerradoOperativamente;
            ViewBag.DocumentosObligatoriosFaltantes = documentosObligatoriosFaltantes;

            try
            {
                ViewBag.InspeccionesSolicitud = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(id) ?? new List<Inspeccion>();
            }
            catch
            {
                ViewBag.InspeccionesSolicitud = new List<Inspeccion>();
            }

            ViewBag.AsignacionActiva = _solicitudAocrInfraBL.ObtenerAsignacionActiva(id);
            ViewBag.HistorialAsignaciones = _solicitudAocrInfraBL.ObtenerHistorialAsignacion(id);
            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            var revisionesDocumentales = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            ViewBag.DocumentosSolicitud = documentosRevision;
            ViewBag.RevisionesDocumentales = revisionesDocumentales;
            ViewBag.EstadoDocumentalVisible = ObtenerEstadoDocumentalVisible(solicitud, revisionesDocumentales);
            ViewBag.PuedeFinalizarRevisionDocumental =
                documentosRevision.Count > 0 &&
                documentosRevision.All(d => DocumentoTieneDecisionFinal(d, revisionesDocumentales)) &&
                !documentosRevision.Any(d => DocumentoRequiereObservacionPendiente(d, revisionesDocumentales));

            // Trazabilidad completa (aditivo, no rompe nada si la vista BD no existe)
            try
            {
                ViewBag.DocumentosHistorialCompleto = _documentoDAO.ObtenerPorSolicitud(id) ?? new List<Documento>();
            }
            catch { ViewBag.DocumentosHistorialCompleto = new List<Documento>(); }

            try
            {
                ViewBag.DocumentosSubsanacion = _solicitudAocrInfraBL.ObtenerDocumentosSubsanacionPorSolicitud(id);
            }
            catch { ViewBag.DocumentosSubsanacion = new List<CapaDatos.Entidades.DocumentoSubsanacionRegistro>(); }

            try
            {
                ViewBag.TrazabilidadCompleta = _solicitudAocrInfraBL.ObtenerTrazabilidadCompleta(id);
            }
            catch { ViewBag.TrazabilidadCompleta = new List<CapaDatos.Entidades.EventoTrazabilidad>(); }

            // Generación AOCR (reemplaza carga manual de "Borrador AOCR")
            try
            {
                var dispAocr = _generacionAocrService.Evaluar(id);
                ViewBag.PuedeGenerarAOCR = dispAocr != null && dispAocr.Habilitado;
                ViewBag.MotivoGenerarAOCR = dispAocr != null
                    ? dispAocr.Motivo
                    : "La AOCR estará disponible cuando la inspeccion sea satisfactoria y el informe tecnico quede firmado por el inspector.";
                ViewBag.DocumentoAOCRGenerado = dispAocr != null ? dispAocr.DocumentoGenerado : null;
                ViewBag.AocrYaGenerado = dispAocr != null && dispAocr.YaGenerado;
            }
            catch
            {
                ViewBag.PuedeGenerarAOCR = false;
                ViewBag.MotivoGenerarAOCR = "La AOCR estará disponible cuando la inspeccion sea satisfactoria y el informe tecnico quede firmado por el inspector.";
                ViewBag.DocumentoAOCRGenerado = null;
                ViewBag.AocrYaGenerado = false;
            }

            return View(solicitud);
        }

        [HttpPost]
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarAceptacionDocumental(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound();
            }

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, id, out redireccionProcesoCerrado))
            {
                return redireccionProcesoCerrado;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!string.Equals(estadoActual, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La aceptación documental solo se puede firmar cuando el inspector haya aceptado toda la documentación.";
                return RedirectToAction("Detalle", new { id });
            }

            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            if (documentosRevision.Count == 0 || documentosRevision.Any(d => ObtenerDecisionRevisionDocumental(d, revisiones) != "ACEPTADO"))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No se puede firmar la aceptación mientras existan documentos sin aceptar por el inspector.";
                return RedirectToAction("Detalle", new { id });
            }

            var observacionFirma = string.IsNullOrWhiteSpace(observacion)
                ? "Aceptación documental firmada por coordinación."
                : observacion.Trim();

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.FirmadoCoordinador, observacionFirma, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                var solicitudActualizada = _solicitudDAO.ObtenerPorId(id) ?? solicitud;
                _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "ACEPTACION_COORDINADOR_FIRMADA", observacionFirma);
            }
            catch (Exception exCorreo)
            {
                System.Diagnostics.Debug.WriteLine("[FirmarAceptacionDocumental] Error notificando aceptación firmada: " + exCorreo.Message);
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "La aceptación documental fue firmada. El RT ya puede descargar el documento final.";
            return RedirectToAction("Detalle", new { id });
        }

        public ActionResult DescargarAceptacionDocumental(int id, bool vistaPrevia = false)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound();
            }

            var usuarioActualId = ObtenerUsuarioActualId();
            var esPropietario = usuarioActualId > 0 && solicitud.CodigoUsuario == usuarioActualId;
            var puedeDescargar = esPropietario
                || EsAdmin()
                || (User != null && (User.IsInRole("Coordinador") || User.IsInRole("CoordinadorInspecciones")));
            if (!puedeDescargar)
            {
                return new HttpStatusCodeResult(403, "No autorizado para descargar la aceptación documental.");
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!string.Equals(estadoActual, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoActual, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La aceptación documental aún no está firmada por coordinación.";
                return RedirectToAction("Detalle", new { id });
            }

            var historialEstados = _solicitudAocrInfraBL.ObtenerHistorialEstadosPorSolicitud(id) ?? new List<CapaModelo.HistorialEstado>();
            var firmaCoordinacion = historialEstados
                .Where(h => h != null && string.Equals(EstadoSolicitud.Normalizar(h.EstadoNuevo), EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.FechaCambio)
                .FirstOrDefault();

            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            ViewBag.AceptacionFirmante = firmaCoordinacion != null && !string.IsNullOrWhiteSpace(firmaCoordinacion.NombreUsuario)
                ? firmaCoordinacion.NombreUsuario
                : (User != null && User.Identity != null ? User.Identity.Name : "Coordinación AOCR");
            ViewBag.AceptacionFechaFirma = firmaCoordinacion != null ? firmaCoordinacion.FechaCambio : (solicitud.UpdatedAt ?? DateTime.Now);
            ViewBag.AceptacionObservacion = firmaCoordinacion != null ? firmaCoordinacion.Observaciones : "Aceptación documental firmada por coordinación.";
            ViewBag.AceptacionDocumentos = documentosRevision
                .Where(d => ObtenerDecisionRevisionDocumental(d, revisiones) == "ACEPTADO")
                .Select(ObtenerEtiquetaDocumento)
                .ToList();

            var pdf = new ViewAsPdf("~/Views/SolicitudAOCR/AceptacionDocumentalPdf.cshtml", solicitud)
            {
                FileName = "AceptacionDocumental_AOCR_" + id + ".pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(10, 10, 12, 12),
                CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
            };

            var pdfBytes = pdf.BuildFile(ControllerContext);
            var nombreArchivo = ConstruirNombrePdfAceptacionDocumental(solicitud, firmaCoordinacion != null ? firmaCoordinacion.FechaCambio : (DateTime?)null);

            if (!vistaPrevia && esPropietario && string.Equals(estadoActual, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase))
            {
                string mensajeCambio;
                if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.Finalizado, "Aceptación documental descargada por el RT.", out mensajeCambio))
                {
                    System.Diagnostics.Debug.WriteLine("[DescargarAceptacionDocumental] No se pudo marcar la solicitud como finalizada: " + mensajeCambio);
                }
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreArchivo);
            return File(pdfBytes, "application/pdf");
        }

        [Authorize]
        public ActionResult DescargarCondicionesLimitacionesModificacion(int id, bool vistaPrevia = false)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound();
            }

            if (!EsSolicitudModificacion(solicitud))
            {
                return new HttpStatusCodeResult(400, "La solicitud indicada no corresponde a una modificación AOCR.");
            }

            var usuarioActualId = ObtenerUsuarioActualId();
            var esPropietario = usuarioActualId > 0 && solicitud.CodigoUsuario == usuarioActualId;
            var esUsuarioInterno = EsAdmin()
                || (User != null && (
                    User.IsInRole("DIRDAC")
                    || User.IsInRole("Direccion")
                    || User.IsInRole("DirectorGeneral")
                    || User.IsInRole("JefaturaTecnica")
                    || User.IsInRole("Coordinador")
                    || User.IsInRole("CoordinadorInspecciones")));

            if (!esPropietario && !esUsuarioInterno)
            {
                return new HttpStatusCodeResult(403, "No tiene permisos para acceder al documento firmado.");
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!string.Equals(estadoActual, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoActual, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "El documento firmado de Condiciones y Limitaciones aún no está disponible para descarga.";
                return RedirectToAction("Detalle", new { id });
            }

            var firma = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(id, DocumentoTipoCondicionesLimitaciones);
            if (firma == null || string.IsNullOrWhiteSpace(firma.RutaDocumento))
            {
                return HttpNotFound("No existe un PDF firmado de Condiciones y Limitaciones para esta solicitud.");
            }

            var rutaFisica = ResolverRutaDocumentoAocrFirmado(firma.RutaDocumento);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                return HttpNotFound("No se encontró el archivo PDF firmado en almacenamiento.");
            }

            if (!vistaPrevia && esPropietario && string.Equals(estadoActual, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase))
            {
                string mensajeCambio;
                if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.Finalizado, "Descarga final de Condiciones y Limitaciones firmada por RT.", out mensajeCambio))
                {
                    System.Diagnostics.Debug.WriteLine("[DescargarCondicionesLimitacionesModificacion] No se pudo marcar la solicitud como finalizada: " + mensajeCambio);
                }
            }

            var nombreArchivo = ConstruirNombrePdfCondicionesLimitaciones(solicitud, firma.FechaFirma);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreArchivo);
            return File(rutaFisica, "application/pdf");
        }

        // ==========================================================================
        // GENERACIÓN AUTOMÁTICA DEL DOCUMENTO AOCR
        // Reemplaza la antigua "Subir Documento / Borrador AOCR" por generación
        // institucional a partir de los datos del trámite y del informe técnico
        // aprobado. Valida todas las reglas de negocio en backend.
        // ==========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Inspector,JefaturaTecnica,CoordinacionLegal,CoordinadorLegal,Direccion,DirectorGeneral,Administrador")]
        public ActionResult GenerarAOCR(int id)
        {
            try
            {
                var disponibilidad = _generacionAocrService.Evaluar(id);
                if (disponibilidad == null || disponibilidad.Solicitud == null)
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = "La solicitud no existe o no se pudo evaluar.";
                    return RedirectToAction("Detalle", new { id });
                }

                if (!disponibilidad.Habilitado)
                {
                    TempData["NotificacionTipo"] = "warning";
                    TempData["NotificacionMensaje"] = disponibilidad.Motivo ?? "La generación de la AOCR aún no está habilitada.";
                    return RedirectToAction("Detalle", new { id });
                }

                var solicitud = disponibilidad.Solicitud;
                string numeroAOCR = GeneracionAOCRService.GenerarNumeroAOCR(id, DateTime.Now);

                // Construir ViewModel institucional para el PDF
                var modelo = ConstruirCertificadoAocrViewModel(solicitud, numeroAOCR);

                // Generar el PDF con Rotativa (mismo pipeline que CertificadoController)
                byte[] pdfBytes;
                try
                {
                    var pdf = new ViewAsPdf("~/Views/Certificado/CertificadoAOCR.cshtml", modelo)
                    {
                        PageSize = Rotativa.Options.Size.A4,
                        PageOrientation = Rotativa.Options.Orientation.Portrait,
                        PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5),
                        CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
                    };
                    pdfBytes = pdf.BuildFile(ControllerContext);
                }
                catch (Exception exPdf)
                {
                    System.Diagnostics.Debug.WriteLine("[GenerarAOCR] Error al construir PDF: " + exPdf);
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = "No se pudo generar el PDF de la AOCR: " + exPdf.Message;
                    return RedirectToAction("Detalle", new { id });
                }

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = "La generación del PDF devolvió un resultado vacío.";
                    return RedirectToAction("Detalle", new { id });
                }

                // Guardar archivo físico
                string carpetaVirtual = "~/Uploads/AOCR";
                string carpetaFisica = Server.MapPath(carpetaVirtual);
                if (!Directory.Exists(carpetaFisica))
                {
                    Directory.CreateDirectory(carpetaFisica);
                }

                string nombreArchivo = ObtenerNombreArchivoDisponible(
                    carpetaFisica,
                    ConstruirNombrePdfCertificadoAocr(solicitud, modelo != null ? (DateTime?)modelo.FechaEmision : null));
                string rutaFisica = Path.Combine(carpetaFisica, nombreArchivo);
                System.IO.File.WriteAllBytes(rutaFisica, pdfBytes);

                // Persistir metadata + historial
                int usuarioId = ObtenerUsuarioActualId();
                string usuarioNombre = (User != null && User.Identity != null) ? User.Identity.Name : "sistema";

                string mensajePersistencia;
                var documento = _generacionAocrService.RegistrarDocumentoGenerado(
                    id,
                    rutaFisica,
                    nombreArchivo,
                    numeroAOCR,
                    usuarioId,
                    usuarioNombre,
                    out mensajePersistencia);

                if (documento == null)
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = mensajePersistencia ?? "La AOCR se generó pero no se pudo registrar.";
                    return RedirectToAction("Detalle", new { id });
                }

                TempData["NotificacionTipo"] = "success";
                TempData["NotificacionMensaje"] = "AOCR generada correctamente (" + numeroAOCR + "). Documento añadido al expediente.";
                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[GenerarAOCR] Error inesperado: " + ex);
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "Error inesperado al generar la AOCR: " + ex.Message;
                return RedirectToAction("Detalle", new { id });
            }
        }

        /// <summary>
        /// Descarga el archivo PDF de la AOCR generada para una solicitud.
        /// </summary>
        public ActionResult DescargarAOCRGenerada(int id, bool vistaPrevia = false)
        {
            var documento = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
            if (documento == null || string.IsNullOrWhiteSpace(documento.RutaArchivo))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No existe una AOCR generada para esta solicitud.";
                return RedirectToAction("Detalle", new { id });
            }

            string ruta = documento.RutaArchivo;
            if (!Path.IsPathRooted(ruta))
            {
                try { ruta = Server.MapPath(ruta); } catch { /* ignore */ }
            }

            if (!System.IO.File.Exists(ruta))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = "El archivo de la AOCR no se encuentra disponible en el servidor.";
                return RedirectToAction("Detalle", new { id });
            }

            var solicitud = _solicitudDAO.ObtenerPorId(id);
            string nombreDescarga = ConstruirNombrePdfCertificadoAocr(solicitud);

            var bytes = System.IO.File.ReadAllBytes(ruta);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreDescarga);
            return File(bytes, "application/pdf");
        }

        private string ConstruirNombrePdfAceptacionDocumental(SolicitudAOCR solicitud, DateTime? fecha = null)
        {
            return PdfFileNameHelper.CrearNombreAceptacionDocumental(
                ObtenerNumeroSolicitudPdf(solicitud),
                ObtenerSegmentoOperadorPdf(solicitud),
                fecha ?? ObtenerFechaDocumentoPdf(solicitud));
        }

        private string ConstruirNombrePdfCondicionesLimitaciones(SolicitudAOCR solicitud, DateTime? fecha = null)
        {
            return PdfFileNameHelper.CrearNombreCondicionesLimitaciones(
                ObtenerNumeroSolicitudPdf(solicitud),
                ObtenerSegmentoOperadorPdf(solicitud),
                fecha ?? ObtenerFechaDocumentoPdf(solicitud));
        }

        private string ConstruirNombrePdfCertificadoAocr(SolicitudAOCR solicitud, DateTime? fecha = null)
        {
            return PdfFileNameHelper.CrearNombreCertificadoAocr(
                ObtenerNumeroSolicitudPdf(solicitud),
                ObtenerSegmentoOperadorPdf(solicitud),
                fecha ?? ObtenerFechaDocumentoPdf(solicitud));
        }

        private string ObtenerNumeroSolicitudPdf(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? solicitud.NumeroSolicitud
                : solicitud.CodigoSolicitud.ToString();
        }

        private string ObtenerSegmentoOperadorPdf(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return string.Empty;
            }

            return PdfFileNameHelper.PrimerValorNoVacio(
                PdfFileNameHelper.CombinarSegmentos(solicitud.Ruc, solicitud.NombreOperador),
                PdfFileNameHelper.CombinarSegmentos(solicitud.Ruc, solicitud.NombreComercial),
                PdfFileNameHelper.CombinarSegmentos(solicitud.Ruc, solicitud.RazonSocial),
                solicitud.NombreOperador,
                solicitud.NombreComercial,
                solicitud.RazonSocial,
                solicitud.Ruc);
        }

        private DateTime? ObtenerFechaDocumentoPdf(SolicitudAOCR solicitud)
        {
            return solicitud != null
                ? (solicitud.UpdatedAt ?? solicitud.FechaSolicitud ?? solicitud.CreatedAt)
                : (DateTime?)null;
        }

        private static string ObtenerNombreArchivoDisponible(string carpetaFisica, string nombreArchivoDeseado)
        {
            var nombreArchivo = string.IsNullOrWhiteSpace(nombreArchivoDeseado)
                ? "Documento_AOCR.pdf"
                : nombreArchivoDeseado;
            var rutaFisica = Path.Combine(carpetaFisica, nombreArchivo);
            if (!System.IO.File.Exists(rutaFisica))
            {
                return nombreArchivo;
            }

            var baseName = Path.GetFileNameWithoutExtension(nombreArchivo);
            var extension = Path.GetExtension(nombreArchivo);
            return baseName + "_" + DateTime.Now.ToString("HHmmss") + extension;
        }

        /// <summary>
        /// Construye el ViewModel institucional para el certificado AOCR.
        /// Replicado desde CertificadoController.ConstruirViewModel para mantener
        /// consistencia de datos y firma institucional.
        /// </summary>
        private CapaModelo.Common.CertificadoAOCRViewModel ConstruirCertificadoAocrViewModel(SolicitudAOCR solicitud, string numeroAOCR)
        {
            string logoBase64 = null;
            string escudoBase64 = null;
            try
            {
                string logoPath = Server.MapPath("~/Content/assets/imganes/logo2.jpg");
                if (!System.IO.File.Exists(logoPath))
                {
                    logoPath = Server.MapPath("~/Content/assets/imganes/logodgac.jpg");
                }
                if (System.IO.File.Exists(logoPath))
                {
                    logoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(logoPath));
                }
                string escudoPath = Server.MapPath("~/Content/assets/imganes/escudo-ecuador.jpg");
                if (System.IO.File.Exists(escudoPath))
                {
                    escudoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(escudoPath));
                }
            }
            catch { /* opcional */ }

            return new CapaModelo.Common.CertificadoAOCRViewModel
            {
                NumeroAOCR = numeroAOCR,
                NumeroAOCBase = solicitud.NumeroSolicitud,
                FechaEmision = DateTime.Now,
                FechaVencimiento = null,
                FechaRenovacion = null,
                NumeroEnmienda = 1,

                NombreExplotador = solicitud.NombreOperador,
                EstadoExplotador = solicitud.Pais ?? "Ecuador",
                RazonSocial = !string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial : solicitud.NombreOperador,
                RUC = solicitud.Ruc,
                DireccionExplotador = solicitud.Direccion,
                TelefonoExplotador = solicitud.Telefono,
                CorreoExplotador = solicitud.Email,

                PuntoContactoEcuador = solicitud.RepresentanteLegal,
                DireccionContactoEcuador = solicitud.Direccion,
                TelefonoContactoEcuador = solicitud.Telefono,
                CorreoContactoEcuador = solicitud.Email,

                DireccionOperacional = solicitud.Direccion,
                TelefonoOperacional = solicitud.Telefono,
                CorreoOperacional = solicitud.Email,

                RepresentanteTecnico = solicitud.TecnicoResponsableNombre,
                CorreoRT = solicitud.CorreoRepresentanteTecnico,
                RepresentanteLegal = solicitud.RepresentanteLegal,

                TipoOperacion = solicitud.TipoOperacion,
                AlcanceOperacion = solicitud.DescripcionOperacion,

                NombreFirmante = !string.IsNullOrWhiteSpace(solicitud.Director) ? solicitud.Director : "DIRECTOR GENERAL DE AVIACION CIVIL",
                CargoFirmante = !string.IsNullOrWhiteSpace(solicitud.CargoDirector) ? solicitud.CargoDirector : "Director General de Aviacion Civil",
                TituloFirmante = "DIRECTOR GENERAL DE AVIACION CIVIL",

                Observaciones = solicitud.Observaciones,

                LogoBase64 = logoBase64,
                EscudoBase64 = escudoBase64,
                Solicitud = solicitud
            };
        }

        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
        public ActionResult RevisarLegalizacion()
        {
            var lista = _solicitudDAO.ObtenerPorEstados(EstadoSolicitud.AOCR_Validado);
            return View(lista);
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Legalizar(int id, string observacionLegal = "")
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null) return HttpNotFound();

                var aocrGenerada = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
                if (aocrGenerada == null)
                {
                    TempData["Error"] = "No se puede legalizar sin documento AOCR generado en el expediente.";
                    return RedirectToAction("RevisarLegalizacion");
                }

                string mensajeCambio;
                if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_Legalizado, observacionLegal ?? "Legalizado por Coordinación Legal", out mensajeCambio))
                {
                    TempData["Error"] = mensajeCambio;
                    return RedirectToAction("RevisarLegalizacion");
                }

                var solicitudActualizada = _solicitudDAO.ObtenerPorId(id);
                _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "AOCR_LEGALIZADO", observacionLegal);

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

            var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(id) ?? new List<Inspeccion>();
            var inspeccionVinculada = ObtenerUltimaInspeccionVinculada(inspecciones);
            if (inspeccionVinculada == null)
            {
                TempData["NotificacionMensaje"] = "No existe una inspección vinculada para este trámite. Registre o ubique la inspección desde el módulo correspondiente antes de continuar.";
                TempData["NotificacionTipo"] = "warning";
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionMensaje"] = "El acceso SolicitarInspeccion se mantiene solo por compatibilidad. Continúe desde la inspección vinculada para iniciar y gestionar esta fase.";
            TempData["NotificacionTipo"] = "info";
            return RedirectToAction("Detalle", "Inspeccion", new { id = inspeccionVinculada.CodigoInspeccion });
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarRequiereInspeccionModificacion(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            if (!EsSolicitudModificacion(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud indicada no corresponde a una modificación AOCR.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo puede derivar a inspección una modificación con documentación ya aceptada.";
                return RedirectToAction("Detalle", new { id });
            }

            var observacionFinal = !string.IsNullOrWhiteSpace(observacion)
                ? observacion.Trim()
                : "El inspector determinó que la modificación requiere derivación al módulo de inspección.";

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.RequiereInspeccion, observacionFinal, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "La modificación fue marcada como REQUIERE_INSPECCIÓN.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult GenerarCondicionesLimitacionesModificacion(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            if (!EsSolicitudModificacion(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud indicada no corresponde a una modificación AOCR.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo puede generar Condiciones y Limitaciones cuando la documentación de la modificación ya fue aceptada.";
                return RedirectToAction("Detalle", new { id });
            }

            var observacionFinal = !string.IsNullOrWhiteSpace(observacion)
                ? observacion.Trim()
                : "El inspector determinó que la modificación no requiere nueva inspección. Se habilita la generación de Condiciones y Limitaciones.";

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.GeneradoCondicionesLimitaciones, observacionFinal, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "La modificación quedó lista para revisión final de Condiciones y Limitaciones.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult RevisarCondicionesLimitacionesModificacion(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            if (!EsSolicitudModificacion(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud indicada no corresponde a una modificación AOCR.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.GeneradoCondicionesLimitaciones, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo puede abrir revisión final desde el estado GENERADO_CONDICIONES_LIMITACIONES.";
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.EnRevisionCoordinadorFinal,
                string.IsNullOrWhiteSpace(observacion) ? "Condiciones y Limitaciones enviadas a revisión final de coordinación." : observacion.Trim(),
                out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "La modificación pasó a EN_REVISION_COORDINADOR_FINAL.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult EnviarCondicionesLimitacionesDcav(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            if (!EsSolicitudModificacion(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud indicada no corresponde a una modificación AOCR.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.EnRevisionCoordinadorFinal, StringComparison.OrdinalIgnoreCase))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo puede enviar a DCAV una modificación en revisión final de coordinación.";
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.EnviadoDcav,
                string.IsNullOrWhiteSpace(observacion) ? "Condiciones y Limitaciones enviadas a DCAV/DGAC para firma." : observacion.Trim(),
                out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = "La modificación fue enviada a DCAV/DGAC para firma institucional.";
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
            var aocrGenerada = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
            if (aocrGenerada == null)
            {
                TempData["Error"] = "Debe generar primero el documento AOCR antes de enviarlo a revisión.";
                return RedirectToAction("Detalle", new { id });
            }

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
        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult EmitirAocr(int id, string observacion = "")
        {
            string mensajeInspeccion;
            if (!SolicitudTieneInspeccionSatisfactoria(id, out mensajeInspeccion))
            {
                TempData["Error"] = mensajeInspeccion;
                return RedirectToAction("Detalle", new { id });
            }

            var aocrGenerada = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
            if (aocrGenerada == null)
            {
                TempData["Error"] = "No se puede emitir AOCR sin documento AOCR generado y vigente.";
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, EstadoSolicitud.AOCR_EmitidoRecibido, observacion ?? "AOCR emitido/recibido", out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
            }
            else
            {
                var solicitudActualizada = _solicitudDAO.ObtenerPorId(id);
                _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "AOCR_EMITIDO_RECIBIDO", observacion);
                TempData["Exito"] = "AOCR emitido y marcado como recibido.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        private bool SolicitudTieneInspeccionSatisfactoria(int codigoSolicitud, out string mensaje)
        {
            mensaje = string.Empty;
            var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(codigoSolicitud) ?? new List<Inspeccion>();

            if (inspecciones.Count == 0)
            {
                mensaje = "No se puede avanzar porque la solicitud no tiene inspecciones registradas.";
                return false;
            }

            var inspeccionSatisfactoria = inspecciones
                .Where(EsInspeccionSatisfactoria)
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault();

            if (inspeccionSatisfactoria == null)
            {
                mensaje = "No se puede avanzar a AOCR final sin una inspección satisfactoria (estado APROBADA/CERRADA o resultado satisfactorio).";
                return false;
            }

            foreach (var inspeccion in inspecciones.Where(i => i != null && i.CodigoInspeccion > 0))
            {
                var hallazgos = _hallazgoDAO.ObtenerPorInspeccion(inspeccion.CodigoInspeccion) ?? new List<Hallazgo>();
                var tieneNcAbiertas = hallazgos.Any(h =>
                    h != null &&
                    !string.Equals((h.Estado ?? string.Empty).Trim(), "CERRADO", StringComparison.OrdinalIgnoreCase));

                if (tieneNcAbiertas)
                {
                    mensaje = "No se puede avanzar porque existen no conformidades abiertas en la inspección #" + inspeccion.CodigoInspeccion + ".";
                    return false;
                }
            }

            var informe = _inspeccionInformeDAO.ObtenerUltimoPorInspeccion(inspeccionSatisfactoria.CodigoInspeccion);
            if (informe == null)
            {
                mensaje = "No se puede avanzar porque la inspección satisfactoria no tiene informe técnico registrado.";
                return false;
            }

            if (!informe.Finalizado)
            {
                mensaje = "No se puede avanzar porque el informe técnico aún no está finalizado.";
                return false;
            }

            if (!informe.FirmadoInspector)
            {
                mensaje = "No se puede avanzar porque el informe técnico aún no cuenta con firma del inspector.";
                return false;
            }

            if (!InformeCompletaFaseTecnicaAocr(informe))
            {
                mensaje = "No se puede avanzar porque el informe tecnico todavia no completa la firma final del flujo tecnico AOCR.";
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

        private static bool InformeCompletaFaseTecnicaAocr(InspeccionInformeTecnico informe)
        {
            if (informe == null)
            {
                return false;
            }

            if (!informe.Finalizado || !informe.FirmadoInspector)
            {
                return false;
            }

            if (informe.FirmadoDirdac)
            {
                return true;
            }

            if (informe.FechaFirma2.HasValue && !string.IsNullOrWhiteSpace(informe.UsuarioFirma2))
            {
                return true;
            }

            var estadoInforme = (informe.EstadoInforme ?? string.Empty).Trim().ToUpperInvariant();
            return estadoInforme == "APROBADO_DIRECCION"
                || estadoInforme == "FIRMADO_FINAL";
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
                return User != null && (
                    User.IsInRole("Inspector")
                    || User.IsInRole("Coordinador")
                    || User.IsInRole("CoordinadorInspecciones")
                    || User.IsInRole("JefaturaTecnica")
                    || User.IsInRole("DIRDAC")
                    || User.IsInRole("Direccion")
                    || User.IsInRole("CoordinacionLegal")
                    || User.IsInRole("CoordinadorLegal"));
            }

            if (destino == EstadoSolicitud.AceptacionDocumental)
            {
                return User != null && (
                    User.IsInRole("Inspector")
                    || User.IsInRole("Coordinador")
                    || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.RequiereInspeccion || destino == EstadoSolicitud.GeneradoCondicionesLimitaciones)
            {
                return User != null && User.IsInRole("Inspector");
            }

            if (destino == EstadoSolicitud.EnRevisionCoordinadorFinal || destino == EstadoSolicitud.EnviadoDcav)
            {
                return User != null && (User.IsInRole("Coordinador") || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.FirmadoDcav)
            {
                return User != null && (
                    User.IsInRole("DIRDAC")
                    || User.IsInRole("Direccion")
                    || User.IsInRole("JefaturaTecnica")
                    || User.IsInRole("DirectorGeneral"));
            }

            if (destino == EstadoSolicitud.PendienteAsignacionRT)
            {
                return User != null && (User.IsInRole("Coordinador") || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.FirmadoCoordinador)
            {
                return User != null && (User.IsInRole("Coordinador") || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.Finalizado)
            {
                return User != null && User.Identity != null && User.Identity.IsAuthenticated;
            }

            if (destino == EstadoSolicitud.EnInspeccion || destino == EstadoSolicitud.AOCR_EnElaboracion)
            {
                return User != null && (User.IsInRole("Inspector") || User.IsInRole("Coordinador") || User.IsInRole("CoordinadorInspecciones"));
            }

            if (destino == EstadoSolicitud.AOCR_EnRevision || destino == EstadoSolicitud.AOCR_Validado)
            {
                return User != null && (
                    User.IsInRole("DIRDAC")
                    || User.IsInRole("Direccion")
                    || User.IsInRole("JefaturaTecnica")
                    || User.IsInRole("DirectorGeneral"));
            }

            if (destino == EstadoSolicitud.AOCR_Legalizado || destino == EstadoSolicitud.AOCR_EmitidoRecibido)
            {
                return User != null && (User.IsInRole("CoordinacionLegal") || User.IsInRole("CoordinadorLegal") || User.IsInRole("DirectorGeneral"));
            }

            return false;
        }

        private static bool SolicitudEstaEnEtapaRevisionDocumental(string estadoNormalizado)
        {
            return string.Equals(estadoNormalizado, EstadoSolicitud.EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.DocumentacionPendiente, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.DocumentacionCompleta, StringComparison.OrdinalIgnoreCase);
        }

        private string ObtenerEstadoDocumentalVisible(SolicitudAOCR solicitud, IDictionary<int, Tuple<string, string>> revisiones)
        {
            var estadoNormalizado = EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : null);
            if (string.Equals(estadoNormalizado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                return "FINALIZADO";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.FirmadoDcav, StringComparison.OrdinalIgnoreCase))
            {
                return "FIRMADO_DCAV";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.EnviadoDcav, StringComparison.OrdinalIgnoreCase))
            {
                return "ENVIADO_DCAV";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.EnRevisionCoordinadorFinal, StringComparison.OrdinalIgnoreCase))
            {
                return "EN_REVISION_COORDINADOR_FINAL";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.GeneradoCondicionesLimitaciones, StringComparison.OrdinalIgnoreCase))
            {
                return "GENERADO_CONDICIONES_LIMITACIONES";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.RequiereInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                return "REQUIERE_INSPECCION";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase))
            {
                return "AUTORIZACION_FIRMADA";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.DocumentacionCompleta, StringComparison.OrdinalIgnoreCase))
            {
                return "ACEPTADO_INSPECTOR";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                return "OBSERVADO";
            }

            var tieneInspector = SolicitudTieneInspectorAsignado(solicitud);
            if (string.Equals(estadoNormalizado, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                return revisiones != null && revisiones.Count > 0
                    ? "EN_REVISION_INSPECTOR"
                    : "INSPECTOR_ASIGNADO";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase))
            {
                return tieneInspector ? "EN_REVISION_INSPECTOR" : "EN_REVISION_COORDINADOR";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.EnRevision, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.DocumentacionPendiente, StringComparison.OrdinalIgnoreCase))
            {
                return tieneInspector ? "INSPECTOR_ASIGNADO" : "EN_REVISION_COORDINADOR";
            }

            if (string.Equals(estadoNormalizado, EstadoSolicitud.Pendiente, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.SolicitudCreada, StringComparison.OrdinalIgnoreCase))
            {
                return "BORRADOR";
            }

            return (solicitud != null ? solicitud.Estado : estadoNormalizado ?? string.Empty) ?? string.Empty;
        }

        private string ResolverRutaDocumentoAocrFirmado(string rutaDocumento)
        {
            if (string.IsNullOrWhiteSpace(rutaDocumento))
            {
                return null;
            }

            var ruta = rutaDocumento.Trim();
            if (Path.IsPathRooted(ruta))
            {
                return ruta;
            }

            if (ruta.StartsWith("~", StringComparison.OrdinalIgnoreCase))
            {
                return Server.MapPath(ruta);
            }

            return Server.MapPath("~" + (ruta.StartsWith("/") ? ruta : "/" + ruta));
        }

        private static bool SolicitudTieneInspectorAsignado(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return false;
            }

            return (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre);
        }

        private static Inspeccion ObtenerUltimaInspeccionVinculada(IEnumerable<Inspeccion> inspecciones)
        {
            if (inspecciones == null)
            {
                return null;
            }

            return inspecciones
                .Where(i => i != null && i.CodigoInspeccion > 0)
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault();
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
                return NormalizarDecisionRevisionDocumental(revisionActual.Item1);
            }

            var estadoDocumento = (documento.Estado ?? string.Empty).Trim().ToUpperInvariant();
            if (estadoDocumento == "APROBADO")
            {
                return "ACEPTADO";
            }

            if (estadoDocumento == "OBSERVADO")
            {
                return "OBSERVADO";
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
            if (!DecisionRevisionRequiereObservacion(decision))
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
            IDictionary<int, Tuple<string, string>> revisiones,
            ISet<int> documentosYaNotificadosIndividualmente)
        {
            if (solicitud == null)
            {
                return;
            }

            var destinatarios = new[]
                {
                    solicitud.CorreoRepresentanteTecnico,
                    solicitud.Email
                }
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (destinatarios.Count == 0)
            {
                return;
            }

            var documentosNotificados = documentosYaNotificadosIndividualmente ?? new HashSet<int>();

            var itemsDevueltos = (documentos ?? Enumerable.Empty<Documento>())
                .Select(d => new
                {
                    CodigoDocumento = d.CodigoDocumento,
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

            var itemsPendientesResumen = itemsDevueltos
                .Where(x => x.CodigoDocumento <= 0 || !documentosNotificados.Contains(x.CodigoDocumento))
                .ToList();

            string bloqueDetalle;
            if (itemsPendientesResumen.Count > 0)
            {
                var detalleHtml = string.Join(string.Empty, itemsPendientesResumen.Select(x =>
                    "<li><strong>" + HttpUtility.HtmlEncode(x.Documento) + "</strong>: " +
                    HttpUtility.HtmlEncode(RevisionDocumentalDisplayHelper.GetVisibleStateLabel(x.Decision)) +
                    " - " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(x.Observacion) ? "Sin observación registrada." : x.Observacion) +
                    " <em>(" + HttpUtility.HtmlEncode(RevisionDocumentalDisplayHelper.GetVisibleStateLabel(x.Decision)) + ")</em></li>"));

                bloqueDetalle = "<strong>Documentos rechazados/devueltos pendientes de resumen:</strong><ul>" + detalleHtml + "</ul>";

                if (itemsPendientesResumen.Count < itemsDevueltos.Count)
                {
                    bloqueDetalle += "Los demas documentos devueltos ya fueron notificados individualmente durante la revision.<br><br>";
                }
            }
            else
            {
                bloqueDetalle = "Los documentos devueltos/observados ya fueron notificados individualmente durante la revision. " +
                               "Este correo resume el cierre formal de la revision documental.<br><br>";
            }

            var numeroSolicitud = FirstNonEmpty(solicitud.NumeroSolicitud, "#" + solicitud.CodigoSolicitud);
            var operador = FirstNonEmpty(solicitud.NombreComercial, solicitud.NombreOperador, solicitud.RazonSocial, "Operador");
            var inspector = FirstNonEmpty(solicitud.TecnicoResponsableNombre, "Inspector asignado");
            var asunto = "AOCR - Resumen final de revision documental con observaciones";
            var cuerpo = "Estimado/a usuario AOCR:<br><br>" +
                         "Se informa que la revisión documental de su Solicitud AOCR fue finalizada con documentos devueltos/observados. " +
                         "A continuación se detalla por qué fue rechazada la documentación y cuál documento requiere corrección.<br><br>" +
                         "<strong>Número de solicitud AOCR:</strong> " + HttpUtility.HtmlEncode(numeroSolicitud) + "<br>" +
                         "<strong>Solicitante / EAE:</strong> " + HttpUtility.HtmlEncode(operador) + "<br>" +
                         "<strong>Inspector:</strong> " + HttpUtility.HtmlEncode(inspector) + "<br>" +
                         "<strong>Fecha de revisión:</strong> " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "<br><br>" +
                         bloqueDetalle +
                         "Por favor ingrese al sistema, revise las observaciones detalladas y cargue la documentación corregida para continuar con el proceso.<br><br>" +
                         "Saludos.";

            foreach (var destinatario in destinatarios)
            {
                EmailHelper.EnviarEmail(destinatario, asunto, cuerpo);
            }
        }

        private static string NormalizarDecisionRevisionDocumental(string decision)
        {
            var normalized = (decision ?? string.Empty).Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "ACEPTADO":
                case "APROBADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                case "DEVUELTO":
                    return "DEVUELTO";
                case "OBSERVADO":
                case "MODIFICACION_SOLICITADA":
                case "MODIFICACION SOLICITADA":
                case "SOLICITAR_MODIFICACION":
                    return "OBSERVADO";
                default:
                    return normalized;
            }
        }

        private static bool DecisionRevisionRequiereObservacion(string decision)
        {
            var normalizada = NormalizarDecisionRevisionDocumental(decision);
            return normalizada == "DEVUELTO" || normalizada == "OBSERVADO";
        }

        private static bool SolicitudEstaCerradaOperativamente(SolicitudAOCR solicitud)
        {
            var estado = EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : null);
            return string.Equals(estado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase);
        }

        private bool TryRedirigirSiProcesoCerrado(SolicitudAOCR solicitud, int id, out ActionResult result)
        {
            if (!SolicitudEstaCerradaOperativamente(solicitud))
            {
                result = null;
                return false;
            }

            TempData["NotificacionTipo"] = "warning";
            TempData["NotificacionMensaje"] = "El proceso AOCR ya se encuentra cerrado. Solo puede consultarlo históricamente o iniciar una Nueva Orden de Recaudación.";
            result = RedirectToAction("Detalle", new { id });
            return true;
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
                    catalogo = _solicitudAocrInfraBL.ObtenerEmpresas()
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

        public class GuardarProgresoPayload
        {
            [JsonProperty("seccion")]
            public string Seccion { get; set; }

            [JsonProperty("solicitud")]
            public SolicitudAOCR Solicitud { get; set; }
        }

        private JsonResult JsonEnvelope(bool ok, string code, string message, object data = null, object legacy = null)
        {
            var safeCode = string.IsNullOrWhiteSpace(code) ? (ok ? "OK" : "ERROR") : code.Trim();
            var safeMessage = string.IsNullOrWhiteSpace(message) ? (ok ? "Operación exitosa." : "Error no controlado.") : message.Trim();

            if (legacy != null)
            {
                return Json(new
                {
                    ok = ok,
                    success = ok,
                    code = safeCode,
                    message = safeMessage,
                    mensaje = safeMessage,
                    data = data,
                    legacy = legacy
                });
            }

            return Json(new
            {
                ok = ok,
                success = ok,
                code = safeCode,
                message = safeMessage,
                mensaje = safeMessage,
                data = data
            });
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
            return RoleGroupingHelper.IsAdministrador((Session["Rol"] ?? "").ToString());
        }
    }
}
