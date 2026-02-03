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

namespace CapaPresentacion
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static EmailQueueProcessor _emailProcessor;

        protected void Application_Start()
        {
            // Dapper
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Iniciar procesador de cola de correos
            // NOTA: Comentado temporalmente - descomentar cuando la tabla email_queue exista
            // IniciarProcesadorEmail();
        }

        protected void Application_BeginRequest()
        {
            Response.ContentEncoding = System.Text.Encoding.UTF8;
            Request.ContentEncoding = System.Text.Encoding.UTF8;
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
