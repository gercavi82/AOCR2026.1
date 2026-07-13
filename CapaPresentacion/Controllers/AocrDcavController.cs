using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using CapaDatos.DAOs;
using CapaDatos.Services;
using CapaNegocio.Services;
using CapaNegocio.DTOs;
using CapaPresentacion.Models;
using CapaPresentacion.Services;

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
        private readonly IUsuarioContextoService _usuarioContexto = new UsuarioContextoService();

        private IRevisionDocumentosDcavService CrearRevisionDocumentosService()
        {
            return new RevisionDocumentosDcavService(
                new AocrDcavDocumentosDAO(),
                new DocumentoPdfService(Server.MapPath("~/App_Data/AOCR")));
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult RevisionDocumentos()
        {
            try
            {
                var u=_usuarioContexto.ObtenerContextoActual();
                var items=CrearRevisionDocumentosService().ObtenerPendientes(u.UsuarioId,ResolverRolDcav(u));
                return View("~/Views/AocrDcav/RevisionDocumentos.cshtml",RevisionDocumentosDcavViewModelFactory.Bandeja(items));
            }
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult DetalleDocumentos(int solicitudId,int inspeccionId)
        {
            try
            {
                var u=_usuarioContexto.ObtenerContextoActual();var svc=CrearRevisionDocumentosService();
                var d=svc.ObtenerDetalle(solicitudId,inspeccionId,u.UsuarioId,ResolverRolDcav(u));
                var pdf=new DocumentoPdfService(Server.MapPath("~/App_Data/AOCR"));
                var model=RevisionDocumentosDcavViewModelFactory.Detalle(d,pdf.ObtenerPorId(d.AocrPdfId),pdf.ObtenerPorId(d.CondicionesPdfId));model.Observaciones=new DevolucionDocumentosDcavService().ObtenerObservaciones(solicitudId).Select(x=>new ObservacionVerificacionDcavViewModel{ObservacionId=x.ObservacionId,TipoDocumento=x.TipoDocumento,Seccion=x.Seccion,Campo=x.Campo,Texto=x.Texto,Estado=x.Estado,DocumentoCorreccionId=x.DocumentoCorreccionId,VersionCorreccion=x.VersionCorreccion}).ToList();
                return View("~/Views/AocrDcav/DetalleDocumentos.cshtml",model);
            }
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult HistorialDocumentos(int solicitudId,int inspeccionId)
        {
            try
            {
                var u=_usuarioContexto.ObtenerContextoActual();var hist=CrearRevisionDocumentosService().ObtenerHistorial(solicitudId,inspeccionId,u.UsuarioId,ResolverRolDcav(u));
                var model=hist.Select(h=>new HistorialDcavViewModel{Accion=h.Accion,Usuario=h.UsuarioNombre,Rol=h.Rol,EstadoAnterior=h.EstadoAnterior,EstadoNuevo=h.EstadoNuevo,Observacion=h.Observacion,Fecha=h.Fecha}).ToList();
                return View("~/Views/AocrDcav/HistorialDocumentos.cshtml",model);
            }
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult AprobarDocumentos(AprobarDocumentosDcavRequest request)
        {
            try
            {
                var u=_usuarioContexto.ObtenerContextoActual();request=request??new AprobarDocumentosDcavRequest();
                request.UsuarioDcavId=u.UsuarioId;request.Rol=ResolverRolDcav(u);request.Ip=Request!=null?Request.UserHostAddress:null;request.CorrelationId=HttpContext!=null?HttpContext.Items["CorrelationId"] as string:null;
                var result=new AprobacionDocumentosDcavService(new AocrDcavDocumentosDAO(),new AprobacionDocumentosDcavDAO(),new DocumentoPdfService(Server.MapPath("~/App_Data/AOCR"))).Aprobar(request);
                TempData["Success"]=result.Mensaje;return RedirectToAction("RevisionDocumentos");
            }
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
            catch(UsuarioContextoInvalidoException ex){return EstadoDocumentos(401,ex.Message);}
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult DevolverDocumentos(DevolverDocumentosDcavRequest request)
        {
            try
            {
                var u=_usuarioContexto.ObtenerContextoActual();request=request??new DevolverDocumentosDcavRequest();
                request.UsuarioId=u.UsuarioId;request.UsuarioNombre=u.NombreCompleto;request.Rol=ResolverRolDcav(u);
                request.Ip=Request!=null?Request.UserHostAddress:null;request.CorrelationId=HttpContext!=null?HttpContext.Items["CorrelationId"] as string:null;
                var result=new DevolucionDocumentosDcavService().Devolver(request);
                TempData["Success"]=result.Mensaje;return RedirectToAction("RevisionDocumentos");
            }
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
            catch(UsuarioContextoInvalidoException ex){return EstadoDocumentos(401,ex.Message);}
        }

        [HttpPost,ValidateAntiForgeryToken]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV")]
        public ActionResult CerrarObservacionDocumento(int observacionId,int solicitudId,int inspeccionId,int documentoCorreccionId)
        {
            try{var u=_usuarioContexto.ObtenerContextoActual();new DevolucionDocumentosDcavService().Cerrar(new CambiarEstadoObservacionDcavRequest{ObservacionId=observacionId,SolicitudId=solicitudId,DocumentoCorreccionId=documentoCorreccionId,UsuarioId=u.UsuarioId,Rol=ResolverRolDcav(u)});TempData["Success"]="Observación verificada y cerrada por DCAV.";return RedirectToAction("DetalleDocumentos",new{solicitudId,inspeccionId});}
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
        }

        private ActionResult DecidirDocumentos(DecisionDocumentosDcavRequest request,bool aprobar)
        {
            try
            {
                var u=_usuarioContexto.ObtenerContextoActual();request=request??new DecisionDocumentosDcavRequest();
                request.UsuarioId=u.UsuarioId;request.Rol=ResolverRolDcav(u);request.Ip=Request!=null?Request.UserHostAddress:null;
                request.CorrelationId=HttpContext!=null?HttpContext.Items["CorrelationId"] as string:null;
                var result=aprobar?CrearRevisionDocumentosService().AprobarDocumentos(request):CrearRevisionDocumentosService().DevolverDocumentos(request);
                TempData["Success"]=result.Mensaje;return RedirectToAction("RevisionDocumentos");
            }
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
            catch(UsuarioContextoInvalidoException ex){return EstadoDocumentos(401,ex.Message);}
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult VerInformeDocumento(int solicitudId,int inspeccionId)
        {
            try{var u=_usuarioContexto.ObtenerContextoActual();var d=CrearRevisionDocumentosService().ObtenerDetalle(solicitudId,inspeccionId,u.UsuarioId,ResolverRolDcav(u));return ServirPdfFirmado(d.InformeRuta,"InformeTecnico_"+solicitudId+".pdf");}
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult VerLvDocumento(int solicitudId,int inspeccionId)
        {
            try{var u=_usuarioContexto.ObtenerContextoActual();var d=CrearRevisionDocumentosService().ObtenerDetalle(solicitudId,inspeccionId,u.UsuarioId,ResolverRolDcav(u));return ServirPdfFirmado(d.LvEaeRuta,"LV_EAE_"+solicitudId+".pdf");}
            catch(RevisionDocumentosDcavException ex){return EstadoDocumentos(ex.Codigo,ex.Message);}
        }

        private static string ResolverRolDcav(UsuarioContextoDto u)
        {
            var raw=u!=null&&u.RolesRaw!=null?u.RolesRaw.FirstOrDefault(x=>(x??string.Empty).IndexOf("DCAV",StringComparison.OrdinalIgnoreCase)>=0):null;
            return !string.IsNullOrWhiteSpace(raw)?raw:(u!=null?u.RolActivo:null);
        }

        private ActionResult EstadoDocumentos(int codigo,string mensaje)
        {
            Response.StatusCode=codigo;Response.TrySkipIisCustomErrors=true;return Content(mensaje??"Error en revision documental DCAV.","text/plain");
        }

        [HttpGet]
        [Authorize(Roles = "DIRECTOR_CERTIFICACIONES_DCAV,DirectorCertificacionesDcav,DirectorCertificacionesDCAV,Director de Certificaciones DCAV,DirectorDCAV,DCAV,Administrador")]
        public ActionResult Revision(string fase)
        {
            if (string.Equals((fase ?? string.Empty).Trim(), "documentos", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("RevisionDocumentos");
            var items = _service.ListarPendientesInformes();
            var faseNormalizada = (fase ?? string.Empty).Trim().ToLowerInvariant();
            if (faseNormalizada == "informe")
            {
                items = items.Where(i => i != null && string.Equals(i.TipoRevision, "INFORME_TECNICO", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            faseNormalizada = "informe";

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
        public ActionResult Aprobar(int solicitudId,long? versionRegistro=null,int? inspeccionId=null,int? informeId=null,int? versionInforme=null,string claveIdempotencia=null)
        {
            try
            {
                var result = _service.AprobarEnviarDirectorGeneral(
                    solicitudId,
                    ObtenerUsuarioActualId(),
                    ObtenerRolActual(),
                    "Aprobado por Director de Certificaciones DCAV.",
                    versionRegistro,inspeccionId,informeId,versionInforme,claveIdempotencia,
                    Request != null ? Request.UserHostAddress : null,
                    HttpContext != null ? HttpContext.Items["CorrelationId"] as string : null);

                if (!result.Ok)
                {
                    Response.StatusCode = result.Codigo > 0 ? result.Codigo : 500;
                    Response.TrySkipIisCustomErrors = true;
                    return Content(result.Mensaje ?? "No se pudo completar la habilitacion.", "text/plain");
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
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Content("No se pudo completar la aprobacion. La operacion fue revertida y puede intentarla nuevamente.", "text/plain");
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
        [NonAction]
        public ActionResult EnviarRevisionDcav(int solicitudId)
        {
            return new HttpStatusCodeResult(
                410,
                "Use la accion conjunta FINALIZAR REVISION Y ENVIAR A DCAV de Documentos Finales.");
        }

        private int ObtenerUsuarioActualId()
        {
            object valor = Session != null ? (Session["UsuarioId"] ?? Session["UserId"] ?? Session["IdUsuario"]) : null;
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
                : (User != null && User.IsInRole("DirectorGeneral") ? "DirectorGeneral" : string.Empty);
        }
    }
}
