using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs; // Solo para SolicitudDAO (Cabecera)
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio;    // <--- IMPORTANTE: Usamos la Capa de Negocio
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;
using CapaUtilidades;
using CapaPresentacion.Filters;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    [AocrAuthorize(Modulo = "Documento")]
    public class DocumentoController : Controller
    {
        // 1. Usamos la BL en lugar del DAO
        private readonly DocumentoBL _documentoBL;
        private readonly DocumentoDAO _documentoDAO;
        private readonly SolicitudAOCRDAO _solicitudDAO; // Solo para obtener datos de la solicitud (padre)
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBL;
        private readonly string _rutaDocumentos;

        public DocumentoController()
        {
            _documentoBL = new DocumentoBL();
            _documentoDAO = new DocumentoDAO();
            _solicitudDAO = new SolicitudAOCRDAO();
            _solicitudAocrInfraBL = new SolicitudAocrInfraBL();
            if (System.Web.HttpContext.Current != null)
            {
                _rutaDocumentos = FileStorageHelper.GetPhysicalBasePath("~/App_Data/Documentos");
                if (!Directory.Exists(_rutaDocumentos))
                {
                    Directory.CreateDirectory(_rutaDocumentos);
                }
            }
        }

        #region Vistas Principales

        // GET: Documento/Lista/5
        public ActionResult Lista(int solicitudId, string modo = null, string origen = null, int? inspeccionId = null)
        {
            try
            {
                string motivoAuth;
                if (!AocrPresentacionAuthorizationHelper.EsPermitido(HttpContext, "Documento", "Lista", out motivoAuth, solicitudId))
                {
                    TempData["Error"] = motivoAuth;
                    return RedirectToAction("Index", "SolicitudAOCR");
                }

                var solicitud = _solicitudDAO.ObtenerPorId(solicitudId);
                if (solicitud == null) return RedirectToAction("Index", "SolicitudAOCR");

                int usuarioId;
                var tieneUsuario = TryObtenerUsuarioActualId(out usuarioId);
                Inspeccion inspeccionVinculada;
                string rolesActuales;
                var puedeVer = PuedeVerDocumentosSolicitud(solicitud, tieneUsuario ? usuarioId : 0, out inspeccionVinculada, out rolesActuales);

                var documentos = ObtenerDocumentosVigentesParaListado(_documentoBL.ObtenerPorSolicitud(solicitudId), solicitud);
                AplicarContextoRevisionDocumental(documentos, solicitud);
                var esModoRevisionDocumental = EsModoRevisionDocumental(modo);
                var esModoVerDocumentacion = !esModoRevisionDocumental;
                var puedeRevisar = esModoRevisionDocumental && PuedeRevisarDocumentosSolicitud(solicitud, tieneUsuario ? usuarioId : 0, inspeccionVinculada);
                var puedeReabrir = esModoRevisionDocumental && PuedeReabrirRevisionDocumental(solicitud, tieneUsuario ? usuarioId : 0);

                System.Diagnostics.Trace.TraceInformation(
                    "[DOC_SOLICITUD] solicitudId=" + solicitudId +
                    "; modo=" + (string.IsNullOrWhiteSpace(modo) ? "auto" : modo.Trim()) +
                    "; usuarioId=" + (tieneUsuario ? usuarioId.ToString() : "0") +
                    "; roles=" + rolesActuales +
                    "; documentos=" + documentos.Count +
                    "; estadoSolicitud=" + (solicitud.Estado ?? string.Empty) +
                    "; codigoInspector=" + (inspeccionVinculada != null && inspeccionVinculada.CodigoInspector.HasValue
                        ? inspeccionVinculada.CodigoInspector.Value.ToString()
                        : "0") +
                    "; codigoInspeccion=" + (inspeccionVinculada != null ? inspeccionVinculada.CodigoInspeccion.ToString() : "0") +
                    "; puedeVer=" + puedeVer);

                if (!puedeVer)
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "No tiene permisos para ver los documentos de esta solicitud.");
                }

                var stats = CalcularEstadisticasDocumentos(documentos);

                ViewBag.Stats = stats;
                ViewBag.Estadisticas = stats;

                ViewBag.Solicitud = solicitud;
                ViewBag.SolicitudId = solicitudId;
                ViewBag.CodigoInspeccion = inspeccionVinculada != null ? (int?)inspeccionVinculada.CodigoInspeccion : null;
                ViewBag.PuedeRevisarDocumentos = puedeRevisar;
                ViewBag.PuedeReabrirDocumentos = puedeReabrir;
                ViewBag.ModoDocumentos = esModoRevisionDocumental ? "revision" : "ver";
                ViewBag.EsFaseInspectorDocumental = _solicitudAocrInfraBL.RequiereDecisionDocumentalInspector(solicitudId);
                ViewBag.OperadoraEae = ObtenerOperadoraEaeVisible(solicitud);
                ViewData["SolicitudId"] = solicitudId;

                if (inspeccionId.HasValue && inspeccionId.Value > 0)
                {
                    ViewBag.CodigoInspeccion = inspeccionId.Value;
                }

                var volver = ResolverUrlRetornoDocumentos(origen, solicitudId, inspeccionId);
                ViewBag.VolverUrl = volver.Url;
                ViewBag.VolverTexto = volver.Texto;
                ViewBag.SolicitudNumero = solicitud.NumeroSolicitud ?? solicitudId.ToString();

                return View(documentos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Documento/RevisarDocumentos — redirige a bandeja institucional (no expone listado global sin filtro).
        [AocrAuthorize(Modulo = "Documento", Accion = "RevisarDocumentos")]
        public ActionResult RevisarDocumentos()
        {
            return RedirectToAction("Index", "RevisionDocumental");
        }

        // GET: Documento/Detalle/5
        public ActionResult Detalle(int id)
        {
            var doc = _documentoBL.ObtenerPorId(id);
            if (doc == null) return RedirectToAction("RevisarDocumentos");
            return View(doc);
        }

        #endregion

        #region Subir y Descargar

        // Alias de compatibilidad: Documento/SubirDocumento
        [HttpGet]
        public ActionResult SubirDocumento(int? solicitudId)
        {
            if (!solicitudId.HasValue || solicitudId.Value <= 0)
            {
                TempData["Error"] = "Debe especificar una solicitud válida para subir documentos.";
                return RedirectToAction("Index", "SolicitudAOCR");
            }

            return RedirectToAction("Subir", new { solicitudId = solicitudId.Value });
        }

        // GET: Documento/Subir/5
        public ActionResult Subir(int? solicitudId)
        {
            if (!solicitudId.HasValue || solicitudId.Value <= 0)
            {
                TempData["Error"] = "Debe especificar una solicitud válida para subir documentos.";
                return RedirectToAction("Index", "SolicitudAOCR");
            }

            var solicitud = _solicitudDAO.ObtenerPorId(solicitudId.Value);
            int usuarioId;
            if (solicitud != null && TryObtenerUsuarioActualId(out usuarioId) && solicitud.CodigoUsuario == usuarioId)
            {
                string mensajeBloqueo;
                if (!new AocrPostPagoWorkflowService().PuedeRtAccederModuloSolicitud(solicitudId.Value, usuarioId, out mensajeBloqueo))
                {
                    TempData["Error"] = mensajeBloqueo;
                    return RedirectToAction("Index", "SolicitudAOCR");
                }
            }

            ViewBag.SolicitudId = solicitudId.Value;
            ViewBag.TiposDocumento = ObtenerTiposDocumento();
            return View(new Documento { CodigoSolicitud = solicitudId.Value });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Subir(int? solicitudId, string tipoDocumento, HttpPostedFileBase archivo, string observaciones)
        {
            string rutaCompleta = null;
            try
            {
                if (!solicitudId.HasValue || solicitudId.Value <= 0)
                {
                    TempData["Error"] = "Debe especificar una solicitud válida para subir documentos.";
                    return RedirectToAction("Index", "SolicitudAOCR");
                }

                string motivoAuth;
                if (!AocrPresentacionAuthorizationHelper.EsPermitido(HttpContext, "Documento", "Subir", out motivoAuth, solicitudId))
                {
                    TempData["Error"] = motivoAuth;
                    return RedirectToAction("Detalle", "SolicitudAOCR", new { id = solicitudId.Value });
                }

                var solicitud = _solicitudDAO.ObtenerPorId(solicitudId.Value);
                int usuarioId;
                if (solicitud != null && TryObtenerUsuarioActualId(out usuarioId) && solicitud.CodigoUsuario == usuarioId)
                {
                    string mensajeBloqueo;
                    if (!new AocrPostPagoWorkflowService().PuedeRtAccederModuloSolicitud(solicitudId.Value, usuarioId, out mensajeBloqueo))
                    {
                        TempData["Error"] = mensajeBloqueo;
                        return RedirectToAction("Index", "SolicitudAOCR");
                    }
                }

                // Guard institucional: el "Borrador AOCR" y el "AOCR generado" nunca pueden
                // subirse manualmente. La AOCR se genera automáticamente desde
                // SolicitudAOCR/GenerarAOCR una vez aprobado el informe técnico.
                var tipoNormalizado = (tipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
                if (tipoNormalizado == "BORRADOR_AOCR" || tipoNormalizado == "AOCR_GENERADO" || tipoNormalizado == "AOCR")
                {
                    TempData["Error"] = "La AOCR no puede subirse manualmente. Use la opción 'Generar AOCR' en el detalle de la solicitud.";
                    return RedirectToAction("Detalle", "SolicitudAOCR", new { id = solicitudId.Value });
                }

                if (archivo == null || archivo.ContentLength == 0)
                {
                    TempData["Error"] = "Seleccione un archivo válido.";
                    return RedirectToAction("Subir", new { solicitudId = solicitudId.Value });
                }

                var options = new FileUploadOptions
                {
                    BasePath = _rutaDocumentos,
                    Subfolder = string.Empty,
                    AllowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                    AllowedContentTypes = new[] { "application/pdf", "image/jpeg", "image/png" },
                    MaxSizeMb = 10,
                    ValidateMagicBytes = true
                };

                string error;
                FileUploadResult result;
                if (!FileUploadService.TrySave(archivo, options, out result, out error))
                {
                    TempData["Error"] = error ?? "No se pudo guardar el archivo.";
                    return RedirectToAction("Subir", new { solicitudId = solicitudId.Value });
                }

                rutaCompleta = Path.Combine(_rutaDocumentos, result.StoredName);

                // 2. Preparar objeto para la BL
                var doc = new Documento
                {
                    CodigoSolicitud = solicitudId.Value,
                    TipoDocumento = tipoDocumento,
                    NombreArchivo = Path.GetFileName(archivo.FileName),
                    RutaArchivo = rutaCompleta,
                    TamanioArchivo = archivo.ContentLength,
                    Observaciones = observaciones,
                    UsuarioRegistro = User.Identity.Name ?? "System"
                };

                // 3. La BL valida reglas de negocio y guarda en BD
                if (_documentoBL.Crear(doc))
                {
                    new AocrPostPagoWorkflowService().MarcarDocumentosHabilitantesCargados(
                        solicitudId.Value,
                        User != null && User.Identity != null ? User.Identity.Name : "RT");

                    TempData["Exito"] = "Documento subido correctamente.";
                    return RedirectToAction("Lista", new { solicitudId = solicitudId.Value });
                }
                else
                {
                    // Si la BL dice que no (reglas de negocio), borramos el archivo físico
                    if (System.IO.File.Exists(rutaCompleta)) System.IO.File.Delete(rutaCompleta);
                    TempData["Error"] = "No se pudo guardar el documento.";
                    return RedirectToAction("Subir", new { solicitudId = solicitudId.Value });
                }
            }
            catch (Exception ex)
            {
                // Si hubo excepción en la BL (ej: extensión no permitida), borramos el archivo
                if (rutaCompleta != null && System.IO.File.Exists(rutaCompleta))
                    System.IO.File.Delete(rutaCompleta);

                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Subir", new { solicitudId = solicitudId.HasValue ? solicitudId.Value : 0 });
            }
        }

        public ActionResult Descargar(int id, bool vistaPrevia = false)
        {
            try
            {
                var doc = _documentoBL.ObtenerPorId(id); // Usamos BL
                if (doc == null) return HttpNotFound();

                int usuarioId;
                if (!TryObtenerUsuarioActualId(out usuarioId))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "Sesión expirada o usuario no identificado.");
                }

                var solicitud = _solicitudDAO.ObtenerPorId(doc.CodigoSolicitud);
                if (solicitud == null)
                {
                    return HttpNotFound();
                }

                string motivoAuth;
                if (!AocrPresentacionAuthorizationHelper.EsPermitido(HttpContext, "Documento", "Descargar", out motivoAuth, doc.CodigoSolicitud))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, motivoAuth);
                }

                Inspeccion inspeccionVinculada;
                string rolesActuales;
                if (!PuedeVerDocumentosSolicitud(solicitud, usuarioId, out inspeccionVinculada, out rolesActuales))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "No tiene permisos para acceder a este documento.");
                }

                var rutaFisica = ResolverRutaFisicaDocumento(doc);

                if (string.IsNullOrWhiteSpace(rutaFisica) || !EsRutaDocumentoPermitida(rutaFisica) || !System.IO.File.Exists(rutaFisica))
                {
                    TempData["Error"] = "El archivo físico no existe en el servidor.";
                    return RedirectToAction("Lista", new { solicitudId = doc.CodigoSolicitud });
                }

                byte[] bytes = System.IO.File.ReadAllBytes(rutaFisica);
                var mimeType = ObtenerMimeTypeDocumento(doc.NombreArchivo ?? rutaFisica ?? string.Empty);
                if (vistaPrevia && (string.Equals(mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                    || mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
                {
                    Response.Headers["X-Content-Type-Options"] = "nosniff";
                    return File(bytes, mimeType);
                }

                return File(bytes, string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType, doc.NombreArchivo);
            }
            catch
            {
                return HttpNotFound();
            }
        }

        #endregion

        #region Gestión (Eliminar / Aprobar / Rechazar)

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Eliminar(int id)
        {
            try
            {
                // La BL se encarga de borrar el archivo físico y el registro BD
                if (_documentoBL.Eliminar(id))
                {
                    TempData["Exito"] = "Documento eliminado.";
                }
                else
                {
                    TempData["Error"] = "No se pudo eliminar.";
                }
                return RedirectToAction("RevisarDocumentos"); // O volver al historial
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("RevisarDocumentos");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult CambiarEstado(int id, string estado, string observaciones)
        {
            try
            {
                bool resultado = false;
                string usuario = User.Identity.Name ?? "System";

                // Usamos los métodos específicos de la BL
                if (estado.ToUpper() == "APROBADO")
                {
                    resultado = _documentoBL.Aprobar(id, usuario, observaciones);
                }
                else if (estado.ToUpper() == "RECHAZADO")
                {
                    resultado = _documentoBL.Rechazar(id, usuario, observaciones);
                }
                else
                {
                    return Json(new { success = false, mensaje = "Estado no válido" });
                }

                return Json(new { success = resultado, mensaje = resultado ? "Actualizado" : "Error al actualizar" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AceptarDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion)
        {
            return ProcesarRevisionDocumentoSolicitud(idDocumento, codigoSolicitud, codigoInspeccion, "ACEPTADO", null);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DevolverDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion, string motivoDevolucion)
        {
            return ProcesarRevisionDocumentoSolicitud(idDocumento, codigoSolicitud, codigoInspeccion, "DEVUELTO", motivoDevolucion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ReabrirDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion)
        {
            try
            {
                int usuarioId;
                if (!TryObtenerUsuarioActualId(out usuarioId))
                {
                    return JsonError(401, "Sesión expirada. Vuelva a iniciar sesión.");
                }

                var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
                if (solicitud == null)
                {
                    return JsonError(404, "La solicitud no existe.");
                }

                Inspeccion inspeccionVinculada;
                string rolesActuales;
                var puedeVer = PuedeVerDocumentosSolicitud(solicitud, usuarioId, out inspeccionVinculada, out rolesActuales);
                if (!puedeVer || !PuedeReabrirRevisionDocumental(solicitud, usuarioId))
                {
                    return JsonError(403, "No tiene permisos para reabrir la revisión de este documento.");
                }

                if (codigoInspeccion.HasValue && inspeccionVinculada != null && inspeccionVinculada.CodigoInspeccion != codigoInspeccion.Value)
                {
                    return JsonError(400, "La inspección no corresponde a la solicitud seleccionada.");
                }

                var documentos = ObtenerDocumentosVigentesParaListado(_documentoBL.ObtenerPorSolicitud(codigoSolicitud), solicitud);
                AplicarContextoRevisionDocumental(documentos, solicitud);

                var documento = documentos.FirstOrDefault(d => d != null && d.CodigoDocumento == idDocumento);
                if (documento == null)
                {
                    return JsonError(404, "El documento no pertenece a la solicitud o ya fue reemplazado por una nueva versión.");
                }

                var estadoActual = ObtenerEstadoRevisionNormalizado(documento);
                if (estadoActual == "PENDIENTE")
                {
                    return Json(new
                    {
                        success = true,
                        message = "El documento ya se encuentra pendiente de revisión.",
                        estado = "PENDIENTE",
                        documento = ConstruirDocumentoResponse(documento, true, true),
                        contadores = CalcularEstadisticasDocumentos(documentos)
                    });
                }

                var usuarioVisible = ObtenerNombreVisibleUsuarioActual(usuarioId);
                documento.Estado = "CARGADO";
                documento.Validado = false;
                documento.Observaciones = null;
                documento.FechaValidacion = null;
                documento.ValidadoPor = null;
                documento.UsuarioRegistro = usuarioVisible;

                if (!_documentoDAO.Actualizar(documento))
                {
                    return JsonError(500, "No se pudo reabrir la revisión del documento.");
                }

                _solicitudAocrInfraBL.RegistrarRevisionDocumental(
                    codigoSolicitud,
                    idDocumento,
                    "PENDIENTE_REVISION",
                    "Documento reabierto para nueva revisión.",
                    usuarioId,
                    usuarioVisible);
                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    codigoSolicitud,
                    idDocumento,
                    "DOCUMENTO_REABIERTO",
                    "Documento reabierto para nueva revisión por " + usuarioVisible + ".",
                    usuarioId,
                    usuarioVisible);

                var documentosActualizados = ObtenerDocumentosVigentesParaListado(_documentoBL.ObtenerPorSolicitud(codigoSolicitud), solicitud);
                AplicarContextoRevisionDocumental(documentosActualizados, solicitud);
                var documentoActualizado = documentosActualizados.FirstOrDefault(d => d != null && d.CodigoDocumento == idDocumento) ?? documento;

                return Json(new
                {
                    success = true,
                    message = "El documento quedó pendiente para una nueva revisión.",
                    estado = "PENDIENTE",
                    documento = ConstruirDocumentoResponse(documentoActualizado, true, true),
                    contadores = CalcularEstadisticasDocumentos(documentosActualizados)
                });
            }
            catch (Exception ex)
            {
                return JsonError(500, "No se pudo reabrir la revisión del documento. " + ex.Message);
            }
        }

        // Vista Editar solo para cambiar metadatos si fuera necesario
        public ActionResult Editar(int id)
        {
            var doc = _documentoBL.ObtenerPorId(id);
            if (doc == null) return HttpNotFound();

            ViewBag.TiposDocumento = ObtenerTiposDocumento();
            return View(doc);
        }

        #endregion

        #region Auxiliares
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

            var codigoSesion = (Session["CodigoUsuario"] ?? string.Empty).ToString().Trim();
            if (!string.IsNullOrWhiteSpace(codigoSesion))
            {
                if (int.TryParse(codigoSesion, out idUsuario) && idUsuario > 0)
                {
                    Session["IdUsuario"] = idUsuario;
                    Session["UserId"] = idUsuario;
                    return true;
                }

                try
                {
                    var usuarioPorCodigo = UsuarioDAO.ObtenerPorNombreUsuario(codigoSesion);
                    if (usuarioPorCodigo != null && usuarioPorCodigo.Id > 0)
                    {
                        idUsuario = usuarioPorCodigo.Id;
                        Session["IdUsuario"] = idUsuario;
                        Session["UserId"] = idUsuario;
                        return true;
                    }
                }
                catch
                {
                    // Se ignora para continuar con otros orígenes de identidad.
                }
            }

            var login = User != null && User.Identity != null ? User.Identity.Name : string.Empty;
            if (!string.IsNullOrWhiteSpace(login))
            {
                try
                {
                    var usuarioPorLogin = UsuarioDAO.ObtenerPorNombreUsuario(login);
                    if (usuarioPorLogin != null && usuarioPorLogin.Id > 0)
                    {
                        idUsuario = usuarioPorLogin.Id;
                        Session["IdUsuario"] = idUsuario;
                        Session["UserId"] = idUsuario;
                        return true;
                    }
                }
                catch
                {
                    // Se ignora para no romper navegación por un fallo de resolución puntual.
                }
            }

            return false;
        }

        private JsonResult ProcesarRevisionDocumentoSolicitud(int idDocumento, int codigoSolicitud, int? codigoInspeccion, string decision, string observacion)
        {
            try
            {
                int usuarioId;
                if (!TryObtenerUsuarioActualId(out usuarioId))
                {
                    return JsonError(401, "Sesión expirada. Vuelva a iniciar sesión.");
                }

                var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
                if (solicitud == null)
                {
                    return JsonError(404, "La solicitud no existe.");
                }

                Inspeccion inspeccionVinculada;
                string rolesActuales;
                var puedeVer = PuedeVerDocumentosSolicitud(solicitud, usuarioId, out inspeccionVinculada, out rolesActuales);
                if (!puedeVer || !PuedeRevisarDocumentosSolicitud(solicitud, usuarioId, inspeccionVinculada))
                {
                    return JsonError(403, "No tiene permisos para revisar documentos de esta solicitud.");
                }

                if (inspeccionVinculada != null && inspeccionVinculada.CodigoInspeccion > 0)
                {
                    string mensajeBloqueo;
                    if (!new AocrPostPagoWorkflowService().PuedeInspectorIniciarRevisionDocumental(inspeccionVinculada.CodigoInspeccion, out mensajeBloqueo))
                    {
                        return JsonError(409, mensajeBloqueo);
                    }
                }

                if (codigoInspeccion.HasValue && inspeccionVinculada != null && inspeccionVinculada.CodigoInspeccion != codigoInspeccion.Value)
                {
                    return JsonError(400, "La inspección no corresponde a la solicitud seleccionada.");
                }

                var documentos = ObtenerDocumentosVigentesParaListado(_documentoBL.ObtenerPorSolicitud(codigoSolicitud), solicitud);
                AplicarContextoRevisionDocumental(documentos, solicitud);

                var documento = documentos.FirstOrDefault(d => d != null && d.CodigoDocumento == idDocumento);
                if (documento == null)
                {
                    return JsonError(404, "El documento no pertenece a la solicitud o ya fue reemplazado por una nueva versión.");
                }

                var decisionNormalizada = NormalizarDecisionRevision(decision);
                if (decisionNormalizada != "ACEPTADO" && decisionNormalizada != "DEVUELTO")
                {
                    return JsonError(400, "La decisión documental no es válida.");
                }

                var observacionNormalizada = (observacion ?? string.Empty).Trim();
                if (decisionNormalizada == "DEVUELTO" && observacionNormalizada.Length < 10)
                {
                    return JsonError(400, "Debe ingresar un motivo de devolución con al menos 10 caracteres.");
                }

                var estadoActual = ObtenerEstadoRevisionNormalizado(documento);
                if (decisionNormalizada == "ACEPTADO" && (estadoActual == "RECHAZADO" || estadoActual == "OBSERVADO"))
                {
                    return JsonError(409, "El documento fue devuelto u observado. Debe cargarse una nueva versión o reabrirse la revisión antes de aceptarlo.");
                }

                if (decisionNormalizada == "DEVUELTO" && estadoActual == "APROBADO" && !PuedeReabrirRevisionDocumental(solicitud, usuarioId))
                {
                    return JsonError(409, "El documento ya fue aceptado. Solo Coordinación o Administrador pueden reabrirlo antes de una nueva devolución.");
                }

                var usuarioVisible = ObtenerNombreVisibleUsuarioActual(usuarioId);
                documento.Estado = decisionNormalizada == "ACEPTADO" ? "APROBADO" : "RECHAZADO";
                documento.Validado = decisionNormalizada == "ACEPTADO";
                documento.Observaciones = decisionNormalizada == "ACEPTADO" ? null : observacionNormalizada;
                documento.FechaValidacion = DateTime.Now;
                documento.ValidadoPor = usuarioVisible;
                documento.UsuarioRegistro = usuarioVisible;

                if (!_documentoDAO.Actualizar(documento))
                {
                    return JsonError(500, "No se pudo registrar la revisión del documento.");
                }

                _solicitudAocrInfraBL.RegistrarRevisionDocumental(
                    codigoSolicitud,
                    idDocumento,
                    decisionNormalizada,
                    observacionNormalizada,
                    usuarioId,
                    usuarioVisible);
                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    codigoSolicitud,
                    idDocumento,
                    decisionNormalizada == "ACEPTADO" ? "DOCUMENTO_ACEPTADO" : "DOCUMENTO_DEVUELTO",
                    decisionNormalizada == "ACEPTADO"
                        ? "Documento " + ObtenerEtiquetaDocumento(documento) + " aceptado por " + usuarioVisible + "."
                        : "Documento " + ObtenerEtiquetaDocumento(documento) + " devuelto por " + usuarioVisible + ". Motivo: " + observacionNormalizada,
                    usuarioId,
                    usuarioVisible);

                if (decisionNormalizada == "DEVUELTO")
                {
                    NotificarDocumentoDevueltoEnSistema(solicitud, documento, observacionNormalizada, usuarioId, usuarioVisible);
                }

                var documentosActualizados = ObtenerDocumentosVigentesParaListado(_documentoBL.ObtenerPorSolicitud(codigoSolicitud), solicitud);
                AplicarContextoRevisionDocumental(documentosActualizados, solicitud);
                var documentoActualizado = documentosActualizados.FirstOrDefault(d => d != null && d.CodigoDocumento == idDocumento) ?? documento;
                var puedeReabrir = PuedeReabrirRevisionDocumental(solicitud, usuarioId);
                var contadores = CalcularEstadisticasDocumentos(documentosActualizados);
                var totalDocumentos = Convert.ToInt32(contadores["Total"]);
                var pendientesDocumentos = Convert.ToInt32(contadores["Pendientes"]);
                var rechazadosDocumentos = Convert.ToInt32(contadores["Rechazados"]);
                var autoAbrirLvEae = decisionNormalizada == "ACEPTADO"
                    && inspeccionVinculada != null
                    && inspeccionVinculada.CodigoInspeccion > 0
                    && totalDocumentos > 0
                    && pendientesDocumentos == 0
                    && rechazadosDocumentos == 0;
                var redirectUrl = autoAbrirLvEae
                    ? Url.Action("Detalle", "Inspeccion", new { id = inspeccionVinculada.CodigoInspeccion, lvAutoFlow = "open" })
                    : string.Empty;

                return Json(new
                {
                    success = true,
                    message = decisionNormalizada == "ACEPTADO"
                        ? (autoAbrirLvEae
                            ? "Documentación completada. Abriendo automáticamente la LV/EAE."
                            : "Documento aceptado correctamente.")
                        : "Documento devuelto correctamente.",
                    estado = documentoActualizado.EstadoRevisionVisible ?? ObtenerEstadoDocumentoVisible(documentoActualizado),
                    documento = ConstruirDocumentoResponse(documentoActualizado, true, puedeReabrir),
                    contadores = contadores,
                    redirectUrl = redirectUrl,
                    autoAbrirLvEae = autoAbrirLvEae
                });
            }
            catch (Exception ex)
            {
                return JsonError(500, "No se pudo registrar la revisión del documento. " + ex.Message);
            }
        }

        private bool PuedeVerDocumentosSolicitud(SolicitudAOCR solicitud, int usuarioId, out Inspeccion inspeccionVinculada, out string rolesActuales)
        {
            var roles = ObtenerRolesActuales();
            rolesActuales = string.Join(",", roles);

            var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            inspeccionVinculada = inspecciones
                .Where(i => i != null && i.CodigoInspeccion > 0)
                .OrderByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault();

            var esAdmin = roles.Any(r => RoleGroupingHelper.IsAdministrador(r));
            var esCoordinacion = roles.Any(r => RoleGroupingHelper.IsCoordinacion(r));
            var esPropietario = usuarioId > 0 && solicitud != null && solicitud.CodigoUsuario == usuarioId;
            var identidadInspector = ConstruirIdentidadInspectorActual(usuarioId);
            var esInspectorAsignado = EsInspectorAsignadoActual(solicitud, inspecciones, identidadInspector);

            return esAdmin || esCoordinacion || esPropietario || esInspectorAsignado;
        }

        private bool PuedeRevisarDocumentosSolicitud(SolicitudAOCR solicitud, int usuarioId, Inspeccion inspeccionVinculada)
        {
            if (solicitud == null)
            {
                return false;
            }

            var roles = ObtenerRolesActuales();
            var esAdmin = roles.Any(r => RoleGroupingHelper.IsAdministrador(r));
            if (esAdmin)
            {
                return true;
            }

            var estadoRevision = _solicitudAocrInfraBL.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
                ?? new EstadoRevisionDocumental();
            var inspecciones = _solicitudAocrInfraBL.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            var identidadInspector = ConstruirIdentidadInspectorActual(usuarioId);
            var esInspectorAsignado = EsInspectorAsignadoActual(solicitud, inspecciones, identidadInspector);

            Trace.TraceInformation(
                "[DOC_FLOW] Accion=EVALUAR_REVISION_DOCUMENTO; SolicitudId=" + solicitud.CodigoSolicitud +
                "; UsuarioId=" + usuarioId +
                "; ResponsableActual=" + (estadoRevision.ResponsableActual ?? string.Empty) +
                "; Flujo=" + (estadoRevision.FlujoDocumentalCodigo ?? string.Empty) +
                "; EsInspectorAsignado=" + esInspectorAsignado);

            if (SolicitudAocrInfraBL.EsRevisionDocumentalPreAsignacion(solicitud, inspecciones))
            {
                var esInspector = roles.Any(r => RoleGroupingHelper.IsInspectorTecnico(r))
                    || RoleGroupingHelper.HasAnyRawRole(roles, "Inspector", "InspectorTecnico");
                var esCoordinacion = roles.Any(r => RoleGroupingHelper.IsCoordinacion(r));
                return esInspector || esCoordinacion;
            }

            if (esInspectorAsignado)
            {
                return true;
            }

            return esInspectorAsignado && estadoRevision.VisibleEnBandejaInspector;
        }

        private bool PuedeReabrirRevisionDocumental(SolicitudAOCR solicitud, int usuarioId)
        {
            if (solicitud == null || usuarioId <= 0)
            {
                return false;
            }

            var roles = ObtenerRolesActuales();
            return roles.Any(r => RoleGroupingHelper.IsAdministrador(r))
                   || roles.Any(r => RoleGroupingHelper.IsCoordinacion(r))
                   || (User != null && User.IsInRole("CoordinadorInspecciones"));
        }

        private InspectorIdentityContext ConstruirIdentidadInspectorActual(int usuarioId)
        {
            var ids = new HashSet<int>();
            var identificadores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (usuarioId > 0)
            {
                ids.Add(usuarioId);
                AgregarIdentificadorInspector(identificadores, usuarioId.ToString());
            }

            AgregarIdentificadorInspector(identificadores, (Session["CodigoUsuario"] ?? string.Empty).ToString());
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                AgregarIdentificadorInspector(identificadores, User.Identity.Name);
            }

            try
            {
                var usuarioInternoRtDao = new UsuarioInternoRTDAO();
                var inspectorActual = usuarioId > 0
                    ? usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(usuarioId)
                    : null;

                if (inspectorActual == null)
                {
                    var codigoUsuario = (Session["CodigoUsuario"] ?? string.Empty).ToString();
                    if (!string.IsNullOrWhiteSpace(codigoUsuario))
                    {
                        inspectorActual = usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(codigoUsuario)
                            ?? usuarioInternoRtDao.ObtenerInspectorAsignableActivo(codigoUsuario);
                    }
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                        AgregarIdentificadorInspector(identificadores, inspectorActual.UsuarioId.Value.ToString());
                    }

                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                        AgregarIdentificadorInspector(identificadores, inspectorActual.TecnicoId.Value.ToString());
                    }

                    AgregarIdentificadorInspector(identificadores, inspectorActual.CodigoUsuario);
                    AgregarIdentificadorInspector(identificadores, inspectorActual.Identificacion);
                    AgregarIdentificadorInspector(identificadores, inspectorActual.UsuarioLogin);
                }
            }
            catch
            {
            }

            return new InspectorIdentityContext
            {
                Ids = ids,
                Identificadores = identificadores
            };
        }

        private static bool EsInspectorAsignadoActual(SolicitudAOCR solicitud, IEnumerable<Inspeccion> inspecciones, InspectorIdentityContext identidad)
        {
            if (identidad == null)
            {
                return false;
            }

            if (solicitud != null)
            {
                if (solicitud.CodigoTecnico.HasValue && identidad.Ids.Contains(solicitud.CodigoTecnico.Value))
                {
                    return true;
                }

                if (CoincideIdentificadorInspector(solicitud.TecnicoResponsableCedula, identidad.Identificadores)
                    || CoincideIdentificadorInspector(solicitud.InspectorApoyoCedula, identidad.Identificadores))
                {
                    return true;
                }
            }

            return (inspecciones ?? Enumerable.Empty<Inspeccion>())
                .Any(i => i != null
                    && ((i.CodigoInspector.HasValue && identidad.Ids.Contains(i.CodigoInspector.Value))
                        || CoincideIdentificadorInspector(i.InspectorPrincipalCedula, identidad.Identificadores)
                        || CoincideIdentificadorInspector(i.InspectorApoyoCedula, identidad.Identificadores)));
        }

        private static bool CoincideIdentificadorInspector(string valor, HashSet<string> identificadores)
        {
            return !string.IsNullOrWhiteSpace(valor)
                && identificadores != null
                && identificadores.Contains(valor.Trim().ToUpperInvariant());
        }

        private static void AgregarIdentificadorInspector(HashSet<string> identificadores, string valor)
        {
            if (identificadores == null || string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            identificadores.Add(valor.Trim().ToUpperInvariant());
        }

        private static bool EsModoRevisionDocumental(string modo)
        {
            var valor = (modo ?? string.Empty).Trim();
            return string.Equals(valor, "revision", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(valor, "revisar", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(valor, "revision-documental", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class DocumentoRetornoNavegacion
        {
            public string Url { get; set; }
            public string Texto { get; set; }
        }

        private DocumentoRetornoNavegacion ResolverUrlRetornoDocumentos(string origen, int solicitudId, int? inspeccionId)
        {
            var origenNormalizado = (origen ?? string.Empty).Trim().ToLowerInvariant();

            if (string.Equals(origenNormalizado, "revision-documental", StringComparison.OrdinalIgnoreCase))
            {
                return new DocumentoRetornoNavegacion
                {
                    Url = Url.Action("Index", "RevisionDocumental"),
                    Texto = "Volver a revisión documental"
                };
            }

            if (string.Equals(origenNormalizado, "inspeccion", StringComparison.OrdinalIgnoreCase))
            {
                return new DocumentoRetornoNavegacion
                {
                    Url = Url.Action("Index", "Inspeccion"),
                    Texto = "Volver a inspecciones"
                };
            }

            if (string.Equals(origenNormalizado, "inspeccion-detalle", StringComparison.OrdinalIgnoreCase)
                && inspeccionId.HasValue
                && inspeccionId.Value > 0)
            {
                return new DocumentoRetornoNavegacion
                {
                    Url = Url.Action("Detalle", "Inspeccion", new { id = inspeccionId.Value }),
                    Texto = "Volver a la inspección"
                };
            }

            if (string.Equals(origenNormalizado, "solicitud-detalle", StringComparison.OrdinalIgnoreCase)
                && solicitudId > 0)
            {
                return new DocumentoRetornoNavegacion
                {
                    Url = Url.Action("Detalle", "SolicitudAOCR", new { id = solicitudId }),
                    Texto = "Volver al detalle de solicitud"
                };
            }

            if (inspeccionId.HasValue && inspeccionId.Value > 0)
            {
                return new DocumentoRetornoNavegacion
                {
                    Url = Url.Action("Index", "Inspeccion"),
                    Texto = "Volver a inspecciones"
                };
            }

            return new DocumentoRetornoNavegacion
            {
                Url = solicitudId > 0
                    ? Url.Action("Detalle", "SolicitudAOCR", new { id = solicitudId })
                    : Url.Action("Index", "SolicitudAOCR"),
                Texto = "Volver al detalle de solicitud"
            };
        }

        private IList<string> ObtenerRolesActuales()
        {
            var rolesCrudos = new List<string>();
            var rolSesion = (Session["Rol"] ?? string.Empty).ToString();
            if (!string.IsNullOrWhiteSpace(rolSesion))
            {
                rolesCrudos.Add(rolSesion);
            }

            foreach (var rol in new[] { "Administrador", "Coordinador", "Coordinacion", "CoordinadorInspecciones", "Inspector", "Solicitante", "Operador" })
            {
                if (User != null && User.IsInRole(rol))
                {
                    rolesCrudos.Add(rol);
                }
            }

            return RoleGroupingHelper.BuildUnifiedRoles(rolesCrudos);
        }

        private static IDictionary<string, object> CalcularEstadisticasDocumentos(IEnumerable<Documento> documentos)
        {
            var lista = (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToList();

            var tamanioTotal = lista.Sum(d => d.TamanioArchivo ?? 0L);
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "Total", lista.Count },
                { "Pendientes", lista.Count(d => ObtenerEstadoRevisionNormalizado(d) == "PENDIENTE") },
                { "Aprobados", lista.Count(d => ObtenerEstadoRevisionNormalizado(d) == "APROBADO") },
                { "Rechazados", lista.Count(d => ObtenerEstadoRevisionNormalizado(d) == "RECHAZADO" || ObtenerEstadoRevisionNormalizado(d) == "OBSERVADO") },
                { "TamanioTotal", tamanioTotal > int.MaxValue ? int.MaxValue : (int)tamanioTotal }
            };
        }

        private static string ObtenerEstadoRevisionNormalizado(Documento documento)
        {
            var decision = NormalizarDecisionRevision(documento != null ? documento.DecisionRevision : null);
            switch (decision)
            {
                case "ACEPTADO":
                    return "APROBADO";
                case "DEVUELTO":
                    return "RECHAZADO";
                case "OBSERVADO":
                    return "OBSERVADO";
                case "PENDIENTE_REVISION":
                    return "PENDIENTE";
            }

            return NormalizarEstadoDocumento(documento != null ? documento.Estado : null);
        }

        private static string NormalizarTextoVisible(string value)
        {
            return CapaPresentacion.Helpers.VisibleTextHelper.Normalize(value);
        }

        private static bool EsEstadoDocumentoPendiente(string estado)
        {
            return NormalizarEstadoDocumento(estado) == "PENDIENTE";
        }

        private static bool EsEstadoDocumentoAprobado(string estado)
        {
            return NormalizarEstadoDocumento(estado) == "APROBADO";
        }

        private static bool EsEstadoDocumentoDevueltoODocumentado(string estado)
        {
            return NormalizarEstadoDocumento(estado) == "RECHAZADO";
        }

        private static string NormalizarDecisionRevision(string decision)
        {
            var valor = (decision ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace('Á', 'A')
                .Replace('É', 'E')
                .Replace('Í', 'I')
                .Replace('Ó', 'O')
                .Replace('Ú', 'U')
                .Replace('_', ' ');

            while (valor.Contains("  "))
            {
                valor = valor.Replace("  ", " ");
            }

            switch (valor)
            {
                case "ACEPTADO":
                case "APROBADO":
                case "VALIDADO":
                    return "ACEPTADO";
                case "DEVUELTO":
                case "RECHAZADO":
                    return "DEVUELTO";
                case "OBSERVADO":
                case "MODIFICACION SOLICITADA":
                case "MODIFICACION_SOLICITADA":
                    return "OBSERVADO";
                case "PENDIENTE REVISION":
                case "PENDIENTE":
                case "CARGADO":
                case "SIN REVISAR":
                    return "PENDIENTE_REVISION";
                default:
                    return string.Empty;
            }
        }

        private static string NormalizarEstadoDocumento(string estado)
        {
            var valor = (estado ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace('_', ' ');

            while (valor.Contains("  "))
            {
                valor = valor.Replace("  ", " ");
            }

            switch (valor)
            {
                case "APROBADO":
                case "ACEPTADO":
                case "VALIDADO":
                    return "APROBADO";
                case "DEVUELTO":
                case "OBSERVADO":
                case "RECHAZADO":
                case "SUBSANACION":
                    return "RECHAZADO";
                case "":
                case "CARGADO":
                case "PENDIENTE":
                case "SIN REVISAR":
                case "EN REVISION":
                case "REGISTRADO":
                default:
                    return "PENDIENTE";
            }
        }

        private string ResolverRutaFisicaDocumento(Documento documento)
        {
            var ruta = documento != null ? (documento.RutaArchivo ?? documento.RutaGuardada) : null;
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return null;
            }

            ruta = ruta.Trim();
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

        private bool EsRutaDocumentoPermitida(string rutaFisica)
        {
            if (string.IsNullOrWhiteSpace(rutaFisica))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(rutaFisica);
            var basesPermitidas = new List<string>();

            if (!string.IsNullOrWhiteSpace(_rutaDocumentos))
            {
                basesPermitidas.Add(Path.GetFullPath(_rutaDocumentos));
            }

            var baseAppData = Server != null ? Server.MapPath("~/App_Data") : null;
            if (!string.IsNullOrWhiteSpace(baseAppData))
            {
                basesPermitidas.Add(Path.GetFullPath(baseAppData));
            }

            return basesPermitidas
                .Where(baseDir => !string.IsNullOrWhiteSpace(baseDir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Any(baseDir =>
                {
                    var normalizedBase = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return string.Equals(fullPath, normalizedBase, StringComparison.OrdinalIgnoreCase)
                        || fullPath.StartsWith(normalizedBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || fullPath.StartsWith(normalizedBase + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                });
        }

        private static string ObtenerMimeTypeDocumento(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty);
            switch ((extension ?? string.Empty).Trim().ToLowerInvariant())
            {
                case ".pdf":
                    return "application/pdf";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                default:
                    return "application/octet-stream";
            }
        }

        private List<Documento> ObtenerDocumentosVigentesParaListado(IEnumerable<Documento> documentos, SolicitudAOCR solicitud)
        {
            var operadora = ObtenerOperadoraEaeVisible(solicitud);

            return (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(d => d.Version ?? 0)
                    .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                    .ThenByDescending(d => d.CodigoDocumento)
                    .First())
                .Select(d =>
                {
                    d.OperadoraEae = operadora;
                    return d;
                })
                .OrderBy(d => d.OperadoraEae ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(d => d.FechaCarga ?? DateTime.MinValue)
                .ThenBy(d => d.TipoDocumento ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AplicarContextoRevisionDocumental(IEnumerable<Documento> documentos, SolicitudAOCR solicitud)
        {
            var lista = (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToList();

            if (solicitud == null || lista.Count == 0)
            {
                return;
            }

            var faseInspector = _solicitudAocrInfraBL.RequiereDecisionDocumentalInspector(solicitud.CodigoSolicitud);
            var revisiones = faseInspector
                ? _solicitudAocrInfraBL.ObtenerUltimosDetallesRevisionInspectorPorSolicitud(solicitud.CodigoSolicitud)
                : _solicitudAocrInfraBL.ObtenerUltimosDetallesRevisionPorSolicitud(solicitud.CodigoSolicitud);
            var operadora = ObtenerOperadoraEaeVisible(solicitud);

            foreach (var documento in lista)
            {
                documento.OperadoraEae = operadora;

                RevisionDocumentalDetalle revision;
                if (revisiones != null && revisiones.TryGetValue(documento.CodigoDocumento, out revision) && revision != null)
                {
                    documento.DecisionRevision = NormalizarDecisionRevision(revision.Decision);
                    documento.ObservacionRevision = NormalizarTextoVisible(string.IsNullOrWhiteSpace(revision.Observacion)
                        ? documento.Observaciones
                        : revision.Observacion);
                    documento.FechaRevision = revision.FechaRevision ?? documento.FechaValidacion;
                    documento.CodigoUsuarioRevisor = revision.CodigoUsuarioRevisor;
                    documento.NombreUsuarioRevisor = !string.IsNullOrWhiteSpace(revision.NombreUsuarioRevisor)
                        ? revision.NombreUsuarioRevisor.Trim()
                        : ((!string.IsNullOrWhiteSpace(revision.CreatedBy) ? revision.CreatedBy.Trim() : (documento.ValidadoPor ?? string.Empty).Trim()));
                }
                else if (faseInspector)
                {
                    documento.DecisionRevision = string.Empty;
                    documento.ObservacionRevision = string.Empty;
                    documento.FechaRevision = null;
                    documento.CodigoUsuarioRevisor = null;
                    documento.NombreUsuarioRevisor = string.Empty;
                }
                else
                {
                    documento.DecisionRevision = NormalizarDecisionRevision(documento.Estado);
                    documento.ObservacionRevision = NormalizarTextoVisible(documento.Observaciones);
                    documento.FechaRevision = documento.FechaValidacion;
                    documento.NombreUsuarioRevisor = (documento.ValidadoPor ?? string.Empty).Trim();
                }

                documento.EstadoRevisionVisible = ObtenerEstadoDocumentoVisible(documento, faseInspector);
            }
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

        private sealed class InspectorIdentityContext
        {
            public HashSet<int> Ids { get; set; }
            public HashSet<string> Identificadores { get; set; }
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

        private static string ObtenerEstadoDocumentoVisible(Documento documento, bool faseInspector = false)
        {
            var decision = NormalizarDecisionRevision(documento != null ? documento.DecisionRevision : null);
            switch (decision)
            {
                case "ACEPTADO":
                    return "ACEPTADO";
                case "DEVUELTO":
                    return "DEVUELTO";
                case "OBSERVADO":
                    return "OBSERVADO";
            }

            if (faseInspector)
            {
                return "PENDIENTE";
            }

            switch (NormalizarEstadoDocumento(documento != null ? documento.Estado : null))
            {
                case "APROBADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                    return "DEVUELTO";
                default:
                    return "PENDIENTE";
            }
        }

        private static string ObtenerBadgeDocumentoCss(Documento documento)
        {
            switch (ObtenerEstadoDocumentoVisible(documento))
            {
                case "ACEPTADO":
                    return "badge bg-success";
                case "DEVUELTO":
                    return "badge bg-danger";
                case "OBSERVADO":
                    return "badge bg-warning text-dark";
                default:
                    return "badge bg-secondary";
            }
        }

        private string ObtenerNombreVisibleUsuarioActual(int usuarioId)
        {
            var nombreSesion = (Session["NombreUsuario"] ?? Session["NombreCompleto"] ?? string.Empty).ToString().Trim();
            if (!string.IsNullOrWhiteSpace(nombreSesion))
            {
                return nombreSesion;
            }

            try
            {
                var usuario = usuarioId > 0 ? UsuarioDAO.ObtenerPorId(usuarioId) : null;
                var nombreUsuario = usuario != null ? (usuario.NombreCompleto ?? string.Empty).Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(nombreUsuario))
                {
                    return nombreUsuario;
                }

                if (usuario != null && !string.IsNullOrWhiteSpace(usuario.NombreUsuario))
                {
                    return usuario.NombreUsuario.Trim();
                }
            }
            catch
            {
                // Se ignora para no bloquear la revisión documental por fallos de catálogo.
            }

            return User != null && User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name)
                ? User.Identity.Name.Trim()
                : "sistema";
        }

        private string ObtenerOperadoraEaeVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return string.Empty;
            }

            var candidatos = new[]
            {
                solicitud.NombreOperador,
                solicitud.NombreComercial,
                solicitud.RazonSocial,
                solicitud.CodigoOaci,
                solicitud.CompaniasSeleccionadas,
                solicitud.ResumenOperacionesEae
            };

            return candidatos
                .Select(x => (x ?? string.Empty).Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? string.Empty;
        }

        private void NotificarDocumentoDevueltoEnSistema(SolicitudAOCR solicitud, Documento documento, string motivo, int usuarioId, string usuarioRegistro)
        {
            if (solicitud == null || documento == null)
            {
                return;
            }

            var tipoDocumento = string.IsNullOrWhiteSpace(documento.TipoDocumento) ? "Documento" : documento.TipoDocumento.Trim();
            var operadora = ObtenerOperadoraEaeVisible(solicitud);
            var mensaje = "Se devolvió el documento " + tipoDocumento +
                          (string.IsNullOrWhiteSpace(operadora) ? string.Empty : " de " + operadora) +
                          " para la solicitud #" + solicitud.CodigoSolicitud + ". Motivo: " + motivo;
            var url = Url.Action("Detalle", "SolicitudAOCR", new { id = solicitud.CodigoSolicitud });

            if (solicitud.CodigoUsuario > 0)
            {
                NotificacionBL.EnviarNotificacion(
                    solicitud.CodigoUsuario,
                    "Documento devuelto en revisión AOCR",
                    mensaje,
                    "WARNING",
                    url,
                    "SolicitudAOCR",
                    solicitud.CodigoSolicitud,
                    "aocr_tbsolicitud");
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

            var numeroSolicitud = string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? "#" + solicitud.CodigoSolicitud
                : solicitud.NumeroSolicitud.Trim();
            var etiquetaDocumento = ObtenerEtiquetaDocumento(documento);
            var asunto = "AOCR - Documento devuelto en revision documental " + numeroSolicitud;
            var cuerpo = "Estimado/a usuario AOCR:<br><br>" +
                         "Se devolvio un documento durante la revision documental de su solicitud AOCR.<br><br>" +
                         "<strong>Solicitud AOCR:</strong> " + HttpUtility.HtmlEncode(numeroSolicitud) + "<br>" +
                         "<strong>Operadora / EAE:</strong> " + HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(operadora) ? "No definida" : operadora) + "<br>" +
                         "<strong>Documento devuelto:</strong> " + HttpUtility.HtmlEncode(etiquetaDocumento) + "<br>" +
                         "<strong>Motivo de devolucion:</strong> " + HttpUtility.HtmlEncode(motivo ?? string.Empty) + "<br>" +
                         "<strong>Fecha de revision:</strong> " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "<br><br>" +
                         "Por favor ingrese al sistema AOCR, revise la observacion y cargue la version corregida del documento para continuar con el tramite.<br><br>" +
                         "Saludos.<br>Sistema AOCR";
            var correoEnviado = false;

            foreach (var destinatario in destinatarios)
            {
                try
                {
                    EmailHelper.EnviarEmail(destinatario, asunto, cuerpo);
                    correoEnviado = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning("[DOC_SOLICITUD] No se pudo enviar correo de devolucion documental. Solicitud=" + solicitud.CodigoSolicitud + "; documento=" + documento.CodigoDocumento + "; destinatario=" + destinatario + "; detalle=" + ex.Message);
                }
            }

            if (correoEnviado)
            {
                _solicitudAocrInfraBL.RegistrarEventoHistorialRevision(
                    solicitud.CodigoSolicitud,
                    documento.CodigoDocumento,
                    "CORREO_DOCUMENTO_DEVUELTO_ENVIADO",
                    "Correo de devolucion documental enviado para " + etiquetaDocumento + ".",
                    usuarioId,
                    usuarioRegistro);
            }
        }

        private object ConstruirDocumentoResponse(Documento documento, bool puedeRevisar, bool puedeReabrir)
        {
            if (documento == null)
            {
                return null;
            }

            var estadoVisible = documento.EstadoRevisionVisible ?? ObtenerEstadoDocumentoVisible(documento);
            var estadoNormalizado = ObtenerEstadoRevisionNormalizado(documento);
            var esPdf = string.Equals(Path.GetExtension(documento.NombreArchivo ?? string.Empty), ".pdf", StringComparison.OrdinalIgnoreCase);

            return new
            {
                id = documento.CodigoDocumento,
                tipo = documento.TipoDocumento ?? string.Empty,
                archivo = documento.NombreArchivo ?? string.Empty,
                operadora = documento.OperadoraEae ?? string.Empty,
                fechaCarga = documento.FechaCarga.HasValue ? documento.FechaCarga.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty,
                fechaCargaIso = documento.FechaCarga.HasValue ? documento.FechaCarga.Value.ToString("yyyy-MM-dd") : string.Empty,
                estado = estadoVisible,
                estadoNormalizado = estadoNormalizado,
                badgeClass = ObtenerBadgeDocumentoCss(documento),
                observacion = NormalizarTextoVisible(documento.ObservacionRevision),
                revisadoPor = documento.NombreUsuarioRevisor ?? string.Empty,
                fechaRevision = documento.FechaRevision.HasValue ? documento.FechaRevision.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty,
                previewUrl = esPdf ? Url.Action("Descargar", "Documento", new { id = documento.CodigoDocumento, vistaPrevia = true }) : string.Empty,
                downloadUrl = Url.Action("Descargar", "Documento", new { id = documento.CodigoDocumento }),
                puedeAceptar = puedeRevisar && estadoNormalizado == "PENDIENTE",
                puedeDevolver = puedeRevisar && estadoNormalizado == "PENDIENTE",
                puedeReabrir = puedeReabrir && estadoNormalizado != "PENDIENTE",
                esPdf = esPdf
            };
        }

        private JsonResult JsonError(int statusCode, string mensaje)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            Response.SuppressFormsAuthenticationRedirect = true;

            return Json(new { success = false, message = mensaje });
        }

        private SelectList ObtenerTiposDocumento()
        {
            // NOTA: "BORRADOR_AOCR" fue removido intencionalmente.
            // La AOCR NO debe subirse manualmente; se genera automáticamente desde
            // SolicitudAOCR/GenerarAOCR cuando el informe técnico queda aprobado.
            return new SelectList(new[] {
                new { Val="AOC", Txt="Certificado AOC" },
                new { Val="MEL", Txt="Lista MEL" },
                new { Val="MANUAL_OPS", Txt="Manual Operaciones" },
                new { Val="OTRO", Txt="Otro" }
            }, "Val", "Txt");
        }
        #endregion
    }
}
