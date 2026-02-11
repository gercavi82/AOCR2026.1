using System.Collections.Generic;
using System.Linq;
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
            var ordenesEnt = _ordenDAO.ObtenerTodasLasOrdenes(estadoConsulta) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
            var ordenes = ordenesEnt.Select(MapearOrden).ToList();

            // Si no hay resultados y se estaba filtrando, intentar sin filtro para descartar problemas de estado
            if (!string.IsNullOrEmpty(estadoConsulta) && (ordenes == null || ordenes.Count == 0))
            {
                var todasEnt = _ordenDAO.ObtenerTodasLasOrdenes(null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
                var todas = todasEnt.Select(MapearOrden).ToList();
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
                var pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(solicitudId > 0 ? solicitudId : orden.Id);
                var pago = MapearPago(pagoEnt);
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

            try
            {
                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, null, "VALIDADO", user, "Aprobado por Finanzas", "FACTURADA", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error aprobando orden Id={id} NumOrden={orden.NumeroOrden}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al aprobar la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("Index");
                }

                try
                {
                    // Generar PDF directamente desde la orden
                    var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(orden);
                    new EmailService().EnviarFacturaGenerada(orden, pdf);
                }
                catch (System.Exception exPdf)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error generando/mandando factura Orden={orden.NumeroOrden}", exPdf.ToString(), "FinancieroController");
                }

                TempData["Success"] = "Orden aprobada y factura generada.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error aprobando orden Id={id} NumOrden={orden.NumeroOrden}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al aprobar la orden.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPago(int id, int? pagoId)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var user = User?.Identity?.Name ?? "FINANCIERO";

            // Marcar el último pago (o el indicado) como VALIDADO y mover la orden a FACTURADA
            _ordenDAO.ActualizarUltimoPagoEstado(id, "VALIDADO", user, "Aprobado por Finanzas");
            _ordenDAO.CambiarEstadoOrden(id, "FACTURADA");

            TempData["Success"] = "Pago aprobado y orden facturada.";
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

            _ordenDAO.ActualizarUltimoPagoEstado(id, "ANULADO", user, motivo);
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
            var ordenesEnt = _ordenDAO.ObtenerTodasLasOrdenes(estado) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
            var ordenes = ordenesEnt.Select(MapearOrden).ToList();
            return View(ordenes);
        }

        #region Helpers
        private OrdenRecaudacionModel MapearOrden(CapaDatos.Entidades.OrdenRecaudacion o)
        {
            if (o == null) return null;
            int usuarioId = o.CodigoUsuario ?? 0;
            return new OrdenRecaudacionModel
            {
                Id = o.Id,
                NumeroOrden = o.NumeroOrden,
                Estado = o.Estado,
                Total = o.Total ?? 0m,
                Subtotal = o.Subtotal ?? 0m,
                Iva = o.Iva ?? 0m,
                FechaCreacion = o.FechaCreacion,
                NombreContribuyente = o.NombreContribuyente,
                CodigoUsuario = usuarioId,
                CodigoSolicitud = o.CodigoSolicitud?.ToString(),
                LugarEmision = o.LugarEmision,
                Compania = o.NombreContribuyente,
                RucCedula = o.RucContribuyente,
                Correo = o.EmailContribuyente,
                Telefono = null,
                Observacion = o.Observaciones ?? o.Observacion
            };
        }

        private PagoModel MapearPago(CapaDatos.Entidades.Pago p)
        {
            if (p == null) return null;
            return new PagoModel
            {
                CodigoPago = p.Id,
                CodigoSolicitud = p.CodigoSolicitud,
                NumeroFactura = p.NumeroComprobante,
                Monto = p.MontoPagado,
                MetodoPago = p.MetodoPago,
                Estado = p.Estado,
                FechaPago = p.FechaPago,
                Observaciones = p.Observaciones,
                ComprobanteRuta = p.RutaComprobante
            };
        }
        #endregion
    }
}
