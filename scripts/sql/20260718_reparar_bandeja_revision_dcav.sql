BEGIN;

-- Recupera solicitudes cuyo informe ya fue firmado y enviado a Dirección,
-- pero cuyo estado central activo quedó en una etapa anterior. La copia de
-- respaldo permite un rollback acotado sin perder el estado previo.
CREATE TABLE IF NOT EXISTS public.aocr_migration_backup_proceso_estado (
    migration_key VARCHAR(100) NOT NULL,
    proceso_estado_id BIGINT NOT NULL,
    solicitud_id INTEGER NOT NULL,
    inspeccion_id INTEGER NULL,
    estado_actual VARCHAR(100) NOT NULL,
    etapa_actual VARCHAR(100) NULL,
    rol_responsable VARCHAR(100) NOT NULL,
    observacion TEXT NULL,
    activo BOOLEAN NOT NULL,
    version BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL,
    created_by INTEGER NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    updated_by INTEGER NOT NULL,
    PRIMARY KEY (migration_key, proceso_estado_id)
);

CREATE TEMP TABLE tmp_dcav_pendientes_reparar ON COMMIT DROP AS
SELECT DISTINCT ON (i.codigo_solicitud)
       i.codigo_solicitud AS solicitud_id,
       i.codigo_inspeccion AS inspeccion_id,
       COALESCE(NULLIF(regexp_replace(COALESCE(inf.usuario_firma_1::text,''),'[^0-9]','','g'),''),'0')::INTEGER AS usuario_id,
       COALESCE(inf.fecha_envio_dirdac,inf.updated_at,inf.created_at,NOW()) AS fecha
FROM public.aocr_tbinspeccion i
JOIN public.aocr_tbsolicitud s
  ON s.codigo_solicitud=i.codigo_solicitud AND s.deleted_at IS NULL
JOIN public.aocr_tbinforme_inspeccion inf
  ON inf.codigo_inspeccion=i.codigo_inspeccion
WHERE inf.finalizado=TRUE
  AND inf.firmado_inspector=TRUE
  AND COALESCE(inf.firmado_dirdac,FALSE)=FALSE
  AND UPPER(TRIM(COALESCE(inf.resultado,'')))='SATISFACTORIO'
  AND NULLIF(TRIM(COALESCE(inf.ruta_documento_firmado,'')),'') IS NOT NULL
  AND regexp_replace(UPPER(COALESCE(inf.estado_informe,'')),'[\s_-]+','_','g') IN
      ('ENVIADO_A_DIRDAC','ENVIADO_A_DIRECCION','PENDIENTE_REVISION_DIRDAC',
       'PENDIENTE_REVISION_DIRECCION','PENDIENTE_REVISION_INSTITUCIONAL',
       -- Versiones afectadas por el error que confundía "firmado" con
       -- "enviado" y, por ello, nunca creaba el estado central de DIRDAC.
       'FIRMADO_INSPECTOR','FIRMADO_POR_INSPECTOR',
       'INFORME_TECNICO_FIRMADO_INSPECTOR')
ORDER BY i.codigo_solicitud,inf.version DESC,inf.codigo_informe DESC;

INSERT INTO public.aocr_migration_backup_proceso_estado
    (migration_key,proceso_estado_id,solicitud_id,inspeccion_id,estado_actual,
     etapa_actual,rol_responsable,observacion,activo,version,created_at,
     created_by,updated_at,updated_by)
SELECT '20260718_REPARAR_BANDEJA_DCAV',pe.id,pe.solicitud_id,pe.inspeccion_id,
       pe.estado_actual,pe.etapa_actual,pe.rol_responsable,pe.observacion,
       pe.activo,pe.version,pe.created_at,pe.created_by,pe.updated_at,pe.updated_by
FROM public.aocr_proceso_estado pe
JOIN tmp_dcav_pendientes_reparar c ON c.solicitud_id=pe.solicitud_id
WHERE pe.activo=TRUE
  AND pe.estado_actual<>'PENDIENTE_REVISION_INFORME_DCAV'
ON CONFLICT (migration_key,proceso_estado_id) DO NOTHING;

UPDATE public.aocr_proceso_estado pe
   SET activo=FALSE,
       updated_at=NOW(),
       updated_by=c.usuario_id
FROM tmp_dcav_pendientes_reparar c
WHERE pe.solicitud_id=c.solicitud_id
  AND pe.activo=TRUE
  AND pe.estado_actual<>'PENDIENTE_REVISION_INFORME_DCAV';

INSERT INTO public.aocr_proceso_estado
    (solicitud_id,inspeccion_id,estado_actual,etapa_actual,rol_responsable,
     observacion,activo,version,created_at,created_by,updated_at,updated_by)
SELECT c.solicitud_id,c.inspeccion_id,'PENDIENTE_REVISION_INFORME_DCAV',
       'REVISION_INFORME_DCAV','DirectorCertificacionesDcav',
       'REPARACION_20260718: informe firmado por Inspector pendiente de revisión DCAV.',
       TRUE,1,c.fecha,c.usuario_id,NOW(),c.usuario_id
FROM tmp_dcav_pendientes_reparar c
WHERE NOT EXISTS (
    SELECT 1
    FROM public.aocr_proceso_estado pe
    WHERE pe.solicitud_id=c.solicitud_id AND pe.activo=TRUE
);

COMMIT;
