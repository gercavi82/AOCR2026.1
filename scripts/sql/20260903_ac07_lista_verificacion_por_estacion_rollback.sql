-- =========================================================================================
-- SCRIPT DE ROLLBACK: 20260903_ac07_lista_verificacion_por_estacion_rollback.sql
-- OBJETIVO: Reversión idempotente de los índices y restricciones creados para AC-07.
-- No elimina destructivamente los datos existentes.
-- =========================================================================================

DROP INDEX IF EXISTS public.uq_aocr_tblv_eae_vigente;
DROP INDEX IF EXISTS public.ix_aocr_tblv_eae_vigente_lookup;
DROP INDEX IF EXISTS public.ix_aocr_tblv_eae_solicitud_estacion;
