-- AC-09: PostgreSQL. Aditiva e idempotente; no reescribe informes históricos.
BEGIN;
ALTER TABLE public.aocr_tbinforme_inspeccion
    ADD COLUMN IF NOT EXISTS base_legal TEXT;
COMMIT;
