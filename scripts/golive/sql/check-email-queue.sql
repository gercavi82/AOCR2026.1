-- Evidencia cola correos pre/post go-live
SELECT COALESCE(status, estado, 'SIN_ESTADO') AS estado,
       COUNT(*) AS cantidad,
       MIN(created_at) AS mas_antiguo,
       MAX(created_at) AS mas_reciente
FROM email_queue
GROUP BY 1
ORDER BY 2 DESC;

SELECT COUNT(*) AS pendientes_sin_event_key
FROM email_queue
WHERE COALESCE(status, estado, '') IN ('PENDIENTE', 'PENDING')
  AND (event_key IS NULL OR TRIM(event_key) = '');

SELECT id, to_address, subject, COALESCE(status, estado) AS estado,
       event_key, created_at, intentos
FROM email_queue
WHERE COALESCE(status, estado, '') IN ('PENDIENTE', 'PENDING', 'ERROR')
ORDER BY created_at DESC
LIMIT 20;
