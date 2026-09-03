using System;
using System.Collections.Generic;
using System.IO;
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
    /// Controlador exclusivo para el rol DIRCAV (Director DIRCAV / Certificación Aeronáutica).
    /// Maneja la aceptación documental, designación oficial de inspectores, revisión de informes
    /// técnicos, firma exclusiva de Condiciones y Limitaciones y remisión a DIRDAC.
    /// Bloquea terminantemente acciones operativas del Administrador (Regla 7) y de DIRDAC.
    /// </summary>
    [Authorize(Roles = "DIRCAV,DCAV,Administrador")]
    public class DircavController : Controller
    {
        private readonly DircavBandejaService _bandejaService;
        private readonly DircavDesignacionService _designacionService;
        private readonly DesignacionDocumentoService _documentoDesignacionService;
        private readonly SolicitudAOCRDAO _solicitudDao;
        private readonly InspeccionDAO _inspeccionDao;
        private readonly AocrFirmaDocumentoDAO _firmaDao;
        private readonly SolicitudEstacionService _estacionService;

        public DircavController()
        {
            _bandejaService = new DircavBandejaService();
            _designacionService = new DircavDesignacionService();
            _documentoDesignacionService = new DesignacionDocumentoService();
            _solicitudDao = new SolicitudAOCRDAO();
            _inspeccionDao = new InspeccionDAO();
            _firmaDao = new AocrFirmaDocumentoDAO();
            _estacionService = new SolicitudEstacionService();
        }

        public DircavController(
            DircavBandejaService bandejaService,
            DircavDesignacionService designacionService,
            SolicitudAOCRDAO solicitudDao,
            InspeccionDAO inspeccionDao,
            AocrFirmaDocumentoDAO firmaDao,
            SolicitudEstacionService estacionService,
            DesignacionDocumentoService documentoDesignacionService = null)
        {
            _bandejaService = bandejaService;
            _designacionService = designacionService;
            _documentoDesignacionService = documentoDesignacionService ?? new DesignacionDocumentoService();
            _solicitudDao = solicitudDao;
            _inspeccionDao = inspeccionDao;
            _firmaDao = firmaDao;
            _estacionService = estacionService;
        }

        private string ObtenerRolActual()
        {
            return Session != null && Session["Rol"] != null ? Session["Rol"].ToString() : string.Empty;
        }

        private int ObtenerUsuarioIdActual()
        {
            if (Session != null && Session["UsuarioId"] != null)
            {
                int.TryParse(Session["UsuarioId"].ToString(), out var id);
                return id;
            }
            return 0;
        }

        private string ObtenerUsuarioLoginActual()
        {
            if (Session != null && Session["Usuario"] != null)
            {
                return Session["Usuario"].ToString();
            }
            return User != null && User.Identity != null ? User.Identity.Name : "DIRCAV";
        }

        private bool EsDircavAutorizado()
        {
            var rolActual = ObtenerRolActual();
            return _designacionService.EsDircavAutorizado(rolActual);
        }

        // =======================================================
        // 1. BANDEJA INSTITUCIONAL DIRCAV
        // =======================================================
        [HttpGet]
        public ActionResult Bandeja(string tab = "documentacion")
        {
            if (!EsDircavAutorizado() && !User.IsInRole("DIRCAV") && !User.IsInRole("DCAV"))
            {
                return new HttpStatusCodeResult(403, "No tiene permisos para acceder a la bandeja DIRCAV.");
            }

            ViewBag.ActiveTab = tab ?? "documentacion";
            ViewBag.ContadorDocumentacion = _bandejaService.ContarDocumentacionPendienteAceptacion();
            ViewBag.ContadorDesignaciones = _bandejaService.ContarDesignacionesPendientes();
            ViewBag.ContadorInformes = _bandejaService.ContarInformesPendientesRevision();
            ViewBag.ContadorCondiciones = _bandejaService.ContarCondicionesPendientesFirma();
            ViewBag.ContadorRemision = _bandejaService.ContarExpedientesPendientesRemisionDirdac();

            var docPendiente = _bandejaService.ObtenerDocumentacionPendienteAceptacion();
            if (docPendiente != null)
            {
                foreach (var item in docPendiente)
                {
                    try
                    {
                        item.Estaciones = _estacionService.ObtenerEstacionesPorSolicitud(
                            item.CodigoSolicitud, item, _inspeccionDao.ListarPorSolicitud(item.CodigoSolicitud));
                    }
                    catch { }
                }
            }
            ViewBag.DocumentacionPendiente = docPendiente;

            var desigPendiente = _bandejaService.ObtenerDesignacionesPendientes();
            if (desigPendiente != null)
            {
                foreach (var item in desigPendiente)
                {
                    try
                    {
                        item.Estaciones = _estacionService.ObtenerEstacionesPorSolicitud(
                            item.CodigoSolicitud, item, _inspeccionDao.ListarPorSolicitud(item.CodigoSolicitud));
                    }
                    catch { }
                }
            }
            ViewBag.DesignacionesPendientes = desigPendiente;

            ViewBag.DesignacionesFirmadas = _bandejaService.ObtenerDesignacionesFirmadas();
            ViewBag.InformesPendientes = _bandejaService.ObtenerInformesPendientesRevision();
            ViewBag.CondicionesPendientes = _bandejaService.ObtenerCondicionesPendientesFirma();
            ViewBag.ExpedientesRemision = _bandejaService.ObtenerExpedientesPendientesRemisionDirdac();
            ViewBag.ExpedientesDevueltos = _bandejaService.ObtenerExpedientesDevueltos();
            ViewBag.Historial = _bandejaService.ObtenerHistorialGestionados();

            return View();
        }

        [HttpGet]
        public ActionResult BandejaDocumentacion()
        {
            return RedirectToAction("Bandeja", new { tab = "documentacion" });
        }

        // =======================================================
        // 2. DETALLE DE EXPEDIENTE PARA DIRCAV
        // =======================================================
        [HttpGet]
        public ActionResult Detalle(int id)
        {
            if (id <= 0) return new HttpStatusCodeResult(400, "ID de solicitud inválido.");
            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            solicitud.Estaciones = _estacionService.ObtenerEstacionesPorSolicitud(
                id, solicitud, _inspeccionDao.ListarPorSolicitud(id));
            ViewBag.EstacionesSolicitud = solicitud.Estaciones;

            return View(solicitud);
        }

        [HttpGet]
        public ActionResult DetalleExpediente(int id)
        {
            return RedirectToAction("Detalle", new { id });
        }

        // =======================================================
        // 3. INSPECTORES DISPONIBLES (AJAX SELECTOR)
        // =======================================================
        [HttpGet]
        public ActionResult InspectoresDisponibles()
        {
            if (!EsDircavAutorizado() && !User.IsInRole("DIRCAV") && !User.IsInRole("DCAV"))
            {
                return new HttpStatusCodeResult(403, "Acceso denegado: Se requiere rol DIRCAV.");
            }

            var inspectores = _designacionService.ListarInspectoresDisponibles()
                .Select(i => new
                {
                    cedula = i.Cedula ?? i.UsuarioLogin,
                    nombre = i.NombreCompleto ?? i.UsuarioLogin,
                    tipo = i.Tipo ?? "AIR",
                    rol = i.RolInterno ?? "Inspector"
                })
                .ToList();

            return Json(inspectores, JsonRequestBehavior.AllowGet);
        }

        // =======================================================
        // 4. ACEPTAR DOCUMENTACIÓN FORMALMENTE (DIRCAV)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AceptarDocumentacion(int id, string observacion)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();
            var usuarioLogin = ObtenerUsuarioLoginActual();

            var resultado = _designacionService.AceptarDocumentacion(id, usuarioId, usuarioLogin, rol);
            if (!resultado.Exitoso)
            {
                if (resultado.HttpStatusCode == 403)
                {
                    return new HttpStatusCodeResult(403, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 409)
                {
                    return new HttpStatusCodeResult(409, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 404)
                {
                    return HttpNotFound(resultado.Mensaje);
                }

                TempData["Error"] = resultado.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["Success"] = resultado.Mensaje;
            return RedirectToAction("Bandeja", new { tab = "designaciones" });
        }

        // =======================================================
        // 5. DEVOLVER AL COORDINADOR (DIRCAV)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DevolverCoordinador(int id, string motivo)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();
            var usuarioLogin = ObtenerUsuarioLoginActual();

            var resultado = _designacionService.DevolverAlCoordinador(id, usuarioId, usuarioLogin, motivo, rol);
            if (!resultado.Exitoso)
            {
                if (resultado.HttpStatusCode == 403)
                {
                    return new HttpStatusCodeResult(403, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 409)
                {
                    return new HttpStatusCodeResult(409, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 404)
                {
                    return HttpNotFound(resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 400)
                {
                    TempData["Error"] = resultado.Mensaje;
                    return RedirectToAction("Detalle", new { id });
                }

                TempData["Error"] = resultado.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["Warning"] = resultado.Mensaje;
            return RedirectToAction("Bandeja", new { tab = "devueltos" });
        }

        // =======================================================
        // 6. DESIGNAR FORMALMENTE AL INSPECTOR (DIRCAV)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DesignarInspector(int id, string inspectorCedula, string inspectorApoyoCedula, string observacion, int? estacionId)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();
            var usuarioLogin = ObtenerUsuarioLoginActual();

            var request = new DircavDesignacionRequest
            {
                SolicitudId = id,
                EstacionId = estacionId,
                InspectorPrincipalCedula = inspectorCedula,
                InspectorApoyoCedula = inspectorApoyoCedula,
                Motivo = observacion,
                DircavUsuarioId = usuarioId,
                DircavUsuarioNombre = usuarioLogin,
                RolSolicitante = rol
            };

            var resultado = _designacionService.DesignarInspector(request);
            if (!resultado.Exitoso)
            {
                if (resultado.HttpStatusCode == 403)
                {
                    return new HttpStatusCodeResult(403, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 409)
                {
                    return new HttpStatusCodeResult(409, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 404)
                {
                    return HttpNotFound(resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 400)
                {
                    TempData["Error"] = resultado.Mensaje;
                    return RedirectToAction("Detalle", new { id });
                }

                TempData["Error"] = resultado.Mensaje;
                return RedirectToAction("Detalle", new { id });
            }

            TempData["Success"] = resultado.Mensaje;
            return RedirectToAction("Bandeja", new { tab = "designaciones" });
        }

        // =======================================================
        // 6. DETALLE DE DESIGNACIÓN (DIRCAV / COORDINACIÓN)
        // =======================================================
        [HttpGet]
        public ActionResult DesignacionDetalle(int id)
        {
            var rol = ObtenerRolActual();
            if (!_designacionService.EsDircavAutorizado(rol) && !AocrRolesInstitucionales.EsCoordinador(rol))
            {
                return new HttpStatusCodeResult(403, "Acceso denegado: Se requiere rol DIRCAV o Coordinación.");
            }

            try
            {
                var vm = _documentoDesignacionService.ConstruirDatosDesignacion(id);
                return Json(vm, JsonRequestBehavior.AllowGet);
            }
            catch (KeyNotFoundException ex)
            {
                return HttpNotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(400, ex.Message);
            }
        }

        // =======================================================
        // 7. VISTA PREVIA DEL PDF DE DESIGNACIÓN (DIRCAV)
        // =======================================================
        [HttpGet]
        public ActionResult VistaPreviaDesignacion(int id)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();

            try
            {
                var pdfBytes = _documentoDesignacionService.GenerarVistaPrevia(id, usuarioId, rol);
                Response.AppendHeader("Content-Disposition", $"inline; filename=VistaPrevia_Designacion_{id}.pdf");
                return File(pdfBytes, "application/pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(403, ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return HttpNotFound(ex.Message);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Detalle", new { id });
            }
        }

        // =======================================================
        // 8. FIRMAR DESIGNACIÓN DE INSPECTOR (EXCLUSIVO DIRCAV - AC-06)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarDesignacion(int id, string passwordCertificado)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();
            var usuarioLogin = ObtenerUsuarioLoginActual();

            // Validación estricta de rol exclusivo DIRCAV (bloquea DIRDAC, Admin, Coord e Inspector)
            if (!EsDircavAutorizado())
            {
                return new HttpStatusCodeResult(403, "Acceso denegado: La firma del oficio de designación es competencia exclusiva de la Autoridad DIRCAV.");
            }

            var resultado = _documentoDesignacionService.FirmarDesignacion(
                id,
                usuarioId,
                usuarioLogin,
                rol,
                certificadoBytes: null,
                passwordCert: passwordCertificado
            );

            if (!resultado.Exitoso)
            {
                if (resultado.HttpStatusCode == 403)
                {
                    return new HttpStatusCodeResult(403, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 409)
                {
                    return new HttpStatusCodeResult(409, resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 404)
                {
                    return HttpNotFound(resultado.Mensaje);
                }
                if (resultado.HttpStatusCode == 400)
                {
                    TempData["Error"] = resultado.Mensaje;
                    return RedirectToAction("Detalle", new { id });
                }

                return new HttpStatusCodeResult(resultado.HttpStatusCode, resultado.Mensaje);
            }

            TempData["Success"] = resultado.Mensaje;
            return RedirectToAction("Bandeja", new { tab = "designaciones" });
        }

        // =======================================================
        // 9. DESCARGAR DESIGNACIÓN FIRMADA (DIRCAV / COORDINACIÓN)
        // =======================================================
        [HttpGet]
        public ActionResult DescargarDesignacion(int id)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();
            var usuarioLogin = ObtenerUsuarioLoginActual();

            try
            {
                string nombreArchivo;
                var pdfBytes = _documentoDesignacionService.ObtenerDocumentoParaDescarga(
                    id,
                    usuarioId,
                    rol,
                    usuarioLogin,
                    out nombreArchivo
                );

                Response.AppendHeader("Content-Disposition", $"attachment; filename={nombreArchivo}");
                return File(pdfBytes, "application/pdf", nombreArchivo);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(403, ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                return HttpNotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, ex.Message);
            }
        }

        // =======================================================
        // 10. DESCARGAR DESIGNACIÓN PARA EL INSPECTOR ASIGNADO (AC-06)
        // =======================================================
        [HttpGet]
        [Authorize(Roles = "Inspector,INSPECTOR,DIRCAV,DCAV,Coordinador,COORDINADOR")]
        public ActionResult DescargarDesignacionInspector(int id)
        {
            var rol = ObtenerRolActual();
            var usuarioId = ObtenerUsuarioIdActual();
            var usuarioLogin = ObtenerUsuarioLoginActual();

            try
            {
                string nombreArchivo;
                var pdfBytes = _documentoDesignacionService.ObtenerDocumentoParaDescarga(
                    id,
                    usuarioId,
                    rol,
                    usuarioLogin,
                    out nombreArchivo
                );

                Response.AppendHeader("Content-Disposition", $"attachment; filename={nombreArchivo}");
                return File(pdfBytes, "application/pdf", nombreArchivo);
            }
            catch (UnauthorizedAccessException ex)
            {
                return new HttpStatusCodeResult(403, ex.Message);
            }
            catch (FileNotFoundException ex)
            {
                return HttpNotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, ex.Message);
            }
        }

        // =======================================================
        // 7. REVISAR INFORME TÉCNICO (DIRCAV)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RevisarInforme(int id, string observacion, bool aprobado = true)
        {
            if (!EsDircavAutorizado())
            {
                return new HttpStatusCodeResult(403, "Solo el rol DIRCAV puede resolver la revisión institucional del Informe Técnico.");
            }

            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            if (aprobado)
            {
                solicitud.Estado = AocrEstadosProceso.ClPendienteFirmaDircav;
                TempData["Success"] = "Informe Técnico aprobado por DIRCAV. Proceda a firmar Condiciones y Limitaciones.";
            }
            else
            {
                solicitud.Estado = AocrEstadosProceso.DevueltoCoordinadorFinalDircav;
                solicitud.Observaciones = observacion;
                TempData["Warning"] = "Informe Técnico observado y devuelto a Coordinación.";
            }

            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            return RedirectToAction("Bandeja", new { tab = "informes" });
        }

        // =======================================================
        // 8. FIRMAR CONDICIONES Y LIMITACIONES (EXCLUSIVO DIRCAV)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FirmarCondiciones(int id, string passwordCertificado)
        {
            if (!EsDircavAutorizado())
            {
                return new HttpStatusCodeResult(403, "Acceso denegado: La firma de Condiciones y Limitaciones es exclusiva del rol DIRCAV.");
            }

            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            if (string.Equals(solicitud.Estado, AocrEstadosProceso.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitud.Estado, AocrEstadosProceso.Finalizado, StringComparison.OrdinalIgnoreCase))
            {
                return new HttpStatusCodeResult(409, "Conflicto: Las Condiciones y Limitaciones ya se encuentran firmadas o el expediente finalizado.");
            }

            solicitud.Estado = AocrEstadosProceso.ClFirmadaDircav;
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            TempData["Success"] = "Condiciones y Limitaciones firmadas digitalmente por DIRCAV. Listo para remisión a DIRDAC.";
            return RedirectToAction("Bandeja", new { tab = "condiciones" });
        }

        // =======================================================
        // 9. REMITIR EXPEDIENTE Y AOCR A DIRDAC (DIRCAV)
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemitirDIRDAC(int id, string observacion)
        {
            if (!EsDircavAutorizado())
            {
                return new HttpStatusCodeResult(403, "Solo el rol DIRCAV puede remitir el expediente aprobado a DIRDAC.");
            }

            var solicitud = _solicitudDao.ObtenerPorId(id);
            if (solicitud == null) return HttpNotFound("Solicitud no encontrada.");

            // Validar que Condiciones y Limitaciones fue firmada previamente por DIRCAV
            var clFirmada = string.Equals(solicitud.Estado, AocrEstadosProceso.ClFirmadaDircav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(solicitud.Estado, AocrEstadosProceso.CondicionesFirmadasDcav, StringComparison.OrdinalIgnoreCase);

            if (!clFirmada)
            {
                return new HttpStatusCodeResult(409, "Conflicto: No se puede remitir el expediente a DIRDAC sin la firma previa obligatoria de Condiciones y Limitaciones por DIRCAV.");
            }

            solicitud.Estado = AocrEstadosProceso.AocrPendienteDirdac;
            solicitud.UpdatedAt = DateTime.Now;
            _solicitudDao.Actualizar(solicitud);

            TempData["Success"] = "Expediente y AOCR remitidos formalmente al Director General (DIRDAC) para su firma y legalización.";
            return RedirectToAction("Bandeja", new { tab = "remision" });
        }
    }
}
