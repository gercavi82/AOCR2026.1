BEGIN;

DO $diagnostico$
DECLARE v_duplicados integer;
BEGIN
  SELECT COUNT(*) INTO v_duplicados
  FROM (
    SELECT codigo_solicitud,codigo_inspeccion,UPPER(tipo_documento),version
    FROM public.aocr_tbfirma_documento
    WHERE estado_documento IN ('FIRMADO_DGAC','FIRMADO_DCAV')
    GROUP BY 1,2,3,4 HAVING COUNT(*)>1
  ) d;
  IF v_duplicados>0 THEN
    RAISE EXCEPTION 'Existen % grupos de firmas institucionales duplicadas. No se modifica información histórica.',v_duplicados;
  END IF;
  IF NOT EXISTS(SELECT 1 FROM public.rol WHERE codigorol=17 AND UPPER(TRIM(descripcion))='DIRECCION' AND activo) THEN
    RAISE EXCEPTION 'No existe el rol DGAC real esperado (17/Direccion).';
  END IF;
  IF NOT EXISTS(SELECT 1 FROM public.rol WHERE codigorol=24 AND UPPER(TRIM(descripcion))='DIRECTOR_CERTIFICACIONES_DCAV' AND activo) THEN
    RAISE EXCEPTION 'No existe el rol DCAV real esperado (24/DIRECTOR_CERTIFICACIONES_DCAV).';
  END IF;
END $diagnostico$;

UPDATE public.aocr_proceso_estado
SET estado_actual='PENDIENTE_FIRMAS_INSTITUCIONALES',
    etapa_actual='FIRMAS_INSTITUCIONALES',
    rol_responsable='FIRMANTES_INSTITUCIONALES',
    siguiente_accion='COMPLETAR_FIRMAS_INSTITUCIONALES',
    fecha_estado=NOW(),version=version+1
WHERE activo=TRUE AND estado_actual='PENDIENTE_FIRMA_DIRDAC';

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_firma_institucional_vigente
ON public.aocr_tbfirma_documento(codigo_solicitud,codigo_inspeccion,UPPER(tipo_documento),version)
WHERE estado_documento IN ('FIRMADO_DGAC','FIRMADO_DCAV');

CREATE INDEX IF NOT EXISTS idx_aocr_estado_firmas_institucionales
ON public.aocr_proceso_estado(fecha_estado,solicitud_id,inspeccion_id)
WHERE activo=TRUE AND estado_actual='PENDIENTE_FIRMAS_INSTITUCIONALES';

ALTER TABLE public.aocr_tbfirma_documento
  DROP CONSTRAINT IF EXISTS ck_aocr_firma_institucional_matriz;
ALTER TABLE public.aocr_tbfirma_documento
  ADD CONSTRAINT ck_aocr_firma_institucional_matriz CHECK (
    estado_documento NOT IN ('FIRMADO_DGAC','FIRMADO_DCAV') OR
    (UPPER(tipo_documento)='RECONOCIMIENTO' AND estado_documento='FIRMADO_DGAC' AND UPPER(firmado_por_rol)='DIRECCION') OR
    (UPPER(tipo_documento)='CONDICIONES_LIMITACIONES' AND estado_documento='FIRMADO_DCAV' AND UPPER(firmado_por_rol)='DIRECTOR_CERTIFICACIONES_DCAV')
  );

COMMIT;
