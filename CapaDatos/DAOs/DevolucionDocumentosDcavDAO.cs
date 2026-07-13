using System;
using System.Collections.Generic;
using System.Configuration;
using CapaDatos.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class DevolucionDocumentosDcavDAO
    {
        private readonly string _connectionString;
        public DevolucionDocumentosDcavDAO()
        {
            var cs=ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString=cs!=null?cs.ConnectionString:ConexionDAO.CadenaConexion;
        }
        public NpgsqlConnection CrearConexion(){return new NpgsqlConnection(_connectionString);}

        public VersionCorreccionDcavRegistro CrearCorreccion(NpgsqlConnection cn,NpgsqlTransaction tx,int id,int version,int usuario)
        {
            using(var old=new NpgsqlCommand(@"UPDATE public.aocr_tbdocumento_generado SET vigente=FALSE,estado='OBSERVADO_DCAV',codigo_usuario=@u,fecha_actualizacion=NOW()
                WHERE codigo_documento=@id AND version=@v AND vigente=TRUE AND eliminado=FALSE AND estado='ENVIADO_DCAV';",cn,tx))
            {old.Parameters.AddWithValue("@u",usuario);old.Parameters.AddWithValue("@id",id);old.Parameters.AddWithValue("@v",version);if(old.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbdocumento_generado
                (codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,nombre_archivo,ruta_documento,tamanio_pdf,estado,fecha_generacion,codigo_usuario,usuario_nombre,created_at,hash_pdf_firmado,fecha_liberacion,disponible_rt,fecha_disponible_rt,codigo_compania,codigo_inspector,version,vigente,eliminado,usuario_creador_id,fecha_actualizacion)
                SELECT codigo_solicitud,codigo_inspeccion,tipo_documento,numero_aocr,'','',NULL,'CORRECCION_INSPECTOR',NOW(),@u,'Inspector',NOW(),NULL,NULL,FALSE,NULL,codigo_compania,codigo_inspector,version+1,TRUE,FALSE,@u,NOW()
                FROM public.aocr_tbdocumento_generado WHERE codigo_documento=@id AND version=@v AND vigente=FALSE AND estado='OBSERVADO_DCAV'
                RETURNING codigo_documento,version;",cn,tx))
            {cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@v",version);using(var rd=cmd.ExecuteReader()){if(!rd.Read())throw new InvalidOperationException("CONCURRENCY_CONFLICT");return new VersionCorreccionDcavRegistro{DocumentoId=Convert.ToInt32(rd[0]),Version=Convert.ToInt32(rd[1])};}}
        }

        public void AprobarSinCambios(NpgsqlConnection cn,NpgsqlTransaction tx,int id,int version,int usuario)
        {using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_tbdocumento_generado SET estado='APROBADO_DCAV',codigo_usuario=@u,fecha_actualizacion=NOW()
             WHERE codigo_documento=@id AND version=@v AND vigente=TRUE AND eliminado=FALSE AND estado='ENVIADO_DCAV';",cn,tx)){cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@v",version);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}}

        public int InsertarObservacion(NpgsqlConnection cn,NpgsqlTransaction tx,ObservacionDocumentoDcavRegistro x)
        {
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbobservacion(codigo_solicitud,codigo_usuario,mensaje,fecha_registro,compania_id)
                VALUES(@s,@u,@m,NOW(),@c) RETURNING codigo_observacion;",cn,tx))
            {cmd.Parameters.AddWithValue("@s",x.SolicitudId);cmd.Parameters.AddWithValue("@u",x.UsuarioDcavId);cmd.Parameters.AddWithValue("@m",Serializar(x));cmd.Parameters.AddWithValue("@c",(object)x.CodigoCompania??DBNull.Value);return Convert.ToInt32(cmd.ExecuteScalar());}
        }

        public IList<ObservacionDocumentoDcavRegistro> ObtenerObservaciones(int solicitudId)
        {
            var result=new List<ObservacionDocumentoDcavRegistro>();
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(@"SELECT codigo_observacion,codigo_solicitud,codigo_usuario,mensaje,fecha_registro,compania_id FROM public.aocr_tbobservacion WHERE codigo_solicitud=@s AND mensaje LIKE '{%DCAV_DOCUMENTAL_V1%' ORDER BY fecha_registro DESC,codigo_observacion DESC;",cn))
            {cn.Open();cmd.Parameters.AddWithValue("@s",solicitudId);using(var rd=cmd.ExecuteReader())while(rd.Read()){var x=Deserializar(Convert.ToString(rd[3]));if(x==null)continue;x.ObservacionId=Convert.ToInt32(rd[0]);x.Fecha=Convert.ToDateTime(rd[4]);result.Add(x);}}
            return result;
        }

        public ObservacionDocumentoDcavRegistro CambiarEstadoObservacion(NpgsqlConnection cn,NpgsqlTransaction tx,int observacionId,int solicitudId,int documentoCorreccionId,int usuarioId,string estadoEsperado,string estadoNuevo,bool exigirInspector)
        {
            using(var cmd=new NpgsqlCommand(@"SELECT o.mensaje,COALESCE(i.codigo_inspector,s.codigo_tecnico,0) inspector,d.estado FROM public.aocr_tbobservacion o JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=o.codigo_solicitud LEFT JOIN public.aocr_tbinspeccion i ON i.codigo_solicitud=s.codigo_solicitud JOIN public.aocr_tbdocumento_generado d ON d.codigo_documento=@doc AND d.codigo_solicitud=o.codigo_solicitud WHERE o.codigo_observacion=@id AND o.codigo_solicitud=@s AND o.mensaje LIKE '{%DCAV_DOCUMENTAL_V1%' FOR UPDATE OF o;",cn,tx))
            {cmd.Parameters.AddWithValue("@id",observacionId);cmd.Parameters.AddWithValue("@s",solicitudId);cmd.Parameters.AddWithValue("@doc",documentoCorreccionId);using(var rd=cmd.ExecuteReader()){if(!rd.Read())return null;var x=Deserializar(Convert.ToString(rd[0]));var inspector=Convert.ToInt32(rd[1]);var estadoDocumento=Convert.ToString(rd[2]);if(x==null||x.DocumentoCorreccionId!=documentoCorreccionId)return null;if(exigirInspector&&inspector!=usuarioId)throw new UnauthorizedAccessException("INSPECTOR_FORBIDDEN");if(exigirInspector&&estadoDocumento!="CORREGIDO_INSPECTOR"&&estadoDocumento!="GENERADO")throw new InvalidOperationException("CORRECTION_NOT_READY");if(!string.Equals(x.Estado,estadoEsperado,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("OBSERVATION_STATE_CONFLICT");x.ObservacionId=observacionId;x.Estado=estadoNuevo;rd.Close();using(var up=new NpgsqlCommand("UPDATE public.aocr_tbobservacion SET mensaje=@m,codigo_usuario_respuesta=@u WHERE codigo_observacion=@id;",cn,tx)){up.Parameters.AddWithValue("@m",Serializar(x));up.Parameters.AddWithValue("@u",usuarioId);up.Parameters.AddWithValue("@id",observacionId);if(up.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}using(var audit=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,descripcion,detalle,modulo,resultado) VALUES('aocr_tbobservacion',@id,@accion,@u,NOW(),@accion,@detalle,'DCAV_DOCUMENTOS','OK');",cn,tx)){audit.Parameters.AddWithValue("@id",observacionId);audit.Parameters.AddWithValue("@accion",estadoNuevo);audit.Parameters.AddWithValue("@u",usuarioId.ToString());audit.Parameters.AddWithValue("@detalle","SolicitudId="+solicitudId+";DocumentoCorreccionId="+documentoCorreccionId+";EstadoAnterior="+estadoEsperado+";EstadoNuevo="+estadoNuevo);audit.ExecuteNonQuery();}return x;}}
        }

        public void CambiarEstadoCentral(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,int usuario,string resumen)
        {using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_proceso_estado SET estado_actual='DOCUMENTOS_OBSERVADOS_DCAV',etapa_actual='CORRECCION_DOCUMENTOS_INSPECTOR',rol_responsable='InspectorTecnico',siguiente_accion='CORREGIR_DOCUMENTOS_OBSERVADOS',observacion=@o,fecha_estado=NOW(),version=version+1 WHERE solicitud_id=@s AND inspeccion_id=@i AND activo=TRUE AND estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV' AND version=@v;",cn,tx)){cmd.Parameters.AddWithValue("@o",resumen);cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@v",d.VersionExpediente);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}}

        public void RegistrarTrazabilidad(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,int usuario,string rol,string clave,string ip,string corr,string resumen,VersionCorreccionDcavRegistro a,VersionCorreccionDcavRegistro c)
        {
            var detalle="SolicitudId="+d.SolicitudId+";InspeccionId="+d.InspeccionId+";AocrOrigenId="+d.AocrId+";AocrCorreccionId="+(a!=null?a.DocumentoId:0)+";CondicionesOrigenId="+d.CondicionesId+";CondicionesCorreccionId="+(c!=null?c.DocumentoId:0)+";Clave="+clave+";CorrelationId="+corr;
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_estado_historial(solicitud_id,inspeccion_id,informe_id,estado_anterior,estado_nuevo,etapa,accion,rol_usuario,usuario_id,rol_responsable,observacion,fecha_creacion,ip,correlation_id,clave_idempotencia,resultado) VALUES(@s,@i,@inf,'PENDIENTE_REVISION_DOCUMENTOS_DCAV','DOCUMENTOS_OBSERVADOS_DCAV','REVISION_DOCUMENTOS_DCAV','DEVOLVER_DOCUMENTOS_DCAV',@rol,@u,'InspectorTecnico',@o,NOW(),@ip,@corr,@clave,'OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@inf",d.InformeTecnicoId);cmd.Parameters.AddWithValue("@rol",rol??"");cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@o",resumen+";"+detalle);cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);cmd.Parameters.AddWithValue("@clave",clave);cmd.ExecuteNonQuery();}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,descripcion,detalle,modulo,resultado) VALUES('aocr_tbdocumento_generado',@s,'DEVOLVER_DOCUMENTOS_DCAV',@u,NOW(),@o,@d,'DCAV_DOCUMENTOS','OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@u",usuario.ToString());cmd.Parameters.AddWithValue("@o",resumen);cmd.Parameters.AddWithValue("@d",detalle);cmd.ExecuteNonQuery();}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_idempotencia(clave,solicitud_id,fecha_registro,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,correlation_id) VALUES(@clave,@s,NOW(),@a,@c,'PENDIENTE_REVISION_DOCUMENTOS_DCAV','DOCUMENTOS_OBSERVADOS_DCAV','OK',@corr);",cn,tx)){cmd.Parameters.AddWithValue("@clave",clave);cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@a",a!=null?a.DocumentoId:d.AocrId);cmd.Parameters.AddWithValue("@c",c!=null?c.DocumentoId:d.CondicionesId);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);cmd.ExecuteNonQuery();}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbnotificacion(codigousuario,titulo,mensaje,tipo,url,fechacreacion,leida,event_key,correlation_id) VALUES(@u,'Documentos observados por DCAV',@m,'DOCUMENTOS_OBSERVADOS_DCAV',@url,NOW(),FALSE,@key,@corr) ON CONFLICT(event_key) WHERE event_key IS NOT NULL DO NOTHING;",cn,tx)){cmd.Parameters.AddWithValue("@u",d.InspectorId);cmd.Parameters.AddWithValue("@m",resumen);cmd.Parameters.AddWithValue("@url","/aocr/InspectorDocumentosFinales/Detalle?solicitudId="+d.SolicitudId);cmd.Parameters.AddWithValue("@key",clave+":NOTIFICACION:"+d.InspectorId);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);cmd.ExecuteNonQuery();}
        }

        private static string Serializar(ObservacionDocumentoDcavRegistro x)
        {var j=JObject.FromObject(x);j.AddFirst(new JProperty("schema","DCAV_DOCUMENTAL_V1"));return j.ToString(Formatting.None);}
        private static ObservacionDocumentoDcavRegistro Deserializar(string json){try{var j=JObject.Parse(json);return string.Equals((string)j["schema"],"DCAV_DOCUMENTAL_V1",StringComparison.Ordinal)?j.ToObject<ObservacionDocumentoDcavRegistro>():null;}catch{return null;}}
    }
}
