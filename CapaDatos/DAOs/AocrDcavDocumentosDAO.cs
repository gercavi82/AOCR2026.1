using System;
using System.Collections.Generic;
using System.Configuration;
using CapaDatos.Models;
using Npgsql;

namespace CapaDatos.DAOs
{
    public interface IAocrDcavDocumentosDAO
    {
        IList<DocumentosPendientesDcavDto> ObtenerPendientesRevisionDocumentos();
        int ContarPendientesRevisionDocumentos();
        RevisionDocumentosDcavDto ObtenerDetalleRevision(int solicitudId, int inspeccionId);
        IList<HistorialDocumentoDcavDto> ObtenerHistorial(int solicitudId, int inspeccionId);
    }

    public sealed class AocrDcavDocumentosDAO : IAocrDcavDocumentosDAO
    {
        private readonly string _connectionString;
        public AocrDcavDocumentosDAO()
        {
            var cs=ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString=cs!=null?cs.ConnectionString:ConexionDAO.CadenaConexion;
        }
        public NpgsqlConnection CrearConexion(){return new NpgsqlConnection(_connectionString);}

        public IList<DocumentosPendientesDcavDto> ObtenerPendientesRevisionDocumentos()
        {
            var result=new List<DocumentosPendientesDcavDto>();
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(BaseQuery+" ORDER BY pe.fecha_estado ASC,s.codigo_solicitud ASC;",cn))
            {cn.Open();using(var rd=cmd.ExecuteReader())while(rd.Read())result.Add(Map(rd));}
            return result;
        }

        public int ContarPendientesRevisionDocumentos()
        {
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand("SELECT COUNT(*) FROM ("+BaseQuery+") q;",cn))
            {cn.Open();return Convert.ToInt32(cmd.ExecuteScalar());}
        }

        public RevisionDocumentosDcavDto ObtenerDetalleRevision(int solicitudId,int inspeccionId)
        {
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(BaseQuery+" AND s.codigo_solicitud=@solicitud AND i.codigo_inspeccion=@inspeccion;",cn))
            {cn.Open();cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);using(var rd=cmd.ExecuteReader())
            {if(!rd.Read())return null;var x=Map(rd);var d=Copiar(x);d.Historial=ObtenerHistorial(solicitudId,inspeccionId);return d;}}
        }

        public IList<HistorialDocumentoDcavDto> ObtenerHistorial(int solicitudId,int inspeccionId)
        {
            const string sql=@"SELECT h.id AS historial_id,h.accion AS accion,h.usuario_id AS usuario_id,
                TRIM(COALESCE(u.nombreusuario,'')||' '||COALESCE(u.apellidousuario,'')) AS usuario_nombre,
                h.rol_usuario AS rol,h.estado_anterior AS estado_anterior,h.estado_nuevo AS estado_nuevo,
                h.observacion AS observacion,h.fecha_creacion AS fecha,h.correlation_id AS correlation_id
              FROM public.aocr_proceso_estado_historial h
              LEFT JOIN public.usuario u ON u.idusuario=h.usuario_id
              WHERE h.solicitud_id=@solicitud AND (h.inspeccion_id=@inspeccion OR h.inspeccion_id IS NULL)
              ORDER BY h.fecha_creacion DESC,h.id DESC;";
            var result=new List<HistorialDocumentoDcavDto>();
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(sql,cn)){cn.Open();cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);using(var rd=cmd.ExecuteReader())while(rd.Read())result.Add(new HistorialDocumentoDcavDto
            {Id=I(rd,"historial_id"),Accion=S(rd,"accion"),UsuarioId=NI(rd,"usuario_id"),UsuarioNombre=S(rd,"usuario_nombre"),Rol=S(rd,"rol"),EstadoAnterior=S(rd,"estado_anterior"),EstadoNuevo=S(rd,"estado_nuevo"),Observacion=S(rd,"observacion"),Fecha=D(rd,"fecha"),CorrelationId=S(rd,"correlation_id")});}
            return result;
        }

        public RevisionDocumentosDcavDto BloquearDetalle(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId,int inspeccionId)
        {
            using(var lockCmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key));",cn,tx)){lockCmd.Parameters.AddWithValue("@key","REVISION_DOCUMENTOS_DCAV:"+solicitudId+":"+inspeccionId);lockCmd.ExecuteNonQuery();}
            using(var cmd=new NpgsqlCommand(BaseQuery+" AND s.codigo_solicitud=@solicitud AND i.codigo_inspeccion=@inspeccion FOR UPDATE OF pe,a,c;",cn,tx))
            {cmd.Parameters.AddWithValue("@solicitud",solicitudId);cmd.Parameters.AddWithValue("@inspeccion",inspeccionId);using(var rd=cmd.ExecuteReader()){if(!rd.Read())return null;return Copiar(Map(rd));}}
        }

        public bool ExisteIdempotencia(NpgsqlConnection cn,NpgsqlTransaction tx,string clave)
        {using(var cmd=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM public.aocr_proceso_idempotencia WHERE clave=@clave);",cn,tx)){cmd.Parameters.AddWithValue("@clave",clave);return Convert.ToBoolean(cmd.ExecuteScalar());}}

        public void AplicarDecision(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,bool aprobar,bool observarAocr,bool observarCondiciones,int usuarioId,string rol,string seccion,string observacion,string clave,string ip,string correlation)
        {
            var ea=aprobar||!observarAocr?"APROBADO_DCAV":"OBSERVADO_DCAV";
            var ec=aprobar||!observarCondiciones?"APROBADO_DCAV":"OBSERVADO_DCAV";
            ActualizarDocumento(cn,tx,d.AocrId,d.VersionAocrEnviada,ea,usuarioId);
            ActualizarDocumento(cn,tx,d.CondicionesId,d.VersionCondicionesEnviada,ec,usuarioId);
            var destino=aprobar?"PENDIENTE_FIRMA_DIRDAC":"DOCUMENTOS_OBSERVADOS_DCAV";
            var accion=aprobar?"APROBAR_DOCUMENTOS_DCAV":"DEVOLVER_DOCUMENTOS_DCAV";
            using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_proceso_estado SET estado_actual=@estado,
                etapa_actual=@etapa,rol_responsable=@responsable,siguiente_accion=@siguiente,
                observacion=@observacion,fecha_estado=NOW(),version=version+1
                WHERE solicitud_id=@solicitud AND inspeccion_id=@inspeccion AND activo=TRUE
                  AND estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV' AND version=@version;",cn,tx))
            {cmd.Parameters.AddWithValue("@estado",destino);cmd.Parameters.AddWithValue("@etapa",aprobar?"FIRMA_DIRDAC":"CORRECCION_DOCUMENTOS_INSPECTOR");cmd.Parameters.AddWithValue("@responsable",aprobar?"DIRDAC":"InspectorTecnico");cmd.Parameters.AddWithValue("@siguiente",aprobar?"FIRMAR_DOCUMENTOS_DIRDAC":"CORREGIR_DOCUMENTOS_OBSERVADOS");cmd.Parameters.AddWithValue("@observacion",(object)observacion??DBNull.Value);cmd.Parameters.AddWithValue("@solicitud",d.SolicitudId);cmd.Parameters.AddWithValue("@inspeccion",d.InspeccionId);cmd.Parameters.AddWithValue("@version",d.VersionExpediente);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}
            if(aprobar)InsertHist(cn,tx,d,"APROBADO_DOCUMENTOS_DCAV",usuarioId,rol,"DCAV_APROBAR_DOCUMENTOS",observacion,clave,ip,correlation);
            InsertHist(cn,tx,d,destino,usuarioId,rol,accion,"Documento="+(aprobar?"AMBOS":observarAocr&&observarCondiciones?"AMBOS":observarAocr?"AOCR":"CONDICIONES")+";Seccion="+(seccion??"")+";Observacion="+(observacion??""),clave,ip,correlation);
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,descripcion,detalle,modulo,resultado)
                VALUES('aocr_tbdocumento_generado',@solicitud,@accion,@usuario,NOW(),@descripcion,@detalle,'DCAV_DOCUMENTOS','OK');",cn,tx)){cmd.Parameters.AddWithValue("@solicitud",d.SolicitudId);cmd.Parameters.AddWithValue("@accion",accion);cmd.Parameters.AddWithValue("@usuario",usuarioId.ToString());cmd.Parameters.AddWithValue("@descripcion",observacion??accion);cmd.Parameters.AddWithValue("@detalle",Detalle(d,clave,correlation,ip));cmd.ExecuteNonQuery();}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_idempotencia(clave,solicitud_id,fecha_registro,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,correlation_id)
                VALUES(@clave,@solicitud,NOW(),@aocr,@condiciones,'PENDIENTE_REVISION_DOCUMENTOS_DCAV',@nuevo,'OK',@correlation);",cn,tx)){cmd.Parameters.AddWithValue("@clave",clave);cmd.Parameters.AddWithValue("@solicitud",d.SolicitudId);cmd.Parameters.AddWithValue("@aocr",d.AocrId);cmd.Parameters.AddWithValue("@condiciones",d.CondicionesId);cmd.Parameters.AddWithValue("@nuevo",destino);cmd.Parameters.AddWithValue("@correlation",(object)correlation??DBNull.Value);cmd.ExecuteNonQuery();}
            CrearNotificaciones(cn,tx,d,aprobar,clave,correlation);
        }

        private static void ActualizarDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,int id,int version,string estado,int usuario)
        {using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_tbdocumento_generado SET estado=@estado,codigo_usuario=@usuario,fecha_actualizacion=NOW()
             WHERE codigo_documento=@id AND version=@version AND vigente=TRUE AND eliminado=FALSE AND estado='ENVIADO_DCAV';",cn,tx)){cmd.Parameters.AddWithValue("@estado",estado);cmd.Parameters.AddWithValue("@usuario",usuario);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@version",version);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}}
        private static void InsertHist(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,string nuevo,int usuario,string rol,string accion,string obs,string clave,string ip,string corr)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_estado_historial(solicitud_id,inspeccion_id,informe_id,estado_anterior,estado_nuevo,etapa,accion,rol_usuario,usuario_id,rol_responsable,observacion,fecha_creacion,ip,correlation_id,clave_idempotencia,resultado)
             VALUES(@s,@i,@inf,@anterior,@nuevo,'REVISION_DOCUMENTOS_DCAV',@accion,@rol,@u,@resp,@obs,NOW(),@ip,@corr,@clave,'OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@inf",d.InformeTecnicoId);cmd.Parameters.AddWithValue("@anterior",nuevo=="PENDIENTE_FIRMA_DIRDAC"?"APROBADO_DOCUMENTOS_DCAV":"PENDIENTE_REVISION_DOCUMENTOS_DCAV");cmd.Parameters.AddWithValue("@nuevo",nuevo);cmd.Parameters.AddWithValue("@accion",accion);cmd.Parameters.AddWithValue("@rol",rol??"");cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@resp",nuevo=="PENDIENTE_FIRMA_DIRDAC"?"DIRDAC":"InspectorTecnico");cmd.Parameters.AddWithValue("@obs",obs??"");cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);cmd.Parameters.AddWithValue("@clave",clave);cmd.ExecuteNonQuery();}}
        private static void CrearNotificaciones(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,bool aprobar,string clave,string correlation)
        {var sql=aprobar?@"SELECT DISTINCT u.idusuario FROM public.usuario u LEFT JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario AND COALESCE(ur.activo,TRUE) LEFT JOIN public.rol r ON r.codigorol=COALESCE(ur.codigorol,u.codigorol) WHERE UPPER(COALESCE(r.descripcion,u.rol,'')) IN ('DIRDAC','DIRECTORGENERAL','DIRECTOR GENERAL','DGAC') AND UPPER(COALESCE(u.estadoactividad,'ACTIVO')) NOT IN ('INACTIVO','BLOQUEADO','ELIMINADO')":@"SELECT idusuario FROM public.usuario WHERE idusuario=@inspector AND UPPER(COALESCE(estadoactividad,'ACTIVO')) NOT IN ('INACTIVO','BLOQUEADO','ELIMINADO')";using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbnotificacion(codigousuario,titulo,mensaje,tipo,url,fechacreacion,leida,event_key,correlation_id) "+"SELECT q.idusuario,@titulo,@mensaje,@tipo,@url,NOW(),FALSE,@clave||':NOTIFICACION:'||q.idusuario,@correlation FROM ("+sql+") q ON CONFLICT(event_key) WHERE event_key IS NOT NULL DO NOTHING;",cn,tx)){cmd.Parameters.AddWithValue("@inspector",d.InspectorId);cmd.Parameters.AddWithValue("@titulo",aprobar?"Documentos pendientes de firma DIRDAC":"Documentos observados por DCAV");cmd.Parameters.AddWithValue("@mensaje",aprobar?"AOCR y Condiciones fueron aprobados por DCAV.":"DCAV devolvio documentos al Inspector asignado.");cmd.Parameters.AddWithValue("@tipo",aprobar?"PENDIENTE_FIRMA_DIRDAC":"DOCUMENTOS_OBSERVADOS_DCAV");cmd.Parameters.AddWithValue("@url",aprobar?"/aocr/Direccion/FirmasPendientes":"/aocr/InspectorDocumentosFinales/Detalle?solicitudId="+d.SolicitudId);cmd.Parameters.AddWithValue("@clave",clave);cmd.Parameters.AddWithValue("@correlation",(object)correlation??DBNull.Value);cmd.ExecuteNonQuery();}}
        private static string Detalle(RevisionDocumentosDcavDto d,string clave,string corr,string ip){return "SolicitudId="+d.SolicitudId+";InspeccionId="+d.InspeccionId+";AocrId="+d.AocrId+";VersionAocr="+d.VersionAocrEnviada+";AocrPdfId="+d.AocrPdfId+";CondicionesId="+d.CondicionesId+";VersionCondiciones="+d.VersionCondicionesEnviada+";CondicionesPdfId="+d.CondicionesPdfId+";Informe="+d.InformeTecnicoId+";LV="+d.LvEaeId+";IP="+ip+";CorrelationId="+corr+";Clave="+clave;}

        private static RevisionDocumentosDcavDto Copiar(DocumentosPendientesDcavDto x){return new RevisionDocumentosDcavDto{SolicitudId=x.SolicitudId,InspeccionId=x.InspeccionId,NumeroSolicitud=x.NumeroSolicitud,Explotador=x.Explotador,Pais=x.Pais,TipoTramite=x.TipoTramite,InspectorId=x.InspectorId,InspectorNombre=x.InspectorNombre,FechaEnvio=x.FechaEnvio,EstadoFuncional=x.EstadoFuncional,VersionExpediente=x.VersionExpediente,AocrId=x.AocrId,VersionAocrEnviada=x.VersionAocrEnviada,AocrPdfId=x.AocrPdfId,EstadoAocr=x.EstadoAocr,InspectorAocrId=x.InspectorAocrId,CompaniaAocr=x.CompaniaAocr,CondicionesId=x.CondicionesId,VersionCondicionesEnviada=x.VersionCondicionesEnviada,CondicionesPdfId=x.CondicionesPdfId,EstadoCondiciones=x.EstadoCondiciones,InspectorCondicionesId=x.InspectorCondicionesId,CompaniaCondiciones=x.CompaniaCondiciones,InformeTecnicoId=x.InformeTecnicoId,InformeTecnicoPdfId=x.InformeTecnicoPdfId,InformeRuta=x.InformeRuta,InformeHash=x.InformeHash,LvEaeId=x.LvEaeId,LvEaePdfId=x.LvEaePdfId,LvEaeRuta=x.LvEaeRuta,LvEaeHash=x.LvEaeHash,ObservacionesAbiertas=x.ObservacionesAbiertas,UltimaAccion=x.UltimaAccion,FechaUltimaAccion=x.FechaUltimaAccion,CodigoCompania=x.CodigoCompania};}
        private static DocumentosPendientesDcavDto Map(NpgsqlDataReader r){return new DocumentosPendientesDcavDto{SolicitudId=I(r,"solicitud_id"),InspeccionId=I(r,"inspeccion_id"),NumeroSolicitud=S(r,"numero_solicitud"),Explotador=S(r,"explotador"),Pais=S(r,"pais"),TipoTramite=S(r,"tipo_tramite"),InspectorId=I(r,"inspector_id"),InspectorNombre=S(r,"inspector_nombre"),FechaEnvio=D(r,"fecha_envio"),EstadoFuncional=S(r,"estado_funcional"),VersionExpediente=L(r,"version_expediente"),AocrId=I(r,"aocr_id"),VersionAocrEnviada=I(r,"aocr_version"),AocrPdfId=I(r,"aocr_pdf_id"),EstadoAocr=S(r,"aocr_estado"),InspectorAocrId=I(r,"aocr_inspector_id"),CompaniaAocr=S(r,"aocr_compania"),CondicionesId=I(r,"condiciones_id"),VersionCondicionesEnviada=I(r,"condiciones_version"),CondicionesPdfId=I(r,"condiciones_pdf_id"),EstadoCondiciones=S(r,"condiciones_estado"),InspectorCondicionesId=I(r,"condiciones_inspector_id"),CompaniaCondiciones=S(r,"condiciones_compania"),InformeTecnicoId=I(r,"informe_id"),InformeRuta=S(r,"informe_ruta"),InformeHash=S(r,"informe_hash"),LvEaeId=I(r,"lv_id"),LvEaeRuta=S(r,"lv_ruta"),LvEaeHash=S(r,"lv_hash"),ObservacionesAbiertas=I(r,"observaciones_abiertas"),UltimaAccion=S(r,"ultima_accion"),FechaUltimaAccion=D(r,"fecha_ultima_accion"),CodigoCompania=S(r,"codigo_compania")};}
        private static int I(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt32(r[n]);}private static int? NI(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?(int?)null:Convert.ToInt32(r[n]);}private static long L(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt64(r[n]);}private static string S(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?null:Convert.ToString(r[n]);}private static DateTime D(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?DateTime.MinValue:Convert.ToDateTime(r[n]);}

        private const string BaseQuery=@"SELECT s.codigo_solicitud AS solicitud_id,i.codigo_inspeccion AS inspeccion_id,
          s.numero_solicitud AS numero_solicitud,COALESCE(NULLIF(s.razon_social,''),NULLIF(s.nombre_operador,''),s.nombre_comercial,'') AS explotador,
          COALESCE(s.pais,'') AS pais,COALESCE(s.tipo_operacion,s.tipo_solicitud::text) AS tipo_tramite,
          COALESCE(i.codigo_inspector,s.codigo_tecnico,0) AS inspector_id,TRIM(COALESCE(ui.nombreusuario,'')||' '||COALESCE(ui.apellidousuario,'')) AS inspector_nombre,
          pe.fecha_estado AS fecha_envio,pe.estado_actual AS estado_funcional,pe.version AS version_expediente,
          a.codigo_documento AS aocr_id,a.version AS aocr_version,ap.codigo_documento AS aocr_pdf_id,a.estado AS aocr_estado,COALESCE(a.codigo_inspector,0) AS aocr_inspector_id,COALESCE(a.codigo_compania,'') AS aocr_compania,
          c.codigo_documento AS condiciones_id,c.version AS condiciones_version,cp.codigo_documento AS condiciones_pdf_id,c.estado AS condiciones_estado,COALESCE(c.codigo_inspector,0) AS condiciones_inspector_id,COALESCE(c.codigo_compania,'') AS condiciones_compania,
          inf.codigo_informe AS informe_id,inf.ruta_documento_firmado AS informe_ruta,inf.hash_documento AS informe_hash,
          lv.codigo_lv AS lv_id,lv.ruta_documento_firmado AS lv_ruta,lv.hash_documento AS lv_hash,
          0 AS observaciones_abiertas,env.accion AS ultima_accion,env.fecha_creacion AS fecha_ultima_accion,
          COALESCE(NULLIF(a.codigo_compania,''),NULLIF(c.codigo_compania,''),s.compania_id,'') AS codigo_compania
        FROM public.aocr_proceso_estado pe
        JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=pe.solicitud_id AND s.deleted_at IS NULL
        JOIN public.aocr_tbinspeccion i ON i.codigo_inspeccion=pe.inspeccion_id AND i.codigo_solicitud=s.codigo_solicitud
        LEFT JOIN public.usuario ui ON ui.idusuario=COALESCE(i.codigo_inspector,s.codigo_tecnico)
        JOIN LATERAL(SELECT h.* FROM public.aocr_proceso_estado_historial h WHERE h.solicitud_id=s.codigo_solicitud AND h.accion='ENVIAR_DOCUMENTOS_DCAV' ORDER BY h.fecha_creacion DESC,h.id DESC LIMIT 1) env ON TRUE
        JOIN public.aocr_tbdocumento_generado a ON a.codigo_documento=substring(env.observacion from 'AocrId=([0-9]+)')::integer AND a.codigo_solicitud=s.codigo_solicitud AND a.codigo_inspeccion=i.codigo_inspeccion
        JOIN public.aocr_tbdocumento_generado c ON c.codigo_documento=substring(env.observacion from 'CondicionesId=([0-9]+)')::integer AND c.codigo_solicitud=s.codigo_solicitud AND c.codigo_inspeccion=i.codigo_inspeccion
        JOIN public.aocr_tbdocumento_inspeccion ap ON ap.codigo_documento=substring(env.observacion from 'AocrPdfId=([0-9]+)')::integer AND ap.codigo_documento_base=a.codigo_documento
        JOIN public.aocr_tbdocumento_inspeccion cp ON cp.codigo_documento=substring(env.observacion from 'CondicionesPdfId=([0-9]+)')::integer AND cp.codigo_documento_base=c.codigo_documento
        JOIN LATERAL(SELECT x.* FROM public.aocr_tbinforme_inspeccion x WHERE x.codigo_inspeccion=i.codigo_inspeccion ORDER BY x.version DESC,x.codigo_informe DESC LIMIT 1) inf ON inf.finalizado=TRUE AND inf.firmado_inspector=TRUE AND UPPER(COALESCE(inf.estado_informe,''))='INFORME_TECNICO_APROBADO_DCAV' AND NULLIF(TRIM(inf.ruta_documento_firmado),'') IS NOT NULL AND NULLIF(TRIM(inf.hash_documento),'') IS NOT NULL AND UPPER(COALESCE(inf.resultado,'')) LIKE '%SATISFACTORIO%' AND UPPER(COALESCE(inf.resultado,'')) NOT LIKE '%INSATISFACTORIO%'
        JOIN LATERAL(SELECT x.* FROM public.aocr_tblv_operacional_eae x WHERE x.codigo_inspeccion=i.codigo_inspeccion ORDER BY x.version DESC,x.codigo_lv DESC LIMIT 1) lv ON lv.finalizado=TRUE AND lv.firmado_tecnico=TRUE AND NULLIF(TRIM(lv.ruta_documento_firmado),'') IS NOT NULL AND NULLIF(TRIM(lv.hash_documento),'') IS NOT NULL
        WHERE pe.activo=TRUE AND pe.estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV'
          AND a.estado='ENVIADO_DCAV' AND c.estado='ENVIADO_DCAV' AND a.vigente=TRUE AND c.vigente=TRUE AND a.eliminado=FALSE AND c.eliminado=FALSE
          AND COALESCE(a.hash_pdf_firmado,'')='' AND COALESCE(c.hash_pdf_firmado,'')='' AND UPPER(COALESCE(s.estado,'')) NOT IN ('ANULADA','ANULADO','FINALIZADA','FINALIZADO')";
    }
}
