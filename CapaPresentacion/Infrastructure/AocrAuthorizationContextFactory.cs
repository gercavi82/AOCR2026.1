using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Security;
using CapaDatos.Services;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Infrastructure
{
    public static class AocrAuthorizationContextFactory
    {
        private static readonly IUserContextAccessor UserContextAccessor = new UserContextAccessor();

        public static AocrAuthorizationContext Build(HttpContextBase httpContext)
        {
            var session = httpContext != null ? httpContext.Session : null;
            var principal = httpContext != null ? httpContext.User : null;

            int userId;
            UserContextAccessor.TryGetUserId(session, out userId);

            int codigoUsuario;
            UserContextAccessor.TryGetCodigoUsuario(session, out codigoUsuario);

            var sessionRoles = RoleGroupingHelper.ExtractRoles(
                session != null ? (session["RolesRaw"] ?? session["Roles"]) : null,
                session != null ? session["Rol"] as string : null);
            var ticketRoleData = ReadFormsTicketRoleData(httpContext);
            var principalRoles = ReadPrincipalRoles(principal);
            var rawRoles = sessionRoles
                .Concat(ticketRoleData.Roles)
                .Concat(principalRoles)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var effectiveRoles = RoleGroupingHelper.BuildUnifiedRoles(rawRoles);

            var selectedRole = AuthTicketRoleDataHelper.ReadSelectedRoleFromCookie(
                httpContext != null && httpContext.Request != null ? httpContext.Request.Cookies : null);
            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                selectedRole = UserContextAccessor.GetRol(session);
            }
            if (string.IsNullOrWhiteSpace(selectedRole))
            {
                selectedRole = RoleGroupingHelper.NormalizeSelectedRole(ticketRoleData.SelectedRole);
            }
            if (string.IsNullOrWhiteSpace(selectedRole) && effectiveRoles.Count == 1)
            {
                selectedRole = effectiveRoles[0];
            }
            else if (!string.IsNullOrWhiteSpace(selectedRole)
                && effectiveRoles.Count > 0
                && !effectiveRoles.Contains(RoleGroupingHelper.NormalizeSelectedRole(selectedRole), StringComparer.OrdinalIgnoreCase))
            {
                LogRolActivoInconsistente(httpContext, selectedRole, rawRoles, effectiveRoles);
                selectedRole = effectiveRoles[0];
            }

            return new AocrAuthorizationContext
            {
                IsAuthenticated = principal != null && principal.Identity != null && principal.Identity.IsAuthenticated,
                UserId = userId,
                CodigoUsuario = codigoUsuario > 0
                    ? codigoUsuario.ToString()
                    : Convert.ToString(session != null ? session["CodigoUsuario"] : null),
                UserName = UserContextAccessor.GetNombreUsuario(session, principal),
                SelectedRole = selectedRole,
                RawRoles = rawRoles,
                Roles = effectiveRoles,
                CompanyCode = CompaniaActivaSessionHelper.ObtenerCodigo(session),
                CompanyName = CompaniaActivaSessionHelper.ObtenerNombre(session)
            };
        }

        private static void LogRolActivoInconsistente(
            HttpContextBase httpContext,
            string selectedRole,
            IList<string> rawRoles,
            IList<string> effectiveRoles)
        {
            try
            {
                var session = httpContext != null ? httpContext.Session : null;
                CapaDatos.Services.LoggingServiceFactory.Create().LogWarning(string.Format(
                    "[AOCR][ROL_ACTIVO] Usuario={0}; RolSeleccionado={1}; RolSesion={2}; RolesRaw={3}; RolesEfectivos={4}; Compania={5}; Path={6}; Resultado=ROL_COOKIE_DESCARTADO; Detalle=El rol seleccionado no pertenece a los roles resueltos del usuario.",
                    httpContext != null && httpContext.User != null && httpContext.User.Identity != null
                        ? httpContext.User.Identity.Name
                        : string.Empty,
                    selectedRole ?? string.Empty,
                    session != null ? Convert.ToString(session["Rol"]) : string.Empty,
                    string.Join(",", rawRoles ?? new List<string>()),
                    string.Join(",", effectiveRoles ?? new List<string>()),
                    session != null ? CompaniaActivaSessionHelper.ObtenerCodigo(session) : string.Empty,
                    httpContext != null && httpContext.Request != null && httpContext.Request.Url != null
                        ? httpContext.Request.Url.AbsolutePath
                        : string.Empty));
            }
            catch
            {
            }
        }

        private static AuthTicketRoleData ReadFormsTicketRoleData(HttpContextBase httpContext)
        {
            if (httpContext == null || httpContext.Request == null)
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }

            var authCookie = httpContext.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }

            try
            {
                var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                if (authTicket == null || authTicket.Expired || string.IsNullOrWhiteSpace(authTicket.UserData))
                {
                    return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
                }

                return AuthTicketRoleDataHelper.Deserialize(authTicket.UserData);
            }
            catch
            {
                return new AuthTicketRoleData(Array.Empty<string>(), string.Empty);
            }
        }

        private static IList<string> ReadPrincipalRoles(IPrincipal principal)
        {
            var knownRoles = new[]
            {
                "Administrador",
                "Admin",
                "Solicitante",
                "Operador",
                "RepresentanteTecnico",
                "Representante Tecnico",
                "RepresentanteLegal",
                "RT",
                "Inspector",
                "Tecnico",
                "EvaluadorTecnico",
                "InspectorTecnico",
                "Coordinador",
                "CoordinadorInspecciones",
                "Coordinacion",
                "CoordinacionLegal",
                "CoordinadorLegal",
                "DIRDAC",
                "Direccion",
                "JefaturaTecnica",
                "DirectorGeneral",
                "DireccionJefaturaTecnica",
                "Financiero",
                "CoordinadorFinanciero",
                "DirectorFinanciero"
            };

            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return new List<string>();
            }

            return knownRoles
                .Where(principal.IsInRole)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
