using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaModelo;
using CapaPresentacion.Models;
using CapaUtilidades;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Financiero,Administrador")]
    public class FinancieroController : Controller
    {
        private readonly PagoDAO _pagoDAO = new PagoDAO();
        private readonly SolicitudAOCRDAO _solicitudDAO = new SolicitudAOCRDAO();
        private readonly OrdenRecaudacionDAO _ordenDAO = new OrdenRecaudacionDAO();
        private readonly EmailService _emailService = new EmailService();

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

        // GET: /Financiero/TodasOrdenes
        public ActionResult TodasOrdenes(string estado)
        {
            var ordenes = _ordenDAO.ObtenerTodasLasOrdenes(estado) ?? new List<CapaDatos.Models.OrdenRecaudacionModel>();
            return View(ordenes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPago(int idPago, string observaciones)
        {
            try
            {
                var pago = _pagoDAO.ObtenerPorId(idPago);
                if (pago == null) return HttpNotFound();

                pago.Estado = "APROBADO";
                pago.FechaValidacion = DateTime.Now;
                pago.UsuarioValidacion = (Session["CodigoUsuario"] ?? "SISTEMA").ToString();
                pago.Observaciones = observaciones ?? "Validado";

                var ok = _pagoDAO.Actualizar(pago);
                Logger.Info($"Pago aprobado. CodigoPago={pago.CodigoPago} CodigoSolicitud={pago.CodigoSolicitud}");

                NotificarSolicitante(pago, "APROBADO", pago.Observaciones);

                TempData[ok ? "Success" : "Error"] = ok ? "Pago aprobado." : "No se pudo aprobar el pago.";
            }
            catch (Exception ex)
            {
                Logger.Error("Error al aprobar pago", ex);
                TempData["Error"] = "Error al aprobar pago.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RechazarPago(int idPago, string observaciones)
        {
            try
            {
                var pago = _pagoDAO.ObtenerPorId(idPago);
                if (pago == null) return HttpNotFound();

                pago.Estado = "RECHAZADO";
                pago.FechaRechazo = DateTime.Now;
                pago.UsuarioRechazo = (Session["CodigoUsuario"] ?? "SISTEMA").ToString();
                pago.Observaciones = observaciones ?? "Rechazado";

                var ok = _pagoDAO.Actualizar(pago);
                Logger.Info($"Pago rechazado. CodigoPago={pago.CodigoPago} CodigoSolicitud={pago.CodigoSolicitud}");

                NotificarSolicitante(pago, "RECHAZADO", pago.Observaciones);

                TempData[ok ? "Success" : "Error"] = ok ? "Pago rechazado." : "No se pudo rechazar el pago.";
            }
            catch (Exception ex)
            {
                Logger.Error("Error al rechazar pago", ex);
                TempData["Error"] = "Error al rechazar pago.";
            }

            return RedirectToAction("Index");
        }

        private void NotificarSolicitante(Pago pago, string estado, string observaciones)
        {
            try
            {
                var solicitud = _solicitudDAO.ObtenerPorId(pago.CodigoSolicitud);
                if (solicitud == null || string.IsNullOrWhiteSpace(solicitud.Email)) return;

                var nombre = !string.IsNullOrWhiteSpace(solicitud.NombreOperador)
                    ? solicitud.NombreOperador
                    : solicitud.RepresentanteLegal;

                var numero = !string.IsNullOrWhiteSpace(solicitud.NumeroSolicitud)
                    ? solicitud.NumeroSolicitud
                    : pago.CodigoSolicitud.ToString();

                _emailService.EnviarResultadoValidacionPago(solicitud.Email, nombre, numero, estado, observaciones);
            }
            catch (Exception ex)
            {
                Logger.Warn("No se pudo notificar al solicitante: " + ex.Message);
            }
        }
    }
}
