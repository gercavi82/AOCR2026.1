using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaDatos.Services;
using CapaNegocio.DTOs;

namespace CapaNegocio.Services
{
    public interface IAprobacionDocumentosDcavService
    {
        ResultadoAprobacionDocumentosDcav Aprobar(AprobarDocumentosDcavRequest request);
        ResultadoValidacionAprobacion Validar(int solicitudId,int inspeccionId,int usuarioDcavId);
    }

    public sealed class AprobacionDocumentosDcavService:IAprobacionDocumentosDcavService
    {
        private const string Origen="PENDIENTE_REVISION_DOCUMENTOS_DCAV";
        private const string Destino="PENDIENTE_FIRMAS_INSTITUCIONALES";
        private readonly AocrDcavDocumentosDAO _paquete;
        private readonly AprobacionDocumentosDcavDAO _dao;
        private readonly IDocumentoPdfService _pdf;
        private readonly ILoggingService _log;
        public AprobacionDocumentosDcavService():this(new AocrDcavDocumentosDAO(),new AprobacionDocumentosDcavDAO(),new DocumentoPdfService(System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/AOCR"))){}
        public AprobacionDocumentosDcavService(AocrDcavDocumentosDAO paquete,AprobacionDocumentosDcavDAO dao,IDocumentoPdfService pdf){_paquete=paquete;_dao=dao;_pdf=pdf;_log=LoggingServiceFactory.Create();}

        public ResultadoValidacionAprobacion Validar(int solicitudId,int inspeccionId,int usuarioDcavId)
        {
            if(usuarioDcavId<=0)return Invalido(401,"Usuario no autenticado.");if(solicitudId<=0||inspeccionId<=0)return Invalido(400,"Solicitud e inspección son obligatorias.");
            try{using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction(IsolationLevel.RepeatableRead)){if(!_dao.UsuarioDcavActivo(cn,tx,usuarioDcavId))return Invalido(403,"El usuario no posee el rol activo DirectorCertificacionesDcav.");var d=_paquete.BloquearDetalle(cn,tx,solicitudId,inspeccionId);if(d==null)return Invalido(404,"El expediente no está pendiente de revisión documental DCAV.");var v=ValidarPaquete(cn,tx,d);tx.Rollback();return v;}}}catch(Exception ex){_log.LogError("[DCAV_APPROVAL][ERROR] Validar;SolicitudId="+solicitudId+";"+ex);return Invalido(500,"No fue posible validar la aprobación documental.");}
        }

        public ResultadoAprobacionDocumentosDcav Aprobar(AprobarDocumentosDcavRequest r)
        {
            Trace.TraceInformation("[DCAV_APPROVAL][IN] SolicitudId="+(r!=null?r.SolicitudId:0));ValidarRequest(r);
            var clave=CrearClave(r);if(!string.IsNullOrWhiteSpace(r.ClaveIdempotencia)&&!string.Equals(r.ClaveIdempotencia,clave,StringComparison.Ordinal))throw Error(409,"El expediente fue actualizado por otro usuario. Recargue la información antes de continuar.");
            var corr=string.IsNullOrWhiteSpace(r.CorrelationId)?Guid.NewGuid().ToString("N"):r.CorrelationId.Trim();
            try{using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction(IsolationLevel.Serializable)){try
            {
                if(!_dao.UsuarioDcavActivo(cn,tx,r.UsuarioDcavId))throw Error(403,"El usuario no posee el rol activo DirectorCertificacionesDcav.");
                var d=_paquete.BloquearDetalle(cn,tx,r.SolicitudId,r.InspeccionId);
                if(d==null){var old=_dao.ObtenerIdempotencia(cn,tx,clave);if(old!=null){tx.Rollback();Trace.TraceInformation("[IDEMPOTENCY][HIT] Clave="+clave);return Hit(old);}throw Error(409,"El expediente fue actualizado por otro usuario. Recargue la información antes de continuar.");}
                Trace.TraceInformation("[DCAV_APPROVAL][CONTEXT_OK] SolicitudId="+d.SolicitudId);Coincidir(r,d);Trace.TraceInformation("[DCAV_APPROVAL][STATE_VALIDATION_OK] Estado="+d.EstadoFuncional);
                var hit=_dao.ObtenerIdempotencia(cn,tx,clave);if(hit!=null){tx.Rollback();Trace.TraceInformation("[IDEMPOTENCY][HIT] Clave="+clave);return Hit(hit);}
                Trace.TraceInformation("[DCAV_APPROVAL][PACKAGE_LOAD_OK] AocrId="+d.AocrId+";CondicionesId="+d.CondicionesId);
                var validacion=ValidarPaquete(cn,tx,d);if(!validacion.Valido){Trace.TraceWarning("[DCAV_APPROVAL][VALIDATION_ERROR] "+validacion.Mensaje);throw Error(validacion.Codigo,validacion.Mensaje);}
                var a=_pdf.ObtenerPorId(d.AocrPdfId);var c=_pdf.ObtenerPorId(d.CondicionesPdfId);var detalle=Detalle(d,a.HashSha256,c.HashSha256,r,clave,corr);
                foreach(var e in new[]{"APROBACION_DOCUMENTOS_DCAV_INICIADA","AOCR_VALIDADO_PARA_APROBACION","CONDICIONES_VALIDADAS_PARA_APROBACION","PAQUETE_DOCUMENTAL_APROBADO_DCAV"})_dao.RegistrarAuditoria(cn,tx,d.SolicitudId,r.UsuarioDcavId,e,detalle);
                _dao.AprobarDocumento(cn,tx,d.AocrId,d.SolicitudId,d.InspeccionId,d.VersionAocrEnviada,r.UsuarioDcavId,"AOCR");Trace.TraceInformation("[DCAV_APPROVAL][AOCR_APPROVED] Id="+d.AocrId);_dao.RegistrarAuditoria(cn,tx,d.SolicitudId,r.UsuarioDcavId,"AOCR_APROBADO_DCAV",detalle);
                _dao.AprobarDocumento(cn,tx,d.CondicionesId,d.SolicitudId,d.InspeccionId,d.VersionCondicionesEnviada,r.UsuarioDcavId,"CONDICIONES");Trace.TraceInformation("[DCAV_APPROVAL][CONDICIONES_APPROVED] Id="+d.CondicionesId);_dao.RegistrarAuditoria(cn,tx,d.SolicitudId,r.UsuarioDcavId,"CONDICIONES_APROBADAS_DCAV",detalle);
                _dao.PrepararFirmaDocumento(cn,tx,d.AocrId,d.SolicitudId,d.InspeccionId,d.VersionAocrEnviada,"PENDIENTE_FIRMA_DGAC");
                _dao.PrepararFirmaDocumento(cn,tx,d.CondicionesId,d.SolicitudId,d.InspeccionId,d.VersionCondicionesEnviada,"PENDIENTE_FIRMA_DCAV");
                _dao.RegistrarAuditoria(cn,tx,d.SolicitudId,r.UsuarioDcavId,"DOCUMENTOS_BLOQUEADOS",detalle);Trace.TraceInformation("[DCAV_APPROVAL][DOCUMENTS_LOCKED] SolicitudId="+d.SolicitudId);
                _dao.CambiarEstado(cn,tx,d,r.UsuarioDcavId,detalle);Trace.TraceInformation("[DCAV_APPROVAL][STATE_UPDATED] Estado="+Destino);
                _dao.RegistrarHistorial(cn,tx,d,r.UsuarioDcavId,r.Rol,clave,r.Ip,corr,detalle);_dao.RegistrarAuditoria(cn,tx,d.SolicitudId,r.UsuarioDcavId,"EXPEDIENTE_ENVIADO_FIRMA_DIRDAC",detalle);
                var msg="DCAV aprobó el AOCR y las Condiciones y Limitaciones. Los documentos se encuentran disponibles en la bandeja de firma institucional. Solicitud "+d.NumeroSolicitud+", explotador "+d.Explotador+", trámite "+d.TipoTramite+", Inspector "+d.InspectorNombre+", fecha "+DateTime.Now.ToString("dd/MM/yyyy HH:mm")+".";
                _dao.CrearNotificacionesYOutbox(cn,tx,d,clave,corr,msg);_dao.RegistrarAuditoria(cn,tx,d.SolicitudId,r.UsuarioDcavId,"NOTIFICACION_DIRDAC_CREADA",detalle);Trace.TraceInformation("[DCAV_APPROVAL][OUTBOX_CREATED] SolicitudId="+d.SolicitudId);
                _dao.RegistrarIdempotencia(cn,tx,clave,d,corr);Trace.TraceInformation("[IDEMPOTENCY][CREATED] Clave="+clave);tx.Commit();Trace.TraceInformation("[DCAV_APPROVAL][OK] SolicitudId="+d.SolicitudId);return new ResultadoAprobacionDocumentosDcav{Exitoso=true,Codigo=200,Mensaje="AOCR y Condiciones aprobados conjuntamente y enviados a firma institucional.",EstadoAnterior=Origen,EstadoNuevo=Destino,AocrId=d.AocrId,CondicionesId=d.CondicionesId,Fecha=DateTime.Now};
            }catch{try{tx.Rollback();}catch{}Trace.TraceWarning("[DCAV_APPROVAL][ROLLBACK] SolicitudId="+r.SolicitudId);throw;}}}}
            catch(RevisionDocumentosDcavException){throw;}
            catch(Exception ex){var conflict=ex.Message.IndexOf("CONCURRENCY_CONFLICT",StringComparison.OrdinalIgnoreCase)>=0;if(conflict)Trace.TraceWarning("[CONCURRENCY][CONFLICT] SolicitudId="+r.SolicitudId);Trace.TraceError("[DCAV_APPROVAL][ERROR] SolicitudId="+r.SolicitudId+";"+ex);throw Error(conflict?409:500,conflict?"El expediente fue actualizado por otro usuario. Recargue la información antes de continuar.":"La aprobación fue revertida por un error interno.",ex);}
        }

        private ResultadoValidacionAprobacion ValidarPaquete(Npgsql.NpgsqlConnection cn,Npgsql.NpgsqlTransaction tx,RevisionDocumentosDcavDto d)
        {
            var e=new List<string>();if(d.EstadoFuncional!=Origen)e.Add("El estado central no permite aprobar.");if(d.EstadoAocr!="ENVIADO_DCAV")e.Add("El AOCR no conserva el estado enviado.");if(d.EstadoCondiciones!="ENVIADO_DCAV")e.Add("Las Condiciones no conservan el estado enviado.");if(d.AocrId<=0||d.CondicionesId<=0)e.Add("El paquete documental está incompleto.");
            var a=d.AocrPdfId>0?_pdf.ObtenerPorId(d.AocrPdfId):null;var c=d.CondicionesPdfId>0?_pdf.ObtenerPorId(d.CondicionesPdfId):null;if(a==null)e.Add("El PDF AOCR enviado no existe.");if(c==null)e.Add("El PDF de Condiciones enviado no existe.");
            if(a!=null&&(a.SolicitudId!=d.SolicitudId||a.InspeccionId!=d.InspeccionId||a.DocumentoOrigenId!=d.AocrId||a.Version!=d.VersionAocrEnviada))e.Add("El PDF AOCR no corresponde a la versión enviada.");if(c!=null&&(c.SolicitudId!=d.SolicitudId||c.InspeccionId!=d.InspeccionId||c.DocumentoOrigenId!=d.CondicionesId||c.Version!=d.VersionCondicionesEnviada))e.Add("El PDF de Condiciones no corresponde a la versión enviada.");
            if(a!=null&&!_pdf.ValidarArchivo(a.Id).Valido)e.Add("El archivo, tamaño o hash del PDF AOCR es inválido.");if(c!=null&&!_pdf.ValidarArchivo(c.Id).Valido)e.Add("El archivo, tamaño o hash del PDF de Condiciones es inválido.");
            if(a!=null&&c!=null&&(!Igual(a.CodigoCompania,c.CodigoCompania)||!Igual(a.CodigoCompania,d.CodigoCompania)))e.Add("Los documentos pertenecen a compañías diferentes.");
            if(d.InspectorAocrId!=d.InspectorId||d.InspectorCondicionesId!=d.InspectorId)e.Add("Los documentos no pertenecen al mismo Inspector asignado.");if(!Igual(d.CompaniaAocr,d.CompaniaCondiciones)||!Igual(d.CompaniaAocr,d.CodigoCompania))e.Add("Los documentos generados pertenecen a compañías diferentes.");
            if(d.InformeTecnicoId<=0||string.IsNullOrWhiteSpace(d.InformeRuta)||string.IsNullOrWhiteSpace(d.InformeHash))e.Add("El Informe Técnico aprobado no está disponible.");else ValidarSoporte(d.InformeRuta,d.InformeHash,"Informe Técnico",e);if(d.LvEaeId<=0||string.IsNullOrWhiteSpace(d.LvEaeRuta)||string.IsNullOrWhiteSpace(d.LvEaeHash))e.Add("La LV/EAE firmada no está disponible.");else ValidarSoporte(d.LvEaeRuta,d.LvEaeHash,"LV/EAE",e);
            if(_dao.ContarObservacionesNoCerradas(cn,tx,d.SolicitudId)>0)e.Add("Existen observaciones abiertas, correcciones pendientes o atenciones aún no verificadas por DCAV.");else Trace.TraceInformation("[DCAV_APPROVAL][OPEN_OBSERVATIONS_CHECK_OK] SolicitudId="+d.SolicitudId);
            if(e.Count==0){Trace.TraceInformation("[DCAV_APPROVAL][AOCR_VALIDATION_OK] Id="+d.AocrId);Trace.TraceInformation("[DCAV_APPROVAL][CONDICIONES_VALIDATION_OK] Id="+d.CondicionesId);Trace.TraceInformation("[DCAV_APPROVAL][SUPPORT_DOCUMENTS_OK] SolicitudId="+d.SolicitudId);return new ResultadoValidacionAprobacion{Valido=true,Codigo=200,Mensaje="Paquete válido para aprobación conjunta."};}return new ResultadoValidacionAprobacion{Valido=false,Codigo=e.Exists(x=>x.IndexOf("no existe",StringComparison.OrdinalIgnoreCase)>=0||x.IndexOf("no está disponible",StringComparison.OrdinalIgnoreCase)>=0)?404:422,Mensaje=string.Join(" ",e),Errores=e};
        }

        public static string CrearClave(AprobarDocumentosDcavRequest r){return r.SolicitudId+":"+r.InspeccionId+":"+r.AocrId+":"+r.VersionAocr+":"+r.CondicionesId+":"+r.VersionCondiciones+":APROBAR_DOCUMENTOS_DCAV";}
        private static void ValidarRequest(AprobarDocumentosDcavRequest r){if(r==null)throw Error(400,"Solicitud de aprobación inválida.");if(r.UsuarioDcavId<=0)throw Error(401,"Usuario no autenticado.");if(r.SolicitudId<=0||r.InspeccionId<=0)throw Error(400,"Solicitud e inspección son obligatorias.");if(!string.Equals(r.EstadoEsperado,Origen,StringComparison.OrdinalIgnoreCase))throw Error(409,"El expediente fue actualizado por otro usuario. Recargue la información antes de continuar.");}
        private static void Coincidir(AprobarDocumentosDcavRequest r,RevisionDocumentosDcavDto d){if(r.VersionExpediente!=d.VersionExpediente||r.AocrId!=d.AocrId||r.AocrPdfId!=d.AocrPdfId||r.VersionAocr!=d.VersionAocrEnviada||r.CondicionesId!=d.CondicionesId||r.CondicionesPdfId!=d.CondicionesPdfId||r.VersionCondiciones!=d.VersionCondicionesEnviada)throw Error(409,"El expediente fue actualizado por otro usuario. Recargue la información antes de continuar.");}
        private static void ValidarSoporte(string ruta,string hash,string nombre,IList<string> e){try{var rel=(ruta??"").Trim().Replace('\\','/');var path=System.Web.Hosting.HostingEnvironment.MapPath(rel.StartsWith("~")?rel:"~"+(rel.StartsWith("/")?rel:"/"+rel));var root=System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Uploads");if(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(root)||!Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||new FileInfo(path).Length<=0){e.Add(nombre+" no está disponible.");return;}using(var sha=SHA256.Create())using(var fs=File.OpenRead(path)){var h=BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","").ToLowerInvariant();if(!string.Equals(h,(hash??"").Replace("-","").Trim().ToLowerInvariant(),StringComparison.Ordinal))e.Add("El hash de "+nombre+" es inválido.");}}catch{e.Add(nombre+" no está disponible.");}}
        private static string Detalle(RevisionDocumentosDcavDto d,string ha,string hc,AprobarDocumentosDcavRequest r,string k,string corr){return "SolicitudId="+d.SolicitudId+";InspeccionId="+d.InspeccionId+";UsuarioDcavId="+r.UsuarioDcavId+";Rol="+r.Rol+";AocrId="+d.AocrId+";VersionAocr="+d.VersionAocrEnviada+";AocrPdfId="+d.AocrPdfId+";HashAocr="+ha+";CondicionesId="+d.CondicionesId+";VersionCondiciones="+d.VersionCondicionesEnviada+";CondicionesPdfId="+d.CondicionesPdfId+";HashCondiciones="+hc+";InformeTecnicoId="+d.InformeTecnicoId+";LvEaeId="+d.LvEaeId+";EstadoAnterior="+Origen+";EstadoNuevo="+Destino+";IP="+r.Ip+";CorrelationId="+corr+";Clave="+k+";Resultado=OK";}
        private static bool Igual(string a,string b){return string.Equals((a??"").Trim(),(b??"").Trim(),StringComparison.OrdinalIgnoreCase);}
        private static ResultadoValidacionAprobacion Invalido(int c,string m){return new ResultadoValidacionAprobacion{Valido=false,Codigo=c,Mensaje=m,Errores=new List<string>{m}};}
        private static ResultadoAprobacionDocumentosDcav Hit(ResultadoIdempotenciaAprobacion x){return new ResultadoAprobacionDocumentosDcav{Exitoso=true,YaProcesado=true,Codigo=200,Mensaje="La aprobación conjunta ya fue procesada.",EstadoAnterior=x.EstadoAnterior,EstadoNuevo=x.EstadoNuevo,AocrId=x.AocrId,CondicionesId=x.CondicionesId,Fecha=x.Fecha};}
        private static RevisionDocumentosDcavException Error(int c,string m,Exception x=null){return new RevisionDocumentosDcavException(c,m,x);}
    }
}
