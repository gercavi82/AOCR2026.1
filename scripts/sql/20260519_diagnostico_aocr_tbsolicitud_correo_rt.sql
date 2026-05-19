-- Diagnóstico de esquema AOCR para la solicitud y el correo del RT.
-- Este script es solo de lectura. No altera tablas ni agrega columnas.
-- Regla canónica vigente: cuando exista, public.aocr_tbsolicitud.email es la fuente primaria
-- para el correo del representante técnico en el flujo AOCR.

SELECT
    'aocr_tbsolicitud' AS tabla,
    c.ordinal_position,
    c.column_name,
    c.data_type
FROM information_schema.columns c
WHERE c.table_schema = 'public'
  AND c.table_name = 'aocr_tbsolicitud'
ORDER BY c.ordinal_position;

SELECT
    c.table_name,
    c.column_name,
    c.data_type
FROM information_schema.columns c
WHERE c.table_schema = 'public'
  AND c.column_name ILIKE '%correo_representante%'
ORDER BY c.table_name, c.column_name;

SELECT
    c.table_name,
    c.column_name,
    c.data_type
FROM information_schema.columns c
WHERE c.table_schema = 'public'
  AND c.table_name = 'aocr_tbsolicitud'
  AND c.column_name IN (
        'email',
        'correo',
        'correo_electronico',
        'email_representante_tecnico',
        'correo_representante_tecnico',
        'email_representante',
        'correo_representante',
        'codigo_usuario',
        'codigo_tecnico'
      )
ORDER BY c.column_name;