BEGIN;

-- Sólo revierte expedientes que todavía permanecen en el estado creado por
-- la reparación. Si el trámite ya avanzó, no altera su estado vigente.
CREATE TEMP TABLE tmp_dcav_reparacion_rollback ON COMMIT DROP AS
SELECT pe.solicitud_id
FROM public.aocr_proceso_estado pe
WHERE pe.activo=TRUE
  AND pe.estado_actual='PENDIENTE_REVISION_INFORME_DCAV'
  AND pe.observacion LIKE 'REPARACION_20260718:%';

DELETE FROM public.aocr_proceso_estado pe
USING tmp_dcav_reparacion_rollback r
WHERE pe.solicitud_id=r.solicitud_id
  AND pe.activo=TRUE
  AND pe.estado_actual='PENDIENTE_REVISION_INFORME_DCAV'
  AND pe.observacion LIKE 'REPARACION_20260718:%';

UPDATE public.aocr_proceso_estado pe
   SET inspeccion_id=b.inspeccion_id,
       estado_actual=b.estado_actual,
       etapa_actual=b.etapa_actual,
       rol_responsable=b.rol_responsable,
       observacion=b.observacion,
       activo=b.activo,
       version=b.version,
       created_at=b.created_at,
       created_by=b.created_by,
       updated_at=b.updated_at,
       updated_by=b.updated_by
FROM public.aocr_migration_backup_proceso_estado b
JOIN tmp_dcav_reparacion_rollback r ON r.solicitud_id=b.solicitud_id
WHERE b.migration_key='20260718_REPARAR_BANDEJA_DCAV'
  AND pe.id=b.proceso_estado_id;

DELETE FROM public.aocr_migration_backup_proceso_estado b
USING tmp_dcav_reparacion_rollback r
WHERE b.migration_key='20260718_REPARAR_BANDEJA_DCAV'
  AND b.solicitud_id=r.solicitud_id;

COMMIT;
