-- ========================================================
-- MIGRACIÓN: mejoras_tabla_notificaciones.sql
-- Descripción: Mejora tabla aocr_tbnotificacion con constraints, índices y triggers
-- Autor: Sistema AOCR
-- Fecha: 2026-02-07
-- ========================================================

BEGIN;

-- ========================================================
-- 1. Agregar CHECK constraint para tipo de notificación
-- ========================================================
COMMENT ON COLUMN aocr_tbnotificacion.tipo IS 'Tipo de notificación: INFO, SUCCESS, WARNING, ERROR';

DO $$
BEGIN
    -- Eliminar constraint anterior si existe
    IF EXISTS (
        SELECT 1 FROM information_schema.constraint_column_usage 
        WHERE table_name = 'aocr_tbnotificacion' 
        AND constraint_name = 'chk_notificacion_tipo'
    ) THEN
        ALTER TABLE aocr_tbnotificacion DROP CONSTRAINT chk_notificacion_tipo;
    END IF;
END $$;

-- Crear CHECK constraint con los 4 tipos válidos
ALTER TABLE aocr_tbnotificacion
ADD CONSTRAINT chk_notificacion_tipo
CHECK (tipo IN ('INFO', 'SUCCESS', 'WARNING', 'ERROR'));

-- ========================================================
-- 2. Crear índices para mejorar consultas
-- ========================================================

-- Índice compuesto para consultas de notificaciones no leídas por usuario
-- Útil para: ObtenerNoLeidas(codigoUsuario)
CREATE INDEX IF NOT EXISTS idx_notificacion_usuario_leida 
ON aocr_tbnotificacion(codigo_usuario, leida) 
WHERE leida = FALSE;

-- Índice compuesto para consultas por tipo
-- Útil para: ObtenerPorTipo(codigoUsuario, tipo)
CREATE INDEX IF NOT EXISTS idx_notificacion_usuario_tipo 
ON aocr_tbnotificacion(codigo_usuario, tipo);

-- Índice para ordenamiento por fecha
-- Útil para: ObtenerRecientes con ORDER BY created_at DESC
CREATE INDEX IF NOT EXISTS idx_notificacion_created_at 
ON aocr_tbnotificacion(created_at DESC);

-- ========================================================
-- 3. Crear función para limpiar notificaciones antiguas
-- ========================================================
CREATE OR REPLACE FUNCTION limpiar_notificaciones_antiguas(dias_antiguedad INT DEFAULT 90)
RETURNS INT AS $$
DECLARE
    registros_eliminados INT;
BEGIN
    DELETE FROM aocr_tbnotificacion
    WHERE leida = TRUE
      AND created_at < (CURRENT_TIMESTAMP - (dias_antiguedad || ' days')::INTERVAL);
    
    GET DIAGNOSTICS registros_eliminados = ROW_COUNT;
    
    RETURN registros_eliminados;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION limpiar_notificaciones_antiguas(INT) IS 
'Elimina notificaciones leídas con más de X días de antigüedad. Uso: SELECT limpiar_notificaciones_antiguas(90);';

-- ========================================================
-- 4. Crear vista resumen de notificaciones por tipo
-- ========================================================
CREATE OR REPLACE VIEW vw_notificaciones_resumen AS
SELECT 
    codigo_usuario,
    COUNT(*) AS total_notificaciones,
    COUNT(*) FILTER (WHERE leida = FALSE) AS no_leidas,
    COUNT(*) FILTER (WHERE leida = TRUE) AS leidas,
    COUNT(*) FILTER (WHERE tipo = 'INFO') AS tipo_info,
    COUNT(*) FILTER (WHERE tipo = 'SUCCESS') AS tipo_success,
    COUNT(*) FILTER (WHERE tipo = 'WARNING') AS tipo_warning,
    COUNT(*) FILTER (WHERE tipo = 'ERROR') AS tipo_error,
    MAX(created_at) AS ultima_notificacion
FROM aocr_tbnotificacion
GROUP BY codigo_usuario;

COMMENT ON VIEW vw_notificaciones_resumen IS 
'Vista con estadísticas de notificaciones por usuario';

-- ========================================================
-- 5. Normalizar tipos existentes (si hay datos legacy)
-- ========================================================
-- Normalizar valores a mayúsculas
UPDATE aocr_tbnotificacion 
SET tipo = UPPER(tipo)
WHERE tipo IN ('info', 'success', 'warning', 'error');

-- Valores nulos o no válidos → INFO por defecto
UPDATE aocr_tbnotificacion 
SET tipo = 'INFO'
WHERE tipo IS NULL 
   OR tipo NOT IN ('INFO', 'SUCCESS', 'WARNING', 'ERROR');

-- Hacer NOT NULL después de normalizar
ALTER TABLE aocr_tbnotificacion
ALTER COLUMN tipo SET NOT NULL;

-- ========================================================
-- 6. Trigger para validar usuario existe
-- ========================================================
CREATE OR REPLACE FUNCTION validar_usuario_notificacion()
RETURNS TRIGGER AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM usuario WHERE codigousuario = NEW.codigo_usuario) THEN
        RAISE EXCEPTION 'Usuario % no existe', NEW.codigo_usuario;
    END IF;
    
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_validar_usuario_notificacion ON aocr_tbnotificacion;
CREATE TRIGGER trg_validar_usuario_notificacion
BEFORE INSERT OR UPDATE ON aocr_tbnotificacion
FOR EACH ROW
EXECUTE FUNCTION validar_usuario_notificacion();

-- ========================================================
-- 7. Verificación de resultados
-- ========================================================
-- Verificar constraint CHECK
SELECT constraint_name, check_clause 
FROM information_schema.check_constraints
WHERE constraint_name = 'chk_notificacion_tipo';

-- Verificar índices creados
SELECT indexname, indexdef 
FROM pg_indexes 
WHERE tablename = 'aocr_tbnotificacion'
ORDER BY indexname;

-- Mostrar resumen de notificaciones (si hay datos)
SELECT * FROM vw_notificaciones_resumen
ORDER BY total_notificaciones DESC
LIMIT 10;

COMMIT;

-- ========================================================
-- PRUEBAS OPCIONALES (descomentar para probar)
-- ========================================================

-- Test 1: Insertar notificación válida
-- INSERT INTO aocr_tbnotificacion (codigo_usuario, titulo, mensaje, tipo)
-- VALUES (1, 'Test', 'Mensaje de prueba', 'INFO');

-- Test 2: Intentar insertar tipo inválido (debe fallar)
-- INSERT INTO aocr_tbnotificacion (codigo_usuario, titulo, mensaje, tipo)
-- VALUES (1, 'Test', 'Mensaje de prueba', 'INVALID'); -- ❌ Falla por CHECK

-- Test 3: Limpiar notificaciones antiguas
-- SELECT limpiar_notificaciones_antiguas(30); -- Elimina leídas con +30 días

-- Test 4: Ver resumen por usuario
-- SELECT * FROM vw_notificaciones_resumen WHERE codigo_usuario = 1;
