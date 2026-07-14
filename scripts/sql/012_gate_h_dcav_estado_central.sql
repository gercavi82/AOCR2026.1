BEGIN;
CREATE TABLE IF NOT EXISTS public.aocr_proceso_estado (
    id BIGSERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL,
    inspeccion_id INTEGER NULL,
    estado_actual VARCHAR(100) NOT NULL,
    etapa_actual VARCHAR(100) NULL,
    rol_responsable VARCHAR(100) NOT NULL,
    observacion TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    version BIGINT NOT NULL DEFAULT 1,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by INTEGER NOT NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_by INTEGER NOT NULL
);

-- Compatibilidad con instalaciones donde la tabla fue creada por una versión preliminar.
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS solicitud_id INTEGER;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS inspeccion_id INTEGER NULL;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS estado_actual VARCHAR(100);
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS etapa_actual VARCHAR(100) NULL;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS rol_responsable VARCHAR(100);
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS observacion TEXT NULL;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS activo BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS version BIGINT NOT NULL DEFAULT 1;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS created_by INTEGER NOT NULL DEFAULT 0;
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP NOT NULL DEFAULT NOW();
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS updated_by INTEGER NOT NULL DEFAULT 0;
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_proceso_estado_solicitud_activo
ON public.aocr_proceso_estado(solicitud_id) WHERE activo=TRUE;
CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_bandeja
ON public.aocr_proceso_estado(estado_actual,activo,inspeccion_id);

WITH candidatos AS (
    SELECT DISTINCT ON (i.codigo_solicitud)
        i.codigo_solicitud AS solicitud_id,
        i.codigo_inspeccion AS inspeccion_id,
        COALESCE(
            NULLIF(regexp_replace(COALESCE(inf.usuario_firma_1::text, ''), '[^0-9]', '', 'g'), ''),
            '0'
        )::INTEGER AS usuario_id,
        COALESCE(inf.fecha_envio_dirdac,inf.updated_at,inf.created_at,NOW()) AS fecha
    FROM public.aocr_tbinspeccion i
    JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud=i.codigo_solicitud AND s.deleted_at IS NULL
    JOIN public.aocr_tbinforme_inspeccion inf ON inf.codigo_inspeccion=i.codigo_inspeccion
    WHERE inf.finalizado=TRUE AND inf.firmado_inspector=TRUE AND COALESCE(inf.firmado_dirdac,FALSE)=FALSE
      AND UPPER(TRIM(COALESCE(inf.resultado,'')))='SATISFACTORIO'
      AND NULLIF(TRIM(COALESCE(inf.ruta_documento_firmado,'')),'') IS NOT NULL
      AND regexp_replace(UPPER(COALESCE(inf.estado_informe,'')),'[\s_-]+','_','g') IN
          ('ENVIADO_A_DIRDAC','ENVIADO_A_DIRECCION','PENDIENTE_REVISION_DIRDAC','PENDIENTE_REVISION_DIRECCION','PENDIENTE_REVISION_INSTITUCIONAL')
    ORDER BY i.codigo_solicitud,inf.version DESC,inf.codigo_informe DESC
)
INSERT INTO public.aocr_proceso_estado
    (solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,observacion,activo,version,created_at,created_by,updated_at,updated_by)
SELECT solicitud_id,inspeccion_id,'PENDIENTE_REVISION_INFORME_DCAV','REVISION_INFORME_DCAV','DirectorCertificacionesDcav',
       'Migración GATE H desde informe técnico pendiente institucional.',TRUE,1,fecha,usuario_id,fecha,usuario_id
FROM candidatos c
WHERE NOT EXISTS(SELECT 1 FROM public.aocr_proceso_estado pe WHERE pe.solicitud_id=c.solicitud_id AND pe.activo=TRUE);
COMMIT;
