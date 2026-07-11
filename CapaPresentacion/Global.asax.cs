using System;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using System.Web.Helpers;
using Dapper;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Services;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Filters;

namespace CapaPresentacion
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static EmailQueueProcessor _emailProcessor;
        private const string PerfStopwatchKey = "__AocrPerfStopwatch";
        private const string PerfLabelKey = "__AocrPerfLabel";
        private const string AjaxStopwatchKey = "__AocrAjaxStopwatch";
        private const string AjaxCorrelationKey = "__AocrAjaxCorrelation";
        private static readonly CapaDatos.Services.ILoggingService PerfLogger = CapaDatos.Services.LoggingServiceFactory.Create();

        protected void Application_Start()
        {
            var totalStopwatch = Stopwatch.StartNew();
            PerfLogger.LogInfo("[PERF][APP_START] Inicio Application_Start");

            // Dapper
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
            // Evita colisiones de antiforgery con otras apps en localhost.
            AntiForgeryConfig.CookieName = "__AOCR_RequestVerificationToken";
            // Suprime la heurística de identidad: evita HttpAntiForgeryException
            // "token creado para usuario distinto" al cambiar de sesión.
            // La protección sigue activa: el cookie + campo oculto se validan.
            AntiForgeryConfig.SuppressIdentityHeuristicChecks = true;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Configurar Dependency Injection con Unity
            var unityStopwatch = Stopwatch.StartNew();
            UnityConfig.RegisterComponents();
            PerfLogger.LogInfo(string.Format(
                "[PERF][APP_START] UnityConfig.RegisterComponents completado en {0} ms",
                unityStopwatch.ElapsedMilliseconds));

            // Iniciar procesador de cola de correos
            var emailStopwatch = Stopwatch.StartNew();
            IniciarProcesadorEmail();
            PerfLogger.LogInfo(string.Format(
                "[PERF][APP_START] IniciarProcesadorEmail completado en {0} ms",
                emailStopwatch.ElapsedMilliseconds));
            PerfLogger.LogInfo(string.Format(
                "[PERF][APP_START] Fin Application_Start. Total={0} ms",
                totalStopwatch.ElapsedMilliseconds));
        }

        protected void Application_BeginRequest()
        {
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            Request.ContentEncoding = System.Text.Encoding.UTF8;

            if (IsAjaxLikeRequest(Request))
            {
                var correlationId = Request.Headers["X-Correlation-ID"];
                if (string.IsNullOrWhiteSpace(correlationId)) correlationId = Guid.NewGuid().ToString("N");
                Context.Items[AjaxCorrelationKey] = correlationId;
                Context.Items[AjaxStopwatchKey] = Stopwatch.StartNew();
                Response.Headers["X-Correlation-ID"] = correlationId;

                PerfLogger.LogInfo(string.Format(
                    "[AJAX][REQUEST_IN] CorrelationId={0}; Method={1}; Url={2}; Query={3}; Authenticated={4}",
                    correlationId,
                    Request.HttpMethod,
                    Request.Url != null ? Request.Url.AbsolutePath : Request.Path,
                    Request.Url != null ? Request.Url.Query : string.Empty,
                    Request.IsAuthenticated));

                if (IsNotificationCountRequest(Request))
                {
                    PerfLogger.LogInfo(string.Format(
                        "[NOTIFICACIONES][CONTAR_IN] CorrelationId={0}; UsuarioId=; Rol=; CompaniaActiva=; Authenticated={1}; Origen=Pipeline",
                        correlationId,
                        Request.IsAuthenticated));
                }
            }

            if (IsWellKnownRequest(Request))
            {
                Context.SkipAuthorization = true;
                Response.SuppressFormsAuthenticationRedirect = true;

                var physicalPath = Request.PhysicalPath;
                if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
                {
                    Response.TrySkipIisCustomErrors = true;
                    Response.StatusCode = 404;
                    Response.StatusDescription = "Not Found";
                    Response.ContentType = "text/plain";
                    Response.Write("Not Found");
                    CompleteRequest();
                    return;
                }
            }

            string perfLabel;
            if (TryResolvePerfLabel(Request, out perfLabel))
            {
                Context.Items[PerfStopwatchKey] = Stopwatch.StartNew();
                Context.Items[PerfLabelKey] = perfLabel;
                PerfLogger.LogInfo(string.Format(
                    "{0} Request start {1} {2}",
                    perfLabel,
                    Request.HttpMethod,
                    Request.RawUrl));
            }
        }

        protected void Application_PreSendRequestHeaders()
        {
            try
            {
                var context = HttpContext.Current;
                if (context == null || context.Items[AjaxCorrelationKey] == null) return;

                var request = context.Request;
                var response = context.Response;
                var route = RouteTable.Routes.GetRouteData(new HttpContextWrapper(context));
                var correlationId = Convert.ToString(context.Items[AjaxCorrelationKey]);
                var stopwatch = context.Items[AjaxStopwatchKey] as Stopwatch;
                var exception = context.Items[AjaxResponseMetadataFilter.ExceptionKey] as Exception;
                var internalCode = context.Items[AjaxResponseMetadataFilter.InternalCodeKey];
                var session = context.Session;
                var identity = context.User != null ? context.User.Identity : null;
                var controller = route != null ? Convert.ToString(route.Values["controller"]) : string.Empty;
                var action = route != null ? Convert.ToString(route.Values["action"]) : string.Empty;
                var role = session != null ? Convert.ToString(session["Rol"] ?? session["RolActivo"] ?? session["SelectedRole"]) : string.Empty;
                var company = session != null ? Convert.ToString(session["CompaniaActivaCodigo"] ?? session["CodigoCompania"] ?? session["CompaniaCodigo"]) : string.Empty;
                var status = response != null ? response.StatusCode : 0;
                if (internalCode == null && (status == 401 || status == 403)) internalCode = status;

                var message = string.Format(
                    "[{0}] CorrelationId={1}; Method={2}; Url={3}; Query={4}; User={5}; Authenticated={6}; Rol={7}; CompaniaActiva={8}; Controller={9}; Action={10}; HttpStatus={11}; Code={12}; DurationMs={13}; Exception={14}",
                    exception == null ? "AJAX][REQUEST_OUT" : "AJAX][REQUEST_ERROR",
                    correlationId,
                    request != null ? request.HttpMethod : string.Empty,
                    request != null && request.Url != null ? request.Url.AbsolutePath : string.Empty,
                    request != null && request.Url != null ? request.Url.Query : string.Empty,
                    identity != null ? identity.Name : string.Empty,
                    identity != null && identity.IsAuthenticated,
                    role,
                    company,
                    controller,
                    action,
                    status,
                    internalCode ?? string.Empty,
                    stopwatch != null ? stopwatch.ElapsedMilliseconds : 0,
                    exception != null ? exception.GetType().Name + ": " + exception.Message : string.Empty);

                if (exception != null) PerfLogger.LogError(message);
                else PerfLogger.LogInfo(message);

                if (IsNotificationCountRequest(request))
                {
                    PerfLogger.LogInfo(string.Format(
                        "[NOTIFICACIONES][CONTAR_OUT] CorrelationId={0}; HttpStatus={1}; Code={2}; Total=; Motivo={3}; Origen=Pipeline",
                        correlationId,
                        status,
                        internalCode ?? string.Empty,
                        status == 200 ? "OK" : (status == 401 ? "Sesion no activa" : "Respuesta no exitosa")));
                }
            }
            catch (Exception ex)
            {
                PerfLogger.LogError("[AJAX][REQUEST_ERROR] No se pudo completar la traza AJAX. " + ex.Message);
            }
        }

        private static bool IsNotificationCountRequest(HttpRequest request)
        {
            var path = request != null && request.Url != null ? request.Url.AbsolutePath : string.Empty;
            return path.EndsWith("/Notificacion/ContarNoLeidas", StringComparison.OrdinalIgnoreCase);
        }

        protected void Application_EndRequest()
        {
            try
            {
                var context = HttpContext.Current;
                if (context == null)
                {
                    return;
                }

                var response = context.Response;
                var request = context.Request;
                if (response == null || request == null)
                {
                    return;
                }

                var perfStopwatch = context.Items[PerfStopwatchKey] as Stopwatch;
                var perfLabel = context.Items[PerfLabelKey] as string;
                if (perfStopwatch != null && !string.IsNullOrWhiteSpace(perfLabel))
                {
                    PerfLogger.LogInfo(string.Format(
                        "{0} Request end {1} {2} => {3} ({4} ms)",
                        perfLabel,
                        request.HttpMethod,
                        request.RawUrl,
                        response.StatusCode,
                        perfStopwatch.ElapsedMilliseconds));
                }

                if (TryNormalizeAuthenticatedUnauthorizedResponse(context, request, response))
                {
                    return;
                }

                if (TryNormalizeAuthenticatedLoginRedirect(context, request, response))
                {
                    return;
                }

                if (TryNormalizeAjaxLoginRedirect(context, request, response))
                {
                    return;
                }

                if (response.StatusCode != 400)
                {
                    return;
                }

                var path = request.Url != null ? request.Url.AbsolutePath : string.Empty;
                if (string.IsNullOrWhiteSpace(path) ||
                    path.IndexOf("/Financiero/AprobarYEnviarAS400", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return;
                }

                var user = context.User != null && context.User.Identity != null && context.User.Identity.IsAuthenticated
                    ? context.User.Identity.Name
                    : "ANON";

                var formKeys = request.Form != null
                    ? string.Join(",", request.Form.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                    : string.Empty;
                var queryKeys = request.QueryString != null
                    ? string.Join(",", request.QueryString.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                    : string.Empty;

                var headerToken = request.Headers["RequestVerificationToken"] ??
                                 request.Headers["__RequestVerificationToken"] ??
                                 request.Headers["X-CSRF-TOKEN"];
                var formToken = request.Form["__RequestVerificationToken"];
                var tokenInfo = string.Format("HdrTokenLen={0},FormTokenLen={1}",
                    string.IsNullOrWhiteSpace(headerToken) ? 0 : headerToken.Length,
                    string.IsNullOrWhiteSpace(formToken) ? 0 : formToken.Length);

                var mensaje = string.Format(
                    "AprobarYEnviarAS400 400 (EndRequest). User={0}; Method={1}; Url={2}; FormKeys={3}; QueryKeys={4}; {5}",
                    user,
                    request.HttpMethod,
                    request.Url != null ? request.Url.ToString() : "N/A",
                    formKeys,
                    queryKeys,
                    tokenInfo);

                CapaNegocio.LogBL.RegistrarAdvertencia(mensaje, "Global.asax");
            }
            catch
            {
                // Evitar fallos en pipeline
            }
        }

        private static bool TryNormalizeAuthenticatedUnauthorizedResponse(HttpContext context, HttpRequest request, HttpResponse response)
        {
            if (context == null || request == null || response == null)
            {
                return false;
            }

            if (response.StatusCode != 401)
            {
                return false;
            }

            var identity = context.User != null ? context.User.Identity : null;
            if (identity == null || !identity.IsAuthenticated)
            {
                return false;
            }

            var path = request.Url != null ? request.Url.AbsolutePath : request.Path;
            if (string.IsNullOrWhiteSpace(path) ||
                path.StartsWith(VirtualPathUtility.ToAbsolute("~/Error"), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            response.SuppressFormsAuthenticationRedirect = true;

            if (IsAjaxLikeRequest(request))
            {
                response.StatusCode = 403;
                response.TrySkipIisCustomErrors = true;
            }
            else
            {
                var destino = VirtualPathUtility.ToAbsolute("~/Error/NoAutorizado");
                response.StatusCode = 302;
                response.RedirectLocation = destino;
                response.AddHeader("Location", destino);
            }

            PerfLogger.LogWarning(string.Format(
                "[AUTH] 401 autenticado normalizado. Path={0}; Method={1}; User={2}; Ajax={3}; Destino={4}",
                path,
                request.HttpMethod,
                identity.Name ?? string.Empty,
                IsAjaxLikeRequest(request),
                IsAjaxLikeRequest(request) ? "403" : VirtualPathUtility.ToAbsolute("~/Error/NoAutorizado")));

            return true;
        }

        private static bool TryNormalizeAuthenticatedLoginRedirect(HttpContext context, HttpRequest request, HttpResponse response)
        {
            if (context == null || request == null || response == null)
            {
                return false;
            }

            if (!IsFormsLoginRedirect(response))
            {
                return false;
            }

            var identity = context.User != null ? context.User.Identity : null;
            if (identity == null || !identity.IsAuthenticated)
            {
                return false;
            }

            var path = request.Url != null ? request.Url.AbsolutePath : request.Path;
            if (string.IsNullOrWhiteSpace(path) ||
                path.StartsWith(VirtualPathUtility.ToAbsolute("~/Account/Login"), StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(VirtualPathUtility.ToAbsolute("~/Account/Logout"), StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(VirtualPathUtility.ToAbsolute("~/Error"), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var redirectOriginal = response.RedirectLocation ?? string.Empty;
            var destino = VirtualPathUtility.ToAbsolute("~/Error/NoAutorizado");

            response.Clear();
            response.SuppressFormsAuthenticationRedirect = true;
            response.TrySkipIisCustomErrors = true;

            if (IsAjaxLikeRequest(request))
            {
                response.StatusCode = 403;
                response.ContentType = "application/json; charset=utf-8";
                response.Write("{\"success\":false,\"code\":403,\"requiresLogin\":false,\"message\":\"No tiene permisos para acceder a este recurso.\"}");
            }
            else
            {
                response.StatusCode = 302;
                response.RedirectLocation = destino;
                response.AddHeader("Location", destino);
            }

            PerfLogger.LogWarning(string.Format(
                "[AUTH][DENIED] Redireccion a Login bloqueada para usuario autenticado. Path={0}; Method={1}; User={2}; RedirectOriginal={3}; Destino={4}",
                request.RawUrl ?? string.Empty,
                request.HttpMethod,
                identity.Name ?? string.Empty,
                redirectOriginal,
                IsAjaxLikeRequest(request) ? "JSON_403" : destino));

            context.ApplicationInstance.CompleteRequest();
            return true;
        }

        private static bool TryNormalizeAjaxLoginRedirect(HttpContext context, HttpRequest request, HttpResponse response)
        {
            if (context == null || request == null || response == null)
            {
                return false;
            }

            if (!IsAjaxLikeRequest(request))
            {
                return false;
            }

            var loginRedirect = IsFormsLoginRedirect(response);
            if (response.StatusCode != 401 && !loginRedirect)
            {
                return false;
            }

            var identity = context.User != null ? context.User.Identity : null;
            if (identity != null && identity.IsAuthenticated)
            {
                return false;
            }

            response.Clear();
            response.SuppressFormsAuthenticationRedirect = true;
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = 401;
            response.RedirectLocation = null;
            response.Headers.Remove("Location");
            response.ContentType = "application/json; charset=utf-8";

            var returnUrl = request.RawUrl ?? (request.Url != null ? request.Url.PathAndQuery : string.Empty);
            var loginUrl = VirtualPathUtility.ToAbsolute("~/Account/Login");
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                loginUrl += "?ReturnUrl=" + HttpUtility.UrlEncode(returnUrl);
            }

            response.Write("{\"success\":false,\"code\":401,\"requiresLogin\":true,\"redirectUrl\":\""
                + JsonEscape(loginUrl)
                + "\",\"message\":\"La sesion expiro o la aplicacion se reinicio. Inicie sesion nuevamente y vuelva a intentar finalizar la LV/EAE.\"}");

            PerfLogger.LogWarning(string.Format(
                "[AUTH][AJAX] Respuesta no autenticada normalizada a JSON 401. Path={0}; Method={1}; RedirectLogin={2}; AuthCookie={3}",
                request.RawUrl ?? string.Empty,
                request.HttpMethod,
                loginRedirect,
                request.Cookies[FormsAuthentication.FormsCookieName] != null));

            context.ApplicationInstance.CompleteRequest();
            return true;
        }

        private static bool IsFormsLoginRedirect(HttpResponse response)
        {
            if (response == null || response.StatusCode != 302)
            {
                return false;
            }

            var redirectLocation = response.RedirectLocation ?? string.Empty;
            return redirectLocation.IndexOf("/Account/Login", StringComparison.OrdinalIgnoreCase) >= 0
                || redirectLocation.IndexOf("Account%2fLogin", StringComparison.OrdinalIgnoreCase) >= 0
                || redirectLocation.IndexOf("Account%2FLogin", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static bool IsAjaxLikeRequest(HttpRequest request)
        {
            if (request == null)
            {
                return false;
            }

            var requestedWith = request.Headers["X-Requested-With"];
            if (string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var acceptHeader = request.Headers["Accept"] ?? string.Empty;
            return acceptHeader.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
            var mideLogin = IsLoginPath(Context != null ? Context.Request : null);
            var authStopwatch = mideLogin ? Stopwatch.StartNew() : null;
            HttpCookie authCookie = Context.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrEmpty(authCookie.Value)) return;

            FormsAuthenticationTicket authTicket;
            try { authTicket = FormsAuthentication.Decrypt(authCookie.Value); }
            catch { return; }

            if (authTicket == null || authTicket.Expired) return;

            var ticketRoleData = AuthTicketRoleDataHelper.Deserialize(authTicket.UserData);
            var rolesDesdeTicket = ticketRoleData.Roles.Count > 0;
            var rolesTicket = RoleGroupingHelper.SanitizeRawRolesForUser(authTicket.Name, ticketRoleData.Roles).ToArray();
            var rolesDb = RoleGroupingHelper.SanitizeRawRolesForUser(authTicket.Name, GetRolesFromDBCached(authTicket.Name)).ToArray();
            var rolesBase = rolesDb.Length > 0
                ? rolesDb
                : (rolesDesdeTicket ? rolesTicket : new string[] { });
            string[] roles = rolesBase
                .Concat(RoleGroupingHelper.BuildUnifiedRoles(rolesBase))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var selectedRole = RoleGroupingHelper.ResolveSelectedRoleForUser(
                authTicket.Name,
                RoleGroupingHelper.BuildUnifiedRoles(rolesBase),
                ticketRoleData.SelectedRole);

            if (!RolesEquivalent(rolesTicket, rolesBase) ||
                !string.Equals(ticketRoleData.SelectedRole ?? string.Empty, selectedRole ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                ReemitirTicketRoles(authTicket, rolesBase, selectedRole);
            }
            else if (Context.Items["__AocrRolesSyncLogged"] == null)
            {
                Context.Items["__AocrRolesSyncLogged"] = true;
            }

            var identity = new GenericIdentity(authTicket.Name);
            var principal = new GenericPrincipal(identity, roles);

            Context.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;

            if (mideLogin && authStopwatch != null)
            {
                PerfLogger.LogInfo(string.Format(
                    "[PERF][LOGIN] AuthenticateRequest usuario={0}; rolesSource={1}; roles={2}; total={3} ms",
                    authTicket.Name,
                    rolesDb.Length > 0 ? "db" : (rolesDesdeTicket ? "ticket" : "empty"),
                    roles != null ? roles.Length : 0,
                    authStopwatch.ElapsedMilliseconds));
            }
        }

        protected void Application_PostAcquireRequestState(object sender, EventArgs e)
        {
            if (Context == null || Context.Session == null)
            {
                return;
            }

            if (Context.User == null || Context.User.Identity == null || !Context.User.Identity.IsAuthenticated)
            {
                return;
            }

            if (Context.Items["__AocrSessionBootstrapped"] != null)
            {
                return;
            }

            Context.Items["__AocrSessionBootstrapped"] = true;
            AuthenticatedSessionBootstrapper.EnsureSession(new HttpContextWrapper(Context));
        }

        private static bool RolesEquivalent(string[] left, string[] right)
        {
            var a = (left ?? new string[] { })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var b = (right ?? new string[] { })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return a.SequenceEqual(b, StringComparer.OrdinalIgnoreCase);
        }

        private static void ReemitirTicketRoles(FormsAuthenticationTicket authTicket, string[] roles, string selectedRole)
        {
            try
            {
                if (authTicket == null || HttpContext.Current == null)
                {
                    return;
                }

                var expiracion = authTicket.IsPersistent && authTicket.Expiration > DateTime.Now
                    ? authTicket.Expiration
                    : DateTime.Now.AddMinutes(SessionTimeoutHelper.GetTimeoutMinutes());
                var ticketActualizado = new FormsAuthenticationTicket(
                    authTicket.Version,
                    authTicket.Name,
                    DateTime.Now,
                    expiracion,
                    authTicket.IsPersistent,
                    AuthTicketRoleDataHelper.Serialize(roles, selectedRole),
                    string.IsNullOrWhiteSpace(authTicket.CookiePath) ? FormsAuthentication.FormsCookiePath : authTicket.CookiePath);

                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticketActualizado))
                {
                    HttpOnly = true,
                    Secure = HttpContext.Current.Request != null && HttpContext.Current.Request.IsSecureConnection,
                    Path = string.IsNullOrWhiteSpace(ticketActualizado.CookiePath) ? FormsAuthentication.FormsCookiePath : ticketActualizado.CookiePath
                };
                CookieHelper.SetSameSiteLax(cookie);

                if (authTicket.IsPersistent)
                {
                    cookie.Expires = expiracion;
                }

                HttpContext.Current.Response.Cookies.Add(cookie);
                PerfLogger.LogInfo(string.Format(
                    "[AOCR][ROLES_SYNC] Usuario={0}; TicketReemitido=True; RolesTicketNuevo={1}; RolActivo={2}; Resultado=OK",
                    authTicket.Name,
                    string.Join(",", roles ?? new string[] { }),
                    selectedRole ?? string.Empty));
            }
            catch (Exception ex)
            {
                PerfLogger.LogWarning("[AOCR][ROLES_SYNC] No se pudo reemitir ticket de roles: " + ex.Message);
            }
        }

        private string[] GetRolesFromDBCached(string username)
        {
            const string cacheKey = "__AocrRolesDbCache";
            if (HttpContext.Current != null && HttpContext.Current.Items[cacheKey] is string[] cachedRoles)
            {
                return cachedRoles;
            }

            var roles = GetRolesFromDB(username);
            if (HttpContext.Current != null)
            {
                HttpContext.Current.Items[cacheKey] = roles;
            }

            return roles;
        }

        private string[] GetRolesFromDB(string username)
        {
            try
            {
                using (var cn = ConexionDAO.CrearConexion())
                {
                    cn.Open();
                    string sql = @"
                        SELECT r.descripcion
                        FROM usuario u
                        INNER JOIN usuario_rol ur ON u.codigousuario::text = ur.codigousuario::text
                        INNER JOIN rol r ON r.codigorol = ur.codigorol
                        WHERE (LOWER(u.nombreusuario) = LOWER(@user) OR LOWER(u.correo) = LOWER(@user))
                          AND ur.activo = true 
                          AND r.activo = true;";

                    return cn.Query<string>(sql, new { user = username }).ToArray();
                }
            }
            catch
            {
                return new string[] { };
            }
        }

        private static bool IsWellKnownRequest(HttpRequest request)
        {
            if (request == null)
            {
                return false;
            }

            var path = request.Url != null
                ? request.Url.AbsolutePath
                : request.Path;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            return path.IndexOf("/.well-known", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(path, VirtualPathUtility.ToAbsolute("~/favicon.ico"), StringComparison.OrdinalIgnoreCase);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_global.txt");
            
            try
            {
                var httpContext = HttpContext.Current;
                string url = httpContext != null ? httpContext.Request.Url.ToString() : "Unknown URL";
                string userAgent = httpContext != null ? httpContext.Request.UserAgent : "Unknown";
                string user = httpContext != null && httpContext.User != null ? httpContext.User.Identity.Name : "Anonymous";
                
                System.IO.File.AppendAllText(logPath, 
                    $"\n=== ERROR GLOBAL {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===\n" +
                    $"URL: {url}\n" +
                    $"Usuario: {user}\n" +
                    $"UserAgent: {userAgent}\n" +
                    $"Excepción: {ex?.ToString() ?? "null"}\n\n");
            }
            catch
            {
                // Si falla el logging, no hacer nada
            }
        }

        protected void Application_End()
        {
            // Detener procesador de cola
            if (_emailProcessor != null)
            {
                _emailProcessor.Dispose();
                _emailProcessor = null;
            }
        }

        private void IniciarProcesadorEmail()
        {
            try
            {
                var configService = new CapaDatos.Services.SecureConfigurationService();
                var connectionString = configService.GetConnectionString("PostgreSQL");

                var queueService = new CapaDatos.Services.EmailQueueService();
                var emailService = new CapaDatos.Services.EmailService(configService);

                _emailProcessor = new EmailQueueProcessor(queueService, emailService);
                _emailProcessor.Start();

                PerfLogger.LogInfo("[PERF][APP_START] Procesador de cola de correos iniciado en modo no bloqueante");
            }
            catch (Exception ex)
            {
                PerfLogger.LogError(ex, new CapaDatos.Services.LogContext { ErrorCode = "EMAIL_QUEUE_START_ERROR" });
            }
        }

        private static bool TryResolvePerfLabel(HttpRequest request, out string perfLabel)
        {
            perfLabel = null;
            if (request == null || request.Url == null)
            {
                return false;
            }

            var path = request.Url.AbsolutePath ?? string.Empty;
            if (path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                perfLabel = "[PERF][LOGIN]";
                return true;
            }

            if (path.Equals("/Empresa/ObtenerEmpresas", StringComparison.OrdinalIgnoreCase))
            {
                perfLabel = "[PERF][LOGIN][EMPRESAS]";
                return true;
            }

            if (path.Equals("/Account/CuentasBancos", StringComparison.OrdinalIgnoreCase))
            {
                perfLabel = "[PERF][LOGIN][BANCOS]";
                return true;
            }

            if (path.Equals("/Account/ModalCrearUsuario", StringComparison.OrdinalIgnoreCase))
            {
                perfLabel = "[PERF][LOGIN][REGISTRO]";
                return true;
            }

            return false;
        }

        private static bool IsLoginPath(HttpRequest request)
        {
            if (request == null || request.Url == null)
            {
                return false;
            }

            return string.Equals(request.Url.AbsolutePath, "/Account/Login", StringComparison.OrdinalIgnoreCase);
        }
    }
}
