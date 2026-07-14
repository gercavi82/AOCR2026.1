BEGIN;
DROP INDEX IF EXISTS public.ix_aocr_proceso_estado_bandeja;
DROP INDEX IF EXISTS public.ux_aocr_proceso_estado_solicitud_activo;
DROP TABLE IF EXISTS public.aocr_proceso_estado;
COMMIT;
