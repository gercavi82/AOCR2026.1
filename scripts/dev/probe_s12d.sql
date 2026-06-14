SELECT estado_anterior, estado_nuevo, observaciones, fecha_modificacion FROM aocr_tbhistorial_estado WHERE codigo_solicitud = 12 ORDER BY codigo_historial DESC LIMIT 15;
SELECT id, numero_or, estado, codigo_solicitud FROM ordenes_recaudacion WHERE codigo_solicitud = 12 ORDER BY id DESC LIMIT 3;
SELECT table_name, column_name FROM information_schema.columns WHERE table_schema='public' AND column_name ILIKE '%revision%' AND table_name ILIKE '%document%' ORDER BY table_name LIMIT 30;
