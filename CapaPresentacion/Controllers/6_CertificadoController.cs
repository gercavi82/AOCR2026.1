using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using CapaUtilidades;
using CapaNegocio;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class CertificadoController : Controller
    {
        private readonly CertificadoBL _bl = new CertificadoBL();

        // ============================================================
        //        MOSTRAR DETALLE DEL CERTIFICADO POR SOLICITUD
        // ============================================================
        public ActionResult Detalle(int solicitudId)
        {
            var certificado = _bl.ObtenerPorSolicitud(solicitudId);

            ViewBag.SolicitudId = solicitudId;

            return View(certificado);
        }

        // ============================================================
        //        GENERAR CERTIFICADO AUTOMÁTICAMENTE
        // ============================================================
        public ActionResult Generar(int solicitudId)
        {
            string usuario = User?.Identity?.Name ?? "Sistema";

            int id = _bl.GenerarCertificado(solicitudId, usuario);

            return RedirectToAction("Detalle", new { solicitudId });
        }

        // ============================================================
        //                   SUBIR PDF DE CERTIFICADO
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador,Inspector")]
        public ActionResult SubirPDF(int id, int solicitudId, HttpPostedFileBase archivo)
        {
            try
            {
                if (archivo == null || archivo.ContentLength == 0)
                {
                    TempData["Error"] = "Debe seleccionar un archivo PDF.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // Validar extensión
                string extension = Path.GetExtension(archivo.FileName).ToLower();
                if (extension != ".pdf")
                {
                    TempData["Error"] = "Solo se permiten archivos PDF.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                var maxSize = GetMaxUploadSize();
                var allowed = new[] { ".pdf" };
                var basePath = GetUploadBasePath("Certificados");

                var result = FileUploadService.SaveFile(
                    archivo.InputStream,
                    archivo.FileName,
                    archivo.ContentType,
                    basePath,
                    maxSize,
                    allowed);

                // Registrar en BD
                _bl.SubirPDF(id, result.StoredPath);

                TempData["OK"] = "Archivo PDF subido correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al subir PDF: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { solicitudId });
        }

        // ============================================================
        //                   DESCARGAR PDF DE CERTIFICADO
        // ============================================================
        public ActionResult DescargarPDF(int id)
        {
            var cert = _bl.Obtener(id);

            if (cert == null)
                return Content("El certificado no existe.");

            if (string.IsNullOrWhiteSpace(cert.RutaPdf))
                return Content("El archivo PDF no está registrado.");

            string rutaFisica = cert.RutaPdf;
            if (!Path.IsPathRooted(rutaFisica))
                rutaFisica = Server.MapPath(cert.RutaPdf);

            if (!System.IO.File.Exists(rutaFisica))
                return Content("El archivo PDF no se encuentra en el servidor.");

            return File(rutaFisica, "application/pdf", "certificado.pdf");
        }

        private long GetMaxUploadSize()
        {
            var raw = ConfigurationManager.AppSettings["MaxUploadSize"];
            long size;
            return long.TryParse(raw, out size) ? size : (10 * 1024 * 1024);
        }

        private string GetUploadBasePath(string subfolder)
        {
            var baseSetting = ConfigurationManager.AppSettings["UploadStoragePath"] ?? "~/App_Data/Uploads";
            var basePath = Server.MapPath(baseSetting);
            return Path.Combine(basePath, subfolder ?? string.Empty);
        }
    }
}
