using System;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;
using CapaPresentacion.Models;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = CapaDatos.Constants.AocrRolesInstitucionales.RolesAccesoMvc)]
    public class FirmaAocrController : Controller
    {
        private readonly FirmaAocrAuthorizationService _authorizationService = new FirmaAocrAuthorizationService();
        private readonly FirmaAocrPdfService _pdfService = new FirmaAocrPdfService();
        private readonly FirmaAocrDigitalService _digitalService = new FirmaAocrDigitalService();
        private readonly CapaNegocio.Interfaces.IUsuarioContextoService _usuarioContexto = System.Web.Mvc.DependencyResolver.Current.GetService<CapaNegocio.Interfaces.IUsuarioContextoService>() ?? new CapaNegocio.Services.UsuarioContextoService();
        private readonly CapaNegocio.Interfaces.IDocumentoFirmaService _firmaService = new CapaNegocio.Services.DocumentoFirmaService();
        private readonly DocumentosFinalesWorkflowService _documentosFinalesService = new DocumentosFinalesWorkflowService();

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
            var permisos = ObtenerPermisosRolActivo();
            if (!permisos.EsInspectorRol && !permisos.EsCoordinadorRol && !permisos.EsDirdacRol && !permisos.EsDcavRol && !permisos.EsAdministrador)
                return new HttpStatusCodeResult(403, "No está autorizado para gestionar documentos finales con el rol activo.");
            if (permisos.EsInspectorRol && !EsSolicitudAsignadaAlInspectorActivo(solicitudId))
                return new HttpStatusCodeResult(403, "El expediente no está asignado al Inspector autenticado.");
            var expedientePendienteInspector = !permisos.EsInspectorRol || EsExpedientePendienteDelInspectorActivo(solicitudId);

            Trace.TraceInformation("[FIRMA_AOCR][OPEN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre() + "; Rol=" + ObtenerRolActual() + ";");
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PAGE_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre());
            Trace.TraceInformation("[FIRMA_AOCR_V2][PAGE_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre() + "; Rol=" + ObtenerRolActual());
            try
            {
                var model = WorkflowService.ConstruirViewModel(solicitudId, User, Url);
                FirmaAocrActiveRoleViewPolicy.Aplicar(model, permisos);
                if (model != null && permisos.EsInspectorRol)
                {
                    var contextoInspector = WorkflowService.CargarContexto(solicitudId);
                    var asignado = contextoInspector != null && contextoInspector.Inspeccion != null
                        && EsSolicitudAsignadaAlInspectorActivo(solicitudId);
                    if (!asignado)
                    {
                        model.EsInspector = false;
                        model.PuedeGuardarDatos = false;
                        model.PuedeGenerar = false;
                        model.PuedeRegenerar = false;
                        model.PuedeEnviarParaFirma = false;
                        foreach (var documento in model.Documentos ?? new System.Collections.Generic.List<FirmaAocrDocumentoItemViewModel>())
                            documento.PuedeGenerar = false;
                        model.MotivoBloqueo = "Solo el Inspector asignado puede modificar los documentos finales.";
                    }
                    else if (!expedientePendienteInspector)
                    {
                        model.EsInspector = false;
                        model.PuedeGuardarDatos = false;
                        model.PuedeGenerar = false;
                        model.PuedeRegenerar = false;
                        model.PuedeEnviarParaFirma = false;
                        foreach (var documento in model.Documentos ?? new System.Collections.Generic.List<FirmaAocrDocumentoItemViewModel>())
                        {
                            documento.PuedeGenerar = false;
                            documento.PuedeFirmar = false;
                        }
                        model.MotivoBloqueo = "Los documentos finales ya fueron enviados a DIRDAC y DCAV para sus firmas institucionales.";
                    }
                }
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

                if (!EsRolInspectorActivo() || !_authorizationService.UsuarioPuedeGenerar(User))
                {
                    return JsonError(403, "Solo el Inspector asignado puede guardar los borradores finales.", solicitudId);
                }

                var contextoGuardar = WorkflowService.CargarContexto(solicitudId);
                if (contextoGuardar == null || contextoGuardar.Inspeccion == null
                    || !EsSolicitudAsignadaAlInspectorActivo(solicitudId))
                    return JsonError(403, "Solo el Inspector asignado puede modificar estos documentos.", solicitudId);
                if (!EsExpedientePendienteDelInspectorActivo(solicitudId))
                    return JsonError(409, "El expediente ya no está habilitado para generar documentos finales.", solicitudId);

                var estadoExplotador = PrimerValorFormulario(
                    request.EstadoExplotador,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["EstadoExplotador"] : null,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["estadoExplotador"] : null);
                var fechaVencimientoRaw = PrimerValorFormulario(
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["FechaVencimiento"] : null,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["fechaVencimiento"] : null,
                    request.FechaVencimiento.HasValue ? request.FechaVencimiento.Value.ToString("yyyy-MM-dd") : null);
                var fechaVencimiento = ParseFechaAocr(fechaVencimientoRaw);
                var nombreDirectorGeneral = PrimerValorFormulario(
                    request.NombreDirectorGeneral,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["NombreDirectorGeneral"] : null);
                var nombreDirectorCertificacion = PrimerValorFormulario(
                    request.NombreDirectorCertificacion,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["NombreDirectorCertificacion"] : null);

                estadoExplotador = NormalizarMayusculas(estadoExplotador);
                nombreDirectorGeneral = NormalizarMayusculas(nombreDirectorGeneral);
                nombreDirectorCertificacion = NormalizarMayusculas(nombreDirectorCertificacion);

                Trace.TraceInformation("[FIRMA_AOCR][GUARDAR_DATOS_IN] SolicitudId=" + solicitudId
                    + "; EstadoExplotador='" + (estadoExplotador ?? string.Empty) + "'"
                    + "; FechaVencimientoRaw='" + (fechaVencimientoRaw ?? string.Empty) + "';");

                var errores = new System.Collections.Generic.List<string>();
                if (string.IsNullOrWhiteSpace(estadoExplotador))
                {
                    errores.Add("Estado del explotador");
                }

                if (!fechaVencimiento.HasValue)
                {
                    errores.Add("Fecha de vencimiento");
                }
                if (string.IsNullOrWhiteSpace(nombreDirectorGeneral))
                {
                    errores.Add("Nombre del Director General de Aviacion Civil");
                }
                if (string.IsNullOrWhiteSpace(nombreDirectorCertificacion))
                {
                    errores.Add("Nombre del Director de Certificacion Aeronautica y Vigilancia Continua");
                }
                if ((nombreDirectorGeneral ?? string.Empty).Length > 100 || (nombreDirectorCertificacion ?? string.Empty).Length > 100)
                {
                    errores.Add("Los nombres de los firmantes no pueden superar 100 caracteres");
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
                    estadoExplotador,
                    fechaVencimiento,
                    nombreDirectorGeneral,
                    nombreDirectorCertificacion,
                    ObtenerUsuarioActualId(),
                    ObtenerUsuarioActualNombre());

                Trace.TraceInformation("[FIRMA_AOCR][GUARDAR_DATOS_OK] SolicitudId=" + solicitudId
                    + "; FechaVencimiento=" + (fechaVencimiento.HasValue ? fechaVencimiento.Value.ToString("yyyy-MM-dd") : string.Empty) + ";");

                Response.StatusCode = resultado.Ok ? 200 : 400;
                return Json(new
                {
                    ok = resultado.Ok,
                    message = resultado.Message,
                    datosCompletos = resultado.CamposFaltantes == null || resultado.CamposFaltantes.Count == 0,
                    estadoExplotador = estadoExplotador,
                    fechaVencimiento = fechaVencimiento.HasValue ? fechaVencimiento.Value.ToString("dd/MM/yyyy") : string.Empty,
                    puedeGenerarPdf = resultado.PuedeGenerarPdf,
                    puedeFirmar = resultado.PuedeFirmar,
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
            var tipoDocumento = FirmaAocrWorkflowService.NormalizarTipoDocumento(Request != null ? Request["tipoDocumento"] : null);
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][GENERAR_IN] SolicitudId=" + solicitudId + "; TipoDocumento=" + tipoDocumento + "; Usuario=" + ObtenerUsuarioActualNombre());
            try
            {
                var workflow = WorkflowService;
                var contexto = workflow.CargarContexto(solicitudId);
                if (contexto == null || contexto.Solicitud == null)
                {
                    return JsonError(404, "La solicitud AOCR indicada no existe.", solicitudId);
                }

                if (!EsRolInspectorActivo() || !_authorizationService.UsuarioPuedeGenerar(User))
                {
                    return JsonError(403, "Solo el Inspector asignado puede generar los PDF finales.", solicitudId);
                }
                if (contexto.Inspeccion == null || !EsSolicitudAsignadaAlInspectorActivo(solicitudId))
                    return JsonError(403, "Solo el Inspector asignado puede generar estos documentos.", solicitudId);
                if (!EsExpedientePendienteDelInspectorActivo(solicitudId))
                    return JsonError(409, "El expediente ya no está habilitado para generar documentos finales.", solicitudId);

                string motivoTipoTramite;
                if (!new AocrCierrePorTipoTramiteService().PuedeGenerarDocumento(contexto.Solicitud, tipoDocumento, out motivoTipoTramite))
                    return JsonError(409, motivoTipoTramite, solicitudId);

                if (contexto.CamposFaltantes != null && contexto.CamposFaltantes.Count > 0)
                {
                    return JsonCamposFaltantes(solicitudId, contexto.CamposFaltantes);
                }

                if (!FirmaAocrWorkflowService.InformeAprobadoDireccion(contexto.Informe))
                {
                    return JsonError(409, "El informe tecnico no esta aprobado por Direccion.", solicitudId);
                }

                var firmaExistente = ObtenerFirmaPorTipo(contexto, tipoDocumento);
                if (firmaExistente != null && !string.IsNullOrWhiteSpace(firmaExistente.RutaDocumento) && StorageService.Existe(firmaExistente.RutaDocumento))
                {
                    return JsonError(409, "No se puede regenerar un documento AOCR ya firmado.", solicitudId);
                }

                var pdfBytes = string.Equals(tipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase)
                    ? _pdfService.GenerarPdfCondiciones(ControllerContext, contexto.Documento)
                    : _pdfService.GenerarPdfReconocimiento(ControllerContext, contexto.Documento);
                if (pdfBytes == null || pdfBytes.LongLength <= 0)
                {
                    return JsonError(500, "No se pudo generar el PDF oficial AOCR.", solicitudId);
                }

                var ruta = StorageService.GuardarPdfDocumento(solicitudId, tipoDocumento, pdfBytes);
                var rutaFisica = StorageService.ResolverRutaFisica(ruta);
                var existe = !string.IsNullOrWhiteSpace(rutaFisica) && System.IO.File.Exists(rutaFisica);
                var bytes = existe ? new FileInfo(rutaFisica).Length : 0;
                if (!existe || bytes <= 0)
                {
                    Trace.TraceError("[FIRMA_AOCR][PDF_VERIFY_FAIL] SolicitudId=" + solicitudId
                        + "; RutaRelativa=" + (ruta ?? string.Empty)
                        + "; RutaFisica=" + (rutaFisica ?? string.Empty)
                        + "; Existe=" + existe
                        + "; Bytes=" + bytes + ";");
                    return JsonError(500, "El PDF oficial se genero, pero no se pudo verificar el archivo fisico.", solicitudId);
                }

                workflow.RegistrarDocumentoGenerado(contexto, tipoDocumento, ruta, bytes, ObtenerUsuarioActualId(), ObtenerUsuarioActualNombre());
                CapaNegocio.LogBL.RegistrarInfo(
                    "[FIRMA_AOCR][GENERAR_PDF_OK] SolicitudId=" + solicitudId
                    + "; TipoDocumento=" + tipoDocumento
                    + "; Ruta=" + ruta
                    + "; Bytes=" + bytes + ";",
                    "FirmaAocrController",
                    ObtenerUsuarioActualId());
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][GENERAR_OK] SolicitudId=" + solicitudId + "; TipoDocumento=" + tipoDocumento + "; Ruta=" + ruta + "; Bytes=" + bytes + "; Paginas=1");
                Trace.TraceInformation("[FIRMA_AOCR_V2][GENERAR_PDF_OK] SolicitudId=" + solicitudId + "; TipoDocumento=" + tipoDocumento + "; RutaPdf=" + ruta + "; Bytes=" + bytes + "; Paginas=1");

                return JsonOk("PDF oficial AOCR generado correctamente.", new
                {
                    solicitudId,
                    rutaPdf = ruta,
                    bytes,
                    tipoDocumento,
                    urlVer = Url.Action("VerPdf", "FirmaAocr", new { solicitudId, tipoDocumento, firmado = false }),
                    urlDescarga = Url.Action("DescargarPdf", "FirmaAocr", new { solicitudId, tipoDocumento, firmado = false })
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError("[FIRMA_AOCR_NUEVA][ERROR] GENERAR SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                Trace.TraceError("[FIRMA_AOCR_V2][ERROR] SolicitudId=" + solicitudId + "; Motivo=" + ex.Message + "; Exception=" + ex);
                CapaNegocio.LogBL.RegistrarError(
                    "[FIRMA_AOCR][GENERAR_PDF_ERROR] SolicitudId=" + solicitudId
                    + "; TipoDocumento=" + tipoDocumento
                    + "; Usuario=" + ObtenerUsuarioActualNombre(),
                    ex.ToString(),
                    "FirmaAocrController",
                    ObtenerUsuarioActualId());
                return JsonError(500, "Error interno al generar PDF AOCR. " + ex.Message, solicitudId);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FinalizarDocumentosYEnviarParaFirma(int solicitudId)
        {
            if (!EsRolInspectorActivo() || !_authorizationService.UsuarioPuedeGenerar(User))
                return JsonError(403, "Solo el Inspector asignado puede ejecutar el envio conjunto.", solicitudId);

            var contexto = WorkflowService.CargarContexto(solicitudId);
            if (contexto == null || contexto.Inspeccion == null)
                return JsonError(404, "No existe una inspeccion vigente para el expediente.", solicitudId);
            if (!EsSolicitudAsignadaAlInspectorActivo(solicitudId))
                return JsonError(403, "Solo el Inspector asignado puede enviar estos documentos.", solicitudId);
            if (!EsExpedientePendienteDelInspectorActivo(solicitudId))
                return JsonError(409, "El expediente ya no está habilitado para enviar documentos finales.", solicitudId);

            var baseUrl = Request != null && Request.Url != null
                ? Request.Url.GetLeftPart(UriPartial.Authority) + (Request.ApplicationPath == "/" ? string.Empty : Request.ApplicationPath)
                : string.Empty;
            var resultado = _documentosFinalesService.FinalizarDocumentosYEnviarParaFirma(
                solicitudId,
                contexto.Inspeccion.CodigoInspeccion,
                contexto.Inspeccion.CodigoInspector.GetValueOrDefault(),
                ObtenerUsuarioActualNombre(),
                baseUrl,
                ruta => StorageService.ResolverRutaFisica(ruta));

            if (!resultado.Exitoso)
            {
                if (Request == null || !Request.IsAjaxRequest())
                {
                    TempData["FirmaAocrMensaje"] = resultado.Mensaje;
                    TempData["FirmaAocrMensajeOk"] = false;
                    return RedirectToAction("Index", new { solicitudId });
                }

                return JsonError(resultado.Mensaje != null && resultado.Mensaje.IndexOf("Inspector", StringComparison.OrdinalIgnoreCase) >= 0 ? 403 : 409, resultado.Mensaje, solicitudId);
            }

            if (Request == null || !Request.IsAjaxRequest())
            {
                return RedirectToAction("PendientesEmisionAocr", "Inspeccion");
            }

            return JsonOk(resultado.Mensaje, new
            {
                solicitudId,
                idempotente = resultado.Idempotente,
                estadoExpediente = resultado.EstadoExpediente,
                estadoAocr = resultado.EstadoAocr,
                estadoCondiciones = resultado.EstadoCondiciones,
                redirectUrl = Url.Action("PendientesEmisionAocr", "Inspeccion")
            });
        }

        [HttpGet]
        public ActionResult VerPdf(int solicitudId, bool firmado = false, string tipoDocumento = "RECONOCIMIENTO")
        {
            return ServirPdf(solicitudId, firmado, false, tipoDocumento);
        }

        [HttpGet]
        public ActionResult DescargarPdf(int solicitudId, bool firmado = false, string tipoDocumento = "RECONOCIMIENTO")
        {
            return ServirPdf(solicitudId, firmado, true, tipoDocumento);
        }

        [HttpGet]
        public ActionResult DescargarFirmado(int solicitudId, string tipoDocumento = "RECONOCIMIENTO")
        {
            return ServirPdf(solicitudId, true, true, tipoDocumento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Firmar(FirmarAocrInstitucionalRequest request, int solicitudId = 0)
        {
            var id = solicitudId > 0 ? solicitudId : (request != null ? request.SolicitudId : 0);
            var tipoDocumento = FirmaAocrWorkflowService.NormalizarTipoDocumento(request != null ? request.TipoDocumento : (Request != null ? Request["tipoDocumento"] : null));
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][FIRMAR_IN] SolicitudId=" + id + "; TipoDocumento=" + tipoDocumento + "; Usuario=" + ObtenerUsuarioActualNombre());
            Trace.TraceInformation("[FIRMA_AOCR_V2][FIRMAR_IN] SolicitudId=" + id + "; Usuario=" + ObtenerUsuarioActualNombre() + "; TieneCertificado=" + (request != null && request.CertificadoDigital != null && request.CertificadoDigital.ContentLength > 0) + "; TienePassword=" + (request != null && !string.IsNullOrWhiteSpace(request.PasswordCertificado)));
            try
            {
                if (request == null)
                {
                    return JsonError(400, "No se recibieron datos validos para firmar el AOCR.", id);
                }

                request.SolicitudId = id;
                request.TipoDocumento = tipoDocumento;
                if (id <= 0)
                {
                    return JsonError(400, "Solicitud AOCR invalida.", id);
                }
                if (!EsFirmanteActivo(tipoDocumento) || !_authorizationService.UsuarioPuedeFirmar(User, tipoDocumento))
                    return JsonError(403, string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase)
                        ? "DIRDAC firma exclusivamente el AOCR."
                        : "DCAV firma exclusivamente Condiciones y Limitaciones.", id);

                // 1. Validar Roles mediante IDocumentoFirmaService (GATE 7)
                var validacionFirma = _firmaService.Firmar(new CapaModelo.FirmaDocumentoRequest
                {
                    SolicitudId = id,
                    TipoDocumento = tipoDocumento,
                    UsuarioId = ObtenerUsuarioActualId(),
                    RolSolicitado = ObtenerRolFirma(tipoDocumento)
                });
                
                if (!validacionFirma.Exitoso)
                {
                    return JsonError(403, validacionFirma.Mensaje, id);
                }

                var workflow = WorkflowService;
                var contexto = workflow.CargarContexto(id);
                if (contexto == null || contexto.Solicitud == null)
                {
                    return JsonError(404, "No existe contexto documental AOCR para firmar.", id);
                }

                string motivoTipoTramite;
                if (!new AocrCierrePorTipoTramiteService().PuedeGenerarDocumento(
                    contexto.Solicitud, tipoDocumento, out motivoTipoTramite))
                {
                    return JsonError(409, motivoTipoTramite, id);
                }

                var firmaActual = ObtenerFirmaPorTipo(contexto, tipoDocumento);
                if (firmaActual != null && !string.IsNullOrWhiteSpace(firmaActual.RutaDocumento) && StorageService.Existe(firmaActual.RutaDocumento))
                {
                    return JsonError(409, "El documento AOCR ya fue firmado oficialmente.", id);
                }

                var documentoGenerado = ObtenerDocumentoGeneradoPorTipo(contexto, tipoDocumento);
                var estadoPendienteFirma = string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase)
                    ? CapaDatos.Constants.AocrEstadosProceso.PendienteFirmaAocrDirdac
                    : CapaDatos.Constants.AocrEstadosProceso.PendienteFirmaCondicionesDcav;
                if (documentoGenerado == null || !documentoGenerado.Bloqueado
                    || !string.Equals(documentoGenerado.Estado, estadoPendienteFirma, StringComparison.OrdinalIgnoreCase))
                    return JsonError(409, "El documento no fue finalizado y enviado conjuntamente para firma.", id);
                var rutaOrigen = documentoGenerado != null ? documentoGenerado.RutaDocumento : null;
                if (string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(rutaOrigen) && contexto.Certificado != null)
                {
                    rutaOrigen = contexto.Certificado.RutaDocumento;
                }
                var rutaFisicaOrigen = StorageService.ResolverRutaFisica(rutaOrigen);
                var pdfExiste = !string.IsNullOrWhiteSpace(rutaFisicaOrigen) && System.IO.File.Exists(rutaFisicaOrigen);
                var bytesOrigen = pdfExiste ? new FileInfo(rutaFisicaOrigen).Length : 0;
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PDF_ORIGEN] SolicitudId=" + id + "; Ruta=" + (rutaOrigen ?? string.Empty) + "; Existe=" + pdfExiste + "; Bytes=" + bytesOrigen);
                if (!pdfExiste || bytesOrigen <= 0)
                {
                    return JsonError(409, "Primero debe generar el PDF oficial AOCR.", id);
                }
                if (documentoGenerado.TamanioPdf.GetValueOrDefault() != bytesOrigen
                    || string.IsNullOrWhiteSpace(documentoGenerado.HashPdf)
                    || !string.Equals(CalcularSha256(rutaFisicaOrigen), documentoGenerado.HashPdf, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonError(409, "El PDF vigente fue modificado o no coincide con la evidencia persistida. Debe regenerarlo antes de firmar.", id);
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
                var contenidoQr = ConstruirContenidoQr(contexto, tipoDocumento, infoCertificado, nombreFirmante);
                var resultadoFirma = _digitalService.Firmar(pdfOrigen, certificadoBytes, request.PasswordCertificado, nombreFirmante, contenidoQr);
                if (resultadoFirma == null || !resultadoFirma.Exitoso || resultadoFirma.PdfFirmado == null || resultadoFirma.PdfFirmado.LongLength <= 0)
                {
                    return JsonError(500, resultadoFirma != null ? resultadoFirma.Mensaje : "No se pudo firmar digitalmente el AOCR.", id);
                }

                var rutaFirmada = StorageService.GuardarPdfFirmado(id, tipoDocumento, resultadoFirma.PdfFirmado);
                var rutaFisicaFirmada = StorageService.ResolverRutaFisica(rutaFirmada);
                var firmadoExiste = !string.IsNullOrWhiteSpace(rutaFisicaFirmada) && System.IO.File.Exists(rutaFisicaFirmada);
                var bytesFirmado = firmadoExiste ? new FileInfo(rutaFisicaFirmada).Length : 0;
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PDF_FIRMADO] SolicitudId=" + id + "; Ruta=" + rutaFirmada + "; Existe=" + firmadoExiste + "; Bytes=" + bytesFirmado + "; Hash=" + (resultadoFirma.HashSha256 ?? string.Empty));
                Trace.TraceInformation("[FIRMA_AOCR_V2][PDF_FIRMADO] SolicitudId=" + id + "; RutaFirmada=" + rutaFirmada + "; Bytes=" + bytesFirmado + "; Hash=" + (resultadoFirma.HashSha256 ?? string.Empty));
                if (!firmadoExiste || bytesFirmado <= 0 || string.IsNullOrWhiteSpace(resultadoFirma.HashSha256))
                {
                    return JsonError(500, "La firma se genero, pero no se pudo verificar el archivo PDF firmado.", id);
                }

                var finalizacion = _documentosFinalesService.RegistrarFirmaInstitucional(new DocumentoFinalFirmaRequest
                {
                    SolicitudId = id,
                    InspeccionId = contexto.Inspeccion.CodigoInspeccion,
                    UsuarioId = ObtenerUsuarioActualId(),
                    UsuarioNombre = ObtenerUsuarioActualNombre(),
                    Rol = ObtenerRolFirma(tipoDocumento),
                    TipoDocumento = tipoDocumento,
                    RutaPdfFirmado = rutaFirmada,
                    HashPdfFirmado = resultadoFirma.HashSha256,
                    TamanioPdfFirmado = bytesFirmado,
                    NumeroAocr = contexto.Documento != null ? contexto.Documento.NumeroAocr : null,
                    NombreArchivo = Path.GetFileName(rutaFisicaFirmada),
                    CodigoQr = contenidoQr,
                    SujetoCertificado = resultadoFirma.SujetoCertificado,
                    NombreFirmante = nombreFirmante,
                    CargoFirmante = contexto.Documento != null ? contexto.Documento.CargoFirmante : "Direccion General de Aviacion Civil"
                }, ruta => StorageService.ResolverRutaFisica(ruta));
                if (!finalizacion.Exitoso) return JsonError(409, finalizacion.Mensaje, id);

                var estadoSolicitudNuevo = finalizacion.EstadoExpediente;
                var urlDescarga = Url.Action("DescargarFirmado", "FirmaAocr", new { solicitudId = id, tipoDocumento });

                return Json(new
                {
                    ok = true,
                    code = 200,
                    message = finalizacion.Mensaje,
                    estadoAocr = finalizacion.EstadoAocr,
                    urlDescarga = urlDescarga,
                    data = new
                    {
                        solicitudId = id,
                        tipoDocumento,
                        estadoAocr = finalizacion.EstadoAocr,
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
                return JsonError(500, "No se pudo completar la firma institucional. Consulte el registro de auditoria.", id);
            }
        }

        private ActionResult ServirPdf(int solicitudId, bool firmado, bool descargar, string tipoDocumento)
        {
            tipoDocumento = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            CapaNegocio.LogBL.RegistrarInfo(
                "[FIRMA_AOCR][SERVIR_PDF_IN] SolicitudId=" + solicitudId
                + "; TipoDocumento=" + tipoDocumento
                + "; Firmado=" + firmado
                + "; Descargar=" + descargar
                + "; Usuario=" + ObtenerUsuarioActualNombre() + ";",
                "FirmaAocrController",
                ObtenerUsuarioActualId());
            var contexto = WorkflowService.CargarContexto(solicitudId);
            if (contexto == null || contexto.Solicitud == null)
            {
                return HttpNotFound("La solicitud AOCR indicada no existe.");
            }
            var inspectorAsignado = EsRolInspectorActivo()
                && _authorizationService.UsuarioPuedeGenerar(User)
                && contexto.Inspeccion != null
                && EsSolicitudAsignadaAlInspectorActivo(solicitudId);
            if (!inspectorAsignado && (!EsFirmanteActivo(tipoDocumento) || !_authorizationService.UsuarioPuedeFirmar(User, tipoDocumento)))
                return new HttpStatusCodeResult(403, "No esta autorizado para visualizar este tipo de documento.");

            var firma = ObtenerFirmaPorTipo(contexto, tipoDocumento);
            var documentoGenerado = ObtenerDocumentoGeneradoPorTipo(contexto, tipoDocumento);
            var ruta = firmado
                ? (firma != null ? firma.RutaDocumento : null)
                : (documentoGenerado != null ? documentoGenerado.RutaDocumento : null);
            if (!firmado && string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(ruta) && contexto.Certificado != null)
            {
                ruta = contexto.Certificado.RutaDocumento;
            }
            var nombreBase = (contexto.Solicitud.NumeroSolicitud ?? ("AOCR-" + solicitudId)).Replace("/", "-").Replace("\\", "-");
            var sufijo = string.Equals(tipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase) ? "-condiciones" : "-aocr";
            var nombre = firmado ? nombreBase + sufijo + "-firmado.pdf" : nombreBase + sufijo + "-oficial.pdf";

            var documentoId = firmado && firma != null ? firma.CodigoFirma
                : (documentoGenerado != null ? documentoGenerado.CodigoDocumento
                    : (contexto.Certificado != null ? contexto.Certificado.CodigoCertificado : solicitudId));
            var raicesPermitidas = CapaNegocio.Helpers.FileStorageHelper.GetAllowedStorageRoots()
                .Concat(new[] { Server.MapPath("~/App_Data") })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var seguro = new DocumentoSeguroService(raicesPermitidas,
                evento => Trace.TraceInformation("[GATE7] " + evento + ";Usuario=" + (User != null ? User.Identity.Name : string.Empty)));
            var archivo = seguro.Resolver(documentoId, solicitudId, contexto.Solicitud.CodigoSolicitud, ruta, nombre,
                valor => StorageService.ResolverRutaFisica(valor));
            if (!archivo.EsValido)
            {
                CapaNegocio.LogBL.RegistrarError(
                    "[FIRMA_AOCR][SERVIR_PDF_DENEGADO] SolicitudId=" + solicitudId
                    + "; TipoDocumento=" + tipoDocumento
                    + "; Error=" + archivo.Error
                    + "; RaicesPermitidas=" + raicesPermitidas.Length + ";",
                    archivo.MensajePublico,
                    "FirmaAocrController",
                    ObtenerUsuarioActualId());
                return archivo.Error == DocumentoSeguroError.NoEncontrado || archivo.Error == DocumentoSeguroError.Vacio
                    ? (ActionResult)HttpNotFound(archivo.MensajePublico)
                    : new HttpStatusCodeResult(403, archivo.MensajePublico);
            }

            CapaNegocio.LogBL.RegistrarInfo(
                "[FIRMA_AOCR][SERVIR_PDF_OK] SolicitudId=" + solicitudId
                + "; TipoDocumento=" + tipoDocumento
                + "; Bytes=" + new FileInfo(archivo.RutaFisica).Length
                + "; Descargar=" + descargar + ";",
                "FirmaAocrController",
                ObtenerUsuarioActualId());
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (descargar)
            {
                return File(archivo.RutaFisica, archivo.Mime, nombre);
            }

            Response.AppendHeader("Content-Disposition", "inline; filename=\"" + nombre + "\"");
            return File(archivo.RutaFisica, archivo.Mime);
        }

        private SidebarPermissionSnapshot ObtenerPermisosRolActivo()
        {
            return SidebarPermissionHelper.Resolve(
                Session != null ? Session["Rol"] as string ?? string.Empty : string.Empty,
                Session != null ? Session["RolesRaw"] ?? Session["Roles"] : null);
        }

        private bool EsRolInspectorActivo()
        {
            return ObtenerPermisosRolActivo().EsInspectorRol;
        }

        private bool EsFirmanteActivo(string tipoDocumento)
        {
            var permisos = ObtenerPermisosRolActivo();
            var tipo = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            return string.Equals(tipo, AocrCierrePorTipoTramiteService.Reconocimiento, StringComparison.OrdinalIgnoreCase)
                ? permisos.EsDirdacRol
                : permisos.EsDcavRol;
        }

        private bool EsExpedientePendienteDelInspectorActivo(int solicitudId)
        {
            if (solicitudId <= 0 || !EsRolInspectorActivo())
                return false;

            var contexto = AocrUserContextService.ToBandejaRoleContext(AocrUserContextService.FromHttpContext(HttpContext));
            return new InspectorBandejaService().ObtenerPendientesDocumentosFinales(contexto)
                .Any(x => x.SolicitudId == solicitudId);
        }

        private bool EsSolicitudAsignadaAlInspectorActivo(int solicitudId)
        {
            if (solicitudId <= 0 || !EsRolInspectorActivo())
                return false;

            var contexto = AocrUserContextService.ToBandejaRoleContext(AocrUserContextService.FromHttpContext(HttpContext));
            return new InspectorBandejaService().ObtenerInspeccionesAsignadas(contexto)
                .Any(x => x.CodigoSolicitud == solicitudId);
        }

        private static string CalcularSha256(string rutaFisica)
        {
            using (var stream = System.IO.File.OpenRead(rutaFisica))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private string ConstruirContenidoQr(FirmaAocrContexto contexto, string tipoDocumento, InformacionCertificadoDigital infoCertificado, string nombreFirmante)
        {
            var solicitud = contexto != null ? contexto.Solicitud : null;
            return string.Join(" | ", new[]
            {
                "Modulo=Firma Institucional AOCR",
                "TipoDocumento=" + FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento),
                "SolicitudId=" + (solicitud != null ? solicitud.CodigoSolicitud.ToString() : string.Empty),
                "NumeroSolicitud=" + (solicitud != null ? solicitud.NumeroSolicitud : string.Empty),
                "FirmadoPor=" + ObtenerRolActual(),
                "Firmante=" + (nombreFirmante ?? string.Empty),
                "Fecha=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "Certificado=" + (infoCertificado != null ? infoCertificado.SujetoCertificado : string.Empty)
            });
        }

        private static AocrFirmaDocumento ObtenerFirmaPorTipo(FirmaAocrContexto contexto, string tipoDocumento)
        {
            var tipo = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            if (string.Equals(tipo, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase))
            {
                return contexto != null ? contexto.FirmaCondiciones : null;
            }

            return contexto != null ? contexto.FirmaReconocimiento : null;
        }

        private static AocrDocumentoGenerado ObtenerDocumentoGeneradoPorTipo(FirmaAocrContexto contexto, string tipoDocumento)
        {
            var tipo = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            if (string.Equals(tipo, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase))
            {
                return contexto != null ? contexto.DocumentoCondiciones : null;
            }

            return contexto != null ? contexto.DocumentoReconocimiento : null;
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

        private static DateTime? ParseFechaAocr(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return null;
            }

            string[] formatos =
            {
                "dd/MM/yyyy",
                "d/M/yyyy",
                "yyyy-MM-dd",
                "yyyy/MM/dd"
            };

            DateTime fecha;
            if (DateTime.TryParseExact(
                valor.Trim(),
                formatos,
                new CultureInfo("es-EC"),
                DateTimeStyles.None,
                out fecha))
            {
                return fecha.Date;
            }

            return null;
        }

        private static string PrimerValorFormulario(params string[] valores)
        {
            if (valores == null)
            {
                return null;
            }

            foreach (var valor in valores)
            {
                if (!string.IsNullOrWhiteSpace(valor))
                {
                    return valor.Trim();
                }
            }

            return null;
        }

        private int ObtenerUsuarioActualId()
        {
            var ctx = _usuarioContexto.ObtenerContextoActual();
            return ctx.UsuarioId;
        }

        private string ObtenerUsuarioActualNombre()
        {
            var ctx = _usuarioContexto.ObtenerContextoActual();
            return ctx.Nombre;
        }

        private string ObtenerRolActual()
        {
            return _authorizationService.ObtenerRolActual(User);
        }

        private static string NormalizarMayusculas(string valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? valor
                : valor.Trim().ToUpperInvariant();
        }

        private string ObtenerRolFirma(string tipoDocumento)
        {
            var tipo = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            if (string.Equals(tipo, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase))
                return CapaDatos.Constants.AocrRolesInstitucionales.DirdacAliases
                    .FirstOrDefault(rol => User != null && User.IsInRole(rol)) ?? string.Empty;
            return CapaDatos.Constants.AocrRolesInstitucionales.DcavAliases
                .FirstOrDefault(rol => User != null && User.IsInRole(rol)) ?? string.Empty;
        }
    }
}
