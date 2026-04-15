using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Entidades;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionDashboardController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao;

        public OrdenRecaudacionDashboardController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        // GET: OrdenRecaudacionDashboard
        public ActionResult Index()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return RedirectToAction("Login", "Account");

                return View();
            }
            catch
            {
                return RedirectToAction("Login", "Account");
            }
        }

        // GET: OrdenRecaudacionDashboard/ObtenerDatos
        [HttpGet]
        public JsonResult ObtenerDatos(string estado, string fechaDesde, string fechaHasta, string numeroOrden)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesion expirada" }, JsonRequestBehavior.AllowGet);

                var esAdmin = User != null && (User.IsInRole("Administrador") || User.IsInRole("Financiero"));

                DateTime? desde = null;
                DateTime? hasta = null;
                if (!string.IsNullOrWhiteSpace(fechaDesde) && DateTime.TryParse(fechaDesde, out var fd))
                    desde = fd.Date;
                if (!string.IsNullOrWhiteSpace(fechaHasta) && DateTime.TryParse(fechaHasta, out var fh))
                    hasta = fh.Date.AddDays(1).AddSeconds(-1);

                var ordenes = esAdmin
                    ? _dao.ListarFiltrado(null, estado, desde, hasta, numeroOrden)
                    : _dao.ListarFiltrado(idUsuario, estado, desde, hasta, numeroOrden);

                int ordenesPendientes = 0;
                int ordenesCompletadas = 0;
                int ordenesAnuladas = 0;
                int ordenesRechazadas = 0;
                decimal montoTotal = 0m;

                foreach (var o in ordenes)
                {
                    var estadoOrden = (o.Estado ?? "").Trim().ToUpperInvariant();

                    if (estadoOrden == "BORRADOR" || estadoOrden == "GENERADA" || estadoOrden == "ENVIADA" || estadoOrden == "PENDIENTE")
                        ordenesPendientes++;
                    else if (estadoOrden == "PAGADA" || estadoOrden == "COMPLETADA" || estadoOrden == "FACTURADA")
                        ordenesCompletadas++;
                    else if (estadoOrden == "ANULADA")
                        ordenesAnuladas++;
                    else if (estadoOrden == "RECHAZADA")
                        ordenesRechazadas++;

                    montoTotal += o.Total ?? 0m;
                }

                var ultima = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .FirstOrDefault();

                var ultimaOrden = (ultima != null && !string.IsNullOrWhiteSpace(ultima.NumeroOrden))
                    ? ultima.NumeroOrden
                    : "N/A";

                var ordenesRecientes = ordenes
                    .OrderByDescending(x => x.FechaCreacion)
                    .Select(o => new
                    {
                        id = o.Id,
                        numeroOrden = o.NumeroOrden,
                        fechaCreacion = o.FechaCreacion,
                        estado = (o.Estado ?? "").Trim(),
                        estadoColor = ObtenerColorEstado(o.Estado),
                        nombreContribuyente = string.IsNullOrWhiteSpace(o.NombreContribuyente) ? o.Compania : o.NombreContribuyente,
                        total = o.Total ?? 0m,
                        diasVencimiento = CalcularDias(o.FechaCreacion)
                    })
                    .ToList();

                decimal montoPagado = ordenes
                    .Where(o => EsEstadoPagado(o.Estado))
                    .Sum(o => o.Total ?? 0m);

                decimal saldoPendiente = ordenes
                    .Where(o => !EsEstadoPagado(o.Estado) && !EsEstadoAnulado(o.Estado))
                    .Sum(o => o.Total ?? 0m);

                var tasaAprobacion = ordenes.Count() > 0
                    ? (ordenesCompletadas * 100.0) / ordenes.Count()
                    : 0.0;

                return Json(new
                {
                    success = true,
                    kpis = new
                    {
                        totalOrdenes = ordenes.Count(),
                        ordenesPendientes,
                        ordenesCompletadas,
                        ordenesAnuladas,
                        ordenesRechazadas,
                        montoTotal,
                        montoPagado,
                        saldoPendiente,
                        tasaAprobacion,
                        promedioTiempoPago = 0
                    },
                    ultimaOrden,
                    ordenesRecientes,
                    filtrosAplicados = new
                    {
                        totalFiltradas = ordenes.Count()
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: OrdenRecaudacionDashboard/AccionRapida
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador,Financiero")]
        public JsonResult AccionRapida(string accion, int ordenId, string motivo)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesion expirada" });

                var orden = _dao.ObtenerOrdenPorIdModel(ordenId);
                if (orden == null)
                    return Json(new { success = false, message = "Orden no encontrada" });

                var esAdmin = User != null && (User.IsInRole("Administrador") || User.IsInRole("Financiero"));
                if (!esAdmin && orden.CodigoUsuario != idUsuario)
                    return Json(new { success = false, message = "No autorizado" });

                var estado = (orden.Estado ?? "").Trim().ToUpperInvariant();
                var accionNorm = (accion ?? "").Trim().ToUpperInvariant();

                if (accionNorm == "GENERAR")
                {
                    if (estado != "BORRADOR")
                        return Json(new { success = false, message = "Solo BORRADOR puede generarse" });
                    if (orden.Total <= 0)
                        return Json(new { success = false, message = "La orden debe tener total mayor a 0" });

                    var ok = _dao.CambiarEstadoOrden(ordenId, "GENERADA");
                    return Json(new { success = ok, message = ok ? "Orden generada" : "No se pudo generar" });
                }

                if (accionNorm == "ANULAR")
                {
                    if (estado == "FACTURADA" || estado == "COMPLETADA" || estado == "PAGADA")
                        return Json(new { success = false, message = "No se puede anular una orden pagada" });

                    if (string.IsNullOrWhiteSpace(motivo))
                        return Json(new { success = false, message = "Motivo requerido" });

                    var ok = _dao.CambiarEstadoOrden(ordenId, "ANULADA");
                    return Json(new { success = ok, message = ok ? "Orden anulada" : "No se pudo anular" });
                }

                return Json(new { success = false, message = "Accion no soportada" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private int ObtenerIdUsuario()
        {
            if (Session["IdUsuario"] != null &&
                int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            return 0;
        }

        private static int CalcularDias(DateTime fechaCreacion)
        {
            var dias = (DateTime.Now.Date - fechaCreacion.Date).Days;
            return dias < 0 ? 0 : dias;
        }

        private static string ObtenerColorEstado(string estado)
        {
            var e = (estado ?? "").Trim().ToUpperInvariant();
            if (e == "BORRADOR") return "warning";
            if (e == "GENERADA" || e == "ENVIADA" || e == "PENDIENTE") return "info";
            if (e == "PROCESADA" || e == "APROBADA" || e == "FACTURADA" || e == "COMPLETADA" || e == "PAGADA") return "success";
            if (e == "ANULADA") return "secondary";
            if (e == "RECHAZADA") return "danger";
            return "dark";
        }

        private static bool EsEstadoPagado(string estado)
        {
            var e = (estado ?? "").Trim().ToUpperInvariant();
            return e == "PAGADA" || e == "COMPLETADA" || e == "FACTURADA";
        }

        private static bool EsEstadoAnulado(string estado)
        {
            var e = (estado ?? "").Trim().ToUpperInvariant();
            return e == "ANULADA";
        }
    }
}
