using System.Web;
using CapaNegocio.Services;
using CapaPresentacion.Infrastructure;

namespace CapaPresentacion.Helpers
{
    /// <summary>
    /// Validación backend de permisos desde controladores (complementa [AocrAuthorize]).
    /// </summary>
    public static class AocrPresentacionAuthorizationHelper
    {
        public static AocrAuthorizationResult Validar(
            HttpContextBase httpContext,
            string modulo,
            string accion,
            int? codigoSolicitud = null,
            int? codigoInspeccion = null,
            int? codigoOrden = null,
            int? codigoInforme = null)
        {
            var contexto = AocrAuthorizationContextFactory.Build(httpContext);
            return new AocrAuthorizationService().PuedeEjecutarAccion(
                accion,
                contexto,
                codigoSolicitud,
                codigoInspeccion,
                codigoOrden,
                codigoInforme,
                modulo);
        }

        public static bool EsPermitido(
            HttpContextBase httpContext,
            string modulo,
            string accion,
            out string motivo,
            int? codigoSolicitud = null,
            int? codigoInspeccion = null,
            int? codigoOrden = null,
            int? codigoInforme = null)
        {
            var resultado = Validar(httpContext, modulo, accion, codigoSolicitud, codigoInspeccion, codigoOrden, codigoInforme);
            motivo = resultado != null ? resultado.Motivo : "No autorizado.";
            return resultado != null && resultado.Permitido;
        }
    }
}
