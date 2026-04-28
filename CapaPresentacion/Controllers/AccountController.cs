using System;
using System.Collections.Generic;
using System.Configuration;
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
            "Recepcion"
        };

        [AllowAnonymous]
        public ActionResult Login(string returnUrl, string af = null)
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
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
                // Limpia token de sesión previa para evitar desalineación de antiforgery.
                ExpirarCookieAntiForgery();

                // Importante: el request actual sigue teniendo el principal autenticado.
                // Si renderizamos el Login así, @Html.AntiForgeryToken() se genera atado al usuario anterior
                // y el siguiente POST falla con HttpAntiForgeryException.
                var anonymous = new GenericPrincipal(new GenericIdentity(string.Empty), null);
                HttpContext.User = anonymous;
                if (System.Web.HttpContext.Current != null)
                {
                    System.Web.HttpContext.Current.User = anonymous;
                }
                Thread.CurrentPrincipal = anonymous;
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

            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
                return View(model);

            string mensaje;
            Usuario usuario;
            List<string> roles;

            bool ok;
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
            }
            catch (Exception ex) when (EsErrorConexionBaseDatos(ex))
            {
                System.Diagnostics.Debug.WriteLine("Account/Login: error de conexión a base de datos: " + ex.Message);
                ModelState.AddModelError("", "No se pudo conectar con la base de datos. Intente nuevamente en unos minutos.");
                return View(model);
            }

            if (!ok || usuario == null)
            {
                ModelState.AddModelError("", string.IsNullOrWhiteSpace(mensaje) ? "Credenciales inválidas." : mensaje);
                return View(model);
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
                    return View(model);
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

            roles = roles ?? new List<string>();
            var rolesString = roles.Count > 0 ? string.Join(",", roles.Distinct(StringComparer.OrdinalIgnoreCase)) : string.Empty;

            // ============================
            // COOKIE DE AUTENTICACIÓN (PRODUCCIÓN)
            // ============================
            var ticket = new FormsAuthenticationTicket(
                1,
                usuario.NombreUsuario ?? model.Usuario ?? "usuario",
                DateTime.Now,
                DateTime.Now.AddMinutes(60),
                model.Recordarme,
                rolesString,
                FormsAuthentication.FormsCookiePath
            );

            string encrypted = FormsAuthentication.Encrypt(ticket);

            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encrypted)
            {
                HttpOnly = true,
                Secure = Request.IsSecureConnection
            };
            CookieHelper.SetSameSiteLax(cookie);

            if (model.Recordarme)
                cookie.Expires = DateTime.Now.AddDays(7);

            Response.Cookies.Add(cookie);
            // Forzar emisión de token antiforgery nuevo en la siguiente vista autenticada.
            ExpirarCookieAntiForgery();

            // ============================
            // SESIÓN UNIFICADA (NO TOCAR)
            // ============================
            Session["UserId"] = usuario.Id;
            Session["IdUsuario"] = usuario.Id;
            Session["CodigoUsuario"] = !string.IsNullOrWhiteSpace(usuario.CodigoUsuario)
                ? usuario.CodigoUsuario
                : usuario.NombreUsuario;

            Session["NombreUsuario"] = !string.IsNullOrWhiteSpace(usuario.NombreCompleto)
                ? usuario.NombreCompleto
                : (usuario.NombreUsuario ?? "Usuario");

            Session["Correo"] = usuario.Email;
            CompaniaActivaSessionHelper.Limpiar(Session);

            Session["Roles"] = roles;
            Session["Rol"] = roles.Count > 0 ? roles[0] : null;
            Session["LastActivity"] = DateTime.Now;

            // Forzar cambio cuando hay marca explícita o cuando la última conexión fue limpiada (reset clave).
            if (usuario.MustChangePassword || !usuario.FechaUltimaConexion.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(returnUrl))
                {
                    Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] = returnUrl;
                }
                return RedirectToAction("CambiarContrasena", "Account");
            }

            var esUsuarioRt = EsUsuarioRt(usuario) && !EsUsuarioAdministrador(usuario, roles);
            var companiasAsignadas = new List<UsuarioCompaniaRT>();
            if (esUsuarioRt)
            {
                companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
                if (companiasAsignadas.Count == 0)
                {
                    ModelState.AddModelError("", "Su usuario RT no tiene compañías asignadas. Solicite al administrador la asociación correspondiente.");
                    return View(model);
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
                    return RedirectToAction("SeleccionarCompania");
                }
            }
            else if (!string.IsNullOrWhiteSpace(usuario.EmpresaCodigo))
            {
                var nombreEmpresa = ResolverNombreCompaniaPorCodigo(usuario.EmpresaCodigo);
                CompaniaActivaSessionHelper.Establecer(Session, usuario.EmpresaCodigo, nombreEmpresa);
            }

            // Actualizar última conexión después de autenticación normal.
            UsuarioDAO.ActualizarUltimaConexion(usuario.Id);
            return RedireccionarDespuesLogin(usuario.Id, returnUrl);
        }

        [Authorize]
        public ActionResult SeleccionarCompania(string companiaSeleccionada = null)
        {
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                return RedirectToAction("Login", "Account");
            }

            var esUsuarioRt = EsUsuarioRt(usuario) && !EsAdministradorSesion(usuario);
            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);

            if (!esUsuarioRt || companiasAsignadas.Count <= 1)
            {
                if (companiasAsignadas.Count == 1)
                {
                    var unica = companiasAsignadas[0];
                    var nombre = !string.IsNullOrWhiteSpace(unica.CompaniaNombre)
                        ? unica.CompaniaNombre
                        : ResolverNombreCompaniaPorCodigo(unica.CompaniaCodigo);
                    CompaniaActivaSessionHelper.Establecer(Session, unica.CompaniaCodigo, nombre);
                }

                var returnUrlUnica = Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string;
                Session.Remove(CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl);
                return RedireccionarDespuesLogin(usuarioId, returnUrlUnica);
            }

            var codigoActivo = CompaniaActivaSessionHelper.ObtenerCodigo(Session);
            var vm = ConstruirSeleccionCompaniaViewModel(
                companiasAsignadas,
                Session[CompaniaActivaSessionHelper.SessionCompaniaPendienteReturnUrl] as string,
                !string.IsNullOrWhiteSpace(companiaSeleccionada) ? companiaSeleccionada : codigoActivo);

            return View(vm);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult SeleccionarCompania(SeleccionCompaniaViewModel model)
        {
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                return RedirectToAction("Login", "Account");
            }

            if (EsAdministradorSesion(usuario))
            {
                return RedireccionarDespuesLogin(usuarioId, model != null ? model.ReturnUrl : null);
            }

            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
            var seleccion = companiasAsignadas.FirstOrDefault(c =>
                string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), (model?.CompaniaSeleccionada ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

            if (seleccion == null)
            {
                var vm = ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    model != null ? model.ReturnUrl : null,
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

            var returnUrl = model != null ? model.ReturnUrl : null;
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
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
            }

            var usuario = UsuarioDAO.ObtenerPorId(usuarioId);
            if (usuario == null)
            {
                TempData["LoginError"] = "No se pudo cargar su perfil de usuario.";
                return RedirectToAction("Login", "Account");
            }

            if (EsAdministradorSesion(usuario))
            {
                return RedireccionarDespuesLogin(usuarioId, model != null ? model.ReturnUrl : null);
            }

            var companiasAsignadas = ObtenerCompaniasAsignadasConFallback(usuario);
            var codigo = (model != null ? model.NuevaCompaniaCodigo : null) ?? string.Empty;
            codigo = codigo.Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(codigo))
            {
                ModelState.AddModelError("", "Seleccione una compañía adicional para agregar.");
                return View("SeleccionarCompania", ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    model != null ? model.ReturnUrl : null,
                    model != null ? model.CompaniaSeleccionada : null,
                    codigo,
                    true));
            }

            if (companiasAsignadas.Any(c => string.Equals((c.CompaniaCodigo ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("", "La compañía seleccionada ya está asignada a su usuario.");
                return View("SeleccionarCompania", ConstruirSeleccionCompaniaViewModel(
                    companiasAsignadas,
                    model != null ? model.ReturnUrl : null,
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
                    model != null ? model.ReturnUrl : null,
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
                    model != null ? model.ReturnUrl : null,
                    model != null ? model.CompaniaSeleccionada : null,
                    codigo,
                    true));
            }

            TempData["SeleccionCompaniaSuccess"] = "La compañía se agregó correctamente. Ahora puede seleccionarla para continuar.";
            return RedirectToAction("SeleccionarCompania", new { companiaSeleccionada = codigo });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarCompaniaActiva(string companiaCodigo, string returnUrl)
        {
            var usuarioId = ObtenerUsuarioSesionId();
            if (usuarioId <= 0)
            {
                return RedirectToAction("Login", "Account");
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
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        [Authorize]
        public ActionResult CambiarRol(string rolSeleccionado)
        {
            var roles = Session["Roles"] as List<string> ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(rolSeleccionado) &&
                roles.Contains(rolSeleccionado, StringComparer.OrdinalIgnoreCase))
            {
                // set rol exacto como está en la lista
                var match = roles.First(r => r.Equals(rolSeleccionado, StringComparison.OrdinalIgnoreCase));
                Session["Rol"] = match;
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
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
                    Secure = Request.IsSecureConnection
                };
                CookieHelper.SetSameSiteLax(c);
                Response.Cookies.Add(c);
            }
            ExpirarCookieAntiForgery();

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
                bool tieneOrden = ordenDAO.ExisteORGeneradaOPagada(idUsuario);
                bool tieneBorrador = ordenDAO.ExisteORMinima(idUsuario);

                return Json(new
                {
                    tieneOrdenGenerada = tieneOrden,
                    tieneOrdenBorrador = tieneBorrador,
                    redireccionar = !tieneOrden
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
            Session["LastActivity"] = DateTime.Now;
            return Json(new { ok = true });
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
                var rolesObj = Session["Roles"];
                if (rolesObj is List<string>)
                {
                    roles.AddRange((List<string>)rolesObj);
                }
                else if (rolesObj is string[])
                {
                    roles.AddRange((string[])rolesObj);
                }
                else if (rolesObj is IEnumerable<string>)
                {
                    roles.AddRange((IEnumerable<string>)rolesObj);
                }

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

            return roles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            if (v == null)
            {
                return 0;
            }

            int id;
            return int.TryParse(v.ToString(), out id) ? id : 0;
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

        private ActionResult RedireccionarDespuesLogin(int usuarioId, string returnUrl)
        {
            // ============================
            // VERIFICACIÓN DE ORDEN
            // ============================
            var ordenDAO = new OrdenRecaudacionDAO();

            bool tieneOrdenGeneradaOPagada = ordenDAO.ExisteORGeneradaOPagada(usuarioId);
            bool tieneOrdenBorrador = ordenDAO.ExisteORMinima(usuarioId);

            Session["TieneOrdenGenerada"] = tieneOrdenGeneradaOPagada;
            Session["TieneOrdenBorrador"] = tieneOrdenBorrador;

            if (!tieneOrdenGeneradaOPagada)
            {
                return RedirectToAction("Obligatoria", "OrdenRecaudacion");
            }

            return RedirectToLocal(returnUrl);
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
