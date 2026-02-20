using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaModelo.Seguridad;
using CapaNegocio;
using CapaPresentacion.Filters;
using CapaPresentacion.Models.AdminUsuarios;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador,Direccion,JefaturaTecnica")]
    public class AdminUsuariosController : Controller
    {
        [HttpGet]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Index(string filtro, bool? activo)
        {
            var vm = new AdminUsuariosIndexViewModel
            {
                Filtro = filtro,
                Activo = activo,
                Usuarios = AdminUsuariosBL.BuscarUsuarios(filtro, activo)
            };

            return View(vm);
        }

        [HttpGet]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Create()
        {
            var vm = new AdminUsuarioFormViewModel
            {
                Activo = true,
                GenerarPassword = true
            };

            CargarRolesParaFormulario(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Create(AdminUsuarioFormViewModel model)
        {
            ValidarFormularioUsuario(model, isEdit: false);

            if (!ModelState.IsValid)
            {
                CargarRolesParaFormulario(model);
                return View(model);
            }

            int nuevoId;
            string passwordTemporal;
            string mensaje;

            var ok = AdminUsuariosBL.CrearUsuario(
                MapearDto(model),
                model.RolesSeleccionados,
                model.PasswordInicial,
                model.GenerarPassword,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out nuevoId,
                out passwordTemporal,
                out mensaje);

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, mensaje);
                CargarRolesParaFormulario(model);
                return View(model);
            }

            TempData["Success"] = mensaje;
            if (!string.IsNullOrWhiteSpace(passwordTemporal))
            {
                TempData["PasswordTemporal"] = string.Format(
                    "Contrasena temporal para {0}: {1}",
                    model.CodigoUsuario,
                    passwordTemporal);
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Edit(int id)
        {
            var usuario = AdminUsuariosBL.ObtenerUsuarioPorId(id);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction("Index");
            }

            var vm = new AdminUsuarioFormViewModel
            {
                IdUsuario = usuario.IdUsuario,
                CodigoUsuario = usuario.CodigoUsuario,
                NombreUsuario = usuario.NombreUsuario,
                ApellidoUsuario = usuario.ApellidoUsuario,
                Correo = usuario.Correo,
                Activo = usuario.Activo,
                RolesSeleccionados = usuario.RolesAsignados != null
                    ? usuario.RolesAsignados.ToList()
                    : new List<int>()
            };

            CargarRolesParaFormulario(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Edit(AdminUsuarioFormViewModel model)
        {
            ValidarFormularioUsuario(model, isEdit: true);

            if (!ModelState.IsValid)
            {
                CargarRolesParaFormulario(model);
                return View(model);
            }

            string mensaje;
            var ok = AdminUsuariosBL.ActualizarUsuario(
                MapearDto(model),
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out mensaje);

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, mensaje);
                CargarRolesParaFormulario(model);
                return View(model);
            }

            // Guardar roles en el mismo flujo de edicion.
            if (model.RolesSeleccionados != null && model.RolesSeleccionados.Any())
            {
                string mensajeRoles;
                AdminUsuariosBL.ReemplazarRolesUsuario(
                    model.IdUsuario,
                    model.RolesSeleccionados,
                    ObtenerActorId(),
                    ObtenerActorCodigoUsuario(),
                    Request != null ? Request.UserHostAddress : null,
                    out mensajeRoles);
            }

            TempData["Success"] = mensaje;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult CambiarEstado(int id, bool activo)
        {
            string mensaje;
            var ok = AdminUsuariosBL.CambiarEstadoUsuario(
                id,
                activo,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out mensaje);

            TempData[ok ? "Success" : "Error"] = mensaje;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_RESET_PASSWORD")]
        public ActionResult ResetPassword(int id)
        {
            string passwordTemporal;
            string mensaje;

            var ok = AdminUsuariosBL.ResetPassword(
                id,
                true,
                null,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out passwordTemporal,
                out mensaje);

            if (ok)
            {
                TempData["Success"] = mensaje;
                if (!string.IsNullOrWhiteSpace(passwordTemporal))
                {
                    TempData["PasswordTemporal"] = string.Format(
                        "Contrasena temporal generada: {0}",
                        passwordTemporal);
                }
            }
            else
            {
                TempData["Error"] = mensaje;
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Roles(int id)
        {
            var usuario = AdminUsuariosBL.ObtenerUsuarioPorId(id);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction("Index");
            }

            var vm = new AdminUsuarioRolesViewModel
            {
                IdUsuario = usuario.IdUsuario,
                CodigoUsuario = usuario.CodigoUsuario,
                NombreCompleto = string.Format(
                    "{0} {1}",
                    usuario.NombreUsuario ?? string.Empty,
                    usuario.ApellidoUsuario ?? string.Empty).Trim(),
                Activo = usuario.Activo,
                RolesSeleccionados = usuario.RolesAsignados != null
                    ? usuario.RolesAsignados.ToList()
                    : new List<int>()
            };

            vm.RolesDisponibles = ObtenerRolesSelectList(vm.RolesSeleccionados);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Roles(AdminUsuarioRolesViewModel model)
        {
            if (model == null || model.IdUsuario <= 0)
            {
                TempData["Error"] = "Usuario invalido.";
                return RedirectToAction("Index");
            }

            if (model.RolesSeleccionados == null || !model.RolesSeleccionados.Any())
            {
                ModelState.AddModelError(string.Empty, "Debe seleccionar al menos un rol.");
            }

            if (!ModelState.IsValid)
            {
                model.RolesDisponibles = ObtenerRolesSelectList(model.RolesSeleccionados);
                return View(model);
            }

            string mensaje;
            var ok = AdminUsuariosBL.ReemplazarRolesUsuario(
                model.IdUsuario,
                model.RolesSeleccionados,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out mensaje);

            TempData[ok ? "Success" : "Error"] = mensaje;
            return RedirectToAction("Index");
        }

        [HttpGet]
        [RequirePermission("ADM_ROLES_PERMISOS")]
        public ActionResult PermisosRol(int? codigoRol)
        {
            var rolesActivos = AdminUsuariosBL.ObtenerRolesActivos();
            var rolSeleccionado = codigoRol.GetValueOrDefault(0);

            var vm = new AdminRolPermisosViewModel
            {
                CodigoRolSeleccionado = rolSeleccionado,
                RolesDisponibles = rolesActivos.Select(r => new SelectListItem
                {
                    Value = r.CodigoRol.ToString(),
                    Text = r.Descripcion,
                    Selected = r.CodigoRol == rolSeleccionado
                }).ToList(),
                InfraestructuraPermisosDisponible = SeguridadBL.InfraestructuraPermisosDisponible()
            };

            if (rolSeleccionado > 0 && vm.InfraestructuraPermisosDisponible)
            {
                vm.PermisosDisponibles = AdminUsuariosBL.ObtenerPermisos(true);
                vm.PermisosSeleccionados = AdminUsuariosBL.ObtenerPermisosPorRol(rolSeleccionado);
                vm.NombreRolSeleccionado = rolesActivos
                    .Where(r => r.CodigoRol == rolSeleccionado)
                    .Select(r => r.Descripcion)
                    .FirstOrDefault();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_ROLES_PERMISOS")]
        public ActionResult PermisosRol(AdminRolPermisosViewModel model)
        {
            if (model == null || model.CodigoRolSeleccionado <= 0)
            {
                TempData["Error"] = "Debe seleccionar un rol.";
                return RedirectToAction("PermisosRol");
            }

            string mensaje;
            var ok = AdminUsuariosBL.ReemplazarPermisosRol(
                model.CodigoRolSeleccionado,
                model.PermisosSeleccionados,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out mensaje);

            TempData[ok ? "Success" : "Error"] = mensaje;
            return RedirectToAction("PermisosRol", new { codigoRol = model.CodigoRolSeleccionado });
        }

        private static SeguridadUsuarioDTO MapearDto(AdminUsuarioFormViewModel model)
        {
            return new SeguridadUsuarioDTO
            {
                IdUsuario = model.IdUsuario,
                CodigoUsuario = (model.CodigoUsuario ?? string.Empty).Trim(),
                NombreUsuario = (model.NombreUsuario ?? string.Empty).Trim(),
                ApellidoUsuario = (model.ApellidoUsuario ?? string.Empty).Trim(),
                Correo = (model.Correo ?? string.Empty).Trim(),
                Activo = model.Activo,
                RolesAsignados = model.RolesSeleccionados != null
                    ? model.RolesSeleccionados.ToList()
                    : new List<int>()
            };
        }

        private void CargarRolesParaFormulario(AdminUsuarioFormViewModel model)
        {
            model.RolesDisponibles = ObtenerRolesSelectList(model.RolesSeleccionados);
        }

        private IEnumerable<SelectListItem> ObtenerRolesSelectList(IEnumerable<int> seleccionados)
        {
            var seleccion = new HashSet<int>((seleccionados ?? Enumerable.Empty<int>()));
            return AdminUsuariosBL.ObtenerRolesActivos()
                .Select(r => new SelectListItem
                {
                    Value = r.CodigoRol.ToString(),
                    Text = r.Descripcion,
                    Selected = seleccion.Contains(r.CodigoRol)
                })
                .ToList();
        }

        private void ValidarFormularioUsuario(AdminUsuarioFormViewModel model, bool isEdit)
        {
            if (model == null)
            {
                ModelState.AddModelError(string.Empty, "Solicitud invalida.");
                return;
            }

            if (!isEdit && !model.GenerarPassword && string.IsNullOrWhiteSpace(model.PasswordInicial))
            {
                ModelState.AddModelError("PasswordInicial", "Debe ingresar una contrasena inicial o marcar generacion temporal.");
            }

            if (model.RolesSeleccionados == null || !model.RolesSeleccionados.Any())
            {
                ModelState.AddModelError("RolesSeleccionados", "Debe seleccionar al menos un rol.");
            }
        }

        private int? ObtenerActorId()
        {
            if (Session == null)
            {
                return null;
            }

            var valor = Session["IdUsuario"] ?? Session["UserId"];
            if (valor == null)
            {
                return null;
            }

            int id;
            return int.TryParse(Convert.ToString(valor), out id) ? (int?)id : null;
        }

        private string ObtenerActorCodigoUsuario()
        {
            if (Session != null)
            {
                var codigo = Convert.ToString(Session["CodigoUsuario"]);
                if (!string.IsNullOrWhiteSpace(codigo))
                {
                    return codigo.Trim();
                }
            }

            var identityName = User != null && User.Identity != null ? User.Identity.Name : null;
            return string.IsNullOrWhiteSpace(identityName) ? "SYSTEM" : identityName.Trim();
        }
    }
}
