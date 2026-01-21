using System;
using System.IO;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaPresentacion.Models.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net;
using System.Configuration;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class OrdenRecaudacionController : Controller
    {
        private readonly IOrdenRecaudacionDAO _dao;

        public OrdenRecaudacionController()
        {
            _dao = new OrdenRecaudacionDAO();
        }

        // ============================
        // PANTALLA OBLIGATORIA DESPUÉS DE LOGIN
        // ============================
        [HttpGet]
        public ActionResult Obligatoria()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Debe iniciar sesión.";
                    return RedirectToAction("Login", "Account");
                }

                if (_dao.ExisteORGeneradaOPagada(idUsuario))
                    return RedirectToAction("Index", "Dashboard");

                bool tieneOrdenBorrador = _dao.ExisteORMinima(idUsuario);
                ViewBag.TieneOrdenBorrador = tieneOrdenBorrador;

                if (tieneOrdenBorrador)
                {
                    var dtOrdenes = _dao.ObtenerOrdenesPorUsuario(idUsuario);
                    var ordenBorrador = dtOrdenes.AsEnumerable()
                        .FirstOrDefault(row => (row.Field<string>("estado") ?? "") == "BORRADOR");

                    if (ordenBorrador != null)
                    {
                        ViewBag.OrdenId = Convert.ToInt32(ordenBorrador["id"]);
                        ViewBag.NumeroOrden = ordenBorrador["numero_orden"]?.ToString();
                        ViewBag.FechaCreacion = Convert.ToDateTime(ordenBorrador["fecha_creacion"]).ToString("dd/MM/yyyy");
                        ViewBag.Total = Convert.ToDecimal(ordenBorrador["total"]).ToString("C");
                    }
                }

                var conceptos = _dao.ObtenerConceptosActivos();
                var listaConceptos = new List<dynamic>();

                if (conceptos != null && conceptos.Rows.Count > 0)
                {
                    listaConceptos = conceptos.AsEnumerable()
                        .Take(4)
                        .Select(row => new
                        {
                            id = row.Field<int>("id"),
                            codigo = row.Field<string>("codigo"),
                            nombre = row.Field<string>("nombre"),
                            valor = row.Field<decimal>("valor_base")
                        })
                        .ToList<dynamic>();
                }

                ViewBag.ConceptosPrincipales = listaConceptos;
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Login", "Account");
            }
        }

        // ============================
        // NUEVA ORDEN
        // ============================
        [HttpGet]
        public ActionResult Nueva()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Debe iniciar sesión.";
                    return RedirectToAction("Login", "Account");
                }

                if (_dao.ExisteORMinima(idUsuario))
                {
                    TempData["Advertencia"] = "Ya tiene una orden en estado BORRADOR. Debe completarla primero.";
                    return RedirectToAction("Obligatoria");
                }

                return View(new OrdenRecaudacionViewModel());
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Obligatoria");
            }
        }

        // ============================
        // CREAR ORDEN
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> Crear(OrdenRecaudacionViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errores = ModelState.Values.SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .ToList();

                    return Json(new { success = false, mensaje = "Errores de validación", errores });
                }

                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, mensaje = "Sesión expirada. Inicie sesión nuevamente." });

                // ✅ Alineado a tu interfaz:
                // InsertarOrdenAOCR(int idUsuario, string codigoSolicitud, int conceptoId, int estaciones, int dias, string obs)

                string codigoSolicitud = (model.CodigoSolicitud ?? "").Trim();
                if (string.IsNullOrWhiteSpace(codigoSolicitud))
                    return Json(new { success = false, mensaje = "Solicitud no válida (Código de Solicitud vacío)." });

                // Concepto: permitir recibir ConceptoId (preferido).
                int conceptoId = model.ConceptoId;

                // Compatibilidad: si tu front aún manda ConceptoPrincipalCodigo, intenta mapearlo a ID
                if (conceptoId <= 0 && !string.IsNullOrWhiteSpace(model.ConceptoPrincipalCodigo))
                {
                    conceptoId = MapearConceptoIdPorCodigo(model.ConceptoPrincipalCodigo.Trim());
                }

                if (conceptoId <= 0)
                    return Json(new { success = false, mensaje = "Debe seleccionar un concepto válido." });

                if (model.Estaciones < 0 || model.Estaciones > 50)
                    return Json(new { success = false, mensaje = "El número de estaciones debe estar entre 0 y 50." });

                if (model.Dias < 0 || model.Dias > 30)
                    return Json(new { success = false, mensaje = "El número de días debe estar entre 0 y 30." });

                // Si ya existe borrador, corta por seguridad/flujo
                if (_dao.ExisteORMinima(idUsuario))
                    return Json(new { success = false, mensaje = "Ya tiene una orden BORRADOR. Complete esa orden primero." });

                int ordenId = await Task.Run(() =>
                    _dao.InsertarOrdenAOCR(
                        idUsuario,
                        codigoSolicitud,
                        conceptoId,
                        model.Estaciones,
                        model.Dias,
                        model.Observacion ?? ""
                    ));

                if (ordenId > 0)
                {
                    Session["TieneOrdenGenerada"] = true;
                    Session["TieneOrdenBorrador"] = false;

                    return Json(new
                    {
                        success = true,
                        ordenId,
                        mensaje = "✅ Orden creada exitosamente",
                        // Ajusta si tu acción real se llama distinto
                        redireccion = Url.Action("Obligatoria", "OrdenRecaudacion")
                    });
                }

                return Json(new { success = false, mensaje = "No se pudo crear la orden." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = ex.Message });
            }
        }

        // ============================
        // VALIDACIONES / ENDPOINTS JSON
        // ============================
        [HttpGet]
        public JsonResult ValidarOrdenBorrador()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                bool tieneBorrador = idUsuario > 0 && _dao.ExisteORMinima(idUsuario);
                return Json(new { tieneBorrador }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { tieneBorrador = false }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult ObtenerConceptos()
        {
            try
            {
                var conceptos = _dao.ObtenerConceptosActivos();

                var lista = conceptos.AsEnumerable()
                    .Select(row => new
                    {
                        id = row.Field<int>("id"),
                        codigo = row.Field<string>("codigo"),
                        nombre = row.Field<string>("nombre"),
                        valor = row.Field<decimal>("valor_base"),
                        descripcion = row.Field<string>("descripcion")
                    })
                    .ToList();

                return Json(new { success = true, conceptos = lista }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = $"Error al cargar conceptos: {ex.Message}" }, JsonRequestBehavior.AllowGet);
            }
        }

        // ✅ Alineado a tu interfaz: ObtenerValorConceptoPorId(int conceptoId)
        [HttpGet]
        public JsonResult CalcularTotal(int conceptoId, int estaciones, int dias)
        {
            try
            {
                if (conceptoId <= 0) return Json(new { success = false, mensaje = "Concepto inválido" }, JsonRequestBehavior.AllowGet);
                if (estaciones < 0 || estaciones > 50) return Json(new { success = false, mensaje = "Estaciones fuera de rango" }, JsonRequestBehavior.AllowGet);
                if (dias < 0 || dias > 30) return Json(new { success = false, mensaje = "Días fuera de rango" }, JsonRequestBehavior.AllowGet);

                decimal valorBase = _dao.ObtenerValorConceptoPorId(conceptoId);
                decimal inspeccion = estaciones * 500m;
                decimal viaticos = dias * 80m;
                decimal gastosAdmin = viaticos * 0.08m;
                decimal total = valorBase + inspeccion + viaticos + gastosAdmin;

                return Json(new
                {
                    success = true,
                    valorBase,
                    inspeccion,
                    viaticos,
                    gastosAdmin,
                    total
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, mensaje = $"Error en cálculo: {ex.Message}" }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================
        // VERIFICAR ACCESO AL DASHBOARD
        // ============================
        [HttpGet]
        public JsonResult VerificarAccesoDashboard()
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    return Json(new
                    {
                        accesoPermitido = false,
                        mensaje = "Sesión expirada",
                        redireccion = Url.Action("Login", "Account")
                    }, JsonRequestBehavior.AllowGet);
                }

                bool tieneOrden = _dao.ExisteORGeneradaOPagada(idUsuario);

                return Json(new
                {
                    accesoPermitido = tieneOrden,
                    tieneOrden,
                    mensaje = tieneOrden ? "Acceso permitido" : "Requiere orden de recaudación",
                    redireccion = tieneOrden ? "" : Url.Action("Obligatoria", "OrdenRecaudacion")
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    accesoPermitido = false,
                    mensaje = $"Error: {ex.Message}",
                    redireccion = Url.Action("Login", "Account")
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // ============================
        // ENVIAR ORDEN POR EMAIL (PDF)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult EnviarOrdenPorEmail(int ordenId, string emailDestino, string asunto, string mensaje)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                    return Json(new { success = false, message = "Sesión expirada. Inicie sesión nuevamente." });

                if (ordenId <= 0)
                    return Json(new { success = false, message = "Orden inválida." });

                if (string.IsNullOrWhiteSpace(emailDestino))
                    return Json(new { success = false, message = "Email destino es requerido." });

                // Validación simple de email
                try { var _ = new MailAddress(emailDestino); }
                catch { return Json(new { success = false, message = "Email destino inválido." }); }

                // ✅ Seguridad: el DAO debe validar ordenId + usuarioId
                byte[] pdfBytes = _dao.GenerarPDFOrden(ordenId, idUsuario);
                if (pdfBytes == null || pdfBytes.Length == 0)
                    return Json(new { success = false, message = "No se pudo generar el PDF." });

                string fromEmail = ConfigurationManager.AppSettings["SmtpFromEmail"];
                string fromName = ConfigurationManager.AppSettings["SmtpFromName"] ?? "Sistema AOCR - DGAC";
                string host = ConfigurationManager.AppSettings["SmtpHost"];
                int port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"] ?? "587");
                bool enableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpEnableSsl"] ?? "true");
                string user = ConfigurationManager.AppSettings["SmtpUser"];
                string pass = ConfigurationManager.AppSettings["SmtpPass"];

                if (string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                    return Json(new { success = false, message = "SMTP no está configurado en Web.config (AppSettings)." });

                string safeSubject = string.IsNullOrWhiteSpace(asunto) ? "Orden de Recaudación - AOCR" : asunto.Trim();
                string safeBody = string.IsNullOrWhiteSpace(mensaje) ? "Adjunto se envía la Orden de Recaudación." : mensaje.Trim();

                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(fromEmail, fromName);
                    mailMessage.To.Add(emailDestino.Trim());
                    mailMessage.Subject = safeSubject;
                    mailMessage.Body = safeBody;
                    mailMessage.IsBodyHtml = false;

                    using (var pdfStream = new MemoryStream(pdfBytes))
                    {
                        var attachment = new Attachment(pdfStream, $"OrdenRecaudacion_{ordenId}.pdf", "application/pdf");
                        mailMessage.Attachments.Add(attachment);

                        using (var smtpClient = new SmtpClient(host, port))
                        {
                            smtpClient.Credentials = new NetworkCredential(user, pass);
                            smtpClient.EnableSsl = enableSsl;
                            smtpClient.Send(mailMessage);
                        }
                    }
                }

                return Json(new { success = true, message = "Email enviado correctamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al enviar email: {ex.Message}" });
            }
        }

        // ============================
        // HELPERS
        // ============================
        private int ObtenerIdUsuario()
        {
            if (Session["IdUsuario"] != null &&
                int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
            {
                return idUsuario;
            }
            return 0;
        }

        private int MapearConceptoIdPorCodigo(string codigo)
        {
            try
            {
                var dt = _dao.ObtenerConceptosActivos();
                var row = dt.AsEnumerable()
                    .FirstOrDefault(r => string.Equals((r.Field<string>("codigo") ?? "").Trim(), codigo.Trim(), StringComparison.OrdinalIgnoreCase));

                return row != null ? row.Field<int>("id") : 0;
            }
            catch
            {
                return 0;
            }
        }
        [HttpGet]
        public ActionResult Detalle(int id)
        {
            try
            {
                int idUsuario = ObtenerIdUsuario();
                if (idUsuario <= 0)
                {
                    TempData["Error"] = "Sesión expirada. Inicie sesión nuevamente.";
                    return RedirectToAction("Login", "Account");
                }

                if (id <= 0)
                    return HttpNotFound("Orden inválida.");

                // IMPORTANTE: que tu DAO valide que la orden pertenezca al usuario
                var dto = _dao.ObtenerDatosParaPdf(id, idUsuario);
                if (dto == null)
                    return HttpNotFound("Orden no encontrada o no autorizada.");

                return View(dto);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo cargar el detalle: " + ex.Message;
                return RedirectToAction("Obligatoria");
            }
        }
        [HttpGet]
        public ActionResult DescargarPdf(int id)
        {
            int idUsuario = ObtenerIdUsuario();
            if (idUsuario <= 0)
                return RedirectToAction("Login", "Account");

            if (id <= 0)
                return HttpNotFound();

            var pdfBytes = _dao.GenerarPDFOrden(id, idUsuario);
            if (pdfBytes == null || pdfBytes.Length == 0)
                return HttpNotFound("No se pudo generar el PDF.");

            var datos = _dao.ObtenerDatosParaPdf(id, idUsuario);
            string fileName = $"OrdenRecaudacion_{(datos?.NumeroOrden ?? id.ToString())}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }


    }
}
