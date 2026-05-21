using System;
using System.Collections.Generic;
using CapaDatos.DAOs;
using CapaModelo;

namespace CapaNegocio.Services
{
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
    }
}