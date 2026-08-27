using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using Npgsql;

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

    public sealed class RevisionDocumentalSubsanacionResult
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public int CodigoInspector { get; set; }
        public int DocumentosActualizados { get; set; }
        public int? CodigoHistorialEstado { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
    }

    public class RevisionDocumentalService
    {
        private const string MensajeBloqueoPredeterminado = "No se puede iniciar la inspección porque la fase documental aún no ha sido finalizada.";
        private const string EventoHistorialDocumentalSubsanacionEnviada = "SUBSANACION_ENVIADA_POR_RT";
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["AOCRConnection"] != null
            ? ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString
            : string.Empty;
        private static readonly HashSet<string> EstadosSolicitudNoCompatiblesInspeccion = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ANULADA",
            "CANCELADA"
        };

        private readonly SolicitudAocrInfraBL _solicitudAocrInfraBL;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly AocrPostPagoWorkflowService _postPagoWorkflowService;
        private readonly RevisionDocumentalCoordinadorService _coordinadorService;

        public RevisionDocumentalService(CapaDatos.Interfaces.IUsuarioAS400DAO usuarioAs400Dao = null, CapaDatos.Interfaces.IEmpresaAS400DAO empresaAs400Dao = null)
        {
            _solicitudAocrInfraBL = new SolicitudAocrInfraBL(usuarioAs400Dao, empresaAs400Dao);
            _solicitudDao = new SolicitudAOCRDAO();
            _postPagoWorkflowService = new AocrPostPagoWorkflowService();
            _coordinadorService = new RevisionDocumentalCoordinadorService();
        }

        public RevisionDocumentalSubsanacionResult EnviarSubsanacionAlInspector(
            int solicitudId,
            int usuarioRtId,
            string observacionRt,
            string usuarioRegistro = null)
        {
            var resultado = new RevisionDocumentalSubsanacionResult
            {
                Ok = false,
                Mensaje = "No fue posible enviar la subsanación al Inspector.",
                CodigoInspector = 0,
                DocumentosActualizados = 0,
                EstadoAnterior = string.Empty,
                EstadoNuevo = string.Empty
            };

            if (solicitudId <= 0 || usuarioRtId <= 0)
            {
                resultado.Mensaje = "Solicitud o usuario inválido para enviar subsanación.";
                return resultado;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                resultado.Mensaje = "No existe configuración de conexión a la base de datos AOCR.";
                return resultado;
            }

            usuarioRegistro = string.IsNullOrWhiteSpace(usuarioRegistro)
                ? usuarioRtId.ToString()
                : usuarioRegistro.Trim();

            System.Diagnostics.Trace.TraceInformation(
                "[DOC_FLOW][SUBSANACION_RT_INICIO] SolicitudId={0}; UsuarioRT={1}",
                solicitudId,
                usuarioRtId);

            try
            {
                using (var cn = new NpgsqlConnection(_connectionString))
                {
                    cn.Open();
                    using (var tx = cn.BeginTransaction())
                    {
                        var columnasSolicitud = ObtenerColumnasTabla(cn, tx, "aocr_tbsolicitud");
                        var columnasDocumento = ObtenerColumnasTabla(cn, tx, "aocr_tbdocumento");
                        var columnasInspeccion = ObtenerColumnasTabla(cn, tx, "aocr_tbinspeccion");

                        var solicitud = ObtenerSolicitudParaSubsanacion(cn, tx, solicitudId);
                        if (solicitud == null)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "La solicitud no existe.";
                            return resultado;
                        }

                        var estadoAnterior = EstadoSolicitud.Normalizar(solicitud.Estado);
                        resultado.EstadoAnterior = estadoAnterior;
                        if (!EstadoPermiteEnviarSubsanacion(estadoAnterior))
                        {
                            tx.Rollback();
                            resultado.Mensaje = "La solicitud no se encuentra en un estado válido para enviar subsanación al Inspector.";
                            return resultado;
                        }

                        var inspector = ResolverInspectorAsignado(cn, tx, solicitudId, solicitud.CodigoTecnico);
                        if (inspector.CodigoInspector <= 0)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "No se puede enviar la subsanación porque no existe Inspector asignado a la solicitud.";
                            return resultado;
                        }

                        var totalDevueltosPendientes = ContarDocumentosPorEstado(
                            cn,
                            tx,
                            solicitudId,
                            EstadoDocumentoInstitucional.DevueltoInspector,
                            EstadoDocumentoInstitucional.Observado,
                            EstadoDocumentoInstitucional.PendienteSubsanacion);

                        var totalSubsanadosPendientes = ContarDocumentosPorEstado(
                            cn,
                            tx,
                            solicitudId,
                            EstadoDocumentoInstitucional.SubsanadoRt,
                            EstadoDocumentoInstitucional.PendienteRevisionSubsanacion);

                        System.Diagnostics.Trace.TraceInformation(
                            "[DOC_FLOW][SUBSANACION_RT_VALIDACION] SolicitudId={0}; Devueltos={1}; Subsanados={2}; Faltantes={3}; InspectorAsignado={4}",
                            solicitudId,
                            totalDevueltosPendientes,
                            totalSubsanadosPendientes,
                            totalDevueltosPendientes,
                            inspector.CodigoInspector);

                        if (totalSubsanadosPendientes <= 0 || totalDevueltosPendientes > 0)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "Debe cargar la subsanación de todos los documentos devueltos por el Inspector antes de enviar nuevamente a revisión.";
                            return resultado;
                        }

                        var codigosDocumentosEnviados = ActualizarDocumentosSubsanadosARevisionInspector(
                            cn,
                            tx,
                            solicitudId,
                            usuarioRtId,
                            columnasDocumento);

                        if (codigosDocumentosEnviados.Count == 0)
                        {
                            tx.Rollback();
                            resultado.Mensaje = "No existen documentos subsanados pendientes para enviar al Inspector.";
                            return resultado;
                        }

                        var observacionFlujo = ConstruirObservacionFlujoSubsanacion(observacionRt, codigosDocumentosEnviados.Count);
                        var estadoDestinoPersistencia = EstadoSolicitud.EnInspeccion;
                        const string estadoDestinoLog = "EN_REVISION_INSPECTOR";

                        if (!ActualizarSolicitudARevisionInspector(
                            cn,
                            tx,
                            solicitudId,
                            inspector,
                            estadoDestinoPersistencia,
                            observacionFlujo,
                            usuarioRegistro,
                            columnasSolicitud))
                        {
                            tx.Rollback();
                            resultado.Mensaje = "No fue posible actualizar el estado de la solicitud.";
                            return resultado;
                        }

                        ActualizarInspeccionParaRevisionDocumental(
                            cn,
                            tx,
                            solicitudId,
                            inspector.CodigoInspector,
                            columnasInspeccion,
                            observacionFlujo,
                            usuarioRegistro);

                        var codigoHistorial = InsertarHistorialEstado(
                            cn,
                            tx,
                            solicitudId,
                            estadoAnterior,
                            estadoDestinoLog,
                            usuarioRtId,
                            "SUBSANACION_ENVIADA_POR_RT. " + observacionFlujo);

                        RegistrarHistorialDocumentalSubsanacion(
                            cn,
                            tx,
                            solicitudId,
                            codigosDocumentosEnviados,
                            usuarioRtId,
                            usuarioRegistro);

                        tx.Commit();

                        resultado.Ok = true;
                        resultado.Mensaje = "La subsanación fue enviada correctamente al Inspector.";
                        resultado.CodigoInspector = inspector.CodigoInspector;
                        resultado.DocumentosActualizados = codigosDocumentosEnviados.Count;
                        resultado.CodigoHistorialEstado = codigoHistorial;
                        resultado.EstadoNuevo = estadoDestinoLog;

                        System.Diagnostics.Trace.TraceInformation(
                            "[DOC_FLOW][SUBSANACION_RT_ESTADO] SolicitudId={0}; EstadoAnterior={1}; EstadoNuevo={2}; ResponsableNuevo=INSPECTOR",
                            solicitudId,
                            estadoAnterior ?? string.Empty,
                            estadoDestinoLog);
                        System.Diagnostics.Trace.TraceInformation(
                            "[DOC_FLOW][SUBSANACION_RT_OK] SolicitudId={0}; InspectorAsignado={1}; NotificacionCreada=True",
                            solicitudId,
                            inspector.CodigoInspector);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[DOC_FLOW][SUBSANACION_RT_ERROR] SolicitudId={0}; Error={1}",
                    solicitudId,
                    ex.Message ?? string.Empty);
                resultado.Ok = false;
                resultado.Mensaje = "Error al enviar la subsanación al Inspector: " + ex.Message;
            }

            return resultado;
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

            if (!EstaFaseDocumentalAprobada(inspeccion.CodigoSolicitud))
            {
                return false;
            }

            // Compatibilidad: los expedientes cerrados antes de este flujo no tienen registro
            // coordinador y conservan su comportamiento. Todo expediente nuevo que sí lo tenga
            // queda bloqueado hasta la aceptación y confirmación explícita del inspector.
            return !_coordinadorService.RequiereAceptacionCoordinador(inspeccion.CodigoSolicitud)
                || _coordinadorService.EstaAceptadaParaInspector(
                    inspeccion.CodigoSolicitud,
                    inspeccion.CodigoInspector.GetValueOrDefault());
        }

        /// <summary>
        /// El inspector completó explícitamente la fase documental (Confirmar cierre documental).
        /// Distinto de <see cref="EstaInspeccionHabilitadaParaEjecucion"/>, que solo valida precondiciones técnicas.
        /// </summary>
        public static bool InspectorConfirmoCierreDocumental(Inspeccion inspeccion)
        {
            if (inspeccion == null)
            {
                return false;
            }

            var estadoDocumental = (inspeccion.EstadoDocumental ?? string.Empty).Trim();
            if (string.Equals(estadoDocumental, "EN_REVISION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "ACEPTADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDocumental, "APROBADO", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var comentarios = inspeccion.Comentarios ?? string.Empty;
            return comentarios.IndexOf("Inspector confirmó revisión documental", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool PuedeInspectorAbrirFaseOperativaLv(Inspeccion inspeccion, SolicitudAOCR solicitud = null)
        {
            var requiereCoordinador = inspeccion != null
                && _coordinadorService.RequiereAceptacionCoordinador(inspeccion.CodigoSolicitud);
            return EstaInspeccionHabilitadaParaEjecucion(inspeccion, solicitud)
                && (requiereCoordinador || InspectorConfirmoCierreDocumental(inspeccion));
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

            if (_coordinadorService.RequiereAceptacionCoordinador(inspeccion.CodigoSolicitud)
                && !_coordinadorService.EstaAceptadaParaInspector(
                    inspeccion.CodigoSolicitud,
                    inspeccion.CodigoInspector.GetValueOrDefault()))
            {
                return "La revision documental esta pendiente de aceptacion por Coordinacion y confirmacion del inspector. LV e Informe Tecnico permanecen bloqueados.";
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
            return PrepararFirmaAceptacionDocumental(estadoActual, documentos, revisiones, observacion, tipoSolicitud: null);
        }

        public RevisionDocumentalFirmaPlan PrepararFirmaAceptacionDocumental(
            string estadoActual,
            IEnumerable<Documento> documentos,
            IDictionary<int, Tuple<string, string>> revisiones,
            string observacion,
            int? tipoSolicitud,
            bool tieneInspectorAsignado = false)
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
                EstadoDestino = ResolverEstadoDestinoFirmaAceptacionDocumental(tipoSolicitud, tieneInspectorAsignado),
                ObservacionEstado = string.IsNullOrWhiteSpace(observacion)
                    ? "Aceptación documental firmada por coordinación."
                    : observacion.Trim()
            };
        }

        public static string ResolverEstadoDestinoFirmaAceptacionDocumental(int? tipoSolicitud)
        {
            return ResolverEstadoDestinoFirmaAceptacionDocumental(tipoSolicitud, false);
        }

        public static string ResolverEstadoDestinoFirmaAceptacionDocumental(int? tipoSolicitud, bool tieneInspectorAsignado)
        {
            if (!tipoSolicitud.HasValue)
            {
                return EstadoSolicitud.FirmadoCoordinador;
            }

            if (tipoSolicitud == 1 || tipoSolicitud == 2)
            {
                return tieneInspectorAsignado
                    ? EstadoSolicitud.EnInspeccion
                    : EstadoSolicitud.PendienteAsignacionRT;
            }

            return EstadoSolicitud.FirmadoCoordinador;
        }

        private static bool EstadoPermiteEnviarSubsanacion(string estadoActual)
        {
            return string.Equals(estadoActual, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, EstadoSolicitud.Subsanada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, EstadoSolicitud.EnInspeccion, StringComparison.OrdinalIgnoreCase);
        }

        private static string ConstruirObservacionFlujoSubsanacion(string observacionRt, int totalDocumentos)
        {
            var baseObservacion = "Subsanación documental enviada por RT al Inspector. Documentos enviados: " + totalDocumentos + ".";
            var observacion = (observacionRt ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(observacion))
            {
                return baseObservacion;
            }

            return baseObservacion + " Comentario RT: " + observacion;
        }

        private static HashSet<string> ObtenerColumnasTabla(NpgsqlConnection cn, NpgsqlTransaction tx, string nombreTabla)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            const string sql = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tabla;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@tabla", nombreTabla ?? string.Empty);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!rd.IsDBNull(0))
                        {
                            columnas.Add(rd.GetString(0));
                        }
                    }
                }
            }

            return columnas;
        }

        private static SolicitudSubsanacionData ObtenerSolicitudParaSubsanacion(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId)
        {
            const string sql = @"
                SELECT codigo_solicitud, estado, codigo_tecnico
                FROM aocr_tbsolicitud
                WHERE codigo_solicitud = @id
                  AND deleted_at IS NULL
                FOR UPDATE;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@id", solicitudId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read())
                    {
                        return null;
                    }

                    return new SolicitudSubsanacionData
                    {
                        CodigoSolicitud = rd.IsDBNull(0) ? 0 : rd.GetInt32(0),
                        Estado = rd.IsDBNull(1) ? string.Empty : rd.GetString(1),
                        CodigoTecnico = rd.IsDBNull(2) ? (int?)null : rd.GetInt32(2)
                    };
                }
            }
        }

        private static InspectorAsignadoData ResolverInspectorAsignado(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            int? codigoTecnicoSolicitud)
        {
            var inspector = new InspectorAsignadoData
            {
                CodigoInspeccion = 0,
                CodigoInspector = codigoTecnicoSolicitud.HasValue ? codigoTecnicoSolicitud.Value : 0,
                EstadoInspeccion = string.Empty,
                InspectorPrincipalCedula = string.Empty,
                InspectorPrincipalNombre = string.Empty,
                InspectorPrincipalTipo = string.Empty
            };

            const string sql = @"
                SELECT
                    codigo_inspeccion,
                    codigo_inspector,
                    COALESCE(estado, ''),
                    COALESCE(inspector_principal_cedula, ''),
                    COALESCE(inspector_principal_nombre, ''),
                    COALESCE(inspector_principal_tipo, '')
                FROM aocr_tbinspeccion
                WHERE codigo_solicitud = @solicitud
                ORDER BY COALESCE(updated_at, created_at) DESC, codigo_inspeccion DESC
                LIMIT 1
                FOR UPDATE;";

            try
            {
                using (var cmd = new NpgsqlCommand(sql, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            inspector.CodigoInspeccion = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                            inspector.CodigoInspector = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                            inspector.EstadoInspeccion = rd.IsDBNull(2) ? string.Empty : rd.GetString(2);
                            inspector.InspectorPrincipalCedula = rd.IsDBNull(3) ? string.Empty : rd.GetString(3);
                            inspector.InspectorPrincipalNombre = rd.IsDBNull(4) ? string.Empty : rd.GetString(4);
                            inspector.InspectorPrincipalTipo = rd.IsDBNull(5) ? string.Empty : rd.GetString(5);
                        }
                    }
                }
            }
            catch
            {
                const string sqlFallback = @"
                    SELECT codigo_inspeccion, codigo_inspector, COALESCE(estado, '')
                    FROM aocr_tbinspeccion
                    WHERE codigo_solicitud = @solicitud
                    ORDER BY COALESCE(updated_at, created_at) DESC, codigo_inspeccion DESC
                    LIMIT 1
                    FOR UPDATE;";

                using (var cmd = new NpgsqlCommand(sqlFallback, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        if (rd.Read())
                        {
                            inspector.CodigoInspeccion = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                            inspector.CodigoInspector = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                            inspector.EstadoInspeccion = rd.IsDBNull(2) ? string.Empty : rd.GetString(2);
                        }
                    }
                }
            }

            if (inspector.CodigoInspector <= 0 && codigoTecnicoSolicitud.HasValue && codigoTecnicoSolicitud.Value > 0)
            {
                inspector.CodigoInspector = codigoTecnicoSolicitud.Value;
            }

            return inspector;
        }

        private static int ContarDocumentosPorEstado(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            params string[] estados)
        {
            var filtroEstados = (estados ?? Array.Empty<string>())
                .Where(valor => !string.IsNullOrWhiteSpace(valor))
                .Select(valor => valor.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();
            if (filtroEstados.Length == 0)
            {
                return 0;
            }

            const string sql = @"
                SELECT COUNT(*)
                FROM aocr_tbdocumento
                WHERE codigo_solicitud = @solicitud
                  AND UPPER(COALESCE(estado, '')) = ANY(@estados);";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@estados", filtroEstados);
                var valor = cmd.ExecuteScalar();
                return valor == null || valor == DBNull.Value ? 0 : Convert.ToInt32(valor);
            }
        }

        private static List<int> ActualizarDocumentosSubsanadosARevisionInspector(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            int usuarioRtId,
            HashSet<string> columnasDocumento)
        {
            var setClauses = new List<string> { "estado = @estado_nuevo" };
            if (columnasDocumento.Contains("validado"))
            {
                setClauses.Add("validado = FALSE");
            }
            if (columnasDocumento.Contains("fecha_validacion"))
            {
                setClauses.Add("fecha_validacion = NULL");
            }
            if (columnasDocumento.Contains("validado_por"))
            {
                setClauses.Add("validado_por = NULL");
            }
            if (columnasDocumento.Contains("requiere_subsanacion"))
            {
                setClauses.Add("requiere_subsanacion = FALSE");
            }
            if (columnasDocumento.Contains("fecha_subsanacion"))
            {
                setClauses.Add("fecha_subsanacion = NOW()");
            }
            if (columnasDocumento.Contains("usuario_subsanacion"))
            {
                setClauses.Add("usuario_subsanacion = @usuario_subsanacion");
            }
            if (columnasDocumento.Contains("version_activa"))
            {
                setClauses.Add("version_activa = TRUE");
            }
            if (columnasDocumento.Contains("es_version_activa"))
            {
                setClauses.Add("es_version_activa = TRUE");
            }

            var sql = @"
                UPDATE aocr_tbdocumento
                SET " + string.Join(", ", setClauses) + @"
                WHERE codigo_solicitud = @solicitud
                  AND UPPER(COALESCE(estado, '')) = ANY(@estados_origen)
                RETURNING codigo_documento;";

            var codigos = new List<int>();
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@estado_nuevo", EstadoDocumentoInstitucional.EnRevisionInspector);
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@usuario_subsanacion", usuarioRtId);
                cmd.Parameters.AddWithValue("@estados_origen", new[]
                {
                    EstadoDocumentoInstitucional.SubsanadoRt,
                    EstadoDocumentoInstitucional.PendienteRevisionSubsanacion
                });

                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        if (!rd.IsDBNull(0))
                        {
                            codigos.Add(rd.GetInt32(0));
                        }
                    }
                }
            }

            return codigos;
        }

        private static bool ActualizarSolicitudARevisionInspector(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            InspectorAsignadoData inspector,
            string estadoDestino,
            string observacion,
            string usuarioRegistro,
            HashSet<string> columnasSolicitud)
        {
            var setClauses = new List<string>
            {
                "estado = @estado",
                "observaciones = @observaciones",
                "updated_at = NOW()"
            };

            if (columnasSolicitud.Contains("updated_by"))
            {
                setClauses.Add("updated_by = @usuario");
            }
            if (columnasSolicitud.Contains("pendiente_asignacion_inspector"))
            {
                setClauses.Add("pendiente_asignacion_inspector = FALSE");
            }
            if (columnasSolicitud.Contains("pendiente_carga_documental_rt"))
            {
                setClauses.Add("pendiente_carga_documental_rt = FALSE");
            }
            if (columnasSolicitud.Contains("solicitud_finalizada_rt"))
            {
                setClauses.Add("solicitud_finalizada_rt = FALSE");
            }
            if (columnasSolicitud.Contains("fecha_subsanacion"))
            {
                setClauses.Add("fecha_subsanacion = NOW()");
            }
            if (columnasSolicitud.Contains("codigo_tecnico"))
            {
                setClauses.Add("codigo_tecnico = CASE WHEN COALESCE(codigo_tecnico, 0) <= 0 THEN @codigo_tecnico ELSE codigo_tecnico END");
            }
            if (columnasSolicitud.Contains("responsable_actual"))
            {
                setClauses.Add("responsable_actual = 'INSPECTOR'");
            }
            if (columnasSolicitud.Contains("tecnico_responsable_cedula"))
            {
                setClauses.Add("tecnico_responsable_cedula = CASE WHEN NULLIF(TRIM(COALESCE(tecnico_responsable_cedula, '')), '') IS NULL THEN @inspector_cedula ELSE tecnico_responsable_cedula END");
            }
            if (columnasSolicitud.Contains("tecnico_responsable_nombre"))
            {
                setClauses.Add("tecnico_responsable_nombre = CASE WHEN NULLIF(TRIM(COALESCE(tecnico_responsable_nombre, '')), '') IS NULL THEN @inspector_nombre ELSE tecnico_responsable_nombre END");
            }
            if (columnasSolicitud.Contains("tecnico_responsable_tipo"))
            {
                setClauses.Add("tecnico_responsable_tipo = CASE WHEN NULLIF(TRIM(COALESCE(tecnico_responsable_tipo, '')), '') IS NULL THEN @inspector_tipo ELSE tecnico_responsable_tipo END");
            }

            var sql = @"
                UPDATE aocr_tbsolicitud
                SET " + string.Join(", ", setClauses) + @"
                WHERE codigo_solicitud = @solicitud
                  AND deleted_at IS NULL;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@estado", estadoDestino);
                cmd.Parameters.AddWithValue("@observaciones", observacion ?? string.Empty);
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@usuario", usuarioRegistro ?? string.Empty);
                cmd.Parameters.AddWithValue("@codigo_tecnico", inspector.CodigoInspector > 0 ? (object)inspector.CodigoInspector : DBNull.Value);
                cmd.Parameters.AddWithValue("@inspector_cedula", (object)(inspector.InspectorPrincipalCedula ?? string.Empty));
                cmd.Parameters.AddWithValue("@inspector_nombre", (object)(inspector.InspectorPrincipalNombre ?? string.Empty));
                cmd.Parameters.AddWithValue("@inspector_tipo", (object)(inspector.InspectorPrincipalTipo ?? string.Empty));
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        private static void ActualizarInspeccionParaRevisionDocumental(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            int codigoInspector,
            HashSet<string> columnasInspeccion,
            string observacion,
            string usuarioRegistro)
        {
            if (codigoInspector <= 0)
            {
                return;
            }

            var setClauses = new List<string> { "updated_at = NOW()" };

            if (columnasInspeccion.Contains("codigo_inspector"))
            {
                setClauses.Add("codigo_inspector = CASE WHEN COALESCE(codigo_inspector, 0) <= 0 THEN @codigo_inspector ELSE codigo_inspector END");
            }
            if (columnasInspeccion.Contains("estado_documental"))
            {
                setClauses.Add("estado_documental = @estado_documental");
            }
            if (columnasInspeccion.Contains("estado"))
            {
                setClauses.Add("estado = CASE WHEN UPPER(COALESCE(estado, '')) IN ('OBSERVADA', 'OBSERVACION_DOCUMENTAL', 'SUBSANADA') THEN @estado_inspeccion ELSE estado END");
            }
            if (columnasInspeccion.Contains("comentarios"))
            {
                setClauses.Add("comentarios = CASE WHEN NULLIF(TRIM(COALESCE(comentarios, '')), '') IS NULL THEN @comentarios ELSE comentarios || ' | ' || @comentarios END");
            }
            if (columnasInspeccion.Contains("updated_by"))
            {
                setClauses.Add("updated_by = @updated_by");
            }

            var sql = @"
                UPDATE aocr_tbinspeccion
                SET " + string.Join(", ", setClauses) + @"
                WHERE codigo_inspeccion = (
                    SELECT codigo_inspeccion
                    FROM aocr_tbinspeccion
                    WHERE codigo_solicitud = @solicitud
                    ORDER BY COALESCE(updated_at, created_at) DESC, codigo_inspeccion DESC
                    LIMIT 1
                );";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@codigo_inspector", codigoInspector);
                cmd.Parameters.AddWithValue("@estado_documental", "EN_REVISION_DOCUMENTAL");
                cmd.Parameters.AddWithValue("@estado_inspeccion", EstadosInspeccion.VERIFICACION_SOLICITUD);
                cmd.Parameters.AddWithValue("@comentarios", "Subsanación RT enviada para revisión documental. " + (observacion ?? string.Empty));
                cmd.Parameters.AddWithValue("@updated_by", usuarioRegistro ?? string.Empty);
                cmd.ExecuteNonQuery();
            }
        }

        private static int? InsertarHistorialEstado(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            string estadoAnterior,
            string estadoNuevo,
            int usuarioId,
            string observacion)
        {
            const string sql = @"
                INSERT INTO aocr_tbhistorial_estado
                (
                    codigo_solicitud,
                    estado_anterior,
                    estado_nuevo,
                    codigo_usuario,
                    observaciones,
                    fecha_cambio
                )
                VALUES
                (
                    @solicitud,
                    @estado_anterior,
                    @estado_nuevo,
                    @usuario,
                    @observaciones,
                    NOW()
                )
                RETURNING codigo_historial;";

            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@estado_anterior", estadoAnterior ?? string.Empty);
                cmd.Parameters.AddWithValue("@estado_nuevo", estadoNuevo ?? string.Empty);
                cmd.Parameters.AddWithValue("@usuario", usuarioId);
                cmd.Parameters.AddWithValue("@observaciones", observacion ?? string.Empty);
                var value = cmd.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    return null;
                }

                return Convert.ToInt32(value);
            }
        }

        private static void RegistrarHistorialDocumentalSubsanacion(
            NpgsqlConnection cn,
            NpgsqlTransaction tx,
            int solicitudId,
            IEnumerable<int> codigosDocumento,
            int usuarioId,
            string usuarioRegistro)
        {
            var codigos = (codigosDocumento ?? Enumerable.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (codigos.Count == 0)
            {
                return;
            }

            const string sql = @"
                INSERT INTO aocr_tbhistorial_documental
                (
                    codigo_solicitud,
                    codigo_documento,
                    evento,
                    detalle,
                    codigo_usuario,
                    fecha_evento,
                    created_at,
                    created_by
                )
                VALUES
                (
                    @codigo_solicitud,
                    @codigo_documento,
                    @evento,
                    @detalle,
                    @codigo_usuario,
                    NOW(),
                    NOW(),
                    @created_by
                );";

            foreach (var codigoDocumento in codigos)
            {
                using (var cmd = new NpgsqlCommand(sql, cn, tx))
                {
                    cmd.Parameters.AddWithValue("@codigo_solicitud", solicitudId);
                    cmd.Parameters.AddWithValue("@codigo_documento", codigoDocumento);
                    cmd.Parameters.AddWithValue("@evento", EventoHistorialDocumentalSubsanacionEnviada);
                    cmd.Parameters.AddWithValue("@detalle", "Documento enviado nuevamente al inspector luego de subsanación RT.");
                    cmd.Parameters.AddWithValue("@codigo_usuario", usuarioId);
                    cmd.Parameters.AddWithValue("@created_by", usuarioRegistro ?? string.Empty);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private sealed class SolicitudSubsanacionData
        {
            public int CodigoSolicitud { get; set; }
            public string Estado { get; set; }
            public int? CodigoTecnico { get; set; }
        }

        private sealed class InspectorAsignadoData
        {
            public int CodigoInspeccion { get; set; }
            public int CodigoInspector { get; set; }
            public string EstadoInspeccion { get; set; }
            public string InspectorPrincipalCedula { get; set; }
            public string InspectorPrincipalNombre { get; set; }
            public string InspectorPrincipalTipo { get; set; }
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
