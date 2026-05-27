using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaPresentacion.Helpers;
using CapaPresentacion.Infrastructure;
using CapaPresentacion.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao;
        private readonly InspectorDashboardService _inspectorDashboardService;
        private readonly UsuarioInternoRTDAO _usuarioInternoRtDao;
        private readonly IUserContextAccessor _userContext;

        public DashboardController()
        {
            _dao = new OrdenRecaudacionDAO();
            _inspectorDashboardService = new InspectorDashboardService();
            _usuarioInternoRtDao = new UsuarioInternoRTDAO();
            _userContext = new UserContextAccessor();
        }

        // GET: Dashboard
        public ActionResult Index()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return RedirectToAction("Login", "Account");

                // Mantener /Dashboard como alias del hub institucional nuevo.
                return RedirectToAction("Index", "Home");
            }
            catch
            {
                return RedirectToAction("Login", "Account");
            }
        }

        // GET: Dashboard/ObtenerDatosDashboard
        [HttpGet]
        public JsonResult ObtenerDatosDashboard()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                var esAdministrador = User != null && User.IsInRole("Administrador");

                // ✅ USAR LISTA (NO DataTable)
                var ordenes = esAdministrador
                    ? (_dao.ObtenerTodasLasOrdenes(null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>())
                    : (_dao.ListarPorUsuario(idUsuario, null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>());

                int ordenesPendientes = 0;
                int ordenesCompletadas = 0;
                decimal totalRecaudado = 0m;
                string ultimaOrden = "N/A";

                foreach (var o in ordenes)
                {
                    var estado = (o.Estado ?? "").Trim().ToUpperInvariant();

                    if (estado == "BORRADOR" || estado == "GENERADA" || estado == "ENVIADA")
                        ordenesPendientes++;
                    else if (estado == "PAGADA" || estado == "COMPLETADA")
                        ordenesCompletadas++;

                    totalRecaudado += o.Total ?? 0m;
                }

                var ultima = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .FirstOrDefault();

                if (ultima != null && !string.IsNullOrWhiteSpace(ultima.NumeroOrden))
                    ultimaOrden = ultima.NumeroOrden;

                var ordenesRecientes = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .Take(10)
                    .Select(x => new
                    {
                        id = x.Id,
                        numeroOrden = x.NumeroOrden,
                        fechaCreacion = x.FechaCreacion,
                        estado = x.Estado,
                        total = x.Total ?? 0m
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    ordenesPendientes,
                    ordenesCompletadas,
                    totalRecaudado,
                    ultimaOrden,
                    ordenesRecientes
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        [Authorize(Roles = "Inspector,Administrador,CoordinadorInspecciones,JefaturaTecnica")]
        public ActionResult Inspector(DateTime? fechaDesde = null, DateTime? fechaHasta = null, string estado = null, string compania = null, int? codigoSolicitud = null)
        {
            try
            {
                var puedeVerGlobal = User != null && User.IsInRole("Administrador");
                var codigosInspector = ObtenerCodigosInspector().Where(id => id > 0).ToList();
                if (!puedeVerGlobal && codigosInspector.Count == 0)
                {
                    TempData["Error"] = "No se pudo identificar el inspector en sesión.";
                    return RedirectToAction("Index", "Inspeccion");
                }

                Trace.TraceInformation(
                    "[DashboardInspector] Usuario={0}; Rol={1}; UserIdSesion={2}; CodigoUsuarioSesion={3}; IdsFiltro={4}",
                    _userContext.GetNombreUsuario(Session, User),
                    RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"] as string),
                    ObtenerIdUsuario(),
                    ObtenerCodigoUsuarioSesion(),
                    codigosInspector.Count == 0 ? "GLOBAL" : string.Join(",", codigosInspector));

                Trace.TraceInformation(
                    "[DashboardInspector] Filtros: Desde={0}; Hasta={1}; Estado={2}; Compania={3}; Solicitud={4}",
                    fechaDesde.HasValue ? fechaDesde.Value.ToString("yyyy-MM-dd") : "null",
                    fechaHasta.HasValue ? fechaHasta.Value.ToString("yyyy-MM-dd") : "null",
                    string.IsNullOrWhiteSpace(estado) ? "null" : estado.Trim(),
                    string.IsNullOrWhiteSpace(compania) ? "null" : compania.Trim(),
                    codigoSolicitud.HasValue ? codigoSolicitud.Value.ToString() : "null");

                var vm = _inspectorDashboardService.ObtenerDashboard(
                    codigosInspector,
                    fechaDesde,
                    fechaHasta,
                    estado,
                    compania,
                    codigoSolicitud,
                    puedeVerGlobal);

                vm.NombreInspector = _userContext.GetNombreUsuario(Session, User);
                vm.RolActual = RoleGroupingHelper.NormalizeSelectedRole(Session["Rol"] as string);
                vm.PuedeVerGlobal = puedeVerGlobal;

                Trace.TraceInformation(
                    "[DashboardInspector] TotalAsignadas={0}; Pendientes={1}; EnEjecucion={2}; Cerradas={3}; ConNC={4}; DocsPendientes={5}; Alertas={6}; Ultimas={7}",
                    vm.InspeccionesAsignadas,
                    vm.InspeccionesPendientes,
                    vm.InspeccionesEnEjecucion,
                    vm.InspeccionesCerradas,
                    vm.InspeccionesConNc,
                    vm.DocumentosPendientesRevision,
                    vm.AlertasUrgentes.Count,
                    vm.UltimasInspecciones.Count);

                return View("Inspector", vm);
            }
            catch (Exception ex)
            {
                Trace.TraceError("[DashboardInspector] Error cargando dashboard inspector. Detalle={0}", ex);
                TempData["Error"] = "No fue posible cargar el dashboard del inspector en este momento.";
                return RedirectToAction("Index", "Inspeccion");
            }
        }

        [HttpGet]
        [Authorize(Roles = "Inspector,Administrador,CoordinadorInspecciones,JefaturaTecnica")]
        public JsonResult ObtenerDatosInspectorDashboard(DateTime? fechaDesde = null, DateTime? fechaHasta = null, string estado = null, string compania = null, int? codigoSolicitud = null)
        {
            try
            {
                var puedeVerGlobal = User != null && User.IsInRole("Administrador");
                var codigosInspector = ObtenerCodigosInspector().Where(id => id > 0).ToList();
                if (!puedeVerGlobal && codigosInspector.Count == 0)
                {
                    return Json(new { success = false, message = "No se identificó inspector en sesión." }, JsonRequestBehavior.AllowGet);
                }

                var vm = _inspectorDashboardService.ObtenerDashboard(codigosInspector, fechaDesde, fechaHasta, estado, compania, codigoSolicitud, puedeVerGlobal);
                return Json(new
                {
                    success = true,
                    metricas = new
                    {
                        vm.InspeccionesAsignadas,
                        vm.InspeccionesPendientes,
                        vm.InspeccionesEnEjecucion,
                        vm.InspeccionesConNc,
                        vm.InspeccionesCerradas,
                        vm.InspeccionesRequierenNueva,
                        vm.DocumentosPendientesRevision,
                        vm.DocumentacionSubsanadaRt,
                        vm.InformesTecnicosPendientes,
                        vm.TiempoPromedioAtencionHoras
                    },
                    tendencia = new
                    {
                        etiquetas = vm.TendenciaAtencionEtiquetas,
                        valores = vm.TendenciaAtencionValores
                    },
                    ultimasInspecciones = vm.UltimasInspecciones,
                    alertasUrgentes = vm.AlertasUrgentes
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        private int ObtenerIdUsuario()
        {
            int idUsuario;
            return _userContext.TryGetUserId(Session, out idUsuario) ? idUsuario : 0;
        }

        private string ObtenerCodigoUsuarioSesion()
        {
            var codigoUsuario = Session != null ? Session["CodigoUsuario"] as string : null;
            if (!string.IsNullOrWhiteSpace(codigoUsuario))
            {
                return codigoUsuario.Trim();
            }

            if (User != null && User.Identity != null && User.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(User.Identity.Name))
            {
                return User.Identity.Name.Trim();
            }

            return string.Empty;
        }

        private HashSet<int> ObtenerCodigosInspector()
        {
            var ids = new HashSet<int>();
            var codigoUsuarioTexto = ObtenerCodigoUsuarioSesion();
            var codigoUsuarioNumerico = 0;
            var idUsuario = ObtenerIdUsuario();

            if (idUsuario > 0)
            {
                ids.Add(idUsuario);
            }

            if (_userContext.TryGetCodigoUsuario(Session, out codigoUsuarioNumerico) && codigoUsuarioNumerico > 0)
            {
                ids.Add(codigoUsuarioNumerico);
            }

            if (User == null || !User.IsInRole("Inspector"))
            {
                return ids;
            }

            try
            {
                UsuarioInternoRTRegistro inspectorActual = null;

                if (idUsuario > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(idUsuario);
                }

                if (inspectorActual == null && !string.IsNullOrWhiteSpace(codigoUsuarioTexto))
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerActivoPorCodigoUsuario(codigoUsuarioTexto)
                        ?? _usuarioInternoRtDao.ObtenerInspectorAsignableActivo(codigoUsuarioTexto);
                }

                if (inspectorActual == null && codigoUsuarioNumerico > 0)
                {
                    inspectorActual = _usuarioInternoRtDao.ObtenerInspectorActivoPorTecnicoIdOUsuarioId(codigoUsuarioNumerico);
                }

                if (inspectorActual != null)
                {
                    if (inspectorActual.TecnicoId.HasValue && inspectorActual.TecnicoId.Value > 0)
                    {
                        ids.Add(inspectorActual.TecnicoId.Value);
                    }

                    if (inspectorActual.UsuarioId.HasValue && inspectorActual.UsuarioId.Value > 0)
                    {
                        ids.Add(inspectorActual.UsuarioId.Value);
                    }
                }
            }
            catch
            {
            }

            return ids;
        }
    }
}
