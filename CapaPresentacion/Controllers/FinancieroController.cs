using System.Collections.Generic;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Financiero,Administrador")]
    public class FinancieroController : Controller
    {
        private readonly OrdenRecaudacionDAO _ordenDAO = new OrdenRecaudacionDAO();

        public ActionResult Index(string estado = "TODAS")
        {
            var estadoFiltro = string.IsNullOrWhiteSpace(estado)
                ? "TODAS"
                : estado.Trim().ToUpperInvariant();

            // Para "TODAS" no aplicar filtro en SQL
            var estadoConsulta = estadoFiltro == "TODAS" ? null : estadoFiltro;
            var ordenes = _ordenDAO.ObtenerTodasLasOrdenes(estadoConsulta) ?? new List<OrdenRecaudacionModel>();

            // Si no hay resultados y se estaba filtrando, intentar sin filtro para descartar problemas de estado
            if (!string.IsNullOrEmpty(estadoConsulta) && (ordenes == null || ordenes.Count == 0))
            {
                var todas = _ordenDAO.ObtenerTodasLasOrdenes(null) ?? new List<OrdenRecaudacionModel>();
                ViewBag.SinResultadosConFiltro = true;
                ViewBag.TotalSinFiltro = todas.Count;
                if (todas.Any())
                {
                    ordenes = todas;
                    estadoFiltro = "TODAS";
                    estadoConsulta = null;
                }
            }

            var vms = new List<OrdenValidacionFinancieraVM>();
            foreach (var orden in ordenes)
            {
                var solicitudId = 0;
                if (orden != null && !string.IsNullOrWhiteSpace(orden.CodigoSolicitud))
                {
                    int.TryParse(orden.CodigoSolicitud, out solicitudId);
                }
                var pago = _ordenDAO.ObtenerUltimoPagoPorOrden(solicitudId > 0 ? solicitudId : orden.Id);
                vms.Add(new OrdenValidacionFinancieraVM
                {
                    Orden = orden,
                    Pago = pago
                });
            }

            ViewBag.EstadoFiltro = estadoFiltro;
            return View(vms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarOrden(int id)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estado != "PROCESADA")
            {
                TempData["Error"] = "Solo se pueden aprobar órdenes en estado PROCESADA.";
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";

            _ordenDAO.ActualizarUltimoPagoEstado(id, "APROBADO", user, "Aprobado por Finanzas");
            _ordenDAO.CambiarEstadoOrden(id, "FACTURADA");

            try
            {
                var dto = _ordenDAO.ObtenerDatosParaPdf(id, 0);
                if (dto != null)
                {
                    var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(dto);
                    new EmailService().EnviarFacturaGenerada(orden, pdf);
                }
            }
            catch
            {
                // No bloquear el flujo si el PDF/correo falla
            }

            TempData["Success"] = "Orden aprobada y factura generada.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RechazarOrden(int id, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe ingresar un motivo de rechazo.";
                return RedirectToAction("Index");
            }

            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estado != "PROCESADA")
            {
                TempData["Error"] = "Solo se pueden rechazar órdenes en estado PROCESADA.";
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";

            _ordenDAO.ActualizarUltimoPagoEstado(id, "RECHAZADO", user, motivo);
            _ordenDAO.CambiarEstadoOrden(id, "PENDIENTE");

            try
            {
                new EmailService().EnviarNotificacionRechazo(orden, motivo);
            }
            catch
            {
                // No bloquear el flujo si el email falla
            }

            TempData["Success"] = "Orden rechazada correctamente.";
            return RedirectToAction("Index");
        }

        // GET: /Financiero/TodasOrdenes
        public ActionResult TodasOrdenes(string estado)
        {
            var ordenes = _ordenDAO.ObtenerTodasLasOrdenes(estado) ?? new List<OrdenRecaudacionModel>();
            return View(ordenes);
        }
    }
}
