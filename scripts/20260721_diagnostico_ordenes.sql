-- -----------------------------------------------------------------------------
-- DIAGNÓSTICO PREVIO DE ÓRDENES Y NUMERACIÓN AOCR
-- -----------------------------------------------------------------------------

-- 1. Órdenes con número repetido
SELECT numero_orden, COUNT(*) AS total_repetidos
FROM public.aocr_or_orden
WHERE numero_orden IS NOT NULL AND numero_orden != ''
GROUP BY numero_orden
HAVING COUNT(*) > 1;

-- 2. Órdenes sin compañía
SELECT id, numero_orden, codigo_usuario, compania, fecha_creacion, estado
FROM public.aocr_or_orden
WHERE compania IS NULL OR TRIM(compania) = '';

-- 3. Órdenes sin número de orden
SELECT id, codigo_usuario, codigo_solicitud, compania, fecha_creacion, estado
FROM public.aocr_or_orden
WHERE numero_orden IS NULL OR TRIM(numero_orden) = ''
ORDER BY fecha_creacion DESC;

-- 4. Correlativo máximo actual por año en la tabla aocr_or_orden
SELECT 
    CAST(regexp_replace(numero_orden, '^DGAC-OR-(\d{4})-AOCR.*$', '\1') AS INTEGER) AS anio,
    MAX(CAST(regexp_replace(numero_orden, '.*AOCR0*', '') AS INTEGER)) AS ultimo_correlativo,
    COUNT(*) AS total_ordenes
FROM public.aocr_or_orden
WHERE numero_orden ~ '^DGAC-OR-\d{4}-AOCR\d+$'
GROUP BY CAST(regexp_replace(numero_orden, '^DGAC-OR-(\d{4})-AOCR.*$', '\1') AS INTEGER)
ORDER BY anio DESC;
