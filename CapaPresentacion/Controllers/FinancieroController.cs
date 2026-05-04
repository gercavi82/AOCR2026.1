using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using EmailServiceData = CapaDatos.Services.EmailService;
using CapaNegocio.Services;
using CapaPresentacion.Models;
using CapaPresentacion.Filters;
using CapaPresentacion.Infrastructure;
using CapaUtilidades;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Financiero,CoordinadorFinanciero,Administrador")]
    public class FinancieroController : Controller
    {
        private readonly OrdenRecaudacionDAO _ordenDAO = new OrdenRecaudacionDAO();

        [RequirePermission("FIN_VER_PAGOS")]
        public ActionResult Dashboard(string estado = "TODAS")
        {
            return ConstruirDashboardFinanciero(estado);
        }

        [RequirePermission("FIN_VER_PAGOS")]
        public ActionResult Index(string estado = "TODAS")
        {
            return ConstruirDashboardFinanciero(estado);
        }

        private ActionResult ConstruirDashboardFinanciero(string estado)
        {
            var estadoFiltro = NormalizarFiltroDashboard(estado);
            var ordenesEnt = _ordenDAO.ObtenerTodasLasOrdenes(null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
            var ordenes = ordenesEnt.Select(MapearOrden).ToList();

            if (!string.Equals(estadoFiltro, "TODAS", StringComparison.OrdinalIgnoreCase))
            {
                ordenes = ordenes
                    .Where(o => CoincideEstadoDashboard(o != null ? o.Estado : null, estadoFiltro))
                    .ToList();
            }

            var vms = new List<OrdenValidacionFinancieraVM>();
            foreach (var orden in ordenes)
            {
                // El DAO espera id de orden para resolver internamente el codigo_solicitud correcto.
                var pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(orden?.Id ?? 0);
                var pago = MapearPago(pagoEnt);
                var factura = _ordenDAO.ObtenerFacturaPagoPorOrden(orden?.Id ?? 0);
                var fr3Estado = factura?.Fr3Estado;
                var fr3Numero = factura?.Fr3Numero;
                var fr3Error = factura?.Fr3Error;
                var tieneFacturaRegistrada = factura != null && !string.IsNullOrWhiteSpace(factura.NumeroFactura);
                var puedeReintentarFr3 = FacturacionAS400Service.IsEnabled() &&
                                         tieneFacturaRegistrada &&
                                         (string.Equals(fr3Estado, "FR3_ERROR", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(fr3Estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase) ||
                                          (string.IsNullOrWhiteSpace(fr3Estado) &&
                                           string.Equals((orden?.Estado ?? "").Trim(), "FACTURADA", StringComparison.OrdinalIgnoreCase)));

                vms.Add(new OrdenValidacionFinancieraVM
                {
                    Orden = orden,
                    Pago = pago,
                    Fr3Estado = fr3Estado,
                    Fr3Numero = fr3Numero,
                    Fr3Error = fr3Error,
                    PuedeReintentarFr3 = puedeReintentarFr3
                });
            }

            ViewBag.EstadoFiltro = estadoFiltro;
            return View("Index", vms);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult AprobarOrden(int id)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = EstadoOrden.NormalizarEstado(orden.Estado);
            if (estado != EstadoOrden.EnRevisionFinanciera)
            {
                TempData["Error"] = "Solo se pueden aprobar ordenes en revision financiera.";
                return RedirectToAction("Index");
            }

            var comprobanteService = new ComprobanteService();
            if (!comprobanteService.ExisteComprobanteValido(id, out var mensajeComprobante))
            {
                TempData["Error"] = mensajeComprobante;
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
                    new EmailServiceData().EnviarFacturaGenerada(orden, pdf);
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
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult AprobarPago(int id, int? pagoId)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = EstadoOrden.NormalizarEstado(orden.Estado);
            if (estado != EstadoOrden.EnRevisionFinanciera)
            {
                TempData["Error"] = "Solo se pueden aprobar pagos de ordenes en revision financiera.";
                return RedirectToAction("Index");
            }

            var comprobanteService = new ComprobanteService();
            if (!comprobanteService.ExisteComprobanteValido(id, out var mensajeComprobante))
            {
                TempData["Error"] = mensajeComprobante;
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";

            try
            {
                // Actualizar pago y estado en transacciÃ³n
                if (!_ordenDAO.ActualizarPagoYEstadoTransaccional(id, pagoId, "VALIDADO", user, "Aprobado por Finanzas", "FACTURADA", out var err))
                {
                    CapaNegocio.LogBL.RegistrarError($"Error aprobando pago OrdenId={id}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al aprobar el pago. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("Index");
                }

                try
                {
                    var ordenActualizada = _ordenDAO.ObtenerOrdenPorId(id) ?? orden;
                    var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(ordenActualizada);
                    new EmailServiceData().EnviarFacturaGenerada(ordenActualizada, pdf);
                }
                catch (System.Exception exPdf)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error generando/mandando factura al aprobar pago OrdenId={id}", exPdf.ToString(), "FinancieroController");
                }

                TempData["Success"] = "Pago aprobado y orden facturada.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error aprobando pago OrdenId={id}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al aprobar el pago.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Financiero")]
        [ValidateAntiForgeryToken]
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult AprobarPagoConFactura(AprobarPagoConFacturaVM model, HttpPostedFileBase facturaFile)
        {
            CapaNegocio.LogBL.RegistrarInfo(
                string.Format("Solicitud AprobarPagoConFactura recibida. OrdenId={0}, PagoId={1}",
                    model != null ? model.OrdenId.ToString() : "null",
                    model != null && model.PagoId.HasValue ? model.PagoId.Value.ToString() : "null"),
                "FinancieroController");

            if (model == null)
            {
                return JsonErrorLogged("Solicitud inválida.");
            }

            if (model.OrdenId <= 0)
            {
                return JsonErrorLogged("Orden inválida.");
            }

            var orden = _ordenDAO.ObtenerOrdenPorId(model.OrdenId);
            if (orden == null)
            {
                return JsonErrorLogged("Orden no encontrada.");
            }

            var estadoOrden = EstadoOrden.NormalizarEstado(orden.Estado);
            var ordenYaAprobada = estadoOrden == EstadoOrden.Facturada ||
                                  estadoOrden == EstadoOrden.Completada ||
                                  estadoOrden == EstadoOrden.Pagada;
            if (estadoOrden != EstadoOrden.EnRevisionFinanciera && !ordenYaAprobada)
            {
                return JsonErrorLogged("Solo se puede registrar factura para ordenes en revision financiera.");
            }

            if (!ordenYaAprobada)
            {
                var comprobanteService = new ComprobanteService();
                if (!comprobanteService.ExisteComprobanteValido(model.OrdenId, out var mensajeComprobante))
                {
                    return JsonErrorLogged(mensajeComprobante);
                }
            }

            if (string.IsNullOrWhiteSpace(model.NumeroFactura))
            {
                return JsonErrorLogged("El número de factura es obligatorio.");
            }

            if (facturaFile == null || facturaFile.ContentLength <= 0)
            {
                return JsonErrorLogged("Debe adjuntar la factura (PDF/JPG/PNG).");
            }

            DateTime fechaEmision;
            if (!TryParseDateFlexible(model.FechaEmision, out fechaEmision))
            {
                return JsonErrorLogged("La fecha de emisión no es válida.");
            }

            decimal subtotal;
            if (!TryParseDecimalFlexible(model.Subtotal, out subtotal) || subtotal < 0m)
            {
                return JsonErrorLogged("El subtotal no es válido.");
            }

            decimal iva;
            if (!TryParseDecimalFlexible(model.Iva, out iva) || iva < 0m)
            {
                return JsonErrorLogged("El IVA no es válido.");
            }

            decimal total;
            if (!TryParseDecimalFlexible(model.Total, out total) || total <= 0m)
            {
                return JsonErrorLogged("El total no es válido.");
            }

            var totalCalculado = Math.Round(subtotal + iva, 2);
            if (Math.Abs(Math.Round(total, 2) - totalCalculado) > 0.05m)
            {
                return JsonErrorLogged("El total no coincide con subtotal + IVA.");
            }

            var uploadOptions = new FileUploadOptions
            {
                BasePath = Server.MapPath("~/App_Data/AOCR/Facturas"),
                Subfolder = model.OrdenId.ToString(),
                AllowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" },
                AllowedContentTypes = null,
                MaxSizeMb = 10,
                ValidateMagicBytes = true
            };

            string uploadError;
            FileUploadResult uploadResult;
            if (!FileUploadService.TrySave(facturaFile, uploadOptions, out uploadResult, out uploadError))
            {
                return JsonErrorLogged(uploadError ?? "No se pudo guardar el archivo de factura.");
            }

            var virtualFilePath = string.Format("~/App_Data/AOCR/Facturas/{0}/{1}", model.OrdenId, uploadResult.StoredName)
                .Replace("\\", "/");

            try
            {
                var usuario = User != null && User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name)
                    ? User.Identity.Name
                    : "FINANCIERO";

                string error;
                bool idempotente;
                string advertencia;

                var aprobado = _ordenDAO.AprobarPagoConFacturaTransaccional(
                    model.OrdenId,
                    model.PagoId,
                    usuario,
                    model.NumeroFactura,
                    model.AutorizacionFactura,
                    fechaEmision,
                    subtotal,
                    iva,
                    total,
                    model.Observaciones,
                    uploadResult.StoredName,
                    string.IsNullOrWhiteSpace(uploadResult.ContentType) ? facturaFile.ContentType : uploadResult.ContentType,
                    uploadResult.Size,
                    virtualFilePath,
                    out error,
                    out idempotente,
                    out advertencia);

                if (!aprobado)
                {
                    CapaNegocio.Helpers.FileStorageHelper.DeleteFile(virtualFilePath);
                    CapaNegocio.LogBL.RegistrarError(
                        string.Format("Error aprobando pago con factura. OrdenId={0}", model.OrdenId),
                        error ?? "n/a",
                        "FinancieroController");
                    return JsonErrorLogged(string.IsNullOrWhiteSpace(error) ? "No se pudo aprobar el pago." : error);
                }

                if (idempotente)
                {
                    // Si la orden ya estaba procesada, descartamos el nuevo archivo subido para no dejar huérfanos.
                    CapaNegocio.Helpers.FileStorageHelper.DeleteFile(virtualFilePath);
                }

                var mensaje = idempotente
                    ? "La orden ya estaba aprobada con factura. No se duplicaron registros."
                    : "Pago aprobado y factura registrada correctamente.";

                string advertenciaAs400 = null;
                if (!idempotente && FacturacionAS400Service.IsEnabled())
                {
                    var as400Service = new FacturacionAS400Service();
                    if (!as400Service.TryRegistrarFactura(
                        model.OrdenId,
                        model.PagoId,
                        model.NumeroFactura,
                        model.AutorizacionFactura,
                        fechaEmision,
                        subtotal,
                        iva,
                        total,
                        model.Observaciones,
                        usuario,
                        out advertenciaAs400))
                    {
                        CapaNegocio.LogBL.RegistrarError(
                            string.Format("Error registrando factura en AS400. OrdenId={0}", model.OrdenId),
                            advertenciaAs400 ?? "n/a",
                            "FinancieroController");
                    }
                }

                CapaNegocio.LogBL.RegistrarInfo(
                    string.Format("AprobarPagoConFactura OK. OrdenId={0}, PagoId={1}, Idempotente={2}, Advertencia={3}",
                        model.OrdenId,
                        model.PagoId.HasValue ? model.PagoId.Value.ToString() : "null",
                        idempotente,
                        string.IsNullOrWhiteSpace(advertencia) ? "N/A" : advertencia),
                    "FinancieroController");

                return Json(new
                {
                    ok = true,
                    message = mensaje,
                    idempotent = idempotente,
                    warning = string.IsNullOrWhiteSpace(advertenciaAs400)
                        ? advertencia
                        : (string.IsNullOrWhiteSpace(advertencia) ? advertenciaAs400 : (advertencia + " | " + advertenciaAs400))
                });
            }
            catch (Exception ex)
            {
                CapaNegocio.Helpers.FileStorageHelper.DeleteFile(virtualFilePath);
                CapaNegocio.LogBL.RegistrarError(
                    string.Format("Excepción aprobando pago con factura. OrdenId={0}", model.OrdenId),
                    ex.ToString(),
                    "FinancieroController");
                return JsonErrorLogged("Error interno al aprobar el pago con factura.", 500);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryTokenAjax]
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult AprobarYEnviarAS400(int ordenId)
        {
            if (ordenId <= 0)
            {
                var rawOrdenId = (Request["ordenId"] ?? Request["id"] ?? string.Empty).Trim();
                if (!int.TryParse(rawOrdenId, out ordenId) || ordenId <= 0)
                {
                    LogAprobarYEnviarAs400Request("ORDEN_INVALIDA", rawOrdenId, null);
                    return JsonErrorLogged("Orden invalida.");
                }
            }

            var orden = _ordenDAO.ObtenerOrdenPorId(ordenId);
            if (orden == null)
            {
                LogAprobarYEnviarAs400Request("ORDEN_NO_ENCONTRADA", ordenId.ToString(CultureInfo.InvariantCulture), null);
                return JsonErrorLogged("Orden no encontrada.");
            }

            var estado = EstadoOrden.NormalizarEstado(orden.Estado);
            var permiteAprobar = estado == EstadoOrden.EnRevisionFinanciera;
            var yaAprobada = estado == EstadoOrden.Facturada || estado == EstadoOrden.Completada || estado == EstadoOrden.Pagada;
            if (!permiteAprobar && !yaAprobada)
            {
                LogAprobarYEnviarAs400Request("ESTADO_NO_PERMITIDO", ordenId.ToString(CultureInfo.InvariantCulture), orden.Estado);
                return JsonErrorLogged("Solo se pueden aprobar ordenes en revision financiera. Estado actual: " + (orden.Estado ?? "N/D"));
            }

            if (permiteAprobar)
            {
                var comprobanteService = new ComprobanteService();
                if (!comprobanteService.ExisteComprobanteValido(ordenId, out var mensajeComprobante))
                {
                    LogAprobarYEnviarAs400Request("COMPROBANTE_INVALIDO", ordenId.ToString(CultureInfo.InvariantCulture), mensajeComprobante);
                    return JsonErrorLogged(mensajeComprobante);
                }
            }

            var usuario = User != null && User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name)
                ? User.Identity.Name
                : "FINANCIERO";

            try
            {
                var pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(ordenId);
                var pagoId = pagoEnt != null ? (int?)pagoEnt.Id : null;
                var aprobacionIdempotente = false;

                if (permiteAprobar)
                {
                    string errAprobacion;
                    var aprobado = _ordenDAO.ActualizarPagoYEstadoTransaccional(
                        ordenId, pagoId, "VALIDADO", usuario, "Aprobado por Finanzas", "FACTURADA", out errAprobacion);

                    if (!aprobado)
                    {
                        var ordenRevalidada = _ordenDAO.ObtenerOrdenPorId(ordenId);
                        var estadoRevalidado = ((ordenRevalidada != null ? ordenRevalidada.Estado : null) ?? string.Empty)
                            .Trim();
                        var estadoRevalidadoNormalizado = EstadoOrden.NormalizarEstado(estadoRevalidado);
                        if (estadoRevalidadoNormalizado == EstadoOrden.Facturada ||
                            estadoRevalidadoNormalizado == EstadoOrden.Completada ||
                            estadoRevalidadoNormalizado == EstadoOrden.Pagada)
                        {
                            aprobacionIdempotente = true;
                            orden = ordenRevalidada ?? orden;
                            CapaNegocio.LogBL.RegistrarInfo(
                                string.Format("AprobarYEnviarAS400 idempotente por carrera. OrdenId={0}, Estado={1}", ordenId, estadoRevalidadoNormalizado),
                                "FinancieroController");
                        }
                        else
                        {
                            LogAprobarYEnviarAs400Request("ERROR_APROBACION", ordenId.ToString(CultureInfo.InvariantCulture), errAprobacion);
                            return JsonErrorLogged("Error al aprobar la orden. " + (errAprobacion ?? ""));
                        }
                    }
                    else
                    {
                        try
                        {
                            var ordenActualizada = _ordenDAO.ObtenerOrdenPorId(ordenId) ?? orden;
                            var pdf = new CapaPresentacion.Services.PdfGeneratorService().GenerarOrdenRecaudacionPDF(ordenActualizada);
                            new EmailServiceData().EnviarFacturaGenerada(ordenActualizada, pdf);
                            orden = ordenActualizada;
                        }
                        catch (Exception exPdf)
                        {
                            CapaNegocio.LogBL.RegistrarError(
                                string.Format("Error generando/mandando factura Orden={0}", orden.NumeroOrden),
                                exPdf.ToString(),
                                "FinancieroController");
                        }
                    }
                }
                else
                {
                    aprobacionIdempotente = true;
                    CapaNegocio.LogBL.RegistrarInfo(
                        string.Format("AprobarYEnviarAS400 invocado para orden ya aprobada. OrdenId={0}, Estado={1}", ordenId, estado),
                        "FinancieroController");
                }

                // Releer pago/orden después de la aprobación para usar la referencia más actual al generar FR3.
                orden = _ordenDAO.ObtenerOrdenPorId(ordenId) ?? orden;
                pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(ordenId);
                pagoId = pagoEnt != null ? (int?)pagoEnt.Id : null;

                string advertenciaAs400 = null;
                if (FacturacionAS400Service.IsEnabled())
                {
                    var as400Service = new FacturacionAS400Service();
                    var numeroFactura = pagoEnt != null && !string.IsNullOrWhiteSpace(pagoEnt.NumeroComprobante)
                        ? pagoEnt.NumeroComprobante
                        : orden.NumeroOrden;
                    var subtotal = orden.Subtotal ?? orden.Total ?? 0m;
                    var iva = orden.Iva ?? 0m;
                    var total = orden.Total ?? subtotal + iva;

                    if (!as400Service.TryRegistrarFactura(
                        ordenId,
                        pagoId,
                        numeroFactura,
                        null,
                        DateTime.Now,
                        subtotal,
                        iva,
                        total,
                        null,
                        usuario,
                        out advertenciaAs400))
                    {
                        CapaNegocio.LogBL.RegistrarError(
                            string.Format("Error registrando factura en AS400. OrdenId={0}", ordenId),
                            advertenciaAs400 ?? "n/a",
                            "FinancieroController");

                        LogAprobarYEnviarAs400Request("FR3_ERROR", ordenId.ToString(CultureInfo.InvariantCulture), advertenciaAs400);

                        return JsonErrorLogged(
                            string.IsNullOrWhiteSpace(advertenciaAs400)
                                ? "No se pudo generar FR3 en AS400."
                                : advertenciaAs400,
                            500);
                    }
                }
                else
                {
                    advertenciaAs400 = "AS400 deshabilitado por configuracion.";
                }

                var mensaje = aprobacionIdempotente
                    ? "La orden ya estaba aprobada. Se verifico el envio AS400."
                    : "Orden aprobada y enviada a AS400 correctamente.";
                return Json(new
                {
                    ok = true,
                    message = mensaje,
                    idempotent = aprobacionIdempotente,
                    warning = advertenciaAs400
                });
            }
            catch (Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError(
                    string.Format("Excepcion en AprobarYEnviarAS400. OrdenId={0}", ordenId),
                    ex.ToString(),
                    "FinancieroController");
                LogAprobarYEnviarAs400Request("EXCEPCION", ordenId.ToString(CultureInfo.InvariantCulture), ex.Message);
                return JsonErrorLogged("Error interno al aprobar y enviar a AS400.", 500);
            }
        }

        private void LogAprobarYEnviarAs400Request(string motivo, string ordenId, string detalle)
        {
            try
            {
                var request = Request;
                var url = request != null && request.Url != null ? request.Url.ToString() : "N/A";
                var method = request != null ? request.HttpMethod : "N/A";
                var user = User != null && User.Identity != null && User.Identity.IsAuthenticated
                    ? User.Identity.Name
                    : "ANON";

                var formKeys = request != null && request.Form != null
                    ? string.Join(",", request.Form.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                    : string.Empty;
                var queryKeys = request != null && request.QueryString != null
                    ? string.Join(",", request.QueryString.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)))
                    : string.Empty;

                var headerToken = request != null
                    ? (request.Headers["RequestVerificationToken"] ?? request.Headers["__RequestVerificationToken"] ?? request.Headers["X-CSRF-TOKEN"])
                    : null;
                var formToken = request != null && request.Form != null ? request.Form["__RequestVerificationToken"] : null;
                var tokenInfo = string.Format("HdrTokenLen={0},FormTokenLen={1}",
                    string.IsNullOrWhiteSpace(headerToken) ? 0 : headerToken.Length,
                    string.IsNullOrWhiteSpace(formToken) ? 0 : formToken.Length);

                var mensaje = string.Format(
                    "AprobarYEnviarAS400 400. Motivo={0}; OrdenId={1}; User={2}; Method={3}; Url={4}; FormKeys={5}; QueryKeys={6}; {7}; Detalle={8}",
                    motivo ?? "N/A",
                    ordenId ?? "N/A",
                    user,
                    method,
                    url,
                    formKeys,
                    queryKeys,
                    tokenInfo,
                    detalle ?? string.Empty);

                CapaNegocio.LogBL.RegistrarAdvertencia(mensaje, "FinancieroController");
                CapaNegocio.LogBL.RegistrarInfo(mensaje, "FinancieroController");
            }
            catch
            {
                // no bloquear el flujo de la acción por fallos de logging
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult RechazarOrden(int id, string motivo)
        {
            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe ingresar un motivo de rechazo.";
                return RedirectToAction("Index");
            }

            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = CapaDatos.Constants.EstadoOrden.NormalizarEstado(orden.Estado);
            if (estado != CapaDatos.Constants.EstadoOrden.EnRevisionFinanciera &&
                estado != CapaDatos.Constants.EstadoOrden.Pendiente &&
                estado != CapaDatos.Constants.EstadoOrden.Enviada)
            {
                TempData["Error"] = "Solo se pueden devolver órdenes en revisión financiera.";
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";
            var motivoTrim = motivo.Trim();

            try
            {
                var rechazoAplicado = _ordenDAO.ActualizarPagoYEstadoTransaccional(
                    id,
                    null,
                    CapaDatos.Constants.EstadoPago.Rechazado,
                    user,
                    motivoTrim,
                    CapaDatos.Constants.EstadoOrden.Devuelta,
                    out var err);
                if (!rechazoAplicado)
                {
                    var detalleError = (err ?? string.Empty).Trim();
                    var noTienePago =
                        detalleError.IndexOf("No se encontr", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        detalleError.IndexOf("pago", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Si no existe pago asociado, al menos se devuelve la orden con motivo visible para el RT.
                    if (noTienePago)
                    {
                        rechazoAplicado = _ordenDAO.CambiarEstado(id, CapaDatos.Constants.EstadoOrden.Devuelta, motivoTrim);
                        if (!rechazoAplicado)
                        {
                            err = "No se pudo actualizar el estado de la orden a DEVUELTA.";
                        }
                    }
                }

                if (!rechazoAplicado)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error rechazando orden Id={id} NumOrden={orden.NumeroOrden}", err ?? "n/a", "FinancieroController");
                    TempData["Error"] = "Error al rechazar la orden. " + (string.IsNullOrWhiteSpace(err) ? "" : ("Detalle: " + err));
                    return RedirectToAction("Index");
                }

                try
                {
                    var ordenActualizada = _ordenDAO.ObtenerOrdenPorId(id) ?? orden;
                    new EmailServiceData().EnviarNotificacionRechazo(ordenActualizada, motivo);
                }
                catch (System.Exception exMail)
                {
                    CapaNegocio.LogBL.RegistrarError($"Error notificando rechazo Orden={orden.NumeroOrden}", exMail.ToString(), "FinancieroController");
                }

                TempData["Success"] = "Orden devuelta correctamente al RT para corrección.";
            }
            catch (System.Exception ex)
            {
                CapaNegocio.LogBL.RegistrarError($"Error rechazando orden Id={id} NumOrden={orden.NumeroOrden}", ex.ToString(), "FinancieroController");
                TempData["Error"] = "Error interno al rechazar la orden.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryTokenAjax]
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult ReintentarFr3(int ordenId)
        {
            if (ordenId <= 0)
            {
                var rawOrdenId = (Request["ordenId"] ?? Request["id"] ?? string.Empty).Trim();
                if (!int.TryParse(rawOrdenId, out ordenId) || ordenId <= 0)
                {
                    return JsonErrorLogged("Orden inválida para reintentar FR3.");
                }
            }

            var usuario = User != null && User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name)
                ? User.Identity.Name
                : "FINANCIERO";

            var service = new FacturacionAS400Service();
            string mensaje;
            var ok = service.TryReintentarFr3(ordenId, usuario, out mensaje);

            if (!ok)
            {
                return JsonErrorLogged(string.IsNullOrWhiteSpace(mensaje) ? "No se pudo reintentar FR3." : mensaje);
            }

            return Json(new
            {
                ok = true,
                message = string.IsNullOrWhiteSpace(mensaje)
                    ? "Reintento FR3 ejecutado correctamente."
                    : mensaje
            });
        }

        [HttpGet]
        [RequirePermission("FIN_VER_PAGOS")]
        public JsonResult HealthFinanciero()
        {
            var postgresOk = false;
            var db2Ok = false;
            var db2Mensaje = "N/A";

            try
            {
                postgresOk = _ordenDAO.Ping();
            }
            catch (Exception exPg)
            {
                CapaNegocio.LogBL.RegistrarError("HealthFinanciero PostgreSQL", exPg.ToString(), "FinancieroController");
                postgresOk = false;
            }

            try
            {
                var facturacionService = new FacturacionAS400Service();
                db2Ok = facturacionService.TestDb2Connection(out db2Mensaje);
            }
            catch (Exception exDb2)
            {
                CapaNegocio.LogBL.RegistrarError("HealthFinanciero DB2", exDb2.ToString(), "FinancieroController");
                db2Ok = false;
                db2Mensaje = exDb2.Message;
            }

            return Json(new
            {
                ok = postgresOk && db2Ok,
                postgres = new { ok = postgresOk },
                db2 = new { ok = db2Ok, message = db2Mensaje ?? string.Empty },
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }, JsonRequestBehavior.AllowGet);
        }

        // Compat: redirige la ruta /Financiero/DetalleOrden/{id} a la vista oficial de detalles
        [HttpGet]
        public ActionResult DetalleOrden(int id)
        {
            return RedirectToAction("Detalles", "OrdenRecaudacion", new { id });
        }

        // GET: /Financiero/TodasOrdenes
        public ActionResult TodasOrdenes(string estado)
        {
            var estadoFiltro = NormalizarFiltroDashboard(estado);
            var ordenesEnt = _ordenDAO.ObtenerTodasLasOrdenes(null) ?? new List<CapaDatos.Entidades.OrdenRecaudacion>();
            var ordenes = ordenesEnt.Select(MapearOrden).ToList();
            if (!string.Equals(estadoFiltro, "TODAS", StringComparison.OrdinalIgnoreCase))
            {
                ordenes = ordenes.Where(o => CoincideEstadoDashboard(o != null ? o.Estado : null, estadoFiltro)).ToList();
            }

            return View(ordenes);
        }

        #region Helpers
        private static string NormalizarEstadoDashboard(string estado)
        {
            var actual = EstadoOrden.NormalizarEstado(estado);
            if (actual == EstadoOrden.Pendiente || actual == EstadoOrden.Generada)
            {
                return EstadoOrden.Generada;
            }

            return actual;
        }

        private static string NormalizarFiltroDashboard(string estado)
        {
            var actual = (estado ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(actual))
            {
                return "TODAS";
            }

            switch (actual)
            {
                case "TODAS":
                    return "TODAS";
                case "PROCESADA":
                case "EN_REVISION":
                case "EN_REVISION_FINANCIERA":
                    return EstadoOrden.EnRevisionFinanciera;
                case "PENDIENTE":
                case "GENERADA":
                    return EstadoOrden.Generada;
                default:
                    return NormalizarEstadoDashboard(actual);
            }
        }

        private static bool CoincideEstadoDashboard(string estadoOrden, string estadoFiltro)
        {
            return string.Equals(
                NormalizarEstadoDashboard(estadoOrden),
                NormalizarFiltroDashboard(estadoFiltro),
                StringComparison.OrdinalIgnoreCase);
        }

        private OrdenRecaudacionModel MapearOrden(CapaDatos.Entidades.OrdenRecaudacion o)
        {
            if (o == null) return null;
            int usuarioId = o.CodigoUsuario ?? 0;
            return new OrdenRecaudacionModel
            {
                Id = o.Id,
                NumeroOrden = o.NumeroOrden,
                Estado = NormalizarEstadoDashboard(o.Estado),
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

        private JsonResult JsonError(string message, int statusCode = 400)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Json(new
            {
                ok = false,
                message = message ?? "Error procesando la solicitud."
            });
        }

        private JsonResult JsonErrorLogged(string message, int statusCode = 400)
        {
            var action = RouteData != null && RouteData.Values != null && RouteData.Values.ContainsKey("action")
                ? Convert.ToString(RouteData.Values["action"])
                : "AccionDesconocida";

            CapaNegocio.LogBL.RegistrarAdvertencia(
                string.Format("{0} rechazado ({1}): {2}", action, statusCode, message ?? "Error procesando la solicitud."),
                "FinancieroController");
            CapaNegocio.LogBL.RegistrarInfo(
                string.Format("{0} rechazado ({1}): {2}", action, statusCode, message ?? "Error procesando la solicitud."),
                "FinancieroController");
            return JsonError(message, statusCode);
        }

        private static bool TryParseDecimalFlexible(string raw, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var normalized = NormalizeDecimalInput(raw);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                decimal.TryParse(
                    normalized,
                    NumberStyles.Number | NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            raw = raw.Trim();
            var styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
            var cultures = new[]
            {
                CultureInfo.CurrentCulture,
                new CultureInfo("es-EC"),
                CultureInfo.InvariantCulture
            };

            foreach (var culture in cultures)
            {
                if (decimal.TryParse(raw, styles, culture, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeDecimalInput(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cleaned = new string(raw
                .Trim()
                .Where(ch => char.IsDigit(ch) || ch == '.' || ch == ',' || ch == '-' || ch == '+')
                .ToArray());

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            var sign = string.Empty;
            if (cleaned[0] == '-' || cleaned[0] == '+')
            {
                sign = cleaned[0].ToString();
                cleaned = cleaned.Substring(1);
            }

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return null;
            }

            var lastDot = cleaned.LastIndexOf('.');
            var lastComma = cleaned.LastIndexOf(',');

            char decimalSeparator = '\0';
            if (lastDot >= 0 && lastComma >= 0)
            {
                decimalSeparator = lastDot > lastComma ? '.' : ',';
            }
            else if (lastComma >= 0)
            {
                decimalSeparator = DetermineSingleSeparator(cleaned, ',');
            }
            else if (lastDot >= 0)
            {
                decimalSeparator = DetermineSingleSeparator(cleaned, '.');
            }

            if (decimalSeparator == '\0')
            {
                return sign + cleaned.Replace(".", string.Empty).Replace(",", string.Empty);
            }

            var decimalIndex = cleaned.LastIndexOf(decimalSeparator);
            var integerPart = decimalIndex > 0 ? cleaned.Substring(0, decimalIndex) : cleaned;
            var decimalPart = decimalIndex >= 0 && decimalIndex + 1 < cleaned.Length
                ? cleaned.Substring(decimalIndex + 1)
                : string.Empty;

            integerPart = integerPart.Replace(".", string.Empty).Replace(",", string.Empty);
            decimalPart = decimalPart.Replace(".", string.Empty).Replace(",", string.Empty);

            if (string.IsNullOrEmpty(integerPart))
            {
                integerPart = "0";
            }

            if (string.IsNullOrEmpty(decimalPart))
            {
                return sign + integerPart;
            }

            return sign + integerPart + "." + decimalPart;
        }

        private static char DetermineSingleSeparator(string value, char separator)
        {
            var count = value.Count(ch => ch == separator);
            var lastIndex = value.LastIndexOf(separator);
            var digitsAfter = lastIndex >= 0 ? value.Length - lastIndex - 1 : 0;

            if (count == 1)
            {
                if (digitsAfter == 0)
                {
                    return '\0';
                }

                return digitsAfter <= 2 ? separator : '\0';
            }

            return digitsAfter > 0 && digitsAfter <= 2 ? separator : '\0';
        }

        private static bool TryParseDateFlexible(string raw, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd" };
            if (DateTime.TryParseExact(raw.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            {
                return true;
            }

            return DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out value) ||
                   DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        public class AprobarPagoConFacturaVM
        {
            public int OrdenId { get; set; }
            public int? PagoId { get; set; }
            public string NumeroFactura { get; set; }
            public string AutorizacionFactura { get; set; }
            public string FechaEmision { get; set; }
            public string Subtotal { get; set; }
            public string Iva { get; set; }
            public string Total { get; set; }
            public string Observaciones { get; set; }
        }
        #endregion
    }
}
