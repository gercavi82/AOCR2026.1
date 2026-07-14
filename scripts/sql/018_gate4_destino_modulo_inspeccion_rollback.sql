BEGIN;
DROP INDEX IF EXISTS public.ix_solicitud_modulo_destino_gate4;
ALTER TABLE public.aocr_tbsolicitud DROP CONSTRAINT IF EXISTS chk_solicitud_modulo_destino_gate4;
-- No destructivo: conserva clasificación histórica.
COMMIT;
