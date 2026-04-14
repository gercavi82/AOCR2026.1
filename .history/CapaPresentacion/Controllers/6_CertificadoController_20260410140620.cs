using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaModelo.Common;
using CapaUtilidades;
using CapaModelo;
using CapaDatos.DAOs;
using CapaDatos.Constants;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
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

                // Construcción de ruta segura
                string carpeta = "~/App_Data/Certificados/";

                var options = new FileUploadOptions
                {
                    BasePath = FileStorageHelper.GetPhysicalBasePath(carpeta),
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
                    TempData["Error"] = error ?? "No se pudo guardar el PDF.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                string rutaRelativa = carpeta + result.StoredName;

                // Registrar en BD
                _bl.SubirPDF(id, rutaRelativa);

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

            string rutaFisica = Server.MapPath(cert.RutaPdf);

            if (!System.IO.File.Exists(rutaFisica))
                return Content("El archivo PDF no se encuentra en el servidor.");

            return File(rutaFisica, "application/pdf", "certificado.pdf");
        }

        // ============================================================
        //        GENERAR PDF DEL CERTIFICADO AOCR
        // ============================================================
        public ActionResult GenerarCertificadoPdf(int solicitudId)
        {
            var solicitudDAO = new SolicitudAOCRDAO();
            var solicitud = solicitudDAO.ObtenerPorId(solicitudId);

            if (solicitud == null)
            {
                TempData["Error"] = "La solicitud no existe.";
                return RedirectToAction("GenerarCertificados", "CoordinacionLegal");
            }

            var cert = _bl.ObtenerPorSolicitud(solicitudId);
            string numeroCert = cert != null && !string.IsNullOrWhiteSpace(cert.NumeroCertificado)
                ? cert.NumeroCertificado
                : AOCRPdfService.GenerarNumeroAOCR(solicitudId, solicitud.FechaSolicitud);

            // Si no existe certificado en BD, crearlo
            if (cert == null)
            {
                string usuario = User?.Identity?.Name ?? "Sistema";
                _bl.GenerarCertificado(solicitudId, usuario);
                cert = _bl.ObtenerPorSolicitud(solicitudId);
            }

            // Cargar logo como base64
            string logoBase64 = null;
            string escudoBase64 = null;
            try
            {
                string logoPath = Server.MapPath("~/Content/assets/imganes/logodgac.png");
                if (System.IO.File.Exists(logoPath))
                    logoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(logoPath));

                string escudoPath = Server.MapPath("~/Content/assets/imganes/escudo-ecuador.jpg");
                if (System.IO.File.Exists(escudoPath))
                    escudoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(escudoPath));
            }
            catch { }

            var modelo = new CertificadoAOCRViewModel
            {
                NumeroAOCR = numeroCert,
                NumeroAOCBase = solicitud.NumeroSolicitud,
                FechaEmision = cert?.FechaEmision ?? DateTime.Now,
                FechaVencimiento = cert?.FechaVencimiento,
                FechaRenovacion = null,
                NumeroEnmienda = 1,

                // Explotador
                NombreExplotador = solicitud.NombreOperador,
                EstadoExplotador = solicitud.Pais ?? "Ecuador",
                RazonSocial = !string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial : solicitud.NombreOperador,
                RUC = solicitud.Ruc,
                DireccionExplotador = solicitud.Direccion,
                TelefonoExplotador = solicitud.Telefono,
                CorreoExplotador = solicitud.Email,

                // Contacto Ecuador (usa mismos datos del operador como fallback)
                PuntoContactoEcuador = solicitud.RepresentanteLegal,
                DireccionContactoEcuador = solicitud.Direccion,
                TelefonoContactoEcuador = solicitud.Telefono,
                CorreoContactoEcuador = solicitud.Email,

                // Contacto operacional
                DireccionOperacional = solicitud.Direccion,
                TelefonoOperacional = solicitud.Telefono,
                CorreoOperacional = solicitud.Email,

                // Representante técnico
                RepresentanteTecnico = solicitud.TecnicoResponsableNombre,
                CorreoRT = solicitud.CorreoRepresentanteTecnico,

                // Representante Legal
                RepresentanteLegal = solicitud.RepresentanteLegal,

                // Operación
                TipoOperacion = solicitud.TipoOperacion,
                AlcanceOperacion = solicitud.DescripcionOperacion,

                // Firmante
                NombreFirmante = !string.IsNullOrWhiteSpace(solicitud.Director) ? solicitud.Director : "DIRECTOR GENERAL DE AVIACION CIVIL",
                CargoFirmante = !string.IsNullOrWhiteSpace(solicitud.CargoDirector) ? solicitud.CargoDirector : "Director General de Aviacion Civil",
                TituloFirmante = "DIRECTOR GENERAL DE AVIACION CIVIL",

                // Observaciones
                Observaciones = solicitud.Observaciones,

                // Textos legales
                TextoLegalEs = "Este Certificado es expedido en base al AOC # --- vigente hasta ---, y cualquier cambio al AOC original, o de las condiciones o limitaciones, que afecte las operaciones del explotador en su Estado, deberan ser notificados a esta AAC, dentro de 30 dias de dicho cambio.\nEste Certificado deja de tener efecto inmediatamente despues de la expiracion, suspension, revocacion, cancelacion o cualquier accion similar sobre el AOC.",
                TextoLegalEn = "This certificate is issued based on the current AOC # --- until ---, and any changes to the original AOC or to the conditions or limitations that affect the operator's operations in its State, shall be notified to this CAA within 30 days of such change.\nThis certificate ceases to have effect immediately upon expiration, suspension, revocation, cancellation or any similar action on the AOC.",

                // Recursos
                LogoBase64 = logoBase64,
                EscudoBase64 = escudoBase64,

                Solicitud = solicitud
            };

            var pdf = new ViewAsPdf("~/Views/Certificado/CertificadoAOCR.cshtml", modelo)
            {
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5),
                CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
            };

            return pdf;
        }

        // ============================================================
        //        VISTA PREVIA DEL CERTIFICADO (HTML)
        // ============================================================
        public ActionResult VistaPreviaCertificado(int solicitudId)
        {
            var solicitudDAO = new SolicitudAOCRDAO();
            var solicitud = solicitudDAO.ObtenerPorId(solicitudId);

            if (solicitud == null)
            {
                TempData["Error"] = "La solicitud no existe.";
                return RedirectToAction("GenerarCertificados", "CoordinacionLegal");
            }

            var cert = _bl.ObtenerPorSolicitud(solicitudId);
            string numeroCert = cert != null && !string.IsNullOrWhiteSpace(cert.NumeroCertificado)
                ? cert.NumeroCertificado
                : AOCRPdfService.GenerarNumeroAOCR(solicitudId, solicitud.FechaSolicitud);

            string logoBase64 = null;
            string escudoBase64 = null;
            try
            {
                string logoPath = Server.MapPath("~/Content/assets/imganes/logodgac.png");
                if (System.IO.File.Exists(logoPath))
                    logoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(logoPath));

                string escudoPath = Server.MapPath("~/Content/assets/imganes/escudo-ecuador.jpg");
                if (System.IO.File.Exists(escudoPath))
                    escudoBase64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(escudoPath));
            }
            catch { }

            var modelo = new CertificadoAOCRViewModel
            {
                NumeroAOCR = numeroCert,
                NumeroAOCBase = solicitud.NumeroSolicitud,
                FechaEmision = cert?.FechaEmision ?? DateTime.Now,
                FechaVencimiento = cert?.FechaVencimiento,
                NumeroEnmienda = 1,
                NombreExplotador = solicitud.NombreOperador,
                EstadoExplotador = solicitud.Pais ?? "Ecuador",
                RazonSocial = !string.IsNullOrWhiteSpace(solicitud.RazonSocial) ? solicitud.RazonSocial : solicitud.NombreOperador,
                RUC = solicitud.Ruc,
                DireccionExplotador = solicitud.Direccion,
                TelefonoExplotador = solicitud.Telefono,
                CorreoExplotador = solicitud.Email,
                PuntoContactoEcuador = solicitud.RepresentanteLegal,
                DireccionContactoEcuador = solicitud.Direccion,
                TelefonoContactoEcuador = solicitud.Telefono,
                CorreoContactoEcuador = solicitud.Email,
                DireccionOperacional = solicitud.Direccion,
                TelefonoOperacional = solicitud.Telefono,
                CorreoOperacional = solicitud.Email,
                RepresentanteTecnico = solicitud.TecnicoResponsableNombre,
                CorreoRT = solicitud.CorreoRepresentanteTecnico,
                RepresentanteLegal = solicitud.RepresentanteLegal,
                TipoOperacion = solicitud.TipoOperacion,
                AlcanceOperacion = solicitud.DescripcionOperacion,
                NombreFirmante = !string.IsNullOrWhiteSpace(solicitud.Director) ? solicitud.Director : "DIRECTOR GENERAL DE AVIACION CIVIL",
                CargoFirmante = !string.IsNullOrWhiteSpace(solicitud.CargoDirector) ? solicitud.CargoDirector : "Director General de Aviacion Civil",
                TituloFirmante = "DIRECTOR GENERAL DE AVIACION CIVIL",
                Observaciones = solicitud.Observaciones,
                TextoLegalEs = "Este Certificado es expedido en base al AOC # --- vigente hasta ---, y cualquier cambio al AOC original, o de las condiciones o limitaciones, que afecte las operaciones del explotador en su Estado, deberan ser notificados a esta AAC, dentro de 30 dias de dicho cambio.\nEste Certificado deja de tener efecto inmediatamente despues de la expiracion, suspension, revocacion, cancelacion o cualquier accion similar sobre el AOC.",
                TextoLegalEn = "This certificate is issued based on the current AOC # --- until ---, and any changes to the original AOC or to the conditions or limitations that affect the operator's operations in its State, shall be notified to this CAA within 30 days of such change.\nThis certificate ceases to have effect immediately upon expiration, suspension, revocation, cancellation or any similar action on the AOC.",
                LogoBase64 = logoBase64,
                EscudoBase64 = escudoBase64,
                Solicitud = solicitud
            };

            return View("~/Views/Certificado/CertificadoAOCR.cshtml", modelo);
        }
    }
}
