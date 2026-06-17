using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Direccion,DireccionJefaturaTecnica,DIRDAC,JefaturaTecnica")]
    public class FirmaAocrController : Controller
    {
        private readonly FirmaAocrAuthorizationService _authorizationService = new FirmaAocrAuthorizationService();
        private readonly FirmaAocrPdfService _pdfService = new FirmaAocrPdfService();
        private readonly FirmaAocrDigitalService _digitalService = new FirmaAocrDigitalService();
        private readonly FirmaAocrHistorialService _historialService = new FirmaAocrHistorialService();
        private readonly FirmaAocrFinalizacionService _finalizacionService = new FirmaAocrFinalizacionService();
        private readonly FirmaAocrNotificationService _notificationService = new FirmaAocrNotificationService();
        private readonly AocrFirmaDocumentoDAO _firmaDocumentoDao = new AocrFirmaDocumentoDAO();

        private FirmaAocrStorageService StorageService
        {
            get { return new FirmaAocrStorageService(Server); }
        }

        private FirmaAocrWorkflowService WorkflowService
        {
            get { return new FirmaAocrWorkflowService(_authorizationService, StorageService); }
        }

        [HttpGet]
        public ActionResult Index(int solicitudId)
        {
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PAGE_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre());
            Trace.TraceInformation("[FIRMA_AOCR_V2][PAGE_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre() + "; Rol=" + ObtenerRolActual());
            try
            {
                var model = WorkflowService.ConstruirViewModel(solicitudId, User, Url);
                Trace.TraceInformation(
                    "[FIRMA_AOCR_NUEVA][MODEL] SolicitudId=" + solicitudId +
                    "; PdfExiste=" + model.PdfExiste +
                    "; Firmado=" + model.PdfFirmadoExiste +
                    "; PuedeFirmar=" + model.PuedeFirmar +
                    "; CamposFaltantes=" + (model.CamposFaltantes != null ? model.CamposFaltantes.Count : 0));
                return View("~/Views/FirmaAocr/Index.cshtml", model);
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FIRMA_AOCR_NUEVA][ERROR] PAGE SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                Trace.TraceError("[FIRMA_AOCR_V2][ERROR] SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                throw;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GuardarDatos(GuardarDatosFirmaAocrRequest request)
        {
            var solicitudId = request != null ? request.SolicitudId : 0;
            try
            {
                if (request == null || solicitudId <= 0)
                {
                    return JsonError(400, "No se recibieron datos validos para guardar el AOCR.", solicitudId);
                }

                if (!_authorizationService.UsuarioPuedeEntrar(User))
                {
                    return JsonError(403, "Rol no autorizado para guardar datos AOCR.", solicitudId);
                }

                var errores = new System.Collections.Generic.List<string>();
                if (string.IsNullOrWhiteSpace(request.EstadoExplotador))
                {
                    errores.Add("Estado del explotador");
                }

                if (!request.FechaVencimiento.HasValue)
                {
                    errores.Add("Fecha de vencimiento");
                }

                if (errores.Count > 0)
                {
                    Response.StatusCode = 400;
                    return Json(new
                    {
                        ok = false,
                        message = "El AOCR tiene campos obligatorios incompletos.",
                        data = new
                        {
                            camposFaltantes = errores,
                            puedeGenerarPdf = false,
                            puedeFirmar = false
                        }
                    });
                }

                var resultado = WorkflowService.GuardarDatosObligatorios(
                    solicitudId,
                    request.EstadoExplotador,
                    request.FechaVencimiento,
                    ObtenerUsuarioActualId(),
                    ObtenerUsuarioActualNombre());

                Response.StatusCode = resultado.Ok ? 200 : 400;
                return Json(new
                {
                    ok = resultado.Ok,
                    message = resultado.Message,
                    data = new
                    {
                        solicitudId = resultado.SolicitudId,
                        camposFaltantes = resultado.CamposFaltantes ?? new System.Collections.Generic.List<string>(),
                        puedeGenerarPdf = resultado.PuedeGenerarPdf,
                        puedeFirmar = resultado.PuedeFirmar,
                        camposActualizados = resultado.CamposActualizados,
                        urlGenerar = Url.Action("GenerarPdf", "FirmaAocr", new { solicitudId })
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FIRMA_AOCR_V2][ERROR] GUARDAR_DATOS SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                return JsonError(500, "Error interno al guardar datos AOCR. " + ex.Message, solicitudId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult GenerarPdf(int solicitudId)
        {
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][GENERAR_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre());
            try
            {
                var workflow = WorkflowService;
                var contexto = workflow.CargarContexto(solicitudId);
                if (contexto == null || contexto.Solicitud == null)
                {
                    return JsonError(404, "La solicitud AOCR indicada no existe.", solicitudId);
                }

                if (!_authorizationService.UsuarioPuedeEntrar(User))
                {
                    return JsonError(403, "Rol no autorizado para generar el PDF oficial AOCR.", solicitudId);
                }

                if (contexto.CamposFaltantes != null && contexto.CamposFaltantes.Count > 0)
                {
                    return JsonCamposFaltantes(solicitudId, contexto.CamposFaltantes);
                }

                if (!FirmaAocrWorkflowService.InformeAprobadoDireccion(contexto.Informe))
                {
                    return JsonError(409, "El informe tecnico no esta aprobado por Direccion.", solicitudId);
                }

                var pdfBytes = _pdfService.GenerarPdfOficial(ControllerContext, contexto.Documento);
                if (pdfBytes == null || pdfBytes.LongLength <= 0)
                {
                    return JsonError(500, "No se pudo generar el PDF oficial AOCR.", solicitudId);
                }

                var ruta = StorageService.GuardarPdfOficial(solicitudId, pdfBytes);
                var rutaFisica = StorageService.ResolverRutaFisica(ruta);
                var existe = !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica);
                var bytes = existe ? new FileInfo(rutaFisica).Length : 0;
                if (!existe || bytes <= 0)
                {
                    return JsonError(500, "El PDF oficial se genero, pero no se pudo verificar el archivo fisico.", solicitudId);
                }

                workflow.SincronizarCertificadoPdfOficial(contexto, ruta, ObtenerUsuarioActualId(), ObtenerUsuarioActualNombre());
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][GENERAR_OK] SolicitudId=" + solicitudId + "; Ruta=" + ruta + "; Bytes=" + bytes + "; Paginas=2");
                Trace.TraceInformation("[FIRMA_AOCR_V2][GENERAR_PDF_OK] SolicitudId=" + solicitudId + "; RutaPdf=" + ruta + "; Bytes=" + bytes + "; Paginas=2");

                return JsonOk("PDF oficial AOCR generado correctamente.", new
                {
                    solicitudId,
                    rutaPdf = ruta,
                    bytes,
                    urlVer = Url.Action("VerPdf", "FirmaAocr", new { solicitudId, firmado = false }),
                    urlDescarga = Url.Action("DescargarPdf", "FirmaAocr", new { solicitudId, firmado = false })
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FIRMA_AOCR_NUEVA][ERROR] GENERAR SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                Trace.TraceError("[FIRMA_AOCR_V2][ERROR] SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                return JsonError(500, "Error interno al generar PDF AOCR. " + ex.Message, solicitudId);
            }
        }

        [HttpGet]
        public ActionResult VerPdf(int solicitudId, bool firmado = false)
        {
            return ServirPdf(solicitudId, firmado, false);
        }

        [HttpGet]
        public ActionResult DescargarPdf(int solicitudId, bool firmado = false)
        {
            return ServirPdf(solicitudId, firmado, true);
        }

        [HttpGet]
        public ActionResult DescargarFirmado(int solicitudId)
        {
            return ServirPdf(solicitudId, true, true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Firmar(FirmarAocrInstitucionalRequest request, int solicitudId = 0)
        {
            var id = solicitudId > 0 ? solicitudId : (request != null ? request.SolicitudId : 0);
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][FIRMAR_IN] SolicitudId=" + id + "; Usuario=" + ObtenerUsuarioActualNombre());
            Trace.TraceInformation("[FIRMA_AOCR_V2][FIRMAR_IN] SolicitudId=" + id + "; Usuario=" + ObtenerUsuarioActualNombre() + "; TieneCertificado=" + (request != null && request.CertificadoDigital != null && request.CertificadoDigital.ContentLength > 0) + "; TienePassword=" + (request != null && !string.IsNullOrWhiteSpace(request.PasswordCertificado)));
            try
            {
                if (request == null)
                {
                    return JsonError(400, "No se recibieron datos validos para firmar el AOCR.", id);
                }

                request.SolicitudId = id;
                if (id <= 0)
                {
                    return JsonError(400, "Solicitud AOCR invalida.", id);
                }

                if (!_authorizationService.UsuarioPuedeEntrar(User))
                {
                    return JsonError(403, "Solo Direccion / DIRDAC puede firmar el AOCR final.", id);
                }

                var workflow = WorkflowService;
                var contexto = workflow.CargarContexto(id);
                if (contexto == null || contexto.Solicitud == null)
                {
                    return JsonError(404, "No existe contexto documental AOCR para firmar.", id);
                }

                if (contexto.PdfFirmadoExiste)
                {
                    return JsonError(409, "El AOCR ya fue firmado oficialmente.", id);
                }

                var rutaOrigen = contexto.Certificado != null ? contexto.Certificado.RutaDocumento : null;
                var rutaFisicaOrigen = StorageService.ResolverRutaFisica(rutaOrigen);
                var pdfExiste = !string.IsNullOrWhiteSpace(rutaFisicaOrigen) && System.IO.File.Exists(rutaFisicaOrigen);
                var bytesOrigen = pdfExiste ? new FileInfo(rutaFisicaOrigen).Length : 0;
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PDF_ORIGEN] SolicitudId=" + id + "; Ruta=" + (rutaOrigen ?? string.Empty) + "; Existe=" + pdfExiste + "; Bytes=" + bytesOrigen);
                if (!pdfExiste || bytesOrigen <= 0)
                {
                    return JsonError(409, "Primero debe generar el PDF oficial AOCR.", id);
                }

                if (contexto.CamposFaltantes != null && contexto.CamposFaltantes.Count > 0)
                {
                    return JsonCamposFaltantes(id, contexto.CamposFaltantes);
                }

                if (!FirmaAocrWorkflowService.InformeAprobadoDireccion(contexto.Informe))
                {
                    return JsonError(409, "El informe tecnico no esta aprobado por Direccion.", id);
                }

                var archivoCertificado = request.CertificadoDigital;
                if (archivoCertificado == null || archivoCertificado.ContentLength <= 0)
                {
                    return JsonError(400, "Debe seleccionar el certificado digital .p12 o .pfx.", id);
                }

                var extension = Path.GetExtension(archivoCertificado.FileName ?? string.Empty);
                if (!string.Equals(extension, ".p12", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".pfx", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonError(400, "Solo se admiten certificados digitales .p12 o .pfx.", id);
                }

                if (string.IsNullOrWhiteSpace(request.PasswordCertificado))
                {
                    return JsonError(400, "Debe ingresar la contrasena del certificado.", id);
                }

                byte[] certificadoBytes;
                using (var ms = new MemoryStream())
                {
                    archivoCertificado.InputStream.CopyTo(ms);
                    certificadoBytes = ms.ToArray();
                }

                var infoCertificado = _digitalService.LeerCertificado(certificadoBytes, request.PasswordCertificado);
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][CERT_VALID] SolicitudId=" + id + "; Exitoso=" + (infoCertificado != null && infoCertificado.Exitoso) + "; Sujeto=" + (infoCertificado != null ? infoCertificado.SujetoCertificado : string.Empty));
                Trace.TraceInformation("[FIRMA_AOCR_V2][CERT_VALID] Extension=" + extension + "; Bytes=" + certificadoBytes.LongLength + "; TieneClavePrivada=" + (infoCertificado != null && infoCertificado.Exitoso) + "; Vigente=" + (infoCertificado != null && infoCertificado.Exitoso));
                if (infoCertificado == null || !infoCertificado.Exitoso)
                {
                    return JsonError(400, infoCertificado != null ? infoCertificado.Mensaje : "No se pudo abrir el certificado digital.", id);
                }

                var pdfOrigen = System.IO.File.ReadAllBytes(rutaFisicaOrigen);
                var nombreFirmante = FirmaAocrWorkflowService.PrimerValorNoVacio(infoCertificado.NombreTitular, ObtenerUsuarioActualNombre());
                var contenidoQr = ConstruirContenidoQr(contexto, infoCertificado, nombreFirmante);
                var resultadoFirma = _digitalService.Firmar(pdfOrigen, certificadoBytes, request.PasswordCertificado, nombreFirmante, contenidoQr);
                if (resultadoFirma == null || !resultadoFirma.Exitoso || resultadoFirma.PdfFirmado == null || resultadoFirma.PdfFirmado.LongLength <= 0)
                {
                    return JsonError(500, resultadoFirma != null ? resultadoFirma.Mensaje : "No se pudo firmar digitalmente el AOCR.", id);
                }

                var rutaFirmada = StorageService.GuardarPdfFirmado(id, resultadoFirma.PdfFirmado);
                var rutaFisicaFirmada = StorageService.ResolverRutaFisica(rutaFirmada);
                var firmadoExiste = !string.IsNullOrWhiteSpace(rutaFisicaFirmada) && System.IO.File.Exists(rutaFisicaFirmada);
                var bytesFirmado = firmadoExiste ? new FileInfo(rutaFisicaFirmada).Length : 0;
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PDF_FIRMADO] SolicitudId=" + id + "; Ruta=" + rutaFirmada + "; Existe=" + firmadoExiste + "; Bytes=" + bytesFirmado + "; Hash=" + (resultadoFirma.HashSha256 ?? string.Empty));
                Trace.TraceInformation("[FIRMA_AOCR_V2][PDF_FIRMADO] SolicitudId=" + id + "; RutaFirmada=" + rutaFirmada + "; Bytes=" + bytesFirmado + "; Hash=" + (resultadoFirma.HashSha256 ?? string.Empty));
                if (!firmadoExiste || bytesFirmado <= 0 || string.IsNullOrWhiteSpace(resultadoFirma.HashSha256))
                {
                    return JsonError(500, "La firma se genero, pero no se pudo verificar el archivo PDF firmado.", id);
                }

                RegistrarFirma(contexto, rutaFirmada, resultadoFirma, contenidoQr, nombreFirmante, bytesFirmado);
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][DB_UPDATE] SolicitudId=" + id + "; EstadoAocrNuevo=AOCR_FIRMADO_DIRDAC; FilasAfectadas=1");
                Trace.TraceInformation("[FIRMA_AOCR_V2][DB_UPDATE] SolicitudId=" + id + "; EstadoAnterior=" + (contexto.Solicitud.Estado ?? string.Empty) + "; EstadoNuevo=AOCR_FIRMADO_DIRDAC; FilasAfectadas=1");

                var finalizacion = _finalizacionService.LiberarDocumentoFinal(id, ObtenerUsuarioActualId(), StorageService.Existe);
                var estadoSolicitudNuevo = finalizacion != null && !string.IsNullOrWhiteSpace(finalizacion.EstadoNuevo)
                    ? finalizacion.EstadoNuevo
                    : "AOCR_LEGALIZADO";
                _historialService.Registrar(id, contexto.Solicitud.Estado, estadoSolicitudNuevo, ObtenerUsuarioActualId(), "Firma institucional AOCR DIRDAC. Documento final liberado.");
                _notificationService.NotificarLiberacion(id, rutaFirmada);

                var urlDescarga = Url.Action("DescargarFirmado", "FirmaAocr", new { solicitudId = id });
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][OK] SolicitudId=" + id + "; EstadoAocr=AOCR_FIRMADO_DIRDAC; UrlDescarga=" + urlDescarga);
                Trace.TraceInformation("[FIRMA_AOCR_V2][OK] SolicitudId=" + id + "; RutaFirmada=" + rutaFirmada + "; Hash=" + (resultadoFirma.HashSha256 ?? string.Empty) + "; Bytes=" + bytesFirmado);

                return Json(new
                {
                    ok = true,
                    code = 200,
                    message = "AOCR firmada oficialmente por Direccion / DIRDAC.",
                    estadoAocr = "AOCR_FIRMADO_DIRDAC",
                    urlDescarga = urlDescarga,
                    data = new
                    {
                        solicitudId = id,
                        estadoAocr = "AOCR_FIRMADO_DIRDAC",
                        estadoSolicitud = estadoSolicitudNuevo,
                        rutaOrigen,
                        rutaFirmada,
                        hash = resultadoFirma.HashSha256,
                        bytes = bytesFirmado,
                        urlDescarga
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FIRMA_AOCR_NUEVA][ERROR] FIRMAR SolicitudId=" + id + "; Motivo=" + ex.Message + "; Exception=" + ex);
                Trace.TraceError("[FIRMA_AOCR_V2][ERROR] SolicitudId=" + id + "; Motivo=" + ex.Message + "; Exception=" + ex);
                return JsonError(500, "Error interno al firmar AOCR. " + ex.Message, id);
            }
        }

        private ActionResult ServirPdf(int solicitudId, bool firmado, bool descargar)
        {
            var contexto = WorkflowService.CargarContexto(solicitudId);
            if (contexto == null || contexto.Solicitud == null)
            {
                return HttpNotFound("La solicitud AOCR indicada no existe.");
            }

            var ruta = firmado
                ? (contexto.Firma != null ? contexto.Firma.RutaDocumento : null)
                : (contexto.Certificado != null ? contexto.Certificado.RutaDocumento : null);
            var rutaFisica = StorageService.ResolverRutaFisica(ruta);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                return new HttpStatusCodeResult(404, firmado ? "No existe AOCR firmado para descargar." : "No existe PDF oficial AOCR generado.");
            }

            var nombreBase = (contexto.Solicitud.NumeroSolicitud ?? ("AOCR-" + solicitudId)).Replace("/", "-").Replace("\\", "-");
            var nombre = firmado ? nombreBase + "-firmado.pdf" : nombreBase + "-oficial.pdf";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (descargar)
            {
                Response.AppendHeader("Content-Disposition", "attachment; filename=\"" + nombre + "\"");
            }
            else
            {
                Response.AppendHeader("Content-Disposition", "inline; filename=\"" + nombre + "\"");
            }

            return File(rutaFisica, "application/pdf");
        }

        private void RegistrarFirma(FirmaAocrContexto contexto, string rutaFirmada, ResultadoFirmaDigital resultadoFirma, string contenidoQr, string nombreFirmante, long bytesFirmado)
        {
            var solicitudId = contexto != null && contexto.Solicitud != null ? contexto.Solicitud.CodigoSolicitud : 0;
            var firma = new AocrFirmaDocumento
            {
                CodigoSolicitud = solicitudId,
                CodigoInspeccion = contexto != null && contexto.Inspeccion != null ? (int?)contexto.Inspeccion.CodigoInspeccion : null,
                TipoDocumento = "RECONOCIMIENTO",
                NumeroAocr = contexto != null && contexto.Documento != null ? contexto.Documento.NumeroAocr : null,
                NombreArchivo = Path.GetFileName(StorageService.ResolverRutaFisica(rutaFirmada)),
                RutaDocumento = rutaFirmada,
                HashDocumento = resultadoFirma.HashSha256,
                TamanioPdfFirmado = bytesFirmado,
                FirmadoPorRol = "DIRECCION_DIRDAC",
                CodigoQr = contenidoQr,
                SujetoCertificado = resultadoFirma.SujetoCertificado,
                NombreFirmante = nombreFirmante,
                CargoFirmante = contexto != null && contexto.Documento != null ? contexto.Documento.CargoFirmante : "Direccion General de Aviacion Civil",
                FechaFirma = DateTime.Now,
                CodigoUsuario = ObtenerUsuarioActualId() > 0 ? (int?)ObtenerUsuarioActualId() : null,
                UsuarioNombre = ObtenerUsuarioActualNombre()
            };

            _firmaDocumentoDao.Registrar(firma);
        }

        private string ConstruirContenidoQr(FirmaAocrContexto contexto, InformacionCertificadoDigital infoCertificado, string nombreFirmante)
        {
            var solicitud = contexto != null ? contexto.Solicitud : null;
            return string.Join(" | ", new[]
            {
                "Modulo=Firma Institucional AOCR",
                "SolicitudId=" + (solicitud != null ? solicitud.CodigoSolicitud.ToString() : string.Empty),
                "NumeroSolicitud=" + (solicitud != null ? solicitud.NumeroSolicitud : string.Empty),
                "EstadoAocr=AOCR_FIRMADO_DIRDAC",
                "Firmante=" + (nombreFirmante ?? string.Empty),
                "Fecha=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Certificado=" + (infoCertificado != null ? infoCertificado.SujetoCertificado : string.Empty)
            });
        }

        private ActionResult JsonOk(string message, object data)
        {
            return Json(new
            {
                ok = true,
                code = 200,
                message,
                data
            });
        }

        private ActionResult JsonError(int code, string message, int solicitudId)
        {
            Trace.TraceError("[FIRMA_AOCR_V2][ERROR] SolicitudId=" + solicitudId + "; Motivo=" + (message ?? string.Empty) + "; Exception=");
            Response.StatusCode = code;
            return Json(new
            {
                ok = false,
                code,
                message,
                data = new
                {
                    solicitudId,
                    redirectUrl = Url.Action("Index", "FirmaAocr", new { solicitudId })
                }
            }, JsonRequestBehavior.AllowGet);
        }

        private ActionResult JsonCamposFaltantes(int solicitudId, System.Collections.Generic.IEnumerable<string> camposFaltantes)
        {
            var campos = camposFaltantes != null
                ? new System.Collections.Generic.List<string>(camposFaltantes)
                : new System.Collections.Generic.List<string>();
            Trace.TraceError("[FIRMA_AOCR_V2][ERROR] SolicitudId=" + solicitudId + "; Motivo=Campos obligatorios incompletos; Exception=");
            Response.StatusCode = 400;
            return Json(new
            {
                ok = false,
                code = 400,
                message = "El AOCR tiene campos obligatorios incompletos.",
                data = new
                {
                    solicitudId,
                    camposFaltantes = campos,
                    puedeGenerarPdf = false,
                    puedeFirmar = false,
                    redirectUrl = Url.Action("Index", "FirmaAocr", new { solicitudId })
                }
            }, JsonRequestBehavior.AllowGet);
        }

        private int ObtenerUsuarioActualId()
        {
            object valor = Session != null ? (Session["IdUsuario"] ?? Session["UserId"]) : null;
            int id;
            if (valor != null && int.TryParse(Convert.ToString(valor), out id))
            {
                return id;
            }

            valor = Session != null ? Session["CodigoUsuario"] : null;
            if (valor != null && int.TryParse(Convert.ToString(valor), out id))
            {
                return id;
            }

            return 0;
        }

        private string ObtenerUsuarioActualNombre()
        {
            var nombreSesion = Session != null ? Convert.ToString(Session["NombreUsuario"] ?? Session["NombreCompleto"]) : null;
            if (!string.IsNullOrWhiteSpace(nombreSesion))
            {
                return nombreSesion.Trim();
            }

            return User != null && User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name)
                ? User.Identity.Name.Trim()
                : "Sistema AOCR";
        }

        private string ObtenerRolActual()
        {
            return _authorizationService.ObtenerRolActual(User);
        }
    }
}
