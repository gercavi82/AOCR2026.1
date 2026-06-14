-- Gate D — email idempotencia y contador financiero
SELECT tipo_notificacion, event_key, status, COUNT(*) AS cnt
FROM email_queue
WHERE UPPER(COALESCE(tipo_notificacion,'')) IN ('PAGO_APROBADO','SOLICITUD_AOCR_HABILITADA','PAGO_APROBADO_FINANCIERO')
   OR UPPER(COALESCE(subject,'')) LIKE '%PAGO APROBADO%'
GROUP BY tipo_notificacion, event_key, status
ORDER BY cnt DESC, tipo_notificacion
LIMIT 30;

SELECT id, tipo_notificacion, event_key, status, solicitud_id, orden_id, created_at, LEFT(subject,70) AS subject
FROM email_queue
WHERE event_key IS NULL AND created_at >= NOW() - INTERVAL '30 days'
ORDER BY id DESC
LIMIT 15;

SELECT id, tipo_notificacion, event_key, status, solicitud_id, created_at
FROM email_queue
WHERE event_key IS NOT NULL
  AND tipo_notificacion IN ('PAGO_APROBADO','SOLICITUD_AOCR_HABILITADA')
ORDER BY id DESC
LIMIT 10;

-- Órdenes pendientes gestión financiera (misma lógica helper)
SELECT o.id, o.numero_orden, o.estado AS estado_orden, p.estado AS estado_pago, o.codigo_solicitud
FROM aocr_or_orden o
LEFT JOIN LATERAL (
  SELECT estado FROM aocr_or_pago WHERE orden_id = o.id ORDER BY id DESC LIMIT 1
) p ON TRUE
WHERE UPPER(TRIM(COALESCE(o.estado,''))) NOT IN ('ANULADA','FACTURADA','PAGADA','COMPLETADA','DEVUELTA')
  AND UPPER(TRIM(COALESCE(o.estado,''))) IN ('EN_REVISION_FINANCIERA','ENVIADA','GENERADA','PENDIENTE','EN REVISION FINANCIERA')
ORDER BY o.id DESC
LIMIT 15;

-- NC / inspecciones no satisfactorias
SELECT i.codigo_inspeccion, i.codigo_solicitud, i.estado, inf.resultado, inf.tipo_resultado_insatisfactorio, inf.firmado_inspector
FROM aocr_tbinspeccion i
LEFT JOIN aocr_tbinforme_inspeccion inf ON inf.codigo_inspeccion = i.codigo_inspeccion
WHERE UPPER(COALESCE(i.estado,'')) LIKE '%NO_SATISFACTORIO%'
   OR UPPER(COALESCE(inf.resultado,'')) LIKE '%INSATISFACTORIO%'
ORDER BY i.codigo_inspeccion DESC
LIMIT 10;

SELECT h.codigo_hallazgo, h.codigo_inspeccion, h.estado, LEFT(h.descripcion,80) AS descripcion
FROM aocr_tbhallazgo h
JOIN aocr_tbinspeccion i ON i.codigo_inspeccion = h.codigo_inspeccion
ORDER BY h.codigo_hallazgo DESC
LIMIT 10;
