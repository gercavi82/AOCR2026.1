-- =============================================================================
-- Rollback: 20260903_ac04_recorrido_inspector_coordinador_dircav_rollback.sql
-- Descripción: Revierte índices, claves foráneas y restricciones de AC-04.
-- =============================================================================

BEGIN;

-- 1. Eliminar restricciones de llave foránea y check
ALTER TABLE IF EXISTS public.aocr_inspector_reasignacion_historial
    DROP CONSTRAINT IF EXISTS fk_reasig_solicitud;

ALTER TABLE IF EXISTS public.aocr_revision_documental_coordinador
    DROP CONSTRAINT IF EXISTS chk_aocr_revdoc_coord_estado;

ALTER TABLE IF EXISTS public.aocr_revision_documental_coordinador
    DROP CONSTRAINT IF EXISTS fk_revdoc_coord_solicitud;

-- 2. Eliminar índices adicionales
DROP INDEX IF EXISTS public.ix_reasig_inspector_nuevo;
DROP INDEX IF EXISTS public.ix_aocr_revdoc_coord_inspector_conf;
DROP INDEX IF EXISTS public.ix_aocr_revdoc_coord_inspector_orig;
DROP INDEX IF EXISTS public.ix_aocr_revdoc_coord_coordinador;

COMMIT;
