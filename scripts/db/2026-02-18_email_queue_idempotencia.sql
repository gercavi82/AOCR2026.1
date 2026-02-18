-- AOCR - Idempotencia de EmailQueue
-- Fecha: 2026-02-18
-- Objetivo:
-- 1) Evitar duplicados por evento de negocio (event_key)
-- 2) Mantener compatibilidad con correlación por destinatario
-- Script idempotente: SI

ALTER TABLE IF EXISTS public.email_queue
    ADD COLUMN IF NOT EXISTS event_key VARCHAR(180),
    ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100),
    ADD COLUMN IF NOT EXISTS tipo_notificacion VARCHAR(80);

-- Clave principal de idempotencia por evento (requerida por flujo ORDEN_*_*)
CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
ON public.email_queue (event_key)
WHERE event_key IS NOT NULL;

-- Compatibilidad adicional: evita duplicado exacto por correlación/tipo/destinatario
CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_idempotencia
ON public.email_queue (correlation_id, tipo_notificacion, to_address)
WHERE correlation_id IS NOT NULL;
