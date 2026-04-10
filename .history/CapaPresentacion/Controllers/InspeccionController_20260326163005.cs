using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaNegocio;
using CapaModelo;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaUtilidades;
using CapaPresentacion.Models.ViewModels;
using Rotativa;
using LoggingServiceType = CapaDatos.Services.ILoggingService;
using LoggingFactoryType = CapaDatos.Services.LoggingServiceFactory;
using SecureConfigType = CapaDatos.Services.SecureConfigurationService;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class InspeccionController : Controller
    {
        private readonly HallazgoBL _hallazgoBL;
        private readonly LoggingServiceType _logger;

        // ✅ Inyección simple (no static)
        private readonly InspeccionBL _inspeccionBL;
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly InspeccionHistorialDAO _historialDAO;
        private readonly InspeccionInformeDAO _informeDAO;
        private readonly DocumentoInspeccionDAO _documentoDAO;
        private readonly SolicitudAOCRDAO _solicitudDAO;
        private readonly InspeccionService _inspeccionService;
        private readonly InspeccionCorreoService _inspeccionCorreoService;
        private readonly FirmaDigitalService _firmaDigitalService;

        private const string ROL_ADMIN = "Administrador";
        private const string ROL_COORD = "CoordinadorInspecciones";
        private const string ROL_COORD_ALIAS = "Coordinador";
        private const string ROL_INSPECTOR = "Inspector";
        private const string ROL_JEFATURA = "JefaturaTecnica";
        private const string ROL_JEFE = "Jefe";
        private const string ROL_DIRECCION = "Direccion";
        private const string ROL_DIRECTOR = "Director";
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
        private const string ROLES_FIRMA_DIRDAC = ROL_DIRECCION + "," + ROL_DIRECTOR + "," + ROL_JEFATURA + "," + ROL_JEFE + "," + ROL_ADMIN;

        // Seguridad: tamaño máximo permitido para PDF (10MB)
        private const int MAX_PDF_BYTES = 10 * 1024 * 1024;

        // Carpeta de informes
        private const string CARPETA_VIRTUAL_INFORMES = "~/App_Data/Uploads/Inspecciones";
        private const string CARPETA_VIRTUAL_INFORMES_TECNICOS = "~/App_Data/Uploads/Inspecciones/InformesTecnicos";
        private const string CARPETA_VIRTUAL_INFORMES_TECNICOS_FIRMADOS = "~/App_Data/Uploads/Inspecciones/InformesTecnicos/Firmados";
        private const string CARPETA_VIRTUAL_DOCUMENTOS_SOLICITANTE = "~/App_Data/Uploads/Inspecciones/DocumentosSolicitante";

        public InspeccionController()
        {
            _hallazgoBL = new HallazgoBL();
            _inspeccionBL = new InspeccionBL();
            _inspeccionDAO = new InspeccionDAO();
            _historialDAO = new InspeccionHistorialDAO();
            _informeDAO = new InspeccionInformeDAO();
            _documentoDAO = new DocumentoInspeccionDAO();
            _solicitudDAO = new SolicitudAOCRDAO();
            _inspeccionService = new InspeccionService();
            _inspeccionCorreoService = new InspeccionCorreoService();
            _firmaDigitalService = new FirmaDigitalService();
            _logger = LoggingFactoryType.Create();
        }

        private int ObtenerCodigoUsuario()
        {
            if (Session["CodigoUsuario"] != null &&
                int.TryParse(Session["CodigoUsuario"].ToString(), out var id))
                return id;

            return 0;
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
                ROL_LEGAL,
                ROL_COORD_LEGAL,
                ROL_COORDINADOR_LEGAL);
        }

        private bool EsRolInspector()
        {
            return User != null && User.IsInRole(ROL_INSPECTOR);
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
        public ActionResult Index()
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

            return View("~/Views/Inspeccion/Index.cshtml", lista);
        }

        // ============================================================
        // ✅ DETALLE
        // ============================================================
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
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

            EnriquecerInspectoresDetalle(inspeccion, ViewBag.Solicitud as SolicitudAOCR);

            return View("~/Views/Inspeccion/Detalle.cshtml", inspeccion);
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
                TempData["Error"] = "No se pudo cargar el listado de documentos pendientes de firma DIRDAC.";
                return RedirectToAction("Index");
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
        public ActionResult CambiarEstado(int id, string estado)
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
                return RedirectToAction("Detalle", new { id });
            }

            if (string.IsNullOrWhiteSpace(estado))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Estado destino vacio.");
                TempData["Error"] = "Debe seleccionar un estado.";
                return RedirectToAction("Detalle", new { id });
            }

            var estadoActual = EstadosInspeccion.NormalizarEstado(inspeccion.Estado);
            var estadoDestino = EstadosInspeccion.NormalizarEstado(estado);

            if (!EstadosInspeccion.EsTransicionValida(estadoActual, estadoDestino))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Transicion no permitida. EstadoActual=" + estadoActual + ", EstadoDestino=" + estadoDestino + ", InspeccionId=" + id);
                TempData["Error"] = "Transición no permitida: " + estadoActual + " -> " + estadoDestino;
                return RedirectToAction("Detalle", new { id });
            }

            if (!UsuarioActualPuedeCambiarEstadoInspeccion(estadoActual, estadoDestino))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Rol sin permisos para estado destino. EstadoDestino=" + estadoDestino + ", Rol=" + ObtenerRolActual());
                TempData["Error"] = "No tiene permisos para cambiar a ese estado.";
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                string bloqueBpmn = EstadosInspeccion.EsEstadoBloqueInspector(estadoDestino)
                    ? "OPERACION_INSPECTOR"
                    : "COORDINACION_Y_JEFATURA";

                bool ok = _inspeccionBL.CambiarEstado(
                    id,
                    estadoDestino,
                    codigoUsuario,
                    "Cambio de estado BPMN desde bloque " + bloqueBpmn + ".",
                    ObtenerUsuarioActual(),
                    bloqueBpmn);

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

                TempData[ok ? "Success" : "Error"] = ok
                    ? "Estado actualizado correctamente."
                    : "No se pudo actualizar el estado.";
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en CambiarEstado: " + ex);
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Detalle", new { id });
        }

        // ============================================================
        // ✅✅✅ VER INFORME (ÚNICO) - SEGURO
        // ============================================================
        [HttpGet]
        [Authorize(Roles = ROLES_GESTION_INSPECCION_CON_SOLICITANTE)]
        public ActionResult VerInforme(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
                return HttpNotFound("Inspección no encontrada.");

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para ver el informe.");

            var informeTecnico = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var rutaRelativa = FirstNonEmpty(
                informeTecnico != null ? informeTecnico.RutaDocumentoFirmado : null,
                inspeccion.RutaInforme,
                informeTecnico != null ? informeTecnico.RutaPdf : null,
                string.Empty);

            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                _logger.LogWarning("[GestionInspeccion] VerInforme sin ruta. InspeccionId=" + id);
                return HttpNotFound("La inspección aún no tiene informe cargado.");
            }

            // Compatibilidad: la ruta puede venir como "~/..." o "/..."
            if (rutaRelativa.StartsWith("~"))
                rutaRelativa = rutaRelativa.Substring(1);

            if (!rutaRelativa.StartsWith("/"))
                rutaRelativa = "/" + rutaRelativa;

            var fullPath = Server.MapPath("~" + rutaRelativa);

            var baseDir = Server.MapPath(CARPETA_VIRTUAL_INFORMES);
            if (!EsRutaDentroDeBase(fullPath, baseDir))
            {
                _logger.LogWarning("[GestionInspeccion] VerInforme ruta fuera de base. InspeccionId=" + id + ", Ruta=" + rutaRelativa + ", FullPath=" + fullPath + ", BaseDir=" + baseDir);
                return new HttpStatusCodeResult(400, "Ruta de informe inválida.");
            }

            if (!System.IO.File.Exists(fullPath))
            {
                _logger.LogWarning("[GestionInspeccion] VerInforme archivo inexistente. InspeccionId=" + id + ", FullPath=" + fullPath);
                return HttpNotFound("El archivo del informe no existe en el servidor.");
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.AddHeader("Content-Disposition", "inline; filename=InformeInspeccion_" + id + ".pdf");

            return File(fullPath, "application/pdf");
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
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_COORD + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult GuardarInformeTecnico()
        {
            var id = 0;
            try
            {
                var form = Request?.Unvalidated?.Form;
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
                    return new HttpStatusCodeResult(400, "ID inválido.");
                }

                var inspeccion = _inspeccionDAO.ObtenerPorId(id);
                if (inspeccion == null)
                {
                    return HttpNotFound("Inspección no encontrada.");
                }

                if (!PuedeAccederInspeccion(inspeccion))
                {
                    return new HttpStatusCodeResult(403, "No autorizado para editar el informe técnico.");
                }

                var titulo = form?["titulo"];
                var resumen = form?["resumen"];
                var resultado = form?["resultado"];
                var observaciones = form?["observaciones"];
                var conclusiones = form?["conclusiones"];
                var recomendaciones = form?["recomendaciones"];

                var usuarioId = ObtenerCodigoUsuario();
                var informe = _informeDAO.GuardarBorrador(new InspeccionInformeTecnico
                {
                    CodigoInspeccion = id,
                    Titulo = LimpiarTextoLibre(titulo, 250),
                    Resumen = LimpiarTextoLibre(resumen, 8000),
                    Resultado = LimpiarTextoLibre(resultado, 120),
                    Observaciones = LimpiarTextoLibre(observaciones, 8000),
                    Conclusiones = LimpiarTextoLibre(conclusiones, 8000),
                    Recomendaciones = LimpiarTextoLibre(recomendaciones, 8000)
                }, usuarioId);

                if (!finalizar)
                {
                    TempData["Success"] = "Borrador del informe técnico guardado correctamente.";
                    return RedirectToAction("Detalle", new { id });
                }

                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                var pdfBytes = GenerarPdfInformeTecnico(inspeccion, solicitud, informe);
                var rutaPdf = GuardarInformeTecnicoPdf(id, informe.Version, pdfBytes);

                _informeDAO.MarcarFinalizado(informe.CodigoInforme, rutaPdf, false, "GENERADO", usuarioId);
                _inspeccionBL.GuardarInforme(id, rutaPdf, usuarioId);
                RegistrarAuditoriaInformeDigital(id, "BORRADOR", "GENERADO", rutaPdf, null, "Informe técnico generado en PDF. IP=" + ObtenerIpCliente(), usuarioId, ObtenerUsuarioActual(), "INFORME_GENERADO");

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

                return RedirectToAction("Detalle", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error en GuardarInformeTecnico: " + ex);
                TempData["Error"] = "No se pudo guardar el informe técnico. Verifique los datos ingresados e intente nuevamente.";
                return RedirectToAction("Detalle", new { id });
            }
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
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarInformeInspector(int id, string passwordCertificado)
        {
            return FirmarInformePorRol(id, passwordCertificado, "CertificadoInspector", "INSPECTOR", "FIRMADO_INSPECTOR", autoEnviarADirdac: true);
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_COORD + "," + ROL_COORD_ALIAS + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
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

            if (!PuedeAccederInspeccion(inspeccion))
            {
                return new HttpStatusCodeResult(403, "No autorizado para enviar el informe a DIRDAC.");
            }

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            var resultado = EnviarInformeADirdacInterno(inspeccion, solicitud, informe, ObtenerCodigoUsuario());
            var informeActualizado = _informeDAO.ObtenerUltimoPorInspeccion(id);
            var mensajeKey = resultado.Exitoso
                ? "Success"
                : (InformeEstaEnviadoADirdac(informeActualizado) ? "Warning" : "Error");

            TempData[mensajeKey] = resultado.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROLES_FIRMA_DIRDAC)]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarInformeDirdac(int id, string passwordCertificado)
        {
            return FirmarInformePorRol(id, passwordCertificado, "CertificadoDirdac", "DIRDAC", "FIRMADO_FINAL", autoEnviarADirdac: false);
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
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarResultado(int id, string resultado, string observacion = "")
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null) return HttpNotFound("Inspección no encontrada.");
            if (!PuedeAccederInspeccion(inspeccion)) return new HttpStatusCodeResult(403, "No autorizado.");

            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.RegistrarResultadoInspeccion(id, resultado, observacion, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;

            return RedirectToAction("Detalle", new { id });
        }

        [HttpPost]
        [Authorize(Roles = ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult Evaluar(int id, string resultado, string observacion = "")
        {
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
            var usuarioId = ObtenerCodigoUsuario();
            var usuarioNombre = ObtenerUsuarioActual();
            var op = _inspeccionService.RegistrarNoConformidad(id, descripcion, criticidad, usuarioId, usuarioNombre);

            TempData[op.Exitoso ? "Success" : "Error"] = op.Mensaje;
            return RedirectToAction("Detalle", new { id });
        }

        // ============================================================
        // ✅ HELPERS DE SEGURIDAD
        // ============================================================
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
                return EsRolCoordinacionYJefatura();
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
                return EsRolInspector() || EsRolCoordinacionYJefatura();
            }

            return EsRolCoordinacionYJefatura();
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

        private byte[] GenerarPdfInformeTecnico(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe)
        {
            var vm = new InformeTecnicoPdfViewModel
            {
                Inspeccion = inspeccion,
                Solicitud = solicitud,
                Informe = informe
            };

            var pdf = new PartialViewAsPdf("InformeTecnicoPdf", vm)
            {
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(12, 12, 14, 14)
            };

            return pdf.BuildFile(ControllerContext);
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
                var cuerpo = "<p>Se ha finalizado el informe técnico de su inspección AOCR.</p>" +
                             "<p><strong>Inspección:</strong> " + inspeccion.CodigoInspeccion + "</p>" +
                             "<p><strong>Resultado:</strong> " + HttpUtility.HtmlEncode(informe != null ? informe.Resultado : inspeccion.Resultado) + "</p>" +
                             "<p>Puede revisar observaciones y cargar documentación corregida desde el siguiente enlace:</p>" +
                             "<p><a href=\"" + HttpUtility.HtmlAttributeEncode(enlace) + "\">Abrir detalle de inspección</a></p>" +
                             "<p>Se adjunta el informe técnico en formato PDF.</p>";

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

            var informe = _informeDAO.ObtenerUltimoPorInspeccion(id);
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

            if (string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase))
            {
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

            var certificado = Request.Files[nombreCampoArchivo];
            string mensajeValidacion;
            if (!EsCertificadoDigitalValido(certificado, out mensajeValidacion))
            {
                TempData["Error"] = mensajeValidacion;
                return RedirectToAction("Detalle", new { id });
            }

            var rutaFuente = string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase)
                ? FirstNonEmpty(informe.RutaDocumentoFirmado, inspeccion.RutaInforme, informe.RutaPdf)
                : FirstNonEmpty(informe.RutaPdf, inspeccion.RutaInforme, informe.RutaDocumentoFirmado);
            var pathFuente = ResolverRutaAbsolutaInforme(rutaFuente);
            if (string.IsNullOrWhiteSpace(pathFuente) || !System.IO.File.Exists(pathFuente))
            {
                TempData["Error"] = "No se encontró el documento PDF a firmar en el servidor.";
                return RedirectToAction("Detalle", new { id });
            }

            byte[] pdfFirmado;
            string hashDocumento;
            using (var ms = new MemoryStream())
            {
                certificado.InputStream.CopyTo(ms);
                _logger.LogInfo("[GestionInspeccion] Inicio firma digital. InspeccionId=" + id
                    + ", RolFirma=" + rolFirma
                    + ", InformeId=" + informe.CodigoInforme
                    + ", VersionInforme=" + informe.Version
                    + ", RutaFuente=" + pathFuente
                    + ", PdfBytes=" + new FileInfo(pathFuente).Length
                    + ", CertificadoBytes=" + ms.Length
                    + ", Usuario=" + ObtenerUsuarioActual());

                var resultadoFirma = _firmaDigitalService.FirmarPdf(
                    System.IO.File.ReadAllBytes(pathFuente),
                    ms.ToArray(),
                    passwordCertificado,
                    ObtenerUsuarioActual(),
                    string.Equals(rolFirma, "DIRDAC", StringComparison.OrdinalIgnoreCase)
                        ? "Firma institucional DIRDAC del informe técnico AOCR"
                        : "Firma del inspector sobre el informe técnico AOCR",
                    "Sistema AOCR DGAC",
                    rolFirma);

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
            }

            var rutaFirmada = GuardarInformeTecnicoFirmadoPdf(id, informe.Version, rolFirma, pdfFirmado);
            var usuarioId = ObtenerCodigoUsuario();
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
                _informeDAO.RegistrarFirmaDirdac(informe.CodigoInforme, rutaFirmada, hashDocumento, DateTime.Now, usuarioActual, estadoFinal, usuarioId);
            }
            else
            {
                _informeDAO.RegistrarFirmaInspector(informe.CodigoInforme, rutaFirmada, hashDocumento, DateTime.Now, usuarioActual, estadoFinal, usuarioId);
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
                string.Format("Firma digital aplicada por {0}. Rol={1}. IP={2}", usuarioActual, rolFirma, ObtenerIpCliente()),
                usuarioId,
                usuarioActual,
                "FIRMA_DIGITAL_" + rolFirma);

            if (autoEnviarADirdac)
            {
                var solicitud = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
                var informeActualizado = _informeDAO.ObtenerPorId(informe.CodigoInforme);
                var resultadoEnvio = EnviarInformeADirdacInterno(inspeccion, solicitud, informeActualizado, usuarioId);
                TempData[resultadoEnvio.Exitoso ? "Success" : "Warning"] = resultadoEnvio.Exitoso
                    ? "Informe firmado por inspector y enviado a DIRDAC para firma institucional."
                    : "Informe firmado por inspector. " + resultadoEnvio.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            var solicitudFinal = _solicitudDAO.ObtenerPorId(inspeccion.CodigoSolicitud);
            var informeFinal = _informeDAO.ObtenerPorId(informe.CodigoInforme);
            var resultadoNotificacion = _inspeccionCorreoService.NotificarInformeTecnicoFirmadoFinal(
                inspeccion,
                solicitudFinal,
                informeFinal,
                pdfFirmado,
                ConstruirUrlDetalle(id),
                ConstruirDetalleCorreoFirmaFinal(inspeccion, solicitudFinal, informeFinal));

            TempData[resultadoNotificacion.Exitoso ? "Success" : "Warning"] = resultadoNotificacion.Exitoso
                ? "Documento firmado por DIRDAC y notificado a los actores del proceso."
                : "Documento firmado por DIRDAC. No fue posible enviar todas las notificaciones finales.";

            return RedirectToAction("Detalle", new { id });
        }

        private ResultadoOperacion EnviarInformeADirdacInterno(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe, int usuarioId)
        {
            if (inspeccion == null || solicitud == null || informe == null)
            {
                return ResultadoOperacion.Error("No existe contexto suficiente para enviar el informe técnico a DIRDAC.");
            }

            if (!informe.Finalizado)
            {
                return ResultadoOperacion.Error("El informe técnico aún no ha sido finalizado en PDF.");
            }

            if (!informe.FirmadoInspector)
            {
                return ResultadoOperacion.Error("Debe firmar primero el informe con el certificado del inspector.");
            }

            if (informe.FirmadoDirdac)
            {
                return ResultadoOperacion.Error("El informe técnico ya cuenta con la firma final de DIRDAC.");
            }

            var yaEnviadoADirdac = InformeEstaEnviadoADirdac(informe);
            if (yaEnviadoADirdac && informe.CorreoEnviado)
            {
                return ResultadoOperacion.Error("El documento ya fue notificado a DIRDAC y está pendiente de firma final.");
            }

            if (!yaEnviadoADirdac)
            {
                var estadoAnterior = FirstNonEmpty(informe.EstadoInforme, "FIRMADO_INSPECTOR", "GENERADO");
                _informeDAO.MarcarEnviadoADirdac(informe.CodigoInforme, DateTime.Now, ObtenerUsuarioActual(), false, "ENVIADO_A_DIRDAC", usuarioId);
                RegistrarAuditoriaInformeDigital(
                    inspeccion.CodigoInspeccion,
                    estadoAnterior,
                    "ENVIADO_A_DIRDAC",
                    FirstNonEmpty(informe.RutaDocumentoFirmado, informe.RutaPdf, inspeccion.RutaInforme),
                    informe.HashDocumento,
                    "Documento transferido automáticamente a la bandeja de firma DIRDAC. IP=" + ObtenerIpCliente(),
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
                    "Reintento de notificación formal a DIRDAC. ResultadoCorreo=" + (resultadoCorreo.Exitoso ? "OK" : "ERROR") + ". IP=" + ObtenerIpCliente(),
                    usuarioId,
                    ObtenerUsuarioActual(),
                    "REENVIO_NOTIFICACION_DIRDAC");
            }

            if (resultadoCorreo.Exitoso)
            {
                return ResultadoOperacion.Ok(null,
                    yaEnviadoADirdac
                        ? "La notificacion formal a DIRDAC se reenvio correctamente. El documento continua pendiente de firma final."
                        : "Documento pendiente de firma enviado a DIRDAC correctamente.");
            }

            return ResultadoOperacion.Error(
                yaEnviadoADirdac
                    ? "El documento ya está en la bandeja DIRDAC, pero continúa pendiente el correo formal. Puede reintentar la notificación más tarde."
                    : (notificacionInternaOk
                        ? "El documento pasó a la bandeja DIRDAC, pero falló el correo formal a Dirección. La notificación interna ya fue registrada."
                        : "El documento pasó a la bandeja DIRDAC, pero no fue posible enviar la notificación formal a Dirección."));
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
            var titulo = "Informe técnico pendiente de firma DIRDAC";
            var mensaje = string.Format(
                "La inspección #{0} de la solicitud {1} ({2}) ya fue firmada por el inspector y quedó disponible para firma institucional.",
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

        private string ResolverRutaAbsolutaInforme(string rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa))
            {
                return null;
            }

            var ruta = rutaRelativa.Trim();
            if (!ruta.StartsWith("~"))
            {
                ruta = "~" + (ruta.StartsWith("/") ? ruta : "/" + ruta);
            }

            return Server.MapPath(ruta);
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
                "Solicitud: {0}. Compañía: {1}. Estado informe: {2}. Generado el: {3:dd/MM/yyyy HH:mm}. Enlace: {4}",
                numeroSolicitud,
                compania,
                FirstNonEmpty(informe != null ? informe.EstadoInforme : null, "GENERADO"),
                informe != null && informe.FechaFinalizacion.HasValue ? informe.FechaFinalizacion.Value : DateTime.Now,
                ConstruirUrlDetalle(inspeccion.CodigoInspeccion));
        }

        private string ConstruirDetalleCorreoFirmaFinal(Inspeccion inspeccion, SolicitudAOCR solicitud, InspeccionInformeTecnico informe)
        {
            var numeroSolicitud = ObtenerNumeroSolicitudVisible(solicitud);
            var compania = FirstNonEmpty(solicitud != null ? solicitud.RazonSocial : null, solicitud != null ? solicitud.NombreOperador : null, "No disponible");
            return string.Format(
                "Solicitud: {0}. Compañía: {1}. Fecha firma final: {2:dd/MM/yyyy HH:mm}. Firmado por: {3}. Hash documento: {4}. Enlace: {5}",
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

            try
            {
                var daoAs400 = new InspectorAS400DAO(new SecureConfigType());

                if (!string.IsNullOrWhiteSpace(cedulaPrincipal) &&
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

                if (!string.IsNullOrWhiteSpace(cedulaApoyo) &&
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
            if (Session != null && Session["Usuario"] != null)
            {
                return Session["Usuario"].ToString();
            }

            return (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                ? User.Identity.Name
                : "ANONIMO";
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
