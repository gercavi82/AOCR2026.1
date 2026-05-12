using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaNegocio;
using CapaModelo;
using CapaModelo.Common;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaUtilidades;
using CapaPresentacion.Filters;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models;
using CapaPresentacion.Models.ViewModels;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using Rotativa;
using LoggingServiceType = CapaDatos.Services.ILoggingService;
using LoggingFactoryType = CapaDatos.Services.LoggingServiceFactory;
using SecureConfigType = CapaDatos.Services.SecureConfigurationService;
using ResultadoOperacion = CapaNegocio.Services.ResultadoOperacion;

namespace CapaPresentacion.Controllers
{
    [AocrAuthorize]
    public class InspeccionController : Controller
    {
        private readonly HallazgoBL _hallazgoBL;
        private readonly LoggingServiceType _logger;

        // ✅ Inyección simple (no static)
        private readonly InspeccionBL _inspeccionBL;
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly InspeccionHistorialDAO _historialDAO;
        private readonly InspeccionInformeDAO _informeDAO;
        private readonly ListaVerificacionOperacionalEaeDAO _listaVerificacionOperacionalEaeDAO;
        private readonly DocumentoInspeccionDAO _documentoDAO;
        private readonly UsuarioInternoRTDAO _usuarioInternoRTDAO;
        private readonly AocrFirmaPosicionDocumentoDAO _firmaPosicionDocumentoDAO;
        private readonly SolicitudAOCRDAO _solicitudDAO;
        private readonly InspeccionService _inspeccionService;
        private readonly InspeccionCorreoService _inspeccionCorreoService;
        private readonly FirmaDigitalService _firmaDigitalService;
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBL;

        private const string ROL_ADMIN = "Administrador";
        private const string ROL_COORD = "CoordinadorInspecciones";
        private const string ROL_COORD_ALIAS = "Coordinador";
        private const string ROL_INSPECTOR = "Inspector";
        private const string ROL_JEFATURA = "JefaturaTecnica";
        private const string ROL_JEFE = "Jefe";
        private const string ROL_DIRECCION = "Direccion";
        private const string ROL_DIRECTOR = "Director";
        private const string ROL_DIRDAC = "DIRDAC";
        private const string ROL_LEGAL = "Legal";
        private const string ROL_COORD_LEGAL = "CoordinacionLegal";
        private const string ROL_COORDINADOR_LEGAL = "CoordinadorLegal";
        private const string ROL_SOLICITANTE = "Solicitante";

        private const string ROLES_COORDINACION_Y_JEFATURA =
            ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_JEFATURA + "," + ROL_JEFE + "," + ROL_DIRECCION + "," + ROL_DIRECTOR + "," + ROL_LEGAL + "," + ROL_COORD_LEGAL + "," + ROL_COORDINADOR_LEGAL + "," + ROL_ADMIN;
        private const string ROLES_GESTION_INSPECCION =
            ROLES_COORDINACION_Y_JEFATURA + "," + ROL_INSPECTOR;
        private const string ROLES_GESTION_INSPECCION_CON_SOLICITANTE =
            ROLES_GESTION_INSPECCION + "," + ROL_SOLICITANTE;
        private const string ROLES_FIRMA_DIRDAC = ROL_DIRECCION + "," + ROL_DIRECTOR + "," + ROL_JEFATURA + "," + ROL_JEFE + "," + ROL_ADMIN + "," + ROL_DIRDAC;

        // Seguridad: tamaño máximo permitido para PDF (10MB)
        private const int MAX_PDF_BYTES = 10 * 1024 * 1024;

        // Carpeta de informes
        private const string CARPETA_VIRTUAL_INFORMES = "~/App_Data/Uploads/Inspecciones";
        private const string CARPETA_VIRTUAL_INFORMES_TECNICOS = "~/App_Data/Uploads/Inspecciones/InformesTecnicos";
        private const string CARPETA_VIRTUAL_INFORMES_TECNICOS_FIRMADOS = "~/App_Data/Uploads/Inspecciones/InformesTecnicos/Firmados";
        private const string CARPETA_VIRTUAL_LV_EAE = "~/App_Data/Uploads/Inspecciones/ListasVerificacionEae";
        private const string CARPETA_VIRTUAL_LV_EAE_FIRMADAS = "~/App_Data/Uploads/Inspecciones/ListasVerificacionEae/Firmadas";
        private const string CARPETA_VIRTUAL_TEMP_PDF = "~/App_Data/TempPdf";
        private const string CARPETA_VIRTUAL_ADJUNTOS_INFORME = "~/App_Data/Uploads/Inspecciones/InformesTecnicos/Adjuntos";
        private const string CARPETA_VIRTUAL_DOCUMENTOS_SOLICITANTE = "~/App_Data/Uploads/Inspecciones/DocumentosSolicitante";
        private static readonly IUserContextAccessor _userContext = new UserContextAccessor();

        public InspeccionController()
        {
            _hallazgoBL = new HallazgoBL();
            _inspeccionBL = new InspeccionBL();
            _inspeccionDAO = new InspeccionDAO();
            _historialDAO = new InspeccionHistorialDAO();
            _informeDAO = new InspeccionInformeDAO();
            _listaVerificacionOperacionalEaeDAO = new ListaVerificacionOperacionalEaeDAO();
            _documentoDAO = new DocumentoInspeccionDAO();
            _usuarioInternoRTDAO = new UsuarioInternoRTDAO();
            _firmaPosicionDocumentoDAO = new AocrFirmaPosicionDocumentoDAO();
            _solicitudDAO = new SolicitudAOCRDAO();
            _inspeccionService = new InspeccionService();
            _inspeccionCorreoService = new InspeccionCorreoService();
            _firmaDigitalService = new FirmaDigitalService();
            _solicitudEstadoTransitionBL = new SolicitudEstadoTransitionBL();
            _logger = LoggingFactoryType.Create();
        }

        private int ObtenerCodigoUsuario()
        {
            int id;
            return _userContext.TryGetCodigoUsuario(Session, out id) ? id : 0;
        }

        private bool EsAdmin() => User != null && User.IsInRole(ROL_ADMIN);

        private bool UsuarioTieneAlMenosUnRol(params string[] roles)
        {
            if (User == null || roles == null || roles.Length == 0)
            {
                return false;
            }

            return roles.Any(rol => !string.IsNullOrWhiteSpace(rol) && User.IsInRole(rol));
        }

        private bool EsRolCoordinacionYJefatura()
        {
            if (EsAdmin())
            {
                return true;
            }

            return UsuarioTieneAlMenosUnRol(
                ROL_COORD,
                ROL_COORD_ALIAS,
                ROL_JEFATURA,
                ROL_JEFE,
                ROL_DIRECCION,
                ROL_DIRECTOR,
                ROL_DIRDAC,
                ROL_LEGAL,
                ROL_COORD_LEGAL,
                ROL_COORDINADOR_LEGAL);
        }

        private bool EsRolDecisionCoordinacionJefatura()
        {
            if (EsAdmin())
            {
                return true;
            }

            return UsuarioTieneAlMenosUnRol(
                ROL_COORD,
                ROL_COORD_ALIAS,
                ROL_JEFATURA,
                ROL_JEFE,
                ROL_DIRECCION,
                ROL_DIRECTOR,
                ROL_DIRDAC);
        }

        private bool EsRolInspector()
        {
            return User != null && User.IsInRole(ROL_INSPECTOR);
        }

        private bool PuedeGestionarInformeTecnicoModal(Inspeccion inspeccion)
        {
            if (inspeccion == null || !PuedeAccederInspeccion(inspeccion))
            {
                return false;
            }

            return EsAdmin() || EsRolInspector() || EsRolDecisionCoordinacionJefatura();
        }

        private bool PuedeEditarInformeTecnicoModal(Inspeccion inspeccion)
        {
            if (inspeccion == null || !PuedeAccederInspeccion(inspeccion))
            {
                return false;
            }

            if (!EsAdmin() && !EsRolInspector())
            {
                return false;
            }

            return InspectorTieneRevisionDocumentalConfirmada(inspeccion);
        }

        private bool EsSolicitudAjaxInformeTecnico()
        {
            if (Request == null)
            {
                return false;
            }

            return Request.IsAjaxRequest()
                || string.Equals(Request["modalRequest"], "true", StringComparison.OrdinalIgnoreCase);
        }

        private ActionResult DevolverResultadoModalInformeTecnico(int statusCode, string mensaje)
        {
            if (EsSolicitudAjaxInformeTecnico())
            {
                Response.StatusCode = statusCode;
                Response.TrySkipIisCustomErrors = true;
                Response.SuppressFormsAuthenticationRedirect = true;
                return Json(new { success = false, message = mensaje }, JsonRequestBehavior.AllowGet);
            }

            return new HttpStatusCodeResult(statusCode, mensaje);
        }

        private JsonResult DevolverJsonErrorInformeTecnico(int statusCode, string mensaje)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            Response.SuppressFormsAuthenticationRedirect = true;
            return Json(new { success = false, code = statusCode, message = mensaje }, JsonRequestBehavior.AllowGet);
        }

        private bool PuedeAccederInspeccion(Inspeccion ins)
        {
            if (ins == null) return false;
            if (EsAdmin()) return true;

            if (User.IsInRole(ROL_SOLICITANTE))
            {
                return PuedeAccederSolicitante(ins);
            }

            if (EsRolCoordinacionYJefatura())
                return true;

            var codigoUsuario = ObtenerCodigoUsuario();
            if (EsRolInspector())
                return ins.CodigoInspector.HasValue && ins.CodigoInspector.Value == codigoUsuario;

            return false;
        }

        // ============================================================
        // ✅ LISTADO (POR ROL)
        // ============================================================
        [Authorize(Roles = ROLES_GESTION_INSPECCION)]
        public ActionResult Index(string vista = null)
        {
            _logger.LogInfo("[InspeccionesController] Inicio pantalla gestion inspecciones. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            List<Inspeccion> lista;

            if (EsRolCoordinacionYJefatura())
                lista = _inspeccionBL.ListarTodas();
            else
                lista = _inspeccionBL.ListarPorInspector(ObtenerCodigoUsuario());

            if (lista == null)
            {
                _logger.LogWarning("[InspeccionesController] Lista de inspecciones vino NULL.");
            }
            else if (lista.Count == 0)
            {
                _logger.LogWarning("[InspeccionesController] No hay inspecciones para el usuario actual.");
            }
            else
            {
                _logger.LogInfo("[InspeccionesController] Inspecciones recibidas=" + lista.Count);
            }

            // Resolver nombres de inspector por fila (cat·logo RT + solicitud).
            var nombresInspector = new Dictionary<int, string>();
            var solicitudesPorInspeccion = new Dictionary<int, SolicitudAOCR>();
            if (lista != null)
            {
                foreach (var insp in lista)
                {
                    if (insp == null) { continue; }
                    SolicitudAOCR solicitudFila = null;
                    try { solicitudFila = _solicitudDAO.ObtenerPorId(insp.CodigoSolicitud); } catch { }
                    nombresInspector[insp.CodigoInspeccion] = ResolverInspectorAsignadoNombre(insp, solicitudFila);
                    if (solicitudFila != null) { solicitudesPorInspeccion[insp.CodigoInspeccion] = solicitudFila; }
                }
            }
            ViewBag.InspectoresNombres = nombresInspector;
            ViewBag.SolicitudesPorInspeccion = solicitudesPorInspeccion;
            ViewBag.VistaActualInspeccion = vista ?? string.Empty;

            return View("~/Views/Inspeccion/Index.cshtml", lista);
        }

        // ============================================================
        // ✅ DETALLE
        // ============================================================
        [AocrAuthorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult Detalle(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            _logger.LogInfo("[GestionInspeccion] Inicio Detalle. InspeccionId=" + id + ", Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");

            _logger.LogInfo("[GestionInspeccion] InspeccionId=" + inspeccion.CodigoInspeccion + ", SolicitudId=" + inspeccion.CodigoSolicitud + ", EstadoActual=" + (inspeccion.Estado ?? "") + ", InspectorAsignado=" + (inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : "null"));

            if (!PuedeAccederInspeccion(inspeccion))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Rol sin permisos para detalle. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", InspeccionId=" + id);
                return new HttpStatusCodeResult(403, "No autorizado para ver esta inspección.");
            }

            _logger.LogInfo("[GestionInspeccion] PuedeGestionar=True para detalle. InspeccionId=" + id);

            try
            {
                ViewBag.Hallazgos = _hallazgoBL.ObtenerPorInspeccion(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando hallazgos en Detalle. InspeccionId=" + id + ", Error=" + ex.Message);
                ViewBag.Hallazgos = new List<Hallazgo>();
            }

            try
            {
                ViewBag.HistorialEstados = _historialDAO.ObtenerPorInspeccion(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando historial en Detalle. InspeccionId=" + id + ", Error=" + ex.Message);
                ViewBag.HistorialEstados = new List<InspeccionHistorialEstado>();
            }

            try
            {
                ViewBag.InformeTecnico = _informeDAO.ObtenerUltimoPorInspeccion(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando informe tecnico en Detalle. InspeccionId=" + id + ", Error=" + ex.Message);
                ViewBag.InformeTecnico = null;
            }

            try
            {
                ViewBag.DocumentosSolicitante = _documentoDAO.ObtenerPorInspeccion(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando documentos en Detalle. InspeccionId=" + id + ", Error=" + ex.Message);
                ViewBag.DocumentosSolicitante = new List<DocumentoInspeccion>();
            }

            try
            {
                ViewBag.Solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);

                var solicitud = ViewBag.Solicitud as SolicitudAOCR;
                if (solicitud != null)
                {
                    NormalizarDatosOperadorSolicitud(solicitud);

                    if (!inspeccion.CodigoInspector.HasValue && solicitud.CodigoTecnico.HasValue)
                    {
                        inspeccion.CodigoInspector = solicitud.CodigoTecnico;
                    }

                    if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula))
                    {
                        inspeccion.InspectorPrincipalCedula = solicitud.TecnicoResponsableCedula;
                    }

                    if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre))
                    {
                        inspeccion.InspectorPrincipalNombre = solicitud.TecnicoResponsableNombre;
                    }

                    if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalTipo))
                    {
                        inspeccion.InspectorPrincipalTipo = solicitud.TecnicoResponsableTipo;
                    }

                    if (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoCedula))
                    {
                        inspeccion.InspectorApoyoCedula = solicitud.InspectorApoyoCedula;
                    }

                    if (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoNombre))
                    {
                        inspeccion.InspectorApoyoNombre = solicitud.InspectorApoyoNombre;
                    }

                    if (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoTipo))
                    {
                        inspeccion.InspectorApoyoTipo = solicitud.InspectorApoyoTipo;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando solicitud en Detalle. InspeccionId=" + id + ", SolicitudId=" + inspeccion.CodigoSolicitud + ", Error=" + ex.Message);
                ViewBag.Solicitud = null;
            }

            AplicarPosicionesFirmaInformeTecnicoDetalle(
                inspeccion,
                ViewBag.Solicitud as SolicitudAOCR,
                ViewBag.InformeTecnico as InspeccionInformeTecnico);

            var informeDetalle = ViewBag.InformeTecnico as InspeccionInformeTecnico;
            if (!string.IsNullOrWhiteSpace(inspeccion.RutaInforme)
                && (informeDetalle == null || !informeDetalle.Finalizado || string.IsNullOrWhiteSpace(informeDetalle.RutaPdf)))
            {
                try
                {
                    var usuarioId = ObtenerCodigoUsuario();
                    var informeFirmable = AsegurarInformeTecnicoFirmable(inspeccion, informeDetalle, usuarioId);
                    if (informeFirmable != null)
                    {
                        ViewBag.InformeTecnico = informeFirmable;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[GestionInspeccion] No se pudo normalizar informe técnico para firma. InspeccionId="
                        + id + ", Error=" + ex.Message);
                }
            }

            informeDetalle = ViewBag.InformeTecnico as InspeccionInformeTecnico;
            ViewBag.RutaInformeTecnicoDisponible = ResolverRutaRelativaInformeDisponible(
                informeDetalle != null ? informeDetalle.RutaDocumentoFirmado : null,
                inspeccion.RutaInforme,
                informeDetalle != null ? informeDetalle.RutaPdf : null);

            EnriquecerInspectoresInformeTecnico(inspeccion, ViewBag.Solicitud as SolicitudAOCR);
            ViewBag.InspectorAsignadoNombre = ResolverInspectorAsignadoNombre(inspeccion, ViewBag.Solicitud as SolicitudAOCR);

            try
            {
                var solicitudLv = ViewBag.Solicitud as SolicitudAOCR;
                var listaVerificacion = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(id);
                if (listaVerificacion == null && UsaFlujoListaVerificacionOperacionalEae(solicitudLv))
                {
                    listaVerificacion = new ListaVerificacionOperacionalEae
                    {
                        CodigoInspeccion = id,
                        EstadoLista = "LV_BORRADOR"
                    };
                }
                HidratarListaVerificacionOperacionalEae(listaVerificacion, solicitudLv);
                ViewBag.ListaVerificacionOperacionalEae = listaVerificacion;
                ViewBag.UsaFlujoListaVerificacionOperacionalEae = UsaFlujoListaVerificacionOperacionalEae(solicitudLv);
                ViewBag.RutaListaVerificacionOperacionalEaeDisponible = ResolverRutaRelativaInformeDisponible(
                    listaVerificacion != null ? listaVerificacion.RutaDocumentoFirmado : null,
                    listaVerificacion != null ? listaVerificacion.RutaPdf : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando lista verificacion operacional EAE en Detalle. InspeccionId=" + id + ", Error=" + ex.Message);
                var listaFallback = new ListaVerificacionOperacionalEae
                {
                    CodigoInspeccion = id,
                    EstadoLista = "LV_BORRADOR"
                };
                HidratarListaVerificacionOperacionalEae(listaFallback, ViewBag.Solicitud as SolicitudAOCR);
                ViewBag.ListaVerificacionOperacionalEae = listaFallback;
                ViewBag.UsaFlujoListaVerificacionOperacionalEae = true;
                ViewBag.RutaListaVerificacionOperacionalEaeDisponible = null;
            }

            return View("~/Views/Inspeccion/Detalle.cshtml", inspeccion);
        }

        [HttpGet]
        [AocrAuthorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult ModalInformeTecnico(int codigoInspeccion)
        {
            if (codigoInspeccion <= 0)
            {
                return DevolverResultadoModalInformeTecnico(400, "Código de inspección inválido para cargar el Informe Técnico.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(codigoInspeccion);
            if (inspeccion == null)
            {
                return DevolverResultadoModalInformeTecnico(404, "La inspección solicitada no existe.");
            }

            if (!PuedeGestionarInformeTecnicoModal(inspeccion))
            {
                return DevolverResultadoModalInformeTecnico(403, "No autorizado para abrir el Informe Técnico de esta inspección.");
            }

            if ((EsRolInspector() || EsAdmin()) && !InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                return DevolverResultadoModalInformeTecnico(403, ObtenerMensajeBloqueoRevisionDocumentalInspector());
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitud);

            ListaVerificacionOperacionalEae listaVerificacion;
            string mensajeLista;
            if (!ValidarPrecondicionInformeTecnico(inspeccion, solicitud, false, out listaVerificacion, out mensajeLista))
            {
                _logger.LogWarning("[GestionInspeccion] Apertura modal informe bloqueada. InspeccionId=" + codigoInspeccion + ", Mensaje=" + (mensajeLista ?? string.Empty));
                return DevolverResultadoModalInformeTecnico(409, mensajeLista);
            }

            IList<DocumentoInspeccion> documentosSolicitante;
            try
            {
                documentosSolicitante = _documentoDAO.ObtenerPorInspeccion(codigoInspeccion) ?? new List<DocumentoInspeccion>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando documentos para modal informe. InspeccionId=" + codigoInspeccion + ", Error=" + ex.Message);
                documentosSolicitante = new List<DocumentoInspeccion>();
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(codigoInspeccion);
            EnriquecerInspectoresInformeTecnico(inspeccion, solicitud);

            var vm = ConstruirInformeTecnicoModalViewModel(inspeccion, solicitud, informe, listaVerificacion, documentosSolicitante);
            _logger.LogInfo("[GestionInspeccion] ModalInformeTecnico cargado. InspeccionId=" + codigoInspeccion
                + ", InformeId=" + (vm.CodigoInformeTecnico.HasValue ? vm.CodigoInformeTecnico.Value.ToString() : "0")
                + ", EstadoInforme=" + (vm.EstadoInformeTecnico ?? string.Empty)
                + ", EstadoLv=" + (vm.EstadoListaVerificacion ?? string.Empty));

            return PartialView("~/Views/InformeTecnico/_ModalInformeTecnico.cshtml", vm);
        }

        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerHallazgo(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var hallazgo = _hallazgoBL.ObtenerPorId(id);
            if (hallazgo == null)
            {
                return HttpNotFound("Hallazgo no encontrado.");
            }

            return RedirectToAction("Detalle", new { id = hallazgo.CodigoInspeccion });
        }

        [HttpGet]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC)]
        public ActionResult PendientesFirmaDirdac()
        {
            try
            {
                var pendientes = (_informeDAO.ListarPendientesFirmaDirdac() ?? new List<InspeccionInformeTecnico>())
                    .Select(informe =>
                    {
                        var inspeccion = _inspeccionDAO.ObtenerPorId(informe.CodigoInspeccion);
                        var solicitud = inspeccion != null ? _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud) : null;
                        return new
                        {
                            Inspeccion = inspeccion,
                            Solicitud = solicitud,
                            Informe = informe
                        };
                    })
                    .Where(x => x.Inspeccion != null)
                    .OrderByDescending(x => x.Informe.FechaEnvioDirdac ?? x.Informe.FechaFinalizacion ?? x.Informe.UpdatedAt ?? DateTime.MinValue)
                    .Select(x => Tuple.Create(x.Inspeccion, x.Solicitud, x.Informe))
                    .ToList();

                return View("~/Views/Inspeccion/PendientesFirmaDirdac.cshtml", pendientes);
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error cargando pendientes DIRDAC: " + ex);
                TempData["Error"] = "No se pudo cargar el listado de documentos pendientes de revision Direccion/Jefatura.";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE + "," + ROL_DIRDAC)]
        public ActionResult PreviewFirmaInformeTecnico(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion) && !User.IsInRole(ROL_DIRECCION) && !User.IsInRole(ROL_DIRECTOR) && !User.IsInRole(ROL_DIRDAC) && !EsAdmin())
            {
                return new HttpStatusCodeResult(403, "No autorizado para visualizar la firma del informe técnico.");
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitud);
            EnriquecerInspectoresInformeTecnico(inspeccion, solicitud);
            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var vm = new InformeTecnicoPdfViewModel
            {
                Inspeccion = inspeccion,
                Solicitud = solicitud,
                Informe = informe
            };

            return View("~/Views/Inspeccion/InformeTecnicoFirmaPreview.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC + "," + ROL_INSPECTOR)]
        public JsonResult GuardarPosicionFirmaInformeTecnico(int id, string rolFirmaVisual, int numeroPaginaFirma, string posicionFirmaX, string posicionFirmaY, string anchoFirma, string altoFirma)
        {
            try
            {
                if (id <= 0)
                {
                    Response.StatusCode = 400;
                    return Json(new { ok = false, mensaje = "Inspección inválida para guardar la posición de firma." });
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(id);
                if (inspeccion == null)
                {
                    Response.StatusCode = 404;
                    return Json(new { ok = false, mensaje = "Inspección no encontrada." });
                }

                if (!PuedeFirmarInformePorRol(inspeccion, rolFirmaVisual))
                {
                    Response.StatusCode = 403;
                    return Json(new { ok = false, mensaje = "No autorizado para configurar la posición de esta firma." });
                }

                if (User.IsInRole(ROL_INSPECTOR) && !InspectorTieneRevisionDocumentalConfirmada(inspeccion))
                {
                    Response.StatusCode = 403;
                    return Json(new { ok = false, mensaje = ObtenerMensajeBloqueoRevisionDocumentalInspector() });
                }

                var posicion = ConstruirPosicionFirmaVisualDesdeValores(numeroPaginaFirma, posicionFirmaX, posicionFirmaY, anchoFirma, altoFirma);
                if (posicion == null || !posicion.EsValida)
                {
                    Response.StatusCode = 400;
                    return Json(new { ok = false, mensaje = "Las coordenadas de firma del informe técnico son inválidas." });
                }

                GuardarPosicionFirmaInformeTecnico(inspeccion, rolFirmaVisual, posicion);
                return Json(new { ok = true, mensaje = "La posición de firma del informe técnico fue guardada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error guardando posición de firma informe técnico. InspeccionId=" + id + ", Rol=" + (rolFirmaVisual ?? string.Empty) + ", Error=" + ex);
                Response.StatusCode = 500;
                return Json(new { ok = false, mensaje = "Ocurrió un error interno al guardar la posición de firma." });
            }
        }

        // ============================================================
        // ✅ CREAR (GET)
        // ============================================================
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        public ActionResult Crear(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0) return new HttpStatusCodeResult(400, "Código de solicitud inválido.");

            var modelo = new Inspeccion { CodigoSolicitud = codigoSolicitud };
            return View("~/Views/Inspeccion/Crear.cshtml", modelo);
        }

        // ============================================================
        // ✅ CREAR (POST)
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(Inspeccion model, string tipoInspector = "OPS")
        {
            if (model == null) return new HttpStatusCodeResult(400, "Modelo inválido.");

            if (!ModelState.IsValid)
                return View("~/Views/Inspeccion/Crear.cshtml", model);

            string mensajeInspector;
            if (!ResolverInspectoresAs400(model, tipoInspector, out mensajeInspector))
            {
                ViewBag.Error = mensajeInspector;
                return View("~/Views/Inspeccion/Crear.cshtml", model);
            }

            var codigoUsuario = ObtenerCodigoUsuario();
            if (codigoUsuario <= 0)
            {
                ViewBag.Error = "No se pudo identificar el usuario en sesión.";
                return View("~/Views/Inspeccion/Crear.cshtml", model);
            }

            // ✅ Crear ahora devuelve int (id)
            int newId = _inspeccionBL.Crear(model, codigoUsuario);
            bool ok = newId > 0;

            if (ok)
                return RedirectToAction("Detalle", new { id = newId });

            ViewBag.Error = "No se pudo crear la inspección.";
            return View("~/Views/Inspeccion/Crear.cshtml", model);
        }

        // ============================================================
        // ✅ EDITAR (GET)
        // ============================================================
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        public ActionResult Editar(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");

            return View("~/Views/Inspeccion/Editar.cshtml", inspeccion);
        }

        // ============================================================
        // ✅ EDITAR (POST)
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(Inspeccion model, string tipoInspector = "TODOS")
        {
            if (model == null) return new HttpStatusCodeResult(400, "Modelo inválido.");

            if (!ModelState.IsValid)
                return View("~/Views/Inspeccion/Editar.cshtml", model);

            string mensajeInspector;
            if (!ResolverInspectoresAs400(model, tipoInspector, out mensajeInspector))
            {
                ViewBag.Error = mensajeInspector;
                return View("~/Views/Inspeccion/Editar.cshtml", model);
            }

            int usuarioId = ObtenerCodigoUsuario();
            model.UpdatedAt = DateTime.Now;
            model.UpdatedBy = usuarioId;

            // ✅ Ahora requiere updatedBy
            bool ok = _inspeccionBL.Actualizar(model, usuarioId);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Inspección actualizada correctamente."
                : "No se pudo actualizar la inspección.";

            return RedirectToAction("Detalle", new { id = model.CodigoInspeccion });
        }

        // ============================================================
        // ✅ CAMBIAR ESTADO
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROLES_GESTION_INSPECCION)]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstado(int id, string estado, string returnUrl = null)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            _logger.LogInfo("[GestionInspeccion] Inicio CambiarEstado. InspeccionId=" + id + ", EstadoSolicitado=" + (estado ?? "") + ", Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                TempData["Error"] = "Inspección no encontrada.";
                return RedirectToAction("Index");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Sin acceso a inspeccion. InspeccionId=" + id + ", Usuario=" + ObtenerUsuarioActual());
                TempData["Error"] = "No tiene permisos para gestionar esta inspección.";
                return RedirigirTrasCambioEstado(id, returnUrl);
            }

            if (string.IsNullOrWhiteSpace(estado))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Estado destino vacio.");
                TempData["Error"] = "Debe seleccionar un estado.";
                return RedirigirTrasCambioEstado(id, returnUrl);
            }

            var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            var estadoDestino = EstadosInspeccion.NormalizarEstado(estado);

            if (!EstadosInspeccion.EsTransicionValida(estadoActual, estadoDestino))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Transicion no permitida. EstadoActual=" + estadoActual + ", EstadoDestino=" + estadoDestino + ", InspeccionId=" + id);
                TempData["Error"] = "Transición no permitida: " + estadoActual + " -> " + estadoDestino;
                return RedirigirTrasCambioEstado(id, returnUrl);
            }

            if (EstadosInspeccion.EsEstadoBloqueInspector(estadoDestino) && !InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirigirTrasCambioEstado(id, returnUrl);
            }

            if (!UsuarioActualPuedeCambiarEstadoInspeccion(estadoActual, estadoDestino))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Rol sin permisos para estado destino. EstadoDestino=" + estadoDestino + ", Rol=" + ObtenerRolActual());
                TempData["Error"] = "No tiene permisos para cambiar a ese estado.";
                return RedirigirTrasCambioEstado(id, returnUrl);
            }

            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string bloqueBpmn = EstadosInspeccion.EsEstadoBloqueInspector(estadoDestino)
                    ? "OPERACION_INSPECTOR"
                    : "COORDINACION_Y_JEFATURA";

                SolicitudAOCR solicitudAocr = null;
                if (string.Equals(estadoDestino, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase))
                {
                    string mensajeValidacion;
                    string claveTempData;
                    if (!PuedeIniciarInspeccionAocr(inspeccion, out solicitudAocr, out mensajeValidacion, out claveTempData))
                    {
                        TempData[claveTempData] = mensajeValidacion;
                        return RedirigirTrasCambioEstado(id, returnUrl);
                    }
                }

                var opcionesTx = new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TransactionManager.MaximumTimeout
                };

                bool ok;
                using (var scope = new TransactionScope(TransactionScopeOption.Required, opcionesTx))
                {
                    ok = _inspeccionBL.CambiarEstado(
                        id,
                        estadoDestino,
                        codigoUsuario,
                        "Cambio de estado BPMN desde bloque " + bloqueBpmn + ".",
                        ObtenerUsuarioActual(),
                        bloqueBpmn);

                    if (ok && string.Equals(estadoDestino, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase))
                    {
                        string mensajeCambioSolicitud;
                        if (!SincronizarSolicitudAocrAlIniciarInspeccion(solicitudAocr, codigoUsuario, out mensajeCambioSolicitud))
                        {
                            throw new ApplicationException(string.IsNullOrWhiteSpace(mensajeCambioSolicitud)
                                ? "No se pudo sincronizar la solicitud AOCR al iniciar la inspección."
                                : mensajeCambioSolicitud);
                        }
                    }

                    if (ok)
                    {
                        scope.Complete();
                    }
                }

                string estadoPersistido = "N/A";
                if (ok)
                {
                    var inspeccionActualizada = _inspeccionDAO.ObtenerPorId(id);
                    if (inspeccionActualizada != null)
                    {
                        estadoPersistido = EstadosInspeccion.NormalizarEstado(inspeccionActualizada.Estado);
                    }

                    if (!string.Equals(estadoPersistido, estadoDestino, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("[GestionInspeccion] DesalineacionEstadoDetectada. InspeccionId=" + id + ", EstadoSolicitado=" + estadoDestino + ", EstadoPersistido=" + estadoPersistido + ", Usuario=" + ObtenerUsuarioActual());
                    }
                }

                _logger.LogInfo("[GestionInspeccion] PuedeGestionar=" + ok + ", InspeccionId=" + id + ", EstadoDestino=" + estadoDestino + ", EstadoPersistido=" + estadoPersistido + ", Usuario=" + ObtenerUsuarioActual());

                if (ok && string.Equals(estadoDestino, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = "Inspección iniciada correctamente. La solicitud AOCR quedó sincronizada y el flujo debe continuar desde este módulo.";
                }
                else
                {
                    TempData[ok ? "Success" : "Error"] = ok
                        ? "Estado actualizado correctamente."
                        : "No se pudo actualizar el estado.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en CambiarEstado: " + ex);
                TempData["Error"] = ex.Message;
            }

            return RedirigirTrasCambioEstado(id, returnUrl);
        }

        // ============================================================
        // ✅✅✅ VER INFORME (ÚNICO) - SEGURO
        // ============================================================
        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerInforme(int id)
        {
            return ServirInformePdf(id, false);
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult DescargarInforme(int id)
        {
            return ServirInformePdf(id, true);
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerListaVerificacionOperacionalEae(int id)
        {
            return ServirListaVerificacionOperacionalEaePdf(id, false);
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult DescargarListaVerificacionOperacionalEae(int id)
        {
            return ServirListaVerificacionOperacionalEaePdf(id, true);
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerLvEaeOficial(int codigoInspeccion)
        {
            return GenerarResultadoPdfListaVerificacionOperacionalEaeOficial(codigoInspeccion, false);
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult DescargarLvEaeOficial(int codigoInspeccion)
        {
            return GenerarResultadoPdfListaVerificacionOperacionalEaeOficial(codigoInspeccion, true);
        }

        private ActionResult ServirInformePdf(int id, bool descargar)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
                return HttpNotFound("Inspección no encontrada.");

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para ver el informe.");

            var informeTecnico = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitud);
            var rutasCandidatas = new[]
            {
                informeTecnico != null ? informeTecnico.RutaDocumentoFirmado : null,
                inspeccion.RutaInforme,
                informeTecnico != null ? informeTecnico.RutaPdf : null
            }
            .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
            .Select(NormalizarRutaRelativaInforme)
            .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            if (rutasCandidatas.Count == 0)
            {
                _logger.LogWarning("[GestionInspeccion] VerInforme sin ruta. InspeccionId=" + id);
                return HttpNotFound("La inspección aún no tiene informe cargado.");
            }

            var baseDir = Server.MapPath(CARPETA_VIRTUAL_INFORMES);
            string rutaRelativa = null;
            string fullPath = null;
            var rutasFueraBase = new List<string>();

            foreach (var rutaCandidata in rutasCandidatas)
            {
                var fullPathCandidata = Server.MapPath("~" + rutaCandidata);
                if (!EsRutaDentroDeBase(fullPathCandidata, baseDir))
                {
                    rutasFueraBase.Add(rutaCandidata);
                    continue;
                }

                if (System.IO.File.Exists(fullPathCandidata))
                {
                    rutaRelativa = rutaCandidata;
                    fullPath = fullPathCandidata;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                if (rutasFueraBase.Count > 0)
                {
                    _logger.LogWarning("[GestionInspeccion] VerInforme rutas fuera de base. InspeccionId=" + id + ", Rutas=" + string.Join(" | ", rutasFueraBase));
                }

                _logger.LogWarning("[GestionInspeccion] VerInforme archivo inexistente. InspeccionId=" + id + ", RutasIntentadas=" + string.Join(" | ", rutasCandidatas));
                Response.TrySkipIisCustomErrors = true;
                return new HttpStatusCodeResult(404, "El archivo del informe firmado ya no existe en el servidor.");
            }

            if (!string.Equals(rutaRelativa, rutasCandidatas[0], StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInfo("[GestionInspeccion] VerInforme resolvio ruta alternativa. InspeccionId=" + id + ", RutaUsada=" + rutaRelativa + ", RutaPreferida=" + rutasCandidatas[0]);
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, ConstruirNombrePdfInformeTecnico(inspeccion, solicitud, informeTecnico));

            return File(fullPath, "application/pdf");
        }

        private ActionResult ServirListaVerificacionOperacionalEaePdf(int id, bool descargar)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para ver la lista de verificación operacional.");
            }

            var lista = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(id);
            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitud);
            var rutasCandidatas = new[]
            {
                lista != null ? lista.RutaDocumentoFirmado : null,
                lista != null ? lista.RutaPdf : null
            }
            .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
            .Select(NormalizarRutaRelativaInforme)
            .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            if (rutasCandidatas.Count == 0)
            {
                _logger.LogWarning("[GestionInspeccion] VerListaVerificacionOperacionalEae sin ruta. InspeccionId=" + id);
                Response.TrySkipIisCustomErrors = true;
                return HttpNotFound("La inspección aún no tiene una lista de verificación operacional generada.");
            }

            var baseDir = Server.MapPath(CARPETA_VIRTUAL_INFORMES);
            string rutaRelativa = null;
            string fullPath = null;
            var rutasFueraBase = new List<string>();

            foreach (var rutaCandidata in rutasCandidatas)
            {
                var fullPathCandidata = Server.MapPath("~" + rutaCandidata);
                if (!EsRutaDentroDeBase(fullPathCandidata, baseDir))
                {
                    rutasFueraBase.Add(rutaCandidata);
                    continue;
                }

                if (System.IO.File.Exists(fullPathCandidata))
                {
                    rutaRelativa = rutaCandidata;
                    fullPath = fullPathCandidata;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                if (rutasFueraBase.Count > 0)
                {
                    _logger.LogWarning("[GestionInspeccion] VerListaVerificacionOperacionalEae rutas fuera de base. InspeccionId=" + id + ", Rutas=" + string.Join(" | ", rutasFueraBase));
                }

                _logger.LogWarning("[GestionInspeccion] VerListaVerificacionOperacionalEae archivo inexistente. InspeccionId=" + id + ", RutasIntentadas=" + string.Join(" | ", rutasCandidatas));
                Response.TrySkipIisCustomErrors = true;
                return new HttpStatusCodeResult(404, "El archivo de la lista de verificación ya no existe en el servidor.");
            }

            if (!string.Equals(rutaRelativa, rutasCandidatas[0], StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInfo("[GestionInspeccion] VerListaVerificacionOperacionalEae resolvio ruta alternativa. InspeccionId=" + id + ", RutaUsada=" + rutaRelativa + ", RutaPreferida=" + rutasCandidatas[0]);
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, ConstruirNombrePdfListaVerificacionOperacionalEae(inspeccion, solicitud, lista));
            return File(fullPath, "application/pdf");
        }

        private ActionResult GenerarResultadoPdfListaVerificacionOperacionalEaeOficial(int codigoInspeccion, bool descargar)
        {
            if (codigoInspeccion <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(codigoInspeccion);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para generar la lista de verificación operacional oficial.");
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitud);
            if (!UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                return new HttpStatusCodeResult(409, "La lista de verificación operacional EAE no aplica para esta inspección.");
            }

            var lista = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(codigoInspeccion);
            if (lista == null)
            {
                return HttpNotFound("La inspección aún no tiene una lista de verificación operacional generada.");
            }

            var vm = ConstruirViewModelListaVerificacionOperacionalEaePdfOficial(inspeccion, solicitud, lista);
            var pdf = CrearPdfListaVerificacionOperacionalEaeOficial(vm);
            var pdfBytes = pdf.BuildFile(ControllerContext);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, descargar, ConstruirNombrePdfListaVerificacionOperacionalEae(inspeccion, solicitud, lista));
            return File(pdfBytes, "application/pdf");
        }

        [HttpPost]
        [Authorize(Roles = ROL_ADMIN)]
        public ActionResult RegenerarHistoricosPdfLvEaeOficial()
        {
            if (Request == null || !Request.IsLocal)
            {
                return new HttpStatusCodeResult(403, "Esta operación solo está disponible localmente.");
            }

            var usuarioId = ObtenerCodigoUsuario();
            var registros = _listaVerificacionOperacionalEaeDAO.ListarConPdfHistorico();
            var items = new List<object>();
            var pendientesRefirma = new List<object>();
            var regenerados = 0;
            var yaCorrectos = 0;
            var errores = 0;

            foreach (var lista in registros)
            {
                try
                {
                    var inspeccion = _inspeccionDAO.ObtenerPorId(lista.CodigoInspeccion);
                    if (inspeccion == null)
                    {
                        errores++;
                        items.Add(new
                        {
                            codigoInspeccion = lista.CodigoInspeccion,
                            version = lista.Version,
                            codigoLv = lista.CodigoListaVerificacion,
                            error = "Inspección no encontrada."
                        });
                        continue;
                    }

                    var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                    NormalizarDatosOperadorSolicitud(solicitud);
                    if (!UsaFlujoListaVerificacionOperacionalEae(solicitud))
                    {
                        items.Add(new
                        {
                            codigoInspeccion = lista.CodigoInspeccion,
                            version = lista.Version,
                            codigoLv = lista.CodigoListaVerificacion,
                            omitido = true,
                            motivo = "La inspección no usa flujo LV/EAE."
                        });
                        continue;
                    }

                    HidratarListaVerificacionOperacionalEae(lista, solicitud);

                    var paginasAntes = ObtenerNumeroPaginasPdfArchivo(lista.RutaPdf);
                    var paginasFirmadas = ObtenerNumeroPaginasPdfArchivo(lista.RutaDocumentoFirmado);
                    var pdfBytes = GenerarPdfListaVerificacionOperacionalEae(inspeccion, solicitud, lista);
                    var paginasGeneradas = ObtenerNumeroPaginasPdf(pdfBytes);
                    var rutaPdfFinal = GuardarOReemplazarListaVerificacionOperacionalEaePdfHistorico(lista, pdfBytes, usuarioId);
                    var paginasDespues = ObtenerNumeroPaginasPdfArchivo(rutaPdfFinal);
                    var reemplazado = paginasAntes != 7 || paginasDespues != paginasAntes || string.IsNullOrWhiteSpace(lista.RutaPdf);

                    if (reemplazado)
                    {
                        regenerados++;
                    }
                    else if (paginasDespues == 7)
                    {
                        yaCorrectos++;
                    }

                    if (lista.FirmadoTecnico && !string.IsNullOrWhiteSpace(lista.RutaDocumentoFirmado) && paginasFirmadas > 0 && paginasFirmadas != 7)
                    {
                        pendientesRefirma.Add(new
                        {
                            codigoInspeccion = lista.CodigoInspeccion,
                            version = lista.Version,
                            codigoLv = lista.CodigoListaVerificacion,
                            rutaDocumentoFirmado = lista.RutaDocumentoFirmado,
                            paginasDocumentoFirmado = paginasFirmadas,
                            motivo = "El PDF firmado histórico conserva la versión previa y requiere re-firma manual con el certificado original."
                        });
                    }

                    items.Add(new
                    {
                        codigoInspeccion = lista.CodigoInspeccion,
                        version = lista.Version,
                        codigoLv = lista.CodigoListaVerificacion,
                        paginasAntes,
                        paginasGeneradas,
                        paginasDespues,
                        rutaPdf = rutaPdfFinal,
                        firmadoTecnico = lista.FirmadoTecnico,
                        paginasDocumentoFirmado = paginasFirmadas,
                        reemplazado
                    });
                }
                catch (Exception ex)
                {
                    errores++;
                    _logger.LogError("[GestionInspeccion] Error regenerando PDF histórico LV/EAE. CodigoLv=" + (lista != null ? lista.CodigoListaVerificacion.ToString() : "0") + ", Error=" + ex);
                    items.Add(new
                    {
                        codigoInspeccion = lista != null ? lista.CodigoInspeccion : 0,
                        version = lista != null ? lista.Version : 0,
                        codigoLv = lista != null ? lista.CodigoListaVerificacion : 0,
                        error = ex.Message
                    });
                }
            }

            return Json(new
            {
                ok = true,
                total = registros.Count,
                regenerados,
                yaCorrectos,
                pendientesRefirma = pendientesRefirma.Count,
                errores,
                items,
                pendientesDocumentoFirmado = pendientesRefirma
            });
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerDocumentoSolicitante(int documentoId)
        {
            if (documentoId <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var documento = _documentoDAO.ObtenerPorId(documentoId);
            if (documento == null)
            {
                return HttpNotFound("Documento no encontrado.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(documento.CodigoInspeccion);
            if (inspeccion == null || !PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para ver este documento.");
            }

            var rutaRelativa = documento.RutaArchivo;
            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                return HttpNotFound("El documento no tiene ruta asociada.");
            }

            if (!rutaRelativa.StartsWith("~"))
            {
                rutaRelativa = "~" + (rutaRelativa.StartsWith("/") ? rutaRelativa : "/" + rutaRelativa);
            }

            var fullPath = Server.MapPath(rutaRelativa);
            var baseDir = Server.MapPath(CARPETA_VIRTUAL_DOCUMENTOS_SOLICITANTE);
            if (!EsRutaDentroDeBase(fullPath, baseDir) || !System.IO.File.Exists(fullPath))
            {
                return HttpNotFound("El archivo solicitado no existe.");
            }

            var contentType = string.IsNullOrWhiteSpace(documento.ContentType) ? "application/octet-stream" : documento.ContentType;
            return File(fullPath, contentType, documento.NombreArchivoOriginal ?? documento.NombreArchivoStorage ?? ("Documento_" + documentoId));
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmarRevisionDocumentalInspector(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para confirmar la revisión documental.");
            }

            if (InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Success"] = "La revisión documental ya fue confirmada previamente.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            inspeccion.EstadoDocumental = "EN_REVISION";
            inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                ? "Inspector confirmó revisión documental."
                : inspeccion.Comentarios + " | Inspector confirmó revisión documental.";

            var ok = _inspeccionBL.Actualizar(inspeccion, usuarioId);
            TempData[ok ? "Success" : "Error"] = ok
                ? "Revisión documental confirmada. Ya puede ejecutar acciones del inspector."
                : "No se pudo confirmar la revisión documental.";

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [AocrAuthorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult GuardarInformeTecnico()
        {
            var id = 0;
            var esSolicitudAjax = false;
            try
            {
                var form = Request?.Unvalidated?.Form;
                esSolicitudAjax = EsSolicitudAjaxInformeTecnico();
                var idRaw = form?["id"] ?? Request?.Unvalidated?.QueryString["id"];
                var finalizarRaw = form?["finalizar"] ?? Request?.Unvalidated?.QueryString["finalizar"];
                var finalizar = false;

                int.TryParse(idRaw, out id);
                if (!string.IsNullOrWhiteSpace(finalizarRaw))
                {
                    bool.TryParse(finalizarRaw, out finalizar);
                }

                if (id <= 0)
                {
                    return DevolverResultadoModalInformeTecnico(400, "ID inválido.");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(id);
                if (inspeccion == null)
                {
                    return DevolverResultadoModalInformeTecnico(404, "Inspección no encontrada.");
                }

                if (!PuedeAccederInspeccion(inspeccion))
                {
                    return DevolverResultadoModalInformeTecnico(403, "No autorizado para editar el informe técnico.");
                }

                if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
                {
                    if (esSolicitudAjax)
                    {
                        return DevolverResultadoModalInformeTecnico(403, ObtenerMensajeBloqueoRevisionDocumentalInspector());
                    }

                    TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                    return RedirectToAction("Detalle", new { id });
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                NormalizarDatosOperadorSolicitud(solicitud);

                ListaVerificacionOperacionalEae listaVerificacion;
                string mensajeLista;
                if (!ValidarPrecondicionInformeTecnico(inspeccion, solicitud, false, out listaVerificacion, out mensajeLista))
                {
                    if (esSolicitudAjax)
                    {
                        return DevolverResultadoModalInformeTecnico(409, mensajeLista);
                    }

                    TempData["Error"] = mensajeLista;
                    return RedirectToAction("Detalle", new { id });
                }

                var usuarioId = ObtenerCodigoUsuario();
                var informeActual = _informeDAO.ObtenerUltimoPorInspeccion(id);
                var informeFormulario = ConstruirInformeTecnicoDesdeFormulario(id, form, informeActual, true);
                string mensajeInforme;
                if (finalizar && !ValidarInformeTecnicoParaFinalizar(informeFormulario, out mensajeInforme))
                {
                    if (esSolicitudAjax)
                    {
                        return DevolverResultadoModalInformeTecnico(400, mensajeInforme);
                    }

                    TempData["Error"] = mensajeInforme;
                    return RedirectToAction("Detalle", new { id });
                }

                var informe = _informeDAO.GuardarBorrador(informeFormulario, usuarioId);

                if (!finalizar)
                {
                    if (esSolicitudAjax)
                    {
                        return Json(new
                        {
                            success = true,
                            finalized = false,
                            message = "Borrador del informe técnico guardado correctamente.",
                            estado = FirstNonEmpty(informe != null ? informe.EstadoInforme : null, "BORRADOR_INFORME"),
                            codigoInforme = informe != null ? informe.CodigoInforme : 0,
                            version = informe != null ? informe.Version : 0
                        });
                    }

                    TempData["Success"] = "Borrador del informe técnico guardado correctamente.";
                    return RedirectToAction("Detalle", new { id });
                }

                var pdfBytes = GenerarPdfInformeTecnico(inspeccion, solicitud, informe);
                var rutaPdf = GuardarInformeTecnicoPdf(id, informe.Version, pdfBytes);
                var detalleAuditoriaInforme = ConstruirDetalleAuditoriaResultadoInforme("Informe técnico generado en PDF.", informe);

                _informeDAO.MarcarFinalizado(informe.CodigoInforme, rutaPdf, false, UsaFlujoListaVerificacionOperacionalEae(solicitud) ? "INFORME_GENERADO" : "GENERADO", usuarioId);
                _inspeccionBL.GuardarInforme(id, rutaPdf, usuarioId);
                RegistrarAuditoriaInformeDigital(id, "BORRADOR", "GENERADO", rutaPdf, null, detalleAuditoriaInforme, usuarioId, ObtenerUsuarioActual(), "INFORME_GENERADO");

                var estadoActual = CapaDatos.Constants.EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                if (!string.Equals(estadoActual, CapaDatos.Constants.EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(estadoActual, CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase) &&
                        CapaDatos.Constants.EstadosInspeccion.EsTransicionValida(estadoActual, CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION))
                    {
                        _inspeccionBL.CambiarEstado(id, CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION, usuarioId, "Inicio técnico previo a cierre de informe.", ObtenerUsuarioActual(), "INFORME_TECNICO");
                        estadoActual = CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION;
                    }

                    if (CapaDatos.Constants.EstadosInspeccion.EsTransicionValida(estadoActual, CapaDatos.Constants.EstadosInspeccion.INFORME_ELABORADO))
                    {
                        _inspeccionBL.CambiarEstado(id, CapaDatos.Constants.EstadosInspeccion.INFORME_ELABORADO, usuarioId, "Informe técnico finalizado y PDF generado.", ObtenerUsuarioActual(), "INFORME_TECNICO");
                    }
                }

                TempData["Success"] = "Informe técnico finalizado y PDF generado. El documento quedó pendiente de firma del inspector.";

                if (esSolicitudAjax)
                {
                    return Json(new
                    {
                        success = true,
                        finalized = true,
                        message = "Informe técnico finalizado y PDF generado. El documento quedó pendiente de firma del inspector.",
                        estado = UsaFlujoListaVerificacionOperacionalEae(solicitud) ? "INFORME_GENERADO" : "GENERADO",
                        codigoInforme = informe.CodigoInforme,
                        version = informe.Version,
                        pdfUrl = Url.Action("VerInforme", "Inspeccion", new { id }),
                        downloadUrl = Url.Action("DescargarInforme", "Inspeccion", new { id }),
                        redirectUrl = Url.Action("Detalle", "Inspeccion", new { id })
                    });
                }

                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en GuardarInformeTecnico: " + ex);

                if (esSolicitudAjax)
                {
                    Response.StatusCode = 500;
                    return Json(new
                    {
                        success = false,
                        message = "No se pudo guardar el informe técnico. Verifique los datos ingresados e intente nuevamente."
                    });
                }

                TempData["Error"] = "No se pudo guardar el informe técnico. Verifique los datos ingresados e intente nuevamente.";
                return RedirectToAction("Detalle", new { id });
            }
        }

        [HttpPost]
        [AocrAuthorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public JsonResult PrevisualizarInformeTecnico()
        {
            var id = 0;
            try
            {
                var form = Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form : null;
                var previewSilentRaw = form != null ? form["previewSilent"] : null;
                var previewSilent = false;

                int.TryParse(form != null ? form["id"] : null, out id);
                if (!string.IsNullOrWhiteSpace(previewSilentRaw))
                {
                    bool.TryParse(previewSilentRaw, out previewSilent);
                }

                if (id <= 0)
                {
                    return DevolverJsonErrorInformeTecnico(400, "ID de inspección inválido.");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(id);
                if (inspeccion == null)
                {
                    return DevolverJsonErrorInformeTecnico(404, "Inspección no encontrada.");
                }

                if (!PuedeAccederInspeccion(inspeccion))
                {
                    return DevolverJsonErrorInformeTecnico(403, "No autorizado para previsualizar este informe técnico.");
                }

                if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
                {
                    return DevolverJsonErrorInformeTecnico(409, ObtenerMensajeBloqueoRevisionDocumentalInspector());
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                NormalizarDatosOperadorSolicitud(solicitud);

                ListaVerificacionOperacionalEae listaVerificacion;
                string mensajeLista;
                if (!ValidarPrecondicionInformeTecnico(inspeccion, solicitud, false, out listaVerificacion, out mensajeLista))
                {
                    return DevolverJsonErrorInformeTecnico(409, mensajeLista);
                }

                var usuarioId = ObtenerCodigoUsuario();
                var informeActual = _informeDAO.ObtenerUltimoPorInspeccion(id);
                var informePreview = ConstruirInformeTecnicoDesdeFormulario(id, form, informeActual, false);
                informePreview.CodigoInforme = informeActual != null ? informeActual.CodigoInforme : 0;
                informePreview.Version = informeActual != null && informeActual.Version > 0 ? informeActual.Version : 1;
                informePreview.EstadoInforme = "EN_PREVISUALIZACION";
                informePreview.Finalizado = false;

                var pdfBytes = GenerarPdfInformeTecnico(inspeccion, solicitud, informePreview, true);
                var token = GuardarInformeTecnicoPreviewPdf(id, usuarioId, pdfBytes);
                var pdfUrl = Url.Action("VerPreviewInformeTecnico", "Inspeccion", new { token = token });

                if (!previewSilent)
                {
                    RegistrarAuditoriaInformeDigital(
                        id,
                        FirstNonEmpty(informeActual != null ? informeActual.EstadoInforme : null, "BORRADOR_INFORME"),
                        "EN_PREVISUALIZACION",
                        "TEMP:" + token,
                        null,
                        "Vista previa temporal del informe técnico generada. No finaliza ni firma documento. IP=" + ObtenerIpCliente(),
                        usuarioId,
                        ObtenerUsuarioActual(),
                        "INFORME_PREVIEW_GENERADO");
                }

                return Json(new
                {
                    success = true,
                    pdfUrl = pdfUrl,
                    downloadUrl = pdfUrl,
                    estado = "VISTA PREVIA",
                    message = "Vista previa generada correctamente."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en PrevisualizarInformeTecnico: " + ex);
                return DevolverJsonErrorInformeTecnico(500, "No se pudo generar la vista previa. Verifique los datos e intente nuevamente.");
            }
        }

        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerPreviewInformeTecnico(string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new HttpStatusCodeResult(400, "Token inválido.");
                }

                var safeToken = Path.GetFileNameWithoutExtension(token).Replace("\0", string.Empty);
                var parts = safeToken.Split('_');
                if (parts.Length < 6 || !string.Equals(parts[0], "InformeTecnico", StringComparison.OrdinalIgnoreCase) || !string.Equals(parts[1], "Preview", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpStatusCodeResult(400, "Token inválido.");
                }

                int codigoInspeccion;
                int usuarioToken;
                if (!int.TryParse(parts[2], out codigoInspeccion) || !int.TryParse(parts[3], out usuarioToken))
                {
                    return new HttpStatusCodeResult(400, "Token inválido.");
                }

                if (usuarioToken != ObtenerCodigoUsuario() && !EsAdmin())
                {
                    return new HttpStatusCodeResult(403, "No autorizado para ver esta vista previa.");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(codigoInspeccion);
                if (inspeccion == null || !PuedeAccederInspeccion(inspeccion))
                {
                    return new HttpStatusCodeResult(403, "No autorizado para ver esta vista previa.");
                }

                LimpiarPdfTemporalesAntiguos();
                var basePath = Server.MapPath(CARPETA_VIRTUAL_TEMP_PDF);
                var fullPath = Path.Combine(basePath, safeToken + ".pdf");
                if (!EsRutaDentroDeBase(fullPath, basePath) || !System.IO.File.Exists(fullPath))
                {
                    return HttpNotFound("La vista previa expiró o no se encuentra disponible. Vuelva a generarla.");
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                NormalizarDatosOperadorSolicitud(solicitud);
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                PdfFileNameHelper.AplicarContentDispositionPdf(Response, false, ConstruirNombrePdfInformeTecnico(inspeccion, solicitud, null, "Vista_Previa"));
                return File(fullPath, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en VerPreviewInformeTecnico: " + ex);
                return new HttpStatusCodeResult(500, "No se pudo cargar la vista previa del informe técnico.");
            }
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult FinalizarInformeTecnico(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para finalizar el informe técnico.");
            }

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                NormalizarDatosOperadorSolicitud(solicitud);

                ListaVerificacionOperacionalEae listaVerificacion;
                string mensajeLista;
                if (!ValidarPrecondicionInformeTecnico(inspeccion, solicitud, false, out listaVerificacion, out mensajeLista))
                {
                    TempData["Error"] = mensajeLista;
                    return RedirectToAction("Detalle", new { id });
                }

                var usuarioId = ObtenerCodigoUsuario();
                var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
                if (informe == null)
                {
                    TempData["Error"] = "Debe guardar un borrador del Informe Técnico antes de finalizarlo desde este panel.";
                    return RedirectToAction("Detalle", new { id });
                }

                string mensajeInforme;
                if (!ValidarInformeTecnicoParaFinalizar(informe, out mensajeInforme))
                {
                    TempData["Error"] = mensajeInforme;
                    return RedirectToAction("Detalle", new { id });
                }

                var pdfBytes = GenerarPdfInformeTecnico(inspeccion, solicitud, informe);
                var rutaPdf = GuardarInformeTecnicoPdf(id, informe.Version, pdfBytes);
                var estadoAnterior = FirstNonEmpty(informe.EstadoInforme, informe.Finalizado ? "GENERADO" : "BORRADOR", "BORRADOR");
                var detalleAuditoriaInforme = ConstruirDetalleAuditoriaResultadoInforme("Informe técnico finalizado desde panel de firma.", informe);

                _informeDAO.MarcarFinalizado(informe.CodigoInforme, rutaPdf, informe.CorreoEnviado, UsaFlujoListaVerificacionOperacionalEae(solicitud) ? "INFORME_GENERADO" : "GENERADO", usuarioId);
                _inspeccionBL.GuardarInforme(id, rutaPdf, usuarioId);
                RegistrarAuditoriaInformeDigital(
                    id,
                    estadoAnterior,
                    "GENERADO",
                    rutaPdf,
                    null,
                    detalleAuditoriaInforme,
                    usuarioId,
                    ObtenerUsuarioActual(),
                    "INFORME_GENERADO_DESDE_FIRMA");

                var estadoActual = CapaDatos.Constants.EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
                if (!string.Equals(estadoActual, CapaDatos.Constants.EstadosInspeccion.INFORME_ELABORADO, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(estadoActual, CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase) &&
                        CapaDatos.Constants.EstadosInspeccion.EsTransicionValida(estadoActual, CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION))
                    {
                        _inspeccionBL.CambiarEstado(id, CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION, usuarioId, "Inicio técnico previo a cierre de informe.", ObtenerUsuarioActual(), "INFORME_TECNICO");
                        estadoActual = CapaDatos.Constants.EstadosInspeccion.EN_INSPECCION;
                    }

                    if (CapaDatos.Constants.EstadosInspeccion.EsTransicionValida(estadoActual, CapaDatos.Constants.EstadosInspeccion.INFORME_ELABORADO))
                    {
                        _inspeccionBL.CambiarEstado(id, CapaDatos.Constants.EstadosInspeccion.INFORME_ELABORADO, usuarioId, "Informe técnico finalizado y PDF generado.", ObtenerUsuarioActual(), "INFORME_TECNICO");
                    }
                }

                TempData["Success"] = "Informe técnico finalizado y PDF generado. Ya puede firmar como inspector.";
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en FinalizarInformeTecnico: " + ex);
                TempData["Error"] = "No se pudo finalizar el informe técnico desde este panel.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult GuardarListaVerificacionOperacionalEae()
        {
            var id = 0;
            try
            {
                var form = Request?.Unvalidated?.Form;
                var idRaw = form?["id"] ?? Request?.Unvalidated?.QueryString["id"];
                var submitActionRaw = form?["lvSubmitAction"] ?? Request?.Unvalidated?.QueryString["lvSubmitAction"];
                var finalizarRaw = form?["finalizar"] ?? Request?.Unvalidated?.QueryString["finalizar"];
                var finalizar = EsAccionFinalizarListaVerificacionOperacionalEae(submitActionRaw, finalizarRaw);

                int.TryParse(idRaw, out id);
                _logger.LogInfo("[GestionInspeccion] GuardarListaVerificacionOperacionalEae recibido. InspeccionId=" + id
                    + ", SubmitAction=" + (submitActionRaw ?? string.Empty)
                    + ", FinalizarRaw=" + (finalizarRaw ?? string.Empty)
                    + ", Finalizar=" + finalizar);

                if (id <= 0)
                {
                    return new HttpStatusCodeResult(400, "ID inválido.");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(id);
                if (inspeccion == null)
                {
                    return HttpNotFound("Inspección no encontrada.");
                }

                if (!PuedeAccederInspeccion(inspeccion))
                {
                    return new HttpStatusCodeResult(403, "No autorizado para editar la lista de verificación operacional.");
                }

                if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
                {
                    TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                    return RedirectToAction("Detalle", new { id });
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                NormalizarDatosOperadorSolicitud(solicitud);
                if (!UsaFlujoListaVerificacionOperacionalEae(solicitud))
                {
                    TempData["Error"] = "La lista de verificación operacional EAE no aplica para esta inspección.";
                    return RedirectToAction("Detalle", new { id });
                }

                var usuarioId = ObtenerCodigoUsuario();
                var listaActual = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(id);
                HidratarListaVerificacionOperacionalEae(listaActual, solicitud);
                var lista = ConstruirListaVerificacionOperacionalEaeDesdeFormulario(id, form, listaActual, solicitud);

                string mensajeValidacion;
                if (finalizar && !ValidarListaVerificacionOperacionalEaeParaFinalizar(lista, out mensajeValidacion))
                {
                    _logger.LogWarning("[GestionInspeccion] Finalizacion LV/EAE rechazada por validacion. InspeccionId=" + id + ", Mensaje=" + (mensajeValidacion ?? string.Empty));
                    TempData["Error"] = mensajeValidacion;
                    return RedirectToAction("Detalle", new { id });
                }

                var listaGuardada = _listaVerificacionOperacionalEaeDAO.GuardarBorrador(lista, usuarioId);
                HidratarListaVerificacionOperacionalEae(listaGuardada, solicitud);
                _logger.LogInfo("[GestionInspeccion] LV/EAE guardada. InspeccionId=" + id
                    + ", CodigoLv=" + listaGuardada.CodigoListaVerificacion
                    + ", Version=" + listaGuardada.Version
                    + ", Estado=" + (listaGuardada.EstadoLista ?? string.Empty)
                    + ", Finalizar=" + finalizar);

                if (!finalizar)
                {
                    TempData["Success"] = "Borrador de la lista de verificación operacional EAE guardado correctamente.";
                    return RedirectToAction("Detalle", new { id });
                }

                var pdfBytes = GenerarPdfListaVerificacionOperacionalEae(inspeccion, solicitud, listaGuardada);
                var rutaPdf = GuardarListaVerificacionOperacionalEaePdf(id, listaGuardada.Version, pdfBytes);
                var rutaPdfFisica = ResolverRutaAbsolutaInforme(rutaPdf);
                var pdfExiste = !string.IsNullOrWhiteSpace(rutaPdfFisica) && System.IO.File.Exists(rutaPdfFisica);
                var pdfTamano = pdfExiste ? new FileInfo(rutaPdfFisica).Length : 0L;
                _logger.LogInfo("[GestionInspeccion] LV/EAE PDF generado. InspeccionId=" + id
                    + ", CodigoLv=" + listaGuardada.CodigoListaVerificacion
                    + ", Version=" + listaGuardada.Version
                    + ", Bytes=" + (pdfBytes != null ? pdfBytes.Length : 0)
                    + ", RutaPdf=" + (rutaPdf ?? string.Empty)
                    + ", RutaPdfFisica=" + (rutaPdfFisica ?? string.Empty)
                    + ", Existe=" + pdfExiste
                    + ", Tamano=" + pdfTamano);
                _listaVerificacionOperacionalEaeDAO.MarcarFinalizada(listaGuardada.CodigoListaVerificacion, rutaPdf, "LV_COMPLETADA", usuarioId);

                TempData["Success"] = "Lista de verificación operacional EAE finalizada y PDF generado. Ya puede firmarla digitalmente.";
                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en GuardarListaVerificacionOperacionalEae: " + ex);
                TempData["Error"] = "No se pudo guardar la lista de verificación operacional EAE.";
                return RedirectToAction("Detalle", new { id });
            }
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult FinalizarListaVerificacionOperacionalEae(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para finalizar la lista de verificación operacional.");
            }

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                NormalizarDatosOperadorSolicitud(solicitud);
                if (!UsaFlujoListaVerificacionOperacionalEae(solicitud))
                {
                    TempData["Error"] = "La lista de verificación operacional EAE no aplica para esta inspección.";
                    return RedirectToAction("Detalle", new { id });
                }

                var lista = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(id);
                HidratarListaVerificacionOperacionalEae(lista, solicitud);
                if (lista == null)
                {
                    TempData["Error"] = "Primero debe registrar un borrador de la lista de verificación operacional EAE.";
                    return RedirectToAction("Detalle", new { id });
                }

                string mensajeValidacion;
                if (!ValidarListaVerificacionOperacionalEaeParaFinalizar(lista, out mensajeValidacion))
                {
                    TempData["Error"] = mensajeValidacion;
                    return RedirectToAction("Detalle", new { id });
                }

                var usuarioId = ObtenerCodigoUsuario();
                var pdfBytes = GenerarPdfListaVerificacionOperacionalEae(inspeccion, solicitud, lista);
                var rutaPdf = GuardarListaVerificacionOperacionalEaePdf(id, lista.Version, pdfBytes);
                _listaVerificacionOperacionalEaeDAO.MarcarFinalizada(lista.CodigoListaVerificacion, rutaPdf, "LV_COMPLETADA", usuarioId);

                TempData["Success"] = "Lista de verificación operacional EAE finalizada y PDF generado.";
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en FinalizarListaVerificacionOperacionalEae: " + ex);
                TempData["Error"] = "No se pudo finalizar la lista de verificación operacional EAE.";
            }

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarListaVerificacionOperacionalEae(int id, string passwordCertificado)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para firmar la lista de verificación operacional.");
            }

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitud);
            if (!UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "La lista de verificación operacional EAE no aplica para esta inspección.";
                return RedirectToAction("Detalle", new { id });
            }

            var lista = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(id);
            HidratarListaVerificacionOperacionalEae(lista, solicitud);
            if (lista == null || !lista.Finalizado)
            {
                TempData["Error"] = "Debe finalizar la lista de verificación operacional EAE antes de firmarla.";
                return RedirectToAction("Detalle", new { id });
            }

            if (lista.FirmadoTecnico)
            {
                TempData["Error"] = "La lista de verificación operacional EAE ya fue firmada digitalmente.";
                return RedirectToAction("Detalle", new { id });
            }

            string mensajeValidacion;
            if (!ValidarListaVerificacionOperacionalEaeParaFinalizar(lista, out mensajeValidacion))
            {
                TempData["Error"] = mensajeValidacion;
                return RedirectToAction("Detalle", new { id });
            }

            var certificadoArchivo = Request.Files["CertificadoInspector"];
            if (!EsCertificadoDigitalValido(certificadoArchivo, out mensajeValidacion))
            {
                TempData["Error"] = mensajeValidacion;
                return RedirectToAction("Detalle", new { id });
            }

            byte[] certificadoBytes;
            using (var ms = new MemoryStream())
            {
                certificadoArchivo.InputStream.CopyTo(ms);
                certificadoBytes = ms.ToArray();
            }

            var infoCertificado = _firmaDigitalService.LeerCertificado(certificadoBytes, passwordCertificado);
            if (!infoCertificado.Exitoso)
            {
                TempData["Error"] = infoCertificado.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var pdfFuente = GenerarPdfListaVerificacionOperacionalEae(inspeccion, solicitud, lista);
            var rutaPdfActualizada = GuardarOReemplazarListaVerificacionOperacionalEaePdfHistorico(lista, pdfFuente, usuarioId);
            if (!string.IsNullOrWhiteSpace(rutaPdfActualizada))
            {
                lista.RutaPdf = rutaPdfActualizada;
            }

            var nombreFirmanteCertificado = !string.IsNullOrWhiteSpace(infoCertificado.NombreTitular)
                ? infoCertificado.NombreTitular
                : ObtenerUsuarioActual();

            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                pdfFuente,
                certificadoBytes,
                passwordCertificado,
                nombreFirmanteCertificado,
                "Firma del tecnico responsable sobre la lista de verificación operacional EAE",
                "Sistema AOCR DGAC",
                "LV_EAE_INSPECTOR",
                null,
                null);

            if (!resultadoFirma.Exitoso)
            {
                TempData["Error"] = resultadoFirma.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            var rutaFirmada = GuardarListaVerificacionOperacionalEaeFirmadaPdf(id, lista.Version, resultadoFirma.PdfFirmado);
            _listaVerificacionOperacionalEaeDAO.RegistrarFirmaTecnico(
                lista.CodigoListaVerificacion,
                rutaFirmada,
                resultadoFirma.HashSha256,
                DateTime.Now,
                nombreFirmanteCertificado,
                "LV_FIRMADA",
                usuarioId);

            TempData["Success"] = "Lista de verificación operacional EAE firmada correctamente. Ahora puede trabajar el informe técnico.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_SOLICITANTE + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult SubirDocumentoSolicitante(int id, string tipoDocumento, string observacion = "", int? documentoBaseId = null)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para cargar documentos.");
            }

            var archivo = Request.Files["DocumentoSolicitante"];
            if (archivo == null || archivo.ContentLength <= 0)
            {
                TempData["Error"] = "Debe adjuntar un documento corregido.";
                return RedirectToAction("Detalle", new { id });
            }

            var options = new FileUploadOptions
            {
                BasePath = FileStorageHelper.GetPhysicalBasePath(CARPETA_VIRTUAL_DOCUMENTOS_SOLICITANTE),
                Subfolder = string.Empty,
                AllowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png" },
                AllowedContentTypes = null,
                MaxSizeMb = 10,
                ValidateMagicBytes = false
            };

            string error;
            FileUploadResult result;
            if (!FileUploadService.TrySave(archivo, options, out result, out error))
            {
                TempData["Error"] = error ?? "No se pudo guardar el documento corregido.";
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            _documentoDAO.RegistrarDocumentoVersionado(new DocumentoInspeccion
            {
                CodigoInspeccion = id,
                CodigoInforme = informe != null ? (int?)informe.CodigoInforme : null,
                CodigoDocumentoBase = documentoBaseId,
                TipoDocumento = string.IsNullOrWhiteSpace(tipoDocumento) ? "DOCUMENTO_CORREGIDO" : tipoDocumento,
                NombreArchivoOriginal = result.OriginalName,
                NombreArchivoStorage = result.StoredName,
                RutaArchivo = CARPETA_VIRTUAL_DOCUMENTOS_SOLICITANTE + "/" + result.StoredName,
                HashArchivo = result.HashSha256,
                TamanoBytes = result.Size,
                ContentType = result.ContentType,
                Observacion = observacion,
                SubidoPorRol = ROL_SOLICITANTE,
                CodigoUsuario = ObtenerCodigoUsuario()
            });

            var usuarioId = ObtenerCodigoUsuario();
            var estadoActual = CapaDatos.Constants.EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            if (CapaDatos.Constants.EstadosInspeccion.EsTransicionValida(estadoActual, CapaDatos.Constants.EstadosInspeccion.SUBSANADA))
            {
                _inspeccionBL.CambiarEstado(id, CapaDatos.Constants.EstadosInspeccion.SUBSANADA, usuarioId, "Solicitante cargó documentación corregida.", ObtenerUsuarioActual(), "SUBSANACION_SOLICITANTE");
            }

            inspeccion.EstadoDocumental = "SUBSANADA";
            inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                ? "Solicitante cargó documentación corregida."
                : inspeccion.Comentarios + " | Solicitante cargó documentación corregida.";
            _inspeccionBL.Actualizar(inspeccion, usuarioId);

            TempData["Success"] = "Documento corregido cargado correctamente. Se registró una nueva versión para revisión técnica.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarInformeInspector(int id, string passwordCertificado)
        {
            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para firmar el informe técnico.");
            }

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            return FirmarInformePorRol(id, passwordCertificado, "CertificadoInspector", "INSPECTOR", "FIRMADO_FINAL", autoEnviarADirdac: false);
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult EnviarADirdac(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "Este flujo EAE no requiere envío a DIRDAC. El informe se firma directamente luego de la lista de verificación operacional.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para enviar el informe a DIRDAC.");
            }

            if ((User.IsInRole(ROL_INSPECTOR) || User.IsInRole(ROL_ADMIN)) && !InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var resultado = EnviarInformeADirdacInterno(inspeccion, solicitud, informe, ObtenerCodigoUsuario());
            var informeActualizado = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var mensajeKey = resultado.Exitoso
                ? "Success"
                : (InformeEstaEnviadoADirdac(informeActualizado) ? "Warning" : "Error");

            TempData[mensajeKey] = resultado.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult EnviarACoordinador(int id)
        {
            if (id <= 0) { return new HttpStatusCodeResult(400, "ID inválido."); }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) { return HttpNotFound("Inspección no encontrada."); }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "El flujo EAE omite la etapa de Coordinación. El informe técnico se firma directamente por el inspector.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!PuedeAccederInspeccion(inspeccion)) { return new HttpStatusCodeResult(403, "No autorizado."); }

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            if (informe == null || !informe.FirmadoInspector)
            {
                TempData["Error"] = "El informe debe estar firmado por el inspector antes de enviarlo al coordinador.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.Equals(informe.EstadoInforme, "ENVIADO_A_COORDINADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe ya fue enviado a Coordinación y está pendiente de revisión.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.Equals(informe.EstadoInforme, "ENVIADO_A_DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe ya se encuentra en revisión de Dirección / Jefatura.";
                return RedirectToAction("Detalle", new { id });
            }

            if (informe.FirmadoDirdac || string.Equals(informe.EstadoInforme, "APROBADO_DIRECCION", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe técnico ya fue aprobado por Dirección / Jefatura.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            _informeDAO.ActualizarEstadoInforme(informe.CodigoInforme, "ENVIADO_A_COORDINADOR", usuarioId);

            RegistrarAuditoriaInformeDigital(id,
                FirstNonEmpty(informe.EstadoInforme, "FIRMADO_INSPECTOR"),
                "ENVIADO_A_COORDINADOR", null, null,
                "Informe enviado al coordinador para revisión. IP=" + ObtenerIpCliente(),
                usuarioId, ObtenerUsuarioActual(), "ENVIO_COORDINADOR");

            TempData["Success"] = "Informe técnico enviado al coordinador para revisión.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult CoordinadorAprobar(int id)
        {
            if (id <= 0) { return new HttpStatusCodeResult(400, "ID inválido."); }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) { return HttpNotFound("Inspección no encontrada."); }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "El flujo EAE no utiliza aprobación de Coordinación para el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            if (informe == null)
            {
                TempData["Error"] = "No se encontró el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(informe.EstadoInforme, "ENVIADO_A_COORDINADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe no se encuentra en bandeja de revisión de Coordinación.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            _informeDAO.ActualizarEstadoInforme(informe.CodigoInforme, "APROBADO_COORDINADOR", usuarioId);

            RegistrarAuditoriaInformeDigital(id,
                "ENVIADO_A_COORDINADOR", "APROBADO_COORDINADOR", null, null,
                "Coordinación aprobó el informe y lo devolvió al inspector para firma final. IP=" + ObtenerIpCliente(),
                usuarioId, ObtenerUsuarioActual(), "APROBACION_COORDINADOR");

            TempData["Success"] = "Coordinación aprobó el informe. El inspector debe aplicar la firma final.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult CoordinadorDevolver(int id, string observacionDevolucion)
        {
            if (id <= 0) { return new HttpStatusCodeResult(400, "ID inválido."); }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) { return HttpNotFound("Inspección no encontrada."); }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "El flujo EAE no utiliza devoluciones de Coordinación para el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            if (informe == null)
            {
                TempData["Error"] = "No se encontró el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(informe.EstadoInforme, "ENVIADO_A_COORDINADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe no se encuentra en bandeja de revisión de Coordinación.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.IsNullOrWhiteSpace(observacionDevolucion))
            {
                TempData["Error"] = "Debe ingresar los argumentos de devolución.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioActual = ObtenerUsuarioActual();

            _informeDAO.RegistrarDevolucionCoordinador(informe.CodigoInforme, observacionDevolucion.Trim(), usuarioActual, "DEVUELTO_COORDINADOR", usuarioId);

            RegistrarAuditoriaInformeDigital(id,
                "ENVIADO_A_COORDINADOR", "DEVUELTO_COORDINADOR", null, null,
                string.Format("Informe devuelto por coordinador ({0}). Argumentos: {1}. IP={2}", usuarioActual, observacionDevolucion.Trim(), ObtenerIpCliente()),
                usuarioId, usuarioActual, "DEVOLUCION_COORDINADOR");

            _logger.LogInfo("[GestionInspeccion] Informe devuelto por coordinador. InspeccionId=" + id
                + ", InformeId=" + informe.CodigoInforme
                + ", Usuario=" + usuarioActual
                + ", Observacion=" + observacionDevolucion.Trim());

            TempData["Warning"] = "Informe técnico devuelto al inspector con argumentos del coordinador.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC)]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarInformeDirdac(int id, string passwordCertificado)
        {
            TempData["Error"] = "La firma digital de Dirección / Jefatura ya no aplica en esta etapa. Use el flujo de aprobación o devolución.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC)]
        [ValidateAntiForgeryToken]
        public ActionResult DireccionAprobar(int id)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "El flujo EAE no requiere revisión institucional DIRDAC para el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            if (informe == null)
            {
                TempData["Error"] = "No se encontró el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(informe.EstadoInforme, "ENVIADO_A_DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe no se encuentra en bandeja de revisión de Dirección / Jefatura.";
                return RedirectToAction("Detalle", new { id });
            }

            if (informe.FirmadoDirdac || string.Equals(informe.EstadoInforme, "APROBADO_DIRECCION", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe ya fue aprobado por Dirección / Jefatura.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioActual = ObtenerUsuarioActual();
            var estadoAnterior = FirstNonEmpty(informe.EstadoInforme, "ENVIADO_A_DIRDAC");

            _informeDAO.RegistrarAprobacionDireccion(
                informe.CodigoInforme,
                DateTime.Now,
                usuarioActual,
                "ENVIADO_A_COORDINADOR",
                usuarioId);

            RegistrarAuditoriaInformeDigital(
                id,
                estadoAnterior,
                "ENVIADO_A_COORDINADOR",
                null,
                null,
                string.Format("Informe revisado por DIRDAC / Dirección - Jefatura ({0}) y remitido a Coordinación para aprobación. IP={1}", usuarioActual, ObtenerIpCliente()),
                usuarioId,
                usuarioActual,
                "APROBACION_DIRECCION");

            _logger.LogInfo("[GestionInspeccion] Informe aprobado por Dirección / Jefatura. InspeccionId=" + id
                + ", InformeId=" + informe.CodigoInforme
                + ", Usuario=" + usuarioActual);

            TempData["Success"] = "DIRDAC / Dirección - Jefatura revisó el informe y lo remitió a Coordinación.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC)]
        [ValidateAntiForgeryToken]
        public ActionResult DireccionDevolver(int id, string observacionRechazo)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                TempData["Error"] = "El flujo EAE no utiliza devoluciones DIRDAC para el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            if (informe == null)
            {
                TempData["Error"] = "No se encontró el informe técnico.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!string.Equals(informe.EstadoInforme, "ENVIADO_A_DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Warning"] = "El informe no se encuentra en bandeja de revisión de Dirección / Jefatura.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.IsNullOrWhiteSpace(observacionRechazo))
            {
                TempData["Error"] = "Debe ingresar una observación para devolver el informe.";
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioActual = ObtenerUsuarioActual();
            var observacion = observacionRechazo.Trim();

            _informeDAO.RegistrarDevolucionCoordinador(
                informe.CodigoInforme,
                observacion,
                usuarioActual,
                "DEVUELTO_DIRECCION",
                usuarioId);

            RegistrarAuditoriaInformeDigital(
                id,
                FirstNonEmpty(informe.EstadoInforme, "ENVIADO_A_DIRDAC"),
                "DEVUELTO_DIRECCION",
                null,
                null,
                string.Format("Informe devuelto por Dirección / Jefatura ({0}). Observación: {1}. IP={2}", usuarioActual, observacion, ObtenerIpCliente()),
                usuarioId,
                usuarioActual,
                "DEVOLUCION_DIRECCION");

            _logger.LogInfo("[GestionInspeccion] Informe devuelto por Dirección / Jefatura. InspeccionId=" + id
                + ", InformeId=" + informe.CodigoInforme
                + ", Usuario=" + usuarioActual
                + ", Observacion=" + observacion);

            TempData["Warning"] = "Dirección / Jefatura devolvió el informe técnico al inspector para correcciones.";
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC)]
        [ValidateAntiForgeryToken]
        public ActionResult RechazarInformeDirdac(int id, string observacionRechazo)
        {
            // Compatibilidad con formularios antiguos.
            return DireccionDevolver(id, observacionRechazo);
        }

        // ============================================================
        // ✅✅✅ SUBIR INFORME - SEGURO (PDF)
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult SubirInforme(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para subir informe.");

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            HttpPostedFileBase archivo = Request.Files["Informe"];
            if (archivo == null || archivo.ContentLength <= 0)
            {
                TempData["Error"] = "No se recibió ningún archivo.";
                return RedirectToAction("Detalle", new { id });
            }

            if (archivo.ContentLength > MAX_PDF_BYTES)
            {
                TempData["Error"] = "El archivo supera el tamaño permitido (10 MB).";
                return RedirectToAction("Detalle", new { id });
            }

            if (!archivo.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se permiten archivos PDF (.pdf).";
                return RedirectToAction("Detalle", new { id });
            }

            if (!TieneFirmaPdf(archivo))
            {
                TempData["Error"] = "El archivo no parece un PDF válido (firma).";
                return RedirectToAction("Detalle", new { id });
            }

            var options = new FileUploadOptions
            {
                BasePath = FileStorageHelper.GetPhysicalBasePath(CARPETA_VIRTUAL_INFORMES),
                Subfolder = string.Empty,
                AllowedExtensions = new[] { ".pdf" },
                AllowedContentTypes = new[] { "application/pdf" },
                MaxSizeMb = 10,
                ValidateMagicBytes = true
            };

            string error;
            FileUploadResult result;
            if (!FileUploadService.TrySave(archivo, options, out result, out error))
            {
                TempData["Error"] = error ?? "No se pudo guardar el archivo.";
                return RedirectToAction("Detalle", new { id });
            }

            string rutaRelativa = CARPETA_VIRTUAL_INFORMES + "/" + result.StoredName;
            int codigoUsuario = ObtenerCodigoUsuario();

            bool ok = _inspeccionBL.GuardarInforme(id, rutaRelativa, codigoUsuario);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Informe cargado y asociado correctamente."
                : "El PDF se guardó, pero no se pudo asociar en base de datos.";

            return RedirectToAction("Detalle", new { id });
        }

        // ============================================================
        // ✅ REGISTRAR HALLAZGO
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarHallazgo(Hallazgo h)
        {
            if (h == null || h.CodigoInspeccion <= 0)
                return RedirectToAction("Index");

            if (!ModelState.IsValid)
                return RedirectToAction("Detalle", new { id = h.CodigoInspeccion });

            var inspeccion = _inspeccionDAO.ObtenerPorId(h.CodigoInspeccion);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para registrar hallazgos.");

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id = h.CodigoInspeccion });
            }

            var codigoUsuario = ObtenerCodigoUsuario();
            string usuarioNombre = User?.Identity?.Name ?? codigoUsuario.ToString();

            // ✅ HallazgoBL.Crear devuelve int (según tu BL)
            int idHallazgo = _hallazgoBL.Crear(h, usuarioNombre);
            bool ok = idHallazgo > 0;

            TempData[ok ? "Success" : "Error"] = ok
                ? "Hallazgo registrado correctamente."
                : "Error al registrar hallazgo.";

            return RedirectToAction("Detalle", new { id = h.CodigoInspeccion });
        }

        // ============================================================
        // ✅ CERRAR INSPECCIÓN
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Cerrar(int id, string resultado)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");

            try
            {
                var codigoUsuario = ObtenerCodigoUsuario();
                bool ok = _inspeccionBL.CerrarInspeccion(id, resultado, codigoUsuario);

                TempData[ok ? "Success" : "Error"] = ok
                    ? "Inspección cerrada correctamente."
                    : "No se pudo cerrar la inspección.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Detalle", new { id });
        }

        // ============================================================
        // ✅✅✅ PLANIFICACIÓN (GET ÚNICO)
        // ============================================================
        [HttpGet]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        public ActionResult Planificacion(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para planificar esta inspección.");

            return View("~/Views/Inspeccion/Planificacion.cshtml", inspeccion);
        }

        // ============================================================
        // ✅✅✅ PLANIFICACIÓN (POST ÚNICO)
        // ============================================================
        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Planificacion(
            int codigoInspeccion,
            DateTime fechaInspeccion,
            TimeSpan horaInicio,
            int duracionEstimada,
            string ubicacion,
            string latitud,
            string longitud,
            string tipoInspeccion,
            string alcance,
            string equiposNecesarios,
            string contactoSitio,
            string telefonoContacto,
            string observaciones)
        {
            if (codigoInspeccion <= 0)
                return new HttpStatusCodeResult(400, "ID de inspección inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(codigoInspeccion);
            if (inspeccion == null)
            {
                TempData["Error"] = "No se encontró la inspección.";
                return RedirectToAction("Index");
            }

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para planificar esta inspección.");

            inspeccion.FechaProgramada = fechaInspeccion;
            inspeccion.HoraProgramada = horaInicio;
            inspeccion.DuracionEstimada = duracionEstimada;

            inspeccion.Lugar = ubicacion;
            inspeccion.Latitud = latitud;
            inspeccion.Longitud = longitud;

            inspeccion.Tipo = tipoInspeccion;
            inspeccion.ObservacionesGenerales = observaciones;
            inspeccion.HallazgosPrincipales = alcance;

            inspeccion.Comentarios =
                $"Contacto: {contactoSitio} - Tel: {telefonoContacto}. Equipos: {equiposNecesarios}";

            inspeccion.Estado = EstadosInspeccion.VERIFICACION_SOLICITUD;

            int usuarioId = ObtenerCodigoUsuario();
            inspeccion.UpdatedAt = DateTime.Now;
            inspeccion.UpdatedBy = usuarioId;

            // ✅ Actualizar con updatedBy
            bool ok = _inspeccionBL.Actualizar(inspeccion, usuarioId);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Planificación guardada correctamente."
                : "Error al guardar la planificación.";

            return RedirectToAction("Detalle", new { id = codigoInspeccion });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_COORD + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult SolicitarViaticos(int id, decimal? monto, string observacion = "")
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");
            if (!PuedeAccederInspeccion(inspeccion)) return new HttpStatusCodeResult(403, "No autorizado.");

            if ((User.IsInRole(ROL_INSPECTOR) || User.IsInRole(ROL_ADMIN)) && !InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            inspeccion.ViaticosRequeridos = true;
            inspeccion.ViaticosMonto = monto;
            inspeccion.PagoViaticosValidado = false;
            inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                ? ("Viáticos requeridos. " + (observacion ?? string.Empty))
                : (inspeccion.Comentarios + " | Viáticos requeridos. " + (observacion ?? string.Empty));

            var okUpdate = _inspeccionBL.Actualizar(inspeccion, usuarioId);
            var okEstado = false;
            try
            {
                okEstado = _inspeccionBL.CambiarEstado(id, EstadosInspeccion.VIATICOS_REQUERIDOS, usuarioId, observacion, ObtenerUsuarioActual(), "VIATICOS");
            }
            catch
            {
                okEstado = false;
            }

            TempData[(okUpdate && okEstado) ? "Success" : "Error"] = (okUpdate && okEstado)
                ? "Viáticos solicitados correctamente."
                : "No se pudo registrar la solicitud de viáticos.";

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = "CoordinadorFinanciero,DirectorFinanciero," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult ValidarPagoViaticos(int id, string observacion = "")
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");
            if (!PuedeAccederInspeccion(inspeccion) && !User.IsInRole("CoordinadorFinanciero") && !User.IsInRole("DirectorFinanciero"))
                return new HttpStatusCodeResult(403, "No autorizado.");

            var usuarioId = ObtenerCodigoUsuario();
            inspeccion.PagoViaticosValidado = true;
            inspeccion.FechaPagoViaticos = DateTime.Now;
            inspeccion.Comentarios = string.IsNullOrWhiteSpace(inspeccion.Comentarios)
                ? ("Pago de viáticos validado. " + (observacion ?? string.Empty))
                : (inspeccion.Comentarios + " | Pago de viáticos validado. " + (observacion ?? string.Empty));

            var okUpdate = _inspeccionBL.Actualizar(inspeccion, usuarioId);
            var okEstado = false;
            try
            {
                okEstado = _inspeccionBL.CambiarEstado(id, EstadosInspeccion.PAGO_VALIDADO, usuarioId, observacion, ObtenerUsuarioActual(), "VIATICOS");
            }
            catch
            {
                okEstado = false;
            }

            TempData[(okUpdate && okEstado) ? "Success" : "Error"] = (okUpdate && okEstado)
                ? "Pago de viáticos validado."
                : "No se pudo validar el pago de viáticos.";

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarResultado(int id, string resultado, string observacion = "")
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");
            if (!PuedeAccederInspeccion(inspeccion)) return new HttpStatusCodeResult(403, "No autorizado.");

            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.RegistrarResultadoInspeccion(id, resultado, observacion, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Evaluar(int id, string resultado, string observacion = "")
        {
            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");
            if (!PuedeAccederInspeccion(inspeccion)) return new HttpStatusCodeResult(403, "No autorizado.");
            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.EvaluarInspeccion(id, resultado, observacion, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_SOLICITANTE + "," + ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Subsanar(int id, string observacion = "")
        {
            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.SubsanarInspeccion(id, observacion, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Revalidar(int id, bool aprobada, string observacion = "")
        {
            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.RevalidarInspeccion(id, aprobada, observacion, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult SolicitarNueva(int id, string observacion = "")
        {
            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.SolicitarNuevaInspeccion(id, observacion, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarNoConforme(int id, string descripcion, string criticidad = "MEDIA")
        {
            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");
            if (!PuedeAccederInspeccion(inspeccion)) return new HttpStatusCodeResult(403, "No autorizado.");
            if (!InspectorTieneRevisionDocumentalConfirmada(inspeccion))
            {
                TempData["Error"] = ObtenerMensajeBloqueoRevisionDocumentalInspector();
                return RedirectToAction("Detalle", new { id });
            }

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.RegistrarNoConformidad(id, descripcion, criticidad, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        // ============================================================
        // ✅ HELPERS DE SEGURIDAD
        // ============================================================
        private static bool EstadoDocumentalHabilitaAccionesInspector(string estadoDocumental)
        {
            var estadoNormalizado = (estadoDocumental ?? string.Empty).Trim().ToUpperInvariant();

            return string.Equals(estadoNormalizado, "EN_REVISION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, "ACEPTADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, "OBSERVACION_DOCUMENTAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, "OBSERVADO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, "APROBADO", StringComparison.OrdinalIgnoreCase);
        }

        private bool InspectorTieneRevisionDocumentalConfirmada(Inspeccion inspeccion)
        {
            return inspeccion != null && EstadoDocumentalHabilitaAccionesInspector(inspeccion.EstadoDocumental);
        }

        private string ObtenerMensajeBloqueoRevisionDocumentalInspector()
        {
            return "Antes de ejecutar acciones del inspector debe revisar los documentos de la solicitud y confirmar la revisión documental.";
        }

        private bool TieneFirmaPdf(HttpPostedFileBase archivo)
        {
            try
            {
                if (archivo == null || archivo.InputStream == null || !archivo.InputStream.CanRead)
                    return false;

                byte[] header = new byte[4];
                int read = archivo.InputStream.Read(header, 0, 4);
                archivo.InputStream.Position = 0;

                if (read < 4) return false;

                string sig = Encoding.ASCII.GetString(header);
                return sig == "%PDF";
            }
            catch
            {
                try { if (archivo?.InputStream != null) archivo.InputStream.Position = 0; } catch { }
                return false;
            }
        }

        private bool EsRutaDentroDeBase(string archivoFullPath, string baseDirFullPath)
        {
            try
            {
                var archivo = Path.GetFullPath(archivoFullPath);
                var baseDir = Path.GetFullPath(baseDirFullPath);

                if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    baseDir += Path.DirectorySeparatorChar;

                return archivo.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private bool ResolverInspectoresAs400(Inspeccion model, string tipoInspector, out string mensaje)
        {
            mensaje = string.Empty;
            if (model == null)
            {
                mensaje = "Modelo de inspección inválido.";
                return false;
            }

            var tipoInspectorNormalizado = NormalizarTipoInspector(tipoInspector);
            var cedulaPrincipal = (model.InspectorPrincipalCedula ?? string.Empty).Trim();
            var cedulaApoyo = (model.InspectorApoyoCedula ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(cedulaPrincipal) && string.IsNullOrWhiteSpace(cedulaApoyo))
            {
                return true;
            }

            var dao = new InspectorAS400DAO(new SecureConfigType());

            if (!string.IsNullOrWhiteSpace(cedulaPrincipal))
            {
                var principal = dao.ObtenerActivoPorCedula(cedulaPrincipal, tipoInspectorNormalizado);
                if (principal == null)
                {
                    mensaje = "El inspector principal seleccionado no existe o no está activo en OPINSPECTORES.";
                    return false;
                }

                model.InspectorPrincipalCedula = principal.Cedula;
                model.InspectorPrincipalNombre = principal.NombreCompleto;
                model.InspectorPrincipalTipo = principal.Tipo;

                int codigoInspector;
                if (!model.CodigoInspector.HasValue &&
                    int.TryParse((principal.Cedula ?? string.Empty).Trim(), out codigoInspector))
                {
                    model.CodigoInspector = codigoInspector;
                }
            }

            if (!string.IsNullOrWhiteSpace(cedulaApoyo))
            {
                var apoyo = dao.ObtenerActivoPorCedula(cedulaApoyo, tipoInspectorNormalizado);
                if (apoyo == null)
                {
                    mensaje = "El inspector de apoyo seleccionado no existe o no está activo en OPINSPECTORES.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(model.InspectorPrincipalCedula) &&
                    string.Equals(model.InspectorPrincipalCedula.Trim(), apoyo.Cedula.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    mensaje = "El inspector principal y el inspector de apoyo no pueden ser el mismo.";
                    return false;
                }

                model.InspectorApoyoCedula = apoyo.Cedula;
                model.InspectorApoyoNombre = apoyo.NombreCompleto;
                model.InspectorApoyoTipo = apoyo.Tipo;
            }

            return true;
        }

        private bool UsuarioActualPuedeCambiarEstadoInspeccion(string estadoActual, string estadoDestino)
        {
            if (EsAdmin())
            {
                return true;
            }

            var actual = EstadosInspeccion.NormalizarEstado(estadoActual);
            var destino = EstadosInspeccion.NormalizarEstado(estadoDestino);

            if (!EstadosInspeccion.EsTransicionValida(actual, destino))
            {
                return false;
            }

            if (EstadosInspeccion.EsEstadoBloqueCoordinacionJefatura(destino))
            {
                return EsRolDecisionCoordinacionJefatura();
            }

            if (EstadosInspeccion.EsEstadoBloqueInspector(destino))
            {
                return EsRolInspector();
            }

            if (destino == EstadosInspeccion.RESULTADO_SATISFACTORIO
                || destino == EstadosInspeccion.RESULTADO_NO_SATISFACTORIO
                || destino == EstadosInspeccion.VIATICOS_REQUERIDOS
                || destino == EstadosInspeccion.PAGO_VALIDADO)
            {
                return EsRolInspector() || EsRolDecisionCoordinacionJefatura();
            }

            return EsRolDecisionCoordinacionJefatura();
        }

        private ActionResult RedirigirTrasCambioEstado(int id, string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url != null && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Detalle", new { id });
        }

        private bool PuedeIniciarInspeccionAocr(Inspeccion inspeccion, out SolicitudAOCR solicitud, out string mensaje, out string claveTempData)
        {
            solicitud = null;
            mensaje = string.Empty;
            claveTempData = "Error";

            if (inspeccion == null || inspeccion.CodigoSolicitud <= 0)
            {
                return true;
            }

            solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (solicitud == null)
            {
                mensaje = "No se pudo resolver la solicitud AOCR vinculada a esta inspección.";
                return false;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (!string.Equals(estadoSolicitud, EstadoSolicitud.PendienteAsignacionRT, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoSolicitud, EstadoSolicitud.RequiereInspeccion, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(estadoSolicitud, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                claveTempData = "Warning";
                mensaje = "La solicitud AOCR vinculada no está en una etapa válida para iniciar la inspección desde este módulo.";
                return false;
            }

            if (!SolicitudAocrTieneInspectorAsignado(solicitud) && !InspeccionAocrTieneInspectorAsignado(inspeccion))
            {
                claveTempData = "Warning";
                mensaje = "Debe existir un inspector/RT asignado antes de iniciar la inspección.";
                return false;
            }

            if (!InspeccionPermiteInicioOperativoAocr(inspeccion))
            {
                claveTempData = "Warning";
                mensaje = "La inspección vinculada aún está en revisión o con observaciones. Complete primero ese flujo antes de iniciarla.";
                return false;
            }

            if (!SolicitudTieneAprobacionFinancieraAocr(solicitud.CodigoSolicitud))
            {
                mensaje = "La solicitud no tiene aprobación financiera registrada. No se puede iniciar inspección.";
                return false;
            }

            return true;
        }

        private bool SincronizarSolicitudAocrAlIniciarInspeccion(SolicitudAOCR solicitud, int usuarioId, out string mensajeCambio)
        {
            mensajeCambio = string.Empty;

            if (solicitud == null)
            {
                mensajeCambio = "No existe una solicitud AOCR vinculada para sincronizar.";
                return false;
            }

            if (usuarioId <= 0)
            {
                mensajeCambio = "No se pudo resolver el usuario actual para sincronizar la solicitud AOCR.";
                return false;
            }

            var estadoSolicitud = EstadoSolicitud.Normalizar(solicitud.Estado ?? string.Empty);
            if (string.Equals(estadoSolicitud, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                solicitud.CodigoSolicitud,
                EstadoSolicitud.EnInspeccion,
                "Inicio operativo desde el módulo de inspección.",
                usuarioId,
                destino => string.Equals(destino, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase),
                out mensajeCambio);
        }

        private bool SolicitudTieneAprobacionFinancieraAocr(int codigoSolicitud)
        {
            if (codigoSolicitud <= 0)
            {
                return false;
            }

            try
            {
                return new OrdenRecaudacionDAO().TieneAprobacionFinancieraSolicitud(codigoSolicitud);
            }
            catch
            {
                return false;
            }
        }

        private static bool SolicitudAocrTieneInspectorAsignado(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return false;
            }

            return (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre);
        }

        private static bool InspeccionAocrTieneInspectorAsignado(Inspeccion inspeccion)
        {
            if (inspeccion == null)
            {
                return false;
            }

            return (inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                || !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula)
                || !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre);
        }

        private static bool InspeccionPermiteInicioOperativoAocr(Inspeccion inspeccion)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var estadoNormalizado = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            return string.Equals(estadoNormalizado, EstadosInspeccion.ACEPTADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.SUBSANADA, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.PAGO_VALIDADO, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoNormalizado, EstadosInspeccion.EN_INSPECCION, StringComparison.OrdinalIgnoreCase);
        }

        private bool PuedeAccederSolicitante(Inspeccion ins)
        {
            if (ins == null)
            {
                return false;
            }

            var solicitud = _solicitudDAO.ObtenerPorId(ins.CodigoSolicitud);
            if (solicitud == null)
            {
                return false;
            }

            var codigoUsuario = ObtenerCodigoUsuario();
            if (codigoUsuario > 0 && solicitud.CodigoUsuario == codigoUsuario)
            {
                return true;
            }

            var correoSesion = Session != null && Session["Correo"] != null
                ? (Session["Correo"].ToString() ?? string.Empty).Trim()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(correoSesion))
            {
                if (string.Equals(correoSesion, solicitud.Email ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(correoSesion, solicitud.CorreoRepresentanteTecnico ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private byte[] GenerarPdfInformeTecnico(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, bool esVistaPrevia = false)
        {
            EnriquecerInspectoresInformeTecnico(inspeccion, solicitud);
            var usaFlujoLvEae = UsaFlujoListaVerificacionOperacionalEae(solicitud);

            var vm = new InformeTecnicoPdfViewModel
            {
                Inspeccion = inspeccion,
                Solicitud = solicitud,
                Informe = informe,
                EsVistaPrevia = esVistaPrevia,
                EsDefinitivo = !esVistaPrevia,
                MostrarMarcaAguaBorrador = esVistaPrevia,
                MostrarFirmas = true,
                MostrarFirmaInspector = true,
                MostrarFirmaDirector = !esVistaPrevia && !usaFlujoLvEae,
                EstadoDocumento = esVistaPrevia ? "EN_PREVISUALIZACION" : FirstNonEmpty(informe != null ? informe.EstadoInforme : null, "GENERADO")
            };

            var pdf = new PartialViewAsPdf("InformeTecnicoPdf", vm)
            {
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                CustomSwitches = ConstruirSwitchesPdfInformeTecnico()
            };

            return pdf.BuildFile(ControllerContext);
        }

        private string ConstruirSwitchesPdfInformeTecnico()
        {
            var switches = PdfBrandingHelper.StandardRotativaSwitches
                + " --disable-smart-shrinking --margin-top 30mm --margin-bottom 26mm --margin-left 8mm --margin-right 8mm --header-spacing 0 --footer-spacing 0";

            var headerHtmlPath = CrearArchivoBrandingTemporalInformeTecnico(true);
            var footerHtmlPath = CrearArchivoBrandingTemporalInformeTecnico(false);

            if (!string.IsNullOrWhiteSpace(headerHtmlPath))
            {
                switches += " --header-html \"" + ConvertirRutaFisicaAUrlArchivo(headerHtmlPath) + "\"";
            }

            if (!string.IsNullOrWhiteSpace(footerHtmlPath))
            {
                switches += " --footer-html \"" + ConvertirRutaFisicaAUrlArchivo(footerHtmlPath) + "\"";
            }

            return switches;
        }

        private string CrearArchivoBrandingTemporalInformeTecnico(bool esHeader)
        {
            if (Server == null)
            {
                return null;
            }

            var carpetaTemporal = Server.MapPath("~/App_Data/Temp/PdfBranding");
            if (!Directory.Exists(carpetaTemporal))
            {
                Directory.CreateDirectory(carpetaTemporal);
            }

            var fileName = esHeader ? "informe_tecnico_header.html" : "informe_tecnico_footer.html";
            var htmlPath = Path.Combine(carpetaTemporal, fileName);
            var html = esHeader ? ConstruirHtmlHeaderHojaInformeTecnico() : ConstruirHtmlFooterHojaInformeTecnico();

            if (string.IsNullOrWhiteSpace(html))
            {
                html = ConstruirHtmlBrandingFallbackInformeTecnico(esHeader);
            }

            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            System.IO.File.WriteAllText(htmlPath, html, Encoding.UTF8);
            return htmlPath;
        }

        private string ResolverInspectorAsignadoNombre(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion == null)
            {
                return "No asignado";
            }

            // ── 1. Nombre almacenado en la asignación (ya proviene de RT) ──
            var nombreAlmacenado = FirstNonEmpty(
                inspeccion.InspectorPrincipalNombre,
                solicitud != null ? solicitud.TecnicoResponsableNombre : null);

            var cedulaAlmacenada = FirstNonEmpty(
                inspeccion.InspectorPrincipalCedula,
                solicitud != null ? solicitud.TecnicoResponsableCedula : null);

            // ── 2. Catálogo RT / Inspectores (fuente oficial) ──
            UsuarioInternoRTRegistro registroRt = null;
            try
            {
                // Buscar por tecnico_id / usuario_id si hay código numérico
                if (inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                {
                    registroRt = _usuarioInternoRTDAO.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(
                        inspeccion.CodigoInspector.Value);
                }

                // Buscar por cédula/código si no se encontró por ID
                if (registroRt == null && !string.IsNullOrWhiteSpace(cedulaAlmacenada))
                {
                    registroRt = _usuarioInternoRTDAO.ObtenerInspectorAsignableActivo(cedulaAlmacenada);
                }
            }
            catch
            {
            }

            // Si encontró en RT, usar nombre completo oficial
            if (registroRt != null && !string.IsNullOrWhiteSpace(registroRt.NombreVisual))
            {
                var identificacionRt = FirstNonEmpty(registroRt.Identificacion, registroRt.CodigoUsuario);
                return string.IsNullOrWhiteSpace(identificacionRt)
                    ? registroRt.NombreVisual.Trim()
                    : registroRt.NombreVisual.Trim() + " - " + identificacionRt.Trim();
            }

            // ── 3. Nombre almacenado como respaldo ──
            if (!string.IsNullOrWhiteSpace(nombreAlmacenado))
            {
                return string.IsNullOrWhiteSpace(cedulaAlmacenada)
                    ? nombreAlmacenado
                    : nombreAlmacenado + " - " + cedulaAlmacenada;
            }

            if (!inspeccion.CodigoInspector.HasValue || inspeccion.CodigoInspector.Value <= 0)
            {
                return "No asignado";
            }

            var codigoInspector = inspeccion.CodigoInspector.Value;

            // ── 4. Fallback tablas legacy (tbtecnico/usuario) ──
            try
            {
                var tecnicoRt = _usuarioInternoRTDAO.ObtenerTecnicoDisponiblePorId(codigoInspector);
                if (tecnicoRt != null && !string.IsNullOrWhiteSpace(tecnicoRt.NombreCompleto))
                {
                    var identificacionRt = FirstNonEmpty(tecnicoRt.CodigoUsuario, tecnicoRt.Identificacion);
                    return string.IsNullOrWhiteSpace(identificacionRt)
                        ? tecnicoRt.NombreCompleto.Trim()
                        : tecnicoRt.NombreCompleto.Trim() + " - " + identificacionRt.Trim();
                }
            }
            catch
            {
            }

            try
            {
                var usuario = UsuarioDAO.ObtenerPorId(codigoInspector);
                if (usuario != null)
                {
                    var nombreUsuario = FirstNonEmpty(
                        usuario.NombreCompleto,
                        string.Join(" ", new[] { usuario.NombreUsuario, usuario.ApellidoUsuario }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim());

                    if (!string.IsNullOrWhiteSpace(nombreUsuario))
                    {
                        var codigoUsuario = usuario.CodigoUsuario;
                        return string.IsNullOrWhiteSpace(codigoUsuario)
                            ? nombreUsuario.Trim()
                            : nombreUsuario.Trim() + " - " + codigoUsuario.Trim();
                    }
                }
            }
            catch
            {
            }

            return codigoInspector.ToString();
        }

        private void EnriquecerInspectoresInformeTecnico(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            EnriquecerInspectoresDetalle(inspeccion, solicitud);

            if (inspeccion == null || !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre))
            {
                return;
            }

            var inspectorAsignado = ResolverInspectorAsignadoNombre(inspeccion, solicitud);
            var codigoInspector = inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : null;
            if (string.IsNullOrWhiteSpace(inspectorAsignado)
                || string.Equals(inspectorAsignado, "No asignado", StringComparison.OrdinalIgnoreCase)
                || string.Equals(inspectorAsignado, codigoInspector, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var separador = inspectorAsignado.LastIndexOf(" - ", StringComparison.Ordinal);
            if (separador > 0)
            {
                inspeccion.InspectorPrincipalNombre = inspectorAsignado.Substring(0, separador).Trim();
                if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula))
                {
                    inspeccion.InspectorPrincipalCedula = inspectorAsignado.Substring(separador + 3).Trim();
                }

                return;
            }

            inspeccion.InspectorPrincipalNombre = inspectorAsignado.Trim();
        }

        private string ConstruirHtmlHeaderHojaInformeTecnico()
        {
            var barra = ObtenerFuenteBrandingHojaInformeTecnico("barra.png");
            var escudo = ObtenerFuenteBrandingHojaInformeTecnico("escudo.png");
            var dgca = ObtenerFuenteBrandingHojaInformeTecnico("DGCA.png");

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(escudo) || string.IsNullOrWhiteSpace(dgca))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}"
                + ".header{{position:relative;width:194mm;height:26mm;}}"
                + ".barra{{position:absolute;top:0;right:0;width:129mm;height:3.2mm;}}"
                + ".escudo{{position:absolute;left:0;top:6.2mm;width:34mm;height:auto;}}"
                + ".dgca{{position:absolute;right:0;top:8.2mm;width:82mm;height:auto;}}</style>"
                + "</head><body><div class=\"header\">"
                + "<img class=\"barra\" src=\"{0}\" alt=\"\" />"
                + "<img class=\"escudo\" src=\"{1}\" alt=\"Escudo Republica del Ecuador\" />"
                + "<img class=\"dgca\" src=\"{2}\" alt=\"Direccion General de Aviacion Civil\" />"
                + "</div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(escudo),
                HttpUtility.HtmlAttributeEncode(dgca));
        }

        private string ConstruirHtmlFooterHojaInformeTecnico()
        {
            var barra = ObtenerFuenteBrandingHojaInformeTecnico("barra.png");
            var direccion = ObtenerFuenteBrandingHojaInformeTecnico("direccion.png");
            var nuevo = ObtenerFuenteBrandingHojaInformeTecnico("nuevo.png");

            if (string.IsNullOrWhiteSpace(barra) || string.IsNullOrWhiteSpace(direccion) || string.IsNullOrWhiteSpace(nuevo))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;height:26mm;background:transparent;overflow:hidden;}}"
                + ".footer{{position:relative;width:194mm;height:26mm;}}"
                + ".barra{{position:absolute;left:0;top:0;width:72mm;height:3.2mm;}}"
                + ".direccion{{position:absolute;left:0mm;top:7.2mm;width:64mm;height:auto;}}"
                + ".nuevo{{position:absolute;right:0;top:6.2mm;width:44mm;height:auto;}}</style>"
                + "</head><body><div class=\"footer\">"
                + "<img class=\"barra\" src=\"{0}\" alt=\"\" />"
                + "<img class=\"direccion\" src=\"{1}\" alt=\"Direccion DGAC\" />"
                + "<img class=\"nuevo\" src=\"{2}\" alt=\"El Nuevo Ecuador\" />"
                + "</div></body></html>",
                HttpUtility.HtmlAttributeEncode(barra),
                HttpUtility.HtmlAttributeEncode(direccion),
                HttpUtility.HtmlAttributeEncode(nuevo));
        }

        private string ConstruirHtmlBrandingFallbackInformeTecnico(bool esHeader)
        {
            var assets = PdfBrandingHelper.ResolveAssets(Server, "InspeccionController.CrearArchivoBrandingTemporalInformeTecnico");
            var imageSrc = esHeader
                ? ObtenerFuenteBrandingInformeTecnico(assets != null ? assets.HeaderPhysicalPath : null, assets != null ? assets.HeaderDataUri : null)
                : ObtenerFuenteBrandingInformeTecnico(assets != null ? assets.FooterPhysicalPath : null, assets != null ? assets.FooterDataUri : null);

            if (string.IsNullOrWhiteSpace(imageSrc))
            {
                return null;
            }

            return string.Format(
                "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                + "<style>html,body{{margin:0;padding:0;width:194mm;background:transparent;}}"
                + ".wrap{{width:100%;text-align:center;line-height:0;}}"
                + ".wrap img{{display:block;width:194mm;max-width:194mm;height:auto;margin:0 auto;}}</style>"
                + "</head><body><div class=\"wrap\"><img src=\"{0}\" alt=\"{1}\" /></div></body></html>",
                HttpUtility.HtmlAttributeEncode(imageSrc),
                esHeader ? "Header institucional DGAC" : "Footer institucional DGAC");
        }

        private string ObtenerFuenteBrandingHojaInformeTecnico(string fileName)
        {
            var physicalPath = Server.MapPath("~/Content/assets/imganes/hoja/" + fileName);
            if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
            {
                return null;
            }

            return ConvertirRutaFisicaAUrlArchivo(physicalPath);
        }

        private static string ObtenerFuenteBrandingInformeTecnico(string physicalPath, string dataUri)
        {
            if (!string.IsNullOrWhiteSpace(physicalPath) && System.IO.File.Exists(physicalPath))
            {
                return ConvertirRutaFisicaAUrlArchivo(physicalPath);
            }

            return dataUri;
        }

        private static string ConvertirRutaFisicaAUrlArchivo(string physicalPath)
        {
            if (string.IsNullOrWhiteSpace(physicalPath))
            {
                return null;
            }

            return "file:///" + physicalPath.Replace('\\', '/');
        }

        private string GuardarInformeTecnicoPdf(int codigoInspeccion, int version, byte[] pdfBytes)
        {
            var basePath = Server.MapPath(CARPETA_VIRTUAL_INFORMES_TECNICOS);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var fileName = string.Format("InformeTecnico_{0}_v{1}_{2}.pdf", codigoInspeccion, version, DateTime.Now.ToString("yyyyMMddHHmmss"));
            var fullPath = Path.Combine(basePath, fileName);
            System.IO.File.WriteAllBytes(fullPath, pdfBytes ?? new byte[0]);
            return CARPETA_VIRTUAL_INFORMES_TECNICOS.TrimStart('~') + "/" + fileName;
        }

        private string GuardarInformeTecnicoPreviewPdf(int codigoInspeccion, int usuarioId, byte[] pdfBytes)
        {
            LimpiarPdfTemporalesAntiguos();
            var basePath = Server.MapPath(CARPETA_VIRTUAL_TEMP_PDF);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var token = string.Format(
                "InformeTecnico_Preview_{0}_{1}_{2}_{3}",
                codigoInspeccion,
                usuarioId,
                DateTime.Now.ToString("yyyyMMddHHmmss"),
                Guid.NewGuid().ToString("N"));
            var fullPath = Path.Combine(basePath, token + ".pdf");
            System.IO.File.WriteAllBytes(fullPath, pdfBytes ?? new byte[0]);
            return token;
        }

        private void LimpiarPdfTemporalesAntiguos()
        {
            try
            {
                var basePath = Server.MapPath(CARPETA_VIRTUAL_TEMP_PDF);
                if (!Directory.Exists(basePath))
                {
                    return;
                }

                var limite = DateTime.Now.AddHours(-6);
                foreach (var file in Directory.GetFiles(basePath, "InformeTecnico_Preview_*.pdf"))
                {
                    try
                    {
                        if (System.IO.File.GetLastWriteTime(file) < limite)
                        {
                            System.IO.File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private bool UsaFlujoListaVerificacionOperacionalEae(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return false;
            }

            // En el modulo AOCR el informe tecnico debe pasar siempre por la LV RDAC 129.
            // No se condiciona a campos opcionales de la solicitud, porque en registros
            // incompletos el bloque desaparecia de la pantalla del informe tecnico.
            if (solicitud.CodigoSolicitud > 0)
            {
                return true;
            }

            var resumenOperaciones = FirstNonEmpty(solicitud.ResumenOperacionesEae, solicitud.DescripcionOperacion);
            if (!string.IsNullOrWhiteSpace(resumenOperaciones)
                || !string.IsNullOrWhiteSpace(solicitud.AprobacionesEspeciales)
                || !string.IsNullOrWhiteSpace(solicitud.AeropuertosEcuador)
                || !string.IsNullOrWhiteSpace(solicitud.CompaniasSeleccionadas))
            {
                return true;
            }

            var tipoOperacion = solicitud.TipoOperacion ?? string.Empty;
            return tipoOperacion.IndexOf("EAE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HidratarListaVerificacionOperacionalEae(ListaVerificacionOperacionalEae lista, SolicitudAOCR solicitud)
        {
            if (lista == null)
            {
                return;
            }

            CompletarCabeceraListaVerificacionOperacionalEae(lista, solicitud);

            var plantilla = ObtenerPlantillaListaVerificacionOperacionalEae(solicitud);
            var respuestas = DeserializarItemsListaVerificacionOperacionalEae(lista.ItemsJson)
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Codigo))
                .ToList();

            var respuestasPorCodigo = respuestas
                .GroupBy(item => item.Codigo, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var respuestasPorPregunta = respuestas
                .Select(item => new { Clave = ObtenerClavePreguntaListaVerificacionOperacionalEae(item), Item = item })
                .Where(item => !string.IsNullOrWhiteSpace(item.Clave))
                .GroupBy(item => item.Clave, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Item, StringComparer.OrdinalIgnoreCase);

            foreach (var item in plantilla)
            {
                ListaVerificacionOperacionalEaeItem respuesta = null;
                var clavePregunta = ObtenerClavePreguntaListaVerificacionOperacionalEae(item);
                if ((!respuestasPorCodigo.TryGetValue(item.Codigo, out respuesta) || respuesta == null)
                    && (string.IsNullOrWhiteSpace(clavePregunta)
                        || !respuestasPorPregunta.TryGetValue(clavePregunta, out respuesta)
                        || respuesta == null))
                {
                    continue;
                }

                item.EstadoCumplimiento = NormalizarCumplimientoListaVerificacionOperacionalEae(respuesta.EstadoCumplimiento);
                item.EstadoImplementacion = NormalizarImplementacionListaVerificacionOperacionalEae(respuesta.EstadoImplementacion);
                item.PruebasNotasComentarios = (respuesta.PruebasNotasComentarios ?? string.Empty).Trim();
            }

            UnificarNotasListaVerificacionOperacionalEae(plantilla);

            lista.Items = plantilla;
            if (string.IsNullOrWhiteSpace(lista.ResultadoGeneral))
            {
                lista.ResultadoGeneral = CalcularResultadoGeneralListaVerificacionOperacionalEae(plantilla);
            }
        }

        private static string ObtenerClavePreguntaListaVerificacionOperacionalEae(ListaVerificacionOperacionalEaeItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var codigoPregunta = (item.CodigoPregunta ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(codigoPregunta))
            {
                return codigoPregunta;
            }

            return (item.Codigo ?? string.Empty).Trim();
        }

        private static void UnificarNotasListaVerificacionOperacionalEae(IList<ListaVerificacionOperacionalEaeItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            foreach (var grupo in items
                .Where(item => item != null)
                .GroupBy(ObtenerClavePreguntaListaVerificacionOperacionalEae, StringComparer.OrdinalIgnoreCase))
            {
                ListaVerificacionOperacionalEaeItem itemBase = null;
                foreach (var item in grupo.OrderBy(valor => valor.Orden))
                {
                    if (!item.EsNotaOrientacion || itemBase == null)
                    {
                        itemBase = item;
                        continue;
                    }

                    var cumplimiento = !string.IsNullOrWhiteSpace(itemBase.EstadoCumplimiento)
                        ? itemBase.EstadoCumplimiento
                        : item.EstadoCumplimiento;
                    var implementacion = !string.IsNullOrWhiteSpace(itemBase.EstadoImplementacion)
                        ? itemBase.EstadoImplementacion
                        : item.EstadoImplementacion;
                    var comentarios = !string.IsNullOrWhiteSpace(itemBase.PruebasNotasComentarios)
                        ? itemBase.PruebasNotasComentarios
                        : item.PruebasNotasComentarios;

                    itemBase.EstadoCumplimiento = NormalizarCumplimientoListaVerificacionOperacionalEae(cumplimiento);
                    itemBase.EstadoImplementacion = NormalizarImplementacionListaVerificacionOperacionalEae(implementacion);
                    itemBase.PruebasNotasComentarios = (comentarios ?? string.Empty).Trim();

                    item.EstadoCumplimiento = itemBase.EstadoCumplimiento;
                    item.EstadoImplementacion = itemBase.EstadoImplementacion;
                    item.PruebasNotasComentarios = itemBase.PruebasNotasComentarios;
                }
            }
        }

        private void CompletarCabeceraListaVerificacionOperacionalEae(ListaVerificacionOperacionalEae lista, SolicitudAOCR solicitud)
        {
            if (lista == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(lista.NombreEae))
            {
                lista.NombreEae = FirstNonEmpty(solicitud != null ? solicitud.NombreOperador : null, solicitud != null ? solicitud.NombreComercial : null, solicitud != null ? solicitud.RazonSocial : null);
            }

            if (string.IsNullOrWhiteSpace(lista.NumeroAocFechaValidez))
            {
                lista.NumeroAocFechaValidez = FirstNonEmpty(solicitud != null ? solicitud.NumeroAOC : null);
            }

            if (string.IsNullOrWhiteSpace(lista.DireccionEstadoExplotador))
            {
                lista.DireccionEstadoExplotador = FirstNonEmpty(solicitud != null ? solicitud.Direccion : null, solicitud != null ? solicitud.Pais : null);
            }

            if (string.IsNullOrWhiteSpace(lista.DireccionEstadoReconocimiento))
            {
                lista.DireccionEstadoReconocimiento = "Direccion General de Aviacion Civil del Ecuador";
            }

            if (string.IsNullOrWhiteSpace(lista.TiposAeronaves) && solicitud != null && solicitud.CodigoSolicitud > 0)
            {
                lista.TiposAeronaves = ObtenerResumenAeronavesSolicitud(solicitud.CodigoSolicitud);
            }

            if (string.IsNullOrWhiteSpace(lista.TipoOperacion))
            {
                lista.TipoOperacion = FirstNonEmpty(solicitud != null ? solicitud.TipoOperacion : null, solicitud != null ? solicitud.DescripcionOperacion : null);
            }

            if (!lista.FechaLista.HasValue)
            {
                lista.FechaLista = DateTime.Now;
            }

            if (string.IsNullOrWhiteSpace(lista.InspectorResponsable))
            {
                lista.InspectorResponsable = FirstNonEmpty(solicitud != null ? solicitud.TecnicoResponsableNombre : null, ObtenerUsuarioActual());
            }

            if (string.IsNullOrWhiteSpace(lista.CargoInspector))
            {
                lista.CargoInspector = FirstNonEmpty(solicitud != null ? solicitud.TecnicoResponsableTipo : null, "Tecnico / Inspector responsable");
            }
        }

        private string ObtenerResumenAeronavesSolicitud(int codigoSolicitud)
        {
            try
            {
                var aeronaves = new AeronaveSolicitudDAO().ObtenerPorSolicitud(codigoSolicitud);
                if (aeronaves == null || aeronaves.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join("; ", aeronaves.Select(aeronave => string.Join(" ", new[]
                {
                    aeronave != null ? aeronave.Marca : null,
                    aeronave != null ? aeronave.Modelo : null,
                    aeronave != null ? aeronave.Serie : null,
                    aeronave != null ? aeronave.Matricula : null
                }.Where(valor => !string.IsNullOrWhiteSpace(valor)))));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] No se pudo cargar flota para LV EAE. SolicitudId=" + codigoSolicitud + ", Error=" + ex.Message);
                return string.Empty;
            }
        }

        private sealed class OrientacionPlantillaLvEae
        {
            public string Texto { get; set; }
            public bool EsNotaOrientacion { get; set; }
            public bool EsLiteral { get; set; }
            public bool EsSubnumeral { get; set; }
        }

        private List<ListaVerificacionOperacionalEaeItem> ObtenerPlantillaListaVerificacionOperacionalEae(SolicitudAOCR solicitud)
        {
            var items = new List<ListaVerificacionOperacionalEaeItem>();
            var orden = 1;

            AgregarGrupoLvEae(items, ref orden, 1, "129.010 (a)\n129.100 (b) (1)", "129-1",
                "Ha presentado el explotador extranjero el formulario de solicitud de reconocimiento de su AOC?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que el explotador haya completado los formularios de solicitud de la forma y manera que prescribe la AAC.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("Nota: Revisar el formulario y asegurarse que este debidamente completado.", esNotaOrientacion: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que los datos coincidan con los registros presentados.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 2, "129.100 (b) (5) y (7)", "129-2",
                "Ha presentado el explotador extranjero una descripcion de la operacion propuesta?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacionPlantillaLvEae("1. Verificar que dentro de la descripcion se identifique si se trata de:", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("a. operaciones regulares o no regulares.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("b. transporte de pasajeros/carga/otros.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que en la descripcion del area de operaciones se identifiquen:", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("a. los aeropuertos que se pretende utilizar.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("b. las areas especiales en que pretende operar; por ejemplo Cordillera de los Andes.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("c. las aprobaciones especificas requeridas.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("3. Verificar que el explotador haya presentado los tipos y las matriculas de las aeronaves sujetas a la operacion.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("Nota 1. Algunos Estados requieren que las aeronaves esten listadas en las OpSpecs. Si no estan en las OpSpecs, el explotador debe presentarlas en otro documento, como una parte del manual de operaciones, o copias de los certificados de matricula que atestiguen que el EAE es el explotador de dichas aeronaves.", esNotaOrientacion: true),
                CrearOrientacionPlantillaLvEae("Nota 2. Verificar la nacionalidad de las aeronaves involucradas. Es posible que el Estado de matricula sea diferente del Estado del explotador. En este caso, identificar los Estados de matricula en la Casilla 14.", esNotaOrientacion: true));

            AgregarGrupoLvEae(items, ref orden, 3, "129.200 (a) (3) y (4)", "129-3",
                "Ha presentado el explotador extranjero los contratos de las aeronaves?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacionPlantillaLvEae("1. Verificar que se presenten los contratos de las aeronaves afectadas a intercambio o arrendamiento de aeronave con tripulacion.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. En caso de arrendamiento de aeronave con tripulacion, verificar aprobacion de la AAC del Estado del explotador.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("3. Si existe acuerdo del Articulo 83 bis, verificar resumenes del acuerdo.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("4. Verificar certificados de cobertura de seguro para cada aeronave.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 4, "129.100 (b) (8)", "129-4",
                "Ha presentado el explotador extranjero los certificados de ruido de las aeronaves?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacionPlantillaLvEae("1. Verificar que el certificado de ruido y documentos tecnicos de respaldo esten de acuerdo al Anexo 16, Volumen 1.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que la homologacion acustica fue otorgada o convalidada por el Estado de matricula y se encuentre vigente.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 5, "129.100 (b) (2)", "129-5",
                "Ha presentado el explotador extranjero una copia de su AOC y las OpSpecs actualizadas que autorizan las operaciones solicitadas?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que el AOC contiene la informacion requerida y autoriza las operaciones solicitadas.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que las OpSpecs contienen la informacion requerida y autorizan las operaciones solicitadas.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("a. modelos de aeronave.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("b. tipo de transporte.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("c. area de operaciones.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("d. aprobaciones especificas.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("e. matriculas y aerodromos, si aplica.", esLiteral: true));

            AgregarGrupoLvEae(items, ref orden, 6, "129.100 (b) (3)", "129-6",
                "Ha presentado el explotador extranjero una copia de su manual de operaciones vigente?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que el manual de operaciones este completo con el contenido requerido.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar indicaciones de aprobacion/aceptacion.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("3. Verificar que rutas y aerodromos contienen informacion de:", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("a. comunicaciones.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("b. navegacion.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("c. aerodromos.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("d. aproximaciones.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("e. llegadas y salidas por instrumentos.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("4. Verificar procedimientos o entrenamientos para areas o aerodromos especiales.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("5. Verificar procedimientos especificos de despacho si el Estado los requiere.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 7, "129.100 (b) (9)", "129-7",
                "Ha presentado el explotador extranjero una copia del plan operacional de vuelo para cada ruta que pretende utilizar?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que los planes operacionales de vuelo contienen modelo, pesos, combustible, rutas y aerodromos de alternativa.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que los planes son adecuados frente a la descripcion de la operacion propuesta y las autorizaciones OpSpecs.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 8, "129.100 (b) (9)", "129-8",
                "Ha presentado el explotador extranjero informacion sobre los servicios de mantenimiento que pretende utilizar?",
                "Esta parte requiere coordinacion con los inspectores de aeronavegabilidad.",
                CrearOrientacionPlantillaLvEae("1. Verificar extension y alcance del mantenimiento que pretende realizar.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar si usara estructura propia u OMA contratada.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("3. Verificar que la organizacion indicada esta autorizada y capacitada para los servicios pretendidos.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 9, "129.100 (b) (9)", "129-9",
                "Ha presentado el explotador extranjero los contratos o cartas de intencion de los servicios de tierra que pretende utilizar?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que los servicios de tierra incluyan, si aplica:", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("a. manipulacion de equipaje y carga.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("b. despacho y atencion a pasajeros.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("c. combustible y aceite.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("d. deshielo y antihielo.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("e. limpieza.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("f. aprovisionamiento del servicio de a bordo.", esLiteral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que las organizaciones de servicios de tierra garantizan capacitacion adecuada, incluyendo mercancias peligrosas si aplica.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 10, "129.010 (b)\n129.100 (b) (9)", "129-10",
                "Ha presentado el explotador extranjero informacion sobre su sistema de gestion de seguridad operacional (SMS), en cumplimiento del Anexo 19?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que el explotador tiene todos los elementos minimos presentes.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que tiene implementado el programa de analisis de datos de vuelo como parte del SMS.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("3. De ser requerido por el Estado, verificar que el plan de respuesta ante emergencias es adecuado al pais y aeropuertos que pretende utilizar.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 11, "129.010 (b)\n129.100 (b) (9)", "129-11",
                "Ha presentado el explotador extranjero informacion sobre el cumplimiento del Anexo 1 por su tripulacion?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que las licencias de tripulacion de vuelo son expedidas o convalidadas por el Estado de matricula de las aeronaves.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar que la tripulacion puede hablar y comprender el idioma utilizado para comunicaciones radiotelefonicas en el Estado.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 12, "129.010 (b)\n129.100 (b) (9)", "129-12",
                "Ha presentado el explotador extranjero informacion sobre alguna exencion emitida por su AAC que se aplique a las operaciones solicitadas?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar si la exencion no interfiere con el cumplimiento de los Anexos mencionados.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("2. Verificar anotaciones en certificados de aeronavegabilidad y/o licencias cuando hayan sido objeto de exencion.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("3. Verificar si la AAC del explotador solicito la aceptacion de la exencion a la AAC y si fue aceptada.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 13, "129.100 (b) (6)", "129-13",
                "Ha presentado el explotador extranjero una copia de su plan de seguridad de la aviacion?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que el plan de seguridad de la aviacion, por su calidad de restricto, haya sido presentado a las personas autorizadas en la AAC u organismo apropiado del Estado.", esSubnumeral: true));

            AgregarGrupoLvEae(items, ref orden, 14, "129.100 (b) (4)", "129-14",
                "Ha presentado el explotador extranjero una copia del documento que autoriza los derechos de transito especificos, expedidos por la autoridad del Estado en que pretende operar?",
                null,
                CrearOrientacionPlantillaLvEae("1. Verificar que existe una autorizacion de derecho de transito expedida por la AAC y otro organismo nacional competente.", esSubnumeral: true),
                CrearOrientacionPlantillaLvEae("Nota. Si la autorizacion se emite despues de la evaluacion tecnica, puede considerarse no aplicable justificando en la Casilla 14.", esNotaOrientacion: true));

            return items;
        }

        private static OrientacionPlantillaLvEae CrearOrientacionPlantillaLvEae(string texto, bool esSubnumeral = false, bool esLiteral = false, bool esNotaOrientacion = false)
        {
            return new OrientacionPlantillaLvEae
            {
                Texto = texto,
                EsNotaOrientacion = esNotaOrientacion,
                EsLiteral = esLiteral,
                EsSubnumeral = esSubnumeral
            };
        }

        private static void AgregarGrupoLvEae(List<ListaVerificacionOperacionalEaeItem> items, ref int ordenGlobal, int grupoRequisitoId, string referencia, string codigoPregunta, string preguntaRequisito, string notaPregunta, params OrientacionPlantillaLvEae[] orientaciones)
        {
            if (items == null || orientaciones == null || orientaciones.Length == 0)
            {
                return;
            }

            var codigoPreguntaNormalizado = (codigoPregunta ?? string.Empty).Trim();
            var referenciaNormalizada = (referencia ?? string.Empty).Trim();
            var preguntaNormalizada = (preguntaRequisito ?? string.Empty).Trim();
            var notaNormalizada = (notaPregunta ?? string.Empty).Trim();
            var indiceOrientacion = 1;

            foreach (var orientacion in orientaciones.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Texto)))
            {
                items.Add(new ListaVerificacionOperacionalEaeItem
                {
                    Codigo = codigoPreguntaNormalizado + "-" + indiceOrientacion.ToString("00"),
                    CodigoPregunta = codigoPreguntaNormalizado,
                    Orden = ordenGlobal++,
                    GrupoRequisitoId = grupoRequisitoId,
                    Referencia = referenciaNormalizada,
                    PreguntaRequisito = preguntaNormalizada,
                    NotaPregunta = notaNormalizada,
                    OrientacionEvidencia = orientacion.Texto,
                    EsOrientacionIndependiente = true,
                    EsNotaOrientacion = orientacion.EsNotaOrientacion,
                    EsLiteral = orientacion.EsLiteral,
                    EsSubnumeral = orientacion.EsSubnumeral
                });

                indiceOrientacion++;
            }
        }

        private List<ListaVerificacionOperacionalEaeItem> DeserializarItemsListaVerificacionOperacionalEae(string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(itemsJson))
            {
                return new List<ListaVerificacionOperacionalEaeItem>();
            }

            try
            {
                return JsonConvert.DeserializeObject<List<ListaVerificacionOperacionalEaeItem>>(itemsJson) ?? new List<ListaVerificacionOperacionalEaeItem>();
            }
            catch
            {
                return new List<ListaVerificacionOperacionalEaeItem>();
            }
        }

        private static string NormalizarCumplimientoListaVerificacionOperacionalEae(string resultado)
        {
            var valor = (resultado ?? string.Empty).Trim().ToUpperInvariant();
            if (valor == "SATISFACTORIO" || valor == "NO_SATISFACTORIO" || valor == "NO_APLICABLE")
            {
                return valor;
            }

            return string.Empty;
        }

        private static string NormalizarImplementacionListaVerificacionOperacionalEae(string resultado)
        {
            var valor = (resultado ?? string.Empty).Trim().ToUpperInvariant();
            if (valor == "IMPLEMENTADO" || valor == "NO_IMPLEMENTADO" || valor == "NO_APLICABLE")
            {
                return valor;
            }

            return string.Empty;
        }

        private static string CalcularResultadoGeneralListaVerificacionOperacionalEae(IEnumerable<ListaVerificacionOperacionalEaeItem> items)
        {
            var lista = items != null ? items.Where(item => item != null).ToList() : new List<ListaVerificacionOperacionalEaeItem>();
            if (lista.Count == 0 || lista.Any(item => string.IsNullOrWhiteSpace(item.EstadoCumplimiento) || string.IsNullOrWhiteSpace(item.EstadoImplementacion)))
            {
                return "PENDIENTE";
            }

            if (lista.Any(item => string.Equals(item.EstadoCumplimiento, "NO_SATISFACTORIO", StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.EstadoImplementacion, "NO_IMPLEMENTADO", StringComparison.OrdinalIgnoreCase)))
            {
                return "NO CONFORME";
            }

            if (lista.All(item => string.Equals(item.EstadoCumplimiento, "NO_APLICABLE", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.EstadoImplementacion, "NO_APLICABLE", StringComparison.OrdinalIgnoreCase)))
            {
                return "NO APLICA";
            }

            return "CONFORME";
        }

        private bool ValidarPrecondicionListaVerificacionOperacionalEae(Inspeccion inspeccion, SolicitudAOCR solicitud, out ListaVerificacionOperacionalEae lista, out string mensaje)
        {
            return ValidarPrecondicionInformeTecnico(inspeccion, solicitud, true, out lista, out mensaje);
        }

        private bool ValidarPrecondicionInformeTecnico(Inspeccion inspeccion, SolicitudAOCR solicitud, bool requiereListaFirmada, out ListaVerificacionOperacionalEae lista, out string mensaje)
        {
            lista = null;
            mensaje = string.Empty;

            if (!UsaFlujoListaVerificacionOperacionalEae(solicitud))
            {
                return true;
            }

            lista = _listaVerificacionOperacionalEaeDAO.ObtenerUltimaPorInspeccion(inspeccion.CodigoInspeccion);
            HidratarListaVerificacionOperacionalEae(lista, solicitud);
            if (lista == null || !lista.Finalizado)
            {
                mensaje = "No se puede elaborar el Informe Técnico porque la Lista de Verificación Operacional LV/EAE aún no ha sido finalizada.";
                return false;
            }

            if (requiereListaFirmada && !lista.FirmadoTecnico)
            {
                mensaje = "Para continuar con la firma del Informe Técnico primero debe completar y firmar la Lista de Verificación Operacional (LV).";
                return false;
            }

            return true;
        }

        private bool ValidarInformeTecnicoParaFinalizar(InspeccionInformeTecnico informe, out string mensaje)
        {
            mensaje = string.Empty;
            if (informe == null)
            {
                mensaje = "No existe un Informe Técnico registrado para finalizar.";
                return false;
            }

            var camposObligatorios = new[]
            {
                new { Nombre = "Antecedentes", Valor = informe.Antecedentes },
                new { Nombre = "Objetivo de la inspección", Valor = informe.Resumen },
                new { Nombre = "Alcance", Valor = informe.Alcance },
                new { Nombre = "Desarrollo técnico", Valor = informe.Desarrollo },
                new { Nombre = "Fecha de inspección", Valor = informe.FechasInspeccionManual },
                new { Nombre = "Estación o cobertura inspeccionada", Valor = informe.EstacionesInspeccionManual },
                new { Nombre = "Conclusiones", Valor = informe.Conclusiones },
                new { Nombre = "Recomendaciones", Valor = informe.Recomendaciones },
                new { Nombre = "Resultado técnico final", Valor = informe.Resultado }
            };

            var campoPendiente = camposObligatorios.FirstOrDefault(campo => string.IsNullOrWhiteSpace(campo.Valor));
            if (campoPendiente != null)
            {
                mensaje = "Complete el campo obligatorio del Informe Técnico: " + campoPendiente.Nombre + ".";
                return false;
            }

            if (InformeTecnicoTemplateHelper.IsResultadoInsatisfactorio(informe.Resultado)
                && string.IsNullOrWhiteSpace(InformeTecnicoTemplateHelper.NormalizeTipoResultadoInsatisfactorio(informe.TipoResultadoInsatisfactorio)))
            {
                mensaje = "Debe seleccionar si el resultado insatisfactorio requiere una nueva inspección o no requiere inspección.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(informe.NoConformidades) && string.IsNullOrWhiteSpace(informe.Observaciones))
            {
                mensaje = "Registre al menos un hallazgo técnico u observación antes de finalizar el Informe Técnico.";
                return false;
            }

            return true;
        }

        private string ConstruirDetalleAuditoriaResultadoInforme(string mensajeBase, InspeccionInformeTecnico informe)
        {
            var detalle = string.IsNullOrWhiteSpace(mensajeBase)
                ? "Informe técnico actualizado."
                : mensajeBase.Trim();
            var resultadoLabel = InformeTecnicoTemplateHelper.GetResultadoLabel(informe != null ? informe.Resultado : null);
            if (!string.IsNullOrWhiteSpace(resultadoLabel) && !string.Equals(resultadoLabel, "Pendiente", StringComparison.OrdinalIgnoreCase))
            {
                detalle += " Resultado técnico final: " + resultadoLabel + ".";
            }

            if (informe != null && InformeTecnicoTemplateHelper.IsResultadoInsatisfactorio(informe.Resultado))
            {
                var tipoResultadoLabel = InformeTecnicoTemplateHelper.GetTipoResultadoInsatisfactorioLabel(informe.TipoResultadoInsatisfactorio);
                if (!string.IsNullOrWhiteSpace(tipoResultadoLabel))
                {
                    detalle += " Tipo de resultado insatisfactorio: " + tipoResultadoLabel + ".";
                }
            }

            detalle += " IP=" + ObtenerIpCliente();
            return detalle;
        }

        private ListaVerificacionOperacionalEae ConstruirListaVerificacionOperacionalEaeDesdeFormulario(int codigoInspeccion, System.Collections.Specialized.NameValueCollection form, ListaVerificacionOperacionalEae listaActual, SolicitudAOCR solicitud)
        {
            var items = ObtenerPlantillaListaVerificacionOperacionalEae(solicitud);
            foreach (var grupo in items
                .Where(item => item != null)
                .GroupBy(ObtenerClavePreguntaListaVerificacionOperacionalEae, StringComparer.OrdinalIgnoreCase))
            {
                ListaVerificacionOperacionalEaeItem itemBase = null;
                foreach (var item in grupo.OrderBy(valor => valor.Orden))
                {
                    var itemCaptura = item;
                    if (item.EsNotaOrientacion && itemBase != null)
                    {
                        itemCaptura = itemBase;
                    }
                    else
                    {
                        itemBase = item;
                    }

                    var keyCumplimiento = "lvItem_" + itemCaptura.Codigo + "_cumplimiento";
                    var keyImplementacion = "lvItem_" + itemCaptura.Codigo + "_implementacion";
                    var keyComentarios = "lvItem_" + itemCaptura.Codigo + "_comentarios";
                    var cumplimiento = NormalizarCumplimientoListaVerificacionOperacionalEae(form != null ? form[keyCumplimiento] : null);
                    var implementacion = NormalizarImplementacionListaVerificacionOperacionalEae(form != null ? form[keyImplementacion] : null);
                    var comentarios = TomarCampoTexto(form, keyComentarios, 3000, string.Empty);

                    item.EstadoCumplimiento = cumplimiento;
                    item.EstadoImplementacion = implementacion;
                    item.PruebasNotasComentarios = comentarios;
                }
            }

            UnificarNotasListaVerificacionOperacionalEae(items);

            var lista = new ListaVerificacionOperacionalEae
            {
                CodigoInspeccion = codigoInspeccion,
                EstadoLista = items.All(item => !string.IsNullOrWhiteSpace(item.EstadoCumplimiento) && !string.IsNullOrWhiteSpace(item.EstadoImplementacion)) ? "LV_COMPLETADA" : "LV_BORRADOR",
                NombreEae = TomarCampoTexto(form, "lvNombreEae", 500, listaActual != null ? listaActual.NombreEae : string.Empty),
                NumeroAocFechaValidez = TomarCampoTexto(form, "lvNumeroAocFechaValidez", 500, listaActual != null ? listaActual.NumeroAocFechaValidez : string.Empty),
                DireccionEstadoExplotador = TomarCampoTexto(form, "lvDireccionEstadoExplotador", 1000, listaActual != null ? listaActual.DireccionEstadoExplotador : string.Empty),
                DireccionEstadoReconocimiento = TomarCampoTexto(form, "lvDireccionEstadoReconocimiento", 1000, listaActual != null ? listaActual.DireccionEstadoReconocimiento : string.Empty),
                TiposAeronaves = TomarCampoTexto(form, "lvTiposAeronaves", 1000, listaActual != null ? listaActual.TiposAeronaves : string.Empty),
                TipoOperacion = TomarCampoTexto(form, "lvTipoOperacion", 500, listaActual != null ? listaActual.TipoOperacion : string.Empty),
                FechaLista = ParseDateTimeSeguro(form != null ? form["lvFecha"] : null) ?? (listaActual != null ? listaActual.FechaLista : null) ?? DateTime.Now,
                InspectorResponsable = TomarCampoTexto(form, "lvInspectorResponsable", 500, listaActual != null ? listaActual.InspectorResponsable : string.Empty),
                CargoInspector = TomarCampoTexto(form, "lvCargoInspector", 500, listaActual != null ? listaActual.CargoInspector : string.Empty),
                ResumenVerificacion = TomarCampoTexto(form, "lvResumenVerificacion", 4000, listaActual != null ? listaActual.ResumenVerificacion : (solicitud != null ? solicitud.ResumenOperacionesEae : string.Empty)),
                ObservacionesGenerales = TomarCampoTexto(form, "lvObservacionesGenerales", 4000, listaActual != null ? listaActual.ObservacionesGenerales : string.Empty),
                ResultadoGeneral = CalcularResultadoGeneralListaVerificacionOperacionalEae(items),
                ItemsJson = JsonConvert.SerializeObject(items),
                Items = items,
                RutaPdf = listaActual != null ? listaActual.RutaPdf : string.Empty,
                RutaDocumentoFirmado = listaActual != null ? listaActual.RutaDocumentoFirmado : string.Empty,
                Finalizado = listaActual != null && listaActual.Finalizado,
                FirmadoTecnico = listaActual != null && listaActual.FirmadoTecnico
            };

            CompletarCabeceraListaVerificacionOperacionalEae(lista, solicitud);
            return lista;
        }

        private bool ValidarListaVerificacionOperacionalEaeSegunObservaciones(ListaVerificacionOperacionalEae lista, out string mensaje)
        {
            mensaje = string.Empty;
            if (lista == null)
            {
                mensaje = "No existe una lista de verificación operacional EAE para procesar.";
                return false;
            }

            if (lista.Items == null || lista.Items.Count == 0)
            {
                mensaje = "La lista de verificación operacional EAE no contiene ítems configurados.";
                return false;
            }

            var itemsValidables = (lista.Items ?? new List<ListaVerificacionOperacionalEaeItem>())
                .Where(item => item != null && !item.EsNotaOrientacion)
                .ToList();
            if (itemsValidables.Count == 0)
            {
                itemsValidables = (lista.Items ?? new List<ListaVerificacionOperacionalEaeItem>())
                    .Where(item => item != null)
                    .ToList();
            }

            var itemSinEstadosNiObservacion = itemsValidables.FirstOrDefault(item =>
                (string.IsNullOrWhiteSpace(item.EstadoCumplimiento)
                    || string.IsNullOrWhiteSpace(item.EstadoImplementacion))
                && string.IsNullOrWhiteSpace(item.PruebasNotasComentarios));
            if (itemSinEstadosNiObservacion != null)
            {
                mensaje = "Debe seleccionar el estado de cumplimiento/implementación o registrar una observación en la columna 14 para la orientación: " + ObtenerEtiquetaItemListaVerificacionOperacionalEae(itemSinEstadosNiObservacion);
                return false;
            }

            foreach (var grupo in itemsValidables
                .GroupBy(ObtenerClavePreguntaListaVerificacionOperacionalEae, StringComparer.OrdinalIgnoreCase))
            {
                var comentarioGrupo = grupo
                    .Select(item => (item.PruebasNotasComentarios ?? string.Empty).Trim())
                    .FirstOrDefault(valor => !string.IsNullOrWhiteSpace(valor)) ?? string.Empty;

                var itemCumplimientoNoSatisfactorio = grupo.FirstOrDefault(item =>
                    string.Equals(item.EstadoCumplimiento, "NO_SATISFACTORIO", StringComparison.OrdinalIgnoreCase));
                if (itemCumplimientoNoSatisfactorio != null && string.IsNullOrWhiteSpace(comentarioGrupo))
                {
                    mensaje = "Ingrese una observación en Pruebas / Notas / Comentarios para el requisito: " + ObtenerEtiquetaItemListaVerificacionOperacionalEae(itemCumplimientoNoSatisfactorio);
                    return false;
                }
            }

            var itemNoImplementadoSinObservacion = itemsValidables.FirstOrDefault(item =>
                string.Equals(item.EstadoImplementacion, "NO_IMPLEMENTADO", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(item.PruebasNotasComentarios));
            if (itemNoImplementadoSinObservacion != null)
            {
                mensaje = "Ingrese una observación en Pruebas / Notas / Comentarios para la orientación: " + ObtenerEtiquetaItemListaVerificacionOperacionalEae(itemNoImplementadoSinObservacion);
                return false;
            }

            return true;
        }

        private bool ValidarListaVerificacionOperacionalEaeParaFinalizar(ListaVerificacionOperacionalEae lista, out string mensaje)
        {
            mensaje = string.Empty;
            if (lista == null)
            {
                mensaje = "No existe una lista de verificación operacional EAE para procesar.";
                return false;
            }

            if (lista.Items == null || lista.Items.Count == 0)
            {
                mensaje = "La lista de verificación operacional EAE no contiene ítems configurados.";
                return false;
            }

            var camposCabeceraPendientes = new[]
            {
                new { Nombre = "Nombre del EAE / Nombre comercial del EAE", Valor = lista.NombreEae },
                new { Nombre = "N AOC / Fecha de expedicion / Validez", Valor = lista.NumeroAocFechaValidez },
                new { Nombre = "Direccion del EAE en el Estado del explotador", Valor = lista.DireccionEstadoExplotador },
                new { Nombre = "Direccion del EAE en el Estado que emite el reconocimiento", Valor = lista.DireccionEstadoReconocimiento },
                new { Nombre = "Tipo/s de aeronave/s", Valor = lista.TiposAeronaves },
                new { Nombre = "Tipo de operacion", Valor = lista.TipoOperacion },
                new { Nombre = "Inspector responsable de la aprobacion", Valor = lista.InspectorResponsable }
            };

            var campoPendiente = camposCabeceraPendientes.FirstOrDefault(campo => string.IsNullOrWhiteSpace(campo.Valor));
            if (campoPendiente != null)
            {
                mensaje = "Complete el campo de cabecera de la LV: " + campoPendiente.Nombre;
                return false;
            }

            return ValidarListaVerificacionOperacionalEaeSegunObservaciones(lista, out mensaje);
        }

        private static string ObtenerEtiquetaItemListaVerificacionOperacionalEae(ListaVerificacionOperacionalEaeItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var codigo = !string.IsNullOrWhiteSpace(item.CodigoPregunta)
                ? item.CodigoPregunta.Trim()
                : (item.Codigo ?? string.Empty).Trim();
            var orientacion = (item.OrientacionEvidencia ?? string.Empty).Replace("\r\n", " ").Replace("\n", " ").Trim();
            if (orientacion.Length > 120)
            {
                orientacion = orientacion.Substring(0, 117).TrimEnd() + "...";
            }

            if (string.IsNullOrWhiteSpace(orientacion))
            {
                return codigo;
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                return orientacion;
            }

            return codigo + " - " + orientacion;
        }

        private ListaVerificacionOperacionalEaePdfViewModel ConstruirViewModelListaVerificacionOperacionalEaePdfOficial(Inspeccion inspeccion, SolicitudAOCR solicitud, ListaVerificacionOperacionalEae lista)
        {
            EnriquecerInspectoresInformeTecnico(inspeccion, solicitud);
            HidratarListaVerificacionOperacionalEae(lista, solicitud);

            return new ListaVerificacionOperacionalEaePdfViewModel
            {
                Inspeccion = inspeccion,
                Solicitud = solicitud,
                ListaVerificacion = lista,
                MostrarFirmas = false,
                MostrarMarcaAguaBorrador = false,
                EstadoDocumento = FirstNonEmpty(lista != null ? lista.EstadoLista : null, "BORRADOR")
            };
        }

        private string ConstruirNombrePdfInformeTecnico(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, string sufijo = null)
        {
            var codigoInspeccion = inspeccion != null ? inspeccion.CodigoInspeccion : 0;
            var numeroSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? solicitud.NumeroSolicitud
                : (inspeccion != null ? inspeccion.CodigoSolicitud.ToString(CultureInfo.InvariantCulture) : null);
            var fecha = informe != null
                ? (informe.FechaFirma1 ?? informe.FechaFinalizacion ?? informe.UpdatedAt ?? informe.CreatedAt)
                : (DateTime?)null;

            return PdfFileNameHelper.CrearNombreInformeTecnico(numeroSolicitud, codigoInspeccion, fecha, sufijo);
        }

        private string ConstruirNombrePdfListaVerificacionOperacionalEae(Inspeccion inspeccion, SolicitudAOCR solicitud, ListaVerificacionOperacionalEae lista)
        {
            var codigoInspeccion = inspeccion != null ? inspeccion.CodigoInspeccion : 0;
            var nombreEae = PdfFileNameHelper.PrimerValorNoVacio(
                lista != null ? lista.NombreEae : null,
                PdfFileNameHelper.CombinarSegmentos(solicitud != null ? solicitud.Ruc : null, solicitud != null ? solicitud.NombreOperador : null),
                PdfFileNameHelper.CombinarSegmentos(solicitud != null ? solicitud.Ruc : null, solicitud != null ? solicitud.NombreComercial : null),
                PdfFileNameHelper.CombinarSegmentos(solicitud != null ? solicitud.Ruc : null, solicitud != null ? solicitud.RazonSocial : null),
                solicitud != null ? solicitud.NombreOperador : null,
                solicitud != null ? solicitud.NombreComercial : null,
                solicitud != null ? solicitud.RazonSocial : null,
                solicitud != null ? solicitud.Ruc : null);
            var fecha = lista != null
                ? (lista.FechaFirma ?? lista.FechaFinalizacion ?? lista.UpdatedAt ?? lista.FechaLista ?? lista.CreatedAt)
                : (DateTime?)null;

            return PdfFileNameHelper.CrearNombreListaVerificacionEae(nombreEae, codigoInspeccion, fecha);
        }

        private ViewAsPdf CrearPdfListaVerificacionOperacionalEaeOficial(ListaVerificacionOperacionalEaePdfViewModel vm, string fileName = null)
        {
            return new ViewAsPdf("~/Views/ListaVerificacion/PdfListaVerificacionEaeOficial.cshtml", vm)
            {
                FileName = fileName,
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins
                {
                    Top = 0,
                    Bottom = 0,
                    Left = 0,
                    Right = 0
                },
                CustomSwitches = ConstruirSwitchesPdfListaVerificacionOperacionalEaeOficial()
            };
        }

        private byte[] GenerarPdfListaVerificacionOperacionalEae(Inspeccion inspeccion, SolicitudAOCR solicitud, ListaVerificacionOperacionalEae lista)
        {
            var vm = ConstruirViewModelListaVerificacionOperacionalEaePdfOficial(inspeccion, solicitud, lista);
            var pdf = CrearPdfListaVerificacionOperacionalEaeOficial(vm);

            return pdf.BuildFile(ControllerContext);
        }

        private string ConstruirSwitchesPdfListaVerificacionOperacionalEaeOficial()
        {
            return "--print-media-type --enable-local-file-access --disable-smart-shrinking --background --dpi 96 --encoding utf-8";
        }

        private string CrearArchivoTemporalListaVerificacionOperacionalEaeOficial(bool esHeader)
        {
            if (Server == null)
            {
                return null;
            }

            var carpetaTemporal = Server.MapPath("~/App_Data/Temp/PdfBranding");
            if (!Directory.Exists(carpetaTemporal))
            {
                Directory.CreateDirectory(carpetaTemporal);
            }

            var fileName = esHeader ? "lv_eae_oficial_header.html" : "lv_eae_oficial_footer.html";
            var htmlPath = Path.Combine(carpetaTemporal, fileName);
            var html = esHeader ? ConstruirHtmlHeaderListaVerificacionOperacionalEaeOficial() : ConstruirHtmlFooterListaVerificacionOperacionalEaeOficial();

            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            System.IO.File.WriteAllText(htmlPath, html, Encoding.UTF8);
            return htmlPath;
        }

        private string ConstruirHtmlHeaderListaVerificacionOperacionalEaeOficial()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        html, body { margin: 0; padding: 0; width: 100%; font-family: Arial, Helvetica, sans-serif; font-size: 8px; color: #000; }
        .header-table { width: 100%; border-collapse: collapse; table-layout: fixed; }
        .header-table td { vertical-align: top; font-weight: bold; padding: 0; }
        .header-left { width: 28%; text-align: left; }
        .header-center { width: 44%; text-align: center; }
        .header-right { width: 28%; text-align: right; }
    </style>
</head>
<body>
    <table class=""header-table"">
        <tr>
            <td class=""header-left"">Manual del Inspector de Operaciones Ecuador</td>
            <td class=""header-center"">Volumen VII – Vigilancia de explotadores extranjeros en operaciones de transporte aéreo comercial<br />Capítulo 2 – Solicitud, evaluación y aprobación de un explotador extranjero</td>
            <td class=""header-right"">Parte II - Explotadores de servicios aéreos</td>
        </tr>
    </table>
</body>
</html>";
        }

        private string ConstruirHtmlFooterListaVerificacionOperacionalEaeOficial()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <style>
        html, body { margin: 0; padding: 0; width: 100%; font-family: Arial, Helvetica, sans-serif; font-size: 8px; color: #000; }
        .footer-table { width: 100%; border-collapse: collapse; table-layout: fixed; }
        .footer-table td { vertical-align: top; font-weight: bold; padding: 0; }
        .footer-left { width: 33%; text-align: left; }
        .footer-center { width: 34%; text-align: center; }
        .footer-right { width: 33%; text-align: right; }
    </style>
    <script>
        function subst() {
            var vars = {};
            var query = document.location.search.substring(1).split('&');
            for (var i = 0; i < query.length; i++) {
                var pair = query[i].split('=', 2);
                if (pair.length === 2) {
                    vars[pair[0]] = decodeURIComponent(pair[1].replace(/\+/g, ' '));
                }
            }

            var page = parseInt(vars.page || '1', 10);
            if (isNaN(page) || page < 1) {
                page = 1;
            }

            document.getElementById('page-code').textContent = 'PII-VVII-C2-' + (8 + page);
        }
    </script>
</head>
<body onload=""subst()"">
    <table class=""footer-table"">
        <tr>
            <td class=""footer-left"">31/12/2025</td>
            <td id=""page-code"" class=""footer-center"">PII-VVII-C2-9</td>
            <td class=""footer-right"">Tercera Edición</td>
        </tr>
    </table>
</body>
</html>";
        }

        private string GuardarListaVerificacionOperacionalEaePdf(int codigoInspeccion, int version, byte[] pdfBytes)
        {
            var basePath = Server.MapPath(CARPETA_VIRTUAL_LV_EAE);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var fileName = string.Format("ListaVerificacionEae_{0}_v{1}_{2}.pdf", codigoInspeccion, version, DateTime.Now.ToString("yyyyMMddHHmmss"));
            var fullPath = Path.Combine(basePath, fileName);
            System.IO.File.WriteAllBytes(fullPath, pdfBytes ?? new byte[0]);
            return CARPETA_VIRTUAL_LV_EAE.TrimStart('~') + "/" + fileName;
        }

        private string GuardarOReemplazarListaVerificacionOperacionalEaePdfHistorico(ListaVerificacionOperacionalEae lista, byte[] pdfBytes, int usuarioId)
        {
            var rutaRelativa = NormalizarRutaRelativaInforme(lista != null ? lista.RutaPdf : null);
            var baseDir = Server.MapPath(CARPETA_VIRTUAL_LV_EAE);

            if (!string.IsNullOrWhiteSpace(rutaRelativa))
            {
                var fullPath = ResolverRutaAbsolutaInforme(rutaRelativa);
                if (!string.IsNullOrWhiteSpace(fullPath) && EsRutaDentroDeBase(fullPath, baseDir))
                {
                    var directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    System.IO.File.WriteAllBytes(fullPath, pdfBytes ?? new byte[0]);
                    return rutaRelativa;
                }
            }

            var nuevaRuta = GuardarListaVerificacionOperacionalEaePdf(lista != null ? lista.CodigoInspeccion : 0, lista != null ? lista.Version : 0, pdfBytes);
            if (lista != null && lista.CodigoListaVerificacion > 0)
            {
                _listaVerificacionOperacionalEaeDAO.ActualizarRutaPdf(lista.CodigoListaVerificacion, nuevaRuta, usuarioId);
            }

            return nuevaRuta;
        }

        private string GuardarListaVerificacionOperacionalEaeFirmadaPdf(int codigoInspeccion, int version, byte[] pdfBytes)
        {
            var basePath = Server.MapPath(CARPETA_VIRTUAL_LV_EAE_FIRMADAS);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var fileName = string.Format("ListaVerificacionEae_{0}_v{1}_firmada_{2}.pdf", codigoInspeccion, version, DateTime.Now.ToString("yyyyMMddHHmmss"));
            var fullPath = Path.Combine(basePath, fileName);
            System.IO.File.WriteAllBytes(fullPath, pdfBytes ?? new byte[0]);
            return CARPETA_VIRTUAL_LV_EAE_FIRMADAS.TrimStart('~') + "/" + fileName;
        }

        private int ObtenerNumeroPaginasPdf(byte[] pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return 0;
            }

            try
            {
                using (var reader = new PdfReader(pdfBytes))
                {
                    return reader.NumberOfPages > 0 ? reader.NumberOfPages : 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private int ObtenerNumeroPaginasPdfArchivo(string rutaRelativa)
        {
            var fullPath = ResolverRutaAbsolutaInforme(rutaRelativa);
            if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
            {
                return 0;
            }

            try
            {
                return ObtenerNumeroPaginasPdf(System.IO.File.ReadAllBytes(fullPath));
            }
            catch
            {
                return 0;
            }
        }

        private InspeccionInformeTecnico ConstruirInformeTecnicoDesdeFormulario(int codigoInspeccion, System.Collections.Specialized.NameValueCollection form, InspeccionInformeTecnico informeActual, bool guardarAdjuntos)
        {
            var documentosAdjuntosItems = InformeTecnicoTemplateHelper.SplitLines(TomarDocumentosAdjuntos(form, informeActual != null ? informeActual.DocumentosAdjuntos : null)).ToList();
            var documentosAdjuntosArchivos = InformeTecnicoTemplateHelper.ParseDocumentosAdjuntosArchivos(informeActual != null ? informeActual.DocumentosAdjuntosArchivos : null);
            var otrosAdjuntos = TomarCampoTexto(form, "otrosAdjuntos", 4000, informeActual != null ? informeActual.OtrosAdjuntos : null);
            if (guardarAdjuntos)
            {
                var adjuntosBaseGuardados = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var archivosOtrosAdjuntos = GuardarArchivosAdjuntosInforme(codigoInspeccion, adjuntosBaseGuardados);

                foreach (var adjuntoBase in adjuntosBaseGuardados)
                {
                    if (string.IsNullOrWhiteSpace(adjuntoBase.Key) || adjuntoBase.Value == null || adjuntoBase.Value.Count == 0)
                    {
                        continue;
                    }

                    List<string> archivosRegistrados;
                    if (!documentosAdjuntosArchivos.TryGetValue(adjuntoBase.Key, out archivosRegistrados) || archivosRegistrados == null)
                    {
                        archivosRegistrados = new List<string>();
                        documentosAdjuntosArchivos[adjuntoBase.Key] = archivosRegistrados;
                    }

                    foreach (var nombreArchivo in adjuntoBase.Value.Where(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        if (!archivosRegistrados.Any(x => string.Equals(x, nombreArchivo, StringComparison.OrdinalIgnoreCase)))
                        {
                            archivosRegistrados.Add(nombreArchivo);
                        }
                    }

                    if (!documentosAdjuntosItems.Any(x => string.Equals(x, adjuntoBase.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        documentosAdjuntosItems.Add(adjuntoBase.Key);
                    }
                }

                if (archivosOtrosAdjuntos.Count > 0)
                {
                    var otrosAdjuntosItems = InformeTecnicoTemplateHelper.SplitLines(otrosAdjuntos).ToList();
                    foreach (var nombreArchivo in archivosOtrosAdjuntos)
                    {
                        if (string.IsNullOrWhiteSpace(nombreArchivo))
                        {
                            continue;
                        }

                        if (!otrosAdjuntosItems.Any(x => string.Equals(x, nombreArchivo, StringComparison.OrdinalIgnoreCase)))
                        {
                            otrosAdjuntosItems.Add(nombreArchivo);
                        }
                    }

                    otrosAdjuntos = InformeTecnicoTemplateHelper.SerializeLines(otrosAdjuntosItems);
                }
            }

            var documentosAdjuntosNormalizados = new HashSet<string>(
                documentosAdjuntosItems.Where(x => !string.IsNullOrWhiteSpace(x)),
                StringComparer.OrdinalIgnoreCase);
            documentosAdjuntosArchivos = documentosAdjuntosArchivos
                .Where(x => documentosAdjuntosNormalizados.Contains(x.Key)
                    && x.Value != null
                    && x.Value.Any(nombre => !string.IsNullOrWhiteSpace(nombre)))
                .ToDictionary(
                    x => x.Key,
                    x => x.Value
                        .Where(nombre => !string.IsNullOrWhiteSpace(nombre))
                        .Select(nombre => nombre.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
            var documentosAdjuntos = InformeTecnicoTemplateHelper.SerializeLines(documentosAdjuntosItems);
            var resultadoInforme = InformeTecnicoTemplateHelper.NormalizeResultadoInformeTecnico(
                TomarCampoTexto(form, "resultado", 120, informeActual != null ? informeActual.Resultado : null));
            var tipoResultadoInsatisfactorio = InformeTecnicoTemplateHelper.NormalizeTipoResultadoInsatisfactorio(
                TomarCampoTexto(form, "tipoResultadoInsatisfactorio", 30, null));
            if (!string.Equals(resultadoInforme, "INSATISFACTORIO", StringComparison.OrdinalIgnoreCase))
            {
                tipoResultadoInsatisfactorio = null;
            }

            return new InspeccionInformeTecnico
            {
                CodigoInspeccion = codigoInspeccion,
                Titulo = TomarCampoTexto(form, "titulo", 250, informeActual != null ? informeActual.Titulo : null),
                Resumen = TomarCampoTexto(form, "resumen", 8000, informeActual != null ? informeActual.Resumen : null),
                Antecedentes = TomarCampoTexto(form, "antecedentes", 8000, informeActual != null ? informeActual.Antecedentes : null),
                Alcance = TomarCampoTexto(form, "alcance", 8000, informeActual != null ? informeActual.Alcance : null),
                Desarrollo = TomarCampoTexto(form, "desarrollo", 12000, informeActual != null ? informeActual.Desarrollo : null),
                Evidencias = TomarCampoTexto(form, "evidencias", 12000, informeActual != null ? informeActual.Evidencias : null),
                NumeroLicenciaInspector = TomarCampoTexto(form, "numeroLicenciaInspector", 120, informeActual != null ? informeActual.NumeroLicenciaInspector : null),
                TrabajosRealizados = TomarCampoTexto(form, "trabajosRealizados", 12000, informeActual != null ? informeActual.TrabajosRealizados : null),
                FechasInspeccionManual = TomarCampoTexto(form, "fechasInspeccionManual", 500, informeActual != null ? informeActual.FechasInspeccionManual : null),
                EstacionesInspeccionManual = TomarCampoTexto(form, "estacionesInspeccionManual", 1000, informeActual != null ? informeActual.EstacionesInspeccionManual : null),
                OperacionComercial = TomarCampoTexto(form, "operacionComercial", 500, informeActual != null ? informeActual.OperacionComercial : null),
                ServiciosEstaciones = TomarServiciosEstaciones(form, informeActual != null ? informeActual.ServiciosEstaciones : null),
                Notas = TomarCampoTexto(form, "notas", 8000, informeActual != null ? informeActual.Notas : null),
                NoConformidades = TomarCampoTexto(form, "noConformidades", 8000, informeActual != null ? informeActual.NoConformidades : null),
                DocumentosAdjuntos = documentosAdjuntos,
                DocumentosAdjuntosArchivos = InformeTecnicoTemplateHelper.SerializeDocumentosAdjuntosArchivos(documentosAdjuntosArchivos),
                OtrosAdjuntos = otrosAdjuntos,
                Resultado = resultadoInforme,
                TipoResultadoInsatisfactorio = tipoResultadoInsatisfactorio,
                Observaciones = TomarCampoTexto(form, "observaciones", 8000, informeActual != null ? informeActual.Observaciones : null),
                Conclusiones = TomarCampoTexto(form, "conclusiones", 8000, informeActual != null ? informeActual.Conclusiones : null),
                Recomendaciones = TomarCampoTexto(form, "recomendaciones", 8000, informeActual != null ? informeActual.Recomendaciones : null),
                RutaPdf = informeActual != null ? informeActual.RutaPdf : null,
                EstadoInforme = informeActual != null ? informeActual.EstadoInforme : "BORRADOR_INFORME",
                Finalizado = informeActual != null && informeActual.Finalizado,
                CorreoEnviado = informeActual != null && informeActual.CorreoEnviado
            };
        }

        private InformeTecnicoModalVm ConstruirInformeTecnicoModalViewModel(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, ListaVerificacionOperacionalEae listaVerificacion, IList<DocumentoInspeccion> documentosSolicitante)
        {
            var usaFlujoLv = UsaFlujoListaVerificacionOperacionalEae(solicitud);
            var rutaInformeDisponible = ResolverRutaRelativaInformeDisponible(
                informe != null ? informe.RutaDocumentoFirmado : null,
                inspeccion != null ? inspeccion.RutaInforme : null,
                informe != null ? informe.RutaPdf : null);
            var estadoInforme = !string.IsNullOrWhiteSpace(informe != null ? informe.EstadoInforme : null)
                ? informe.EstadoInforme
                : (informe != null && informe.Finalizado ? "GENERADO" : "BORRADOR_INFORME");
            var estadoLista = !usaFlujoLv
                ? "NO_APLICA"
                : FirstNonEmpty(
                    listaVerificacion != null ? listaVerificacion.EstadoLista : null,
                    listaVerificacion != null && listaVerificacion.Finalizado ? "LV_FINALIZADA" : null,
                    "LV_BORRADOR");

            return new InformeTecnicoModalVm
            {
                CodigoInspeccion = inspeccion != null ? inspeccion.CodigoInspeccion : 0,
                Inspeccion = inspeccion ?? new Inspeccion(),
                Solicitud = solicitud ?? new SolicitudAOCR(),
                InformeTecnico = informe ?? new InspeccionInformeTecnico
                {
                    CodigoInspeccion = inspeccion != null ? inspeccion.CodigoInspeccion : 0,
                    Titulo = "INFORME TÉCNICO AOCR"
                },
                ListaVerificacion = listaVerificacion ?? new ListaVerificacionOperacionalEae
                {
                    CodigoInspeccion = inspeccion != null ? inspeccion.CodigoInspeccion : 0,
                    EstadoLista = "LV_BORRADOR"
                },
                DocumentosSolicitante = documentosSolicitante ?? new List<DocumentoInspeccion>(),
                UsaFlujoListaVerificacionOperacionalEae = usaFlujoLv,
                LvEaeFinalizada = !usaFlujoLv || (listaVerificacion != null && listaVerificacion.Finalizado),
                PuedeGestionarInformeTecnico = PuedeGestionarInformeTecnicoModal(inspeccion),
                PuedeEditarInformeTecnico = PuedeEditarInformeTecnicoModal(inspeccion),
                ExisteInformeTecnico = informe != null && informe.CodigoInforme > 0,
                ExistePdfInformeTecnico = !string.IsNullOrWhiteSpace(rutaInformeDisponible),
                EstadoInformeTecnico = estadoInforme,
                EstadoListaVerificacion = estadoLista,
                CodigoInformeTecnico = informe != null && informe.CodigoInforme > 0 ? (int?)informe.CodigoInforme : null,
                MensajeBloqueo = string.Empty,
                UrlGuardar = Url.Action("GuardarInformeTecnico", "Inspeccion"),
                UrlPrevisualizar = Url.Action("PrevisualizarInformeTecnico", "Inspeccion"),
                UrlVerPdf = inspeccion != null && inspeccion.CodigoInspeccion > 0
                    ? Url.Action("VerInforme", "Inspeccion", new { id = inspeccion.CodigoInspeccion })
                    : string.Empty,
                UrlDescargarPdf = inspeccion != null && inspeccion.CodigoInspeccion > 0
                    ? Url.Action("DescargarInforme", "Inspeccion", new { id = inspeccion.CodigoInspeccion })
                    : string.Empty
            };
        }

        private List<string> GuardarArchivosAdjuntosInforme(int codigoInspeccion, IDictionary<string, List<string>> adjuntosBaseGuardados)
        {
            var otrosAdjuntosGuardados = new List<string>();
            var documentosAdjuntosBase = InformeTecnicoTemplateHelper.GetDocumentosAdjuntosBase();
            if (Request == null || Request.Files == null || Request.Files.Count == 0) { return otrosAdjuntosGuardados; }

            var basePath = Server.MapPath(CARPETA_VIRTUAL_ADJUNTOS_INFORME);
            if (!Directory.Exists(basePath)) { Directory.CreateDirectory(basePath); }

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx" };

            for (int i = 0; i < Request.Files.Count; i++)
            {
                var file = Request.Files[i];
                if (file == null || file.ContentLength <= 0) { continue; }
                var key = Request.Files.GetKey(i);
                if (string.IsNullOrWhiteSpace(key)) { continue; }

                var esAdjuntoBase = key.StartsWith("archivoAdjunto_", StringComparison.OrdinalIgnoreCase);
                var esAdjuntoLibre = key.StartsWith("otrosAdjuntosArchivo", StringComparison.OrdinalIgnoreCase);
                if (!esAdjuntoBase && !esAdjuntoLibre) { continue; }

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrWhiteSpace(ext) || !allowedExtensions.Contains(ext)) { continue; }

                var safeFileName = string.Format("adj_{0}_{1}_{2}_{3}{4}",
                    codigoInspeccion, key, DateTime.Now.ToString("yyyyMMddHHmmssfff"), i, ext);
                var fullPath = Path.Combine(basePath, safeFileName);
                file.SaveAs(fullPath);

                var nombreVisible = LimpiarNombreArchivoVisible(file.FileName);

                if (esAdjuntoBase && adjuntosBaseGuardados != null)
                {
                    var rawIndex = key.Substring("archivoAdjunto_".Length);
                    int indiceAdjunto;
                    if (int.TryParse(rawIndex, out indiceAdjunto) && indiceAdjunto >= 0 && indiceAdjunto < documentosAdjuntosBase.Count && !string.IsNullOrWhiteSpace(nombreVisible))
                    {
                        var etiquetaAdjunto = documentosAdjuntosBase[indiceAdjunto];
                        List<string> archivosAdjunto;
                        if (!adjuntosBaseGuardados.TryGetValue(etiquetaAdjunto, out archivosAdjunto) || archivosAdjunto == null)
                        {
                            archivosAdjunto = new List<string>();
                            adjuntosBaseGuardados[etiquetaAdjunto] = archivosAdjunto;
                        }

                        if (!archivosAdjunto.Any(x => string.Equals(x, nombreVisible, StringComparison.OrdinalIgnoreCase)))
                        {
                            archivosAdjunto.Add(nombreVisible);
                        }
                    }
                }

                if (esAdjuntoLibre)
                {
                    if (!string.IsNullOrWhiteSpace(nombreVisible))
                    {
                        otrosAdjuntosGuardados.Add(nombreVisible);
                    }
                }
            }

            return otrosAdjuntosGuardados;
        }

        private string GuardarInformeTecnicoFirmadoPdf(int codigoInspeccion, int version, string sufijo, byte[] pdfBytes)
        {
            var basePath = Server.MapPath(CARPETA_VIRTUAL_INFORMES_TECNICOS_FIRMADOS);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var fileName = string.Format(
                "InformeTecnico_{0}_v{1}_{2}_{3}.pdf",
                codigoInspeccion,
                version,
                (sufijo ?? "firmado").Trim().ToLowerInvariant(),
                DateTime.Now.ToString("yyyyMMddHHmmss"));
            var fullPath = Path.Combine(basePath, fileName);
            System.IO.File.WriteAllBytes(fullPath, pdfBytes ?? new byte[0]);
            return CARPETA_VIRTUAL_INFORMES_TECNICOS_FIRMADOS.TrimStart('~') + "/" + fileName;
        }

        private bool EnviarInformeTecnicoAlSolicitante(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, byte[] pdfBytes)
        {
            try
            {
                if (solicitud == null)
                {
                    return false;
                }

                var correo = !string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico)
                    ? solicitud.CorreoRepresentanteTecnico.Trim()
                    : (solicitud.Email ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(correo))
                {
                    return false;
                }

                var asunto = "DGAC AOCR - Informe técnico de inspección " + inspeccion.CodigoInspeccion;
                var enlace = ConstruirUrlDetalle(inspeccion.CodigoInspeccion);
                var model = new EmailTemplateModel
                {
                    Titulo = "Informe tecnico de inspeccion",
                    MensajePrincipal = "Se ha finalizado el informe tecnico de su inspeccion AOCR.",
                    Resumen = new System.Collections.Generic.List<EmailFieldItem>
                    {
                        new EmailFieldItem("Inspeccion", inspeccion.CodigoInspeccion.ToString()),
                        new EmailFieldItem("Resultado", informe != null ? informe.Resultado : inspeccion.Resultado)
                    },
                    EnlaceUrl = enlace,
                    EnlaceTexto = "Abrir detalle de inspeccion",
                    TextoCierre = "Se adjunta el informe tecnico en formato PDF.",
                    Footer = "Este es un mensaje automatico del workflow de inspeccion AOCR."
                };
                var cuerpo = EmailTemplateRenderer.Render(model);

                var servicioCorreo = new EnviarCorreo();
                var nombreAdjunto = string.Format("InformeTecnico_Inspeccion_{0}.pdf", inspeccion.CodigoInspeccion);
                return servicioCorreo.enviaMensajeCorreoConAdjunto(correo, asunto, cuerpo, pdfBytes, nombreAdjunto, "application/pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error enviando informe técnico al solicitante: " + ex);
                return false;
            }
        }

        private ActionResult FirmarInformePorRol(int id, string passwordCertificado, string nombreCampoArchivo, string rolFirma, string estadoFinal, bool autoEnviarADirdac)
        {
            if (id <= 0)
            {
                return new HttpStatusCodeResult(400, "ID inválido.");
            }

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
            {
                return HttpNotFound("Inspección no encontrada.");
            }

            if (!PuedeAccederInspeccion(inspeccion) && !User.IsInRole(ROL_DIRECCION) && !User.IsInRole(ROL_DIRECTOR))
            {
                return new HttpStatusCodeResult(403, "No autorizado para firmar el informe técnico.");
            }

            var usuarioIdOperacion = ObtenerCodigoUsuario();
            var solicitudInforme = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            NormalizarDatosOperadorSolicitud(solicitudInforme);
            var usaFlujoLvEae = UsaFlujoListaVerificacionOperacionalEae(solicitudInforme);
            if (usaFlujoLvEae && string.Equals(rolFirma, "INSPECTOR", StringComparison.OrdinalIgnoreCase))
            {
                estadoFinal = "INFORME_FIRMADO_TECNICO";
                autoEnviarADirdac = false;
            }

            ListaVerificacionOperacionalEae listaVerificacion;
            string mensajeLista;
            if (!ValidarPrecondicionListaVerificacionOperacionalEae(inspeccion, solicitudInforme, out listaVerificacion, out mensajeLista))
            {
                TempData["Error"] = mensajeLista;
                return RedirectToAction("Detalle", new { id });
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            informe = AsegurarInformeTecnicoFirmable(inspeccion, informe, usuarioIdOperacion);
            if (informe == null || !informe.Finalizado)
            {
                TempData["Error"] = "Debe generar el PDF del informe técnico antes de firmarlo.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.Equals(rolFirma, "INSPECTOR", StringComparison.OrdinalIgnoreCase) && informe.FirmadoInspector)
            {
                TempData["Error"] = "El informe técnico ya fue firmado por el inspector.";
                return RedirectToAction("Detalle", new { id });
            }

            if (!usaFlujoLvEae
                && string.Equals(rolFirma, "INSPECTOR", StringComparison.OrdinalIgnoreCase)
                && !string.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "APROBADO_COORDINADOR", StringComparison.OrdinalIgnoreCase)
                && !string.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "APROBADO_DIRECCION", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La firma del inspector se habilita cuando DIRDAC revisa y Coordinación aprueba el informe.";
                return RedirectToAction("Detalle", new { id });
            }

            if (string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                if (usaFlujoLvEae)
                {
                    TempData["Error"] = "El flujo EAE no contempla firma DIRDAC en el informe técnico.";
                    return RedirectToAction("Detalle", new { id });
                }

                if (!informe.FirmadoInspector)
                {
                    TempData["Error"] = "El informe debe contar primero con la firma del inspector.";
                    return RedirectToAction("Detalle", new { id });
                }

                if (informe.FirmadoDirdac)
                {
                    TempData["Error"] = "El informe técnico ya cuenta con la firma final de DIRDAC.";
                    return RedirectToAction("Detalle", new { id });
                }

                if (!string.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "ENVIADO_A_DIRDAC", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "El informe aún no ha sido enviado formalmente a DIRDAC para firma.";
                    return RedirectToAction("Detalle", new { id });
                }
            }

            var certificadoArchivo = Request.Files[nombreCampoArchivo];
            string mensajeValidacion;
            if (!EsCertificadoDigitalValido(certificadoArchivo, out mensajeValidacion))
            {
                TempData["Error"] = mensajeValidacion;
                return RedirectToAction("Detalle", new { id });
            }

            // --- VERSIONADO: determinar el PDF fuente ---
            // Inspector: regenera un PDF limpio desde cero.
            // DIRDAC: firma sobre el PDF ya firmado por el inspector (que fue limpio).
            byte[] pdfFuente;
            string origenPdf;
            if (string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                var rutaFirmadaInspector = FirstNonEmpty(informe.RutaDocumentoFirmado, informe.RutaPdf);
                var pathFirmado = ResolverRutaAbsolutaInforme(rutaFirmadaInspector);
                if (string.IsNullOrWhiteSpace(pathFirmado) || !System.IO.File.Exists(pathFirmado))
                {
                    TempData["Error"] = "No se encontró el PDF firmado por el inspector. Regenere el informe.";
                    return RedirectToAction("Detalle", new { id });
                }
                pdfFuente = System.IO.File.ReadAllBytes(pathFirmado);
                origenPdf = "FIRMADO_INSPECTOR:" + rutaFirmadaInspector;
            }
            else
            {
                pdfFuente = GenerarPdfInformeTecnico(inspeccion, solicitudInforme, informe);
                origenPdf = "REGENERADO_LIMPIO";
            }

            byte[] pdfFirmado;
            string hashDocumento;
            byte[] certificadoBytes;
            using (var ms = new MemoryStream())
            {
                certificadoArchivo.InputStream.CopyTo(ms);
                certificadoBytes = ms.ToArray();
            }

            // Leer nombre del titular del certificado digital
            var infoCertificado = _firmaDigitalService.LeerCertificado(certificadoBytes, passwordCertificado);
            if (!infoCertificado.Exitoso)
            {
                TempData["Error"] = infoCertificado.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }
            var nombreFirmanteCertificado = !string.IsNullOrWhiteSpace(infoCertificado.NombreTitular)
                ? infoCertificado.NombreTitular
                : ObtenerUsuarioActual();

            _logger.LogInfo("[GestionInspeccion] Inicio firma digital. InspeccionId=" + id
                + ", RolFirma=" + rolFirma
                + ", InformeId=" + informe.CodigoInforme
                + ", VersionInforme=" + informe.Version
                + ", OrigenPdf=" + origenPdf
                + ", PdfBytes=" + pdfFuente.Length
                + ", CertificadoBytes=" + certificadoBytes.Length
                + ", NombreCertificado=" + nombreFirmanteCertificado
                + ", Usuario=" + ObtenerUsuarioActual());

            var resultadoFirma = _firmaDigitalService.FirmarPdf(
                pdfFuente,
                certificadoBytes,
                passwordCertificado,
                nombreFirmanteCertificado,
                string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase)
                    ? "Firma institucional DIRDAC del informe técnico AOCR"
                    : "Firma del inspector sobre el informe técnico AOCR",
                "Sistema AOCR DGAC",
                string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase)
                    ? "INFORME_TECNICO_DIRDAC"
                    : "INFORME_TECNICO_INSPECTOR",
                null,
                null);

            if (!resultadoFirma.Exitoso)
            {
                _logger.LogError("[GestionInspeccion] Firma digital fallida. InspeccionId=" + id
                    + ", RolFirma=" + rolFirma
                    + ", InformeId=" + informe.CodigoInforme
                    + ", Mensaje=" + resultadoFirma.Mensaje);
                TempData["Error"] = resultadoFirma.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            pdfFirmado = resultadoFirma.PdfFirmado;
            hashDocumento = resultadoFirma.HashSha256;
            _logger.LogInfo("[GestionInspeccion] Firma digital generada. InspeccionId=" + id
                + ", RolFirma=" + rolFirma
                + ", InformeId=" + informe.CodigoInforme
                + ", Hash=" + hashDocumento
                + ", PdfFirmadoBytes=" + (pdfFirmado != null ? pdfFirmado.Length : 0));

            var rutaFirmada = GuardarInformeTecnicoFirmadoPdf(id, informe.Version, rolFirma, pdfFirmado);
            var usuarioId = usuarioIdOperacion;
            var usuarioActual = ObtenerUsuarioActual();
            var estadoAnterior = FirstNonEmpty(informe.EstadoInforme, informe.Finalizado ? "GENERADO" : "BORRADOR", "BORRADOR");

            _logger.LogInfo("[GestionInspeccion] Persistiendo firma digital. InspeccionId=" + id
                + ", RolFirma=" + rolFirma
                + ", InformeId=" + informe.CodigoInforme
                + ", RutaFirmada=" + rutaFirmada
                + ", EstadoAnterior=" + estadoAnterior
                + ", EstadoFinal=" + estadoFinal
                + ", UsuarioId=" + usuarioId);

            if (string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                _informeDAO.RegistrarFirmaDirdac(informe.CodigoInforme, rutaFirmada, hashDocumento, DateTime.Now, nombreFirmanteCertificado, estadoFinal, usuarioId);
            }
            else
            {
                _informeDAO.RegistrarFirmaInspector(informe.CodigoInforme, rutaFirmada, hashDocumento, DateTime.Now, nombreFirmanteCertificado, estadoFinal, usuarioId);
            }

            _inspeccionBL.GuardarInforme(id, rutaFirmada, usuarioId);

            _logger.LogInfo("[GestionInspeccion] Firma digital persistida. InspeccionId=" + id
                + ", RolFirma=" + rolFirma
                + ", InformeId=" + informe.CodigoInforme
                + ", RutaFirmada=" + rutaFirmada);
            RegistrarAuditoriaInformeDigital(
                id,
                estadoAnterior,
                estadoFinal,
                rutaFirmada,
                hashDocumento,
                string.Format("Firma digital aplicada por {0} ({1}). Rol={2}. IP={3}", nombreFirmanteCertificado, usuarioActual, rolFirma, ObtenerIpCliente()),
                usuarioId,
                usuarioActual,
                "FIRMA_DIGITAL_" + rolFirma);

            if (autoEnviarADirdac)
            {
                var informeActualizado = _informeDAO.ObtenerPorId(informe.CodigoInforme);
                var resultadoEnvio = EnviarInformeADirdacInterno(inspeccion, solicitudInforme, informeActualizado, usuarioId);
                TempData[resultadoEnvio.Exitoso ? "Success" : "Warning"] = resultadoEnvio.Exitoso
                    ? "Informe firmado por inspector y enviado a Dirección / Jefatura para revisión institucional."
                    : "Informe firmado por inspector. " + resultadoEnvio.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            var solicitudFinal = solicitudInforme;
            var informeFinal = _informeDAO.ObtenerPorId(informe.CodigoInforme);
            var resultadoNotificacion = _inspeccionCorreoService.NotificarInformeTecnicoFirmadoFinal(
                inspeccion,
                solicitudFinal,
                informeFinal,
                pdfFirmado,
                ConstruirUrlDetalle(id),
                ConstruirDetalleCorreoFirmaFinal(inspeccion, solicitudFinal, informeFinal));

            TempData[resultadoNotificacion.Exitoso ? "Success" : "Warning"] = resultadoNotificacion.Exitoso
                ? "Informe técnico firmado por inspector. Se habilitó la siguiente etapa para activar y generar el certificado AOCR."
                : "Informe técnico firmado por inspector. No fue posible enviar todas las notificaciones finales.";

            SincronizarSolicitudAocrTrasFirmaFinal(inspeccion, solicitudFinal, usuarioId, usuarioActual);

            return RedirectToAction("Detalle", new { id });
        }

        private void SincronizarSolicitudAocrTrasFirmaFinal(Inspeccion inspeccion, SolicitudAOCR solicitud, int usuarioId, string usuarioActual)
        {
            if (inspeccion == null || solicitud == null || usuarioId <= 0)
            {
                return;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            if (!string.Equals(estadoActual, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string mensajeCambio;
            var observacion = "Revision institucional final del informe tecnico completada; documentos AOCR habilitados para validacion.";
            var actualizado = _solicitudEstadoTransitionBL.CambiarEstadoConReglasAocr(
                solicitud.CodigoSolicitud,
                EstadoSolicitud.AOCR_EnElaboracion,
                observacion,
                usuarioId,
                destino => string.Equals(destino, EstadoSolicitud.AOCR_EnElaboracion, StringComparison.OrdinalIgnoreCase),
                out mensajeCambio);

            if (!actualizado)
            {
                _logger.LogWarning("[GestionInspeccion] No se pudo sincronizar solicitud AOCR tras revision final. SolicitudId=" + solicitud.CodigoSolicitud + ", InspeccionId=" + inspeccion.CodigoInspeccion + ", Mensaje=" + mensajeCambio);
                return;
            }

            _logger.LogInfo("[GestionInspeccion] Solicitud AOCR sincronizada tras revision final. SolicitudId=" + solicitud.CodigoSolicitud + ", EstadoNuevo=" + EstadoSolicitud.AOCR_EnElaboracion + ", Usuario=" + usuarioActual);
        }

        private ResultadoOperacion EnviarInformeADirdacInterno(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, int usuarioId)
        {
            if (inspeccion == null || solicitud == null || informe == null)
            {
                return ResultadoOperacion.Error("No existe contexto suficiente para enviar el informe técnico a Dirección / Jefatura.");
            }

            if (!informe.Finalizado)
            {
                return ResultadoOperacion.Error("El informe técnico aún no ha sido finalizado en PDF.");
            }

            if (informe.FirmadoDirdac)
            {
                return ResultadoOperacion.Error("El informe técnico ya cuenta con aprobación final de Dirección / Jefatura.");
            }

            var yaEnviadoADirdac = InformeEstaEnviadoADirdac(informe);
            if (yaEnviadoADirdac && informe.CorreoEnviado)
            {
                return ResultadoOperacion.Error("El documento ya fue notificado a Dirección / Jefatura y está pendiente de revisión final.");
            }

            if (!yaEnviadoADirdac)
            {
                var estadoAnterior = FirstNonEmpty(informe.EstadoInforme, "GENERADO");
                _informeDAO.MarcarEnviadoADirdac(informe.CodigoInforme, DateTime.Now, ObtenerUsuarioActual(), false, "ENVIADO_A_DIRDAC", usuarioId);
                RegistrarAuditoriaInformeDigital(
                    inspeccion.CodigoInspeccion,
                    estadoAnterior,
                    "ENVIADO_A_DIRDAC",
                    FirstNonEmpty(informe.RutaDocumentoFirmado, informe.RutaPdf, inspeccion.RutaInforme),
                    informe.HashDocumento,
                    "Documento transferido automáticamente a la bandeja de revisión de DIRDAC / Dirección - Jefatura. IP=" + ObtenerIpCliente(),
                    usuarioId,
                    ObtenerUsuarioActual(),
                    "ENVIO_DIRDAC");

                informe = _informeDAO.ObtenerPorId(informe.CodigoInforme) ?? informe;
            }

            var detalle = ConstruirDetalleCorreoPendienteDirDac(inspeccion, solicitud, informe);
            var resultadoCorreo = _inspeccionCorreoService.NotificarEvento(inspeccion, solicitud, "PENDIENTE_FIRMA_DIRDAC", detalle);
            _informeDAO.ActualizarCorreoEnviado(informe.CodigoInforme, resultadoCorreo.Exitoso, usuarioId);

            var notificacionInternaOk = !yaEnviadoADirdac && NotificarInternamentePendienteDirdac(inspeccion, solicitud);

            if (yaEnviadoADirdac)
            {
                RegistrarAuditoriaInformeDigital(
                    inspeccion.CodigoInspeccion,
                    "ENVIADO_A_DIRDAC",
                    "ENVIADO_A_DIRDAC",
                    FirstNonEmpty(informe.RutaDocumentoFirmado, informe.RutaPdf, inspeccion.RutaInforme),
                    informe.HashDocumento,
                    "Reintento de notificación formal a Dirección / Jefatura. ResultadoCorreo=" + (resultadoCorreo.Exitoso ? "OK" : "ERROR") + ". IP=" + ObtenerIpCliente(),
                    usuarioId,
                    ObtenerUsuarioActual(),
                    "REENVIO_NOTIFICACION_DIRDAC");
            }

            if (resultadoCorreo.Exitoso)
            {
                return ResultadoOperacion.Ok(null,
                    yaEnviadoADirdac
                        ? "La notificacion formal a DIRDAC / Dirección - Jefatura se reenvio correctamente. El documento continua pendiente de revision."
                        : "Documento enviado a DIRDAC / Dirección - Jefatura para revisión.");
            }

            return ResultadoOperacion.Error(
                yaEnviadoADirdac
                    ? "El documento ya está en la bandeja de DIRDAC / Dirección - Jefatura, pero continúa pendiente el correo formal. Puede reintentar la notificación más tarde."
                    : (notificacionInternaOk
                        ? "El documento pasó a la bandeja de DIRDAC / Dirección - Jefatura, pero falló el correo formal. La notificación interna ya fue registrada."
                        : "El documento pasó a la bandeja de DIRDAC / Dirección - Jefatura, pero no fue posible enviar la notificación formal."));
        }

        private bool InformeEstaEnviadoADirdac(InspeccionInformeTecnico informe)
        {
            return informe != null
                && string.Equals((informe.EstadoInforme ?? string.Empty).Trim(), "ENVIADO_A_DIRDAC", StringComparison.OrdinalIgnoreCase);
        }

        private bool NotificarInternamentePendienteDirdac(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var usuariosDireccion = new[]
                {
                    ROL_DIRECCION,
                    "DirectorGeneral",
                    ROL_JEFATURA,
                    ROL_ADMIN
                }
                .SelectMany(rol => UsuarioDAO.ListarPorRol(rol) ?? new List<Usuario>())
                .Where(usuario => usuario != null && usuario.Id > 0)
                .GroupBy(usuario => usuario.Id)
                .Select(grupo => grupo.First())
                .ToList();

            if (!usuariosDireccion.Any())
            {
                return false;
            }

            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            var compania = FirstNonEmpty(solicitud != null ? solicitud.RazonSocial : null, solicitud != null ? solicitud.NombreOperador : null, "No disponible");
            var titulo = "Informe técnico pendiente de revisión Dirección / Jefatura";
            var mensaje = string.Format(
                "La inspección #{0} de la solicitud {1} ({2}) ya fue firmada por el inspector y quedó disponible para revisión institucional de Dirección / Jefatura.",
                inspeccion.CodigoInspeccion,
                numeroSolicitud,
                compania);
            var url = "/Inspeccion/Detalle/" + inspeccion.CodigoInspeccion;

            var enviado = false;
            foreach (var usuario in usuariosDireccion)
            {
                try
                {
                    enviado = NotificacionBL.EnviarNotificacion(
                        usuario.Id,
                        titulo,
                        mensaje,
                        "INFO",
                        url,
                        "Inspeccion",
                        inspeccion.CodigoInspeccion,
                        "aocr_tbinspeccion") || enviado;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[GestionInspeccion] Error enviando notificación interna DIRDAC. InspeccionId=" + inspeccion.CodigoInspeccion + ", UsuarioId=" + usuario.Id + ", Error=" + ex.Message);
                }
            }

            return enviado;
        }

        private bool EsCertificadoDigitalValido(HttpPostedFileBase archivo, out string mensaje)
        {
            mensaje = string.Empty;
            if (archivo == null || archivo.ContentLength <= 0)
            {
                mensaje = "Debe cargar un certificado digital en formato .p12 o .pfx.";
                return false;
            }

            var extension = Path.GetExtension(archivo.FileName ?? string.Empty);
            if (!string.Equals(extension, ".p12", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".pfx", StringComparison.OrdinalIgnoreCase))
            {
                mensaje = "Solo se admiten certificados digitales .p12 o .pfx.";
                return false;
            }

            if (archivo.ContentLength > 5 * 1024 * 1024)
            {
                mensaje = "El certificado digital supera el tamaño máximo permitido.";
                return false;
            }

            return true;
        }

        private void AplicarPosicionesFirmaInformeTecnicoDetalle(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe)
        {
            if (inspeccion == null)
            {
                return;
            }

            var codigoInspeccion = inspeccion.CodigoInspeccion;
            var paginaDefault = ObtenerPaginaFinalInformeTecnico(inspeccion, solicitud, informe);
            var posicionInspector = _firmaPosicionDocumentoDAO.Obtener(codigoInspeccion, "INFORME_TECNICO", "INFORME_TECNICO_INSPECTOR");
            var posicionDirdac = _firmaPosicionDocumentoDAO.Obtener(codigoInspeccion, "INFORME_TECNICO", "INFORME_TECNICO_DIRDAC");

            AsignarViewBagPosicionFirma("Inspector", posicionInspector, paginaDefault, 0.655462m, 0.209026m, 0.248739m, 0.106888m);
            AsignarViewBagPosicionFirma("Dirdac", posicionDirdac, paginaDefault, 0.655462m, 0.209026m, 0.248739m, 0.106888m);
        }

        private int ObtenerPaginaFinalInformeTecnico(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe)
        {
            if (inspeccion == null || informe == null)
            {
                return 1;
            }

            try
            {
                var pdfBytes = GenerarPdfInformeTecnico(inspeccion, solicitud, informe);
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return 1;
                }

                using (var reader = new PdfReader(pdfBytes))
                {
                    return reader.NumberOfPages > 0 ? reader.NumberOfPages : 1;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] No se pudo calcular la página final del informe técnico para la firma. InspeccionId=" + inspeccion.CodigoInspeccion + ", Error=" + ex.Message);
                return 1;
            }
        }

        private void AsignarViewBagPosicionFirma(string prefijo, AocrFirmaPosicionDocumento posicion, int paginaDefault, decimal xDefault, decimal yDefault, decimal anchoDefault, decimal altoDefault)
        {
            var paginaReferencia = paginaDefault > 0 ? paginaDefault : 1;
            var paginaPosicion = posicion != null && posicion.NumeroPagina > 0
                ? Math.Min(posicion.NumeroPagina, paginaReferencia)
                : paginaReferencia;

            ViewData[prefijo + "FirmaUsaPosicionPersonalizada"] = posicion != null ? "true" : "false";
            ViewData[prefijo + "FirmaNumeroPagina"] = paginaPosicion;
            ViewData[prefijo + "FirmaPosicionX"] = FormatearDecimalInvariante(posicion != null ? posicion.PosicionXRatio : xDefault);
            ViewData[prefijo + "FirmaPosicionY"] = FormatearDecimalInvariante(posicion != null ? posicion.PosicionYRatio : yDefault);
            ViewData[prefijo + "FirmaAncho"] = FormatearDecimalInvariante(posicion != null ? posicion.AnchoRatio : anchoDefault);
            ViewData[prefijo + "FirmaAlto"] = FormatearDecimalInvariante(posicion != null ? posicion.AltoRatio : altoDefault);
        }

        private bool PuedeFirmarInformePorRol(Inspeccion inspeccion, string rolFirmaVisual)
        {
            var rolNormalizado = ObtenerClaveFirmaVisualInforme(rolFirmaVisual);
            if (string.Equals(rolNormalizado, "INFORME_TECNICO_INSPECTOR", StringComparison.OrdinalIgnoreCase))
            {
                return PuedeAccederInspeccion(inspeccion) && (User.IsInRole(ROL_INSPECTOR) || EsAdmin());
            }

            if (string.Equals(rolNormalizado, "INFORME_TECNICO_DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
                return User.IsInRole(ROL_DIRDAC) || User.IsInRole(ROL_DIRECCION) || User.IsInRole(ROL_DIRECTOR) || User.IsInRole(ROL_JEFATURA) || User.IsInRole(ROL_JEFE) || EsAdmin();
            }

            return false;
        }

        private static string ObtenerClaveFirmaVisualInforme(string rolFirmaVisual)
        {
            var rol = (rolFirmaVisual ?? string.Empty).Trim().ToUpperInvariant();
            if (rol == "DIRDAC" || rol == "INFORME_TECNICO_DIRDAC")
            {
                return "INFORME_TECNICO_DIRDAC";
            }

            return "INFORME_TECNICO_INSPECTOR";
        }

        private PosicionFirmaVisualPdf ObtenerPosicionFirmaInformeDesdeRequest(int codigoInspeccion, string rolFirmaVisual)
        {
            var sufijo = string.Equals(rolFirmaVisual, "INFORME_TECNICO_DIRDAC", StringComparison.OrdinalIgnoreCase)
                ? "Dirdac"
                : "Inspector";
            var usaPersonalizada = (Request.Form["UsaPosicionFirmaPersonalizada" + sufijo] ?? string.Empty).Trim();
            if (!string.Equals(usaPersonalizada, "true", StringComparison.OrdinalIgnoreCase))
            {
                var posicionGuardada = _firmaPosicionDocumentoDAO.Obtener(codigoInspeccion, "INFORME_TECNICO", rolFirmaVisual);
                return ConvertirPosicionFirmaVisual(posicionGuardada);
            }

            var posicion = ConstruirPosicionFirmaVisualDesdeValores(
                ParseIntSeguro(Request.Form["NumeroPaginaFirma" + sufijo], 2),
                Request.Form["PosicionFirmaX" + sufijo],
                Request.Form["PosicionFirmaY" + sufijo],
                Request.Form["AnchoFirma" + sufijo],
                Request.Form["AltoFirma" + sufijo]);

            if (posicion != null && posicion.EsValida)
            {
                return posicion;
            }

            var fallback = _firmaPosicionDocumentoDAO.Obtener(codigoInspeccion, "INFORME_TECNICO", rolFirmaVisual);
            return ConvertirPosicionFirmaVisual(fallback);
        }

        private void GuardarPosicionFirmaInformeTecnico(Inspeccion inspeccion, string rolFirmaVisual, PosicionFirmaVisualPdf posicion)
        {
            if (inspeccion == null || posicion == null || !posicion.EsValida)
            {
                return;
            }

            _firmaPosicionDocumentoDAO.Guardar(new AocrFirmaPosicionDocumento
            {
                CodigoSolicitud = inspeccion.CodigoInspeccion,
                CodigoInspeccion = inspeccion.CodigoInspeccion,
                TipoDocumento = "INFORME_TECNICO",
                RolFirmante = ObtenerClaveFirmaVisualInforme(rolFirmaVisual),
                OrigenPosicion = "PUNTERO",
                NumeroPagina = posicion.NumeroPagina,
                PosicionXRatio = (decimal)posicion.PosicionXRatio,
                PosicionYRatio = (decimal)posicion.PosicionYRatio,
                AnchoRatio = (decimal)posicion.AnchoRatio,
                AltoRatio = (decimal)posicion.AltoRatio,
                CodigoUsuario = ObtenerCodigoUsuario() > 0 ? (int?)ObtenerCodigoUsuario() : null,
                UsuarioNombre = ObtenerUsuarioActual()
            });
        }

        private static PosicionFirmaVisualPdf ConvertirPosicionFirmaVisual(AocrFirmaPosicionDocumento posicion)
        {
            if (posicion == null)
            {
                return null;
            }

            return new PosicionFirmaVisualPdf
            {
                NumeroPagina = posicion.NumeroPagina > 0 ? posicion.NumeroPagina : 2,
                PosicionXRatio = (float)posicion.PosicionXRatio,
                PosicionYRatio = (float)posicion.PosicionYRatio,
                AnchoRatio = (float)posicion.AnchoRatio,
                AltoRatio = (float)posicion.AltoRatio
            };
        }

        private static PosicionFirmaVisualPdf ConstruirPosicionFirmaVisualDesdeValores(int numeroPagina, string posicionX, string posicionY, string ancho, string alto)
        {
            decimal x;
            decimal y;
            decimal width;
            decimal height;
            if (!TryParseDecimalInvariant(posicionX, out x)
                || !TryParseDecimalInvariant(posicionY, out y)
                || !TryParseDecimalInvariant(ancho, out width)
                || !TryParseDecimalInvariant(alto, out height))
            {
                return null;
            }

            return new PosicionFirmaVisualPdf
            {
                NumeroPagina = numeroPagina > 0 ? numeroPagina : 2,
                PosicionXRatio = (float)x,
                PosicionYRatio = (float)y,
                AnchoRatio = (float)width,
                AltoRatio = (float)height
            };
        }

        private static bool TryParseDecimalInvariant(string value, out decimal result)
        {
            return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static int ParseIntSeguro(string value, int defaultValue)
        {
            int result;
            return int.TryParse(value, out result) ? result : defaultValue;
        }

        private static string FormatearDecimalInvariante(decimal value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private InspeccionInformeTecnico AsegurarInformeTecnicoFirmable(Inspeccion inspeccion, InspeccionInformeTecnico informeActual, int usuarioId)
        {
            if (inspeccion == null || inspeccion.CodigoInspeccion <= 0)
            {
                return informeActual;
            }

            var informe = informeActual ?? _informeDAO.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion);
            var rutaPdfDisponible = FirstNonEmpty(informe != null ? informe.RutaPdf : null, inspeccion.RutaInforme);

            if (informe == null)
            {
                if (string.IsNullOrWhiteSpace(rutaPdfDisponible))
                {
                    return null;
                }

                var informeCreado = _informeDAO.GuardarBorrador(new InspeccionInformeTecnico
                {
                    CodigoInspeccion = inspeccion.CodigoInspeccion,
                    Titulo = "INFORME TÉCNICO AOCR",
                    Resumen = "Registro técnico normalizado para habilitar el flujo de firma digital."
                }, usuarioId);

                _informeDAO.MarcarFinalizado(
                    informeCreado.CodigoInforme,
                    rutaPdfDisponible,
                    false,
                    "GENERADO",
                    usuarioId);

                _logger.LogInfo("[GestionInspeccion] Informe técnico creado desde ruta existente para habilitar firma. InspeccionId="
                    + inspeccion.CodigoInspeccion
                    + ", InformeId=" + informeCreado.CodigoInforme
                    + ", RutaPdf=" + rutaPdfDisponible
                    + ", UsuarioId=" + usuarioId);

                return _informeDAO.ObtenerPorId(informeCreado.CodigoInforme) ?? informeCreado;
            }

            var requiereNormalizacion = (!informe.Finalizado || string.IsNullOrWhiteSpace(informe.RutaPdf))
                && !string.IsNullOrWhiteSpace(rutaPdfDisponible);

            if (!requiereNormalizacion)
            {
                return informe;
            }

            var estado = FirstNonEmpty(informe.EstadoInforme, "GENERADO");
            _informeDAO.MarcarFinalizado(
                informe.CodigoInforme,
                rutaPdfDisponible,
                informe.CorreoEnviado,
                estado,
                usuarioId);

            _logger.LogInfo("[GestionInspeccion] Informe técnico normalizado para firma. InspeccionId="
                + inspeccion.CodigoInspeccion
                + ", InformeId=" + informe.CodigoInforme
                + ", Estado=" + estado
                + ", RutaPdf=" + rutaPdfDisponible
                + ", UsuarioId=" + usuarioId);

            return _informeDAO.ObtenerPorId(informe.CodigoInforme) ?? informe;
        }

        private string ResolverRutaAbsolutaInforme(string rutaRelativa)
        {
            var rutaNormalizada = NormalizarRutaRelativaInforme(rutaRelativa);
            if (string.IsNullOrWhiteSpace(rutaNormalizada))
            {
                return null;
            }

            return Server.MapPath("~" + rutaNormalizada);
        }

        private string ResolverRutaRelativaInformeDisponible(params string[] rutasRelativas)
        {
            var rutasCandidatas = (rutasRelativas ?? new string[0])
                .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
                .Select(NormalizarRutaRelativaInforme)
                .Where(ruta => !string.IsNullOrWhiteSpace(ruta))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (rutasCandidatas.Count == 0)
            {
                return null;
            }

            var baseDir = Server.MapPath(CARPETA_VIRTUAL_INFORMES);
            foreach (var rutaCandidata in rutasCandidatas)
            {
                var fullPath = Server.MapPath("~" + rutaCandidata);
                if (!EsRutaDentroDeBase(fullPath, baseDir))
                {
                    continue;
                }

                if (System.IO.File.Exists(fullPath))
                {
                    return rutaCandidata;
                }
            }

            return null;
        }

        private static string NormalizarRutaRelativaInforme(string rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                return null;
            }

            var ruta = rutaRelativa.Trim();
            if (ruta.StartsWith("~"))
            {
                ruta = ruta.Substring(1);
            }

            if (!ruta.StartsWith("/"))
            {
                ruta = "/" + ruta;
            }

            return ruta;
        }

        private static bool EsAccionFinalizarListaVerificacionOperacionalEae(string submitActionRaw, string finalizarRaw)
        {
            if (string.Equals((submitActionRaw ?? string.Empty).Trim(), "finalizar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ContieneValorVerdadero(finalizarRaw);
        }

        private static bool ContieneValorVerdadero(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var valores = rawValue
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(valor => valor.Trim());

            foreach (var valor in valores)
            {
                if (string.Equals(valor, "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(valor, "1", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(valor, "on", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(valor, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void RegistrarAuditoriaInformeDigital(int codigoInspeccion, string estadoAnterior, string estadoNuevo, string rutaDocumento, string hashDocumento, string detalle, int usuarioId, string usuarioNombre, string origen)
        {
            try
            {
                var observacion = string.Format(
                    "Documento={0}; Hash={1}; {2}",
                    string.IsNullOrWhiteSpace(rutaDocumento) ? "N/D" : rutaDocumento,
                    string.IsNullOrWhiteSpace(hashDocumento) ? "N/D" : hashDocumento,
                    string.IsNullOrWhiteSpace(detalle) ? string.Empty : detalle);

                _historialDAO.Registrar(
                    codigoInspeccion,
                    estadoAnterior,
                    estadoNuevo,
                    usuarioId,
                    usuarioNombre,
                    observacion,
                    origen);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error registrando auditoría de firma digital. InspeccionId=" + codigoInspeccion + ", Error=" + ex.Message);
            }
        }

        private string ConstruirDetalleCorreoPendienteDirDac(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe)
        {
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            var compania = FirstNonEmpty(solicitud != null ? solicitud.RazonSocial : null, solicitud != null ? solicitud.NombreOperador : null, "No disponible");
            return string.Format(
                "Solicitud: {0}. Compañía: {1}. Estado informe: {2}. Siguiente paso: Revisión institucional DIRDAC / Dirección - Jefatura, aprobación de Coordinación, firma final del inspector y posterior generación del certificado AOCR. Enlace: {3}",
                numeroSolicitud,
                compania,
                FirstNonEmpty(informe != null ? informe.EstadoInforme : null, "ENVIADO_A_DIRDAC"),
                ConstruirUrlDetalle(inspeccion.CodigoInspeccion));
        }

        private string ConstruirDetalleCorreoFirmaFinal(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe)
        {
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            var compania = FirstNonEmpty(solicitud != null ? solicitud.RazonSocial : null, solicitud != null ? solicitud.NombreOperador : null, "No disponible");
            return string.Format(
                "Solicitud: {0}. Compañía: {1}. Fecha revisión final: {2:dd/MM/yyyy HH:mm}. Revisado por: {3}. Hash documento: {4}. Enlace: {5}",
                numeroSolicitud,
                compania,
                informe != null && informe.FechaFirma2.HasValue ? informe.FechaFirma2.Value : DateTime.Now,
                FirstNonEmpty(informe != null ? informe.UsuarioFirma2 : null, ObtenerUsuarioActual()),
                informe != null ? informe.HashDocumento : "N/D",
                ConstruirUrlDetalle(inspeccion.CodigoInspeccion));
        }

        private string ObtenerNumeroSolicitudVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "DGAC-GOP-2026-AOCR0000";
            }

            return string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? "DGAC-GOP-2026-AOCR" + solicitud.CodigoSolicitud
                : solicitud.NumeroSolicitud.Trim();
        }

        private string ObtenerIpCliente()
        {
            try
            {
                var forwarded = Request != null ? Request.ServerVariables["HTTP_X_FORWARDED_FOR"] : null;
                if (!string.IsNullOrWhiteSpace(forwarded))
                {
                    return forwarded.Split(',')[0].Trim();
                }

                return Request != null ? Request.UserHostAddress : "IP_NO_DISPONIBLE";
            }
            catch
            {
                return "IP_NO_DISPONIBLE";
            }
        }

        private string ConstruirUrlDetalle(int codigoInspeccion)
        {
            try
            {
                if (Request != null && Request.Url != null)
                {
                    return Url.Action("Detalle", "Inspeccion", new { id = codigoInspeccion }, Request.Url.Scheme);
                }
            }
            catch
            {
            }

            return Url.Action("Detalle", "Inspeccion", new { id = codigoInspeccion }) ?? string.Empty;
        }

        private static string LimpiarNombreArchivoVisible(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var limpio = Path.GetFileName(fileName).Replace("\0", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(limpio))
            {
                return null;
            }

            return limpio.Length <= 240 ? limpio : limpio.Substring(0, 240);
        }

        private static string TomarCampoTexto(System.Collections.Specialized.NameValueCollection form, string key, int maxLen, string valorActual)
        {
            if (!TieneCampo(form, key))
            {
                return valorActual;
            }

            return LimpiarTextoLibre(form[key], maxLen);
        }

        private static DateTime? ParseDateTimeSeguro(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime fecha;
            if (DateTime.TryParse(value, new CultureInfo("es-EC"), DateTimeStyles.None, out fecha)
                || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
            {
                return fecha;
            }

            return null;
        }

        private static string TomarServiciosEstaciones(System.Collections.Specialized.NameValueCollection form, string valorActual)
        {
            if (!TieneCampo(form, "serviciosEstacionesPresent"))
            {
                return valorActual;
            }

            var values = new Dictionary<string, string[]>();
            foreach (var row in InformeTecnicoTemplateHelper.GetServicioRows(null))
            {
                values[row.Key] = new[]
                {
                    form["servicio_" + row.Key + "_uio"],
                    form["servicio_" + row.Key + "_gye"],
                    form["servicio_" + row.Key + "_mec"],
                    form["servicio_" + row.Key + "_ltx"]
                };
            }

            return InformeTecnicoTemplateHelper.SerializeServicioRows(values);
        }

        private static string TomarDocumentosAdjuntos(System.Collections.Specialized.NameValueCollection form, string valorActual)
        {
            if (!TieneCampo(form, "documentosAdjuntosPresent"))
            {
                return valorActual;
            }

            var seleccionados = form.GetValues("documentosAdjuntos");
            return InformeTecnicoTemplateHelper.SerializeLines(seleccionados);
        }

        private static bool TieneCampo(System.Collections.Specialized.NameValueCollection form, string key)
        {
            if (form == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            var keys = form.AllKeys;
            if (keys == null)
            {
                return false;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string LimpiarTextoLibre(string valor, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }

            var limpio = valor.Replace("\0", string.Empty).Trim();
            if (limpio.Length > maxLen)
            {
                limpio = limpio.Substring(0, maxLen);
            }

            return limpio;
        }

        private static string NormalizarTipoInspector(string tipoInspector)
        {
            if (string.IsNullOrWhiteSpace(tipoInspector))
            {
                return null;
            }

            var valor = tipoInspector.Trim().ToUpperInvariant();
            if (valor == "OPS" || valor == "AIR")
            {
                return valor;
            }

            return null;
        }

        private void EnriquecerInspectoresDetalle(Inspeccion inspeccion, SolicitudAOCR solicitud)
        {
            if (inspeccion == null)
            {
                return;
            }

            var cedulaPrincipal = FirstNonEmpty(
                inspeccion.InspectorPrincipalCedula,
                solicitud != null ? solicitud.TecnicoResponsableCedula : null,
                inspeccion.CodigoInspector.HasValue ? inspeccion.CodigoInspector.Value.ToString() : null,
                solicitud != null && solicitud.CodigoTecnico.HasValue ? solicitud.CodigoTecnico.Value.ToString() : null);

            var cedulaApoyo = FirstNonEmpty(
                inspeccion.InspectorApoyoCedula,
                solicitud != null ? solicitud.InspectorApoyoCedula : null);

            // ── Fuente principal: catálogo RT / Inspectores ──
            bool principalResuelto = false;
            bool apoyoResuelto = false;
            try
            {
                if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalTipo)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula))
                {
                    UsuarioInternoRTRegistro rtPrincipal = null;
                    if (inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0)
                    {
                        rtPrincipal = _usuarioInternoRTDAO.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(
                            inspeccion.CodigoInspector.Value);
                    }
                    if (rtPrincipal == null && !string.IsNullOrWhiteSpace(cedulaPrincipal))
                    {
                        rtPrincipal = _usuarioInternoRTDAO.ObtenerInspectorAsignableActivo(cedulaPrincipal);
                    }
                    if (rtPrincipal != null)
                    {
                        inspeccion.InspectorPrincipalCedula = FirstNonEmpty(inspeccion.InspectorPrincipalCedula, rtPrincipal.Identificacion, rtPrincipal.CodigoUsuario);
                        inspeccion.InspectorPrincipalNombre = FirstNonEmpty(inspeccion.InspectorPrincipalNombre, rtPrincipal.NombreVisual);
                        inspeccion.InspectorPrincipalTipo = FirstNonEmpty(inspeccion.InspectorPrincipalTipo, rtPrincipal.Tipo);
                        principalResuelto = !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre)
                            && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalTipo);
                    }
                }
                else
                {
                    principalResuelto = true;
                }

                if (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoNombre)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoTipo)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoCedula))
                {
                    if (!string.IsNullOrWhiteSpace(cedulaApoyo))
                    {
                        var rtApoyo = _usuarioInternoRTDAO.ObtenerInspectorAsignableActivo(cedulaApoyo);
                        if (rtApoyo != null)
                        {
                            inspeccion.InspectorApoyoCedula = FirstNonEmpty(inspeccion.InspectorApoyoCedula, rtApoyo.Identificacion, rtApoyo.CodigoUsuario);
                            inspeccion.InspectorApoyoNombre = FirstNonEmpty(inspeccion.InspectorApoyoNombre, rtApoyo.NombreVisual);
                            inspeccion.InspectorApoyoTipo = FirstNonEmpty(inspeccion.InspectorApoyoTipo, rtApoyo.Tipo);
                            apoyoResuelto = !string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoNombre)
                                && !string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoTipo);
                        }
                    }
                }
                else
                {
                    apoyoResuelto = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error enriqueciendo inspectores desde RT. InspeccionId=" + inspeccion.CodigoInspeccion + ", Error=" + ex.Message);
            }

            // ── Fuente secundaria: Espejo PG (rápido, sin depender de AS400) ──
            try
            {
                var daoPg = new InspectorMirrorPGDAO();

                if (!principalResuelto && !string.IsNullOrWhiteSpace(cedulaPrincipal) &&
                    EsCedulaIdentificacionValida(cedulaPrincipal))
                {
                    var principal = daoPg.ObtenerPorCedula(cedulaPrincipal);
                    if (principal != null)
                    {
                        inspeccion.InspectorPrincipalCedula = FirstNonEmpty(inspeccion.InspectorPrincipalCedula, principal.Cedula);
                        inspeccion.InspectorPrincipalNombre = FirstNonEmpty(inspeccion.InspectorPrincipalNombre, principal.NombreCompleto);
                        inspeccion.InspectorPrincipalTipo   = FirstNonEmpty(inspeccion.InspectorPrincipalTipo, principal.Tipo);
                        principalResuelto = !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre)
                            && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalTipo);
                    }
                }

                if (!apoyoResuelto && !string.IsNullOrWhiteSpace(cedulaApoyo) &&
                    EsCedulaIdentificacionValida(cedulaApoyo))
                {
                    var apoyo = daoPg.ObtenerPorCedula(cedulaApoyo);
                    if (apoyo != null)
                    {
                        inspeccion.InspectorApoyoCedula = FirstNonEmpty(inspeccion.InspectorApoyoCedula, apoyo.Cedula);
                        inspeccion.InspectorApoyoNombre = FirstNonEmpty(inspeccion.InspectorApoyoNombre, apoyo.NombreCompleto);
                        inspeccion.InspectorApoyoTipo   = FirstNonEmpty(inspeccion.InspectorApoyoTipo, apoyo.Tipo);
                        apoyoResuelto = !string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoNombre)
                            && !string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoTipo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error enriqueciendo inspectores desde espejo PG. InspeccionId="
                    + inspeccion.CodigoInspeccion + ", Error=" + ex.Message);
            }

            // ── Fuente terciaria: AS400 (fallback final si PG no tiene el registro) ──
            try
            {
                var daoAs400 = new InspectorAS400DAO(new SecureConfigType());

                if (!principalResuelto && !string.IsNullOrWhiteSpace(cedulaPrincipal) &&
                    EsCedulaIdentificacionValida(cedulaPrincipal) &&
                    (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalTipo)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula)))
                {
                    var principal = daoAs400.ObtenerActivoPorCedula(
                        cedulaPrincipal,
                        FirstNonEmpty(inspeccion.InspectorPrincipalTipo, solicitud != null ? solicitud.TecnicoResponsableTipo : null));
                    if (principal == null)
                    {
                        principal = daoAs400.ObtenerActivoPorCedula(cedulaPrincipal);
                    }
                    if (principal != null)
                    {
                        inspeccion.InspectorPrincipalCedula = FirstNonEmpty(inspeccion.InspectorPrincipalCedula, principal.Cedula);
                        inspeccion.InspectorPrincipalNombre = FirstNonEmpty(inspeccion.InspectorPrincipalNombre, principal.NombreCompleto);
                        inspeccion.InspectorPrincipalTipo = FirstNonEmpty(inspeccion.InspectorPrincipalTipo, principal.Tipo);
                    }
                }

                if (!apoyoResuelto && !string.IsNullOrWhiteSpace(cedulaApoyo) &&
                    EsCedulaIdentificacionValida(cedulaApoyo) &&
                    (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoNombre)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoTipo)
                    || string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoCedula)))
                {
                    var apoyo = daoAs400.ObtenerActivoPorCedula(
                        cedulaApoyo,
                        FirstNonEmpty(inspeccion.InspectorApoyoTipo, solicitud != null ? solicitud.InspectorApoyoTipo : null));
                    if (apoyo == null)
                    {
                        apoyo = daoAs400.ObtenerActivoPorCedula(cedulaApoyo);
                    }
                    if (apoyo != null)
                    {
                        inspeccion.InspectorApoyoCedula = FirstNonEmpty(inspeccion.InspectorApoyoCedula, apoyo.Cedula);
                        inspeccion.InspectorApoyoNombre = FirstNonEmpty(inspeccion.InspectorApoyoNombre, apoyo.NombreCompleto);
                        inspeccion.InspectorApoyoTipo = FirstNonEmpty(inspeccion.InspectorApoyoTipo, apoyo.Tipo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error enriqueciendo inspectores desde AS400. InspeccionId=" + inspeccion.CodigoInspeccion + ", Error=" + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula))
            {
                inspeccion.InspectorPrincipalCedula = cedulaPrincipal;
            }

            if (string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre) && !string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalCedula))
            {
                inspeccion.InspectorPrincipalNombre = inspeccion.InspectorPrincipalCedula;
            }

            if (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoCedula))
            {
                inspeccion.InspectorApoyoCedula = cedulaApoyo;
            }

            if (string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoNombre) && !string.IsNullOrWhiteSpace(inspeccion.InspectorApoyoCedula))
            {
                inspeccion.InspectorApoyoNombre = inspeccion.InspectorApoyoCedula;
            }

            _logger.LogInfo("[GestionInspeccion] Detalle inspectores enriquecidos. InspeccionId=" + inspeccion.CodigoInspeccion
                + ", PrincipalCedula=" + (inspeccion.InspectorPrincipalCedula ?? "")
                + ", PrincipalNombre=" + (inspeccion.InspectorPrincipalNombre ?? "")
                + ", PrincipalTipo=" + (inspeccion.InspectorPrincipalTipo ?? "")
                + ", ApoyoCedula=" + (inspeccion.InspectorApoyoCedula ?? "")
                + ", ApoyoNombre=" + (inspeccion.InspectorApoyoNombre ?? "")
                + ", ApoyoTipo=" + (inspeccion.InspectorApoyoTipo ?? ""));
        }

        private static void NormalizarDatosOperadorSolicitud(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return;
            }

            var nombreOperador = (solicitud.NombreOperador ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombreOperador))
            {
                nombreOperador = FirstNonEmpty(solicitud.RazonSocial, solicitud.NombreComercial, solicitud.CodigoOaci, string.Empty);
            }

            solicitud.NombreOperador = nombreOperador;

            if (string.IsNullOrWhiteSpace(solicitud.Email))
            {
                solicitud.Email = FirstNonEmpty(solicitud.CorreoRepresentanteTecnico, string.Empty);
            }

            solicitud.Ruc = FirstNonEmpty(solicitud.Ruc, string.Empty);
            solicitud.CodigoOaci = FirstNonEmpty(solicitud.CodigoOaci, string.Empty);
            solicitud.Telefono = FirstNonEmpty(solicitud.Telefono, string.Empty);
            solicitud.Direccion = FirstNonEmpty(solicitud.Direccion, string.Empty);
            solicitud.RepresentanteLegal = FirstNonEmpty(solicitud.RepresentanteLegal, string.Empty);
        }

        /// <summary>
        /// Determina si la cadena parece una cédula/RUC/pasaporte real (≥7 caracteres)
        /// y no un ID numérico interno del sistema (p.ej. "35").
        /// Evita consultas innecesarias a AS400/DB2.
        /// </summary>
        private static bool EsCedulaIdentificacionValida(string cedula)
        {
            if (string.IsNullOrWhiteSpace(cedula)) return false;
            return cedula.Trim().Length >= 7;
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

        private string ObtenerUsuarioActual()
        {
            var usuarioSesion = Session != null ? Session["Usuario"] as string : null;
            if (!string.IsNullOrWhiteSpace(usuarioSesion))
            {
                return usuarioSesion.Trim();
            }

            return _userContext.GetNombreUsuario(Session, User);
        }

        private string ObtenerRolActual()
        {
            var roles = new List<string>();
            if (User != null)
            {
                if (User.IsInRole(ROL_ADMIN)) roles.Add(ROL_ADMIN);
                if (User.IsInRole(ROL_COORD)) roles.Add(ROL_COORD);
                if (User.IsInRole(ROL_COORD_ALIAS)) roles.Add(ROL_COORD_ALIAS);
                if (User.IsInRole(ROL_INSPECTOR)) roles.Add(ROL_INSPECTOR);
                if (User.IsInRole(ROL_JEFATURA)) roles.Add(ROL_JEFATURA);
                if (User.IsInRole(ROL_JEFE)) roles.Add(ROL_JEFE);
                if (User.IsInRole(ROL_DIRECCION)) roles.Add(ROL_DIRECCION);
                if (User.IsInRole(ROL_DIRECTOR)) roles.Add(ROL_DIRECTOR);
                if (User.IsInRole(ROL_LEGAL)) roles.Add(ROL_LEGAL);
                if (User.IsInRole(ROL_COORD_LEGAL)) roles.Add(ROL_COORD_LEGAL);
                if (User.IsInRole(ROL_COORDINADOR_LEGAL)) roles.Add(ROL_COORDINADOR_LEGAL);
            }

            return roles.Count == 0 ? "SIN_ROL_DETECTADO" : string.Join(",", roles);
        }
    }
}
