-- ========================================================
-- MIGRACIÓN: insertar_roles_faltantes.sql
-- Descripción: Inserta roles críticos faltantes para workflow AOCR completo
-- Autor: Sistema AOCR
-- Fecha: 2026-02-07
-- ========================================================

BEGIN;

-- ========================================================
-- 1. Verificar existencia de roles actuales
-- ========================================================
DO $$
BEGIN
    RAISE NOTICE 'Roles existentes antes de inserción:';
END $$;

SELECT codigorol, descripcion, activo 
FROM rol 
ORDER BY codigorol;

-- ========================================================
-- 2. Insertar roles faltantes (solo si NO existen)
-- ========================================================

-- Operador - Recepciona solicitudes
INSERT INTO rol (descripcion, activo)
SELECT 'Operador', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'Operador'
);

-- Evaluador Técnico - Evalúa documentación
INSERT INTO rol (descripcion, activo)
SELECT 'EvaluadorTecnico', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'EvaluadorTecnico'
);

-- Coordinador Legal - Aprueba aspectos legales
INSERT INTO rol (descripcion, activo)
SELECT 'CoordinadorLegal', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'CoordinadorLegal'
);

-- Coordinador Financiero - Aprueba aspectos financieros
INSERT INTO rol (descripcion, activo)
SELECT 'CoordinadorFinanciero', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'CoordinadorFinanciero'
);

-- Director Financiero - CRÍTICO: Aprobación final
INSERT INTO rol (descripcion, activo)
SELECT 'DirectorFinanciero', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'DirectorFinanciero'
);

-- Representante Legal - Representa al solicitante
INSERT INTO rol (descripcion, activo)
SELECT 'RepresentanteLegal', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'RepresentanteLegal'
);

-- Solicitante - Operador de aeronaves
INSERT INTO rol (descripcion, activo)
SELECT 'Solicitante', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'Solicitante'
);

-- Inspector - Realiza inspecciones (verificar si existe)
INSERT INTO rol (descripcion, activo)
SELECT 'Inspector', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'Inspector'
);

-- Coordinador de Inspecciones (verificar si existe)
INSERT INTO rol (descripcion, activo)
SELECT 'CoordinadorInspecciones', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'CoordinadorInspecciones'
);

-- Jefatura Técnica (verificar si existe)
INSERT INTO rol (descripcion, activo)
SELECT 'JefaturaTecnica', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'JefaturaTecnica'
);

-- Administrador (verificar si existe)
INSERT INTO rol (descripcion, activo)
SELECT 'Administrador', TRUE
WHERE NOT EXISTS (
    SELECT 1 FROM rol WHERE descripcion = 'Administrador'
);

-- ========================================================
-- 3. Agregar columna nivel_jerarquico si no existe
-- ========================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'rol' 
        AND column_name = 'nivel_jerarquico'
    ) THEN
        ALTER TABLE rol ADD COLUMN nivel_jerarquico INT DEFAULT 0;
        COMMENT ON COLUMN rol.nivel_jerarquico IS 'Nivel jerárquico: 1=Operativo, 2=Coordinación, 3=Dirección';
    END IF;
END $$;

-- ========================================================
-- 4. Actualizar niveles jerárquicos
-- ========================================================
UPDATE rol SET nivel_jerarquico = 3 WHERE descripcion IN ('Administrador');
UPDATE rol SET nivel_jerarquico = 3 WHERE descripcion IN ('DirectorFinanciero', 'JefaturaTecnica');
UPDATE rol SET nivel_jerarquico = 2 WHERE descripcion IN ('CoordinadorLegal', 'CoordinadorFinanciero', 'CoordinadorInspecciones');
UPDATE rol SET nivel_jerarquico = 1 WHERE descripcion IN ('Operador', 'EvaluadorTecnico', 'Inspector');
UPDATE rol SET nivel_jerarquico = 0 WHERE descripcion IN ('Solicitante', 'RepresentanteLegal');

-- ========================================================
-- 5. Agregar columna puede_aprobar si no existe
-- ========================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'rol' 
        AND column_name = 'puede_aprobar'
    ) THEN
        ALTER TABLE rol ADD COLUMN puede_aprobar BOOLEAN DEFAULT FALSE;
        COMMENT ON COLUMN rol.puede_aprobar IS 'Indica si el rol puede aprobar solicitudes';
    END IF;
END $$;

-- ========================================================
-- 6. Configurar permisos de aprobación
-- ========================================================
UPDATE rol SET puede_aprobar = TRUE WHERE descripcion IN (
    'Administrador',
    'DirectorFinanciero',
    'JefaturaTecnica',
    'CoordinadorLegal',
    'CoordinadorFinanciero',
    'CoordinadorInspecciones'
);

UPDATE rol SET puede_aprobar = FALSE WHERE descripcion IN (
    'Operador',
    'EvaluadorTecnico',
    'Inspector',
    'Solicitante',
    'RepresentanteLegal'
);

-- ========================================================
-- 7. Agregar columna categoria_rol si no existe
-- ========================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'rol' 
        AND column_name = 'categoria_rol'
    ) THEN
        ALTER TABLE rol ADD COLUMN categoria_rol VARCHAR(50);
        COMMENT ON COLUMN rol.categoria_rol IS 'Categoría: INTERNO, EXTERNO';
    END IF;
END $$;

-- ========================================================
-- 8. Categorizar roles
-- ========================================================
UPDATE rol SET categoria_rol = 'INTERNO' WHERE descripcion IN (
    'Administrador',
    'Operador',
    'EvaluadorTecnico',
    'Inspector',
    'CoordinadorInspecciones',
    'CoordinadorLegal',
    'CoordinadorFinanciero',
    'DirectorFinanciero',
    'JefaturaTecnica'
);

UPDATE rol SET categoria_rol = 'EXTERNO' WHERE descripcion IN (
    'Solicitante',
    'RepresentanteLegal'
);

-- ========================================================
-- 9. Crear vista resumen de roles
-- ========================================================
CREATE OR REPLACE VIEW vw_roles_resumen AS
SELECT 
    r.codigorol,
    r.descripcion,
    r.activo,
    r.nivel_jerarquico,
    r.puede_aprobar,
    r.categoria_rol,
    COUNT(ur.codigousuario) AS usuarios_asignados
FROM rol r
LEFT JOIN usuariorol ur ON ur.codigorol = r.codigorol
GROUP BY r.codigorol, r.descripcion, r.activo, r.nivel_jerarquico, r.puede_aprobar, r.categoria_rol
ORDER BY r.nivel_jerarquico DESC, r.descripcion;

COMMENT ON VIEW vw_roles_resumen IS 
'Vista con resumen de roles y cantidad de usuarios asignados';

-- ========================================================
-- 10. Verificar resultados
-- ========================================================
-- Ver todos los roles con sus atributos
SELECT 
    codigorol,
    descripcion,
    activo,
    nivel_jerarquico,
    puede_aprobar,
    categoria_rol
FROM rol
ORDER BY nivel_jerarquico DESC, descripcion;

-- Ver resumen con usuarios
SELECT * FROM vw_roles_resumen;

-- Contar roles por categoría
SELECT 
    categoria_rol,
    COUNT(*) AS cantidad_roles,
    COUNT(*) FILTER (WHERE activo = TRUE) AS roles_activos
FROM rol
GROUP BY categoria_rol;

-- Roles críticos para workflow
SELECT 
    descripcion,
    CASE 
        WHEN nivel_jerarquico = 3 THEN '🔴 Crítico - Dirección'
        WHEN nivel_jerarquico = 2 THEN '🟡 Importante - Coordinación'
        WHEN nivel_jerarquico = 1 THEN '🟢 Normal - Operativo'
        ELSE '⚪ Externo'
    END AS prioridad,
    puede_aprobar
FROM rol
WHERE activo = TRUE
ORDER BY nivel_jerarquico DESC;

COMMIT;

-- ========================================================
-- VERIFICACIÓN POST-MIGRACIÓN
-- ========================================================
DO $$
DECLARE
    rol_count INT;
    director_exists INT;
BEGIN
    -- Contar roles totales
    SELECT COUNT(*) INTO rol_count FROM rol WHERE activo = TRUE;
    RAISE NOTICE 'Total roles activos: %', rol_count;
    
    -- Verificar que Director Financiero existe
    SELECT COUNT(*) INTO director_exists 
    FROM rol 
    WHERE descripcion = 'DirectorFinanciero' AND activo = TRUE;
    
    IF director_exists = 0 THEN
        RAISE EXCEPTION '❌ CRÍTICO: Rol DirectorFinanciero no fue creado';
    ELSE
        RAISE NOTICE '✅ Rol DirectorFinanciero creado correctamente';
    END IF;
    
    -- Verificar Coordinadores
    SELECT COUNT(*) INTO director_exists 
    FROM rol 
    WHERE descripcion IN ('CoordinadorLegal', 'CoordinadorFinanciero') AND activo = TRUE;
    
    IF director_exists < 2 THEN
        RAISE WARNING '⚠️ Faltan roles de Coordinadores';
    ELSE
        RAISE NOTICE '✅ Roles de Coordinadores OK';
    END IF;
END $$;
