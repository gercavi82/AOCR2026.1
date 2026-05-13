SELECT COUNT(*) AS total_mirror,
       COUNT(*) FILTER (WHERE UPPER(TRIM(COALESCE(estado,''))) = 'AC') AS activos
FROM public.aocr_tbinspectores;

SELECT cedula, nombre_completo, estado, tipo
FROM public.aocr_tbinspectores
ORDER BY nombre_completo;
