using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;

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
            if (httpContext != null && httpContext.Session != null
                && httpContext.User != null
                && httpContext.User.Identity != null
                && httpContext.User.Identity.IsAuthenticated)
            {
                AuthenticatedSessionBootstrapper.EnsureSession(httpContext);
            }

            var auth = AocrAuthorizationContextFactory.Build(httpContext);
            var rolesRaw = auth.RawRoles ?? new List<string>();
            var rolesUnificados = auth.Roles ?? new List<string>();
            var rolActivo = RoleGroupingHelper.NormalizeSelectedRole(auth.SelectedRole);

            return new AocrUserContext
            {
                UsuarioId = auth.UserId,
                Login = !string.IsNullOrWhiteSpace(auth.CodigoUsuario)
                    ? auth.CodigoUsuario
                    : (httpContext != null && httpContext.User != null && httpContext.User.Identity != null
                        ? httpContext.User.Identity.Name
                        : string.Empty),
                Nombre = auth.UserName,
                RolesRaw = rolesRaw,
                RolesUnificados = rolesUnificados,
                RolActivo = rolActivo,
                EsAdministrador = RoleGroupingHelper.IsAdministrador(rolActivo),
                EsCoordinacion = RoleGroupingHelper.IsCoordinacion(rolActivo),
                EsInspectorTecnico = RoleGroupingHelper.IsInspectorTecnico(rolActivo),
                EsFinanciero = RoleGroupingHelper.IsFinanciero(rolActivo),
                EsDireccionJefaturaTecnica = RoleGroupingHelper.IsDireccionJefaturaTecnica(rolActivo),
                EsSolicitante = RoleGroupingHelper.IsSolicitante(rolActivo),
                EsLegal = RoleGroupingHelper.IsCoordinacion(rolActivo)
                    && rolesRaw.Any(r => RoleGroupingHelper.HasAnyRawRole(new[] { r }, "CoordinacionLegal", "CoordinadorLegal")),
                CodigoCompaniaActiva = auth.CompanyCode,
                NombreCompaniaActiva = auth.CompanyName
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
