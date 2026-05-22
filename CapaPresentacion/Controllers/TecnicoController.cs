using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Services;
using CapaDatos.DAOs;
using CapaPresentacion.Infrastructure;
using DataSecureConfigurationService = CapaDatos.Services.SecureConfigurationService;

namespace CapaPresentacion.Controllers
{
    [Authorize] // No restringas aquí para no bloquear otras acciones por rol
    public class TecnicoController : Controller
    {
        private const string CatalogoInspectoresLabel = "Catalogo institucional de inspectores activos";

        private readonly CapaNegocio.Services.ILoggingService _logger;
        private readonly SolicitudAocrCorreoService _solicitudAocrCorreoService;
        private static readonly IUserContextAccessor _userContext = new UserContextAccessor();

        public TecnicoController()
        {
            _logger = CapaNegocio.Services.LoggingServiceFactory.Create();
            _solicitudAocrCorreoService = new SolicitudAocrCorreoService();
        }

        // =======================================================
        // BANDEJA STANDALONE - Asignación de inspectores
        // =======================================================
        [Authorize(Roles = "Administrador,Coordinador,CoordinadorInspecciones")]
        public ActionResult Index()
        {
            _logger.LogInfo("[InspeccionesController] Inicio pantalla gestion (Tecnico/Index). Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual());

            var pendientes = SolicitudAOCRBL.ObtenerPendientesAsignacion() ?? new List<SolicitudAOCR>();
            ViewBag.TotalPendientes = pendientes.Count;

            _logger.LogInfo("[InspeccionesController] Bandeja standalone de asignacion cargada. Pendientes=" + pendientes.Count);

            return View("Index", pendientes);
        }

        // =======================================================
        // ASIGNAR INSPECTOR (GET)
        // =======================================================
        [HttpGet]
        [Authorize(Roles = "Administrador,Coordinador,CoordinadorInspecciones")]
        public ActionResult AsignarInspector(int? solicitudId, string tipoInspector = "OPS")
        {
            _logger.LogInfo("[InspeccionesController] Inicio pantalla gestion de asignacion. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", SolicitudId=" + (solicitudId.HasValue ? solicitudId.Value.ToString() : "null"));

            if (!solicitudId.HasValue || solicitudId.Value <= 0)
            {
                _logger.LogWarning("[InspeccionesController] Bloqueo funcional: solicitudId invalido.");
                TempData["Info"] = "Seleccione una solicitud pendiente para asignar inspector.";
                return RedirigirABandejaAsignacion();
            }

            var solicitud = SolicitudAOCRBL.ObtenerPorId(solicitudId.Value);
            if (solicitud == null)
            {
                _logger.LogWarning("[InspeccionesController] Bloqueo funcional: solicitud no encontrada. SolicitudId=" + solicitudId.Value);
                TempData["Error"] = "Solicitud no encontrada.";
                return RedirigirABandejaAsignacion();
            }

            _logger.LogInfo("[InspeccionesController] SolicitudId=" + solicitud.CodigoSolicitud + ", EstadoActual=" + (solicitud.Estado ?? "(null)") + ", NumeroSolicitud=" + (solicitud.NumeroSolicitud ?? ""));

            var esReasignacion = TieneInspectorAsignado(solicitud);

            var tipoInspectorNormalizado = NormalizarTipoInspector(tipoInspector);
            var inspectores = UsuarioInternoRTBL.ListarInspectoresAsignables(tipoInspectorNormalizado) ?? new List<CapaDatos.Models.UsuarioInternoRTRegistro>();
            var origenInspectores = CatalogoInspectoresLabel;

            _logger.LogInfo("[InspeccionesController] Origen inspectores=" + origenInspectores + ", TipoFiltro=" + tipoInspectorNormalizado + ", InspectoresRecibidos=" + inspectores.Count);

            if (inspectores.Count == 0)
            {
                _logger.LogWarning("[InspeccionesController] Lista de inspectores vacia para SolicitudId=" + solicitud.CodigoSolicitud + ".");
                ViewBag.WarningInspectores = "No se encontraron inspectores activos con rol Inspector para el filtro seleccionado.";
            }

            ViewBag.TipoInspector = tipoInspectorNormalizado;
            ViewBag.TiposInspector = new SelectList(
                new List<SelectListItem>
                {
                    new SelectListItem { Value = "OPS", Text = "Operaciones (OPS)" },
                    new SelectListItem { Value = "AIR", Text = "Aeronavegabilidad (AIR)" },
                    new SelectListItem { Value = "TODOS", Text = "Todos (OPS + AIR)" }
                },
                "Value",
                "Text",
                tipoInspectorNormalizado);
            ViewBag.Inspectores = new SelectList(
                inspectores.Select(i => new
                {
                    Cedula = i.UsuarioLogin,
                    Etiqueta = ConstruirEtiquetaInspectorRt(i)
                }),
                "Cedula",
                "Etiqueta",
                solicitud.TecnicoResponsableCedula);
            ViewBag.InspectoresApoyo = new SelectList(
                inspectores.Select(i => new
                {
                    Cedula = i.UsuarioLogin,
                    Etiqueta = ConstruirEtiquetaInspectorRt(i)
                }),
                "Cedula",
                "Etiqueta",
                solicitud.InspectorApoyoCedula);
            ViewBag.EsReasignacion = esReasignacion;

            _logger.LogInfo("[InspeccionesController] ViewModel cargado correctamente. SolicitudId=" + solicitud.CodigoSolicitud + ", ViewBagInspectores=" + inspectores.Count);

            return View("AsignarInspector", solicitud);
        }

        // =======================================================
        // ASIGNAR INSPECTOR (POST)
        // =======================================================
        [HttpPost]
        [Authorize(Roles = "Administrador,Coordinador,CoordinadorInspecciones")]
        [ValidateAntiForgeryToken]
        public ActionResult AsignarInspector(
            int solicitudId,
            string inspectorPrincipal,
            string inspectorApoyo,
            DateTime fechaInspeccion,
            string horaInspeccion,
            string observaciones,
            string tipoInspector = "OPS")
        {
            _logger.LogInfo("[GestionInspeccion] Inicio. SolicitudId=" + solicitudId + ", Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", TipoInspector=" + (tipoInspector ?? "") + ", InspectorPrincipal=" + (inspectorPrincipal ?? "") + ", InspectorApoyo=" + (inspectorApoyo ?? ""));

            if (solicitudId <= 0)
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=SolicitudId invalido");
                TempData["Error"] = "Solicitud inválida.";
                return RedirigirABandejaAsignacion();
            }

            if (string.IsNullOrWhiteSpace(inspectorPrincipal))
            {
                _logger.LogWarning("[GestionInspeccion] PuedeGestionar=False, Motivo=No existe inspector asignado en request");
                TempData["Error"] = "Debe seleccionar un inspector principal activo.";
                return RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }

            if (!string.IsNullOrWhiteSpace(inspectorApoyo)
                && string.Equals(inspectorPrincipal.Trim(), inspectorApoyo.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "El inspector principal y el inspector de apoyo no pueden ser el mismo.";
                return RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }

            try
            {
                var solicitud = SolicitudAOCRBL.ObtenerPorId(solicitudId);
                var esReasignacion = TieneInspectorAsignado(solicitud);
                _logger.LogInfo("[GestionInspeccion] EstadoActual=" + (solicitud == null ? "(solicitud-null)" : (solicitud.Estado ?? "(null)")));

                var tipoInspectorNormalizado = NormalizarTipoInspector(tipoInspector);
                var inspectorPrincipalRegistro = UsuarioInternoRTBL.ObtenerInspectorAsignable(inspectorPrincipal, tipoInspectorNormalizado);
                if (inspectorPrincipalRegistro == null)
                {
                    TempData["Error"] = "El inspector principal seleccionado ya no está activo o no pertenece al catálogo de inspectores.";
                    return RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
                }

                CapaDatos.Models.UsuarioInternoRTRegistro inspectorApoyoRegistro = null;
                if (!string.IsNullOrWhiteSpace(inspectorApoyo))
                {
                    inspectorApoyoRegistro = UsuarioInternoRTBL.ObtenerInspectorAsignable(inspectorApoyo, tipoInspectorNormalizado);
                    if (inspectorApoyoRegistro == null)
                    {
                        TempData["Error"] = "El inspector de apoyo seleccionado ya no está activo o no pertenece al catálogo de inspectores.";
                        return RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
                    }
                }

                TimeSpan horaRevision;
                if (!TimeSpan.TryParse(horaInspeccion, out horaRevision))
                {
                    horaRevision = new TimeSpan(9, 0, 0);
                }

                var fechaHoraInspeccion = fechaInspeccion.Date.Add(horaRevision);

                _logger.LogInfo("[GestionInspeccion] Inspectores validados desde catalogo institucional. Principal=" + inspectorPrincipalRegistro.UsuarioLogin + ", Apoyo=" + (inspectorApoyoRegistro != null ? inspectorApoyoRegistro.UsuarioLogin : string.Empty));

                string mensaje;
                bool ok = SolicitudAOCRBL.AsignarInspectores(
                    solicitudId,
                    inspectorPrincipalRegistro.UsuarioLogin,
                    inspectorApoyoRegistro != null ? inspectorApoyoRegistro.UsuarioLogin : null,
                    fechaHoraInspeccion,
                    observaciones,
                    tipoInspectorNormalizado,
                    ObtenerUsuarioActual(),
                    out mensaje
                );

                _logger.LogInfo("[GestionInspeccion] PuedeGestionar=" + ok + ", Motivo=" + (mensaje ?? "(sin mensaje)"));

                if (ok)
                {
                    var solicitudActualizada = SolicitudAOCRBL.ObtenerPorId(solicitudId) ?? solicitud;
                    var nombreTecnico = FirstNonEmpty(
                        solicitudActualizada != null ? solicitudActualizada.TecnicoResponsableNombre : null,
                        inspectorPrincipalRegistro.NombreVisual,
                        inspectorPrincipalRegistro.UsuarioLogin);
                    var nombreOperador = solicitudActualizada != null
                        ? FirstNonEmpty(solicitudActualizada.NombreOperador, solicitudActualizada.RazonSocial, "No disponible")
                        : "No disponible";
                    var detalleNotificacion = string.Format(
                        "Inspector principal asignado: {0}. Inspector de apoyo: {1}. Fecha programada: {2:dd/MM/yyyy HH:mm}. Operador/compania: {3}. Asignado por: {4}.{5}",
                        nombreTecnico,
                        inspectorApoyoRegistro != null ? inspectorApoyoRegistro.NombreVisual : "No aplica",
                        fechaHoraInspeccion,
                        nombreOperador,
                        ObtenerUsuarioActual(),
                        string.IsNullOrWhiteSpace(observaciones) ? string.Empty : " Observacion: " + observaciones.Trim());
                    var resultadoNotificacionInterna = _solicitudAocrCorreoService.NotificarEvento(
                        solicitudActualizada,
                        "INSPECTOR_ASIGNADO",
                        detalleNotificacion);

                    TempData["Success"] = (mensaje ?? (esReasignacion ? "Reasignación realizada correctamente." : "Asignación realizada correctamente."));

                    if (!resultadoNotificacionInterna.Exitoso)
                    {
                        var warningActual = TempData["Warning"] as string;
                        TempData["Warning"] = string.IsNullOrWhiteSpace(warningActual)
                            ? resultadoNotificacionInterna.Mensaje
                            : warningActual + " " + resultadoNotificacionInterna.Mensaje;
                    }
                }
                else
                {
                    TempData["Error"] = mensaje;
                }

                return ok
                    ? RedirigirABandejaAsignacion()
                    : RedirectToAction("AsignarInspector", new { solicitudId, tipoInspector });
            }
            catch (Exception ex)
            {
                _logger.LogError("[GestionInspeccion] Error no controlado en asignacion: " + ex);
                TempData["Error"] = "Error crítico: " + ex.Message;
                return RedirigirABandejaAsignacion();
            }
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Coordinador,CoordinadorInspecciones")]
        public JsonResult ListarInspectoresActivos(string tipoInspector = "OPS")
        {
            _logger.LogInfo("[InspeccionesController] Inicio endpoint AJAX inspectores. Usuario=" + ObtenerUsuarioActual() + ", Rol=" + ObtenerRolActual() + ", TipoInspector=" + (tipoInspector ?? ""));

            var tipoNormalizado = NormalizarTipoInspector(tipoInspector);
            var data = UsuarioInternoRTBL.ListarInspectoresAsignables(tipoNormalizado) ?? new List<CapaDatos.Models.UsuarioInternoRTRegistro>();

            var payload = data
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.UsuarioLogin))
                .Select(x => new
                {
                    cedula = x.UsuarioLogin,
                    nombre = x.NombreVisual,
                    tipo = x.Tipo,
                    etiqueta = ConstruirEtiquetaInspectorRt(x),
                    correo = x.CorreoInstitucional,
                    rol = x.RolInterno
                })
                .ToList();

            _logger.LogInfo("[InspeccionesController] Endpoint AJAX inspectores OK. Origen=" + CatalogoInspectoresLabel + ", Tipo=" + tipoNormalizado + ", Cantidad=" + payload.Count);

            return Json(new { success = true, tipo = tipoNormalizado, origen = CatalogoInspectoresLabel, items = payload }, JsonRequestBehavior.AllowGet);
        }

        private RedirectToRouteResult RedirigirABandejaAsignacion()
        {
            return RedirectToAction("Index", "Tecnico");
        }

        private static string NormalizarTipoInspector(string tipoInspector)
        {
            if (string.IsNullOrWhiteSpace(tipoInspector))
            {
                return "OPS";
            }

            var value = tipoInspector.Trim().ToUpperInvariant();
            if (value == "OPS" || value == "AIR" || value == "TODOS")
            {
                return value;
            }

            return "OPS";
        }

        private string ObtenerUsuarioActual()
        {
            var usuarioSesion = Session != null ? Session["Usuario"] as string : null;
            if (!string.IsNullOrWhiteSpace(usuarioSesion))
            {
                return usuarioSesion.Trim();
            }

            return _userContext.GetNombreUsuario(Session, User);
        }

        private string ObtenerRolActual()
        {
            var roles = new[] { "Administrador", "Coordinador", "CoordinadorInspecciones" }
                .Where(r => User != null && User.IsInRole(r))
                .ToList();

            return roles.Count == 0 ? "SIN_ROL_DETECTADO" : string.Join(",", roles);
        }

        private static string ConstruirEtiquetaInspectorRt(CapaDatos.Models.UsuarioInternoRTRegistro inspector)
        {
            if (inspector == null)
            {
                return string.Empty;
            }

            var nombre = FirstNonEmpty(inspector.NombreVisual, inspector.UsuarioLogin, "Inspector");
            var tipo = string.IsNullOrWhiteSpace(inspector.Tipo) ? string.Empty : " [" + inspector.Tipo.Trim().ToUpperInvariant() + "]";
            var correo = string.IsNullOrWhiteSpace(inspector.CorreoInstitucional) ? string.Empty : " - " + inspector.CorreoInstitucional.Trim();
            return nombre + tipo + correo;
        }

        private static bool TieneInspectorAsignado(SolicitudAOCR solicitud)
        {
            if (solicitud == null)
            {
                return false;
            }

            return solicitud.CodigoTecnico.HasValue && solicitud.CodigoTecnico.Value > 0
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableCedula)
                || !string.IsNullOrWhiteSpace(solicitud.TecnicoResponsableNombre);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return null;
            }

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

    }
}
