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
    public interface IRevisionDocumentosDcavService
    {
        IList<DocumentosPendientesDcavDto> ObtenerPendientes(int usuarioId,string rol);
        RevisionDocumentosDcavDto ObtenerDetalle(int solicitudId,int inspeccionId,int usuarioId,string rol);
        IList<HistorialDocumentoDcavDto> ObtenerHistorial(int solicitudId,int inspeccionId,int usuarioId,string rol);
        RevisionDocumentosDcavResultado AprobarDocumentos(DecisionDocumentosDcavRequest request);
        RevisionDocumentosDcavResultado DevolverDocumentos(DecisionDocumentosDcavRequest request);
    }

    public sealed class RevisionDocumentosDcavService:IRevisionDocumentosDcavService
    {
        private readonly AocrDcavDocumentosDAO _dao;
        private readonly IDocumentoPdfService _pdf;
        private readonly ILoggingService _logger;
        public RevisionDocumentosDcavService():this(new AocrDcavDocumentosDAO(),new DocumentoPdfService(System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/AOCR"))){ }
        public RevisionDocumentosDcavService(AocrDcavDocumentosDAO dao,IDocumentoPdfService pdf){_dao=dao??new AocrDcavDocumentosDAO();_pdf=pdf;_logger=LoggingServiceFactory.Create();}

        public IList<DocumentosPendientesDcavDto> ObtenerPendientes(int usuarioId,string rol)
        {
            Autorizar(usuarioId,rol);var sw=Stopwatch.StartNew();_logger.LogInfo("[DCAV_DOCS][BANDEJA_IN] UsuarioId="+usuarioId+";");
            try{_logger.LogInfo("[DCAV_DOCS][BANDEJA_QUERY] Estado=PENDIENTE_REVISION_DOCUMENTOS_DCAV;");var r=_dao.ObtenerPendientesRevisionDocumentos();_logger.LogInfo("[DCAV_DOCS][COUNT] Cantidad="+r.Count+";");_logger.LogInfo("[DCAV_DOCS][BANDEJA_OUT] Cantidad="+r.Count+";DuracionMs="+sw.ElapsedMilliseconds+";");return r;}
            catch(Exception ex){_logger.LogError("[DCAV_DOCS][BANDEJA_ERROR] "+ex);throw new RevisionDocumentosDcavException(500,"No fue posible consultar la bandeja documental DCAV.",ex);}
        }
        public RevisionDocumentosDcavDto ObtenerDetalle(int solicitudId,int inspeccionId,int usuarioId,string rol)
        {Autorizar(usuarioId,rol);_logger.LogInfo("[DCAV_DOCS][DETALLE_IN] SolicitudId="+solicitudId+";InspeccionId="+inspeccionId+";");if(solicitudId<=0||inspeccionId<=0)throw Error(400,"Solicitud e inspeccion son obligatorias.");var d=_dao.ObtenerDetalleRevision(solicitudId,inspeccionId);if(d==null)throw Error(404,"El expediente no esta pendiente de revision documental DCAV.");ValidarIntegridad(d);_logger.LogInfo("[DCAV_DOCS][AOCR_LOAD_OK] PdfId="+d.AocrPdfId+";Version="+d.VersionAocrEnviada+";");_logger.LogInfo("[DCAV_DOCS][CONDICIONES_LOAD_OK] PdfId="+d.CondicionesPdfId+";Version="+d.VersionCondicionesEnviada+";");_logger.LogInfo("[DCAV_DOCS][INFORME_LOAD_OK] InformeId="+d.InformeTecnicoId+";LV="+d.LvEaeId+";");_logger.LogInfo("[DCAV_DOCS][HISTORIAL_LOAD_OK] Cantidad="+d.Historial.Count+";");return d;}
        public IList<HistorialDocumentoDcavDto> ObtenerHistorial(int solicitudId,int inspeccionId,int usuarioId,string rol){Autorizar(usuarioId,rol);if(_dao.ObtenerDetalleRevision(solicitudId,inspeccionId)==null)throw Error(404,"Expediente inexistente.");return _dao.ObtenerHistorial(solicitudId,inspeccionId);}
        public RevisionDocumentosDcavResultado AprobarDocumentos(DecisionDocumentosDcavRequest r)
        {
            if(r==null)throw Error(400,"Solicitud invalida.");var req=new AprobarDocumentosDcavRequest{SolicitudId=r.SolicitudId,InspeccionId=r.InspeccionId,UsuarioDcavId=r.UsuarioId,Rol=r.Rol,EstadoEsperado="PENDIENTE_REVISION_DOCUMENTOS_DCAV",AocrId=r.AocrId,AocrPdfId=r.AocrPdfId,VersionAocr=r.VersionAocr,CondicionesId=r.CondicionesId,CondicionesPdfId=r.CondicionesPdfId,VersionCondiciones=r.VersionCondiciones,VersionExpediente=r.VersionExpediente,Ip=r.Ip,CorrelationId=r.CorrelationId};req.ClaveIdempotencia=AprobacionDocumentosDcavService.CrearClave(req);var x=new AprobacionDocumentosDcavService().Aprobar(req);return new RevisionDocumentosDcavResultado{Exitoso=x.Exitoso,YaProcesado=x.YaProcesado,Codigo=x.Codigo,Mensaje=x.Mensaje,EstadoNuevo=x.EstadoNuevo};
        }
        public RevisionDocumentosDcavResultado DevolverDocumentos(DecisionDocumentosDcavRequest r)
        {
            if(r==null)throw Error(400,"Solicitud invalida.");
            var central=new DevolucionDocumentosDcavService();var req=new DevolverDocumentosDcavRequest{SolicitudId=r.SolicitudId,InspeccionId=r.InspeccionId,VersionExpediente=r.VersionExpediente,AocrId=r.AocrId,VersionAocr=r.VersionAocr,AocrPdfId=r.AocrPdfId,CondicionesId=r.CondicionesId,VersionCondiciones=r.VersionCondiciones,CondicionesPdfId=r.CondicionesPdfId,UsuarioId=r.UsuarioId,Rol=r.Rol,Ip=r.Ip,CorrelationId=r.CorrelationId};
            if(r.ObservarAocr)req.Observaciones.Add(new ObservacionDevolucionDcavRequest{TipoDocumento="RECONOCIMIENTO",Seccion=r.SeccionCampo,Campo=r.SeccionCampo,Texto=r.Observacion});
            if(r.ObservarCondiciones)req.Observaciones.Add(new ObservacionDevolucionDcavRequest{TipoDocumento="CONDICIONES_LIMITACIONES",Seccion=r.SeccionCampo,Campo=r.SeccionCampo,Texto=r.Observacion});
            var x=central.Devolver(req);return new RevisionDocumentosDcavResultado{Exitoso=x.Exitoso,YaProcesado=x.YaProcesado,Codigo=x.Codigo,Mensaje=x.Mensaje,EstadoNuevo=x.EstadoNuevo};
        }

        private RevisionDocumentosDcavResultado Decidir(DecisionDocumentosDcavRequest r,bool aprobar)
        {
            var tag=aprobar?"APPROVE":"RETURN";if(aprobar)_logger.LogInfo("[DCAV_DOCS][APPROVE_IN] SolicitudId="+(r!=null?r.SolicitudId:0)+";");else _logger.LogInfo("[DCAV_DOCS][RETURN_IN] SolicitudId="+(r!=null?r.SolicitudId:0)+";");
            if(r==null)throw Error(400,"Solicitud invalida.");Autorizar(r.UsuarioId,r.Rol);
            if(!aprobar&&(!r.ObservarAocr&&!r.ObservarCondiciones))throw Error(400,"Seleccione AOCR, Condiciones o ambos.");
            if(!aprobar&&string.IsNullOrWhiteSpace(r.Observacion))throw Error(422,"La observacion es obligatoria.");
            if(!aprobar&&string.IsNullOrWhiteSpace(r.SeccionCampo))throw Error(422,"La seccion o campo observado es obligatorio.");
            var correlation=string.IsNullOrWhiteSpace(r.CorrelationId)?Guid.NewGuid().ToString("N"):r.CorrelationId.Trim();
            try
            {using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction(IsolationLevel.Serializable))
            {var d=_dao.BloquearDetalle(cn,tx,r.SolicitudId,r.InspeccionId);var claveRequest=ClaveDesdeRequest(r,aprobar);if(d==null){if(_dao.ExisteIdempotencia(cn,tx,claveRequest)){tx.Rollback();return new RevisionDocumentosDcavResultado{Exitoso=true,YaProcesado=true,Codigo=200,Mensaje="La decision ya fue procesada.",EstadoNuevo=aprobar?"PENDIENTE_FIRMA_DIRDAC":"DOCUMENTOS_OBSERVADOS_DCAV"};}throw Error(409,"El expediente ya no esta pendiente de revision documental DCAV.");}
             ValidarCoincidencia(r,d);ValidarIntegridad(d);if(d.ObservacionesAbiertas>0)throw Error(422,"Existen observaciones abiertas.");
             var clave=Clave(r,d,aprobar);if(_dao.ExisteIdempotencia(cn,tx,clave)){tx.Rollback();return new RevisionDocumentosDcavResultado{Exitoso=true,YaProcesado=true,Codigo=200,Mensaje="La decision ya fue procesada.",EstadoNuevo=aprobar?"PENDIENTE_FIRMA_DIRDAC":"DOCUMENTOS_OBSERVADOS_DCAV"};}
             _dao.AplicarDecision(cn,tx,d,aprobar,r.ObservarAocr,r.ObservarCondiciones,r.UsuarioId,r.Rol,r.SeccionCampo,r.Observacion,clave,r.Ip,correlation);tx.Commit();
             var estado=aprobar?"PENDIENTE_FIRMA_DIRDAC":"DOCUMENTOS_OBSERVADOS_DCAV";if(aprobar)_logger.LogInfo("[DCAV_DOCS][APPROVE_OK] SolicitudId="+d.SolicitudId+";Estado="+estado+";");else _logger.LogInfo("[DCAV_DOCS][RETURN_OK] SolicitudId="+d.SolicitudId+";Estado="+estado+";");return new RevisionDocumentosDcavResultado{Exitoso=true,Codigo=200,Mensaje=aprobar?"AOCR y Condiciones aprobados y enviados a DIRDAC.":"Documentos devueltos al Inspector con observaciones.",EstadoNuevo=estado};}}
            }
            catch(RevisionDocumentosDcavException){if(aprobar)_logger.LogWarning("[DCAV_DOCS][APPROVE_ERROR] SolicitudId="+r.SolicitudId+";ErrorControlado=TRUE;");else _logger.LogWarning("[DCAV_DOCS][RETURN_ERROR] SolicitudId="+r.SolicitudId+";ErrorControlado=TRUE;");throw;}
            catch(Exception ex){var conflicto=ex.Message.IndexOf("CONCURRENCY_CONFLICT",StringComparison.OrdinalIgnoreCase)>=0;if(conflicto)_logger.LogWarning("[DCAV_DOCS][CONCURRENCY_CONFLICT] SolicitudId="+r.SolicitudId+";");_logger.LogError("[DCAV_DOCS][ROLLBACK] SolicitudId="+r.SolicitudId+";Error="+ex);_logger.LogError("[DCAV_DOCS]["+tag+"_ERROR] "+ex);throw Error(conflicto?409:500,conflicto?"El expediente fue modificado por otro usuario.":"La operacion fue revertida por un error interno.",ex);}
        }
        private void ValidarIntegridad(RevisionDocumentosDcavDto d)
        {if(d.EstadoFuncional!="PENDIENTE_REVISION_DOCUMENTOS_DCAV")throw Error(409,"Estado funcional incorrecto.");if(d.EstadoAocr!="ENVIADO_DCAV"||d.EstadoCondiciones!="ENVIADO_DCAV")throw Error(409,"El par documental no conserva el estado enviado.");if(d.AocrPdfId<=0||d.CondicionesPdfId<=0||d.InformeTecnicoId<=0||d.LvEaeId<=0)throw Error(404,"Falta un documento obligatorio.");if(_pdf==null)throw Error(500,"Servicio PDF no disponible.");var a=_pdf.ObtenerPorId(d.AocrPdfId);var c=_pdf.ObtenerPorId(d.CondicionesPdfId);if(a==null||c==null)throw Error(404,"No existen los PDF exactos enviados.");if(a.SolicitudId!=d.SolicitudId||c.SolicitudId!=d.SolicitudId||a.InspeccionId!=d.InspeccionId||c.InspeccionId!=d.InspeccionId||a.DocumentoOrigenId!=d.AocrId||c.DocumentoOrigenId!=d.CondicionesId)throw Error(409,"Los PDF no pertenecen al expediente enviado.");if(!string.IsNullOrWhiteSpace(d.CodigoCompania)&&(!string.Equals(a.CodigoCompania,d.CodigoCompania,StringComparison.OrdinalIgnoreCase)||!string.Equals(c.CodigoCompania,d.CodigoCompania,StringComparison.OrdinalIgnoreCase)))throw Error(422,"Los documentos no pertenecen a la misma compania.");var va=_pdf.ValidarArchivo(d.AocrPdfId);var vc=_pdf.ValidarArchivo(d.CondicionesPdfId);if(!va.Valido||!vc.Valido)throw Error(422,"La integridad de los PDF enviados no es valida.");if(string.IsNullOrWhiteSpace(d.InformeRuta)||string.IsNullOrWhiteSpace(d.InformeHash)||string.IsNullOrWhiteSpace(d.LvEaeRuta)||string.IsNullOrWhiteSpace(d.LvEaeHash))throw Error(422,"Informe tecnico o LV/EAE incompletos.");ValidarSoporte(d.InformeRuta,d.InformeHash,"Informe Tecnico");ValidarSoporte(d.LvEaeRuta,d.LvEaeHash,"LV/EAE");}
        private static void ValidarSoporte(string ruta,string hash,string nombre){var rel=(ruta??"").Trim().Replace('\\','/');var path=System.Web.Hosting.HostingEnvironment.MapPath(rel.StartsWith("~")?rel:"~"+(rel.StartsWith("/")?rel:"/"+rel));var raiz=System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/Uploads");if(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(raiz)||!Path.GetFullPath(path).StartsWith(Path.GetFullPath(raiz).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw Error(404,nombre+" no se encuentra disponible.");using(var sha=SHA256.Create())using(var fs=File.OpenRead(path)){var actual=BitConverter.ToString(sha.ComputeHash(fs)).Replace("-","").ToLowerInvariant();if(!string.Equals(actual,(hash??"").Replace("-","").Trim().ToLowerInvariant(),StringComparison.Ordinal))throw Error(422,"Hash invalido en "+nombre+".");}}
        private static void ValidarCoincidencia(DecisionDocumentosDcavRequest r,RevisionDocumentosDcavDto d){if(r.VersionExpediente!=d.VersionExpediente||r.AocrId!=d.AocrId||r.VersionAocr!=d.VersionAocrEnviada||r.AocrPdfId!=d.AocrPdfId||r.CondicionesId!=d.CondicionesId||r.VersionCondiciones!=d.VersionCondicionesEnviada||r.CondicionesPdfId!=d.CondicionesPdfId)throw Error(409,"Las versiones enviadas cambiaron. Recargue el expediente.");}
        public static string Clave(DecisionDocumentosDcavRequest r,RevisionDocumentosDcavDto d,bool aprobar){return d.SolicitudId+":"+d.InspeccionId+":"+d.AocrId+":"+d.VersionAocrEnviada+":"+d.CondicionesId+":"+d.VersionCondicionesEnviada+":"+(aprobar?"APROBAR_DOCUMENTOS_DCAV":"DEVOLVER_DOCUMENTOS_DCAV")+":"+r.UsuarioId+":"+(aprobar?"AMBOS":r.ObservarAocr&&r.ObservarCondiciones?"AMBOS":r.ObservarAocr?"AOCR":"CONDICIONES");}
        private static string ClaveDesdeRequest(DecisionDocumentosDcavRequest r,bool aprobar){return r.SolicitudId+":"+r.InspeccionId+":"+r.AocrId+":"+r.VersionAocr+":"+r.CondicionesId+":"+r.VersionCondiciones+":"+(aprobar?"APROBAR_DOCUMENTOS_DCAV":"DEVOLVER_DOCUMENTOS_DCAV")+":"+r.UsuarioId+":"+(aprobar?"AMBOS":r.ObservarAocr&&r.ObservarCondiciones?"AMBOS":r.ObservarAocr?"AOCR":"CONDICIONES");}
        private static void Autorizar(int usuario,string rol){if(usuario<=0)throw Error(401,"Usuario no autenticado.");var x=(rol??"").Replace("_","").Replace(" ","").ToUpperInvariant();if(x!="DIRECTORCERTIFICACIONESDCAV"&&x!="DCAV"&&!x.Contains("ADMINISTRADOR"))throw Error(403,"Solo el Director de Certificaciones DCAV puede realizar esta operacion.");}
        private static RevisionDocumentosDcavException Error(int c,string m,Exception e=null){return new RevisionDocumentosDcavException(c,m,e);}
    }
    public sealed class RevisionDocumentosDcavException:Exception{public int Codigo{get;private set;}public RevisionDocumentosDcavException(int codigo,string mensaje,Exception inner=null):base(mensaje,inner){Codigo=codigo;}}
}
