using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaModelo;
using CapaModelo.Common;
using CapaModelo.Seguridad;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaPresentacion.Filters;
using CapaPresentacion.Helpers;
using CapaPresentacion.Models.AdminUsuarios;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Administrador,Direccion,JefaturaTecnica")]
    public class AdminUsuariosController : Controller
    {
        private readonly ILoggingService _logger = LoggingServiceFactory.Create();

        private List<InspectorAs400Record> BuscarInspectoresInternosPreferMirror(string texto)
        {
            var termino = (texto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(termino))
            {
                return new List<InspectorAs400Record>();
            }

            try
            {
                var mirrorDao = new InspectorMirrorPGDAO();
                var resultadosMirror = mirrorDao.BuscarActivosPorCedulaONombre(termino);
                if (resultadosMirror != null && resultadosMirror.Count > 0)
                {
                    _logger.LogInfo("[AdminUsuariosController] BuscarInspector origen=mirror, resultados=" + resultadosMirror.Count);
                    return resultadosMirror;
                }
            }
            catch (Exception exMirror)
            {
                _logger.LogWarning("[AdminUsuariosController] BuscarInspector mirror error: " + exMirror.Message);
            }

            try
            {
                var inspectorDao = new InspectorAS400DAO(new SecureConfigurationService());
                var resultadosAs400 = inspectorDao.BuscarPorCedulaONombre(termino) ?? new List<InspectorAs400Record>();
                _logger.LogInfo("[AdminUsuariosController] BuscarInspector origen=as400, resultados=" + resultadosAs400.Count);
                return resultadosAs400;
            }
            catch (Exception exAs400)
            {
                _logger.LogWarning("[AdminUsuariosController] BuscarInspector as400 error: " + exAs400.Message);
                return new List<InspectorAs400Record>();
            }
        }

        private InspectorAs400Record ObtenerInspectorActivoPreferMirror(string cedula, string tipo = null)
        {
            var codigo = (cedula ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return null;
            }

            try
            {
                var mirrorDao = new InspectorMirrorPGDAO();
                var inspectorMirror = mirrorDao.ObtenerActivoPorCedula(codigo, tipo);
                if (inspectorMirror != null)
                {
                    _logger.LogInfo("[AdminUsuariosController] ObtenerInspectorActivo origen=mirror, cedula=" + codigo);
                    return inspectorMirror;
                }
            }
            catch (Exception exMirror)
            {
                _logger.LogWarning("[AdminUsuariosController] ObtenerInspectorActivo mirror error: " + exMirror.Message);
            }

            try
            {
                var inspectorDao = new InspectorAS400DAO(new SecureConfigurationService());
                var inspectorAs400 = inspectorDao.ObtenerActivoPorCedula(codigo, tipo) ?? inspectorDao.ObtenerActivoPorCedula(codigo);
                if (inspectorAs400 != null)
                {
                    _logger.LogInfo("[AdminUsuariosController] ObtenerInspectorActivo origen=as400, cedula=" + codigo);
                }

                return inspectorAs400;
            }
            catch (Exception exAs400)
            {
                _logger.LogWarning("[AdminUsuariosController] ObtenerInspectorActivo as400 error: " + exAs400.Message);
                return null;
            }
        }

        [HttpGet]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Index(string filtro, bool? activo, string tipo)
        {
            var usuarios = AdminUsuariosBL.BuscarUsuarios(filtro, activo) ?? new List<SeguridadUsuarioDTO>();

            // Filtrar por tipo de usuario (Interno / Externo / Sin rol) en memoria
            if (!string.IsNullOrWhiteSpace(tipo))
            {
                var tipoNorm = tipo.Trim();
                usuarios = usuarios.Where(u => string.Equals(u.TipoUsuario, tipoNorm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var ahora = DateTime.Now;

            var vm = new AdminUsuariosIndexViewModel
            {
                Filtro = filtro,
                Activo = activo,
                TipoFiltro = tipo,
                Usuarios = usuarios,
                TotalUsuarios = usuarios.Count,
                UsuariosActivos = usuarios.Count(u => u != null && u.Activo),
                UsuariosInactivos = usuarios.Count(u => u != null && !u.Activo),
                UsuariosConRoles = usuarios.Count(u => u != null && !string.IsNullOrWhiteSpace(u.RolesTexto)),
                UsuariosSinRoles = usuarios.Count(u => u != null && string.IsNullOrWhiteSpace(u.RolesTexto)),
                UsuariosConAccesoReciente = usuarios.Count(u => u != null && u.UltimoLogin.HasValue && u.UltimoLogin.Value >= ahora.AddDays(-7)),
                UsuariosRecientes = usuarios
                    .Where(u => u != null && u.UltimoLogin.HasValue)
                    .OrderByDescending(u => u.UltimoLogin)
                    .Take(5)
                    .ToList()
            };

            try
            {
                vm.RolesActivos = AdminUsuariosBL.ObtenerRolesActivos()?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AdminUsuarios.Index: no se pudo obtener roles activos: " + ex.Message);
            }

            try
            {
                vm.PendientesDesignacionRt = UsuarioDAO.ObtenerUsuariosPendientesDesignacion()?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("AdminUsuarios.Index: no se pudo obtener designaciones RT pendientes: " + ex.Message);
            }

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
            CargarCompaniasParaFormulario(vm, null);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Create(AdminUsuarioFormViewModel model)
        {
            NormalizarNombreApellidoParaEdicion(model);
            ValidarFormularioUsuario(model, isEdit: false);

            if (!ModelState.IsValid)
            {
                CargarRolesParaFormulario(model);
                CargarCompaniasParaFormulario(model, model.IdUsuario > 0 ? (int?)model.IdUsuario : null);
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
                CargarCompaniasParaFormulario(model, model.IdUsuario > 0 ? (int?)model.IdUsuario : null);
                return View(model);
            }

            string mensajeCompaniasCreate;
            if (!GuardarCompaniasRtUsuario(nuevoId, model.CompaniasSeleccionadas, out mensajeCompaniasCreate))
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(mensajeCompaniasCreate)
                    ? "Usuario creado, pero no se pudieron guardar las compaÃ±Ã­as RT."
                    : mensajeCompaniasCreate;
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
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult CrearUsuarioInternoRT()
        {
            var vm = new AdminUsuarioInternoRTViewModel
            {
                Activo = true
            };
            _logger.LogInfo("[AdminUsuariosController] CrearUsuarioInternoRT GET. Usuario=" + ObtenerActorCodigoUsuario());
            CargarRolesUsuarioInterno(vm, null);
            return View(vm);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public JsonResult BuscarUsuarioInternoRT(string codigoUsuario)
        {
            var texto = (codigoUsuario ?? string.Empty).Trim();
            _logger.LogInfo("[AdminUsuariosController] BuscarUsuarioInternoRT inicio. Usuario=" + ObtenerActorCodigoUsuario()
            + ", Busqueda=" + texto);

            if (string.IsNullOrWhiteSpace(texto))
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "Debe ingresar cedula o nombre para buscar."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            var esNumerico = texto.All(char.IsDigit);
            _logger.LogInfo("[AdminUsuariosController] BuscarUsuarioInternoRT criterio=" + (esNumerico ? "cedula" : "nombre"));

            var resultados = BuscarInspectoresInternosPreferMirror(texto);

            _logger.LogInfo("[AdminUsuariosController] BuscarUsuarioInternoRT resultados=" + resultados.Count);

            if (resultados.Count == 0)
            {
                return Json(
                    new
                    {
                        success = false,
                        message = "No se encontro coincidencia por cedula o nombre en la base institucional."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            if (resultados.Count > 1)
            {
                return Json(
                    new
                    {
                        success = true,
                        multiple = true,
                        resultados = resultados.Select(r => new
                        {
                            cedula = (r.Cedula ?? string.Empty).Trim(),
                            nombre = (r.NombreCompleto ?? string.Empty).Trim(),
                            tipo = (r.Tipo ?? string.Empty).Trim()
                        }),
                        message = resultados.Count + " coincidencias encontradas. Seleccione una."
                    },
                    JsonRequestBehavior.AllowGet);
            }

            var inspector = resultados[0];
            var cedula = (inspector.Cedula ?? string.Empty).Trim();
            var nombre = (inspector.NombreCompleto ?? string.Empty).Trim();
            var tipo = (inspector.Tipo ?? string.Empty).Trim();

            var usuarioAs400Dao = new UsuarioAS400DAO(new SecureConfigurationService());
            var infoAs400 = usuarioAs400Dao.ObtenerDatosUsuarioInterno(cedula)
                           ?? usuarioAs400Dao.ObtenerDatosUsuarioInterno(nombre)
                           ?? usuarioAs400Dao.ObtenerDatosUsuarioInterno(cedula.PadLeft(10, '0'));
            var opcoi3 = ResolverOpcoi3PorCiudad(
                infoAs400 != null ? infoAs400.CiudadCodigo : string.Empty,
                infoAs400 != null ? infoAs400.Opcoi3 : null);

            var daoInterno = new UsuarioInternoRTDAO();
            var existente = daoInterno.ObtenerActivoPorCodigoUsuario(cedula);

            return Json(
                new
                {
                    success = true,
                    multiple = false,
                    cedula = cedula,
                    nombre = nombre,
                    tipo = tipo,
                    codigoUsuario = cedula,
                    ciudadCodigo = infoAs400 != null ? (infoAs400.CiudadCodigo ?? string.Empty) : string.Empty,
                    codigoFinanciero = infoAs400 != null ? infoAs400.CodigoFinanciero : null,
                    opcoi3 = opcoi3,
                    yaRegistrado = existente != null,
                    message = existente != null
                        ? "El usuario ya tiene un registro interno RT activo."
                        : "Datos cargados correctamente. Complete el rol interno y guarde."
                },
                JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult CrearUsuarioInternoRT(AdminUsuarioInternoRTViewModel model)
        {
            model = model ?? new AdminUsuarioInternoRTViewModel();
            model.CodigoUsuarioBusqueda = NormalizarCodigo(model.CodigoUsuarioBusqueda);
            _logger.LogInfo("[AdminUsuariosController] CrearUsuarioInternoRT POST. Usuario=" + ObtenerActorCodigoUsuario()
                + ", InspectorBusqueda=" + (model.CodigoUsuarioBusqueda ?? string.Empty));

            if (string.IsNullOrWhiteSpace(model.CodigoUsuarioBusqueda))
            {
                ModelState.AddModelError("CodigoUsuarioBusqueda", "Debe ingresar la cedula del inspector.");
            }

            if (!ModelState.IsValid)
            {
                CargarRolesUsuarioInterno(model, model.RolInterno);
                return View(model);
            }

            var inspector = ObtenerInspectorActivoPreferMirror(model.CodigoUsuarioBusqueda);
            if (inspector == null)
            {
                ModelState.AddModelError("CodigoUsuarioBusqueda", "No se encontro coincidencia por cedula o nombre en la base institucional.");
                CargarRolesUsuarioInterno(model, model.RolInterno);
                return View(model);
            }

            var cedula = (inspector.Cedula ?? string.Empty).Trim();
            model.CodigoUsuario = cedula;
            model.Cedula = cedula;
            model.NombreCompleto = (inspector.NombreCompleto ?? string.Empty).Trim();
            model.TipoInspector = (inspector.Tipo ?? string.Empty).Trim();

            var usuarioAs400Dao = new UsuarioAS400DAO(new SecureConfigurationService());
            var infoAs400 = usuarioAs400Dao.ObtenerDatosUsuarioInterno(cedula)
                           ?? usuarioAs400Dao.ObtenerDatosUsuarioInterno(model.CodigoUsuario)
                           ?? usuarioAs400Dao.ObtenerDatosUsuarioInterno(model.NombreCompleto);

            model.CiudadCodigo = infoAs400 != null ? (infoAs400.CiudadCodigo ?? string.Empty) : (model.CiudadCodigo ?? string.Empty);
            model.CodigoFinanciero = infoAs400 != null ? infoAs400.CodigoFinanciero : model.CodigoFinanciero;
            model.Opcoi3 = ResolverOpcoi3PorCiudad(
                model.CiudadCodigo,
                infoAs400 != null ? infoAs400.Opcoi3 : model.Opcoi3);

            string nombres;
            string apellidos;
            SepararNombreCompleto(model.NombreCompleto, out nombres, out apellidos);
            var opcoi3 = ResolverOpcoi3PorCiudad(model.CiudadCodigo, model.Opcoi3);

            var daoInterno = new UsuarioInternoRTDAO();
            var registro = new UsuarioInternoRTRegistro
            {
                UsuarioId = daoInterno.ObtenerUsuarioIdPorCodigoUsuario(cedula),
                Identificacion = cedula,
                Nombres = nombres,
                Apellidos = apellidos,
                CodigoUsuario = cedula,
                NombreCompleto = model.NombreCompleto,
                Tipo = model.TipoInspector,
                EstadoAs400 = "AC",
                CiudadCodigo = (model.CiudadCodigo ?? string.Empty).Trim(),
                CodigoFinanciero = model.CodigoFinanciero ?? 0m,
                Opcar5 = (model.Opcar5 ?? string.Empty).Trim(),
                Opcaer = string.Empty,
                Opcoi3 = opcoi3 ?? 0m,
                CorreoInstitucional = (model.CorreoInstitucional ?? string.Empty).Trim(),
                RolInterno = (model.RolInterno ?? string.Empty).Trim(),
                Observaciones = (model.Observaciones ?? string.Empty).Trim(),
                Activo = model.Activo
            };

            string mensaje;
            if (!UsuarioInternoRTBL.CrearUsuarioInterno(registro, ObtenerActorCodigoUsuario(), out mensaje))
            {
                ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(mensaje)
                    ? "No se pudo guardar el usuario interno RT."
                    : mensaje);
                CargarRolesUsuarioInterno(model, model.RolInterno);
                return View(model);
            }

            TempData["Success"] = mensaje;
            return RedirectToAction("ListarUsuariosInternosRT");
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public ActionResult ListarUsuariosInternosRT()
        {
            var lista = UsuarioInternoRTBL.ListarUsuariosInternos(true) ?? new List<UsuarioInternoRTRegistro>();
            SincronizarVinculoCuentaAccesoEnMemoria(lista);
            return View(lista);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult EditarUsuarioInternoRT(int id)
        {
            var registro = UsuarioInternoRTBL.ObtenerPorId(id);
            if (registro == null)
            {
                TempData["Error"] = "No se encontro el usuario interno RT solicitado.";
                return RedirectToAction("ListarUsuariosInternosRT");
            }

            var model = MapearUsuarioInternoRTViewModel(registro);
            model.Opcoi3 = ResolverOpcoi3PorCiudad(model.CiudadCodigo, model.Opcoi3);
            CargarRolesUsuarioInterno(model, model.RolInterno);
            return View("CrearUsuarioInternoRT", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult EditarUsuarioInternoRT(AdminUsuarioInternoRTViewModel model)
        {
            model = model ?? new AdminUsuarioInternoRTViewModel();
            model.CodigoUsuarioBusqueda = NormalizarCodigo(model.CodigoUsuarioBusqueda);
            model.CodigoUsuario = NormalizarCodigo(model.CodigoUsuario);
            model.Cedula = NormalizarCodigo(model.Cedula);

            if (model.Id <= 0)
            {
                TempData["Error"] = "Identificador de usuario interno RT invalido.";
                return RedirectToAction("ListarUsuariosInternosRT");
            }

            if (string.IsNullOrWhiteSpace(model.CodigoUsuario))
            {
                ModelState.AddModelError("CodigoUsuarioBusqueda", "Debe mantener la cedula del inspector.");
            }

            if (!ModelState.IsValid)
            {
                CargarRolesUsuarioInterno(model, model.RolInterno);
                return View("CrearUsuarioInternoRT", model);
            }

            var opcoi3 = ResolverOpcoi3PorCiudad(model.CiudadCodigo, model.Opcoi3);
            var registro = new UsuarioInternoRTRegistro
            {
                Id = model.Id,
                UsuarioId = string.IsNullOrWhiteSpace(model.CodigoUsuario)
                    ? (int?)null
                    : new UsuarioInternoRTDAO().ObtenerUsuarioIdPorCodigoUsuario(model.CodigoUsuario),
                Identificacion = (model.Cedula ?? string.Empty).Trim(),
                Nombres = string.Empty,
                Apellidos = string.Empty,
                CodigoUsuario = model.CodigoUsuario,
                NombreCompleto = (model.NombreCompleto ?? string.Empty).Trim(),
                Tipo = (model.TipoInspector ?? string.Empty).Trim(),
                EstadoAs400 = "AC",
                CiudadCodigo = (model.CiudadCodigo ?? string.Empty).Trim(),
                CodigoFinanciero = model.CodigoFinanciero ?? 0m,
                Opcar5 = (model.Opcar5 ?? string.Empty).Trim(),
                Opcaer = string.Empty,
                Opcoi3 = opcoi3 ?? 0m,
                CorreoInstitucional = (model.CorreoInstitucional ?? string.Empty).Trim(),
                RolInterno = (model.RolInterno ?? string.Empty).Trim(),
                Observaciones = (model.Observaciones ?? string.Empty).Trim(),
                Activo = model.Activo
            };

            string nombres;
            string apellidos;
            SepararNombreCompleto(model.NombreCompleto, out nombres, out apellidos);
            registro.Nombres = nombres;
            registro.Apellidos = apellidos;

            string mensaje;
            if (!UsuarioInternoRTBL.ActualizarUsuarioInterno(registro, ObtenerActorCodigoUsuario(), out mensaje))
            {
                ModelState.AddModelError(string.Empty, string.IsNullOrWhiteSpace(mensaje)
                    ? "No se pudo actualizar el usuario interno RT."
                    : mensaje);
                CargarRolesUsuarioInterno(model, model.RolInterno);
                return View("CrearUsuarioInternoRT", model);
            }

            TempData["Success"] = mensaje;
            return RedirectToAction("ListarUsuariosInternosRT");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult CambiarEstadoUsuarioInternoRT(int id, bool activo)
        {
            string mensaje;
            if (!UsuarioInternoRTBL.CambiarEstado(id, activo, ObtenerActorCodigoUsuario(), out mensaje))
            {
                TempData["Error"] = mensaje;
                return RedirectToAction("ListarUsuariosInternosRT");
            }

            TempData["Success"] = mensaje;
            return RedirectToAction("ListarUsuariosInternosRT");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_RESET_PASSWORD")]
        public ActionResult ReenviarNotificacionUsuarioInternoRT(int id)
        {
            string mensajeExito;
            string mensajeWarning;
            string mensajeError;
            string passwordTemporalFallback;

            if (!ProcesarProvisionCuentaInspectorRT(
                id,
                crearCuentaSiNoExiste: true,
                resetearClaveSiExiste: true,
                out mensajeExito,
                out mensajeWarning,
                out mensajeError,
                out passwordTemporalFallback))
            {
                TempData["Error"] = mensajeError;
                return RedirectToAction("ListarUsuariosInternosRT");
            }

            TempData["Success"] = mensajeExito;
            if (!string.IsNullOrWhiteSpace(mensajeWarning))
            {
                TempData["Warning"] = mensajeWarning;
            }

            if (!string.IsNullOrWhiteSpace(passwordTemporalFallback))
            {
                TempData["PasswordTemporal"] = passwordTemporalFallback;
            }

            return RedirectToAction("ListarUsuariosInternosRT");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult CrearCuentaInspectorRT(int id)
        {
            string mensajeExito;
            string mensajeWarning;
            string mensajeError;
            string passwordTemporalFallback;

            if (!ProcesarProvisionCuentaInspectorRT(
                id,
                crearCuentaSiNoExiste: true,
                resetearClaveSiExiste: false,
                out mensajeExito,
                out mensajeWarning,
                out mensajeError,
                out passwordTemporalFallback))
            {
                TempData["Error"] = mensajeError;
                return RedirectToAction("ListarUsuariosInternosRT");
            }

            TempData["Success"] = mensajeExito;
            if (!string.IsNullOrWhiteSpace(mensajeWarning))
            {
                TempData["Warning"] = mensajeWarning;
            }

            if (!string.IsNullOrWhiteSpace(passwordTemporalFallback))
            {
                TempData["PasswordTemporal"] = passwordTemporalFallback;
            }

            return RedirectToAction("ListarUsuariosInternosRT");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        [RequirePermission("ADM_RESET_PASSWORD")]
        public ActionResult ResetearClaveRT(int id)
        {
            string mensajeExito;
            string mensajeWarning;
            string mensajeError;
            string passwordTemporalFallback;

            if (!ProcesarProvisionCuentaInspectorRT(
                id,
                crearCuentaSiNoExiste: true,
                resetearClaveSiExiste: true,
                out mensajeExito,
                out mensajeWarning,
                out mensajeError,
                out passwordTemporalFallback))
            {
                TempData["Error"] = mensajeError;
                return RedirectToAction("ListarUsuariosInternosRT");
            }

            TempData["Success"] = mensajeExito;
            if (!string.IsNullOrWhiteSpace(mensajeWarning))
            {
                TempData["Warning"] = mensajeWarning;
            }

            if (!string.IsNullOrWhiteSpace(passwordTemporalFallback))
            {
                TempData["PasswordTemporal"] = passwordTemporalFallback;
            }

            return RedirectToAction("ListarUsuariosInternosRT");
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
            NormalizarNombreApellidoParaEdicion(vm);

            CargarRolesParaFormulario(vm);
            CargarCompaniasParaFormulario(vm, id);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult Edit(AdminUsuarioFormViewModel model)
        {
            NormalizarNombreApellidoParaEdicion(model);
            ValidarFormularioUsuario(model, isEdit: true);

            if (!ModelState.IsValid)
            {
                CargarRolesParaFormulario(model);
                CargarCompaniasParaFormulario(model, model.IdUsuario > 0 ? (int?)model.IdUsuario : null);
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
                CargarCompaniasParaFormulario(model, model.IdUsuario > 0 ? (int?)model.IdUsuario : null);
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

            string mensajeCompaniasEdit;
            if (!GuardarCompaniasRtUsuario(model.IdUsuario, model.CompaniasSeleccionadas, out mensajeCompaniasEdit))
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(mensajeCompaniasEdit)
                    ? "Usuario actualizado, pero no se pudieron guardar las compaÃ±Ã­as RT."
                    : mensajeCompaniasEdit;
            }

            TempData["Success"] = mensaje;
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult ReenviarDeclaracionResponsabilidad(int idUsuario)
        {
            if (idUsuario <= 0)
            {
                TempData["Error"] = "Usuario invalido para reenvio de declaracion.";
                return RedirectToAction("Index");
            }

            try
            {
                var usuarioAdmin = AdminUsuariosBL.ObtenerUsuarioPorId(idUsuario);
                if (usuarioAdmin == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction("Index");
                }

                var usuarioDb = UsuarioDAO.ObtenerPorId(idUsuario);
                var correoDestino = (usuarioAdmin.Correo ?? usuarioDb?.Email ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(correoDestino))
                {
                    TempData["Error"] = "El usuario no tiene correo registrado.";
                    return RedirectToAction("Edit", new { id = idUsuario });
                }

                var rtDao = new RTDao();
                CapaModelo.RT.SolicitudRTModel solicitud = null;
                try
                {
                    solicitud = rtDao.GetSolicitudByUsuario(idUsuario);
                }
                catch (Exception exSolicitud)
                {
                    LogBL.RegistrarError(
                        "No se pudo consultar aocr_solicitud_rt al reenviar declaracion. Se aplicara fallback por historial.",
                        exSolicitud.ToString(),
                        "AdminUsuariosController");
                }

                var declaracionDao = new DeclaracionTemporalDAO();
                var historialDeclaracion = declaracionDao.GetUltimaAceptadaHistorial(correoDestino);

                var declaracionAceptada = (solicitud != null && solicitud.DeclaracionAceptada)
                    || (historialDeclaracion != null && historialDeclaracion.Aceptada);
                if (!declaracionAceptada)
                {
                    TempData["Error"] = "El usuario no tiene una declaracion de responsabilidad aceptada para reenviar.";
                    return RedirectToAction("Edit", new { id = idUsuario });
                }

                var companias = ObtenerCompaniasDeclaracionUsuario(idUsuario, usuarioDb);
                if (historialDeclaracion != null)
                {
                    var codigosHistorial = ParsearCodigosCompaniaLegacy(historialDeclaracion.EmpresaCodigo);
                    if (codigosHistorial.Count == 0)
                    {
                        codigosHistorial = ExtraerCodigosCompaniaDesdeTexto(historialDeclaracion.EmpresaNombre);
                    }

                    foreach (var codigo in codigosHistorial)
                    {
                        if (companias.Any(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        companias.Add(new CompaniaDeclaracionItem
                        {
                            Codigo = codigo,
                            Nombre = ResolverNombreCompania(codigo)
                        });
                    }
                }

                if (companias.Count == 0)
                {
                    TempData["Error"] = "No se encontraron companias asociadas para generar la declaracion.";
                    return RedirectToAction("Edit", new { id = idUsuario });
                }

                var nombreCompleto = ConstruirNombreCompletoUsuario(usuarioAdmin, usuarioDb);
                var identificacion = (usuarioDb != null ? usuarioDb.Ruc : string.Empty) ?? string.Empty;
                var textoDeclaracion = (solicitud != null && !string.IsNullOrWhiteSpace(solicitud.DeclaracionTexto))
                    ? solicitud.DeclaracionTexto.Trim()
                    : ConstruirTextoDeclaracionResponsabilidad(nombreCompleto, companias);
                var fechaAceptacion = solicitud != null
                    ? (solicitud.UpdatedAt ?? solicitud.CreatedAt ?? DateTime.Now)
                    : (historialDeclaracion != null
                        ? (historialDeclaracion.FinalizedAt ?? historialDeclaracion.UpdatedAt ?? historialDeclaracion.CreatedAt ?? DateTime.Now)
                        : DateTime.Now);
                var referenciaTramite = solicitud != null
                    ? ("Solicitud RT #" + solicitud.Id)
                    : "Declaracion RT (historial)";

                var pdfBytes = GenerarPdfDeclaracionResponsabilidad(
                    nombreCompleto,
                    identificacion.Trim(),
                    companias,
                    textoDeclaracion,
                    fechaAceptacion);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    TempData["Error"] = "No se pudo generar el PDF de declaracion para el reenvio.";
                    return RedirectToAction("Edit", new { id = idUsuario });
                }

                var companiasHtml = ConstruirCompaniasHtmlCorreo(companias);
                var asunto = "Reenvio - Declaracion de responsabilidad aceptada - Sistema AOCR";
                var cuerpo = $@"
                    <div style='font-family:Arial,sans-serif; font-size:14px; color:#222;'>
                        <p>Estimado/a {HttpUtility.HtmlEncode(nombreCompleto)},</p>
                        <p>De acuerdo con su solicitud, reenviamos el comprobante de aceptacion de su declaracion de responsabilidad RT.</p>
                        <p><strong>Tramite:</strong> {HttpUtility.HtmlEncode(referenciaTramite)}</p>
                        <p><strong>Fecha de aceptacion registrada:</strong> {fechaAceptacion:dd/MM/yyyy HH:mm}</p>
                        <p><strong>Companias declaradas:</strong></p>
                        {companiasHtml}
                        <p>Adjunto encontrara el PDF institucional de la declaracion.</p>
                        <hr />
                        <small>Este es un correo automatico, por favor no responder.</small>
                    </div>";

                var codigoUsuario = (usuarioAdmin.CodigoUsuario ?? usuarioDb?.CodigoUsuario ?? idUsuario.ToString()).Trim();
                var nombreAdjunto = string.Format(
                    "Declaracion_Responsabilidad_RT_{0}_{1:yyyyMMddHHmmss}.pdf",
                    codigoUsuario,
                    fechaAceptacion);

                var servicioCorreo = new EnviarCorreo();
                var enviado = servicioCorreo.enviaMensajeCorreoConAdjunto(
                    correoDestino,
                    asunto,
                    cuerpo,
                    pdfBytes,
                    nombreAdjunto,
                    "application/pdf");

                TempData[enviado ? "Success" : "Error"] = enviado
                    ? "Correo de declaracion reenviado correctamente."
                    : "No se pudo reenviar el correo de declaracion. Verifique SMTP.";

                return RedirectToAction("Edit", new { id = idUsuario });
            }
            catch (Exception ex)
            {
                LogBL.RegistrarError(
                    "Error al reenviar declaracion de responsabilidad desde AdminUsuarios.",
                    ex.ToString(),
                    "AdminUsuariosController");

                TempData["Error"] = "Ocurrio un error al reenviar la declaracion de responsabilidad.";
                return RedirectToAction("Edit", new { id = idUsuario });
            }
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
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult EliminarPermanente(int id)
        {
            // Obtener datos del usuario para determinar tipo
            var usuario = AdminUsuariosBL.ObtenerUsuarioPorId(id);
            if (usuario == null)
            {
                TempData["Error"] = "Usuario no encontrado.";
                return RedirectToAction("Index");
            }

            // Intentar eliminación física controlada de cualquier usuario
            string mensaje;
            var ok = AdminUsuariosBL.EliminarUsuarioPermanente(
                id,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out mensaje);

            if (ok)
            {
                TempData["Success"] = mensaje;
            }
            else
            {
                // La eliminación falló (tiene relaciones) → desactivar como fallback
                string msgFallback;
                var fallbackOk = AdminUsuariosBL.CambiarEstadoUsuario(
                    id,
                    false,
                    ObtenerActorId(),
                    ObtenerActorCodigoUsuario(),
                    Request != null ? Request.UserHostAddress : null,
                    out msgFallback);

                if (fallbackOk)
                {
                    TempData["Error"] = mensaje +
                        " El usuario fue desactivado en su lugar para preservar la trazabilidad.";
                }
                else
                {
                    TempData["Error"] = mensaje;
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_RESET_PASSWORD")]
        public ActionResult ReenviarCredenciales(int id)
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
                TempData["Success"] = "Credenciales reenviadas correctamente. " + mensaje;
                if (!string.IsNullOrWhiteSpace(passwordTemporal))
                {
                    TempData["PasswordTemporal"] = string.Format(
                        "Nueva contrasena temporal generada: {0}",
                        passwordTemporal);
                }
            }
            else
            {
                TempData["Error"] = mensaje;
            }

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

        [HttpGet]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult TransferirEliminar(int id)
        {
            if (id <= 0)
            {
                TempData["Error"] = "Usuario invalido.";
                return RedirectToAction("Index");
            }

            var vm = new AdminUsuarioTransferenciaViewModel
            {
                UsuarioOrigenId = id
            };

            if (!CargarContextoTransferencia(vm))
            {
                TempData["Error"] = "Usuario origen no encontrado.";
                return RedirectToAction("Index");
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("ADM_GESTION_USUARIOS")]
        public ActionResult TransferirEliminar(AdminUsuarioTransferenciaViewModel model)
        {
            if (model == null)
            {
                TempData["Error"] = "Solicitud invalida.";
                return RedirectToAction("Index");
            }

            if (!model.ConfirmarTransferencia)
            {
                ModelState.AddModelError("ConfirmarTransferencia", "Debe confirmar la operacion.");
            }

            if (model.UsuarioOrigenId == model.UsuarioDestinoId)
            {
                ModelState.AddModelError("UsuarioDestinoId", "El usuario destino no puede ser igual al usuario origen.");
            }

            if (!ModelState.IsValid)
            {
                CargarContextoTransferencia(model);
                return View(model);
            }

            UsuarioTransferenciaResultadoDTO resultado;
            string mensaje;
            var ok = AdminUsuariosBL.TransferirYDesactivarUsuario(
                model.UsuarioOrigenId,
                model.UsuarioDestinoId,
                model.Motivo,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out resultado,
                out mensaje);

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, mensaje);
                model.Resultado = resultado;
                CargarContextoTransferencia(model);
                return View(model);
            }

            var mensajeExito = string.Format(
                "Transferencia completada. Detectados: {0}, transferidos: {1}. Usuario origen desactivado.",
                resultado != null ? resultado.TotalRegistrosDetectados : 0,
                resultado != null ? resultado.TotalRegistrosTransferidos : 0);

            TempData["Success"] = mensajeExito;
            return RedirectToAction("Index");
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

        private static void NormalizarNombreApellidoParaEdicion(AdminUsuarioFormViewModel model)
        {
            if (model == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(model.ApellidoUsuario))
            {
                model.NombreUsuario = (model.NombreUsuario ?? string.Empty).Trim();
                model.ApellidoUsuario = (model.ApellidoUsuario ?? string.Empty).Trim();
                return;
            }

            var nombreCrudo = (model.NombreUsuario ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombreCrudo))
            {
                return;
            }

            string nombres;
            string apellidos;
            SepararNombreCompleto(nombreCrudo, out nombres, out apellidos);

            model.NombreUsuario = string.IsNullOrWhiteSpace(nombres) ? nombreCrudo : nombres;
            model.ApellidoUsuario = string.IsNullOrWhiteSpace(apellidos) ? string.Empty : apellidos;
        }

        private static void SepararNombreCompleto(string nombreCompleto, out string nombres, out string apellidos)
        {
            nombres = string.Empty;
            apellidos = string.Empty;

            var limpio = (nombreCompleto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(limpio))
            {
                return;
            }

            var partes = limpio
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (partes.Count <= 1)
            {
                nombres = limpio;
                return;
            }

            if (partes.Count == 2)
            {
                nombres = partes[0];
                apellidos = partes[1];
                return;
            }

            if (partes.Count == 3)
            {
                nombres = string.Join(" ", partes.Take(2));
                apellidos = partes[2];
                return;
            }

            nombres = string.Join(" ", partes.Take(partes.Count - 2));
            apellidos = string.Join(" ", partes.Skip(partes.Count - 2));
        }

        private static string ConstruirNombreCompletoUsuario(SeguridadUsuarioDTO usuarioAdmin, Usuario usuarioDb)
        {
            var nombres = (usuarioAdmin != null ? usuarioAdmin.NombreUsuario : string.Empty) ?? string.Empty;
            var apellidos = (usuarioAdmin != null ? usuarioAdmin.ApellidoUsuario : string.Empty) ?? string.Empty;
            var combinado = string.Format("{0} {1}", nombres, apellidos).Trim();
            if (!string.IsNullOrWhiteSpace(combinado))
            {
                return combinado.ToUpperInvariant();
            }

            var nombreCompletoDb = (usuarioDb != null ? usuarioDb.NombreCompleto : string.Empty) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(nombreCompletoDb))
            {
                return nombreCompletoDb.Trim().ToUpperInvariant();
            }

            var codigo = (usuarioAdmin != null ? usuarioAdmin.CodigoUsuario : string.Empty) ?? string.Empty;
            return string.IsNullOrWhiteSpace(codigo) ? "USUARIO RT" : codigo.Trim().ToUpperInvariant();
        }

        private List<CompaniaDeclaracionItem> ObtenerCompaniasDeclaracionUsuario(int usuarioId, Usuario usuarioDb)
        {
            var resultado = new List<CompaniaDeclaracionItem>();
            var daoCompanias = new UsuarioCompaniaRTDAO();
            var companiasAsignadas = new List<UsuarioCompaniaRT>();

            try
            {
                companiasAsignadas = daoCompanias.ObtenerCompaniasAsignadas(usuarioId);
            }
            catch
            {
                companiasAsignadas = new List<UsuarioCompaniaRT>();
            }

            foreach (var compania in companiasAsignadas)
            {
                var codigo = (compania.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    continue;
                }

                var nombre = (compania.CompaniaNombre ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    nombre = ResolverNombreCompania(codigo);
                }

                resultado.Add(new CompaniaDeclaracionItem
                {
                    Codigo = codigo,
                    Nombre = string.IsNullOrWhiteSpace(nombre) ? codigo : nombre
                });
            }

            var codigosLegacy = ParsearCodigosCompaniaLegacy(usuarioDb != null ? usuarioDb.EmpresaCodigo : null);
            foreach (var codigo in codigosLegacy)
            {
                if (resultado.Any(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                resultado.Add(new CompaniaDeclaracionItem
                {
                    Codigo = codigo,
                    Nombre = ResolverNombreCompania(codigo)
                });
            }

            return resultado
                .GroupBy(c => c.Codigo, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
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

        private string ResolverNombreCompania(string codigoCompania)
        {
            var codigo = (codigoCompania ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return string.Empty;
            }

            try
            {
                var daoEmpresa = new EmpresaAS400DAO(new SecureConfigurationService());
                var empresa = daoEmpresa.ObtenerEmpresaPorCodigo(codigo);
                if (empresa != null && !string.IsNullOrWhiteSpace(empresa.Nombre))
                {
                    return empresa.Nombre.Trim();
                }
            }
            catch
            {
                // No bloquear por resoluciÃ³n de nombre.
            }

            return codigo;
        }

        private static string ConstruirTextoDeclaracionResponsabilidad(string nombreCompleto, IList<CompaniaDeclaracionItem> companias)
        {
            var nombre = string.IsNullOrWhiteSpace(nombreCompleto)
                ? "__________________________"
                : nombreCompleto.Trim().ToUpperInvariant();

            var listado = (companias ?? new List<CompaniaDeclaracionItem>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                .Select((c, index) => string.Format("{0}. {1}", index + 1, FormatearCompania(c)))
                .ToList();

            if (listado.Count == 0)
            {
                listado.Add("1. __________________________");
            }

            var sb = new StringBuilder();
            sb.Append("Yo, ");
            sb.Append(nombre);
            sb.Append(" declaro conocer las politicas y procedimientos tecnicos y operativos aplicables en las estaciones regulares de Ecuador para las siguientes companias:");
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(string.Join(Environment.NewLine, listado));
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("Asumo la responsabilidad como RT de mantener comunicacion directa con la DGAC del Ecuador, a fin de gestionar los tramites de emision, renovacion o modificacion del AOCR; asi como tambien, de mantener la supervision de las empresas contratadas para la asistencia tecnica en tierra a sus aeronaves en los aeropuertos de Ecuador.");

            return sb.ToString();
        }

        private static string ConstruirCompaniasHtmlCorreo(IList<CompaniaDeclaracionItem> companias)
        {
            var items = (companias ?? new List<CompaniaDeclaracionItem>())
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                .Select(FormatearCompania)
                .ToList();

            if (items.Count == 0)
            {
                return "<p>No se registraron companias en la declaracion.</p>";
            }

            var sb = new StringBuilder();
            sb.Append("<ul>");
            foreach (var item in items)
            {
                sb.Append("<li>");
                sb.Append(HttpUtility.HtmlEncode(item));
                sb.Append("</li>");
            }
            sb.Append("</ul>");
            return sb.ToString();
        }

        private static string FormatearCompania(CompaniaDeclaracionItem compania)
        {
            if (compania == null)
            {
                return "Compania no especificada";
            }

            var codigo = (compania.Codigo ?? string.Empty).Trim().ToUpperInvariant();
            var nombre = (compania.Nombre ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(codigo) && string.IsNullOrWhiteSpace(nombre))
            {
                return "Compania no especificada";
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                return nombre;
            }

            return "[" + codigo + "] " + (string.IsNullOrWhiteSpace(nombre) ? codigo : nombre);
        }

        private static byte[] GenerarPdfDeclaracionResponsabilidad(
            string nombreCompleto,
            string identificacion,
            IList<CompaniaDeclaracionItem> companias,
            string textoDeclaracion,
            DateTime fechaAceptacion)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 36f, 36f, 130f, 90f);
                var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);
                var server = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Server : null;
                writer.PageEvent = PdfBrandingHelper.CreateITextPageEvent(server, "AdminUsuariosController.ReenviarDeclaracionResponsabilidad");
                doc.Open();

                var titleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 14);
                var subtitleFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA_BOLD, 11);
                var normalFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 10);
                var smallFont = iTextSharp.text.FontFactory.GetFont(iTextSharp.text.FontFactory.HELVETICA, 9);

                var titulo = new iTextSharp.text.Paragraph("DECLARACION DE RESPONSABILIDAD", titleFont)
                {
                    Alignment = iTextSharp.text.Element.ALIGN_CENTER,
                    SpacingAfter = 10f
                };
                doc.Add(titulo);

                var datos = new iTextSharp.text.pdf.PdfPTable(2)
                {
                    WidthPercentage = 100,
                    SpacingAfter = 8f
                };
                datos.SetWidths(new[] { 35f, 65f });

                AgregarFilaTabla(datos, "Representante tecnico:", string.IsNullOrWhiteSpace(nombreCompleto) ? "N/D" : nombreCompleto.Trim().ToUpperInvariant(), normalFont);
                AgregarFilaTabla(datos, "Identificacion:", string.IsNullOrWhiteSpace(identificacion) ? "N/D" : identificacion.Trim(), normalFont);
                AgregarFilaTabla(datos, "Fecha aceptacion:", fechaAceptacion.ToString("dd/MM/yyyy HH:mm"), normalFont);
                AgregarFilaTabla(datos, "Referencia:", "DECL-RT-" + fechaAceptacion.ToString("yyyyMMddHHmmss"), normalFont);
                doc.Add(datos);

                var subtituloCompanias = new iTextSharp.text.Paragraph("Companias declaradas", subtitleFont)
                {
                    SpacingAfter = 4f
                };
                doc.Add(subtituloCompanias);

                var companiasNormalizadas = (companias ?? new List<CompaniaDeclaracionItem>())
                    .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Codigo))
                    .ToList();
                if (companiasNormalizadas.Count == 0)
                {
                    companiasNormalizadas.Add(new CompaniaDeclaracionItem
                    {
                        Codigo = string.Empty,
                        Nombre = "No especificada"
                    });
                }

                var listaCompanias = new iTextSharp.text.List(iTextSharp.text.List.ORDERED, 12f);
                foreach (var compania in companiasNormalizadas)
                {
                    listaCompanias.Add(new iTextSharp.text.ListItem(FormatearCompania(compania), normalFont));
                }
                doc.Add(listaCompanias);
                doc.Add(new iTextSharp.text.Paragraph(" "));

                var subtituloDeclaracion = new iTextSharp.text.Paragraph("Texto de la declaracion", subtitleFont)
                {
                    SpacingAfter = 4f
                };
                doc.Add(subtituloDeclaracion);

                var cuerpo = new iTextSharp.text.Paragraph(textoDeclaracion ?? string.Empty, normalFont)
                {
                    Alignment = iTextSharp.text.Element.ALIGN_JUSTIFIED,
                    SpacingAfter = 16f
                };
                cuerpo.SetLeading(0f, 1.5f);
                doc.Add(cuerpo);

                doc.Add(new iTextSharp.text.Paragraph("______________________________________________", normalFont));
                doc.Add(new iTextSharp.text.Paragraph("Aceptacion del Responsable Tecnico", smallFont));
                doc.Add(new iTextSharp.text.Paragraph("Documento generado automaticamente por AOCR.", smallFont));

                doc.Close();
                return ms.ToArray();
            }
        }

        private static void AgregarFilaTabla(iTextSharp.text.pdf.PdfPTable tabla, string etiqueta, string valor, iTextSharp.text.Font font)
        {
            var cellEtiqueta = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(etiqueta, font))
            {
                Border = iTextSharp.text.Rectangle.BOX,
                Padding = 5f,
                BackgroundColor = new iTextSharp.text.BaseColor(244, 246, 248)
            };

            var cellValor = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Phrase(valor ?? string.Empty, font))
            {
                Border = iTextSharp.text.Rectangle.BOX,
                Padding = 5f
            };

            tabla.AddCell(cellEtiqueta);
            tabla.AddCell(cellValor);
        }

        private sealed class CompaniaDeclaracionItem
        {
            public string Codigo { get; set; }
            public string Nombre { get; set; }
        }

        private void CargarRolesParaFormulario(AdminUsuarioFormViewModel model)
        {
            model.RolesDisponibles = ObtenerRolesSelectList(model.RolesSeleccionados);
        }

        private void CargarCompaniasParaFormulario(AdminUsuarioFormViewModel model, int? idUsuario)
        {
            if (model == null)
            {
                return;
            }

            var seleccionadas = new List<string>();
            if (idUsuario.HasValue && idUsuario.Value > 0)
            {
                try
                {
                    var daoCompanias = new UsuarioCompaniaRTDAO();
                    seleccionadas = daoCompanias.ObtenerCompaniasAsignadas(idUsuario.Value)
                        .Select(c => (c.CompaniaCodigo ?? string.Empty).Trim().ToUpperInvariant())
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch
                {
                    seleccionadas = new List<string>();
                }

                if (seleccionadas.Count == 0)
                {
                    var usuario = UsuarioDAO.ObtenerPorId(idUsuario.Value);
                    seleccionadas = ParsearCodigosCompaniaLegacy(usuario != null ? usuario.EmpresaCodigo : null);
                }
            }
            else if (model.CompaniasSeleccionadas != null && model.CompaniasSeleccionadas.Any())
            {
                seleccionadas = model.CompaniasSeleccionadas
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            model.CompaniasSeleccionadas = seleccionadas;
            model.CatalogoCompanias = ConstruirCatalogoCompaniasSelect(seleccionadas);
        }

        private List<SelectListItem> ConstruirCatalogoCompaniasSelect(IEnumerable<string> seleccionadas)
        {
            var seleccionLookup = new HashSet<string>(
                (seleccionadas ?? Enumerable.Empty<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim().ToUpperInvariant()),
                StringComparer.OrdinalIgnoreCase);

            var items = new List<SelectListItem>();
            var catalogo = new List<Empresa>();
            try
            {
                var daoEmpresa = new EmpresaAS400DAO(new SecureConfigurationService());
                catalogo = daoEmpresa.ObtenerEmpresas() ?? new List<Empresa>();
            }
            catch
            {
                catalogo = new List<Empresa>();
            }

            foreach (var empresa in catalogo
                .Where(e => e != null && !string.IsNullOrWhiteSpace(e.CodigoOaci))
                .OrderBy(e => e.Nombre ?? string.Empty))
            {
                var codigo = (empresa.CodigoOaci ?? string.Empty).Trim().ToUpperInvariant();
                var nombre = (empresa.Nombre ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    continue;
                }

                items.Add(new SelectListItem
                {
                    Value = codigo,
                    Text = string.IsNullOrWhiteSpace(nombre) ? codigo : nombre,
                    Selected = seleccionLookup.Contains(codigo)
                });
            }

            foreach (var codigoSel in seleccionLookup)
            {
                if (items.Any(i => string.Equals(i.Value, codigoSel, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                items.Add(new SelectListItem
                {
                    Value = codigoSel,
                    Text = ResolverNombreCompania(codigoSel),
                    Selected = true
                });
            }

            return items;
        }

        private bool GuardarCompaniasRtUsuario(int usuarioId, IEnumerable<string> codigosSeleccionados, out string mensaje)
        {
            mensaje = string.Empty;
            if (usuarioId <= 0)
            {
                mensaje = "No se pudo identificar el usuario para guardar compaÃ±Ã­as RT.";
                return false;
            }

            var codigos = (codigosSeleccionados ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codigos.Count == 0)
            {
                return true;
            }

            var actor = ObtenerActorCodigoUsuario();
            var asignaciones = codigos
                .Select(c => new UsuarioCompaniaRT
                {
                    UsuarioId = usuarioId,
                    CompaniaCodigo = c,
                    CompaniaNombre = ResolverNombreCompania(c),
                    Activo = true
                })
                .ToList();

            try
            {
                var daoCompanias = new UsuarioCompaniaRTDAO();
                var guardado = daoCompanias.GuardarAsignaciones(usuarioId, asignaciones, actor, true);
                if (!guardado)
                {
                    UsuarioDAO.ActualizarEmpresaCodigoPrincipal(usuarioId, string.Join(",", codigos));
                    mensaje = "No se pudo persistir la relacion usuario RT - companias. Se aplico fallback legacy temporal.";
                    return false;
                }

                UsuarioDAO.ActualizarEmpresaCodigoPrincipal(usuarioId, codigos[0]);
                return true;
            }
            catch (Exception ex)
            {
                mensaje = "Error guardando compaÃ±Ã­as RT: " + ex.Message;
                return false;
            }
        }

        private string ObtenerCodigoCompaniaOperativa(string companiaCodigoPreferida)
        {
            var codigoPreferido = (companiaCodigoPreferida ?? string.Empty).Trim().ToUpperInvariant();
            var codigoSesion = (CompaniaActivaSessionHelper.ObtenerCodigo(Session) ?? string.Empty).Trim().ToUpperInvariant();

            var usuarioId = ObtenerActorId() ?? 0;
            if (usuarioId > 0)
            {
                var usuarioDb = UsuarioDAO.ObtenerPorId(usuarioId);
                var companiasPermitidas = ObtenerCompaniasDeclaracionUsuario(usuarioId, usuarioDb);

                if (!string.IsNullOrWhiteSpace(codigoPreferido))
                {
                    var coincidenciaPreferida = companiasPermitidas.FirstOrDefault(c =>
                        string.Equals((c.Codigo ?? string.Empty).Trim(), codigoPreferido, StringComparison.OrdinalIgnoreCase));
                    if (coincidenciaPreferida != null)
                    {
                        return (coincidenciaPreferida.Codigo ?? string.Empty).Trim().ToUpperInvariant();
                    }
                }

                if (!string.IsNullOrWhiteSpace(codigoSesion))
                {
                    var coincidenciaSesion = companiasPermitidas.FirstOrDefault(c =>
                        string.Equals((c.Codigo ?? string.Empty).Trim(), codigoSesion, StringComparison.OrdinalIgnoreCase));
                    if (coincidenciaSesion != null)
                    {
                        return (coincidenciaSesion.Codigo ?? string.Empty).Trim().ToUpperInvariant();
                    }
                }

                if (companiasPermitidas.Count == 1)
                {
                    return (companiasPermitidas[0].Codigo ?? string.Empty).Trim().ToUpperInvariant();
                }
            }

            if (!string.IsNullOrWhiteSpace(codigoPreferido))
            {
                return codigoPreferido;
            }

            return codigoSesion;
        }

        private string ResolverNombreCompaniaOperativa(string companiaCodigo)
        {
            var codigo = (companiaCodigo ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return (CompaniaActivaSessionHelper.ObtenerNombre(Session) ?? string.Empty).Trim();
            }

            var usuarioId = ObtenerActorId() ?? 0;
            if (usuarioId > 0)
            {
                var usuarioDb = UsuarioDAO.ObtenerPorId(usuarioId);
                var coincidencia = ObtenerCompaniasDeclaracionUsuario(usuarioId, usuarioDb)
                    .FirstOrDefault(c => string.Equals((c.Codigo ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase));
                if (coincidencia != null && !string.IsNullOrWhiteSpace(coincidencia.Nombre))
                {
                    return coincidencia.Nombre.Trim();
                }
            }

            var nombreSesion = (CompaniaActivaSessionHelper.ObtenerNombre(Session) ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(nombreSesion) &&
                string.Equals((CompaniaActivaSessionHelper.ObtenerCodigo(Session) ?? string.Empty).Trim(), codigo, StringComparison.OrdinalIgnoreCase))
            {
                return nombreSesion;
            }

            return ResolverNombreCompania(codigo);
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

        private bool CargarContextoTransferencia(AdminUsuarioTransferenciaViewModel model)
        {
            if (model == null || model.UsuarioOrigenId <= 0)
            {
                return false;
            }

            var usuario = AdminUsuariosBL.ObtenerUsuarioPorId(model.UsuarioOrigenId);
            if (usuario == null)
            {
                return false;
            }

            model.UsuarioOrigenCodigo = usuario.CodigoUsuario;
            model.UsuarioOrigenCorreo = usuario.Correo;
            model.UsuarioOrigenActivo = usuario.Activo;
            model.UsuarioOrigenNombreCompleto = string.Format(
                "{0} {1}",
                usuario.NombreUsuario ?? string.Empty,
                usuario.ApellidoUsuario ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(model.UsuarioOrigenNombreCompleto))
            {
                model.UsuarioOrigenNombreCompleto = usuario.CodigoUsuario;
            }

            CargarUsuariosDestinoTransferencia(model);
            model.Impacto = AdminUsuariosBL.ObtenerImpactoTransferencia(model.UsuarioOrigenId);
            return true;
        }

        private void CargarUsuariosDestinoTransferencia(AdminUsuarioTransferenciaViewModel model)
        {
            var usuarios = AdminUsuariosBL.ObtenerUsuariosActivosParaTransferencia(model.UsuarioOrigenId);
            model.UsuariosDestino = usuarios
                .Where(u => u.IdUsuario > 0)
                .Select(u => new SelectListItem
                {
                    Value = u.IdUsuario.ToString(),
                    Text = string.Format(
                        "{0} - {1} {2}",
                        u.CodigoUsuario,
                        u.NombreUsuario ?? string.Empty,
                        u.ApellidoUsuario ?? string.Empty).Trim(),
                    Selected = u.IdUsuario == model.UsuarioDestinoId
                })
                .ToList();
        }

        private static string NormalizarCodigo(string value, int maxLength = 64)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalizado = value.Trim().ToUpperInvariant();
            if (normalizado.Length > maxLength)
            {
                normalizado = normalizado.Substring(0, maxLength);
            }

            return normalizado;
        }

        private decimal? ResolverOpcoi3PorCiudad(string ciudadCodigo, decimal? fallback = null)
        {
            var codigoNormalizado = NormalizarCodigo(ciudadCodigo, 10);
            if (string.IsNullOrWhiteSpace(codigoNormalizado))
            {
                return fallback.HasValue && fallback.Value > 0m ? fallback : null;
            }

            try
            {
                var ubicacion = CD_UbicacionUsuario.Instancia.UbicacionUsuarioPorCiudad(codigoNormalizado);
                if (ubicacion != null && ubicacion.OidUbicacion > 0m)
                {
                    return ubicacion.OidUbicacion;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    string.Format(
                        "[AdminUsuariosController] No se pudo resolver OPUOID para ciudad {0}: {1}",
                        codigoNormalizado,
                        ex.Message));
            }

            return fallback.HasValue && fallback.Value > 0m ? fallback : null;
        }

        private void SincronizarVinculoCuentaAccesoEnMemoria(IList<UsuarioInternoRTRegistro> registros)
        {
            if (registros == null || registros.Count == 0)
            {
                return;
            }

            var dao = new UsuarioInternoRTDAO();
            foreach (var registro in registros)
            {
                if (registro == null)
                {
                    continue;
                }

                if (registro.UsuarioId.HasValue && registro.UsuarioId.Value > 0)
                {
                    continue;
                }

                var usuarioId = ResolverUsuarioIdAocrDesdeRegistro(registro, dao);
                if (usuarioId.HasValue && usuarioId.Value > 0)
                {
                    registro.UsuarioId = usuarioId.Value;
                }
            }
        }

        private bool ProcesarProvisionCuentaInspectorRT(
            int registroId,
            bool crearCuentaSiNoExiste,
            bool resetearClaveSiExiste,
            out string mensajeExito,
            out string mensajeWarning,
            out string mensajeError,
            out string passwordTemporalFallback)
        {
            mensajeExito = string.Empty;
            mensajeWarning = string.Empty;
            mensajeError = string.Empty;
            passwordTemporalFallback = string.Empty;

            var registro = UsuarioInternoRTBL.ObtenerPorId(registroId);
            if (registro == null)
            {
                mensajeError = "No se encontro el inspector solicitado.";
                return false;
            }

            if (!registro.Activo)
            {
                mensajeError = "El inspector se encuentra inactivo. Active el registro antes de gestionar credenciales.";
                return false;
            }

            var correoDestino = (registro.CorreoInstitucional ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(correoDestino))
            {
                mensajeError = "El inspector no tiene correo institucional registrado.";
                return false;
            }

            var rolInspectorId = ObtenerCodigoRolInspector();
            if (!rolInspectorId.HasValue || rolInspectorId.Value <= 0)
            {
                mensajeError = "No se encontro un rol activo de tipo Inspector en la tabla de roles.";
                return false;
            }

            var actorId = ObtenerActorId();
            var actorCodigo = ObtenerActorCodigoUsuario();
            var ip = Request != null ? Request.UserHostAddress : null;

            var dao = new UsuarioInternoRTDAO();
            var usuarioId = ResolverUsuarioIdAocrDesdeRegistro(registro, dao);
            var cuentaCreada = false;
            var passwordTemporal = string.Empty;
            var correoEnviado = true;
            var detalleCorreo = string.Empty;
            var mensajeOperacion = string.Empty;
            var mensajeRol = string.Empty;

            if (usuarioId.HasValue && usuarioId.Value > 0)
            {
                if (!AsegurarRolInspectorEnUsuario(usuarioId.Value, rolInspectorId.Value, out mensajeRol))
                {
                    mensajeError = string.IsNullOrWhiteSpace(mensajeRol)
                        ? "No se pudo asignar el rol Inspector al usuario."
                        : mensajeRol;
                    return false;
                }

                if (resetearClaveSiExiste)
                {
                    var okReset = AdminUsuariosBL.ResetPassword(
                        usuarioId.Value,
                        true,
                        null,
                        actorId,
                        actorCodigo,
                        ip,
                        correoDestino,
                        out passwordTemporal,
                        out correoEnviado,
                        out detalleCorreo,
                        out mensajeOperacion);

                    if (!okReset)
                    {
                        mensajeError = "No se pudo regenerar la clave temporal del inspector: " + mensajeOperacion;
                        return false;
                    }
                }
                else
                {
                    mensajeOperacion = "La cuenta AOCR ya estaba creada y se validó el rol Inspector.";
                }
            }
            else
            {
                if (!crearCuentaSiNoExiste)
                {
                    mensajeError = "El inspector no tiene cuenta AOCR asociada.";
                    return false;
                }

                string nombres;
                string apellidos;
                var nombreCompleto = string.IsNullOrWhiteSpace(registro.NombreCompleto) ? registro.NombreVisual : registro.NombreCompleto;
                SepararNombreCompleto(nombreCompleto, out nombres, out apellidos);

                var codigoUsuario = ConstruirCodigoUsuarioCuentaInspector(registro);
                if (string.IsNullOrWhiteSpace(codigoUsuario))
                {
                    mensajeError = "No se pudo determinar un codigo de usuario valido para crear la cuenta AOCR del inspector.";
                    return false;
                }

                var dto = new SeguridadUsuarioDTO
                {
                    CodigoUsuario = codigoUsuario,
                    NombreUsuario = string.IsNullOrWhiteSpace(nombres) ? codigoUsuario : nombres.Trim(),
                    ApellidoUsuario = string.IsNullOrWhiteSpace(apellidos) ? string.Empty : apellidos.Trim(),
                    Correo = correoDestino,
                    Activo = true
                };

                int nuevoUsuarioId;
                var okCrear = AdminUsuariosBL.CrearUsuario(
                    dto,
                    new[] { rolInspectorId.Value },
                    null,
                    true,
                    actorId,
                    actorCodigo,
                    ip,
                    out nuevoUsuarioId,
                    out passwordTemporal,
                    out correoEnviado,
                    out detalleCorreo,
                    out mensajeOperacion);

                if (!okCrear)
                {
                    mensajeError = "No se pudo crear la cuenta AOCR del inspector: " + mensajeOperacion;
                    return false;
                }

                usuarioId = nuevoUsuarioId;
                cuentaCreada = true;
            }

            if (usuarioId.HasValue && usuarioId.Value > 0)
            {
                string mensajeVinculo;
                if (!UsuarioInternoRTBL.VincularCuentaAcceso(registro.Id, usuarioId.Value, actorCodigo, out mensajeVinculo))
                {
                    _logger.LogWarning("[AdminUsuariosController] No se pudo vincular usuario interno RT con cuenta AOCR. RegistroId="
                        + registro.Id + ", UsuarioId=" + usuarioId.Value + ", Detalle=" + mensajeVinculo);
                    mensajeWarning = string.IsNullOrWhiteSpace(mensajeWarning)
                        ? "No se pudo actualizar el vínculo interno con la cuenta AOCR. " + mensajeVinculo
                        : (mensajeWarning + " " + mensajeVinculo);
                }
            }

            if (!string.IsNullOrWhiteSpace(mensajeRol))
            {
                mensajeWarning = string.IsNullOrWhiteSpace(mensajeWarning)
                    ? mensajeRol
                    : (mensajeWarning + " " + mensajeRol);
            }

            if (!correoEnviado && !string.IsNullOrWhiteSpace(passwordTemporal))
            {
                var detalle = string.IsNullOrWhiteSpace(detalleCorreo) ? "Revise la configuración SMTP/cola y logs del sistema." : detalleCorreo;
                var warningCorreo = "No se pudo enviar el correo institucional. Detalle: " + detalle
                    + ". Se muestra la clave temporal una sola vez para entrega controlada.";

                mensajeWarning = string.IsNullOrWhiteSpace(mensajeWarning)
                    ? warningCorreo
                    : (mensajeWarning + " " + warningCorreo);
                passwordTemporalFallback = "Clave temporal (mostrar una sola vez): " + passwordTemporal;
            }

            if (cuentaCreada)
            {
                mensajeExito = correoEnviado
                    ? "Cuenta AOCR del inspector creada y credenciales enviadas a " + correoDestino + "."
                    : "Cuenta AOCR del inspector creada correctamente.";
            }
            else if (resetearClaveSiExiste)
            {
                mensajeExito = correoEnviado
                    ? "Clave temporal regenerada y enviada al correo institucional " + correoDestino + "."
                    : "Clave temporal regenerada correctamente para el inspector.";
            }
            else
            {
                mensajeExito = "La cuenta AOCR del inspector ya estaba creada y quedó validada con rol Inspector.";
            }

            if (!string.IsNullOrWhiteSpace(mensajeOperacion))
            {
                mensajeExito = mensajeExito + " " + mensajeOperacion;
            }

            return true;
        }

        private bool AsegurarRolInspectorEnUsuario(int usuarioId, int rolInspectorId, out string mensaje)
        {
            mensaje = string.Empty;

            if (usuarioId <= 0 || rolInspectorId <= 0)
            {
                mensaje = "Datos invalidos para asignar rol Inspector.";
                return false;
            }

            var usuario = AdminUsuariosBL.ObtenerUsuarioPorId(usuarioId);
            if (usuario == null)
            {
                mensaje = "No se encontro la cuenta AOCR asociada al inspector.";
                return false;
            }

            var roles = (usuario.RolesAsignados ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (roles.Contains(rolInspectorId))
            {
                return true;
            }

            roles.Add(rolInspectorId);

            string mensajeRoles;
            var okRoles = AdminUsuariosBL.ReemplazarRolesUsuario(
                usuarioId,
                roles,
                ObtenerActorId(),
                ObtenerActorCodigoUsuario(),
                Request != null ? Request.UserHostAddress : null,
                out mensajeRoles);

            if (!okRoles)
            {
                mensaje = string.IsNullOrWhiteSpace(mensajeRoles)
                    ? "No se pudo asignar el rol Inspector a la cuenta."
                    : mensajeRoles;
                return false;
            }

            mensaje = "Se asigno el rol Inspector a la cuenta existente.";
            return true;
        }

        private int? ResolverUsuarioIdAocrDesdeRegistro(UsuarioInternoRTRegistro registro, UsuarioInternoRTDAO dao = null)
        {
            if (registro == null)
            {
                return null;
            }

            var usuarioId = registro.UsuarioId;
            if (usuarioId.HasValue && usuarioId.Value > 0)
            {
                return usuarioId;
            }

            dao = dao ?? new UsuarioInternoRTDAO();

            if (!string.IsNullOrWhiteSpace(registro.CodigoUsuario))
            {
                usuarioId = dao.ObtenerUsuarioIdPorCodigoUsuario(registro.CodigoUsuario);
            }

            if ((!usuarioId.HasValue || usuarioId.Value <= 0) && !string.IsNullOrWhiteSpace(registro.Identificacion))
            {
                usuarioId = dao.ObtenerUsuarioIdPorCodigoUsuario(registro.Identificacion);
            }

            if ((!usuarioId.HasValue || usuarioId.Value <= 0) && !string.IsNullOrWhiteSpace(registro.CorreoInstitucional))
            {
                usuarioId = dao.ObtenerUsuarioIdPorCorreo(registro.CorreoInstitucional);
            }

            if ((!usuarioId.HasValue || usuarioId.Value <= 0) && registro.TecnicoId.HasValue && registro.TecnicoId.Value > 0)
            {
                usuarioId = dao.ObtenerUsuarioIdPorTecnicoId(registro.TecnicoId.Value);
            }

            return usuarioId;
        }

        private int? ObtenerCodigoRolInspector()
        {
            var roles = AdminUsuariosBL.ObtenerRolesActivos() ?? new List<SeguridadRolDTO>();

            var rolInspector = roles.FirstOrDefault(r =>
                string.Equals((r.Descripcion ?? string.Empty).Trim(), "Inspector", StringComparison.OrdinalIgnoreCase))
                ?? roles.FirstOrDefault(r =>
                    (r.Descripcion ?? string.Empty).IndexOf("Inspector", StringComparison.OrdinalIgnoreCase) >= 0);

            return rolInspector != null ? (int?)rolInspector.CodigoRol : null;
        }

        private static string ConstruirCodigoUsuarioCuentaInspector(UsuarioInternoRTRegistro registro)
        {
            var codigo = NormalizarCodigo(registro != null ? registro.CodigoUsuario : null);
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo;
            }

            codigo = NormalizarCodigo(registro != null ? registro.Identificacion : null);
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                return codigo;
            }

            var correo = registro != null ? (registro.CorreoInstitucional ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(correo))
            {
                return string.Empty;
            }

            var at = correo.IndexOf('@');
            var localPart = at > 0 ? correo.Substring(0, at) : correo;
            return NormalizarCodigo(localPart, 64);
        }

        private static AdminUsuarioInternoRTViewModel MapearUsuarioInternoRTViewModel(UsuarioInternoRTRegistro registro)
        {
            return new AdminUsuarioInternoRTViewModel
            {
                Id = registro.Id,
                CodigoUsuarioBusqueda = registro.Identificacion,
                CodigoUsuario = registro.CodigoUsuario,
                Cedula = registro.Identificacion,
                NombreCompleto = registro.NombreCompleto,
                TipoInspector = registro.Tipo,
                CiudadCodigo = registro.CiudadCodigo,
                CodigoFinanciero = registro.CodigoFinanciero,
                Opcoi3 = registro.Opcoi3,
                Opcar5 = registro.Opcar5,
                RolInterno = registro.RolInterno,
                CorreoInstitucional = registro.CorreoInstitucional,
                Observaciones = registro.Observaciones,
                Activo = registro.Activo
            };
        }

        private bool EnviarNotificacionAltaUsuarioInternoRT(UsuarioInternoRTRegistro registro, string correoDestino, out string mensaje)
        {
            mensaje = string.Empty;
            var asunto = "Registro como Usuario RT / Inspector - Sistema AOCR";
            var cuerpo = ConstruirCorreoAltaUsuarioInternoRT(registro);

            try
            {
                var queueService = new EmailQueueService();
                var configService = new SecureConfigurationService();
                var servicioCorreo = new EnviarCorreo(configService, queueService);

                if (servicioCorreo.EnviarEncolado(correoDestino, asunto, cuerpo, null, "RT_USUARIO_CREADO"))
                {
                    mensaje = "Correo de notificacion enviado correctamente al inspector.";
                    return true;
                }

                if (servicioCorreo.enviaMensajeCorreo(correoDestino, asunto, cuerpo))
                {
                    mensaje = "La cola de correos fallo, pero el correo de notificacion se envio directamente.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AdminUsuariosController] Error enviando correo RT creado: " + ex.Message);
            }

            try
            {
                var servicioCorreo = new EnviarCorreo();
                if (servicioCorreo.enviaMensajeCorreo(correoDestino, asunto, cuerpo))
                {
                    mensaje = "La cola de correos fallo, pero el correo de notificacion se envio directamente.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[AdminUsuariosController] Error en envio directo de correo RT creado: " + ex.Message);
            }

            mensaje = "No se pudo enviar el correo de notificacion para el usuario RT / inspector.";
            return false;
        }

        private static string ConstruirCorreoAltaUsuarioInternoRT(UsuarioInternoRTRegistro registro)
        {
            var nombre = registro != null ? registro.NombreVisual : "Usuario";
            var codigo = registro != null ? registro.UsuarioLogin : string.Empty;
            var rol = registro != null ? (registro.RolInterno ?? string.Empty) : string.Empty;
            var tipo = registro != null ? (registro.Tipo ?? string.Empty) : string.Empty;
            var fecha = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            var extraHtml = "<p style='margin:0 0 8px 0; font-size:14px; color:#3a4f5e;'><strong>Usuario / Cédula:</strong> "
                + System.Net.WebUtility.HtmlEncode(codigo) + "</p>"
                + "<p style='margin:0 0 8px 0; font-size:14px; color:#3a4f5e;'><strong>Tipo:</strong> "
                + System.Net.WebUtility.HtmlEncode(tipo) + "</p>"
                + "<p style='margin:0 0 8px 0; font-size:14px; color:#3a4f5e;'><strong>Rol interno:</strong> "
                + System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(rol) ? "Inspector" : rol) + "</p>"
                + "<p style='margin:0 0 8px 0; font-size:13px; color:#3a4f5e;'>Fecha de notificación: " + System.Net.WebUtility.HtmlEncode(fecha) + "</p>";

            var model = new EmailTemplateModel
            {
                Titulo = "Registro como Usuario RT / Inspector",
                NombreDestinatario = System.Net.WebUtility.HtmlEncode(nombre),
                MensajePrincipal = "Se confirma su registro en el sistema AOCR como usuario interno RT / inspector.",
                ContenidoHtmlExtra = extraHtml,
                TextoCierre = "Si requiere credenciales de acceso o restablecimiento de contraseña, contacte al administrador del sistema.",
                Footer = "Mensaje automático del sistema AOCR."
            };

            return EmailTemplateRenderer.Render(model);
        }

        private static void CargarRolesUsuarioInterno(AdminUsuarioInternoRTViewModel model, string seleccionado)
        {
            if (model == null)
            {
                return;
            }

            var selectedRole = (seleccionado ?? string.Empty).Trim();
            var roles = new[]
            {
                "",
                "Inspector",
                "Coordinador",
                "RT",
                "DIRDAC",
                "Financiero"
            };

            model.RolesInternos = roles.Select(r => new SelectListItem
            {
                Value = r,
                Text = string.IsNullOrWhiteSpace(r) ? "-- Seleccione --" : r,
                Selected = string.Equals(r, selectedRole, StringComparison.OrdinalIgnoreCase)
            }).ToList();
        }
    }
}

