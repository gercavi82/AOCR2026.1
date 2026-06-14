SELECT column_name FROM information_schema.columns WHERE table_name='aocr_tbhistorial_estado' ORDER BY ordinal_position;
SELECT * FROM aocr_tbhistorial_estado WHERE codigo_solicitud = 12 ORDER BY codigo_historial DESC LIMIT 12;
SELECT id, numero_or, estado FROM ordenes_recaudacion WHERE codigo_solicitud = 12 ORDER BY id DESC LIMIT 5;
