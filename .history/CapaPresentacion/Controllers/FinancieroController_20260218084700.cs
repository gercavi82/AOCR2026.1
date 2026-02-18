using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaDatos.Constants;
using CapaNegocio;
using CapaNegocio.Helpers;
using CapaPresentacion.Models;
using CapaPresentacion.Models.EmailTemplates;
using CapaPresentacion.Services;
using CapaModelo;
using PagoModelDatos = CapaDatos.Models.PagoModel;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "FINANCIERO,Financiero,Administrador")]
    public class FinancieroController : Controller
    {
        private readonly OrdenRecaudacionDAO _ordenDAO = new OrdenRecaudacionDAO();

        public ActionResult Index(FinancieroOrdenFiltroVM filtro)
        {
            filtro = filtro ?? new FinancieroOrdenFiltroVM();

            var estadoFiltro = string.IsNullOrWhiteSpace(filtro.Estado)
                ? "PROCESADA"
                : filtro.Estado.Trim().ToUpperInvariant();

            var estadoConsulta = estadoFiltro == "TODAS" ? null : estadoFiltro;

            var ordenesEnt = _ordenDAO.ListarFiltrado(
                null,
                estadoConsulta,
                filtro.FechaDesde,
                filtro.FechaHasta,
                filtro.NumeroOrden,
                filtro.Solicitante) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();

            var ordenes = ordenesEnt.Select(MapearOrden).ToList();

            var vm = new FinancieroOrdenBandejaVM
            {
                Filtro = filtro,
                Ordenes = new List<OrdenValidacionFinancieraVM>()
            };

            var hayFiltro = !string.IsNullOrWhiteSpace(filtro.Estado)
                || filtro.FechaDesde.HasValue
                || filtro.FechaHasta.HasValue
                || !string.IsNullOrWhiteSpace(filtro.NumeroOrden)
                || !string.IsNullOrWhiteSpace(filtro.Solicitante);

            if (hayFiltro && (ordenes == null || ordenes.Count == 0))
            {
                var todasEnt = _ordenDAO.ListarFiltrado(null, null, null, null, null, null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
                var todas = todasEnt.Select(MapearOrden).ToList();
                vm.SinResultadosConFiltro = true;
                vm.TotalSinFiltro = todas.Count;
                if (todas.Any())
                {
                    ordenes = todas;
                    estadoFiltro = "TODAS";
                    estadoConsulta = null;
                }
            }

            foreach (var orden in ordenes)
            {
                var solicitudId = 0;
                if (orden != null && !string.IsNullOrWhiteSpace(orden.CodigoSolicitud))
                {
                    int.TryParse(orden.CodigoSolicitud, out solicitudId);
                }
                var pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(solicitudId > 0 ? solicitudId : orden.Id);
                var pago = MapearPago(pagoEnt);
                vm.Ordenes.Add(new OrdenValidacionFinancieraVM
                {
                    Orden = orden,
                    Pago = pago
                });
            }

            vm.Filtro.Estado = estadoFiltro;
            return View(vm);
        }

        public ActionResult DetalleOrden(int id)
        {
            var ordenEnt = _ordenDAO.ObtenerOrdenPorId(id);
            if (ordenEnt == null)
            {
                return HttpNotFound();
            }

            var orden = MapearOrden(ordenEnt);

            var solicitudId = ordenEnt.CodigoSolicitud ?? 0;
            var pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(solicitudId > 0 ? solicitudId : ordenEnt.Id);
            var pago = MapearPago(pagoEnt);

            var historial = new List<HistorialEstado>();
            if (ordenEnt.CodigoSolicitud.HasValue && ordenEnt.CodigoSolicitud.Value > 0)
            {
                historial = new HistorialEstadoBL().ObtenerPorSolicitud(ordenEnt.CodigoSolicitud.Value);
            }

            var vm = new FinancieroOrdenDetalleVM
            {
                Orden = orden,
                Pago = pago,
                Historial = historial
            };

            return View(vm);
        }

        [Authorize(Roles = "FINANCIERO,Financiero,Administrador")]
        public ActionResult DescargarComprobante(int ordenId, int? pagoId)
        {
            var ordenEnt = _ordenDAO.ObtenerOrdenPorId(ordenId);
            if (ordenEnt == null)
            {
                return HttpNotFound();
            }

            var pagos = _ordenDAO.ObtenerPagosPorOrden(ordenId) ?? new List<PagoModelDatos>();
            var pago = pagoId.HasValue ? pagos.FirstOrDefault(p => p.CodigoPago == pagoId.Value) : pagos.FirstOrDefault();
            if (pago == null || string.IsNullOrWhiteSpace(pago.ComprobanteRuta))
            {
                TempData["Error"] = "No se encontr� comprobante para esta orden.";
                return RedirectToAction("DetalleOrden", new { id = ordenId });
            }

            var rutaFisica = ResolveStoredFilePath(pago.ComprobanteRuta);
            if (string.IsNullOrWhiteSpace(rutaFisica) || !System.IO.File.Exists(rutaFisica))
            {
                TempData["Error"] = "El comprobante no est� disponible.";
                return RedirectToAction("DetalleOrden", new { id = ordenId });
            }

            var nombre = Path.GetFileName(rutaFisica);
            var mime = MimeMapping.GetMimeMapping(nombre);
            return File(System.IO.File.ReadAllBytes(rutaFisica), mime, nombre);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarOrden(int id, HttpPostedFileBase facturaArchivo, string observacionesCorreo, string correoDestino)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estadoAnterior = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estadoAnterior != "PROCESADA")
            {
                TempData["Error"] = "Solo se pueden aprobar ordenes en estado PROCESADA.";
                return RedirectToAction("DetalleOrden", new { id });
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
            var pagoPrevio = _ordenDAO.ObtenerUltimoPagoPorOrden(orden.CodigoSolicitud ?? orden.Id);

            try
            {
                if (!TryPrepararFacturaAdjunta(facturaArchivo, out var facturaBytes, out var facturaNombre, out var facturaRutaFisica, out var facturaRutaVirtual, out var errorFactura))
                {
                    TempData["Error"] = "No se pudo procesar la factura adjunta. " + errorFactura;
                    return RedirectToAction("DetalleOrden", new { id });
                }

                var observacionAprobacion = string.IsNullOrWhiteSpace(observacionesCorreo)
                    ? "Aprobado por Finanzas"
                    : observacionesCorreo.Trim();

                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, null, "VALIDADO", user, observacionAprobacion, "FACTURADA", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error aprobando orden Id={id} NumOrden={orden.NumeroOrden}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al aprobar la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("DetalleOrden", new { id });
                }

                RegistrarHistorialEstado(orden, estadoAnterior, "FACTURADA", observacionAprobacion, user);
                RegistrarAuditoriaCambioEstado(orden, pagoPrevio, estadoAnterior, "FACTURADA", "VALIDADO", observacionAprobacion, user, correlationId);

                var rutaFinal = facturaRutaFisica;
                var nombreFinal = facturaNombre;

                if (string.IsNullOrWhiteSpace(rutaFinal))
                {
                    var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(orden);
                    if (pdf != null && pdf.Length > 0)
                    {
                        rutaFinal = GuardarFacturaGenerada(pdf, orden.NumeroOrden, out nombreFinal);
                    }
                }

                if (!EncolarCorreoFactura(orden, pagoPrevio, rutaFinal, nombreFinal, observacionAprobacion, correoDestino, correlationId, out var errorCorreo))
                {
                    TempData["Error"] = "Orden aprobada, pero no se pudo encolar el correo: " + errorCorreo;
                    return RedirectToAction("DetalleOrden", new { id });
                }

                TempData["Success"] = "Orden aprobada. Correo encolado para env�o.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error aprobando orden Id={id} NumOrden={orden.NumeroOrden}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al aprobar la orden.";
            }
            return RedirectToAction("DetalleOrden", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AprobarPago(int id, int? pagoId, HttpPostedFileBase facturaArchivo, string observacionesCorreo, string correoDestino)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estadoAnterior = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estadoAnterior != "PROCESADA" && estadoAnterior != "PENDIENTE")
            {
                TempData["Error"] = "Solo se pueden aprobar pagos en estado PROCESADA o PENDIENTE.";
                return RedirectToAction("DetalleOrden", new { id });
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
            var pagoPrevio = _ordenDAO.ObtenerUltimoPagoPorOrden(orden.CodigoSolicitud ?? orden.Id);

            try
            {
                if (!TryPrepararFacturaAdjunta(facturaArchivo, out var facturaBytes, out var facturaNombre, out var facturaRutaFisica, out var facturaRutaVirtual, out var errorFactura))
                {
                    TempData["Error"] = "No se pudo procesar la factura adjunta. " + errorFactura;
                    return RedirectToAction("DetalleOrden", new { id });
                }

                var observacionAprobacion = string.IsNullOrWhiteSpace(observacionesCorreo)
                    ? "Aprobado por Finanzas"
                    : observacionesCorreo.Trim();

                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, pagoId, "VALIDADO", user, observacionAprobacion, "FACTURADA", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error aprobando pago OrdenId={id}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al aprobar el pago. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("DetalleOrden", new { id });
                }

                RegistrarHistorialEstado(orden, estadoAnterior, "FACTURADA", observacionAprobacion, user);
                RegistrarAuditoriaCambioEstado(orden, pagoPrevio, estadoAnterior, "FACTURADA", "VALIDADO", observacionAprobacion, user, correlationId);

                var rutaFinal = facturaRutaFisica;
                var nombreFinal = facturaNombre;

                if (string.IsNullOrWhiteSpace(rutaFinal))
                {
                    var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(orden);
                    if (pdf != null && pdf.Length > 0)
                    {
                        rutaFinal = GuardarFacturaGenerada(pdf, orden.NumeroOrden, out nombreFinal);
                    }
                }

                if (!EncolarCorreoFactura(orden, pagoPrevio, rutaFinal, nombreFinal, observacionAprobacion, correoDestino, correlationId, out var errorCorreo))
                {
                    TempData["Error"] = "Pago aprobado, pero no se pudo encolar el correo: " + errorCorreo;
                    return RedirectToAction("DetalleOrden", new { id });
                }

                TempData["Success"] = "Pago aprobado. Correo encolado para env�o.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error aprobando pago OrdenId={id}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al aprobar el pago.";
            }
            return RedirectToAction("DetalleOrden", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RechazarOrden(int id, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe ingresar un motivo de rechazo.";
                return RedirectToAction("DetalleOrden", new { id });
            }

            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estadoAnterior = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estadoAnterior != "PROCESADA")
            {
                TempData["Error"] = "Solo se pueden rechazar ordenes en estado PROCESADA.";
                return RedirectToAction("DetalleOrden", new { id });
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";
            var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
            var pagoPrevio = _ordenDAO.ObtenerUltimoPagoPorOrden(orden.CodigoSolicitud ?? orden.Id);

            try
            {
                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, null, EstadoPago.Rechazado, user, motivo, "PENDIENTE", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error rechazando orden Id={id} NumOrden={orden.NumeroOrden}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al rechazar la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("DetalleOrden", new { id });
                }

                RegistrarHistorialEstado(orden, estadoAnterior, "PENDIENTE", motivo, user);
                RegistrarAuditoriaCambioEstado(orden, pagoPrevio, estadoAnterior, "PENDIENTE", EstadoPago.Rechazado, motivo, user, correlationId);

                var comprobanteRuta = pagoPrevio?.RutaComprobante;
                var comprobanteFisico = string.IsNullOrWhiteSpace(comprobanteRuta) ? null : ResolveStoredFilePath(comprobanteRuta);
                var comprobanteNombre = string.IsNullOrWhiteSpace(comprobanteFisico) ? null : Path.GetFileName(comprobanteFisico);

                if (!EncolarCorreoRechazo(orden, pagoPrevio, comprobanteFisico, comprobanteNombre, motivo, correlationId, out var errorCorreo))
                {
                    TempData["Error"] = "Orden rechazada, pero no se pudo encolar el correo: " + errorCorreo;
                    return RedirectToAction("DetalleOrden", new { id });
                }

                TempData["Success"] = "Orden rechazada correctamente. Correo encolado para env�o.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error rechazando orden Id={id} NumOrden={orden.NumeroOrden}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al rechazar la orden.";
            }
            return RedirectToAction("DetalleOrden", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReenviarFactura(int id, string correoDestino, string observacionesCorreo)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = ((orden.Estado ?? "").Trim()).ToUpperInvariant();
            if (estado != "FACTURADA" && estado != "COMPLETADA")
            {
                TempData["Error"] = "S�lo se puede reenviar la factura cuando la orden est� en estado FACTURADA.";
                return RedirectToAction("Detalles", "OrdenRecaudacion", new { id = id });
            }

            try
            {
                var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
                var pagoPrevio = _ordenDAO.ObtenerUltimoPagoPorOrden(orden.CodigoSolicitud ?? orden.Id);

                var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(orden);
                var nombreAdjunto = string.Empty;
                var rutaAdjunto = GuardarFacturaGenerada(pdf, orden.NumeroOrden, out nombreAdjunto);

                if (!EncolarCorreoFactura(orden, pagoPrevio, rutaAdjunto, nombreAdjunto, observacionesCorreo, correoDestino, correlationId, out var errorCorreo))
                {
                    TempData["Error"] = "No se pudo encolar el correo de reenv�o: " + errorCorreo;
                    return RedirectToAction("DetalleOrden", new { id = id });
                }

                TempData["Success"] = "Factura reenviada. Correo encolado para env�o.";
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error reenviando factura Orden={orden.NumeroOrden}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Ocurri� un error al reenviar la factura.";
            }
            return RedirectToAction("DetalleOrden", new { id = id });
        }

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

        private PagoModelDatos MapearPago(CapaDatos.Entidades.Pago p)
        {
            if (p == null) return null;
            return new PagoModelDatos
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

        private bool TryPrepararFacturaAdjunta(HttpPostedFileBase facturaArchivo, out byte[] facturaBytes, out string facturaNombre, out string facturaRutaFisica, out string facturaRutaVirtual, out string error)
        {
            facturaBytes = null;
            facturaNombre = null;
            facturaRutaFisica = null;
            facturaRutaVirtual = null;
            error = null;
            var rutaVirtual = string.Empty;
            var rutaFisica = string.Empty;

            if (facturaArchivo == null || facturaArchivo.ContentLength <= 0)
            {
                return true;
            }

            if (!FileStorageHelper.ValidatePdf(facturaArchivo, out var validateError))
            {
                error = validateError ?? "El archivo de factura no es valido.";
                return false;
            }

            try
            {
                EnsureStorageSubfolder("FacturasFinanciero");
                rutaVirtual = FileStorageHelper.SavePdf(facturaArchivo, "FacturasFinanciero");
                rutaFisica = ResolveStoredFilePath(rutaVirtual);

                if (!System.IO.File.Exists(rutaFisica))
                {
                    CapaNegocio.LogBL.RegistrarError(
                        "Archivo de factura no encontrado despu�s de guardar",
                        $"rutaGuardada={rutaVirtual}; rutaResuelta={rutaFisica}",
                        "FinancieroController");
                    error = "No se encontro el archivo de factura guardado.";
                    return false;
                }

                facturaBytes = System.IO.File.ReadAllBytes(rutaFisica);
                facturaNombre = Path.GetFileName(rutaFisica);
                facturaRutaFisica = rutaFisica;
                facturaRutaVirtual = rutaVirtual;
                return true;
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError(
                    "Error guardando factura adjunta",
                    $"rutaGuardada={rutaVirtual}; rutaResuelta={rutaFisica}; ex={ex}",
                    "FinancieroController");
                error = "Error al guardar archivo de factura.";
                return false;
            }
        }

        private static void EnsureStorageSubfolder(string subfolder)
        {
            var basePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/AOCR");
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return;
            }

            var target = Path.Combine(basePath, (subfolder ?? string.Empty).TrimStart('~', '/', '\\'));
            if (!Directory.Exists(target))
            {
                Directory.CreateDirectory(target);
            }
        }

        private string ResolveStoredFilePath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
            {
                return string.Empty;
            }

            if (storedPath.StartsWith("~"))
            {
                return Server.MapPath(storedPath);
            }

            if (Path.IsPathRooted(storedPath))
            {
                return storedPath;
            }

            var basePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/AOCR");
            return Path.Combine(basePath, storedPath.TrimStart('/', '\\'));
        }

        private bool EncolarCorreoFactura(
            CapaDatos.Entidades.OrdenRecaudacion orden,
            CapaDatos.Entidades.Pago pago,
            string facturaRutaFisica,
            string facturaNombre,
            string observacionesCorreo,
            string correoDestino,
            string correlationId,
            out string error)
        {
            error = null;
            var destinatarios = ObtenerDestinatariosFactura(orden, correoDestino);
            if (destinatarios.Count == 0)
            {
                error = "No hay destinatarios v�lidos. Ingrese un correo destino o registre el correo del contribuyente.";
                CapaNegocio.LogBL.RegistrarError($"No hay destinatarios v�lidos para factura. Orden={orden?.NumeroOrden}", $"correoDestino={correoDestino}; contribuyente={orden?.EmailContribuyente}", "FinancieroController");
                return false;
            }

            if (string.IsNullOrWhiteSpace(facturaRutaFisica) || !System.IO.File.Exists(facturaRutaFisica))
            {
                error = "No se encontr� la factura para adjuntar.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(facturaNombre))
            {
                facturaNombre = Path.GetFileName(facturaRutaFisica);
            }

            var vm = new OrdenAprobadaEmailVM
            {
                NumeroOrden = orden?.NumeroOrden ?? "N/A",
                NumeroSolicitud = orden?.CodigoSolicitud?.ToString() ?? "N/A",
                NombreContribuyente = orden?.NombreContribuyente ?? "Usuario",
                RucCedula = orden?.RucCedula ?? "N/A",
                Total = orden?.Total ?? 0m,
                Observaciones = observacionesCorreo,
                FechaAprobacion = DateTime.Now,
                MetodoPago = pago?.MetodoPago ?? "N/A",
                NumeroComprobante = pago?.NumeroComprobante ?? pago?.NumeroFactura ?? "N/A"
            };

            var cuerpo = RazorViewRenderer.RenderPartialViewToString(ControllerContext, "EmailTemplates/OrdenAprobada", vm);
            var asunto = $"Factura aprobada - Orden {orden?.NumeroOrden}";

            foreach (var correo in destinatarios)
            {
                var item = new EmailQueueItem
                {
                    Para = correo,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    SolicitudId = orden?.CodigoSolicitud,
                    OrdenId = orden?.Id,
                    NumeroOrden = orden?.NumeroOrden,
                    TipoNotificacion = "OrdenFacturada",
                    CorrelationId = correlationId,
                    AdjuntoRuta = facturaRutaFisica,
                    AdjuntoNombre = facturaNombre,
                    AdjuntoMimeType = "application/pdf",
                    MaxIntentos = 3
                };

                if (!EncolarCorreo(item, out var errItem))
                {
                    error = errItem;
                    return false;
                }
            }

            return true;
        }

        private bool EncolarCorreoRechazo(
            CapaDatos.Entidades.OrdenRecaudacion orden,
            CapaDatos.Entidades.Pago pago,
            string comprobanteRutaFisica,
            string comprobanteNombre,
            string motivo,
            string correlationId,
            out string error)
        {
            error = null;
            var destinatarios = ObtenerDestinatariosFactura(orden, null);
            if (destinatarios.Count == 0)
            {
                error = "No hay destinatarios v�lidos para notificar el rechazo.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(comprobanteRutaFisica) && string.IsNullOrWhiteSpace(comprobanteNombre))
            {
                comprobanteNombre = Path.GetFileName(comprobanteRutaFisica);
            }

            var vm = new OrdenRechazadaEmailVM
            {
                NumeroOrden = orden?.NumeroOrden ?? "N/A",
                NumeroSolicitud = orden?.CodigoSolicitud?.ToString() ?? "N/A",
                NombreContribuyente = orden?.NombreContribuyente ?? "Usuario",
                RucCedula = orden?.RucCedula ?? "N/A",
                Total = orden?.Total ?? 0m,
                Motivo = motivo,
                FechaRechazo = DateTime.Now,
                MetodoPago = pago?.MetodoPago ?? "N/A",
                NumeroComprobante = pago?.NumeroComprobante ?? pago?.NumeroFactura ?? "N/A"
            };

            var cuerpo = RazorViewRenderer.RenderPartialViewToString(ControllerContext, "EmailTemplates/OrdenRechazada", vm);
            var asunto = $"Orden rechazada - {orden?.NumeroOrden}";

            foreach (var correo in destinatarios)
            {
                var item = new EmailQueueItem
                {
                    Para = correo,
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    SolicitudId = orden?.CodigoSolicitud,
                    OrdenId = orden?.Id,
                    NumeroOrden = orden?.NumeroOrden,
                    TipoNotificacion = "OrdenRechazada",
                    CorrelationId = correlationId,
                    AdjuntoRuta = string.IsNullOrWhiteSpace(comprobanteRutaFisica) ? null : comprobanteRutaFisica,
                    AdjuntoNombre = comprobanteNombre,
                    AdjuntoMimeType = string.IsNullOrWhiteSpace(comprobanteRutaFisica) ? null : "application/pdf",
                    MaxIntentos = 3
                };

                if (!EncolarCorreo(item, out var errItem))
                {
                    error = errItem;
                    return false;
                }
            }

            return true;
        }

        private bool EncolarCorreo(EmailQueueItem item, out string error)
        {
            error = null;
            try
            {
                var queue = CreateQueueService();
                var id = queue.EncolarAsync(item).GetAwaiter().GetResult();
                if (id <= 0)
                {
                    error = "No se pudo registrar el correo en la cola.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                CapaNegocio.LogBL.RegistrarError("Error encolando correo", ex.ToString(), "FinancieroController");
                return false;
            }
        }

        private List<string> ObtenerDestinatariosFactura(CapaDatos.Entidades.OrdenRecaudacion orden, string correoDestino)
        {
            var lista = new List<string>();

            AgregarCorreoSiValido(lista, correoDestino);
            AgregarCorreoSiValido(lista, orden?.EmailContribuyente);
            AgregarCorreoSiValido(lista, orden?.Correo);

            var adicional = ConfigurationManager.AppSettings["Financiero:FacturaCorreoAdicional"];
            if (string.IsNullOrWhiteSpace(adicional))
            {
                adicional = ConfigurationManager.AppSettings["FinancieroFacturaCorreoAdicional"];
            }

            foreach (var correo in SepararCorreos(adicional))
            {
                AgregarCorreoSiValido(lista, correo);
            }

            return lista.Distinct().ToList();
        }

        private static IEnumerable<string> SepararCorreos(string correos)
        {
            if (string.IsNullOrWhiteSpace(correos))
            {
                return new string[0];
            }

            return correos
                .Split(new[] { ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim());
        }

        private static void AgregarCorreoSiValido(ICollection<string> lista, string correo)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                return;
            }

            try
            {
                var normalized = new MailAddress(correo.Trim()).Address;
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    lista.Add(normalized);
                }
            }
            catch
            {
            }
        }

        private EmailQueueService CreateQueueService()
        {
            var cs = GetPostgresConnectionString();
            return new EmailQueueService(cs);
        }

        private IAuditService CreateAuditService()
        {
            var cs = GetPostgresConnectionString();
            return new AuditService(cs);
        }

        private string GetPostgresConnectionString()
        {
            var secure = new SecureConfigurationService();
            var cs = secure.GetConnectionString("PostgreSQL");
            if (string.IsNullOrWhiteSpace(cs))
            {
                cs = ConfigurationManager.ConnectionStrings["PostgreSQL"]?.ConnectionString
                    ?? ConfigurationManager.ConnectionStrings["AOCRConnection"]?.ConnectionString
                    ?? ConfigurationManager.ConnectionStrings["DefaultConnection"]?.ConnectionString;
            }
            return cs ?? string.Empty;
        }

        private void RegistrarHistorialEstado(CapaDatos.Entidades.OrdenRecaudacion orden, string estadoAnterior, string estadoNuevo, string observaciones, string usuario)
        {
            try
            {
                if (orden == null || !orden.CodigoSolicitud.HasValue || orden.CodigoSolicitud.Value <= 0)
                {
                    return;
                }

                var historial = new HistorialEstado
                {
                    CodigoSolicitud = orden.CodigoSolicitud.Value,
                    EstadoAnterior = estadoAnterior,
                    EstadoNuevo = estadoNuevo,
                    Observaciones = observaciones,
                    CodigoUsuario = GetCodigoUsuarioSesion() ?? 0,
                    FechaCambio = DateTime.Now
                };

                new HistorialEstadoBL().RegistrarCambioEstado(historial, out _);
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError("Error registrando historial de estado", ex.ToString(), "FinancieroController");
            }
        }

        private void RegistrarAuditoriaCambioEstado(
            CapaDatos.Entidades.OrdenRecaudacion orden,
            CapaDatos.Entidades.Pago pago,
            string estadoAnteriorOrden,
            string estadoNuevoOrden,
            string estadoNuevoPago,
            string motivo,
            string usuario,
            string correlationId)
        {
            try
            {
                var audit = CreateAuditService();
                var ip = Request?.UserHostAddress;

                audit.RegistrarCambioEstadoAsync(new CambioEstadoAudit
                {
                    TipoEntidad = "ORDEN",
                    EntidadId = orden?.Id ?? 0,
                    NumeroReferencia = orden?.NumeroOrden,
                    EstadoAnterior = estadoAnteriorOrden,
                    EstadoNuevo = estadoNuevoOrden,
                    Usuario = usuario,
                    Motivo = motivo,
                    IpOrigen = ip,
                    CorrelationId = correlationId
                }).GetAwaiter().GetResult();

                if (pago != null)
                {
                    audit.RegistrarCambioEstadoAsync(new CambioEstadoAudit
                    {
                        TipoEntidad = "PAGO",
                        EntidadId = pago.Id,
                        NumeroReferencia = pago.NumeroComprobante ?? pago.NumeroFactura,
                        EstadoAnterior = pago.Estado,
                        EstadoNuevo = estadoNuevoPago,
                        Usuario = usuario,
                        Motivo = motivo,
                        IpOrigen = ip,
                        CorrelationId = correlationId
                    }).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError("Error registrando auditor�a financiera", ex.ToString(), "FinancieroController");
            }
        }

        private string GuardarFacturaGenerada(byte[] pdfBytes, string numeroOrden, out string nombreArchivo)
        {
            nombreArchivo = null;
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                return null;
            }

            var basePath = FileStorageHelper.GetPhysicalBasePath("~/App_Data/AOCR");
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return null;
            }

            var safeOrden = SanitizeFileName(string.IsNullOrWhiteSpace(numeroOrden) ? "Orden" : numeroOrden);
            nombreArchivo = $"Factura_{safeOrden}_{DateTime.Now:yyyyMMddHHmmss}.pdf";

            var folder = Path.Combine(basePath, "FacturasFinanciero");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var fullPath = Path.Combine(folder, nombreArchivo);
            System.IO.File.WriteAllBytes(fullPath, pdfBytes);
            return fullPath;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sanitized = value;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }
            return sanitized;
        }

        private int? GetCodigoUsuarioSesion()
        {
            try
            {
                var raw = Session?["IdUsuario"] ?? Session?["UserId"];
                if (raw != null && int.TryParse(raw.ToString(), out var id))
                {
                    return id;
                }
            }
            catch
            {
            }

            return null;
        }
        #endregion
    }
}








