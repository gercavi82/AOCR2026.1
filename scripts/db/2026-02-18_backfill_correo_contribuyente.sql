-- AOCR - Backfill de correo del contribuyente en ordenes
-- Fecha: 2026-02-18
-- Objetivo: rellenar aocr_or_orden.correo solo cuando este vacio, usando aocr_tbsolicitud.email
-- Seguridad: no sobreescribe correos existentes y evita fallos por tipos distintos (varchar/int)

UPDATE aocr_or_orden o
SET correo = TRIM(s.email)
FROM aocr_tbsolicitud s
WHERE
    CASE
        WHEN BTRIM(COALESCE(o.codigo_solicitud::text, '')) ~ '^[0-9]+$'
            THEN BTRIM(o.codigo_solicitud::text)::int
        ELSE NULL
    END = s.codigo_solicitud
  AND (o.correo IS NULL OR BTRIM(o.correo) = '')
  AND s.email IS NOT NULL
  AND BTRIM(s.email) <> '';
