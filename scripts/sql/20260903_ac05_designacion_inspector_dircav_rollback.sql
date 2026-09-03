-- =============================================================================
-- ROLLBACK: AC-05 DESIGNACIÓN DEL INSPECTOR POR DIRCAV
-- =============================================================================

START TRANSACTION;

DROP TABLE IF EXISTS public.aocr_tbdesignacion_inspector CASCADE;

COMMIT;
