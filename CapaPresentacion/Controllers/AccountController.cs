using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using CapaModelo;
using CapaNegocio;
using CapaPresentacion.Models;
using CapaDatos.DAOs; // Añadir para usar OrdenRecaudacionDAO

namespace CapaPresentacion.Controllers
{
    public class AccountController : Controller
    {
        // ============================
        // GET: Login
        // ============================
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        // ============================
        // POST: Login
        // ============================
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
                out mensaje
            );

            if (!ok || usuario == null)
            {
                ModelState.AddModelError("", string.IsNullOrWhiteSpace(mensaje) ? "Credenciales inválidas." : mensaje);
                return View(model);
            }

            // 🔐 ADMIN SUPREMO
            if (!string.IsNullOrWhiteSpace(usuario.NombreUsuario) &&
                usuario.NombreUsuario.Equals("USU_ADMIN", StringComparison.InvariantCultureIgnoreCase))
            {
                roles = new List<string>
                {
                    "Administrador",
                    "Tecnico",
                    "Solicitante",
                    "Financiero",
                    "Inspector",
                    "JefaturaTecnica",
                    "Direccion",
                    "CoordinacionLegal"
                };
            }

            roles = roles ?? new List<string>();
            string rolesString = roles.Count > 0 ? string.Join(",", roles.Distinct()) : string.Empty;

            // ============================
            // COOKIE DE AUTENTICACIÓN
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
            // ✅ SESIÓN (CLAVE)
            // ============================
            Session["IdUsuario"] = usuario.Id;
            Session["NombreUsuario"] = !string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? usuario.NombreCompleto : (usuario.NombreUsuario ?? "Usuario");
            Session["Correo"] = usuario.Email;

            Session["Roles"] = roles;
            Session["Rol"] = roles.Count > 0 ? roles[0] : null;

            // ============================
            // 🔄 VERIFICACIÓN DE ORDEN DE RECAUDACIÓN
            // ============================
            var ordenDAO = new OrdenRecaudacionDAO();

            // 1. Verificar si tiene orden generada o pagada
            bool tieneOrdenGeneradaOPagada = ordenDAO.ExisteORGeneradaOPagada(usuario.Id);

            // 2. Verificar si tiene orden en borrador
            bool tieneOrdenBorrador = ordenDAO.ExisteORMinima(usuario.Id);

            // Guardar estado en sesión para uso posterior
            Session["TieneOrdenGenerada"] = tieneOrdenGeneradaOPagada;
            Session["TieneOrdenBorrador"] = tieneOrdenBorrador;

            // ============================
            // REDIRECCIÓN SEGURA CON VERIFICACIÓN DE ORDEN
            // ============================
            if (!tieneOrdenGeneradaOPagada)
            {
                // No tiene orden válida, redirigir a verificación de orden
                return RedirectToAction("Obligatoria", "OrdenRecaudacion");
            }

            // Si tiene orden válida, proceder con redirección normal
            return RedirectToLocal(returnUrl);
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        // ============================
        // CAMBIAR ROL
        // ============================
        [Authorize]
        public ActionResult CambiarRol(string rolSeleccionado)
        {
            var roles = Session["Roles"] as List<string> ?? new List<string>();

            if (!string.IsNullOrWhiteSpace(rolSeleccionado) && roles.Contains(rolSeleccionado))
                Session["Rol"] = rolSeleccionado;

            return RedirectToAction("Index", "Home");
        }

        // ============================
        // LOGOUT
        // ============================
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

        // ============================
        // RECUPERAR CONTRASEÑA
        // ============================
        [HttpPost]
        [AllowAnonymous]
        public JsonResult EnviarRecuperar(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return Json(new { ok = false, mensaje = "Debe ingresar un correo electrónico válido." });

            string mensaje;
            bool enviado = UsuarioBL.RestablecerContrasenaPorEmail(email.Trim(), out mensaje);

            return Json(new { ok = enviado, mensaje = mensaje });
        }

        // ============================
        // MODAL REGISTRO
        // ============================
        public ActionResult _ModalRegistroUsuario()
        {
            var model = new UsuarioCreateViewModel
            {
                RolesDisponibles = RolBL.ObtenerActivos()
            };
            return PartialView("_ModalRegistroUsuario", model);
        }

        // ============================
        // VERIFICAR ESTADO ORDEN (para AJAX)
        // ============================
        [Authorize]
        public JsonResult VerificarEstadoOrden()
        {
            try
            {
                int idUsuario = Session["IdUsuario"] != null ? Convert.ToInt32(Session["IdUsuario"]) : 0;

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
    }
}
