using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    public class CertificadoController : Controller
    {
        private readonly CertificadoBL _bl = new CertificadoBL();

        // ============================================================
        // MOSTRAR DETALLE DEL CERTIFICADO POR SOLICITUD
        // ============================================================
        public ActionResult Detalle(int solicitudId)
        {
            var certificado = _bl.ObtenerPorSolicitud(solicitudId);
            ViewBag.SolicitudId = solicitudId;

            if (certificado == null)
                TempData["Info"] = "Aún no se ha generado el certificado.";

            return View(certificado);
        }

        // ============================================================
        // GENERAR CERTIFICADO CON FIRMA OBLIGATORIA
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Generar(int solicitudId, string firmadoPor)
        {
            if (string.IsNullOrWhiteSpace(firmadoPor))
            {
                TempData["Error"] = "El nombre del firmante es obligatorio para generar el certificado.";
                return RedirectToAction("Detalle", new { solicitudId });
            }

            try
            {
                // Validación: ¿Ya existe certificado?
                var existente = _bl.ObtenerPorSolicitud(solicitudId);
                if (existente != null)
                {
                    TempData["Error"] = "Ya existe un certificado generado para esta solicitud.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                int id = _bl.GenerarCertificado(solicitudId, firmadoPor.Trim());

                TempData["OK"] = "Certificado generado exitosamente.";
                return RedirectToAction("Detalle", new { solicitudId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar certificado: " + ex.Message;
                return RedirectToAction("Detalle", new { solicitudId });
            }
        }

        // ============================================================
        // SUBIR PDF DE CERTIFICADO FIRMADO DIGITALMENTE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubirPDF(int id, int solicitudId, HttpPostedFileBase archivo)
        {
            try
            {
                if (archivo == null || archivo.ContentLength == 0)
                    throw new Exception("Debe seleccionar un archivo PDF.");

                string extension = Path.GetExtension(archivo.FileName).ToLower();
                if (extension != ".pdf")
                    throw new Exception("Solo se permiten archivos con extensión PDF.");

                string carpeta = "~/PDF/Certificados/";
                string nombreArchivo = $"certificado_{id}.pdf";
                string rutaRelativa = carpeta + nombreArchivo;
                string rutaFisica = Server.MapPath(rutaRelativa);

                string carpetaFisica = Path.GetDirectoryName(rutaFisica);
                if (!Directory.Exists(carpetaFisica))
                    Directory.CreateDirectory(carpetaFisica);

                archivo.SaveAs(rutaFisica);

                bool actualizado = _bl.SubirPDF(id, rutaRelativa);
                if (!actualizado)
                    throw new Exception("No se pudo actualizar la ruta del certificado en la base de datos.");

                TempData["OK"] = "Archivo PDF subido y registrado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al subir el PDF: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { solicitudId });
        }

        // ============================================================
        // DESCARGAR CERTIFICADO EN PDF
        // ============================================================
        public ActionResult DescargarPDF(int id)
        {
            var cert = _bl.Obtener(id);
            if (cert == null)
                return Content("El certificado no existe.");

            if (string.IsNullOrWhiteSpace(cert.RutaPdf))
                return Content("El archivo PDF no está registrado.");

            string rutaFisica = Server.MapPath(cert.RutaPdf);
            if (!System.IO.File.Exists(rutaFisica))
                return Content("El archivo PDF no se encuentra en el servidor.");

            return File(rutaFisica, "application/pdf", $"AOCR-{cert.CodigoCertificado}.pdf");
        }
    }
}
