using System;
using System.Configuration;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;

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

            try
            {
                new HistorialEstadoDAO().RegistrarCambio(
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
                NotificarCambioEstadoAocr(solicitud, codigoSolicitud, estadoDestino);
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

            if ((actual == EstadoSolicitud.Pendiente || actual == EstadoSolicitud.EnRevision || actual == EstadoSolicitud.DocumentacionPendiente) &&
                (destino == EstadoSolicitud.Observada || destino == EstadoSolicitud.AceptacionDocumental))
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

            if ((actual == EstadoSolicitud.Aprobada || actual == EstadoSolicitud.AOCR_EnRevision) &&
                destino == EstadoSolicitud.AOCR_Validado)
            {
                return true;
            }

            if ((actual == EstadoSolicitud.Pendiente || actual == EstadoSolicitud.AOCR_Validado) && destino == EstadoSolicitud.AOCR_Legalizado)
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

        private static void NotificarCambioEstadoAocr(SolicitudAOCR solicitud, int codigoSolicitud, string estadoDestino)
        {
            if (solicitud == null || codigoSolicitud <= 0)
            {
                return;
            }

            if (solicitud.CodigoUsuario > 0)
            {
                NotificacionBL.NotificarCambioEstado(solicitud.CodigoUsuario, codigoSolicitud, estadoDestino);
            }

            if (solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0 &&
                solicitud.CodigoTecnico.Value != solicitud.CodigoUsuario)
            {
                NotificacionBL.NotificarCambioEstado(solicitud.CodigoTecnico.Value, codigoSolicitud, estadoDestino);
            }
        }
    }
}
