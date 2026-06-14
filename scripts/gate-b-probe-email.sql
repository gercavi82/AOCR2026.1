SELECT 'ORDEN' AS bloque, id, codigo_solicitud, numero_orden, estado, fecha_creacion, total
FROM aocr_or_orden WHERE codigo_solicitud::text = '12' ORDER BY id DESC LIMIT 3;

SELECT 'EMAIL' AS bloque, id, event_key, estado, LEFT(asunto,80) AS asunto, created_at
FROM email_queue WHERE codigo_solicitud = 12 ORDER BY id DESC LIMIT 10;
