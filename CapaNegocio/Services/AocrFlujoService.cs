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

        // Acciones exclusivas DIRCAV
        public const string DircavAceptarDocumentacion = "DIRCAV_ACEPTAR_DOCUMENTACION";
        public const string DircavDevolverCoordinador = "DIRCAV_DEVOLVER_COORDINADOR";
        public const string DircavConfirmarDesignacion = "DIRCAV_CONFIRMAR_DESIGNACION";
        public const string DircavFirmarDesignacion = "DIRCAV_FIRMAR_DESIGNACION";
        public const string DircavRevisarInforme = "DIRCAV_REVISAR_INFORME";
        public const string DircavFirmarCl = "DIRCAV_FIRMAR_CL";
        public const string DircavRemitirDirdac = "DIRCAV_REMITIR_DIRDAC";

        // Acciones exclusivas DIRDAC
        public const string DirdacRevisarAocr = "DIRDAC_REVISAR_AOCR";
        public const string DirdacDevolverDircav = "DIRDAC_DEVOLVER_DIRCAV";
        public const string DirdacFirmarAocr = "DIRDAC_FIRMAR_AOCR";
        public const string DirdacConfirmarLegalizacion = "DIRDAC_CONFIRMAR_LEGALIZACION";

        // Acciones exclusivas COORDINADOR
        public const string CoordinadorRemitirDircav = "COORDINADOR_REMITIR_DIRCAV";
        public const string CoordinadorDevolverInspector = "COORDINADOR_DEVOLVER_INSPECTOR";
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
                    || actual == EstadoSolicitud.EnInspeccion
                    || string.Equals(actual, AocrEstadosProceso.DevueltoInspector, StringComparison.OrdinalIgnoreCase)) &&
                (destino == EstadoSolicitud.Observada
                    || destino == EstadoSolicitud.AceptacionDocumental
                    || string.Equals(destino, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // AC-04: Transiciones del Coordinador para Revisión Documental
            if (string.Equals(actual, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(destino, AocrEstadosProceso.DevueltoInspector, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.PendienteAceptacionDircav, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // AC-04: Reenvío del Inspector tras corrección
            if (string.Equals(actual, AocrEstadosProceso.DevueltoInspector, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(destino, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // AC-05: Transiciones de DIRCAV (Aceptación, Devolución y Designación de Inspector)
            if (string.Equals(actual, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(destino, AocrEstadosProceso.DevueltoCoordinador, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.DevueltoCoordinadorPorDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.DocumentacionAceptadaDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if ((string.Equals(actual, AocrEstadosProceso.DevueltoCoordinador, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actual, AocrEstadosProceso.DevueltoCoordinadorPorDircav, StringComparison.OrdinalIgnoreCase)) &&
                (string.Equals(destino, AocrEstadosProceso.PendienteCoordinador, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.PendienteDircav, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (string.Equals(actual, AocrEstadosProceso.DocumentacionAceptadaDircav, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(destino, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.DesignacionPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (string.Equals(actual, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(destino, AocrEstadosProceso.DesignacionPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(actual, AocrEstadosProceso.DesignacionPendienteFirmaDircav, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(destino, AocrEstadosProceso.DesignacionFirmadaDircav, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destino, AocrEstadosProceso.PendienteDesignacionDircav, StringComparison.OrdinalIgnoreCase)))
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

            var rol = NormalizarRolFlujo(rolNormalizado);
            var accion = accionFlujo.Trim();

            // REGLA 7: El Administrador no puede aprobar, devolver, designar o firmar en representacion de roles operativos.
            if (string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                switch (accion)
                {
                    case AocrFlujoAcciones.AprobarPago:
                    case AocrFlujoAcciones.AceptarDocumentacion:
                    case AocrFlujoAcciones.DevolverRtObservaciones:
                    case AocrFlujoAcciones.AsignarInspector:
                    case AocrFlujoAcciones.FirmarListaVerificacion:
                    case AocrFlujoAcciones.FirmarInformeTecnico:
                    case AocrFlujoAcciones.FirmarAocrFinal:
                    case AocrFlujoAcciones.DircavAceptarDocumentacion:
                    case AocrFlujoAcciones.DircavDevolverCoordinador:
                    case AocrFlujoAcciones.DircavConfirmarDesignacion:
                    case AocrFlujoAcciones.DircavFirmarDesignacion:
                    case AocrFlujoAcciones.DircavRevisarInforme:
                    case AocrFlujoAcciones.DircavFirmarCl:
                    case AocrFlujoAcciones.DircavRemitirDirdac:
                    case AocrFlujoAcciones.DirdacRevisarAocr:
                    case AocrFlujoAcciones.DirdacDevolverDircav:
                    case AocrFlujoAcciones.DirdacFirmarAocr:
                    case AocrFlujoAcciones.DirdacConfirmarLegalizacion:
                    case AocrFlujoAcciones.CoordinadorRemitirDircav:
                    case AocrFlujoAcciones.CoordinadorDevolverInspector:
                        return false;
                    default:
                        return true;
                }
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
                case AocrFlujoAcciones.CoordinadorRemitirDircav:
                case AocrFlujoAcciones.CoordinadorDevolverInspector:
                    return string.Equals(rol, "Coordinacion", StringComparison.OrdinalIgnoreCase);

                // COORDINADOR NUNCA REMITE DIRECTAMENTE A DIRDAC
                case AocrFlujoAcciones.EnviarDirdac:
                    return false;

                case AocrFlujoAcciones.RevisarDocumentacionInspector:
                case AocrFlujoAcciones.GenerarSolicitudInspeccion:
                case AocrFlujoAcciones.FirmarListaVerificacion:
                case AocrFlujoAcciones.FirmarInformeTecnico:
                    return string.Equals(rol, "InspectorTecnico", StringComparison.OrdinalIgnoreCase);

                // DIRCAV
                case AocrFlujoAcciones.DircavAceptarDocumentacion:
                case AocrFlujoAcciones.DircavDevolverCoordinador:
                case AocrFlujoAcciones.DircavConfirmarDesignacion:
                case AocrFlujoAcciones.DircavFirmarDesignacion:
                case AocrFlujoAcciones.DircavRevisarInforme:
                case AocrFlujoAcciones.DircavFirmarCl:
                case AocrFlujoAcciones.DircavRemitirDirdac:
                    return string.Equals(rol, "Dcav", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rol, "Dircav", StringComparison.OrdinalIgnoreCase);

                // DIRDAC
                case AocrFlujoAcciones.DirdacRevisarAocr:
                case AocrFlujoAcciones.DirdacDevolverDircav:
                case AocrFlujoAcciones.DirdacFirmarAocr:
                case AocrFlujoAcciones.DirdacConfirmarLegalizacion:
                case AocrFlujoAcciones.FirmarAocrFinal:
                    return string.Equals(rol, "Dirdac", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rol, "DireccionJefaturaTecnica", StringComparison.OrdinalIgnoreCase);

                case AocrFlujoAcciones.LiberarDocumentosFinales:
                    return string.Equals(rol, "Dirdac", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rol, "DireccionJefaturaTecnica", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rol, "Dcav", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rol, "Dircav", StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }

        public static string NormalizarRolFlujo(string rol)
        {
            if (string.IsNullOrWhiteSpace(rol))
            {
                return string.Empty;
            }

            var clean = rol.Trim().ToUpperInvariant()
                .Replace("Á", "A")
                .Replace("É", "E")
                .Replace("Í", "I")
                .Replace("Ó", "O")
                .Replace("Ú", "U")
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");

            if (clean == "ADMIN" || clean == "ADMINISTRADOR") return "Administrador";
            if (clean == "SOLICITANTE" || clean == "OPERADOR" || clean == "REPRESENTANTETECNICO" || clean == "REPRESENTANTELEGAL" || clean == "RT") return "Solicitante";
            if (clean == "INSPECTOR" || clean == "TECNICO" || clean == "EVALUADORTECNICO" || clean == "INSPECTORTECNICO") return "InspectorTecnico";
            if (clean == "FINANCIERO" || clean == "COORDINADORFINANCIERO" || clean == "DIRECTORFINANCIERO") return "Financiero";
            if (clean == "COORDINACION" || clean == "COORDINADOR" || clean == "COORDINADORINSPECCIONES" || clean == "COORDINACIONLEGAL" || clean == "COORDINADORLEGAL") return "Coordinacion";
            if (clean == "DCAV" || clean == "DIRECTORCERTIFICACIONESDCAV") return "Dcav";
            if (clean == "DIRDAC") return "Dirdac";
            if (clean == "DIRECCION" || clean == "JEFATURATECNICA" || clean == "DIRECTORGENERAL" || clean == "DIRECCIONJEFATURA" || clean == "DIRECCIONJEFATURATECNICA") return "DireccionJefaturaTecnica";

            return rol.Trim();
        }
    }
}
