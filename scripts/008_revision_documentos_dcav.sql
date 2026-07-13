-- P0 - Segunda bandeja exclusiva de revision documental DCAV.
-- No crea tablas ni columnas y no modifica expedientes.
BEGIN;

DO $$ BEGIN
 IF to_regclass('public.aocr_proceso_estado') IS NULL OR
    to_regclass('public.aocr_proceso_estado_historial') IS NULL OR
    to_regclass('public.aocr_tbdocumento_generado') IS NULL OR
    to_regclass('public.aocr_tbdocumento_inspeccion') IS NULL
 THEN RAISE EXCEPTION 'Faltan dependencias de estado o documentos.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='aocr_tbnotificacion' AND column_name='event_key')
 THEN RAISE EXCEPTION 'Ejecute primero 004_habilitacion_documentos_dcav.sql.'; END IF;
END $$;

-- Todo expediente ya pendiente debe conservar la trazabilidad de los PDF exactos enviados.
DO $$ BEGIN
 IF EXISTS(
   SELECT 1 FROM public.aocr_proceso_estado pe
   WHERE pe.activo=TRUE AND pe.estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV'
     AND NOT EXISTS(SELECT 1 FROM public.aocr_proceso_estado_historial h
       WHERE h.solicitud_id=pe.solicitud_id AND h.accion='ENVIAR_DOCUMENTOS_DCAV'
         AND h.observacion ~ 'AocrId=[0-9]+' AND h.observacion ~ 'AocrPdfId=[0-9]+'
         AND h.observacion ~ 'CondicionesId=[0-9]+' AND h.observacion ~ 'CondicionesPdfId=[0-9]+')
 ) THEN RAISE EXCEPTION 'Existen expedientes pendientes sin IDs exactos de envío. Corregir trazabilidad antes de desplegar.'; END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_aocr_historial_envio_documentos_dcav
 ON public.aocr_proceso_estado_historial(solicitud_id,fecha_creacion DESC,id DESC)
 WHERE accion='ENVIAR_DOCUMENTOS_DCAV';

CREATE INDEX IF NOT EXISTS ix_aocr_documentos_revision_dcav
 ON public.aocr_tbdocumento_generado(codigo_solicitud,codigo_inspeccion,estado,codigo_documento)
 WHERE vigente=TRUE AND eliminado=FALSE AND estado='ENVIADO_DCAV';

CREATE INDEX IF NOT EXISTS ix_aocr_pdf_documento_base
 ON public.aocr_tbdocumento_inspeccion(codigo_documento_base,codigo_documento);

COMMIT;

SELECT pe.solicitud_id,pe.inspeccion_id,pe.version,pe.fecha_estado
FROM public.aocr_proceso_estado pe
WHERE pe.activo=TRUE AND pe.estado_actual='PENDIENTE_REVISION_DOCUMENTOS_DCAV'
ORDER BY pe.fecha_estado;
