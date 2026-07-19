using System;
using System.Collections.Generic;
using System.Configuration;
using Npgsql;

namespace CapaDatos.DAOs
{
    public sealed class AocrProcesoEstadoDAO
    {
        private readonly string _cs;

        public AocrProcesoEstadoDAO()
        {
            var configuracion = ConfigurationManager.ConnectionStrings["AOCRConnection"];
            _cs = configuracion != null && !string.IsNullOrWhiteSpace(configuracion.ConnectionString)
                ? configuracion.ConnectionString
                : ConexionDAO.CadenaConexion;
        }

        public IList<int> ListarInspeccionesActivas(string estado)
        {
            if (string.IsNullOrWhiteSpace(estado))
            {
                throw new ArgumentException("El estado central es obligatorio.", "estado");
            }

            var result = new List<int>();
            const string sql = @"SELECT DISTINCT pe.inspeccion_id
FROM public.aocr_proceso_estado pe
JOIN public.aocr_tbsolicitud s
  ON s.codigo_solicitud=pe.solicitud_id AND s.deleted_at IS NULL
WHERE pe.activo=TRUE
  AND pe.estado_actual=@estado
  AND pe.inspeccion_id IS NOT NULL
ORDER BY pe.inspeccion_id;";

            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@estado", estado);
                cn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        result.Add(Convert.ToInt32(rd[0]));
                    }
                }
            }

            return result;
        }

        public void CambiarEstado(
            int solicitudId,
            int inspeccionId,
            string estado,
            string etapa,
            string rolResponsable,
            int usuarioId,
            string observacion)
        {
            if (solicitudId <= 0 || inspeccionId <= 0 || usuarioId <= 0)
            {
                throw new ArgumentOutOfRangeException("El contexto de transición DCAV es inválido.");
            }

            if (string.IsNullOrWhiteSpace(estado) || string.IsNullOrWhiteSpace(rolResponsable))
            {
                throw new ArgumentException("Estado y rol responsable son obligatorios.");
            }

            const string sql = @"
WITH guard AS (
    SELECT pg_advisory_xact_lock(@solicitud::bigint)
), actuales AS (
    SELECT pe.id,pe.estado_actual
    FROM public.aocr_proceso_estado pe
    CROSS JOIN guard
    WHERE pe.solicitud_id=@solicitud AND pe.activo=TRUE
    FOR UPDATE
), mismo AS (
    UPDATE public.aocr_proceso_estado pe
       SET inspeccion_id=@inspeccion,
           etapa_actual=@etapa,
           rol_responsable=@rol,
           observacion=COALESCE(NULLIF(@observacion,''),pe.observacion),
           updated_at=NOW(),
           updated_by=@usuario
     WHERE pe.id IN (SELECT id FROM actuales WHERE estado_actual=@estado)
     RETURNING pe.id
), cerrados AS (
    UPDATE public.aocr_proceso_estado pe
       SET activo=FALSE,updated_at=NOW(),updated_by=@usuario
     WHERE pe.id IN (SELECT id FROM actuales WHERE estado_actual<>@estado)
     RETURNING pe.id
)
INSERT INTO public.aocr_proceso_estado
    (solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,observacion,activo,version,created_at,created_by,updated_at,updated_by)
SELECT @solicitud,@inspeccion,@estado,@etapa,@rol,@observacion,TRUE,1,NOW(),@usuario,NOW(),@usuario
WHERE NOT EXISTS(SELECT 1 FROM mismo)
  AND (EXISTS(SELECT 1 FROM cerrados) OR NOT EXISTS(SELECT 1 FROM actuales));";

            // La sentencia participa en el TransactionScope exterior sin abrir una
            // transacción Npgsql anidada. El lock y la actualización del mismo estado
            // hacen idempotentes los reintentos y los dobles clics.
            using (var cn = new NpgsqlConnection(_cs))
            using (var cmd = new NpgsqlCommand(sql, cn))
            {
                cmd.Parameters.AddWithValue("@solicitud", solicitudId);
                cmd.Parameters.AddWithValue("@inspeccion", inspeccionId);
                cmd.Parameters.AddWithValue("@estado", estado);
                cmd.Parameters.AddWithValue("@etapa", (object)etapa ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rol", rolResponsable);
                cmd.Parameters.AddWithValue("@observacion", (object)observacion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuario", usuarioId);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
