-- Revierte solo indices y la normalizacion identificable de estado. No deshace envios reales.
BEGIN;
DROP INDEX IF EXISTS public.ix_aocr_documento_envio_dcav;
DROP INDEX IF EXISTS public.ix_aocr_proceso_documentos_dcav;
UPDATE public.aocr_proceso_estado
SET estado_actual='DOCUMENTOS_EN_REVISION_INSPECTOR',
    etapa_actual='DOCUMENTOS_FINALES_INSPECTOR',
    siguiente_accion='REVISAR_AOCR_CONDICIONES',
    observacion=REPLACE(COALESCE(observacion,''),' [MIGRACION_007: generacion separada del envio]','')
WHERE activo=TRUE AND estado_actual='DOCUMENTOS_HABILITADOS_INSPECTOR'
  AND COALESCE(observacion,'') LIKE '%[MIGRACION_007: generacion separada del envio]%';
COMMIT;
