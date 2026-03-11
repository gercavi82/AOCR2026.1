using System;
using System.Collections.Generic;
using System.IO;
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

        private const string ROL_ADMIN = "Administrador";
        private const string ROL_COORD = "CoordinadorInspecciones";
        private const string ROL_INSPECTOR = "Inspector";
        private const string ROL_JEFATURA = "JefaturaTecnica";

        // Seguridad: tamaño máximo permitido para PDF (10MB)
        private const int MAX_PDF_BYTES = 10 * 1024 * 1024;

        // Carpeta de informes
        private const string CARPETA_VIRTUAL_INFORMES = "~/App_Data/Uploads/Inspecciones";

        public InspeccionController()
        {
            _hallazgoBL = new HallazgoBL();
            _inspeccionBL = new InspeccionBL();
            _inspeccionDAO = new InspeccionDAO();
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

        private bool PuedeAccederInspeccion(Inspeccion ins)
        {
            if (ins == null) return false;
            if (EsAdmin()) return true;

            if (User.IsInRole(ROL_COORD) || User.IsInRole(ROL_JEFATURA))
                return true;

            var codigoUsuario = ObtenerCodigoUsuario();
            if (User.IsInRole(ROL_INSPECTOR))
                return ins.CodigoInspector.HasValue && ins.CodigoInspector.Value == codigoUsuario;

            return false;
        }

        // ============================================================
        // ✅ LISTADO (POR ROL)
        // ============================================================
        [Authorize(Roles = ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        public ActionResult Index()
        {
            _logger.LogInfo("[InspeccionesController] Inicio pantalla gestion inspecciones. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            List<Inspeccion> lista;

            if (EsAdmin() || User.IsInRole(ROL_COORD) || User.IsInRole(ROL_JEFATURA))
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
        [Authorize(Roles = ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
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

            ViewBag.Hallazgos = _hallazgoBL.ObtenerPorInspeccion(id);

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
        [Authorize(Roles = ROL_JEFATURA + "," + ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_ADMIN)]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstado(int id, string estado)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            _logger.LogInfo("[GestionInspeccion] Inicio CambiarEstado. InspeccionId=" + id + ", EstadoSolicitado=" + (estado ?? "") + ", Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            if (string.IsNullOrWhiteSpace(estado))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Estado destino vacio.");
                TempData["Error"] = "Debe seleccionar un estado.";
                return RedirectToAction("Detalle", new { id });
            }

            var estadoDestino = EstadosInspeccion.NormalizarEstado(estado);
            if (!UsuarioActualPuedeCambiarEstadoInspeccion(estadoDestino))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=Rol sin permisos para estado destino. EstadoDestino=" + estadoDestino + ", Rol=" + ObtenerRolActual());
                TempData["Error"] = "No tiene permisos para cambiar a ese estado.";
                return RedirectToAction("Detalle", new { id });
            }

            try
            {
                int codigoUsuario = ObtenerCodigoUsuario();
                bool ok = _inspeccionBL.CambiarEstado(id, estadoDestino, codigoUsuario);

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
        [Authorize(Roles = ROL_COORD + "," + ROL_INSPECTOR + "," + ROL_JEFATURA + "," + ROL_ADMIN)]
        public ActionResult VerInforme(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID inválido.");

            var inspeccion = _inspeccionDAO.ObtenerPorId(id);
            if (inspeccion == null)
                return HttpNotFound("Inspección no encontrada.");

            if (!PuedeAccederInspeccion(inspeccion))
                return new HttpStatusCodeResult(403, "No autorizado para ver el informe.");

            var rutaRelativa = inspeccion.RutaInforme;

            if (string.IsNullOrWhiteSpace(rutaRelativa))
                return HttpNotFound("La inspección aún no tiene informe cargado.");

            if (!rutaRelativa.StartsWith("/"))
                rutaRelativa = "/" + rutaRelativa;

            var fullPath = Server.MapPath("~" + rutaRelativa);

            var baseDir = Server.MapPath(CARPETA_VIRTUAL_INFORMES);
            if (!EsRutaDentroDeBase(fullPath, baseDir))
                return new HttpStatusCodeResult(400, "Ruta de informe inválida.");

            if (!System.IO.File.Exists(fullPath))
                return HttpNotFound("El archivo del informe no existe en el servidor.");

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.AddHeader("Content-Disposition", "inline; filename=InformeInspeccion_" + id + ".pdf");

            return File(fullPath, "application/pdf");
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
                okEstado = _inspeccionBL.CambiarEstado(id, EstadosInspeccion.VIATICOS_REQUERIDOS, usuarioId);
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
                okEstado = _inspeccionBL.CambiarEstado(id, EstadosInspeccion.PAGO_VALIDADO, usuarioId);
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
                okInformeElaborado = _inspeccionBL.CambiarEstado(id, EstadosInspeccion.INFORME_ELABORADO, usuarioId);
            }
            catch
            {
                okInformeElaborado = false;
            }

            try
            {
                okResultado = _inspeccionBL.CambiarEstado(id, estadoDestino, usuarioId);
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

        private bool UsuarioActualPuedeCambiarEstadoInspeccion(string estadoDestino)
        {
            if (EsAdmin())
            {
                return true;
            }

            var destino = EstadosInspeccion.NormalizarEstado(estadoDestino);

            if (User.IsInRole(ROL_INSPECTOR))
            {
                return destino == EstadosInspeccion.EN_INSPECCION
                    || destino == EstadosInspeccion.INFORME_ELABORADO
                    || destino == EstadosInspeccion.RESULTADO_SATISFACTORIO
                    || destino == EstadosInspeccion.RESULTADO_NO_SATISFACTORIO
                    || destino == EstadosInspeccion.OBSERVACION_DOCUMENTAL
                    || destino == EstadosInspeccion.SUBSANADA;
            }

            if (User.IsInRole(ROL_COORD) || User.IsInRole(ROL_JEFATURA))
            {
                return true;
            }

            return false;
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
    }
}
