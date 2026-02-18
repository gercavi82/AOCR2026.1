-- ============================================================
-- AOCR - Flujo rechazo/anulación con trazabilidad + idempotencia de correos
-- Fecha: 2026-02-18
-- Idempotente: SI
-- ============================================================

-- 1) Historial de estados de órdenes de recaudación
CREATE TABLE IF NOT EXISTS public.aocr_or_estado_historial (
    id SERIAL PRIMARY KEY,
    orden_id INTEGER NOT NULL REFERENCES public.aocr_or_orden(id),
    estado_anterior VARCHAR(50),
    estado_nuevo VARCHAR(50) NOT NULL,
    observaciones TEXT,
    usuario VARCHAR(100),
    rol VARCHAR(100),
    fecha TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_aocr_or_estado_historial_orden
    ON public.aocr_or_estado_historial(orden_id, fecha DESC);

-- 2) Campos de auditoría en orden (si no existen)
ALTER TABLE IF EXISTS public.aocr_or_orden
    ADD COLUMN IF NOT EXISTS motivo_anulacion TEXT,
    ADD COLUMN IF NOT EXISTS anulada_por VARCHAR(100),
    ADD COLUMN IF NOT EXISTS anulada_en TIMESTAMP,
    ADD COLUMN IF NOT EXISTS rol_anulador VARCHAR(100);

-- 3) EmailQueue: event_key + metadatos para idempotencia y diagnóstico
ALTER TABLE IF EXISTS public.email_queue
    ADD COLUMN IF NOT EXISTS event_key VARCHAR(180),
    ADD COLUMN IF NOT EXISTS rol_origen VARCHAR(100),
    ADD COLUMN IF NOT EXISTS estado_final VARCHAR(50);

CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
    ON public.email_queue(event_key)
    WHERE event_key IS NOT NULL;

