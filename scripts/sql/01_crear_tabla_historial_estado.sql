-- Tabla de historial de estados de solicitudes AOCR
-- Resuelve error: 42P01 relation "aocr_tbhistorialestado" does not exist

CREATE TABLE IF NOT EXISTS aocr_tbhistorialestado (
    id SERIAL PRIMARY KEY,
    codigo_solicitud INTEGER NOT NULL,
    estado_anterior VARCHAR(50),
    estado_nuevo VARCHAR(50) NOT NULL,
    fecha_cambio TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    codigo_usuario INTEGER,
    observaciones TEXT,
    CONSTRAINT fk_historial_solicitud FOREIGN KEY (codigo_solicitud) 
        REFERENCES aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE
);

-- Índices para optimizar consultas
CREATE INDEX idx_historial_solicitud ON aocr_tbhistorialestado(codigo_solicitud);
CREATE INDEX idx_historial_fecha ON aocr_tbhistorialestado(fecha_cambio);

COMMENT ON TABLE aocr_tbhistorialestado IS 'Historial de cambios de estado de solicitudes AOCR';
