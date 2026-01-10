using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class FinancieroController : Controller
    {
        private readonly SolicitudBL _solicitudBL;
        private readonly PagoBL _pagoBL;
        private readonly HistorialEstadoBL _historialBL;

        public FinancieroController()
        {
            _solicitudBL = new SolicitudBL();
            _pagoBL = new PagoBL();
            _historialBL = new HistorialEstadoBL();
        }

        // ✅ Redirección predeterminada a la lista de pagos
        [HttpGet]
        [Authorize(Roles = "Financiero,Administrador")]
        public ActionResult Index()
        {
            return RedirectToAction("ValidacionPagos");
        }

        [HttpGet]
        [Authorize(Roles = "Financiero,Administrador")]
        public ActionResult ValidacionPagos()
        {
            try
            {
                var pagosPendientes = _pagoBL.ObtenerTodos()
                    .Where(p => p.Estado == "PENDIENTE")
                    .ToList();

                foreach (var pago in pagosPendientes)
                {
                    pago.Solicitud = _solicitudBL.ObtenerDetalle(pago.CodigoSolicitud);
                }

                return View("ListaPagos", pagosPendientes); // ✅ View clara para lista
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al cargar pagos: {ex.Message}";
                return View("ListaPagos", new List<Pago>());
            }
        }

        [HttpGet]
        [Authorize(Roles = "Financiero,Administrador")]
        public ActionResult ValidarPago(int id)
        {
            try
            {
                var pago = _pagoBL.ObtenerPorId(id);

                if (pago == null)
                {
                    TempData["Error"] = "Pago no encontrado";
                    return RedirectToAction("ValidacionPagos");
                }

                if (!string.Equals(pago.Estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Warning"] = $"Este pago ya fue validado (Estado: {pago.Estado})";
                }

                pago.Solicitud = _solicitudBL.ObtenerDetalle(pago.CodigoSolicitud);
                return View("ValidarPago", pago); // ✅ Vista individual para validación
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("ValidacionPagos");
            }
        }
    }
}
