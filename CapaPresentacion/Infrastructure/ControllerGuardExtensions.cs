using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaNegocio.Integraciones.As400Sync;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Infrastructure
{
    public enum AuthenticatedSessionBootstrapStatus
    {
        Unchanged,
        Restored,
        RequiresCompanySelection,
        Failed
    }

    public static class ControllerGuardExtensions
    {
        private static readonly IUserContextAccessor _userContext = new UserContextAccessor();

        public static bool TryGetSessionUserId(this Controller controller, out int userId)
        {
            userId = 0;
            if (controller == null)
            {
                return false;
            }

            return _userContext.TryGetUserId(controller.Session, out userId);
        }

        public static bool TryGetSessionCodigoUsuario(this Controller controller, out int codigoUsuario)
        {
            codigoUsuario = 0;
            if (controller == null)
            {
                return false;
            }

            return _userContext.TryGetCodigoUsuario(controller.Session, out codigoUsuario);
        }

        public static JsonResult JsonContextMissing(this Controller controller, string message)
        {
            var safeMessage = string.IsNullOrWhiteSpace(message) ? "SesiÃ³n expirada." : message.Trim();
            return new JsonResult
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = new
                {
                    ok = false,
                    success = false,
                    code = "CONTEXT_MISSING",
                    message = safeMessage,
                    mensaje = safeMessage,
                    data = (object)null
                }
            };
        }

        public static AuthenticatedSessionBootstrapStatus EnsureAuthenticatedSession(this Controller controller)
        {
            return AuthenticatedSessionBootstrapper.EnsureSession(controller != null ? controller.HttpContext : null);
        }
    }

    public static class AuthenticatedSessionBootstrapper
    {
        public static AuthenticatedSessionBootstrapStatus EnsureSession(HttpContextBase httpContext)
        {
            if (httpContext == null || httpContext.Session == null)
            {
                return AuthenticatedSessionBootstrapStatus.Unchanged;
            }

            var principal = httpContext.User;
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return AuthenticatedSessionBootstrapStatus.Unchanged;
            }

            var session = httpContext.Session;
            var hasBaseline = HasSessionBaseline(session);
            var needsCompanyReview = string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerCodigo(session));

            if (hasBaseline && !needsCompanyReview)
            {
                return AuthenticatedSessionBootstrapStatus.Unchanged;
            }

            Usuario usuario;
            List<string> roles;
            string loginUsado;
            if (!TryResolveAuthenticatedUser(httpContext, out usuario, out roles, out loginUsado))
            {
                return AuthenticatedSessionBootstrapStatus.Failed;
            }

            SincronizarSesionAutenticada(session, usuario, roles, loginUsado);

            var companyStatus = EnsureCompaniaActiva(session, usuario, roles);
            if (companyStatus == AuthenticatedSessionBootstrapStatus.RequiresCompanySelection)
            {
                return companyStatus;
            }

            if (!hasBaseline || companyStatus == AuthenticatedSessionBootstrapStatus.Restored)
            {
                return AuthenticatedSessionBootstrapStatus.Restored;
            }

            return AuthenticatedSessionBootstrapStatus.Unchanged;
        }

        private static bool HasSessionBaseline(HttpSessionStateBase session)
        {
            int userId;
            if (!TryGetUserId(session, out userId) || userId <= 0)
            {
                return false;
            }

            var nombreUsuario = (session["NombreUsuario"] as string ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombreUsuario))
            {
                return false;
            }

            return ObtenerRolesSesion(session).Count > 0;
        }

        private static bool TryResolveAuthenticatedUser(
            HttpContextBase httpContext,
            out Usuario usuario,
            out List<string> roles,
            out string loginUsado)
        {
            usuario = null;
            roles = ObtenerRolesSesion(httpContext.Session);
            loginUsado = (httpContext.Session["CodigoUsuario"] as string ?? string.Empty).Trim();

            int usuarioId;
            if (TryGetUserId(httpContext.Session, out usuarioId) && usuarioId > 0)
            {
                try
                {
                    usuario = UsuarioDAO.ObtenerPorId(usuarioId);
                }
                catch
                {
                    usuario = null;
                }

                if (usuario != null && usuario.Id > 0)
                {
                    roles = CompletarRolesUsuario(usuario, roles);
                    loginUsado = !string.IsNullOrWhiteSpace(usuario.CodigoUsuario)
                        ? usuario.CodigoUsuario.Trim()
                        : loginUsado;
                    return true;
                }
            }

            var identidades = new List<string>();
            if (!string.IsNullOrWhiteSpace(loginUsado))
            {
                identidades.Add(loginUsado);
            }

            try
            {
                if (httpContext.User != null &&
                    httpContext.User.Identity != null &&
                    httpContext.User.Identity.IsAuthenticated)
                {
                    identidades.Add(httpContext.User.Identity.Name);
                }

                if (httpContext.Request != null && httpContext.Request.LogonUserIdentity != null)
                {
                    identidades.Add(httpContext.Request.LogonUserIdentity.Name);
                }
            }
            catch
            {
            }

            foreach (var identidad in identidades
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Usuario usuarioPorLogin;
                if (!TryResolverUsuarioPorLogin(identidad, out usuarioPorLogin))
                {
                    continue;
                }

                usuario = usuarioPorLogin;
                roles = CompletarRolesUsuario(usuario, roles);
                loginUsado = identidad;
                return true;
            }

            return false;
        }

        private static bool TryGetUserId(HttpSessionStateBase session, out int userId)
        {
            userId = 0;
            if (session == null)
            {
                return false;
            }

            var value = session["UserId"] ?? session["IdUsuario"];
            return value != null && int.TryParse(value.ToString(), out userId) && userId > 0;
        }

        private static List<string> ObtenerRolesSesion(HttpSessionStateBase session)
        {
            var roles = new List<string>();
            if (session == null)
            {
                return roles;
            }

            try
            {
                roles.AddRange(RoleGroupingHelper.ExtractRoles(session["RolesRaw"]));
                roles.AddRange(RoleGroupingHelper.ExtractRoles(session["Roles"]));

                var rolUnico = session["Rol"] as string;
                if (!string.IsNullOrWhiteSpace(rolUnico))
                {
                    roles.Add(rolUnico);
                }
            }
            catch
            {
            }

            return roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> CompletarRolesUsuario(Usuario usuario, IEnumerable<string> rolesBase)
        {
            var roles = (rolesBase ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (roles.Count == 0 && usuario != null && usuario.Id > 0)
            {
                try
                {
                    roles = UsuarioDAO.ObtenerRoles(usuario.Id) ?? new List<string>();
                }
                catch
                {
                    roles = new List<string>();
                }
            }

            if (roles.Count == 0 && usuario != null && !string.IsNullOrWhiteSpace(usuario.Rol))
            {
                roles.Add(usuario.Rol.Trim());
            }

            return roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void SincronizarSesionAutenticada(
            HttpSessionStateBase session,
            Usuario usuario,
            IEnumerable<string> roles,
            string loginFallback)
        {
            if (session == null || usuario == null || usuario.Id <= 0)
            {
                return;
            }

            session["UserId"] = usuario.Id;
            session["IdUsuario"] = usuario.Id;
            session["CodigoUsuario"] = !string.IsNullOrWhiteSpace(usuario.CodigoUsuario)
                ? usuario.CodigoUsuario.Trim()
                : (loginFallback ?? usuario.NombreUsuario ?? string.Empty).Trim();

            session["NombreUsuario"] = !string.IsNullOrWhiteSpace(usuario.NombreCompleto)
                ? usuario.NombreCompleto.Trim()
                : (!string.IsNullOrWhiteSpace(usuario.NombreUsuario)
                    ? usuario.NombreUsuario.Trim()
                    : "Usuario");

            session["Correo"] = !string.IsNullOrWhiteSpace(usuario.Email)
                ? usuario.Email.Trim()
                : (session["Correo"] as string ?? string.Empty).Trim();

            var rolesRaw = (roles ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var rolesUnificados = RoleGroupingHelper.BuildUnifiedRoles(rolesRaw);
            var rolActual = RoleGroupingHelper.NormalizeSelectedRole(session["Rol"] as string ?? string.Empty);
            var rolSeleccionado = rolesUnificados.FirstOrDefault(r =>
                string.Equals(r, rolActual, StringComparison.OrdinalIgnoreCase));

            session["RolesRaw"] = rolesRaw;
            session["Roles"] = rolesUnificados;
            session["Rol"] = !string.IsNullOrWhiteSpace(rolSeleccionado)
                ? rolSeleccionado
                : (rolesUnificados.Count > 0 ? rolesUnificados[0] : null);
            session.Timeout = SessionTimeoutHelper.GetTimeoutMinutes();
            session["LastActivity"] = DateTime.Now;
        }

        private static AuthenticatedSessionBootstrapStatus EnsureCompaniaActiva(
            HttpSessionStateBase session,
            Usuario usuario,
            IEnumerable<string> roles)
        {
            if (session == null || usuario == null || usuario.Id <= 0)
            {
                return AuthenticatedSessionBootstrapStatus.Unchanged;
            }

            var codigoActivo = CompaniaActivaSessionHelper.ObtenerCodigo(session);
            if (!EsUsuarioRt(usuario) || EsUsuarioAdministrador(usuario, roles))
            {
                if (!string.IsNullOrWhiteSpace(codigoActivo))
                {
                    if (string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerNombre(session)))
                    {
                        CompaniaActivaSessionHelper.Establecer(
                            session,
                            codigoActivo,
                            ResolverNombreCompaniaPorCodigo(codigoActivo));
                        return AuthenticatedSessionBootstrapStatus.Restored;
                    }

                    return AuthenticatedSessionBootstrapStatus.Unchanged;
                }

                var codigoEmpresa = ParsearCodigosCompaniaLegacy(usuario.EmpresaCodigo).FirstOrDefault()
                    ?? (usuario.EmpresaCodigo ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(codigoEmpresa))
                {
                    return AuthenticatedSessionBootstrapStatus.Unchanged;
                }

                CompaniaActivaSessionHelper.Establecer(
                    session,
                    codigoEmpresa,
                    ResolverNombreCompaniaPorCodigo(codigoEmpresa));
                return AuthenticatedSessionBootstrapStatus.Restored;
            }

            var companias = ObtenerCompaniasAsignadasConFallback(usuario);
            if (companias.Count == 0)
            {
                return AuthenticatedSessionBootstrapStatus.Unchanged;
            }

            if (companias.Count == 1)
            {
                EstablecerCompaniaActiva(session, companias[0]);
                return AuthenticatedSessionBootstrapStatus.Restored;
            }

            var companiaActiva = companias.FirstOrDefault(c =>
                string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigoActivo, StringComparison.OrdinalIgnoreCase));

            if (companiaActiva == null)
            {
                return AuthenticatedSessionBootstrapStatus.RequiresCompanySelection;
            }

            if (string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerNombre(session)))
            {
                EstablecerCompaniaActiva(session, companiaActiva);
                return AuthenticatedSessionBootstrapStatus.Restored;
            }

            return AuthenticatedSessionBootstrapStatus.Unchanged;
        }

        private static bool TryResolverUsuarioPorLogin(string loginInput, out Usuario usuario)
        {
            usuario = null;

            foreach (var candidato in ExpandirCandidatosLogin(loginInput))
            {
                try
                {
                    usuario = UsuarioDAO.ObtenerPorNombreUsuario(candidato);
                }
                catch
                {
                    usuario = null;
                }

                if (usuario != null && usuario.Id > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> ExpandirCandidatosLogin(string valor)
        {
            var bruto = (valor ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(bruto))
            {
                return Enumerable.Empty<string>();
            }

            var candidatos = new List<string> { bruto };

            if (bruto.Contains("\\"))
            {
                var afterSlash = bruto.Substring(bruto.LastIndexOf("\\", StringComparison.Ordinal) + 1).Trim();
                if (!string.IsNullOrWhiteSpace(afterSlash))
                {
                    candidatos.Add(afterSlash);
                }
            }

            if (bruto.Contains("/"))
            {
                var afterForwardSlash = bruto.Substring(bruto.LastIndexOf("/", StringComparison.Ordinal) + 1).Trim();
                if (!string.IsNullOrWhiteSpace(afterForwardSlash))
                {
                    candidatos.Add(afterForwardSlash);
                }
            }

            if (bruto.Contains("@"))
            {
                var localPart = bruto.Split('@')[0].Trim();
                if (!string.IsNullOrWhiteSpace(localPart))
                {
                    candidatos.Add(localPart);
                }
            }

            return candidatos
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool EsUsuarioRt(Usuario usuario)
        {
            return usuario != null &&
                   !string.IsNullOrWhiteSpace(usuario.EstadoDesignacionRT) &&
                   usuario.EstadoDesignacionRT.Trim().Equals("aceptado", StringComparison.OrdinalIgnoreCase);
        }

        private static bool EsUsuarioAdministrador(Usuario usuario, IEnumerable<string> roles)
        {
            var rolesNorm = (roles ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList();

            if (rolesNorm.Any(r => r.Equals("Administrador", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (usuario != null &&
                !string.IsNullOrWhiteSpace(usuario.NombreUsuario) &&
                usuario.NombreUsuario.Trim().Equals("USU_ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (usuario != null &&
                !string.IsNullOrWhiteSpace(usuario.Email) &&
                usuario.Email.Trim().Equals("gercavi82@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static void EstablecerCompaniaActiva(HttpSessionStateBase session, UsuarioCompaniaRT compania)
        {
            if (session == null || compania == null || string.IsNullOrWhiteSpace(compania.CompaniaCodigo))
            {
                return;
            }

            var codigo = compania.CompaniaCodigo.Trim();
            var nombre = !string.IsNullOrWhiteSpace(compania.CompaniaNombre)
                ? compania.CompaniaNombre.Trim()
                : ResolverNombreCompaniaPorCodigo(codigo);

            CompaniaActivaSessionHelper.Establecer(session, codigo, nombre);
        }

        private static List<UsuarioCompaniaRT> ObtenerCompaniasAsignadasConFallback(Usuario usuario)
        {
            var resultado = new List<UsuarioCompaniaRT>();
            if (usuario == null || usuario.Id <= 0)
            {
                return resultado;
            }

            try
            {
                var daoCompanias = new UsuarioCompaniaRTDAO();
                resultado = daoCompanias.ObtenerCompaniasAsignadas(usuario.Id) ?? new List<UsuarioCompaniaRT>();
            }
            catch
            {
                resultado = new List<UsuarioCompaniaRT>();
            }

            foreach (var codigo in ParsearCodigosCompaniaLegacy(usuario.EmpresaCodigo))
            {
                if (resultado.Any(c => string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                resultado.Add(new UsuarioCompaniaRT
                {
                    UsuarioId = usuario.Id,
                    CompaniaCodigo = codigo,
                    CompaniaNombre = ResolverNombreCompaniaPorCodigo(codigo),
                    Activo = true
                });
            }

            if (!string.IsNullOrWhiteSpace(usuario.Email))
            {
                try
                {
                    var declaracionDao = new DeclaracionTemporalDAO();
                    var historial = declaracionDao.GetUltimaAceptadaHistorial(usuario.Email);
                    var codigosHistorial = historial != null
                        ? ParsearCodigosCompaniaLegacy(historial.EmpresaCodigo)
                        : new List<string>();

                    if (codigosHistorial.Count == 0 && historial != null)
                    {
                        codigosHistorial = ExtraerCodigosCompaniaDesdeTexto(historial.EmpresaNombre);
                    }

                    foreach (var codigo in codigosHistorial)
                    {
                        if (resultado.Any(c => string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        resultado.Add(new UsuarioCompaniaRT
                        {
                            UsuarioId = usuario.Id,
                            CompaniaCodigo = codigo,
                            CompaniaNombre = ResolverNombreCompaniaPorCodigo(codigo),
                            Activo = true
                        });
                    }
                }
                catch
                {
                }
            }

            return resultado
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                .GroupBy(c => (c.CompaniaCodigo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.CompaniaCodigo)
                .ToList();
        }

        private static List<string> ParsearCodigosCompaniaLegacy(string empresaCodigo)
        {
            if (string.IsNullOrWhiteSpace(empresaCodigo))
            {
                return new List<string>();
            }

            return (empresaCodigo ?? string.Empty)
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> ExtraerCodigosCompaniaDesdeTexto(string texto)
        {
            var resultado = new List<string>();
            var raw = (texto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return resultado;
            }

            foreach (Match match in Regex.Matches(raw, "\\[(?<code>[A-Za-z0-9]+)(?:/[^\\]]*)?\\]"))
            {
                var codigo = (match.Groups["code"].Value ?? string.Empty).Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    resultado.Add(codigo);
                }
            }

            return resultado
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ResolverNombreCompaniaPorCodigo(string codigoEmpresa)
        {
            if (string.IsNullOrWhiteSpace(codigoEmpresa))
            {
                return string.Empty;
            }

            try
            {
                string nombreEmpresa = null;
                bool preferirMirror;
                if (bool.TryParse(ConfigurationManager.AppSettings["Sync:Mirror:PreferReadForEmpresas"], out preferirMirror) &&
                    preferirMirror)
                {
                    var mirror = new MirrorReadService();
                    var empresaMirror = mirror.ObtenerCompaniaPorCodigo(codigoEmpresa);

                    if (empresaMirror != null && !string.IsNullOrWhiteSpace(empresaMirror.NombreCompania))
                    {
                        nombreEmpresa = empresaMirror.NombreCompania.Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(nombreEmpresa))
                {
                    var empresaDao = new EmpresaAS400DAO(new SecureConfigurationService());
                    var empresa = empresaDao.ObtenerEmpresaPorCodigo(codigoEmpresa);
                    if (empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre))
                    {
                        nombreEmpresa = empresa.Nombre.Trim();
                    }
                }

                return nombreEmpresa ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
