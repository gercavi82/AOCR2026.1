using CapaDatos.Constants;
using CapaModelo;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Infrastructure
{
    public sealed class RtCompaniaBloqueoResult
    {
        public bool Bloqueado { get; set; }
        public string Mensaje { get; set; }
        public string Controller { get; set; }
        public string Action { get; set; }
        public object RouteValues { get; set; }
    }

    /// <summary>
    /// Guardias RT por compañía activa (sesión + validación backend).
    /// </summary>
    public sealed class RtCompaniaScope
    {
        private readonly AocrCompaniaContextService _companiaContext = new AocrCompaniaContextService();
        private readonly AocrProcesoActivoService _procesoActivoService = new AocrProcesoActivoService();

        public string CodigoCompania { get; private set; }
        public string NombreCompania { get; private set; }
        public int UsuarioId { get; private set; }
        public AocrProcesoActivoInfo ProcesoActivo { get; private set; }

        public static RtCompaniaScope FromSession(System.Web.HttpSessionStateBase session, int usuarioId)
        {
            var scope = new RtCompaniaScope
            {
                UsuarioId = usuarioId,
                CodigoCompania = CompaniaActivaSessionHelper.ObtenerCodigo(session),
                NombreCompania = CompaniaActivaSessionHelper.ObtenerNombre(session)
            };

            if (usuarioId > 0 && !string.IsNullOrWhiteSpace(scope.CodigoCompania))
            {
                scope.ProcesoActivo = scope._procesoActivoService.ObtenerProcesoActivoPorCompania(
                    usuarioId,
                    scope.CodigoCompania,
                    scope.NombreCompania);
            }

            return scope;
        }

        public bool TieneCompaniaActivaValida()
        {
            return UsuarioId > 0
                && !string.IsNullOrWhiteSpace(CodigoCompania)
                && _companiaContext.ValidarCompaniaPerteneceAlRt(UsuarioId, CodigoCompania);
        }

        public bool SolicitudPerteneceAlScope(SolicitudAOCR solicitud)
        {
            return _companiaContext.SolicitudPerteneceACompania(solicitud, CodigoCompania, NombreCompania);
        }

        public bool OrdenPerteneceAlScope(CapaDatos.Entidades.OrdenRecaudacion orden)
        {
            return _companiaContext.OrdenPerteneceACompania(orden, CodigoCompania, NombreCompania, UsuarioId);
        }

        public string ObtenerMensajeAccesoDenegado()
        {
            return _companiaContext.ObtenerMensajeAccesoDenegadoCompania();
        }

        public RtCompaniaBloqueoResult EvaluarBloqueoNuevaOrden()
        {
            string mensaje;
            if (_procesoActivoService.PuedeCrearNuevaOrden(UsuarioId, CodigoCompania, NombreCompania, out mensaje))
            {
                return new RtCompaniaBloqueoResult { Bloqueado = false };
            }

            var result = new RtCompaniaBloqueoResult
            {
                Bloqueado = true,
                Mensaje = mensaje,
                Controller = "OrdenRecaudacion",
                Action = "Index"
            };

            if (ProcesoActivo != null && ProcesoActivo.OrdenActiva != null)
            {
                result.Controller = "OrdenRecaudacion";
                result.Action = "Detalles";
                result.RouteValues = new { id = ProcesoActivo.OrdenActiva.Id };
                return result;
            }

            if (ProcesoActivo != null && ProcesoActivo.SolicitudActiva != null)
            {
                result.Controller = "SolicitudAOCR";
                result.Action = "Detalle";
                result.RouteValues = new { id = ProcesoActivo.SolicitudActiva.CodigoSolicitud };
            }

            return result;
        }

        public RtCompaniaBloqueoResult EvaluarBloqueoNuevaSolicitud()
        {
            string mensaje;
            if (_procesoActivoService.PuedeCrearNuevaSolicitud(UsuarioId, CodigoCompania, NombreCompania, out mensaje))
            {
                return new RtCompaniaBloqueoResult { Bloqueado = false };
            }

            var result = new RtCompaniaBloqueoResult
            {
                Bloqueado = true,
                Mensaje = mensaje,
                Controller = "SolicitudAOCR",
                Action = "Index"
            };

            if (ProcesoActivo != null && ProcesoActivo.SolicitudActiva != null)
            {
                if (EstadoSolicitud.PermiteEdicionFormularioEmision(ProcesoActivo.SolicitudActiva.Estado))
                {
                    result.Action = "FormularioEmisionAOCR";
                    result.RouteValues = new { id = ProcesoActivo.SolicitudActiva.CodigoSolicitud };
                }
                else
                {
                    result.Action = "Detalle";
                    result.RouteValues = new { id = ProcesoActivo.SolicitudActiva.CodigoSolicitud };
                }
            }

            return result;
        }

        public void PublicarEnViewBag(System.Web.Mvc.Controller controller)
        {
            if (controller == null || controller.ViewBag == null)
            {
                return;
            }

            controller.ViewBag.CompaniaActivaCodigo = CodigoCompania;
            controller.ViewBag.CompaniaActivaNombre = NombreCompania;
            controller.ViewBag.ProcesoActivoExiste = ProcesoActivo != null && ProcesoActivo.ExisteProcesoActivo;
            controller.ViewBag.ProcesoActivoNumero = ProcesoActivo != null
                ? (ProcesoActivo.NumeroSolicitudActiva ?? ProcesoActivo.NumeroOrdenActiva)
                : null;
            controller.ViewBag.ProcesoActivoEstado = ProcesoActivo != null ? ProcesoActivo.EstadoProcesoActivo : null;
            controller.ViewBag.ProcesoActivoMensaje = ProcesoActivo != null
                ? (ProcesoActivo.ExisteProcesoActivo ? ProcesoActivo.MensajeBloqueo : ProcesoActivo.MensajeInformativo)
                : null;
        }
    }
}
