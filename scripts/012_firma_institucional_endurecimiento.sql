BEGIN;

DO $diagnostico$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM public.rol
    WHERE codigorol=17 AND UPPER(TRIM(descripcion))='DIRECCION' AND activo
  ) THEN
    RAISE EXCEPTION 'No existe el rol DGAC activo esperado (17/Direccion).';
  END IF;
  IF NOT EXISTS (
    SELECT 1 FROM public.rol
    WHERE codigorol=24 AND UPPER(TRIM(descripcion))='DIRECTOR_CERTIFICACIONES_DCAV' AND activo
  ) THEN
    RAISE EXCEPTION 'No existe el rol DCAV activo esperado (24/DIRECTOR_CERTIFICACIONES_DCAV).';
  END IF;
END $diagnostico$;

WITH aprobacion AS (
  SELECT DISTINCT ON (h.solicitud_id,h.inspeccion_id)
         h.solicitud_id,h.inspeccion_id,
         substring(h.observacion from 'AocrId=([0-9]+)')::integer AS aocr_id,
         substring(h.observacion from 'CondicionesId=([0-9]+)')::integer AS condiciones_id
  FROM public.aocr_proceso_estado_historial h
  JOIN public.aocr_proceso_estado pe
    ON pe.solicitud_id=h.solicitud_id
   AND pe.inspeccion_id=h.inspeccion_id
   AND pe.activo=TRUE
   AND pe.estado_actual IN ('PENDIENTE_FIRMA_DIRDAC','PENDIENTE_FIRMAS_INSTITUCIONALES')
  WHERE h.accion='APROBAR_DOCUMENTOS_DCAV'
  ORDER BY h.solicitud_id,h.inspeccion_id,h.fecha_creacion DESC,h.id DESC
)
UPDATE public.aocr_tbdocumento_generado d
SET estado='PENDIENTE_FIRMA_DGAC',fecha_actualizacion=NOW()
FROM aprobacion a
WHERE d.codigo_documento=a.aocr_id
  AND d.codigo_solicitud=a.solicitud_id
  AND d.estado='APROBADO_DCAV'
  AND d.vigente=TRUE AND d.eliminado=FALSE;

WITH aprobacion AS (
  SELECT DISTINCT ON (h.solicitud_id,h.inspeccion_id)
         h.solicitud_id,h.inspeccion_id,
         substring(h.observacion from 'CondicionesId=([0-9]+)')::integer AS condiciones_id
  FROM public.aocr_proceso_estado_historial h
  JOIN public.aocr_proceso_estado pe
    ON pe.solicitud_id=h.solicitud_id
   AND pe.inspeccion_id=h.inspeccion_id
   AND pe.activo=TRUE
   AND pe.estado_actual IN ('PENDIENTE_FIRMA_DIRDAC','PENDIENTE_FIRMAS_INSTITUCIONALES')
  WHERE h.accion='APROBAR_DOCUMENTOS_DCAV'
  ORDER BY h.solicitud_id,h.inspeccion_id,h.fecha_creacion DESC,h.id DESC
)
UPDATE public.aocr_tbdocumento_generado d
SET estado='PENDIENTE_FIRMA_DCAV',fecha_actualizacion=NOW()
FROM aprobacion a
WHERE d.codigo_documento=a.condiciones_id
  AND d.codigo_solicitud=a.solicitud_id
  AND d.estado='APROBADO_DCAV'
  AND d.vigente=TRUE AND d.eliminado=FALSE;

CREATE INDEX IF NOT EXISTS idx_aocr_documento_pendiente_firma_dgac
ON public.aocr_tbdocumento_generado(codigo_solicitud,codigo_documento,version)
WHERE vigente=TRUE AND eliminado=FALSE AND estado='PENDIENTE_FIRMA_DGAC';

CREATE INDEX IF NOT EXISTS idx_aocr_documento_pendiente_firma_dcav
ON public.aocr_tbdocumento_generado(codigo_solicitud,codigo_documento,version)
WHERE vigente=TRUE AND eliminado=FALSE AND estado='PENDIENTE_FIRMA_DCAV';

COMMIT;
