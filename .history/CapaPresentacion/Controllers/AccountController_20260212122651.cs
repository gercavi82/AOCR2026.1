using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaPresentacion.Models;
using CapaDatos.DAOs;

namespace CapaPresentacion.Controllers
{
    public class AccountController : Controller
    {
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
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
                        SameSite = SameSiteMode.Lax,
                        Path = FormsAuthentication.FormsCookiePath
                    };
                    Response.Cookies.Add(c);
                }
            }

            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            Response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-5));

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

            bool ok = UsuarioBL.Autenticar(
                model.Usuario,
                model.Contrasena,
                out usuario,
                out roles,
                out mensaje,
                actualizarUltimaConexion: false
            );

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
                if (roles != null)
                {
                    esAdmin = roles.Any(r => r.Equals("Administrador", StringComparison.OrdinalIgnoreCase));
                }

                if (!esAdmin)
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
                Secure = Request.IsSecureConnection,
                SameSite = SameSiteMode.Lax
            };

            if (model.Recordarme)
                cookie.Expires = DateTime.Now.AddDays(7);

            Response.Cookies.Add(cookie);

            // ============================
            // SESIÓN UNIFICADA (NO TOCAR)
            // ============================
            Session["UserId"] = usuario.Id;
            Session["IdUsuario"] = usuario.Id;

            Session["NombreUsuario"] = !string.IsNullOrWhiteSpace(usuario.NombreCompleto)
                ? usuario.NombreCompleto
                : (usuario.NombreUsuario ?? "Usuario");

            Session["Correo"] = usuario.Email;

            Session["Roles"] = roles;
            Session["Rol"] = roles.Count > 0 ? roles[0] : null;
            Session["LastActivity"] = DateTime.Now;

            // Forzar cambio de contraseña en primer ingreso
            if (!usuario.FechaUltimaConexion.HasValue)
            {
                return RedirectToAction("CambiarContrasena", "Account");
            }

            // Actualizar última conexión (solo si no es primer ingreso)
            UsuarioDAO.ActualizarUltimaConexion(usuario.Id);

            // ============================
            // VERIFICACIÓN DE ORDEN
            // ============================
            var ordenDAO = new OrdenRecaudacionDAO();

            bool tieneOrdenGeneradaOPagada = ordenDAO.ExisteORGeneradaOPagada(usuario.Id);
            bool tieneOrdenBorrador = ordenDAO.ExisteORMinima(usuario.Id);

            Session["TieneOrdenGenerada"] = tieneOrdenGeneradaOPagada;
            Session["TieneOrdenBorrador"] = tieneOrdenBorrador;

            if (!tieneOrdenGeneradaOPagada)
                return RedirectToAction("Obligatoria", "OrdenRecaudacion");

            return RedirectToLocal(returnUrl);
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
                    Secure = Request.IsSecureConnection,
                    SameSite = SameSiteMode.Lax
                };
                Response.Cookies.Add(c);
            }

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

            int idUsuario = 0;
            var v = Session["UserId"] ?? Session["IdUsuario"];
            if (v != null) int.TryParse(v.ToString(), out idUsuario);

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
            return RedirectToAction("Index", "Dashboard");
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
                var v = Session["UserId"] ?? Session["IdUsuario"];
                if (v != null) int.TryParse(v.ToString(), out idUsuario);

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
    }
}
