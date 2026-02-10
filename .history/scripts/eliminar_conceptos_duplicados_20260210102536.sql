-- Eliminar conceptos duplicados en aocr_or_concepto dejando solo el de menor id por cada código
DELETE FROM aocr_or_concepto a
USING aocr_or_concepto b
WHERE a.codigo = b.codigo
  AND a.id > b.id;

-- Opcional: dejar solo los activos
-- DELETE FROM aocr_or_concepto a
-- USING aocr_or_concepto b
-- WHERE a.codigo = b.codigo
--   AND a.id > b.id
--   AND a.activo = false;