using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaModelo;
using CapaPresentacion.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Financiero,Administrador")]
    public class FinancieroController : Controller
    {
        private readonly PagoDAO _pagoDAO = new PagoDAO();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();

        public ActionResult Index()
        {
            var pagos = _pagoDAO.ObtenerTodos() ?? new List<Pago>();

            // Armamos VM: Pago + Solicitud
            var vms = pagos.Select(p => new PagoFinancieroVM
            {
                Pago = p,
                Solicitud = _solicitudDAO.ObtenerPorId(p.CodigoSolicitud)
            }).ToList();

            return View(vms);
        }

        public ActionResult Detalle(int id)
        {
            var pago = _pagoDAO.ObtenerPorId(id);
            if (pago == null) return HttpNotFound();

            var vm = new PagoFinancieroVM
            {
                Pago = pago,
                Solicitud = _solicitudDAO.ObtenerPorId(pago.CodigoSolicitud)
            };

            return View(vm);
        }
    }
}
