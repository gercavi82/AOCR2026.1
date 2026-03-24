-- AOCR - Refuerzo opcional de trazabilidad e idempotencia para notificaciones internas
-- Fecha: 2026-03-23
-- Objetivo: agregar columnas y restricciones no destructivas en aocr_tbnotificacion
--           para soportar correlacion e idempotencia similar a email_queue.
-- Script idempotente.

BEGIN;

-- 1) Columnas de trazabilidad/idempotencia
ALTER TABLE public.aocr_tbnotificacion
    ADD COLUMN IF NOT EXISTS event_key VARCHAR(200),
    ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(64),
    ADD COLUMN IF NOT EXISTS request_hash VARCHAR(128),
    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

-- 2) Backfill liviano para registros existentes sin updated_at
UPDATE public.aocr_tbnotificacion
SET updated_at = COALESCE(fechacreacion, NOW())
WHERE updated_at IS NULL;

-- 3) Indices de consulta operativa
CREATE INDEX IF NOT EXISTS idx_aocr_tbnotificacion_usuario_fecha
    ON public.aocr_tbnotificacion (codigousuario, fechacreacion DESC);

CREATE INDEX IF NOT EXISTS idx_aocr_tbnotificacion_correlation
    ON public.aocr_tbnotificacion (correlation_id)
    WHERE correlation_id IS NOT NULL;

-- 4) Idempotencia por event_key (equivalente funcional a email_queue)
CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbnotificacion_event_key
    ON public.aocr_tbnotificacion (event_key)
    WHERE event_key IS NOT NULL;

-- 5) Trigger de mantenimiento updated_at
CREATE OR REPLACE FUNCTION public.fn_aocr_tbnotificacion_set_updated_at()
RETURNS trigger AS
$$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_aocr_tbnotificacion_set_updated_at ON public.aocr_tbnotificacion;

CREATE TRIGGER trg_aocr_tbnotificacion_set_updated_at
BEFORE UPDATE ON public.aocr_tbnotificacion
FOR EACH ROW
EXECUTE FUNCTION public.fn_aocr_tbnotificacion_set_updated_at();

COMMIT;
