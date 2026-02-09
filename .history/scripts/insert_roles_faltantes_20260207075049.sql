-- ===================================================================
-- INSERCIÓN DE ROLES FALTANTES PARA WORKFLOW AOCR COMPLETO
-- ===================================================================
-- Propósito:
--   Agregar los roles faltantes identificados en auditoría del sistema
--   para soportar el flujo completo de Solicitudes AOCR según diagramas
--
-- Roles actuales (ya existentes):
--   - Administrador
--   - Operador
--   - TecnicoEvaluador
--   - CoordinadorTecnico
--   - JefeFinanciero
--
-- Roles faltantes (críticos para workflow completo):
--   1. Recepcion: Recepciona y verifica documentación inicial
--   2. CoordinadorLegal: Aprueba evaluación legal
--   3. CoordinadorFinanciero: Aprueba liquidación financiera
--   4. DirectorFinanciero: Aprobación final antes de emisión AOCR
--
-- Referencia: AUDITORIA_COMPLETA.md - Sección "7. Roles del Sistema"
-- Fecha: 2025-01-05
-- ===================================================================

BEGIN;

-- ===================================================================
-- PASO 1: VERIFICAR ESTRUCTURA DE TABLA
-- ===================================================================

-- Asumiendo estructura estándar:
-- CREATE TABLE IF NOT EXISTS aocr_tbrol (
--     codigo_rol SERIAL PRIMARY KEY,
--     descripcion VARCHAR(100) NOT NULL,
--     activo BOOLEAN DEFAULT TRUE,
--     created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
--     updated_at TIMESTAMP NULL
-- );

-- ===================================================================
-- PASO 2: INSERTAR ROLES FALTANTES
-- ===================================================================

-- Rol 1: Recepción (Recibe y valida documentación inicial)
INSERT INTO aocr_tbrol (descripcion, activo, created_at)
SELECT 'Recepcion', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'recepcion'
);

-- Rol 2: Coordinador Legal (Aprueba evaluación legal)
INSERT INTO aocr_tbrol (descripcion, activo, created_at)
SELECT 'CoordinadorLegal', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'coordinadorlegal'
);

-- Rol 3: Coordinador Financiero (Aprueba evaluación financiera)
INSERT INTO aocr_tbrol (descripcion, activo, created_at)
SELECT 'CoordinadorFinanciero', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'coordinadorfinanciero'
);

-- Rol 4: Director Financiero (Aprobación final)
INSERT INTO aocr_tbrol (descripcion, activo, created_at)
SELECT 'DirectorFinanciero', TRUE, CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'directorfinanciero'
);

-- ===================================================================
-- PASO 3: ASEGURAR ROLES EXISTENTES ESTÁN ACTIVOS
-- ===================================================================

UPDATE aocr_tbrol 
SET activo = TRUE
WHERE LOWER(descripcion) IN (
    'administrador', 
    'operador', 
    'tecnicoevaluador', 
    'coordinadortecnico',
    'jefefinanciero'
);

-- ===================================================================
-- PASO 4: CREAR PERMISOS BÁSICOS PARA ROLES NUEVOS
-- ===================================================================

-- NOTA: Ajustar id_menu e id_submenu según tu tabla aocr_tbmenu/aocr_tbsubmenu
-- Este es un template básico, debes personalizarlo según tu estructura

-- Permisos para Recepción (solo lectura de solicitudes y creación de recepciones)
DO $$
DECLARE
    rol_recepcion_id INTEGER;
BEGIN
    SELECT codigo_rol INTO rol_recepcion_id 
    FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'recepcion'
    LIMIT 1;
    
    IF rol_recepcion_id IS NOT NULL THEN
        -- Ejemplo: Permiso para módulo Solicitudes (ajustar idmenu/idsubmenu)
        INSERT INTO aocr_tbpermiso (codigorol, idmenu, idsubmenu, leer, crear, editar, eliminar, modulo)
        SELECT rol_recepcion_id, 1, 1, TRUE, TRUE, FALSE, FALSE, 'Solicitudes'
        WHERE NOT EXISTS (
            SELECT 1 FROM aocr_tbpermiso 
            WHERE codigorol = rol_recepcion_id AND modulo = 'Solicitudes'
        );
    END IF;
END $$;

-- Permisos para Coordinador Legal (lectura/edición de evaluaciones legales)
DO $$
DECLARE
    rol_coord_legal_id INTEGER;
BEGIN
    SELECT codigo_rol INTO rol_coord_legal_id 
    FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'coordinadorlegal'
    LIMIT 1;
    
    IF rol_coord_legal_id IS NOT NULL THEN
        -- Ejemplo: Permiso para módulo Evaluaciones
        INSERT INTO aocr_tbpermiso (codigorol, idmenu, idsubmenu, leer, crear, editar, eliminar, modulo)
        SELECT rol_coord_legal_id, 2, 2, TRUE, FALSE, TRUE, FALSE, 'Evaluaciones'
        WHERE NOT EXISTS (
            SELECT 1 FROM aocr_tbpermiso 
            WHERE codigorol = rol_coord_legal_id AND modulo = 'Evaluaciones'
        );
    END IF;
END $$;

-- Permisos para Coordinador Financiero (lectura/edición de evaluaciones financieras)
DO $$
DECLARE
    rol_coord_fin_id INTEGER;
BEGIN
    SELECT codigo_rol INTO rol_coord_fin_id 
    FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'coordinadorfinanciero'
    LIMIT 1;
    
    IF rol_coord_fin_id IS NOT NULL THEN
        -- Ejemplo: Permiso para módulo Financiero
        INSERT INTO aocr_tbpermiso (codigorol, idmenu, idsubmenu, leer, crear, editar, eliminar, modulo)
        SELECT rol_coord_fin_id, 3, 3, TRUE, FALSE, TRUE, FALSE, 'Financiero'
        WHERE NOT EXISTS (
            SELECT 1 FROM aocr_tbpermiso 
            WHERE codigorol = rol_coord_fin_id AND modulo = 'Financiero'
        );
    END IF;
END $$;

-- Permisos para Director Financiero (aprobación final de solicitudes)
DO $$
DECLARE
    rol_director_id INTEGER;
BEGIN
    SELECT codigo_rol INTO rol_director_id 
    FROM aocr_tbrol 
    WHERE LOWER(descripcion) = 'directorfinanciero'
    LIMIT 1;
    
    IF rol_director_id IS NOT NULL THEN
        -- Ejemplo: Permiso total para módulo Aprobaciones
        INSERT INTO aocr_tbpermiso (codigorol, idmenu, idsubmenu, leer, crear, editar, eliminar, modulo)
        SELECT rol_director_id, 4, 4, TRUE, TRUE, TRUE, FALSE, 'Aprobaciones'
        WHERE NOT EXISTS (
            SELECT 1 FROM aocr_tbpermiso 
            WHERE codigorol = rol_director_id AND modulo = 'Aprobaciones'
        );
    END IF;
END $$;

-- ===================================================================
-- PASO 5: COMENTARIOS DESCRIPTIVOS
-- ===================================================================

COMMENT ON TABLE aocr_tbrol IS 'Roles del sistema AOCR para control de acceso basado en roles (RBAC)';

-- Si la tabla soporta comentarios en filas (PostgreSQL no soporta nativamente, pero podemos documentar aquí)
-- Recepcion: Recepciona solicitudes, verifica documentación inicial, genera código de recepción
-- CoordinadorLegal: Revisa y aprueba evaluación legal de solicitudes
-- CoordinadorFinanciero: Revisa y aprueba liquidación financiera de pagos
-- DirectorFinanciero: Aprobación final antes de emisión de certificado AOCR

COMMIT;

-- ===================================================================
-- VERIFICACIÓN POST-INSERCIÓN
-- ===================================================================

-- Listar todos los roles activos
SELECT 
    codigo_rol,
    descripcion,
    activo,
    created_at
FROM aocr_tbrol
WHERE activo = TRUE
ORDER BY descripcion ASC;

-- Contar permisos por rol nuevo
SELECT 
    r.descripcion AS rol,
    COUNT(p.codigorol) AS total_permisos
FROM aocr_tbrol r
LEFT JOIN aocr_tbpermiso p ON r.codigo_rol = p.codigorol
WHERE LOWER(r.descripcion) IN ('recepcion', 'coordinadorlegal', 'coordinadorfinanciero', 'directorfinanciero')
GROUP BY r.descripcion
ORDER BY r.descripcion;

-- ===================================================================
-- MATRIZ DE RESPONSABILIDADES (DOCUMENTACIÓN)
-- ===================================================================

/*
MATRIZ DE ESTADOS Y ROLES:

Estado Solicitud AOCR          | Rol Responsable
------------------------------ | ---------------------------
RECEPCIONADO                   | Recepcion
ANALISIS_REQUISITOS            | TecnicoEvaluador
SUBSANACION                    | Operador (responde), TecnicoEvaluador (valida)
SUBSANADO                      | TecnicoEvaluador
EN_EVALUACION_TECNICA          | TecnicoEvaluador
EN_EVALUACION_LEGAL            | CoordinadorLegal (NUEVO)
EN_EVALUACION_FINANCIERA       | CoordinadorFinanciero (NUEVO)
EN_APROBACION_COORDINADOR      | CoordinadorTecnico
EN_APROBACION_DIRECTOR         | DirectorFinanciero (NUEVO)
APROBADO                       | DirectorFinanciero
RECHAZADO                      | Cualquier rol con permisos de aprobación
AOCR_EMITIDO                   | Administrador
AOCR_ENTREGADO                 | Recepcion

PERMISOS SUGERIDOS POR ROL:

Recepcion:
  - Solicitudes: Leer, Crear
  - Dashboard: Leer
  - Documentos: Leer

CoordinadorLegal:
  - Solicitudes: Leer, Editar
  - Evaluaciones: Leer, Editar
  - Documentos: Leer

CoordinadorFinanciero:
  - Solicitudes: Leer, Editar
  - Financiero: Leer, Editar
  - Órdenes: Leer, Editar
  - Pagos: Leer

DirectorFinanciero:
  - Solicitudes: Leer, Editar
  - Aprobaciones: Leer, Crear, Editar
  - Financiero: Leer
  - Reportes: Leer, Crear
*/

-- ===================================================================
-- ROLLBACK (si es necesario)
-- ===================================================================

/*
DELETE FROM aocr_tbpermiso 
WHERE codigorol IN (
    SELECT codigo_rol FROM aocr_tbrol 
    WHERE LOWER(descripcion) IN ('recepcion', 'coordinadorlegal', 'coordinadorfinanciero', 'directorfinanciero')
);

DELETE FROM aocr_tbrol 
WHERE LOWER(descripcion) IN ('recepcion', 'coordinadorlegal', 'coordinadorfinanciero', 'directorfinanciero');
*/
