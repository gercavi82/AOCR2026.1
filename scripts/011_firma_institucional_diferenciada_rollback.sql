BEGIN;
DROP INDEX IF EXISTS public.idx_aocr_estado_firmas_institucionales;
DROP INDEX IF EXISTS public.uq_aocr_firma_institucional_vigente;
ALTER TABLE public.aocr_tbfirma_documento DROP CONSTRAINT IF EXISTS ck_aocr_firma_institucional_matriz;
-- Los estados e historiales ya producidos no se degradan ni eliminan automáticamente.
COMMIT;
