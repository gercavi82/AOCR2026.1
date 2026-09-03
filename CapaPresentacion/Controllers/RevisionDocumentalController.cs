using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Services;
using CapaPresentacion.Filters;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Models.ViewModels;
using Rotativa;

namespace CapaPresentacion.Controllers
{
    [AocrAuthorize(Roles = "Inspector,InspectorTecnico,Tecnico,EvaluadorTecnico,Coordinador,CoordinadorInspecciones,Coordinacion,Administrador")]
    public class RevisionDocumentalController : Controller
    {
        private const string RolesRevisionDocumentalOperativa = "Inspector,InspectorTecnico,Tecnico,EvaluadorTecnico,Coordinador,CoordinadorInspecciones,Coordinacion,Administrador";
        private readonly RevisionDocumentalBandejaService _revisionDocumentalBandejaService;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBl;
        private readonly DocumentoBL _documentoBl;
        private readonly DocumentoDAO _documentoDao;
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao;
        private readonly IUserContextAccessor _userContext;
        private readonly RevisionDocumentalService _revisionDocumentalService;
        private readonly InspectorIdentityService _inspectorIdentityService;
        private readonly IAocrEstadoService _estadoService;
        private readonly RevisionDocumentalCoordinadorService _coordinadorRevisionService;
        private readonly SolicitudAocrCorreoService _correoService;

        public RevisionDocumentalController()
        {
            _revisionDocumentalBandejaService = new RevisionDocumentalBandejaService();
            _solicitudDao = new SolicitudAOCRDAO();
            _solicitudAocrInfraBl = new SolicitudAocrInfraBL();
            _documentoBl = new DocumentoBL();
            _documentoDao = new DocumentoDAO();
            _usuarioInternoRtDao = new UsuarioInternoRTDAO();
            _userContext = new UserContextAccessor();
            _revisionDocumentalService = new RevisionDocumentalService();
            _inspectorIdentityService = new InspectorIdentityService();
            _estadoService = new AocrEstadoService();
            _coordinadorRevisionService = new RevisionDocumentalCoordinadorService();
            _correoService = new SolicitudAocrCorreoService();
        }

        public ActionResult Index()
        {
            var solicitudes = new List<RevisionDocumentalSolicitudRowViewModel>();
            var contextoInspector = ConstruirContextoInspectorActual();
            var itemsBandeja = EsAdmin()
                ? _revisionDocumentalBandejaService.ObtenerItemsBandejaInspector(Enumerable.Empty<int>(), Enumerable.Empty<string>(), true)
                : _revisionDocumentalBandejaService.ObtenerItemsBandejaInspector(contextoInspector.Ids, contextoInspector.Identificadores);

            foreach (var itemBandeja in itemsBandeja ?? Enumerable.Empty<RevisionDocumentalBandejaItem>())
            {
                var codigoSolicitud = itemBandeja.CodigoSolicitud;
                var solicitud = _solicitudDao.ObtenerPorId(codigoSolicitud);
                var estadoRevision = solicitud != null
                    ? _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
                    : null;

                if (solicitud == null)
                {
                    continue;
                }

                var fila = ConstruirFilaRevisionDocumental(solicitud, estadoRevision);
                if (fila == null)
                {
                    continue;
                }

                if (itemBandeja.MostrarAccionInspeccion)
                {
                    fila.CodigoInspeccion = itemBandeja.CodigoInspeccion;
                    fila.MostrarAccionInspeccion = true;
                    fila.EstadoDocumentalCodigo = "LISTO_INSPECCION_CAMPO";
                    fila.EstadoDocumentalNombre = "Lista para inspección de campo";
                    fila.EstadoDocumentalDetalle = "La fase documental fue confirmada. Continúe con la LV/EAE en el detalle de inspección.";
                }
                else if (itemBandeja.CodigoInspeccion.HasValue
                    && itemBandeja.CodigoInspeccion.Value > 0
                    && estadoRevision != null
                    && estadoRevision.DocumentacionAprobada)
                {
                    fila.CodigoInspeccion = itemBandeja.CodigoInspeccion;
                    fila.PendienteConfirmacionInspector = true;
                    fila.EstadoDocumentalCodigo = "PENDIENTE_CONFIRMACION_INSPECTOR";
                    fila.EstadoDocumentalNombre = "Pendiente confirmación del inspector";
                    fila.EstadoDocumentalDetalle = "Revise la documentación. Al aceptar todos los documentos requeridos, la LV/EAE se habilitará automáticamente.";
                }

                solicitudes.Add(fila);
            }

            Trace.TraceInformation(
                "[DOC_FLOW] Accion=BANDEJA_INSPECTOR; Usuario=" + (Session["CodigoUsuario"] ?? User.Identity.Name ?? string.Empty) +
                "; TotalSolicitudes=" + solicitudes.Count +
                "; InspectorIds=" + string.Join(",", contextoInspector.Ids.OrderBy(x => x)) +
                "; Identificadores=" + string.Join(",", contextoInspector.Identificadores.OrderBy(x => x)));

            var modelo = new RevisionDocumentalIndexViewModel
            {
                Solicitudes = solicitudes
                    .OrderByDescending(item => item.FechaCargaDocumentos ?? DateTime.MinValue)
                    .ThenByDescending(item => item.CodigoSolicitud)
                    .ToList(),
                TotalSolicitudesPendientes = solicitudes.Count,
                TotalDocumentosPendientes = solicitudes.Sum(item => item.DocumentosPendientes),
                TotalSolicitudesEnRevision = solicitudes.Count(item => string.Equals(item.EstadoDocumentalCodigo, "EN_REVISION_DOCUMENTAL", StringComparison.OrdinalIgnoreCase)),
                TotalDocumentosObservados = solicitudes.Sum(item => item.DocumentosObservados),
                TotalDocumentosAceptados = solicitudes.Sum(item => item.DocumentosAceptados),
                TotalDocumentosSubsanados = solicitudes.Sum(item => item.DocumentosSubsanados)
            };

            return View("~/Views/RevisionDocumental/Index.cshtml", modelo);
        }

        public ActionResult Detalle(int id)
        {
            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null)
            {
                return HttpNotFound("Solicitud no encontrada.");
            }

            if (!PuedeAccederRevisionDocumental(solicitud))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Forbidden, "No tiene permisos para revisar esta documentación.");
            }

            return RedirectToAction("Lista", "Documento", new { solicitudId = id, modo = "revision" });
        }

        [HttpPost]
        [Authorize(Roles = RolesRevisionDocumentalOperativa)]
        [ValidateAntiForgeryTokenFromHeader]
        public JsonResult GuardarRevisionDocumental(GuardarRevisionDocumentalRequest request)
        {
            request = request ?? new GuardarRevisionDocumentalRequest();
            request.Decisiones = request.Decisiones ?? new List<DecisionDocumentoRequest>();

            var usuarioId = ObtenerIdUsuarioActual();
            var login = ObtenerCodigoUsuarioSesion();
            var rolActivo = ObtenerRolActivo();
            var formToken = string.Empty;
            try
            {
                formToken = Request != null && Request.Form != null ? Request.Form["__RequestVerificationToken"] : string.Empty;
            }
            catch
            {
                formToken = string.Empty;
            }

            var tieneToken = Request != null
                && (!string.IsNullOrWhiteSpace(Request.Headers["RequestVerificationToken"])
                    || !string.IsNullOrWhiteSpace(Request.Headers["X-RequestVerificationToken"])
                    || !string.IsNullOrWhiteSpace(Request.Headers["X-Request-Verification-Token"])
                    || !string.IsNullOrWhiteSpace(Request.Headers["__RequestVerificationToken"])
                    || !string.IsNullOrWhiteSpace(formToken));

            Trace.TraceInformation(
                "[REV_DOC][POST_IN] SolicitudId=" + request.SolicitudId +
                "; UsuarioId=" + usuarioId +
                "; Login=" + login +
                "; RolActivo=" + rolActivo +
                "; Modo=" + (request.Modo ?? string.Empty) +
                "; Origen=" + (request.Origen ?? string.Empty) +
                "; ContentType=" + (Request != null ? Request.ContentType : string.Empty) +
                "; TieneToken=" + tieneToken +
                "; DecisionesRecibidas=" + request.Decisiones.Count);

            Trace.TraceInformation(
                "[REV_DOC][ESTADO_CHANGE_IN] SolicitudId=" + request.SolicitudId +
                "; UsuarioId=" + usuarioId +
                "; Rol=" + rolActivo +
                "; Decisiones=" + string.Join(",", request.Decisiones
                    .Where(d => d != null)
                    .Select(d => d.DocumentoId + ":" + ((d.Decision ?? d.Estado ?? string.Empty).Trim()))));

            if (request.SolicitudId <= 0)
            {
                return JsonRevisionError(400, "No se recibio el identificador de la solicitud.", request.SolicitudId, "SolicitudId invalido");
            }

            if (!ModelState.IsValid)
            {
                var errores = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(e => !string.IsNullOrWhiteSpace(e)));
                Trace.TraceWarning("[REV_DOC][MODEL_INVALID] SolicitudId=" + request.SolicitudId + "; Errores=" + errores);
                return JsonRevisionError(400, "La solicitud de revision documental no tiene un formato valido.", request.SolicitudId, errores);
            }

            foreach (var decision in request.Decisiones.Where(d => d != null))
            {
                if (string.IsNullOrWhiteSpace(decision.Decision) && !string.IsNullOrWhiteSpace(decision.Estado))
                {
                    decision.Decision = decision.Estado;
                }
            }

            var decisionesRecibidas = request.Decisiones
                .Where(d => d != null && d.DocumentoId > 0)
                .GroupBy(d => d.DocumentoId)
                .ToDictionary(g => g.Key, g => g.First());

            if (decisionesRecibidas.Count == 0 && !request.Finalizar)
            {
                return JsonRevisionError(400, "No se recibieron decisiones documentales validas.", request.SolicitudId, "Decisiones vacias");
            }

            var solicitud = _solicitudDao.ObtenerPorId(request.SolicitudId);
            if (solicitud == null)
            {
                return JsonRevisionError(400, "La solicitud no existe.", request.SolicitudId, "Solicitud no encontrada");
            }

            var estadoRevisable = _estadoService.EsEstadoRevisablePorInspector(solicitud.Estado);
            var inspecciones = _solicitudAocrInfraBl.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            var identidad = _inspectorIdentityService.ObtenerIdentidadInspector(usuarioId, login, login);
            var evaluacion = _inspectorIdentityService.EvaluarInspectorAsignado(solicitud.CodigoSolicitud, solicitud, inspecciones, identidad);
            var puedeGuardar = EsAdmin() || (estadoRevisable && evaluacion != null && evaluacion.EsInspectorAsignado);

            Trace.TraceInformation(
                "[REV_DOC][AUTH_CHECK] SolicitudId=" + solicitud.CodigoSolicitud +
                "; UsuarioId=" + usuarioId +
                "; Login=" + login +
                "; RolActivo=" + rolActivo +
                "; EstadoSolicitud=" + (solicitud.Estado ?? string.Empty) +
                "; InspectorAsignadoRaw=" + (evaluacion != null && evaluacion.Asignado != null ? evaluacion.Asignado.InspectorAsignadoRaw : string.Empty) +
                "; EsInspectorAsignado=" + (evaluacion != null && evaluacion.EsInspectorAsignado) +
                "; EstadoRevisable=" + estadoRevisable +
                "; PuedeGuardar=" + puedeGuardar);

            if (!estadoRevisable)
            {
                return JsonRevisionError(403, "La solicitud no se encuentra en estado valido para revision documental.", solicitud.CodigoSolicitud, "Estado de solicitud no revisable");
            }

            if (!puedeGuardar)
            {
                return JsonRevisionError(
                    403,
                    evaluacion != null && !string.IsNullOrWhiteSpace(evaluacion.Motivo)
                        ? evaluacion.Motivo
                        : "La solicitud no esta asignada a su usuario inspector.",
                    solicitud.CodigoSolicitud,
                    "Inspector no asignado");
            }

            var documentosRevision = ObtenerDocumentosVigentes(_documentoBl.ObtenerPorSolicitud(solicitud.CodigoSolicitud));
            var documentosSoloConsulta = documentosRevision
                .Count(d => d != null && !RevisionDocumentalDisplayHelper.ShouldIncludeInRevisionDocumental(d.TipoDocumento));
            var documentosPorId = documentosRevision
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToDictionary(d => d.CodigoDocumento, d => d);

            var documentosNoEncontrados = decisionesRecibidas.Keys
                .Where(id => !documentosPorId.ContainsKey(id))
                .ToList();
            if (documentosNoEncontrados.Count > 0)
            {
                return JsonRevisionError(400, "Uno o mas documentos enviados no pertenecen a la solicitud o no son vigentes.", solicitud.CodigoSolicitud, "Documentos no vigentes: " + string.Join(",", documentosNoEncontrados));
            }

            var documentosRecibidos = decisionesRecibidas.Keys.Select(id => documentosPorId[id]).ToList();
            var documentosBloqueados = documentosRecibidos
                .Where(DocumentoBloqueaModificacionRevision)
                .ToList();
            if (documentosBloqueados.Count > 0)
            {
                return JsonRevisionError(
                    400,
                    "Uno o mas documentos enviados no pueden modificarse porque ya fueron aceptados o corresponden a una version anterior: " + string.Join(", ", documentosBloqueados.Select(ObtenerEtiquetaDocumento)) + ".",
                    solicitud.CodigoSolicitud,
                    "Intento de modificar documentos bloqueados: " + string.Join(",", documentosBloqueados.Select(d => d.CodigoDocumento)));
            }

            var documentosOperacion = new List<Documento>();
            var documentosNoRevisables = new List<Documento>();
            foreach (var documento in documentosRecibidos)
            {
                string estadoNormalizado;
                string motivoNormalizacion;
                if (DocumentoPermiteRevisionInspector(documento, solicitud, out estadoNormalizado, out motivoNormalizacion))
                {
                    documentosOperacion.Add(documento);
                    if (!string.Equals(EstadoDocumentoInstitucional.Normalizar(documento.Estado), estadoNormalizado, StringComparison.OrdinalIgnoreCase))
                    {
                        Trace.TraceInformation(
                            "[REV_DOC][DOCUMENTO_NORMALIZADO] SolicitudId=" + solicitud.CodigoSolicitud +
                            "; DocumentoId=" + documento.CodigoDocumento +
                            "; EstadoOriginal=" + (documento.Estado ?? string.Empty) +
                            "; EstadoNormalizado=" + estadoNormalizado +
                            "; Motivo=" + motivoNormalizacion);
                    }
                    continue;
                }

                documentosNoRevisables.Add(documento);
                if (string.Equals(motivoNormalizacion, "DocumentoSoloConsultaNoRevisable", StringComparison.OrdinalIgnoreCase))
                {
                    Trace.TraceInformation(
                        "[REV_DOC][DOC_SOLO_CONSULTA_SKIP] SolicitudId=" + solicitud.CodigoSolicitud +
                        "; DocumentoId=" + documento.CodigoDocumento +
                        "; TipoDocumento=" + (documento.TipoDocumento ?? string.Empty));
                }

                Trace.TraceWarning(
                    "[REV_DOC][DOCUMENTO_SKIP] SolicitudId=" + solicitud.CodigoSolicitud +
                    "; DocumentoId=" + documento.CodigoDocumento +
                    "; TipoDocumento=" + (documento.TipoDocumento ?? string.Empty) +
                    "; Estado=" + (documento.Estado ?? string.Empty) +
                    "; Motivo=" + motivoNormalizacion);
            }

            Trace.TraceInformation(
                "[REV_DOC][DOCUMENTOS_CHECK] SolicitudId=" + solicitud.CodigoSolicitud +
                "; DocumentosRecibidos=" + decisionesRecibidas.Count +
                "; DocumentosValidos=" + documentosOperacion.Count +
                "; DocumentosNoRevisables=" + string.Join(",", documentosNoRevisables.Select(d => d.CodigoDocumento)) +
                "; Estados=" + string.Join(",", documentosRecibidos.Select(d => (d.Estado ?? string.Empty).Trim()).Distinct(StringComparer.OrdinalIgnoreCase)));

            if (documentosOperacion.Count == 0 && !(request.Finalizar && decisionesRecibidas.Count == 0))
            {
                return JsonRevisionError(400, "No existen documentos revisables para guardar.", solicitud.CodigoSolicitud, "Todos los documentos enviados fueron omitidos por no revisables");
            }

            foreach (var doc in documentosOperacion)
            {
                var item = decisionesRecibidas[doc.CodigoDocumento];
                var decisionNorm = NormalizarDecisionDocumento(item.Decision);
                if (string.IsNullOrWhiteSpace(decisionNorm))
                {
                    return JsonRevisionError(400, "La decision documental enviada no es valida.", solicitud.CodigoSolicitud, "Decision invalida en documento " + item.DocumentoId);
                }

                item.Decision = decisionNorm;
                item.Observacion = (item.Observacion ?? string.Empty).Trim();
                if (decisionNorm == EstadoDocumentoInstitucional.DevueltoInspector && string.IsNullOrWhiteSpace(item.Observacion))
                {
                    return JsonRevisionError(400, "Debe ingresar la observacion del documento rechazado antes de guardar la revision documental.", solicitud.CodigoSolicitud, "Observacion obligatoria faltante en documento " + item.DocumentoId);
                }
            }

            var usuarioRegistro = string.IsNullOrWhiteSpace(login) ? "sistema" : login;
            foreach (var doc in documentosOperacion)
            {
                var decision = decisionesRecibidas[doc.CodigoDocumento];
                var decisionRevision = decision.Decision == EstadoDocumentoInstitucional.DevueltoInspector ? "DEVUELTO" : "ACEPTADO";
                var estadoAnterior = EstadoDocumentoInstitucional.Normalizar(doc.Estado);

                doc.Estado = decision.Decision == EstadoDocumentoInstitucional.Aceptado
                    ? EstadoDocumentoInstitucional.Aceptado
                    : EstadoDocumentoInstitucional.DevueltoInspector;
                doc.Validado = decision.Decision == EstadoDocumentoInstitucional.Aceptado;
                doc.Observaciones = decision.Decision == EstadoDocumentoInstitucional.Aceptado ? null : decision.Observacion;
                doc.FechaValidacion = DateTime.Now;
                doc.ValidadoPor = usuarioRegistro;
                doc.UsuarioRegistro = usuarioRegistro;

                Trace.TraceInformation(
                    "[REV_DOC][DOC_DECISION] SolicitudId=" + solicitud.CodigoSolicitud +
                    "; DocumentoId=" + doc.CodigoDocumento +
                    "; TipoDocumento=" + (doc.TipoDocumento ?? string.Empty) +
                    "; EstadoAnterior=" + estadoAnterior +
                    "; Decision=" + decision.Decision +
                    "; EstadoNuevo=" + doc.Estado +
                    "; ObservacionLen=" + (decision.Observacion ?? string.Empty).Length +
                    "; PuedeRevisar=true" +
                    "; UsuarioInspector=" + usuarioId);

                if (!_documentoDao.Actualizar(doc))
                {
                    return JsonRevisionError(400, "No se pudo registrar la revision documental para todos los documentos.", solicitud.CodigoSolicitud, "Fallo actualizando documento " + doc.CodigoDocumento);
                }

                _solicitudAocrInfraBl.RegistrarRevisionDocumental(
                    solicitud.CodigoSolicitud,
                    doc.CodigoDocumento,
                    decisionRevision,
                    decision.Observacion,
                    usuarioId,
                    usuarioRegistro);

                _solicitudAocrInfraBl.RegistrarEventoHistorialRevision(
                    solicitud.CodigoSolicitud,
                    doc.CodigoDocumento,
                    decision.Decision == EstadoDocumentoInstitucional.Aceptado ? "DOCUMENTO_ACEPTADO" : "DOCUMENTO_DEVUELTO",
                    "Documento " + ObtenerEtiquetaDocumento(doc) + " marcado como " + decision.Decision + ". " + decision.Observacion,
                    usuarioId,
                    usuarioRegistro);

                Trace.TraceInformation(
                    (decision.Decision == EstadoDocumentoInstitucional.Aceptado ? "[REV_DOC][DOC_ACEPTADO]" : "[REV_DOC][DOC_RECHAZADO]") +
                    " SolicitudId=" + solicitud.CodigoSolicitud +
                    "; DocumentoId=" + doc.CodigoDocumento +
                    "; EstadoAnterior=" + estadoAnterior +
                    "; EstadoNuevo=" + doc.Estado +
                    "; UsuarioInspector=" + usuarioId);
            }

            var revisionesPersistidas = _solicitudAocrInfraBl.ObtenerUltimasRevisionesPorSolicitud(solicitud.CodigoSolicitud);
            var documentosCierre = documentosRevision
                .Where(d => DocumentoParticipaResumenRevision(d, solicitud, revisionesPersistidas))
                .ToList();

            var revisionesResumen = documentosCierre
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToDictionary(
                    d => d.CodigoDocumento,
                    d => Tuple.Create(
                        ObtenerDecisionRevisionDocumental(d, revisionesPersistidas),
                        ObtenerObservacionRevisionDocumental(d, revisionesPersistidas)));

            var aceptados = revisionesResumen.Count(x => x.Value.Item1 == "ACEPTADO");
            var devueltos = revisionesResumen.Count(x => x.Value.Item1 == "DEVUELTO" || x.Value.Item1 == "OBSERVADO");
            var pendientes = documentosCierre.Count - aceptados - devueltos;
            var siguienteEstado = solicitud.Estado;

            if (pendientes <= 0 && request.Finalizar)
            {
                request.ObservacionRevisionDocumental =
                    RevisionDocumentalCoordinadorService.NormalizarObservacion(request.ObservacionRevisionDocumental);
                if (request.ObservacionRevisionDocumental == null)
                {
                    return JsonRevisionError(400, "La observacion general no puede contener HTML ni superar 2000 caracteres.", solicitud.CodigoSolicitud, "Observacion general invalida");
                }

                if (devueltos > 0 && string.IsNullOrWhiteSpace(request.ObservacionRevisionDocumental))
                {
                    return JsonRevisionError(400, "Debe ingresar la observacion general cuando existen documentos observados o rechazados.", solicitud.CodigoSolicitud, "Observacion general obligatoria");
                }

                var inspeccionAsignada = inspecciones
                    .Where(i => i != null && i.CodigoInspeccion > 0 && i.CodigoInspector.GetValueOrDefault() > 0)
                    .OrderByDescending(i => i.CodigoInspeccion)
                    .FirstOrDefault();
                if (inspeccionAsignada == null)
                {
                    return JsonRevisionError(400, "La solicitud no tiene un inspector asignado para finalizar la revision.", solicitud.CodigoSolicitud, "Inspector no asignado");
                }

                var finalizacion = _coordinadorRevisionService.FinalizarRevisionDocumentalInspector(
                    solicitud.CodigoSolicitud,
                    inspeccionAsignada.CodigoInspector.Value,
                    request.ObservacionRevisionDocumental);
                if (!finalizacion.Ok || finalizacion.Registro == null)
                {
                    return JsonRevisionError(400, finalizacion.Mensaje, solicitud.CodigoSolicitud, "No se pudo registrar finalizacion de inspector");
                }

                Trace.TraceInformation("[REV_DOC][OFICIO_GENERAR_IN] SolicitudId=" + solicitud.CodigoSolicitud + ";");
                var documentoOficioId = finalizacion.Registro.DocumentoOficioId.GetValueOrDefault();
                if (documentoOficioId <= 0)
                {
                    try
                    {
                        documentoOficioId = GenerarYPersistirOficioRevisionDocumental(
                            solicitud,
                            inspeccionAsignada,
                            finalizacion.Registro,
                            documentosCierre,
                            usuarioRegistro);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("[REV_DOC][OFICIO_GENERAR_ERROR] SolicitudId=" + solicitud.CodigoSolicitud + "; Error=" + ex);
                        LogBL.RegistrarError(
                            "[REV_DOC][OFICIO_GENERAR_ERROR] SolicitudId=" + solicitud.CodigoSolicitud,
                            ex.ToString(),
                            "RevisionDocumentalController");
                        return JsonRevisionError(500, "No se pudo generar el oficio institucional. La revision no fue habilitada.", solicitud.CodigoSolicitud, ex.Message);
                    }
                }

                if (documentoOficioId <= 0 || !_coordinadorRevisionService.ObtenerPorSolicitud(solicitud.CodigoSolicitud).DocumentoOficioId.HasValue)
                {
                    return JsonRevisionError(500, "No se pudo asociar el oficio a la revision documental.", solicitud.CodigoSolicitud, "Documento oficio no asociado");
                }

                var resumenFinal = ConstruirResumenRevisionDocumental(documentosCierre, revisionesResumen, true);
                if (!_solicitudDao.CambiarEstado(
                    solicitud.CodigoSolicitud,
                    AocrEstadosProceso.PendienteCoordinador,
                    usuarioId,
                    "Revision finalizada por Inspector y pendiente de decision de Coordinacion. " + resumenFinal))
                {
                    return JsonRevisionError(400, "No se pudo actualizar el estado de la solicitud tras la revision documental.", solicitud.CodigoSolicitud, "Fallo cambio estado");
                }

                siguienteEstado = AocrEstadosProceso.PendienteCoordinador;
                _solicitudAocrInfraBl.RegistrarEventoHistorialRevision(
                    solicitud.CodigoSolicitud,
                    null,
                    "PENDIENTE_COORDINADOR",
                    "Revision finalizada por Inspector. Oficio " + finalizacion.Registro.NumeroOficio + " generado. LV e Informe Tecnico bloqueados hasta decision de Coordinacion.",
                    usuarioId,
                    usuarioRegistro);

                try
                {
                    _correoService.NotificarEvento(solicitud, "PENDIENTE_COORDINADOR", "Revision finalizada por Inspector.");
                }
                catch (Exception exNotif)
                {
                    Trace.TraceWarning("[NOTIF][PENDIENTE_COORDINADOR] SolicitudId=" + solicitud.CodigoSolicitud + "; Error=" + exNotif.Message);
                }

                Trace.TraceInformation(
                    "[REV_DOC][BANDEJA_COORDINADOR_OK] SolicitudId=" + solicitud.CodigoSolicitud +
                    "; CoordinadorId=0;");
            }
            else
            {
                _solicitudAocrInfraBl.RegistrarEventoHistorialRevision(
                    solicitud.CodigoSolicitud,
                    null,
                    "REVISION_DOCUMENTAL_GUARDADA_PARCIAL",
                    request.Finalizar
                        ? "Revision documental no finalizada. Pendientes=" + pendientes + "."
                        : "Revision documental guardada sin finalizar. Pendientes=" + pendientes + ".",
                    usuarioId,
                    usuarioRegistro);
            }

            Trace.TraceInformation(
                "[REV_DOC][RESUMEN_FINAL] SolicitudId=" + solicitud.CodigoSolicitud +
                "; Aceptados=" + aceptados +
                "; Rechazados=" + devueltos +
                "; Pendientes=" + pendientes +
                "; SoloConsulta=" + documentosSoloConsulta +
                "; EstadoSolicitudNuevo=" + siguienteEstado);

            Trace.TraceInformation(
                "[REV_DOC][GUARDAR_OK] SolicitudId=" + solicitud.CodigoSolicitud +
                "; Aceptados=" + aceptados +
                "; Devueltos=" + devueltos +
                "; Pendientes=" + pendientes +
                "; SiguienteEstado=" + siguienteEstado);

            return JsonRevisionOk(
                request.Finalizar
                    ? "Revision documental finalizada y enviada a Coordinacion. LV e Informe Tecnico permanecen bloqueados."
                    : "Revision documental guardada correctamente. Use Finalizar revision documental cuando concluya.",
                new
                {
                    aceptados,
                    devueltos,
                    pendientes,
                    siguienteEstado,
                    redirectUrl = Url.Action("Lista", "Documento", new { solicitudId = solicitud.CodigoSolicitud, modo = "revision", origen = "revision-documental" })
                });
        }

        private int GenerarYPersistirOficioRevisionDocumental(
            SolicitudAOCR solicitud,
            Inspeccion inspeccion,
            RevisionDocumentalCoordinadorRegistro registro,
            IEnumerable<Documento> documentos,
            string usuarioRegistro)
        {
            if (solicitud == null || inspeccion == null || registro == null)
            {
                throw new InvalidOperationException("No existe informacion suficiente para generar el oficio.");
            }

            var oficioExistente = (_documentoBl.ObtenerPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Documento>())
                .Where(d => d != null
                    && d.CodigoDocumento > 0
                    && string.Equals(d.TipoDocumento, "OFICIO_ACEPTACION_REVISION_DOCUMENTAL", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(d => d.CodigoDocumento)
                .FirstOrDefault();
            ViewBag.AceptacionNumeroOficio = registro.NumeroOficio;
            ViewBag.AceptacionFechaFirma = registro.FechaFinalizacionInspector ?? DateTime.Now;
            ViewBag.AceptacionFirmante = "Pendiente de firma de Coordinacion";
            ViewBag.AceptacionEstado = EstadoRevisionDocumentalCoordinador.PendienteCoordinador;
            ViewBag.AceptacionInspector = string.IsNullOrWhiteSpace(inspeccion.InspectorPrincipalNombre)
                ? solicitud.TecnicoResponsableNombre
                : inspeccion.InspectorPrincipalNombre;
            ViewBag.AceptacionObservacion = registro.ObservacionInspector;
            ViewBag.AceptacionEstaciones = new[] { solicitud.AeropuertosEcuador, solicitud.AeropuertosEcuadorOtros, solicitud.Provincia }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .SelectMany(x => x.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            ViewBag.AceptacionDocumentos = (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .Select(ObtenerEtiquetaDocumento)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var nombre = "Oficio_Aceptacion_Revision_Documental_" + solicitud.CodigoSolicitud + ".pdf";
            var pdf = new ViewAsPdf("~/Views/SolicitudAOCR/AceptacionDocumentalPdf.cshtml", solicitud)
            {
                FileName = nombre,
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(16, 18, 18, 18),
                CustomSwitches = "--enable-local-file-access --print-media-type --dpi 300 --zoom 1.0"
            };
            var bytes = pdf.BuildFile(ControllerContext);
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException("El motor PDF no genero contenido.");
            }

            // Persistir mediante la raiz centralizada. En produccion puede ser una ruta
            // compartida y no necesariamente el App_Data fisico del sitio IIS.
            var carpetaInterna = Path.Combine("Documentos", "RevisionDocumental", solicitud.CodigoSolicitud.ToString());
            var baseFisica = FileStorageHelper.GetPhysicalBasePath(null);
            var carpetaFisica = Path.Combine(baseFisica, carpetaInterna);
            Directory.CreateDirectory(carpetaFisica);
            var rutaFisica = Path.Combine(carpetaFisica, nombre);
            System.IO.File.WriteAllBytes(rutaFisica, bytes);
            var rutaRelativa = FileStorageHelper.NormalizeStoredPath(
                "~/App_Data/" + carpetaInterna.Replace('\\', '/') + "/" + nombre);

            int documentoId;
            if (oficioExistente != null)
            {
                oficioExistente.NombreArchivo = nombre;
                oficioExistente.RutaGuardada = rutaRelativa;
                oficioExistente.Extension = ".pdf";
                oficioExistente.TamanoBytes = bytes.LongLength;
                oficioExistente.Estado = "PENDIENTE";
                oficioExistente.Validado = false;
                oficioExistente.FechaValidacion = null;
                oficioExistente.ValidadoPor = null;
                oficioExistente.Observaciones = "Oficio regenerado al finalizar la revision documental. Pendiente de decision de Coordinacion.";
                oficioExistente.UsuarioRegistro = string.IsNullOrWhiteSpace(usuarioRegistro) ? "sistema" : usuarioRegistro;
                if (!_documentoDao.Actualizar(oficioExistente))
                {
                    throw new InvalidOperationException("No fue posible actualizar el oficio existente.");
                }
                documentoId = oficioExistente.CodigoDocumento;
            }
            else
            {
                documentoId = _documentoDao.Crear(new Documento
                {
                    CodigoSolicitud = solicitud.CodigoSolicitud,
                    TipoDocumento = "OFICIO_ACEPTACION_REVISION_DOCUMENTAL",
                    NombreArchivo = nombre,
                    NombreArchivoOriginal = nombre,
                    NombreArchivoVisible = "Oficio de aceptacion y designacion de inspector",
                    NombreArchivoFisico = nombre,
                    RutaGuardada = rutaRelativa,
                    Extension = ".pdf",
                    TamanoBytes = bytes.LongLength,
                    Estado = "PENDIENTE",
                    Validado = false,
                    FechaCarga = DateTime.Now,
                    Observaciones = "Oficio generado al finalizar la revision documental. Pendiente de decision de Coordinacion.",
                    Version = 1,
                    UsuarioRegistro = string.IsNullOrWhiteSpace(usuarioRegistro) ? "sistema" : usuarioRegistro
                });
            }

            if (documentoId <= 0 || !_coordinadorRevisionService.ObtenerPorSolicitud(solicitud.CodigoSolicitud).DocumentoOficioId.GetValueOrDefault().Equals(documentoId))
            {
                var dao = new RevisionDocumentalCoordinadorDAO();
                if (documentoId <= 0 || !dao.AsociarOficio(solicitud.CodigoSolicitud, documentoId))
                {
                    throw new InvalidOperationException("No fue posible asociar el oficio generado al flujo coordinador.");
                }
            }

            Trace.TraceInformation(
                "[REV_DOC][OFICIO_GENERADO_OK] SolicitudId=" + solicitud.CodigoSolicitud +
                "; DocumentoId=" + documentoId + ";");
            return documentoId;
        }

        private RevisionDocumentalSolicitudRowViewModel ConstruirFilaRevisionDocumental(SolicitudAOCR solicitud, EstadoRevisionDocumental estadoRevision)
        {
            if (solicitud == null)
            {
                return null;
            }

            var documentos = ObtenerDocumentosVigentes(_documentoBl.ObtenerPorSolicitud(solicitud.CodigoSolicitud));
            estadoRevision = estadoRevision
                ?? _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud)
                ?? new EstadoRevisionDocumental { CodigoSolicitud = solicitud.CodigoSolicitud, TienePendientes = true };

            var estadoDocumental = ResolverEstadoDocumental(estadoRevision, documentos.Count);
            var fechaCargaDocumentos = documentos
                .Select(d => d != null
                    ? (d.FechaCarga ?? d.FechaSubida)
                    : (DateTime?)null)
                .OrderByDescending(fecha => fecha ?? DateTime.MinValue)
                .FirstOrDefault();

            return new RevisionDocumentalSolicitudRowViewModel
            {
                CodigoSolicitud = solicitud.CodigoSolicitud,
                NumeroSolicitud = string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                    ? "AOCR" + solicitud.CodigoSolicitud
                    : solicitud.NumeroSolicitud.Trim(),
                Operadora = ObtenerOperadoraVisible(solicitud),
                Responsable = ObtenerResponsableVisible(solicitud),
                EstadoSolicitud = (solicitud.Estado ?? string.Empty).Trim(),
                EstadoDocumentalCodigo = estadoDocumental.Item1,
                EstadoDocumentalNombre = estadoDocumental.Item2,
                EstadoDocumentalDetalle = estadoDocumental.Item3,
                FechaCargaDocumentos = fechaCargaDocumentos,
                DocumentosCargados = estadoRevision.TotalDocumentosVigentes,
                DocumentosPendientes = estadoRevision.DocumentosPendientesRevision,
                DocumentosObservados = estadoRevision.DocumentosObservadosDevueltos,
                DocumentosAceptados = estadoRevision.DocumentosAceptados,
                DocumentosSubsanados = estadoRevision.DocumentosSubsanadosPendientes,
                TieneDocumentosCargados = estadoRevision.TotalDocumentosVigentes > 0
            };
        }

        private bool PuedeAccederRevisionDocumental(SolicitudAOCR solicitud)
        {
            return PuedeAccederRevisionDocumental(
                solicitud,
                solicitud != null ? _solicitudAocrInfraBl.ObtenerEstadoRevisionDocumental(solicitud.CodigoSolicitud) : null,
                ConstruirContextoInspectorActual());
        }

        private bool PuedeAccederRevisionDocumental(SolicitudAOCR solicitud, EstadoRevisionDocumental estadoRevision, InspectorIdentityContext contextoInspector)
        {
            if (solicitud == null)
            {
                return false;
            }

            if (EsAdmin())
            {
                return true;
            }

            var inspecciones = _solicitudAocrInfraBl.ListarInspeccionesPorSolicitud(solicitud.CodigoSolicitud) ?? new List<Inspeccion>();
            return RevisionDocumentalBandejaService.PuedeAccederRevisionDocumental(
                solicitud,
                estadoRevision,
                inspecciones,
                contextoInspector != null ? contextoInspector.Ids : new HashSet<int>(),
                contextoInspector != null ? contextoInspector.Identificadores : new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private HashSet<int> ObtenerIdsInspectorActual()
        {
            return ConstruirContextoInspectorActual().Ids;
        }

        private InspectorIdentityContext ConstruirContextoInspectorActual()
        {
            var ids = new HashSet<int>();
            var identificadores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var usuarioIdActual = ObtenerIdUsuarioActual();
            var codigoUsuarioTexto = ObtenerCodigoUsuarioSesion();
            var codigoUsuarioNumerico = ObtenerCodigoUsuario();

            if (usuarioIdActual > 0)
            {
                ids.Add(usuarioIdActual);
            }

            AgregarIdentificadorInspector(identificadores, codigoUsuarioTexto);

            try
            {
                UsuarioInternoRTRegistro inspectorActual = null;

                if (usuarioIdActual > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(usuarioIdActual);
                }

                if (inspectorActual == null && !string.IsNullOrWhiteSpace(codigoUsuarioTexto))
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(codigoUsuarioTexto)
                        ?? _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(codigoUsuarioTexto);
                }

                if (inspectorActual == null && codigoUsuarioNumerico > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(codigoUsuarioNumerico);
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                        AgregarIdentificadorInspector(identificadores, inspectorActual.UsuarioId.Value.ToString());
                    }

                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                        AgregarIdentificadorInspector(identificadores, inspectorActual.TecnicoId.Value.ToString());
                    }

                    AgregarIdentificadorInspector(identificadores, inspectorActual.CodigoUsuario);
                    AgregarIdentificadorInspector(identificadores, inspectorActual.Identificacion);
                    AgregarIdentificadorInspector(identificadores, inspectorActual.UsuarioLogin);
                }
            }
            catch
            {
                // La bandeja tolera ambientes donde el catálogo RT no esté completo.
            }

            if (codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
                AgregarIdentificadorInspector(identificadores, codigoUsuarioNumerico.ToString());
            }

            return new InspectorIdentityContext
            {
                Ids = ids,
                Identificadores = identificadores
            };
        }

        private List<Documento> ObtenerDocumentosVigentes(IEnumerable<Documento> documentos)
        {
            return (documentos ?? Enumerable.Empty<Documento>())
                .Where(documento => documento != null && documento.CodigoDocumento > 0)
                .GroupBy(ObtenerClaveDocumentoRevision, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(documento => documento.Version ?? 0)
                    .ThenByDescending(documento => documento.FechaCarga ?? documento.FechaSubida ?? DateTime.MinValue)
                    .ThenByDescending(documento => documento.CodigoDocumento)
                    .First())
                .ToList();
        }

        private static Tuple<string, string, string> ResolverEstadoDocumental(EstadoRevisionDocumental estadoRevision, int totalDocumentos)
        {
            if (totalDocumentos <= 0)
            {
                return Tuple.Create(
                    "PENDIENTE_CARGA_DOCUMENTAL",
                    "Pendiente de carga documental",
                    "El RT todavía no ha cargado documentos habilitantes para iniciar la revisión documental.");
            }

            if (estadoRevision != null && estadoRevision.DocumentacionAprobada)
            {
                return Tuple.Create(
                    "DOCUMENTACION_APROBADA",
                    "Documentación aprobada",
                    "Todos los documentos vigentes fueron aceptados y la fase documental quedó cerrada.");
            }

            if (estadoRevision != null && estadoRevision.TieneDocumentosObservados)
            {
                return Tuple.Create(
                    "DOCUMENTACION_OBSERVADA",
                    "Documentación observada",
                    "Existen documentos observados o devueltos pendientes de subsanación por parte del RT.");
            }

            if (estadoRevision != null && estadoRevision.TieneDocumentosSubsanadosPendientes)
            {
                return Tuple.Create(
                    "DOCUMENTACION_SUBSANADA",
                    "Documentación subsanada",
                    "El RT ya subsanó documentos y requieren una nueva revisión del inspector.");
            }

            if (estadoRevision != null && estadoRevision.DocumentosAceptados > 0 && estadoRevision.DocumentosPendientesRevision > 0)
            {
                return Tuple.Create(
                    "EN_REVISION_DOCUMENTAL",
                    "En revisión documental",
                    "La revisión documental está en curso: existen documentos aceptados y otros pendientes de decisión.");
            }

            return Tuple.Create(
                "DOCUMENTOS_CARGADOS",
                "Documentos cargados",
                "La documentación habilitante ya fue cargada y está pendiente de revisión por el inspector.");
        }

        private static string ObtenerClaveDocumentoRevision(Documento documento)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            var tipoDocumento = (documento.TipoDocumento ?? string.Empty).Trim();
            if (RevisionDocumentalDisplayHelper.AllowsMultipleActiveDocuments(tipoDocumento))
            {
                return "__DOC_" + documento.CodigoDocumento;
            }

            return !string.IsNullOrWhiteSpace(tipoDocumento)
                ? tipoDocumento.ToUpperInvariant()
                : "__DOC_" + documento.CodigoDocumento;
        }

        private static string ObtenerOperadoraVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "No disponible";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RazonSocial))
            {
                return solicitud.RazonSocial.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.NombreOperador))
            {
                return solicitud.NombreOperador.Trim();
            }

            return "No disponible";
        }

        private static string ObtenerResponsableVisible(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return "No disponible";
            }

            if (!string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre))
            {
                return solicitud.TecnicoResponsableNombre.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.RepresentanteLegal))
            {
                return solicitud.RepresentanteLegal.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.CorreoRepresentanteTecnico))
            {
                return solicitud.CorreoRepresentanteTecnico.Trim();
            }

            if (!string.IsNullOrWhiteSpace(solicitud.Email))
            {
                return solicitud.Email.Trim();
            }

            return "No disponible";
        }

        private bool EsAdmin()
        {
            return User != null && User.IsInRole("Administrador");
        }

        private static bool CoincideIdentificadorInspector(string valor, HashSet<string> identificadores)
        {
            return !string.IsNullOrWhiteSpace(valor)
                && identificadores != null
                && identificadores.Contains(valor.Trim().ToUpperInvariant());
        }

        private static void AgregarIdentificadorInspector(HashSet<string> identificadores, string valor)
        {
            if (identificadores == null || string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            identificadores.Add(valor.Trim().ToUpperInvariant());
        }

        private int ObtenerCodigoUsuario()
        {
            int id;
            return _userContext.TryGetCodigoUsuario(Session, out id) ? id : 0;
        }

        private int ObtenerIdUsuarioActual()
        {
            int id;
            return _userContext.TryGetUserId(Session, out id) ? id : 0;
        }

        private string ObtenerCodigoUsuarioSesion()
        {
            var codigoUsuario = Session != null ? Session["CodigoUsuario"] as string : null;
            if (!string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return codigoUsuario.Trim();
            }

            if (User != null && User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(User.Identity.Name))
            {
                return User.Identity.Name.Trim();
            }

            return string.Empty;
        }

        private string ObtenerRolActivo()
        {
            return Session != null && Session["Rol"] != null
                ? Session["Rol"].ToString()
                : string.Empty;
        }

        private JsonResult JsonRevisionOk(string message, object data)
        {
            Response.StatusCode = 200;
            Response.TrySkipIisCustomErrors = true;
            return Json(new AocrJsonResult
            {
                ok = true,
                success = true,
                code = 200,
                message = message,
                data = data
            });
        }

        private JsonResult JsonRevisionError(int code, string message, int solicitudId, string motivo)
        {
            var safeCode = code <= 0 ? 400 : code;
            var safeMessage = string.IsNullOrWhiteSpace(message)
                ? "No se pudo guardar la revision documental."
                : message.Trim();

            Response.StatusCode = safeCode;
            Response.TrySkipIisCustomErrors = true;

            Trace.TraceWarning(
                "[REV_DOC][POST_" + safeCode + "] SolicitudId=" + solicitudId +
                "; Motivo=" + (string.IsNullOrWhiteSpace(motivo) ? safeMessage : motivo));

            return Json(new AocrJsonResult
            {
                ok = false,
                success = false,
                code = safeCode,
                message = safeMessage,
                data = null
            });
        }

        private static bool DocumentoBloqueaModificacionRevision(Documento documento)
        {
            if (documento == null)
            {
                return true;
            }

            var estado = EstadoDocumentoInstitucional.Normalizar(documento.Estado);
            return string.Equals(estado, EstadoDocumentoInstitucional.VersionAnterior, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoDocumentoInstitucional.Aceptado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoDocumentoInstitucional.Bloqueado, StringComparison.OrdinalIgnoreCase);
        }

        private static bool DocumentoPermiteRevisionInspector(Documento documento, SolicitudAOCR solicitud, out string estadoNormalizado, out string motivo)
        {
            estadoNormalizado = string.Empty;
            motivo = string.Empty;

            if (documento == null || documento.CodigoDocumento <= 0)
            {
                motivo = "Documento nulo o sin identificador.";
                return false;
            }

            if (!RevisionDocumentalDisplayHelper.ShouldIncludeInRevisionDocumental(documento.TipoDocumento))
            {
                motivo = "DocumentoSoloConsultaNoRevisable";
                return false;
            }

            var estado = EstadoDocumentoInstitucional.Normalizar(documento.Estado);
            estadoNormalizado = estado;
            if (string.Equals(estado, EstadoDocumentoInstitucional.VersionAnterior, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoDocumentoInstitucional.Aceptado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoDocumentoInstitucional.Bloqueado, StringComparison.OrdinalIgnoreCase))
            {
                motivo = "Documento bloqueado para modificacion.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(documento.NombreArchivo) && string.IsNullOrWhiteSpace(documento.RutaGuardada))
            {
                motivo = "Documento sin archivo vigente.";
                return false;
            }

            var estadoRaw = (documento.Estado ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            if (string.Equals(estadoRaw, "CARGADO", StringComparison.OrdinalIgnoreCase))
            {
                estadoNormalizado = EstadoDocumentoInstitucional.PendienteRevision;
                motivo = "DocumentoCargadoVersionActivaRevisable";
                return true;
            }

            var revisable = EstadoDocumentoInstitucional.EsEstadoRevisablePorInspector(documento.Estado);
            motivo = revisable ? "Estado revisable por inspector." : "Estado no revisable por inspector.";
            return revisable;
        }

        private static bool DocumentoParticipaResumenRevision(Documento documento, SolicitudAOCR solicitud, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null || documento.CodigoDocumento <= 0)
            {
                return false;
            }

            if (!RevisionDocumentalDisplayHelper.ShouldIncludeInRevisionDocumental(documento.TipoDocumento))
            {
                return false;
            }

            var estado = EstadoDocumentoInstitucional.Normalizar(documento.Estado);
            if (string.Equals(estado, EstadoDocumentoInstitucional.VersionAnterior, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoDocumentoInstitucional.Bloqueado, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
            if (!string.IsNullOrWhiteSpace(decision))
            {
                return true;
            }

            string estadoNormalizado;
            string motivo;
            return DocumentoPermiteRevisionInspector(documento, solicitud, out estadoNormalizado, out motivo);
        }

        private static string NormalizarDecisionDocumento(string decision)
        {
            var normalized = (decision ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            switch (normalized)
            {
                case "ACEPTADO":
                case "ACEPTAR":
                case "ACEPTAR_DOCUMENTO":
                case "APROBADO":
                case "VALIDADO":
                    return EstadoDocumentoInstitucional.Aceptado;
                case "DEVUELTO_INSPECTOR":
                case "DEVUELTO":
                case "DEVOLVER":
                case "DEVOLVER_PARA_SUBSANACION":
                case "DEVOLVER_PARA_SUBSANACIÃ“N":
                case "RECHAZADO":
                case "OBSERVADO":
                    return EstadoDocumentoInstitucional.DevueltoInspector;
                default:
                    return string.Empty;
            }
        }

        private static string NormalizarDecisionRevisionDocumental(string decision)
        {
            var normalizada = EstadoDocumentoInstitucional.NormalizarDecisionRevision(decision);
            if (normalizada == "ACEPTADO")
            {
                return "ACEPTADO";
            }

            if (normalizada == "DEVUELTO" || normalizada == "OBSERVADO")
            {
                return normalizada;
            }

            return string.Empty;
        }

        private static string ObtenerDecisionRevisionDocumental(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revisionActual;
            if (revisiones != null
                && revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual)
                && revisionActual != null
                && !string.IsNullOrWhiteSpace(revisionActual.Item1))
            {
                return NormalizarDecisionRevisionDocumental(revisionActual.Item1);
            }

            var estado = EstadoDocumentoInstitucional.Normalizar(documento.Estado);
            if (string.Equals(estado, EstadoDocumentoInstitucional.Aceptado, StringComparison.OrdinalIgnoreCase))
            {
                return "ACEPTADO";
            }

            if (string.Equals(estado, EstadoDocumentoInstitucional.Observado, StringComparison.OrdinalIgnoreCase))
            {
                return "OBSERVADO";
            }

            if (string.Equals(estado, EstadoDocumentoInstitucional.DevueltoInspector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoDocumentoInstitucional.Rechazado, StringComparison.OrdinalIgnoreCase))
            {
                return "DEVUELTO";
            }

            return string.Empty;
        }

        private static string ObtenerObservacionRevisionDocumental(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revisionActual;
            if (revisiones != null
                && revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual)
                && revisionActual != null
                && !string.IsNullOrWhiteSpace(revisionActual.Item2))
            {
                return revisionActual.Item2.Trim();
            }

            return (documento.Observaciones ?? string.Empty).Trim();
        }

        private static string ConstruirResumenRevisionDocumental(IEnumerable<Documento> documentos, IDictionary<int, Tuple<string, string>> revisiones, bool soloDevueltos)
        {
            var items = (documentos ?? Enumerable.Empty<Documento>())
                .Select(d => new
                {
                    Documento = ObtenerEtiquetaDocumento(d),
                    Decision = ObtenerDecisionRevisionDocumental(d, revisiones),
                    Observacion = ObtenerObservacionRevisionDocumental(d, revisiones)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Decision))
                .Where(x => !soloDevueltos || x.Decision == "DEVUELTO" || x.Decision == "OBSERVADO")
                .Select(x => x.Documento + ": " + RevisionDocumentalDisplayHelper.GetVisibleStateLabel(x.Decision) + (string.IsNullOrWhiteSpace(x.Observacion) ? string.Empty : " - " + x.Observacion))
                .ToList();

            if (items.Count == 0)
            {
                return soloDevueltos
                    ? "La solicitud fue devuelta para subsanacion documental."
                    : "La revision documental fue cerrada.";
            }

            return string.Join(" | ", items);
        }

        private static string ObtenerEtiquetaDocumento(Documento documento)
        {
            if (documento == null)
            {
                return "Documento";
            }

            if (!string.IsNullOrWhiteSpace(documento.TipoDocumentoNombre))
            {
                return documento.TipoDocumentoNombre.Trim();
            }

            if (!string.IsNullOrWhiteSpace(documento.TipoDocumento))
            {
                return documento.TipoDocumento.Trim();
            }

            if (!string.IsNullOrWhiteSpace(documento.NombreArchivo))
            {
                return documento.NombreArchivo.Trim();
            }

            return "Documento #" + documento.CodigoDocumento;
        }

        private sealed class InspectorIdentityContext
        {
            public HashSet<int> Ids { get; set; }
            public HashSet<string> Identificadores { get; set; }
        }
    }

    public sealed class GuardarRevisionDocumentalRequest
    {
        public int SolicitudId { get; set; }
        public string Modo { get; set; }
        public string Origen { get; set; }
        public bool Finalizar { get; set; }
        public string ObservacionRevisionDocumental { get; set; }
        public List<DecisionDocumentoRequest> Decisiones { get; set; }
    }

    public sealed class DecisionDocumentoRequest
    {
        public int DocumentoId { get; set; }
        public string Decision { get; set; }
        public string Estado { get; set; }
        public string Observacion { get; set; }
    }

    public sealed class AocrJsonResult
    {
        public bool ok { get; set; }
        public bool success { get; set; }
        public int code { get; set; }
        public string message { get; set; }
        public object data { get; set; }
    }
}
