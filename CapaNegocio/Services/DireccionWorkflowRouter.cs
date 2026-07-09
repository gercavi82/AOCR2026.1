using System;
using System.Diagnostics;
using CapaDatos.Constants;
using CapaDatos.DAOs;

namespace CapaNegocio.Services
{
    public enum DireccionWorkflowAccion
    {
        RevisionDireccion,
        ValidarAocr,
        FirmarAocr,
        FirmarCondiciones,
        DocumentosFirmados,
        SinAccionPendiente
    }

    public sealed class DireccionWorkflowSiguiente
    {
        public DireccionWorkflowAccion Accion { get; set; }
        public AocrContextoResolucion Contexto { get; set; }
        public string Motivo { get; set; }
    }

    public sealed class DireccionWorkflowRouter
    {
        private readonly AocrContextResolverService _resolver;
        private readonly AocrEstadoService _estadoService;
        private readonly InformeTecnicoEstadoService _informeEstadoService;
        private readonly AocrProcesoEstadoDAO _procesoEstadoDao;

        public DireccionWorkflowRouter()
            : this(new AocrContextResolverService(), new AocrEstadoService(), new InformeTecnicoEstadoService(), new AocrProcesoEstadoDAO())
        {
        }

        public DireccionWorkflowRouter(AocrContextResolverService resolver, AocrEstadoService estadoService, InformeTecnicoEstadoService informeEstadoService, AocrProcesoEstadoDAO procesoEstadoDao)
        {
            _resolver = resolver ?? new AocrContextResolverService();
            _estadoService = estadoService ?? new AocrEstadoService();
            _informeEstadoService = informeEstadoService ?? new InformeTecnicoEstadoService();
            _procesoEstadoDao = procesoEstadoDao ?? new AocrProcesoEstadoDAO();
        }

        public DireccionWorkflowSiguiente ObtenerAccionSiguiente(int solicitudId)
        {
            var contexto = _resolver.ResolverDesdeSolicitudId(solicitudId);
            if (!contexto.Ok)
            {
                return CrearResultado(DireccionWorkflowAccion.SinAccionPendiente, contexto, contexto.Mensaje);
            }

            var estadoInforme = Normalizar(contexto.InformeTecnico != null ? contexto.InformeTecnico.EstadoInforme : null);
            var estadoSolicitud = contexto.EstadoSolicitud;
            var estadoSolicitudToken = Normalizar(estadoSolicitud);
            var estadoAocr = Normalizar(contexto.EstadoAocr);
            var estadoCentral = _procesoEstadoDao.ObtenerActivoPorSolicitud(solicitudId);

            if (estadoCentral != null && string.Equals(estadoCentral.EstadoActual, AocrEstadosProceso.PendienteFirmaDirectorGeneral, StringComparison.OrdinalIgnoreCase))
            {
                return CrearResultado(DireccionWorkflowAccion.FirmarAocr, contexto, "AOCR y Condiciones pendientes de firma institucional del Director General.");
            }

            if (InformeTecnicoEstadosInstitucionales.PuedeRevisarDireccion(estadoInforme))
            {
                return CrearResultado(DireccionWorkflowAccion.RevisionDireccion, contexto, "Informe tecnico pendiente de decision institucional.");
            }

            if (estadoSolicitudToken == "AOCR_PENDIENTE_FIRMA_DIRECCION"
                || estadoAocr == "AOCR_PENDIENTE_FIRMA_DIRECCION")
            {
                return CrearResultado(DireccionWorkflowAccion.FirmarAocr, contexto, "AOCR pendiente de firma institucional.");
            }

            if (estadoSolicitudToken == "PENDIENTE_FIRMA_DIRECCION_CONDICIONES"
                || estadoSolicitudToken == "CONDICIONES_PENDIENTE_FIRMA_DIRECCION"
                || estadoAocr == "PENDIENTE_FIRMA_DIRECCION")
            {
                return CrearResultado(DireccionWorkflowAccion.FirmarCondiciones, contexto, "Condiciones y Limitaciones pendientes de firma institucional.");
            }

            if (_estadoService.PuedeDireccionValidarAocr(contexto.EstadoSolicitud, contexto.EstadoAocr)
                || _informeEstadoService.EstaAprobadoPorDireccion(contexto.InformeTecnico))
            {
                return CrearResultado(DireccionWorkflowAccion.ValidarAocr, contexto, "AOCR pendiente de validacion o seguimiento institucional.");
            }

            if (estadoSolicitudToken == "DOCUMENTOS_FINALES_DISPONIBLES"
                || estadoSolicitudToken == "AOCR_LEGALIZADO"
                || estadoSolicitudToken == "CERRADO")
            {
                return CrearResultado(DireccionWorkflowAccion.DocumentosFirmados, contexto, "Documentos finales disponibles.");
            }

            return CrearResultado(DireccionWorkflowAccion.SinAccionPendiente, contexto, "El tramite no se encuentra en una etapa pendiente para Direccion.");
        }

        private DireccionWorkflowSiguiente CrearResultado(DireccionWorkflowAccion accion, AocrContextoResolucion contexto, string motivo)
        {
            try
            {
                Trace.TraceInformation(
                    "[DIR][WORKFLOW_NEXT] SolicitudId=" + (contexto != null && contexto.SolicitudId.HasValue ? contexto.SolicitudId.Value.ToString() : string.Empty) +
                    "; EstadoSolicitud=" + (contexto != null ? (contexto.EstadoSolicitud ?? string.Empty) : string.Empty) +
                    "; EstadoInforme=" + (contexto != null && contexto.InformeTecnico != null ? (contexto.InformeTecnico.EstadoInforme ?? string.Empty) : string.Empty) +
                    "; EstadoAocr=" + (contexto != null ? (contexto.EstadoAocr ?? string.Empty) : string.Empty) +
                    "; AccionSiguiente=" + accion +
                    "; Motivo=" + (motivo ?? string.Empty));
            }
            catch
            {
            }

            return new DireccionWorkflowSiguiente
            {
                Accion = accion,
                Contexto = contexto,
                Motivo = motivo
            };
        }

        private static string Normalizar(string estado)
        {
            return (estado ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("/", "_");
        }
    }
}
