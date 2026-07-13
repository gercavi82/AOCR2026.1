-- AOCR P0 - Habilitacion transaccional de AOCR y Condiciones tras aprobacion DCAV
-- Fecha: 2026-07-13
-- Prerrequisito: scripts/001_estado_central_upgrade.sql
-- El despliegue es no destructivo. Si hay duplicados vigentes, se aborta sin eliminarlos.

BEGIN;

ALTER TABLE public.aocr_tbdocumento_generado
    ADD COLUMN IF NOT EXISTS codigo_compania VARCHAR(160) NULL,
    ADD COLUMN IF NOT EXISTS codigo_inspector INTEGER NULL,
    ADD COLUMN IF NOT EXISTS version INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS eliminado BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS usuario_creador_id INTEGER NULL,
    ADD COLUMN IF NOT EXISTS fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NULL;

ALTER TABLE public.aocr_proceso_idempotencia
    ADD COLUMN IF NOT EXISTS aocr_id INTEGER NULL,
    ADD COLUMN IF NOT EXISTS condiciones_id INTEGER NULL,
    ADD COLUMN IF NOT EXISTS estado_anterior VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS estado_nuevo VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS resultado VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100) NULL;

ALTER TABLE public.aocr_tbnotificacion
    ADD COLUMN IF NOT EXISTS event_key VARCHAR(200) NULL,
    ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100) NULL;

CREATE INDEX IF NOT EXISTS idx_aocr_doc_inspector_bandeja
    ON public.aocr_tbdocumento_generado
       (codigo_inspector, codigo_solicitud, codigo_inspeccion, estado)
    WHERE vigente=TRUE AND eliminado=FALSE;

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbnotificacion_event_key
    ON public.aocr_tbnotificacion(event_key)
    WHERE event_key IS NOT NULL;

DO $migration$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM public.aocr_tbdocumento_generado
        WHERE vigente=TRUE AND eliminado=FALSE
          AND codigo_inspeccion IS NOT NULL
          AND UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES')
        GROUP BY codigo_solicitud,codigo_inspeccion,UPPER(TRIM(tipo_documento))
        HAVING COUNT(*)>1
    ) THEN
        RAISE EXCEPTION 'AOCR_P0_DUPLICADOS: revise el reporte; no se creo la restriccion unica.';
    END IF;
END
$migration$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_doc_vigente_expediente_tipo
    ON public.aocr_tbdocumento_generado
       (codigo_solicitud,codigo_inspeccion,(UPPER(TRIM(tipo_documento))))
    WHERE vigente=TRUE AND eliminado=FALSE
      AND codigo_inspeccion IS NOT NULL
      AND UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES');

COMMIT;

-- REPORTE DE DUPLICADOS (ejecutar antes del despliegue y conservar como evidencia)
SELECT codigo_solicitud,codigo_inspeccion,UPPER(TRIM(tipo_documento)) AS tipo_documento,
       COUNT(*) AS cantidad,ARRAY_AGG(codigo_documento ORDER BY codigo_documento) AS documentos
FROM public.aocr_tbdocumento_generado
WHERE vigente=TRUE AND eliminado=FALSE
  AND UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES')
GROUP BY codigo_solicitud,codigo_inspeccion,UPPER(TRIM(tipo_documento))
HAVING COUNT(*)>1
ORDER BY codigo_solicitud,codigo_inspeccion,tipo_documento;

-- INVENTARIO DE DOCUMENTOS DE ESTA FASE
SELECT codigo_solicitud,codigo_inspeccion,codigo_compania,codigo_inspector,
       tipo_documento,version,estado,vigente,eliminado,fecha_generacion
FROM public.aocr_tbdocumento_generado
WHERE UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES')
ORDER BY codigo_solicitud,codigo_inspeccion,tipo_documento,version DESC;

-- REVERSIÓN ESTRUCTURAL (ejecutar manualmente solo si se revierte también la aplicación):
-- DROP INDEX IF EXISTS public.uq_aocr_doc_vigente_expediente_tipo;
-- DROP INDEX IF EXISTS public.idx_aocr_doc_inspector_bandeja;
-- No eliminar columnas ni datos: pueden contener trazabilidad generada durante la vigencia del cambio.
