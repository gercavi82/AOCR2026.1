using System;
using System.Security.Principal;
using System.Web;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Infrastructure
{
    public interface IUserContextAccessor
    {
        bool TryGetUserId(HttpSessionStateBase session, out int userId);
        bool TryGetCodigoUsuario(HttpSessionStateBase session, out int codigoUsuario);
        string GetNombreUsuario(HttpSessionStateBase session, IPrincipal principal);
        string GetRol(HttpSessionStateBase session);
        string GetCorreo(HttpSessionStateBase session);
    }

    public class UserContextAccessor : IUserContextAccessor
    {
        public bool TryGetUserId(HttpSessionStateBase session, out int userId)
        {
            userId = 0;
            if (session == null)
            {
                return false;
            }

            var value = session["UserId"] ?? session["IdUsuario"];
            return value != null && int.TryParse(value.ToString(), out userId) && userId > 0;
        }

        public bool TryGetCodigoUsuario(HttpSessionStateBase session, out int codigoUsuario)
        {
            codigoUsuario = 0;
            if (session == null)
            {
                return false;
            }

            return session["CodigoUsuario"] != null &&
                int.TryParse(session["CodigoUsuario"].ToString(), out codigoUsuario) &&
                codigoUsuario > 0;
        }

        public string GetNombreUsuario(HttpSessionStateBase session, IPrincipal principal)
        {
            var nombreSesion = (session != null ? session["NombreUsuario"] as string : null) ?? string.Empty;
            nombreSesion = nombreSesion.Trim();
            if (!string.IsNullOrWhiteSpace(nombreSesion))
            {
                return nombreSesion;
            }

            if (principal != null &&
                principal.Identity != null &&
                principal.Identity.IsAuthenticated &&
                !string.IsNullOrWhiteSpace(principal.Identity.Name))
            {
                return principal.Identity.Name.Trim();
            }

            return "ANONIMO";
        }

        public string GetRol(HttpSessionStateBase session)
        {
            var rol = session != null ? session["Rol"] as string : null;
            return RoleGroupingHelper.NormalizeSelectedRole(rol);
        }

        public string GetCorreo(HttpSessionStateBase session)
        {
            var correo = session != null ? session["Correo"] as string : null;
            return (correo ?? string.Empty).Trim();
        }
    }
}
