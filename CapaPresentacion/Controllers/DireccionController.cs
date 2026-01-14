using System;
using System.IO;
using System.Net.Mail;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using SelectPdf;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador,Director,JefaturaTecnica")]
    public class DireccionController : Controller
    {
        // ============================================================
        // 1. CARGA DE LA VISTA (Para resolver el Error 404)
        // URL: /Direccion/ValidacionFinal?codigoSolicitud=X
        // ============================================================
        [HttpGet]
        public ActionResult ValidacionFinal(int? codigoSolicitud)
        {
            if (!codigoSolicitud.HasValue) return RedirectToAction("Index", "Home");

            var solicitud = SolicitudAOCRBL.ObtenerPorId(codigoSolicitud.Value);
            if (solicitud == null) return HttpNotFound();

            // Pasamos los datos necesarios para que el Director valide antes de emitir
            ViewBag.Stats = ChecklistBL.ObtenerEstadisticas(codigoSolicitud.Value);

            // Según tu estructura de carpetas: Views/Checklist/ValidacionFinal.cshtml
            return View("~/Views/Checklist/ValidacionFinal.cshtml", solicitud);
        }

        // ============================================================
        // 2. PROCESO DE EMISIÓN (PDF y Email)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EmitirAOCR(int codigoSolicitud, string nombreDirector, string formato, bool enviarEmail = true)
        {
            try
            {
                // 1. Obtener datos
                var solicitud = SolicitudAOCRBL.ObtenerPorId(codigoSolicitud);
                if (solicitud == null)
                {
                    TempData["Error"] = "Solicitud no encontrada.";
                    return RedirectToAction("ValidacionFinal", new { codigoSolicitud });
                }

                // 2. Crear objeto del modelo para el certificado
                var certificado = new Certificado
                {
                    NumeroCertificado = $"AOCR-{DateTime.Now:yyyy}-{codigoSolicitud:D4}",
                    FechaEmision = DateTime.Now,
                    FechaVencimiento = DateTime.Now.AddYears(2),
                    Estado = "Legalizado",
                    FirmadoPor = nombreDirector,
                    CondicionesEspeciales = "Ninguna",
                    CodigoVerificacion = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper()
                };

                // 3. Renderizar vista HTML a String (Usa la vista alojada en Views/Checklist/Certificado.cshtml)
                string html = RenderViewToString(this.ControllerContext, "~/Views/Checklist/Certificado.cshtml", certificado);

                // 4. Convertir HTML a PDF usando SelectPdf
                HtmlToPdf converter = new HtmlToPdf();
                converter.Options.MarginTop = 30;
                converter.Options.MarginBottom = 30;
                converter.Options.PdfPageSize = PdfPageSize.A4;

                PdfDocument doc = converter.ConvertHtmlString(html);

                // Asegurar que la carpeta Temp existe
                string tempDir = Server.MapPath("~/Temp/");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string fileName = $"AOCR-CERT-{codigoSolicitud}-{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string fullPath = Path.Combine(tempDir, fileName);

                doc.Save(fullPath);
                doc.Close();

                // 5. Firma digital simulada
                if (formato == "pdf_firmado")
                {
                    System.IO.File.AppendAllText(fullPath, "\n// PDF FIRMADO ELECTRÓNICAMENTE POR " + nombreDirector);
                }

                // 6. Email al operador
                if (enviarEmail && !string.IsNullOrWhiteSpace(solicitud.Email))
                {
                    EnviarCorreoConAdjunto(solicitud.Email, "Certificado AOCR Generado",
                        "Estimado operador, se adjunta su certificado AOCR legalizado.", fullPath);
                }

                // 7. Retornar archivo para descarga
                byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);
                return File(fileBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error crítico: " + ex.Message;
                return RedirectToAction("ValidacionFinal", new { codigoSolicitud });
            }
        }

        #region Utilidades

        private string RenderViewToString(ControllerContext context, string viewPath, object model)
        {
            context.Controller.ViewData.Model = model;
            using (var sw = new StringWriter())
            {
                var viewResult = ViewEngines.Engines.FindPartialView(context, viewPath);
                if (viewResult.View == null) throw new FileNotFoundException("No se encontró la vista del certificado.");

                var viewContext = new ViewContext(context, viewResult.View, context.Controller.ViewData, context.Controller.TempData, sw);
                viewResult.View.Render(viewContext, sw);
                return sw.ToString();
            }
        }

        private void EnviarCorreoConAdjunto(string to, string subject, string body, string path)
        {
            using (var message = new MailMessage())
            {
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;
                message.Attachments.Add(new Attachment(path));

                using (var smtp = new SmtpClient()) // Los datos se toman del Web.config <system.net><mailSettings>
                {
                    smtp.Send(message);
                }
            }
        }

        #endregion
    }
}