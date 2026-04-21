using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs; // Solo para SolicitudDAO (Cabecera)
using CapaModelo;
using CapaNegocio;    // <--- IMPORTANTE: Usamos la Capa de Negocio
using CapaNegocio.Helpers;
using CapaUtilidades;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class DocumentoController : Controller
    {
        // 1. Usamos la BL en lugar del DAO
        private readonly DocumentoBL _documentoBL;
        private readonly SolicitudAOCRDAO _solicitudDAO; // Solo para obtener datos de la solicitud (padre)
        private readonly string _rutaDocumentos;

        public DocumentoController()
        {
            _documentoBL = new DocumentoBL();
            _solicitudDAO = new SolicitudAOCRDAO();

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
        public ActionResult Lista(int solicitudId)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(solicitudId);
                if (solicitud == null) return RedirectToAction("Index", "SolicitudAOCR");

                // Llamada a la BL
                var documentos = _documentoBL.ObtenerPorSolicitud(solicitudId) ?? new List<Documento>();

                var stats = new
                {
                    Total = documentos.Count,
                    Aprobados = documentos.Count(d => d.Estado == "APROBADO"),
                    Pendientes = documentos.Count(d => d.Estado == "PENDIENTE"),
                    Rechazados = documentos.Count(d => d.Estado == "RECHAZADO"),
                    TamanioTotal = documentos.Sum(d => d.TamanioArchivo ?? 0)
                };

                ViewBag.Stats = stats;
                ViewBag.Estadisticas = stats;

                ViewBag.Solicitud = solicitud;
                ViewBag.SolicitudId = solicitudId;

                return View(documentos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Documento/RevisarDocumentos
        // [Authorize(Roles = "Administrador,Inspector")] // Descomentar luego
        public ActionResult RevisarDocumentos()
        {
            try
            {
                var todos = _documentoBL.ObtenerTodos() ?? new List<Documento>();

                // Filtramos por PENDIENTE (tu BL usa mayúsculas)
                var pendientes = todos
                    .Where(d => d.Estado != null && d.Estado.ToUpper() == "PENDIENTE")
                    .OrderByDescending(d => d.FechaSubida)
                    .ToList();

                return View(pendientes);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar bandeja: {ex.Message}";
                // Retornamos lista vacía para no causar error 302
                return View(new List<Documento>());
            }
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

        public ActionResult Descargar(int id)
        {
            try
            {
                var doc = _documentoBL.ObtenerPorId(id); // Usamos BL
                if (doc == null) return HttpNotFound();

                if (!System.IO.File.Exists(doc.RutaArchivo))
                {
                    TempData["Error"] = "El archivo físico no existe en el servidor.";
                    return RedirectToAction("Lista", new { solicitudId = doc.CodigoSolicitud });
                }

                byte[] bytes = System.IO.File.ReadAllBytes(doc.RutaArchivo);
                return File(bytes, "application/octet-stream", doc.NombreArchivo);
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
