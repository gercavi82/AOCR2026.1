-- ============================================================================
-- SCRIPT DE MIGRACION: Formalizar estados de inspecciones
-- Fecha: 2026-02-07
-- Objetivo: Agregar constraint CHECK para validar estados de inspecciones
--           y actualizar registros existentes con estados antiguos
-- ============================================================================

BEGIN;

-- ============================================================================
-- PASO 1: Actualizar registros existentes con estados antiguos
-- ============================================================================

-- Actualizar estados existentes del sistema anterior a los nuevos formalizados
-- Estados antiguos: 'Programada', 'En Proceso', 'Completada', 'Cancelada'
UPDATE aocr_tbinspeccion 
SET estado = 'CREADA' 
WHERE estado IS NULL OR LOWER(TRIM(estado)) IN ('nueva', 'pendiente', '');

UPDATE aocr_tbinspeccion 
SET estado = 'PROGRAMADA' 
WHERE LOWER(TRIM(estado)) IN ('programada', 'asignada', 'scheduled');

UPDATE aocr_tbinspeccion 
SET estado = 'EN_CURSO' 
WHERE LOWER(TRIM(estado)) IN ('en curso', 'en proceso', 'en_proceso', 'iniciada', 'en progreso');

UPDATE aocr_tbinspeccion 
SET estado = 'FINALIZADA' 
WHERE LOWER(TRIM(estado)) IN ('finalizada', 'completada', 'terminada', 'finalizado');

UPDATE aocr_tbinspeccion 
SET estado = 'APROBADA' 
WHERE LOWER(TRIM(estado)) IN ('aprobada', 'aprobado', 'validada', 'validado');

UPDATE aocr_tbinspeccion 
SET estado = 'CERRADA' 
WHERE LOWER(TRIM(estado)) IN ('cerrada', 'cerrado', 'archivada', 'closed');

UPDATE aocr_tbinspeccion 
SET estado = 'CANCELADA' 
WHERE LOWER(TRIM(estado)) IN ('cancelada', 'cancelado', 'anulada', 'anulado');

-- Comentario sobre actualizaciones
COMMENT ON TABLE aocr_tbinspeccion IS 'Tabla de inspecciones tecnicas con estados formalizados (CREADA, PROGRAMADA, EN_CURSO, APLAZADA, FINALIZADA, APROBADA, RECHAZADA, CANCELADA, CERRADA)';


-- ============================================================================
-- PASO 2: Eliminar constraint antiguo si existe
-- ============================================================================

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'chk_estado_inspeccion' 
        AND conrelid = 'aocr_tbinspeccion'::regclass
    ) THEN
        ALTER TABLE aocr_tbinspeccion DROP CONSTRAINT chk_estado_inspeccion;
        RAISE NOTICE 'Constraint antiguo chk_estado_inspeccion eliminado';
    END IF;
END $$;


-- ============================================================================
-- PASO 3: Agregar nuevo CHECK constraint con estados formalizados
-- ============================================================================

ALTER TABLE aocr_tbinspeccion 
ADD CONSTRAINT chk_estado_inspeccion 
CHECK (estado IN (
    'CREADA',           -- Inspeccion creada, sin programar
    'PROGRAMADA',       -- Inspeccion programada con fecha/hora/lugar
    'EN_CURSO',         -- Inspector esta ejecutando la inspeccion
    'APLAZADA',         -- Inspector solicito aplazar la inspeccion
    'FINALIZADA',       -- Inspector completo trabajo y genero informe
    'APROBADA',         -- Informe revisado y aprobado por Jefatura Tecnica
    'RECHAZADA',        -- Informe rechazado, requiere correcciones
    'CANCELADA',        -- Inspeccion cancelada sin completar
    'CERRADA'           -- Inspeccion completamente cerrada (estado final)
));

COMMENT ON CONSTRAINT chk_estado_inspeccion ON aocr_tbinspeccion 
IS 'Valida que el estado sea uno de los 9 estados formalizados del flujo de inspecciones AOCR';


-- ============================================================================
-- PASO 4: Crear índice para mejorar performance en consultas por estado
-- ============================================================================

-- Eliminar índice antiguo si existe
DROP INDEX IF EXISTS idx_inspeccion_estado;

-- Crear nuevo índice
CREATE INDEX idx_inspeccion_estado ON aocr_tbinspeccion(estado) 
WHERE estado IS NOT NULL;

COMMENT ON INDEX idx_inspeccion_estado IS 'Indice para optimizar consultas de inspecciones por estado';


-- ============================================================================
-- PASO 5: Crear vista para dashboard de inspecciones por estado
-- ============================================================================

DROP VIEW IF EXISTS vw_inspecciones_por_estado;

CREATE OR REPLACE VIEW vw_inspecciones_por_estado AS
SELECT 
    estado,
    COUNT(*) AS total,
    COUNT(*) FILTER (WHERE fecha_programada IS NOT NULL) AS con_fecha,
    COUNT(*) FILTER (WHERE informe_pdf IS NOT NULL) AS con_informe,
    MAX(updated_at) AS ultima_actualizacion
FROM aocr_tbinspeccion
WHERE estado IS NOT NULL
GROUP BY estado
ORDER BY 
    CASE estado
        WHEN 'CREADA' THEN 1
        WHEN 'PROGRAMADA' THEN 2
        WHEN 'EN_CURSO' THEN 3
        WHEN 'APLAZADA' THEN 4
        WHEN 'FINALIZADA' THEN 5
        WHEN 'APROBADA' THEN 6
        WHEN 'RECHAZADA' THEN 7
        WHEN 'CANCELADA' THEN 8
        WHEN 'CERRADA' THEN 9
        ELSE 99
    END;

COMMENT ON VIEW vw_inspecciones_por_estado IS 'Vista de resumen con contadores de inspecciones agrupadas por estado para dashboard';


-- ============================================================================
-- PASO 6: Agregar columna de historial de transiciones (opcional)
-- ============================================================================

-- Verificar si la columna ya existe antes de crearla
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'aocr_tbinspeccion' 
        AND column_name = 'historial_estados'
    ) THEN
        ALTER TABLE aocr_tbinspeccion 
        ADD COLUMN historial_estados TEXT;
        
        COMMENT ON COLUMN aocr_tbinspeccion.historial_estados 
        IS 'JSON con historial de cambios de estado: [{fecha, usuario, estado_anterior, estado_nuevo, motivo}]';
        
        RAISE NOTICE 'Columna historial_estados agregada correctamente';
    ELSE
        RAISE NOTICE 'Columna historial_estados ya existe, omitiendo creacion';
    END IF;
END $$;


-- ============================================================================
-- PASO 7: Verificación final
-- ============================================================================

-- Contar inspecciones actualizadas
DO $$
DECLARE
    total_inspecciones INT;
    inspecciones_sin_estado INT;
BEGIN
    SELECT COUNT(*) INTO total_inspecciones FROM aocr_tbinspeccion;
    SELECT COUNT(*) INTO inspecciones_sin_estado FROM aocr_tbinspeccion WHERE estado IS NULL;
    
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'VERIFICACION FINAL:';
    RAISE NOTICE '============================================================';
    RAISE NOTICE 'Total inspecciones: %', total_inspecciones;
    RAISE NOTICE 'Inspecciones sin estado: %', inspecciones_sin_estado;
    
    IF inspecciones_sin_estado > 0 THEN
        RAISE WARNING 'Existen % inspecciones sin estado. Se debe asignar CREADA manualmente.', inspecciones_sin_estado;
    END IF;
END $$;

-- Mostrar resumen por estado
SELECT 
    estado,
    COUNT(*) AS cantidad,
    ROUND(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER(), 2) AS porcentaje
FROM aocr_tbinspeccion
WHERE estado IS NOT NULL
GROUP BY estado
ORDER BY cantidad DESC;

COMMENT ON COLUMN aocr_tbinspeccion.estado IS 'Estado actual de la inspeccion: CREADA | PROGRAMADA | EN_CURSO | APLAZADA | FINALIZADA | APROBADA | RECHAZADA | CANCELADA | CERRADA';

COMMIT;

-- ============================================================================
-- NOTAS DE IMPLEMENTACION
-- ============================================================================
-- 
-- Estados formalizados del flujo de inspecciones:
--
-- 1. CREADA → Inspección creada, aún no programada
-- 2. PROGRAMADA → Fecha/hora/lugar asignados
-- 3. EN_CURSO → Inspector ejecutando inspección en campo
-- 4. APLAZADA → Inspector solicitó aplazar (requiere reprogramación)
-- 5. FINALIZADA → Inspector completó y generó informe preliminar
-- 6. APROBADA → Jefatura Técnica aprobó el informe
-- 7. RECHAZADA → Jefatura rechazó informe (requiere correcciones)
-- 8. CANCELADA → Inspección cancelada sin completar (estado terminal)
-- 9. CERRADA → Inspección completamente cerrada (estado terminal)
--
-- Estados terminales (no permiten más transiciones):
-- - CANCELADA
-- - CERRADA
--
-- Transiciones válidas según EstadosInspeccion.cs:
-- CREADA → PROGRAMADA, CANCELADA
-- PROGRAMADA → EN_CURSO, APLAZADA, CANCELADA
-- EN_CURSO → FINALIZADA, APLAZADA, CANCELADA
-- APLAZADA → PROGRAMADA (reprogramar), CANCELADA
-- FINALIZADA → APROBADA, RECHAZADA
-- RECHAZADA → EN_CURSO (para corregir), FINALIZADA (re-entrega)
-- APROBADA → CERRADA
-- CANCELADA → (ninguna)
-- CERRADA → (ninguna)
--
-- Para ejecutar este script:
-- psql -h 172.20.16.55 -p 5432 -U root -d dgac_des -f migrate_estados_inspeccion.sql
--
-- ============================================================================
