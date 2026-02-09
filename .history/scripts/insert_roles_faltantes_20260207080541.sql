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
-- CREATE TABLE IF NOT EXISTS rol (
--     codigorol SERIAL PRIMARY KEY,
--     descripcion VARCHAR(200) NOT NULL,
--     activo BOOLEAN DEFAULT TRUE,
--     fecha_registro TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
--     usuariocreado VARCHAR(100),
--     fechacreado TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
--     usuariomodificado VARCHAR(100),
--     fechamodificado TIMESTAMP
-- );

-- ===================================================================
-- PASO 2: INSERTAR ROLES FALTANTES
-- ===================================================================

-- Rol 1: Recepción (Recibe y valida documentación inicial)
INSERT INTO rol (descripcion, activo, usuariocreado, fechacreado)
SELECT 'Recepcion', TRUE, 'SYSTEM', CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM rol 
    WHERE LOWER(descripcion) = 'recepcion'
);

-- Rol 2: Coordinador Legal (Aprueba evaluación legal)
INSERT INTO rol (descripcion, activo, usuariocreado, fechacreado)
SELECT 'CoordinadorLegal', TRUE, 'SYSTEM', CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM rol 
    WHERE LOWER(descripcion) = 'coordinadorlegal'
);

-- Rol 3: Coordinador Financiero (Aprueba evaluación financiera)
INSERT INTO rol (descripcion, activo, usuariocreado, fechacreado)
SELECT 'CoordinadorFinanciero', TRUE, 'SYSTEM', CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM rol 
    WHERE LOWER(descripcion) = 'coordinadorfinanciero'
);

-- Rol 4: Director Financiero (Aprobación final)
INSERT INTO rol (descripcion, activo, usuariocreado, fechacreado)
SELECT 'DirectorFinanciero', TRUE, 'SYSTEM', CURRENT_TIMESTAMP
WHERE NOT EXISTS (
    SELECT 1 FROM rol 
    WHERE LOWER(descripcion) = 'directorfinanciero'
);

-- ===================================================================
-- PASO 3: ASEGURAR ROLES EXISTENTES ESTÁN ACTIVOS
-- ===================================================================

UPDATE rol 
SET activo = TRUE
WHERE LOWER(descripcion) IN (
    'administrador', 
    'operador', 
    'tecnicoevaluador', 
    'coordinadortecnico',
    'jefefinanciero'
);

-- ===================================================================
-- PASO 4: CREAR PERMISOS BÁSICOS PARA ROLES NUEVOS (COMENTADO)
-- ===================================================================

-- NOTA: Los permisos se configurarán manualmente desde la aplicación
-- o mediante un script separado después de verificar la estructura
-- de la tabla 'permisos' en la base de datos

/*
-- Permisos para Recepción (solo lectura de solicitudes y creación de recepciones)
DO $$
DECLARE
    rol_recepcion_id INTEGER;
BEGIN
    SELECT codigorol INTO rol_recepcion_id 
    FROM rol 
    WHERE LOWER(descripcion) = 'recepcion'
    LIMIT 1;
    
    IF rol_recepcion_id IS NOT NULL THEN
        -- Ejemplo: Permiso para módulo Solicitudes (ajustar según estructura real)
        INSERT INTO permisos (codigorol, idmenu, idsubmenu, leer, crear, editar, eliminar, modulo)
        SELECT rol_recepcion_id, 1, 1, TRUE, TRUE, FALSE, FALSE, 'Solicitudes'
        WHERE NOT EXISTS (
            SELECT 1 FROM permisos 
            WHERE codigorol = rol_recepcion_id AND modulo = 'Solicitudes'
        );
    END IF;
END $$;

-- [Resto de permisos comentados...]
*/

-- ===================================================================
-- PASO 5: COMENTARIOS DESCRIPTIVOS
-- ===================================================================

COMMENT ON TABLE rol IS 'Roles del sistema AOCR para control de acceso basado en roles (RBAC)';

-- Descripción de roles:
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
    codigorol AS codigo_rol,
    descripcion,
    activo,
    fechacreado AS created_at
FROM rol
WHERE activo = TRUE
ORDER BY descripcion ASC;

-- Contar permisos por rol nuevo (comentado hasta configurar permisos)
/*
SELECT 
    r.descripcion AS rol,
    COUNT(p.codigorol) AS total_permisos
FROM rol r
LEFT JOIN permisos p ON r.codigorol = p.codigorol
WHERE LOWER(r.descripcion) IN ('recepcion', 'coordinadorlegal', 'coordinadorfinanciero', 'directorfinanciero')
GROUP BY r.descripcion
ORDER BY r.descripcion;
*/

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
