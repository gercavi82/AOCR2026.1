SELECT codigo_solicitud, estado, COUNT(*) FROM (
  SELECT d.codigo_solicitud, UPPER(COALESCE(d.estado,'SIN_ESTADO')) AS estado
  FROM aocr_tbdocumento d
  WHERE d.codigo_solicitud IN (2,3,6,8,12,13)
) x GROUP BY codigo_solicitud, estado ORDER BY codigo_solicitud, estado;
