BEGIN;
DROP INDEX IF EXISTS public.ix_docsub_revision_gate3;
ALTER TABLE public.aocr_tbdocumento_subsanacion
    DROP CONSTRAINT IF EXISTS fk_docsub_revisor_gate3,
    DROP CONSTRAINT IF EXISTS chk_docsub_rechazo_comentario_gate3,
    DROP CONSTRAINT IF EXISTS chk_docsub_decision_gate3;
-- Rollback no destructivo: conserva decisiones y columnas de auditoria.
COMMIT;
