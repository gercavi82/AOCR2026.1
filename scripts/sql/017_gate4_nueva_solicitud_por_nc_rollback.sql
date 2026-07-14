BEGIN;
DROP INDEX IF EXISTS public.ix_solicitud_origen_gate4;
DROP INDEX IF EXISTS public.ux_solicitud_activa_nc_gate4;
ALTER TABLE public.aocr_tbsolicitud
 DROP CONSTRAINT IF EXISTS fk_solicitud_informe_origen_gate4,
 DROP CONSTRAINT IF EXISTS fk_solicitud_inspeccion_origen_gate4,
 DROP CONSTRAINT IF EXISTS fk_solicitud_solicitud_origen_gate4,
 DROP CONSTRAINT IF EXISTS fk_solicitud_nc_origen_gate4;
-- No destructivo: se conservan columnas y vínculos creados.
COMMIT;
