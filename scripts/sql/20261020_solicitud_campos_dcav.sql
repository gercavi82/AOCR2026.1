-- Campos requeridos por el flujo de aceptacion y concurrencia DCAV.
-- Repetible. No cambia estados, documentos, usuarios ni roles.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
ALTER TABLE public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS estado_documental varchar(50);
COMMIT;
