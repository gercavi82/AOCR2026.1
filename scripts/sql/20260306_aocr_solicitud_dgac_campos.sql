-- =============================================
-- AOCR - Ajustes DGAC/DCAVC Solicitud AOCR
-- Fecha: 2026-03-06
-- Motor: PostgreSQL
-- Tipo: Idempotente
-- =============================================

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS correo_representante_tecnico VARCHAR(200);

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS nombre_comercial VARCHAR(250);

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS resumen_operaciones_eae TEXT;

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS aprobaciones_especiales VARCHAR(500);

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS aprobaciones_especiales_otros VARCHAR(250);

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS aeropuertos_ecuador VARCHAR(500);

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS aeropuertos_ecuador_otros VARCHAR(250);

ALTER TABLE IF EXISTS public.aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS companias_seleccionadas TEXT;

-- Backfill conservador para registros existentes
UPDATE public.aocr_tbsolicitud
SET correo_representante_tecnico = COALESCE(NULLIF(TRIM(correo_representante_tecnico), ''), NULLIF(TRIM(email), ''))
WHERE COALESCE(NULLIF(TRIM(correo_representante_tecnico), ''), '') = '';

UPDATE public.aocr_tbsolicitud
SET nombre_comercial = COALESCE(NULLIF(TRIM(nombre_comercial), ''), NULLIF(TRIM(nombre_operador), ''), NULLIF(TRIM(razon_social), ''))
WHERE COALESCE(NULLIF(TRIM(nombre_comercial), ''), '') = '';

UPDATE public.aocr_tbsolicitud
SET resumen_operaciones_eae = COALESCE(NULLIF(TRIM(resumen_operaciones_eae), ''), NULLIF(TRIM(descripcion_operacion), ''))
WHERE COALESCE(NULLIF(TRIM(resumen_operaciones_eae), ''), '') = '';

