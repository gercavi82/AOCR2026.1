-- Tablas de auditoría para AOCR

-- Tabla de auditoría de cambios de estado
CREATE TABLE IF NOT EXISTS audit_cambios_estado (
    id SERIAL PRIMARY KEY,
    tipo_entidad VARCHAR(50) NOT NULL,
    entidad_id INTEGER NOT NULL,
    numero_referencia VARCHAR(50),
    estado_anterior VARCHAR(50),
    estado_nuevo VARCHAR(50) NOT NULL,
    usuario VARCHAR(100) NOT NULL,
    motivo TEXT,
    ip_origen VARCHAR(50),
    correlation_id VARCHAR(50),
    fecha_cambio TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Índices para consultas frecuentes
CREATE INDEX idx_audit_estado_entidad ON audit_cambios_estado(tipo_entidad, entidad_id);
CREATE INDEX idx_audit_estado_fecha ON audit_cambios_estado(fecha_cambio);
CREATE INDEX idx_audit_estado_usuario ON audit_cambios_estado(usuario);
CREATE INDEX idx_audit_estado_correlation ON audit_cambios_estado(correlation_id);

-- Tabla de auditoría de acciones generales
CREATE TABLE IF NOT EXISTS audit_acciones (
    id SERIAL PRIMARY KEY,
    tipo_accion VARCHAR(50) NOT NULL,
    tipo_entidad VARCHAR(50),
    entidad_id INTEGER,
    descripcion TEXT,
    usuario VARCHAR(100) NOT NULL,
    datos_anteriores JSONB,
    datos_nuevos JSONB,
    ip_origen VARCHAR(50),
    correlation_id VARCHAR(50),
    fecha_accion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Índices
CREATE INDEX idx_audit_acciones_tipo ON audit_acciones(tipo_accion);
CREATE INDEX idx_audit_acciones_fecha ON audit_acciones(fecha_accion);
CREATE INDEX idx_audit_acciones_usuario ON audit_acciones(usuario);
CREATE INDEX idx_audit_acciones_entidad ON audit_acciones(tipo_entidad, entidad_id);

-- Vista para consulta de historial de órdenes
CREATE OR REPLACE VIEW vw_historial_ordenes AS
SELECT 
    a.id,
    a.entidad_id AS orden_id,
    a.numero_referencia AS numero_orden,
    a.estado_anterior,
    a.estado_nuevo,
    a.usuario,
    a.motivo,
    a.fecha_cambio,
    a.ip_origen,
    a.correlation_id
FROM audit_cambios_estado a
WHERE a.tipo_entidad = 'ORDEN'
ORDER BY a.fecha_cambio DESC;

-- Vista para consulta de historial de pagos
CREATE OR REPLACE VIEW vw_historial_pagos AS
SELECT 
    a.id,
    a.entidad_id AS pago_id,
    a.numero_referencia AS numero_comprobante,
    a.estado_anterior,
    a.estado_nuevo,
    a.usuario,
    a.motivo,
    a.fecha_cambio,
    a.ip_origen,
    a.correlation_id
FROM audit_cambios_estado a
WHERE a.tipo_entidad = 'PAGO'
ORDER BY a.fecha_cambio DESC;

-- Función para obtener historial completo de una orden
CREATE OR REPLACE FUNCTION fn_historial_orden(p_orden_id INTEGER)
RETURNS TABLE (
    fecha TIMESTAMP,
    tipo VARCHAR,
    estado_anterior VARCHAR,
    estado_nuevo VARCHAR,
    usuario VARCHAR,
    motivo TEXT,
    correlation_id VARCHAR
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        a.fecha_cambio,
        a.tipo_entidad,
        a.estado_anterior,
        a.estado_nuevo,
        a.usuario,
        a.motivo,
        a.correlation_id
    FROM audit_cambios_estado a
    WHERE (a.tipo_entidad = 'ORDEN' AND a.entidad_id = p_orden_id)
       OR (a.tipo_entidad = 'PAGO' AND a.entidad_id IN (
           SELECT p.id FROM pagos p WHERE p.orden_id = p_orden_id
       ))
    ORDER BY a.fecha_cambio;
END;
$$ LANGUAGE plpgsql;
