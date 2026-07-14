using System;
using CapaDatos.DAOs;
using Npgsql;

namespace CapaNegocio.Services
{
    /// <summary>Gestiona ciclos de reevaluación sin sobrescribir evidencias anteriores.</summary>
    public sealed class ReevaluacionInspeccionService
    {
        public sealed class CicloCreado
        {
            public int CodigoInforme { get; set; }
            public int? CodigoListaVerificacion { get; set; }
            public int CicloEvaluacion { get; set; }
            public bool Existente { get; set; }
        }

        public CicloCreado Preparar(int codigoInspeccion, int codigoNoConformidad, int usuarioId, bool crearListaEae)
        {
            if (codigoInspeccion <= 0 || codigoNoConformidad <= 0 || usuarioId <= 0)
                throw new ArgumentException("Inspección, NC y usuario son obligatorios.");

            using (var cn = new NpgsqlConnection(ConexionDAO.CadenaConexion))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        const string validarSql = @"SELECT i.codigo_inspeccion
FROM public.aocr_tbinspeccion i
JOIN public.aocr_tbnoconformidad nc ON nc.codigo_no_conformidad=@nc
WHERE i.codigo_inspeccion=@inspeccion
  AND UPPER(COALESCE(i.estado,''))='EN_INSPECCION'
  AND UPPER(COALESCE(nc.estado,'')) IN ('SUBSANACION_ACEPTADA','APROBADA_COORDINADOR','FIRMADA_COORDINADOR','NOTIFICADA_RT')
  AND (nc.codigo_inspeccion=@inspeccion OR nc.codigo_inspeccion_nueva=@inspeccion);";
                        using (var validar = new NpgsqlCommand(validarSql, cn, tx))
                        {
                            validar.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            validar.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                            if (validar.ExecuteScalar() == null)
                                throw new InvalidOperationException("La inspección o la NC no están habilitadas para reevaluación.");
                        }

                        const string informeSql = @"WITH anterior AS (
 SELECT codigo_informe, ciclo_evaluacion
 FROM public.aocr_tbinforme_inspeccion
 WHERE codigo_inspeccion=@inspeccion ORDER BY version DESC LIMIT 1
), existente AS (
 SELECT codigo_informe,ciclo_evaluacion FROM public.aocr_tbinforme_inspeccion
 WHERE codigo_no_conformidad_origen=@nc AND codigo_inspeccion=@inspeccion AND es_reevaluacion LIMIT 1
), insertado AS (
 INSERT INTO public.aocr_tbinforme_inspeccion
 (codigo_inspeccion,version,titulo,resumen,estado_informe,finalizado,correo_enviado,
  codigo_informe_anterior,codigo_no_conformidad_origen,ciclo_evaluacion,es_reevaluacion,
  created_at,created_by,updated_at,updated_by)
 SELECT @inspeccion,COALESCE((SELECT MAX(version)+1 FROM public.aocr_tbinforme_inspeccion WHERE codigo_inspeccion=@inspeccion),1),
  'INFORME TÉCNICO DE REEVALUACIÓN','Nuevo ciclo posterior a subsanación de NC','BORRADOR',FALSE,FALSE,
  a.codigo_informe,@nc,a.ciclo_evaluacion+1,TRUE,NOW(),@usuario,NOW(),@usuario
 FROM anterior a WHERE NOT EXISTS(SELECT 1 FROM existente)
 RETURNING codigo_informe,ciclo_evaluacion
)
SELECT codigo_informe,ciclo_evaluacion,FALSE AS existente FROM insertado
UNION ALL SELECT codigo_informe,ciclo_evaluacion,TRUE FROM existente LIMIT 1;";
                        int informeId;
                        int ciclo;
                        bool existente;
                        using (var cmd = new NpgsqlCommand(informeSql, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                            cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                            cmd.Parameters.AddWithValue("@usuario", usuarioId);
                            using (var dr = cmd.ExecuteReader())
                            {
                                if (!dr.Read()) throw new InvalidOperationException("No existe un Informe Técnico anterior para iniciar la reevaluación.");
                                informeId = dr.GetInt32(0); ciclo = dr.GetInt32(1); existente = dr.GetBoolean(2);
                            }
                        }

                        int? listaId = null;
                        if (crearListaEae)
                        {
                            const string listaSql = @"WITH anterior AS (
 SELECT codigo_lv,ciclo_evaluacion,nombre_eae,numero_aoc_fecha_validez,
 direccion_estado_explotador,direccion_estado_reconocimiento,tipos_aeronaves,tipo_operacion
 FROM public.aocr_tblv_operacional_eae WHERE codigo_inspeccion=@inspeccion ORDER BY version DESC LIMIT 1
), ins AS (
 INSERT INTO public.aocr_tblv_operacional_eae
 (codigo_inspeccion,version,estado_lista,nombre_eae,numero_aoc_fecha_validez,direccion_estado_explotador,
 direccion_estado_reconocimiento,tipos_aeronaves,tipo_operacion,codigo_lista_anterior,
 codigo_no_conformidad_origen,ciclo_evaluacion,es_reevaluacion,created_at,created_by,updated_at,updated_by)
 SELECT @inspeccion,COALESCE((SELECT MAX(version)+1 FROM public.aocr_tblv_operacional_eae WHERE codigo_inspeccion=@inspeccion),1),
 'BORRADOR',nombre_eae,numero_aoc_fecha_validez,direccion_estado_explotador,direccion_estado_reconocimiento,
 tipos_aeronaves,tipo_operacion,codigo_lv,@nc,ciclo_evaluacion+1,TRUE,NOW(),@usuario,NOW(),@usuario
 FROM anterior WHERE NOT EXISTS(SELECT 1 FROM public.aocr_tblv_operacional_eae WHERE codigo_no_conformidad_origen=@nc AND es_reevaluacion)
 RETURNING codigo_lv)
SELECT codigo_lv FROM ins UNION ALL
SELECT codigo_lv FROM public.aocr_tblv_operacional_eae
WHERE codigo_no_conformidad_origen=@nc AND es_reevaluacion LIMIT 1;";
                            using (var cmd = new NpgsqlCommand(listaSql, cn, tx))
                            {
                                cmd.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                                cmd.Parameters.AddWithValue("@nc", codigoNoConformidad);
                                cmd.Parameters.AddWithValue("@usuario", usuarioId);
                                var value = cmd.ExecuteScalar();
                                if (value != null && value != DBNull.Value) listaId = Convert.ToInt32(value);
                            }
                        }

                        tx.Commit();
                        return new CicloCreado { CodigoInforme = informeId, CodigoListaVerificacion = listaId, CicloEvaluacion = ciclo, Existente = existente };
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }

        public int CerrarNcConInformeSatisfactorio(int codigoInspeccion, int codigoInforme, int usuarioId, string motivo)
        {
            using (var cn = new NpgsqlConnection(ConexionDAO.CadenaConexion))
            {
                cn.Open();
                const string sql = @"UPDATE public.aocr_tbnoconformidad nc
SET estado='CERRADA',codigo_informe_cierre=i.codigo_informe,fecha_cierre=NOW(),usuario_cierre=@usuario,
 observacion_cierre=@motivo,updated_at=NOW()
FROM public.aocr_tbinforme_inspeccion i
WHERE i.codigo_informe=@informe AND i.codigo_inspeccion=@inspeccion AND i.finalizado
 AND i.firmado_inspector AND UPPER(COALESCE(i.resultado,''))='SATISFACTORIO'
 AND NULLIF(i.hash_documento,'') IS NOT NULL AND nc.fecha_cierre IS NULL
 AND (nc.codigo_inspeccion=@inspeccion OR nc.codigo_inspeccion_nueva=@inspeccion)
 AND UPPER(COALESCE(nc.estado,'')) IN ('SUBSANACION_ACEPTADA','APROBADA_COORDINADOR','FIRMADA_COORDINADOR','NOTIFICADA_RT');";
                using (var cmd = new NpgsqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@informe", codigoInforme);
                    cmd.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                    cmd.Parameters.AddWithValue("@usuario", usuarioId);
                    cmd.Parameters.AddWithValue("@motivo", string.IsNullOrWhiteSpace(motivo) ? "Cierre por reevaluación satisfactoria firmada." : motivo.Trim());
                    return cmd.ExecuteNonQuery();
                }
            }
        }

        public int CrearNcNuevoCicloInsatisfactorio(int codigoInspeccion, int codigoInforme, int usuarioId)
        {
            using (var cn = new NpgsqlConnection(ConexionDAO.CadenaConexion))
            {
                cn.Open();
                using (var tx = cn.BeginTransaction())
                {
                    try
                    {
                        using (var esCiclo = new NpgsqlCommand("SELECT codigo_no_conformidad_origen FROM public.aocr_tbinforme_inspeccion WHERE codigo_informe=@informe AND codigo_inspeccion=@inspeccion;", cn, tx))
                        {
                            esCiclo.Parameters.AddWithValue("@informe", codigoInforme);
                            esCiclo.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                            var origen = esCiclo.ExecuteScalar();
                            if (origen == null || origen == DBNull.Value)
                            {
                                tx.Commit();
                                return 0;
                            }
                        }

                        const string sql = @"WITH informe AS (
 SELECT codigo_informe,codigo_no_conformidad_origen,ciclo_evaluacion,
        UPPER(COALESCE(tipo_resultado_insatisfactorio,'')) AS ruta
 FROM public.aocr_tbinforme_inspeccion
 WHERE codigo_informe=@informe AND codigo_inspeccion=@inspeccion AND finalizado AND firmado_inspector
   AND NULLIF(hash_documento,'') IS NOT NULL AND UPPER(COALESCE(resultado,''))='INSATISFACTORIO'
), base AS (
 SELECT nc.* FROM public.aocr_tbnoconformidad nc JOIN informe i
   ON nc.codigo_no_conformidad=i.codigo_no_conformidad_origen
), ins AS (
 INSERT INTO public.aocr_tbnoconformidad
 (codigo_inspeccion,codigo_informe,codigo_solicitud,tipo_ruta,estado,numero_no_conformidad,
  resumen,detalle,fundamento_tecnico,acciones_requeridas,plazo_subsanacion,requiere_nueva_inspeccion,
  version,fecha_generacion,usuario_creacion,codigo_nc_raiz,codigo_solicitud_origen,
  codigo_inspeccion_origen,codigo_informe_origen,ciclo_evaluacion,correlation_id,created_at)
 SELECT @inspeccion,i.codigo_informe,b.codigo_solicitud,
  CASE WHEN i.ruta IN ('CON_INSPECCION','SIN_INSPECCION') THEN i.ruta ELSE NULL END,
  'GENERADA',b.numero_no_conformidad,b.resumen,b.detalle,b.fundamento_tecnico,b.acciones_requeridas,
  b.plazo_subsanacion,(i.ruta='CON_INSPECCION'),b.version+1,NOW(),@usuario,
  COALESCE(b.codigo_nc_raiz,b.codigo_no_conformidad),COALESCE(b.codigo_solicitud_origen,b.codigo_solicitud),
  COALESCE(b.codigo_inspeccion_origen,b.codigo_inspeccion),COALESCE(b.codigo_informe_origen,b.codigo_informe),
  i.ciclo_evaluacion,'GATE5-NC-'||i.codigo_informe,NOW()
 FROM base b JOIN informe i ON TRUE
 WHERE i.ruta IN ('CON_INSPECCION','SIN_INSPECCION')
   AND NOT EXISTS(SELECT 1 FROM public.aocr_tbnoconformidad WHERE codigo_informe=i.codigo_informe)
 RETURNING codigo_no_conformidad)
SELECT codigo_no_conformidad FROM ins UNION ALL
SELECT codigo_no_conformidad FROM public.aocr_tbnoconformidad WHERE codigo_informe=@informe LIMIT 1;";
                        int id;
                        using (var cmd = new NpgsqlCommand(sql, cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@informe", codigoInforme);
                            cmd.Parameters.AddWithValue("@inspeccion", codigoInspeccion);
                            cmd.Parameters.AddWithValue("@usuario", usuarioId);
                            var value = cmd.ExecuteScalar();
                            if (value == null) throw new InvalidOperationException("La reevaluación insatisfactoria debe elegir CON_INSPECCION o SIN_INSPECCION y estar firmada con hash.");
                            id = Convert.ToInt32(value);
                        }
                        tx.Commit();
                        return id;
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
        }
    }
}
