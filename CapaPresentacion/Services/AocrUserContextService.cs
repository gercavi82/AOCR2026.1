using System.Collections.Generic;
using CapaNegocio.Services;
using System.Web;

namespace CapaPresentacion.Services
{
    public sealed class AocrUserContext
    {
        public int UsuarioId { get; set; }
        public string Login { get; set; }
        public string Nombre { get; set; }
        public IList<string> RolesRaw { get; set; }
        public IList<string> RolesUnificados { get; set; }
        public string RolActivo { get; set; }
        public bool EsAdministrador { get; set; }
        public bool EsCoordinacion { get; set; }
        public bool EsInspectorTecnico { get; set; }
        public bool EsFinanciero { get; set; }
        public bool EsDireccionJefaturaTecnica { get; set; }
        public bool EsSolicitante { get; set; }
        public bool EsLegal { get; set; }
        public string CodigoCompaniaActiva { get; set; }
        public string NombreCompaniaActiva { get; set; }
    }

    public static class AocrUserContextService
    {
        public static AocrUserContext FromHttpContext(HttpContextBase httpContext)
        {
            UsuarioContextoDto contexto;
            var servicio = new UsuarioContextoService(() => httpContext, null);
            if (!servicio.TryObtenerContextoActual(out contexto))
            {
                return new AocrUserContext
                {
                    RolesRaw = new List<string>(),
                    RolesUnificados = new List<string>()
                };
            }

            return new AocrUserContext
            {
                UsuarioId = contexto.UsuarioId,
                Login = contexto.Login,
                Nombre = contexto.NombreCompleto,
                RolesRaw = contexto.RolesRaw,
                RolesUnificados = contexto.Roles,
                RolActivo = contexto.RolActivo,
                EsAdministrador = contexto.EsAdministrador,
                EsCoordinacion = contexto.EsCoordinacion,
                EsInspectorTecnico = contexto.EsInspectorTecnico,
                EsFinanciero = contexto.EsFinanciero,
                EsDireccionJefaturaTecnica = contexto.EsDireccionJefaturaTecnica,
                EsSolicitante = contexto.EsSolicitante,
                EsLegal = contexto.EsLegal,
                CodigoCompaniaActiva = contexto.CompaniaCodigo,
                NombreCompaniaActiva = contexto.CompaniaNombre
            };
        }

        public static AocrBandejaRoleContext ToBandejaRoleContext(AocrUserContext context)
        {
            if (context == null)
            {
                return new AocrBandejaRoleContext();
            }

            return new AocrBandejaRoleContext
            {
                UserId = context.UsuarioId,
                CodigoUsuario = context.Login,
                UserName = context.Nombre,
                RolActivo = context.RolActivo,
                RolesUnificados = context.RolesUnificados,
                EsAdministrador = context.EsAdministrador,
                EsCoordinacion = context.EsCoordinacion,
                EsInspectorTecnico = context.EsInspectorTecnico,
                EsFinanciero = context.EsFinanciero,
                EsDireccionJefaturaTecnica = context.EsDireccionJefaturaTecnica,
                EsSolicitante = context.EsSolicitante,
                EsLegal = context.EsLegal,
                CodigoCompaniaActiva = context.CodigoCompaniaActiva
            };
        }
    }
}
