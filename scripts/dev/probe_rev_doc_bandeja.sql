SELECT s.codigo_solicitud, s.numero_solicitud, s.estado, s.codigo_tecnico, s.pago_aprobado
FROM aocr_tbsolicitud s WHERE s.codigo_solicitud = 12;
SELECT i.codigo_inspeccion, i.codigo_inspector, i.estado FROM aocr_tbinspeccion i WHERE i.codigo_solicitud = 12;
SELECT decision, COUNT(*) FROM aocr_tbrevision_documental WHERE codigo_solicitud = 12 GROUP BY decision;
SELECT DISTINCT s.codigo_solicitud, s.estado, s.codigo_tecnico
FROM aocr_tbsolicitud s
LEFT JOIN aocr_tbinspeccion i ON i.codigo_solicitud = s.codigo_solicitud
WHERE COALESCE(i.codigo_inspector, 0) = 43 OR COALESCE(s.codigo_tecnico, 0) = 43;
