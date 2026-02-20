using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaPresentacion.Models;
using CapaPresentacion.Filters;
using CapaUtilidades;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles = "Financiero,Administrador")]
    public class FinancieroController : Controller
    {
        private readonly OrdenRecaudacionDAO _ordenDAO = new OrdenRecaudacionDAO();

        [RequirePermission("FIN_VER_PAGOS")]
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
                // El DAO espera id de orden para resolver internamente el codigo_solicitud correcto.
                var pagoEnt = _ordenDAO.ObtenerUltimoPagoPorOrden(orden?.Id ?? 0);
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
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult AprobarOrden(int id)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

            var estado = ((orden.Estado ?? "").Trim()).ToUpperInvariant().Replace(" ", "_");
            if (estado != "PROCESADA")
            {
                TempData["Error"] = "Solo se pueden aprobar Ã³rdenes en estado PROCESADA.";
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
        [RequirePermission("FIN_APROBAR_PAGO")]
        public ActionResult AprobarPago(int id, int? pagoId)
        {
            var orden = _ordenDAO.ObtenerOrdenPorId(id);
            if (orden == null) return HttpNotFound();

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
                    new EmailService().EnviarFacturaGenerada(ordenActualizada, pdf);
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
                    warning = advertencia
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
            if (estado != "PROCESADA" && estado != "PENDIENTE")
            {
                TempData["Error"] = "Solo se pueden rechazar Ã³rdenes en estado PROCESADA o PENDIENTE.";
                return RedirectToAction("Index");
            }

            var user = User?.Identity?.Name ?? "FINANCIERO";

            try
            {
                var rechazoAplicado = _ordenDAO.ActualizarPagoYEstadoTransaccional(id, null, "ANULADO", user, motivo, "ANULADA", out var err);
                if (!rechazoAplicado)
                {
                    var detalleError = (err ?? string.Empty).Trim();
                    var noTienePago =
                        detalleError.IndexOf("No se encontr", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        detalleError.IndexOf("pago", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Rechazo sin pago asociado: anula la orden igualmente para retirarla del flujo financiero.
                    if (noTienePago)
                    {
                        rechazoAplicado = _ordenDAO.CambiarEstado(id, "ANULADA", motivo);
                        if (!rechazoAplicado)
                        {
                            err = "No se pudo actualizar el estado de la orden a ANULADA.";
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
                    new EmailService().EnviarNotificacionRechazo(ordenActualizada, motivo);
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

        // Compat: redirige la ruta /Financiero/DetalleOrden/{id} a la vista oficial de detalles
        [HttpGet]
        public ActionResult DetalleOrden(int id)
        {
            return RedirectToAction("Detalles", "OrdenRecaudacion", new { id });
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

        private JsonResult JsonError(string message, int statusCode = 400)
        {
            Response.StatusCode = statusCode;
            return Json(new
            {
                ok = false,
                message = message ?? "Error procesando la solicitud."
            });
        }

        private JsonResult JsonErrorLogged(string message, int statusCode = 400)
        {
            CapaNegocio.LogBL.RegistrarAdvertencia(
                string.Format("AprobarPagoConFactura rechazado ({0}): {1}", statusCode, message ?? "Error procesando la solicitud."),
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
