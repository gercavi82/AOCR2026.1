-- Tablas para cola de correos y registro de PDF

-- Cola de correos - ACTUALIZADO para coincidir con base de datos real
CREATE TABLE IF NOT EXISTS email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'PENDIENTE',
    solicitud_id INTEGER REFERENCES aocr_tbsolicitud(codigo_solicitud),
    proximo_intento TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índices para procesamiento eficiente
CREATE INDEX IF NOT EXISTS idx_email_queue_status ON email_queue(status, proximo_intento) WHERE status = 'PENDIENTE';
CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud ON email_queue(solicitud_id);
CREATE INDEX IF NOT EXISTS idx_email_queue_created ON email_queue(created_at);

-- Registro de generación de PDF
CREATE TABLE IF NOT EXISTS pdf_generaciones (
    id SERIAL PRIMARY KEY,
    tipo_documento VARCHAR(50) NOT NULL,
    entidad_id INTEGER NOT NULL,
    numero_referencia VARCHAR(50),
    fecha_inicio TIMESTAMP NOT NULL,
    fecha_fin TIMESTAMP,
    exitoso BOOLEAN NOT NULL,
    error TEXT,
    intentos INTEGER DEFAULT 1,
    tamano_bytes BIGINT DEFAULT 0
);

CREATE INDEX idx_pdf_gen_tipo ON pdf_generaciones(tipo_documento, entidad_id);
CREATE INDEX idx_pdf_gen_fecha ON pdf_generaciones(fecha_inicio);

-- Vista de estadísticas de cola
CREATE OR REPLACE VIEW vw_email_queue_stats AS
SELECT 
    estado,
    COUNT(*) as cantidad,
    AVG(intentos) as promedio_intentos,
    MIN(fecha_creacion) as mas_antiguo
FROM email_queue
GROUP BY estado;

-- Función para limpiar correos antiguos
CREATE OR REPLACE FUNCTION fn_limpiar_email_queue(dias INTEGER DEFAULT 30)
RETURNS INTEGER AS $$
DECLARE
    eliminados INTEGER;
BEGIN
    DELETE FROM email_queue 
    WHERE estado IN ('ENVIADO', 'CANCELADO') 
      AND fecha_creacion < NOW() - (dias || ' days')::INTERVAL;
    GET DIAGNOSTICS eliminados = ROW_COUNT;
    RETURN eliminados;
END;
$$ LANGUAGE plpgsql;
