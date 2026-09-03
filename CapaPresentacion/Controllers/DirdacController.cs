using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CapaDatos.Constants;
using CapaDatos.DAOs;
using CapaModelo;
using CapaNegocio;
using CapaNegocio.Services;
using CapaPresentacion.Filters;
using CapaPresentacion.Helpers;

namespace CapaPresentacion.Controllers
{
    /// <summary>
    /// Controlador exclusivo para el rol DIRDAC (Director General de Aviación Civil).
    /// Maneja la revisión del AOCR, legalización y firma exclusiva del AOCR, devolución con
    /// observaciones a DIRCAV y confirmación de conclusión formal del trámite.
    /// Bloquea terminantemente acciones operativas del Administrador (Regla 7) y de DIRCAV.
    /// </summary>
    [Authorize(Roles = "DIRDAC,Administrador")]
    public class DirdacController : Controller
    {
        private readonly DirdacBandejaService _bandejaService;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly CertificadoDAO _certificadoDao;
        private readonly AocrFirmaDocumentoDAO _firmaDao;

        public DirdacController()
        {
            _bandejaService = new DirdacBandejaService();
            _solicitudDao = new SolicitudAOCRDAO();
            _certificadoDao = new CertificadoDAO();
            _firmaDao = new AocrFirmaDocumentoDAO();
        }

        public DirdacController(
            DirdacBandejaService bandejaService,
            SolicitudAOCRDAO solicitudDao,
            CertificadoDAO certificadoDao,
            AocrFirmaDocumentoDAO firmaDao)
        {
            _bandejaService = bandejaService;
            _solicitudDao = solicitudDao;
            _certificadoDao = certificadoDao;
            _firmaDao = firmaDao;
        }

        private bool EsDirdacAutorizado()
        {
            var rolActual = Session != null && Session["Rol"] != null ? Session["Rol"].ToString() : string.Empty;
            // Solo rol activo DIRDAC; el Administrador no puede ejecutar acciones operativas
            return AocrRolesInstitucionales.EsDirdac(rolActual);
        }

        // =======================================================
        // 1. BANDEJA INSTITUCIONAL DIRDAC
        // =======================================================
        [HttpGet]
        public ActionResult Bandeja(string tab = "revision")
        {
            if (!EsDirdacAutorizado() && !User.IsInRole("DIRDAC"))
            {
                return new HttpStatusCodeResult(403, "No tiene permisos para acceder a la bandeja DIRDAC.");
            }

            ViewBag.ActiveTab = tab ?? "revision";
            ViewBag.ContadorRevision = _bandejaService.ContarAocrPendientesRevision();
            ViewBag.ContadorFirma = _bandejaService.ContarAocrPendientesFirma();
            ViewBag.ContadorDevueltos = _bandejaService.ContarExpedientesDevueltosDircav();

            ViewBag.AocrPendientesRevision = _bandejaService.ObtenerAocrPendientesRevision();
            ViewBag.AocrPendientesFirma = _bandejaService.ObtenerAocrPendientesFirma();
            ViewBag.ExpedientesDevueltos = _bandejaService.ObtenerExpedientesDevueltosDircav();
            ViewBag.AocrFirmados = _bandejaService.ObtenerAocrFirmados();
            ViewBag.ProcesosConcluidos = _bandejaService.ObtenerProcesosConcluidos();
            ViewBag.Historial = _bandejaService.ObtenerHistorialGestionados();

            return View();
        }

        // =======================================================
        // 2. DETALLE DE AOCR Y EXPEDIENTE PARA DIRDAC
        // =======================================================
        [HttpGet]
        public ActionResult Detalle(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID de solicitud inválido.");
            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            // Confirmar que Condiciones y Limitaciones tiene firma DIRCAV
            var firmaDircav = _firmaDao.ObtenerUltimoPorSolicitudTipo(id, "CONDICIONES")
                ?? _firmaDao.ObtenerUltimoPorSolicitudTipo(id, "CONDICIONES_LIMITACIONES");
            ViewBag.TieneFirmaDircav = firmaDircav != null;

            return View(solicitud);
        }

        // =======================================================
        // 3. FIRMAR Y LEGALIZAR AOCR (EXCLUSIVO DIRDAC)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarAOCR(int id, string passwordCertificado)
        {
            if (!EsDirdacAutorizado())
            {
                return new HttpStatusCodeResult(403, "Acceso denegado: La firma y legalización del AOCR es exclusiva del rol DIRDAC.");
            }

            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            if (string.Equals(solicitud.Estado, AocrEstadosProceso.AocrFirmadaDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitud.Estado, AocrEstadosProceso.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpStatusCodeResult(409, "Conflicto: El AOCR ya se encuentra firmado o el proceso finalizado.");
            }

            var firmaDircav = _firmaDao.ObtenerUltimoPorSolicitudTipo(id, "CONDICIONES")
                ?? _firmaDao.ObtenerUltimoPorSolicitudTipo(id, "CONDICIONES_LIMITACIONES");
            var clFirmada = firmaDircav != null
                || string.Equals(solicitud.Estado, AocrEstadosProceso.AocrPendienteDirdac, StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitud.Estado, AocrEstadosProceso.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase);

            if (!clFirmada)
            {
                return new HttpStatusCodeResult(409, "Conflicto: No se puede legalizar el AOCR sin la firma previa obligatoria de DIRCAV en Condiciones y Limitaciones.");
            }

            solicitud.Estado = AocrEstadosProceso.AocrFirmadaDirdac;
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            TempData["Success"] = "Documento AOCR firmado y legalizado formalmente por la Dirección General (DIRDAC).";
            return RedirectToAction("Bandeja", new { tab = "firma" });
        }

        // =======================================================
        // 4. DEVOLVER EXPEDIENTE A DIRCAV (DIRDAC)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DevolverDIRCAV(int id, string motivo)
        {
            if (!EsDirdacAutorizado())
            {
                return new HttpStatusCodeResult(403, "Solo el rol DIRDAC puede devolver el expediente a DIRCAV.");
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                TempData["Error"] = "El motivo de la devolución a DIRCAV es obligatorio.";
                return RedirectToAction("Detalle", new { id });
            }

            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            if (string.Equals(solicitud.Estado, AocrEstadosProceso.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpStatusCodeResult(409, "Conflicto: No se puede devolver un trámite concluido.");
            }

            solicitud.Estado = AocrEstadosProceso.DevueltoDircavPorDirdac;
            solicitud.Observaciones = motivo;
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            TempData["Warning"] = "Expediente devuelto a DIRCAV con las observaciones requeridas.";
            return RedirectToAction("Bandeja", new { tab = "revision" });
        }

        // =======================================================
        // 5. CONFIRMAR LEGALIZACIÓN Y CIERRE FORMAL (DIRDAC)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmarLegalizacion(int id)
        {
            if (!EsDirdacAutorizado())
            {
                return new HttpStatusCodeResult(403, "Solo el rol DIRDAC puede confirmar la conclusión institucional del trámite.");
            }

            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            solicitud.Estado = AocrEstadosProceso.Finalizado;
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            TempData["Success"] = "Trámite AOCR legalizado y finalizado exitosamente. Notificado a RT e Inspector.";
            return RedirectToAction("Bandeja", new { tab = "concluidos" });
        }
    }
}
