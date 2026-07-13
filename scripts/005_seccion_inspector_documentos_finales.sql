-- AOCR P0 - soporte de consulta para la seccion exclusiva del Inspector
-- Fecha: 2026-07-13
-- No crea tablas, no agrega columnas y no modifica documentos historicos.

BEGIN;

CREATE INDEX IF NOT EXISTS idx_aocr_doc_final_inspector_estado
    ON public.aocr_tbdocumento_generado
       (codigo_inspector,codigo_solicitud,codigo_inspeccion,estado,version DESC)
    WHERE vigente=TRUE AND eliminado=FALSE
      AND UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES');

CREATE INDEX IF NOT EXISTS idx_aocr_historial_observaciones_dcav
    ON public.aocr_proceso_estado_historial(solicitud_id,fecha_creacion DESC)
    WHERE estado_nuevo='DOCUMENTOS_OBSERVADOS_DCAV';

COMMIT;

-- Diagnostico: el resultado esperado es cero filas.
SELECT codigo_solicitud,codigo_inspeccion,codigo_inspector,UPPER(TRIM(tipo_documento)) tipo,COUNT(*) cantidad
FROM public.aocr_tbdocumento_generado
WHERE vigente=TRUE AND eliminado=FALSE
  AND UPPER(TRIM(tipo_documento)) IN ('RECONOCIMIENTO','CONDICIONES_LIMITACIONES')
GROUP BY codigo_solicitud,codigo_inspeccion,codigo_inspector,UPPER(TRIM(tipo_documento))
HAVING COUNT(*)>1;

-- Reversion manual:
-- DROP INDEX IF EXISTS public.idx_aocr_doc_final_inspector_estado;
-- DROP INDEX IF EXISTS public.idx_aocr_historial_observaciones_dcav;
