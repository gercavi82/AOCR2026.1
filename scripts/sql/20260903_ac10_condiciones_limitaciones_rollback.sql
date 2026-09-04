-- =========================================================================================
-- SCRIPT DE ROLLBACK: 20260903_ac10_condiciones_limitaciones_rollback.sql
-- OBJETIVO: Reversión de la migración AC-10.
-- =========================================================================================

DROP INDEX IF EXISTS public.ix_cl_solicitud_fecha;
DROP INDEX IF EXISTS public.ix_cl_estado_vigente;
DROP INDEX IF EXISTS public.ux_cl_solicitud_version;
DROP INDEX IF EXISTS public.ux_cl_solicitud_vigente;

DROP TABLE IF EXISTS public.aocr_tbcondiciones_limitaciones;
