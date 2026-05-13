SELECT 'aocr_tbauditoria' AS tabla, COALESCE(MAX(codigo_auditoria),0) AS max_id FROM public.aocr_tbauditoria;
SELECT 'aocr_tblog' AS tabla, COALESCE(MAX(codigo_log),0) AS max_id FROM public.aocr_tblog;
SELECT 'aocr_tbinspeccion' AS tabla, COALESCE(MAX(codigo_inspeccion),0) AS max_id FROM public.aocr_tbinspeccion;
SELECT 'aocr_tbsolicitud' AS tabla, COALESCE(MAX(codigo_solicitud),0) AS max_id FROM public.aocr_tbsolicitud;
