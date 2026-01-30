using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using CapaUtilidades;
using CapaNegocio;
using CapaModelo;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class PagoController : Controller
    {
        private readonly PagoBL _bl = new PagoBL();

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

        // ============================================================
        // SUBIR COMPROBANTE
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Solicitante,Administrador")]
        public ActionResult SubirComprobante(int id, int solicitudId, HttpPostedFileBase archivo)
        {
            try
            {
                if (archivo != null && archivo.ContentLength > 0)
                {
                    var maxSize = GetMaxUploadSize();
                    var allowed = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                    var basePath = GetUploadBasePath("Pagos");

                    var result = FileUploadService.SaveFile(
                        archivo.InputStream,
                        archivo.FileName,
                        archivo.ContentType,
                        basePath,
                        maxSize,
                        allowed);

                    var pago = _bl.ObtenerPorId(id);
                    if (pago != null)
                    {
                        string usuario = Session["CodigoUsuario"]?.ToString() ?? "SISTEMA";

                        pago.RutaComprobante = result.StoredPath;
                        pago.UsuarioValidacion = usuario;
                        pago.FechaValidacion = DateTime.Now;

                        _bl.Actualizar(pago);
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al subir comprobante: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { solicitudId });
        }

        // ============================================================
        // VALIDAR (APROBAR) PAGO
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Financiero,Administrador")]
        public ActionResult Validar(int id, int solicitudId)
        {
            try
            {
                string usuario = Session["CodigoUsuario"]?.ToString() ?? "SISTEMA";
                var pago = _bl.ObtenerPorId(id);

                if (pago != null)
                {
                    pago.Estado = "APROBADO";
                    pago.FechaValidacion = DateTime.Now;
                    pago.UsuarioValidacion = usuario;

                    _bl.Actualizar(pago);
                    TempData["Success"] = "Pago aprobado correctamente.";
                }
                else
                {
                    TempData["Error"] = "Pago no encontrado.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al aprobar pago: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { solicitudId });
        }

        // ============================================================
        // RECHAZAR PAGO
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Financiero,Administrador")]
        public ActionResult Rechazar(int id, int solicitudId, string motivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    TempData["Error"] = "Debe indicar el motivo del rechazo.";
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                string usuario = Session["CodigoUsuario"]?.ToString() ?? "SISTEMA";
                var pago = _bl.ObtenerPorId(id);

                if (pago != null)
                {
                    pago.Estado = "RECHAZADO";
                    pago.FechaValidacion = DateTime.Now;
                    pago.UsuarioValidacion = usuario;
                    pago.ObservacionesValidacion = motivo;

                    _bl.Actualizar(pago);
                    TempData["Success"] = "Pago rechazado correctamente.";
                }
                else
                {
                    TempData["Error"] = "Pago no encontrado.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al rechazar pago: " + ex.Message;
            }

            return RedirectToAction("Detalle", new { solicitudId });
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
    }
}
