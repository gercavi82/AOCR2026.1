using System;
using System.IO;
using System.Web.Mvc;
using CapaNegocio.DTOs;
using CapaNegocio.Services;
using CapaPresentacion.Services;
using CapaDatos.DAOs;

namespace CapaPresentacion.Controllers
{
    [Authorize]
    public sealed class FirmaInstitucionalAocrController:Controller
    {
        private readonly FirmaInstitucionalAocrService _consulta=new FirmaInstitucionalAocrService();
        private readonly IFirmaDocumentoInstitucionalService _firma=new FirmaDocumentoInstitucionalService();
        private readonly AocrFirmaDocumentoDAO _firmasRegistradas=new AocrFirmaDocumentoDAO();
        [HttpGet]public ActionResult Pendientes(){ConfigurarRolVista();return View("~/Views/FirmaInstitucionalAocr/Pendientes.cshtml",_consulta.ObtenerPendientes());}
        [HttpGet]public ActionResult Detalle(int solicitudId,int inspeccionId){var x=_consulta.ObtenerDetalle(solicitudId,inspeccionId);if(x==null)return HttpNotFound("El expediente no está pendiente de firma institucional.");ConfigurarRolVista();return View("~/Views/FirmaInstitucionalAocr/Detalle.cshtml",x);}
        [HttpGet]public ActionResult VerInforme(int solicitudId,int inspeccionId){var x=_consulta.ObtenerDetalle(solicitudId,inspeccionId);return x==null?HttpNotFound():Servir(x.InformeRuta,"InformeTecnico_"+solicitudId+".pdf");}
        [HttpGet]public ActionResult VerLvEae(int solicitudId,int inspeccionId){var x=_consulta.ObtenerDetalle(solicitudId,inspeccionId);return x==null?HttpNotFound():Servir(x.LvEaeRuta,"LV_EAE_"+solicitudId+".pdf");}
        [HttpGet]public ActionResult DescargarFirmado(int solicitudId,int inspeccionId,string tipoDocumento)
        {
            var d=_consulta.ObtenerDetalle(solicitudId,inspeccionId);
            if(d==null)return HttpNotFound();
            var solicitado=(tipoDocumento??string.Empty).Trim();
            string tipo;
            if(string.Equals(solicitado,"AOCR",StringComparison.OrdinalIgnoreCase)||string.Equals(solicitado,TiposDocumentoFirmaInstitucional.Aocr,StringComparison.OrdinalIgnoreCase))tipo=TiposDocumentoFirmaInstitucional.Aocr;
            else if(string.Equals(solicitado,"CONDICIONES",StringComparison.OrdinalIgnoreCase)||string.Equals(solicitado,TiposDocumentoFirmaInstitucional.Condiciones,StringComparison.OrdinalIgnoreCase))tipo=TiposDocumentoFirmaInstitucional.Condiciones;
            else return new HttpStatusCodeResult(400,"Tipo de documento institucional no válido.");
            var f=_firmasRegistradas.ObtenerUltimoPorSolicitudTipo(solicitudId,tipo);
            if(f==null||f.CodigoInspeccion!=inspeccionId)return HttpNotFound();
            return Servir(f.RutaDocumento,(tipo==TiposDocumentoFirmaInstitucional.Aocr?"AOCR_DGAC_":"CONDICIONES_DCAV_")+solicitudId+".pdf");
        }

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult FirmarAocr(int solicitudId,int inspeccionId,long versionExpediente)
        {return Firmar(solicitudId,inspeccionId,versionExpediente,TiposDocumentoFirmaInstitucional.Aocr);}

        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult FirmarCondiciones(int solicitudId,int inspeccionId,long versionExpediente)
        {return Firmar(solicitudId,inspeccionId,versionExpediente,TiposDocumentoFirmaInstitucional.Condiciones);}

        private ActionResult Firmar(int solicitudId,int inspeccionId,long versionExpediente,string tipo)
        {
            var u=AocrUserContextService.FromHttpContext(HttpContext);
            if(u==null||u.UsuarioId<=0){Response.StatusCode=401;return Json(new{ok=false,code=401,message="Usuario no autenticado."});}
            var correlation=HttpContext!=null?HttpContext.Items["CorrelationId"] as string:null;
            var r=_firma.Firmar(new FirmarDocumentoInstitucionalRequest{SolicitudId=solicitudId,InspeccionId=inspeccionId,TipoDocumento=tipo,UsuarioId=u.UsuarioId,VersionExpediente=versionExpediente,Ip=Request!=null?Request.UserHostAddress:null,CorrelationId=correlation});
            Response.StatusCode=r.CodigoHttp;
            return Json(new{ok=r.Exitoso,code=r.CodigoHttp,message=r.Mensaje,data=r.Exitoso?new{r.FirmaId,r.DocumentoId,r.PdfOrigenId,r.VersionDocumento,r.TipoDocumento,r.EstadoDocumento,r.EstadoExpediente,r.HashPdfFirmado,r.TamanioPdfFirmado,r.FechaFirma}:null});
        }

        private void ConfigurarRolVista()
        {
            ViewBag.EsDgac=User!=null&&(User.IsInRole("Direccion")||User.IsInRole("DirectorGeneral"));
            ViewBag.EsDcav=User!=null&&(User.IsInRole("DIRECTOR_CERTIFICACIONES_DCAV")||User.IsInRole("DirectorCertificacionesDcav")||User.IsInRole("DCAV"));
        }

        private ActionResult Servir(string ruta,string nombre){var rel=(ruta??"").Trim().Replace('\\','/');if(string.IsNullOrWhiteSpace(rel))return HttpNotFound();var root=Path.GetFullPath(Server.MapPath("~/App_Data/Uploads"));var path=Path.GetFullPath(Server.MapPath("~"+(rel.StartsWith("/")?rel:"/"+rel)));if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!System.IO.File.Exists(path))return HttpNotFound();Response.Headers["X-Content-Type-Options"]="nosniff";Response.AddHeader("Content-Disposition","inline; filename=\""+nombre.Replace("\"","")+"\"");return File(path,"application/pdf");}
    }
}
