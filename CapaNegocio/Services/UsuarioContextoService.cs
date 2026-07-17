using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;

namespace CapaNegocio.Services
{
    public class UsuarioContextoService : IUsuarioContextoService
    {
        private readonly HttpContextBase _httpContext;

        public UsuarioContextoService()
        {
        }

        public UsuarioContextoService(HttpContextBase httpContext)
        {
            _httpContext = httpContext;
        }

        private HttpContextBase GetContext()
        {
            if (_httpContext != null)
                return _httpContext;

            if (HttpContext.Current != null)
                return new HttpContextWrapper(HttpContext.Current);

            return null;
        }

        public UsuarioContexto ObtenerContextoActual()
        {
            var ctx = GetContext();
            if (ctx == null)
            {
                return new UsuarioContexto { EstaAutenticado = false };
            }

            var session = ctx.Session;
            var identity = ctx.User?.Identity;

            var estaAutenticado = identity != null && identity.IsAuthenticated;

            var contexto = new UsuarioContexto
            {
                EstaAutenticado = estaAutenticado,
                LoginNormalizado = identity?.Name ?? string.Empty
            };

            if (session != null)
            {
                // Extraer UsuarioId
                var userIdObj = session["UserId"] ?? session["IdUsuario"] ?? session["CodigoUsuario"];
                if (userIdObj != null && int.TryParse(userIdObj.ToString(), out int userId) && userId > 0)
                {
                    contexto.UsuarioId = userId;
                }

                // Extraer Nombre
                var nombre = session["NombreUsuario"] as string;
                contexto.Nombre = string.IsNullOrWhiteSpace(nombre) ? (estaAutenticado ? identity.Name : "ANONIMO") : nombre.Trim();

                // Extraer Roles
                var rol = session["Rol"] as string;
                if (!string.IsNullOrWhiteSpace(rol))
                {
                    contexto.Roles.Add(rol.Trim().ToUpperInvariant());
                }

                // Extraer Compañia
                var ciaObj = session["CompaniaActivaId"] ?? session["CodigoCompania"];
                if (ciaObj != null && int.TryParse(ciaObj.ToString(), out int ciaId) && ciaId > 0)
                {
                    contexto.CompaniaActivaId = ciaId;
                }
            }

            return contexto;
        }

        public void ValidarAutenticacion()
        {
            var contexto = ObtenerContextoActual();
            if (!contexto.EstaAutenticado || contexto.UsuarioId <= 0)
            {
                throw new HttpException(401, "No autenticado o sesión expirada.");
            }
        }

        public void ValidarRol(params string[] rolesPermitidos)
        {
            ValidarAutenticacion();

            if (rolesPermitidos == null || rolesPermitidos.Length == 0)
                return;

            var contexto = ObtenerContextoActual();
            bool tieneRol = contexto.Roles.Any(r => rolesPermitidos.Select(p => p.ToUpperInvariant()).Contains(r));

            if (!tieneRol)
            {
                throw new HttpException(403, "No tiene permisos para realizar esta acción.");
            }
        }
    }
}
