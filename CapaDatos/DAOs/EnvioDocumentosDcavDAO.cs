using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using CapaDatos.Models;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class EnvioDocumentosDcavDAO
    {
        private readonly string _connectionString;

        public EnvioDocumentosDcavDAO()
        {
            var configured = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _connectionString = configured != null ? configured.ConnectionString : ConexionDAO.CadenaConexion;
        }

        public NpgsqlConnection CrearConexion() { return new NpgsqlConnection(_connectionString); }

        public void BloquearOperacion(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, int inspeccionId)
        {
            using (var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@key));", cn, tx))
            {
                cmd.Parameters.AddWithValue("@key", "ENVIO_DOCUMENTOS_DCAV:" + solicitudId + ":" + inspeccionId);
                cmd.ExecuteNonQuery();
            }
        }

        public EnvioDocumentosDcavSnapshot CargarParaActualizar(NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, int inspeccionId)
        {
            const string sql = @"SELECT to_jsonb(s) solicitud_json,(s.deleted_at IS NULL) solicitud_activa,
                       i.codigo_inspeccion,COALESCE(i.codigo_inspector,s.codigo_tecnico,0) inspector_id,
                       pe.estado_actual,pe.version version_expediente,
                       COALESCE(inf.codigo_informe,0) informe_id,COALESCE(inf.estado_informe,'') estado_informe,
                       COALESCE(inf.resultado,'') resultado_informe,COALESCE(inf.finalizado,FALSE) informe_finalizado,
                       (COALESCE(inf.firmado_inspector,FALSE) AND NULLIF(TRIM(inf.ruta_documento_firmado),'') IS NOT NULL
                         AND NULLIF(TRIM(inf.hash_documento),'') IS NOT NULL) informe_firmado,
                       (COALESCE(lv.finalizado,FALSE) AND COALESCE(lv.firmado_tecnico,FALSE)
                         AND NULLIF(TRIM(lv.ruta_documento_firmado),'') IS NOT NULL AND NULLIF(TRIM(lv.hash_documento),'') IS NOT NULL) lista_firmada,
                       cert.cert_json,
                       (SELECT COUNT(*) FROM public.aocr_tbaeronave_solicitud av
                         WHERE av.codigosolicitud=s.codigo_solicitud AND NULLIF(TRIM(av.matricula),'') IS NOT NULL
                           AND (NULLIF(TRIM(av.modelo),'') IS NOT NULL OR NULLIF(TRIM(av.marca),'') IS NOT NULL)) aeronaves_completas
                FROM public.aocr_tbsolicitud s
                JOIN public.aocr_tbinspeccion i ON i.codigo_solicitud=s.codigo_solicitud AND i.codigo_inspeccion=@inspeccion
                JOIN public.aocr_proceso_estado pe ON pe.solicitud_id=s.codigo_solicitud AND pe.activo=TRUE
                LEFT JOIN LATERAL(SELECT x.* FROM public.aocr_tbinforme_inspeccion x
                    WHERE x.codigo_inspeccion=i.codigo_inspeccion ORDER BY x.version DESC,x.codigo_informe DESC LIMIT 1) inf ON TRUE
                LEFT JOIN LATERAL(SELECT x.* FROM public.aocr_tblv_operacional_eae x
                    WHERE x.codigo_inspeccion=i.codigo_inspeccion ORDER BY x.version DESC,x.codigo_lv DESC LIMIT 1) lv ON TRUE
                LEFT JOIN LATERAL(SELECT to_jsonb(x) cert_json FROM public.aocr_tbcertificado x
                    WHERE x.codigo_solicitud=s.codigo_solicitud ORDER BY x.codigo_certificado DESC LIMIT 1) cert ON TRUE
                WHERE s.codigo_solicitud=@solicitud
                FOR UPDATE OF s,i,pe;";
            EnvioDocumentosDcavSnapshot result;
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    var solicitudJson = Convert.ToString(rd["solicitud_json"]);
                    var certJson = rd["cert_json"] == DBNull.Value ? null : Convert.ToString(rd["cert_json"]);
                    result = new EnvioDocumentosDcavSnapshot
                    {
                        SolicitudId=solicitudId,SolicitudActiva=Convert.ToBoolean(rd["solicitud_activa"]),
                        EstadoSolicitud=JsonValue(solicitudJson,"estado"),CodigoCompania=PrimerNoVacio(JsonValue(solicitudJson,"companias_seleccionadas"),JsonValue(solicitudJson,"codigo_oaci")),
                        InspeccionId=Convert.ToInt32(rd["codigo_inspeccion"]),InspectorId=Convert.ToInt32(rd["inspector_id"]),
                        EstadoCentral=Convert.ToString(rd["estado_actual"]),VersionExpediente=Convert.ToInt64(rd["version_expediente"]),
                        InformeId=Convert.ToInt32(rd["informe_id"]),EstadoInforme=Convert.ToString(rd["estado_informe"]),ResultadoInforme=Convert.ToString(rd["resultado_informe"]),
                        InformeFinalizado=Convert.ToBoolean(rd["informe_finalizado"]),InformeFirmado=Convert.ToBoolean(rd["informe_firmado"]),ListaFirmada=Convert.ToBoolean(rd["lista_firmada"]),
                        NumeroAoc=JsonValue(solicitudJson,"numero_aoc"),Pais=JsonValue(solicitudJson,"pais"),
                        Operador=PrimerNoVacio(JsonValue(solicitudJson,"razon_social"),JsonValue(solicitudJson,"nombre_operador"),JsonValue(solicitudJson,"nombre_comercial")),
                        PuntoContacto=PrimerNoVacio(JsonValue(solicitudJson,"representante_legal"),JsonValue(solicitudJson,"email")),
                        RepresentanteTecnico=PrimerNoVacio(JsonValue(solicitudJson,"tecnico_responsable_nombre"),JsonValue(solicitudJson,"representante_legal")),
                        Aeropuertos=PrimerNoVacio(JsonValue(solicitudJson,"aeropuertos_ecuador"),JsonValue(solicitudJson,"aeropuertos_ecuador_otros")),
                        Condiciones=JsonValue(solicitudJson,"aprobaciones_especiales"),Limitaciones=JsonValue(solicitudJson,"aprobaciones_especiales_otros"),
                        AeronavesCompletas=Convert.ToInt32(rd["aeronaves_completas"]),
                        FechaVencimiento=JsonDate(certJson,"fecha_vencimiento","fechavencimiento")
                    };
                }
            }
            CargarDocumentosParaActualizar(cn, tx, result);
            return result;
        }

        public EnvioDocumentosDcavIdempotencia ObtenerIdempotencia(NpgsqlConnection cn, NpgsqlTransaction tx, string clave)
        {
            const string sql = @"SELECT clave,solicitud_id,COALESCE(aocr_id,0) aocr_id,COALESCE(condiciones_id,0) condiciones_id,
                    estado_anterior,estado_nuevo,resultado,fecha_registro
                FROM public.aocr_proceso_idempotencia WHERE clave=@clave FOR UPDATE;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@clave", clave);
                using (var rd = cmd.ExecuteReader()) return rd.Read() ? new EnvioDocumentosDcavIdempotencia
                {
                    Clave=Convert.ToString(rd["clave"]),SolicitudId=Convert.ToInt32(rd["solicitud_id"]),AocrId=Convert.ToInt32(rd["aocr_id"]),
                    CondicionesId=Convert.ToInt32(rd["condiciones_id"]),EstadoAnterior=Convert.ToString(rd["estado_anterior"]),
                    EstadoNuevo=Convert.ToString(rd["estado_nuevo"]),Resultado=Convert.ToString(rd["resultado"]),Fecha=Convert.ToDateTime(rd["fecha_registro"])
                } : null;
            }
        }

        public void MarcarDocumentosEnviados(NpgsqlConnection cn, NpgsqlTransaction tx, EnvioDocumentosDcavSnapshot s, int usuarioId)
        {
            const string sql = @"UPDATE public.aocr_tbdocumento_generado
                SET estado='ENVIADO_DCAV',codigo_usuario=@usuario,usuario_nombre='Inspector',fecha_actualizacion=NOW()
                WHERE codigo_documento=@id AND codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                  AND codigo_inspector=@usuario AND version=@version AND vigente=TRUE AND eliminado=FALSE
                  AND COALESCE(hash_pdf_firmado,'')='' AND estado IN ('GENERADO','APROBADO_DCAV');";
            ActualizarDocumento(cn,tx,sql,s.AocrId,s.SolicitudId,s.InspeccionId,usuarioId,s.VersionAocr,"AOCR");
            ActualizarDocumento(cn,tx,sql,s.CondicionesId,s.SolicitudId,s.InspeccionId,usuarioId,s.VersionCondiciones,"Condiciones");
        }

        public int CambiarEstadoCentral(NpgsqlConnection cn,NpgsqlTransaction tx,EnvioDocumentosDcavSnapshot s,string estadoNuevo,int usuarioId)
        {
            const string desactivar=@"UPDATE public.aocr_proceso_estado SET activo=FALSE
                WHERE solicitud_id=@solicitud AND activo=TRUE AND version=@version;";
            using(var cmd=new NpgsqlCommand(desactivar,cn,tx)){cmd.Parameters.AddWithValue("@solicitud",s.SolicitudId);cmd.Parameters.AddWithValue("@version",s.VersionExpediente);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("CONCURRENCY_CONFLICT");}
            const string insertar=@"INSERT INTO public.aocr_proceso_estado
                (solicitud_id,inspeccion_id,informe_id,estado_actual,etapa_actual,rol_responsable,usuario_responsable_id,siguiente_accion,observacion,fecha_estado,activo,version)
                VALUES(@solicitud,@inspeccion,@informe,@estado,'REVISION_DOCUMENTOS_DCAV','DirectorCertificacionesDcav',NULL,'REVISAR_DOCUMENTOS_DCAV',@observacion,NOW(),TRUE,@version)
                RETURNING id;";
            using(var cmd=new NpgsqlCommand(insertar,cn,tx)){cmd.Parameters.AddWithValue("@solicitud",s.SolicitudId);cmd.Parameters.AddWithValue("@inspeccion",s.InspeccionId);cmd.Parameters.AddWithValue("@informe",s.InformeId>0?(object)s.InformeId:DBNull.Value);cmd.Parameters.AddWithValue("@estado",estadoNuevo);cmd.Parameters.AddWithValue("@observacion","AOCR y Condiciones enviados conjuntamente por Inspector "+usuarioId+".");cmd.Parameters.AddWithValue("@version",s.VersionExpediente+1);return Convert.ToInt32(cmd.ExecuteScalar());}
        }

        public void RegistrarHistorial(NpgsqlConnection cn,NpgsqlTransaction tx,EnvioDocumentosDcavSnapshot s,string estadoNuevo,int usuarioId,string rol,string clave,string ip,string correlation,string detalle)
        {
            const string sql=@"INSERT INTO public.aocr_proceso_estado_historial
                (solicitud_id,inspeccion_id,informe_id,estado_anterior,estado_nuevo,etapa,accion,rol_usuario,usuario_id,rol_responsable,observacion,fecha_creacion,ip,correlation_id,clave_idempotencia,resultado)
                VALUES(@solicitud,@inspeccion,@informe,@anterior,@nuevo,'REVISION_DOCUMENTOS_DCAV','ENVIAR_DOCUMENTOS_DCAV',@rol,@usuario,'DirectorCertificacionesDcav',@detalle,NOW(),@ip,@correlation,@clave,'OK');";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){BindTrazabilidad(cmd,s,estadoNuevo,usuarioId,rol,clave,ip,correlation,detalle);cmd.ExecuteNonQuery();}
        }

        public void RegistrarAuditoria(NpgsqlConnection cn,NpgsqlTransaction tx,EnvioDocumentosDcavSnapshot s,string estadoNuevo,int usuarioId,string accion,string detalle)
        {
            const string sql=@"INSERT INTO public.aocr_tbauditoria(entidad,accion,usuario,fecha,datos_previos,datos_nuevos)
                VALUES('DOCUMENTOS_FINALES',@accion,@usuario,NOW(),@anterior,@detalle);";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@accion",accion);cmd.Parameters.AddWithValue("@usuario",usuarioId.ToString());cmd.Parameters.AddWithValue("@anterior",s.EstadoCentral);cmd.Parameters.AddWithValue("@detalle",detalle+";EstadoNuevo="+estadoNuevo);cmd.ExecuteNonQuery();}
        }

        public void RegistrarRechazo(int solicitudId,int usuarioId,string detalle)
        {
            const string sql=@"INSERT INTO public.aocr_tbauditoria(entidad,accion,usuario,fecha,datos_previos,datos_nuevos)
                VALUES('DOCUMENTOS_FINALES','ENVIO_DOCUMENTOS_DCAV_RECHAZADO',@usuario,NOW(),NULL,@detalle);";
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(sql,cn)){cn.Open();cmd.Parameters.AddWithValue("@usuario",usuarioId.ToString());cmd.Parameters.AddWithValue("@detalle","SolicitudId="+solicitudId+";"+(detalle??string.Empty));cmd.ExecuteNonQuery();}
        }

        public int CrearNotificacionesDcav(NpgsqlConnection cn,NpgsqlTransaction tx,int solicitudId,string clave,string correlationId)
        {
            const string sql=@"INSERT INTO public.aocr_tbnotificacion
                (codigousuario,titulo,mensaje,tipo,url,fechacreacion,leida,event_key,correlation_id)
                SELECT DISTINCT u.idusuario,'AOCR y Condiciones pendientes de revision DCAV',@mensaje,
                    'DOCUMENTOS_ENVIADOS_DCAV',@url,NOW(),FALSE,@clave||':NOTIFICACION:'||u.idusuario,@correlation
                FROM public.usuario u
                JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario AND COALESCE(ur.activo,TRUE)=TRUE
                JOIN public.rol r ON r.codigorol=ur.codigorol AND COALESCE(r.activo,TRUE)=TRUE
                WHERE UPPER(TRIM(r.descripcion)) IN ('DIRECTOR_CERTIFICACIONES_DCAV','DIRECTORCERTIFICACIONESDCAV','DIRECTOR DE CERTIFICACIONES DCAV','DCAV')
                  AND UPPER(COALESCE(u.estadoactividad,'ACTIVO')) NOT IN ('INACTIVO','BLOQUEADO','ELIMINADO')
                ON CONFLICT (event_key) WHERE event_key IS NOT NULL DO NOTHING;";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@mensaje","El Inspector ha finalizado la revision del AOCR y de las Condiciones y Limitaciones. Los documentos se encuentran disponibles para revision DCAV.");cmd.Parameters.AddWithValue("@url","/aocr/AocrDcav/Detalle?solicitudId="+solicitudId);cmd.Parameters.AddWithValue("@clave",clave);cmd.Parameters.AddWithValue("@correlation",(object)correlationId??DBNull.Value);return cmd.ExecuteNonQuery();}
        }

        public IList<string> ObtenerCorreosDcav()
        {
            var result=new List<string>();
            const string sql=@"SELECT DISTINCT LOWER(TRIM(u.correo)) correo FROM public.usuario u
                JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario AND COALESCE(ur.activo,TRUE)=TRUE
                JOIN public.rol r ON r.codigorol=ur.codigorol AND COALESCE(r.activo,TRUE)=TRUE
                WHERE UPPER(TRIM(r.descripcion)) IN ('DIRECTOR_CERTIFICACIONES_DCAV','DIRECTORCERTIFICACIONESDCAV','DIRECTOR DE CERTIFICACIONES DCAV','DCAV')
                  AND UPPER(COALESCE(u.estadoactividad,'ACTIVO')) NOT IN ('INACTIVO','BLOQUEADO','ELIMINADO') AND NULLIF(TRIM(u.correo),'') IS NOT NULL;";
            using(var cn=CrearConexion())using(var cmd=new NpgsqlCommand(sql,cn)){cn.Open();using(var rd=cmd.ExecuteReader())while(rd.Read())result.Add(Convert.ToString(rd["correo"]));}
            return result;
        }

        public void RegistrarIdempotencia(NpgsqlConnection cn,NpgsqlTransaction tx,string clave,EnvioDocumentosDcavSnapshot s,string estadoNuevo,string correlation)
        {
            const string sql=@"INSERT INTO public.aocr_proceso_idempotencia
                (clave,solicitud_id,fecha_registro,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,correlation_id)
                VALUES(@clave,@solicitud,NOW(),@aocr,@condiciones,@anterior,@nuevo,'OK',@correlation);";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@clave",clave);cmd.Parameters.AddWithValue("@solicitud",s.SolicitudId);cmd.Parameters.AddWithValue("@aocr",s.AocrId);cmd.Parameters.AddWithValue("@condiciones",s.CondicionesId);cmd.Parameters.AddWithValue("@anterior",s.EstadoCentral);cmd.Parameters.AddWithValue("@nuevo",estadoNuevo);cmd.Parameters.AddWithValue("@correlation",(object)correlation??DBNull.Value);cmd.ExecuteNonQuery();}
        }

        private static void CargarDocumentosParaActualizar(NpgsqlConnection cn,NpgsqlTransaction tx,EnvioDocumentosDcavSnapshot s)
        {
            const string sql=@"SELECT codigo_documento,tipo_documento,version,estado,codigo_compania,COALESCE(codigo_inspector,0) inspector,
                    vigente,eliminado,(COALESCE(hash_pdf_firmado,'')<>'' OR EXISTS(SELECT 1 FROM public.aocr_tbfirma_documento f
                      WHERE f.codigo_solicitud=d.codigo_solicitud AND f.codigo_inspeccion=d.codigo_inspeccion
                        AND (CASE WHEN UPPER(TRIM(f.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END)
                           =(CASE WHEN UPPER(TRIM(d.tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END))) firmado
                FROM public.aocr_tbdocumento_generado d WHERE codigo_solicitud=@solicitud AND codigo_inspeccion=@inspeccion
                  AND vigente=TRUE AND eliminado=FALSE AND UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO','CONDICIONES','CONDICIONES_LIMITACIONES')
                ORDER BY version DESC,codigo_documento DESC FOR UPDATE;";
            var seenAocr=false;var seenCond=false;
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@solicitud",s.SolicitudId);cmd.Parameters.AddWithValue("@inspeccion",s.InspeccionId);using(var rd=cmd.ExecuteReader())while(rd.Read())
            {
                var aocr=new[]{"AOCR","RECONOCIMIENTO"}.Contains(Convert.ToString(rd["tipo_documento"]).Trim().ToUpperInvariant());
                if(aocr&&seenAocr)throw new InvalidOperationException("Existen multiples AOCR vigentes.");if(!aocr&&seenCond)throw new InvalidOperationException("Existen multiples Condiciones vigentes.");
                if(aocr){seenAocr=true;s.AocrId=Convert.ToInt32(rd["codigo_documento"]);s.VersionAocr=Convert.ToInt32(rd["version"]);s.EstadoAocr=Convert.ToString(rd["estado"]);s.CompaniaAocr=Convert.ToString(rd["codigo_compania"]);s.InspectorAocr=Convert.ToInt32(rd["inspector"]);s.AocrVigente=Convert.ToBoolean(rd["vigente"]);s.AocrEliminado=Convert.ToBoolean(rd["eliminado"]);s.AocrFirmado=Convert.ToBoolean(rd["firmado"]);}
                else{seenCond=true;s.CondicionesId=Convert.ToInt32(rd["codigo_documento"]);s.VersionCondiciones=Convert.ToInt32(rd["version"]);s.EstadoCondiciones=Convert.ToString(rd["estado"]);s.CompaniaCondiciones=Convert.ToString(rd["codigo_compania"]);s.InspectorCondiciones=Convert.ToInt32(rd["inspector"]);s.CondicionesVigente=Convert.ToBoolean(rd["vigente"]);s.CondicionesEliminado=Convert.ToBoolean(rd["eliminado"]);s.CondicionesFirmadas=Convert.ToBoolean(rd["firmado"]);}
            }}
        }

        private static void ActualizarDocumento(NpgsqlConnection cn,NpgsqlTransaction tx,string sql,int id,int solicitud,int inspeccion,int usuario,int version,string nombre)
        {using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@id",id);cmd.Parameters.AddWithValue("@solicitud",solicitud);cmd.Parameters.AddWithValue("@inspeccion",inspeccion);cmd.Parameters.AddWithValue("@usuario",usuario);cmd.Parameters.AddWithValue("@version",version);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("No se pudo bloquear y enviar "+nombre+".");}}
        private static void BindTrazabilidad(NpgsqlCommand cmd,EnvioDocumentosDcavSnapshot s,string nuevo,int usuario,string rol,string clave,string ip,string correlation,string detalle)
        {cmd.Parameters.AddWithValue("@solicitud",s.SolicitudId);cmd.Parameters.AddWithValue("@inspeccion",s.InspeccionId);cmd.Parameters.AddWithValue("@informe",s.InformeId>0?(object)s.InformeId:DBNull.Value);cmd.Parameters.AddWithValue("@anterior",s.EstadoCentral);cmd.Parameters.AddWithValue("@nuevo",nuevo);cmd.Parameters.AddWithValue("@rol",rol??string.Empty);cmd.Parameters.AddWithValue("@usuario",usuario);cmd.Parameters.AddWithValue("@detalle",detalle);cmd.Parameters.AddWithValue("@ip",(object)ip??DBNull.Value);cmd.Parameters.AddWithValue("@correlation",(object)correlation??DBNull.Value);cmd.Parameters.AddWithValue("@clave",clave);}
        private static string PrimerNoVacio(params string[] values){foreach(var value in values)if(!string.IsNullOrWhiteSpace(value))return value.Trim();return string.Empty;}
        private static string JsonValue(string json,string key){if(string.IsNullOrWhiteSpace(json)||string.IsNullOrWhiteSpace(key))return string.Empty;try{var obj=JObject.Parse(json);var token=obj.GetValue(key,StringComparison.OrdinalIgnoreCase);return token==null||token.Type==JTokenType.Null?string.Empty:Convert.ToString(token).Trim();}catch{return string.Empty;}}
        private static DateTime? JsonDate(string json,params string[] keys){foreach(var key in keys){DateTime value;if(DateTime.TryParse(JsonValue(json,key),out value))return value;}return null;}
    }
}
