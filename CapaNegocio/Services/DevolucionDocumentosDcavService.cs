using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using CapaDatos.DAOs;
using CapaDatos.Models;
using CapaNegocio.DTOs;

namespace CapaNegocio.Services
{
    public interface IDevolucionDocumentosDcavService
    {
        DevolucionDocumentosDcavResultado Devolver(DevolverDocumentosDcavRequest request);
        ValidacionDevolucionDocumentosDcav Validar(DevolverDocumentosDcavRequest request);
        IList<ObservacionDocumentoDcavRegistro> ObtenerObservaciones(int solicitudId);
        ObservacionDocumentoDcavRegistro Atender(CambiarEstadoObservacionDcavRequest request);
        ObservacionDocumentoDcavRegistro Cerrar(CambiarEstadoObservacionDcavRequest request);
    }

    public sealed class DevolucionDocumentosDcavService : IDevolucionDocumentosDcavService
    {
        private readonly AocrDcavDocumentosDAO _revision;
        private readonly DevolucionDocumentosDcavDAO _dao;
        public DevolucionDocumentosDcavService():this(new AocrDcavDocumentosDAO(),new DevolucionDocumentosDcavDAO()){}
        public DevolucionDocumentosDcavService(AocrDcavDocumentosDAO revision,DevolucionDocumentosDcavDAO dao){_revision=revision;_dao=dao;}

        public ValidacionDevolucionDocumentosDcav Validar(DevolverDocumentosDcavRequest r)
        {
            var errores=new List<string>();
            if(r==null)errores.Add("Solicitud de devolución inválida.");
            else
            {
                if(r.SolicitudId<=0||r.InspeccionId<=0||r.UsuarioId<=0)errores.Add("Solicitud, inspección y usuario son obligatorios.");
                if(!EsDcav(r.Rol))errores.Add("Sólo DCAV puede devolver documentos.");
                var obs=(r.Observaciones??new List<ObservacionDevolucionDcavRequest>()).Where(EsConContenido).ToList();
                if(obs.Count==0)errores.Add("Registre al menos una observación.");
                foreach(var x in obs)
                {
                    var tipo=NormalizarTipo(x.TipoDocumento);
                    if(tipo==null)errores.Add("Cada observación debe corresponder a AOCR o Condiciones y Limitaciones.");
                    if(string.IsNullOrWhiteSpace(x.Seccion))errores.Add("La sección observada es obligatoria.");
                    if(string.IsNullOrWhiteSpace(x.Campo))errores.Add("El campo observado es obligatorio.");
                    if(string.IsNullOrWhiteSpace(x.Texto))errores.Add("El texto de la observación es obligatorio.");
                    if((x.Seccion??"").Length>200||(x.Campo??"").Length>200||(x.Texto??"").Length>2000)errores.Add("Una observación excede la longitud permitida.");
                }
            }
            return errores.Count==0?new ValidacionDevolucionDocumentosDcav{Valido=true,Codigo=200,Mensaje="Devolución válida."}:new ValidacionDevolucionDocumentosDcav{Valido=false,Codigo=422,Mensaje=string.Join(" ",errores.Distinct()),Errores=errores.Distinct().ToList()};
        }

        public DevolucionDocumentosDcavResultado Devolver(DevolverDocumentosDcavRequest r)
        {
            Trace.TraceInformation("[DCAV_RETURN][IN] SolicitudId="+(r!=null?r.SolicitudId:0));
            var validacion=Validar(r);if(!validacion.Valido)throw new RevisionDocumentosDcavException(validacion.Codigo,validacion.Mensaje);
            r.Observaciones=r.Observaciones.Where(EsConContenido).ToList();
            var observaAocr=r.Observaciones.Any(x=>NormalizarTipo(x.TipoDocumento)=="RECONOCIMIENTO");
            var observaCond=r.Observaciones.Any(x=>NormalizarTipo(x.TipoDocumento)=="CONDICIONES_LIMITACIONES");
            var clave=CrearClave(r,observaAocr,observaCond);
            var corr=string.IsNullOrWhiteSpace(r.CorrelationId)?Guid.NewGuid().ToString("N"):r.CorrelationId.Trim();
            try
            {
                using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction(IsolationLevel.Serializable))
                {
                    var d=_revision.BloquearDetalle(cn,tx,r.SolicitudId,r.InspeccionId);
                    if(d==null){if(_revision.ExisteIdempotencia(cn,tx,clave)){tx.Rollback();return Hit();}throw new RevisionDocumentosDcavException(409,"El expediente ya no está pendiente de revisión documental DCAV.");}
                    Coincidir(r,d);
                    VersionCorreccionDcavRegistro a=observaAocr?_dao.CrearCorreccion(cn,tx,d.AocrId,d.VersionAocrEnviada,r.UsuarioId):null;
                    VersionCorreccionDcavRegistro c=observaCond?_dao.CrearCorreccion(cn,tx,d.CondicionesId,d.VersionCondicionesEnviada,r.UsuarioId):null;
                    if(!observaAocr)_dao.AprobarSinCambios(cn,tx,d.AocrId,d.VersionAocrEnviada,r.UsuarioId);
                    if(!observaCond)_dao.AprobarSinCambios(cn,tx,d.CondicionesId,d.VersionCondicionesEnviada,r.UsuarioId);
                    foreach(var o in r.Observaciones)
                    {
                        var tipo=NormalizarTipo(o.TipoDocumento);var correction=tipo=="RECONOCIMIENTO"?a:c;
                        _dao.InsertarObservacion(cn,tx,new ObservacionDocumentoDcavRegistro{SolicitudId=d.SolicitudId,InspeccionId=d.InspeccionId,TipoDocumento=tipo,DocumentoOrigenId=tipo=="RECONOCIMIENTO"?d.AocrId:d.CondicionesId,VersionOrigen=tipo=="RECONOCIMIENTO"?d.VersionAocrEnviada:d.VersionCondicionesEnviada,PdfOrigenId=tipo=="RECONOCIMIENTO"?d.AocrPdfId:d.CondicionesPdfId,Seccion=o.Seccion.Trim(),Campo=o.Campo.Trim(),Texto=o.Texto.Trim(),UsuarioDcavId=r.UsuarioId,UsuarioDcav=r.UsuarioNombre,RolDcav=r.Rol,Fecha=DateTime.Now,Estado="ABIERTA",DocumentoCorreccionId=correction.DocumentoId,VersionCorreccion=correction.Version,CodigoCompania=d.CodigoCompania});
                    }
                    var resumen="Observados="+(observaAocr&&observaCond?"AMBOS":observaAocr?"AOCR":"CONDICIONES")+";Cantidad="+r.Observaciones.Count;
                    _dao.CambiarEstadoCentral(cn,tx,d,r.UsuarioId,resumen);
                    _dao.RegistrarTrazabilidad(cn,tx,d,r.UsuarioId,r.Rol,clave,r.Ip,corr,resumen,a,c);
                    tx.Commit();Trace.TraceInformation("[DCAV_RETURN][OK] SolicitudId="+d.SolicitudId+";Observaciones="+r.Observaciones.Count+";AocrCorreccion="+(a!=null?a.DocumentoId:0)+";CondicionesCorreccion="+(c!=null?c.DocumentoId:0));return new DevolucionDocumentosDcavResultado{Exitoso=true,Codigo=200,Mensaje="Documentos devueltos al Inspector con observaciones estructuradas.",EstadoNuevo="DOCUMENTOS_OBSERVADOS_DCAV",Aocr=a??new VersionCorreccionDcavRegistro{DocumentoId=d.AocrId,Version=d.VersionAocrEnviada},Condiciones=c??new VersionCorreccionDcavRegistro{DocumentoId=d.CondicionesId,Version=d.VersionCondicionesEnviada}};
                }}
            }
            catch(RevisionDocumentosDcavException){throw;}
            catch(Exception ex){var conflict=ex.Message.IndexOf("CONCURRENCY_CONFLICT",StringComparison.OrdinalIgnoreCase)>=0;Trace.TraceError("[DCAV_RETURN][ROLLBACK] SolicitudId="+r.SolicitudId+";Conflict="+conflict+";Error="+ex);throw new RevisionDocumentosDcavException(conflict?409:500,conflict?"El expediente fue modificado por otro usuario. Recargue la información.":"La devolución fue revertida por un error interno.",ex);}
        }

        public IList<ObservacionDocumentoDcavRegistro> ObtenerObservaciones(int solicitudId){return solicitudId>0?_dao.ObtenerObservaciones(solicitudId):new List<ObservacionDocumentoDcavRegistro>();}
        public ObservacionDocumentoDcavRegistro Atender(CambiarEstadoObservacionDcavRequest r){return CambiarEstado(r,"ABIERTA","ATENDIDA_INSPECTOR",true);}
        public ObservacionDocumentoDcavRegistro Cerrar(CambiarEstadoObservacionDcavRequest r){if(r==null||!EsDcav(r.Rol))throw new RevisionDocumentosDcavException(403,"Sólo DCAV puede cerrar observaciones.");return CambiarEstado(r,"ATENDIDA_INSPECTOR","CERRADA_DCAV",false);}
        private ObservacionDocumentoDcavRegistro CambiarEstado(CambiarEstadoObservacionDcavRequest r,string esperado,string nuevo,bool inspector){if(r==null||r.ObservacionId<=0||r.SolicitudId<=0||r.DocumentoCorreccionId<=0||r.UsuarioId<=0)throw new RevisionDocumentosDcavException(400,"Datos de observación inválidos.");try{using(var cn=_dao.CrearConexion()){cn.Open();using(var tx=cn.BeginTransaction(IsolationLevel.Serializable)){var x=_dao.CambiarEstadoObservacion(cn,tx,r.ObservacionId,r.SolicitudId,r.DocumentoCorreccionId,r.UsuarioId,esperado,nuevo,inspector);if(x==null)throw new RevisionDocumentosDcavException(404,"La observación no corresponde al documento.");tx.Commit();return x;}}}catch(RevisionDocumentosDcavException){throw;}catch(UnauthorizedAccessException ex){throw new RevisionDocumentosDcavException(403,"Sólo el Inspector asignado puede atender la observación.",ex);}catch(Exception ex){throw new RevisionDocumentosDcavException(409,"La observación cambió de estado. Recargue la información.",ex);}}
        public static string CrearClave(DevolverDocumentosDcavRequest r,bool a,bool c){return r.SolicitudId+":"+r.InspeccionId+":"+r.AocrId+":"+r.VersionAocr+":"+r.CondicionesId+":"+r.VersionCondiciones+":DEVOLVER_DOCUMENTOS_DCAV:"+(a&&c?"AMBOS":a?"AOCR":"CONDICIONES");}
        private static void Coincidir(DevolverDocumentosDcavRequest r,RevisionDocumentosDcavDto d){if(r.VersionExpediente!=d.VersionExpediente||r.AocrId!=d.AocrId||r.VersionAocr!=d.VersionAocrEnviada||r.AocrPdfId!=d.AocrPdfId||r.CondicionesId!=d.CondicionesId||r.VersionCondiciones!=d.VersionCondicionesEnviada||r.CondicionesPdfId!=d.CondicionesPdfId)throw new RevisionDocumentosDcavException(409,"Las versiones enviadas cambiaron. Recargue el expediente.");}
        private static bool EsConContenido(ObservacionDevolucionDcavRequest x){return x!=null&&(!string.IsNullOrWhiteSpace(x.TipoDocumento)||!string.IsNullOrWhiteSpace(x.Seccion)||!string.IsNullOrWhiteSpace(x.Campo)||!string.IsNullOrWhiteSpace(x.Texto));}
        private static string NormalizarTipo(string x){x=(x??"").Trim().ToUpperInvariant();if(x=="AOCR"||x=="RECONOCIMIENTO")return "RECONOCIMIENTO";if(x=="CONDICIONES"||x=="CONDICIONES_LIMITACIONES")return "CONDICIONES_LIMITACIONES";return null;}
        private static bool EsDcav(string r){var x=(r??"").Replace("_","").Replace(" ","").ToUpperInvariant();return x=="DCAV"||x=="DIRECTORCERTIFICACIONESDCAV"||x.Contains("ADMINISTRADOR");}
        private static DevolucionDocumentosDcavResultado Hit(){return new DevolucionDocumentosDcavResultado{Exitoso=true,YaProcesado=true,Codigo=200,Mensaje="La devolución ya fue procesada.",EstadoNuevo="DOCUMENTOS_OBSERVADOS_DCAV"};}
    }
}
