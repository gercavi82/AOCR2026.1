-- =====================================================================
-- ROLLBACK MIGRACIÓN: AC-02 FECHAS DE INSPECCIÓN POR ESTACIÓN
-- FECHA: 2026-09-03
-- =====================================================================

DO $$
BEGIN
    DROP INDEX IF EXISTS public.idx_solicitud_estacion_unicidad;
    DROP INDEX IF EXISTS public.idx_solicitud_estacion_solicitud;
    DROP TABLE IF EXISTS public.aocr_tbsolicitud_estacion;
END $$;
