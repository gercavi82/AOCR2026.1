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

        private string NormalizarLogin(string login)
        {
            if (string.IsNullOrWhiteSpace(login)) return string.Empty;
            login = login.Trim().ToLowerInvariant();
            
            int idxDomain = login.IndexOf('\\');
            if (idxDomain >= 0) login = login.Substring(idxDomain + 1);

            int idxAt = login.IndexOf('@');
            if (idxAt > 0) login = login.Substring(0, idxAt);

            return login.Trim();
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
            bool estaAutenticado = identity != null && identity.IsAuthenticated;
            string loginOriginal = identity?.Name ?? string.Empty;
            string loginNormalizado = NormalizarLogin(loginOriginal);

            var contexto = new UsuarioContexto
            {
                EstaAutenticado = estaAutenticado,
                LoginNormalizado = loginNormalizado
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
                contexto.Nombre = string.IsNullOrWhiteSpace(nombre) ? (estaAutenticado ? loginNormalizado : "ANONIMO") : nombre.Trim();

                // Extraer Correo
                contexto.Correo = (session["Correo"] as string ?? string.Empty).Trim();

                // Extraer Roles
                var rol = session["Rol"] as string;
                if (!string.IsNullOrWhiteSpace(rol))
                {
                    contexto.RolSeleccionado = rol.Trim().ToUpperInvariant();
                    contexto.Roles.Add(contexto.RolSeleccionado);
                }
                
                var rolesList = session["Roles"] as List<string>;
                if (rolesList != null)
                {
                    foreach (var r in rolesList)
                    {
                        if (!contexto.Roles.Contains(r.ToUpperInvariant()))
                            contexto.Roles.Add(r.ToUpperInvariant());
                    }
                }

                // Extraer Compañia
                var ciaObj = session["CompaniaActivaId"] ?? session["CodigoCompania"];
                if (ciaObj != null && int.TryParse(ciaObj.ToString(), out int ciaId) && ciaId > 0)
                {
                    contexto.CompaniaActivaId = ciaId;
                }
            }

            // Fallback a DAO si falta info vital pero estamos autenticados
            if (estaAutenticado && (contexto.UsuarioId <= 0 || !contexto.Roles.Any() || string.IsNullOrWhiteSpace(contexto.Correo)))
            {
                var usrDb = CapaDatos.DAOs.UsuarioDAO.ObtenerPorNombreUsuario(loginNormalizado);
                if (usrDb != null && usrDb.Id > 0)
                {
                    if (contexto.UsuarioId <= 0) contexto.UsuarioId = usrDb.Id;
                    if (string.IsNullOrWhiteSpace(contexto.Nombre)) contexto.Nombre = usrDb.NombreCompleto ?? usrDb.NombreUsuario;
                    if (string.IsNullOrWhiteSpace(contexto.Correo)) contexto.Correo = usrDb.Email;
                    
                    var dbRoles = CapaDatos.DAOs.UsuarioDAO.ObtenerRoles(usrDb.Id);
                    foreach (var r in dbRoles)
                    {
                        string rUpper = r.ToUpperInvariant();
                        if (!contexto.Roles.Contains(rUpper))
                        {
                            contexto.Roles.Add(rUpper);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(contexto.RolSeleccionado) && contexto.Roles.Any())
                    {
                        contexto.RolSeleccionado = contexto.Roles.First();
                    }

                    contexto.CodigoInstitucional = usrDb.CodigoUsuario;
                    contexto.IdentificadorInstitucional = usrDb.Ruc ?? usrDb.CodigoUsuario;

                    // Poblar la sesión si era nula o le faltaba info (para evitar consultas futuras repetidas en la misma sesión si es WebForms/MVC tradicional)
                    if (session != null && session["UserId"] == null)
                    {
                        session["UserId"] = contexto.UsuarioId;
                        session["IdUsuario"] = contexto.UsuarioId;
                        session["CodigoUsuario"] = contexto.UsuarioId;
                        session["NombreUsuario"] = contexto.Nombre;
                        session["Correo"] = contexto.Correo;
                        session["Rol"] = contexto.RolSeleccionado;
                        session["Roles"] = contexto.Roles;
                    }
                }
                else
                {
                    // Usuario no encontrado en BD. Podría ser un error de estado.
                    contexto.EstaAutenticado = false; 
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
