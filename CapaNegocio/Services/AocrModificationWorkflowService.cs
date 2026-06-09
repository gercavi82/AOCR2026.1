using System;
using System.Collections.Generic;
using System.Linq;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaNegocio;
using CapaModelo;

namespace CapaNegocio.Services
{
    public sealed class AocrModificacionWorkflowResult
    {
        public bool Exitoso { get; set; }
        public string ClaveTempData { get; set; }
        public string Mensaje { get; set; }
        public string EstadoAnterior { get; set; }
        public string EstadoNuevo { get; set; }
        public bool HistorialRegistrado { get; set; }
        public IList<string> Errores { get; set; }
        public string AccionRedireccion { get; set; }
        public string ControladorRedireccion { get; set; }
        public IDictionary<string, object> RouteValues { get; set; }
    }

    public sealed class AocrModificationWorkflowPlan
    {
        public bool PuedeContinuar { get; set; }
        public string ClaveTempData { get; set; }
        public string Mensaje { get; set; }
        public string EstadoDestino { get; set; }
        public string ObservacionEstado { get; set; }
    }

    public class AocrModificationWorkflowService
    {
        private readonly SolicitudAOCRDAO _solicitudDao = new SolicitudAOCRDAO();
        private readonly SolicitudEstadoTransitionBL _solicitudEstadoTransitionBl = new SolicitudEstadoTransitionBL();
        private readonly AocrFinalWorkflowService _aocrFinalWorkflowService = new AocrFinalWorkflowService();

        public AocrModificacionWorkflowResult EjecutarRequiereInspeccion(
            int solicitudId,
            string observacion,
            int usuarioId,
            IEnumerable<string> rolesActuales,
            bool usuarioAutenticado)
        {
            return EjecutarCambioEstadoModificacion(
                solicitudId,
                observacion,
                usuarioId,
                rolesActuales,
                usuarioAutenticado,
                PrepararRequiereInspeccion,
                "La modificación fue marcada como REQUIERE_INSPECCIÓN.");
        }

        public AocrModificacionWorkflowResult EjecutarGeneracionCondicionesLimitaciones(
            int solicitudId,
            string observacion,
            int usuarioId,
            IEnumerable<string> rolesActuales,
            bool usuarioAutenticado)
        {
            return EjecutarCambioEstadoModificacion(
                solicitudId,
                observacion,
                usuarioId,
                rolesActuales,
                usuarioAutenticado,
                PrepararGeneracionCondicionesLimitaciones,
                "La modificación quedó lista para revisión final de Condiciones y Limitaciones.");
        }

        public AocrModificationWorkflowPlan PrepararRequiereInspeccion(SolicitudAOCR solicitud, string observacion)
        {
            if (solicitud == null)
            {
                return CrearPlanError("warning", "La solicitud indicada no existe.");
            }

            if (!EsSolicitudModificacion(solicitud))
            {
                return CrearPlanError("warning", "La solicitud indicada no corresponde a una modificación AOCR.");
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                return CrearPlanError("warning", "Solo puede derivar a inspección una modificación con documentación ya aceptada.");
            }

            return CrearPlanExito(
                EstadoSolicitud.RequiereInspeccion,
                string.IsNullOrWhiteSpace(observacion)
                    ? "El inspector determinó que la modificación requiere derivación al módulo de inspección."
                    : observacion.Trim());
        }

        public AocrModificationWorkflowPlan PrepararGeneracionCondicionesLimitaciones(SolicitudAOCR solicitud, string observacion)
        {
            if (solicitud == null)
            {
                return CrearPlanError("warning", "La solicitud indicada no existe.");
            }

            if (!EsSolicitudModificacion(solicitud))
            {
                return CrearPlanError("warning", "La solicitud indicada no corresponde a una modificación AOCR.");
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.AceptacionDocumental, StringComparison.OrdinalIgnoreCase))
            {
                return CrearPlanError("warning", "Solo puede generar Condiciones y Limitaciones cuando la documentación de la modificación ya fue aceptada.");
            }

            return CrearPlanExito(
                EstadoSolicitud.GeneradoCondicionesLimitaciones,
                string.IsNullOrWhiteSpace(observacion)
                    ? "El inspector determinó que la modificación no requiere nueva inspección. Se habilita la generación de Condiciones y Limitaciones."
                    : observacion.Trim());
        }

        public AocrModificationWorkflowPlan PrepararRevisionFinalCondicionesLimitaciones(SolicitudAOCR solicitud, string observacion)
        {
            if (solicitud == null)
            {
                return CrearPlanError("warning", "La solicitud indicada no existe.");
            }

            if (!EsSolicitudModificacion(solicitud))
            {
                return CrearPlanError("warning", "La solicitud indicada no corresponde a una modificación AOCR.");
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.GeneradoCondicionesLimitaciones, StringComparison.OrdinalIgnoreCase))
            {
                return CrearPlanError("warning", "Solo puede abrir revisión final desde el estado GENERADO_CONDICIONES_LIMITACIONES.");
            }

            return CrearPlanExito(
                EstadoSolicitud.EnRevisionCoordinadorFinal,
                string.IsNullOrWhiteSpace(observacion)
                    ? "Condiciones y Limitaciones enviadas a revisión final de coordinación."
                    : observacion.Trim());
        }

        public AocrModificationWorkflowPlan PrepararEnvioDcavCondicionesLimitaciones(SolicitudAOCR solicitud, string observacion)
        {
            if (solicitud == null)
            {
                return CrearPlanError("warning", "La solicitud indicada no existe.");
            }

            if (!EsSolicitudModificacion(solicitud))
            {
                return CrearPlanError("warning", "La solicitud indicada no corresponde a una modificación AOCR.");
            }

            if (!string.Equals(EstadoSolicitud.Normalizar(solicitud.Estado), EstadoSolicitud.EnRevisionCoordinadorFinal, StringComparison.OrdinalIgnoreCase))
            {
                return CrearPlanError("warning", "Solo puede enviar a DCAV una modificación en revisión final de coordinación.");
            }

            return CrearPlanExito(
                EstadoSolicitud.EnviadoDcav,
                string.IsNullOrWhiteSpace(observacion)
                    ? "Condiciones y Limitaciones enviadas a DCAV/DGAC para firma."
                    : observacion.Trim());
        }

        private static bool EsSolicitudModificacion(SolicitudAOCR solicitud)
        {
            return solicitud != null && solicitud.TipoSolicitud.GetValueOrDefault() == 3;
        }

        private AocrModificacionWorkflowResult EjecutarCambioEstadoModificacion(
            int solicitudId,
            string observacion,
            int usuarioId,
            IEnumerable<string> rolesActuales,
            bool usuarioAutenticado,
            Func<SolicitudAOCR, string, AocrModificationWorkflowPlan> prepararPlan,
            string mensajeExito)
        {
            var solicitud = solicitudId > 0 ? _solicitudDao.ObtenerPorId(solicitudId) : null;
            var estadoAnterior = EstadoSolicitud.Normalizar(solicitud != null ? solicitud.Estado : string.Empty);

            if (solicitud == null)
            {
                return CrearResultadoError("warning", "La solicitud indicada no existe.", estadoAnterior, estadoAnterior, solicitudId);
            }

            var plan = prepararPlan(solicitud, observacion);
            if (!plan.PuedeContinuar)
            {
                return CrearResultadoError(plan.ClaveTempData, plan.Mensaje, estadoAnterior, estadoAnterior, solicitudId);
            }

            var estadoDestino = EstadoSolicitud.Normalizar(plan.EstadoDestino ?? string.Empty);
            if (string.Equals(estadoAnterior, estadoDestino, StringComparison.OrdinalIgnoreCase))
            {
                return new AocrModificacionWorkflowResult
                {
                    Exitoso = true,
                    ClaveTempData = "warning",
                    Mensaje = "La transición ya había sido aplicada previamente.",
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = estadoDestino,
                    HistorialRegistrado = false,
                    Errores = new List<string>(),
                    AccionRedireccion = "Detalle",
                    ControladorRedireccion = null,
                    RouteValues = CrearRouteValues(solicitudId)
                };
            }

            string mensajeCambio;
            var ok = _solicitudEstadoTransitionBl.CambiarEstadoConReglasAocr(
                solicitudId,
                estadoDestino,
                plan.ObservacionEstado,
                usuarioId,
                destino => _aocrFinalWorkflowService.UsuarioPuedeTransicionarEstadoAocr(destino, rolesActuales ?? Enumerable.Empty<string>(), usuarioAutenticado),
                out mensajeCambio);

            if (!ok)
            {
                return CrearResultadoError("error", mensajeCambio, estadoAnterior, estadoAnterior, solicitudId);
            }

            return new AocrModificacionWorkflowResult
            {
                Exitoso = true,
                ClaveTempData = "success",
                Mensaje = mensajeExito,
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = estadoDestino,
                HistorialRegistrado = true,
                Errores = new List<string>(),
                AccionRedireccion = "Detalle",
                ControladorRedireccion = null,
                RouteValues = CrearRouteValues(solicitudId)
            };
        }

        private static AocrModificationWorkflowPlan CrearPlanError(string claveTempData, string mensaje)
        {
            return new AocrModificationWorkflowPlan
            {
                PuedeContinuar = false,
                ClaveTempData = claveTempData,
                Mensaje = mensaje
            };
        }

        private static AocrModificationWorkflowPlan CrearPlanExito(string estadoDestino, string observacionEstado)
        {
            return new AocrModificationWorkflowPlan
            {
                PuedeContinuar = true,
                EstadoDestino = estadoDestino,
                ObservacionEstado = observacionEstado
            };
        }

        private static AocrModificacionWorkflowResult CrearResultadoError(
            string claveTempData,
            string mensaje,
            string estadoAnterior,
            string estadoNuevo,
            int solicitudId)
        {
            return new AocrModificacionWorkflowResult
            {
                Exitoso = false,
                ClaveTempData = claveTempData,
                Mensaje = mensaje,
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = estadoNuevo,
                HistorialRegistrado = false,
                Errores = string.IsNullOrWhiteSpace(mensaje) ? new List<string>() : new List<string> { mensaje },
                AccionRedireccion = "Detalle",
                ControladorRedireccion = null,
                RouteValues = CrearRouteValues(solicitudId)
            };
        }

        private static IDictionary<string, object> CrearRouteValues(int solicitudId)
        {
            return new Dictionary<string, object>
            {
                { "id", solicitudId }
            };
        }
    }
}