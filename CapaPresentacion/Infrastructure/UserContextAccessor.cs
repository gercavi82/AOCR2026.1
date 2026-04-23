using System;
using System.Security.Principal;
using System.Web;

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
            if (value != null && int.TryParse(value.ToString(), out userId) && userId > 0)
            {
                return true;
            }

            if (session["CodigoUsuario"] != null &&
                int.TryParse(session["CodigoUsuario"].ToString(), out userId) &&
                userId > 0)
            {
                session["UserId"] = userId;
                session["IdUsuario"] = userId;
                return true;
            }

            userId = 0;
            return false;
        }

        public bool TryGetCodigoUsuario(HttpSessionStateBase session, out int codigoUsuario)
        {
            codigoUsuario = 0;
            if (session == null)
            {
                return false;
            }

            if (session["CodigoUsuario"] != null &&
                int.TryParse(session["CodigoUsuario"].ToString(), out codigoUsuario) &&
                codigoUsuario > 0)
            {
                return true;
            }

            if (TryGetUserId(session, out codigoUsuario))
            {
                session["CodigoUsuario"] = codigoUsuario.ToString();
                return true;
            }

            codigoUsuario = 0;
            return false;
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
            return (rol ?? string.Empty).Trim();
        }

        public string GetCorreo(HttpSessionStateBase session)
        {
            var correo = session != null ? session["Correo"] as string : null;
            return (correo ?? string.Empty).Trim();
        }
    }
}
