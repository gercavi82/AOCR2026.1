using System;
using System.Collections.Generic;
using System.Configuration;
using CapaDatos.Models;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class AprobacionDocumentosDcavDAO
    {
        private readonly string _cs;
        public AprobacionDocumentosDcavDAO(){var x=ConfigurationManager.ConnectionStrings["AOCRConnection"];_cs=x!=null?x.ConnectionString:ConexionDAO.CadenaConexion;}
        public NpgsqlConnection CrearConexion(){return new NpgsqlConnection(_cs);}

        public bool UsuarioDcavActivo(NpgsqlConnection cn,NpgsqlTransaction tx,int usuarioId)
        {
            const string sql=@"SELECT EXISTS(SELECT 1 FROM public.usuario u LEFT JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario AND COALESCE(ur.activo,TRUE) LEFT JOIN public.rol r ON r.codigorol=COALESCE(ur.codigorol,u.codigorol) WHERE u.idusuario=@u AND UPPER(COALESCE(u.estadoactividad,'1')) NOT IN ('0','INACTIVO','BLOQUEADO','ELIMINADO') AND UPPER(TRIM(COALESCE(r.descripcion,u.rol,'')))='DIRECTOR_CERTIFICACIONES_DCAV');";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@u",usuarioId);return Convert.ToBoolean(cmd.ExecuteScalar());}
        }

        public ResultadoIdempotenciaAprobacion ObtenerIdempotencia(NpgsqlConnection cn,NpgsqlTransaction tx,string clave)
        {using(var cmd=new NpgsqlCommand(@"SELECT solicitud_id,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,fecha_registro FROM public.aocr_proceso_idempotencia WHERE clave=@k;",cn,tx)){cmd.Parameters.AddWithValue("@k",clave);using(var rd=cmd.ExecuteReader()){return rd.Read()?new ResultadoIdempotenciaAprobacion{SolicitudId=Convert.ToInt32(rd[0]),AocrId=Convert.ToInt32(rd[1]),CondicionesId=Convert.ToInt32(rd[2]),EstadoAnterior=Convert.ToString(rd[3]),EstadoNuevo=Convert.ToString(rd[4]),Resultado=Convert.ToString(rd[5]),Fecha=Convert.ToDateTime(rd[6])}:null;}}}

        public int ContarObservacionesNoCerradas(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId)
        {using(var cmd=new NpgsqlCommand(@"SELECT COUNT(*) FROM public.aocr_tbobservacion WHERE codigo_solicitud=@s AND mensaje LIKE '{%DCAV_DOCUMENTAL_V1%' AND UPPER(COALESCE((mensaje::jsonb->>'Estado'),''))<>'CERRADA_DCAV';",cn,tx)){cmd.Parameters.AddWithValue("@s",solicitudId);return Convert.ToInt32(cmd.ExecuteScalar());}}

        public void AprobarDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,int id,int solicitud,int inspeccion,int version,int usuario,string nombre)
        {using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_tbdocumento_generado SET estado='APROBADO_DCAV',codigo_usuario=@u,usuario_nombre='DCAV',fecha_actualizacion=NOW() WHERE codigo_documento=@id AND codigo_solicitud=@s AND codigo_inspeccion=@i AND version=@v AND vigente=TRUE AND eliminado=FALSE AND estado='ENVIADO_DCAV' AND COALESCE(hash_pdf_firmado,'')='';",cn,tx)){cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@s",solicitud);cmd.Parameters.AddWithValue("@i",inspeccion);cmd.Parameters.AddWithValue("@v",version);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT:"+nombre);}}

        public void PrepararFirmaDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,int id,int solicitud,int inspeccion,int version,string estadoFirma)
        {using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_tbdocumento_generado SET estado=@estado,fecha_actualizacion=NOW() WHERE codigo_documento=@id AND codigo_solicitud=@s AND codigo_inspeccion=@i AND version=@v AND vigente=TRUE AND eliminado=FALSE AND estado='APROBADO_DCAV';",cn,tx)){cmd.Parameters.AddWithValue("@estado",estadoFirma);cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@s",solicitud);cmd.Parameters.AddWithValue("@i",inspeccion);cmd.Parameters.AddWithValue("@v",version);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT:PREPARAR_FIRMA:"+id);}}

        public void CambiarEstado(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,int usuario,string detalle)
        {using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_proceso_estado SET estado_actual='PENDIENTE_FIRMAS_INSTITUCIONALES',etapa_actual='FIRMAS_INSTITUCIONALES',rol_responsable='FIRMANTES_INSTITUCIONALES',siguiente_accion='COMPLETAR_FIRMAS_INSTITUCIONALES',observacion=@o,fecha_estado=NOW(),version=version+1 WHERE solicitud_id=@s AND inspeccion_id=@i AND activo=TRUE AND estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV' AND version=@v;",cn,tx)){cmd.Parameters.AddWithValue("@o",detalle);cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@v",d.VersionExpediente);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT:ESTADO");}}

        public void RegistrarHistorial(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,int usuario,string rol,string clave,string ip,string corr,string detalle)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_estado_historial(solicitud_id,inspeccion_id,informe_id,estado_anterior,estado_nuevo,etapa,accion,rol_usuario,usuario_id,rol_responsable,observacion,fecha_creacion,ip,correlation_id,clave_idempotencia,resultado) VALUES(@s,@i,@inf,'PENDIENTE_REVISION_DOCUMENTOS_DCAV','PENDIENTE_FIRMAS_INSTITUCIONALES','FIRMAS_INSTITUCIONALES','APROBAR_DOCUMENTOS_DCAV',@rol,@u,'FIRMANTES_INSTITUCIONALES',@o,NOW(),@ip,@corr,@clave,'OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@inf",d.InformeTecnicoId);cmd.Parameters.AddWithValue("@rol",rol??"");cmd.Parameters.AddWithValue("@u",usuario);cmd.Parameters.AddWithValue("@o",detalle);cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);cmd.Parameters.AddWithValue("@clave",clave);cmd.ExecuteNonQuery();}}

        public void RegistrarAuditoria(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitud,int usuario,string evento,string detalle)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,descripcion,detalle,modulo,resultado) VALUES('aocr_tbdocumento_generado',@s,@a,@u,NOW(),@a,@d,'DCAV_APROBACION_DOCUMENTOS','OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",solicitud);cmd.Parameters.AddWithValue("@a",evento);cmd.Parameters.AddWithValue("@u",usuario.ToString());cmd.Parameters.AddWithValue("@d",detalle);cmd.ExecuteNonQuery();}}

        public int CrearNotificacionesYOutbox(NpgsqlConnection cn,NpgsqlTransaction tx,RevisionDocumentosDcavDto d,string clave,string corr,string mensaje)
        {
            var destinatarios=new List<DestinatarioFirma>();
            using(var cmd=new NpgsqlCommand(@"SELECT DISTINCT u.idusuario,u.correo,TRIM(COALESCE(u.nombreusuario,'')||' '||COALESCE(u.apellidousuario,'')) FROM public.usuario u JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario AND COALESCE(ur.activo,TRUE) JOIN public.rol r ON r.codigorol=ur.codigorol WHERE UPPER(TRIM(r.descripcion)) IN ('DIRECCION','DIRECTOR_CERTIFICACIONES_DCAV') AND UPPER(COALESCE(u.estadoactividad,'1')) NOT IN ('0','INACTIVO','BLOQUEADO','ELIMINADO');",cn,tx))using(var rd=cmd.ExecuteReader())while(rd.Read())destinatarios.Add(new DestinatarioFirma{UsuarioId=Convert.ToInt32(rd[0]),Correo=Convert.ToString(rd[1]),Nombre=Convert.ToString(rd[2])});
            var total=0;
            foreach(var x in destinatarios)
            {
                using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbnotificacion(codigousuario,titulo,mensaje,tipo,url,fechacreacion,leida,event_key,correlation_id) VALUES(@u,'Firma institucional AOCR',@m,'PENDIENTE_FIRMA_DIRDAC',@url,NOW(),FALSE,@key,@corr) ON CONFLICT(event_key) WHERE event_key IS NOT NULL DO NOTHING;",cn,tx)){cmd.Parameters.AddWithValue("@u",x.UsuarioId);cmd.Parameters.AddWithValue("@m",mensaje);cmd.Parameters.AddWithValue("@url","/aocr/FirmaInstitucionalAocr/Detalle?solicitudId="+d.SolicitudId+"&inspeccionId="+d.InspeccionId);cmd.Parameters.AddWithValue("@key",clave+":NOTIFICACION:"+x.UsuarioId);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);total+=cmd.ExecuteNonQuery();}
                if(!string.IsNullOrWhiteSpace(x.Correo))using(var outbox=new NpgsqlCommand(@"INSERT INTO public.email_queue(to_address,subject,body,status,solicitud_id,created_at,proximo_intento,correlation_id,tipo_notificacion,event_key,intentos,updated_at) VALUES(@to,'AOCR y Condiciones disponibles para firma institucional',@body,'PENDIENTE',@s,NOW(),NOW(),@corr,'PENDIENTE_FIRMA_DIRDAC',@key,0,NOW()) ON CONFLICT(event_key) WHERE event_key IS NOT NULL DO NOTHING;",cn,tx)){outbox.Parameters.AddWithValue("@to",x.Correo);outbox.Parameters.AddWithValue("@body",mensaje);outbox.Parameters.AddWithValue("@s",d.SolicitudId);outbox.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);outbox.Parameters.AddWithValue("@key",clave+":EMAIL:"+x.UsuarioId);outbox.ExecuteNonQuery();}
            }
            return total;
        }

        public void RegistrarIdempotencia(NpgsqlConnection cn,NpgsqlTransaction tx,string clave,RevisionDocumentosDcavDto d,string corr)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_idempotencia(clave,solicitud_id,fecha_registro,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,correlation_id) VALUES(@k,@s,NOW(),@a,@c,'PENDIENTE_REVISION_DOCUMENTOS_DCAV','PENDIENTE_FIRMAS_INSTITUCIONALES','OK',@corr);",cn,tx)){cmd.Parameters.AddWithValue("@k",clave);cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@a",d.AocrId);cmd.Parameters.AddWithValue("@c",d.CondicionesId);cmd.Parameters.AddWithValue("@corr",(object)corr??DBNull.Value);cmd.ExecuteNonQuery();}}

        private sealed class DestinatarioFirma{public int UsuarioId{get;set;}public string Correo{get;set;}public string Nombre{get;set;}}
    }
    public sealed class ResultadoIdempotenciaAprobacion{public int SolicitudId{get;set;}public int AocrId{get;set;}public int CondicionesId{get;set;}public string EstadoAnterior{get;set;}public string EstadoNuevo{get;set;}public string Resultado{get;set;}public DateTime Fecha{get;set;}}
}
