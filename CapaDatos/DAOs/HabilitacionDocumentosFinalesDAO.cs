using System;
using CapaDatos.Models;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class HabilitacionDocumentosFinalesDAO
    {
        public void PrepararEsquema(NpgsqlConnection cn)
        {
            if (cn == null) throw new ArgumentNullException(nameof(cn));
            new AocrDocumentoGeneradoDAO().PrepararEsquema(cn);
            const string sql = @"
                ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS companias_seleccionadas TEXT NULL;
                CREATE TABLE IF NOT EXISTS public.aocr_proceso_idempotencia
                (clave VARCHAR(100) PRIMARY KEY, solicitud_id INTEGER NOT NULL, fecha_registro TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW());
                ALTER TABLE public.aocr_proceso_idempotencia ADD COLUMN IF NOT EXISTS aocr_id INTEGER NULL;
                ALTER TABLE public.aocr_proceso_idempotencia ADD COLUMN IF NOT EXISTS condiciones_id INTEGER NULL;
                ALTER TABLE public.aocr_proceso_idempotencia ADD COLUMN IF NOT EXISTS estado_anterior VARCHAR(100) NULL;
                ALTER TABLE public.aocr_proceso_idempotencia ADD COLUMN IF NOT EXISTS estado_nuevo VARCHAR(100) NULL;
                ALTER TABLE public.aocr_proceso_idempotencia ADD COLUMN IF NOT EXISTS resultado VARCHAR(50) NULL;
                ALTER TABLE public.aocr_proceso_idempotencia ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100) NULL;
                ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS event_key VARCHAR(200) NULL;
                ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100) NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbnotificacion_event_key
                    ON public.aocr_tbnotificacion(event_key) WHERE event_key IS NOT NULL;";
            using (var cmd = new NpgsqlCommand(sql, cn)) cmd.ExecuteNonQuery();
        }

        public HabilitacionDocumentosSnapshot CargarParaActualizar(
            NpgsqlConnection cn, NpgsqlTransaction tx, int solicitudId, int inspeccionId, int informeId)
        {
            const string sql = @"
                SELECT s.codigo_solicitud, s.estado AS estado_solicitud,
                       (s.deleted_at IS NULL) AS solicitud_activa,
                       COALESCE(NULLIF(TRIM(s.companias_seleccionadas),''), '') AS codigo_compania,
                       i.codigo_inspeccion, COALESCE(i.codigo_inspector, s.codigo_tecnico, 0) AS inspector_id,
                       i.estado AS estado_inspeccion, i.resultado AS resultado_inspeccion,
                       inf.codigo_informe, inf.version AS version_informe, inf.estado_informe,
                       inf.resultado AS resultado_informe, inf.finalizado AS informe_finalizado,
                       inf.firmado_inspector AS informe_firmado, inf.ruta_documento_firmado AS ruta_informe_firmado,
                       inf.hash_documento AS hash_informe,
                       NOT EXISTS (SELECT 1 FROM public.aocr_tbinforme_inspeccion nx
                                   WHERE nx.codigo_inspeccion=inf.codigo_inspeccion
                                     AND (nx.version>inf.version OR (nx.version=inf.version AND nx.codigo_informe>inf.codigo_informe))) AS informe_vigente,
                       COALESCE(lv.codigo_lv,0) AS lista_id, COALESCE(lv.finalizado,FALSE) AS lista_finalizada,
                       COALESCE(lv.firmado_tecnico,FALSE) AS lista_firmada,
                       COALESCE(lv.ruta_documento_firmado,'') AS ruta_lista_firmada,
                       COALESCE(lv.hash_documento,'') AS hash_lista,
                       pe.estado_actual AS estado_central, pe.version AS version_registro
                FROM public.aocr_tbsolicitud s
                JOIN public.aocr_tbinspeccion i ON i.codigo_solicitud=s.codigo_solicitud AND i.codigo_inspeccion=@inspeccion
                JOIN public.aocr_tbinforme_inspeccion inf ON inf.codigo_inspeccion=i.codigo_inspeccion AND inf.codigo_informe=@informe
                JOIN public.aocr_proceso_estado pe ON pe.solicitud_id=s.codigo_solicitud AND pe.activo=TRUE
                LEFT JOIN LATERAL (
                    SELECT l.* FROM public.aocr_tblv_operacional_eae l
                    WHERE l.codigo_inspeccion=i.codigo_inspeccion
                    ORDER BY l.version DESC, l.codigo_lv DESC LIMIT 1
                ) lv ON TRUE
                WHERE s.codigo_solicitud=@solicitud
                FOR UPDATE OF s, i, inf, pe;";
            using (var cmd = new NpgsqlCommand(sql, cn, tx))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@informe", informeId);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    return new HabilitacionDocumentosSnapshot
                    {
                        SolicitudId=Convert.ToInt32(rd["codigo_solicitud"]), EstadoSolicitud=Convert.ToString(rd["estado_solicitud"]),
                        SolicitudActiva=Convert.ToBoolean(rd["solicitud_activa"]), CodigoCompania=Convert.ToString(rd["codigo_compania"]),
                        InspeccionId=Convert.ToInt32(rd["codigo_inspeccion"]), InspectorId=Convert.ToInt32(rd["inspector_id"]),
                        EstadoInspeccion=Convert.ToString(rd["estado_inspeccion"]), ResultadoInspeccion=Convert.ToString(rd["resultado_inspeccion"]),
                        InformeId=Convert.ToInt32(rd["codigo_informe"]), VersionInforme=Convert.ToInt32(rd["version_informe"]),
                        EstadoInforme=Convert.ToString(rd["estado_informe"]), ResultadoInforme=Convert.ToString(rd["resultado_informe"]),
                        InformeFinalizado=Convert.ToBoolean(rd["informe_finalizado"]), InformeFirmado=Convert.ToBoolean(rd["informe_firmado"]),
                        RutaInformeFirmado=Convert.ToString(rd["ruta_informe_firmado"]), HashInforme=Convert.ToString(rd["hash_informe"]),
                        InformeVigente=Convert.ToBoolean(rd["informe_vigente"]), ListaId=Convert.ToInt32(rd["lista_id"]),
                        ListaFinalizada=Convert.ToBoolean(rd["lista_finalizada"]), ListaFirmada=Convert.ToBoolean(rd["lista_firmada"]),
                        RutaListaFirmada=Convert.ToString(rd["ruta_lista_firmada"]), HashLista=Convert.ToString(rd["hash_lista"]),
                        EstadoCentral=Convert.ToString(rd["estado_central"]), VersionRegistro=Convert.ToInt64(rd["version_registro"])
                    };
                }
            }
        }

        public HabilitacionIdempotenciaRecord ObtenerIdempotencia(NpgsqlConnection cn, NpgsqlTransaction tx, string clave)
        {
            const string sql=@"SELECT clave, solicitud_id, COALESCE(aocr_id,0) aocr_id, COALESCE(condiciones_id,0) condiciones_id,
                                      estado_anterior, estado_nuevo, resultado
                               FROM public.aocr_proceso_idempotencia WHERE clave=@clave FOR UPDATE;";
            using(var cmd=new NpgsqlCommand(sql,cn,tx))
            {
                cmd.Parameters.AddWithValue("@clave",clave);
                using(var rd=cmd.ExecuteReader())
                {
                    if(!rd.Read()) return null;
                    return new HabilitacionIdempotenciaRecord { Clave=Convert.ToString(rd["clave"]), SolicitudId=Convert.ToInt32(rd["solicitud_id"]),
                        AocrId=Convert.ToInt32(rd["aocr_id"]), CondicionesId=Convert.ToInt32(rd["condiciones_id"]),
                        EstadoAnterior=Convert.ToString(rd["estado_anterior"]), EstadoNuevo=Convert.ToString(rd["estado_nuevo"]), Resultado=Convert.ToString(rd["resultado"]) };
                }
            }
        }

        public void RegistrarIdempotencia(NpgsqlConnection cn,NpgsqlTransaction tx,HabilitacionIdempotenciaRecord record,string correlationId)
        {
            const string sql=@"INSERT INTO public.aocr_proceso_idempotencia
                (clave,solicitud_id,fecha_registro,aocr_id,condiciones_id,estado_anterior,estado_nuevo,resultado,correlation_id)
                VALUES(@clave,@solicitud,NOW(),@aocr,@condiciones,@anterior,@nuevo,@resultado,@correlation);";
            using(var cmd=new NpgsqlCommand(sql,cn,tx))
            {
                cmd.Parameters.AddWithValue("@clave",record.Clave); cmd.Parameters.AddWithValue("@solicitud",record.SolicitudId);
                cmd.Parameters.AddWithValue("@aocr",record.AocrId); cmd.Parameters.AddWithValue("@condiciones",record.CondicionesId);
                cmd.Parameters.AddWithValue("@anterior",(object)record.EstadoAnterior??DBNull.Value); cmd.Parameters.AddWithValue("@nuevo",(object)record.EstadoNuevo??DBNull.Value);
                cmd.Parameters.AddWithValue("@resultado",(object)record.Resultado??DBNull.Value); cmd.Parameters.AddWithValue("@correlation",(object)correlationId??DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        public void MarcarInformeAprobado(NpgsqlConnection cn,NpgsqlTransaction tx,int informeId,int usuarioId)
        {
            const string sql=@"UPDATE public.aocr_tbinforme_inspeccion SET estado_informe='INFORME_TECNICO_APROBADO_DCAV',
                updated_at=NOW(), updated_by=@usuario WHERE codigo_informe=@informe;";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@usuario",usuarioId);cmd.Parameters.AddWithValue("@informe",informeId);if(cmd.ExecuteNonQuery()!=1)throw new InvalidOperationException("No se actualizo el Informe Tecnico.");}
        }

        public void RegistrarAuditoria(NpgsqlConnection cn,NpgsqlTransaction tx,string usuario,string estadoAnterior,string datosNuevos)
        {
            const string sql=@"INSERT INTO public.aocr_tbauditoria(entidad,accion,usuario,fecha,datos_previos,datos_nuevos)
                VALUES('SOLICITUD_AOCR','DOCUMENTOS_HABILITADOS_INSPECTOR',@usuario,NOW(),@anterior,@nuevos);";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@usuario",usuario);cmd.Parameters.AddWithValue("@anterior",(object)estadoAnterior??DBNull.Value);cmd.Parameters.AddWithValue("@nuevos",datosNuevos);cmd.ExecuteNonQuery();}
        }

        public void CrearNotificacionInspector(NpgsqlConnection cn,NpgsqlTransaction tx,int inspectorId,int solicitudId,string claveIdempotencia,string correlationId)
        {
            const string sql=@"INSERT INTO public.aocr_tbnotificacion(codigousuario,titulo,mensaje,tipo,url,fechacreacion,leida,event_key,correlation_id)
                VALUES(@usuario,'Documentos AOCR habilitados',@mensaje,'DOCUMENTOS_HABILITADOS_INSPECTOR',@url,NOW(),FALSE,@event_key,@correlation)
                ON CONFLICT (event_key) WHERE event_key IS NOT NULL DO NOTHING;";
            using(var cmd=new NpgsqlCommand(sql,cn,tx)){cmd.Parameters.AddWithValue("@usuario",inspectorId);cmd.Parameters.AddWithValue("@mensaje","El Informe Tecnico fue aprobado por DCAV. Se encuentran habilitados el AOCR y las Condiciones y Limitaciones para su revision.");cmd.Parameters.AddWithValue("@url","/aocr/InspectorDocumentosFinales/Detalle?solicitudId="+solicitudId);cmd.Parameters.AddWithValue("@event_key",claveIdempotencia+":NOTIFICACION:"+inspectorId);cmd.Parameters.AddWithValue("@correlation",(object)correlationId??DBNull.Value);cmd.ExecuteNonQuery();}
        }
    }
}
