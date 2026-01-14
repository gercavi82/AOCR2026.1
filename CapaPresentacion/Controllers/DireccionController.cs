using System;
using System.IO;
using System.Net.Mail;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using SelectPdf;

public class DireccionController : Controller
{
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
                return RedirectToAction("Index");
            }

            // 2. Crear objeto del modelo para la vista PDF
            var certificado = new Certificado
            {
                NumeroCertificado = $"AOCR-{DateTime.Now:yyyy}-{codigoSolicitud:D4}",
                FechaEmision = DateTime.Now,
                FechaVencimiento = DateTime.Now.AddYears(2),
                Estado = "Legalizado",
                FirmadoPor = nombreDirector,
                CondicionesEspeciales = "", // O llena según lógica de negocio
                CodigoVerificacion = Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper()
            };

            // 3. Renderizar HTML
            string html = RenderViewToString(this.ControllerContext, "Certificado", certificado);

            // 4. Convertir HTML a PDF
            HtmlToPdf converter = new HtmlToPdf();
            converter.Options.MarginTop = 30;
            converter.Options.PdfPageSize = PdfPageSize.A4;
            PdfDocument doc = converter.ConvertHtmlString(html);

            string fileName = $"AOCR-CERT-{codigoSolicitud}-{DateTime.Now:yyyyMMddHHmmss}.pdf";
            string fullPath = Server.MapPath($"~/Temp/{fileName}");
            doc.Save(fullPath);
            doc.Close();

            // 5. Firma digital simulada (ejemplo simple, se puede mejorar)
            if (formato == "pdf_firmado")
            {
                // Aquí puedes agregar integración con un firmador real o un servicio externo.
                System.IO.File.AppendAllText(fullPath, "\n// PDF FIRMADO DIGITALMENTE");
            }

            // 6. Email al operador
            if (enviarEmail && !string.IsNullOrWhiteSpace(solicitud.Email))
            {
                EnviarCorreoConAdjunto(solicitud.Email, "Certificado AOCR", "Estimado operador, se adjunta su certificado AOCR.", fullPath);
            }

            // 7. Retornar archivo
            byte[] fileBytes = System.IO.File.ReadAllBytes(fullPath);
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Error generando certificado: " + ex.Message;
            return RedirectToAction("Index");
        }
    }

    // Utilidad para renderizar vista a string
    private string RenderViewToString(ControllerContext context, string viewName, object model)
    {
        context.Controller.ViewData.Model = model;
        using (var sw = new StringWriter())
        {
            var viewResult = ViewEngines.Engines.FindPartialView(context, viewName);
            var viewContext = new ViewContext(context, viewResult.View, context.Controller.ViewData, context.Controller.TempData, sw);
            viewResult.View.Render(viewContext, sw);
            return sw.ToString();
        }
    }

    // Enviar correo con adjunto
    private void EnviarCorreoConAdjunto(string to, string subject, string body, string path)
    {
        using (var message = new MailMessage())
        {
            message.To.Add(to);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;
            message.Attachments.Add(new Attachment(path));

            using (var smtp = new SmtpClient())
            {
                smtp.Send(message);
            }
        }
    }
}
