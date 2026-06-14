SELECT DISTINCT s.codigo_solicitud, s.estado, s.codigo_tecnico, i.codigo_inspector
FROM aocr_tbsolicitud s
LEFT JOIN aocr_tbinspeccion i ON i.codigo_solicitud = s.codigo_solicitud
WHERE s.codigo_solicitud IS NOT NULL
  AND s.deleted_at IS NULL
  AND UPPER(COALESCE(s.estado, '')) NOT IN ('ANULADA', 'CANCELADA')
  AND ((COALESCE(i.codigo_inspector, 0) = ANY(ARRAY[43]) OR COALESCE(s.codigo_tecnico, 0) = ANY(ARRAY[43])))
ORDER BY s.codigo_solicitud DESC;
