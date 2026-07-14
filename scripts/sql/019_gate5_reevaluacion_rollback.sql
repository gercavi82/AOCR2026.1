BEGIN;
-- Rollback no destructivo: se retiran únicamente restricciones e índices.
-- Las columnas y su trazabilidad se conservan para no perder evidencia histórica.
DROP INDEX IF EXISTS public.ux_informe_reevaluacion_antecedente;
DROP INDEX IF EXISTS public.ux_lv_reevaluacion_antecedente;
DROP INDEX IF EXISTS public.ix_informe_nc_ciclo;
ALTER TABLE public.aocr_tbinforme_inspeccion DROP CONSTRAINT IF EXISTS fk_informe_ciclo_anterior;
ALTER TABLE public.aocr_tbinforme_inspeccion DROP CONSTRAINT IF EXISTS fk_informe_ciclo_nc;
ALTER TABLE public.aocr_tbinforme_inspeccion DROP CONSTRAINT IF EXISTS ck_informe_ciclo_positivo;
ALTER TABLE public.aocr_tblv_operacional_eae DROP CONSTRAINT IF EXISTS fk_lv_ciclo_anterior;
ALTER TABLE public.aocr_tblv_operacional_eae DROP CONSTRAINT IF EXISTS fk_lv_ciclo_nc;
ALTER TABLE public.aocr_tblv_operacional_eae DROP CONSTRAINT IF EXISTS ck_lv_ciclo_positivo;
COMMIT;
