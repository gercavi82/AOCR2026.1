using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Entidades;
using CapaDatos.Models;
using CapaNegocio.Helpers;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Dashboard Empresarial para Órdenes de Recaudación
    /// Nivel: Producción - Clean Architecture
    /// </summary>
    [Authorize]
    public class OrdenRecaudacionDashboardController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao;
        
        public OrdenRecaudacionDashboardController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        /// <summary>
        /// Vista principal del dashboard empresarial
        /// </summary>
        public ActionResult Index()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return RedirectToAction("Login", "Account");

                // Cargar filtros iniciales para la vista
                ViewBag.EstadosFiltro = ObtenerEstadosParaFiltro();
                ViewBag.UsuarioActual = idUsuario;
                
                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Dashboard Index: {ex.Message}");
                return RedirectToAction("Login", "Account");
            }
        }

        /// <summary>
        /// API: Obtener datos del dashboard con filtros empresariales
        /// GET: /OrdenRecaudacionDashboard/ObtenerDatos?estado=PENDIENTE&fechaDesde=2024-01-01
        /// </summary>
        [HttpGet]
        public JsonResult ObtenerDatos(string estado = null, string fechaDesde = null, string fechaHasta = null, string numeroOrden = null)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                // =============================
                // FILTROS EMPRESARIALES
                // =============================
                DateTime? desde = null, hasta = null;
                
                if (!string.IsNullOrEmpty(fechaDesde) && DateTime.TryParse(fechaDesde, out DateTime d1))
                    desde = d1;
                
                if (!string.IsNullOrEmpty(fechaHasta) && DateTime.TryParse(fechaHasta, out DateTime d2))
                    hasta = d2.AddDays(1).AddSeconds(-1); // Hasta final del día

                // Obtener órdenes con filtros aplicados
                var ordenes = ObtenerOrdenesFiltradas(idUsuario, estado, desde, hasta, numeroOrden);

                // =============================
                // KPIs EMPRESARIALES CORREGIDOS
                // =============================
                var kpis = CalcularKPIsEmpresariales(ordenes);

                // =============================
                // ÓRDENES RECIENTES OPTIMIZADAS
                // =============================
                var ordenesRecientes = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .Take(15)
                    .Select(o => new
                    {
                        id = o.Id,
                        numeroOrden = o.NumeroOrden ?? "SIN-NUM",
                        fechaCreacion = o.FechaCreacion,
                        estado = o.Estado ?? "SIN-ESTADO",
                        estadoColor = ObtenerColorEstado(o.Estado),
                        total = o.Total ?? 0m,
                        nombreContribuyente = o.NombreContribuyente ?? o.Compania ?? "Sin nombre",
                        diasVencimiento = CalcularDiasVencimiento(o.FechaCreacion, o.Estado)
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    kpis = new
                    {
                        totalOrdenes = kpis.TotalOrdenes,
                        ordenesPendientes = kpis.OrdenesPendientes,
                        ordenesCompletadas = kpis.OrdenesCompletadas,
                        ordenesAnuladas = kpis.OrdenesAnuladas,
                        ordenesRechazadas = kpis.OrdenesRechazadas,
                        montoTotal = kpis.MontoTotal,
                        montoPagado = kpis.MontoPagado, // ? REAL DESDE PAGOS
                        saldoPendiente = kpis.SaldoPendiente,
                        ultimaOrden = kpis.UltimaOrden,
                        promedioTiempoPago = kpis.PromedioTiempoPago,
                        tasaAprobacion = kpis.TasaAprobacion
                    },
                    ordenesRecientes = ordenesRecientes,
                    filtrosAplicados = new
                    {
                        estado = estado,
                        fechaDesde = fechaDesde,
                        fechaHasta = fechaHasta,
                        numeroOrden = numeroOrden,
                        totalFiltradas = ordenes.Count
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerDatos Dashboard: {ex.Message}");
                return Json(new { 
                    success = false, 
                    message = "Error interno del servidor",
                    error = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// API: Obtener datos para gráficos del dashboard
        /// </summary>
        [HttpGet]
        public JsonResult ObtenerDatosGraficos()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesión expirada" }, JsonRequestBehavior.AllowGet);

                var ordenes = _dao.ListarPorUsuarioModel(idUsuario, null) ?? new List<OrdenRecaudacionModel>();

                // Gráfico de estados
                var estadosPorMes = ordenes
                    .GroupBy(o => new { Mes = o.FechaCreacion.Month, Año = o.FechaCreacion.Year, Estado = o.Estado })
                    .Select(g => new
                    {
                        periodo = $"{g.Key.Año}-{g.Key.Mes:00}",
                        estado = g.Key.Estado,
                        cantidad = g.Count(),
                        monto = g.Sum(o => o.Total)
                    })
                    .OrderBy(x => x.periodo)
                    .ToList();

                // Gráfico de tendencias
                var tendenciasMensuales = ordenes
                    .GroupBy(o => new { Mes = o.FechaCreacion.Month, Año = o.FechaCreacion.Year })
                    .Select(g => new
                    {
                        periodo = $"{g.Key.Año}-{g.Key.Mes:00}",
                        totalOrdenes = g.Count(),
                        montoTotal = g.Sum(o => o.Total),
                        ordenesCompletadas = g.Count(o => o.Estado == "COMPLETADA" || o.Estado == "FACTURADA")
                    })
                    .OrderBy(x => x.periodo)
                    .ToList();

                return Json(new
                {
                    success = true,
                    estadosPorMes = estadosPorMes,
                    tendenciasMensuales = tendenciasMensuales
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        /// <summary>
        /// API: Acciones rápidas del dashboard
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AccionRapida(string accion, int ordenId)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesión expirada" });

                var orden = _dao.ObtenerOrdenPorIdModel(ordenId);
                if (orden == null || orden.CodigoUsuario != idUsuario)
                    return Json(new { success = false, message = "Orden no encontrada o sin permisos" });

                bool resultado = false;
                string mensaje = "";

                switch (accion?.ToUpperInvariant())
                {
                    case "GENERAR":
                        if (orden.Estado == "BORRADOR" && orden.Total > 0)
                        {
                            resultado = _dao.CambiarEstadoOrden(ordenId, "PENDIENTE");
                            mensaje = resultado ? "Orden generada correctamente" : "Error al generar orden";
                        }
                        else
                        {
                            mensaje = "Solo se pueden generar órdenes en BORRADOR con monto > 0";
                        }
                        break;

                    case "ANULAR":
                        if (orden.Estado != "FACTURADA" && orden.Estado != "COMPLETADA")
                        {
                            resultado = _dao.CambiarEstadoOrden(ordenId, "ANULADA");
                            mensaje = resultado ? "Orden anulada correctamente" : "Error al anular orden";
                        }
                        else
                        {
                            mensaje = "No se pueden anular órdenes facturadas o completadas";
                        }
                        break;

                    default:
                        mensaje = "Acción no válida";
                        break;
                }

                return Json(new { success = resultado, message = mensaje });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno: " + ex.Message });
            }
        }

        #region Métodos Privados Empresariales

        /// <summary>
        /// Obtener órdenes con filtros empresariales aplicados
        /// </summary>
        private List<OrdenRecaudacionModel> ObtenerOrdenesFiltradas(int idUsuario, string estado, DateTime? fechaDesde, DateTime? fechaHasta, string numeroOrden)
        {
            var ordenes = _dao.ListarPorUsuarioModel(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();

            // Aplicar filtros adicionales
            if (fechaDesde.HasValue)
                ordenes = ordenes.Where(o => o.FechaCreacion >= fechaDesde.Value).ToList();

            if (fechaHasta.HasValue)
                ordenes = ordenes.Where(o => o.FechaCreacion <= fechaHasta.Value).ToList();

            if (!string.IsNullOrEmpty(numeroOrden))
                ordenes = ordenes.Where(o => (o.NumeroOrden ?? "").Contains(numeroOrden)).ToList();

            return ordenes;
        }

        /// <summary>
        /// Calcular KPIs empresariales corregidos
        /// </summary>
        private KPIsDashboard CalcularKPIsEmpresariales(List<OrdenRecaudacionModel> ordenes)
        {
            var kpis = new KPIsDashboard();

            kpis.TotalOrdenes = ordenes.Count;
            kpis.OrdenesPendientes = ordenes.Count(o => EsEstadoPendiente(o.Estado));
            kpis.OrdenesCompletadas = ordenes.Count(o => EsEstadoCompletado(o.Estado));
            kpis.OrdenesAnuladas = ordenes.Count(o => o.Estado == "ANULADA");
            kpis.OrdenesRechazadas = ordenes.Count(o => o.Estado == "RECHAZADA");

            kpis.MontoTotal = ordenes.Sum(o => o.Total);

            // ? MONTO PAGADO REAL: Solo órdenes con pagos aprobados
            kpis.MontoPagado = ordenes
                .Where(o => EsEstadoCompletado(o.Estado))
                .Sum(o => ObtenerMontoPagadoReal(o.Id));

            kpis.SaldoPendiente = kpis.MontoTotal - kpis.MontoPagado;

            var ultimaOrden = ordenes.OrderByDescending(x => x.FechaCreacion).FirstOrDefault();
            kpis.UltimaOrden = ultimaOrden?.NumeroOrden ?? "N/A";

            // Métricas avanzadas
            var ordenesConPago = ordenes.Where(o => EsEstadoCompletado(o.Estado)).ToList();
            if (ordenesConPago.Any())
            {
                var tiemposPromedio = ordenesConPago.Select(o => (DateTime.Now - o.FechaCreacion).TotalDays).Average();
                kpis.PromedioTiempoPago = Math.Round(tiemposPromedio, 1);
            }

            if (kpis.TotalOrdenes > 0)
            {
                kpis.TasaAprobacion = Math.Round((double)kpis.OrdenesCompletadas / kpis.TotalOrdenes * 100, 1);
            }

            return kpis;
        }

        /// <summary>
        /// Obtener monto pagado real desde tabla de pagos
        /// </summary>
        private decimal ObtenerMontoPagadoReal(int ordenId)
        {
            try
            {
                var pagos = _dao.ObtenerPagosPorOrden(ordenId);
                return pagos
                    .Where(p => p.Estado == "APROBADO" || p.Estado == "VALIDADO")
                    .Sum(p => p.Monto);
            }
            catch
            {
                return 0m;
            }
        }

        /// <summary>
        /// Determinar si un estado es considerado pendiente
        /// </summary>
        private bool EsEstadoPendiente(string estado)
        {
            var estadoUpper = (estado ?? "").Trim().ToUpperInvariant();
            return estadoUpper == "BORRADOR" || 
                   estadoUpper == "PENDIENTE" || 
                   estadoUpper == "GENERADA" || 
                   estadoUpper == "PROCESADA";
        }

        /// <summary>
        /// Determinar si un estado es considerado completado
        /// </summary>
        private bool EsEstadoCompletado(string estado)
        {
            var estadoUpper = (estado ?? "").Trim().ToUpperInvariant();
            return estadoUpper == "COMPLETADA" || 
                   estadoUpper == "FACTURADA" || 
                   estadoUpper == "PAGADA";
        }

        /// <summary>
        /// Obtener color CSS para badges de estado
        /// </summary>
        private string ObtenerColorEstado(string estado)
        {
            var estadoUpper = (estado ?? "").Trim().ToUpperInvariant();
            
            switch (estadoUpper)
            {
                case "BORRADOR": return "secondary";
                case "PENDIENTE": case "GENERADA": return "warning";
                case "PROCESADA": return "info";
                case "FACTURADA": case "COMPLETADA": return "success";
                case "ANULADA": case "RECHAZADA": return "danger";
                default: return "dark";
            }
        }

        /// <summary>
        /// Calcular días de vencimiento/antigüedad
        /// </summary>
        private int CalcularDiasVencimiento(DateTime fechaCreacion, string estado)
        {
            if (EsEstadoCompletado(estado) || estado == "ANULADA")
                return 0;

            return (DateTime.Now - fechaCreacion).Days;
        }

        /// <summary>
        /// Obtener estados disponibles para filtros
        /// </summary>
        private List<SelectListItem> ObtenerEstadosParaFiltro()
        {
            return new List<SelectListItem>
            {
                new SelectListItem { Text = "Todos los estados", Value = "" },
                new SelectListItem { Text = "Borrador", Value = "BORRADOR" },
                new SelectListItem { Text = "Pendiente", Value = "PENDIENTE" },
                new SelectListItem { Text = "Procesada", Value = "PROCESADA" },
                new SelectListItem { Text = "Facturada", Value = "FACTURADA" },
                new SelectListItem { Text = "Completada", Value = "COMPLETADA" },
                new SelectListItem { Text = "Anulada", Value = "ANULADA" }
            };
        }

        /// <summary>
        /// Obtener ID de usuario desde sesión de manera segura
        /// </summary>
        private int ObtenerIdUsuario()
        {
            if (Session["IdUsuario"] != null && int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            
            if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int userId))
            {
                return userId;
            }
            
            return 0;
        }

        #endregion
    }

    /// <summary>
    /// Clase para estructurar KPIs del dashboard
    /// </summary>
    public class KPIsDashboard
    {
        public int TotalOrdenes { get; set; }
        public int OrdenesPendientes { get; set; }
        public int OrdenesCompletadas { get; set; }
        public int OrdenesAnuladas { get; set; }
        public int OrdenesRechazadas { get; set; }
        public decimal MontoTotal { get; set; }
        public decimal MontoPagado { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string UltimaOrden { get; set; }
        public double PromedioTiempoPago { get; set; }
        public double TasaAprobacion { get; set; }
    }
}
