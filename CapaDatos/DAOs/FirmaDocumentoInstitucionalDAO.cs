using System;
using CapaDatos.Models;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class FirmaDocumentoInstitucionalDAO
    {
        public NpgsqlConnection CrearConexion() { return new NpgsqlConnection(ConexionDAO.CadenaConexion); }

        public FirmaDocumentoInstitucionalSnapshot CargarParaFirma(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, int inspeccionId, string tipoDocumento)
        {
            const string sql = @"
SELECT pe.estado_actual,pe.version version_expediente,pe.informe_id,
       a.codigo_documento aocr_id,a.version version_aocr,a.estado estado_aocr,
       c.codigo_documento condiciones_id,c.version version_condiciones,c.estado estado_condiciones,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN a.codigo_documento ELSE c.codigo_documento END documento_id,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN a.version ELSE c.version END version_documento,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN a.estado ELSE c.estado END estado_documento,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN a.codigo_compania ELSE c.codigo_compania END compania_id,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN pa.codigo_documento ELSE pc.codigo_documento END pdf_origen_id,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN pa.ruta_archivo ELSE pc.ruta_archivo END ruta_pdf_origen,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN pa.hash_archivo ELSE pc.hash_archivo END hash_pdf_origen,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN pa.tamano_bytes ELSE pc.tamano_bytes END tamanio_pdf_origen,
       CASE WHEN @tipo='RECONOCIMIENTO' THEN pa.content_type ELSE pc.content_type END content_type,
       pa.codigo_documento aocr_pdf_id,pc.codigo_documento condiciones_pdf_id
FROM public.aocr_proceso_estado pe
JOIN LATERAL(
  SELECT h.* FROM public.aocr_proceso_estado_historial h
  WHERE h.solicitud_id=pe.solicitud_id AND h.inspeccion_id=pe.inspeccion_id
    AND h.accion='APROBAR_DOCUMENTOS_DCAV'
  ORDER BY h.fecha_creacion DESC,h.id DESC LIMIT 1
) ap ON TRUE
JOIN public.aocr_tbdocumento_generado a
  ON a.codigo_documento=substring(ap.observacion from 'AocrId=([0-9]+)')::integer
 AND a.codigo_solicitud=pe.solicitud_id AND a.codigo_inspeccion=pe.inspeccion_id AND a.vigente=TRUE AND a.eliminado=FALSE
JOIN public.aocr_tbdocumento_generado c
  ON c.codigo_documento=substring(ap.observacion from 'CondicionesId=([0-9]+)')::integer
 AND c.codigo_solicitud=pe.solicitud_id AND c.codigo_inspeccion=pe.inspeccion_id AND c.vigente=TRUE AND c.eliminado=FALSE
JOIN public.aocr_tbdocumento_inspeccion pa
  ON pa.codigo_documento=substring(ap.observacion from 'AocrPdfId=([0-9]+)')::integer AND pa.codigo_documento_base=a.codigo_documento AND pa.codigo_inspeccion=pe.inspeccion_id
JOIN public.aocr_tbdocumento_inspeccion pc
  ON pc.codigo_documento=substring(ap.observacion from 'CondicionesPdfId=([0-9]+)')::integer AND pc.codigo_documento_base=c.codigo_documento AND pc.codigo_inspeccion=pe.inspeccion_id
WHERE pe.solicitud_id=@s AND pe.inspeccion_id=@i AND pe.activo=TRUE
FOR UPDATE OF pe,a,c;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@s", solicitudId); cmd.Parameters.AddWithValue("@i", inspeccionId); cmd.Parameters.AddWithValue("@tipo", tipoDocumento);
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;
                    return new FirmaDocumentoInstitucionalSnapshot
                    {
                        SolicitudId=solicitudId,InspeccionId=inspeccionId,InformeId=I(r,"informe_id"),VersionExpediente=L(r,"version_expediente"),EstadoCentral=S(r,"estado_actual"),
                        DocumentoId=I(r,"documento_id"),VersionDocumento=I(r,"version_documento"),EstadoDocumento=S(r,"estado_documento"),TipoDocumento=tipoDocumento,CompaniaId=S(r,"compania_id"),
                        PdfOrigenId=I(r,"pdf_origen_id"),RutaPdfOrigen=S(r,"ruta_pdf_origen"),HashPdfOrigen=S(r,"hash_pdf_origen"),TamanioPdfOrigen=L(r,"tamanio_pdf_origen"),ContentType=S(r,"content_type"),
                        AocrId=I(r,"aocr_id"),AocrPdfId=I(r,"aocr_pdf_id"),VersionAocr=I(r,"version_aocr"),EstadoAocr=S(r,"estado_aocr"),
                        CondicionesId=I(r,"condiciones_id"),CondicionesPdfId=I(r,"condiciones_pdf_id"),VersionCondiciones=I(r,"version_condiciones"),EstadoCondiciones=S(r,"estado_condiciones")
                    };
                }
            }
        }

        public bool ExisteFirma(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId,int inspeccionId,string tipo,int version)
        {
            using(var cmd=new NpgsqlCommand(@"SELECT EXISTS(SELECT 1 FROM public.aocr_tbfirma_documento WHERE codigo_solicitud=@s AND codigo_inspeccion=@i AND UPPER(tipo_documento)=UPPER(@t) AND version=@v AND estado_documento IN ('FIRMADO_DGAC','FIRMADO_DCAV'));",cn,tx))
            {cmd.Parameters.AddWithValue("@s",solicitudId);cmd.Parameters.AddWithValue("@i",inspeccionId);cmd.Parameters.AddWithValue("@t",tipo);cmd.Parameters.AddWithValue("@v",version);return Convert.ToBoolean(cmd.ExecuteScalar());}
        }

        public bool IdempotenciaExiste(NpgsqlConnection cn,NpgsqlTransaction tx,string clave)
        {using(var cmd=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM public.aocr_proceso_idempotencia WHERE clave=@k);",cn,tx)){cmd.Parameters.AddWithValue("@k",clave);return Convert.ToBoolean(cmd.ExecuteScalar());}}

        public int RegistrarFirma(NpgsqlConnection cn,NpgsqlTransaction tx,FirmaDocumentoInstitucionalSnapshot d,int usuarioId,string rol,string nombre,string cargo,string ruta,string hash,long bytes,string qr,string estadoFinal,string sujeto)
        {
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbfirma_documento(codigo_solicitud,codigo_inspeccion,tipo_documento,nombre_archivo,ruta_documento,hash_documento,codigo_qr,sujeto_certificado,nombre_firmante,cargo_firmante,fecha_firma,codigo_usuario,usuario_nombre,created_at,tamanio_pdf_firmado,firmado_por_rol,ruta_pdf_preliminar,ruta_pdf_firmado,hash_pdf_firmado,tamanio_pdf_preliminar,fecha_generacion,estado_documento,version,compania_id) VALUES(@s,@i,@t,@n,@r,@h,@qr,@su,@nom,@car,NOW(),@u,@nom,NOW(),@b,@rol,@origen,@r,@h,@bo,NOW(),@e,@v,@cia) RETURNING codigo_firma;",cn,tx))
            {
                cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@t",d.TipoDocumento);cmd.Parameters.AddWithValue("@n",System.IO.Path.GetFileName(ruta));cmd.Parameters.AddWithValue("@r",ruta);cmd.Parameters.AddWithValue("@h",hash);cmd.Parameters.AddWithValue("@qr",(object)qr??DBNull.Value);cmd.Parameters.AddWithValue("@su",(object)sujeto??DBNull.Value);cmd.Parameters.AddWithValue("@nom",nombre);cmd.Parameters.AddWithValue("@car",cargo);cmd.Parameters.AddWithValue("@u",usuarioId);cmd.Parameters.AddWithValue("@b",bytes);cmd.Parameters.AddWithValue("@rol",rol);cmd.Parameters.AddWithValue("@origen",d.RutaPdfOrigen);cmd.Parameters.AddWithValue("@bo",d.TamanioPdfOrigen);cmd.Parameters.AddWithValue("@e",estadoFinal);cmd.Parameters.AddWithValue("@v",d.VersionDocumento);cmd.Parameters.AddWithValue("@cia",(object)d.CompaniaId??DBNull.Value);return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public void ActualizarEstados(NpgsqlConnection cn,NpgsqlTransaction tx,FirmaDocumentoInstitucionalSnapshot d,string estadoDocumento,string estadoCentral,int usuarioId,string rol,string ip,string correlation,string clave,string auditoria)
        {
            using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_tbdocumento_generado SET estado=@ed,fecha_actualizacion=NOW() WHERE codigo_documento=@doc AND version=@vd AND ((@ed='FIRMADO_DGAC' AND estado IN ('APROBADO_DCAV','PENDIENTE_FIRMA_DGAC')) OR (@ed='FIRMADO_DCAV' AND estado IN ('APROBADO_DCAV','PENDIENTE_FIRMA_DCAV')));",cn,tx)){cmd.Parameters.AddWithValue("@ed",estadoDocumento);cmd.Parameters.AddWithValue("@doc",d.DocumentoId);cmd.Parameters.AddWithValue("@vd",d.VersionDocumento);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT:DOCUMENTO");}
            using(var cmd=new NpgsqlCommand(@"UPDATE public.aocr_proceso_estado SET estado_actual=@ec,etapa_actual='FIRMAS_INSTITUCIONALES',rol_responsable=CASE WHEN @ec='DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE' THEN NULL ELSE 'FIRMANTES_INSTITUCIONALES' END,siguiente_accion=CASE WHEN @ec='DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE' THEN 'PREPARAR_NOTIFICACION_CIERRE' ELSE 'COMPLETAR_FIRMAS_INSTITUCIONALES' END,observacion=@o,fecha_estado=NOW(),version=version+1 WHERE solicitud_id=@s AND inspeccion_id=@i AND activo=TRUE AND version=@ve;",cn,tx)){cmd.Parameters.AddWithValue("@ec",estadoCentral);cmd.Parameters.AddWithValue("@o",auditoria);cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@ve",d.VersionExpediente);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT:EXPEDIENTE");}
            using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_estado_historial(solicitud_id,inspeccion_id,informe_id,estado_anterior,estado_nuevo,etapa,accion,rol_usuario,usuario_id,rol_responsable,observacion,fecha_creacion,ip,correlation_id,clave_idempotencia,resultado) VALUES(@s,@i,@inf,@ea,@en,'FIRMAS_INSTITUCIONALES',@ac,@rol,@u,NULL,@o,NOW(),@ip,@corr,@k,'OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@i",d.InspeccionId);cmd.Parameters.AddWithValue("@inf",d.InformeId);cmd.Parameters.AddWithValue("@ea",d.EstadoCentral);cmd.Parameters.AddWithValue("@en",estadoCentral);cmd.Parameters.AddWithValue("@ac",estadoCentral=="DOCUMENTOS_FIRMADOS_INSTITUCIONALMENTE"?"AMBAS_FIRMAS_COMPLETADAS":(d.TipoDocumento=="RECONOCIMIENTO"?"AOCR_FIRMADO_DGAC":"CONDICIONES_FIRMADAS_DCAV"));cmd.Parameters.AddWithValue("@rol",rol);cmd.Parameters.AddWithValue("@u",usuarioId);cmd.Parameters.AddWithValue("@o",auditoria);cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@corr",(object)correlation??DBNull.Value);cmd.Parameters.AddWithValue("@k",clave);cmd.ExecuteNonQuery();}
        }

        public void RegistrarIdempotencia(NpgsqlConnection cn,NpgsqlTransaction tx,FirmaDocumentoInstitucionalSnapshot d,string clave,string estadoFinal,string correlation)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_proceso_idempotencia(clave,solicitud_id,fecha_registro,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,correlation_id) VALUES(@k,@s,NOW(),@a,@c,@ea,@en,'OK',@corr);",cn,tx)){cmd.Parameters.AddWithValue("@k",clave);cmd.Parameters.AddWithValue("@s",d.SolicitudId);cmd.Parameters.AddWithValue("@a",d.AocrId);cmd.Parameters.AddWithValue("@c",d.CondicionesId);cmd.Parameters.AddWithValue("@ea",d.EstadoCentral);cmd.Parameters.AddWithValue("@en",estadoFinal);cmd.Parameters.AddWithValue("@corr",(object)correlation??DBNull.Value);cmd.ExecuteNonQuery();}}

        public void RegistrarAuditoria(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId,int usuarioId,string ip,string evento,string detalle)
        {using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,fecha_hora,ip_address,descripcion,detalle,modulo,resultado) VALUES('aocr_tbfirma_documento',@s,@e,@u,NOW(),NOW(),@ip,@e,@d,'FIRMA_INSTITUCIONAL_DIFERENCIADA','OK');",cn,tx)){cmd.Parameters.AddWithValue("@s",solicitudId);cmd.Parameters.AddWithValue("@e",evento);cmd.Parameters.AddWithValue("@u",usuarioId.ToString());cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@d",detalle);cmd.ExecuteNonQuery();}}

        public void RegistrarRechazo(int solicitudId,int usuarioId,string ip,string detalle)
        {using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,fecha_hora,ip_address,descripcion,detalle,modulo,resultado) VALUES('aocr_tbfirma_documento',@s,'FIRMA_DOCUMENTO_RECHAZADA',@u,NOW(),NOW(),@ip,'FIRMA_DOCUMENTO_RECHAZADA',@d,'FIRMA_INSTITUCIONAL_DIFERENCIADA','RECHAZADO');",cn)){cn.Open();cmd.Parameters.AddWithValue("@s",solicitudId>0?(object)solicitudId:DBNull.Value);cmd.Parameters.AddWithValue("@u",usuarioId>0?usuarioId.ToString():"ANONIMO");cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@d",detalle??string.Empty);cmd.ExecuteNonQuery();}}

        public void RegistrarAlertaCompensacion(int solicitudId,int usuarioId,string ip,string detalle)
        {using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(@"INSERT INTO public.aocr_tbauditoria(tabla_afectada,registro_id,accion,usuario,fecha_accion,fecha_hora,ip_address,descripcion,detalle,modulo,resultado,mensaje_error) VALUES('aocr_tbfirma_documento',@s,'FIRMA_COMPENSACION_ERROR',@u,NOW(),NOW(),@ip,'Posible archivo huérfano de firma institucional',@d,'FIRMA_INSTITUCIONAL_DIFERENCIADA','ERROR',@d);",cn)){cn.Open();cmd.Parameters.AddWithValue("@s",solicitudId>0?(object)solicitudId:DBNull.Value);cmd.Parameters.AddWithValue("@u",usuarioId>0?usuarioId.ToString():"SISTEMA");cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@d",detalle??string.Empty);cmd.ExecuteNonQuery();}}

        public string ObtenerEstadoDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,int documentoId)
        {using(var cmd=new NpgsqlCommand("SELECT estado FROM public.aocr_tbdocumento_generado WHERE codigo_documento=@d;",cn,tx)){cmd.Parameters.AddWithValue("@d",documentoId);return Convert.ToString(cmd.ExecuteScalar());}}

        private static string S(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?null:Convert.ToString(r[n]);}
        private static int I(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt32(r[n]);}
        private static long L(NpgsqlDataReader r,string n){return r[n]==DBNull.Value?0:Convert.ToInt64(r[n]);}
    }
}
