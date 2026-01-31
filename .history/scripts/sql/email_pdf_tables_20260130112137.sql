-- Tablas para cola de correos y registro de PDF

-- Cola de correos
CREATE TABLE IF NOT EXISTS email_queue (
    id SERIAL PRIMARY KEY,
    para VARCHAR(255) NOT NULL,
    para_nombre VARCHAR(255),
    asunto VARCHAR(500) NOT NULL,
    cuerpo TEXT NOT NULL,
    es_html BOOLEAN DEFAULT true,
    adjunto_nombre VARCHAR(255),
    adjunto_contenido BYTEA,
    adjunto_mime_type VARCHAR(100),
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    intentos INTEGER DEFAULT 0,
    max_intentos INTEGER DEFAULT 3,
    ultimo_error TEXT,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_envio TIMESTAMP,
    proximo_intento TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    correlation_id VARCHAR(50),
    numero_orden VARCHAR(50),
    orden_id INTEGER,
    tipo_notificacion VARCHAR(50)
);

-- Índices para procesamiento eficiente
CREATE INDEX idx_email_queue_estado ON email_queue(estado, proximo_intento) WHERE estado = 'PENDIENTE';
CREATE INDEX idx_email_queue_orden ON email_queue(orden_id);
CREATE INDEX idx_email_queue_fecha ON email_queue(fecha_creacion);

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
