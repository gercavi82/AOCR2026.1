using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio.Helpers;
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

            var estadoConsulta = estadoFiltro == "TODAS" ? null : estadoFiltro;
            var ordenesEnt = _ordenDAO.ObtenerTodasLasOrdenes(estadoConsulta) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
            var ordenes = ordenesEnt.Select(MapearOrden).ToList();

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
        public ActionResult AprobarOrden(int id, HttpPostedFileBase facturaArchivo, string observacionesCorreo, string correoDestino)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estado != "PROCESADA")
            {
                TempData["Error"] = "Solo se pueden aprobar ordenes en estado PROCESADA.";
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";

            try
            {
                if (!TryPrepararFacturaAdjunta(facturaArchivo, out var facturaBytes, out var facturaNombre, out var errorFactura))
                {
                    TempData["Error"] = "No se pudo procesar la factura adjunta. " + errorFactura;
                    return RedirectToAction("Index");
                }

                var observacionAprobacion = string.IsNullOrWhiteSpace(observacionesCorreo)
                    ? "Aprobado por Finanzas"
                    : observacionesCorreo.Trim();

                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, null, "VALIDADO", user, observacionAprobacion, "FACTURADA", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error aprobando orden Id={id} NumOrden={orden.NumeroOrden}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al aprobar la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("Index");
                }

                try
                {
                    var adjunto = facturaBytes;
                    var nombreAdjunto = facturaNombre;

                    if (adjunto == null || adjunto.Length == 0)
                    {
                        adjunto = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(orden);
                        nombreAdjunto = $"Factura_{orden.NumeroOrden}.pdf";
                    }

                    string errorCorreo;
                    var destinatarios = ObtenerDestinatariosFactura(orden, correoDestino);
                    var enviado = EnviarCorreoFactura(orden, adjunto, nombreAdjunto, observacionAprobacion, correoDestino, out errorCorreo);
                    if (!enviado)
                    {
                        TempData["Error"] = "Orden aprobada, pero no se pudo enviar el correo: " + errorCorreo;
                        return RedirectToAction("Index");
                    }
                }
                catch (System.Exception exPdf)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error generando/mandando factura Orden={orden.NumeroOrden}", exPdf.ToString(), "FinancieroController");
                    TempData["Error"] = "Orden aprobada, pero ocurrio un error al enviar el correo.";
                    return RedirectToAction("Index");
                }

                // Success message already set with destinatarios when correo fue enviado.
                // (Se mantiene por compatibilidad si no se ejecutó el bloque anterior.)
                if (TempData["Success"] == null) TempData["Success"] = "Orden aprobada y factura enviada.";
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
        public ActionResult AprobarPago(int id, int? pagoId, HttpPostedFileBase facturaArchivo, string observacionesCorreo, string correoDestino)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var user = User?.Identity?.Name ?? "FINANCIERO";

            try
            {
                if (!TryPrepararFacturaAdjunta(facturaArchivo, out var facturaBytes, out var facturaNombre, out var errorFactura))
                {
                    TempData["Error"] = "No se pudo procesar la factura adjunta. " + errorFactura;
                    return RedirectToAction("Index");
                }

                var observacionAprobacion = string.IsNullOrWhiteSpace(observacionesCorreo)
                    ? "Aprobado por Finanzas"
                    : observacionesCorreo.Trim();

                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, pagoId, "VALIDADO", user, observacionAprobacion, "FACTURADA", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error aprobando pago OrdenId={id}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al aprobar el pago. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("Index");
                }

                try
                {
                    var adjunto = facturaBytes;
                    var nombreAdjunto = facturaNombre;

                    if (adjunto == null || adjunto.Length == 0)
                    {
                        adjunto = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(orden);
                        nombreAdjunto = $"Factura_{orden.NumeroOrden}.pdf";
                    }

                    string errorCorreo;
                    var destinatarios = ObtenerDestinatariosFactura(orden, correoDestino);
                    var enviado = EnviarCorreoFactura(orden, adjunto, nombreAdjunto, observacionAprobacion, correoDestino, out errorCorreo);
                    if (!enviado)
                    {
                        TempData["Error"] = "Pago aprobado, pero no se pudo enviar el correo: " + errorCorreo;
                        return RedirectToAction("Index");
                    }
                }
                catch (System.Exception exPdf)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error adjuntando/mandando factura Orden={orden.NumeroOrden}", exPdf.ToString(), "FinancieroController");
                    TempData["Error"] = "Pago aprobado, pero ocurrio un error al enviar el correo.";
                    return RedirectToAction("Index");
                }

                TempData["Success"] = "Pago aprobado y factura enviada a: " + string.Join(", ", ObtenerDestinatariosFactura(orden, correoDestino));
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error aprobando pago OrdenId={id}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al aprobar el pago.";
            }
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
                TempData["Error"] = "Solo se pueden rechazar ordenes en estado PROCESADA.";
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";

            try
            {
                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, null, "ANULADO", user, motivo, "PENDIENTE", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error rechazando orden Id={id} NumOrden={orden.NumeroOrden}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al rechazar la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("Index");
                }

                try
                {
                    new EmailService().EnviarNotificacionRechazo(orden, motivo);
                }
                catch (System.Exception exMail)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error notificando rechazo Orden={orden.NumeroOrden}", exMail.ToString(), "FinancieroController");
                }

                TempData["Success"] = "Orden rechazada correctamente.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error rechazando orden Id={id} NumOrden={orden.NumeroOrden}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al rechazar la orden.";
            }
            return RedirectToAction("Index");
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

        private bool TryPrepararFacturaAdjunta(HttpPostedFileBase facturaArchivo, out byte[] facturaBytes, out string facturaNombre, out string error)
        {
            facturaBytes = null;
            facturaNombre = null;
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
                        "Archivo de factura no encontrado después de guardar",
                        $"rutaGuardada={rutaVirtual}; rutaResuelta={rutaFisica}",
                        "FinancieroController");
                    error = "No se encontro el archivo de factura guardado.";
                    return false;
                }

                facturaBytes = System.IO.File.ReadAllBytes(rutaFisica);
                facturaNombre = Path.GetFileName(rutaFisica);
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

        private bool EnviarCorreoFactura(
            CapaDatos.Entidades.OrdenRecaudacion orden,
            byte[] facturaBytes,
            string facturaNombre,
            string observacionesCorreo,
            string correoDestino,
            out string error)
        {
            error = null;
            var ordenRef = orden?.NumeroOrden ?? "N/A";
            var destinatarios = ObtenerDestinatariosFactura(orden, correoDestino);
            if (destinatarios.Count == 0)
            {
                error = "No hay destinatarios validos. Ingrese un correo destino o registre el correo del contribuyente.";
                CapaNegocio.LogBL.RegistrarError($"No hay destinatarios validos para factura. Orden={orden?.NumeroOrden}", $"correoDestino={correoDestino}; contribuyente={orden?.EmailContribuyente}", "FinancieroController");
                return false;
            }

            var asunto = $"Factura aprobada - Orden {orden?.NumeroOrden}";
            var cuerpo = ConstruirCuerpoCorreoFactura(orden, observacionesCorreo);
            var emailService = new EmailService();

            foreach (var correo in destinatarios)
            {
                var adjuntoInfo = (facturaBytes != null && facturaBytes.Length > 0)
                    ? $"adjunto={facturaNombre}; bytes={facturaBytes.Length}"
                    : "adjunto=none";
                CapaNegocio.LogBL.RegistrarInfo(
                    $"ENVIO_FACTURA | Orden={ordenRef} | Destinatario={correo} | {adjuntoInfo}",
                    "FinancieroController");

                var result = emailService.EnviarAsync(correo, "Destinatario", asunto, cuerpo, facturaBytes, facturaNombre).GetAwaiter().GetResult();
                if (!result.Success)
                {
                    error = $"No se pudo enviar factura a {correo}. {result.Error}";
                    CapaNegocio.LogBL.RegistrarError($"Fallo envio de factura. Orden={orden?.NumeroOrden}", error, "FinancieroController");
                    return false;
                }

                // Registrar MessageId (si el servicio lo devuelve) para facilitar trazabilidad en el servidor SMTP
                var msgId = string.IsNullOrWhiteSpace(result.MessageId) ? "-" : result.MessageId;
                CapaNegocio.LogBL.RegistrarInfo(
                    $"ENVIO_FACTURA_OK | Orden={ordenRef} | Destinatario={correo} | MessageId={msgId}",
                    "FinancieroController");
            }

            return true;
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

        private static string ConstruirCuerpoCorreoFactura(CapaDatos.Entidades.OrdenRecaudacion orden, string observacionesCorreo)
        {
            var numeroOrden = orden?.NumeroOrden ?? "N/A";
            var nombre = orden?.NombreContribuyente ?? "Usuario";
            var total = (orden?.Total ?? 0m).ToString("N2");
            var observacion = string.IsNullOrWhiteSpace(observacionesCorreo) ? "Sin observaciones." : observacionesCorreo.Trim();

            var sb = new StringBuilder();
            sb.Append("<p>Estimado/a ");
            sb.Append(HttpUtility.HtmlEncode(nombre));
            sb.Append(".</p>");
            sb.Append("<p>Se ha finalizado la validacion financiera de su orden de recaudacion.</p>");
            sb.Append("<ul>");
            sb.Append("<li><strong>Orden:</strong> ");
            sb.Append(HttpUtility.HtmlEncode(numeroOrden));
            sb.Append("</li>");
            sb.Append("<li><strong>Estado:</strong> FACTURADA</li>");
            sb.Append("<li><strong>Total:</strong> $");
            sb.Append(HttpUtility.HtmlEncode(total));
            sb.Append("</li>");
            sb.Append("<li><strong>Observaciones de Finanzas:</strong> ");
            sb.Append(HttpUtility.HtmlEncode(observacion));
            sb.Append("</li>");
            sb.Append("</ul>");
            sb.Append("<p>Se adjunta la factura en este correo.</p>");
            sb.Append("<p>Saludos,<br/>Sistema AOCR</p>");
            return sb.ToString();
        }
        #endregion
    }
}
