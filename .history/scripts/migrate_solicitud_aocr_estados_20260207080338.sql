-- ===================================================================
-- MIGRACIÓN: ESTADOS Y SUBSANACIONES SOLICITUD AOCR
-- ===================================================================
-- Propósito: 
--   1. Agregar columnas faltantes a aocr_tbsolicitud para workflow completo
--   2. Crear tabla aocr_tbsubsanacion para gestión de subsanaciones
--   3. Actualizar estados existentes para usar nuevas constantes
-- 
-- Diagrama de referencia: Ver ANALISIS_ESTADOS_ACTUAL.md
-- Constantes C#: CapaDatos\Constants\EstadosSolicitudAOCR.cs
-- 
-- Fecha: 2025-01-05
-- ===================================================================

BEGIN;

-- ===================================================================
-- PASO 0: ELIMINAR CONSTRAINT DE ESTADOS (para permitir nuevos estados)
-- ===================================================================

ALTER TABLE aocr_tbsolicitud 
DROP CONSTRAINT IF EXISTS chk_estado_solicitud;

-- ===================================================================
-- PASO 1: AGREGAR COLUMNAS A TABLA aocr_tbsolicitud
-- ===================================================================

-- Columnas para fechas de workflow
ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS fecha_recepcion TIMESTAMP NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS fecha_solicitud_subsanacion TIMESTAMP NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS fecha_subsanacion TIMESTAMP NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS fecha_aprobacion_coordinador TIMESTAMP NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS fecha_emision_aocr TIMESTAMP NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS fecha_entrega_aocr TIMESTAMP NULL;

-- Columnas para certificado AOCR
ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS numero_aocr VARCHAR(50) NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS ruta_archivo_pdf_aocr VARCHAR(500) NULL;

-- Columnas para usuarios de aprobación
ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS codigo_usuario_aprobacion_coordinador INTEGER NULL;

ALTER TABLE aocr_tbsolicitud 
ADD COLUMN IF NOT EXISTS codigo_usuario_aprobacion_director INTEGER NULL;

-- Comentarios descriptivos
COMMENT ON COLUMN aocr_tbsolicitud.fecha_recepcion IS 'Fecha formal de recepción de la solicitud (estado RECEPCIONADO)';
COMMENT ON COLUMN aocr_tbsolicitud.fecha_solicitud_subsanacion IS 'Fecha cuando se solicitó subsanación de documentos (estado SUBSANACION)';
COMMENT ON COLUMN aocr_tbsolicitud.fecha_subsanacion IS 'Fecha cuando el operador completó subsanación (estado SUBSANADO)';
COMMENT ON COLUMN aocr_tbsolicitud.fecha_aprobacion_coordinador IS 'Fecha de aprobación por Coordinador (estado EN_APROBACION_COORDINADOR)';
COMMENT ON COLUMN aocr_tbsolicitud.fecha_emision_aocr IS 'Fecha de emisión del certificado AOCR (estado AOCR_EMITIDO)';
COMMENT ON COLUMN aocr_tbsolicitud.fecha_entrega_aocr IS 'Fecha de entrega física del certificado (estado AOCR_ENTREGADO)';
COMMENT ON COLUMN aocr_tbsolicitud.numero_aocr IS 'Número único del certificado AOCR (ej: AOCR-2024-001)';
COMMENT ON COLUMN aocr_tbsolicitud.ruta_archivo_pdf_aocr IS 'Ruta del archivo PDF del certificado generado';

-- ===================================================================
-- PASO 2: CREAR TABLA aocr_tbsubsanacion
-- ===================================================================

CREATE TABLE IF NOT EXISTS aocr_tbsubsanacion (
    codigo_subsanacion SERIAL PRIMARY KEY,
    codigo_solicitud INTEGER NOT NULL,
    fecha_solicitud TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    observaciones TEXT NOT NULL,
    codigo_usuario_solicitante INTEGER NOT NULL,
    fecha_respuesta TIMESTAMP NULL,
    respuesta TEXT NULL,
    codigo_usuario_respuesta INTEGER NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NULL,
    created_by VARCHAR(100) NULL,
    updated_by VARCHAR(100) NULL,
    
    -- Foreign Keys
    CONSTRAINT fk_subsanacion_solicitud 
        FOREIGN KEY (codigo_solicitud) 
        REFERENCES aocr_tbsolicitud(codigo_solicitud) 
        ON DELETE CASCADE,
    
    CONSTRAINT fk_subsanacion_usuario_solicitante 
        FOREIGN KEY (codigo_usuario_solicitante) 
        REFERENCES aocr_tbusuario(codigo_usuario) 
        ON DELETE RESTRICT,
    
    CONSTRAINT fk_subsanacion_usuario_respuesta 
        FOREIGN KEY (codigo_usuario_respuesta) 
        REFERENCES aocr_tbusuario(codigo_usuario) 
        ON DELETE RESTRICT
);

-- Comentarios descriptivos
COMMENT ON TABLE aocr_tbsubsanacion IS 'Registro de solicitudes de subsanación de documentos en solicitudes AOCR';
COMMENT ON COLUMN aocr_tbsubsanacion.codigo_subsanacion IS 'Código único de subsanación';
COMMENT ON COLUMN aocr_tbsubsanacion.codigo_solicitud IS 'Solicitud relacionada';
COMMENT ON COLUMN aocr_tbsubsanacion.fecha_solicitud IS 'Fecha cuando se solicitó la subsanación';
COMMENT ON COLUMN aocr_tbsubsanacion.observaciones IS 'Descripción de documentos/requisitos a subsanar';
COMMENT ON COLUMN aocr_tbsubsanacion.codigo_usuario_solicitante IS 'Usuario técnico que solicita la subsanación';
COMMENT ON COLUMN aocr_tbsubsanacion.fecha_respuesta IS 'Fecha cuando el operador respondió/completó';
COMMENT ON COLUMN aocr_tbsubsanacion.respuesta IS 'Comentarios del operador al completar subsanación';
COMMENT ON COLUMN aocr_tbsubsanacion.codigo_usuario_respuesta IS 'Usuario operador que completó subsanación';
COMMENT ON COLUMN aocr_tbsubsanacion.estado IS 'Estado: PENDIENTE, COMPLETADA, VENCIDA';

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_subsanacion_solicitud ON aocr_tbsubsanacion(codigo_solicitud);
CREATE INDEX IF NOT EXISTS idx_subsanacion_estado ON aocr_tbsubsanacion(estado);
CREATE INDEX IF NOT EXISTS idx_subsanacion_fecha_solicitud ON aocr_tbsubsanacion(fecha_solicitud);

-- ===================================================================
-- PASO 3: CREAR TABLA aocr_tbdocumento_subsanacion
-- ===================================================================

CREATE TABLE IF NOT EXISTS aocr_tbdocumento_subsanacion (
    codigo_documento SERIAL PRIMARY KEY,
    codigo_subsanacion INTEGER NOT NULL,
    nombre_archivo VARCHAR(255) NOT NULL,
    ruta_archivo VARCHAR(500) NOT NULL,
    tipo_documento VARCHAR(100) NULL,
    tamanio_bytes INTEGER NULL,
    fecha_carga TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    codigo_usuario_carga INTEGER NOT NULL,
    
    -- Foreign Keys
    CONSTRAINT fk_doc_subsanacion 
        FOREIGN KEY (codigo_subsanacion) 
        REFERENCES aocr_tbsubsanacion(codigo_subsanacion) 
        ON DELETE CASCADE,
    
    CONSTRAINT fk_doc_usuario 
        FOREIGN KEY (codigo_usuario_carga) 
        REFERENCES aocr_tbusuario(codigo_usuario) 
        ON DELETE RESTRICT
);

COMMENT ON TABLE aocr_tbdocumento_subsanacion IS 'Documentos adjuntos cargados por operadores en respuesta a subsanaciones';
COMMENT ON COLUMN aocr_tbdocumento_subsanacion.codigo_subsanacion IS 'Subsanación relacionada';
COMMENT ON COLUMN aocr_tbdocumento_subsanacion.nombre_archivo IS 'Nombre original del archivo';
COMMENT ON COLUMN aocr_tbdocumento_subsanacion.ruta_archivo IS 'Ruta física/URL del documento';

CREATE INDEX IF NOT EXISTS idx_doc_subsanacion ON aocr_tbdocumento_subsanacion(codigo_subsanacion);

-- ===================================================================
-- PASO 4: ACTUALIZAR ESTADOS EXISTENTES
-- ===================================================================

-- Mapear estados antiguos a nuevos según constantes EstadosSolicitudAOCR
UPDATE aocr_tbsolicitud 
SET estado = 'RECEPCIONADO'
WHERE estado IN ('RECEPCIONADA', 'Recepcionada', 'recepcionada');

UPDATE aocr_tbsolicitud 
SET estado = 'ANALISIS_REQUISITOS'
WHERE estado IN ('EN_REVISION', 'EN REVISION', 'EnRevision', 'Revisión');

UPDATE aocr_tbsolicitud 
SET estado = 'EN_EVALUACION_TECNICA'
WHERE estado IN ('EN_EVALUACION', 'EN EVALUACION', 'Evaluación', 'EnEvaluacion');

UPDATE aocr_tbsolicitud 
SET estado = 'APROBADO'
WHERE estado IN ('APROBADA', 'Aprobada', 'APROBADO');

UPDATE aocr_tbsolicitud 
SET estado = 'RECHAZADO'
WHERE estado IN ('RECHAZADA', 'Rechazada', 'RECHAZADO', 'Denegada');

-- ===================================================================
-- PASO 5: FUNCIÓN DE AUDITORÍA PARA SUBSANACIONES
-- ===================================================================

CREATE OR REPLACE FUNCTION fn_audit_subsanacion()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'UPDATE') THEN
        NEW.updated_at = CURRENT_TIMESTAMP;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger para auditoría automática
DROP TRIGGER IF EXISTS trg_audit_subsanacion ON aocr_tbsubsanacion;
CREATE TRIGGER trg_audit_subsanacion
    BEFORE UPDATE ON aocr_tbsubsanacion
    FOR EACH ROW
    EXECUTE FUNCTION fn_audit_subsanacion();

-- ===================================================================
-- PASO 6: VISTA PARA CONSULTAS RÁPIDAS DE SUBSANACIONES
-- ===================================================================

CREATE OR REPLACE VIEW vw_subsanaciones_pendientes AS
SELECT 
    s.codigo_subsanacion,
    s.codigo_solicitud,
    sol.numero_solicitud,
    sol.nombre_operador,
    s.fecha_solicitud,
    s.observaciones,
    u_sol.nombre || ' ' || u_sol.apellido AS tecnico_solicitante,
    CASE 
        WHEN s.fecha_respuesta IS NULL THEN 
            EXTRACT(DAY FROM CURRENT_TIMESTAMP - s.fecha_solicitud)::INTEGER
        ELSE 0
    END AS dias_pendiente,
    s.estado
FROM aocr_tbsubsanacion s
INNER JOIN aocr_tbsolicitud sol ON s.codigo_solicitud = sol.codigo_solicitud
INNER JOIN aocr_tbusuario u_sol ON s.codigo_usuario_solicitante = u_sol.codigo_usuario
WHERE s.estado = 'PENDIENTE'
ORDER BY s.fecha_solicitud ASC;

COMMENT ON VIEW vw_subsanaciones_pendientes IS 'Vista rápida de subsanaciones pendientes con días transcurridos';

-- ===================================================================
-- PASO 7: DATOS INICIALES (OPCIONAL)
-- ===================================================================

-- Insertar registro de ejemplo solo si tabla está vacía
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM aocr_tbsubsanacion LIMIT 1) THEN
        -- Este es solo un ejemplo, eliminar en producción
        INSERT INTO aocr_tbsubsanacion 
            (codigo_solicitud, observaciones, codigo_usuario_solicitante, estado, created_by)
        VALUES 
            (1, 'Ejemplo: Falta certificado de matrícula de aeronave actualizado', 1, 'PENDIENTE', 'SYSTEM');
    END IF;
END $$;

COMMIT;

-- ===================================================================
-- VERIFICACIÓN POST-MIGRACIÓN
-- ===================================================================

-- Verificar columnas agregadas
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns
WHERE table_name = 'aocr_tbsolicitud'
AND column_name IN (
    'fecha_recepcion',
    'fecha_solicitud_subsanacion',
    'fecha_subsanacion',
    'fecha_aprobacion_coordinador',
    'fecha_emision_aocr',
    'fecha_entrega_aocr',
    'numero_aocr',
    'ruta_archivo_pdf_aocr'
)
ORDER BY ordinal_position;

-- Verificar tabla subsanaciones
SELECT COUNT(*) AS total_subsanaciones FROM aocr_tbsubsanacion;

-- Mostrar distribución de estados actualizados
SELECT estado, COUNT(*) AS total
FROM aocr_tbsolicitud
GROUP BY estado
ORDER BY total DESC;

-- ===================================================================
-- NOTAS DE IMPLEMENTACIÓN
-- ===================================================================

/*
SIGUIENTES PASOS:
1. ✅ Ejecutar este script en ambiente de desarrollo
2. ⏳ Actualizar EntityFramework DbContext con nuevas columnas
3. ⏳ Crear DAO para SubsanacionDAO en CapaDatos
4. ⏳ Crear clase de negocio SubsanacionBL en CapaNegocio
5. ⏳ Actualizar SolicitudAOCRController con acciones de subsanación
6. ⏳ Crear vistas Razor para solicitar/completar subsanaciones

ROLLBACK (si es necesario):
DROP VIEW IF EXISTS vw_subsanaciones_pendientes;
DROP TRIGGER IF EXISTS trg_audit_subsanacion ON aocr_tbsubsanacion;
DROP FUNCTION IF EXISTS fn_audit_subsanacion();
DROP TABLE IF EXISTS aocr_tbdocumento_subsanacion;
DROP TABLE IF EXISTS aocr_tbsubsanacion;

ALTER TABLE aocr_tbsolicitud 
DROP COLUMN IF EXISTS fecha_recepcion,
DROP COLUMN IF EXISTS fecha_solicitud_subsanacion,
DROP COLUMN IF EXISTS fecha_subsanacion,
DROP COLUMN IF EXISTS fecha_aprobacion_coordinador,
DROP COLUMN IF EXISTS fecha_emision_aocr,
DROP COLUMN IF EXISTS fecha_entrega_aocr,
DROP COLUMN IF EXISTS numero_aocr,
DROP COLUMN IF EXISTS ruta_archivo_pdf_aocr,
DROP COLUMN IF EXISTS codigo_usuario_aprobacion_coordinador,
DROP COLUMN IF EXISTS codigo_usuario_aprobacion_director;
*/
