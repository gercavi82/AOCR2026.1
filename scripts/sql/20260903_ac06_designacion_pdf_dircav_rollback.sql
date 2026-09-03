-- =========================================================================================
-- SCRIPT DE ROLLBACK: 20260903_ac06_designacion_pdf_dircav_rollback.sql
-- OBJETIVO: Reversión idempotente de índices de AC-06.
-- No elimina destructivamente columnas ni datos de producción.
-- =========================================================================================

DROP INDEX IF EXISTS public.ix_aocr_designacion_firmado;
