using System;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaNegocio.Services;
using CapaPresentacion.Filters;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Controllers
{
    [AocrAuthorize]
    public class CorreoInstitucionalController : Controller
    {
        private readonly CorreoInstitucionalDAO _dao = new CorreoInstitucionalDAO();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            if (filterContext == null || filterContext.Result != null)
            {
                return;
            }

            if (!UsuarioTienePermisoAdministracion())
            {
                filterContext.Result = ConstruirResultadoAccesoDenegado(filterContext);
            }
        }

        [HttpGet]
        public ActionResult Index()
        {
            var lista = _dao.ListarCorreosInstitucionales();
            return View(lista);
        }

        [HttpGet]
        public ActionResult Crear()
        {
            return View("Editar", new CorreoInstitucionalModel { Activo = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Crear(CorreoInstitucionalModel model)
        {
            model = model ?? new CorreoInstitucionalModel();
            Normalizar(model);
            ValidarModelo(model, true);

            if (!ModelState.IsValid)
            {
                return View("Editar", model);
            }

            model.CreatedBy = ObtenerUsuario();
            _dao.Crear(model);
            TempData["Success"] = "Correo institucional creado correctamente.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Editar(int id)
        {
            var model = _dao.ObtenerPorId(id);
            if (model == null)
            {
                TempData["Error"] = "No se encontró el correo institucional solicitado.";
                return RedirectToAction("Index");
            }

            if (CorreoInstitucionalService.EsAreaReservadaNoAdministrable(model.CodigoArea))
            {
                TempData["Error"] = "El correo del inspector se administra desde el usuario personalizado y no desde correos institucionales.";
                return RedirectToAction("Index");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(CorreoInstitucionalModel model)
        {
            model = model ?? new CorreoInstitucionalModel();
            Normalizar(model);
            ValidarModelo(model, false);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var actual = _dao.ObtenerPorId(model.CodigoCorreo);
            if (actual == null)
            {
                TempData["Error"] = "No se encontró el correo institucional solicitado.";
                return RedirectToAction("Index");
            }

            if (CorreoInstitucionalService.EsAreaReservadaNoAdministrable(actual.CodigoArea))
            {
                TempData["Error"] = "El correo del inspector se administra desde el usuario personalizado y no desde correos institucionales.";
                return RedirectToAction("Index");
            }

            model.CodigoArea = actual.CodigoArea;
            model.CreatedAt = actual.CreatedAt;
            model.CreatedBy = actual.CreatedBy;
            model.UpdatedBy = ObtenerUsuario();
            _dao.Actualizar(model);
            TempData["Success"] = "Correo institucional actualizado correctamente.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstado(int id, bool activo)
        {
            var actual = _dao.ObtenerPorId(id);
            if (actual != null && CorreoInstitucionalService.EsAreaReservadaNoAdministrable(actual.CodigoArea))
            {
                TempData["Error"] = "El correo del inspector se administra desde el usuario personalizado y no desde correos institucionales.";
                return RedirectToAction("Index");
            }

            if (!_dao.CambiarEstado(id, activo, ObtenerUsuario()))
            {
                TempData["Error"] = "No se pudo actualizar el estado del correo institucional.";
            }
            else
            {
                TempData["Success"] = activo ? "Correo institucional activado." : "Correo institucional inactivado.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public ActionResult Historial(int id)
        {
            var model = _dao.ObtenerPorId(id);
            if (model == null)
            {
                TempData["Error"] = "No se encontró el correo institucional solicitado.";
                return RedirectToAction("Index");
            }

            if (CorreoInstitucionalService.EsAreaReservadaNoAdministrable(model.CodigoArea))
            {
                TempData["Error"] = "El correo del inspector se administra desde el usuario personalizado y no desde correos institucionales.";
                return RedirectToAction("Index");
            }

            ViewBag.CorreoInstitucional = model;
            return View(_dao.ListarHistorial(id));
        }

        private void ValidarModelo(CorreoInstitucionalModel model, bool esCreacion)
        {
            if (string.IsNullOrWhiteSpace(model.CodigoArea))
            {
                ModelState.AddModelError("CodigoArea", "El código de área es obligatorio.");
            }
            else if (CorreoInstitucionalService.EsAreaReservadaNoAdministrable(model.CodigoArea))
            {
                ModelState.AddModelError("CodigoArea", "El correo del inspector se administra desde el usuario personalizado y no desde correos institucionales.");
            }
            else if (esCreacion && _dao.ExisteCodigoArea(model.CodigoArea))
            {
                ModelState.AddModelError("CodigoArea", "Ya existe un correo institucional con ese código de área.");
            }

            if (string.IsNullOrWhiteSpace(model.NombreArea))
            {
                ModelState.AddModelError("NombreArea", "El nombre del área es obligatorio.");
            }

            if (!CorreoInstitucionalService.EsCorreoValido(model.CorreoPrincipal))
            {
                ModelState.AddModelError("CorreoPrincipal", "Ingrese un correo principal válido.");
            }

            string mensaje;
            if (!CorreoInstitucionalService.SonCorreosMultiplesValidos(model.CorreosCc, out mensaje))
            {
                ModelState.AddModelError("CorreosCc", mensaje);
            }

            if (!CorreoInstitucionalService.SonCorreosMultiplesValidos(model.CorreosBcc, out mensaje))
            {
                ModelState.AddModelError("CorreosBcc", mensaje);
            }
        }

        private static void Normalizar(CorreoInstitucionalModel model)
        {
            model.CodigoArea = (model.CodigoArea ?? string.Empty).Trim().ToUpperInvariant();
            model.NombreArea = (model.NombreArea ?? string.Empty).Trim();
            model.CorreoPrincipal = (model.CorreoPrincipal ?? string.Empty).Trim();
            model.CorreosCc = NormalizarListaCorreos(model.CorreosCc);
            model.CorreosBcc = NormalizarListaCorreos(model.CorreosBcc);
            model.Descripcion = (model.Descripcion ?? string.Empty).Trim();
        }

        private static string NormalizarListaCorreos(string correos)
        {
            return string.Join("; ", CorreoInstitucionalService.SepararCorreos(correos));
        }

        private string ObtenerUsuario()
        {
            return User != null && User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "SYSTEM";
        }

        private bool UsuarioTienePermisoAdministracion()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return false;
            }

            if (User.IsInRole("Administrador") || User.IsInRole("SuperAdministrador") || User.IsInRole("SuperAdmin"))
            {
                return true;
            }

            var rolSesion = Session != null ? Session["Rol"] as string : string.Empty;
            if (EsRolAdministracion(rolSesion))
            {
                return true;
            }

            var rolesSesion = RoleGroupingHelper.ExtractRoles(
                Session != null ? Session["RolesRaw"] ?? Session["Roles"] : null,
                rolSesion);

            return rolesSesion.Any(EsRolAdministracion);
        }

        private ActionResult ConstruirResultadoAccesoDenegado(ActionExecutingContext filterContext)
        {
            var httpContext = filterContext != null ? filterContext.HttpContext : null;
            var request = httpContext != null ? httpContext.Request : null;
            var response = httpContext != null ? httpContext.Response : null;

            if (response != null)
            {
                response.StatusCode = 403;
                response.TrySkipIisCustomErrors = true;
                response.SuppressFormsAuthenticationRedirect = true;
            }

            if (EsAjaxLikeRequest(request))
            {
                return new JsonResult
                {
                    Data = new
                    {
                        success = false,
                        code = 403,
                        message = "No tiene permisos para administrar correos institucionales."
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            var viewData = new ViewDataDictionary();
            viewData["ErrorCode"] = 403;
            viewData["ErrorMessage"] = "Acceso denegado";
            viewData["ErrorDescription"] = "No tiene permisos para administrar correos institucionales.";

            return new ViewResult
            {
                ViewName = "~/Views/Error/Error.cshtml",
                ViewData = viewData
            };
        }

        private static bool EsAjaxLikeRequest(System.Web.HttpRequestBase request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.IsAjaxRequest())
            {
                return true;
            }

            var requestedWith = request.Headers["X-Requested-With"];
            if (string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var accept = request.Headers["Accept"] ?? string.Empty;
            return accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EsRolAdministracion(string rol)
        {
            return RoleGroupingHelper.IsAdministrador(rol)
                || string.Equals((rol ?? string.Empty).Trim(), "SuperAdministrador", StringComparison.OrdinalIgnoreCase)
                || string.Equals((rol ?? string.Empty).Trim(), "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
