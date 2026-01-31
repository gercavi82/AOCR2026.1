using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;
using CapaNegocio;
using CapaNegocio.DTOs;
using CapaNegocio.Interfaces;
using CapaModelo;
using CapaDatos.Services;
using CapaNegocio.Services;
using System.Threading.Tasks;
using CapaPresentacion.Filters;
using CapaPresentacion.Models;
using CapaDatos.Interfaces;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class PagoController : Controller
    {
        private readonly IOrdenRecaudacionOrchestrator _orchestrator;
        private readonly IPagoRepository _pagoRepository;
        private readonly IAuditService _auditService;
        private readonly CapaDatos.Services.ILoggingService _logger = CapaDatos.Services.LoggingServiceFactory.Create();

        public PagoController(
            IOrdenRecaudacionOrchestrator orchestrator,
            IPagoRepository pagoRepository,
            IAuditService auditService)
        {
            _orchestrator = orchestrator;
            _pagoRepository = pagoRepository;
            _auditService = auditService;
        }

        // ============================================================
        // DETALLE: lista de pagos por solicitud
        // ============================================================
        public ActionResult Detalle(int solicitudId)
        {
            var pagos = new List<Pago>();
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
        [Audit(EntityType = "PAGO", Action = "VALIDAR")]
        public async Task<ActionResult> Validar(ValidarPagoViewModel model)
        {
            var correlationId = HttpContext.Items["CorrelationId"] as string;

            using (_logger.BeginScope(new CapaDatos.Services.LogContext { CorrelationId = correlationId }))
            {
                try
                {
                    // Obtener pago actual para auditoría
                    var pagoActual = await _pagoRepository.ObtenerPorIdAsync(model.PagoId);
                    if (pagoActual == null)
                    {
                        TempData["ErrorMessage"] = "Pago no encontrado.";
                        return RedirectToAction("Pendientes");
                    }

                    // Establecer contexto de orden
                    var orden = await _orchestrator.ObtenerEstadoFlujoAsync(pagoActual.OrdenId);
                    if (orden.Success && orden.Data?.Orden != null)
                    {
                        HttpContext.Items["NumeroOrden"] = orden.Data.Orden.NumeroOrden;
                    }

                    var estadoAnterior = pagoActual.Estado;

                    var request = new ValidarPagoRequest
                    {
                        PagoId = model.PagoId,
                        Aprobado = model.Aprobado,
                        Observaciones = model.Observaciones,
                        UsuarioValidacion = User.Identity.Name
                    };

                    _logger.LogInfo(string.Format("Validando pago {0}: {1}",
                        model.PagoId, model.Aprobado ? "APROBAR" : "RECHAZAR"),
                        new CapaDatos.Services.LogContext { CorrelationId = correlationId });

                    var resultado = await _orchestrator.ValidarPagoAsync(request);

                    if (resultado.Success)
                    {
                        // Registrar auditoría de cambio de estado
                        await _auditService.RegistrarCambioEstadoAsync(new CambioEstadoAudit
                        {
                            TipoEntidad = "PAGO",
                            EntidadId = model.PagoId,
                            NumeroReferencia = pagoActual.NumeroComprobante,
                            EstadoAnterior = estadoAnterior,
                            EstadoNuevo = resultado.Data.EstadoPago,
                            Usuario = User.Identity.Name,
                            Motivo = model.Observaciones,
                            IpOrigen = GetClientIp(),
                            CorrelationId = correlationId
                        });

                        // También auditar cambio de estado de orden
                        await _auditService.RegistrarCambioEstadoAsync(new CambioEstadoAudit
                        {
                            TipoEntidad = "ORDEN",
                            EntidadId = resultado.Data.OrdenId,
                            NumeroReferencia = orden.Data?.Orden?.NumeroOrden,
                            EstadoAnterior = orden.Data?.EstadoActual,
                            EstadoNuevo = resultado.Data.EstadoOrden,
                            Usuario = User.Identity.Name,
                            Motivo = string.Format("Pago {0}: {1}",
                                model.Aprobado ? "aprobado" : "rechazado", model.Observaciones),
                            IpOrigen = GetClientIp(),
                            CorrelationId = correlationId
                        });

                        var mensaje = model.Aprobado
                            ? "Pago validado correctamente."
                            : "Pago rechazado.";

                        if (resultado.Data.FacturaGenerada)
                        {
                            mensaje += " Factura generada: " + resultado.Data.NumeroFactura;
                        }

                        _logger.LogInfo(mensaje, new CapaDatos.Services.LogContext { CorrelationId = correlationId });

                        TempData["SuccessMessage"] = mensaje;
                        return RedirectToAction("Detalles", "OrdenRecaudacion", new { id = resultado.Data.OrdenId });
                    }

                    _logger.LogWarning("Error al validar pago: " + resultado.Message,
                        new CapaDatos.Services.LogContext { CorrelationId = correlationId });

                    TempData["ErrorMessage"] = resultado.Message;
                    return RedirectToAction("Pendientes");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, new CapaDatos.Services.LogContext { CorrelationId = correlationId });
                    TempData["ErrorMessage"] = "Error interno al validar el pago.";
                    return RedirectToAction("Pendientes");
                }
            }
        }

        private string GetClientIp()
        {
            var ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (string.IsNullOrEmpty(ip))
            {
                ip = Request.ServerVariables["REMOTE_ADDR"];
            }
            return ip;
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
