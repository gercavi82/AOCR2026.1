using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class PagoController : Controller
    {
        private readonly IOrdenRecaudacionOrchestrator _orchestrator;
        private readonly IPagoRepository _pagoRepository;

        public PagoController(
            IOrdenRecaudacionOrchestrator orchestrator,
            IPagoRepository pagoRepository)
        {
            _orchestrator = orchestrator;
            _pagoRepository = pagoRepository;
        }

        // ============================================================
        // DETALLE: lista de pagos por solicitud
        // ============================================================
        public ActionResult Detalle(int solicitudId)
        {
            var pagos = _bl.ObtenerPorSolicitud(solicitudId);
            if (pagos == null)
                pagos = new List<Pago>();

            ViewBag.SolicitudId = solicitudId;
            return View(pagos);
        }

        /// <summary>
        /// Registrar pago - refactorizado para usar orquestador
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador")]
        public async Task<ActionResult> Registrar(RegistrarPagoViewModel model, HttpPostedFileBase comprobante)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var request = new RegistrarPagoRequest
                {
                    OrdenId = model.OrdenId,
                    NumeroComprobante = model.NumeroComprobante,
                    MontoPagado = model.MontoPagado,
                    FechaPago = model.FechaPago,
                    MetodoPago = model.MetodoPago,
                    BancoOrigen = model.BancoOrigen,
                    Observaciones = model.Observaciones,
                    UsuarioRegistro = User.Identity.Name
                };

                // Procesar archivo de comprobante
                if (comprobante != null && comprobante.ContentLength > 0)
                {
                    request.NombreArchivo = comprobante.FileName;
                    request.TipoArchivo = comprobante.ContentType;
                    request.TamanoArchivo = comprobante.ContentLength;

                    using (var ms = new System.IO.MemoryStream())
                    {
                        comprobante.InputStream.CopyTo(ms);
                        request.ContenidoArchivo = ms.ToArray();
                    }
                }

                // Usar orquestador para registrar pago
                var resultado = await _orchestrator.RegistrarPagoAsync(request);

                if (resultado.Success)
                {
                    TempData["SuccessMessage"] = "Pago registrado exitosamente. Pendiente de validación.";
                    return RedirectToAction("Detalles", "OrdenRecaudacion", new { id = model.OrdenId });
                }

                ModelState.AddModelError("", resultado.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al registrar pago: " + ex.Message);
                ModelState.AddModelError("", "Error interno al registrar el pago.");
                return View(model);
            }
        }

        /// <summary>
        /// Validar pago (aprobar/rechazar) - refactorizado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Financiero,Administrador")]
        public async Task<ActionResult> Validar(ValidarPagoViewModel model)
        {
            try
            {
                var request = new ValidarPagoRequest
                {
                    PagoId = model.PagoId,
                    Aprobado = model.Aprobado,
                    Observaciones = model.Observaciones,
                    UsuarioValidacion = User.Identity.Name
                };

                var resultado = await _orchestrator.ValidarPagoAsync(request);

                if (resultado.Success)
                {
                    var mensaje = model.Aprobado
                        ? "Pago validado correctamente."
                        : "Pago rechazado.";

                    if (resultado.Data.FacturaGenerada)
                    {
                        mensaje += " Factura generada: " + resultado.Data.NumeroFactura;
                    }

                    TempData["SuccessMessage"] = mensaje;
                    return RedirectToAction("Detalles", "OrdenRecaudacion", new { id = resultado.Data.OrdenId });
                }

                TempData["ErrorMessage"] = resultado.Message;
                return RedirectToAction("Pendientes");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al validar pago: " + ex.Message);
                TempData["ErrorMessage"] = "Error interno al validar el pago.";
                return RedirectToAction("Pendientes");
            }
        }

        /// <summary>
        /// Lista de pagos pendientes de validación
        /// </summary>
        [Authorize(Roles = "Financiero,Administrador")]
        public async Task<ActionResult> Pendientes()
        {
            try
            {
                var pagos = await _pagoRepository.ObtenerPorEstadoAsync("PENDIENTE_VALIDACION");
                return View(pagos);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener pagos pendientes: " + ex.Message);
                TempData["ErrorMessage"] = "Error al cargar los pagos pendientes.";
                return View(new List<CapaDatos.Entidades.Pago>());
            }
        }
    }
}
