-- Mantiene los informes históricos sin reasignar ni modificar documentos firmados.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';
ALTER TABLE public.aocr_tbinforme_inspeccion ADD COLUMN IF NOT EXISTS estacion_id INTEGER NULL;
CREATE INDEX IF NOT EXISTS idx_informe_inspeccion_estacion
    ON public.aocr_tbinforme_inspeccion(codigo_inspeccion, estacion_id, version DESC);
COMMIT;
