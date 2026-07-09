using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaNegocio.Integraciones.As400Sync;
using CapaDatos.Services;
using CapaPresentacion.Models;
using CapaDatos.DAOs;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Controllers
{
    public class AccountController : Controller
    {
        private static readonly ILoggingService _logger = LoggingServiceFactory.Create();

        private static readonly HashSet<string> RolesInternosNoBloqueoDesignacion = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Administrador",
            "Direccion",
            "JefaturaTecnica",
            "Financiero",
            "CoordinadorFinanciero",
            "CoordinacionLegal",
            "CoordinadorLegal",
            "Inspector",
            "Tecnico",
            "EvaluadorTecnico",
            "CoordinadorInspecciones",
            "DirectorFinanciero",
            "DirectorGeneral",
            "DIRECTOR_CERTIFICACIONES_DCAV",
            "DirectorCertificacionesDcav",
            "DCAV",
            "Recepcion"
        };

        [AllowAnonymous]
        public ActionResult Login(string returnUrl, string af = null)
        {
            var totalStopwatch = Stopwatch.StartNew();
            var usuarioAutenticado = User != null && User.Identity != null && User.Identity.IsAuthenticated;
            var returnUrlSeguro = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null;
            _logger.LogInfo(string.Format(
                "[PERF][LOGIN] Inicio Login GET. Authenticated={0}; ReturnUrl={1}",
                usuarioAutenticado,
                returnUrl ?? string.Empty));

            if (usuarioAutenticado)
            {
                Usuario usuarioActual;
                List<string> rolesActuales;
                string loginRestaurado;
                if (TryRestaurarSesionUsuarioActual(out usuarioActual, out rolesActuales, out loginRestaurado))
                {
                    _logger.LogInfo(string.Format(
                        "[PERF][LOGIN] Sesion autenticada restaurada. UsuarioId={0}; Login={1}; Roles={2}",
                        usuarioActual.Id,
                        loginRestaurado ?? string.Empty,
                        rolesActuales != null ? string.Join(",", rolesActuales) : string.Empty));

                    if (usuarioActual.MustChangePassword || !usuarioActual.FechaUltimaConexion.HasValue)
                    {
                        if (!string.IsNullOrWhiteSpace(returnUrlSeguro))
                        {
                            Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrlSeguro;
                        }

                        return LogLoginResult(
                            RedirectToAction("CambiarContrasena", "Account"),
                            totalStopwatch,
                            "Login GET usuario autenticado redirige a cambio de contrasena");
                    }

                    var esUsuarioRt = EsUsuarioRt(usuarioActual) && !EsUsuarioAdministrador(usuarioActual, rolesActuales);
                    var companiasAsignadas = esUsuarioRt
                        ? ObtenerCompaniasAsignadasConFallback(usuarioActual)
                        : new List<UsuarioCompaniaRT>();

                    if (esUsuarioRt)
                    {
                        if (companiasAsignadas.Count == 1)
                        {
                            EstablecerCompaniaActiva(companiasAsignadas[0]);
                        }
                        else if (companiasAsignadas.Count > 1)
                        {
                            var codigoActivo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
                            var companiaActiva = companiasAsignadas.FirstOrDefault(c =>
                                string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigoActivo, StringComparison.OrdinalIgnoreCase));

                            if (companiaActiva == null)
                            {
                                if (!string.IsNullOrWhiteSpace(returnUrlSeguro))
                                {
                                    Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrlSeguro;
                                }

                                return LogLoginResult(
                                    RedirectToAction("SeleccionarCompania", new { returnUrl = returnUrlSeguro }),
                                    totalStopwatch,
                                    "Login GET usuario autenticado redirige a seleccionar compania");
                            }

                            if (string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerNombre(Session)))
                            {
                                EstablecerCompaniaActiva(companiaActiva);
                            }
                        }
                    }
                    else if (string.IsNullOrWhiteSpace(CompaniaActivaSessionHelper.ObtenerCodigo(Session)) &&
                             !string.IsNullOrWhiteSpace(usuarioActual.EmpresaCodigo))
                    {
                        var nombreEmpresa = ResolverNombreCompaniaPorCodigo(usuarioActual.EmpresaCodigo);
                        CompaniaActivaSessionHelper.Establecer(Session, usuarioActual.EmpresaCodigo, nombreEmpresa);
                    }

                    return LogLoginResult(
                        RedireccionarDespuesLogin(usuarioActual.Id, returnUrlSeguro),
                        totalStopwatch,
                        string.IsNullOrWhiteSpace(returnUrlSeguro)
                            ? "Login GET usuario autenticado redirige a home"
                            : "Login GET usuario autenticado redirige a returnUrl");
                }

                LimpiarAutenticacionParaMostrarLogin();
                _logger.LogInfo("[PERF][LOGIN] No se pudo restaurar la sesion autenticada. Se mostrara Login anonimo.");
            }

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-5));

            if (string.Equals(af, "1", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "La sesión expiró o el formulario perdió validez. Recargue la página e intente nuevamente.");
            }
            if (TempData["LoginError"] != null)
            {
                ModelState.AddModelError("", TempData["LoginError"].ToString());
            }

            ViewBag.ReturnUrl = returnUrlSeguro;
            _logger.LogInfo(string.Format(
                "[PERF][LOGIN] Fin Login GET pre-view. Total={0} ms",
                totalStopwatch.ElapsedMilliseconds));
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            var totalStopwatch = Stopwatch.StartNew();
            _logger.LogInfo(string.Format(
                "[PERF][LOGIN] Inicio Login POST. Usuario={0}; ReturnUrl={1}",
                model != null ? (model.Usuario ?? string.Empty) : string.Empty,
                returnUrl ?? string.Empty));

            if (!ModelState.IsValid)
                return LogLoginResult(View(model), totalStopwatch, "Login POST rechazado por ModelState invalido");

            string mensaje;
            Usuario usuario;
            List<string> roles;

            bool ok;
            var autenticacionStopwatch = Stopwatch.StartNew();
            try
            {
                ok = UsuarioBL.Autenticar(
                    model.Usuario,
                    model.Contrasena,
                    out usuario,
                    out roles,
                    out mensaje,
                    actualizarUltimaConexion: false
                );

                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN] UsuarioBL.Autenticar completado en {0} ms. Ok={1}; UsuarioEncontrado={2}; Roles={3}",
                    autenticacionStopwatch.ElapsedMilliseconds,
                    ok,
                    usuario != null,
                    roles != null ? roles.Count : 0));
            }
            catch (Exception ex) when (EsErrorConexionBaseDatos(ex))
            {
                System.Diagnostics.Debug.WriteLine("Account/Login: error de conexión a base de datos: " + ex.Message);
                ModelState.AddModelError("", "No se pudo conectar con la base de datos. Intente nuevamente en unos minutos.");
                return LogLoginResult(View(model), totalStopwatch, "Login POST con error de conexion a base de datos");
            }

            if (!ok || usuario == null)
            {
                ModelState.AddModelError("", string.IsNullOrWhiteSpace(mensaje) ? "Credenciales inválidas." : mensaje);
                return LogLoginResult(View(model), totalStopwatch, "Login POST con credenciales invalidas");
            }

            // 🔐 BLOQUEO DE ACCESO PARA RT PENDIENTE / RECHAZADO
            var bypassRtRestriction = !string.IsNullOrWhiteSpace(usuario.Email) &&
                (usuario.Email.Equals("gercavi82@gmail.com", StringComparison.OrdinalIgnoreCase)
                 || usuario.Email.Equals("german.cajas@aviacioncivil.gob.ec", StringComparison.OrdinalIgnoreCase));
            if (!bypassRtRestriction &&
                !string.IsNullOrWhiteSpace(usuario.EstadoDesignacionRT) &&
                !usuario.EstadoDesignacionRT.Equals("aceptado", StringComparison.OrdinalIgnoreCase))
            {
                var esAdmin = false;
                var esRolInterno = false;
                if (roles != null)
                {
                    esAdmin = roles.Any(r => r.Equals("Administrador", StringComparison.OrdinalIgnoreCase));
                    esRolInterno = roles.Any(r => RolesInternosNoBloqueoDesignacion.Contains((r ?? string.Empty).Trim()));
                }

                if (!esAdmin && !esRolInterno)
                {
                    var estado = usuario.EstadoDesignacionRT.Trim().ToLowerInvariant();
                    var msg = estado == "rechazado"
                        ? "Su designación RT fue rechazada. Corrija y vuelva a subir el documento."
                        : "Su designación RT está en proceso de validación y aprobación por el Coordinador.";
                    ModelState.AddModelError("", msg);
                    return LogLoginResult(View(model), totalStopwatch, "Login POST bloqueado por estado RT=" + estado);
                }
            }

            // 🔐 ADMIN SUPREMO
            if (!string.IsNullOrWhiteSpace(usuario.NombreUsuario) &&
                usuario.NombreUsuario.Equals("USU_ADMIN", StringComparison.InvariantCultureIgnoreCase))
            {
                roles = new List<string>
                {
                    "Administrador","Tecnico","Solicitante","Financiero","Inspector",
                    "JefaturaTecnica","Direccion","CoordinacionLegal"
                };
            }

            roles = RoleGroupingHelper.SanitizeRawRolesForUser(
                !string.IsNullOrWhiteSpace(usuario.NombreUsuario) ? usuario.NombreUsuario : model.Usuario,
                roles ?? new List<string>()).ToList();
            var rolesString = AuthTicketRoleDataHelper.Serialize(
                roles,
                RoleGroupingHelper.ResolveSelectedRoleForUser(
                    !string.IsNullOrWhiteSpace(usuario.NombreUsuario) ? usuario.NombreUsuario : model.Usuario,
                    RoleGroupingHelper.BuildUnifiedRoles(roles),
                    string.Empty));
            var sessionTimeoutMinutes = SessionTimeoutHelper.GetTimeoutMinutes();

            // ============================
            // COOKIE DE AUTENTICACIÓN (PRODUCCIÓN)
            // ============================
            var ticket = new FormsAuthenticationTicket(
                1,
                usuario.NombreUsuario ?? model.Usuario ?? "usuario",
                DateTime.Now,
                DateTime.Now.AddMinutes(sessionTimeoutMinutes),
                model.Recordarme,
                rolesString,
                FormsAuthentication.FormsCookiePath
            );

            string encrypted = FormsAuthentication.Encrypt(ticket);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection,
                Path = FormsAuthentication.FormsCookiePath
            };
            CookieHelper.SetSameSiteLax(cookie);

            if (model.Recordarme)
                cookie.Expires = DateTime.Now.AddDays(7);

            Response.Cookies.Add(cookie);
            // Forzar emisión de token antiforgery nuevo en la siguiente vista autenticada.
            ExpirarCookieAntiForgery();
            ExpirarCookieRolSeleccionado();

            SincronizarSesionAutenticada(usuario, roles, model.Usuario, limpiarCompaniaActiva: true);
            PersistirRolSeleccionado(Session["Rol"] as string, model.Recordarme ? (DateTime?)DateTime.Now.AddDays(7) : null);

            // Forzar cambio cuando hay marca explícita o cuando la última conexión fue limpiada (reset clave).
            if (usuario.MustChangePassword || !usuario.FechaUltimaConexion.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrl;
                }
                return LogLoginResult(
                    RedirectToAction("CambiarContrasena", "Account"),
                    totalStopwatch,
                    "Login POST redirige a cambio de contrasena");
            }

            var esUsuarioRt = EsUsuarioRt(usuario) && !EsUsuarioAdministrador(usuario, roles);
            var companiasAsignadas = new List<UsuarioCompaniaRT>();
            if (esUsuarioRt)
            {
                var companiasStopwatch = Stopwatch.StartNew();
                companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN] ObtenerCompaniasAsignadasConFallback completado en {0} ms. Total={1}",
                    companiasStopwatch.ElapsedMilliseconds,
                    companiasAsignadas.Count));

                if (companiasAsignadas.Count == 0)
                {
                    ModelState.AddModelError("", "Su usuario RT no tiene compañías asignadas. Solicite al administrador la asociación correspondiente.");
                    return LogLoginResult(View(model), totalStopwatch, "Login POST RT sin companias asignadas");
                }

                if (companiasAsignadas.Count == 1)
                {
                    var unica = companiasAsignadas[0];
                    var nombreCompania = !string.IsNullOrWhiteSpace(unica.CompaniaNombre)
                        ? unica.CompaniaNombre
                        : ResolverNombreCompaniaPorCodigo(unica.CompaniaCodigo);

                    CompaniaActivaSessionHelper.Establecer(Session, unica.CompaniaCodigo, nombreCompania);
                }
                else
                {
                    Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrl;
                    return LogLoginResult(
                        RedirectToAction("SeleccionarCompania"),
                        totalStopwatch,
                        "Login POST redirige a seleccionar compania");
                }
            }
            else if (!string.IsNullOrWhiteSpace(usuario.EmpresaCodigo))
            {
                var nombreEmpresa = ResolverNombreCompaniaPorCodigo(usuario.EmpresaCodigo);
                CompaniaActivaSessionHelper.Establecer(Session, usuario.EmpresaCodigo, nombreEmpresa);
            }

            // Actualizar última conexión después de autenticación normal.
            UsuarioDAO.ActualizarUltimaConexion(usuario.Id);
            return LogLoginResult(
                RedireccionarDespuesLogin(usuario.Id, returnUrl),
                totalStopwatch,
                "Login POST autenticado correctamente");
        }

        [HttpGet]
        [AllowAnonymous]
        public PartialViewResult ModalCrearUsuario()
        {
            var totalStopwatch = Stopwatch.StartNew();

            try
            {
                return PartialView("_ModalCrearUsuario");
            }
            finally
            {
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN][REGISTRO] Controller ModalCrearUsuario completado en {0} ms",
                    totalStopwatch.ElapsedMilliseconds));
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public PartialViewResult CuentasBancos()
        {
            var totalStopwatch = Stopwatch.StartNew();

            try
            {
                return PartialView("_CuentasBancosPartial");
            }
            finally
            {
                _logger.LogInfo(string.Format(
                    "[PERF][LOGIN][BANCOS] Controller CuentasBancos completado en {0} ms",
                    totalStopwatch.ElapsedMilliseconds));
            }
        }

        [Authorize]
        public ActionResult SeleccionarCompania(string companiaSeleccionada = null, string returnUrl = null)
        {
            var returnUrlSeguro = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null;
            if (!string.IsNullOrWhiteSpace(returnUrlSeguro))
            {
                Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrlSeguro;
            }

            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrlSeguro });
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrlSeguro });
            }

            var esUsuarioRt = EsUsuarioRt(usuario) && !EsAdministradorSesion(usuario);
            var rolActivo = RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"] as string ?? string.Empty);
            var requiereSeleccionCompania = esUsuarioRt || string.Equals(rolActivo, RoleGroupingHelper.Solicitante, StringComparison.OrdinalIgnoreCase);
            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);

            if (!requiereSeleccionCompania)
            {
                if (companiasAsignadas.Count == 1)
                {
                    var unica = companiasAsignadas[0];
                    var nombre = !string.IsNullOrWhiteSpace(unica.CompaniaNombre)
                        ? unica.CompaniaNombre
                        : ResolverNombreCompaniaPorCodigo(unica.CompaniaCodigo);
                    CompaniaActivaSessionHelper.Establecer(Session, unica.CompaniaCodigo, nombre);
                }

                var returnUrlUnica = !string.IsNullOrWhiteSpace(returnUrlSeguro)
                    ? returnUrlSeguro
                    : Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string;
                Session.Remove(CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl);
                return RedireccionarDespuesLogin(usuarioId, returnUrlUnica);
            }

            if (companiasAsignadas.Count == 1)
            {
                var unica = companiasAsignadas[0];
                var nombre = !string.IsNullOrWhiteSpace(unica.CompaniaNombre)
                    ? unica.CompaniaNombre
                    : ResolverNombreCompaniaPorCodigo(unica.CompaniaCodigo);
                CompaniaActivaSessionHelper.Establecer(Session, unica.CompaniaCodigo, nombre);

                var returnUrlUnica = !string.IsNullOrWhiteSpace(returnUrlSeguro)
                    ? returnUrlSeguro
                    : Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string;
                Session.Remove(CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl);
                return RedireccionarDespuesLogin(usuarioId, returnUrlUnica);
            }

            var codigoActivo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            var vm = ConstruirSeleccionCompaniaViewModel(
                companiasAsignadas,
                !string.IsNullOrWhiteSpace(returnUrlSeguro)
                    ? returnUrlSeguro
                    : Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string,
                !string.IsNullOrWhiteSpace(companiaSeleccionada) ? companiaSeleccionada : codigoActivo);

            if (companiasAsignadas.Count == 0)
            {
                ModelState.AddModelError("", "No existen companias asignadas o disponibles para el rol Solicitante. Actualice la asociacion de companias del usuario antes de continuar.");
            }

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult SeleccionarCompania(SeleccionCompaniaViewModel model)
        {
            var returnUrlSeguro = model != null && !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : null;
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrlSeguro });
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrlSeguro });
            }

            if (EsAdministradorSesion(usuario))
            {
                return RedireccionarDespuesLogin(usuarioId, returnUrlSeguro);
            }

            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
            var seleccion = companiasAsignadas.FirstOrDefault(c =>
                string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), (model?.CompaniaSeleccionada ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

            if (seleccion == null)
            {
                var vm = ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    returnUrlSeguro,
                    model != null ? model.CompaniaSeleccionada : null,
                    model != null ? model.NuevaCompaniaCodigo : null,
                    model != null && model.MostrarAgregarCompania);

                ModelState.AddModelError("", "La compañía seleccionada no está asignada a su usuario.");
                return View(vm);
            }

            var nombreCompania = !string.IsNullOrWhiteSpace(seleccion.CompaniaNombre)
                ? seleccion.CompaniaNombre
                : ResolverNombreCompaniaPorCodigo(seleccion.CompaniaCodigo);
            CompaniaActivaSessionHelper.Establecer(Session, seleccion.CompaniaCodigo, nombreCompania);

            var returnUrl = returnUrlSeguro;
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string;
            }

            Session.Remove(CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl);
            return RedireccionarDespuesLogin(usuarioId, returnUrl);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult AgregarCompaniaSeleccion(SeleccionCompaniaViewModel model)
        {
            var returnUrlSeguro = model != null && !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
                ? model.ReturnUrl
                : null;
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrlSeguro });
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrlSeguro });
            }

            if (EsAdministradorSesion(usuario))
            {
                return RedireccionarDespuesLogin(usuarioId, returnUrlSeguro);
            }

            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
            var codigo = (model != null ? model.NuevaCompaniaCodigo : null) ?? string.Empty;
            codigo = codigo.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(codigo))
            {
                ModelState.AddModelError("", "Seleccione una compañía adicional para agregar.");
                return View("SeleccionarCompania", ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    returnUrlSeguro,
                    model != null ? model.CompaniaSeleccionada : null,
                    codigo,
                    true));
            }

            if (companiasAsignadas.Any(c => string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("", "La compañía seleccionada ya está asignada a su usuario.");
                return View("SeleccionarCompania", ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    returnUrlSeguro,
                    model != null ? model.CompaniaSeleccionada : null,
                    codigo,
                    true));
            }

            var nombreCompania = ((model != null ? model.NuevaCompaniaNombre : null) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombreCompania))
            {
                nombreCompania = ResolverNombreCompaniaPorCodigo(codigo);
            }

            if (string.IsNullOrWhiteSpace(nombreCompania))
            {
                ModelState.AddModelError("", "No se pudo validar la compañía seleccionada. Intente nuevamente.");
                return View("SeleccionarCompania", ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    returnUrlSeguro,
                    model != null ? model.CompaniaSeleccionada : null,
                    codigo,
                    true));
            }

            var actor = !string.IsNullOrWhiteSpace(usuario.NombreUsuario)
                ? usuario.NombreUsuario.Trim()
                : ("usuario_" + usuarioId);

            var daoCompanias = new UsuarioCompaniaRTDAO();
            var agregado = false;
            try
            {
                agregado = daoCompanias.AgregarCompania(usuarioId, codigo, nombreCompania, actor);
            }
            catch
            {
                agregado = false;
            }

            if (!agregado)
            {
                ModelState.AddModelError("", "No fue posible agregar la compañía seleccionada a su usuario.");
                return View("SeleccionarCompania", ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    returnUrlSeguro,
                    model != null ? model.CompaniaSeleccionada : null,
                    codigo,
                    true));
            }

            TempData["SeleccionCompaniaSuccess"] = "La compañía se agregó correctamente. Ahora puede seleccionarla para continuar.";
            return RedirectToAction("SeleccionarCompania", new { companiaSeleccionada = codigo, returnUrl = returnUrlSeguro });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarCompaniaActiva(string companiaCodigo, string returnUrl)
        {
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account", new { returnUrl });
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
            var compania = companiasAsignadas.FirstOrDefault(c =>
                string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), (companiaCodigo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

            if (compania == null)
            {
                TempData["LoginError"] = "No tiene permisos para cambiar a la compañía seleccionada.";
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("Index", "Dashboard");
            }

            var nombre = (compania != null && !string.IsNullOrWhiteSpace(compania.CompaniaNombre))
                ? compania.CompaniaNombre
                : ResolverNombreCompaniaPorCodigo(companiaCodigo);

            CompaniaActivaSessionHelper.Establecer(Session, companiaCodigo, nombre);
            CompaniaActivaSessionHelper.LimpiarDatosTemporalesCambioCompania(Session, usuarioId);
            Session.Remove("_Sidebar_OrdenStatus_" + usuarioId);
            if (!string.IsNullOrWhiteSpace(companiaCodigo))
            {
                Session.Remove("_Sidebar_OrdenStatus_" + usuarioId + "_" + companiaCodigo);
            }

            TempData["OK"] = "Compañía activa actualizada correctamente.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }

        private void ExpirarCookieAntiForgery()
        {
            try
            {
                var cookieName = System.Web.Helpers.AntiForgeryConfig.CookieName;
                var existente = Request.Cookies[cookieName];
                if (existente == null)
                {
                    return;
                }

                var expired = new HttpCookie(cookieName)
                {
                    Value = string.Empty,
                    Expires = DateTime.UtcNow.AddDays(-1),
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    Path = "/"
                };
                CookieHelper.SetSameSiteLax(expired);
                Response.Cookies.Add(expired);
            }
            catch
            {
                // best-effort
            }
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            string motivo;
            var returnUrlPermitido = ResolverReturnUrlPermitido(returnUrl, ObtenerRolesSesion(), out motivo);
            if (!string.IsNullOrWhiteSpace(returnUrlPermitido))
            {
                return Redirect(returnUrlPermitido);
            }

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                _logger.LogWarning(string.Format(
                    "[AUTH][RETURN_URL] ReturnUrl descartado en RedirectToLocal. Usuario={0}; Roles={1}; ReturnUrl={2}; Motivo={3}; Destino=Dashboard/Index",
                    Session["CodigoUsuario"] as string ?? string.Empty,
                    string.Join(",", ObtenerRolesSesion()),
                    returnUrl,
                    motivo ?? string.Empty));
            }

            return RedirectToAction("Index", "Dashboard");
        }

        [Authorize]
        public ActionResult CambiarRol(string rolSeleccionado)
        {
            var roles = RoleGroupingHelper.BuildUnifiedRoles(
                RoleGroupingHelper.ExtractRoles(Session["RolesRaw"] ?? Session["Roles"], Session["Rol"] as string));
            var rolUnificado = RoleGroupingHelper.NormalizeSelectedRole(rolSeleccionado);

            if (!string.IsNullOrWhiteSpace(rolUnificado) &&
                roles.Contains(rolUnificado, StringComparer.OrdinalIgnoreCase))
            {
                var match = roles.First(r => r.Equals(rolUnificado, StringComparison.OrdinalIgnoreCase));
                Session["Rol"] = match;
                PersistirRolSeleccionado(match, null);
                ActualizarTicketAutenticacionRolSeleccionado(match);
            }

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            if (Request.Cookies[FormsAuthentication.FormsCookieName] != null)
            {
                var c = new HttpCookie(FormsAuthentication.FormsCookieName)
                {
                    Expires = DateTime.Now.AddDays(-1),
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    Path = FormsAuthentication.FormsCookiePath
                };
                CookieHelper.SetSameSiteLax(c);
                Response.Cookies.Add(c);
            }
            ExpirarCookieAntiForgery();
            ExpirarCookieRolSeleccionado();

            return RedirectToAction("Login", "Account");
        }

        [Authorize]
        public ActionResult CambiarContrasena()
        {
            return View(new CambiarContrasenaViewModel());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarContrasena(CambiarContrasenaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            int idUsuario = ObtenerUsuarioSesionId();

            if (idUsuario <= 0)
            {
                ModelState.AddModelError("", "Sesión expirada. Inicie sesión nuevamente.");
                return View(model);
            }

            var usuario = UsuarioDAO.ObtenerPorId(idUsuario);
            if (usuario == null)
            {
                ModelState.AddModelError("", "Usuario no encontrado.");
                return View(model);
            }

            if (!PasswordHelper.VerifyPassword(model.ContrasenaActual, usuario.Contrasena))
            {
                ModelState.AddModelError("", "La contraseña actual es incorrecta.");
                return View(model);
            }

            if (model.NuevaContrasena != model.ConfirmarContrasena)
            {
                ModelState.AddModelError("", "La nueva contraseña y la confirmación no coinciden.");
                return View(model);
            }

            var (esValida, mensaje) = PasswordHelper.ValidarFortaleza(model.NuevaContrasena);
            if (!esValida)
            {
                ModelState.AddModelError("", mensaje);
                return View(model);
            }

            string hash = PasswordHelper.HashPassword(model.NuevaContrasena);
            string msg;
            if (!UsuarioDAO.ActualizarContrasena(idUsuario, hash, out msg))
            {
                ModelState.AddModelError("", msg);
                return View(model);
            }

            UsuarioDAO.ActualizarUltimaConexion(idUsuario);

            var usuarioActualizado = UsuarioDAO.ObtenerPorId(idUsuario);
            if (usuarioActualizado == null)
            {
                FormsAuthentication.SignOut();
                Session.Clear();
                Session.Abandon();
                TempData["LoginError"] = "No se pudo recuperar el perfil del usuario luego de actualizar su contraseña.";
                return RedirectToAction("Login", "Account");
            }

            var rolesSesion = ObtenerRolesSesion();
            var esUsuarioRt = EsUsuarioRt(usuarioActualizado) && !EsUsuarioAdministrador(usuarioActualizado, rolesSesion);
            if (esUsuarioRt)
            {
                var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuarioActualizado);
                if (companiasAsignadas.Count == 0)
                {
                    FormsAuthentication.SignOut();
                    Session.Clear();
                    Session.Abandon();
                    TempData["LoginError"] = "Su usuario RT no tiene compañías asignadas. Solicite al administrador la asociación correspondiente.";
                    return RedirectToAction("Login", "Account");
                }

                if (companiasAsignadas.Count == 1)
                {
                    var unica = companiasAsignadas[0];
                    var nombreCompania = !string.IsNullOrWhiteSpace(unica.CompaniaNombre)
                        ? unica.CompaniaNombre
                        : ResolverNombreCompaniaPorCodigo(unica.CompaniaCodigo);
                    CompaniaActivaSessionHelper.Establecer(Session, unica.CompaniaCodigo, nombreCompania);
                }
                else
                {
                    return RedirectToAction("SeleccionarCompania");
                }
            }
            else if (!string.IsNullOrWhiteSpace(usuarioActualizado.EmpresaCodigo))
            {
                var nombreEmpresa = ResolverNombreCompaniaPorCodigo(usuarioActualizado.EmpresaCodigo);
                CompaniaActivaSessionHelper.Establecer(Session, usuarioActualizado.EmpresaCodigo, nombreEmpresa);
            }

            var returnUrlPendiente = Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string;
            Session.Remove(CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl);
            return RedirectToLocal(returnUrlPendiente);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public JsonResult EnviarRecuperar(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { ok = false, mensaje = "Debe ingresar un correo electrónico válido." });

            string mensaje;
            bool enviado = UsuarioBL.RestablecerContrasenaPorEmail(email.Trim(), out mensaje);

            return Json(new { ok = enviado, mensaje = mensaje });
        }

        [Authorize]
        public ActionResult _ModalRegistroUsuario()
        {
            var model = new UsuarioCreateViewModel
            {
                RolesDisponibles = RolBL.ObtenerActivos()
            };
            return PartialView("_ModalRegistroUsuario", model);
        }

        // ✅ AJAX: verificación de orden (tu layout lo consulta)
        [HttpGet]
        [Authorize]
        public JsonResult VerificarEstadoOrden()
        {
            try
            {
                int idUsuario = 0;
                idUsuario = ObtenerUsuarioSesionId();

                if (idUsuario <= 0)
                    return Json(new { tieneOrden = false, mensaje = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                var ordenDAO = new OrdenRecaudacionDAO();
                bool tieneOrden = ordenDAO.TieneOrdenHabilitanteAOCR(idUsuario);
                bool tieneBorrador = ordenDAO.ExisteORMinima(idUsuario);
                bool tieneOrdenPendiente = ordenDAO.TieneOrdenActivaEnProceso(idUsuario);
                bool tieneOrdenPendienteComprobante = ordenDAO.TieneOrdenPendienteComprobante(idUsuario);

                return Json(new
                {
                    tieneOrdenGenerada = tieneOrden,
                    tieneOrdenBorrador = tieneBorrador,
                    tieneOrdenPendiente = tieneOrdenPendiente,
                    tieneOrdenPendienteComprobante = tieneOrdenPendienteComprobante,
                    redireccionar = !(tieneOrden || tieneOrdenPendiente || tieneBorrador)
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // ✅ tu layout llama POST /Account/ExtenderSesion
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public JsonResult ExtenderSesion()
        {
            Session.Timeout = SessionTimeoutHelper.GetTimeoutMinutes();
            Session["LastActivity"] = DateTime.Now;
            return Json(new { ok = true, timeoutMinutes = Session.Timeout });
        }

        private static bool EsUsuarioRt(Usuario usuario)
        {
            return usuario != null &&
                   !string.IsNullOrWhiteSpace(usuario.EstadoDesignacionRT) &&
                   usuario.EstadoDesignacionRT.Trim().Equals("aceptado", StringComparison.OrdinalIgnoreCase);
        }

        private bool EsAdministradorSesion(Usuario usuario)
        {
            var rolesSesion = ObtenerRolesSesion();
            return EsUsuarioAdministrador(usuario, rolesSesion);
        }

        private List<string> ObtenerRolesSesion()
        {
            var roles = new List<string>();
            try
            {
                roles.AddRange(RoleGroupingHelper.ExtractRoles(Session["RolesRaw"]));
                roles.AddRange(RoleGroupingHelper.ExtractRoles(Session["Roles"]));

                var rolUnico = Session["Rol"] as string;
                if (!string.IsNullOrWhiteSpace(rolUnico))
                {
                    roles.Add(rolUnico);
                }
            }
            catch
            {
                // best-effort
            }

            var usuarioClave = FirstNonEmpty(
                Session["NombreUsuario"] as string,
                Session["CodigoUsuario"] as string,
                Session["Email"] as string,
                User != null && User.Identity != null ? User.Identity.Name : null);

            return RoleGroupingHelper.SanitizeRawRolesForUser(usuarioClave, roles)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void LimpiarAutenticacionParaMostrarLogin()
        {
            var limpiezaStopwatch = Stopwatch.StartNew();

            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();

            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie != null)
            {
                var c = new HttpCookie(FormsAuthentication.FormsCookieName)
                {
                    Value = string.Empty,
                    Expires = DateTime.Now.AddDays(-1),
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    Path = FormsAuthentication.FormsCookiePath
                };
                CookieHelper.SetSameSiteLax(c);
                Response.Cookies.Add(c);
            }

            ExpirarCookieAntiForgery();

            var anonymous = new GenericPrincipal(new GenericIdentity(string.Empty), null);
            HttpContext.User = anonymous;
            if (System.Web.HttpContext.Current != null)
            {
                System.Web.HttpContext.Current.User = anonymous;
            }
            Thread.CurrentPrincipal = anonymous;

            _logger.LogInfo(string.Format(
                "[PERF][LOGIN] Limpieza de autenticacion previa completada en {0} ms",
                limpiezaStopwatch.ElapsedMilliseconds));
        }

        private bool TryRestaurarSesionUsuarioActual(out Usuario usuario, out List<string> roles, out string loginUsado)
        {
            usuario = null;
            roles = ObtenerRolesSesion();
            loginUsado = (Session["CodigoUsuario"] as string ?? string.Empty).Trim();

            var usuarioIdActual = Session["UserId"] ?? Session["IdUsuario"];
            int usuarioId;
            if (usuarioIdActual != null && int.TryParse(usuarioIdActual.ToString(), out usuarioId) && usuarioId > 0)
            {
                try
                {
                    usuario = UsuarioDAO.ObtenerPorId(usuarioId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Account/Login: error restaurando usuario por id de sesion: " + ex.Message);
                }

                if (usuario != null && usuario.Id > 0)
                {
                    roles = CompletarRolesUsuario(usuario, roles);
                    SincronizarSesionAutenticada(usuario, roles, loginUsado, limpiarCompaniaActiva: false);
                    loginUsado = !string.IsNullOrWhiteSpace(usuario.CodigoUsuario) ? usuario.CodigoUsuario.Trim() : loginUsado;
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
                if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                {
                    identidades.Add(User.Identity.Name);

                    if (HttpContext != null && HttpContext.User != null && HttpContext.User.Identity != null)
                    {
                        identidades.Add(HttpContext.User.Identity.Name);
                    }

                    if (Request != null && Request.LogonUserIdentity != null)
                    {
                        identidades.Add(Request.LogonUserIdentity.Name);
                    }
                }
            }
            catch (Exception exIdentity)
            {
                Debug.WriteLine("Account/Login: error obteniendo identidades autenticadas: " + exIdentity.Message);
            }

            foreach (var identidad in identidades.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Usuario usuarioPorLogin;
                if (!TryResolverUsuarioPorLogin(identidad, out usuarioPorLogin))
                {
                    continue;
                }

                usuario = usuarioPorLogin;
                roles = CompletarRolesUsuario(usuario, roles);
                SincronizarSesionAutenticada(usuario, roles, identidad, limpiarCompaniaActiva: false);
                loginUsado = identidad;
                return true;
            }

            return false;
        }

        private static List<string> ExpandirCandidatosLogin(string valor)
        {
            var candidatos = new List<string>();
            var bruto = (valor ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(bruto))
            {
                return candidatos;
            }

            candidatos.Add(bruto);

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

        private bool TryResolverUsuarioPorLogin(string loginInput, out Usuario usuario)
        {
            usuario = null;

            foreach (var candidato in ExpandirCandidatosLogin(loginInput))
            {
                try
                {
                    usuario = UsuarioDAO.ObtenerPorNombreUsuario(candidato);
                    if (usuario != null && usuario.Id > 0)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Account/Login: error resolviendo usuario por login '" + candidato + "': " + ex.Message);
                }
            }

            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private List<string> CompletarRolesUsuario(Usuario usuario, IEnumerable<string> rolesBase)
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
                catch (Exception ex)
                {
                    Debug.WriteLine("Account/Login: error obteniendo roles desde base: " + ex.Message);
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

        private void SincronizarSesionAutenticada(Usuario usuario, IEnumerable<string> roles, string loginFallback, bool limpiarCompaniaActiva)
        {
            if (usuario == null || usuario.Id <= 0)
            {
                return;
            }

            Session["UserId"] = usuario.Id;
            Session["IdUsuario"] = usuario.Id;
            Session["CodigoUsuario"] = !string.IsNullOrWhiteSpace(usuario.CodigoUsuario)
                ? usuario.CodigoUsuario.Trim()
                : (loginFallback ?? usuario.NombreUsuario ?? string.Empty).Trim();

            Session["NombreUsuario"] = !string.IsNullOrWhiteSpace(usuario.NombreCompleto)
                ? usuario.NombreCompleto.Trim()
                : (!string.IsNullOrWhiteSpace(usuario.NombreUsuario)
                    ? usuario.NombreUsuario.Trim()
                    : "Usuario");

            Session["Correo"] = !string.IsNullOrWhiteSpace(usuario.Email)
                ? usuario.Email.Trim()
                : (Session["Correo"] as string ?? string.Empty).Trim();

            if (limpiarCompaniaActiva)
            {
                CompaniaActivaSessionHelper.Limpiar(Session);
            }

            var usuarioClaveRoles = FirstNonEmpty(usuario.NombreUsuario, usuario.CodigoUsuario, usuario.Email, loginFallback);
            var rolesRaw = RoleGroupingHelper.SanitizeRawRolesForUser(usuarioClaveRoles, roles ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var rolesUnificados = RoleGroupingHelper.BuildUnifiedRoles(rolesRaw);
            var rolActual = RoleGroupingHelper.ResolveSelectedRoleForUser(
                usuarioClaveRoles,
                rolesUnificados,
                ResolverRolSeleccionadoPersistido(rolesUnificados));
            var rolSeleccionado = rolesUnificados.FirstOrDefault(r =>
                string.Equals(r, rolActual, StringComparison.OrdinalIgnoreCase));

            Session["RolesRaw"] = rolesRaw;
            Session["Roles"] = rolesUnificados;
            Session["Rol"] = !string.IsNullOrWhiteSpace(rolSeleccionado)
                ? rolSeleccionado
                : (rolesUnificados.Count > 0 ? rolesUnificados[0] : null);
            Session["RolActivo"] = Session["Rol"];
            Session["RolActual"] = Session["Rol"];
            Session.Timeout = SessionTimeoutHelper.GetTimeoutMinutes();
            Session["LastActivity"] = DateTime.Now;

            try
            {
                var rolesTicket = LeerRolesDesdeTicket();
                var rolesBd = new List<string>();
                try
                {
                    rolesBd = RoleGroupingHelper.SanitizeRawRolesForUser(
                        usuarioClaveRoles,
                        UsuarioDAO.ObtenerRoles(usuario.Id) ?? new List<string>()).ToList();
                }
                catch
                {
                    rolesBd = new List<string>();
                }

                _logger.LogInfo(string.Format(
                    "[AOCR][ROLES_SYNC] Usuario={0}; RolesBD={1}; RolesTicket={2}; RolesSession={3}; RolActivo={4}; Resultado=OK",
                    usuarioClaveRoles,
                    string.Join(",", rolesBd),
                    string.Join(",", rolesTicket),
                    string.Join(",", rolesRaw),
                    Session["Rol"] as string ?? string.Empty));

                _logger.LogInfo(string.Format(
                    "[AOCR][ROL_ACTIVO_RESUELTO] UsuarioId={0}; Login={1}; RolesRaw={2}; RolesUnificados={3}; RolPersistido={4}; RolActivo={5}; LimpiarCompania={6}; CompaniaActiva={7}",
                    usuario.Id,
                    Session["CodigoUsuario"] as string ?? string.Empty,
                    string.Join(",", rolesRaw),
                    string.Join(",", rolesUnificados),
                    rolActual ?? string.Empty,
                    Session["Rol"] as string ?? string.Empty,
                    limpiarCompaniaActiva,
                    Session[CompaniaActivaSessionHelper.SessionCompaniaActivaCodigo] as string ?? string.Empty));
            }
            catch
            {
            }
        }

        private string ResolverRolSeleccionadoPersistido(IEnumerable<string> rolesUnificados)
        {
            var rolesDisponibles = (rolesUnificados ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var candidatos = new[]
            {
                AuthTicketRoleDataHelper.ReadSelectedRoleFromCookie(Request != null ? Request.Cookies : null),
                LeerRolSeleccionadoDesdeTicket(),
                RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"] as string ?? string.Empty)
            };

            foreach (var candidato in candidatos)
            {
                if (!string.IsNullOrWhiteSpace(candidato)
                    && rolesDisponibles.Contains(candidato, StringComparer.OrdinalIgnoreCase))
                {
                    return candidato;
                }
            }

            return rolesDisponibles.FirstOrDefault() ?? string.Empty;
        }

        private string LeerRolSeleccionadoDesdeTicket()
        {
            try
            {
                var authCookie = Request != null ? Request.Cookies[FormsAuthentication.FormsCookieName] : null;
                if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
                {
                    return string.Empty;
                }

                var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                if (authTicket == null || authTicket.Expired)
                {
                    return string.Empty;
                }

                return AuthTicketRoleDataHelper.Deserialize(authTicket.UserData).SelectedRole;
            }
            catch
            {
                return string.Empty;
            }
        }

        private IList<string> LeerRolesDesdeTicket()
        {
            try
            {
                var authCookie = Request != null ? Request.Cookies[FormsAuthentication.FormsCookieName] : null;
                if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
                {
                    return new List<string>();
                }

                var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                if (authTicket == null || authTicket.Expired)
                {
                    return new List<string>();
                }

                return AuthTicketRoleDataHelper.Deserialize(authTicket.UserData).Roles;
            }
            catch
            {
                return new List<string>();
            }
        }

        private void ActualizarTicketAutenticacionRolSeleccionado(string rolSeleccionado)
        {
            try
            {
                var authCookie = Request != null ? Request.Cookies[FormsAuthentication.FormsCookieName] : null;
                if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
                {
                    return;
                }

                var authTicket = FormsAuthentication.Decrypt(authCookie.Value);
                if (authTicket == null || authTicket.Expired)
                {
                    return;
                }

                var rolesRaw = RoleGroupingHelper.ExtractRoles(Session["RolesRaw"] ?? Session["Roles"]);
                if (rolesRaw.Count == 0)
                {
                    rolesRaw = AuthTicketRoleDataHelper.Deserialize(authTicket.UserData).Roles.ToList();
                }

                rolesRaw = RoleGroupingHelper.SanitizeRawRolesForUser(
                    Session["CodigoUsuario"] as string ?? authTicket.Name,
                    rolesRaw).ToList();
                rolSeleccionado = RoleGroupingHelper.ResolveSelectedRoleForUser(
                    Session["CodigoUsuario"] as string ?? authTicket.Name,
                    RoleGroupingHelper.BuildUnifiedRoles(rolesRaw),
                    rolSeleccionado);

                var expiracion = authTicket.IsPersistent && authTicket.Expiration > DateTime.Now
                    ? authTicket.Expiration
                    : DateTime.Now.AddMinutes(SessionTimeoutHelper.GetTimeoutMinutes());

                var ticketActualizado = new FormsAuthenticationTicket(
                    authTicket.Version,
                    authTicket.Name,
                    DateTime.Now,
                    expiracion,
                    authTicket.IsPersistent,
                    AuthTicketRoleDataHelper.Serialize(rolesRaw, rolSeleccionado),
                    string.IsNullOrWhiteSpace(authTicket.CookiePath) ? FormsAuthentication.FormsCookiePath : authTicket.CookiePath);

                var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticketActualizado))
                {
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    Path = string.IsNullOrWhiteSpace(ticketActualizado.CookiePath) ? FormsAuthentication.FormsCookiePath : ticketActualizado.CookiePath
                };
                CookieHelper.SetSameSiteLax(cookie);

                if (authTicket.IsPersistent)
                {
                    cookie.Expires = expiracion;
                }

                Response.Cookies.Add(cookie);
            }
            catch
            {
            }
        }

        private void PersistirRolSeleccionado(string rolSeleccionado, DateTime? expires)
        {
            var rolNormalizado = RoleGroupingHelper.NormalizeSelectedRole(rolSeleccionado);
            if (string.IsNullOrWhiteSpace(rolNormalizado) || Response == null)
            {
                return;
            }

            var cookie = new HttpCookie(AuthTicketRoleDataHelper.SelectedRoleCookieName, Uri.EscapeDataString(rolNormalizado))
            {
                HttpOnly = true,
                Secure = Request != null && Request.IsSecureConnection,
                Path = "/"
            };
            CookieHelper.SetSameSiteLax(cookie);

            if (expires.HasValue)
            {
                cookie.Expires = expires.Value;
            }

            Response.Cookies.Add(cookie);
        }

        private void ExpirarCookieRolSeleccionado()
        {
            if (Response == null)
            {
                return;
            }

            var cookie = new HttpCookie(AuthTicketRoleDataHelper.SelectedRoleCookieName)
            {
                Value = string.Empty,
                Expires = DateTime.Now.AddDays(-1),
                HttpOnly = true,
                Secure = Request != null && Request.IsSecureConnection,
                Path = "/"
            };
            CookieHelper.SetSameSiteLax(cookie);
            Response.Cookies.Add(cookie);
        }

        private void EstablecerCompaniaActiva(UsuarioCompaniaRT compania)
        {
            if (compania == null || string.IsNullOrWhiteSpace(compania.CompaniaCodigo))
            {
                return;
            }

            var codigo = compania.CompaniaCodigo.Trim();
            var nombre = !string.IsNullOrWhiteSpace(compania.CompaniaNombre)
                ? compania.CompaniaNombre.Trim()
                : ResolverNombreCompaniaPorCodigo(codigo);

            CompaniaActivaSessionHelper.Establecer(Session, codigo, nombre);
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

        private int ObtenerUsuarioSesionId()
        {
            var v = Session["UserId"] ?? Session["IdUsuario"];
            int id;
            if (v != null && int.TryParse(v.ToString(), out id) && id > 0)
            {
                Session["UserId"] = id;
                Session["IdUsuario"] = id;
                return id;
            }

            Usuario usuario;
            List<string> roles;
            string loginUsado;
            if (TryRestaurarSesionUsuarioActual(out usuario, out roles, out loginUsado) && usuario != null)
            {
                return usuario.Id;
            }

            return 0;
        }

        private SeleccionCompaniaViewModel ConstruirSeleccionCompaniaViewModel(
            IEnumerable<UsuarioCompaniaRT> companiasAsignadas,
            string returnUrl,
            string companiaSeleccionada = null,
            string nuevaCompaniaCodigo = null,
            bool mostrarAgregarCompania = false)
        {
            var lista = (companiasAsignadas ?? Enumerable.Empty<UsuarioCompaniaRT>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                .Select(c => new CompaniaAsignadaViewModel
                {
                    Codigo = (c.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant(),
                    Nombre = !string.IsNullOrWhiteSpace(c.CompaniaNombre)
                        ? c.CompaniaNombre.Trim()
                        : ResolverNombreCompaniaPorCodigo(c.CompaniaCodigo)
                })
                .GroupBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.Nombre ?? string.Empty)
                .ToList();

            var seleccion = (companiaSeleccionada ?? string.Empty).Trim().ToUpperInvariant();
            if (!lista.Any(c => string.Equals(c.Codigo, seleccion, StringComparison.OrdinalIgnoreCase)))
            {
                seleccion = string.Empty;
            }

            return new SeleccionCompaniaViewModel
            {
                ReturnUrl = returnUrl,
                CompaniaSeleccionada = seleccion,
                NuevaCompaniaCodigo = (nuevaCompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant(),
                MostrarAgregarCompania = mostrarAgregarCompania,
                Companias = lista
            };
        }

        private List<UsuarioCompaniaRT> ObtenerCompaniasAsignadasConFallback(Usuario usuario)
        {
            var resultado = new List<UsuarioCompaniaRT>();
            if (usuario == null || usuario.Id <= 0)
            {
                return resultado;
            }

            var daoCompanias = new UsuarioCompaniaRTDAO();
            try
            {
                resultado = daoCompanias.ObtenerCompaniasAsignadas(usuario.Id);
            }
            catch
            {
                resultado = new List<UsuarioCompaniaRT>();
            }

            var codigosLegacy = ParsearCodigosCompaniaLegacy(usuario.EmpresaCodigo);
            foreach (var codigo in codigosLegacy)
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

            var codigosHistorial = new List<string>();
            if (!string.IsNullOrWhiteSpace(usuario.Email))
            {
                try
                {
                    var declaracionDao = new DeclaracionTemporalDAO();
                    var historial = declaracionDao.GetUltimaAceptadaHistorial(usuario.Email);
                    if (historial != null)
                    {
                        codigosHistorial = ParsearCodigosCompaniaLegacy(historial.EmpresaCodigo);
                        if (codigosHistorial.Count == 0)
                        {
                            codigosHistorial = ExtraerCodigosCompaniaDesdeTexto(historial.EmpresaNombre);
                        }
                    }
                }
                catch
                {
                    codigosHistorial = new List<string>();
                }
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

            // Best effort: persistir fallback legacy en tabla relacional, sin bloquear login.
            if (codigosLegacy.Count > 0)
            {
                foreach (var codigo in codigosLegacy)
                {
                    try
                    {
                        if (!daoCompanias.UsuarioTieneCompaniaAsignada(usuario.Id, codigo))
                        {
                            daoCompanias.AgregarCompania(usuario.Id, codigo, ResolverNombreCompaniaPorCodigo(codigo), "fallback_legacy");
                        }
                    }
                    catch
                    {
                        // Ignorar para no interrumpir autenticacion.
                    }
                }
            }

            if (codigosHistorial.Count > 0)
            {
                foreach (var codigo in codigosHistorial)
                {
                    try
                    {
                        if (!daoCompanias.UsuarioTieneCompaniaAsignada(usuario.Id, codigo))
                        {
                            daoCompanias.AgregarCompania(usuario.Id, codigo, ResolverNombreCompaniaPorCodigo(codigo), "fallback_historial");
                        }
                    }
                    catch
                    {
                        // Ignorar para no interrumpir autenticacion.
                    }
                }
            }

            return resultado
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.CompaniaCodigo))
                .GroupBy(c => (c.CompaniaCodigo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.CompaniaCodigo)
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

        private string ResolverNombreCompaniaPorCodigo(string codigoEmpresa)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Account: no se pudo resolver nombre de empresa: " + ex.Message);
                return string.Empty;
            }
        }

        private string ResolverReturnUrlPermitido(string returnUrl, IEnumerable<string> roles, out string motivo)
        {
            motivo = "ReturnUrl vacio";
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                return null;
            }

            if (!Url.IsLocalUrl(returnUrl))
            {
                motivo = "ReturnUrl no local";
                return null;
            }

            var path = ObtenerPathLocalNormalizado(returnUrl);
            if (string.IsNullOrWhiteSpace(path))
            {
                motivo = "No se pudo resolver path local";
                return null;
            }

            if (path.StartsWith("/Account/Login", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Account/Logout", StringComparison.OrdinalIgnoreCase))
            {
                motivo = "ReturnUrl apunta a Account/Login o Account/Logout";
                return null;
            }

            if (!UsuarioPuedeAccederReturnUrl(path, roles, out motivo))
            {
                return null;
            }

            motivo = "ReturnUrl permitido";
            return returnUrl;
        }

        private string ObtenerPathLocalNormalizado(string returnUrl)
        {
            var raw = (returnUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var queryIndex = raw.IndexOf('?');
            if (queryIndex >= 0)
            {
                raw = raw.Substring(0, queryIndex);
            }

            if (!raw.StartsWith("/", StringComparison.Ordinal))
            {
                raw = "/" + raw;
            }

            var appPath = Request != null ? (Request.ApplicationPath ?? string.Empty) : string.Empty;
            if (!string.IsNullOrWhiteSpace(appPath) &&
                !string.Equals(appPath, "/", StringComparison.Ordinal) &&
                raw.StartsWith(appPath, StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring(appPath.Length);
                if (!raw.StartsWith("/", StringComparison.Ordinal))
                {
                    raw = "/" + raw;
                }
            }
            else if (raw.StartsWith("/aocr/", StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring("/aocr".Length);
            }

            return raw;
        }

        private bool UsuarioPuedeAccederReturnUrl(string path, IEnumerable<string> roles, out string motivo)
        {
            var rolesUsuario = (roles ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .ToList();
            rolesUsuario.AddRange(RoleGroupingHelper.BuildUnifiedRoles(rolesUsuario));
            rolesUsuario = rolesUsuario
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (path.StartsWith("/Tecnico", StringComparison.OrdinalIgnoreCase))
            {
                var permitido = rolesUsuario.Any(r =>
                    string.Equals(r, "Administrador", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Coordinacion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "Coordinador", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "CoordinadorInspecciones", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "CoordinacionLegal", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r, "CoordinadorLegal", StringComparison.OrdinalIgnoreCase));

                motivo = permitido
                    ? "Ruta Tecnico permitida por rol"
                    : "Ruta Tecnico no permitida para roles=" + string.Join(",", rolesUsuario);
                return permitido;
            }

            if (path.StartsWith("/Error", StringComparison.OrdinalIgnoreCase))
            {
                motivo = "ReturnUrl a Error descartado";
                return false;
            }

            motivo = "Ruta local permitida por validacion generica";
            return true;
        }

        private string ResolverDestinoPorRolYProceso(int usuarioId, string rolActivo, string companiaActiva)
        {
            if (string.IsNullOrWhiteSpace(rolActivo) || !RoleGroupingHelper.IsSolicitante(rolActivo))
            {
                if (RoleGroupingHelper.IsCoordinacion(rolActivo))
                {
                    return Url.Action("Index", "Tecnico");
                }
                return Url.Action("Index", "Dashboard");
            }

            if (string.IsNullOrWhiteSpace(companiaActiva))
            {
                return Url.Action("Index", "Dashboard");
            }

            try
            {
                var procesoService = new CapaNegocio.Services.AocrProcesoActivoService();
                var info = procesoService.ObtenerProcesoActivoPorCompania(usuarioId, companiaActiva);

                // 4. SolicitudAOCR/Detalle o Continuar si tiene proceso habilitado.
                if (info.SolicitudActiva != null)
                {
                    return Url.Action("Detalle", "SolicitudAOCR", new { id = info.SolicitudActiva.CodigoSolicitud });
                }

                // If no active request, check active order
                if (info.OrdenActiva != null)
                {
                    return Url.Action("Index", "OrdenRecaudacion");
                }

                // Check if they have a paid/habilitante order to start a new request
                var ordenDao = new OrdenRecaudacionDAO();
                var compContext = new CapaNegocio.Services.AocrCompaniaContextService();
                var rawOrdenes = ordenDao.ListarPorUsuario(usuarioId, null);
                var compNombre = ResolverNombreCompaniaPorCodigo(companiaActiva);
                var ordenesCompania = compContext.FiltrarOrdenesPorCompania(rawOrdenes, companiaActiva, compNombre, usuarioId);

                // Look for an order that is Facturada/Pagada/Completada
                var ordenHabilitante = ordenesCompania
                    .Where(o => o != null && o.Id > 0)
                    .Where(o => {
                        var est = (o.Estado ?? string.Empty).Trim().ToUpperInvariant();
                        return est == "FACTURADA" || est == "PAGADA" || est == "COMPLETADA" || est == "FACTURADO";
                    })
                    .OrderByDescending(o => o.FechaCreacion)
                    .FirstOrDefault();

                if (ordenHabilitante != null)
                {
                    // Check if a request has already been created for this order
                    var solDao = new SolicitudAOCRDAO();
                    var solicitudesRaw = solDao.ObtenerPorUsuario(usuarioId);
                    var tieneSolicitudParaOrden = solicitudesRaw.Any(s => {
                        if (s == null) return false;
                        var ordIdStr = ordenHabilitante.CodigoSolicitud.HasValue ? ordenHabilitante.CodigoSolicitud.Value.ToString() : string.Empty;
                        var solIdStr = s.CodigoSolicitud.ToString();
                        return !string.IsNullOrWhiteSpace(ordIdStr) && ordIdStr == solIdStr;
                    });

                    if (!tieneSolicitudParaOrden)
                    {
                        return Url.Action("FormularioEmisionAOCR", "SolicitudAOCR", new { oid = ordenHabilitante.Id });
                    }
                }

                // If no active request, no active order, and no unused paid order:
                // Check if they have ANY completed/finalized request for this company to show final docs
                var solDaoCheck = new SolicitudAOCRDAO();
                var solicitudesCompania = compContext.FiltrarSolicitudesPorCompania(solDaoCheck.ObtenerPorUsuario(usuarioId), companiaActiva, compNombre);
                var tieneProcesoTerminado = solicitudesCompania.Any(s => s != null && !new CapaNegocio.Services.AocrEstadoService().EsEstadoActivoProceso(s.Estado));
                
                if (tieneProcesoTerminado)
                {
                    // 5. Documentos finales si el proceso ya terminó.
                    return Url.Action("Index", "Dashboard");
                }

                // 3. OrdenRecaudacion/Nueva si debe generar orden.
                return Url.Action("Nueva", "OrdenRecaudacion");
            }
            catch (Exception ex)
            {
                _logger.LogError(string.Format("[AOCR_FIX][RESOLVER_DESTINO_ERROR] Error al resolver destino por rol y proceso. Ex={0}", ex.ToString()));
                return Url.Action("Index", "Dashboard");
            }
        }

        private ActionResult RedireccionarDespuesLogin(int usuarioId, string returnUrl)
        {
            var companiaActiva = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            return RedireccionarDespuesLogin(usuarioId, returnUrl, companiaActiva);
        }

        private ActionResult RedireccionarDespuesLogin(int usuarioId, string returnUrl, string companiaCodigo)
        {
            var rolesSesion = ObtenerRolesSesion();
            string motivoReturnUrl;
            var returnUrlPermitido = ResolverReturnUrlPermitido(returnUrl, rolesSesion, out motivoReturnUrl);
            
            // Check if returnUrl points to unauthorized or error
            if (!string.IsNullOrWhiteSpace(returnUrlPermitido) &&
                !returnUrlPermitido.Contains("/Error/") &&
                !returnUrlPermitido.Contains("NoAutorizado"))
            {
                _logger.LogInfo(string.Format(
                    "[AUTH][LOGIN_REDIRECT] UsuarioId={0}; Login={1}; Roles={2}; ReturnUrl={3}; Destino={4}; Motivo={5}",
                    usuarioId,
                    Session["CodigoUsuario"] as string ?? string.Empty,
                    string.Join(",", rolesSesion),
                    returnUrl ?? string.Empty,
                    returnUrlPermitido,
                    motivoReturnUrl));
                return Redirect(returnUrlPermitido);
            }

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                _logger.LogWarning(string.Format(
                    "[AUTH][LOGIN_REDIRECT] ReturnUrl descartado o no seguro/autorizado. UsuarioId={0}; Login={1}; Roles={2}; ReturnUrl={3}; Motivo={4}",
                    usuarioId,
                    Session["CodigoUsuario"] as string ?? string.Empty,
                    string.Join(",", rolesSesion),
                    returnUrl,
                    motivoReturnUrl));
            }

            var rolSesion = RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"] as string ?? string.Empty);
            
            // Add Log: [AUTH][COMPANIA_SESSION]
            var companiaActiva = companiaCodigo ?? CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            _logger.LogInfo(string.Format(
                "[AUTH][COMPANIA_SESSION] UsuarioId={0}; CompaniaActiva={1}",
                usuarioId,
                companiaActiva ?? "null"));

            var destino = ResolverDestinoPorRolYProceso(usuarioId, rolSesion, companiaActiva);
            
            _logger.LogInfo(string.Format(
                "[AUTH][LOGIN_REDIRECT_RESUELTO] UsuarioId={0}; Rol={1}; ReturnUrl={2}; Destino={3}; Motivo=ResolverDestinoPorRolYProceso",
                usuarioId,
                rolSesion ?? "null",
                returnUrl ?? "null",
                destino ?? "null"));

            // Add Log: [AUTH][REDIRECT_FINAL]
            _logger.LogInfo(string.Format(
                "[AUTH][REDIRECT_FINAL] UsuarioId={0}; Rol={1}; Destino={2}",
                usuarioId,
                rolSesion ?? "null",
                destino ?? "null"));

            return Redirect(destino);
        }

        private ActionResult LogLoginResult(ActionResult result, Stopwatch stopwatch, string mensaje)
        {
            _logger.LogInfo(string.Format(
                "[PERF][LOGIN] {0}. Total={1} ms",
                mensaje,
                stopwatch.ElapsedMilliseconds));
            return result;
        }

        private static bool EsErrorConexionBaseDatos(Exception ex)
        {
            while (ex != null)
            {
                var typeName = ex.GetType().FullName ?? string.Empty;
                if (ex is TimeoutException ||
                    typeName.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                ex = ex.InnerException;
            }

            return false;
        }
    }
}
