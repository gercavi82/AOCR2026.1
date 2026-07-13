-- 003_diagnostico_consistencia.sql
-- Detecta inconsistencias entre las tablas legacy y el estado central

SELECT 
    s.codigo_solicitud,
    s.estado AS estado_legacy,
    pe.estado_actual AS estado_central
FROM public.aocr_tbsolicitud s
LEFT JOIN public.aocr_proceso_estado pe 
    ON pe.solicitud_id = s.codigo_solicitud AND pe.activo = TRUE
WHERE s.estado != pe.estado_actual
   OR pe.id IS NULL;
