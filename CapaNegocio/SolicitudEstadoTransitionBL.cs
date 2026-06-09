using System;
using System.Configuration;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio.Services;

namespace CapaNegocio
{
    /// <summary>
    /// Centraliza reglas de transicion de estado para Solicitud AOCR.
    /// </summary>
    public class SolicitudEstadoTransitionBL
    {
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();

        private static bool ValidacionCanonicaHabilitada
        {
            get
            {
                var raw = ConfigurationManager.AppSettings["toggle.aocr.solicitud.transicionCanonica"];
                bool enabled;
                return !bool.TryParse(raw, out enabled) || enabled;
            }
        }

        public bool CambiarEstadoConReglasAocr(
            int codigoSolicitud,
            string nuevoEstado,
            string observacion,
            int usuarioId,
            Func<string, bool> puedeTransicionar,
            out string mensaje)
        {
            return CambiarEstadoConReglasAocr(
                codigoSolicitud,
                nuevoEstado,
                observacion,
                usuarioId,
                puedeTransicionar,
                out mensaje,
                false,
                false);
        }

        public bool CambiarEstadoConReglasAocr(
            int codigoSolicitud,
            string nuevoEstado,
            string observacion,
            int usuarioId,
            Func<string, bool> puedeTransicionar,
            out string mensaje,
            bool omitirCorreoGenericoCambioEstado,
            bool omitirCorreoWorkflowEstado)
        {
            mensaje = string.Empty;

            if (codigoSolicitud <= 0)
            {
                mensaje = "Solicitud invalida.";
                return false;
            }

            if (usuarioId <= 0)
            {
                mensaje = "Sesion expirada o usuario invalido.";
                return false;
            }

            var solicitud = _solicitudDAO.ObtenerPorId(codigoSolicitud);
            if (solicitud == null)
            {
                mensaje = "La solicitud no existe.";
                return false;
            }

            var estadoActual = EstadoSolicitud.Normalizar(solicitud.Estado);
            var estadoDestino = EstadoSolicitud.Normalizar(nuevoEstado);

            if (puedeTransicionar != null && !puedeTransicionar(estadoDestino))
            {
                mensaje = "El rol actual no tiene permisos para ejecutar este cambio de estado.";
                return false;
            }

            if (ValidacionCanonicaHabilitada && !EsTransicionAocrPermitida(estadoActual, estadoDestino))
            {
                mensaje = "Transicion no permitida: '" + estadoActual + "' -> '" + estadoDestino + "'.";
                return false;
            }

            var ok = _solicitudDAO.CambiarEstado(codigoSolicitud, estadoDestino, usuarioId, observacion ?? string.Empty);
            if (!ok)
            {
                mensaje = "No se pudo persistir el cambio de estado.";
                return false;
            }

            int? codigoHistorial = null;
            try
            {
                codigoHistorial = new HistorialEstadoDAO().RegistrarCambioYObtenerCodigo(
                    codigoSolicitud,
                    estadoActual,
                    estadoDestino,
                    usuarioId,
                    observacion ?? string.Empty);
            }
            catch
            {
                // Historial auxiliar: no se revierte cambio principal.
            }

            try
            {
                NotificarCambioEstadoAocr(
                    solicitud,
                    codigoSolicitud,
                    estadoActual,
                    estadoDestino,
                    codigoHistorial,
                    omitirCorreoGenericoCambioEstado,
                    omitirCorreoWorkflowEstado);
            }
            catch
            {
                // Notificacion auxiliar: no bloquear flujo principal.
            }

            mensaje = "Estado actualizado correctamente.";
            return true;
        }

        /// <summary>
        /// Punto de validacion pura para pruebas automatizadas de matriz de transiciones.
        /// No accede a base de datos.
        /// </summary>
        public bool EsTransicionPermitidaParaPruebas(string estadoActual, string estadoDestino)
        {
            return EsTransicionAocrPermitida(estadoActual, estadoDestino);
        }

        private static bool EsTransicionAocrPermitida(string estadoActual, string estadoDestino)
        {
            if (EstadoSolicitud.EsTransicionValida(estadoActual, estadoDestino))
            {
                return true;
            }

            var actual = EstadoSolicitud.Normalizar(estadoActual);
            var destino = EstadoSolicitud.Normalizar(estadoDestino);

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

            if (actual == EstadoSolicitud.FirmadoCoordinador && destino == EstadoSolicitud.Finalizado)
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

        private static void NotificarCambioEstadoAocr(
            SolicitudAOCR solicitud,
            int codigoSolicitud,
            string estadoAnterior,
            string estadoDestino,
            int? codigoHistorial,
            bool omitirCorreoGenericoCambioEstado,
            bool omitirCorreoWorkflowEstado)
        {
            if (solicitud == null || codigoSolicitud <= 0)
            {
                return;
            }

            var eventoCorreoWorkflow = ResolverEventoCorreoPorEstado(estadoAnterior, estadoDestino);
            var omitirCorreoGenerico =
                omitirCorreoGenericoCambioEstado || DebeOmitirCorreoGenericoCambioEstado(eventoCorreoWorkflow);

            // Notificaciones en-sistema (campana)
            if (solicitud.CodigoUsuario > 0)
            {
                NotificarCambioEstadoInternoSinCorreoGenericoSiCorresponde(
                    solicitud.CodigoUsuario,
                    codigoSolicitud,
                    estadoDestino,
                    omitirCorreoGenerico);
            }

            if (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0 &&
                solicitud.CodigoTecnico.Value != solicitud.CodigoUsuario)
            {
                NotificarCambioEstadoInternoSinCorreoGenericoSiCorresponde(
                    solicitud.CodigoTecnico.Value,
                    codigoSolicitud,
                    estadoDestino,
                    omitirCorreoGenerico);
            }

            // Notificaciones por correo según transición
            if (!omitirCorreoWorkflowEstado)
            {
                try { DispatchCorreoEventoPorEstado(solicitud, estadoAnterior, estadoDestino, codigoHistorial); } catch { }
            }
        }

        private static bool DebeOmitirCorreoGenericoCambioEstado(string eventoCorreoWorkflow)
        {
            return string.Equals(eventoCorreoWorkflow, "OBSERVADA", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventoCorreoWorkflow, "ACEPTACION_DOCUMENTAL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventoCorreoWorkflow, "PAGO_APROBADO", StringComparison.OrdinalIgnoreCase);
        }

        private static void NotificarCambioEstadoInternoSinCorreoGenericoSiCorresponde(
            int codigoUsuario,
            int codigoSolicitud,
            string estadoDestino,
            bool omitirCorreoGenericoCambioEstado)
        {
            if (!omitirCorreoGenericoCambioEstado)
            {
                NotificacionBL.NotificarCambioEstado(codigoUsuario, codigoSolicitud, estadoDestino);
                return;
            }

            string tipo;
            string titulo;
            ResolverTituloYTipoNotificacionInterna(estadoDestino, out titulo, out tipo);

            NotificacionBL.EnviarNotificacion(
                codigoUsuario,
                titulo,
                $"La solicitud #{codigoSolicitud} cambió a estado: {estadoDestino}",
                tipo,
                TiposNotificacion.Urls.Solicitud(codigoSolicitud),
                "SolicitudAOCR",
                codigoSolicitud,
                "aocr_tbsolicitud");
        }

        private static void ResolverTituloYTipoNotificacionInterna(string estadoDestino, out string titulo, out string tipo)
        {
            tipo = "INFO";
            titulo = "Cambio de Estado";

            if (string.Equals(estadoDestino, EstadoSolicitud.Observada, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDestino, EstadoSolicitud.Rechazada, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "WARNING";
                titulo = "Solicitud Observada";
                return;
            }

            if (string.Equals(estadoDestino, EstadoSolicitud.Anulada, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "ERROR";
                titulo = "Solicitud Anulada";
                return;
            }

            if (string.Equals(estadoDestino, EstadoSolicitud.AOCR_EmitidoRecibido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDestino, EstadoSolicitud.CertificadoEmitido, StringComparison.OrdinalIgnoreCase)
                || string.Equals(estadoDestino, EstadoSolicitud.Aprobada, StringComparison.OrdinalIgnoreCase))
            {
                tipo = "SUCCESS";
                titulo = "Solicitud Aprobada";
            }
        }

        private static void DispatchCorreoEventoPorEstado(
            SolicitudAOCR solicitud, string estadoAnterior, string estadoDestino, int? codigoHistorial)
        {
            var evento = ResolverEventoCorreoPorEstado(estadoAnterior, estadoDestino);
            if (evento == null) { return; }
            new SolicitudAocrCorreoService().NotificarEvento(solicitud, evento, null, null, null, codigoHistorial);
        }

        private static string ResolverEventoCorreoPorEstado(string estadoAnterior, string estadoDestino)
        {
            switch (estadoDestino)
            {
                case EstadoSolicitud.Observada:
                    return "OBSERVADA";

                case EstadoSolicitud.Subsanada:
                    return "SUBSANADA";

                case EstadoSolicitud.AceptacionDocumental:
                    return "ACEPTACION_DOCUMENTAL";

                case EstadoSolicitud.PendienteAsignacionRT:
                    return "PENDIENTE_ASIGNACION_INSPECTOR";

                case EstadoSolicitud.SolicitudCreada:
                case EstadoSolicitud.DocumentacionPendiente:
                    if (estadoAnterior == EstadoSolicitud.PagoPendiente ||
                        estadoAnterior == EstadoSolicitud.PagoValidado)
                    {
                        return "PAGO_APROBADO";
                    }
                    return null;

                case EstadoSolicitud.AOCR_Legalizado:
                    return "AOCR_LEGALIZADO";

                case EstadoSolicitud.AOCR_EmitidoRecibido:
                    return "AOCR_EMITIDO_RECIBIDO";

                default:
                    return null;
            }
        }
    }
}
