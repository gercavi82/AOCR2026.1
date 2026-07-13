-- P0 - Envio conjunto AOCR + Condiciones desde Inspector a DCAV.
-- No crea tablas ni columnas; valida dependencias de fases anteriores y agrega indices.
BEGIN;

DO $$
DECLARE missing TEXT;
BEGIN
    SELECT string_agg(req.objeto, ', ') INTO missing
    FROM (VALUES
        (CASE WHEN to_regclass('public.aocr_proceso_estado') IS NULL THEN 'aocr_proceso_estado' END),
        (CASE WHEN to_regclass('public.aocr_proceso_estado_historial') IS NULL THEN 'aocr_proceso_estado_historial' END),
        (CASE WHEN to_regclass('public.aocr_proceso_idempotencia') IS NULL THEN 'aocr_proceso_idempotencia' END),
        (CASE WHEN to_regclass('public.aocr_tbdocumento_generado') IS NULL THEN 'aocr_tbdocumento_generado' END),
        (CASE WHEN to_regclass('public.aocr_tbdocumento_inspeccion') IS NULL THEN 'aocr_tbdocumento_inspeccion' END)
    ) req(objeto) WHERE req.objeto IS NOT NULL;
    IF missing IS NOT NULL THEN RAISE EXCEPTION 'Dependencias ausentes: %', missing; END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_proceso_idempotencia' AND column_name='aocr_id')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_proceso_idempotencia' AND column_name='condiciones_id')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbnotificacion' AND column_name='event_key')
    THEN RAISE EXCEPTION 'Ejecute primero scripts/004_habilitacion_documentos_dcav.sql.'; END IF;
END $$;

-- Preflight: no debe devolver filas.
SELECT codigo_solicitud,codigo_inspeccion,
       CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END tipo,
       COUNT(*) vigentes
FROM public.aocr_tbdocumento_generado
WHERE vigente=TRUE AND eliminado=FALSE
  AND UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO','CONDICIONES','CONDICIONES_LIMITACIONES')
GROUP BY codigo_solicitud,codigo_inspeccion,CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END
HAVING COUNT(*)<>1;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM public.aocr_tbdocumento_generado
        WHERE vigente=TRUE AND eliminado=FALSE
          AND UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO','CONDICIONES','CONDICIONES_LIMITACIONES')
        GROUP BY codigo_solicitud,codigo_inspeccion,CASE WHEN UPPER(TRIM(tipo_documento)) IN ('AOCR','RECONOCIMIENTO') THEN 'RECONOCIMIENTO' ELSE 'CONDICIONES_LIMITACIONES' END
        HAVING COUNT(*)>1
    ) THEN RAISE EXCEPTION 'Existen documentos vigentes duplicados. Resolver sin eliminar historicos.'; END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_aocr_documento_envio_dcav
ON public.aocr_tbdocumento_generado(codigo_solicitud,codigo_inspeccion,estado,version DESC)
WHERE vigente=TRUE AND eliminado=FALSE;

CREATE INDEX IF NOT EXISTS ix_aocr_proceso_documentos_dcav
ON public.aocr_proceso_estado(estado_actual,fecha_estado DESC)
WHERE activo=TRUE;

-- Corrige solamente el estado intermedio creado por el generador legado; generar nunca equivale a enviar.
UPDATE public.aocr_proceso_estado
SET estado_actual='DOCUMENTOS_HABILITADOS_INSPECTOR',
    etapa_actual='DOCUMENTOS_FINALES_INSPECTOR',
    siguiente_accion='FINALIZAR_Y_ENVIAR_DOCUMENTOS_DCAV',
    observacion=COALESCE(observacion,'')||' [MIGRACION_007: generacion separada del envio]'
WHERE activo=TRUE AND estado_actual='DOCUMENTOS_EN_REVISION_INSPECTOR';

COMMIT;

SELECT estado_actual,COUNT(*) FROM public.aocr_proceso_estado
WHERE activo=TRUE AND estado_actual IN ('DOCUMENTOS_HABILITADOS_INSPECTOR','DOCUMENTOS_OBSERVADOS_DCAV','PENDIENTE_REVISION_DOCUMENTOS_DCAV')
GROUP BY estado_actual ORDER BY estado_actual;
