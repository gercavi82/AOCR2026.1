SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name ILIKE '%historial%estado%' ORDER BY table_name;
SELECT column_name FROM information_schema.columns WHERE table_name='aocr_orden_recaudacion' ORDER BY ordinal_position LIMIT 25;
SELECT * FROM aocr_orden_recaudacion WHERE codigo_solicitud = 12 ORDER BY 1 DESC LIMIT 3;
SELECT column_name FROM information_schema.columns WHERE table_name='aocr_tbdocumento' ORDER BY ordinal_position LIMIT 20;
SELECT codigo_documento, tipo_documento, estado, nombre_archivo FROM aocr_tbdocumento WHERE codigo_solicitud = 12 LIMIT 10;
