using System;
using System.IO;
using System.Web.Mvc;
using CapaModelo.RT.ViewModels;
using CapaNegocio.Services;
using CapaNegocio.Helpers;
using CapaDatos.DAOs;
using CapaNegocio;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class RTController : Controller
    {
        private readonly RTService _service = new RTService();

        private int ObtenerUsuarioId()
        {
            var v = Session["UserId"] ?? Session["IdUsuario"] ?? Session["CodigoUsuario"];
            if (v != null && int.TryParse(v.ToString(), out var id))
                return id;

            return 0;
        }

        [HttpGet]
        public ActionResult Registro()
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            var vm = new RegistroRTVM();

            if (solicitud != null)
            {
                var compania = _service.GetCompaniaById(solicitud.CompaniaId);
                vm.SolicitudId = solicitud.Id;
                vm.RazonSocial = compania?.RazonSocial;
                vm.Ruc = compania?.Ruc;
                vm.Telefono = compania?.Telefono;
                vm.Email = compania?.EmailContacto;
                vm.AreaContableJson = compania?.AreaContableJson;

                ViewBag.Estado = solicitud.Estado;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarRegistro(RegistroRTVM vm)
        {
            var usuarioId = ObtenerUsuarioId();

            if (!ModelState.IsValid)
            {
                return View("Registro", vm);
            }

            try
            {
                var solicitudId = _service.GuardarBorrador(vm, usuarioId);
                TempData["Ok"] = "Borrador guardado correctamente.";
                return RedirectToAction("Declaracion", new { solicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Registro", vm);
            }
        }

        [HttpGet]
        public ActionResult Declaracion(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Registro");
            }

            var vm = new DeclaracionRTVM
            {
                SolicitudId = solicitud.Id,
                TextoDeclaracion = solicitud.DeclaracionTexto,
                Acepto = solicitud.DeclaracionAceptada,
                Estado = solicitud.Estado
            };

            var compania = _service.GetCompaniaById(solicitud.CompaniaId);
            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            var nombre = (usuario != null && !string.IsNullOrWhiteSpace(usuario.NombreCompleto))
                ? usuario.NombreCompleto
                : (usuario != null ? usuario.NombreUsuario : "");

            var razonSocial = compania != null ? compania.RazonSocial : "";
            var textoPersonalizado = _service.ObtenerTextoDeclaracionPersonalizado(nombre, razonSocial);
            if (!string.IsNullOrWhiteSpace(textoPersonalizado))
            {
                vm.TextoDeclaracion = textoPersonalizado;
            }

            return View(vm);
        }

        [HttpGet]
        public ActionResult DescargarDeclaracionPdf(int solicitudId, bool vistaPrevia = false)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitudId <= 0 && solicitud != null)
            {
                solicitudId = solicitud.Id;
            }
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                LogBL.RegistrarError("Solicitud RT no encontrada para generar PDF. usuarioId=" + usuarioId + " solicitudId=" + solicitudId, "n/a", "RTController");
                return Content("Solicitud no encontrada para generar la declaración.");
            }

            var compania = _service.GetCompaniaById(solicitud.CompaniaId);
            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            var nombre = (usuario != null && !string.IsNullOrWhiteSpace(usuario.NombreCompleto))
                ? usuario.NombreCompleto
                : (usuario != null ? usuario.NombreUsuario : "");

            var razonSocial = compania != null ? compania.RazonSocial : "";
            var vm = new DeclaracionPdfVM
            {
                NombreCompleto = nombre,
                Compania = razonSocial,
                TextoDeclaracion = _service.ObtenerTextoDeclaracionPersonalizado(nombre, razonSocial),
                FechaEmision = DateTime.Now
            };

            var fileName = "Declaracion_RT_" + solicitudId + ".pdf";
            try
            {
                var pdfBytes = GenerarDeclaracionPdf(vm);
                return vistaPrevia
                    ? File(pdfBytes, "application/pdf")
                    : File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError("Error generando PDF de declaración RT (iText).", ex.ToString(), "RTController");
                return Content("Error generando PDF. Revise logs.");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AceptarDeclaracion(DeclaracionRTVM vm)
        {
            var usuarioId = ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                return View("Declaracion", vm);
            }

            try
            {
                _service.AceptarDeclaracion(vm.SolicitudId, usuarioId);
                TempData["Ok"] = "Declaración aceptada.";
                return RedirectToAction("Designacion", new { solicitudId = vm.SolicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Declaracion", vm);
            }
        }

        [HttpGet]
        public ActionResult Designacion(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            var solicitud = _service.GetSolicitudByUsuario(usuarioId);
            if (solicitud == null || solicitud.Id != solicitudId)
            {
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirectToAction("Registro");
            }

            var doc = _service.GetDocumentoDesignacion(solicitudId);
            var vm = new DesignacionUploadVM
            {
                SolicitudId = solicitud.Id,
                NombreArchivoActual = doc?.NombreArchivo,
                Estado = solicitud.Estado
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubirDesignacion(DesignacionUploadVM vm)
        {
            var usuarioId = ObtenerUsuarioId();
            if (!ModelState.IsValid)
            {
                return View("Designacion", vm);
            }

            try
            {
                _service.SubirDesignacionPdf(vm.SolicitudId, usuarioId, vm.ArchivoPdf);
                TempData["Ok"] = "Documento cargado correctamente.";
                return RedirectToAction("Designacion", new { solicitudId = vm.SolicitudId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Designacion", vm);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Enviar(int solicitudId)
        {
            var usuarioId = ObtenerUsuarioId();
            try
            {
                _service.EnviarSolicitud(solicitudId, usuarioId);
                TempData["Ok"] = "Solicitud enviada. En proceso de validación y aprobación por Coordinador.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Designacion", new { solicitudId });
        }

        private static byte[] GenerarDeclaracionPdf(DeclaracionPdfVM vm)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4, 25f, 25f, 120f, 80f);
                var writer = PdfWriter.GetInstance(doc, ms);
                var server = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "RTController.GenerarDeclaracionPdf");
                doc.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);
                var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                doc.Add(new Paragraph("Declaración de Responsabilidad", titleFont));
                doc.Add(new Paragraph("Responsable Técnico (RT)", normalFont));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph(vm.TextoDeclaracion ?? "", normalFont));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("Nombre: " + (vm.NombreCompleto ?? ""), normalFont));
                doc.Add(new Paragraph("Compañía: " + (vm.Compania ?? ""), normalFont));
                doc.Add(new Paragraph(" "));
                doc.Add(new Paragraph("_______________________________", normalFont));
                doc.Add(new Paragraph("Firma del Responsable Técnico", smallFont));
                doc.Add(new Paragraph("Fecha emisión: " + vm.FechaEmision.ToString("dd/MM/yyyy"), smallFont));

                doc.Close();
                return ms.ToArray();
            }
        }
    }
}
