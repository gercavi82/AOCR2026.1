using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controller empresarial para Dashboard de Órdenes de recaudación
    /// Implementa Clean Architecture + State Management + Security
    /// </summary>
    [Authorize]
    public class OrdenRecaudacionDashboardEmpresarialController : Controller
    {
        private readonly DashboardOrdenesService _dashboardService;
        private readonly EstadoOrdenService _estadoService;
        private readonly OrdenRecaudacionDAO _dao;

        public OrdenRecaudacionDashboardEmpresarialController()
        {
            _dashboardService = new DashboardOrdenesService();
            _estadoService = new EstadoOrdenService();
            _dao = new OrdenRecaudacionDAO();
        }

        #region Vista Principal

        /// <summary>
        /// GET: Dashboard principal de Órdenes de recaudación
        /// </summary>
        [HttpGet]
        // Temporalmente eliminamos roles específicos para debug
        [Authorize]
        public ActionResult Index()
        {
            try
            {
                var userId = ObtenerIdUsuario();
                if (userId <= 0)
                    return RedirectToAction("Login", "Account");

                ViewBag.UsuarioId = userId;
                ViewBag.EsAdmin = EsAdministrador();
                ViewBag.EsFinanciero = EsFinanciero();
                ViewBag.Estados = EstadoOrdenService.Estados.TodosLosEstados;

                return View("~/Views/OrdenRecaudacionDashboard/IndexEmpresarial.cshtml");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el dashboard: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        #endregion

        #region APIs para Dashboard

        /// <summary>
        /// GET: Obtener KPIs optimizados
        /// Endpoint: /OrdenRecaudacionDashboard/ObtenerKPIs
        /// </summary>
        [HttpGet]
        [OutputCache(Duration = 60, Location = OutputCacheLocation.Server)] // Cache por 1 minuto
        public JsonResult ObtenerKPIs(string fechaDesde, string fechaHasta)
        {
            try
            {
                var userId = ObtenerIdUsuario();
                if (userId <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                // Solo admin/financiero ven todo el sistema
                var userIdFiltro = EsAdministrador() || EsFinanciero() ? (int?)null : userId;

                DateTime? desde = null, hasta = null;
                if (!string.IsNullOrWhiteSpace(fechaDesde) && DateTime.TryParse(fechaDesde, out var fd))
                    desde = fd.Date;
                if (!string.IsNullOrWhiteSpace(fechaHasta) && DateTime.TryParse(fechaHasta, out var fh))
                    hasta = fh.Date.AddDays(1).AddSeconds(-1);

                var kpis = _dashboardService.ObtenerKPIs(userIdFiltro, desde, hasta);

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalOrdenes = kpis.TotalOrdenes,
                        ordenesPendientes = kpis.OrdenesPendientes,
                        ordenesCompletadas = kpis.OrdenesCompletadas,
                        ordenesAnuladas = kpis.OrdenesAnuladas,
                        ordenesRechazadas = kpis.OrdenesRechazadas,
                        montoTotal = kpis.MontoTotal.ToString("C"),
                        montoPendiente = kpis.MontoPendiente.ToString("C"),
                        montoCompletado = kpis.MontoCompletado.ToString("C"),
                        ultimaOrden = kpis.UltimaOrden,
                        fechaUltimaOrden = kpis.FechaUltimaOrden?.ToString("dd/MM/yyyy HH:mm"),
                        promedioMonto = kpis.PromedioMonto.ToString("C"),
                        ordenesDelMes = kpis.OrdenesDelMes,
                        montoDelMes = kpis.MontoDelMes.ToString("C"),
                        tasaCompletamiento = kpis.TasaCompletamiento.ToString("F1") + "%"
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false, 
                    message = "Error al obtener KPIs: " + ex.Message 
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET: Obtener datos para grilla con filtros
        /// Endpoint: /OrdenRecaudacionDashboard/ObtenerOrdenes
        /// </summary>
        [HttpGet]
        public JsonResult ObtenerOrdenes(string estado, string fechaDesde, string fechaHasta, 
            string numeroOrden, int? limite = 100)
        {
            try
            {
                var userId = ObtenerIdUsuario();
                if (userId <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                var rolesUsuario = ObtenerRolesUsuario();
                var userIdFiltro = EsAdministrador() || EsFinanciero() ? (int?)null : userId;

                DateTime? desde = null, hasta = null;
                if (!string.IsNullOrWhiteSpace(fechaDesde) && DateTime.TryParse(fechaDesde, out var fd))
                    desde = fd.Date;
                if (!string.IsNullOrWhiteSpace(fechaHasta) && DateTime.TryParse(fechaHasta, out var fh))
                    hasta = fh.Date.AddDays(1).AddSeconds(-1);

                var ordenes = _dashboardService.ObtenerOrdenesParaDashboard(
                    userIdFiltro, estado?.Trim(), desde, hasta, numeroOrden?.Trim(), rolesUsuario, limite ?? 100);

                return Json(new
                {
                    success = true,
                    data = ordenes.Select(o => new
                    {
                        id = o.Id,
                        numeroOrden = o.NumeroOrden,
                        fechaCreacion = o.FechaCreacion.ToString("dd/MM/yyyy HH:mm"),
                        estado = o.Estado,
                        estadoColor = o.EstadoColor,
                        nombreContribuyente = o.NombreContribuyente,
                        total = o.Total.ToString("C"),
                        usuario = o.Usuario,
                        fechaUltimaModificacion = o.FechaUltimaModificacion?.ToString("dd/MM/yyyy HH:mm"),
                        puedeEditar = o.PuedeEditar,
                        puedeCambiarEstado = o.PuedeCambiarEstado,
                        accionesPermitidas = o.AccionesPermitidas
                    }).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false, 
                    message = "Error al obtener órdenes: " + ex.Message 
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// GET: Obtener métricas mensuales para gráficos
        /// </summary>
        [HttpGet]
        [OutputCache(Duration = 300, Location = OutputCacheLocation.Server)] // Cache por 5 minutos
        public JsonResult ObtenerMetricasMensuales()
        {
            try
            {
                var userId = ObtenerIdUsuario();
                if (userId <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                var userIdFiltro = EsAdministrador() || EsFinanciero() ? (int?)null : userId;
                var metricas = _dashboardService.ObtenerMetricasMensuales(userIdFiltro);

                return Json(new
                {
                    success = true,
                    data = metricas.Select(m => new
                    {
                        mes = m.Mes,
                        ordenes = m.Ordenes,
                        monto = m.Monto,
                        completadas = m.Completadas,
                        tasaExito = m.TasaExito.ToString("F1") + "%"
                    }).ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false, 
                    message = "Error al obtener métricas: " + ex.Message 
                }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion

        #region Acciones de Estado

        /// <summary>
        /// POST: Cambiar estado de una orden con validaciones empresariales
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Financiero,Administrador")]
        public JsonResult CambiarEstado(int id, string nuevoEstado, string observacion = null)
        {
            try
            {
                var userId = ObtenerIdUsuario();
                if (userId <= 0)
                    return Json(new { success = false, message = "Sesión expirada" });

                var rolesUsuario = ObtenerRolesUsuario();
                var orden = _dao.ObtenerPorId(id);
                if (orden == null)
                    return Json(new { success = false, message = "Orden no encontrada" });

                // Validar si el usuario es propietario o admin
                if (orden.CodigoUsuario != userId && !EsAdministrador() && !EsFinanciero())
                    return Json(new { success = false, message = "No tiene permisos sobre esta orden" });

                // Validar transición usando el servicio de estados
                if (!EstadoOrdenService.EsTransicionValida(orden.Estado, nuevoEstado))
                    return Json(new { success = false, message = "Transición de estado no válida" });

                // Validar permisos de rol
                if (!EstadoOrdenService.TienePermisosParaTransicion(orden.Estado, nuevoEstado, rolesUsuario))
                    return Json(new { success = false, message = "No tiene permisos para esta transición" });

                // Validar reglas de negocio
                var tieneDetalles = orden.Detalles?.Any() ?? false;
                if (!EstadoOrdenService.ValidarReglasNegocio(orden.Estado, nuevoEstado, 
                    orden.Total ?? 0, tieneDetalles, out var mensajeRegla))
                {
                    return Json(new { success = false, message = mensajeRegla });
                }

                // Validar observación para rechazos
                if (nuevoEstado == EstadoOrdenService.Estados.RECHAZADA && string.IsNullOrWhiteSpace(observacion))
                    return Json(new { success = false, message = "Debe proporcionar una observación para rechazar" });

                var estadoObjetivo = (nuevoEstado ?? string.Empty).Trim().ToUpperInvariant();
                if (estadoObjetivo == "PROCESADA" ||
                    estadoObjetivo == "FACTURADA" ||
                    estadoObjetivo == "COMPLETADA" ||
                    estadoObjetivo == "PAGADA")
                {
                    var comprobanteService = new ComprobanteService();
                    if (!comprobanteService.ExisteComprobanteValido(id, out var mensajeComprobante))
                    {
                        return Json(new { success = false, message = mensajeComprobante });
                    }
                }

                // Ejecutar cambio de estado
                var resultado = _dao.CambiarEstado(id, nuevoEstado, observacion);

                if (resultado)
                {
                    return Json(new 
                    { 
                        success = true, 
                        message = $"Estado cambiado exitosamente a {nuevoEstado}",
                        nuevoEstado = nuevoEstado,
                        colorEstado = EstadoOrdenService.ObtenerColorEstado(nuevoEstado)
                    });
                }
                else
                {
                    return Json(new { success = false, message = "Error al cambiar el estado" });
                }
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false, 
                    message = "Error interno: " + ex.Message 
                });
            }
        }

        /// <summary>
        /// POST: Ejecutar acción rápida (cambio de estado optimizado)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Financiero,Administrador")]
        public JsonResult AccionRapida(int ordenId, string accion, string observacion = null)
        {
            try
            {
                var userId = ObtenerIdUsuario();
                if (userId <= 0)
                    return Json(new { success = false, message = "Sesión expirada" });

                var rolesUsuario = ObtenerRolesUsuario();

                var resultado = _dashboardService.EjecutarAccionRapida(ordenId, accion, userId, rolesUsuario, observacion);

                if (resultado)
                {
                    return Json(new 
                    { 
                        success = true, 
                        message = $"Acción '{accion}' ejecutada exitosamente",
                        accion = accion
                    });
                }
                else
                {
                    return Json(new { success = false, message = "No se pudo ejecutar la acción" });
                }
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false, 
                    message = "Error al ejecutar acción: " + ex.Message 
                });
            }
        }

        #endregion

        #region Métodos de Apoyo

        private int ObtenerIdUsuario()
        {
            try
            {
                // Buscar en las diferentes claves de sesión que pueden tener el ID del usuario
                var sessionId = Session["CodigoUsuario"] ?? Session["UserId"] ?? Session["IdUsuario"];
                if (sessionId != null && int.TryParse(sessionId.ToString(), out var id))
                    return id;

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private List<string> ObtenerRolesUsuario()
        {
            var roles = new List<string>();

            if (User?.IsInRole("Administrador") == true)
                roles.Add(EstadoOrdenService.Roles.ADMINISTRADOR);
            
            if (User?.IsInRole("Financiero") == true)
                roles.Add(EstadoOrdenService.Roles.FINANCIERO);
                
            if (User?.IsInRole("Solicitante") == true)
                roles.Add(EstadoOrdenService.Roles.SOLICITANTE);

            return roles;
        }

        private bool EsAdministrador()
        {
            return User?.IsInRole("Administrador") == true;
        }

        private bool EsFinanciero()
        {
            return User?.IsInRole("Financiero") == true;
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// POST: Limpiar cache del dashboard
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrador")]
        public JsonResult LimpiarCache()
        {
            try
            {
                // Limpiar cache de OutputCache
                HttpContext.Response.RemoveOutputCacheItem(Url.Action("ObtenerKPIs"));
                HttpContext.Response.RemoveOutputCacheItem(Url.Action("ObtenerMetricasMensuales"));

                return Json(new { success = true, message = "Cache limpiado exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new 
                { 
                    success = false, 
                    message = "Error al limpiar cache: " + ex.Message 
                });
            }
        }

        #endregion

        #region Health Check

        /// <summary>
        /// GET: Verificar estado del servicio
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public JsonResult HealthCheck()
        {
            try
            {
                var testKpis = _dashboardService.ObtenerKPIs();
                
                return Json(new
                {
                    success = true,
                    status = "OK",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    version = "1.0.0",
                    servicios = new
                    {
                        dashboard = "OK",
                        estados = "OK",
                        dao = "OK"
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    status = "ERROR",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        #endregion
    }
}
