BEGIN;
-- Rollback no destructivo: preserva eventos y correos; solo retira índices auxiliares no únicos.
DROP INDEX IF EXISTS public.ix_aocr_evento_workflow_correlation;
DROP INDEX IF EXISTS public.ix_aocr_evento_workflow_solicitud;
COMMIT;
