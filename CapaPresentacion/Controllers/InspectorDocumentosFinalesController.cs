using System;
using System.Diagnostics;
using System.Web.Mvc;
using CapaPresentacion.Models;
using CapaPresentacion.Services;
using CapaNegocio.DTOs.EnvioDocumentosDcav;
using CapaNegocio.Services;

namespace CapaPresentacion.Controllers
{
    [Authorize(Roles="InspectorTecnico,Inspector,Tecnico")]
    public sealed class InspectorDocumentosFinalesController:AocrBaseController
    {
        public InspectorDocumentosFinalesController(IUsuarioContextoService usuarios):base(usuarios){}
        private IRevisionDocumentosInspectorService Servicio{get{return new RevisionDocumentosInspectorService(new FirmaAocrStorageService(Server));}}

        [HttpGet]
        public ActionResult Revision()
        {
            var acceso=ValidarRol();if(acceso!=null)return acceso;
            try{return View("~/Views/InspectorDocumentosFinales/Revision.cshtml",Servicio.ObtenerPendientes(UsuarioActualId,Url));}
            catch(RevisionDocumentosHttpException ex){return Estado(ex.Codigo,ex.Message);}
            catch(Exception ex){Trace.TraceError("[INSPECTOR_DOCS][BANDEJA_ERROR] "+ex);return Estado(500,"Error interno al cargar la bandeja.");}
        }

        [HttpGet]
        public ActionResult Detalle(int solicitudId)
        {
            var acceso=ValidarRol();if(acceso!=null)return acceso;
            try{return View("~/Views/InspectorDocumentosFinales/Detalle.cshtml",Servicio.ObtenerDetalle(solicitudId,UsuarioActualId,Url));}
            catch(RevisionDocumentosHttpException ex){return Estado(ex.Codigo,ex.Message);}
            catch(Exception ex){Trace.TraceError("[INSPECTOR_DOCS][DETALLE_ERROR] SolicitudId="+solicitudId+"; "+ex);return Estado(500,"Error interno al cargar los documentos finales.");}
        }

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult GuardarAocr(GuardarAocrInspectorRequest request){return Ejecutar(()=>Servicio.GuardarAocr(request,UsuarioActualId,UsuarioActual.NombreCompleto),request!=null?request.SolicitudId:0);}

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult GuardarCondiciones(GuardarCondicionesInspectorRequest request){return Ejecutar(()=>Servicio.GuardarCondiciones(request,UsuarioActualId,UsuarioActual.NombreCompleto),request!=null?request.SolicitudId:0);}

        [HttpGet]
        public ActionResult PrevisualizarAocr(int solicitudId){return Archivo(()=>Servicio.PrevisualizarAocr(solicitudId,UsuarioActualId,ControllerContext),"AOCR_Borrador.pdf");}

        [HttpGet]
        public ActionResult PrevisualizarCondiciones(int solicitudId){return Archivo(()=>Servicio.PrevisualizarCondiciones(solicitudId,UsuarioActualId,ControllerContext),"Condiciones_Borrador.pdf");}

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult GenerarPdfAocr(int solicitudId,int documentoId,int versionEsperada){return Ejecutar(()=>Servicio.GenerarPdfAocr(solicitudId,documentoId,versionEsperada,UsuarioActualId,UsuarioActual.NombreCompleto,ControllerContext),solicitudId);}

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult GenerarPdfCondiciones(int solicitudId,int documentoId,int versionEsperada){return Ejecutar(()=>Servicio.GenerarPdfCondiciones(solicitudId,documentoId,versionEsperada,UsuarioActualId,UsuarioActual.NombreCompleto,ControllerContext),solicitudId);}

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult AtenderObservacion(int observacionId,int solicitudId,int documentoCorreccionId)
        {
            var acceso=ValidarRol();if(acceso!=null)return acceso;
            try{new DevolucionDocumentosDcavService().Atender(new CapaNegocio.DTOs.CambiarEstadoObservacionDcavRequest{ObservacionId=observacionId,SolicitudId=solicitudId,DocumentoCorreccionId=documentoCorreccionId,UsuarioId=UsuarioActualId,Rol=RolActual});TempData["Success"]="Observación marcada como atendida.";return RedirectToAction("Detalle",new{solicitudId});}
            catch(RevisionDocumentosDcavException ex){return Estado(ex.Codigo,ex.Message);}
        }

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult FinalizarYEnviarDcav(EnviarDocumentosDcavRequest request)
        {
            var acceso=ValidarRol();if(acceso!=null)return acceso;
            if(request==null)return Estado(400,"Datos de envio invalidos.");
            request.UsuarioInspectorId=UsuarioActualId;request.Rol=RolActual;
            request.Ip=Request!=null?Request.UserHostAddress:null;
            request.CorrelationId=Guid.NewGuid().ToString("N");
            try
            {
                var result=new EnvioDocumentosDcavService(Server.MapPath("~/App_Data/AOCR")).FinalizarYEnviar(request);
                TempData["Success"]=result.Mensaje;
                return RedirectToAction("Revision");
            }
            catch(EnvioDocumentosDcavException ex){return Estado(ex.Codigo,ex.Message);}
            catch(Exception ex){Trace.TraceError("[INSPECTOR_DCAV][CONTROLLER_ERROR] "+ex);return Estado(500,"Error interno al enviar los documentos a DCAV.");}
        }

        [HttpGet]
        public ActionResult VerPdf(int solicitudId,string tipoDocumento,int version){return Archivo(()=>Servicio.ObtenerPdfGenerado(solicitudId,tipoDocumento,version,UsuarioActualId),"DocumentoFinal_v"+version+".pdf");}

        private ActionResult Ejecutar(Func<RevisionDocumentosOperacionResult> accion,int solicitudId)
        {
            var acceso=ValidarRol();if(acceso!=null)return acceso;
            try{var r=accion();if(!r.Ok)return Estado(r.Codigo,r.Mensaje);TempData["Success"]=r.Mensaje;return RedirectToAction("Detalle",new{solicitudId});}
            catch(RevisionDocumentosHttpException ex){return Estado(ex.Codigo,ex.Message);}
            catch(Exception ex){Trace.TraceError("[INSPECTOR_DOCS][OPERACION_ERROR] SolicitudId="+solicitudId+"; "+ex);return Estado(500,"Error interno al procesar el documento.");}
        }

        private ActionResult Archivo(Func<RevisionDocumentosOperacionResult> accion,string nombre)
        {
            var acceso=ValidarRol();if(acceso!=null)return acceso;
            try{var r=accion();if(!r.Ok)return Estado(r.Codigo,r.Mensaje);Response.Headers["X-Content-Type-Options"]="nosniff";Response.AddHeader("Content-Disposition","inline; filename=\""+nombre.Replace("\"","")+"\"");return File(r.Contenido,"application/pdf");}
            catch(RevisionDocumentosHttpException ex){return Estado(ex.Codigo,ex.Message);}
            catch(Exception ex){Trace.TraceError("[INSPECTOR_DOCS][PDF_ERROR] "+ex);return Estado(500,"Error interno al procesar el PDF.");}
        }

        private ActionResult ValidarRol(){var u=UsuarioActual;if(u==null||!u.EstaAutenticado)return Estado(401,"Usuario no autenticado.");return u.UsuarioId<=0?Estado(401,"Usuario invalido."):(!u.EsInspectorTecnico?Estado(403,"Solo InspectorTecnico puede acceder a esta seccion."):null);}
        private ActionResult Estado(int codigo,string mensaje){Response.StatusCode=codigo;Response.TrySkipIisCustomErrors=true;return Content(mensaje??"Error","text/plain");}
    }
}
