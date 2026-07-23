using System;
using System.Collections.Generic;
using System.Configuration;
using CapaDatos.Models;
using Npgsql;
using NpgsqlTypes;

namespace CapaDatos.DAOs
{
    public sealed class RevisionDocumentalCoordinadorDAO
    {
        private static readonly object SchemaLock = new object();
        private static bool _schemaReady;

        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["AOCRConnection"].ConnectionString; }
        }

        public RevisionDocumentalCoordinadorRegistro ObtenerPorSolicitud(int solicitudId)
        {
            if (solicitudId <= 0) return null;
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                EnsureSchema(cn);
                using (var cmd = new NpgsqlCommand(SelectBase + " WHERE solicitud_id=@solicitud_id AND activo=TRUE LIMIT 1;", cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    using (var rd = cmd.ExecuteReader())
                    {
                        return rd.Read() ? Map(rd) : null;
                    }
                }
            }
        }

        public RevisionDocumentalCoordinadorRegistro RegistrarFinalizacionInspector(
            int solicitudId,
            int inspectorId,
            string observacionInspector)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                EnsureSchema(cn);
                using (var tx = cn.BeginTransaction())
                {
                    const string sql = @"
INSERT INTO public.aocr_revision_documental_coordinador
(
    solicitud_id, inspector_original_id, estado, observacion_inspector,
    fecha_finalizacion_inspector, fecha_creacion, fecha_actualizacion, activo
)
VALUES
(
    @solicitud_id, @inspector_id, 'PENDIENTE_REVISION_COORDINADOR', @observacion,
    NOW(), NOW(), NOW(), TRUE
)
ON CONFLICT (solicitud_id) DO UPDATE SET
    inspector_original_id = COALESCE(aocr_revision_documental_coordinador.inspector_original_id, EXCLUDED.inspector_original_id),
    estado = CASE
        WHEN aocr_revision_documental_coordinador.estado = 'ACEPTADA_POR_COORDINADOR'
            THEN aocr_revision_documental_coordinador.estado
        ELSE 'PENDIENTE_REVISION_COORDINADOR'
    END,
    observacion_inspector = EXCLUDED.observacion_inspector,
    fecha_finalizacion_inspector = CASE
        WHEN aocr_revision_documental_coordinador.estado = 'ACEPTADA_POR_COORDINADOR'
            THEN aocr_revision_documental_coordinador.fecha_finalizacion_inspector
        ELSE NOW()
    END,
    fecha_actualizacion = NOW(),
    activo = TRUE
RETURNING id;";
                    int id;
                    using (var cmd = new NpgsqlCommand(sql, cn, tx))
                    {
                        cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                        cmd.Parameters.AddWithValue("@inspector_id", inspectorId);
                        cmd.Parameters.AddWithValue("@observacion", (object)(observacionInspector ?? string.Empty));
                        id = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var cmd = new NpgsqlCommand(@"
UPDATE public.aocr_revision_documental_coordinador
SET numero_oficio = COALESCE(
        NULLIF(numero_oficio, ''),
        'DGAC-DCAV-' || TO_CHAR(CURRENT_DATE, 'YYYY') || '-' || LPAD(id::text, 4, '0') || '-O'
    ),
    fecha_actualizacion=NOW()
WHERE id=@id;", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                }
            }

            return ObtenerPorSolicitud(solicitudId);
        }

        public bool AsociarOficio(int solicitudId, int documentoId)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                EnsureSchema(cn);
                using (var cmd = new NpgsqlCommand(@"
UPDATE public.aocr_revision_documental_coordinador
SET documento_oficio_id=@documento_id, fecha_actualizacion=NOW()
WHERE solicitud_id=@solicitud_id AND activo=TRUE;", cn))
                {
                    cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                    cmd.Parameters.AddWithValue("@documento_id", documentoId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        public bool RegistrarDecision(
            int solicitudId,
            int coordinadorId,
            string estado,
            int? inspectorConfirmadoId,
            string observacion,
            string inspectorCodigo = null,
            string inspectorNombre = null,
            string inspectorTipo = null)
        {
            using (var cn = new NpgsqlConnection(ConnectionString))
            {
                cn.Open();
                EnsureSchema(cn);
                using (var tx = cn.BeginTransaction())
                {
                    using (var cmd = new NpgsqlCommand(@"
UPDATE public.aocr_revision_documental_coordinador
SET estado=@estado,
    coordinador_id=@coordinador_id,
    inspector_confirmado_id=@inspector_id,
    observacion_coordinador=@observacion,
    fecha_decision_coordinador=NOW(),
    fecha_habilitacion_lv=CASE WHEN @aceptada THEN NOW() ELSE NULL END,
    fecha_habilitacion_informe=CASE WHEN @aceptada THEN NOW() ELSE NULL END,
    fecha_actualizacion=NOW()
WHERE solicitud_id=@solicitud_id AND activo=TRUE
  AND estado <> 'ACEPTADA_POR_COORDINADOR';", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                        cmd.Parameters.AddWithValue("@coordinador_id", coordinadorId);
                        cmd.Parameters.AddWithValue("@estado", estado);
                        cmd.Parameters.Add("@inspector_id", NpgsqlDbType.Integer).Value = (object)inspectorConfirmadoId ?? DBNull.Value;
                        cmd.Parameters.AddWithValue("@observacion", (object)(observacion ?? string.Empty));
                        cmd.Parameters.AddWithValue("@aceptada", string.Equals(estado, EstadoRevisionDocumentalCoordinador.AceptadaCoordinador, StringComparison.OrdinalIgnoreCase));
                        if (cmd.ExecuteNonQuery() <= 0)
                        {
                            tx.Rollback();
                            return false;
                        }
                    }

                    using (var cmd = new NpgsqlCommand(@"
INSERT INTO public.aocr_inspector_reasignacion_historial
(solicitud_id, inspector_anterior_id, inspector_nuevo_id, coordinador_id, motivo, estado, fecha_creacion)
SELECT solicitud_id, inspector_original_id, @inspector_id, @coordinador_id, @motivo,
       CASE WHEN inspector_original_id=@inspector_id THEN 'INSPECTOR_CONFIRMADO' ELSE 'INSPECTOR_REASIGNADO' END,
       NOW()
FROM public.aocr_revision_documental_coordinador
WHERE solicitud_id=@solicitud_id AND @inspector_id IS NOT NULL
  AND NOT EXISTS
  (
      SELECT 1 FROM public.aocr_inspector_reasignacion_historial h
      WHERE h.solicitud_id=@solicitud_id
        AND h.inspector_nuevo_id=@inspector_id
        AND h.estado IN ('INSPECTOR_CONFIRMADO','INSPECTOR_REASIGNADO')
  );", cn, tx))
                    {
                        cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                        cmd.Parameters.Add("@inspector_id", NpgsqlDbType.Integer).Value = (object)inspectorConfirmadoId ?? DBNull.Value;
                        cmd.Parameters.AddWithValue("@coordinador_id", coordinadorId);
                        cmd.Parameters.AddWithValue("@motivo", (object)(observacion ?? string.Empty));
                        cmd.ExecuteNonQuery();
                    }

                    if (string.Equals(estado, EstadoRevisionDocumentalCoordinador.AceptadaCoordinador, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!inspectorConfirmadoId.HasValue || inspectorConfirmadoId.Value <= 0)
                        {
                            tx.Rollback();
                            return false;
                        }

                        var columnasInspeccion = ObtenerColumnas(cn, tx, "aocr_tbinspeccion");
                        var setInspeccion = new List<string>();
                        if (columnasInspeccion.Contains("codigo_inspector")) setInspeccion.Add("codigo_inspector=@inspector_id");
                        if (columnasInspeccion.Contains("inspector_principal_cedula")) setInspeccion.Add("inspector_principal_cedula=@inspector_codigo");
                        if (columnasInspeccion.Contains("inspector_principal_nombre")) setInspeccion.Add("inspector_principal_nombre=@inspector_nombre");
                        if (columnasInspeccion.Contains("inspector_principal_tipo")) setInspeccion.Add("inspector_principal_tipo=@inspector_tipo");
                        if (columnasInspeccion.Contains("estado_documental")) setInspeccion.Add("estado_documental='ACEPTADA'");
                        if (columnasInspeccion.Contains("updated_at")) setInspeccion.Add("updated_at=NOW()");
                        if (columnasInspeccion.Contains("updated_by")) setInspeccion.Add("updated_by=@coordinador_texto");

                        if (setInspeccion.Count == 0)
                        {
                            tx.Rollback();
                            return false;
                        }

                        using (var cmd = new NpgsqlCommand(
                            "UPDATE public.aocr_tbinspeccion SET " + string.Join(",", setInspeccion) + " WHERE codigo_solicitud=@solicitud_id;", cn, tx))
                        {
                            cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                            cmd.Parameters.AddWithValue("@inspector_id", inspectorConfirmadoId.Value);
                            cmd.Parameters.AddWithValue("@inspector_codigo", (object)(inspectorCodigo ?? string.Empty));
                            cmd.Parameters.AddWithValue("@inspector_nombre", (object)(inspectorNombre ?? string.Empty));
                            cmd.Parameters.AddWithValue("@inspector_tipo", (object)(inspectorTipo ?? string.Empty));
                            cmd.Parameters.AddWithValue("@coordinador_texto", coordinadorId.ToString());
                            if (cmd.ExecuteNonQuery() <= 0)
                            {
                                tx.Rollback();
                                return false;
                            }
                        }

                        var columnasSolicitud = ObtenerColumnas(cn, tx, "aocr_tbsolicitud");
                        var setSolicitud = new List<string>();
                        if (columnasSolicitud.Contains("codigo_tecnico")) setSolicitud.Add("codigo_tecnico=@inspector_id");
                        if (columnasSolicitud.Contains("tecnico_responsable_cedula")) setSolicitud.Add("tecnico_responsable_cedula=@inspector_codigo");
                        if (columnasSolicitud.Contains("tecnico_responsable_nombre")) setSolicitud.Add("tecnico_responsable_nombre=@inspector_nombre");
                        if (columnasSolicitud.Contains("tecnico_responsable_tipo")) setSolicitud.Add("tecnico_responsable_tipo=@inspector_tipo");
                        if (columnasSolicitud.Contains("updated_at")) setSolicitud.Add("updated_at=NOW()");
                        if (columnasSolicitud.Contains("updated_by")) setSolicitud.Add("updated_by=@coordinador_texto");

                        if (setSolicitud.Count > 0)
                        {
                            using (var cmd = new NpgsqlCommand(
                                "UPDATE public.aocr_tbsolicitud SET " + string.Join(",", setSolicitud) + " WHERE codigo_solicitud=@solicitud_id;", cn, tx))
                            {
                                cmd.Parameters.AddWithValue("@solicitud_id", solicitudId);
                                cmd.Parameters.AddWithValue("@inspector_id", inspectorConfirmadoId.Value);
                                cmd.Parameters.AddWithValue("@inspector_codigo", (object)(inspectorCodigo ?? string.Empty));
                                cmd.Parameters.AddWithValue("@inspector_nombre", (object)(inspectorNombre ?? string.Empty));
                                cmd.Parameters.AddWithValue("@inspector_tipo", (object)(inspectorTipo ?? string.Empty));
                                cmd.Parameters.AddWithValue("@coordinador_texto", coordinadorId.ToString());
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    tx.Commit();
                    return true;
                }
            }
        }

        private const string SelectBase = @"
SELECT id, solicitud_id, inspector_original_id, inspector_confirmado_id, coordinador_id,
       documento_oficio_id, numero_oficio, estado, observacion_inspector, observacion_coordinador,
       fecha_finalizacion_inspector, fecha_decision_coordinador, fecha_habilitacion_lv,
       fecha_habilitacion_informe, fecha_creacion, fecha_actualizacion, activo
FROM public.aocr_revision_documental_coordinador";

        private static RevisionDocumentalCoordinadorRegistro Map(NpgsqlDataReader rd)
        {
            return new RevisionDocumentalCoordinadorRegistro
            {
                Id = rd.GetInt32(0),
                SolicitudId = rd.GetInt32(1),
                InspectorOriginalId = rd.IsDBNull(2) ? (int?)null : rd.GetInt32(2),
                InspectorConfirmadoId = rd.IsDBNull(3) ? (int?)null : rd.GetInt32(3),
                CoordinadorId = rd.IsDBNull(4) ? (int?)null : rd.GetInt32(4),
                DocumentoOficioId = rd.IsDBNull(5) ? (int?)null : rd.GetInt32(5),
                NumeroOficio = rd.IsDBNull(6) ? string.Empty : rd.GetString(6),
                Estado = rd.IsDBNull(7) ? string.Empty : rd.GetString(7),
                ObservacionInspector = rd.IsDBNull(8) ? string.Empty : rd.GetString(8),
                ObservacionCoordinador = rd.IsDBNull(9) ? string.Empty : rd.GetString(9),
                FechaFinalizacionInspector = rd.IsDBNull(10) ? (DateTime?)null : rd.GetDateTime(10),
                FechaDecisionCoordinador = rd.IsDBNull(11) ? (DateTime?)null : rd.GetDateTime(11),
                FechaHabilitacionLv = rd.IsDBNull(12) ? (DateTime?)null : rd.GetDateTime(12),
                FechaHabilitacionInforme = rd.IsDBNull(13) ? (DateTime?)null : rd.GetDateTime(13),
                FechaCreacion = rd.GetDateTime(14),
                FechaActualizacion = rd.GetDateTime(15),
                Activo = rd.GetBoolean(16)
            };
        }

        private static HashSet<string> ObtenerColumnas(NpgsqlConnection cn, NpgsqlTransaction tx, string tabla)
        {
            var columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new NpgsqlCommand(@"
SELECT column_name
FROM information_schema.columns
WHERE table_schema='public' AND table_name=@tabla;", cn, tx))
            {
                cmd.Parameters.AddWithValue("@tabla", tabla);
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read()) columnas.Add(rd.GetString(0));
                }
            }
            return columnas;
        }

        private static void EnsureSchema(NpgsqlConnection cn)
        {
            if (_schemaReady) return;
            lock (SchemaLock)
            {
                if (_schemaReady) return;
                using (var cmd = new NpgsqlCommand(@"
CREATE TABLE IF NOT EXISTS public.aocr_revision_documental_coordinador
(
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL UNIQUE,
    inspector_original_id INTEGER NULL,
    inspector_confirmado_id INTEGER NULL,
    coordinador_id INTEGER NULL,
    documento_oficio_id INTEGER NULL,
    numero_oficio VARCHAR(80) NULL,
    estado VARCHAR(80) NOT NULL,
    observacion_inspector TEXT NULL,
    observacion_coordinador TEXT NULL,
    fecha_finalizacion_inspector TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_decision_coordinador TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_habilitacion_lv TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_habilitacion_informe TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS numero_oficio VARCHAR(80) NULL;
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS fecha_habilitacion_lv TIMESTAMP WITHOUT TIME ZONE NULL;
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS fecha_habilitacion_informe TIMESTAMP WITHOUT TIME ZONE NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_revdoc_coord_solicitud
    ON public.aocr_revision_documental_coordinador(solicitud_id);
CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_solicitud
    ON public.aocr_revision_documental_coordinador(solicitud_id);
CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_estado
    ON public.aocr_revision_documental_coordinador(estado);

CREATE TABLE IF NOT EXISTS public.aocr_inspector_reasignacion_historial
(
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL,
    inspector_anterior_id INTEGER NULL,
    inspector_nuevo_id INTEGER NOT NULL,
    coordinador_id INTEGER NOT NULL,
    motivo TEXT NULL,
    estado VARCHAR(80) NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_aocr_reasignacion_solicitud
    ON public.aocr_inspector_reasignacion_historial(solicitud_id);
", cn))
                {
                    cmd.ExecuteNonQuery();
                }
                _schemaReady = true;
            }
        }
    }
}
