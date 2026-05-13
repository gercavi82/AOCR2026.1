SELECT 'aocr_tblog' AS tabla, *
FROM public.aocr_tblog
WHERE UPPER(COALESCE(modulo, '')) = 'MIRRORREADSERVICE'
   OR UPPER(COALESCE(mensaje, '')) LIKE '%MIRRORREADSERVICE%'
ORDER BY COALESCE(fecha, created_at, now()) DESC
LIMIT 20;
