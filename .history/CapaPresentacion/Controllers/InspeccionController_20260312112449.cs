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
using CapaUtilidades;
using CapaPresentacion.Models.ViewModels;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class InspeccionController : Controller
    {
        private readonly HallazgoBL _hallazgoBL;
        private readonly ILoggingService _logger;

        // ✅ Inyección simple (no static)
        private readonly InspeccionBL _inspeccionBL;
        private readonly InspeccionDAO _inspeccionDAO;
        private readonly InspeccionHistorialDAO _historialDAO;
        private readonly InspeccionInformeDAO _informeDAO;
        private readonly DocumentoInspeccionDAO _documentoDAO;
        private readonly SolicitudAOCRDAO _solicitudDAO;

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

        // Seguridad: tamaño máximo permitido para PDF (10MB)
        private const int MAX_PDF_BYTES = 10 * 1024 * 1024;

        // Carpeta de informes
        private const string CARPETA_VIRTUAL_INFORMES = "~/App_Data/Uploads/Inspecciones";
        private const string CARPETA_VIRTUAL_INFORMES_TECNICOS = "~/App_Data/Uploads/Inspecciones/InformesTecnicos";
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
            _logger = LoggingServiceFactory.Create();
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
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[GestionInspeccion] Error cargando solicitud en Detalle. InspeccionId=" + id + ", SolicitudId=" + inspeccion.CodigoSolicitud + ", Error=" + ex.Message);
                ViewBag.Solicitud = null;
            }

            return View("~/Views/Inspeccion/Detalle.cshtml", inspeccion);
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

                _logger.LogInfo("[GestionInspeccion] PuedeGestionar=" + ok + ", InspeccionId=" + id + ", EstadoDestino=" + estadoDestino + ", Usuario=" + ObtenerUsuarioActual());

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

            var rutaRelativa = (inspeccion.RutaInforme ?? string.Empty).Trim();

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
                var correoEnviado = EnviarInformeTecnicoAlSolicitante(inspeccion, solicitud, informe, pdfBytes);

                _informeDAO.MarcarFinalizado(informe.CodigoInforme, rutaPdf, correoEnviado, usuarioId);
                _inspeccionBL.GuardarInforme(id, rutaPdf, usuarioId);

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

                TempData["Success"] = correoEnviado
                    ? "Informe técnico finalizado, PDF generado y correo enviado al solicitante."
                    : "Informe técnico finalizado y PDF generado. No se pudo enviar el correo al solicitante.";

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

            var codigoUsuario = ObtenerCodigoUsuario();
            bool ok = _inspeccionBL.CerrarInspeccion(id, resultado, codigoUsuario);

            TempData[ok ? "Success" : "Error"] = ok
                ? "Inspección cerrada correctamente."
                : "No se pudo cerrar la inspección.";

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
            var resultadoNormalizado = (resultado ?? string.Empty).Trim().ToUpperInvariant();
            var esSatisfactorio = resultadoNormalizado == "SATISFACTORIO" || resultadoNormalizado == "APROBADO";
            var estadoDestino = esSatisfactorio
                ? EstadosInspeccion.RESULTADO_SATISFACTORIO
                : EstadosInspeccion.RESULTADO_NO_SATISFACTORIO;

            inspeccion.ResultadoEvaluacion = esSatisfactorio ? "RESULTADO_SATISFACTORIO" : "RESULTADO_NO_SATISFACTORIO";
            inspeccion.Resultado = esSatisfactorio ? "APROBADO" : "RECHAZADO";
            inspeccion.EstadoDocumental = esSatisfactorio ? "ACEPTADA" : "OBSERVACION_DOCUMENTAL";
            inspeccion.ObservacionesGenerales = string.IsNullOrWhiteSpace(observacion)
                ? inspeccion.ObservacionesGenerales
                : observacion;

            var okUpdate = _inspeccionBL.Actualizar(inspeccion, usuarioId);
            var okInformeElaborado = false;
            var okResultado = false;

            try
            {
                okInformeElaborado = _inspeccionBL.CambiarEstado(id, EstadosInspeccion.INFORME_ELABORADO, usuarioId, "Resultado registrado con informe asociado.", ObtenerUsuarioActual(), "RESULTADO_INSPECCION");
            }
            catch
            {
                okInformeElaborado = false;
            }

            try
            {
                okResultado = _inspeccionBL.CambiarEstado(id, estadoDestino, usuarioId, observacion, ObtenerUsuarioActual(), "RESULTADO_INSPECCION");
            }
            catch
            {
                okResultado = false;
            }

            TempData[(okUpdate && okInformeElaborado && okResultado) ? "Success" : "Error"] = (okUpdate && okInformeElaborado && okResultado)
                ? "Resultado de inspección registrado correctamente."
                : "No se pudo registrar el resultado de la inspección.";

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

            var dao = new InspectorAS400DAO(new SecureConfigurationService());

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
