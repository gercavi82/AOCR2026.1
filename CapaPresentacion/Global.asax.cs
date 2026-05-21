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

namespace CapaPresentacion
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static EmailQueueProcessor _emailProcessor;
        private const string PerfStopwatchKey = "__AocrPerfStopwatch";
        private const string PerfLabelKey = "__AocrPerfLabel";
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
            response.StatusCode = 403;

            if (IsAjaxLikeRequest(request))
            {
                response.TrySkipIisCustomErrors = true;
            }

            PerfLogger.LogWarning(string.Format(
                "[AUTH] 401 autenticado normalizado a 403. Path={0}; Method={1}; User={2}; Ajax={3}",
                path,
                request.HttpMethod,
                identity.Name ?? string.Empty,
                IsAjaxLikeRequest(request)));

            return true;
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

            var rolesDesdeTicket = !string.IsNullOrEmpty(authTicket.UserData);
            string[] roles = rolesDesdeTicket
                ? authTicket.UserData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                : GetRolesFromDB(authTicket.Name);

            var identity = new GenericIdentity(authTicket.Name);
            var principal = new GenericPrincipal(identity, roles);

            Context.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;

            if (mideLogin && authStopwatch != null)
            {
                PerfLogger.LogInfo(string.Format(
                    "[PERF][LOGIN] AuthenticateRequest usuario={0}; rolesSource={1}; roles={2}; total={3} ms",
                    authTicket.Name,
                    rolesDesdeTicket ? "ticket" : "db",
                    roles != null ? roles.Length : 0,
                    authStopwatch.ElapsedMilliseconds));
            }
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

            return path.IndexOf("/.well-known", StringComparison.OrdinalIgnoreCase) >= 0;
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
