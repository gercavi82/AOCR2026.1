using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public class AocrDcavController : Controller
    {
        private readonly AocrDcavRevisionService _service = new AocrDcavRevisionService();
        private readonly InspeccionInformeDAO _informeDao = new InspeccionInformeDAO();
        private readonly ListaVerificacionOperacionalEaeDAO _listaDao = new ListaVerificacionOperacionalEaeDAO();
        private readonly InspeccionDAO _inspeccionDao = new InspeccionDAO();
        private readonly AocrDocumentoGeneradoDAO _documentoDao = new AocrDocumentoGeneradoDAO();
        private readonly CapaDatos.Services.ILoggingService _logger = CapaDatos.Services.LoggingServiceFactory.Create();

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult Revision(string fase)
        {
            var items = _service.ListarPendientes();
            var faseNormalizada = (fase ?? string.Empty).Trim().ToLowerInvariant();
            if (faseNormalizada == "informe")
            {
                items = items.Where(i => i != null && string.Equals(i.TipoRevision, "INFORME_TECNICO", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else if (faseNormalizada == "documentos")
            {
                items = items.Where(i => i != null && string.Equals(i.TipoRevision, "DOCUMENTOS_AOCR", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.FaseDcav = faseNormalizada;
            ViewBag.ResumenBandejaDcav = _service.ObtenerResumenBandeja();
            Trace.TraceInformation("[DCAV_BANDEJA][OPEN] Usuario=" + (User != null && User.Identity != null ? User.Identity.Name : string.Empty) + "; Rol=" + ObtenerRolActual() + "; Registros=" + (items != null ? items.Count : 0) + ";");
            _logger.LogInfo("[DCAV_BANDEJA][OPEN] Usuario=" + (User != null && User.Identity != null ? User.Identity.Name : string.Empty)
                + "; Rol=" + ObtenerRolActual()
                + "; Registros=" + (items != null ? items.Count : 0)
                + "; Fase=" + faseNormalizada
                + "; GeneradosSinFirma=" + (((AocrDcavBandejaResumen)ViewBag.ResumenBandejaDcav).InformesGeneradosSinFirma) + ";");
            return View("~/Views/AocrDcav/Revision.cshtml", items);
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult Detalle(int solicitudId)
        {
            var item = _service.ObtenerDetalle(solicitudId);
            if (item == null)
            {
                return HttpNotFound("No existe expediente AOCR para revision DCAV.");
            }

            return View("~/Views/AocrDcav/Detalle.cshtml", item);
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult VerInformeFirmado(int solicitudId)
        {
            var item = _service.ObtenerDetalle(solicitudId);
            if (item == null || item.InformeId <= 0)
            {
                return HttpNotFound("No existe Informe Tecnico disponible para revision DCAV.");
            }

            var informe = _informeDao.ObtenerPorId(item.InformeId);
            if (!AocrDcavRevisionService.EsInformeFirmadoValido(informe))
            {
                return new HttpStatusCodeResult(409, "El Informe Tecnico no cuenta con una firma valida del Inspector.");
            }

            return ServirPdfFirmado(informe.RutaDocumentoFirmado, "InformeTecnicoFirmado_" + solicitudId + ".pdf");
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult VerListaFirmada(int solicitudId)
        {
            var item = _service.ObtenerDetalle(solicitudId);
            if (item == null || item.InspeccionId <= 0)
            {
                return HttpNotFound("No existe LV/EAE disponible para revision DCAV.");
            }

            var lista = _listaDao.ObtenerUltimaPorInspeccion(item.InspeccionId);
            if (lista == null || !lista.Finalizado || !lista.FirmadoTecnico || string.IsNullOrWhiteSpace(lista.RutaDocumentoFirmado))
            {
                return new HttpStatusCodeResult(409, "La LV/EAE no cuenta con una firma valida del Inspector.");
            }

            return ServirPdfFirmado(lista.RutaDocumentoFirmado, "ListaVerificacionFirmada_" + solicitudId + ".pdf");
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult VerDocumentoEnviado(int solicitudId, string tipoDocumento)
        {
            var item = _service.ObtenerDetalle(solicitudId);
            if (item == null || !string.Equals(item.TipoRevision, "DOCUMENTOS_AOCR", StringComparison.OrdinalIgnoreCase))
            {
                return HttpNotFound("El expediente no se encuentra en revisión documental DCAV.");
            }

            var tipo = string.Equals(tipoDocumento, "CONDICIONES_LIMITACIONES", StringComparison.OrdinalIgnoreCase)
                ? "CONDICIONES_LIMITACIONES"
                : "RECONOCIMIENTO";
            var documento = _documentoDao.ObtenerUltimoPorSolicitudTipo(solicitudId, tipo);
            if (documento == null || string.IsNullOrWhiteSpace(documento.RutaDocumento))
            {
                return HttpNotFound("El documento enviado no se encuentra disponible.");
            }

            return ServirPdfFirmado(documento.RutaDocumento, tipo == "RECONOCIMIENTO" ? "AOCR.pdf" : "CondicionesLimitaciones.pdf");
        }

        private ActionResult ServirPdfFirmado(string rutaRelativa, string nombreArchivo)
        {
            var relativa = (rutaRelativa ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(relativa))
            {
                return HttpNotFound("El archivo firmado no se encuentra registrado.");
            }

            var raiz = Path.GetFullPath(Server.MapPath("~/App_Data/Uploads"));
            var ruta = Path.GetFullPath(Server.MapPath("~" + (relativa.StartsWith("/") ? relativa : "/" + relativa)));
            if (!ruta.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(ruta))
            {
                return HttpNotFound("El archivo firmado no se encuentra disponible.");
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.AddHeader("Content-Disposition", "inline; filename=\"" + nombreArchivo.Replace("\"", string.Empty) + "\"");
            return File(ruta, "application/pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult Aprobar(int solicitudId)
        {
            try
            {
                var result = _service.AprobarEnviarDirectorGeneral(
                    solicitudId,
                    ObtenerUsuarioActualId(),
                    ObtenerRolActual(),
                    "Aprobado por Director de Certificaciones DCAV.");

                if (!result.Ok)
                {
                    TempData["Error"] = result.Mensaje;
                    return RedirectToAction("Detalle", new { solicitudId });
                }

                TempData["Success"] = result.Mensaje;
                return RedirectToAction("Revision");
            }
            catch (Exception ex)
            {
                _logger.LogError("[DCAV_TRANSICION][APROBAR_ERROR] SolicitudId=" + solicitudId
                    + "; Usuario=" + ObtenerUsuarioActualId()
                    + "; Error=" + ex + ";");
                TempData["Error"] = "No se pudo completar la aprobación. La operación fue revertida y puede intentarla nuevamente.";
                return RedirectToAction("Detalle", new { solicitudId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult Devolver(int solicitudId, string destino, string observacion)
        {
            var result = _service.DevolverConObservaciones(
                solicitudId,
                destino,
                observacion,
                ObtenerUsuarioActualId(),
                ObtenerRolActual());

            if (!result.Ok)
            {
                TempData["Error"] = result.Mensaje;
                return RedirectToAction("Detalle", new { solicitudId });
            }

            TempData["Success"] = result.Mensaje;
            return RedirectToAction("Revision");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Inspector,InspectorTecnico,Tecnico,Administrador")]
        public ActionResult EnviarRevisionDcav(int solicitudId)
        {
            if (User == null || !User.IsInRole("Administrador"))
            {
                var inspeccion = (_inspeccionDao.ListarPorSolicitud(solicitudId) ?? new System.Collections.Generic.List<CapaModelo.Inspeccion>())
                    .OrderByDescending(i => i.CodigoInspeccion)
                    .FirstOrDefault();
                if (inspeccion == null || !new AocrAuthorizationService().PuedeInspectorAbrirInspeccion(inspeccion.CodigoInspeccion, ObtenerUsuarioActualId()))
                {
                    return new HttpStatusCodeResult(403, "Solo el Inspector asignado puede enviar AOCR y Condiciones a DCAV.");
                }
            }

            var result = _service.EnviarRevisionDcav(
                solicitudId,
                ObtenerUsuarioActualId(),
                ObtenerRolActual(),
                "Inspector finaliza revision de AOCR y Condiciones y envia a revision DCAV.");

            if (!result.Ok)
            {
                TempData["Error"] = result.Mensaje;
            }
            else
            {
                TempData["Success"] = result.Mensaje;
            }

            return RedirectToAction("Index", "FirmaAocr", new { solicitudId });
        }

        private int ObtenerUsuarioActualId()
        {
            object valor = Session != null ? (Session["IdUsuario"] ?? Session["UserId"] ?? Session["CodigoUsuario"]) : null;
            int id;
            return valor != null && int.TryParse(Convert.ToString(valor), out id) ? id : 0;
        }

        private string ObtenerRolActual()
        {
            var rolSesion = Session != null ? Convert.ToString(Session["RolActual"] ?? Session["SelectedRole"] ?? Session["Rol"]) : null;
            if (!string.IsNullOrWhiteSpace(rolSesion))
            {
                return rolSesion.Trim();
            }

            return User != null && (User.IsInRole("DIRECTOR_CERTIFICACIONES_DCAV")
                || User.IsInRole("DirectorCertificacionesDcav")
                || User.IsInRole("DirectorCertificacionesDCAV")
                || User.IsInRole("Director de Certificaciones DCAV")
                || User.IsInRole("DirectorDCAV")
                || User.IsInRole("DCAV"))
                ? "DIRECTOR_CERTIFICACIONES_DCAV"
                : (User != null && User.IsInRole("DirectorGeneral") ? "DirectorGeneral" : "DCAV");
        }
    }
}
