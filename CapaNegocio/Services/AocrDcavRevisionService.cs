using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Transactions;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class AocrDcavRevisionItem
    {
        public int SolicitudId { get; set; }
        public string NumeroSolicitud { get; set; }
        public string NumeroAocr { get; set; }
        public string NombreExplotador { get; set; }
        public string TipoTramite { get; set; }
        public string InspectorResponsable { get; set; }
        public string CoordinadorResponsable { get; set; }
        public DateTime? FechaEnvioDcav { get; set; }
        public string Estado { get; set; }
        public int InformeId { get; set; }
        public int InspeccionId { get; set; }
        public bool InformeFirmado { get; set; }
        public bool ListaFirmada { get; set; }
        public bool InformeSatisfactorio { get; set; }
        public bool AocrGenerado { get; set; }
        public bool CondicionesGeneradas { get; set; }
        public bool PuedeAprobar { get; set; }
        public string MotivoBloqueo { get; set; }
        public string TipoRevision { get; set; }
    }

    public sealed class AocrDcavResultado
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public AocrDcavRevisionItem Item { get; set; }
    }

    public sealed class AocrDcavBandejaResumen
    {
        public int PendientesDcav { get; set; }
        public int InformesGeneradosSinFirma { get; set; }
        public int InformesFirmadosRecuperados { get; set; }
    }

    public sealed class AocrDcavRevisionService
    {
        private readonly AocrProcesoEstadoDAO _estadoDao;
        private readonly AocrDcavDAO _dcavDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly ListaVerificacionOperacionalEaeDAO _listaDao;
        private readonly CertificadoDAO _certificadoDao;
        private readonly AocrDocumentoGeneradoDAO _documentoDao;
        private readonly AocrFirmaDocumentoDAO _firmaDao;
        private readonly AocrEstadoProcesoService _estadoProcesoService;
        private readonly AocrProcesoNotificacionService _notificacionService;
        private readonly GeneracionAOCRService _generacionAocrService;
        private readonly GeneracionCondicionesService _generacionCondicionesService;
        private readonly ILoggingService _logger;
        // Re-evaluating Roslyn cache

        public AocrDcavRevisionService()
            : this(new AocrProcesoEstadoDAO(), new AocrDcavDAO(), new SolicitudAOCRDAO(), new InspeccionDAO(), new InspeccionInformeDAO(), new ListaVerificacionOperacionalEaeDAO(), new CertificadoDAO(), new AocrDocumentoGeneradoDAO(), new AocrFirmaDocumentoDAO(), new AocrEstadoProcesoService(), new AocrProcesoNotificacionService())
        {
        }

        public AocrDcavRevisionService(
            AocrProcesoEstadoDAO estadoDao,
            AocrDcavDAO dcavDao,
            SolicitudAOCRDAO solicitudDao,
            InspeccionDAO inspeccionDao,
            InspeccionInformeDAO informeDao,
            ListaVerificacionOperacionalEaeDAO listaDao,
            CertificadoDAO certificadoDao,
            AocrDocumentoGeneradoDAO documentoDao,
            AocrFirmaDocumentoDAO firmaDao,
            AocrEstadoProcesoService estadoProcesoService,
            AocrProcesoNotificacionService notificacionService)
        {
            _estadoDao = estadoDao ?? new AocrProcesoEstadoDAO();
            _dcavDao = dcavDao ?? new AocrDcavDAO(_estadoDao);
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _informeDao = informeDao ?? new InspeccionInformeDAO();
            _listaDao = listaDao ?? new ListaVerificacionOperacionalEaeDAO();
            _certificadoDao = certificadoDao ?? new CertificadoDAO();
            _documentoDao = documentoDao ?? new AocrDocumentoGeneradoDAO();
            _firmaDao = firmaDao ?? new AocrFirmaDocumentoDAO();
            _estadoProcesoService = estadoProcesoService ?? new AocrEstadoProcesoService();
            _notificacionService = notificacionService ?? new AocrProcesoNotificacionService();
            _generacionAocrService = new GeneracionAOCRService();
            _generacionCondicionesService = new GeneracionCondicionesService();
            _logger = LoggingServiceFactory.Create();
        }

        public IList<AocrDcavRevisionItem> ListarPendientes()
        {
            var sw = Stopwatch.StartNew();
            var informes = _dcavDao.ObtenerPendientesRevisionInforme();
            var documentos = _dcavDao.ObtenerPendientesRevisionDocumentos();
            var observados = _dcavDao.ObtenerObservados();
            var legacy = _dcavDao.ObtenerLegacyPendientesRevisionDcav();
            var records = new List<AocrProcesoEstadoRecord>();
            records.AddRange(informes);
            records.AddRange(documentos);
            records.AddRange(observados);
            records.AddRange(legacy);
            var recuperados = ObtenerInformesFirmadosPendientesPorDatos();
            records.AddRange(recuperados);

            var items = records
                .GroupBy(e => e.SolicitudId)
                .Select(g => g.OrderByDescending(e => e.FechaEstado).First())
                .Select(e => ConstruirItem(e.SolicitudId, e))
                .Where(i => i != null)
                .ToList();
            sw.Stop();
            Trace.TraceInformation("[DCAV][BANDEJA_INFORMES] Estado=" + AocrEstadosProceso.PendienteRevisionInformeDcav + "; Cantidad=" + informes.Count + "; DuracionMs=" + sw.ElapsedMilliseconds + "; Resultado=OK;");
            Trace.TraceInformation("[DCAV][BANDEJA_DOCUMENTOS] Estado=" + AocrEstadosProceso.PendienteRevisionDocumentosDcav + "; Cantidad=" + documentos.Count + "; Observados=" + observados.Count + "; Legacy=" + legacy.Count + "; Total=" + items.Count + "; DuracionMs=" + sw.ElapsedMilliseconds + "; Resultado=OK;");
            _logger.LogInfo("[DCAV][BANDEJA] InformesEstado=" + informes.Count
                + "; DocumentosEstado=" + documentos.Count
                + "; Observados=" + observados.Count
                + "; Legacy=" + legacy.Count
                + "; InformesRecuperados=" + recuperados.Count
                + "; TotalVisible=" + items.Count
                + "; DuracionMs=" + sw.ElapsedMilliseconds + ";");
            return items;
        }

        public AocrDcavBandejaResumen ObtenerResumenBandeja()
        {
            var informes = (_informeDao.ListarTodos() ?? new List<InspeccionInformeTecnico>())
                .Where(i => i != null && i.CodigoInspeccion > 0)
                .GroupBy(i => i.CodigoInspeccion)
                .Select(g => g.OrderByDescending(i => i.Version).ThenByDescending(i => i.CodigoInforme).First())
                .ToList();

            var pendientes = new List<AocrProcesoEstadoRecord>();
            pendientes.AddRange(_dcavDao.ObtenerPendientesRevisionInforme());
            pendientes.AddRange(_dcavDao.ObtenerPendientesRevisionDocumentos());
            pendientes.AddRange(_dcavDao.ObtenerObservados());
            pendientes.AddRange(ObtenerInformesFirmadosPendientesPorDatos());

            return new AocrDcavBandejaResumen
            {
                PendientesDcav = pendientes.Select(e => e.SolicitudId).Distinct().Count(),
                InformesGeneradosSinFirma = informes.Count(i => i.Finalizado && !i.FirmadoInspector),
                InformesFirmadosRecuperados = ObtenerInformesFirmadosPendientesPorDatos().Count
            };
        }

        public AocrDcavRevisionItem ObtenerDetalle(int solicitudId)
        {
            var item = ConstruirItem(solicitudId, _estadoDao.ObtenerActivoPorSolicitud(solicitudId));
            if (item != null && !EsEstadoVisibleDcav(item.Estado))
            {
                var recuperado = ObtenerInformesFirmadosPendientesPorDatos()
                    .FirstOrDefault(e => e != null && e.SolicitudId == solicitudId);
                if (recuperado != null)
                {
                    item = ConstruirItem(solicitudId, recuperado);
                }
            }

            return item;
        }

        public AocrDcavResultado EnviarInformeFirmadoADcav(int solicitudId, int usuarioId, string rolUsuario, string observacion)
        {
            var item = ConstruirItem(solicitudId, _estadoDao.ObtenerActivoPorSolicitud(solicitudId));
            Trace.TraceInformation("[DCAV_TRANSICION][INFORME_ENVIO_IN] SolicitudId=" + solicitudId + "; Usuario=" + usuarioId + "; Rol=" + (rolUsuario ?? string.Empty) + "; EstadoActual=" + (item != null ? item.Estado : string.Empty) + ";");
            var validacion = ValidarPrecondicionesInformeDcav(item, exigirEstadoPendiente: false);
            if (!validacion.Ok)
            {
                Trace.TraceWarning("[DCAV_TRANSICION][INFORME_ENVIO_DENY] SolicitudId=" + solicitudId + "; Motivo=" + (validacion.Mensaje ?? string.Empty) + ";");
                return validacion;
            }

            AocrEstadoProcesoResult result;
            using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(2)
            }))
            {
                var actual = _estadoDao.ObtenerActivoPorSolicitud(solicitudId);
                if (actual == null || (!string.Equals(actual.EstadoActual, AocrEstadosProceso.InformeTecnicoFirmadoInspector, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(actual.EstadoActual, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase)))
                {
                    var sincronizado = _estadoProcesoService.SincronizarDesdeFuentesActuales(
                        solicitudId,
                        "FIRMAR_INFORME_TECNICO",
                        usuarioId,
                        rolUsuario,
                        "Informe tecnico firmado por Inspector.",
                        inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                        informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                    if (sincronizado == null || !sincronizado.Ok
                        || sincronizado.EstadoActual == null
                        || !string.Equals(sincronizado.EstadoActual.EstadoActual, AocrEstadosProceso.InformeTecnicoFirmadoInspector, StringComparison.OrdinalIgnoreCase))
                    {
                        return new AocrDcavResultado
                        {
                            Ok = false,
                            Mensaje = sincronizado != null ? sincronizado.Motivo : "No se pudo registrar el estado de firma del Inspector.",
                            Item = item
                        };
                    }
                }

                actual = _estadoDao.ObtenerActivoPorSolicitud(solicitudId);
                if (actual != null && string.Equals(actual.EstadoActual, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase))
                {
                    scope.Complete();
                    return new AocrDcavResultado { Ok = true, Mensaje = "El informe ya se encuentra pendiente de revision DCAV.", Item = ObtenerDetalle(solicitudId) };
                }

                result = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.PendienteRevisionInformeDcav,
                    "ENVIAR_INFORME_DCAV",
                    usuarioId,
                    rolUsuario,
                    string.IsNullOrWhiteSpace(observacion) ? "Inspector firma y envia automaticamente el informe tecnico a revision DCAV." : observacion,
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (result == null || !result.Ok)
                {
                    return new AocrDcavResultado
                    {
                        Ok = false,
                        Mensaje = result != null ? result.Motivo : "No se pudo enviar el informe a revision DCAV.",
                        Item = item
                    };
                }

                _notificacionService.NotificarInformeTecnicoPendienteRevisionDcav(solicitudId);
                scope.Complete();
            }
            Trace.TraceInformation("[DCAV_TRANSICION][INFORME_ENVIO_OUT] SolicitudId=" + solicitudId + "; EstadoNuevo=" + AocrEstadosProceso.PendienteRevisionInformeDcav + "; Ok=" + (result != null && result.Ok) + "; Motivo=" + (result != null ? result.Motivo : string.Empty) + ";");
            _logger.LogInfo("[DCAV_TRANSICION][INFORME_ENVIO_OK] SolicitudId=" + solicitudId
                + "; InspeccionId=" + item.InspeccionId
                + "; InformeId=" + item.InformeId
                + "; EstadoNuevo=" + AocrEstadosProceso.PendienteRevisionInformeDcav + ";");

            return new AocrDcavResultado
            {
                Ok = result != null && result.Ok,
                Mensaje = result != null && result.Ok ? "Informe tecnico enviado a revision DCAV." : (result != null ? result.Motivo : "No se pudo enviar el informe a revision DCAV."),
                Item = ObtenerDetalle(solicitudId)
            };
        }

        public AocrDcavResultado EnviarRevisionDcav(int solicitudId, int usuarioId, string rolUsuario, string observacion)
        {
            var item = ConstruirItem(solicitudId, _estadoDao.ObtenerActivoPorSolicitud(solicitudId));
            _logger.LogInfo("[INSPECTOR][FINALIZAR_DOCUMENTOS_IN] SolicitudId=" + solicitudId
                + "; InspeccionId=" + (item != null ? item.InspeccionId : 0)
                + "; Usuario=" + usuarioId + "; Rol=" + (rolUsuario ?? string.Empty)
                + "; EstadoAnterior=" + (item != null ? item.Estado : string.Empty) + ";");
            _logger.LogInfo("[INSPECTOR][VALIDAR_AOCR] SolicitudId=" + solicitudId + "; Generado=" + (item != null && item.AocrGenerado) + ";");
            _logger.LogInfo("[INSPECTOR][VALIDAR_CONDICIONES] SolicitudId=" + solicitudId + "; Generado=" + (item != null && item.CondicionesGeneradas) + ";");
            Trace.TraceInformation("[DCAV_TRANSICION][DOCUMENTOS_ENVIO_IN] SolicitudId=" + solicitudId + "; Usuario=" + usuarioId + "; Rol=" + (rolUsuario ?? string.Empty) + "; EstadoActual=" + (item != null ? item.Estado : string.Empty) + ";");
            var validacion = ValidarPrecondicionesDocumentosDcav(item, exigirEstadoHabilitado: true);
            if (!validacion.Ok)
            {
                _logger.LogWarning("[INSPECTOR][ENVIAR_DOCUMENTOS_DCAV_ERROR] SolicitudId=" + solicitudId + "; Motivo=" + (validacion.Mensaje ?? string.Empty) + ";");
                Trace.TraceWarning("[DCAV_TRANSICION][DOCUMENTOS_ENVIO_DENY] SolicitudId=" + solicitudId + "; Motivo=" + (validacion.Mensaje ?? string.Empty) + ";");
                return validacion;
            }

            AocrEstadoProcesoResult result;
            using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(2)
            }))
            {
                result = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.PendienteRevisionDocumentosDcav,
                    "ENVIAR_DOCUMENTOS_DCAV",
                    usuarioId,
                    rolUsuario,
                    string.IsNullOrWhiteSpace(observacion) ? "Inspector finaliza revision de AOCR y Condiciones y envia a DCAV." : observacion,
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (result == null || !result.Ok)
                {
                    return new AocrDcavResultado
                    {
                        Ok = false,
                        Mensaje = result != null ? result.Motivo : "No se pudo enviar a revision DCAV.",
                        Item = item
                    };
                }

                _notificacionService.NotificarDocumentosPendientesRevisionDcav(solicitudId);
                scope.Complete();
            }
            Trace.TraceInformation("[DCAV_TRANSICION][DOCUMENTOS_ENVIO_OUT] SolicitudId=" + solicitudId + "; EstadoNuevo=" + AocrEstadosProceso.PendienteRevisionDocumentosDcav + "; Ok=" + (result != null && result.Ok) + "; Motivo=" + (result != null ? result.Motivo : string.Empty) + ";");
            _logger.LogInfo("[INSPECTOR][ENVIAR_DOCUMENTOS_DCAV_OK] SolicitudId=" + solicitudId
                + "; InspeccionId=" + item.InspeccionId
                + "; EstadoAnterior=" + item.Estado
                + "; EstadoNuevo=" + AocrEstadosProceso.PendienteRevisionDocumentosDcav + ";");
            _logger.LogInfo("[WORKFLOW][DOCUMENTOS_TRANSICION] SolicitudId=" + solicitudId
                + "; EstadoAnterior=" + item.Estado
                + "; EstadoNuevo=" + AocrEstadosProceso.PendienteRevisionDocumentosDcav
                + "; Resultado=OK;");

            return new AocrDcavResultado
            {
                Ok = result != null && result.Ok,
                Mensaje = result != null && result.Ok ? "AOCR y Condiciones enviados a revision DCAV." : (result != null ? result.Motivo : "No se pudo enviar a revision DCAV."),
                Item = ObtenerDetalle(solicitudId)
            };
        }

        public AocrDcavResultado AprobarEnviarDirectorGeneral(int solicitudId, int usuarioId, string rolUsuario, string observacion)
        {
            var item = ObtenerDetalle(solicitudId);
            Trace.TraceInformation("[DCAV_TRANSICION][APROBAR_IN] SolicitudId=" + solicitudId + "; Usuario=" + usuarioId + "; Rol=" + (rolUsuario ?? string.Empty) + "; EstadoActual=" + (item != null ? item.Estado : string.Empty) + "; TipoRevision=" + (item != null ? item.TipoRevision : string.Empty) + ";");
            if (item != null && string.Equals(item.Estado, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase))
            {
                return AprobarInformeTecnico(solicitudId, item, usuarioId, rolUsuario, observacion);
            }

            if (item != null && string.Equals(item.Estado, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase))
            {
                return AprobarDocumentosEnviarDirectorGeneral(solicitudId, item, usuarioId, rolUsuario, observacion);
            }

            return new AocrDcavResultado { Ok = false, Mensaje = "El expediente no se encuentra en una etapa de revision DCAV.", Item = item };
        }

        private AocrDcavResultado AprobarInformeTecnico(int solicitudId, AocrDcavRevisionItem item, int usuarioId, string rolUsuario, string observacion)
        {
            var validacion = ValidarPrecondicionesInformeDcav(item, exigirEstadoPendiente: true);
            if (!validacion.Ok)
            {
                Trace.TraceWarning("[DCAV_TRANSICION][APROBAR_INFORME_DENY] SolicitudId=" + solicitudId + "; Motivo=" + (validacion.Mensaje ?? string.Empty) + ";");
                return validacion;
            }

            AocrEstadoProcesoResult habilitado;
            using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(2)
            }))
            {
                var aprobacion = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.InformeTecnicoAprobadoDcav,
                    "DCAV_APROBAR_INFORME",
                    usuarioId,
                    rolUsuario,
                    string.IsNullOrWhiteSpace(observacion) ? "Informe tecnico aprobado por DCAV." : observacion,
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (aprobacion == null || !aprobacion.Ok)
                {
                    return new AocrDcavResultado { Ok = false, Mensaje = aprobacion != null ? aprobacion.Motivo : "No se pudo aprobar el informe en DCAV.", Item = item };
                }

                if (item.InformeId > 0)
                {
                    _informeDao.ActualizarEstadoInforme(item.InformeId, AocrEstadosProceso.InformeTecnicoAprobadoDcav, usuarioId);
                }

                habilitado = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.DocumentosHabilitadosInspector,
                    "HABILITAR_DOCUMENTOS_INSPECTOR",
                    usuarioId,
                    rolUsuario,
                    "AOCR y Condiciones quedan habilitados para revision del Inspector.",
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (habilitado == null || !habilitado.Ok)
                {
                    return new AocrDcavResultado { Ok = false, Mensaje = habilitado != null ? habilitado.Motivo : "No se pudo habilitar documentos para el Inspector.", Item = item };
                }

                // Generar borradores físicos para que el Inspector pueda verlos
                var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
                var inspectorId = 0;
                if (item.InspeccionId > 0)
                {
                    var inspeccion = _inspeccionDao.ObtenerPorId(item.InspeccionId);
                    if (inspeccion != null && inspeccion.CodigoInspector.HasValue)
                    {
                        inspectorId = inspeccion.CodigoInspector.Value;
                    }
                }

                var resAocr = _generacionAocrService.ObtenerOCrearBorradorAocr(solicitudId, inspectorId, usuarioId);
                if (!resAocr.Ok)
                {
                    Trace.TraceWarning("[DCAV_TRANSICION][GENERAR_AOCR_FAIL] SolicitudId=" + solicitudId + "; Error=" + resAocr.Mensaje);
                }

                var resCond = _generacionCondicionesService.ObtenerOCrearBorradorCondiciones(solicitudId, inspectorId, usuarioId);
                if (!resCond.Ok)
                {
                    Trace.TraceWarning("[DCAV_TRANSICION][GENERAR_CONDICIONES_FAIL] SolicitudId=" + solicitudId + "; Error=" + resCond.Mensaje);
                }

                _notificacionService.NotificarInformeTecnicoAprobadoDocumentosHabilitados(solicitudId);
                scope.Complete();
            }
            Trace.TraceInformation("[DCAV_TRANSICION][APROBAR_INFORME_OUT] SolicitudId=" + solicitudId + "; EstadoNuevo=" + AocrEstadosProceso.DocumentosHabilitadosInspector + "; Ok=" + (habilitado != null && habilitado.Ok) + "; Motivo=" + (habilitado != null ? habilitado.Motivo : string.Empty) + ";");

            return new AocrDcavResultado
            {
                Ok = habilitado != null && habilitado.Ok,
                Mensaje = habilitado != null && habilitado.Ok ? "Informe aprobado por DCAV. AOCR y Condiciones habilitados para el Inspector." : (habilitado != null ? habilitado.Motivo : "No se pudo habilitar documentos para el Inspector."),
                Item = ObtenerDetalle(solicitudId)
            };
        }

        private AocrDcavResultado AprobarDocumentosEnviarDirectorGeneral(int solicitudId, AocrDcavRevisionItem item, int usuarioId, string rolUsuario, string observacion)
        {
            var validacion = ValidarPrecondicionesDocumentosDcav(item, exigirEstadoHabilitado: false);
            if (!validacion.Ok)
            {
                Trace.TraceWarning("[DCAV_TRANSICION][APROBAR_DOCUMENTOS_DENY] SolicitudId=" + solicitudId + "; Motivo=" + (validacion.Mensaje ?? string.Empty) + ";");
                return validacion;
            }

            AocrEstadoProcesoResult firma;
            using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(2)
            }))
            {
                var aprobacion = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.AprobadoDocumentosDcav,
                    "DCAV_APROBAR_DOCUMENTOS",
                    usuarioId,
                    rolUsuario,
                    string.IsNullOrWhiteSpace(observacion) ? "AOCR y Condiciones aprobados por DCAV." : observacion,
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (aprobacion == null || !aprobacion.Ok)
                {
                    return new AocrDcavResultado { Ok = false, Mensaje = aprobacion != null ? aprobacion.Motivo : "No se pudo aprobar documentos en DCAV.", Item = item };
                }

                firma = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    AocrEstadosProceso.PendienteFirmaDirectorGeneral,
                    "ENVIAR_FIRMA_DIRECTOR_GENERAL",
                    usuarioId,
                    rolUsuario,
                    "AOCR y Condiciones quedan pendientes de firma institucional del Director General.",
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (firma == null || !firma.Ok)
                {
                    return new AocrDcavResultado { Ok = false, Mensaje = firma != null ? firma.Motivo : "No se pudo enviar al Director General.", Item = item };
                }

                _solicitudDao.CambiarEstado(solicitudId, EstadoSolicitud.EnviadoDcav, usuarioId, "Aprobado por DCAV. Pendiente firma Director General.");
                _notificacionService.NotificarFirmaDirectorGeneralPendiente(solicitudId);
                scope.Complete();
            }
            Trace.TraceInformation("[DCAV_TRANSICION][APROBAR_DOCUMENTOS_OUT] SolicitudId=" + solicitudId + "; EstadoNuevo=" + AocrEstadosProceso.PendienteFirmaDirectorGeneral + "; Ok=" + (firma != null && firma.Ok) + "; Motivo=" + (firma != null ? firma.Motivo : string.Empty) + ";");
            _logger.LogInfo("[DCAV][APROBAR_DOCUMENTOS] SolicitudId=" + solicitudId
                + "; InspeccionId=" + item.InspeccionId
                + "; EstadoAnterior=" + item.Estado
                + "; EstadoNuevo=" + AocrEstadosProceso.PendienteFirmaDirectorGeneral
                + "; Resultado=OK;");

            return new AocrDcavResultado
            {
                Ok = firma != null && firma.Ok,
                Mensaje = firma != null && firma.Ok ? "Documentos aprobados por DCAV y enviados al Director General." : (firma != null ? firma.Motivo : "No se pudo enviar al Director General."),
                Item = ObtenerDetalle(solicitudId)
            };
        }

        public AocrDcavResultado DevolverConObservaciones(int solicitudId, string destino, string observacion, int usuarioId, string rolUsuario)
        {
            if (string.IsNullOrWhiteSpace(observacion))
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "La observacion es obligatoria." };
            }

            var item = ObtenerDetalle(solicitudId);
            if (item == null)
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "No existe expediente AOCR para revisar." };
            }

            string estadoDestino;
            string accion;
            string destinoNormalizado = "INSPECTOR";
            if (string.Equals(item.Estado, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase))
            {
                estadoDestino = AocrEstadosProceso.InformeTecnicoObservadoDcav;
                accion = "DCAV_DEVOLVER_INFORME";
            }
            else if (string.Equals(item.Estado, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase))
            {
                estadoDestino = AocrEstadosProceso.DocumentosObservadosDcav;
                accion = "DCAV_DEVOLVER_DOCUMENTOS";
            }
            else
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "El expediente no se encuentra pendiente de revision DCAV.", Item = item };
            }

            var texto = "Devuelto por DCAV al " + destinoNormalizado + ". Motivo: " + observacion.Trim();
            AocrEstadoProcesoResult result;
            using (var scope = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromMinutes(2)
            }))
            {
                result = _estadoProcesoService.CambiarEstado(
                    solicitudId,
                    estadoDestino,
                    accion,
                    usuarioId,
                    rolUsuario,
                    texto,
                    inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                    informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

                if (result == null || !result.Ok)
                {
                    return new AocrDcavResultado { Ok = false, Mensaje = result != null ? result.Motivo : "No se pudo devolver el expediente.", Item = item };
                }

                if (item.InformeId > 0 && string.Equals(estadoDestino, AocrEstadosProceso.InformeTecnicoObservadoDcav, StringComparison.OrdinalIgnoreCase))
                {
                    _informeDao.ActualizarEstadoInforme(item.InformeId, AocrEstadosProceso.InformeTecnicoObservadoDcav, usuarioId);
                }

                _solicitudDao.CambiarEstado(solicitudId, EstadoSolicitud.Observada, usuarioId, texto);
                scope.Complete();
            }
            _logger.LogInfo("[DCAV][DEVOLVER_DOCUMENTOS] SolicitudId=" + solicitudId
                + "; InspeccionId=" + item.InspeccionId
                + "; EstadoAnterior=" + item.Estado
                + "; EstadoNuevo=" + estadoDestino
                + "; Resultado=OK;");
            return new AocrDcavResultado
            {
                Ok = result != null && result.Ok,
                Mensaje = result != null && result.Ok ? "Expediente devuelto con observaciones." : (result != null ? result.Motivo : "No se pudo devolver el expediente."),
                Item = ObtenerDetalle(solicitudId)
            };
        }

        public static bool EsInformeSatisfactorio(InspeccionInformeTecnico informe)
        {
            var resultado = (informe != null ? informe.Resultado : null) ?? string.Empty;
            var token = resultado.Trim().ToUpperInvariant();
            return token.Contains("SATISFACTORIO") && !token.Contains("INSATISFACTORIO");
        }

        public static bool EsInformeFirmadoValido(InspeccionInformeTecnico informe)
        {
            return informe != null
                && informe.Finalizado
                && informe.FirmadoInspector
                && !string.IsNullOrWhiteSpace(informe.RutaDocumentoFirmado)
                && !string.IsNullOrWhiteSpace(informe.HashDocumento)
                && informe.FechaFirma1.HasValue;
        }

        private AocrDcavResultado ValidarPrecondicionesInformeDcav(AocrDcavRevisionItem item, bool exigirEstadoPendiente)
        {
            if (item == null || item.SolicitudId <= 0)
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "No existe expediente AOCR para revisar." };
            }

            if (exigirEstadoPendiente && !string.Equals(item.Estado, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase))
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "El informe no se encuentra pendiente de revision DCAV.", Item = item };
            }

            if (!item.ListaFirmada) return new AocrDcavResultado { Ok = false, Mensaje = "Falta Lista de Verificacion finalizada y firmada.", Item = item };
            if (!item.InformeFirmado) return new AocrDcavResultado { Ok = false, Mensaje = "Falta Informe Tecnico firmado.", Item = item };
            if (!item.InformeSatisfactorio) return new AocrDcavResultado { Ok = false, Mensaje = "El Informe Tecnico no tiene resultado satisfactorio.", Item = item };

            return new AocrDcavResultado { Ok = true, Mensaje = "Precondiciones de informe para DCAV completas.", Item = item };
        }

        private AocrDcavResultado ValidarPrecondicionesDocumentosDcav(AocrDcavRevisionItem item, bool exigirEstadoHabilitado)
        {
            if (item == null || item.SolicitudId <= 0)
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "No existe expediente AOCR para revisar." };
            }

            if (exigirEstadoHabilitado
                && !string.Equals(item.Estado, AocrEstadosProceso.DocumentosHabilitadosInspector, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Estado, AocrEstadosProceso.DocumentosEnRevisionInspector, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Estado, AocrEstadosProceso.DocumentosObservadosDcav, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Estado, AocrEstadosProceso.InformeTecnicoAprobadoDcav, StringComparison.OrdinalIgnoreCase))
            {
                return new AocrDcavResultado { Ok = false, Mensaje = "AOCR y Condiciones no estan habilitados para envio a DCAV.", Item = item };
            }

            if (!item.ListaFirmada) return new AocrDcavResultado { Ok = false, Mensaje = "Falta Lista de Verificacion finalizada y firmada.", Item = item };
            if (!item.InformeFirmado) return new AocrDcavResultado { Ok = false, Mensaje = "Falta Informe Tecnico firmado.", Item = item };
            if (!item.InformeSatisfactorio) return new AocrDcavResultado { Ok = false, Mensaje = "El Informe Tecnico no tiene resultado satisfactorio.", Item = item };
            if (!item.AocrGenerado) return new AocrDcavResultado { Ok = false, Mensaje = "No se puede enviar el expediente al DCAV porque el AOCR todavía no ha sido generado.", Item = item };
            if (!item.CondicionesGeneradas) return new AocrDcavResultado { Ok = false, Mensaje = "No se puede enviar el expediente al DCAV porque las Condiciones y Limitaciones todavía no han sido generadas.", Item = item };

            return new AocrDcavResultado { Ok = true, Mensaje = "Precondiciones de documentos para DCAV completas.", Item = item };
        }

        private AocrDcavRevisionItem ConstruirItem(int solicitudId, AocrProcesoEstadoRecord estado)
        {
            var solicitud = _solicitudDao.ObtenerPorId(solicitudId);
            if (solicitud == null)
            {
                return null;
            }

            var inspeccion = (_inspeccionDao.ListarPorSolicitud(solicitudId) ?? new List<Inspeccion>())
                .OrderByDescending(i => i.FechaProgramada)
                .ThenByDescending(i => i.CodigoInspeccion)
                .FirstOrDefault();
            var informe = inspeccion != null ? _informeDao.ObtenerUltimoPorInspeccion(inspeccion.CodigoInspeccion) : null;
            var lista = inspeccion != null ? _listaDao.ObtenerUltimaPorInspeccion(inspeccion.CodigoInspeccion) : null;
            var certificado = _certificadoDao.ObtenerPorSolicitud(solicitudId);
            var docAocr = _documentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var docCond = _documentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES")
                ?? _documentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES");
            var firmaAocr = _firmaDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "RECONOCIMIENTO");
            var firmaCond = _firmaDao.ObtenerUltimoPorSolicitudTipo(solicitudId, "CONDICIONES_LIMITACIONES");
            var aocrGenerado = DocumentoGeneradoValido(docAocr)
                || (certificado != null && !string.IsNullOrWhiteSpace(certificado.RutaDocumento));
            var condicionesGeneradas = DocumentoGeneradoValido(docCond);
            var informeFirmado = EsInformeFirmadoValido(informe);
            var listaFirmada = lista != null
                && lista.Finalizado
                && lista.FirmadoTecnico
                && !string.IsNullOrWhiteSpace(lista.RutaDocumentoFirmado)
                && !string.IsNullOrWhiteSpace(lista.HashDocumento)
                && lista.FechaFirma.HasValue;
            var estadoActual = estado != null ? estado.EstadoActual : null;
            var item = new AocrDcavRevisionItem
            {
                SolicitudId = solicitudId,
                NumeroSolicitud = solicitud.NumeroSolicitud,
                NumeroAocr = certificado != null ? certificado.NumeroCertificado : (docAocr != null ? docAocr.NumeroAocr : null),
                NombreExplotador = FirstNonEmpty(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial),
                TipoTramite = solicitud.TipoSolicitud.HasValue ? solicitud.TipoSolicitud.Value.ToString() : FirstNonEmpty(solicitud.TipoOperacion, "AOCR"),
                InspectorResponsable = ResolverInspectorResponsable(solicitud, inspeccion, informe),
                CoordinadorResponsable = "Coordinacion",
                FechaEnvioDcav = estado != null ? (DateTime?)estado.FechaEstado : null,
                Estado = estadoActual,
                InformeId = informe != null ? informe.CodigoInforme : 0,
                InspeccionId = inspeccion != null ? inspeccion.CodigoInspeccion : 0,
                InformeFirmado = informeFirmado,
                ListaFirmada = listaFirmada,
                InformeSatisfactorio = EsInformeSatisfactorio(informe),
                AocrGenerado = aocrGenerado && firmaAocr == null,
                CondicionesGeneradas = condicionesGeneradas && firmaCond == null,
                TipoRevision = string.Equals(estadoActual, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(estadoActual, AocrEstadosProceso.InformeTecnicoObservadoDcav, StringComparison.OrdinalIgnoreCase)
                    ? "INFORME_TECNICO"
                    : (string.Equals(estadoActual, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(estadoActual, AocrEstadosProceso.DocumentosObservadosDcav, StringComparison.OrdinalIgnoreCase)
                        ? "DOCUMENTOS_AOCR"
                        : "GENERAL")
            };

            if (string.Equals(estadoActual, AocrEstadosProceso.InformeTecnicoObservadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoActual, AocrEstadosProceso.DocumentosObservadosDcav, StringComparison.OrdinalIgnoreCase))
            {
                item.PuedeAprobar = false;
                item.MotivoBloqueo = "Expediente devuelto al Inspector para correccion.";
            }
            else
            {
                var validacion = string.Equals(item.TipoRevision, "INFORME_TECNICO", StringComparison.OrdinalIgnoreCase)
                    ? ValidarPrecondicionesInformeDcav(item, exigirEstadoPendiente: false)
                    : ValidarPrecondicionesDocumentosDcav(item, exigirEstadoHabilitado: false);
                item.PuedeAprobar = validacion.Ok;
                item.MotivoBloqueo = validacion.Ok ? null : validacion.Mensaje;
            }
            return item;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? new string[0]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private IList<AocrProcesoEstadoRecord> ObtenerInformesFirmadosPendientesPorDatos()
        {
            var records = new List<AocrProcesoEstadoRecord>();
            var informes = _informeDao.ListarPendientesFirmaDirdac() ?? new List<InspeccionInformeTecnico>();
            foreach (var informe in informes)
            {
                if (informe == null || informe.CodigoInspeccion <= 0 || !informe.Finalizado || !informe.FirmadoInspector || informe.FirmadoDirdac)
                {
                    continue;
                }

                var inspeccion = _inspeccionDao.ObtenerPorId(informe.CodigoInspeccion);
                if (inspeccion == null || inspeccion.CodigoSolicitud <= 0)
                {
                    continue;
                }

                var actual = _estadoDao.ObtenerActivoPorSolicitud(inspeccion.CodigoSolicitud);
                if (actual != null && EsEstadoPosteriorRevisionInformeDcav(actual.EstadoActual))
                {
                    continue;
                }

                records.Add(new AocrProcesoEstadoRecord
                {
                    SolicitudId = inspeccion.CodigoSolicitud,
                    InspeccionId = inspeccion.CodigoInspeccion,
                    InformeId = informe.CodigoInforme,
                    EstadoActual = AocrEstadosProceso.PendienteRevisionInformeDcav,
                    EtapaActual = "REVISION_INFORME_DCAV",
                    RolResponsable = "DirectorCertificacionesDcav",
                    SiguienteAccion = "DCAV_REVISAR_INFORME",
                    Observacion = "Informe tecnico firmado por Inspector pendiente de revision DCAV.",
                    FechaEstado = informe.FechaEnvioDirdac
                        ?? informe.FechaFirma1
                        ?? informe.FechaFinalizacion
                        ?? informe.UpdatedAt
                        ?? informe.CreatedAt
                        ?? DateTime.Now,
                    Activo = true
                });
            }

            return records;
        }

        private static bool EsEstadoVisibleDcav(string estado)
        {
            return string.Equals(estado, AocrEstadosProceso.PendienteRevisionInformeDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.InformeTecnicoObservadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.DocumentosObservadosDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.PendienteRevisionDcav, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsEstadoPosteriorRevisionInformeDcav(string estado)
        {
            return string.Equals(estado, AocrEstadosProceso.InformeTecnicoAprobadoDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.DocumentosHabilitadosInspector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.DocumentosEnRevisionInspector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.AprobadoDocumentosDcav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.PendienteFirmaDirectorGeneral, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.PendienteFirmaDirectorGeneralLegacy, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.AocrFirmadoDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.CondicionesFirmadasDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.DocumentosFirmadosDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.DocumentosFinalesLiberadosRt, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.AocrFinalizado, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, AocrEstadosProceso.AocrAnulado, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolverInspectorResponsable(SolicitudAOCR solicitud, Inspeccion inspeccion, InspeccionInformeTecnico informe)
        {
            var nombre = FirstNonEmpty(
                informe != null ? informe.UsuarioFirma1 : null,
                inspeccion != null ? inspeccion.InspectorPrincipalNombre : null,
                solicitud != null ? solicitud.TecnicoResponsableNombre : null);

            var identificador = FirstNonEmpty(
                inspeccion != null ? inspeccion.InspectorPrincipalCedula : null,
                solicitud != null ? solicitud.TecnicoResponsableCedula : null,
                inspeccion != null && inspeccion.CodigoInspector.HasValue && inspeccion.CodigoInspector.Value > 0
                    ? inspeccion.CodigoInspector.Value.ToString()
                    : null,
                solicitud != null && solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0
                    ? solicitud.CodigoTecnico.Value.ToString()
                    : null);

            if (!string.IsNullOrWhiteSpace(nombre) && !string.IsNullOrWhiteSpace(identificador) && nombre.IndexOf(identificador, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return nombre.Trim() + " - " + identificador.Trim();
            }

            return FirstNonEmpty(nombre, identificador, "No registrado");
        }

        private static bool DocumentoGeneradoValido(AocrDocumentoGenerado documento)
        {
            if (documento == null)
            {
                return false;
            }

            var estado = (documento.Estado ?? string.Empty).Trim().ToUpperInvariant();
            return !string.Equals(estado, "BORRADOR", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(documento.RutaDocumento);
        }
    }
}
