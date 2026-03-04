using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio;
using Dapper;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsuarioRolController : Controller
    {
        // ======================================================
        // INDEX - Listar usuarios para gestión de roles
        // ======================================================
        [HttpGet]
        public ActionResult Index()
        {
            try
            {
                using (var cn = ConexionDAO.CrearConexion())
                {
                    cn.Open();
                    var usuarios = cn.Query<Usuario>(@"
                        SELECT
                            idusuario       AS Id,
                            codigousuario   AS CodigoUsuario,
                            codigousuario   AS NombreUsuario,
                            nombreusuario   AS NombreCompleto,
                            COALESCE(apellidousuario, '') AS ApellidoUsuario,
                            correo          AS Email,
                            (estadoactividad = '1') AS Activo
                        FROM usuario
                        WHERE estadoactividad = '1'
                        ORDER BY nombreusuario ASC
                    ").ToList();

                    return View(usuarios);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al listar usuarios: " + ex.Message);
                return View(new List<Usuario>());
            }
        }

        // ======================================================
        // ASIGNAR ROLES GET - Formulario de asignación
        // ======================================================
        [HttpGet]
        public ActionResult AsignarRoles(int id)
        {
            try
            {
                var todosLosRoles = RolBL.ObtenerActivos();
                var rolesAsignados = UsuarioRolDAO.ObtenerPorUsuario(id);
                var codigosAsignados = rolesAsignados
                    .Select(ur => ur.CodigoRol.ToString())
                    .ToList();

                using (var cn = ConexionDAO.CrearConexion())
                {
                    cn.Open();
                    var usuario = cn.QueryFirstOrDefault<Usuario>(@"
                        SELECT
                            idusuario     AS Id,
                            codigousuario AS NombreUsuario,
                            nombreusuario AS NombreCompleto,
                            COALESCE(apellidousuario, '') AS ApellidoUsuario
                        FROM usuario
                        WHERE idusuario = @id
                        LIMIT 1
                    ", new { id });

                    ViewBag.UsuarioId = id;
                    ViewBag.NombreUsuario = usuario != null
                        ? $"{usuario.NombreCompleto} {usuario.ApellidoUsuario}".Trim()
                        : "Usuario #" + id;
                }

                ViewBag.TodosLosRoles = todosLosRoles;
                ViewBag.RolesAsignados = codigosAsignados;

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar datos: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ======================================================
        // ASIGNAR ROLES POST - Guardar cambios
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarRoles(int id, string[] rolesSeleccionados)
        {
            try
            {
                rolesSeleccionados = rolesSeleccionados ?? new string[0];

                var actuales = UsuarioRolDAO.ObtenerPorUsuario(id);
                var codigosActuales = actuales.Select(ur => ur.CodigoRol).ToList();
                var codigosNuevos = rolesSeleccionados
                    .Where(r => int.TryParse(r, out _))
                    .Select(r => int.Parse(r))
                    .ToList();

                // Eliminar roles desmarcados
                foreach (var codActual in codigosActuales)
                {
                    if (!codigosNuevos.Contains(codActual))
                    {
                        UsuarioRolDAO.Eliminar(id, codActual);
                    }
                }

                // Agregar roles nuevos
                foreach (var codNuevo in codigosNuevos)
                {
                    if (!codigosActuales.Contains(codNuevo))
                    {
                        UsuarioRolDAO.Asignar(id, codNuevo);
                    }
                }

                TempData["Success"] = "Roles actualizados correctamente.";
                return RedirectToAction("AsignarRoles", new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al guardar roles: " + ex.Message;
                return RedirectToAction("AsignarRoles", new { id });
            }
        }

        // ======================================================
        // REMOVER ROL POST - Quitar un rol vía AJAX
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult RemoverRol(int usuarioId, int rolId)
        {
            try
            {
                var resultado = UsuarioRolDAO.Eliminar(usuarioId, rolId);
                if (resultado)
                {
                    return Json(new { success = true, mensaje = "Rol removido correctamente." });
                }
                return Json(new { success = false, mensaje = "No se encontró la asignación." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = "Error: " + ex.Message });
            }
        }

        // ======================================================
        // OBTENER ROLES JSON - Endpoint para selectores
        // ======================================================
        [AllowAnonymous]
        [HttpGet]
        public JsonResult ObtenerRoles()
        {
            try
            {
                using (var cn = ConexionDAO.CrearConexion())
                {
                    cn.Open();

                    var rolesRaw = cn.Query(@"
                        SELECT codigorol, descripcion
                        FROM rol
                        WHERE activo IS TRUE
                          AND descripcion ILIKE '%representante%'
                        ORDER BY descripcion ASC
                    ").ToList();

                    var rolesFormateados = rolesRaw.Select(r => new
                    {
                        Value = r.codigorol.ToString(),
                        Text = r.descripcion
                    }).ToList();

                    return Json(rolesFormateados, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { error = "Fallo Postgres: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
