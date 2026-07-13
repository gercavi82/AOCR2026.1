using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using CapaDatos.Services;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;

namespace CapaPresentacion.Services
{
    public sealed class UsuarioContextoService : IUsuarioContextoService
    {
        internal const string RequestCacheKey = "AOCR_USUARIO_CONTEXT";

        private readonly Func<HttpContextBase> _httpContextResolver;
        private readonly ILoggingService _logger;

        public UsuarioContextoService()
            : this(ResolverHttpContextActual, LoggingServiceFactory.Create())
        {
        }

        public UsuarioContextoService(Func<HttpContextBase> httpContextResolver, ILoggingService logger)
        {
            _httpContextResolver = httpContextResolver ?? ResolverHttpContextActual;
            _logger = logger ?? LoggingServiceFactory.Create();
        }

        public UsuarioContextoDto ObtenerContextoActual()
        {
            var httpContext = _httpContextResolver();
            if (httpContext == null)
            {
                throw CrearExcepcion("No existe un contexto HTTP disponible.", null, null);
            }

            var cache = httpContext.Items[RequestCacheKey] as UsuarioContextoDto;
            if (cache != null)
            {
                return cache;
            }

            var autenticado = httpContext.User != null
                && httpContext.User.Identity != null
                && httpContext.User.Identity.IsAuthenticated;
            if (!autenticado)
            {
                throw CrearExcepcion("El usuario no esta autenticado.", httpContext, null);
            }

            try
            {
                AuthenticatedSessionBootstrapper.EnsureSession(httpContext);
                var auth = AocrAuthorizationContextFactory.Build(httpContext);
                if (auth == null || auth.UserId <= 0)
                {
                    throw CrearExcepcion("No fue posible resolver el identificador del usuario autenticado.", httpContext, null);
                }

                SincronizarClavesUsuarioId(httpContext, auth.UserId);
                var rolesRaw = auth.RawRoles ?? new List<string>();
                var roles = auth.Roles ?? new List<string>();
                var rolActivo = RoleGroupingHelper.NormalizeSelectedRole(auth.SelectedRole);
                var contexto = new UsuarioContextoDto
                {
                    UsuarioId = auth.UserId,
                    Login = !string.IsNullOrWhiteSpace(auth.CodigoUsuario)
                        ? auth.CodigoUsuario.Trim()
                        : httpContext.User.Identity.Name,
                    NombreCompleto = auth.UserName,
                    Correo = httpContext.Session != null ? Convert.ToString(httpContext.Session["Correo"]) : string.Empty,
                    RolActivo = rolActivo,
                    Roles = roles,
                    RolesRaw = rolesRaw,
                    CompaniaCodigo = auth.CompanyCode,
                    CompaniaNombre = auth.CompanyName,
                    EstaAutenticado = true,
                    EsValido = true,
                    EsAdministrador = RoleGroupingHelper.IsAdministrador(rolActivo),
                    EsCoordinacion = RoleGroupingHelper.IsCoordinacion(rolActivo),
                    EsInspectorTecnico = RoleGroupingHelper.IsInspectorTecnico(rolActivo),
                    EsFinanciero = RoleGroupingHelper.IsFinanciero(rolActivo),
                    EsDireccionJefaturaTecnica = RoleGroupingHelper.IsDireccionJefaturaTecnica(rolActivo),
                    EsSolicitante = RoleGroupingHelper.IsSolicitante(rolActivo),
                    EsLegal = RoleGroupingHelper.IsCoordinacion(rolActivo)
                        && rolesRaw.Any(r => RoleGroupingHelper.HasAnyRawRole(new[] { r }, "CoordinacionLegal", "CoordinadorLegal"))
                };

                httpContext.Items[RequestCacheKey] = contexto;
                LogInfo(string.Format(
                    "[AUTH][CONTEXT_OK] UsuarioId={0}; Login={1}; Rol={2}; Compania={3}; Path={4}",
                    contexto.UsuarioId,
                    contexto.Login ?? string.Empty,
                    contexto.RolActivo ?? string.Empty,
                    contexto.CompaniaCodigo ?? string.Empty,
                    ObtenerPath(httpContext)));
                return contexto;
            }
            catch (UsuarioContextoInvalidoException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CrearExcepcion("Ocurrio un error al reconstruir el contexto del usuario.", httpContext, ex);
            }
        }

        public bool TryObtenerContextoActual(out UsuarioContextoDto contexto)
        {
            try
            {
                contexto = ObtenerContextoActual();
                return contexto != null && contexto.EsValido;
            }
            catch (UsuarioContextoInvalidoException)
            {
                contexto = null;
                return false;
            }
        }

        public int ObtenerUsuarioId()
        {
            return ObtenerContextoActual().UsuarioId;
        }

        public void InvalidarCache()
        {
            var httpContext = _httpContextResolver();
            if (httpContext != null)
            {
                httpContext.Items.Remove(RequestCacheKey);
            }
        }

        private UsuarioContextoInvalidoException CrearExcepcion(string mensaje, HttpContextBase httpContext, Exception innerException)
        {
            try
            {
                _logger.LogWarning(string.Format(
                    "[AUTH][CONTEXT_ERROR] Identity={0}; Path={1}; Detalle={2}",
                    httpContext != null && httpContext.User != null && httpContext.User.Identity != null
                        ? httpContext.User.Identity.Name
                        : string.Empty,
                    ObtenerPath(httpContext),
                    mensaje));
            }
            catch
            {
            }

            return innerException == null
                ? new UsuarioContextoInvalidoException(mensaje)
                : new UsuarioContextoInvalidoException(mensaje, innerException);
        }

        private void LogInfo(string mensaje)
        {
            try
            {
                _logger.LogInfo(mensaje);
            }
            catch
            {
            }
        }

        private static void SincronizarClavesUsuarioId(HttpContextBase httpContext, int usuarioId)
        {
            if (httpContext == null || httpContext.Session == null || usuarioId <= 0)
            {
                return;
            }

            httpContext.Session["UsuarioId"] = usuarioId;
            httpContext.Session["UserId"] = usuarioId;
            httpContext.Session["IdUsuario"] = usuarioId;
        }

        private static string ObtenerPath(HttpContextBase httpContext)
        {
            return httpContext != null && httpContext.Request != null
                ? httpContext.Request.Path
                : string.Empty;
        }

        private static HttpContextBase ResolverHttpContextActual()
        {
            return HttpContext.Current != null ? new HttpContextWrapper(HttpContext.Current) : null;
        }
    }
}
