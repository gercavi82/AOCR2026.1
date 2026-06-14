SELECT id, event_key, status, subject, created_at, tipo_notificacion
FROM email_queue WHERE solicitud_id = 12 ORDER BY id DESC LIMIT 12;
