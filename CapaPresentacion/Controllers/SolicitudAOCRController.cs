using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
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
using CapaModelo.Common;
using CapaPresentacion.Models.ViewModels;
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
        private readonly SolicitudAocrService _solicitudAocrService = new SolicitudAocrService();
        private readonly GeneracionAOCRService _generacionAocrService = new GeneracionAOCRService();
        private readonly AocrBandejaDAO _aocrBandejaDao = new AocrBandejaDAO();
        private readonly RevisionDocumentalService _revisionDocumentalService = new RevisionDocumentalService();
        private readonly DocumentoSubsanacionService _documentoSubsanacionService = new DocumentoSubsanacionService();
        private readonly AocrFinalWorkflowService _aocrFinalWorkflowService = new AocrFinalWorkflowService();
        private readonly AocrModificationWorkflowService _aocrModificationWorkflowService = new AocrModificationWorkflowService();
        private readonly IAocrAuthorizationService _aocrAuthorizationService = new AocrAuthorizationService();
        private static readonly IAocrEstadoService AocrEstadoService = new AocrEstadoService();
        private readonly InspectorIdentityService _inspectorIdentityService = new InspectorIdentityService();
        private readonly CapaDatos.Services.ILoggingService _logger = CapaDatos.Services.LoggingServiceFactory.Create();

        private readonly AeronaveSolicitudDAO _aeronaveSolDAO = new AeronaveSolicitudDAO();
        private readonly PagoDAO _pagoDAO = new PagoDAO();
        private readonly OrdenRecaudacionDAO _ordenRecaudacionDAO = new OrdenRecaudacionDAO();
        private readonly InspeccionInformeDAO _inspeccionInformeDAO = new InspeccionInformeDAO();
        private readonly HallazgoDAO _hallazgoDAO = new HallazgoDAO();
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDAO = new UsuarioInternoRTDAO();
        private readonly AocrCompaniaContextService _companiaContextService = new AocrCompaniaContextService();
        private readonly AocrProcesoActivoService _procesoActivoService = new AocrProcesoActivoService();

        private static readonly HashSet<string> ExtensionesPermitidasDocumentos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx"
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
        private const string DocumentoTipoReconocimiento = "RECONOCIMIENTO";
        private const string CodigoConceptoInspeccionExt = "INSPECCION_EXT";
        private const string TipoSolicitudInspeccionFirmada = "SOLICITUD_INSPECCIONES_FIRMADA";
        private const string EtiquetaSolicitudInspeccionFirmada = "Solicitud de inspecciones firmada";
        private const string RolesRevisionDocumentalOperativa = "Inspector,InspectorTecnico,Tecnico,EvaluadorTecnico,Coordinador,CoordinadorInspecciones,Coordinacion,Administrador";

        private bool UsuarioActualEsRt()
        {
            var rolActual = RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"] as string ?? string.Empty);
            var rolesRaw = RoleGroupingHelper.ExtractRoles(Session["RolesRaw"] ?? Session["Roles"], Session["Rol"] as string);

            return RoleGroupingHelper.IsSolicitante(rolActual)
                && RoleGroupingHelper.HasAnyRawRole(rolesRaw, "RepresentanteTecnico", "Representante Técnico", "RepresentanteLegal", "RT");
        }

        private bool TryObtenerBloqueoModuloSolicitudRt(out string mensaje)
        {
            mensaje = string.Empty;

            int codigoUsuario;
            if (!TryObtenerUsuarioActualId(out codigoUsuario) || codigoUsuario <= 0)
            {
                return false;
            }

            return TryObtenerBloqueoModuloSolicitudRt(codigoUsuario, out mensaje);
        }

        private bool TryObtenerBloqueoModuloSolicitudRt(int codigoUsuario, out string mensaje)
        {
            mensaje = string.Empty;
            if (codigoUsuario <= 0 || EsAdmin() || !UsuarioActualEsRt())
            {
                return false;
            }

            if (BuscarSolicitudRtHabilitadaReutilizable(codigoUsuario, ObtenerCompaniaActivaCodigo(), null) != null)
            {
                return false;
            }

            if (_ordenRecaudacionDAO.TieneOrdenHabilitanteAOCR(codigoUsuario))
            {
                return false;
            }

            if (_ordenRecaudacionDAO.TieneOrdenActivaEnProceso(codigoUsuario)
                || _ordenRecaudacionDAO.TieneOrdenPendienteComprobante(codigoUsuario)
                || _ordenRecaudacionDAO.ExisteORGeneradaOPagada(codigoUsuario))
            {
                mensaje = "El módulo de Solicitud AOCR se habilitará cuando Financiero apruebe el pago correspondiente.";
                return true;
            }

            mensaje = "Debe generar la Orden de Recaudación para continuar con el proceso AOCR.";
            return true;
        }

        public ActionResult Index(int? tipoSolicitud = null, bool abrirModal = false)
        {
            string mensajeBloqueo;
            if (TryObtenerBloqueoModuloSolicitudRt(out mensajeBloqueo))
            {
                TempData["Warning"] = mensajeBloqueo;
                return RedirectToAction("Index", "OrdenRecaudacion");
            }

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

                string mensajeBloqueo;
                if (TryObtenerBloqueoModuloSolicitudRt(codigoUsuario, out mensajeBloqueo))
                {
                    return Json(new { success = true, data = new List<object>(), message = mensajeBloqueo }, JsonRequestBehavior.AllowGet);
                }

                var esAdministrador = EsAdmin();
                var solicitudes = _solicitudDAO.ObtenerPorUsuario(esAdministrador ? (int?)null : codigoUsuario);
                var companiaActiva = ObtenerCompaniaActivaCodigo();
                if (!esAdministrador && !string.IsNullOrWhiteSpace(companiaActiva))
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
        [ValidateAntiForgeryTokenFromHeader]
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "GuardarProgresoRT", RequireCompanySelection = true, CodigoSolicitudParameter = "codigoSolicitud")]
        public JsonResult GuardarFlota(GuardarFlotaRequest request)
        {
            try
            {
                if (request == null || request.CodigoSolicitud <= 0)
                {
                    return JsonGuardado(false, "Solicitud inválida para guardar flota.", null);
                }

                var usuarioId = ObtenerUsuarioActualId();
                if (usuarioId <= 0)
                {
                    return JsonGuardado(false, "Sesión expirada.", null, Url.Action("Login", "Account"));
                }

                if (string.IsNullOrWhiteSpace(ObtenerCompaniaActivaCodigo()))
                {
                    CompaniaActivaRecoveryHelper.TryRestoreFromSolicitud(Session, request.CodigoSolicitud, usuarioId, EsAdmin());
                }

                var solicitud = _solicitudDAO.ObtenerPorId(request.CodigoSolicitud);
                if (solicitud == null)
                {
                    return JsonGuardado(false, "La solicitud no existe.", null);
                }

                if (!EsAdmin() && solicitud.CodigoUsuario != usuarioId)
                {
                    return JsonGuardado(false, "No tiene permisos para guardar la flota de esta solicitud.", null);
                }

                var companiaActiva = ObtenerCompaniaActivaCodigo();
                if (string.IsNullOrWhiteSpace(companiaActiva))
                {
                    return JsonGuardado(
                        false,
                        "Debe seleccionar una compañía activa antes de continuar.",
                        null,
                        Url.Action("SeleccionarCompania", "Account", new { returnUrl = Request?.RawUrl }),
                        requiresCompanySelection: true);
                }

                if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(solicitud, companiaActiva))
                {
                    CompaniaActivaRecoveryHelper.TryRestoreFromSolicitud(Session, request.CodigoSolicitud, usuarioId, EsAdmin(), forzarReemplazo: true);
                    companiaActiva = ObtenerCompaniaActivaCodigo();
                    if (!SolicitudCoincideConCompaniaActiva(solicitud, companiaActiva))
                    {
                        return JsonGuardado(false, "La solicitud no corresponde a la compañía activa seleccionada.", null);
                    }
                }

                if (!EsAdmin() && !SolicitudEsEditableFormularioEmision(solicitud))
                {
                    return JsonRechazoEdicionFormularioEmision(solicitud);
                }

                var aeronaves = (request.Aeronaves ?? new List<AeronaveSolicitud>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Matricula))
                    .ToList();

                if (!aeronaves.Any())
                {
                    return JsonGuardado(false, "Debe ingresar al menos una aeronave válida.", null);
                }

                var usuarioCorreo = Session["Correo"]?.ToString() ?? "sistema";
                var insertadas = _aeronaveSolDAO.ReemplazarPorSolicitud(request.CodigoSolicitud, aeronaves, usuarioCorreo);

                // Verificación posterior al guardado: releer desde base con el mismo SolicitudId.
                var persistidas = _aeronaveSolDAO.ObtenerPorSolicitud(request.CodigoSolicitud) ?? new List<AeronaveSolicitud>();

                System.Diagnostics.Trace.TraceInformation(
                    "[SOLICITUD_AOCR][GUARDAR_FLOTA] SolicitudId=" + request.CodigoSolicitud +
                    "; UsuarioId=" + usuarioId +
                    "; Compania=" + (companiaActiva ?? string.Empty) +
                    "; Enviadas=" + aeronaves.Count +
                    "; FilasAfectadas=" + insertadas +
                    "; PersistidasEnBase=" + persistidas.Count +
                    "; Resultado=" + (persistidas.Count > 0 ? "OK" : "SIN_FILAS"));

                if (insertadas <= 0 || persistidas.Count <= 0)
                {
                    return JsonGuardado(
                        false,
                        "No se pudo guardar la información de la flota. La solicitud activa no fue encontrada o no se registraron aeronaves.",
                        null);
                }

                return JsonGuardado(
                    true,
                    "Flota guardada correctamente.",
                    new
                    {
                        solicitudId = request.CodigoSolicitud,
                        total = persistidas.Count,
                        aeronaves = persistidas
                    },
                    id: request.CodigoSolicitud);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[SOLICITUD_AOCR][GUARDAR_FLOTA] SolicitudId=" + (request != null ? request.CodigoSolicitud : 0) +
                    "; Resultado=ERROR; Detalle=" + ex);
                return JsonGuardado(false, "No se pudo guardar la flota por un error de base de datos. Revise el log técnico.", null);
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

        private static bool EstadoPermiteDescargaAceptacionDocumental(string estadoNormalizado)
        {
            return string.Equals(estadoNormalizado, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsTransicionFirmaAceptacionDocumentalCoordinacion(string estadoNuevo)
        {
            var normalizado = EstadoSolicitud.Normalizar(estadoNuevo ?? string.Empty);
            return string.Equals(normalizado, EstadoSolicitud.FirmadoCoordinador, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizado, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SolicitudModificacionTieneNuevoAeropuertoDeclarado(SolicitudAOCR solicitud)
        {
            return AocrModificationWorkflowService.TieneNuevoAeropuertoDeclarado(solicitud);
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

            var nombreAs400 = ResolverNombreInspectorVisible(solicitud.CodigoTecnico, solicitud.TecnicoResponsableNombre);
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

            return "Sin Asignar";
        }

        private string ResolverNombreInspectorVisible(int? inspectorId, string nombreActual)
        {
            var nombreLimpio = (nombreActual ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nombreLimpio))
            {
                return nombreLimpio;
            }

            if (!inspectorId.HasValue || inspectorId.Value <= 0)
            {
                return string.Empty;
            }

            try
            {
                var registroInterno = _usuarioInternoRtDAO.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(inspectorId.Value);
                var nombreInterno = registroInterno != null ? (registroInterno.NombreVisual ?? string.Empty).Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(nombreInterno))
                {
                    return nombreInterno;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error resolviendo inspector desde usuario interno RT: " + ex.Message);
            }

            try
            {
                var nombrePrincipal = (UsuarioDAO.ObtenerNombreCompletoPrincipal(inspectorId.Value) ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(nombrePrincipal))
                {
                    return nombrePrincipal;
                }

                var usuario = UsuarioDAO.ObtenerPorId(inspectorId.Value);
                var nombreUsuario = string.Join(" ", new[]
                {
                    usuario != null ? (usuario.NombreCompleto ?? string.Empty).Trim() : string.Empty,
                    usuario != null ? (usuario.ApellidoUsuario ?? string.Empty).Trim() : string.Empty
                }.Where(segmento => !string.IsNullOrWhiteSpace(segmento))).Trim();

                return nombreUsuario;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error resolviendo inspector desde usuario: " + ex.Message);
                return string.Empty;
            }
        }

        private void EnriquecerNombresInspectoresDetalle(SolicitudAOCR solicitud, IList<Inspeccion> inspeccionesSolicitud)
        {
            if (solicitud != null)
            {
                solicitud.TecnicoResponsableNombre = ResolverNombreInspectorVisible(solicitud.CodigoTecnico, solicitud.TecnicoResponsableNombre);
            }

            if (inspeccionesSolicitud == null)
            {
                return;
            }

            foreach (var inspeccion in inspeccionesSolicitud.Where(i => i != null))
            {
                inspeccion.InspectorPrincipalNombre = ResolverNombreInspectorVisible(inspeccion.CodigoInspector, inspeccion.InspectorPrincipalNombre);
            }

            if (solicitud != null && string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                var inspeccionVinculada = ObtenerUltimaInspeccionVinculada(inspeccionesSolicitud);
                if (inspeccionVinculada != null && !string.IsNullOrWhiteSpace(inspeccionVinculada.InspectorPrincipalNombre))
                {
                    solicitud.TecnicoResponsableNombre = inspeccionVinculada.InspectorPrincipalNombre.Trim();
                }
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
            if (norm == EstadoSolicitud.PendienteAsignacionRT)
                return "Pendiente asignación inspector";
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
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "AbrirFormularioRT", RequireCompanySelection = true, CodigoSolicitudParameter = "oid")]
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
                    return InstitutionalAccessViewHelper.AccesoDenegado(
                        this,
                        "Sesión expirada",
                        "Por favor, inicie sesión nuevamente para continuar con el formulario de emisión AOCR.",
                        mostrarSeleccionCompania: false);
                }

                var companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                var companiaActivaNombre = ObtenerCompaniaActivaNombre();
                var scope = RtCompaniaScope.FromSession(Session, usuarioId);
                scope.PublicarEnViewBag(this);

                if (oid.HasValue && oid.Value > 0 && string.IsNullOrWhiteSpace(companiaActivaCodigo))
                {
                    CompaniaActivaRecoveryHelper.TryRestoreFromSolicitud(Session, oid.Value, usuarioId, EsAdmin());
                    companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                    companiaActivaNombre = ObtenerCompaniaActivaNombre();
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
                    var solicitudVinculadaOrden = ObtenerSolicitudVinculadaOrdenUsuario(usuarioId, tipoSolicitud);
                    if (solicitudVinculadaOrden != null
                        && !SolicitudEsEditableFormularioEmision(solicitudVinculadaOrden)
                        && SolicitudCoincideConCompaniaActiva(solicitudVinculadaOrden, companiaActivaCodigo))
                    {
                        var editableAlternativa = BuscarSolicitudRtHabilitadaReutilizable(usuarioId, companiaActivaCodigo, tipoSolicitud)
                            ?? BuscarSolicitudActivaReutilizable(usuarioId, companiaActivaCodigo, tipoSolicitud);
                        if (editableAlternativa != null)
                        {
                            oid = editableAlternativa.CodigoSolicitud;
                            System.Diagnostics.Trace.TraceInformation(
                                "[SOLICITUD_AOCR] FormularioEmisionAOCR abre solicitud editable alternativa=" + editableAlternativa.CodigoSolicitud +
                                " en lugar de solicitud no editable vinculada=" + solicitudVinculadaOrden.CodigoSolicitud +
                                "; usuario=" + usuarioId);
                        }
                        else
                        {
                            return RedirigirSeguimientoSolicitudNoEditable(
                                solicitudVinculadaOrden,
                                usuarioId,
                                "FormularioEmisionAOCR.OrdenVinculada");
                        }
                    }

                    if (!oid.HasValue || oid.Value <= 0)
                    {
                        var oidDesdeOrden = ResolverOidSolicitudDesdeOrdenUsuario(usuarioId, tipoSolicitud);
                        if (oidDesdeOrden.HasValue && oidDesdeOrden.Value > 0)
                        {
                            oid = oidDesdeOrden;
                            System.Diagnostics.Trace.TraceInformation(
                                "[SOLICITUD_AOCR] Reutilizando solicitud editable vinculada a orden: solicitud=" + oidDesdeOrden.Value +
                                " para usuario=" + usuarioId +
                                "; compania=" + (companiaActivaCodigo ?? string.Empty));
                        }
                        else
                        {
                            var solicitudActiva = BuscarSolicitudRtHabilitadaReutilizable(usuarioId, companiaActivaCodigo, tipoSolicitud)
                                ?? BuscarSolicitudActivaReutilizable(usuarioId, companiaActivaCodigo, tipoSolicitud);
                            if (solicitudActiva != null)
                            {
                                oid = solicitudActiva.CodigoSolicitud;
                                System.Diagnostics.Trace.TraceInformation(
                                    "[SOLICITUD_AOCR] Reutilizando solicitud activa existente " + solicitudActiva.CodigoSolicitud +
                                    " para usuario=" + usuarioId +
                                    "; compania=" + (companiaActivaCodigo ?? string.Empty));
                            }
                        }
                    }
                }

                // 2) Si es edición
                if (oid.HasValue && oid.Value > 0)
                {
                    vm.Solicitud = _solicitudBL.ObtenerDetalle(oid.Value);
                    if (vm.Solicitud == null)
                        return InstitutionalAccessViewHelper.MensajeInstitucional(
                            this,
                            "Solicitud no encontrada",
                            "La solicitud indicada no existe o fue eliminada del sistema.",
                            tituloEncabezado: "Recurso no disponible",
                            estilo: "info",
                            statusCode: 404);

                    // Seguridad: si no es admin, solo su solicitud
                    if (!EsAdmin() && vm.Solicitud.CodigoUsuario != usuarioId)
                        return InstitutionalAccessViewHelper.AccesoDenegado(
                            this,
                            "No tiene permisos para acceder a esta solicitud",
                            "Solo el representante técnico titular o un administrador institucional puede abrir este trámite.");

                    if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(vm.Solicitud, companiaActivaCodigo))
                    {
                        CompaniaActivaRecoveryHelper.TryRestoreFromSolicitud(Session, oid.Value, usuarioId, EsAdmin(), forzarReemplazo: true);
                        companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                        companiaActivaNombre = ObtenerCompaniaActivaNombre();
                    }

                    if (!EsAdmin() && !SolicitudCoincideConCompaniaActiva(vm.Solicitud, companiaActivaCodigo))
                        return InstitutionalAccessViewHelper.AccesoDenegado(
                            this,
                            _companiaContextService.ObtenerMensajeAccesoDenegadoCompania(),
                            "La solicitud pertenece a otra compañía distinta a la activa en su sesión. Seleccione la compañía correcta o verifique su asignación institucional.",
                            panelHint: "Si administra varias compañías, use «Seleccionar compañía» y elija la que corresponde al trámite N.º " + oid.Value + ".");

                    if (!EsAdmin() && !SolicitudEsEditableFormularioEmision(vm.Solicitud))
                    {
                        return RedirigirSeguimientoSolicitudNoEditable(vm.Solicitud, usuarioId, "FormularioEmisionAOCR");
                    }

                    // Guard: bloquear edición si el pago aún está pendiente de aprobación por Financiero
                    if (!EsAdmin() && !User.IsInRole("Financiero") && !User.IsInRole("CoordinadorFinanciero"))
                    {
                        string mensajeBloqueo;
                        if (!_solicitudAocrService.PuedeRtEditarSolicitud(vm.Solicitud.CodigoSolicitud, usuarioId, out mensajeBloqueo))
                        {
                            return InstitutionalAccessViewHelper.MensajeInstitucional(
                                this,
                                "Solicitud bloqueada",
                                mensajeBloqueo,
                                tituloEncabezado: "Trámite en espera",
                                estilo: "warning");
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

                    NormalizarTextosSolicitudFormulario(vm.Solicitud);

                    ConfigurarModoSubsanacionObservada(vm, oid.Value);
                }
                else
                {
                    if (!EsAdmin())
                    {
                        string mensajeBloqueoNueva;
                        if (!_procesoActivoService.PuedeCrearNuevaSolicitud(
                            usuarioId,
                            companiaActivaCodigo,
                            companiaActivaNombre,
                            out mensajeBloqueoNueva))
                        {
                            var activa = scope.ProcesoActivo != null ? scope.ProcesoActivo.SolicitudActiva : null;
                            var urlContinuar = activa != null
                                ? Url.Action("Detalle", "SolicitudAOCR", new { id = activa.CodigoSolicitud })
                                : null;
                            return InstitutionalAccessViewHelper.MensajeInstitucional(
                                this,
                                "No puede iniciar una nueva solicitud",
                                mensajeBloqueoNueva,
                                tituloEncabezado: "Proceso activo en curso",
                                panelHint: "Debe finalizar o continuar el trámite vigente antes de abrir uno nuevo para la misma compañía.",
                                accionUrl: urlContinuar,
                                accionTexto: activa != null ? "Continuar proceso actual" : null,
                                estilo: "warning");
                        }
                    }

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

                var esEdicion = oid.HasValue && oid.Value > 0;
                var usarDatosUsuarioActual = !esEdicion ||
                    (vm.Solicitud != null && vm.Solicitud.CodigoUsuario == usuarioId);

                // Los datos persistidos en la solicitud SIEMPRE prevalecen sobre los datos
                // del perfil del usuario; el perfil es solo un valor inicial (fallback).
                var representanteGuardado = esEdicion && vm.Solicitud != null
                    ? FormatearNombreCompleto(vm.Solicitud.RepresentanteLegal, null)
                    : string.Empty;
                var identificacionGuardada = esEdicion && vm.Solicitud != null
                    ? NormalizarIdentificacion(!string.IsNullOrWhiteSpace(vm.Solicitud.CedulaRepresentante)
                        ? vm.Solicitud.CedulaRepresentante
                        : vm.Solicitud.Ruc)
                    : string.Empty;

                var nombreRepresentanteUsuario = usarDatosUsuarioActual
                    ? ObtenerNombreRepresentanteTecnicoActual(usuarioId, vm.Usuario)
                    : string.Empty;
                var identificacionUsuario = ObtenerIdentificacionUsuarioActual(usuarioId, vm.Usuario);

                var identificacionVista = !string.IsNullOrWhiteSpace(identificacionGuardada)
                    ? identificacionGuardada
                    : (!string.IsNullOrWhiteSpace(identificacionUsuario) ? identificacionUsuario : string.Empty);
                var nombreRepresentanteVista = !string.IsNullOrWhiteSpace(representanteGuardado)
                    ? representanteGuardado
                    : nombreRepresentanteUsuario;

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

                System.Diagnostics.Trace.TraceInformation(
                    "[SOLICITUD_AOCR][CARGAR_FORMULARIO] SolicitudId=" + (vm.Solicitud != null ? vm.Solicitud.CodigoSolicitud : 0) +
                    "; UsuarioId=" + usuarioId +
                    "; Compania=" + (companiaSeleccionadaCodigo ?? string.Empty) +
                    "; Estado=" + (vm.Solicitud != null ? (vm.Solicitud.Estado ?? string.Empty) : string.Empty) +
                    "; RepresentanteEncontrado=" + (!string.IsNullOrWhiteSpace(representanteGuardado)) +
                    "; Aeronaves=" + (vm.Aeronaves != null ? vm.Aeronaves.Count : 0) +
                    "; TipoSolicitud=" + (vm.Solicitud != null && vm.Solicitud.TipoSolicitud.HasValue ? vm.Solicitud.TipoSolicitud.Value : 0));

                if (Request != null && Request.IsAjaxRequest())
                {
                    return PartialView("_FormularioEmisionAOCR", vm);
                }

                return View("FormularioEmisionAOCR", vm);
            }
            catch (Exception ex)
            {
                var cid = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                var usuarioActual = User != null && User.Identity != null && User.Identity.IsAuthenticated
                    ? User.Identity.Name
                    : "ANONIMO";
                var postgresEx = ex as PostgresException;
                var mensajeUsuario = "No se pudo cargar la información de la solicitud. Revise la configuración del formulario o contacte al administrador.";

                System.Diagnostics.Trace.TraceError(
                    "[FormularioEmisionAOCR][CID:{0}] Usuario={1}; Oid={2}; TipoSolicitud={3}; SqlState={4}; Mensaje={5}; Detalle={6}",
                    cid,
                    usuarioActual,
                    oid.HasValue ? oid.Value.ToString() : "N/A",
                    tipoSolicitud.HasValue ? tipoSolicitud.Value.ToString() : "N/A",
                    postgresEx != null ? postgresEx.SqlState : "N/A",
                    postgresEx != null ? ObtenerMensajeErrorBaseDatos(postgresEx) : ex.Message,
                    ex.ToString());

                return Content($@"
                    <div class='alert alert-danger m-3'>
                        <i class='fas fa-exclamation-triangle'></i> 
                        <strong>Error al cargar el formulario:</strong><br/>
                        {HttpUtility.HtmlEncode(mensajeUsuario)}
                        <br/><small class='text-muted'>CID: {HttpUtility.HtmlEncode(cid)}. Revise los registros del servidor para más detalles.</small>
                    </div>");
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
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "EditarRT", RequireCompanySelection = true)]
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
                    var solicitudActiva = BuscarSolicitudRtHabilitadaReutilizable(usuarioId, companiaSeleccionadaCodigo, vm.Solicitud.TipoSolicitud)
                        ?? BuscarSolicitudActivaReutilizable(usuarioId, companiaSeleccionadaCodigo, vm.Solicitud.TipoSolicitud);
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

                    if (EsSubsanacionDocumentalInspectorPendiente(actual))
                    {
                        return JsonFlujoIncorrectoSubsanacionInspector(actual);
                    }

                    if (!EsAdmin() && !SolicitudEsEditableFormularioEmision(actual))
                    {
                        return JsonRechazoEdicionFormularioEmision(actual);
                    }

                    // Guard POST: no permitir guardar si el pago está pendiente de aprobación
                    if (!EsAdmin() && !User.IsInRole("Financiero") && !User.IsInRole("CoordinadorFinanciero"))
                    {
                        string mensajeBloqueo;
                        if (!_solicitudAocrService.PuedeRtEditarSolicitud(actual.CodigoSolicitud, usuarioId, out mensajeBloqueo))
                        {
                            return Json(new { success = false, mensaje = mensajeBloqueo }, JsonRequestBehavior.AllowGet);
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
                    idFinal = GuardarFormularioCompletoAtomico(
                        vm,
                        usuarioId,
                        usuarioCorreo,
                        requiereEnvioCoordinador && !EsAdmin() && UsuarioActualEsRt());
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

                if (requiereEnvioCoordinador)
                {
                    try
                    {
                        _solicitudDAO.MarcarPendienteAsignacionCoordinacion(idFinal, usuarioCorreo);
                    }
                    catch (Exception exPendienteAsignacion)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] No se pudo marcar pendiente_asignacion_inspector: " + exPendienteAsignacion.Message);
                    }
                }

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

                SolicitudAOCR solicitudNotificacion = null;
                if (requiereEnvioCoordinador)
                {
                    solicitudNotificacion = _solicitudDAO.ObtenerPorId(idFinal) ?? vm.Solicitud;

                    try
                    {
                        _solicitudAocrCorreoService.NotificarEvento(
                            solicitudNotificacion,
                            "SOLICITUD_COMPLETADA",
                            "Solicitud formal enviada al coordinador para revisión documental.");
                    }
                    catch (Exception exCorreoCoordinacion)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Error notificando envío a coordinación: " + exCorreoCoordinacion.Message);
                    }

                    try
                    {
                        NotificarInspectorDocumentacionLista(
                            solicitudNotificacion,
                            usuarioId,
                            !string.IsNullOrWhiteSpace(usuarioCorreo) ? usuarioCorreo : usuarioId.ToString());
                    }
                    catch (Exception exCorreoInspector)
                    {
                        System.Diagnostics.Debug.WriteLine("[FormularioCompleto] Error notificando documentación lista al inspector: " + exCorreoInspector.Message);
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
                var mensajeExito = requiereEnvioCoordinador && !EsAdmin() && UsuarioActualEsRt()
                    ? SolicitudAocrService.MensajeNuevaOrdenRequerida
                    : "Solicitud AOCR registrada correctamente.";
                return Json(new { success = true, mensaje = mensajeExito, id = idFinal }, JsonRequestBehavior.AllowGet);
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
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "GuardarProgresoRT", RequireCompanySelection = true, CodigoSolicitudParameter = "codigoSolicitud")]
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

                if (string.IsNullOrWhiteSpace(companiaActivaCodigo) && sol.CodigoSolicitud > 0)
                {
                    CompaniaActivaRecoveryHelper.TryRestoreFromSolicitud(Session, sol.CodigoSolicitud, usuarioId, EsAdmin());
                    companiaActivaCodigo = ObtenerCompaniaActivaCodigo();
                    companiaActivaNombre = ObtenerCompaniaActivaNombre();
                }

                var companiaFinal = ResolverCompaniaSeleccionadaUnica(
                    companiaActivaCodigo, sol.CompaniasSeleccionadas, null);

                if (string.IsNullOrWhiteSpace(companiaFinal))
                {
                    return Json(new
                    {
                        ok = false,
                        success = false,
                        code = "COMPANY_CONTEXT_MISSING",
                        message = "No hay compañía activa seleccionada.",
                        mensaje = "No hay compañía activa seleccionada.",
                        requiresCompanySelection = true,
                        redirectUrl = Url.Action("SeleccionarCompania", "Account", new { returnUrl = Request != null ? Request.RawUrl : null }),
                        data = (object)null
                    });
                }
                sol.CompaniasSeleccionadas = companiaFinal;
                sol.TipoSolicitud = NormalizarTipoSolicitud(sol.TipoSolicitud);
                NormalizarTextosSolicitudFormulario(sol);

                if (sol.CodigoSolicitud <= 0)
                {
                    var solicitudActiva = BuscarSolicitudRtHabilitadaReutilizable(usuarioId, companiaFinal, sol.TipoSolicitud)
                        ?? BuscarSolicitudActivaReutilizable(usuarioId, companiaFinal, sol.TipoSolicitud);
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

                    if (EsSubsanacionDocumentalInspectorPendiente(actual))
                    {
                        return Json(new
                        {
                            ok = false,
                            success = false,
                            code = "SUBSANACION_DOCUMENTAL_REQUIERE_FLUJO_INSPECTOR",
                            message = "La solicitud corresponde a una subsanación documental. Debe enviarse al Inspector asignado, no al flujo inicial de asignación.",
                            mensaje = "La solicitud corresponde a una subsanación documental. Debe enviarse al Inspector asignado, no al flujo inicial de asignación.",
                            data = (object)null,
                            redirectUrl = Url.Action("Subsanar", "SolicitudAOCR", new { id = actual.CodigoSolicitud })
                        }, JsonRequestBehavior.AllowGet);
                    }

                    if (!EsAdmin() && !SolicitudEsEditableFormularioEmision(actual))
                    {
                        var estadoVisible = string.IsNullOrWhiteSpace(actual.Estado) ? "desconocido" : actual.Estado.Trim();
                        return JsonEnvelope(
                            false,
                            "NOT_EDITABLE",
                            "La solicitud ya no puede editarse porque avanzó a la etapa: " + estadoVisible,
                            data: null);
                    }

                    if (!EsAdmin() && !User.IsInRole("Financiero") && !User.IsInRole("CoordinadorFinanciero"))
                    {
                        string mensajeBloqueo;
                        if (!_solicitudAocrService.PuedeRtEditarSolicitud(actual.CodigoSolicitud, usuarioId, out mensajeBloqueo))
                        {
                            return JsonEnvelope(false, "FORBIDDEN", mensajeBloqueo, data: null);
                        }
                    }

                    AplicarCambiosGuardarProgreso(actual, sol, seccion);
                    bool ok = _solicitudBL.Actualizar(actual, usuarioId, out msg, EsAdmin());
                    if (!ok)
                    {
                        System.Diagnostics.Trace.TraceWarning(
                            "[SOLICITUD_AOCR][GUARDAR_PROGRESO] SolicitudId=" + actual.CodigoSolicitud +
                            "; UsuarioId=" + usuarioId +
                            "; Compania=" + (companiaFinal ?? string.Empty) +
                            "; Seccion=" + seccion +
                            "; FilasAfectadas=0; Resultado=UPDATE_FAILED; Mensaje=" + (msg ?? string.Empty));
                        return JsonEnvelope(false, "UPDATE_FAILED",
                            string.IsNullOrWhiteSpace(msg)
                                ? "No se guardaron cambios. Verifique la solicitud activa y los datos enviados."
                                : msg,
                            data: null);
                    }
                    idFinal = actual.CodigoSolicitud;
                }

                var persistida = _solicitudDAO.ObtenerPorId(idFinal);
                if (persistida == null)
                {
                    System.Diagnostics.Trace.TraceError(
                        "[SOLICITUD_AOCR][GUARDAR_PROGRESO] SolicitudId=" + idFinal +
                        "; UsuarioId=" + usuarioId +
                        "; Seccion=" + seccion +
                        "; Resultado=NO_CONFIRMADO (relectura nula)");
                    return JsonEnvelope(false, "PERSISTENCE_NOT_CONFIRMED",
                        "El sistema intentó guardar, pero no pudo confirmar la persistencia de los datos.", data: null);
                }

                NormalizarTextosSolicitudFormulario(persistida);

                string campoNoPersistido;
                if (!SeccionQuedoPersistida(persistida, sol, seccion, out campoNoPersistido))
                {
                    System.Diagnostics.Trace.TraceError(
                        "[SOLICITUD_AOCR][GUARDAR_PROGRESO] SolicitudId=" + idFinal +
                        "; UsuarioId=" + usuarioId +
                        "; Seccion=" + seccion +
                        "; Resultado=NO_CONFIRMADO; CampoNoPersistido=" + campoNoPersistido);
                    return JsonEnvelope(false, "PERSISTENCE_NOT_CONFIRMED",
                        "El sistema guardó, pero no pudo confirmar la persistencia del campo: " + campoNoPersistido + ". Revise el log técnico.", data: null);
                }

                System.Diagnostics.Trace.TraceInformation(
                    "[SOLICITUD_AOCR][GUARDAR_PROGRESO] SolicitudId=" + idFinal +
                    "; UsuarioId=" + usuarioId +
                    "; Compania=" + (companiaFinal ?? string.Empty) +
                    "; Seccion=" + seccion +
                    "; Estado=" + (persistida.Estado ?? string.Empty) +
                    "; Resultado=OK; PersistenciaConfirmada=True");

                return Json(new
                {
                    ok = true,
                    success = true,
                    code = "OK",
                    message = "Datos guardados correctamente.",
                    mensaje = "Datos guardados correctamente.",
                    id = idFinal,
                    seccion = seccion,
                    redirectUrl = (string)null,
                    data = new
                    {
                        id = idFinal,
                        seccion = seccion,
                        solicitud = ConstruirSnapshotSolicitudGuardada(persistida, seccion)
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[GuardarProgreso] Error: " + ex.Message);
                return JsonEnvelope(false, "INTERNAL_ERROR", "Error al guardar: " + ex.Message, data: null);
            }
        }

        /// <summary>
        /// Compara los campos editables de la sección guardada contra la fila releída de base,
        /// para confirmar que la persistencia fue real (no solo que el endpoint terminó sin error).
        /// </summary>
        private static bool SeccionQuedoPersistida(SolicitudAOCR persistida, SolicitudAOCR enviada, string seccion, out string campoNoPersistido)
        {
            campoNoPersistido = string.Empty;
            if (persistida == null)
            {
                campoNoPersistido = "(solicitud)";
                return false;
            }

            if (enviada == null)
            {
                return true;
            }

            Func<string, string> norm = v => FormularioEmisionTextHelper.NormalizarTextoPlano(v ?? string.Empty).Trim();

            var seccionNormalizada = (seccion ?? string.Empty).Trim().ToLowerInvariant();
            if (seccionNormalizada == "explotador")
            {
                if (norm(persistida.Direccion) != norm(enviada.Direccion)) { campoNoPersistido = "Direccion"; return false; }
                if (norm(persistida.Telefono) != norm(enviada.Telefono)) { campoNoPersistido = "Telefono"; return false; }
                if (norm(persistida.RepresentanteLegal) != norm(enviada.RepresentanteLegal)) { campoNoPersistido = "RepresentanteLegal"; return false; }
                return true;
            }

            if (seccionNormalizada == "operaciones")
            {
                if (norm(persistida.ResumenOperacionesEae) != norm(enviada.ResumenOperacionesEae)
                    && norm(persistida.DescripcionOperacion) != norm(enviada.ResumenOperacionesEae))
                {
                    campoNoPersistido = "ResumenOperacionesEae";
                    return false;
                }
                if (!TokensCsvEquivalentes(persistida.TipoOperacion, enviada.TipoOperacion, '|')) { campoNoPersistido = "TipoOperacion"; return false; }
                if (norm(persistida.NumeroAOC) != norm(enviada.NumeroAOC)) { campoNoPersistido = "NumeroAOC"; return false; }
                if (!TokensCsvEquivalentes(persistida.AeropuertosEcuador, enviada.AeropuertosEcuador, ',')) { campoNoPersistido = "AeropuertosEcuador"; return false; }
                if (norm(persistida.AeropuertosEcuadorOtros) != norm(enviada.AeropuertosEcuadorOtros)) { campoNoPersistido = "AeropuertosEcuadorOtros"; return false; }
                return true;
            }

            return true;
        }

        /// <summary>
        /// Compara listas de tokens separados por delimitador sin depender del orden
        /// (evita falsos negativos en verificación post-guardado).
        /// </summary>
        private static bool TokensCsvEquivalentes(string valorA, string valorB, char separadorPrincipal)
        {
            var setA = TokenizarListaPersistencia(valorA, separadorPrincipal);
            var setB = TokenizarListaPersistencia(valorB, separadorPrincipal);
            if (setA.Count != setB.Count)
            {
                return false;
            }

            return setA.SetEquals(setB);
        }

        private static HashSet<string> TokenizarListaPersistencia(string valor, char separadorPrincipal)
        {
            var separadores = new[] { separadorPrincipal, ',', ';', '|' };
            return new HashSet<string>(
                (valor ?? string.Empty)
                    .Split(separadores, StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => FormularioEmisionTextHelper.NormalizarTextoPlano(v).Trim().ToUpperInvariant())
                    .Where(v => !string.IsNullOrWhiteSpace(v)),
                StringComparer.OrdinalIgnoreCase);
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

            // El guardado parcial nunca debe mover el estado del trámite. Las transiciones
            // formales se ejecutan en endpoints de flujo para conservar historial y notificaciones.

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
                var resumen = FormularioEmisionTextHelper.NormalizarTextoPlano(parcial.ResumenOperacionesEae);
                if (string.IsNullOrWhiteSpace(resumen))
                {
                    resumen = FormularioEmisionTextHelper.NormalizarTextoPlano(parcial.DescripcionOperacion);
                }

                actual.TipoOperacion = FormularioEmisionTextHelper.NormalizarTextoPlano(parcial.TipoOperacion);
                actual.ResumenOperacionesEae = resumen;
                actual.DescripcionOperacion = resumen;
                actual.NumeroAOC = FormularioEmisionTextHelper.NormalizarTextoPlano(parcial.NumeroAOC);
                actual.AeropuertosEcuador = FormularioEmisionTextHelper.NormalizarTextoPlano(parcial.AeropuertosEcuador);
                actual.AeropuertosEcuadorOtros = FormularioEmisionTextHelper.NormalizarTextoPlano(parcial.AeropuertosEcuadorOtros);
            }
        }

        private static void NormalizarTextosSolicitudFormulario(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return;
            }

            solicitud.RepresentanteLegal = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.RepresentanteLegal);
            solicitud.Direccion = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.Direccion);
            solicitud.Telefono = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.Telefono);
            solicitud.Email = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.Email);
            solicitud.RazonSocial = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.RazonSocial);
            solicitud.NombreComercial = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.NombreComercial);
            solicitud.NombreOperador = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.NombreOperador);
            solicitud.TipoOperacion = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.TipoOperacion);
            solicitud.DescripcionOperacion = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.DescripcionOperacion);
            solicitud.ResumenOperacionesEae = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.ResumenOperacionesEae);
            solicitud.NumeroAOC = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.NumeroAOC);
            solicitud.AprobacionesEspeciales = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.AprobacionesEspeciales);
            solicitud.AprobacionesEspecialesOtros = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.AprobacionesEspecialesOtros);
            solicitud.AeropuertosEcuador = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.AeropuertosEcuador);
            solicitud.AeropuertosEcuadorOtros = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.AeropuertosEcuadorOtros);
            solicitud.ObservacionesGenerales = FormularioEmisionTextHelper.NormalizarTextoPlano(solicitud.ObservacionesGenerales);
        }

        private static object ConstruirSnapshotSolicitudGuardada(SolicitudAOCR persistida, string seccion)
        {
            if (persistida == null)
            {
                return null;
            }

            NormalizarTextosSolicitudFormulario(persistida);

            var seccionNormalizada = (seccion ?? string.Empty).Trim().ToLowerInvariant();
            if (seccionNormalizada == "explotador")
            {
                return new
                {
                    persistida.RepresentanteLegal,
                    persistida.CedulaRepresentante,
                    persistida.CorreoRepresentanteTecnico,
                    persistida.Direccion,
                    persistida.Telefono,
                    persistida.Email,
                    persistida.Ruc
                };
            }

            if (seccionNormalizada == "operaciones")
            {
                return new
                {
                    persistida.TipoOperacion,
                    persistida.DescripcionOperacion,
                    persistida.ResumenOperacionesEae,
                    persistida.NumeroAOC,
                    persistida.AeropuertosEcuador,
                    persistida.AeropuertosEcuadorOtros
                };
            }

            return new
            {
                persistida.NombreOperador,
                persistida.RazonSocial,
                persistida.NombreComercial,
                persistida.CodigoOaci,
                persistida.CompaniasSeleccionadas
            };
        }

        private int GuardarFormularioCompletoAtomico(SolicitudAOCRViewModel vm, int usuarioId, string usuarioCorreo, bool bloquearModuloRtAlFinalizar)
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

                    var estadoFormularioNormalizado = EstadoSolicitud.Normalizar(vm.Solicitud != null ? vm.Solicitud.Estado : null);
                    var debeValidarSolicitudInspeccionFirmada = !string.Equals(estadoFormularioNormalizado, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase);
                    var mensajeSolicitudInspeccionPendiente = debeValidarSolicitudInspeccionFirmada
                        ? ObtenerMensajeSolicitudInspeccionFirmadaPendiente(idFinal, vm.Solicitud)
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(mensajeSolicitudInspeccionPendiente))
                    {
                        throw new ApplicationException(mensajeSolicitudInspeccionPendiente);
                    }

                    if (bloquearModuloRtAlFinalizar)
                    {
                        string mensajeBloqueoRt;
                        if (!_solicitudAocrService.FinalizarSolicitudRt(idFinal, usuarioId, usuarioId.ToString(), out mensajeBloqueoRt))
                        {
                            throw new ApplicationException(mensajeBloqueoRt);
                        }
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
                    SetIfExists(doc, "NombreArchivoOriginal", fileName);
                    SetIfExists(doc, "NombreArchivoVisible", fileName);
                    SetIfExists(doc, "NombreArchivoFisico", fileName);
                    SetIfExists(doc, "NombreArchivoGuardado", fileName);
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
                    NombreArchivo = Path.GetFileName(file.FileName),
                    NombreArchivoOriginal = Path.GetFileName(file.FileName),
                    NombreArchivoVisible = Path.GetFileName(file.FileName),
                    NombreArchivoFisico = result.StoredName,
                    NombreArchivoGuardado = result.StoredName,
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
            var solicitudActual = codigoSolicitud.HasValue && codigoSolicitud.Value > 0
                ? _solicitudDAO.ObtenerPorId(codigoSolicitud.Value)
                : null;
            var esModoSubsanacionObservada = solicitudActual != null
                && string.Equals(EstadoSolicitud.Normalizar(solicitudActual.Estado), EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase);
            var documentosObligatorios = ObtenerDocumentosObligatoriosPorTipoSolicitud(tipoSolicitud).ToList();
            var clavesPendientesSubsanacion = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var restringirDocumentosFormularioPorSubsanacion = false;

            if (esModoSubsanacionObservada && codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
            {
                foreach (var documentoPendiente in ObtenerDocumentosPendientesSubsanacionParaSolicitud(codigoSolicitud.Value))
                {
                    string claveDocumentoObligatorio;
                    if (TryResolverDocumentoObligatorioKeyPorTipo(documentoPendiente, out claveDocumentoObligatorio))
                    {
                        clavesPendientesSubsanacion.Add(claveDocumentoObligatorio);
                    }
                    else
                    {
                        // Los documentos sin input directo se corrigen desde la pantalla de subsanación, no desde este formulario.
                    }
                }

                restringirDocumentosFormularioPorSubsanacion = clavesPendientesSubsanacion.Count > 0;
                if (restringirDocumentosFormularioPorSubsanacion)
                {
                    documentosObligatorios = documentosObligatorios
                        .Where(item => clavesPendientesSubsanacion.Contains(item.Key))
                        .ToList();
                }
            }

            var clavesObligatorias = new HashSet<string>(documentosObligatorios.Select(item => item.Key), StringComparer.OrdinalIgnoreCase);

            foreach (var documento in documentosExistentes.Where(d => d != null && d.CodigoDocumento > 0))
            {
                foreach (var item in DocumentoObligatorioTipos)
                {
                    if (clavesObligatorias.Contains(item.Key)
                        && (!restringirDocumentosFormularioPorSubsanacion || !clavesPendientesSubsanacion.Contains(item.Key))
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

            var faltantes = documentosObligatorios
                .Where(item => !cubiertos.Contains(item.Key))
                .Select(item => item.Value)
                .ToList();

            if (!esModoSubsanacionObservada && codigoSolicitud.HasValue && codigoSolicitud.Value > 0)
            {
                var mensajeSolicitudInspeccionPendiente = ObtenerMensajeSolicitudInspeccionFirmadaPendiente(codigoSolicitud.Value, null, documentosExistentes);
                if (!string.IsNullOrWhiteSpace(mensajeSolicitudInspeccionPendiente)
                    && !faltantes.Contains(EtiquetaSolicitudInspeccionFirmada, StringComparer.OrdinalIgnoreCase))
                {
                    faltantes.Add(EtiquetaSolicitudInspeccionFirmada);
                }
            }

            return faltantes;
        }

        private void ConfigurarModoSubsanacionObservada(SolicitudAOCRViewModel vm, int codigoSolicitud)
        {
            if (vm == null || vm.Solicitud == null || codigoSolicitud <= 0)
            {
                return;
            }

            var estadoNormalizado = EstadoSolicitud.Normalizar(vm.Solicitud.Estado ?? string.Empty);
            if (!string.Equals(estadoNormalizado, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            vm.EsModoSubsanacionObservada = true;

            foreach (var documentoPendiente in ObtenerDocumentosPendientesSubsanacionParaSolicitud(codigoSolicitud))
            {
                var etiqueta = ObtenerEtiquetaDocumento(documentoPendiente);
                if (!string.IsNullOrWhiteSpace(etiqueta)
                    && !vm.DocumentosPendientesSubsanacionEtiquetas.Contains(etiqueta, StringComparer.OrdinalIgnoreCase))
                {
                    vm.DocumentosPendientesSubsanacionEtiquetas.Add(etiqueta);
                }

                string claveDocumentoObligatorio;
                if (!TryResolverDocumentoObligatorioKeyPorTipo(documentoPendiente, out claveDocumentoObligatorio))
                {
                    if (!string.IsNullOrWhiteSpace(etiqueta)
                        && !vm.DocumentosPendientesSubsanacionNoGestionables.Contains(etiqueta, StringComparer.OrdinalIgnoreCase))
                    {
                        vm.DocumentosPendientesSubsanacionNoGestionables.Add(etiqueta);
                    }

                    continue;
                }

                string inputId;
                if (TryResolverInputIdDocumentoObligatorio(claveDocumentoObligatorio, out inputId)
                    && !vm.DocumentosPendientesSubsanacionInputIds.Contains(inputId, StringComparer.OrdinalIgnoreCase))
                {
                    vm.DocumentosPendientesSubsanacionInputIds.Add(inputId);
                }
            }
        }

        private List<Documento> ObtenerDocumentosPendientesSubsanacionParaSolicitud(int codigoSolicitud)
        {
            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(codigoSolicitud);
            return ObtenerDocumentosPendientesSubsanacionParaSolicitud(codigoSolicitud, revisiones);
        }

        private List<Documento> ObtenerDocumentosPendientesSubsanacionParaSolicitud(
            int codigoSolicitud,
            IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (codigoSolicitud <= 0)
            {
                return new List<Documento>();
            }

            var pendientes = _revisionDocumentalService.ObtenerDocumentosPendientesSubsanacion(
                    ObtenerDocumentosElegiblesParaSubsanacion(codigoSolicitud),
                    revisiones)
                .ToList();

            return SeleccionarUltimosDocumentosPendientesSubsanacionPorGrupo(pendientes);
        }

        private static bool TryResolverDocumentoObligatorioKeyPorTipo(Documento documento, out string claveDocumentoObligatorio)
        {
            return TryResolverDocumentoObligatorioKeyPorTipo(documento != null ? documento.TipoDocumento : null, out claveDocumentoObligatorio);
        }

        private static bool TryResolverDocumentoObligatorioKeyPorTipo(string tipoDocumento, out string claveDocumentoObligatorio)
        {
            var tipoCanonico = RevisionDocumentalDisplayHelper.GetCanonicalDocumentType(tipoDocumento);
            foreach (var item in DocumentoObligatorioTipos)
            {
                if (item.Value.Any(tipo => string.Equals(tipo, tipoCanonico, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tipo, tipoDocumento ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
                {
                    claveDocumentoObligatorio = item.Key;
                    return true;
                }
            }

            claveDocumentoObligatorio = null;
            return false;
        }

        private static bool TryResolverInputIdDocumentoObligatorio(string claveDocumentoObligatorio, out string inputId)
        {
            foreach (var item in DocumentoObligatorioInputs)
            {
                if (string.Equals(item.Value, claveDocumentoObligatorio, StringComparison.OrdinalIgnoreCase))
                {
                    inputId = item.Key;
                    return true;
                }
            }

            inputId = null;
            return false;
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

        private string ObtenerMensajeSolicitudInspeccionFirmadaPendiente(int codigoSolicitud, SolicitudAOCR solicitud = null, IList<Documento> documentosSolicitud = null)
        {
            if (codigoSolicitud <= 0)
            {
                return string.Empty;
            }

            var solicitudEvaluada = solicitud;
            if (solicitudEvaluada == null || solicitudEvaluada.CodigoSolicitud != codigoSolicitud)
            {
                solicitudEvaluada = _solicitudDAO.ObtenerPorId(codigoSolicitud);
            }

            if (!SolicitudRequiereInspeccionExtFirmada(solicitudEvaluada))
            {
                return string.Empty;
            }

            var documentos = documentosSolicitud ?? (_documentoDAO.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>());
            if (documentos.Any(EsDocumentoSolicitudInspeccionFirmada))
            {
                return string.Empty;
            }

            return "Debe adjuntar todos los documentos obligatorios antes de enviar la solicitud. Faltan: " + EtiquetaSolicitudInspeccionFirmada + ".";
        }

        private bool SolicitudRequiereInspeccionExtFirmada(SolicitudAOCR solicitud)
        {
            if (solicitud == null || solicitud.CodigoSolicitud <= 0 || solicitud.CodigoUsuario <= 0)
            {
                return false;
            }

            try
            {
                var codigoSolicitudTexto = solicitud.CodigoSolicitud.ToString();
                var ordenes = _ordenRecaudacionDAO.ListarPorUsuarioModel(solicitud.CodigoUsuario, null) ?? new List<CapaDatos.Models.OrdenRecaudacionModel>();

                return ordenes
                    .Where(o => o != null)
                    .Where(o => string.Equals((o.CodigoSolicitud ?? string.Empty).Trim(), codigoSolicitudTexto, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(o => o.Id)
                    .Any(OrdenContieneInspeccionExt);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[SolicitudAOCR] Error validando concepto INSPECCION_EXT para solicitud " + solicitud.CodigoSolicitud + ": " + ex.Message);
                return false;
            }
        }

        private static bool OrdenContieneInspeccionExt(CapaDatos.Models.OrdenRecaudacionModel orden)
        {
            return orden != null
                && orden.Detalles != null
                && orden.Detalles.Any(d => d != null
                    && string.Equals((d.ConceptoCodigo ?? string.Empty).Trim(), CodigoConceptoInspeccionExt, StringComparison.OrdinalIgnoreCase));
        }

        private static bool EsDocumentoSolicitudInspeccionFirmada(Documento documento)
        {
            if (documento == null || documento.CodigoDocumento <= 0)
            {
                return false;
            }

            var tipoCanonico = RevisionDocumentalDisplayHelper.GetCanonicalDocumentType(documento.TipoDocumento);
            return string.Equals(tipoCanonico, TipoSolicitudInspeccionFirmada, StringComparison.OrdinalIgnoreCase)
                || string.Equals((documento.TipoDocumento ?? string.Empty).Trim(), TipoSolicitudInspeccionFirmada, StringComparison.OrdinalIgnoreCase);
        }

        private void NotificarInspectorDocumentacionLista(SolicitudAOCR solicitud, int usuarioId, string usuarioRegistro)
        {
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return;
            }

            try
            {
                var documentosFaltantes = ObtenerDocumentosObligatoriosFaltantes(solicitud.CodigoSolicitud, null, solicitud.TipoSolicitud);
                if (documentosFaltantes.Count > 0)
                {
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        solicitud.CodigoSolicitud,
                        null,
                        "NOTIFICACION_DOCUMENTACION_LISTA_INSPECTOR_OMITIDA",
                        "No se notificó al inspector porque la documentación aún está incompleta. Faltan: " + string.Join(", ", documentosFaltantes),
                        usuarioId,
                        usuarioRegistro);
                    return;
                }

                var inspeccion = ObtenerUltimaInspeccionVinculada(_solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud));
                var codigoInspector = inspeccion != null && inspeccion.CodigoInspector.HasValue
                    ? inspeccion.CodigoInspector.Value
                    : (solicitud.CodigoTecnico.HasValue ? solicitud.CodigoTecnico.Value : 0);

                if (codigoInspector <= 0)
                {
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        solicitud.CodigoSolicitud,
                        null,
                        "NOTIFICACION_DOCUMENTACION_LISTA_INSPECTOR_OMITIDA",
                        "No se encontró inspector asignado para notificar la documentación lista para revisión.",
                        usuarioId,
                        usuarioRegistro);
                    return;
                }

                var eventKey = "DOCUMENTACION_LISTA_RT_" + solicitud.CodigoSolicitud + "_" + codigoInspector;
                var queue = new EmailQueueService();
                if (queue.ExisteNotificacionAsync("DOCUMENTACION_LISTA_RT", eventKey, solicitud.CodigoSolicitud).GetAwaiter().GetResult())
                {
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
                var fechaEnvio = DateTime.Now;

                NotificacionBL.EnviarNotificacion(
                    codigoInspector,
                    "Documentación AOCR lista para revisión",
                    "El RT ha completado la carga documental de la Solicitud AOCR " + numeroSolicitud + ".",
                    "INFO",
                    Url.Action("Detalle", "SolicitudAOCR", new { id = solicitud.CodigoSolicitud }),
                    "AOCR",
                    solicitud.CodigoSolicitud,
                    "SOLICITUD_AOCR");

                if (!string.IsNullOrWhiteSpace(correoInspector))
                {
                    var asunto = "Solicitud AOCR " + numeroSolicitud + " - Documentación lista para revisión";
                    var cuerpo = ConstruirHtmlCorreoDocumentacionListaInspector(
                        nombreInspector,
                        nombreRt,
                        numeroSolicitud,
                        operadora,
                        fechaEnvio,
                        solicitud.CodigoSolicitud);

                    queue.EncolarAsync(new EmailQueueItem
                    {
                        Para = correoInspector,
                        ParaNombre = nombreInspector,
                        Asunto = asunto,
                        Cuerpo = cuerpo,
                        EsHtml = true,
                        TipoNotificacion = "DOCUMENTACION_LISTA_RT",
                        SolicitudId = solicitud.CodigoSolicitud,
                        EventKey = eventKey,
                        MaxIntentos = 3
                    }).GetAwaiter().GetResult();
                }

                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    solicitud.CodigoSolicitud,
                    null,
                    "NOTIFICACION_DOCUMENTACION_LISTA_ENVIADA_INSPECTOR",
                    "Notificación de documentación lista para revisión enviada al inspector asignado.",
                    usuarioId,
                    usuarioRegistro);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[FormularioCompleto][NotificarInspectorDocumentacionLista] " + ex.Message);
                try
                {
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        solicitud.CodigoSolicitud,
                        null,
                        "NOTIFICACION_DOCUMENTACION_LISTA_INSPECTOR_ERROR",
                        "No se pudo encolar la notificación de documentación lista al inspector. Error: " + ex.Message,
                        usuarioId,
                        usuarioRegistro);
                }
                catch
                {
                }
            }
        }

        private string ConstruirHtmlCorreoDocumentacionListaInspector(
            string nombreInspector,
            string nombreRt,
            string numeroSolicitud,
            string operadora,
            DateTime fechaEnvio,
            int codigoSolicitud)
        {
            string enlaceDetalle;
            try
            {
                enlaceDetalle = Url.Action("Detalle", "SolicitudAOCR", new { id = codigoSolicitud }, Request != null && Request.Url != null ? Request.Url.Scheme : "http");
            }
            catch
            {
                enlaceDetalle = string.Empty;
            }

            return "Estimado/a " + HttpUtility.HtmlEncode(nombreInspector) + ",<br><br>"
                + "Se informa que el Representante Técnico " + HttpUtility.HtmlEncode(nombreRt)
                + " completó la carga documental de la Solicitud AOCR " + HttpUtility.HtmlEncode(numeroSolicitud)
                + " correspondiente a la operadora " + HttpUtility.HtmlEncode(operadora) + ".<br><br>"
                + "<strong>Fecha de envío documental:</strong> " + fechaEnvio.ToString("dd/MM/yyyy HH:mm") + "<br>"
                + "<strong>Estado:</strong> Documentación lista para revisión documental.<br><br>"
                + (!string.IsNullOrWhiteSpace(enlaceDetalle)
                    ? "Puede revisar el detalle en el siguiente enlace: <a href=\"" + HttpUtility.HtmlAttributeEncode(enlaceDetalle) + "\">Ver solicitud</a>.<br><br>"
                    : string.Empty)
                + "Por favor, ingrese al sistema AOCR para continuar con la revisión documental.<br><br>"
                + "Atentamente,<br>Sistema AOCR<br>Dirección General de Aviación Civil";
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
            return _companiaContextService
                .FiltrarSolicitudesPorCompania(solicitudes, companiaActivaCodigo, ObtenerCompaniaActivaNombre())
                .ToList();
        }

        private bool SolicitudCoincideConCompaniaActiva(SolicitudAOCR solicitud, string companiaActivaCodigo)
        {
            return _companiaContextService.SolicitudPerteneceACompania(
                solicitud,
                companiaActivaCodigo,
                ObtenerCompaniaActivaNombre());
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

        private SolicitudAOCR BuscarSolicitudRtHabilitadaReutilizable(int codigoUsuario, string companiaActivaCodigo, int? tipoSolicitud, int? excluirCodigoSolicitud = null)
        {
            if (codigoUsuario <= 0)
            {
                return null;
            }

            var oidDesdeOrden = ResolverOidSolicitudDesdeOrdenUsuario(codigoUsuario, tipoSolicitud);
            if (oidDesdeOrden.HasValue && oidDesdeOrden.Value > 0
                && (!excluirCodigoSolicitud.HasValue || excluirCodigoSolicitud.Value != oidDesdeOrden.Value))
            {
                var solicitudOrden = _solicitudDAO.ObtenerPorId(oidDesdeOrden.Value);
                if (solicitudOrden != null && EsSolicitudActivaReutilizable(solicitudOrden))
                {
                    string mensajeBloqueoOrden;
                    if (_solicitudAocrService.PuedeRtEditarSolicitud(solicitudOrden.CodigoSolicitud, codigoUsuario, out mensajeBloqueoOrden))
                    {
                        return solicitudOrden;
                    }
                }
            }

            var tipoNormalizado = NormalizarTipoSolicitud(tipoSolicitud);
            var workflow = _solicitudAocrService;

            foreach (var solicitud in FiltrarSolicitudesPorCompaniaActiva(_solicitudDAO.ObtenerPorUsuario(codigoUsuario), companiaActivaCodigo)
                .Where(s => s != null && s.CodigoSolicitud > 0)
                .Where(s => !excluirCodigoSolicitud.HasValue || s.CodigoSolicitud != excluirCodigoSolicitud.Value)
                .Where(s => NormalizarTipoSolicitud(s.TipoSolicitud) == tipoNormalizado)
                .Where(EsSolicitudActivaReutilizable)
                .OrderByDescending(s => s.CodigoSolicitud))
            {
                string mensajeBloqueo;
                if (workflow.PuedeRtEditarSolicitud(solicitud.CodigoSolicitud, codigoUsuario, out mensajeBloqueo))
                {
                    return solicitud;
                }
            }

            return null;
        }

        private static bool EsSolicitudActivaReutilizable(SolicitudAOCR solicitud)
        {
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return false;
            }

            return EstadoSolicitud.PermiteEdicionFormularioEmision(solicitud.Estado);
        }

        private const string MensajeRedireccionSeguimientoNoEditable =
            "Esta solicitud ya avanzó a la etapa de inspección y no puede ser editada. Puede consultar el estado del trámite y continuar el flujo desde la pantalla de seguimiento.";

        private static bool SolicitudEsEditableFormularioEmision(SolicitudAOCR solicitud)
        {
            return solicitud != null && EstadoSolicitud.PermiteEdicionFormularioEmision(solicitud.Estado);
        }

        private ActionResult RedirigirSeguimientoSolicitudNoEditable(SolicitudAOCR solicitud, int usuarioId, string origen)
        {
            System.Diagnostics.Trace.TraceInformation(
                "[SOLICITUD_AOCR] Redirigiendo seguimiento por estado no editable: origen={0}; solicitud={1}; estado={2}; usuario={3}",
                origen ?? string.Empty,
                solicitud != null ? solicitud.CodigoSolicitud.ToString() : "N/A",
                solicitud != null ? (solicitud.Estado ?? string.Empty) : string.Empty,
                usuarioId);

            TempData["Info"] = MensajeRedireccionSeguimientoNoEditable;

            if (Request != null && Request.IsAjaxRequest())
            {
                var url = Url.Action("Detalle", new { id = solicitud.CodigoSolicitud });
                return Content(
                    "<div class='alert alert-info m-3'>" +
                    "<i class='fas fa-circle-info me-2'></i>" +
                    HttpUtility.HtmlEncode(MensajeRedireccionSeguimientoNoEditable) +
                    "<div class='mt-3'><a class='btn btn-primary btn-sm' href='" + HttpUtility.HtmlAttributeEncode(url) + "'>" +
                    "<i class='fas fa-route me-1'></i>Ir al seguimiento AOCR</a></div></div>");
            }

            return RedirectToAction("Detalle", new { id = solicitud.CodigoSolicitud });
        }

        private JsonResult JsonRechazoEdicionFormularioEmision(SolicitudAOCR solicitud)
        {
            var estadoVisible = solicitud == null || string.IsNullOrWhiteSpace(solicitud.Estado)
                ? "desconocido"
                : solicitud.Estado.Trim();

            return Json(new
            {
                success = false,
                message = "La solicitud ya no puede editarse porque avanzó a la etapa: " + estadoVisible
            });
        }

        private bool EsSubsanacionDocumentalInspectorPendiente(SolicitudAOCR solicitud)
        {
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return false;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!string.Equals(estadoActual, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoActual, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var pendientes = ObtenerDocumentosPendientesSubsanacionParaSolicitud(solicitud.CodigoSolicitud);
            if (pendientes != null && pendientes.Count > 0)
            {
                return true;
            }

            return SolicitudTieneInspectorAsignadoEnSolicitudOInspeccion(solicitud)
                && TieneRevisionDocumentalDevueltaPorInspector(solicitud.CodigoSolicitud);
        }

        private bool SolicitudTieneInspectorAsignadoEnSolicitudOInspeccion(SolicitudAOCR solicitud)
        {
            if (SolicitudTieneInspectorAsignado(solicitud))
            {
                return true;
            }

            try
            {
                return (_solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>())
                    .Any(i => i != null
                        && ((i.CodigoInspector.HasValue && i.CodigoInspector.Value > 0)
                            || !string.IsNullOrWhiteSpace(i.InspectorPrincipalCedula)
                            || !string.IsNullOrWhiteSpace(i.InspectorPrincipalNombre)));
            }
            catch
            {
                return false;
            }
        }

        private bool TieneRevisionDocumentalDevueltaPorInspector(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0)
            {
                return false;
            }

            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(codigoSolicitud);
            return ObtenerDocumentosElegiblesParaSubsanacion(codigoSolicitud)
                .Any(d =>
                {
                    var decision = ObtenerDecisionRevisionDocumental(d, revisiones);
                    return string.Equals(decision, "DEVUELTO", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(decision, "OBSERVADO", StringComparison.OrdinalIgnoreCase);
                });
        }

        private JsonResult JsonFlujoIncorrectoSubsanacionInspector(SolicitudAOCR solicitud)
        {
            var solicitudId = solicitud != null ? solicitud.CodigoSolicitud : 0;
            var mensaje = "La solicitud corresponde a una subsanación documental. Debe enviarse al Inspector asignado, no al flujo inicial de asignación.";

            Response.StatusCode = 409;
            Response.TrySkipIisCustomErrors = true;
            return Json(new
            {
                ok = false,
                success = false,
                code = 409,
                message = mensaje,
                mensaje,
                redirectUrl = solicitudId > 0 ? Url.Action("Subsanar", "SolicitudAOCR", new { id = solicitudId }) : null
            }, JsonRequestBehavior.AllowGet);
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

        private int? ResolverOidSolicitudDesdeOrdenUsuario(int codigoUsuario, int? tipoSolicitud)
        {
            var solicitud = ObtenerSolicitudVinculadaOrdenUsuario(codigoUsuario, tipoSolicitud);
            if (solicitud == null || !EstadoSolicitud.PermiteEdicionFormularioEmision(solicitud.Estado))
            {
                return null;
            }

            return solicitud.CodigoSolicitud;
        }

        private SolicitudAOCR ObtenerSolicitudVinculadaOrdenUsuario(int codigoUsuario, int? tipoSolicitud)
        {
            if (codigoUsuario <= 0)
            {
                return null;
            }

            var codigoSolicitudOrden = _ordenRecaudacionDAO.ObtenerCodigoSolicitudOrdenRecienteUsuario(codigoUsuario, soloOrdenGenerada: true);
            if (!codigoSolicitudOrden.HasValue || codigoSolicitudOrden.Value <= 0)
            {
                return null;
            }

            var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitudOrden.Value);
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return null;
            }

            if (NormalizarTipoSolicitud(solicitud.TipoSolicitud) != NormalizarTipoSolicitud(tipoSolicitud))
            {
                return null;
            }

            return solicitud;
        }

        // =========================================================
        // Resto de acciones (tu código igual)
        // =========================================================
        public ActionResult MisSolicitudes()
        {
            int codigoUsuario;
            if (!TryObtenerUsuarioActualId(out codigoUsuario))
                return RedirectToAction("Login", "Account");

            return RedirectToAction("Index", new { filtro = "observado" });
        }

        [Authorize]
        public ActionResult GeneradasFirmadas(AocrGeneradasFirmadasFiltroViewModel filtros)
        {
            var contexto = ConstruirContextoBandeja();
            if (!contexto.PuedeVerBandeja)
            {
                return new HttpStatusCodeResult(403, "No tiene permisos para consultar esta bandeja.");
            }

            filtros = NormalizarFiltrosBandeja(filtros ?? new AocrGeneradasFirmadasFiltroViewModel());
            if (filtros.Page <= 0)
            {
                filtros.Page = 1;
            }

            if (filtros.PageSize <= 0)
            {
                filtros.PageSize = 15;
            }

            filtros.PageSize = Math.Min(filtros.PageSize, 50);

            LogBL.RegistrarInfo("[AOCR_BANDEJA] Inicio GeneradasFirmadas", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] Usuario={Session["Usuario"] ?? User.Identity.Name}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] Roles={Session["RolesRaw"] ?? Session["Roles"] ?? Session["Rol"]}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] EsAdmin={contexto.EsAdministrador}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] CompaniaActiva={contexto.CompaniaActivaCodigo ?? string.Empty}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] FiltroTexto={filtros.Search ?? string.Empty}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] FiltroEstadoFinal={filtros.EstadoFinal ?? string.Empty}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] FiltroEstadoFirma={filtros.EstadoFirma ?? string.Empty}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] FiltroTipoTramite={filtros.TipoTramite ?? string.Empty}", "SolicitudAOCRController");
            LogBL.RegistrarInfo($"[AOCR_BANDEJA] SQLParametros=consulta base sin parametros SQL; filtros aplicados en controlador {JsonConvert.SerializeObject(new { filtros.Search, filtros.EstadoFinal, filtros.EstadoFirma, filtros.TipoTramite, filtros.SoloConPdf })}", "SolicitudAOCRController");

            List<AocrGeneradasFirmadasRowViewModel> visibles;
            int totalBaseSinFiltros = 0;
            int totalDespuesRol = 0;
            int totalDespuesTextoTipo = 0;
            int totalDespuesEstado = 0;
            int totalDespuesPdf = 0;
            string estadosEncontrados = string.Empty;
            string motivoSinRegistros = string.Empty;
            try
            {
                var filas = _aocrBandejaDao.ListarGeneradasFirmadas() ?? new List<AocrBandejaDocumentoRow>();
                totalBaseSinFiltros = filas.Count;
                estadosEncontrados = ResumirEstadosBandeja(filas);

                var filasRol = filas
                    .Where(x => DebeMostrarFilaBandeja(x, contexto))
                    .ToList();

                totalDespuesRol = filasRol.Count;

                var visiblesRol = filasRol
                    .Select(x => MapearFilaBandeja(x, contexto))
                    .ToList();

                var visiblesTextoTipo = AplicarFiltrosBusquedaYTipoBandeja(visiblesRol, filtros);
                totalDespuesTextoTipo = visiblesTextoTipo.Count;

                var visiblesEstado = AplicarFiltrosEstadoBandeja(visiblesTextoTipo, filtros);
                totalDespuesEstado = visiblesEstado.Count;

                visibles = AplicarFiltroPdfBandeja(visiblesEstado, filtros);
                totalDespuesPdf = visibles.Count;
                motivoSinRegistros = ResolverMotivoSinRegistros(totalBaseSinFiltros, totalDespuesRol, totalDespuesTextoTipo, totalDespuesEstado, totalDespuesPdf);

                LogBL.RegistrarInfo($"[AOCR_BANDEJA] TotalBaseSinFiltros={totalBaseSinFiltros}", "SolicitudAOCRController");
                LogBL.RegistrarInfo($"[AOCR_BANDEJA] TotalDespuesRol={totalDespuesRol}", "SolicitudAOCRController");
                LogBL.RegistrarInfo($"[AOCR_BANDEJA] TotalDespuesEstado={totalDespuesEstado}", "SolicitudAOCRController");
                LogBL.RegistrarInfo($"[AOCR_BANDEJA] TotalDespuesPdf={totalDespuesPdf}", "SolicitudAOCRController");
                LogBL.RegistrarInfo($"[AOCR_BANDEJA] TotalFinal={visibles.Count}", "SolicitudAOCRController");
                LogBL.RegistrarInfo($"[AOCR_BANDEJA] EstadosEncontrados={estadosEncontrados}", "SolicitudAOCRController");
                LogBL.RegistrarInfo($"[AOCR_BANDEJA] MotivoSinRegistros={motivoSinRegistros}", "SolicitudAOCRController");

                if (contexto.EsAdministrador && totalBaseSinFiltros > 0 && visibles.Count == 0 && TieneEstadosAocrDetectados(filas))
                {
                    LogBL.RegistrarAdvertencia("[AOCR_BANDEJA] ALERTA: existen estados AOCR detectados pero la bandeja no los muestra.", "SolicitudAOCRController");
                }
            }
            catch (PostgresException ex)
            {
                LogBL.RegistrarError(
                    $"[AOCR_BANDEJA] Error=PostgresException SqlState={ex.SqlState} Usuario={Session["Usuario"] ?? User.Identity.Name}",
                    ex.ToString(),
                    "SolicitudAOCRController");
                TempData["Error"] = "No se pudo cargar la bandeja AOCR. Revise la consulta de datos.";
                return View(CrearModeloBandejaVacio(filtros, contexto));
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    $"[AOCR_BANDEJA] Error={ex.GetType().Name} Usuario={Session["Usuario"] ?? User.Identity.Name}",
                    ex.ToString(),
                    "SolicitudAOCRController");
                TempData["Error"] = "No se pudo cargar la bandeja AOCR. Revise la consulta de datos.";
                return View(CrearModeloBandejaVacio(filtros, contexto));
            }

            var totalRegistros = visibles.Count;
            var totalFirmadas = visibles.Count(x => string.Equals(x.EstadoFinal, "Firmado", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.EstadoFinal, "Finalizado", StringComparison.OrdinalIgnoreCase));
            var totalPendientesFirma = visibles.Count(x => string.Equals(x.EstadoFinal, "Pendiente firma", StringComparison.OrdinalIgnoreCase));
            var totalObservadas = visibles.Count(x => string.Equals(x.EstadoFinal, "Observada", StringComparison.OrdinalIgnoreCase));
            var totalConPdf = visibles.Count(x => x.TienePdfFirmado || x.TienePdfPreliminar);

            var totalPaginas = Math.Max(1, (int)Math.Ceiling(totalRegistros / (double)filtros.PageSize));
            if (filtros.Page > totalPaginas)
            {
                filtros.Page = totalPaginas;
            }

            var itemsPagina = visibles
                .Skip((filtros.Page - 1) * filtros.PageSize)
                .Take(filtros.PageSize)
                .ToList();

            var model = new AocrGeneradasFirmadasViewModel
            {
                Filtros = filtros,
                Items = itemsPagina,
                TotalRegistros = totalRegistros,
                TotalFirmadas = totalFirmadas,
                TotalPendientesFirma = totalPendientesFirma,
                TotalObservadas = totalObservadas,
                TotalConPdf = totalConPdf,
                PaginaActual = filtros.Page,
                TotalPaginas = totalPaginas,
                PageSize = filtros.PageSize,
                EsAdministrador = contexto.EsAdministrador,
                EsSolicitante = contexto.EsSolicitante,
                EsInspector = contexto.EsInspector,
                EsCoordinacion = contexto.EsCoordinacion,
                EsDireccion = contexto.EsDireccion
            };

            ConfigurarEmptyStateBandeja(model, totalBaseSinFiltros, totalDespuesRol, totalDespuesTextoTipo, totalDespuesEstado, totalDespuesPdf);

            model.EstadosFinales = ConstruirOpcionesFiltro(visibles.Select(x => x.EstadoFinal), filtros.EstadoFinal, "Todos los estados finales");
            model.EstadosFirma = ConstruirOpcionesFiltro(visibles.Select(x => x.EstadoFirma), filtros.EstadoFirma, "Todos los estados de firma");
            model.TiposTramite = ConstruirOpcionesFiltro(visibles.Select(x => x.TipoTramite), filtros.TipoTramite, "Todos los trámites");

            LogBL.RegistrarInfo(
                $"[AOCR_BANDEJA] TotalRegistros={totalRegistros} TotalGeneradas={totalConPdf} TotalPendientesFirma={totalPendientesFirma} TotalFirmadas={totalFirmadas}",
                "SolicitudAOCRController");

            return View(model);
        }

        private AocrGeneradasFirmadasViewModel CrearModeloBandejaVacio(
            AocrGeneradasFirmadasFiltroViewModel filtros,
            BandejaAocrContexto contexto)
        {
            filtros = filtros ?? new AocrGeneradasFirmadasFiltroViewModel();
            if (filtros.Page <= 0)
            {
                filtros.Page = 1;
            }

            if (filtros.PageSize <= 0)
            {
                filtros.PageSize = 15;
            }

            filtros.PageSize = Math.Min(filtros.PageSize, 50);

            return new AocrGeneradasFirmadasViewModel
            {
                Filtros = filtros,
                Items = new List<AocrGeneradasFirmadasRowViewModel>(),
                TotalRegistros = 0,
                TotalFirmadas = 0,
                TotalPendientesFirma = 0,
                TotalObservadas = 0,
                TotalConPdf = 0,
                PaginaActual = filtros.Page,
                TotalPaginas = 1,
                PageSize = filtros.PageSize,
                EsAdministrador = contexto.EsAdministrador,
                EsSolicitante = contexto.EsSolicitante,
                EsInspector = contexto.EsInspector,
                EsCoordinacion = contexto.EsCoordinacion,
                EsDireccion = contexto.EsDireccion,
                EmptyStateTitle = "No existen AOCR generadas o firmadas para los criterios seleccionados.",
                EmptyStateMessage = "La bandeja no encontró registros visibles para el rol actual o para los filtros enviados.",
                EstadosFinales = ConstruirOpcionesFiltro(Enumerable.Empty<string>(), filtros.EstadoFinal, "Todos los estados finales"),
                EstadosFirma = ConstruirOpcionesFiltro(Enumerable.Empty<string>(), filtros.EstadoFirma, "Todos los estados de firma"),
                TiposTramite = ConstruirOpcionesFiltro(Enumerable.Empty<string>(), filtros.TipoTramite, "Todos los trámites")
            };
        }

        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
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
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "Aprobar", CodigoSolicitudParameter = "id")]
        public ActionResult Aprobar(string id)
        {
            if (!int.TryParse(id, out int idSolicitud))
                return HttpNotFound();

            var solicitud = _solicitudDAO.ObtenerPorCodigo(idSolicitud);
            if (solicitud == null) return HttpNotFound();

            var validacionChecklist = _revisionDocumentalService.ValidarChecklistParaAprobacion(
                ChecklistDAO.ObtenerEstadisticasPorSolicitud(solicitud.CodigoSolicitud));

            if (!validacionChecklist.EsValido)
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = validacionChecklist.Mensaje;
                return RedirectToAction("RevisarSolicitudes");
            }

            var decisionRevision = _revisionDocumentalService.CrearDecisionRevisionSimple(true, null);

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(idSolicitud, decisionRevision.EstadoDestino, decisionRevision.ObservacionCierre, out mensajeCambio))
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
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Observar(string id, string observacion)
        {
            if (!int.TryParse(id, out int idSolicitud))
                return HttpNotFound();

            var solicitud = _solicitudDAO.ObtenerPorCodigo(idSolicitud);
            if (solicitud == null) return HttpNotFound();

            var decisionRevision = _revisionDocumentalService.CrearDecisionRevisionSimple(false, observacion);

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(idSolicitud, decisionRevision.EstadoDestino, decisionRevision.ObservacionCierre, out mensajeCambio))
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
        [Authorize(Roles = RolesRevisionDocumentalOperativa)]
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

            if (!SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!UsuarioPuedeOperarRevisionDocumental(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo el inspector asignado puede registrar decisiones documentales en esta etapa.";
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
        [Authorize(Roles = RolesRevisionDocumentalOperativa)]
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

            if (!SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!UsuarioPuedeOperarRevisionDocumental(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo el inspector asignado puede ejecutar la revisión documental masiva en esta etapa.";
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

            var revisionesPersistidas = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);

            var revisionesPorDocumento = revisionesPayload
                .Where(x => x != null && x.CodigoDocumento > 0)
                .GroupBy(x => x.CodigoDocumento)
                .ToDictionary(g => g.Key, g => g.First());

            var documentosSinDecision = new List<string>();
            var documentosSinObservacion = new List<string>();
            var hayDevueltosUObservados = false;
            var todosAceptados = true;
            var documentosBloqueadosParaAprobacionMasiva = new List<string>();

            foreach (var doc in documentosRevision)
            {
                RevisionDocumentalMasivaItem revision;
                var decisionActualPersistida = ObtenerDecisionRevisionDocumental(doc, revisionesPersistidas);
                if (!revisionesPorDocumento.TryGetValue(doc.CodigoDocumento, out revision) || revision == null)
                {
                    if (tipoAccionNorm == "APROBAR_TODOS")
                    {
                        revision = new RevisionDocumentalMasivaItem
                        {
                            CodigoDocumento = doc.CodigoDocumento,
                            Decision = string.IsNullOrWhiteSpace(decisionActualPersistida) ? "ACEPTADO" : decisionActualPersistida,
                            Observacion = string.Empty
                        };
                        revisionesPorDocumento[doc.CodigoDocumento] = revision;
                    }
                    else
                    {
                        documentosSinDecision.Add(ObtenerEtiquetaDocumento(doc));
                        todosAceptados = false;
                        continue;
                    }
                }

                var decisionNorm = NormalizarDecisionRevisionDocumental(revision.Decision);
                if (tipoAccionNorm == "APROBAR_TODOS" && string.IsNullOrWhiteSpace(decisionNorm))
                {
                    decisionNorm = "ACEPTADO";
                }

                revision.Decision = decisionNorm;
                revision.Observacion = (revision.Observacion ?? string.Empty).Trim();

                if (decisionNorm != "ACEPTADO" && decisionNorm != "DEVUELTO" && decisionNorm != "OBSERVADO")
                {
                    documentosSinDecision.Add(ObtenerEtiquetaDocumento(doc));
                    todosAceptados = false;
                    continue;
                }

                if (tipoAccionNorm == "APROBAR_TODOS")
                {
                    if (decisionActualPersistida == "DEVUELTO" || decisionActualPersistida == "OBSERVADO"
                        || decisionNorm == "DEVUELTO" || decisionNorm == "OBSERVADO")
                    {
                        documentosBloqueadosParaAprobacionMasiva.Add(ObtenerEtiquetaDocumento(doc));
                        hayDevueltosUObservados = true;
                        todosAceptados = false;
                        continue;
                    }

                    revision.Decision = "ACEPTADO";
                    revision.Observacion = string.Empty;
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

            if (tipoAccionNorm == "APROBAR_TODOS" && documentosBloqueadosParaAprobacionMasiva.Count > 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No se puede aprobar masivamente mientras existan documentos observados o devueltos: "
                    + string.Join(", ", documentosBloqueadosParaAprobacionMasiva) + ".";
                return RedirectToAction("Detalle", new { id });
            }

            if (tipoAccionNorm != "APROBAR_TODOS" && documentosSinDecision.Count > 0)
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
                TempData["NotificacionMensaje"] = "La aprobación masiva solo está disponible cuando no existen documentos observados o devueltos pendientes.";
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

            var observacionBase = tipoAccionNorm == "APROBAR_TODOS"
                ? "Todos los documentos vigentes fueron aceptados por el inspector (acción masiva)."
                : ConstruirResumenRevisionDocumental(documentosRevision, revisionesResumen, true);

            var decisionCierre = _revisionDocumentalService.CrearDecisionCierreMasivo(tipoAccionNorm, observacionBase, observacionCoordinador);

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, decisionCierre.EstadoDestino, decisionCierre.ObservacionCierre, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                id,
                null,
                "REVISION_DOCUMENTAL_FINALIZADA",
                decisionCierre.ObservacionCierre,
                usuarioId,
                usuarioRegistro);

            if (decisionCierre.RequiereNotificarObservaciones)
            {
                try
                {
                    NotificarDocumentosDevueltosInspectorConsolidado(solicitud, documentosRevision, revisionesResumen, usuarioId, usuarioRegistro);
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        id,
                        null,
                        "CORREO_REVISION_FINAL_RESUMEN_ENVIADO",
                        "Correo final de resumen de revision documental con observaciones encolado.",
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
        [Authorize(Roles = RolesRevisionDocumentalOperativa)]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "RevisionDocumental", Accion = "Revisar", CodigoSolicitudParameter = "id")]
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

            if (!SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para cerrar la revisión documental.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!UsuarioPuedeOperarRevisionDocumental(solicitud))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo el inspector asignado puede cerrar la revisión documental en esta etapa.";
                return RedirectToAction("Detalle", new { id });
            }

            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            var validacionCierre = _revisionDocumentalService.ValidarCierreRevisionDocumental(documentosRevision, revisiones);
            if (!validacionCierre.EsValido)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = validacionCierre.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            var decisionCierre = _revisionDocumentalService.CrearDecisionCierreFinal(
                validacionCierre.TieneDocumentosDevueltos,
                ConstruirResumenRevisionDocumental(documentosRevision, revisiones, true));

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, decisionCierre.EstadoDestino, decisionCierre.ObservacionCierre, out mensajeCambio))
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
                decisionCierre.ObservacionCierre,
                usuarioId,
                usuarioRegistro);

            if (decisionCierre.RequiereNotificarObservaciones)
            {
                try
                {
                    NotificarDocumentosDevueltosInspectorConsolidado(solicitud, documentosRevision, revisiones, usuarioId, usuarioRegistro);
                    _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                        id,
                        null,
                        "CORREO_REVISION_FINAL_RESUMEN_ENVIADO",
                        "Correo final de resumen de revision documental con observaciones encolado.",
                        usuarioId,
                        usuarioRegistro);
                }
                catch
                {
                    // El correo es auxiliar; no bloquea el cierre de la revisión.
                }
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = validacionCierre.TieneDocumentosDevueltos
                ? "La revisión documental fue cerrada y la solicitud se devolvió al operador con observaciones."
                : "La revisión documental fue cerrada y la solicitud avanzó a Aceptación Documental.";

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = RolesRevisionDocumentalOperativa)]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "RevisionDocumental", Accion = "Revisar", CodigoSolicitudParameter = "id")]
        public ActionResult GuardarRevisionDocumental(int id, string revisionesJson, string returnUrl = null)
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

            if (!SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[REV_DOC][GUARDAR_403] SolicitudId=" + id +
                    "; UsuarioId=" + ObtenerUsuarioActualId() +
                    "; Login=" + (User != null && User.Identity != null ? User.Identity.Name : string.Empty) +
                    "; RolActivo=" + (Session["Rol"] ?? string.Empty) +
                    "; EstadoSolicitud=" + (solicitud.Estado ?? string.Empty) +
                    "; Modo=revision" +
                    "; InspectorAsignadoRaw=" +
                    "; EsInspectorAsignado=False" +
                    "; RevisionActiva=" + SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado) +
                    "; PuedeGuardar=False" +
                    "; Motivo=La solicitud no está en estado válido para revisión documental.");
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La solicitud no se encuentra en una etapa habilitada para revisión documental.";
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    403,
                    "La solicitud no se encuentra en estado valido para revision documental.",
                    "warning");
            }

            var usuarioRevisionId = ObtenerUsuarioActualId();
            var inspeccionesRevision = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            var identidadRevision = ConstruirIdentidadInspectorActual(usuarioRevisionId);
            var evaluacionRevision = _inspectorIdentityService.EvaluarInspectorAsignado(
                solicitud.CodigoSolicitud,
                solicitud,
                inspeccionesRevision,
                new InspectorIdentityInfo
                {
                    Ids = identidadRevision != null ? identidadRevision.Ids : new HashSet<int>(),
                    Identificadores = identidadRevision != null
                        ? identidadRevision.Identificadores
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                });
            var puedeOperarRevision = UsuarioPuedeOperarRevisionDocumental(solicitud);
            System.Diagnostics.Trace.TraceInformation(
                "[REV_DOC][AUTH_CHECK] SolicitudId=" + id +
                "; UsuarioId=" + usuarioRevisionId +
                "; Login=" + (User != null && User.Identity != null ? User.Identity.Name : string.Empty) +
                "; RolActivo=" + (Session["Rol"] ?? string.Empty) +
                "; InspectorAsignadoRaw=" + (evaluacionRevision != null && evaluacionRevision.Asignado != null ? evaluacionRevision.Asignado.InspectorAsignadoRaw : string.Empty) +
                "; InspectorAsignadoUsuarioId=" + (evaluacionRevision != null && evaluacionRevision.Asignado != null ? evaluacionRevision.Asignado.InspectorAsignadoUsuarioId : string.Empty) +
                "; EstadoSolicitud=" + (solicitud.Estado ?? string.Empty) +
                "; Modo=revision" +
                "; PuedeGuardar=" + puedeOperarRevision +
                "; Motivo=" + (evaluacionRevision != null ? evaluacionRevision.Motivo : string.Empty));

            if (!puedeOperarRevision)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "[REV_DOC][GUARDAR_403] SolicitudId=" + id +
                    "; UsuarioId=" + usuarioRevisionId +
                    "; Login=" + (User != null && User.Identity != null ? User.Identity.Name : string.Empty) +
                    "; RolActivo=" + (Session["Rol"] ?? string.Empty) +
                    "; EstadoSolicitud=" + (solicitud.Estado ?? string.Empty) +
                    "; Modo=revision" +
                    "; InspectorAsignadoRaw=" + (evaluacionRevision != null && evaluacionRevision.Asignado != null ? evaluacionRevision.Asignado.InspectorAsignadoRaw : string.Empty) +
                    "; EsInspectorAsignado=" + (evaluacionRevision != null && evaluacionRevision.EsInspectorAsignado) +
                    "; RevisionActiva=" + SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado) +
                    "; PuedeGuardar=" + puedeOperarRevision +
                    "; Motivo=" + (evaluacionRevision != null ? evaluacionRevision.Motivo : "La solicitud no está asignada al inspector autenticado."));
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "Solo el inspector asignado puede guardar la revisión documental en esta etapa.";
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    403,
                    evaluacionRevision != null && !string.IsNullOrWhiteSpace(evaluacionRevision.Motivo)
                        ? evaluacionRevision.Motivo
                        : "La solicitud no esta asignada a su usuario inspector.",
                    "warning",
                    0,
                    string.Empty,
                    evaluacionRevision != null && evaluacionRevision.EsInspectorAsignado,
                    evaluacionRevision != null && evaluacionRevision.Asignado != null ? evaluacionRevision.Asignado.InspectorAsignadoRaw : string.Empty);
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
                TempData["NotificacionMensaje"] = "No fue posible leer las decisiones de revisión documental.";
                return RedirectGuardarRevisionDocumental(id, returnUrl);
            }

            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            if (documentosRevision.Count == 0)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "No existen documentos vigentes para revisión documental.";
                return RedirectGuardarRevisionDocumental(id, returnUrl);
            }

            var revisionesPorDocumento = revisionesPayload
                .Where(x => x != null && x.CodigoDocumento > 0)
                .GroupBy(x => x.CodigoDocumento)
                .ToDictionary(g => g.Key, g => g.First());

            if (revisionesPorDocumento.Count == 0)
            {
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    400,
                    "No se recibieron decisiones documentales validas.",
                    "warning");
            }

            var documentosRevisionPorId = documentosRevision
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToDictionary(d => d.CodigoDocumento, d => d);

            var documentosNoEncontrados = revisionesPorDocumento.Keys
                .Where(documentoId => !documentosRevisionPorId.ContainsKey(documentoId))
                .ToList();
            if (documentosNoEncontrados.Count > 0)
            {
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    400,
                    "Uno o mas documentos enviados no pertenecen a la solicitud o ya no son vigentes.",
                    "warning");
            }

            var documentosOperacion = revisionesPorDocumento.Keys
                .Select(documentoId => documentosRevisionPorId[documentoId])
                .ToList();

            var documentosNoRevisables = documentosOperacion
                .Where(d => !EstadoDocumentoInstitucional.EsEstadoRevisablePorInspector(d.Estado))
                .Select(ObtenerEtiquetaDocumento)
                .ToList();
            if (documentosNoRevisables.Count > 0)
            {
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    403,
                    "Los documentos enviados no estan en estado revisable: " + string.Join(", ", documentosNoRevisables) + ".",
                    "warning",
                    documentosOperacion.Count,
                    string.Join(", ", documentosOperacion.Select(d => (d.Estado ?? string.Empty).Trim()).Distinct(StringComparer.OrdinalIgnoreCase)),
                    evaluacionRevision != null && evaluacionRevision.EsInspectorAsignado,
                    evaluacionRevision != null && evaluacionRevision.Asignado != null ? evaluacionRevision.Asignado.InspectorAsignadoRaw : string.Empty);
            }

            var documentosSinDecision = new List<string>();
            var documentosSinObservacion = new List<string>();

            foreach (var doc in documentosOperacion)
            {
                RevisionDocumentalMasivaItem revision;
                if (!revisionesPorDocumento.TryGetValue(doc.CodigoDocumento, out revision) || revision == null)
                {
                    documentosSinDecision.Add(ObtenerEtiquetaDocumento(doc));
                    continue;
                }

                var decisionNorm = NormalizarDecisionRevisionDocumental(revision.Decision);
                revision.Decision = decisionNorm;
                revision.Observacion = (revision.Observacion ?? string.Empty).Trim();

                if (decisionNorm != "ACEPTADO" && decisionNorm != "DEVUELTO" && decisionNorm != "OBSERVADO")
                {
                    documentosSinDecision.Add(ObtenerEtiquetaDocumento(doc));
                    continue;
                }

                if (DecisionRevisionRequiereObservacion(decisionNorm) && string.IsNullOrWhiteSpace(revision.Observacion))
                {
                    documentosSinObservacion.Add(ObtenerEtiquetaDocumento(doc));
                }
            }

            if (documentosSinDecision.Count > 0)
            {
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    400,
                    "No se puede guardar la revision documental. Faltan decisiones en: " + string.Join(", ", documentosSinDecision) + ".",
                    "warning");
            }

            if (documentosSinObservacion.Count > 0)
            {
                return ResponderGuardarRevisionDocumentalError(
                    id,
                    returnUrl,
                    400,
                    "No se puede guardar la revision documental. Debe registrar observacion en: " + string.Join(", ", documentosSinObservacion) + ".",
                    "warning");
            }

            var usuarioId = ObtenerUsuarioActualId();
            var usuarioRegistro = (Session["CodigoUsuario"] ?? User.Identity.Name ?? "sistema").ToString();

            foreach (var doc in documentosOperacion)
            {
                var revision = revisionesPorDocumento[doc.CodigoDocumento];
                var decisionNorm = NormalizarDecisionRevisionDocumental(revision.Decision);
                var estadoDocumento = EstadoDocumentoInstitucional.ResolverEstadoTrasDecisionInspector(decisionNorm);
                var estadoAnterior = EstadoDocumentoInstitucional.Normalizar(doc.Estado);

                doc.Estado = estadoDocumento == EstadoDocumentoInstitucional.Aceptado ? "APROBADO" : estadoDocumento;
                doc.Validado = decisionNorm == "ACEPTADO";
                doc.Observaciones = decisionNorm == "ACEPTADO" ? null : revision.Observacion;
                doc.FechaValidacion = DateTime.Now;
                doc.ValidadoPor = usuarioRegistro;
                doc.UsuarioRegistro = usuarioRegistro;

                System.Diagnostics.Trace.TraceInformation(
                    "[REV_DOC][GUARDAR_DECISION] SolicitudId=" + id +
                    "; DocumentoId=" + doc.CodigoDocumento +
                    "; EstadoDocumentoAnterior=" + estadoAnterior +
                    "; Decision=" + decisionNorm +
                    "; EstadoDocumentoNuevo=" + doc.Estado +
                    "; UsuarioInspector=" + usuarioId);

                if (!_documentoDAO.Actualizar(doc))
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = "No se pudo registrar la revisión documental para todos los documentos.";
                    return RedirectGuardarRevisionDocumental(id, returnUrl);
                }

                _solicitudAocrInfraBL.RegistrarRevisionDocumental(
                    id,
                    doc.CodigoDocumento,
                    decisionNorm == "DEVUELTO" ? "DEVUELTO" : decisionNorm,
                    revision.Observacion,
                    usuarioId,
                    usuarioRegistro);
                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    id,
                    doc.CodigoDocumento,
                    decisionNorm == "ACEPTADO" ? "DOCUMENTO_ACEPTADO" : "DOCUMENTO_DEVUELTO",
                    "Documento " + ObtenerEtiquetaDocumento(doc) + " marcado como " + decisionNorm + ". " + revision.Observacion,
                    usuarioId,
                    usuarioRegistro);
            }

            var revisionesResumen = documentosRevision
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToDictionary(
                    d => d.CodigoDocumento,
                    d =>
                    {
                        RevisionDocumentalMasivaItem revisionPayload;
                        if (revisionesPorDocumento.TryGetValue(d.CodigoDocumento, out revisionPayload) && revisionPayload != null)
                        {
                            return Tuple.Create(
                                NormalizarDecisionRevisionDocumental(revisionPayload.Decision),
                                (revisionPayload.Observacion ?? string.Empty).Trim());
                        }

                        return Tuple.Create(
                            ObtenerDecisionRevisionDocumental(d, null),
                            ObtenerObservacionRevisionDocumental(d, null));
                    });

            var validacionCierre = _revisionDocumentalService.ValidarCierreRevisionDocumental(documentosRevision, revisionesResumen);
            if (!validacionCierre.EsValido)
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = validacionCierre.Mensaje;
                return RedirectGuardarRevisionDocumental(id, returnUrl);
            }

            var decisionCierre = _revisionDocumentalService.CrearDecisionCierreFinal(
                validacionCierre.TieneDocumentosDevueltos,
                ConstruirResumenRevisionDocumental(documentosRevision, revisionesResumen, true));

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, decisionCierre.EstadoDestino, decisionCierre.ObservacionCierre, out mensajeCambio))
            {
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectGuardarRevisionDocumental(id, returnUrl);
            }

            _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                id,
                null,
                "REVISION_DOCUMENTAL_FINALIZADA",
                decisionCierre.ObservacionCierre,
                usuarioId,
                usuarioRegistro);

            if (decisionCierre.RequiereNotificarObservaciones)
            {
                try
                {
                    NotificarDocumentosDevueltosInspectorConsolidado(solicitud, documentosRevision, revisionesResumen, usuarioId, usuarioRegistro);
                }
                catch
                {
                }
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = validacionCierre.TieneDocumentosDevueltos
                ? "La revisión documental fue guardada y la solicitud se devolvió al RT con observaciones."
                : "La revisión documental fue guardada y la solicitud avanzó a Aceptación Documental.";

            var resumenFinal = _solicitudAocrInfraBL.ObtenerEstadoRevisionDocumental(id) ?? new EstadoRevisionDocumental();
            System.Diagnostics.Trace.TraceInformation(
                "[REV_DOC][GUARDAR_OK] SolicitudId=" + id +
                "; Aceptados=" + resumenFinal.DocumentosAceptados +
                "; Devueltos=" + resumenFinal.DocumentosObservadosDevueltos +
                "; Pendientes=" + resumenFinal.DocumentosPendientesRevision);

            if (Request != null && Request.IsAjaxRequest())
            {
                return Json(new
                {
                    ok = true,
                    success = true,
                    message = TempData["NotificacionMensaje"],
                    redirectUrl = ResolverUrlGuardarRevisionDocumental(id, returnUrl)
                });
            }

            return RedirectGuardarRevisionDocumental(id, returnUrl);
        }

        private ActionResult RedirectGuardarRevisionDocumental(int id, string returnUrl)
        {
            return Redirect(ResolverUrlGuardarRevisionDocumental(id, returnUrl));
        }

        private string ResolverUrlGuardarRevisionDocumental(int id, string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return returnUrl;
            }

            return Url.Action("Lista", "Documento", new { solicitudId = id, modo = "revision" });
        }

        private ActionResult ResponderGuardarRevisionDocumentalError(
            int id,
            string returnUrl,
            int code,
            string message,
            string notificationType,
            int documentosRecibidos = 0,
            string estadosDocumentos = null,
            bool? esInspectorAsignado = null,
            string inspectorAsignadoRaw = null)
        {
            var mensaje = string.IsNullOrWhiteSpace(message)
                ? "No se pudo guardar la revision documental."
                : message.Trim();

            TempData["NotificacionTipo"] = string.IsNullOrWhiteSpace(notificationType) ? "warning" : notificationType;
            TempData["NotificacionMensaje"] = mensaje;

            if (code == 403)
            {
                var estadoSolicitudLog = string.Empty;
                var revisionActivaLog = false;
                try
                {
                    var solicitudLog = id > 0 ? _solicitudDAO.ObtenerPorId(id) : null;
                    estadoSolicitudLog = solicitudLog != null ? (solicitudLog.Estado ?? string.Empty) : string.Empty;
                    revisionActivaLog = SolicitudEstaEnEtapaRevisionDocumental(estadoSolicitudLog);
                }
                catch
                {
                }

                System.Diagnostics.Trace.TraceWarning(
                    "[REV_DOC][GUARDAR_403] SolicitudId=" + id +
                    "; UsuarioId=" + ObtenerUsuarioActualId() +
                    "; Login=" + (User != null && User.Identity != null ? User.Identity.Name : string.Empty) +
                    "; RolActivo=" + (Session["Rol"] ?? string.Empty) +
                    "; EstadoSolicitud=" + estadoSolicitudLog +
                    "; Modo=" + (Request != null ? (Request["modo"] ?? "revision") : "revision") +
                    "; Origen=" + (Request != null ? (Request["origen"] ?? string.Empty) : string.Empty) +
                    "; InspectorAsignadoRaw=" + (inspectorAsignadoRaw ?? string.Empty) +
                    "; EsInspectorAsignado=" + (esInspectorAsignado.HasValue ? esInspectorAsignado.Value.ToString() : "False") +
                    "; RevisionActiva=" + revisionActivaLog +
                    "; SoloLectura=False" +
                    "; PuedeGuardarRevision=False" +
                    "; DocumentosRecibidos=" + documentosRecibidos +
                    "; EstadosDocumentos=" + (estadosDocumentos ?? string.Empty) +
                    "; Motivo=" + mensaje);
            }

            if (Request != null && Request.IsAjaxRequest())
            {
                Response.StatusCode = code;
                Response.TrySkipIisCustomErrors = true;
                Response.SuppressFormsAuthenticationRedirect = true;
                return Json(new
                {
                    ok = false,
                    success = false,
                    code,
                    message = mensaje,
                    redirectUrl = ResolverUrlGuardarRevisionDocumental(id, returnUrl)
                });
            }

            return RedirectGuardarRevisionDocumental(id, returnUrl);
        }

        private DocumentoSubsanacionVM MapearDocumentoSubsanacionVm(
            Documento documento,
            IDictionary<int, Tuple<string, string>> revisiones,
            bool puedeSubsanar)
        {
            if (documento == null)
            {
                return new DocumentoSubsanacionVM();
            }

            var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
            var observacion = ObtenerObservacionRevisionDocumental(documento, revisiones);
            var estadoVisible = !string.IsNullOrWhiteSpace(decision)
                ? decision
                : EstadoDocumentoInstitucional.Normalizar(documento.Estado);

            return new DocumentoSubsanacionVM
            {
                CodigoDocumento = documento.CodigoDocumento,
                TipoDocumento = !string.IsNullOrWhiteSpace(documento.TipoDocumentoNombre)
                    ? documento.TipoDocumentoNombre
                    : documento.TipoDocumento,
                NombreArchivo = documento.NombreArchivo,
                Estado = estadoVisible,
                Observaciones = observacion,
                FechaCarga = documento.FechaCarga,
                Version = documento.Version,
                PuedeSubsanar = puedeSubsanar,
                EsBloqueado = !puedeSubsanar
            };
        }

        private void NotificarDocumentosDevueltosInspectorConsolidado(
            SolicitudAOCR solicitud,
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones,
            int usuarioId,
            string usuarioRegistro)
        {
            if (solicitud == null)
            {
                return;
            }

            var itemsDevueltos = (documentos ?? Enumerable.Empty<Documento>())
                .Select(d => new
                {
                    Documento = d,
                    Decision = ObtenerDecisionRevisionDocumental(d, revisiones),
                    Observacion = ObtenerObservacionRevisionDocumental(d, revisiones)
                })
                .Where(x => x.Decision == "DEVUELTO" || x.Decision == "OBSERVADO")
                .Select(x => new DocumentoDevueltoNotificacionItem
                {
                    CodigoDocumento = x.Documento.CodigoDocumento,
                    Etiqueta = ObtenerEtiquetaDocumento(x.Documento),
                    Observacion = x.Observacion
                })
                .ToList();

            if (itemsDevueltos.Count == 0)
            {
                return;
            }

            var correlationId = _documentoSubsanacionService.ConstruirEventKeyDocumentosDevueltos(
                solicitud.CodigoSolicitud,
                itemsDevueltos.Select(x => x.CodigoDocumento));

            var urlSistema = Url.Action("Subsanar", "SolicitudAOCR", new { id = solicitud.CodigoSolicitud }, Request.Url.Scheme);
            var inspector = FirstNonEmpty(solicitud.TecnicoResponsableNombre, ObtenerNombreInspector(solicitud), "Inspector asignado");

            _documentoSubsanacionService.EncolarCorreoDocumentosDevueltosInspector(
                solicitud,
                itemsDevueltos,
                inspector,
                urlSistema,
                correlationId);

            _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                solicitud.CodigoSolicitud,
                null,
                "CORREO_DOCUMENTOS_DEVUELTOS_INSPECTOR_ENVIADO",
                "Correo consolidado de " + itemsDevueltos.Count + " documento(s) devuelto(s) encolado para el RT.",
                usuarioId,
                usuarioRegistro);
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
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "AprobarJefatura", CodigoSolicitudParameter = "id")]
        public ActionResult AprobarPorJefatura(int id)
        {
            RegistrarTrazaAocrCoordinacion(id, aocrGenerada: _generacionAocrService.ObtenerAocrGeneradoVigente(id));

            var decision = _aocrFinalWorkflowService.CrearDecisionAprobacionJefatura();
            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, decision.EstadoDestino, decision.ObservacionEstado, out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
                return RedirectToAction("RevisarPorJefatura");
            }

            TempData["Exito"] = "La solicitud ha sido validada institucionalmente.";
            return RedirectToAction("RevisarPorJefatura");
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult ObservarPorJefatura(int id, string observaciones)
        {
            var decision = _aocrFinalWorkflowService.CrearDecisionObservacionJefatura(observaciones);
            if (!decision.EsValida)
            {
                TempData["Error"] = decision.MensajeValidacion;
                return RedirectToAction("Detalle", new { id });
            }

            RegistrarTrazaAocrCoordinacion(
                id,
                motivoBloqueo: decision.ObservacionEstado,
                aocrGenerada: _generacionAocrService.ObtenerAocrGeneradoVigente(id));

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, decision.EstadoDestino, decision.ObservacionEstado, out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["Exito"] = "La AOCR fue devuelta al Inspector para corrección.";
            return RedirectToAction("Detalle", new { id });
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
            var documentos = ObtenerDocumentosPendientesSubsanacionParaSolicitud(id, revisionesDocumentales);
            var documentosVigentes = ObtenerDocumentosVigentesParaRevision(id);
            var clasificacion = _documentoSubsanacionService.ClasificarDocumentosParaRt(
                documentosVigentes,
                revisionesDocumentales,
                estadoActual);
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
                DocumentosObservados = documentos.Select(d => MapearDocumentoSubsanacionVm(d, revisionesDocumentales, true)).ToList(),
                DocumentosBloqueados = clasificacion.DocumentosBloqueados
                    .Select(d => MapearDocumentoSubsanacionVm(d, revisionesDocumentales, false))
                    .ToList()
            };

            return View(vm);
        }

        // =========================================================
        // POST: SubsanarPost — Procesar corrección de documentos
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Operador,RepresentanteTecnico,RepresentanteLegal,RT,Administrador")]
        public ActionResult EnviarSubsanacionAlInspector(int codigoSolicitud, string comentario)
        {
            return SubsanarPost(codigoSolicitud, comentario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Operador,RepresentanteTecnico,RepresentanteLegal,RT,Administrador")]
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
                var documentosObservadosPendientes = ObtenerDocumentosPendientesSubsanacionParaSolicitud(codigoSolicitud, revisionesDocumentales);

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

                LogBL.RegistrarInfo(
                    "[SubsanarPost] Archivos recibidos para subsanacion. SolicitudId=" + codigoSolicitud
                    + ", UsuarioId=" + usuarioId
                    + ", Usuario=" + usuarioRegistro
                    + ", DocumentosPendientes=" + documentosObservadosPendientes.Count
                    + ", DocumentosConArchivo=" + archivosPorDocumento.Count
                    + ", CantidadArchivos=" + archivosPorDocumento.Sum(x => x.Value != null ? x.Value.Count : 0),
                    "SolicitudAOCRController",
                    usuarioId > 0 ? (int?)usuarioId : null);

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
                    var validacionSubsanacion = _documentoSubsanacionService.ValidarCargaSubsanacionRt(
                        docOriginal,
                        revisionesDocumentales,
                        estadoActual,
                        true);

                    if (!validacionSubsanacion.EsValido)
                    {
                        TempData["NotificacionTipo"] = "error";
                        TempData["NotificacionMensaje"] = validacionSubsanacion.Mensaje;
                        return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                    }

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
                            NombreArchivo = Path.GetFileName(file.FileName),
                            NombreArchivoOriginal = Path.GetFileName(file.FileName),
                            NombreArchivoVisible = Path.GetFileName(file.FileName),
                            NombreArchivoFisico = result.StoredName,
                            NombreArchivoGuardado = result.StoredName,
                            RutaGuardada = rutaRelativa,
                            Extension = extension,
                            TamanoBytes = file.ContentLength,
                            Estado = EstadoDocumentoInstitucional.SubsanadoRt,
                            Validado = false,
                            FechaCarga = DateTime.Now,
                            Observaciones = "Subsanación: " + (comentario ?? "").Trim(),
                            Version = versionAnterior + 1,
                            UsuarioRegistro = usuarioRegistro
                        };

                        var codigoNuevoDocumento = _documentoDAO.Crear(nuevoDoc);
                        nuevoDoc.CodigoDocumento = codigoNuevoDocumento;

                        docOriginal.Estado = EstadoDocumentoInstitucional.ResolverEstadoVersionAnterior();
                        docOriginal.Observaciones = string.IsNullOrWhiteSpace(docOriginal.Observaciones)
                            ? "Versión anterior conservada por subsanación RT."
                            : docOriginal.Observaciones.Trim();
                        _documentoDAO.Actualizar(docOriginal);

                        documentosSubsanadosNotificacion.Add(nuevoDoc);
                        LogBL.RegistrarInfo(
                            "[SubsanarPost] Documento subsanado registrado como nueva version. SolicitudId=" + codigoSolicitud
                            + ", DocumentoOriginalId=" + docOriginal.CodigoDocumento
                            + ", DocumentoNuevoId=" + codigoNuevoDocumento
                            + ", TipoDocumento=" + (tipoDoc ?? string.Empty)
                            + ", VersionAnterior=" + versionAnterior
                            + ", VersionNueva=" + nuevoDoc.Version
                            + ", NombreOriginal=" + (file.FileName ?? string.Empty)
                            + ", NombreFisico=" + (result.StoredName ?? string.Empty)
                            + ", Ruta=" + (rutaRelativa ?? string.Empty)
                            + ", Bytes=" + file.ContentLength
                            + ", Accion=AGREGAR_VERSION",
                            "SolicitudAOCRController",
                            usuarioId > 0 ? (int?)usuarioId : null);
                        _solicitudAocrInfraBL.RegistrarRevisionDocumental(
                            codigoSolicitud,
                            codigoNuevoDocumento,
                            EstadoDocumentoInstitucional.SubsanadoRt,
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

                var resultadoFlujo = _revisionDocumentalService.EnviarSubsanacionAlInspector(
                    codigoSolicitud,
                    usuarioId,
                    comentario,
                    usuarioRegistro);
                if (!resultadoFlujo.Ok)
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = string.IsNullOrWhiteSpace(resultadoFlujo.Mensaje)
                        ? "No fue posible enviar la subsanación al Inspector."
                        : resultadoFlujo.Mensaje;
                    return RedirectToAction("Subsanar", new { id = codigoSolicitud });
                }

                solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud) ?? solicitud;
                NotificarInspectorDocumentacionSubsanada(
                    solicitud,
                    documentosSubsanadosNotificacion,
                    comentario,
                    usuarioId,
                    usuarioRegistro,
                    resultadoFlujo.CodigoHistorialEstado);

                TempData["NotificacionTipo"] = "success";
                TempData["NotificacionMensaje"] = "La subsanación fue enviada correctamente al Inspector. Se subieron " + archivosSubidos + " documento(s).";
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
            string usuarioRegistro,
            int? codigoRevisionFlujo)
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
                var revisionToken = codigoRevisionFlujo.HasValue && codigoRevisionFlujo.Value > 0
                    ? codigoRevisionFlujo.Value.ToString()
                    : DateTime.Now.ToString("yyyyMMddHHmmss");
                var eventKey = "SUBSANACION_RT_ENVIADA_INSPECTOR_" + solicitud.CodigoSolicitud + "_" + revisionToken;

                NotificacionBL.EnviarNotificacion(
                    codigoInspector,
                    "Subsanación documental recibida",
                    "El Representante Técnico envió la subsanación documental de la Solicitud AOCR " + numeroSolicitud + ".",
                    "INFO",
                    Url.Action("Detalle", "SolicitudAOCR", new { id = solicitud.CodigoSolicitud }),
                    "AOCR",
                    solicitud.CodigoSolicitud,
                    "SOLICITUD_AOCR");

                if (!string.IsNullOrWhiteSpace(correoInspector))
                {
                    var asunto = "AOCR - Subsanación documental recibida para revisión";
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
                        TipoNotificacion = "SUBSANACION_RT_ENVIADA_INSPECTOR",
                        SolicitudId = solicitud.CodigoSolicitud,
                        EventKey = eventKey,
                        Remitente = "no_reply@aviacioncivil.gob.ec",
                        AliasRemitente = "Sistema AOCR",
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

            return "Estimado/a Inspector:<br><br>" +
                   "El Representante Técnico " + HttpUtility.HtmlEncode(nombreRt) +
                   " ha registrado la subsanación documental de la solicitud " + HttpUtility.HtmlEncode(numeroSolicitud) + ".<br><br>" +
                   "La solicitud se encuentra nuevamente disponible en su bandeja para revisión técnica documental.<br><br>" +
                   "<strong>Operadora:</strong> " + HttpUtility.HtmlEncode(operadora) + "<br>" +
                   "<strong>Documentos subsanados:</strong> " + (documentos ?? Enumerable.Empty<Documento>()).Count() + "<br>" +
                   "<strong>Detalle de documentos:</strong><ul>" + lista + "</ul>" +
                   "<strong>Fecha de subsanación:</strong> " + fechaSubsanacion.ToString("dd/MM/yyyy HH:mm") + "<br>" +
                   "<strong>Comentario RT:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(comentarioRt) ? "Sin comentario adicional." : comentarioRt.Trim()) + "<br><br>" +
                   "Sistema AOCR<br>Dirección General de Aviación Civil";
        }

        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "Detalle", CodigoSolicitudParameter = "id")]
        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            var procesoCerradoOperativamente = SolicitudEstaCerradaOperativamente(solicitud);
            var documentosObligatoriosFaltantes = procesoCerradoOperativamente
                || string.Equals(estadoActual, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase)
                ? new List<string>()
                : ObtenerDocumentosObligatoriosFaltantes(id, null, solicitud.TipoSolicitud);

            ViewBag.HistorialEstados = _solicitudAocrInfraBL.ObtenerHistorialEstadosPorSolicitud(id);
            ViewBag.UsuarioActualId = ObtenerUsuarioActualId();
            ViewBag.ProcesoCerradoOperativamente = procesoCerradoOperativamente;
            ViewBag.DocumentosObligatoriosFaltantes = documentosObligatoriosFaltantes;

            IList<Inspeccion> inspeccionesSolicitud;
            try
            {
                inspeccionesSolicitud = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(id) ?? new List<Inspeccion>();
            }
            catch
            {
                inspeccionesSolicitud = new List<Inspeccion>();
            }

            EnriquecerNombresInspectoresDetalle(solicitud, inspeccionesSolicitud);

            ViewBag.InspeccionesSolicitud = inspeccionesSolicitud;

            ViewBag.AsignacionActiva = _solicitudAocrInfraBL.ObtenerAsignacionActiva(id);
            ViewBag.HistorialAsignaciones = _solicitudAocrInfraBL.ObtenerHistorialAsignacion(id);
            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            var revisionesDocumentales = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            EnriquecerDocumentosRevisionDocumental(documentosRevision, solicitud, revisionesDocumentales, inspeccionesSolicitud);
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
                var contextoAocr = CrearContextoAutorizacionAocr();
                var dispAocr = _generacionAocrService.Evaluar(id, ObtenerUsuarioActualId(), contextoAocr.Roles);
                RegistrarTrazaGeneracionAocr("SolicitudDetalle", dispAocr);
                ViewBag.PuedeGenerarAOCR = dispAocr != null && dispAocr.Habilitado;
                ViewBag.MotivoGenerarAOCR = dispAocr != null
                    ? dispAocr.Motivo
                    : "La AOCR estará disponible cuando el Informe Técnico quede aprobado por Dirección/DIRDAC y la solicitud entre en fase AOCR.";
                ViewBag.DocumentoAOCRGenerado = dispAocr != null ? dispAocr.DocumentoGenerado : null;
                ViewBag.AocrYaGenerado = dispAocr != null && dispAocr.YaGenerado;
            }
            catch
            {
                ViewBag.PuedeGenerarAOCR = false;
                ViewBag.MotivoGenerarAOCR = "La AOCR estará disponible cuando el Informe Técnico quede aprobado por Dirección/DIRDAC y la solicitud entre en fase AOCR.";
                ViewBag.DocumentoAOCRGenerado = null;
                ViewBag.AocrYaGenerado = false;
            }

            try
            {
                var authContext = CrearContextoAutorizacionAocr();
                ViewBag.Flujo = SolicitudAocrFlujoViewModelBuilder.Construir(
                    solicitud,
                    authContext,
                    procesoCerradoOperativamente,
                    (bool)(ViewBag.PuedeGenerarAOCR ?? false),
                    ViewBag.MotivoGenerarAOCR as string,
                    inspeccionesSolicitud);
            }
            catch
            {
                ViewBag.Flujo = new SolicitudAocrFlujoViewModel();
            }

            return View(solicitud);
        }

        [HttpPost]
        [AocrAuthorize(Modulo = "CoordinacionJefatura", Accion = "FirmarAceptacionDocumental", CodigoSolicitudParameter = "id")]
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarAceptacionDocumental(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                _logger.LogWarning("[FirmarAceptacionDocumental] Solicitud inexistente. SolicitudId=" + id);
                return HttpNotFound();
            }

            var usuarioFirmante = ObtenerUsuarioActualId();
            var estadoAnteriorRaw = solicitud.Estado ?? string.Empty;
            var numeroSolicitud = solicitud.NumeroSolicitud ?? string.Empty;
            _logger.LogInfo(
                "[FirmarAceptacionDocumental] Inicio. SolicitudId=" + id +
                ", NumeroSolicitud=" + numeroSolicitud +
                ", EstadoAnteriorRaw=" + estadoAnteriorRaw +
                ", EstadoAnteriorNorm=" + EstadoSolicitud.Normalizar(estadoAnteriorRaw) +
                ", UsuarioFirmante=" + usuarioFirmante +
                ", TipoSolicitud=" + (solicitud.TipoSolicitud.HasValue ? solicitud.TipoSolicitud.Value.ToString() : "N/A"));

            ActionResult redireccionProcesoCerrado;
            if (TryRedirigirSiProcesoCerrado(solicitud, id, out redireccionProcesoCerrado))
            {
                _logger.LogWarning("[FirmarAceptacionDocumental] Bloqueado por proceso cerrado. SolicitudId=" + id + ", Estado=" + estadoAnteriorRaw);
                return redireccionProcesoCerrado;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            var revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
            var documentosRevision = ObtenerDocumentosVigentesParaRevision(id);
            var usuarioRegistroFirmante = (Session["CodigoUsuario"] ?? User.Identity.Name ?? "sistema").ToString();
            var documentosAutocompletados = RegistrarAceptacionesPendientesParaRevisionFinal(
                id,
                documentosRevision,
                revisiones,
                usuarioFirmante,
                usuarioRegistroFirmante);
            if (documentosAutocompletados > 0)
            {
                revisiones = _solicitudAocrInfraBL.ObtenerUltimasRevisionesPorSolicitud(id);
                _logger.LogInfo(
                    "[FirmarAceptacionDocumental] Revisiones faltantes autocompletadas. SolicitudId=" + id +
                    ", DocumentosAutocompletados=" + documentosAutocompletados +
                    ", UsuarioFirmante=" + usuarioFirmante);
            }

            var firmaPlan = _revisionDocumentalService.PrepararFirmaAceptacionDocumental(
                estadoActual,
                documentosRevision,
                revisiones,
                observacion,
                solicitud.TipoSolicitud);
            var documentosAceptados = documentosRevision.Count(d => d != null && d.CodigoDocumento > 0 && ObtenerDecisionRevisionDocumentalLog(d, revisiones) == "ACEPTADO");
            _logger.LogInfo(
                "[FirmarAceptacionDocumental] Validacion. SolicitudId=" + id +
                ", TotalDocumentosRevision=" + documentosRevision.Count +
                ", DocumentosAceptados=" + documentosAceptados +
                ", EsValido=" + firmaPlan.EsValido +
                ", EstadoDestino=" + (firmaPlan.EstadoDestino ?? string.Empty) +
                ", Mensaje=" + (firmaPlan.Mensaje ?? string.Empty));
            if (!firmaPlan.EsValido)
            {
                _logger.LogWarning("[FirmarAceptacionDocumental] Firma rechazada por validacion. SolicitudId=" + id + ", Motivo=" + (firmaPlan.Mensaje ?? string.Empty));
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = firmaPlan.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, firmaPlan.EstadoDestino, firmaPlan.ObservacionEstado, out mensajeCambio))
            {
                _logger.LogWarning(
                    "[FirmarAceptacionDocumental] Cambio de estado fallido. SolicitudId=" + id +
                    ", EstadoAnterior=" + estadoActual +
                    ", EstadoDestino=" + (firmaPlan.EstadoDestino ?? string.Empty) +
                    ", UsuarioFirmante=" + usuarioFirmante +
                    ", Mensaje=" + (mensajeCambio ?? string.Empty));
                TempData["NotificacionTipo"] = "error";
                TempData["NotificacionMensaje"] = mensajeCambio;
                return RedirectToAction("Detalle", new { id });
            }

            var solicitudPostCambio = _solicitudDAO.ObtenerPorId(id);
            var estadoNuevoPersistido = solicitudPostCambio != null ? EstadoSolicitud.Normalizar(solicitudPostCambio.Estado) : string.Empty;
            _logger.LogInfo(
                "[FirmarAceptacionDocumental] Commit OK. SolicitudId=" + id +
                ", NumeroSolicitud=" + numeroSolicitud +
                ", EstadoAnterior=" + estadoActual +
                ", EstadoNuevoSolicitado=" + (firmaPlan.EstadoDestino ?? string.Empty) +
                ", EstadoNuevoPersistido=" + estadoNuevoPersistido +
                ", UsuarioFirmante=" + usuarioFirmante +
                ", Observacion=" + (firmaPlan.ObservacionEstado ?? string.Empty));

            try
            {
                var solicitudActualizada = solicitudPostCambio ?? solicitud;
                _solicitudAocrCorreoService.NotificarEvento(solicitudActualizada, "REVISION_FINAL_COORDINACION_REGISTRADA", firmaPlan.ObservacionEstado);
            }
            catch (Exception exCorreo)
            {
                _logger.LogWarning("[FirmarAceptacionDocumental] Error notificando revision final de Coordinacion. SolicitudId=" + id + ", Error=" + exCorreo.Message);
                System.Diagnostics.Debug.WriteLine("[FirmarAceptacionDocumental] Error notificando revisión final de Coordinación: " + exCorreo.Message);
            }

            TempData["NotificacionTipo"] = "success";
            TempData["NotificacionMensaje"] = string.Equals(firmaPlan.EstadoDestino, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase)
                ? "La aceptación documental fue firmada. La solicitud quedó pendiente de asignación de inspector en la bandeja de coordinación."
                : "La aceptación documental fue firmada por coordinación. Continúe el flujo institucional según el tipo de trámite.";
            return RedirectToAction("Detalle", new { id });
        }

        private int RegistrarAceptacionesPendientesParaRevisionFinal(
            int codigoSolicitud,
            IEnumerable<Documento> documentosRevision,
            IDictionary<int, Tuple<string, string>> revisiones,
            int usuarioFirmante,
            string usuarioRegistro)
        {
            var total = 0;
            foreach (var documento in documentosRevision ?? Enumerable.Empty<Documento>())
            {
                if (documento == null || documento.CodigoDocumento <= 0)
                {
                    continue;
                }

                var decisionActual = ObtenerDecisionRevisionDocumental(documento, revisiones);
                if (!string.IsNullOrWhiteSpace(decisionActual))
                {
                    continue;
                }

                var registrado = _solicitudAocrInfraBL.RegistrarRevisionDocumental(
                    codigoSolicitud,
                    documento.CodigoDocumento,
                    "ACEPTADO",
                    "Aceptado automáticamente al registrar la revisión final de Coordinación porque el expediente ya se encuentra en Aceptación Documental.",
                    usuarioFirmante,
                    usuarioRegistro);

                if (registrado)
                {
                    total++;
                }
            }

            return total;
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
            if (!EstadoPermiteDescargaAceptacionDocumental(estadoActual))
            {
                TempData["NotificacionTipo"] = "warning";
                TempData["NotificacionMensaje"] = "La aceptación documental aún no está firmada por coordinación.";
                return RedirectToAction("Detalle", new { id });
            }

            var historialEstados = _solicitudAocrInfraBL.ObtenerHistorialEstadosPorSolicitud(id) ?? new List<CapaModelo.HistorialEstado>();
            var firmaCoordinacion = historialEstados
                .Where(h => h != null && EsTransicionFirmaAceptacionDocumentalCoordinacion(h.EstadoNuevo))
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
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "Generar", CodigoSolicitudParameter = "id")]
        public ActionResult GenerarAOCR(int id)
        {
            if (User != null
                && (User.IsInRole("Direccion")
                    || User.IsInRole("DireccionJefaturaTecnica")
                    || User.IsInRole("DIRDAC")
                    || User.IsInRole("JefaturaTecnica")
                    || User.IsInRole("DirectorGeneral")))
            {
                return RedirectToAction("Index", "FirmaAocr", new { solicitudId = id });
            }

            try
            {
                var usuarioId = ObtenerUsuarioActualId();
                var contextoAocr = CrearContextoAutorizacionAocr();
                var disponibilidad = _generacionAocrService.Evaluar(id, usuarioId, contextoAocr.Roles);
                RegistrarTrazaGeneracionAocr("GenerarAOCR_POST", disponibilidad);
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
                string mensajeSincronizacion;
                if (!_generacionAocrService.MarcarPendienteGeneracionAocr(
                    id,
                    disponibilidad.InformeAprobado != null ? disponibilidad.InformeAprobado.CodigoInforme : 0,
                    usuarioId,
                    (User != null && User.Identity != null) ? User.Identity.Name : "sistema",
                    out mensajeSincronizacion))
                {
                    TempData["NotificacionTipo"] = "error";
                    TempData["NotificacionMensaje"] = string.IsNullOrWhiteSpace(mensajeSincronizacion)
                        ? "No fue posible sincronizar la solicitud a la fase AOCR antes de generar el documento."
                        : mensajeSincronizacion;
                    return RedirectToAction("Detalle", new { id });
                }

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

        private static void RegistrarTrazaGeneracionAocr(string origen, GeneracionAOCRService.Disponibilidad disponibilidad)
        {
            if (disponibilidad == null)
            {
                System.Diagnostics.Debug.WriteLine("[GenerarAOCR] Origen=" + (origen ?? "Desconocido") + " Disponibilidad=null");
                return;
            }

            var informeId = disponibilidad.InformeAprobado != null ? disponibilidad.InformeAprobado.CodigoInforme : 0;
            System.Diagnostics.Debug.WriteLine("[GenerarAOCR] Origen=" + (origen ?? "Desconocido")
                + " SolicitudId=" + (disponibilidad.Solicitud != null ? disponibilidad.Solicitud.CodigoSolicitud : 0)
                + " InspeccionId=" + (disponibilidad.InspeccionAprobada != null ? disponibilidad.InspeccionAprobada.CodigoInspeccion : 0)
                + " InformeTecnicoId=" + informeId
                + " EstadoSolicitud=" + (disponibilidad.EstadoSolicitud ?? string.Empty)
                + " EstadoInspeccion=" + (disponibilidad.EstadoInspeccion ?? string.Empty)
                + " EstadoInforme=" + (disponibilidad.EstadoInforme ?? string.Empty)
                + " ResultadoTecnicoFinal=" + (disponibilidad.ResultadoTecnicoFinal ?? string.Empty)
                + " AprobadoDireccion=" + disponibilidad.AprobadoDireccion
                + " AprobadoDIRDAC=" + disponibilidad.AprobadoDirdac
                + " TieneObservacionesPendientes=" + disponibilidad.TieneObservacionesPendientes
                + " ExisteAOCR=" + disponibilidad.YaGenerado
                + " PuedeGenerarAOCR=" + disponibilidad.Habilitado
                + " MotivoBloqueo=" + (disponibilidad.Motivo ?? string.Empty));
        }

        /// <summary>
        /// Descarga el archivo PDF de la AOCR generada para una solicitud.
        /// </summary>
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "DescargarGenerada", CodigoSolicitudParameter = "id")]
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

        [Authorize]
        public ActionResult DescargarAocrFirmada(int id, bool vistaPrevia = false)
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound();
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
                    || User.IsInRole("CoordinadorInspecciones")
                    || User.IsInRole("CoordinacionLegal")
                    || User.IsInRole("Inspector")
                    || User.IsInRole("Tecnico")
                    || User.IsInRole("EvaluadorTecnico")));

            if (!esPropietario && !esUsuarioInterno)
            {
                return new HttpStatusCodeResult(403, "No tiene permisos para acceder al documento firmado.");
            }

            var firma = _aocrFirmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(id, DocumentoTipoReconocimiento);
            string rutaDocumento = firma != null ? firma.RutaDocumento : null;
            DateTime? fechaDocumento = firma != null ? (DateTime?)firma.FechaFirma : null;

            if (string.IsNullOrWhiteSpace(rutaDocumento))
            {
                var certificado = new CertificadoDAO().ObtenerPorSolicitud(id);
                if (certificado != null)
                {
                    rutaDocumento = certificado.RutaDocumento;
                    fechaDocumento = certificado.UpdatedAt ?? certificado.FechaEmision;
                }
            }

            if (string.IsNullOrWhiteSpace(rutaDocumento))
            {
                return HttpNotFound("No existe un PDF AOCR firmado para esta solicitud.");
            }

            var rutaFisica = ResolverRutaDocumentoAocrFirmado(rutaDocumento);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                return HttpNotFound("No se encontró el archivo PDF firmado en almacenamiento.");
            }

            var nombreArchivo = ConstruirNombrePdfCertificadoAocr(solicitud, fechaDocumento);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreArchivo);
            return File(rutaFisica, "application/pdf");
        }

        private string ConstruirNombrePdfAceptacionDocumental(SolicitudAOCR solicitud, DateTime? fecha = null)
        {
            return PdfFileNameHelper.CrearNombreAceptacionDocumental(
                ObtenerNumeroSolicitudPdf(solicitud),
                ObtenerSegmentoOperadorPdf(solicitud),
                fecha ?? ObtenerFechaDocumentoPdf(solicitud));
        }

        private List<AocrGeneradasFirmadasRowViewModel> AplicarFiltrosBandeja(
            List<AocrGeneradasFirmadasRowViewModel> items,
            AocrGeneradasFirmadasFiltroViewModel filtros)
        {
            var filtrados = items ?? new List<AocrGeneradasFirmadasRowViewModel>();
            if (filtros == null)
            {
                return filtrados;
            }

            filtrados = AplicarFiltrosBusquedaYTipoBandeja(filtrados, filtros);
            filtrados = AplicarFiltrosEstadoBandeja(filtrados, filtros);
            filtrados = AplicarFiltroPdfBandeja(filtrados, filtros);
            return filtrados;
        }

        private List<AocrGeneradasFirmadasRowViewModel> AplicarFiltrosBusquedaYTipoBandeja(
            List<AocrGeneradasFirmadasRowViewModel> items,
            AocrGeneradasFirmadasFiltroViewModel filtros)
        {
            var filtrados = items ?? new List<AocrGeneradasFirmadasRowViewModel>();
            if (filtros == null)
            {
                return filtrados;
            }

            if (!string.IsNullOrWhiteSpace(filtros.Search))
            {
                var texto = filtros.Search.Trim();
                filtrados = filtrados.Where(x =>
                        ContieneTexto(x.NumeroSolicitud, texto)
                        || ContieneTexto(x.NumeroAocr, texto)
                        || ContieneTexto(x.NombreExplotador, texto)
                        || ContieneTexto(x.InspectorNombre, texto)
                        || ContieneTexto(x.CoordinadorNombre, texto))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(filtros.EstadoFinal))
            {
                filtrados = filtrados.Where(x => string.Equals(x.EstadoFinal, filtros.EstadoFinal, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(filtros.EstadoFirma))
            {
                filtrados = filtrados.Where(x => string.Equals(x.EstadoFirma, filtros.EstadoFirma, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(filtros.TipoTramite))
            {
                filtrados = filtrados.Where(x => string.Equals(x.TipoTramite, filtros.TipoTramite, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return filtrados;
        }

        private List<AocrGeneradasFirmadasRowViewModel> AplicarFiltrosEstadoBandeja(
            List<AocrGeneradasFirmadasRowViewModel> items,
            AocrGeneradasFirmadasFiltroViewModel filtros)
        {
            var filtrados = items ?? new List<AocrGeneradasFirmadasRowViewModel>();
            if (filtros == null)
            {
                return filtrados;
            }

            if (!string.IsNullOrWhiteSpace(filtros.EstadoFinal))
            {
                filtrados = filtrados.Where(x => string.Equals(x.EstadoFinal, filtros.EstadoFinal, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(filtros.EstadoFirma))
            {
                filtrados = filtrados.Where(x => string.Equals(x.EstadoFirma, filtros.EstadoFirma, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return filtrados;
        }

        private List<AocrGeneradasFirmadasRowViewModel> AplicarFiltroPdfBandeja(
            List<AocrGeneradasFirmadasRowViewModel> items,
            AocrGeneradasFirmadasFiltroViewModel filtros)
        {
            var filtrados = items ?? new List<AocrGeneradasFirmadasRowViewModel>();
            if (filtros == null)
            {
                return filtrados;
            }

            if (string.Equals(filtros.SoloConPdf, "SI", StringComparison.OrdinalIgnoreCase))
            {
                filtrados = filtrados.Where(x => x.TienePdfFirmado || x.TienePdfPreliminar).ToList();
            }

            return filtrados;
        }

        private AocrGeneradasFirmadasFiltroViewModel NormalizarFiltrosBandeja(AocrGeneradasFirmadasFiltroViewModel filtros)
        {
            filtros = filtros ?? new AocrGeneradasFirmadasFiltroViewModel();
            filtros.Search = (filtros.Search ?? string.Empty).Trim();
            filtros.EstadoFinal = NormalizarFiltroTodos(filtros.EstadoFinal);
            filtros.EstadoFirma = NormalizarFiltroTodos(filtros.EstadoFirma);
            filtros.TipoTramite = NormalizarFiltroTodos(filtros.TipoTramite);
            filtros.SoloConPdf = NormalizarFiltroTodos(filtros.SoloConPdf);
            return filtros;
        }

        private static string NormalizarFiltroTodos(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return string.Empty;
            }

            var limpio = valor.Trim();
            return limpio.StartsWith("Todos", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : limpio;
        }

        private static string ResumirEstadosBandeja(IEnumerable<AocrBandejaDocumentoRow> filas)
        {
            var resumen = (filas ?? Enumerable.Empty<AocrBandejaDocumentoRow>())
                .SelectMany(fila => new[]
                {
                    fila != null ? fila.EstadoSolicitudRaw : null,
                    fila != null ? fila.EstadoCertificadoRaw : null,
                    fila != null ? fila.EstadoInformeTecnicoRaw : null
                })
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Select(AocrBandejaEstadoHelper.NormalizarEstadoSolicitud)
                .GroupBy(valor => valor, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(grupo => grupo.Count())
                .ThenBy(grupo => grupo.Key)
                .Select(grupo => grupo.Key + "=" + grupo.Count())
                .ToList();

            return resumen.Any() ? string.Join(", ", resumen) : "SIN_ESTADOS";
        }

        private static bool TieneEstadosAocrDetectados(IEnumerable<AocrBandejaDocumentoRow> filas)
        {
            return (filas ?? Enumerable.Empty<AocrBandejaDocumentoRow>())
                .SelectMany(fila => new[]
                {
                    fila != null ? fila.EstadoSolicitudRaw : null,
                    fila != null ? fila.EstadoCertificadoRaw : null,
                    fila != null ? fila.EstadoInformeTecnicoRaw : null
                })
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Select(AocrBandejaEstadoHelper.NormalizarEstadoSolicitud)
                .Any(valor => string.Equals(valor, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(valor, EstadoSolicitud.AOCR_Legalizado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(valor, EstadoSolicitud.AOCR_Validado, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(valor, EstadoSolicitud.AOCR_EnRevision, StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolverMotivoSinRegistros(int totalBaseSinFiltros, int totalDespuesRol, int totalDespuesTextoTipo, int totalDespuesEstado, int totalDespuesPdf)
        {
            if (totalBaseSinFiltros <= 0)
            {
                return "No existen AOCR base en el origen consultado.";
            }

            if (totalDespuesRol <= 0)
            {
                return "El filtro por rol, compania activa o asignacion tecnica excluyo todos los registros base.";
            }

            if (totalDespuesTextoTipo <= 0)
            {
                return "Los filtros de busqueda o tipo de tramite eliminaron todos los registros visibles para el rol.";
            }

            if (totalDespuesEstado <= 0)
            {
                return "Los filtros de estado final o estado de firma eliminaron todos los registros visibles.";
            }

            if (totalDespuesPdf <= 0)
            {
                return "El filtro Solo con PDF elimino todos los registros visibles.";
            }

            return "Sin exclusion adicional.";
        }

        private static void ConfigurarEmptyStateBandeja(
            AocrGeneradasFirmadasViewModel model,
            int totalBaseSinFiltros,
            int totalDespuesRol,
            int totalDespuesTextoTipo,
            int totalDespuesEstado,
            int totalDespuesPdf)
        {
            if (model == null || model.TieneResultados)
            {
                return;
            }

            if (totalBaseSinFiltros <= 0)
            {
                model.EmptyStateTitle = "No existen AOCR generadas o firmadas para los criterios seleccionados.";
                model.EmptyStateMessage = "La consulta base no encontro AOCR en etapas visibles del flujo documental.";
                return;
            }

            if (totalDespuesRol <= 0)
            {
                model.EmptyStateTitle = "Existen AOCR en el sistema, pero no son visibles para su rol actual.";
                model.EmptyStateMessage = "Revise el rol seleccionado, la compania activa o la asignacion tecnica asociada al usuario actual.";
                return;
            }

            if (totalDespuesTextoTipo <= 0 || totalDespuesEstado <= 0 || totalDespuesPdf <= 0)
            {
                model.EmptyStateTitle = "Existen AOCR en el sistema, pero no coinciden con los filtros seleccionados.";
                model.EmptyStateMessage = "Ajuste los filtros de busqueda, estado, tipo de tramite o PDF para ampliar la bandeja.";
                return;
            }

            model.EmptyStateTitle = "No existen AOCR generadas o firmadas para los criterios seleccionados.";
            model.EmptyStateMessage = "La consulta no encontro registros visibles despues de aplicar los criterios actuales.";
        }

        private IList<SelectListItem> ConstruirOpcionesFiltro(IEnumerable<string> valores, string seleccionado, string opcionTodos)
        {
            var opciones = new List<SelectListItem>
            {
                new SelectListItem { Text = opcionTodos, Value = string.Empty, Selected = string.IsNullOrWhiteSpace(seleccionado) }
            };

            foreach (var valor in (valores ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x))
            {
                opciones.Add(new SelectListItem
                {
                    Text = valor,
                    Value = valor,
                    Selected = string.Equals(valor, seleccionado, StringComparison.OrdinalIgnoreCase)
                });
            }

            return opciones;
        }

        private AocrGeneradasFirmadasRowViewModel MapearFilaBandeja(AocrBandejaDocumentoRow fila, BandejaAocrContexto contexto)
        {
            var usaFlujoCondiciones = AocrBandejaEstadoHelper.UsaFlujoCondiciones(fila);
            var estadoAocr = AocrBandejaEstadoHelper.ObtenerEstadoAocr(fila);
            var estadoCondiciones = AocrBandejaEstadoHelper.ObtenerEstadoCondiciones(fila);
            var estadoFirma = AocrBandejaEstadoHelper.ObtenerEstadoFirma(fila);
            var estadoFinal = AocrBandejaEstadoHelper.ObtenerEstadoFinal(fila);
            var tienePdfFirmado = AocrBandejaEstadoHelper.TieneDocumentoFinalFirmado(fila);
            var tienePdfPreliminar = AocrBandejaEstadoHelper.TieneDocumentoPreliminar(fila);
            var numeroAocr = PrimeraCadenaNoVacia(
                fila.NumeroAocrReconocimiento,
                fila.NumeroAocrCertificado,
                fila.NumeroAocBase);
            var inspectorNombre = PrimeraCadenaNoVacia(
                fila.InspectorPrincipalNombreInspeccion,
                fila.InspectorNombreSolicitud,
                fila.InspectorApoyoNombreSolicitud);
            var coordinadorNombre = PrimeraCadenaNoVacia(
                fila.EmitidoPor,
                fila.AprobadoPor,
                fila.NombreFirmanteReconocimiento,
                fila.NombreFirmanteCondiciones);
            var urlHelper = new UrlHelper(ControllerContext.RequestContext);
            var puedeGestionarInterno = contexto.EsAdministrador || contexto.EsCoordinacion || contexto.EsDireccion;

            return new AocrGeneradasFirmadasRowViewModel
            {
                SolicitudId = fila.SolicitudId,
                InspeccionId = fila.InspeccionId,
                InformeId = fila.InformeId,
                CertificadoId = fila.CertificadoId,
                FirmaCondicionesId = fila.FirmaCondicionesId,
                FirmaReconocimientoId = fila.FirmaReconocimientoId,
                NumeroSolicitud = fila.NumeroSolicitud,
                NumeroAocr = numeroAocr,
                TipoTramite = ObtenerTipoTramiteTexto(fila.TipoSolicitud),
                NombreExplotador = fila.NombreExplotador,
                InspectorNombre = inspectorNombre,
                CoordinadorNombre = coordinadorNombre,
                EstadoInformeTecnico = fila.EstadoInformeTecnicoRaw,
                ResultadoTecnicoFinal = fila.ResultadoTecnicoFinalRaw,
                EstadoAocr = estadoAocr,
                EstadoCondiciones = estadoCondiciones,
                EstadoFirma = estadoFirma,
                EstadoFinal = estadoFinal,
                BadgeEstadoAocrCss = AocrBandejaEstadoHelper.ObtenerBadgeCss(estadoAocr),
                BadgeEstadoCondicionesCss = AocrBandejaEstadoHelper.ObtenerBadgeCss(estadoCondiciones),
                BadgeEstadoFirmaCss = AocrBandejaEstadoHelper.ObtenerBadgeCss(estadoFirma),
                BadgeEstadoFinalCss = AocrBandejaEstadoHelper.ObtenerBadgeCss(estadoFinal),
                FechaSolicitud = fila.FechaSolicitud,
                FechaUltimoHito = ObtenerFechaUltimoHito(fila),
                NombreFirmante = usaFlujoCondiciones ? fila.NombreFirmanteCondiciones : fila.NombreFirmanteReconocimiento,
                UsaFlujoCondiciones = usaFlujoCondiciones,
                TienePdfPreliminar = tienePdfPreliminar,
                TienePdfFirmado = tienePdfFirmado,
                UrlDetalleSolicitud = urlHelper.Action("Detalle", "SolicitudAOCR", new { id = fila.SolicitudId }),
                UrlDetalleInspeccion = fila.InspeccionId.HasValue ? urlHelper.Action("Detalle", "Inspeccion", new { id = fila.InspeccionId.Value }) : null,
                UrlHistorial = urlHelper.Action("PorSolicitud", "HistorialEstado", new { id = fila.SolicitudId }),
                UrlPreliminar = ConstruirUrlPreliminar(urlHelper, fila, contexto, tienePdfPreliminar, usaFlujoCondiciones),
                UrlFinal = ConstruirUrlFinal(urlHelper, fila, tienePdfFirmado, usaFlujoCondiciones),
                UrlGestion = puedeGestionarInterno
                    ? urlHelper.Action(
                        "Index",
                        "FirmaAocr",
                        new { solicitudId = fila.SolicitudId })
                    : null,
                UrlValidacion = (contexto.EsCoordinacion || contexto.EsDireccion || contexto.EsAdministrador)
                    ? urlHelper.Action("Index", "FirmaAocr", new { solicitudId = fila.SolicitudId })
                    : null
            };
        }

        private string ConstruirUrlPreliminar(
            UrlHelper urlHelper,
            AocrBandejaDocumentoRow fila,
            BandejaAocrContexto contexto,
            bool tienePdfPreliminar,
            bool usaFlujoCondiciones)
        {
            if (!tienePdfPreliminar)
            {
                return null;
            }

            if (usaFlujoCondiciones)
            {
                if (contexto.EsCoordinacion || contexto.EsDireccion || contexto.EsAdministrador)
                {
                    return urlHelper.Action("VerPdf", "FirmaAocr", new { solicitudId = fila.SolicitudId, firmado = false });
                }

                return AocrBandejaEstadoHelper.TieneDocumentoFinalFirmado(fila)
                    ? urlHelper.Action("DescargarCondicionesLimitacionesModificacion", "SolicitudAOCR", new { id = fila.SolicitudId, vistaPrevia = true })
                    : null;
            }

            if (contexto.EsCoordinacion || contexto.EsDireccion || contexto.EsAdministrador)
            {
                return urlHelper.Action("VerPdf", "FirmaAocr", new { solicitudId = fila.SolicitudId, firmado = false });
            }

            return !string.IsNullOrWhiteSpace(fila.RutaAocrGenerada)
                ? urlHelper.Action("DescargarAOCRGenerada", "SolicitudAOCR", new { id = fila.SolicitudId, vistaPrevia = true })
                : null;
        }

        private string ConstruirUrlFinal(UrlHelper urlHelper, AocrBandejaDocumentoRow fila, bool tienePdfFirmado, bool usaFlujoCondiciones)
        {
            if (!tienePdfFirmado)
            {
                return null;
            }

            return usaFlujoCondiciones
                ? urlHelper.Action("DescargarCondicionesLimitacionesModificacion", "SolicitudAOCR", new { id = fila.SolicitudId })
                : urlHelper.Action("DescargarAocrFirmada", "SolicitudAOCR", new { id = fila.SolicitudId });
        }

        private bool DebeMostrarFilaBandeja(AocrBandejaDocumentoRow fila, BandejaAocrContexto contexto)
        {
            if (fila == null || fila.SolicitudId <= 0)
            {
                return false;
            }

            if (contexto.EsAdministrador || contexto.EsCoordinacion || contexto.EsDireccion)
            {
                return true;
            }

            if (contexto.EsSolicitante)
            {
                if (fila.CodigoUsuario != contexto.CodigoUsuarioActual)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(contexto.CompaniaActivaCodigo))
                {
                    return true;
                }

                if (string.IsNullOrWhiteSpace(fila.CompaniasSeleccionadas))
                {
                    return true;
                }

                return ContieneValorLista(fila.CompaniasSeleccionadas, contexto.CompaniaActivaCodigo);
            }

            if (contexto.EsInspector)
            {
                return (fila.CodigoInspectorInspeccion.HasValue && contexto.CodigosInspector.Contains(fila.CodigoInspectorInspeccion.Value))
                    || (fila.CodigoInspectorSolicitud.HasValue && contexto.CodigosInspector.Contains(fila.CodigoInspectorSolicitud.Value));
            }

            return false;
        }

        private BandejaAocrContexto ConstruirContextoBandeja()
        {
            var rolActual = RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"]?.ToString());
            var rolesRaw = RoleGroupingHelper.ExtractRoles(Session["RolesRaw"] ?? Session["Roles"], Session["Rol"] as string);
            var sinRolesRaw = rolesRaw.Count == 0;
            int codigoUsuarioActual;
            TryObtenerUsuarioActualId(out codigoUsuarioActual);

            return new BandejaAocrContexto
            {
                CodigoUsuarioActual = codigoUsuarioActual,
                CompaniaActivaCodigo = ObtenerCompaniaActivaCodigo(),
                EsAdministrador = RoleGroupingHelper.IsAdministrador(rolActual),
                EsSolicitante = RoleGroupingHelper.IsSolicitante(rolActual),
                EsInspector = RoleGroupingHelper.IsInspectorTecnico(rolActual)
                    && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "Inspector", "Tecnico", "EvaluadorTecnico")),
                EsCoordinacion = RoleGroupingHelper.IsCoordinacion(rolActual)
                    && (sinRolesRaw || RoleGroupingHelper.HasAnyRawRole(rolesRaw, "Coordinador", "CoordinadorInspecciones", "CoordinacionLegal", "CoordinadorLegal")),
                EsDireccion = RoleGroupingHelper.IsDireccionJefaturaTecnica(rolActual),
                CodigosInspector = ObtenerCodigosInspectorActual()
            };
        }

        private HashSet<int> ObtenerCodigosInspectorActual()
        {
            var ids = new HashSet<int>();
            int codigoUsuarioActual;
            TryObtenerUsuarioActualId(out codigoUsuarioActual);
            if (codigoUsuarioActual > 0)
            {
                ids.Add(codigoUsuarioActual);
            }

            var codigoUsuarioTexto = (Session["CodigoUsuario"] ?? string.Empty).ToString().Trim();
            int codigoUsuarioNumerico;
            if (int.TryParse(codigoUsuarioTexto, out codigoUsuarioNumerico) && codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
            }

            try
            {
                var inspectorActual = codigoUsuarioActual > 0
                    ? _usuarioInternoRtDAO.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(codigoUsuarioActual)
                    : null;

                if (inspectorActual == null && !string.IsNullOrWhiteSpace(codigoUsuarioTexto))
                {
                    inspectorActual = _usuarioInternoRtDAO.ObtenerActivoPorCodigoUsuario(codigoUsuarioTexto)
                        ?? _usuarioInternoRtDAO.ObtenerInspectorAsignableActivo(codigoUsuarioTexto);
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                    }

                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                    }
                }
            }
            catch
            {
            }

            return ids;
        }

        private static string ObtenerTipoTramiteTexto(int? tipoSolicitud)
        {
            switch (tipoSolicitud ?? 0)
            {
                case 1:
                    return "Emisión";
                case 2:
                    return "Renovación";
                case 3:
                    return "Modificación";
                default:
                    return "AOCR";
            }
        }

        private static DateTime? ObtenerFechaUltimoHito(AocrBandejaDocumentoRow fila)
        {
            return new[]
            {
                fila.FechaFirmaReconocimiento,
                fila.FechaFirmaCondiciones,
                fila.FechaActualizacionCertificado,
                fila.FechaEmisionCertificado,
                fila.FechaAocrGenerada,
                fila.FechaEnvioInformeDirdac,
                fila.FechaFirmaInformeDireccion,
                fila.FechaProgramadaInspeccion,
                fila.FechaSolicitud
            }
            .Where(x => x.HasValue)
            .OrderByDescending(x => x.Value)
            .FirstOrDefault();
        }

        private static string PrimeraCadenaNoVacia(params string[] valores)
        {
            return (valores ?? new string[0])
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        private static bool ContieneTexto(string valor, string texto)
        {
            return !string.IsNullOrWhiteSpace(valor)
                && !string.IsNullOrWhiteSpace(texto)
                && valor.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class BandejaAocrContexto
        {
            public int CodigoUsuarioActual { get; set; }
            public string CompaniaActivaCodigo { get; set; }
            public bool EsAdministrador { get; set; }
            public bool EsSolicitante { get; set; }
            public bool EsInspector { get; set; }
            public bool EsCoordinacion { get; set; }
            public bool EsDireccion { get; set; }
            public HashSet<int> CodigosInspector { get; set; } = new HashSet<int>();
            public bool PuedeVerBandeja => EsAdministrador || EsSolicitante || EsInspector || EsCoordinacion || EsDireccion;
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
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "Legalizar", CodigoSolicitudParameter = "id")]
        public ActionResult Legalizar(int id, string observacionLegal = "")
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(id);
                if (solicitud == null) return HttpNotFound();

                var aocrGenerada = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
                var legalizacionPlan = _aocrFinalWorkflowService.PrepararLegalizacion(aocrGenerada != null, observacionLegal);
                if (!legalizacionPlan.PuedeContinuar)
                {
                    TempData[legalizacionPlan.ClaveTempData] = legalizacionPlan.Mensaje;
                    return RedirectToAction("RevisarLegalizacion");
                }

                string mensajeCambio;
                if (!CambiarEstadoConReglasAocr(id, legalizacionPlan.Decision.EstadoDestino, legalizacionPlan.Decision.ObservacionEstado, out mensajeCambio))
                {
                    TempData["Error"] = mensajeCambio;
                    return RedirectToAction("RevisarLegalizacion");
                }

                var solicitudActualizada = _solicitudDAO.ObtenerPorId(id);
                _aocrFinalWorkflowService.NotificarLegalizacion(solicitudActualizada, legalizacionPlan);

                TempData["Exito"] = "Solicitud legalizada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al legalizar: " + ex.Message;
            }

            return RedirectToAction("RevisarLegalizacion");
        }

        [Authorize(Roles = "Inspector,CoordinadorInspecciones,Coordinacion,Administrador")]
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
        public ActionResult CerrarFaseDocumentalNuevoAeropuertoModificacion(int id, string observacion = "")
        {
            var contextoAocr = CrearContextoAutorizacionAocr();
            var resultado = _aocrModificationWorkflowService.EjecutarCierreFaseDocumentalNuevoAeropuerto(
                id,
                observacion,
                ObtenerUsuarioActualId(),
                contextoAocr.Roles,
                contextoAocr.IsAuthenticated);

            TempData["NotificacionTipo"] = resultado.ClaveTempData;
            TempData["NotificacionMensaje"] = resultado.Mensaje;
            return RedirectToAction(
                resultado.AccionRedireccion,
                resultado.ControladorRedireccion,
                new RouteValueDictionary(resultado.RouteValues ?? new Dictionary<string, object> { { "id", id } }));
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarRequiereInspeccionModificacion(int id, string observacion = "")
        {
            var contextoAocr = CrearContextoAutorizacionAocr();
            var resultado = _aocrModificationWorkflowService.EjecutarRequiereInspeccion(
                id,
                observacion,
                ObtenerUsuarioActualId(),
                contextoAocr.Roles,
                contextoAocr.IsAuthenticated);

            TempData["NotificacionTipo"] = resultado.ClaveTempData;
            TempData["NotificacionMensaje"] = resultado.Mensaje;
            return RedirectToAction(
                resultado.AccionRedireccion,
                resultado.ControladorRedireccion,
                new RouteValueDictionary(resultado.RouteValues ?? new Dictionary<string, object> { { "id", id } }));
        }

        [HttpPost]
        [Authorize(Roles = "Inspector,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult GenerarCondicionesLimitacionesModificacion(int id, string observacion = "")
        {
            var contextoAocr = CrearContextoAutorizacionAocr();
            var resultado = _aocrModificationWorkflowService.EjecutarGeneracionCondicionesLimitaciones(
                id,
                observacion,
                ObtenerUsuarioActualId(),
                contextoAocr.Roles,
                contextoAocr.IsAuthenticated);

            TempData["NotificacionTipo"] = resultado.ClaveTempData;
            TempData["NotificacionMensaje"] = resultado.Mensaje;
            return RedirectToAction(
                resultado.AccionRedireccion,
                resultado.ControladorRedireccion,
                new RouteValueDictionary(resultado.RouteValues ?? new Dictionary<string, object> { { "id", id } }));
        }

        [HttpPost]
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult RevisarCondicionesLimitacionesModificacion(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var revisionPlan = _aocrModificationWorkflowService.PrepararRevisionFinalCondicionesLimitaciones(solicitud, observacion);
            if (!revisionPlan.PuedeContinuar)
            {
                TempData["NotificacionTipo"] = revisionPlan.ClaveTempData;
                TempData["NotificacionMensaje"] = revisionPlan.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, revisionPlan.EstadoDestino, revisionPlan.ObservacionEstado, out mensajeCambio))
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
        [Authorize(Roles = "Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult EnviarCondicionesLimitacionesDcav(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound();

            var envioPlan = _aocrModificationWorkflowService.PrepararEnvioDcavCondicionesLimitaciones(solicitud, observacion);
            if (!envioPlan.PuedeContinuar)
            {
                TempData["NotificacionTipo"] = envioPlan.ClaveTempData;
                TempData["NotificacionMensaje"] = envioPlan.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, envioPlan.EstadoDestino, envioPlan.ObservacionEstado, out mensajeCambio))
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
        [Authorize(Roles = "CoordinadorInspecciones,Coordinacion,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarAocrEnElaboracion(int id, string observacion = "")
        {
            var elaboracionPlan = _aocrFinalWorkflowService.PrepararElaboracion(id, observacion);
            if (!elaboracionPlan.PuedeContinuar)
            {
                TempData[elaboracionPlan.ClaveTempData] = elaboracionPlan.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, elaboracionPlan.Decision.EstadoDestino, elaboracionPlan.Decision.ObservacionEstado, out mensajeCambio))
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
        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,Coordinador,CoordinadorInspecciones,Coordinacion,DIRDAC,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarAocrEnRevision(int id, string observacion = "")
        {
            var solicitud = _solicitudDAO.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound();
            }

            var aocrGenerada = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
            var revisionPlan = _aocrFinalWorkflowService.PrepararEnvioRevisionInstitucional(
                aocrGenerada != null,
                solicitud.Estado,
                observacion);
            if (!revisionPlan.PuedeContinuar)
            {
                TempData[revisionPlan.ClaveTempData] = revisionPlan.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            RegistrarTrazaAocrCoordinacion(
                id,
                aocrGenerada,
                solicitud.Estado,
                string.Empty,
                string.Empty,
                string.Empty,
                ObtenerUltimaInspeccionVinculada(_solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(id) ?? new List<Inspeccion>())?.CodigoInspeccion);

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, revisionPlan.Decision.EstadoDestino, revisionPlan.Decision.ObservacionEstado, out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
            }
            else
            {
                TempData["Exito"] = "La AOCR fue aprobada por Coordinación y enviada a DIRDAC para revisión/firma.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        private string ObtenerRolPrincipalAocr()
        {
            var contexto = CrearContextoAutorizacionAocr();
            var rolCanonico = !string.IsNullOrWhiteSpace(contexto.SelectedRole)
                ? contexto.SelectedRole
                : (contexto.Roles ?? new List<string>()).FirstOrDefault() ?? string.Empty;

            return string.IsNullOrWhiteSpace(rolCanonico)
                ? string.Empty
                : RoleGroupingHelper.ToDisplayName(rolCanonico);
        }

        private void RegistrarTrazaAocrCoordinacion(
            int codigoSolicitud,
            Documento aocrGenerada = null,
            string estadoAocr = null,
            string estadoInforme = null,
            string resultadoInforme = null,
            string motivoBloqueo = null,
            int? codigoInspeccion = null,
            string camposFaltantes = null)
        {
            var contexto = CrearContextoAutorizacionAocr();
            var rolesActuales = contexto.RawRoles ?? contexto.Roles ?? new List<string>();
            var puedeRevisar = _aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(EstadoSolicitud.AOCR_Validado, rolesActuales, contexto.IsAuthenticated);
            var puedeSolicitarModificacion = _aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(EstadoSolicitud.Observada, rolesActuales, contexto.IsAuthenticated);
            var puedeEnviarDirdac = _aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(EstadoSolicitud.AOCR_EnRevision, rolesActuales, contexto.IsAuthenticated);
            var puedeGenerarPdfFirma = aocrGenerada != null
                && _aocrAuthorizationService.PuedeEjecutarAccion("Generar", contexto, codigoSolicitud: codigoSolicitud, modulo: "SolicitudAOCR").Permitido;
            var rolVisible = !string.IsNullOrWhiteSpace(contexto.SelectedRole)
                ? RoleGroupingHelper.ToDisplayName(contexto.SelectedRole)
                : ObtenerRolPrincipalAocr();

            System.Diagnostics.Debug.WriteLine("[AOCR_COORD] SolicitudId=" + codigoSolicitud
                + " InspeccionId=" + (codigoInspeccion.HasValue ? codigoInspeccion.Value.ToString() : string.Empty)
                + " AOCRId=" + (aocrGenerada != null ? aocrGenerada.CodigoDocumento.ToString() : string.Empty)
                + " EstadoAOCR=" + (estadoAocr ?? string.Empty)
                + " EstadoInforme=" + (estadoInforme ?? string.Empty)
                + " ResultadoInforme=" + (resultadoInforme ?? string.Empty)
                + " Usuario=" + (contexto.UserName ?? string.Empty)
                + " Rol=" + rolVisible
                + " PuedeRevisar=" + puedeRevisar
                + " PuedeSolicitarModificacion=" + puedeSolicitarModificacion
                + " PuedeEnviarDIRDAC=" + puedeEnviarDirdac
                + " PuedeGenerarPdfFirma=" + puedeGenerarPdfFirma
                + " MotivoBloqueo=" + (motivoBloqueo ?? string.Empty)
                + " CamposFaltantes=" + (camposFaltantes ?? string.Empty));
        }

        private AocrAuthorizationContext CrearContextoAutorizacionAocr()
        {
            return AocrAuthorizationContextFactory.Build(HttpContext);
        }

        [HttpPost]
        [Authorize(Roles = "CoordinacionLegal,CoordinadorLegal,DirectorGeneral,Administrador")]
        [ValidateAntiForgeryToken]
        [AocrAuthorize(Modulo = "SolicitudAOCR", Accion = "Emitir", CodigoSolicitudParameter = "id")]
        public ActionResult EmitirAocr(int id, string observacion = "")
        {
            var validacionInspeccion = _aocrFinalWorkflowService.ValidarInspeccionSatisfactoriaParaAocr(id);
            if (!validacionInspeccion.PuedeContinuar)
            {
                TempData[validacionInspeccion.ClaveTempData] = validacionInspeccion.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            var aocrGenerada = _generacionAocrService.ObtenerAocrGeneradoVigente(id);
            var emisionPlan = _aocrFinalWorkflowService.PrepararEmision(aocrGenerada != null, observacion);
            if (!emisionPlan.PuedeContinuar)
            {
                TempData[emisionPlan.ClaveTempData] = emisionPlan.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeCambio;
            if (!CambiarEstadoConReglasAocr(id, emisionPlan.Decision.EstadoDestino, emisionPlan.Decision.ObservacionEstado, out mensajeCambio))
            {
                TempData["Error"] = mensajeCambio;
            }
            else
            {
                var solicitudActualizada = _solicitudDAO.ObtenerPorId(id);
                _aocrFinalWorkflowService.NotificarEmision(solicitudActualizada, emisionPlan);
                TempData["Exito"] = "AOCR emitido y marcado como recibido.";
            }

            return RedirectToAction("Detalle", new { id });
        }
        private bool CambiarEstadoConReglasAocr(int codigoSolicitud, string nuevoEstado, string observacion, out string mensaje)
        {
            var usuarioId = ObtenerUsuarioActualId();
            var contextoAocr = CrearContextoAutorizacionAocr();
            var rolesActuales = (contextoAocr.RawRoles ?? contextoAocr.Roles ?? new List<string>()).ToList();
            return _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                codigoSolicitud,
                nuevoEstado,
                observacion,
                usuarioId,
                estadoDestino => _aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(estadoDestino, rolesActuales, contextoAocr.IsAuthenticated),
                out mensaje);
        }

        private bool CambiarEstadoSubsanadaDesdeSubsanarPost(int codigoSolicitud, string observacion, out string mensaje)
        {
            var usuarioId = ObtenerUsuarioActualId();
            var contextoAocr = CrearContextoAutorizacionAocr();
            var rolesActuales = (contextoAocr.RawRoles ?? contextoAocr.Roles ?? new List<string>()).ToList();
            return _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                codigoSolicitud,
                EstadoSolicitud.Subsanada,
                observacion,
                usuarioId,
                estadoDestino => _aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(estadoDestino, rolesActuales, contextoAocr.IsAuthenticated),
                out mensaje,
                true,
                true);
        }

        private static bool SolicitudEstaEnEtapaRevisionDocumental(string estadoSolicitud)
        {
            return AocrEstadoService.EsEstadoRevisablePorInspector(estadoSolicitud);
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
                return "SUBSANADA";
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

        private static string ObtenerDecisionRevisionDocumentalLog(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
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
                return NormalizarDecisionRevisionDocumentalLog(revisionActual.Item1);
            }

            return NormalizarDecisionRevisionDocumentalLog(documento.Estado);
        }

        private static string NormalizarDecisionRevisionDocumentalLog(string valor)
        {
            var normalizado = (valor ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");

            switch (normalizado)
            {
                case "APROBADO":
                case "VALIDADO":
                case "ACEPTADO":
                case "ACEPTADA":
                    return "ACEPTADO";
                case "OBSERVADO":
                case "OBSERVADA":
                    return "OBSERVADO";
                case "RECHAZADO":
                case "RECHAZADA":
                case "DEVUELTO":
                case "DEVUELTA":
                case "DEVUELTO_INSPECTOR":
                    return "DEVUELTO";
                default:
                    return normalizado;
            }
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
                .Where(d => RevisionDocumentalDisplayHelper.ShouldIncludeInRevisionDocumental(d.TipoDocumento))
                .Select(d =>
                {
                    d.TipoDocumentoCodigoCanonico = RevisionDocumentalDisplayHelper.GetCanonicalDocumentType(d.TipoDocumento);
                    d.TipoDocumentoNombre = RevisionDocumentalDisplayHelper.GetDocumentDisplayName(d.TipoDocumento);
                    d.OrdenVisual = RevisionDocumentalDisplayHelper.GetDocumentPriority(d.TipoDocumento);
                    return d;
                })
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(d => d.Version ?? 0)
                    .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .ThenByDescending(d => d.CodigoDocumento)
                    .First())
                .OrderBy(d => d.OrdenVisual)
                .ThenBy(d => d.TipoDocumentoNombre ?? d.TipoDocumento ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.NombreArchivo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<Documento> ObtenerDocumentosElegiblesParaSubsanacion(int codigoSolicitud)
        {
            var documentos = _documentoDAO.ObtenerPorSolicitud(codigoSolicitud) ?? new List<Documento>();
            return documentos
                .Where(d => d != null && d.CodigoDocumento > 0)
                .Where(d => RevisionDocumentalDisplayHelper.ShouldIncludeInRevisionDocumental(d.TipoDocumento))
                .Select(d =>
                {
                    d.TipoDocumentoCodigoCanonico = RevisionDocumentalDisplayHelper.GetCanonicalDocumentType(d.TipoDocumento);
                    d.TipoDocumentoNombre = RevisionDocumentalDisplayHelper.GetDocumentDisplayName(d.TipoDocumento);
                    d.OrdenVisual = ObtenerOrdenVisualParaSubsanacion(d);
                    return d;
                })
                .OrderBy(d => d.OrdenVisual)
                .ThenBy(d => d.TipoDocumentoNombre ?? d.TipoDocumento ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(d => d.Version ?? 0)
                .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                .ThenByDescending(d => d.CodigoDocumento)
                .ToList();
        }

        private static int ObtenerOrdenVisualParaSubsanacion(Documento documento)
        {
            return documento != null && documento.OrdenVisual > 0
                ? documento.OrdenVisual
                : RevisionDocumentalDisplayHelper.GetDocumentPriority(documento != null ? documento.TipoDocumento : null);
        }

        private static List<Documento> SeleccionarUltimosDocumentosPendientesSubsanacionPorGrupo(IEnumerable<Documento> documentos)
        {
            return (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(d => d.Version ?? 0)
                    .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .ThenByDescending(d => d.CodigoDocumento)
                    .First())
                .OrderBy(d => d.OrdenVisual)
                .ThenBy(d => d.TipoDocumentoNombre ?? d.TipoDocumento ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.NombreArchivo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ObtenerClaveDocumentoRevision(Documento documento)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            var tipoDocumento = RevisionDocumentalDisplayHelper.GetDocumentGroupKey(documento.TipoDocumento);
            if (!string.IsNullOrWhiteSpace(tipoDocumento))
            {
                return tipoDocumento;
            }

            return "__DOC_" + documento.CodigoDocumento;
        }

        private static string ObtenerEtiquetaDocumento(Documento documento)
        {
            if (documento == null)
            {
                return "Documento";
            }

            var etiqueta = !string.IsNullOrWhiteSpace(documento.TipoDocumentoNombre)
                ? documento.TipoDocumentoNombre.Trim()
                : RevisionDocumentalDisplayHelper.GetDocumentDisplayName(documento.TipoDocumento);

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

            var estadoDocumento = NormalizarEstadoDocumento(documento.Estado);
            if (estadoDocumento == "APROBADO" || estadoDocumento == "VALIDADO" || estadoDocumento == "ACEPTADO")
            {
                return "ACEPTADO";
            }

            if (estadoDocumento == "OBSERVADO")
            {
                return "OBSERVADO";
            }

            if (estadoDocumento == "RECHAZADO" || estadoDocumento == "DEVUELTO")
            {
                return "DEVUELTO";
            }

            if (estadoDocumento == "MODIFICACION_SOLICITADA"
                || estadoDocumento == "MODIFICACION SOLICITADA"
                || estadoDocumento == "SOLICITAR_MODIFICACION")
            {
                return "OBSERVADO";
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

        private void EnriquecerDocumentosRevisionDocumental(
            IList<Documento> documentos,
            SolicitudAOCR solicitud,
            IDictionary<int, Tuple<string, string>> revisiones,
            IEnumerable<Inspeccion> inspeccionesSolicitud)
        {
            var lista = (documentos ?? new List<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToList();

            if (lista.Count == 0)
            {
                return;
            }

            var usuarioActualId = ObtenerUsuarioActualId();
            var esAdministrador = User != null && User.IsInRole("Administrador");
            var esCoordinacion = User != null && (User.IsInRole("Coordinador") || User.IsInRole("CoordinadorInspecciones"));
            var esPropietario = solicitud != null && usuarioActualId > 0 && solicitud.CodigoUsuario == usuarioActualId;
            var esInspectorAsignado = usuarioActualId > 0 && (inspeccionesSolicitud ?? Enumerable.Empty<Inspeccion>())
                .Any(i => i != null && i.CodigoInspector.HasValue && i.CodigoInspector.Value == usuarioActualId);
            var puedeAccederArchivo = esAdministrador || esCoordinacion || esPropietario || esInspectorAsignado;

            foreach (var documento in lista)
            {
                documento.TipoDocumentoCodigoCanonico = string.IsNullOrWhiteSpace(documento.TipoDocumentoCodigoCanonico)
                    ? RevisionDocumentalDisplayHelper.GetCanonicalDocumentType(documento.TipoDocumento)
                    : documento.TipoDocumentoCodigoCanonico;
                documento.TipoDocumentoNombre = string.IsNullOrWhiteSpace(documento.TipoDocumentoNombre)
                    ? RevisionDocumentalDisplayHelper.GetDocumentDisplayName(documento.TipoDocumento)
                    : documento.TipoDocumentoNombre;
                documento.OrdenVisual = documento.OrdenVisual > 0
                    ? documento.OrdenVisual
                    : RevisionDocumentalDisplayHelper.GetDocumentPriority(documento.TipoDocumento);
                documento.DecisionRevision = ObtenerDecisionRevisionDocumental(documento, revisiones);
                documento.ObservacionRevision = ObtenerObservacionRevisionDocumental(documento, revisiones);
                documento.EstadoRevisionVisible = RevisionDocumentalDisplayHelper.GetVisibleStateLabel(documento.Estado);
                documento.NombreArchivoGuardado = string.IsNullOrWhiteSpace(documento.RutaGuardada)
                    ? string.Empty
                    : Path.GetFileName(documento.RutaGuardada);

                var fechaCarga = documento.FechaCarga.HasValue
                    ? documento.FechaCarga.Value.ToString("yyyy-MM-dd HH:mm")
                    : "—";
                var usuarioCarga = string.IsNullOrWhiteSpace(documento.UsuarioRegistro)
                    ? "—"
                    : documento.UsuarioRegistro.Trim();
                documento.ResumenTrazabilidad = "v" + (documento.Version ?? 1) + " · " + fechaCarga + " · " + usuarioCarga;

                var tieneRuta = !string.IsNullOrWhiteSpace(documento.RutaGuardada);
                documento.PuedeVisualizar = puedeAccederArchivo && tieneRuta;
                documento.PuedeDescargar = puedeAccederArchivo && tieneRuta;
                documento.UrlVisualizar = documento.PuedeVisualizar
                    ? Url.Action("Descargar", "Documento", new { id = documento.CodigoDocumento, vistaPrevia = true })
                    : string.Empty;
                documento.UrlDescargar = documento.PuedeDescargar
                    ? Url.Action("Descargar", "Documento", new { id = documento.CodigoDocumento })
                    : string.Empty;
            }
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
                case "VALIDADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                case "DEVUELTO":
                case "DEVUELTO_INSPECTOR":
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

        private static string NormalizarEstadoDocumento(string estado)
        {
            var normalized = (estado ?? string.Empty).Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "APROBADO":
                case "VALIDADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                    return "DEVUELTO";
                default:
                    return normalized;
            }
        }

        private static bool DecisionRevisionRequiereObservacion(string decision)
        {
            var normalizada = NormalizarDecisionRevisionDocumental(decision);
            return normalizada == "DEVUELTO" || normalizada == "OBSERVADO";
        }

        private bool UsuarioPuedeOperarRevisionDocumental(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return false;
            }

            if (User != null && User.IsInRole("Administrador"))
            {
                return true;
            }

            var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            if (SolicitudAocrInfraBL.EsRevisionDocumentalPreAsignacion(solicitud, inspecciones))
            {
                var rolesRaw = RoleGroupingHelper.ExtractRoles(Session["RolesRaw"] ?? Session["Roles"], Session["Rol"] as string);
                var rolesEfectivos = RoleGroupingHelper.BuildUnifiedRoles(rolesRaw);
                var esInspectorTecnico = rolesEfectivos.Any(role => RoleGroupingHelper.IsInspectorTecnico(role))
                    || (User != null && (
                        User.IsInRole("Inspector")
                        || User.IsInRole("InspectorTecnico")
                        || User.IsInRole("Tecnico")
                        || User.IsInRole("EvaluadorTecnico")));

                var esCoordinacion = User != null && (
                    User.IsInRole("Coordinador")
                    || User.IsInRole("CoordinadorInspecciones")
                    || User.IsInRole("Coordinacion"));
                return esInspectorTecnico || esCoordinacion;
            }

            int usuarioActualId;
            TryObtenerUsuarioActualId(out usuarioActualId);
            var identidadInspector = ConstruirIdentidadInspectorActual(usuarioActualId);
            if (EsInspectorAsignadoActual(solicitud, inspecciones, identidadInspector))
            {
                return SolicitudEstaEnEtapaRevisionDocumental(solicitud.Estado);
            }

            var estadoRevision = _solicitudAocrInfraBL.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
                ?? new EstadoRevisionDocumental();
            return estadoRevision.VisibleEnBandejaInspector;
        }

        private InspectorIdentityContext ConstruirIdentidadInspectorActual(int usuarioId)
        {
            var identidad = _inspectorIdentityService.ObtenerIdentidadInspector(
                usuarioId,
                User != null && User.Identity != null ? User.Identity.Name : string.Empty,
                (Session["CodigoUsuario"] ?? string.Empty).ToString());
            return new InspectorIdentityContext
            {
                Ids = identidad != null ? identidad.Ids : new HashSet<int>(),
                Identificadores = identidad != null
                    ? identidad.Identificadores
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private bool EsInspectorAsignadoActual(SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspecciones, InspectorIdentityContext identidad)
        {
            var identidadServicio = new InspectorIdentityInfo
            {
                Ids = identidad != null ? identidad.Ids : new HashSet<int>(),
                Identificadores = identidad != null
                    ? identidad.Identificadores
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };

            var evaluacion = _inspectorIdentityService.EvaluarInspectorAsignado(
                solicitud != null ? solicitud.CodigoSolicitud : 0,
                solicitud,
                inspecciones,
                identidadServicio);

            return evaluacion != null && evaluacion.EsInspectorAsignado;
        }

        private sealed class InspectorIdentityContext
        {
            public HashSet<int> Ids { get; set; }
            public HashSet<string> Identificadores { get; set; }
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

        private JsonResult JsonGuardado(
            bool success,
            string message,
            object data = null,
            string redirectUrl = null,
            bool requiresCompanySelection = false,
            int? id = null)
        {
            return Json(new
            {
                success = success,
                ok = success,
                message = message ?? string.Empty,
                mensaje = message ?? string.Empty,
                data = data,
                redirectUrl = redirectUrl,
                requiresCompanySelection = requiresCompanySelection,
                id = id
            }, JsonRequestBehavior.AllowGet);
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
                    redirectUrl = (string)null,
                    legacy = legacy
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                ok = ok,
                success = ok,
                code = safeCode,
                message = safeMessage,
                mensaje = safeMessage,
                data = data,
                redirectUrl = (string)null
            }, JsonRequestBehavior.AllowGet);
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
