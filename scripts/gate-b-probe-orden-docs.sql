SELECT 'ORDEN' AS bloque, id, codigo_solicitud, numero_orden, estado, fecha_creacion, total
FROM aocr_or_orden WHERE codigo_solicitud::text = '12' ORDER BY id DESC LIMIT 3;

SELECT 'DOCS_ESTADO' AS bloque, COALESCE(estado,'<null>') AS estado, COUNT(*) AS cnt
FROM aocr_tbdocumento WHERE codigo_solicitud = 12 GROUP BY estado ORDER BY cnt DESC;

SELECT 'DOCS_DET' AS bloque, codigo_documento, tipo_documento, estado, version
FROM aocr_tbdocumento WHERE codigo_solicitud = 12 ORDER BY tipo_documento, version DESC LIMIT 15;

SELECT 'EMAIL_QUEUE' AS bloque, COUNT(*) AS pendientes
FROM aocr_tbemail_queue WHERE codigo_solicitud = 12;
