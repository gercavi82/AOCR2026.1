SELECT table_name FROM information_schema.tables WHERE table_schema='public' AND table_name ILIKE '%orden%' ORDER BY table_name;
SELECT codigo_inspeccion, estado, codigo_inspector, estado_documental FROM aocr_tbinspeccion WHERE codigo_solicitud = 12 ORDER BY codigo_inspeccion DESC LIMIT 3;
SELECT estado_anterior, estado_nuevo, observaciones, fecha_modificacion FROM aocr_tbhistorialestado WHERE codigo_solicitud = 12 ORDER BY codigo_historial DESC LIMIT 10;
SELECT COUNT(*) AS docs, SUM(CASE WHEN estado='VIGENTE' THEN 1 ELSE 0 END) AS vigentes FROM aocr_tbdocumento WHERE codigo_solicitud = 12;
