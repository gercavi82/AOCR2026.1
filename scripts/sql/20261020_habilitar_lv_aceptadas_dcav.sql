-- Regulariza expedientes aceptados antes de habilitar la LV en la aceptación DCAV.
-- No acepta solicitudes pendientes, devueltas o con observaciones.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

CREATE TEMP TABLE cierre_dcav_regularizado ON COMMIT DROP AS
WITH cierre AS (
    UPDATE public.aocr_revision_documental_coordinador r
    SET estado='ACEPTADA_POR_DCAV',
        inspector_confirmado_id=COALESCE(NULLIF(s.codigo_tecnico,0),NULLIF(r.inspector_confirmado_id,0),r.inspector_original_id),
        fecha_habilitacion_lv=COALESCE(r.fecha_habilitacion_lv,NOW()),
        fecha_habilitacion_informe=COALESCE(r.fecha_habilitacion_informe,NOW()),
        fecha_actualizacion=NOW()
    FROM public.aocr_tbsolicitud s
    WHERE s.codigo_solicitud=r.solicitud_id AND r.activo=TRUE
      AND r.estado IN ('PENDIENTE_ACEPTACION_DCAV','ACEPTADA_POR_DCAV')
      AND s.estado IN ('DOCUMENTACION_ACEPTADA_DIRCAV','DESIGNACION_PENDIENTE_FIRMA_DIRCAV','DESIGNACION_FIRMADA_DIRCAV')
      AND COALESCE(s.estado_documental,'') <> 'OBSERVADO'
      AND COALESCE(NULLIF(s.codigo_tecnico,0),NULLIF(r.inspector_confirmado_id,0),r.inspector_original_id)>0
      AND NOT EXISTS (SELECT 1 FROM public.aocr_tbdocumento d
          WHERE d.codigo_solicitud=s.codigo_solicitud
            AND UPPER(d.estado) IN ('OBSERVADO','DEVUELTO_INSPECTOR')
            AND COALESCE((to_jsonb(d)->>'activo')::boolean,TRUE))
    RETURNING r.solicitud_id
)
SELECT solicitud_id FROM cierre;

UPDATE public.aocr_tbinspeccion i SET estado_documental='ACEPTADA'
FROM cierre_dcav_regularizado c WHERE i.codigo_solicitud=c.solicitud_id;
SELECT solicitud_id AS solicitud_regularizada FROM cierre_dcav_regularizado;
COMMIT;
