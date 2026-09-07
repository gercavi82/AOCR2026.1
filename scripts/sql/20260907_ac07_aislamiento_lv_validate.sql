-- Todos los siguientes controles deben devolver cero filas.
SELECT codigo_inspeccion, estacion_id, tipo_lista, count(*)
FROM public.aocr_tblv_operacional_eae WHERE vigente
GROUP BY codigo_inspeccion, estacion_id, tipo_lista HAVING count(*) > 1;

SELECT lv.codigo_lv FROM public.aocr_tblv_operacional_eae lv
LEFT JOIN public.aocr_tbinspeccion i ON i.codigo_inspeccion = lv.codigo_inspeccion
LEFT JOIN public.aocr_tbsolicitud_estacion e ON e.id = lv.estacion_id
WHERE i.codigo_inspeccion IS NULL OR lv.solicitud_id IS DISTINCT FROM i.codigo_solicitud
   OR (lv.estacion_id IS NOT NULL AND (e.id IS NULL OR e.solicitud_id <> i.codigo_solicitud
       OR (e.inspeccion_id IS NOT NULL AND e.inspeccion_id <> i.codigo_inspeccion)));

SELECT ruta_documento_firmado, count(DISTINCT codigo_lv)
FROM public.aocr_tblv_operacional_eae WHERE NULLIF(ruta_documento_firmado, '') IS NOT NULL
GROUP BY ruta_documento_firmado HAVING count(DISTINCT codigo_lv) > 1;
