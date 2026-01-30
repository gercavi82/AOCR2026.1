using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using CapaUtilidades;
using CapaDatos.Services;
using CapaDatos.DAOs;
using CapaDatos.Models;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly OrdenRecaudacionDAO _dao = new OrdenRecaudacionDAO();

        // ✅ Para confirmar conexión real a DB (útil en producción)
        [Authorize(Roles = "Administrador,Financiero")]
        public JsonResult DbPing()
        {
            return Json(new { ok = _dao.Ping() }, JsonRequestBehavior.AllowGet);
        }

        // GET: /OrdenRecaudacion?estado=GENERADA
        public ActionResult Index(string estado)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            CargarEstadosCombo(estado);

            var ordenes = _dao.ListarPorUsuario(idUsuario, estado) ?? new List<OrdenRecaudacionModel>();

            // Estadísticas: tu view espera claves con mayúscula
            var est = _dao.ObtenerEstadisticas(idUsuario);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);

            return View(ordenes);
        }

        // GET: /OrdenRecaudacion/Obligatoria
        public ActionResult Obligatoria()
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Obligatoria: Usuario no autenticado, redirigiendo a login");
                return RedirectToAction("Login", "Account");
            }

            System.Diagnostics.Debug.WriteLine($"Obligatoria: Usuario ID = {idUsuario}");

            CargarEstadosCombo(null);

            var ordenes = _dao.ListarPorUsuario(idUsuario, null) ?? new List<OrdenRecaudacionModel>();
            System.Diagnostics.Debug.WriteLine($"Obligatoria: Se encontraron {ordenes.Count} órdenes para el usuario {idUsuario}");

            // Estadísticas
            var est = _dao.ObtenerEstadisticas(idUsuario);
            ViewBag.Estadisticas = MapearEstadisticasParaVista(est);
            ViewBag.TieneOrdenBorrador = ordenes.Any(o => string.Equals((o.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase));

            return View(ordenes);
        }

        // GET: /OrdenRecaudacion/Nueva
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult Nueva()
        {
            var model = new CapaPresentacion.Models.OrdenRecaudacionNuevaVM();
            // TODO: Cargar conceptos disponibles desde la base de datos
            return View(model);
        }

        // GET: /OrdenRecaudacion/Detalles/5
        public ActionResult Detalles(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            return View(orden);
        }

        // GET: /OrdenRecaudacion/Editar/5
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult Editar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(403);

            return View(orden);
        }

        // POST: /OrdenRecaudacion/Editar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Editar(OrdenRecaudacionModel model)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(model);

            var ordenExistente = _dao.ObtenerOrdenPorId(model.Id);
            if (ordenExistente == null || ordenExistente.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((ordenExistente.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
                return new HttpStatusCodeResult(403);

            try
            {
                // Actualizar los campos editables
                ordenExistente.LugarEmision = model.LugarEmision;
                ordenExistente.Compania = model.Compania;
                ordenExistente.RucCedula = model.RucCedula;
                ordenExistente.NombreContribuyente = model.NombreContribuyente;
                ordenExistente.Correo = model.Correo;
                ordenExistente.Telefono = model.Telefono;
                ordenExistente.Observacion = model.Observacion;

                bool result = _dao.ActualizarOrden(ordenExistente);
                if (result)
                {
                    TempData["OK"] = "Orden actualizada correctamente";
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "Error al actualizar la orden");
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error interno: " + ex.Message);
                return View(model);
            }
        }

        // POST: /OrdenRecaudacion/Anular/5
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public JsonResult Anular(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return Json(new { success = false, message = "Usuario no autenticado" });

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return Json(new { success = false, message = "Orden no encontrada" });

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "La orden ya está anulada" });

            try
            {
                bool result = _dao.CambiarEstadoOrden(id, "ANULADA");
                return Json(new { success = result, message = result ? "Orden anulada correctamente" : "Error al anular la orden" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno: " + ex.Message });
            }
        }

        // POST: /OrdenRecaudacion/Generar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Generar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "BORRADOR", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden generar órdenes en estado BORRADOR";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (orden.Total <= 0)
            {
                TempData["Error"] = "No se puede generar una orden sin conceptos";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                bool result = _dao.CambiarEstadoOrden(id, "GENERADA");
                if (result)
                {
                    TempData["OK"] = "Orden generada correctamente";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    TempData["Error"] = "Error al generar la orden";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // POST: /OrdenRecaudacion/Enviar/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Enviar(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "GENERADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden enviar órdenes en estado GENERADA";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                bool result = _dao.CambiarEstadoOrden(id, "ENVIADA");
                if (result)
                {
                    TempData["OK"] = "Orden enviada correctamente al contribuyente";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    TempData["Error"] = "Error al enviar la orden";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // POST: /OrdenRecaudacion/RegistrarPago/5
        [HttpGet]
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult RegistrarPago(int id)
        {
            if (id <= 0)
                return RedirectToAction("Index");

            TempData["Error"] = "Debe registrar el pago desde el detalle de la orden.";
            return RedirectToAction("Detalles", new { id = id });
        }

        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult RegistrarPago(int id, string Monto, string NumeroFactura, string MetodoPago, HttpPostedFileBase ComprobanteArchivo, string Observaciones)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (!string.Equals((orden.Estado ?? "").Trim(), "ENVIADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Solo se pueden registrar pagos en órdenes enviadas";
                return RedirectToAction("Detalles", new { id = id });
            }

            decimal montoValue;
            var montoRaw = (Monto ?? Request["Monto"] ?? "").Trim();
            if (!decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out montoValue) &&
                !decimal.TryParse(montoRaw, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out montoValue))
            {
                TempData["Error"] = "Monto inválido";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (montoValue <= 0)
            {
                TempData["Error"] = "El monto debe ser mayor a cero";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(NumeroFactura))
            {
                TempData["Error"] = "Debe proporcionar el número de factura o referencia";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(MetodoPago))
            {
                TempData["Error"] = "Debe seleccionar un método de pago";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // Guardar comprobante si existe
                string comprobanteRuta = null;
                if (ComprobanteArchivo != null && ComprobanteArchivo.ContentLength > 0)
                {
                    try
                    {
                        var maxSize = GetMaxUploadSize();
                        var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                        var basePath = GetUploadBasePath("Pagos");

                        var result = FileUploadService.SaveFile(
                            ComprobanteArchivo.InputStream,
                            ComprobanteArchivo.FileName,
                            ComprobanteArchivo.ContentType,
                            basePath,
                            maxSize,
                            allowed);

                        comprobanteRuta = result.StoredPath;
                    }
                    catch (Exception ex)
                    {
                        TempData["Error"] = "No se pudo procesar el comprobante: " + ex.Message;
                        return RedirectToAction("Detalles", new { id = id });
                    }
                }

                var pago = new PagoModel
                {
                    NumeroFactura = NumeroFactura,
                    Monto = montoValue,
                    Moneda = "USD",
                    MetodoPago = MetodoPago,
                    Estado = "Pendiente",
                    FechaPago = DateTime.Now,
                    Observaciones = Observaciones,
                    ComprobanteRuta = comprobanteRuta
                };

                int codigoSolicitud;
                if (!int.TryParse(orden.CodigoSolicitud ?? "", out codigoSolicitud))
                {
                    codigoSolicitud = orden.Id;
                }

                bool result = _dao.RegistrarPagoYActualizarEstado(codigoSolicitud, pago, "PAGADA");
                if (result)
                {
                    Logger.Info($"Pago registrado. OrdenId={orden.Id} NumeroOrden={orden.NumeroOrden} CodigoSolicitud={codigoSolicitud}");

                    NotificarFinanciero(orden, pago);
                    TempData["OK"] = "Pago registrado correctamente";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    Logger.Warn($"Pago NO registrado. OrdenId={orden.Id} NumeroOrden={orden.NumeroOrden} CodigoSolicitud={codigoSolicitud}");
                    TempData["Error"] = "Error al registrar el pago";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error RegistrarPago OrdenId={id}", ex);
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // POST: /OrdenRecaudacion/Anular/5
        [HttpPost]
        [Authorize(Roles = "Solicitante,Administrador")]
        [ValidateAntiForgeryToken]
        public ActionResult Anular(int id, string motivo)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            var orden = _dao.ObtenerOrdenPorId(id);
            if (orden == null || orden.CodigoUsuario != idUsuario)
                return HttpNotFound();

            if (string.Equals((orden.Estado ?? "").Trim(), "PAGADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "No se pueden anular órdenes que ya han sido pagadas";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.Equals((orden.Estado ?? "").Trim(), "ANULADA", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "La orden ya está anulada";
                return RedirectToAction("Detalles", new { id = id });
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "Debe proporcionar un motivo para la anulación";
                return RedirectToAction("Detalles", new { id = id });
            }

            try
            {
                // TODO: Aquí se debería guardar el motivo de la anulación en la base de datos
                bool result = _dao.CambiarEstadoOrden(id, "ANULADA");
                if (result)
                {
                    TempData["OK"] = "Orden anulada correctamente";
                    return RedirectToAction("Detalles", new { id = id });
                }
                else
                {
                    TempData["Error"] = "Error al anular la orden";
                    return RedirectToAction("Detalles", new { id = id });
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error interno: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        // GET: /OrdenRecaudacion/DescargarPDF/5
        public ActionResult DescargarPDF(int id)
        {
            int idUsuario = GetUserId();
            if (idUsuario <= 0) return RedirectToAction("Login", "Account");

            try
            {
                var pdfData = _dao.ObtenerDatosParaPdf(id, idUsuario);
                if (pdfData == null)
                    return HttpNotFound();

                var pdfService = new CapaPresentacion.Services.PdfGeneratorService();
                var pdfBytes = pdfService.GenerarOrdenRecaudacionPDF(pdfData);

                string fileName = $"Orden_Recaudacion_{pdfData.NumeroOrden}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el PDF: " + ex.Message;
                return RedirectToAction("Detalles", new { id = id });
            }
        }

        private int GetUserId()
        {
            int id = 0;
            var v = Session["UserId"] ?? Session["IdUsuario"];
            if (v != null)
            {
                int.TryParse(v.ToString(), out id);
                System.Diagnostics.Debug.WriteLine($"GetUserId: Encontrado ID de usuario = {id} desde Session['{(Session["UserId"] != null ? "UserId" : "IdUsuario")}']");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("GetUserId: No se encontró ID de usuario en la sesión");
            }
            return id;
        }

        private void CargarEstadosCombo(string estadoSeleccionado)
        {
            var selected = (estadoSeleccionado ?? "").Trim().ToUpperInvariant();

            var items = new List<SelectListItem>
            {
                new SelectListItem { Text = "TODAS", Value = "" },
                new SelectListItem { Text = "BORRADOR", Value = "BORRADOR" },
                new SelectListItem { Text = "GENERADA", Value = "GENERADA" },
                new SelectListItem { Text = "ENVIADA", Value = "ENVIADA" },
                new SelectListItem { Text = "PAGADA", Value = "PAGADA" },
                new SelectListItem { Text = "ANULADA", Value = "ANULADA" }
            };

            foreach (var it in items)
                it.Selected = (!string.IsNullOrEmpty(selected) && it.Value == selected) ||
                              (string.IsNullOrEmpty(selected) && it.Value == "");

            ViewBag.Estados = items; // ✅ IEnumerable<SelectListItem> real
        }

        private Dictionary<string, object> MapearEstadisticasParaVista(Dictionary<string, object> d)
        {
            int total = GetInt(d, "total");
            int pagadas = GetInt(d, "pagada");
            decimal montoTotal = GetDec(d, "monto_total");
            decimal montoRecaudado = GetDec(d, "monto_recaudado");

            decimal saldoPendiente = montoTotal - montoRecaudado;
            if (saldoPendiente < 0) saldoPendiente = 0;

            return new Dictionary<string, object>
            {
                ["Total"] = total,
                ["Pagadas"] = pagadas,
                ["SaldoPendiente"] = saldoPendiente,
                ["MontoPagado"] = montoRecaudado
            };
        }

        private int GetInt(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0;
            int x; return int.TryParse(d[key].ToString(), out x) ? x : 0;
        }

        private decimal GetDec(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0m;
            decimal x; return decimal.TryParse(d[key].ToString(), out x) ? x : 0m;
        }

        private void NotificarFinanciero(OrdenRecaudacionModel orden, PagoModel pago)
        {
            try
            {
                var emails = GetAdminEmails();
                if (emails.Length == 0) return;

                var emailService = new EmailService();
                emailService.EnviarNotificacionFinanciero(orden, pago, emails);
            }
            catch (Exception ex)
            {
                Logger.Warn("No se pudo notificar a financiero: " + ex.Message);
            }
        }

        private long GetMaxUploadSize()
        {
            var raw = ConfigurationManager.AppSettings["MaxUploadSize"];
            long size;
            return long.TryParse(raw, out size) ? size : (10 * 1024 * 1024);
        }

        private string GetUploadBasePath(string subfolder)
        {
            var baseSetting = ConfigurationManager.AppSettings["UploadStoragePath"] ?? "~/App_Data/Uploads";
            var basePath = Server.MapPath(baseSetting);
            return Path.Combine(basePath, subfolder ?? string.Empty);
        }

        private string[] GetAdminEmails()
        {
            var raw = ConfigurationManager.AppSettings["AdminEmails"] ?? string.Empty;
            var parts = raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        }
    }
}
