using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class RevisionDocumentalCierreDecision
    {
        public string EstadoDestino { get; set; }
        public string ObservacionCierre { get; set; }
        public bool RequiereNotificarObservaciones { get; set; }
    }

    public sealed class RevisionDocumentalValidacionResult
    {
        public bool EsValido { get; set; }
        public string Mensaje { get; set; }
        public bool TieneDocumentosDevueltos { get; set; }
    }

    public sealed class RevisionDocumentalFirmaPlan
    {
        public bool EsValido { get; set; }
        public string Mensaje { get; set; }
        public string EstadoDestino { get; set; }
        public string ObservacionEstado { get; set; }
    }

    public class RevisionDocumentalService
    {
        private const string MensajeBloqueoPredeterminado = "No se puede iniciar la inspección porque la fase documental aún no ha sido finalizada.";
        private static readonly HashSet<string> EstadosSolicitudNoCompatiblesInspeccion = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ANULADA",
            "CANCELADA"
        };

        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBL;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrPostPagoWorkflowService _postPagoWorkflowService;

        public RevisionDocumentalService()
        {
            _solicitudAocrInfraBL = new SolicitudAocrInfraBL();
            _solicitudDao = new SolicitudAOCRDAO();
            _postPagoWorkflowService = new AocrPostPagoWorkflowService();
        }

        public EstadoRevisionDocumental ObtenerEstadoFaseDocumental(int codigoSolicitud)
        {
            var estado = _solicitudAocrInfraBL.ObtenerEstadoRevisionDocumental(codigoSolicitud);
            if (estado != null)
            {
                return estado;
            }

            return new EstadoRevisionDocumental
            {
                CodigoSolicitud = codigoSolicitud,
                TienePendientes = true,
                MensajeBloqueoDocumental = MensajeBloqueoPredeterminado
            };
        }

        public bool EstaFaseDocumentalAprobada(int codigoSolicitud)
        {
            var estado = ObtenerEstadoFaseDocumental(codigoSolicitud);
            return codigoSolicitud > 0
                && estado.TotalDocumentosVigentes > 0
                && estado.DocumentacionAprobada
                && !estado.TienePendientes
                && !estado.TieneDocumentosObservados
                && !estado.TieneDocumentosSubsanadosPendientes
                && estado.DocumentosPendientesRevision <= 0;
        }

        public bool EstaInspeccionHabilitadaParaEjecucion(Inspeccion inspeccion, SolicitudAOCR solicitud = null)
        {
            if (inspeccion == null
                || inspeccion.CodigoInspeccion <= 0
                || inspeccion.CodigoSolicitud <= 0
                || !inspeccion.CodigoInspector.HasValue
                || inspeccion.CodigoInspector.Value <= 0)
            {
                return false;
            }

            if (!_postPagoWorkflowService.PuedeInspectorIniciarRevisionDocumental(inspeccion.CodigoInspeccion, out _))
            {
                return false;
            }

            var solicitudInspeccion = solicitud ?? _solicitudDao.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (solicitudInspeccion != null && !EsEstadoSolicitudCompatibleInspeccion(solicitudInspeccion.Estado))
            {
                return false;
            }

            return EstaFaseDocumentalAprobada(inspeccion.CodigoSolicitud);
        }

        public bool EsEstadoSolicitudCompatibleInspeccion(string estadoSolicitud)
        {
            var estado = (estadoSolicitud ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(estado)
                || !EstadosSolicitudNoCompatiblesInspeccion.Contains(estado);
        }

        public string ObtenerMensajeInspeccionNoHabilitada(Inspeccion inspeccion, SolicitudAOCR solicitud = null)
        {
            if (inspeccion == null)
            {
                return MensajeBloqueoPredeterminado;
            }

            string mensajePrecondicion;
            if (!_postPagoWorkflowService.PuedeInspectorIniciarRevisionDocumental(inspeccion.CodigoInspeccion, out mensajePrecondicion)
                && !string.IsNullOrWhiteSpace(mensajePrecondicion))
            {
                return mensajePrecondicion;
            }

            var solicitudInspeccion = solicitud ?? _solicitudDao.ObtenerPorId(inspeccion.CodigoSolicitud);
            if (solicitudInspeccion != null && !EsEstadoSolicitudCompatibleInspeccion(solicitudInspeccion.Estado))
            {
                return MensajeBloqueoPredeterminado;
            }

            return ObtenerMensajeBloqueo(inspeccion.CodigoSolicitud);
        }

        public string ObtenerMensajeBloqueo(int codigoSolicitud)
        {
            var estado = ObtenerEstadoFaseDocumental(codigoSolicitud);
            if (!string.IsNullOrWhiteSpace(estado.MensajeBloqueoDocumental))
            {
                return estado.MensajeBloqueoDocumental;
            }

            return MensajeBloqueoPredeterminado;
        }

        public RevisionDocumentalCierreDecision CrearDecisionCierreMasivo(string tipoAccion, string observacionBase, string observacionCoordinador)
        {
            var aprobarTodos = string.Equals((tipoAccion ?? string.Empty).Trim(), "APROBAR_TODOS", StringComparison.OrdinalIgnoreCase);

            return new RevisionDocumentalCierreDecision
            {
                EstadoDestino = aprobarTodos ? EstadoSolicitud.AceptacionDocumental : EstadoSolicitud.Observada,
                ObservacionCierre = CombinarObservacionCierre(observacionBase, observacionCoordinador),
                RequiereNotificarObservaciones = !aprobarTodos
            };
        }

        public RevisionDocumentalCierreDecision CrearDecisionCierreFinal(bool tieneDocumentosDevueltos, string resumenObservaciones)
        {
            return new RevisionDocumentalCierreDecision
            {
                EstadoDestino = tieneDocumentosDevueltos ? EstadoSolicitud.Observada : EstadoSolicitud.AceptacionDocumental,
                ObservacionCierre = tieneDocumentosDevueltos
                    ? (resumenObservaciones ?? string.Empty).Trim()
                    : "Todos los documentos vigentes fueron aceptados por el inspector.",
                RequiereNotificarObservaciones = tieneDocumentosDevueltos
            };
        }

        public RevisionDocumentalCierreDecision CrearDecisionRevisionSimple(bool aprobada, string observacion)
        {
            return new RevisionDocumentalCierreDecision
            {
                EstadoDestino = aprobada ? EstadoSolicitud.AceptacionDocumental : EstadoSolicitud.Observada,
                ObservacionCierre = aprobada
                    ? "Aprobado por inspector"
                    : (observacion ?? string.Empty).Trim(),
                RequiereNotificarObservaciones = !aprobada
            };
        }

        public RevisionDocumentalValidacionResult ValidarChecklistParaAprobacion(IDictionary<string, int> estadisticasChecklist)
        {
            var stats = estadisticasChecklist ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var total = ObtenerConteoChecklist(stats, "Total");
            var sinEvaluar = ObtenerConteoChecklist(stats, "SinEvaluar");
            var noCumplen = ObtenerConteoChecklist(stats, "NoCumplen");

            var incompleto = total == 0 || sinEvaluar > 0 || noCumplen > 0;
            if (!incompleto)
            {
                return new RevisionDocumentalValidacionResult { EsValido = true };
            }

            return new RevisionDocumentalValidacionResult
            {
                EsValido = false,
                Mensaje = string.Format(
                    "No se puede aprobar. Checklist incompleto: Total={0}, SinEvaluar={1}, NoCumplen={2}.",
                    total,
                    sinEvaluar,
                    noCumplen)
            };
        }

        public RevisionDocumentalValidacionResult ValidarCierreRevisionDocumental(IEnumerable<Documento> documentos, IDictionary<int, Tuple<string, string>> revisiones)
        {
            var documentosRevision = (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToList();

            if (documentosRevision.Count == 0)
            {
                return new RevisionDocumentalValidacionResult
                {
                    EsValido = false,
                    Mensaje = "No existen documentos vigentes para cerrar la revisión."
                };
            }

            var documentosSinDecision = documentosRevision
                .Where(d => !DocumentoTieneDecisionFinal(d, revisiones))
                .Select(ObtenerEtiquetaDocumento)
                .ToList();

            if (documentosSinDecision.Count > 0)
            {
                return new RevisionDocumentalValidacionResult
                {
                    EsValido = false,
                    Mensaje = "No se puede enviar la revisión documental. Faltan decisiones en: " + string.Join(", ", documentosSinDecision) + "."
                };
            }

            var documentosSinObservacion = documentosRevision
                .Where(d => DocumentoRequiereObservacionPendiente(d, revisiones))
                .Select(ObtenerEtiquetaDocumento)
                .ToList();

            if (documentosSinObservacion.Count > 0)
            {
                return new RevisionDocumentalValidacionResult
                {
                    EsValido = false,
                    Mensaje = "No se puede enviar la revisión documental. Debe registrar observación en: " + string.Join(", ", documentosSinObservacion) + "."
                };
            }

            return new RevisionDocumentalValidacionResult
            {
                EsValido = true,
                TieneDocumentosDevueltos = documentosRevision.Any(d =>
                {
                    var decisionDoc = ObtenerDecisionRevisionDocumental(d, revisiones);
                    return decisionDoc == "DEVUELTO" || decisionDoc == "OBSERVADO";
                })
            };
        }

        public IList<Documento> ObtenerDocumentosPendientesSubsanacion(
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones)
        {
            return (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .Where(d =>
                {
                    var decision = ObtenerDecisionRevisionDocumental(d, revisiones);
                    return decision == "DEVUELTO" || decision == "OBSERVADO";
                })
                .ToList();
        }

        public RevisionDocumentalFirmaPlan PrepararFirmaAceptacionDocumental(
            string estadoActual,
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones,
            string observacion)
        {
            if (!string.Equals(EstadoSolicitud.Normalizar(estadoActual ?? string.Empty), EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                return new RevisionDocumentalFirmaPlan
                {
                    EsValido = false,
                    Mensaje = "La aceptación documental solo se puede firmar cuando el inspector haya aceptado toda la documentación."
                };
            }

            var documentosRevision = (documentos ?? Enumerable.Empty<Documento>())
                .Where(d => d != null && d.CodigoDocumento > 0)
                .ToList();

            if (documentosRevision.Count == 0 || documentosRevision.Any(d => ObtenerDecisionRevisionDocumental(d, revisiones) != "ACEPTADO"))
            {
                return new RevisionDocumentalFirmaPlan
                {
                    EsValido = false,
                    Mensaje = "No se puede firmar la aceptación mientras existan documentos sin aceptar por el inspector."
                };
            }

            return new RevisionDocumentalFirmaPlan
            {
                EsValido = true,
                EstadoDestino = EstadoSolicitud.FirmadoCoordinador,
                ObservacionEstado = string.IsNullOrWhiteSpace(observacion)
                    ? "Aceptación documental firmada por coordinación."
                    : observacion.Trim()
            };
        }

        private static string CombinarObservacionCierre(string observacionBase, string observacionCoordinador)
        {
            var observacionBaseLimpia = (observacionBase ?? string.Empty).Trim();
            var observacionCoordinadorLimpia = (observacionCoordinador ?? string.Empty).Trim();

            if (observacionCoordinadorLimpia.Length > 500)
            {
                observacionCoordinadorLimpia = observacionCoordinadorLimpia.Substring(0, 500);
            }

            if (string.IsNullOrWhiteSpace(observacionBaseLimpia))
            {
                observacionBaseLimpia = "La revisión documental fue cerrada.";
            }

            return string.IsNullOrWhiteSpace(observacionCoordinadorLimpia)
                ? observacionBaseLimpia
                : observacionBaseLimpia + " Observación para coordinación: " + observacionCoordinadorLimpia;
        }

        private static int ObtenerConteoChecklist(IDictionary<string, int> estadisticasChecklist, string clave)
        {
            int valor;
            return estadisticasChecklist != null && estadisticasChecklist.TryGetValue(clave, out valor)
                ? valor
                : 0;
        }

        private static string ObtenerEtiquetaDocumento(Documento documento)
        {
            if (documento == null)
            {
                return "Documento";
            }

            var etiqueta = !string.IsNullOrWhiteSpace(documento.TipoDocumentoNombre)
                ? documento.TipoDocumentoNombre.Trim()
                : (documento.TipoDocumento ?? "Documento").Trim();

            if (!string.IsNullOrWhiteSpace(documento.NombreArchivo))
            {
                return etiqueta + " (" + documento.NombreArchivo.Trim() + ")";
            }

            return etiqueta;
        }

        private static string ObtenerDecisionRevisionDocumental(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            if (documento == null)
            {
                return string.Empty;
            }

            Tuple<string, string> revisionActual;
            if (revisiones != null &&
                revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual) &&
                revisionActual != null &&
                !string.IsNullOrWhiteSpace(revisionActual.Item1))
            {
                return NormalizarDecisionRevisionDocumental(revisionActual.Item1);
            }

            var estadoDocumento = NormalizarEstadoDocumento(documento.Estado);
            if (estadoDocumento == "APROBADO" || estadoDocumento == "VALIDADO" || estadoDocumento == "ACEPTADO")
            {
                return "ACEPTADO";
            }

            if (estadoDocumento == "OBSERVADO")
            {
                return "OBSERVADO";
            }

            if (estadoDocumento == "RECHAZADO" || estadoDocumento == "DEVUELTO")
            {
                return "DEVUELTO";
            }

            if (estadoDocumento == "MODIFICACION_SOLICITADA"
                || estadoDocumento == "MODIFICACION SOLICITADA"
                || estadoDocumento == "SOLICITAR_MODIFICACION")
            {
                return "OBSERVADO";
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
            if (revisiones != null &&
                revisiones.TryGetValue(documento.CodigoDocumento, out revisionActual) &&
                revisionActual != null &&
                !string.IsNullOrWhiteSpace(revisionActual.Item2))
            {
                return revisionActual.Item2.Trim();
            }

            return (documento.Observaciones ?? string.Empty).Trim();
        }

        private static bool DocumentoTieneDecisionFinal(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
            return decision == "ACEPTADO" || decision == "DEVUELTO" || decision == "OBSERVADO";
        }

        private static bool DocumentoRequiereObservacionPendiente(Documento documento, IDictionary<int, Tuple<string, string>> revisiones)
        {
            var decision = ObtenerDecisionRevisionDocumental(documento, revisiones);
            if (!DecisionRevisionRequiereObservacion(decision))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(ObtenerObservacionRevisionDocumental(documento, revisiones));
        }

        private static string NormalizarDecisionRevisionDocumental(string decision)
        {
            var normalized = (decision ?? string.Empty).Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "ACEPTADO":
                case "APROBADO":
                case "VALIDADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                case "DEVUELTO":
                    return "DEVUELTO";
                case "OBSERVADO":
                case "MODIFICACION_SOLICITADA":
                case "MODIFICACION SOLICITADA":
                case "SOLICITAR_MODIFICACION":
                    return "OBSERVADO";
                default:
                    return normalized;
            }
        }

        private static string NormalizarEstadoDocumento(string estado)
        {
            var normalized = (estado ?? string.Empty).Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "APROBADO":
                case "VALIDADO":
                    return "ACEPTADO";
                case "RECHAZADO":
                    return "DEVUELTO";
                default:
                    return normalized;
            }
        }

        private static bool DecisionRevisionRequiereObservacion(string decision)
        {
            var normalizada = NormalizarDecisionRevisionDocumental(decision);
            return normalizada == "DEVUELTO" || normalizada == "OBSERVADO";
        }
    }
}
