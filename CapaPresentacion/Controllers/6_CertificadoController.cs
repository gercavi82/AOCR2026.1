using System;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaNegocio.Services;
using CapaModelo.Common;
using CapaUtilidades;
using CapaModelo;
using CapaDatos.DAOs;
using CapaDatos.Constants;
using CapaPresentacion.Helpers;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "CoordinacionLegal,Direccion,JefaturaTecnica,DirectorGeneral,Administrador")]
    public class CertificadoController : Controller
    {
        private const string VistaCertificadoAocr = "~/Views/Certificado/CertificadoAOCR.cshtml";

        private readonly CertificadoBL _bl = new CertificadoBL();
        private readonly CertificadoDAO _certificadoDao = new CertificadoDAO();
        private readonly FirmaDigitalService _firmaService = new FirmaDigitalService();
        private readonly AocrFirmaDocumentoDAO _firmaDocDao = new AocrFirmaDocumentoDAO();
        private readonly HistorialEstadoDAO _historialDao = new HistorialEstadoDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();

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
            certificado = SincronizarCertificadoConFirmaRegistrada(certificado);

            ViewBag.SolicitudId = solicitudId;

            return View(certificado);
        }

        public ActionResult Ver(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return HttpNotFound("El certificado no existe.");

            Certificado certificado = null;
            int codigoCertificado;
            if (int.TryParse(id, out codigoCertificado))
                certificado = _bl.Obtener(codigoCertificado);

            if (certificado == null)
                certificado = _bl.ObtenerPorNumero(id);

            if (certificado == null || certificado.CodigoSolicitud <= 0)
                return HttpNotFound("El certificado no existe.");

            return RedirectToAction("Detalle", new { solicitudId = certificado.CodigoSolicitud });
        }

        // ============================================================
        //                   DESCARGAR PDF DE CERTIFICADO
        // ============================================================
        public ActionResult DescargarPDF(int id, bool vistaPrevia = false)
        {
            var cert = _bl.Obtener(id);
            cert = SincronizarCertificadoConFirmaRegistrada(cert);
            var requiereRectificacion = RequiereRectificacionPorCambioPlantilla(cert);
            if (requiereRectificacion)
            {
                cert = RectificarCertificadoFirmadoSiPlantillaCambio(cert);
            }

            if (cert == null)
                return Content("El certificado no existe.");

            if (string.IsNullOrWhiteSpace(cert.RutaPdf))
                return Content("El archivo PDF no está registrado.");

            string rutaFisica = ResolverRutaFisica(cert.RutaPdf);

            if (!System.IO.File.Exists(rutaFisica))
                return Content("El archivo PDF no se encuentra en el servidor.");

            var solicitud = _solicitudDao.ObtenerPorId(cert.CodigoSolicitud);
            var nombreArchivo = ConstruirNombrePdfCertificado(solicitud, cert, null);

            if (vistaPrevia)
            {
                DeshabilitarCacheRespuestaPdf();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreArchivo);
            return File(rutaFisica, "application/pdf");
        }

        // ============================================================
        //        GENERAR PDF DEL CERTIFICADO AOCR
        // ============================================================
        public ActionResult GenerarCertificadoPdf(int solicitudId, bool vistaPrevia = false)
        {
            try
            {
                if (vistaPrevia)
                {
                    DeshabilitarCacheRespuestaPdf();
                }

                var modelo = ConstruirViewModel(solicitudId);
                var nombreArchivo = ConstruirNombrePdfCertificado(modelo != null ? modelo.Solicitud : null, null, modelo);

                var pdf = new ViewAsPdf("~/Views/Certificado/CertificadoAOCR.cshtml", modelo)
                {
                    PageSize = Rotativa.Options.Size.A4,
                    PageOrientation = Rotativa.Options.Orientation.Portrait,
                    PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5),
                    CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
                };

                var pdfBytes = pdf.BuildFile(ControllerContext);
                Response.Headers["X-Content-Type-Options"] = "nosniff";
                PdfFileNameHelper.AplicarContentDispositionPdf(Response, !vistaPrevia, nombreArchivo);
                return File(pdfBytes, "application/pdf");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR GenerarCertificadoPdf: " + ex);
                return Content("<h3>Error al generar PDF</h3><pre>" +
                    System.Web.HttpUtility.HtmlEncode(ex.ToString()) + "</pre>", "text/html");
            }
        }

        private void DeshabilitarCacheRespuestaPdf()
        {
            if (Response == null)
            {
                return;
            }

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetRevalidation(HttpCacheRevalidation.AllCaches);
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
            Response.Cache.SetMaxAge(TimeSpan.Zero);
            Response.AppendHeader("Pragma", "no-cache");
        }

        private Certificado SincronizarCertificadoConFirmaRegistrada(Certificado cert)
        {
            if (cert == null || cert.CodigoSolicitud <= 0)
            {
                return cert;
            }

            AocrFirmaDocumento ultimaFirma = null;
            try
            {
                ultimaFirma = _firmaDocDao.ObtenerUltimoPorSolicitudTipo(cert.CodigoSolicitud, "CERTIFICADO_AOCR");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WARN AocrFirmaDocumento.ObtenerUltimoPorSolicitudTipo: " + ex);
                return cert;
            }

            if (ultimaFirma == null || string.IsNullOrWhiteSpace(ultimaFirma.RutaDocumento))
            {
                return cert;
            }

            if (string.Equals(cert.RutaDocumento, ultimaFirma.RutaDocumento, StringComparison.OrdinalIgnoreCase))
            {
                if (!cert.UpdatedAt.HasValue)
                {
                    cert.UpdatedAt = ultimaFirma.CreatedAt ?? ultimaFirma.FechaFirma;
                }

                if (string.IsNullOrWhiteSpace(cert.AprobadoPor))
                {
                    cert.AprobadoPor = ultimaFirma.NombreFirmante;
                }

                return cert;
            }

            cert.RutaDocumento = ultimaFirma.RutaDocumento;
            cert.Estado = "APROBADO";
            cert.UpdatedAt = ultimaFirma.CreatedAt ?? ultimaFirma.FechaFirma;

            if (string.IsNullOrWhiteSpace(cert.EmitidoPor))
            {
                cert.EmitidoPor = ultimaFirma.UsuarioNombre;
            }

            if (string.IsNullOrWhiteSpace(cert.AprobadoPor))
            {
                cert.AprobadoPor = ultimaFirma.NombreFirmante;
            }

            try
            {
                _certificadoDao.Actualizar(cert);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WARN Certificado.SincronizarRutaFirmada: " + ex);
            }

            return cert;
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

                cert = FirmarYPersistirCertificado(
                    solicitudId,
                    cert,
                    certificadoBytes,
                    password,
                    infoCert,
                    usuarioNombre,
                    usuarioNombre,
                    true);

                TempData["OK"] = "Certificado AOCR firmado correctamente por " + cert.AprobadoPor + ".";
                return RedirectToAction("Detalle", new { solicitudId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al firmar el certificado AOCR: " + ex.Message;
                return RedirectToAction("Detalle", new { solicitudId });
            }
        }

        private Certificado RectificarCertificadoFirmadoSiPlantillaCambio(Certificado cert)
        {
            if (!RequiereRectificacionPorCambioPlantilla(cert))
            {
                return cert;
            }

            try
            {
                byte[] certificadoBytes;
                string passwordConfigurado;
                string errorCert;
                if (!TryCargarCertificadoInstitucional(out certificadoBytes, out passwordConfigurado, out errorCert))
                {
                    System.Diagnostics.Debug.WriteLine("WARN Certificado.RectificarPlantilla: " + errorCert);
                    return cert;
                }

                if (string.IsNullOrWhiteSpace(passwordConfigurado))
                {
                    System.Diagnostics.Debug.WriteLine("WARN Certificado.RectificarPlantilla: no hay contraseña configurada para re-firma automática.");
                    return cert;
                }

                var infoCert = _firmaService.LeerCertificado(certificadoBytes, passwordConfigurado);
                if (infoCert == null || !infoCert.Exitoso)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "WARN Certificado.RectificarPlantilla: " +
                        (infoCert != null ? infoCert.Mensaje : "no se pudo validar el certificado institucional."));
                    return cert;
                }

                var usuarioEmision = !string.IsNullOrWhiteSpace(cert.EmitidoPor)
                    ? cert.EmitidoPor
                    : (User?.Identity?.Name ?? "Sistema");

                return FirmarYPersistirCertificado(
                    cert.CodigoSolicitud,
                    cert,
                    certificadoBytes,
                    passwordConfigurado,
                    infoCert,
                    usuarioEmision,
                    "Sistema AOCR",
                    false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WARN Certificado.RectificarPlantilla: " + ex);
                return cert;
            }
        }

        private bool RequiereRectificacionPorCambioPlantilla(Certificado cert)
        {
            if (cert == null || cert.CodigoSolicitud <= 0 || string.IsNullOrWhiteSpace(cert.RutaDocumento))
            {
                return false;
            }

            try
            {
                var fechaReferenciaUtc = ObtenerFechaReferenciaRectificacionCertificadoUtc();
                if (!fechaReferenciaUtc.HasValue)
                {
                    return false;
                }

                var rutaPdf = ResolverRutaFisica(cert.RutaDocumento);
                if (string.IsNullOrWhiteSpace(rutaPdf) || !System.IO.File.Exists(rutaPdf))
                {
                    return true;
                }

                return System.IO.File.GetLastWriteTimeUtc(rutaPdf) < fechaReferenciaUtc.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WARN Certificado.RequiereRectificacionPorCambioPlantilla: " + ex);
                return false;
            }
        }

        private DateTime? ObtenerFechaReferenciaRectificacionCertificadoUtc()
        {
            DateTime? fechaReferenciaUtc = null;

            ActualizarFechaReferenciaUtc(ref fechaReferenciaUtc, ResolverRutaFisica(VistaCertificadoAocr));
            ActualizarFechaReferenciaUtc(ref fechaReferenciaUtc, ResolverRutaFisica("~/bin/AOCR.dll"));

            try
            {
                var raizWeb = Server?.MapPath("~/");
                if (!string.IsNullOrWhiteSpace(raizWeb))
                {
                    var raizSolucion = System.IO.Path.GetFullPath(System.IO.Path.Combine(raizWeb, ".."));

                    ActualizarFechaReferenciaUtc(ref fechaReferenciaUtc, System.IO.Path.Combine(raizWeb, "Controllers", "6_CertificadoController.cs"));
                    ActualizarFechaReferenciaUtc(ref fechaReferenciaUtc, System.IO.Path.Combine(raizSolucion, "CapaNegocio", "Services", "FirmaDigitalService.cs"));
                    ActualizarFechaReferenciaUtc(ref fechaReferenciaUtc, System.IO.Path.Combine(raizSolucion, "CapaNegocio", "Services", "PdfTextAnchorLocator.cs"));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WARN Certificado.ObtenerFechaReferenciaRectificacionCertificadoUtc: " + ex);
            }

            return fechaReferenciaUtc;
        }

        private static void ActualizarFechaReferenciaUtc(ref DateTime? fechaReferenciaUtc, string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta) || !System.IO.File.Exists(ruta))
            {
                return;
            }

            var fechaRutaUtc = System.IO.File.GetLastWriteTimeUtc(ruta);
            if (!fechaReferenciaUtc.HasValue || fechaRutaUtc > fechaReferenciaUtc.Value)
            {
                fechaReferenciaUtc = fechaRutaUtc;
            }
        }

        private string ResolverRutaFisica(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta))
            {
                return null;
            }

            var rutaNormalizada = ruta.Trim();

            if (rutaNormalizada.StartsWith("~/", StringComparison.Ordinal))
            {
                return Server.MapPath(rutaNormalizada);
            }

            if (rutaNormalizada.StartsWith("/", StringComparison.Ordinal)
                || (rutaNormalizada.StartsWith("\\", StringComparison.Ordinal) && !rutaNormalizada.StartsWith("\\\\", StringComparison.Ordinal)))
            {
                return Server.MapPath("~/" + rutaNormalizada.TrimStart('/', '\\'));
            }

            return Path.IsPathRooted(rutaNormalizada)
                ? rutaNormalizada
                : Server.MapPath("~/" + rutaNormalizada.TrimStart('/', '\\'));
        }

        private Certificado FirmarYPersistirCertificado(
            int solicitudId,
            Certificado cert,
            byte[] certificadoBytes,
            string passwordFirma,
            InformacionCertificadoDigital infoCert,
            string usuarioNombreEmision,
            string usuarioNombreRegistroFirma,
            bool registrarHistorial)
        {
            if (cert == null)
            {
                throw new InvalidOperationException("No se pudo crear/obtener el registro del certificado AOCR.");
            }

            var modelo = ConstruirViewModel(solicitudId);
            var nombreFirmante = !string.IsNullOrWhiteSpace(infoCert?.NombreTitular)
                ? infoCert.NombreTitular
                : (usuarioNombreEmision ?? "Sistema");
            var cargoFirmante = "Coordinador/a Legal AOCR";
            var fechaFirma = DateTime.Now;

            var pdf = new ViewAsPdf(VistaCertificadoAocr, modelo)
            {
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5),
                CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
            };

            byte[] pdfBytes = pdf.BuildFile(ControllerContext);

            var contenidoQr = ConstruirContenidoQrCertificado(solicitudId, modelo, infoCert, nombreFirmante, cargoFirmante);
            var resultado = _firmaService.FirmarPdf(
                pdfBytes,
                certificadoBytes,
                passwordFirma,
                nombreFirmante,
                "Firma del Coordinador — Certificado AOCR",
                "Sistema AOCR DGAC",
                "DIRDAC",
                contenidoQr,
                null);

            if (resultado == null || !resultado.Exitoso)
            {
                throw new InvalidOperationException(
                    resultado != null && !string.IsNullOrWhiteSpace(resultado.Mensaje)
                        ? resultado.Mensaje
                        : "No se pudo aplicar la firma digital al certificado.");
            }

            var rutaRelativa = GuardarCertificadoFirmado(solicitudId, resultado.PdfFirmado, ConstruirNombrePdfCertificado(modelo != null ? modelo.Solicitud : null, cert, modelo));

            cert.RutaDocumento = rutaRelativa;
            cert.Estado = "APROBADO";
            cert.EmitidoPor = usuarioNombreEmision;
            cert.AprobadoPor = nombreFirmante;
            cert.UpdatedAt = fechaFirma;
            _certificadoDao.Actualizar(cert);

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
                    SujetoCertificado = resultado.SujetoCertificado ?? (infoCert != null ? infoCert.SujetoCertificado : null),
                    NombreFirmante = nombreFirmante,
                    CargoFirmante = cargoFirmante,
                    FechaFirma = fechaFirma,
                    CodigoUsuario = null,
                    UsuarioNombre = usuarioNombreRegistroFirma ?? usuarioNombreEmision
                });
            }
            catch (Exception exReg)
            {
                System.Diagnostics.Debug.WriteLine("WARN AocrFirmaDocumento.Registrar: " + exReg);
            }

            if (registrarHistorial)
            {
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
            }

            return cert;
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
                rutaAbsoluta = ResolverRutaFisica(rutaConfigurada);
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

        private string GuardarCertificadoFirmado(int solicitudId, byte[] contenido, string nombreArchivo = null)
        {
            var carpetaRelativa = "~/App_Data/Uploads/AOCR/Certificados/" + solicitudId;
            var carpetaAbsoluta = Server.MapPath(carpetaRelativa);
            if (!Directory.Exists(carpetaAbsoluta))
            {
                Directory.CreateDirectory(carpetaAbsoluta);
            }

            var nombreSeguro = !string.IsNullOrWhiteSpace(nombreArchivo)
                ? nombreArchivo
                : ("certificado_aocr_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + solicitudId + ".pdf");
            var rutaTentativa = Path.Combine(carpetaAbsoluta, nombreSeguro);
            if (System.IO.File.Exists(rutaTentativa))
            {
                var baseName = Path.GetFileNameWithoutExtension(nombreSeguro);
                var extension = Path.GetExtension(nombreSeguro);
                nombreSeguro = baseName + "_" + DateTime.Now.ToString("HHmmss") + extension;
            }
            var rutaAbsoluta = Path.Combine(carpetaAbsoluta, nombreSeguro);
            System.IO.File.WriteAllBytes(rutaAbsoluta, contenido ?? new byte[0]);

            return VirtualPathUtility.ToAbsolute(carpetaRelativa.TrimStart('~') + "/" + nombreSeguro);
        }

        private string ConstruirNombrePdfCertificado(SolicitudAOCR solicitud, Certificado cert, CertificadoAOCRViewModel modelo)
        {
            var numeroSolicitud = solicitud != null && !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                ? solicitud.NumeroSolicitud
                : (cert != null ? cert.CodigoSolicitud.ToString() : string.Empty);
            var nombreOperador = PdfFileNameHelper.PrimerValorNoVacio(
                PdfFileNameHelper.CombinarSegmentos(modelo != null ? modelo.RUC : null, modelo != null ? modelo.NombreExplotador : null),
                PdfFileNameHelper.CombinarSegmentos(solicitud != null ? solicitud.Ruc : null, solicitud != null ? solicitud.NombreOperador : null),
                PdfFileNameHelper.CombinarSegmentos(solicitud != null ? solicitud.Ruc : null, solicitud != null ? solicitud.NombreComercial : null),
                PdfFileNameHelper.CombinarSegmentos(solicitud != null ? solicitud.Ruc : null, solicitud != null ? solicitud.RazonSocial : null),
                modelo != null ? modelo.NombreExplotador : null,
                solicitud != null ? solicitud.NombreOperador : null,
                solicitud != null ? solicitud.NombreComercial : null,
                solicitud != null ? solicitud.RazonSocial : null,
                solicitud != null ? solicitud.Ruc : null);
            var fecha = cert != null
                ? (cert.FechaEmision ?? cert.UpdatedAt ?? cert.CreatedAt)
                : (modelo != null ? (DateTime?)modelo.FechaEmision : (solicitud != null ? (solicitud.UpdatedAt ?? solicitud.FechaSolicitud ?? solicitud.CreatedAt) : (DateTime?)null));

            return PdfFileNameHelper.CrearNombreCertificadoAocr(numeroSolicitud, nombreOperador, fecha);
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
                string logoPath = Server.MapPath("~/Content/assets/imganes/logo2.jpg");
                if (!System.IO.File.Exists(logoPath))
                    logoPath = Server.MapPath("~/Content/assets/imganes/logodgac.jpg");
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
