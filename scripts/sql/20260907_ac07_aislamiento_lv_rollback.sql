-- =========================================================================================
-- SCRIPT DE ROLLBACK: 20260907_ac07_aislamiento_lv_rollback.sql
-- OBJETIVO: Reversión idempotente y segura de las restricciones e índices creados para AC-07
--           (Aislamiento e independencia de Lista de Verificación por inspección y estación).
--           No elimina destructivamente datos existentes.
-- =========================================================================================

BEGIN;

-- 1. Eliminar la restricción de clave foránea hacia estaciones
ALTER TABLE IF EXISTS public.aocr_tblv_operacional_eae
    DROP CONSTRAINT IF EXISTS fk_lv_estacion_ac07;

-- 2. Eliminar el índice único parcial por ámbito y estación vigente
DROP INDEX IF EXISTS public.uq_aocr_tblv_eae_inspeccion_estacion_vigente;

-- 3. Eliminar el índice compuesto de consulta por solicitud y estación
DROP INDEX IF EXISTS public.ix_aocr_tblv_eae_solicitud_estacion;

COMMIT;
