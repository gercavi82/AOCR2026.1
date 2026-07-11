using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Inspector,InspectorTecnico,Tecnico,Direccion,DireccionJefaturaTecnica,DIRDAC,JefaturaTecnica,DirectorGeneral,Administrador")]
    public class FirmaAocrController : Controller
    {
        private readonly FirmaAocrAuthorizationService _authorizationService = new FirmaAocrAuthorizationService();
        private readonly FirmaAocrPdfService _pdfService = new FirmaAocrPdfService();
        private readonly FirmaAocrDigitalService _digitalService = new FirmaAocrDigitalService();
        private readonly FirmaAocrHistorialService _historialService = new FirmaAocrHistorialService();
        private readonly FirmaAocrFinalizacionService _finalizacionService = new FirmaAocrFinalizacionService();
        private readonly FirmaAocrNotificationService _notificationService = new FirmaAocrNotificationService();
        private readonly AocrFirmaDocumentoDAO _firmaDocumentoDao = new AocrFirmaDocumentoDAO();
        private readonly AocrProcesoEstadoDAO _procesoEstadoDao = new AocrProcesoEstadoDAO();
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly AocrProcesoNotificacionService _procesoNotificacionService = new AocrProcesoNotificacionService();

        private FirmaAocrStorageService StorageService
        {
            get { return new FirmaAocrStorageService(Server); }
        }

        private FirmaAocrWorkflowService WorkflowService
        {
            get { return new FirmaAocrWorkflowService(_authorizationService, StorageService); }
        }

        [HttpGet]
        public ActionResult Pendientes()
        {
            if (!_authorizationService.UsuarioPuedeEntrar(User))
            {
                return new HttpUnauthorizedResult("Rol no autorizado para revisar firma institucional AOCR.");
            }

            var estados = _procesoEstadoDao.ListarActivosPorEstado(
                AocrEstadosProceso.PendienteFirmaDirectorGeneral,
                AocrEstadosProceso.AocrFirmadoDirdac,
                AocrEstadosProceso.CondicionesFirmadasDirdac,
                AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy,
                "PENDIENTE_FIRMA_DIRECCION");

            var rows = new List<FirmaAocrPendienteRowViewModel>();
            foreach (var estado in estados ?? new List<CapaDatos.Models.AocrProcesoEstadoRecord>())
            {
                if (estado == null || estado.SolicitudId <= 0)
                {
                    continue;
                }

                var solicitud = _solicitudDao.ObtenerPorId(estado.SolicitudId);
                if (solicitud == null)
                {
                    continue;
                }

                var contexto = WorkflowService.CargarContexto(estado.SolicitudId);
                var docReconocimiento = contexto != null ? contexto.DocumentoReconocimiento : null;
                var docCondiciones = contexto != null ? contexto.DocumentoCondiciones : null;
                var firmaReconocimiento = contexto != null ? contexto.FirmaReconocimiento : null;
                var firmaCondiciones = contexto != null ? contexto.FirmaCondiciones : null;

                var rutaReconocimiento = docReconocimiento != null ? docReconocimiento.RutaDocumento : (contexto != null && contexto.Certificado != null ? contexto.Certificado.RutaDocumento : null);
                rows.Add(new FirmaAocrPendienteRowViewModel
                {
                    SolicitudId = estado.SolicitudId,
                    NumeroSolicitud = solicitud.NumeroSolicitud ?? estado.SolicitudId.ToString(CultureInfo.InvariantCulture),
                    Operadora = FirmaAocrWorkflowService.PrimerValorNoVacio(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial),
                    InspectorResponsable = ResolverInspectorResponsable(solicitud, contexto),
                    EstadoProceso = estado.EstadoActual,
                    Etapa = estado.EtapaActual,
                    SiguienteAccion = estado.SiguienteAccion,
                    FechaEstado = estado.FechaEstado,
                    PdfReconocimientoGenerado = StorageService.Existe(rutaReconocimiento),
                    PdfCondicionesGenerado = StorageService.Existe(docCondiciones != null ? docCondiciones.RutaDocumento : null),
                    ReconocimientoFirmado = firmaReconocimiento != null && StorageService.Existe(firmaReconocimiento.RutaDocumento),
                    CondicionesFirmadas = firmaCondiciones != null && StorageService.Existe(firmaCondiciones.RutaDocumento),
                    UrlGestionar = Url.Action("Index", "FirmaAocr", new { solicitudId = estado.SolicitudId })
                });
            }

            var model = new FirmaAocrPendientesViewModel
            {
                Items = rows
                    .OrderBy(r => r.ReconocimientoFirmado && r.CondicionesFirmadas)
                    .ThenByDescending(r => r.FechaEstado)
                    .ToList()
            };
            model.Total = model.Items.Count;
            model.PendientesFirma = model.Items.Count(r => !r.ReconocimientoFirmado || !r.CondicionesFirmadas);
            model.Parciales = model.Items.Count(r => (r.ReconocimientoFirmado || r.CondicionesFirmadas) && !(r.ReconocimientoFirmado && r.CondicionesFirmadas));
            model.Completos = model.Items.Count(r => r.ReconocimientoFirmado && r.CondicionesFirmadas);

            Trace.TraceInformation("[DIRDAC][BANDEJA_FIRMA] Usuario=" + ObtenerUsuarioActualNombre()
                + "; Rol=" + ObtenerRolActual()
                + "; Total=" + model.Total
                + "; Pendientes=" + model.PendientesFirma
                + "; Parciales=" + model.Parciales
                + "; Completos=" + model.Completos + ";");

            return View("~/Views/FirmaAocr/Pendientes.cshtml", model);
        }

        [HttpGet]
        [Authorize(Roles = "Inspector,InspectorTecnico,Tecnico,Administrador")]
        public ActionResult PendientesInspector(string filtro, string tipoDocumento)
        {
            var usuarioId = ObtenerUsuarioActualId();
            var esAdministrador = User != null && User.IsInRole("Administrador");
            var queue = new FirmaAocrInspectorQueueService().Obtener(usuarioId, esAdministrador);
            var token = (filtro ?? "pendientes").Trim().ToUpperInvariant();
            IEnumerable<FirmaAocrInspectorQueueItem> source;
            if (token == "OBSERVADOS")
            {
                source = queue.Observados;
            }
            else if (token == "ENVIADOS")
            {
                source = queue.Enviados;
            }
            else
            {
                source = queue.Editables.Concat(queue.Observados);
            }

            var rows = source.Select(item => ConstruirFilaInspector(item)).Where(r => r != null).ToList();
            var esCondiciones = string.Equals(tipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase);
            var model = new FirmaAocrPendientesViewModel
            {
                Items = rows.OrderByDescending(r => r.FechaEstado).ToList(),
                Total = rows.Count,
                PendientesFirma = queue.TotalPendientes,
                Observados = queue.Observados.Count,
                Enviados = queue.Enviados.Count,
                EsBandejaInspector = true,
                Titulo = token == "OBSERVADOS" ? "Documentos devueltos por DCAV"
                    : token == "ENVIADOS" ? "Documentos enviados al DCAV"
                    : esCondiciones ? "Condiciones y Limitaciones pendientes de revisión"
                    : "AOCR y Condiciones pendientes de revisión",
                Descripcion = token == "ENVIADOS"
                    ? "Expedientes enviados a DCAV y bloqueados para edición mientras se revisan."
                    : "Revise, modifique y genere los documentos finales sin mezclar la documentación inicial del expediente."
            };

            Trace.TraceInformation("[INSPECTOR][BANDEJA_DOCUMENTOS] Usuario=" + usuarioId
                + "; Filtro=" + token + "; TipoDocumento=" + (tipoDocumento ?? string.Empty)
                + "; Total=" + model.Total + ";");
            return View("~/Views/FirmaAocr/Pendientes.cshtml", model);
        }

        [HttpGet]
        public ActionResult Index(int solicitudId)
        {
            Trace.TraceInformation("[FIRMA_AOCR][OPEN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre() + "; Rol=" + ObtenerRolActual() + ";");
            Trace.TraceInformation("[FIRMA_AOCR_NUEVA][PAGE_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre());
            Trace.TraceInformation("[FIRMA_AOCR_V2][PAGE_IN] SolicitudId=" + solicitudId + "; Usuario=" + ObtenerUsuarioActualNombre() + "; Rol=" + ObtenerRolActual());
            try
            {
                var workflow = WorkflowService;
                var contexto = workflow.CargarContexto(solicitudId);
                var esInspector = User != null && (User.IsInRole("Inspector") || User.IsInRole("InspectorTecnico") || User.IsInRole("Tecnico"));
                if (esInspector && (contexto == null || contexto.Inspeccion == null
                    || !new AocrAuthorizationService().PuedeInspectorAbrirInspeccion(contexto.Inspeccion.CodigoInspeccion, ObtenerUsuarioActualId())))
                {
                    return new HttpStatusCodeResult(403, "Solo el Inspector asignado puede revisar AOCR y Condiciones y Limitaciones.");
                }

                var model = workflow.ConstruirViewModel(solicitudId, User, Url);
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

        private FirmaAocrPendienteRowViewModel ConstruirFilaInspector(FirmaAocrInspectorQueueItem item)
        {
            if (item == null || item.Estado == null || item.Solicitud == null || item.Inspeccion == null)
            {
                return null;
            }

            var contexto = WorkflowService.CargarContexto(item.Estado.SolicitudId);
            var docReconocimiento = contexto != null ? contexto.DocumentoReconocimiento : null;
            var docCondiciones = contexto != null ? contexto.DocumentoCondiciones : null;
            return new FirmaAocrPendienteRowViewModel
            {
                SolicitudId = item.Estado.SolicitudId,
                InspeccionId = item.Inspeccion.CodigoInspeccion,
                NumeroSolicitud = item.Solicitud.NumeroSolicitud ?? item.Estado.SolicitudId.ToString(CultureInfo.InvariantCulture),
                Operadora = FirmaAocrWorkflowService.PrimerValorNoVacio(item.Solicitud.RazonSocial, item.Solicitud.NombreOperador, item.Solicitud.NombreComercial),
                InspectorResponsable = ResolverInspectorResponsable(item.Solicitud, contexto),
                EstadoProceso = item.Estado.EstadoActual,
                Etapa = item.Estado.EtapaActual,
                SiguienteAccion = item.Estado.SiguienteAccion,
                FechaEstado = item.Estado.FechaEstado,
                PdfReconocimientoGenerado = StorageService.Existe(docReconocimiento != null ? docReconocimiento.RutaDocumento : null),
                PdfCondicionesGenerado = StorageService.Existe(docCondiciones != null ? docCondiciones.RutaDocumento : null),
                ReconocimientoFirmado = contexto != null && contexto.FirmaReconocimiento != null && StorageService.Existe(contexto.FirmaReconocimiento.RutaDocumento),
                CondicionesFirmadas = contexto != null && contexto.FirmaCondiciones != null && StorageService.Existe(contexto.FirmaCondiciones.RutaDocumento),
                UrlGestionar = Url.Action("Index", "FirmaAocr", new { solicitudId = item.Estado.SolicitudId })
            };
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

                if (!EsInspectorAsignadoSolicitud(solicitudId))
                {
                    return JsonError(403, "Solo el Inspector asignado puede guardar cambios en estos documentos.", solicitudId);
                }

                if (!WorkflowService.PuedeEditarDocumentosInspector(solicitudId, User))
                {
                    return JsonError(409, "Los documentos solo pueden ser editados por el Inspector responsable durante la etapa habilitada u observada.", solicitudId);
                }

                var estadoExplotador = PrimerValorFormulario(
                    request.EstadoExplotador,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["EstadoExplotador"] : null,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["estadoExplotador"] : null);
                var fechaVencimientoRaw = PrimerValorFormulario(
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["FechaVencimiento"] : null,
                    Request != null && Request.Unvalidated != null ? Request.Unvalidated.Form["fechaVencimiento"] : null,
                    request.FechaVencimiento.HasValue ? request.FechaVencimiento.Value.ToString("yyyy-MM-dd") : null);
                var fechaVencimiento = ParseFechaAocr(fechaVencimientoRaw);

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

                if (!_authorizationService.UsuarioPuedeEntrar(User))
                {
                    return JsonError(403, "Rol no autorizado para generar el PDF oficial AOCR.", solicitudId);
                }

                if (!EsInspectorAsignadoSolicitud(solicitudId))
                {
                    return JsonError(403, "Solo el Inspector asignado puede generar estos documentos.", solicitudId);
                }

                if (!workflow.PuedeEditarDocumentosInspector(solicitudId, User))
                {
                    return JsonError(409, "Los PDF solo pueden ser generados por el Inspector durante la revision de AOCR y Condiciones.", solicitudId);
                }

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
                SincronizarRevisionInspectorSiCorresponde(solicitudId, contexto);
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
                return JsonError(500, "Error interno al generar PDF AOCR. " + ex.Message, solicitudId);
            }
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

                if (!_authorizationService.UsuarioPuedeEntrar(User))
                {
                    return JsonError(403, "Solo Direccion / DIRDAC puede firmar el AOCR final.", id);
                }

                if (!_authorizationService.UsuarioPuedeFirmarDirectorGeneral(User))
                {
                    return JsonError(403, "Solo el Director General puede firmar AOCR y Condiciones y Limitaciones.", id);
                }

                var workflow = WorkflowService;
                var contexto = workflow.CargarContexto(id);
                if (contexto == null || contexto.Solicitud == null)
                {
                    return JsonError(404, "No existe contexto documental AOCR para firmar.", id);
                }

                var estadoCentral = _procesoEstadoDao.ObtenerActivoPorSolicitud(id);
                var estadoFirma = estadoCentral != null ? estadoCentral.EstadoActual : null;
                var estadoPermiteFirma = string.Equals(estadoFirma, AocrEstadosProceso.PendienteFirmaDirectorGeneral, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoFirma, AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoFirma, AocrEstadosProceso.AocrFirmadoDirdac, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoFirma, AocrEstadosProceso.CondicionesFirmadasDirdac, StringComparison.OrdinalIgnoreCase);
                if (!estadoPermiteFirma)
                {
                    return JsonError(409, "El expediente debe estar aprobado por DCAV y pendiente de firma del Director General.", id);
                }

                var firmaActual = ObtenerFirmaPorTipo(contexto, tipoDocumento);
                if (firmaActual != null && !string.IsNullOrWhiteSpace(firmaActual.RutaDocumento) && StorageService.Existe(firmaActual.RutaDocumento))
                {
                    return JsonError(409, "El documento AOCR ya fue firmado oficialmente.", id);
                }

                var documentoGenerado = ObtenerDocumentoGeneradoPorTipo(contexto, tipoDocumento);
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

                RegistrarFirma(contexto, tipoDocumento, rutaFirmada, resultadoFirma, contenidoQr, nombreFirmante, bytesFirmado);
                NotificarDocumentoFirmadoSeguro(id, tipoDocumento);
                RegistrarEstadoDirectorGeneralSiCompleto(id);
                Trace.TraceInformation("[FIRMA_AOCR_NUEVA][DB_UPDATE] SolicitudId=" + id + "; EstadoAocrNuevo=AOCR_FIRMADO_DIRDAC; FilasAfectadas=1");
                Trace.TraceInformation("[FIRMA_AOCR_V2][DB_UPDATE] SolicitudId=" + id + "; EstadoAnterior=" + (contexto.Solicitud.Estado ?? string.Empty) + "; EstadoNuevo=AOCR_FIRMADO_DIRDAC; FilasAfectadas=1");

                var finalizacion = _finalizacionService.LiberarDocumentoFinal(id, ObtenerUsuarioActualId(), StorageService.Existe);
                var estadoSolicitudNuevo = finalizacion != null && !string.IsNullOrWhiteSpace(finalizacion.EstadoNuevo)
                    ? finalizacion.EstadoNuevo
                    : contexto.Solicitud.Estado;
                var observacionHistorial = finalizacion != null && finalizacion.Finalizado
                    ? "Firma institucional AOCR completa. Documento final liberado."
                    : "Firma institucional registrada para " + tipoDocumento + ". Pendiente la firma del otro documento.";
                _historialService.Registrar(id, contexto.Solicitud.Estado, estadoSolicitudNuevo, ObtenerUsuarioActualId(), observacionHistorial);
                if (finalizacion != null && finalizacion.Finalizado)
                {
                    _notificationService.NotificarLiberacion(id, rutaFirmada);
                    _procesoNotificacionService.NotificarProcesoAocrFinalizado(id);
                }

                var urlDescarga = Url.Action("DescargarFirmado", "FirmaAocr", new { solicitudId = id, tipoDocumento });
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
                        tipoDocumento,
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

        private void NotificarDocumentoFirmadoSeguro(int solicitudId, string tipoDocumento)
        {
            try
            {
                var tipo = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
                if (string.Equals(tipo, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase))
                {
                    new AocrEstadoProcesoService().CambiarEstado(
                        solicitudId,
                        AocrEstadosProceso.AocrFirmadoDirdac,
                        "FIRMAR_AOCR_DIRDAC",
                        ObtenerUsuarioActualId(),
                        "DirectorGeneral",
                        "AOCR firmado por Director General.");
                    _procesoNotificacionService.NotificarAocrFirmado(solicitudId);
                }
                else if (string.Equals(tipo, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(tipo, "CONDICIONES", StringComparison.OrdinalIgnoreCase))
                {
                    new AocrEstadoProcesoService().CambiarEstado(
                        solicitudId,
                        AocrEstadosProceso.CondicionesFirmadasDirdac,
                        "FIRMAR_CONDICIONES_DIRDAC",
                        ObtenerUsuarioActualId(),
                        "DirectorGeneral",
                        "Condiciones y Limitaciones firmadas por Director General.");
                    _procesoNotificacionService.NotificarCondicionesFirmadas(solicitudId);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("[NOTIF_AOCR][SEND_ERROR] SolicitudId=" + solicitudId + "; TipoEvento=FIRMA_DOCUMENTO; Email=; Error=" + ex.Message + ";");
            }
        }

        private void RegistrarEstadoDirectorGeneralSiCompleto(int solicitudId)
        {
            var firmaAocr = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var firmaCondiciones = _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES")
                ?? _firmaDocumentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES");
            if (firmaAocr == null || firmaCondiciones == null
                || !StorageService.Existe(firmaAocr.RutaDocumento)
                || !StorageService.Existe(firmaCondiciones.RutaDocumento))
            {
                return;
            }

            new AocrEstadoProcesoService().CambiarEstado(
                solicitudId,
                AocrEstadosProceso.DocumentosFirmadosDirdac,
                "DOCUMENTOS_FIRMADOS_DIRDAC",
                ObtenerUsuarioActualId(),
                "DirectorGeneral",
                "AOCR y Condiciones y Limitaciones firmadas por Director General.");
        }

        private ActionResult ServirPdf(int solicitudId, bool firmado, bool descargar, string tipoDocumento)
        {
            tipoDocumento = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento);
            var contexto = WorkflowService.CargarContexto(solicitudId);
            if (contexto == null || contexto.Solicitud == null)
            {
                return HttpNotFound("La solicitud AOCR indicada no existe.");
            }

            var firma = ObtenerFirmaPorTipo(contexto, tipoDocumento);
            var documentoGenerado = ObtenerDocumentoGeneradoPorTipo(contexto, tipoDocumento);
            var ruta = firmado
                ? (firma != null ? firma.RutaDocumento : null)
                : (documentoGenerado != null ? documentoGenerado.RutaDocumento : null);
            if (!firmado && string.Equals(tipoDocumento, "RECONOCIMIENTO", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(ruta) && contexto.Certificado != null)
            {
                ruta = contexto.Certificado.RutaDocumento;
            }
            var rutaFisica = StorageService.ResolverRutaFisica(ruta);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                return new HttpStatusCodeResult(404, firmado ? "No existe AOCR firmado para descargar." : "No existe PDF oficial AOCR generado.");
            }

            var nombreBase = (contexto.Solicitud.NumeroSolicitud ?? ("AOCR-" + solicitudId)).Replace("/", "-").Replace("\\", "-");
            var sufijo = string.Equals(tipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase) ? "-condiciones" : "-aocr";
            var nombre = firmado ? nombreBase + sufijo + "-firmado.pdf" : nombreBase + sufijo + "-oficial.pdf";
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

        private void RegistrarFirma(FirmaAocrContexto contexto, string tipoDocumento, string rutaFirmada, ResultadoFirmaDigital resultadoFirma, string contenidoQr, string nombreFirmante, long bytesFirmado)
        {
            var solicitudId = contexto != null && contexto.Solicitud != null ? contexto.Solicitud.CodigoSolicitud : 0;
            var firma = new AocrFirmaDocumento
            {
                CodigoSolicitud = solicitudId,
                CodigoInspeccion = contexto != null && contexto.Inspeccion != null ? (int?)contexto.Inspeccion.CodigoInspeccion : null,
                TipoDocumento = FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento),
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

        private string ConstruirContenidoQr(FirmaAocrContexto contexto, string tipoDocumento, InformacionCertificadoDigital infoCertificado, string nombreFirmante)
        {
            var solicitud = contexto != null ? contexto.Solicitud : null;
            return string.Join(" | ", new[]
            {
                "Modulo=Firma Institucional AOCR",
                "TipoDocumento=" + FirmaAocrWorkflowService.NormalizarTipoDocumento(tipoDocumento),
                "SolicitudId=" + (solicitud != null ? solicitud.CodigoSolicitud.ToString() : string.Empty),
                "NumeroSolicitud=" + (solicitud != null ? solicitud.NumeroSolicitud : string.Empty),
                "EstadoAocr=AOCR_FIRMADO_DIRDAC",
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

        private void SincronizarRevisionInspectorSiCorresponde(int solicitudId, FirmaAocrContexto contexto)
        {
            try
            {
                var estado = _procesoEstadoDao.ObtenerActivoPorSolicitud(solicitudId);
                var estadoActual = estado != null ? estado.EstadoActual : string.Empty;
                if (!string.Equals(estadoActual, AocrEstadosProceso.DocumentosHabilitadosInspector, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(estadoActual, AocrEstadosProceso.DocumentosObservadosDcav, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var result = new AocrEstadoProcesoService().CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.DocumentosEnRevisionInspector,
                    "REVISAR_AOCR_CONDICIONES",
                    ObtenerUsuarioActualId(),
                    ObtenerRolActual(),
                    "Inspector genera o actualiza documentos AOCR y Condiciones para revision DCAV.",
                    inspeccionId: contexto != null && contexto.Inspeccion != null ? (int?)contexto.Inspeccion.CodigoInspeccion : null,
                    informeId: contexto != null && contexto.Informe != null ? (int?)contexto.Informe.CodigoInforme : null);

                Trace.TraceInformation("[FIRMA_AOCR][SYNC_REVISION_INSPECTOR] SolicitudId=" + solicitudId
                    + "; EstadoAnterior=" + estadoActual
                    + "; EstadoNuevo=" + AocrEstadosProceso.DocumentosEnRevisionInspector
                    + "; Ok=" + (result != null && result.Ok)
                    + "; Motivo=" + (result != null ? result.Motivo : string.Empty) + ";");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("[FIRMA_AOCR][SYNC_REVISION_INSPECTOR_ERROR] SolicitudId=" + solicitudId + "; Error=" + ex.Message + ";");
            }
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

        private static string ResolverInspectorResponsable(SolicitudAOCR solicitudBase, FirmaAocrContexto contexto)
        {
            var solicitud = contexto != null && contexto.Solicitud != null ? contexto.Solicitud : solicitudBase;
            var inspeccion = contexto != null ? contexto.Inspeccion : null;
            var mismaAsignacion = inspeccion != null && solicitud != null
                && inspeccion.CodigoInspector.HasValue && solicitud.CodigoTecnico.HasValue
                && inspeccion.CodigoInspector.Value == solicitud.CodigoTecnico.Value;

            var nombre = FirmaAocrWorkflowService.PrimerValorNoVacio(
                inspeccion != null ? inspeccion.InspectorPrincipalNombre : null,
                mismaAsignacion ? solicitud.TecnicoResponsableNombre : null);

            var identificador = FirmaAocrWorkflowService.PrimerValorNoVacio(
                inspeccion != null ? inspeccion.InspectorPrincipalCedula : null,
                mismaAsignacion ? solicitud.TecnicoResponsableCedula : null);

            if (string.IsNullOrWhiteSpace(nombre))
            {
                int? inspectorId = inspeccion != null && inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0
                    ? inspeccion.CodigoInspector.Value
                    : (mismaAsignacion && solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0 ? solicitud.CodigoTecnico.Value : (int?)null);

                if (inspectorId.HasValue)
                {
                    try
                    {
                        var usuario = CapaDatos.DAOs.UsuarioDAO.ObtenerPorId(inspectorId.Value);
                        if (usuario != null && !string.IsNullOrWhiteSpace(usuario.NombreCompleto))
                        {
                            nombre = usuario.NombreCompleto;
                        }
                        else
                        {
                            var tecnico = CapaDatos.DAOs.TecnicoDAO.ObtenerPorId(inspectorId.Value);
                            if (tecnico != null && !string.IsNullOrWhiteSpace(tecnico.NombreCompleto))
                            {
                                nombre = tecnico.NombreCompleto;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(nombre) && !string.IsNullOrWhiteSpace(identificador) && nombre.IndexOf(identificador, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return nombre.Trim() + " - " + identificador.Trim();
            }

            return FirmaAocrWorkflowService.PrimerValorNoVacio(nombre, "No registrado");
        }

        private bool EsInspectorAsignadoSolicitud(int solicitudId)
        {
            if (User != null && User.IsInRole("Administrador"))
            {
                return true;
            }

            var contexto = WorkflowService.CargarContexto(solicitudId);
            return contexto != null
                && contexto.Inspeccion != null
                && new AocrAuthorizationService().PuedeInspectorAbrirInspeccion(
                    contexto.Inspeccion.CodigoInspeccion,
                    ObtenerUsuarioActualId());
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
