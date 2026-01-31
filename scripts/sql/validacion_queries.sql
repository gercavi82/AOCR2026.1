-- Queries de validación para correos y PDF

-- ============================================
-- VALIDACIÓN DE COLA DE CORREOS
-- ============================================

-- 1. Estado actual de la cola
SELECT 
    estado,
    COUNT(*) as cantidad,
    AVG(intentos)::numeric(10,2) as promedio_intentos,
    MIN(fecha_creacion) as mas_antiguo,
    MAX(fecha_creacion) as mas_reciente
FROM email_queue
GROUP BY estado
ORDER BY cantidad DESC;

-- 2. Correos con reintentos (auditoría)
SELECT 
    id,
    para,
    asunto,
    tipo_notificacion,
    numero_orden,
    intentos,
    max_intentos,
    ultimo_error,
    fecha_creacion,
    proximo_intento,
    correlation_id
FROM email_queue
WHERE intentos > 1
ORDER BY fecha_creacion DESC
LIMIT 50;

-- 3. Correos fallidos (para revisión)
SELECT 
    id,
    para,
    asunto,
    ultimo_error,
    intentos,
    fecha_creacion,
    correlation_id,
    numero_orden
FROM email_queue
WHERE estado = 'ERROR'
ORDER BY fecha_creacion DESC;

-- 4. Tiempo promedio de envío
SELECT 
    tipo_notificacion,
    COUNT(*) as total,
    AVG(EXTRACT(EPOCH FROM (fecha_envio - fecha_creacion)))::numeric(10,2) as segundos_promedio
FROM email_queue
WHERE estado = 'ENVIADO' AND fecha_envio IS NOT NULL
GROUP BY tipo_notificacion;

-- ============================================
-- VALIDACIÓN DE GENERACIÓN PDF
-- ============================================

-- 5. Estadísticas de generación PDF
SELECT 
    tipo_documento,
    COUNT(*) as total,
    SUM(CASE WHEN exitoso THEN 1 ELSE 0 END) as exitosos,
    SUM(CASE WHEN NOT exitoso THEN 1 ELSE 0 END) as fallidos,
    AVG(intentos)::numeric(10,2) as promedio_intentos,
    AVG(tamano_bytes)::bigint as tamano_promedio,
    AVG(EXTRACT(EPOCH FROM (fecha_fin - fecha_inicio)))::numeric(10,2) as segundos_promedio
FROM pdf_generaciones
GROUP BY tipo_documento;

-- 6. PDFs fallidos (para diagnóstico)
SELECT 
    id,
    tipo_documento,
    numero_referencia,
    error,
    intentos,
    fecha_inicio,
    fecha_fin
FROM pdf_generaciones
WHERE NOT exitoso
ORDER BY fecha_inicio DESC
LIMIT 50;

-- 7. Correlación correo-PDF por orden
SELECT 
    o.numero_orden,
    e.id as email_id,
    e.estado as email_estado,
    e.intentos as email_intentos,
    e.fecha_envio,
    p.id as pdf_id,
    p.exitoso as pdf_exitoso,
    p.tamano_bytes as pdf_tamano
FROM ordenes_recaudacion o
LEFT JOIN email_queue e ON e.orden_id = o.id
LEFT JOIN pdf_generaciones p ON p.entidad_id = o.id AND p.tipo_documento = 'ORDEN_RECAUDACION'
WHERE o.fecha_creacion > NOW() - INTERVAL '7 days'
ORDER BY o.fecha_creacion DESC;

-- ============================================
-- VALIDACIÓN DE NO-BLOQUEO
-- ============================================

-- 8. Verificar que correos se encolan rápido (< 100ms esperado)
-- Comparar fecha_creacion de email_queue con logs de la orden
SELECT 
    e.id,
    e.orden_id,
    e.fecha_creacion as email_encolado,
    o.fecha_creacion as orden_creada,
    EXTRACT(MILLISECONDS FROM (e.fecha_creacion - o.fecha_creacion)) as ms_diferencia
FROM email_queue e
JOIN ordenes_recaudacion o ON e.orden_id = o.id
WHERE e.fecha_creacion > NOW() - INTERVAL '1 day'
ORDER BY e.fecha_creacion DESC
LIMIT 20;
