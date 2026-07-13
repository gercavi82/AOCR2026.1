-- 002_migracion_estados_existentes.sql
-- Migra los estados actuales de aocr_tbsolicitud hacia aocr_proceso_estado
-- solo para aquellas solicitudes que no tienen estado centralizado aun.

INSERT INTO public.aocr_proceso_estado
(
    solicitud_id,
    estado_actual,
    etapa_actual,
    fecha_estado,
    activo,
    version
)
SELECT 
    s.codigo_solicitud,
    s.estado,
    'MIGRADO',
    NOW(),
    TRUE,
    1
FROM public.aocr_tbsolicitud s
LEFT JOIN public.aocr_proceso_estado pe 
    ON pe.solicitud_id = s.codigo_solicitud AND pe.activo = TRUE
WHERE pe.id IS NULL;
