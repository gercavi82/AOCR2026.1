using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
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

    public sealed class AocrDcavRevisionService
    {
        private readonly AocrProcesoEstadoDAO _estadoDao;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly InspeccionInformeDAO _informeDao;
        private readonly ListaVerificacionOperacionalEaeDAO _listaDao;
        private readonly CertificadoDAO _certificadoDao;
        private readonly AocrDocumentoGeneradoDAO _documentoDao;
        private readonly AocrFirmaDocumentoDAO _firmaDao;
        private readonly AocrEstadoProcesoService _estadoProcesoService;
        private readonly AocrProcesoNotificacionService _notificacionService;

        public AocrDcavRevisionService()
            : this(new AocrProcesoEstadoDAO(), new SolicitudAOCRDAO(), new InspeccionDAO(), new InspeccionInformeDAO(), new ListaVerificacionOperacionalEaeDAO(), new CertificadoDAO(), new AocrDocumentoGeneradoDAO(), new AocrFirmaDocumentoDAO(), new AocrEstadoProcesoService(), new AocrProcesoNotificacionService())
        {
        }

        public AocrDcavRevisionService(
            AocrProcesoEstadoDAO estadoDao,
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
            _solicitudDao = solicitudDao ?? new SolicitudAOCRDAO();
            _inspeccionDao = inspeccionDao ?? new InspeccionDAO();
            _informeDao = informeDao ?? new InspeccionInformeDAO();
            _listaDao = listaDao ?? new ListaVerificacionOperacionalEaeDAO();
            _certificadoDao = certificadoDao ?? new CertificadoDAO();
            _documentoDao = documentoDao ?? new AocrDocumentoGeneradoDAO();
            _firmaDao = firmaDao ?? new AocrFirmaDocumentoDAO();
            _estadoProcesoService = estadoProcesoService ?? new AocrEstadoProcesoService();
            _notificacionService = notificacionService ?? new AocrProcesoNotificacionService();
        }

        public IList<AocrDcavRevisionItem> ListarPendientes()
        {
            return (_estadoDao.ListarActivosPorEstado(
                    AocrEstadosProceso.PendienteRevisionInformeDcav,
                    AocrEstadosProceso.PendienteRevisionDocumentosDcav,
                    AocrEstadosProceso.PendienteRevisionDcav) ?? new List<AocrProcesoEstadoRecord>())
                .Select(e => ConstruirItem(e.SolicitudId, e))
                .Where(i => i != null)
                .ToList();
        }

        public AocrDcavRevisionItem ObtenerDetalle(int solicitudId)
        {
            return ConstruirItem(solicitudId, _estadoDao.ObtenerActivoPorSolicitud(solicitudId));
        }

        public AocrDcavResultado EnviarInformeFirmadoADcav(int solicitudId, int usuarioId, string rolUsuario, string observacion)
        {
            var item = ConstruirItem(solicitudId, _estadoDao.ObtenerActivoPorSolicitud(solicitudId));
            var validacion = ValidarPrecondicionesInformeDcav(item, exigirEstadoPendiente: false);
            if (!validacion.Ok)
            {
                return validacion;
            }

            var result = _estadoProcesoService.CambiarEstado(
                solicitudId,
                AocrEstadosProceso.PendienteRevisionInformeDcav,
                "ENVIAR_INFORME_DCAV",
                usuarioId,
                rolUsuario,
                string.IsNullOrWhiteSpace(observacion) ? "Inspector firma y envia automaticamente el informe tecnico a revision DCAV." : observacion,
                inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

            if (result != null && result.Ok)
            {
                _notificacionService.NotificarInformeTecnicoPendienteRevisionDcav(solicitudId);
            }

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
            var validacion = ValidarPrecondicionesDocumentosDcav(item, exigirEstadoHabilitado: true);
            if (!validacion.Ok)
            {
                return validacion;
            }

            var result = _estadoProcesoService.CambiarEstado(
                solicitudId,
                AocrEstadosProceso.PendienteRevisionDocumentosDcav,
                "ENVIAR_DOCUMENTOS_DCAV",
                usuarioId,
                rolUsuario,
                string.IsNullOrWhiteSpace(observacion) ? "Inspector finaliza revision de AOCR y Condiciones y envia a DCAV." : observacion,
                inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

            if (result != null && result.Ok)
            {
                _notificacionService.NotificarDocumentosPendientesRevisionDcav(solicitudId);
            }

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
                return validacion;
            }

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

            var habilitado = _estadoProcesoService.CambiarEstado(
                solicitudId,
                AocrEstadosProceso.DocumentosHabilitadosInspector,
                "HABILITAR_DOCUMENTOS_INSPECTOR",
                usuarioId,
                rolUsuario,
                "AOCR y Condiciones quedan habilitados para revision del Inspector.",
                inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

            if (habilitado != null && habilitado.Ok)
            {
                _notificacionService.NotificarInformeTecnicoAprobadoDocumentosHabilitados(solicitudId);
            }

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
                return validacion;
            }

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

            var firma = _estadoProcesoService.CambiarEstado(
                solicitudId,
                AocrEstadosProceso.PendienteFirmaDirectorGeneral,
                "ENVIAR_FIRMA_DIRECTOR_GENERAL",
                usuarioId,
                rolUsuario,
                "AOCR y Condiciones quedan pendientes de firma institucional del Director General.",
                inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

            _solicitudDao.CambiarEstado(solicitudId, EstadoSolicitud.EnviadoDcav, usuarioId, "Aprobado por DCAV. Pendiente firma Director General.");
            if (firma != null && firma.Ok)
            {
                _notificacionService.NotificarFirmaDirectorGeneralPendiente(solicitudId);
            }

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
            var result = _estadoProcesoService.CambiarEstado(
                solicitudId,
                estadoDestino,
                accion,
                usuarioId,
                rolUsuario,
                texto,
                inspeccionId: item.InspeccionId > 0 ? (int?)item.InspeccionId : null,
                informeId: item.InformeId > 0 ? (int?)item.InformeId : null);

            _solicitudDao.CambiarEstado(solicitudId, EstadoSolicitud.Observada, usuarioId, texto);
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
            if (!item.AocrGenerado) return new AocrDcavResultado { Ok = false, Mensaje = "Falta AOCR generado.", Item = item };
            if (!item.CondicionesGeneradas) return new AocrDcavResultado { Ok = false, Mensaje = "Faltan Condiciones y Limitaciones generadas.", Item = item };

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
            var aocrGenerado = docAocr != null || certificado != null;
            var condicionesGeneradas = docCond != null || firmaCond != null;
            var informeFirmado = informe != null && (informe.FirmadoInspector || !string.IsNullOrWhiteSpace(informe.RutaDocumentoFirmado) || !string.IsNullOrWhiteSpace(informe.RutaPdf));
            var listaFirmada = lista != null && lista.Finalizado && lista.FirmadoTecnico;
            var estadoActual = estado != null ? estado.EstadoActual : null;
            var item = new AocrDcavRevisionItem
            {
                SolicitudId = solicitudId,
                NumeroSolicitud = solicitud.NumeroSolicitud,
                NumeroAocr = certificado != null ? certificado.NumeroCertificado : (docAocr != null ? docAocr.NumeroAocr : null),
                NombreExplotador = FirstNonEmpty(solicitud.RazonSocial, solicitud.NombreOperador, solicitud.NombreComercial),
                TipoTramite = solicitud.TipoSolicitud.HasValue ? solicitud.TipoSolicitud.Value.ToString() : FirstNonEmpty(solicitud.TipoOperacion, "AOCR"),
                InspectorResponsable = FirstNonEmpty(solicitud.TecnicoResponsableNombre, inspeccion != null ? inspeccion.InspectorPrincipalNombre : null),
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
                    ? "INFORME_TECNICO"
                    : (string.Equals(estadoActual, AocrEstadosProceso.PendienteRevisionDocumentosDcav, StringComparison.OrdinalIgnoreCase)
                        ? "DOCUMENTOS_AOCR"
                        : "GENERAL")
            };

            var validacion = string.Equals(item.TipoRevision, "INFORME_TECNICO", StringComparison.OrdinalIgnoreCase)
                ? ValidarPrecondicionesInformeDcav(item, exigirEstadoPendiente: false)
                : ValidarPrecondicionesDocumentosDcav(item, exigirEstadoHabilitado: false);
            item.PuedeAprobar = validacion.Ok;
            item.MotivoBloqueo = validacion.Ok ? null : validacion.Mensaje;
            return item;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? new string[0]).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }
    }
}
