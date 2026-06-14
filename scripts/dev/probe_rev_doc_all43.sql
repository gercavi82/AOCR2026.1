SELECT s.codigo_solicitud, s.estado, s.codigo_tecnico,
  (SELECT COUNT(*) FROM aocr_tbdocumento d WHERE d.codigo_solicitud = s.codigo_solicitud) AS docs,
  (SELECT COUNT(*) FROM aocr_tbrevision_documental r WHERE r.codigo_solicitud = s.codigo_solicitud AND UPPER(r.decision)='ACEPTADO') AS aceptados,
  (SELECT COUNT(*) FROM aocr_tbrevision_documental r WHERE r.codigo_solicitud = s.codigo_solicitud) AS revisiones
FROM aocr_tbsolicitud s
WHERE COALESCE(s.codigo_tecnico,0)=43 OR EXISTS (SELECT 1 FROM aocr_tbinspeccion i WHERE i.codigo_solicitud=s.codigo_solicitud AND i.codigo_inspector=43)
ORDER BY s.codigo_solicitud;
