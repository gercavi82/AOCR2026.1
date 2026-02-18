using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using Dapper;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Services;
using AppLoggingService = CapaNegocio.Services.ILoggingService;
using AppLoggingFactory = CapaNegocio.Services.LoggingServiceFactory;
using AppLogContext = CapaNegocio.Services.LogContext;

namespace CapaPresentacion
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static EmailQueueProcessor _emailProcessor;
        private static readonly AppLoggingService _logger = AppLoggingFactory.Create();

        protected void Application_Start()
        {
            // Dapper
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Configurar Dependency Injection con Unity
            UnityConfig.RegisterComponents();

            // Iniciar procesador de cola de correos
            IniciarProcesadorEmail();
        }

        protected void Application_BeginRequest()
        {
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            Request.ContentEncoding = System.Text.Encoding.UTF8;

            try
            {
                var ctx = HttpContext.Current;
                if (ctx == null) return;

                // CorrelationId para trazar toda la solicitud
                if (ctx.Items["CorrelationId"] == null)
                {
                    ctx.Items["CorrelationId"] = Guid.NewGuid().ToString("N").Substring(0, 12);
                }

                ctx.Items["RequestStartUtc"] = DateTime.UtcNow;

                var path = ctx.Request?.Path ?? string.Empty;
                if (EsRecursoEstatico(path)) return;

                _logger.LogInfo(
                    string.Format("HTTP {0} {1}", ctx.Request.HttpMethod, ctx.Request.RawUrl),
                    new AppLogContext
                    {
                        CorrelationId = ctx.Items["CorrelationId"] as string,
                        UserId = ctx.User?.Identity?.Name,
                        AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "Ip", ctx.Request.UserHostAddress ?? string.Empty },
                            { "UserAgent", ctx.Request.UserAgent ?? string.Empty }
                        }
                    });
            }
            catch
            {
                // No bloquear request por logging
            }
        }

        protected void Application_AuthenticateRequest(object sender, EventArgs e)
        {
            HttpCookie authCookie = Context.Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrEmpty(authCookie.Value)) return;

            FormsAuthenticationTicket authTicket;
            try { authTicket = FormsAuthentication.Decrypt(authCookie.Value); }
            catch { return; }

            if (authTicket == null || authTicket.Expired) return;

            string[] roles = (!string.IsNullOrEmpty(authTicket.UserData))
                ? authTicket.UserData.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                : GetRolesFromDB(authTicket.Name);

            var identity = new GenericIdentity(authTicket.Name);
            var principal = new GenericPrincipal(identity, roles);

            Context.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
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

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();

            try
            {
                var httpContext = HttpContext.Current;
                var context = new AppLogContext
                {
                    CorrelationId = httpContext?.Items["CorrelationId"] as string,
                    UserId = httpContext?.User?.Identity?.Name,
                    ErrorCode = "GLOBAL_ERROR",
                    AdditionalData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "Url", httpContext?.Request?.RawUrl ?? "N/A" },
                        { "Method", httpContext?.Request?.HttpMethod ?? "N/A" },
                        { "Ip", httpContext?.Request?.UserHostAddress ?? "N/A" },
                        { "UserAgent", httpContext?.Request?.UserAgent ?? "N/A" }
                    }
                };

                if (ex is HttpParseException parseEx)
                {
                    context.ErrorCode = "RAZOR_PARSE_ERROR";
                    context.AdditionalData["Parse.FileName"] = parseEx.FileName ?? "N/A";
                    context.AdditionalData["Parse.VirtualPath"] = parseEx.VirtualPath ?? "N/A";
                    context.AdditionalData["Parse.Line"] = parseEx.Line;

                    try
                    {
                        if (parseEx.ParserErrors != null && parseEx.ParserErrors.Count > 0)
                        {
                            var max = Math.Min(5, parseEx.ParserErrors.Count);
                            for (var i = 0; i < max; i++)
                            {
                                var pe = parseEx.ParserErrors[i];
                                context.AdditionalData["ParseError." + i] = string.Format(
                                    "Line={0}; VirtualPath={1}; Message={2}",
                                    pe.Line,
                                    pe.VirtualPath ?? "N/A",
                                    pe.ErrorText ?? "N/A");
                            }
                        }
                    }
                    catch
                    {
                        // Evitar fallar el manejo global de errores por logging extra.
                    }
                }

                _logger.LogError(ex ?? new Exception("Error global sin excepción"), context);
            }
            catch
            {
                // Si falla el logging, no hacer nada
            }
        }

        protected void Application_EndRequest()
        {
            try
            {
                var ctx = HttpContext.Current;
                if (ctx == null) return;

                var path = ctx.Request?.Path ?? string.Empty;
                if (EsRecursoEstatico(path)) return;

                var startObj = ctx.Items["RequestStartUtc"];
                var durationMs = 0L;
                if (startObj is DateTime startUtc)
                {
                    durationMs = (long)(DateTime.UtcNow - startUtc).TotalMilliseconds;
                }

                _logger.LogInfo(
                    string.Format("HTTP END {0} {1} => {2} ({3} ms)",
                        ctx.Request.HttpMethod,
                        ctx.Request.RawUrl,
                        ctx.Response?.StatusCode ?? 0,
                        durationMs),
                    new AppLogContext
                    {
                        CorrelationId = ctx.Items["CorrelationId"] as string,
                        UserId = ctx.User?.Identity?.Name
                    });
            }
            catch
            {
                // No bloquear fin de request por logging
            }
        }

        private static bool EsRecursoEstatico(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var p = path.ToLowerInvariant();
            return p.EndsWith(".css") || p.EndsWith(".js") || p.EndsWith(".png") ||
                   p.EndsWith(".jpg") || p.EndsWith(".jpeg") || p.EndsWith(".gif") ||
                   p.EndsWith(".svg") || p.EndsWith(".ico") || p.EndsWith(".woff") ||
                   p.EndsWith(".woff2") || p.EndsWith(".ttf") || p.EndsWith(".map");
        }

        protected void Application_End()
        {
            // Detener procesador de cola
            if (_emailProcessor != null)
            {
                _emailProcessor.Stop();
                _emailProcessor.Dispose();
            }
        }

        private void IniciarProcesadorEmail()
        {
            try
            {
                var configService = new CapaDatos.Services.SecureConfigurationService();
                var connectionString = configService.GetConnectionString("PostgreSQL");
                var configSource = "PostgreSQL";
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = configService.GetConnectionString("AOCRConnection");
                    configSource = "AOCRConnection";
                }

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    _logger.LogError(
                        new InvalidOperationException("No se encontró connection string para EmailQueue (PostgreSQL/AOCRConnection)."),
                        new AppLogContext { ErrorCode = "EMAIL_QUEUE_CONFIG_MISSING" });
                    return;
                }

                var queueService = new CapaDatos.Services.EmailQueueService(connectionString);
                var emailService = new CapaDatos.Services.EmailService(configService);

                _emailProcessor = new EmailQueueProcessor(queueService, emailService);
                _emailProcessor.Start();
                _logger.LogInfo("EmailQueueProcessor iniciado con connection string desde: " + configSource);

                System.Diagnostics.Debug.WriteLine("Procesador de cola de correos iniciado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al iniciar procesador de correos: " + ex.Message);
                _logger.LogError(ex, new AppLogContext { ErrorCode = "EMAIL_QUEUE_START_ERROR" });
            }
        }
    }
}
