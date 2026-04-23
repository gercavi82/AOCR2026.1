using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
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
        private readonly FirmaDigitalService _firmaService = new FirmaDigitalService();
        private readonly AocrFirmaDocumentoDAO _firmaDocDao = new AocrFirmaDocumentoDAO();
        private readonly HistorialEstadoDAO _historialDao = new HistorialEstadoDAO();

        private static string GenerarNumeroAOCR(int idSolicitud, DateTime? fecha = null)
        {
            fecha = fecha ?? DateTime.Now;
            return $"AOCR-{fecha.Value.Year}-{idSolicitud:D4}";
        }

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
        //  GENERAR REGISTRO EN BD (redirige a Detalle) — legacy
        // ============================================================
        public ActionResult Generar(int solicitudId)
        {
            string usuario = User?.Identity?.Name ?? "Sistema";

            try
            {
                var existente = _bl.ObtenerPorSolicitud(solicitudId);
                if (existente == null)
                    _bl.GenerarCertificado(solicitudId, usuario);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear registro de certificado: " + ex.Message;
            }

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
            try
            {
                var modelo = ConstruirViewModel(solicitudId);

                var pdf = new ViewAsPdf("~/Views/Certificado/CertificadoAOCR.cshtml", modelo)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5),
                    CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
                };

                return pdf;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR GenerarCertificadoPdf: " + ex);
                return Content("<h3>Error al generar PDF</h3><pre>" +
                    System.Web.HttpUtility.HtmlEncode(ex.ToString()) + "</pre>", "text/html");
            }
        }

        // ============================================================
        //        VISTA PREVIA DEL CERTIFICADO (HTML)
        // ============================================================
        public ActionResult VistaPreviaCertificado(int solicitudId)
        {
            try
            {
                var modelo = ConstruirViewModel(solicitudId);
                return View("~/Views/Certificado/CertificadoAOCR.cshtml", modelo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR VistaPreviaCertificado: " + ex);
                return Content("<h3>Error Vista Previa</h3><pre>" +
                    System.Web.HttpUtility.HtmlEncode(ex.ToString()) + "</pre>", "text/html");
            }
        }

        // ============================================================
        //   FIRMAR CERTIFICADO AOCR — SOLO CONTRASEÑA (COORDINADOR)
        //   Usa el certificado institucional preconfigurado en el
        //   servidor; el usuario SOLO ingresa la contraseña del .p12.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "CoordinacionLegal,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
        public ActionResult FirmarCertificadoAOCR(int solicitudId, string password)
        {
            try
            {
                if (solicitudId <= 0)
                {
                    TempData["Error"] = "Identificador de solicitud no válido.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    TempData["Error"] = "Debe ingresar la contraseña del certificado digital.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // 1) Cargar certificado institucional preconfigurado
                byte[] certificadoBytes;
                string passwordDescartada;
                string errorCert;
                if (!TryCargarCertificadoInstitucional(out certificadoBytes, out passwordDescartada, out errorCert))
                {
                    TempData["Error"] = errorCert;
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // 2) Validar contraseña ingresada contra el .p12 institucional
                var infoCert = _firmaService.LeerCertificado(certificadoBytes, password);
                if (infoCert == null || !infoCert.Exitoso)
                {
                    TempData["Error"] = infoCert != null && !string.IsNullOrWhiteSpace(infoCert.Mensaje)
                        ? infoCert.Mensaje
                        : "Contraseña incorrecta o certificado no válido.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // 3) Asegurar que exista el registro de Certificado en BD
                string usuarioNombre = User?.Identity?.Name ?? "Sistema";
                var cert = _bl.ObtenerPorSolicitud(solicitudId);
                if (cert == null)
                {
                    _bl.GenerarCertificado(solicitudId, usuarioNombre);
                    cert = _bl.ObtenerPorSolicitud(solicitudId);
                }
                if (cert == null)
                {
                    TempData["Error"] = "No se pudo crear/obtener el registro del certificado AOCR.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // 4) Construir modelo y generar PDF vía Rotativa
                var modelo = ConstruirViewModel(solicitudId);
                var nombreFirmante = !string.IsNullOrWhiteSpace(infoCert.NombreTitular)
                    ? infoCert.NombreTitular
                    : usuarioNombre;
                var cargoFirmante = "Coordinador/a Legal AOCR";

                var pdf = new ViewAsPdf("~/Views/Certificado/CertificadoAOCR.cshtml", modelo)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5),
                    CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
                };

                byte[] pdfBytes;
                try
                {
                    pdfBytes = pdf.BuildFile(ControllerContext);
                }
                catch (Exception exPdf)
                {
                    TempData["Error"] = "No se pudo generar el PDF del certificado: " + exPdf.Message;
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // 5) QR + firma digital
                var contenidoQr = ConstruirContenidoQrCertificado(solicitudId, modelo, infoCert, nombreFirmante, cargoFirmante);
                var resultado = _firmaService.FirmarPdf(
                    pdfBytes,
                    certificadoBytes,
                    password,
                    nombreFirmante,
                    "Firma del Coordinador — Certificado AOCR",
                    "Sistema AOCR DGAC",
                    "DIRDAC",
                    contenidoQr,
                    null);

                if (resultado == null || !resultado.Exitoso)
                {
                    TempData["Error"] = resultado != null && !string.IsNullOrWhiteSpace(resultado.Mensaje)
                        ? resultado.Mensaje
                        : "No se pudo aplicar la firma digital al certificado.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                // 6) Persistir PDF firmado
                string rutaRelativa = GuardarCertificadoFirmado(solicitudId, resultado.PdfFirmado);

                // 7) Actualizar registro de Certificado
                try
                {
                    cert.RutaDocumento = rutaRelativa;
                    cert.Estado = "Vigente";
                    cert.EmitidoPor = usuarioNombre;
                    cert.AprobadoPor = nombreFirmante;
                    cert.UpdatedAt = DateTime.Now;
                    new CertificadoDAO().Actualizar(cert);
                }
                catch (Exception exUpd)
                {
                    System.Diagnostics.Debug.WriteLine("WARN Certificado.Actualizar: " + exUpd);
                }

                // 8) Registrar firma en aocr_tbfirma_documento
                try
                {
                    _firmaDocDao.Registrar(new AocrFirmaDocumento
                    {
                        CodigoSolicitud = solicitudId,
                        CodigoInspeccion = null,
                        TipoDocumento = "CERTIFICADO_AOCR",
                        NumeroAocr = modelo?.NumeroAOCR,
                        NombreArchivo = System.IO.Path.GetFileName(rutaRelativa),
                        RutaDocumento = rutaRelativa,
                        HashDocumento = resultado.HashSha256,
                        CodigoQr = contenidoQr,
                        SujetoCertificado = resultado.SujetoCertificado ?? infoCert.SujetoCertificado,
                        NombreFirmante = nombreFirmante,
                        CargoFirmante = cargoFirmante,
                        FechaFirma = DateTime.Now,
                        CodigoUsuario = null,
                        UsuarioNombre = usuarioNombre
                    });
                }
                catch (Exception exReg)
                {
                    System.Diagnostics.Debug.WriteLine("WARN AocrFirmaDocumento.Registrar: " + exReg);
                }

                // 9) Registrar trazabilidad en historial de estado
                try
                {
                    _historialDao.RegistrarCambio(
                        solicitudId,
                        "CertificadoAOCR_PendienteFirma",
                        "CertificadoAOCR_Firmado",
                        0,
                        "Certificado AOCR firmado por " + nombreFirmante + " (" + cargoFirmante + ")");
                }
                catch (Exception exHist)
                {
                    System.Diagnostics.Debug.WriteLine("WARN HistorialEstado.RegistrarCambio: " + exHist);
                }

                TempData["OK"] = "Certificado AOCR firmado correctamente por " + nombreFirmante + ".";
                return RedirectToAction("Detalle", new { solicitudId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al firmar el certificado AOCR: " + ex.Message;
                return RedirectToAction("Detalle", new { solicitudId });
            }
        }

        // ============================================================
        //   Helpers privados: certificado institucional + almacenado
        // ============================================================
        private bool TryCargarCertificadoInstitucional(out byte[] certificadoBytes, out string password, out string mensajeError)
        {
            certificadoBytes = null;
            password = null;
            mensajeError = null;

            var rutaConfigurada = System.Configuration.ConfigurationManager.AppSettings["Aocr:CertificadoInstitucionalRuta"];
            var passwordConfigurado = System.Configuration.ConfigurationManager.AppSettings["Aocr:CertificadoInstitucionalPassword"];

            if (string.IsNullOrWhiteSpace(rutaConfigurada))
            {
                mensajeError = "No hay un certificado institucional configurado en el servidor. Solicite al administrador configurar 'Aocr:CertificadoInstitucionalRuta' en Web.config.";
                return false;
            }

            string rutaAbsoluta;
            try
            {
                rutaAbsoluta = rutaConfigurada.StartsWith("~", StringComparison.Ordinal)
                    ? Server.MapPath(rutaConfigurada)
                    : rutaConfigurada;
            }
            catch (Exception ex)
            {
                mensajeError = "Ruta del certificado institucional no válida: " + ex.Message;
                return false;
            }

            if (!System.IO.File.Exists(rutaAbsoluta))
            {
                mensajeError = "No se encontró el archivo del certificado institucional (" + rutaAbsoluta + ").";
                return false;
            }

            try
            {
                certificadoBytes = System.IO.File.ReadAllBytes(rutaAbsoluta);
                password = passwordConfigurado ?? string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                mensajeError = "No se pudo leer el certificado institucional: " + ex.Message;
                return false;
            }
        }

        private string GuardarCertificadoFirmado(int solicitudId, byte[] contenido)
        {
            var carpetaRelativa = "~/App_Data/Uploads/AOCR/Certificados/" + solicitudId;
            var carpetaAbsoluta = Server.MapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var nombreSeguro = "certificado_aocr_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + solicitudId + ".pdf";
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombreSeguro);
            System.IO.File.WriteAllBytes(rutaAbsoluta, contenido ?? new byte[0]);

            return VirtualPathUtility.ToAbsolute(carpetaRelativa.TrimStart('~') + "/" + nombreSeguro);
        }

        private static string ConstruirContenidoQrCertificado(int solicitudId, CertificadoAOCRViewModel modelo, InformacionCertificadoDigital infoCert, string nombreFirmante, string cargoFirmante)
        {
            var partes = new System.Collections.Generic.List<string>
            {
                "Sistema=AOCR DGAC",
                "Documento=CERTIFICADO_AOCR",
                "SolicitudId=" + solicitudId,
                "NumeroAOCR=" + (modelo != null ? (modelo.NumeroAOCR ?? string.Empty) : string.Empty),
                "Firmante=" + (nombreFirmante ?? string.Empty),
                "Cargo=" + (cargoFirmante ?? string.Empty),
                "FechaFirma=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Certificado=" + (infoCert != null ? (infoCert.SujetoCertificado ?? string.Empty) : string.Empty),
                "VigenciaHasta=" + (infoCert != null && infoCert.VigenteHasta.HasValue
                    ? infoCert.VigenteHasta.Value.ToString("yyyy-MM-dd HH:mm:ss")
                    : string.Empty)
            };

            return string.Join(" | ", partes);
        }

        // ============================================================
        //        CONSTRUIR VIEWMODEL COMÚN
        // ============================================================
        private CertificadoAOCRViewModel ConstruirViewModel(int solicitudId)
        {
            var solicitudDAO = new SolicitudAOCRDAO();
            var solicitud = solicitudDAO.ObtenerPorId(solicitudId);

            if (solicitud == null)
                throw new Exception("La solicitud " + solicitudId + " no existe.");

            // Intentar obtener certificado existente (no fatal si falla)
            Certificado cert = null;
            try { cert = _bl.ObtenerPorSolicitud(solicitudId); } catch { }

            string numeroCert = cert != null && !string.IsNullOrWhiteSpace(cert.NumeroCertificado)
                ? cert.NumeroCertificado
                : GenerarNumeroAOCR(solicitudId, solicitud.FechaSolicitud);

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

            return new CertificadoAOCRViewModel
            {
                NumeroAOCR = numeroCert,
                NumeroAOCBase = solicitud.NumeroSolicitud,
                FechaEmision = cert?.FechaEmision ?? DateTime.Now,
                FechaVencimiento = cert?.FechaVencimiento,
                FechaRenovacion = null,
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
        }
    }
}
