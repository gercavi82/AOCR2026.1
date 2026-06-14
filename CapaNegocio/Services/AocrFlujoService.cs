using System;
using CapaDatos.Constants;
using CapaModelo;

namespace CapaNegocio.Services
{
    /// <summary>
    /// Matriz operativa de flujo AOCR: transiciones extendidas, roles y reglas de asignación.
    /// </summary>
    public interface IAocrFlujoService
    {
        bool EsTransicionPermitida(string estadoActual, string estadoDestino);
        bool RequiereRecaudacionFinalizadaParaAsignacion(string estadoSolicitud);
        bool PuedeCoordinadorAsignarInspector(SolicitudAOCR solicitud, bool tieneAprobacionFinanciera);
        bool RolPuedeEjecutarAccion(string rolNormalizado, string accionFlujo);
    }

    public static class AocrFlujoAcciones
    {
        public const string CrearOrdenRecaudacion = "CREAR_ORDEN_RECAUDACION";
        public const string CargarComprobantePago = "CARGAR_COMPROBANTE_PAGO";
        public const string AprobarPago = "APROBAR_PAGO";
        public const string CargarDocumentacionRt = "CARGAR_DOCUMENTACION_RT";
        public const string EnviarCoordinacion = "ENVIAR_COORDINACION";
        public const string RevisarDocumentacionCoordinador = "REVISAR_DOCUMENTACION_COORDINADOR";
        public const string AceptarDocumentacion = "ACEPTAR_DOCUMENTACION";
        public const string DevolverRtObservaciones = "DEVOLVER_RT_OBSERVACIONES";
        public const string AsignarInspector = "ASIGNAR_INSPECTOR";
        public const string RevisarDocumentacionInspector = "REVISAR_DOCUMENTACION_INSPECTOR";
        public const string GenerarSolicitudInspeccion = "GENERAR_SOLICITUD_INSPECCION";
        public const string FirmarListaVerificacion = "FIRMAR_LV";
        public const string FirmarInformeTecnico = "FIRMAR_INFORME_TECNICO";
        public const string GenerarAocr = "GENERAR_AOCR";
        public const string EnviarDirdac = "ENVIAR_DIRDAC";
        public const string FirmarAocrFinal = "FIRMAR_AOCR_FINAL";
        public const string LiberarDocumentosFinales = "LIBERAR_DOCUMENTOS_FINALES";
    }

    public sealed class AocrFlujoService : IAocrFlujoService
    {
        private readonly IAocrEstadoService _estadoService;

        public AocrFlujoService()
            : this(new AocrEstadoService())
        {
        }

        public AocrFlujoService(IAocrEstadoService estadoService)
        {
            _estadoService = estadoService ?? new AocrEstadoService();
        }

        public bool EsTransicionPermitida(string estadoActual, string estadoDestino)
        {
            if (_estadoService.EsTransicionCanonicaValida(estadoActual, estadoDestino))
            {
                return true;
            }

            var actual = _estadoService.Normalizar(estadoActual);
            var destino = _estadoService.Normalizar(estadoDestino);

            if (string.Equals(actual, destino, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actual, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase);
            }

            if ((actual == EstadoSolicitud.Pendiente
                    || actual == EstadoSolicitud.EnRevision
                    || actual == EstadoSolicitud.DocumentacionPendiente
                    || actual == EstadoSolicitud.Subsanada
                    || actual == EstadoSolicitud.EnInspeccion) &&
                (destino == EstadoSolicitud.Observada || destino == EstadoSolicitud.AceptacionDocumental))
            {
                return true;
            }

            if ((actual == EstadoSolicitud.EnRevision
                    || actual == EstadoSolicitud.DocumentacionPendiente
                    || actual == EstadoSolicitud.Subsanada) &&
                destino == EstadoSolicitud.EnInspeccion)
            {
                return true;
            }

            if (actual == EstadoSolicitud.AceptacionDocumental && destino == EstadoSolicitud.FirmadoCoordinador)
            {
                return true;
            }

            if (actual == EstadoSolicitud.AceptacionDocumental &&
                (destino == EstadoSolicitud.RequiereInspeccion || destino == EstadoSolicitud.GeneradoCondicionesLimitaciones))
            {
                return true;
            }

            if (actual == EstadoSolicitud.GeneradoCondicionesLimitaciones && destino == EstadoSolicitud.EnRevisionCoordinadorFinal)
            {
                return true;
            }

            if (actual == EstadoSolicitud.EnRevisionCoordinadorFinal && destino == EstadoSolicitud.EnviadoDcav)
            {
                return true;
            }

            if (actual == EstadoSolicitud.EnviadoDcav && destino == EstadoSolicitud.FirmadoDcav)
            {
                return true;
            }

            if (actual == EstadoSolicitud.FirmadoDcav && destino == EstadoSolicitud.Finalizado)
            {
                return true;
            }

            if (actual == EstadoSolicitud.FirmadoCoordinador && destino == EstadoSolicitud.PendienteAsignacionRT)
            {
                return true;
            }

            if (actual == EstadoSolicitud.FirmadoCoordinador &&
                (destino == EstadoSolicitud.RequiereInspeccion || destino == EstadoSolicitud.GeneradoCondicionesLimitaciones))
            {
                return true;
            }

            if ((actual == EstadoSolicitud.DocumentacionCompleta || actual == EstadoSolicitud.AceptacionDocumental) &&
                destino == EstadoSolicitud.EnInspeccion)
            {
                return true;
            }

            if (actual == EstadoSolicitud.AceptacionDocumental && destino == EstadoSolicitud.PendienteAsignacionRT)
            {
                return true;
            }

            if (actual == EstadoSolicitud.PendienteAsignacionRT && destino == EstadoSolicitud.EnInspeccion)
            {
                return true;
            }

            if (actual == EstadoSolicitud.RequiereInspeccion &&
                (destino == EstadoSolicitud.PendienteAsignacionRT || destino == EstadoSolicitud.EnInspeccion))
            {
                return true;
            }

            if ((actual == EstadoSolicitud.Aprobada || actual == EstadoSolicitud.AOCR_EnRevision) &&
                destino == EstadoSolicitud.AOCR_Validado)
            {
                return true;
            }

            if ((actual == EstadoSolicitud.AOCR_EnElaboracion || actual == EstadoSolicitud.Aprobada)
                && destino == EstadoSolicitud.AOCR_EnRevision)
            {
                return true;
            }

            if ((actual == EstadoSolicitud.Aprobada || actual == EstadoSolicitud.AOCR_Validado) &&
                destino == EstadoSolicitud.AOCR_Legalizado)
            {
                return true;
            }

            if ((actual == EstadoSolicitud.AOCR_Legalizado || actual == EstadoSolicitud.CertificadoEmitido) &&
                destino == EstadoSolicitud.AOCR_EmitidoRecibido)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Alineado con bandeja de coordinación: estados de asignación inicial no exigen recaudación finalizada
        /// si el trámite ya está en revisión documental/coordinación.
        /// </summary>
        public bool RequiereRecaudacionFinalizadaParaAsignacion(string estadoSolicitud)
        {
            return !_estadoService.EstadoPermiteAsignacionInicial(estadoSolicitud);
        }

        public bool PuedeCoordinadorAsignarInspector(SolicitudAOCR solicitud, bool tieneAprobacionFinanciera)
        {
            if (solicitud == null || solicitud.CodigoSolicitud <= 0)
            {
                return false;
            }

            var estado = _estadoService.Normalizar(solicitud.Estado);
            if (string.Equals(estado, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estado, EstadoSolicitud.Finalizado, StringComparison.OrdinalIgnoreCase)
                || _estadoService.EsEstadoFinal(estado))
            {
                return false;
            }

            if (!_estadoService.EstadoPermiteAsignacionInicial(estado)
                && !string.Equals(estado, EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (RequiereRecaudacionFinalizadaParaAsignacion(estado) && !tieneAprobacionFinanciera)
            {
                return false;
            }

            return true;
        }

        public bool RolPuedeEjecutarAccion(string rolNormalizado, string accionFlujo)
        {
            if (string.IsNullOrWhiteSpace(rolNormalizado) || string.IsNullOrWhiteSpace(accionFlujo))
            {
                return false;
            }

            var rol = rolNormalizado.Trim();
            var accion = accionFlujo.Trim();

            if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            switch (accion)
            {
                case AocrFlujoAcciones.CrearOrdenRecaudacion:
                case AocrFlujoAcciones.CargarComprobantePago:
                case AocrFlujoAcciones.CargarDocumentacionRt:
                case AocrFlujoAcciones.EnviarCoordinacion:
                    return string.Equals(rol, "Solicitante", StringComparison.OrdinalIgnoreCase);
                case AocrFlujoAcciones.AprobarPago:
                    return string.Equals(rol, "Financiero", StringComparison.OrdinalIgnoreCase);
                case AocrFlujoAcciones.RevisarDocumentacionCoordinador:
                case AocrFlujoAcciones.AceptarDocumentacion:
                case AocrFlujoAcciones.DevolverRtObservaciones:
                case AocrFlujoAcciones.AsignarInspector:
                case AocrFlujoAcciones.GenerarAocr:
                case AocrFlujoAcciones.EnviarDirdac:
                    return string.Equals(rol, "Coordinacion", StringComparison.OrdinalIgnoreCase);
                case AocrFlujoAcciones.RevisarDocumentacionInspector:
                case AocrFlujoAcciones.GenerarSolicitudInspeccion:
                case AocrFlujoAcciones.FirmarListaVerificacion:
                case AocrFlujoAcciones.FirmarInformeTecnico:
                    return string.Equals(rol, "InspectorTecnico", StringComparison.OrdinalIgnoreCase);
                case AocrFlujoAcciones.FirmarAocrFinal:
                case AocrFlujoAcciones.LiberarDocumentosFinales:
                    return string.Equals(rol, "DireccionJefaturaTecnica", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rol, "Dirdac", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }
    }
}
