using System;
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

        protected void Application_Start()
        {
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
            UnityConfig.RegisterComponents();

            // Iniciar procesador de cola de correos
            IniciarProcesadorEmail();
        }

        protected void Application_BeginRequest()
        {
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            Request.ContentEncoding = System.Text.Encoding.UTF8;
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

                var queueService = new CapaDatos.Services.EmailQueueService();
                var emailService = new CapaDatos.Services.EmailService(configService);

                _emailProcessor = new EmailQueueProcessor(queueService, emailService);
                _emailProcessor.Start();

                System.Diagnostics.Debug.WriteLine("Procesador de cola de correos iniciado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al iniciar procesador de correos: " + ex.Message);
            }
        }
    }
}
