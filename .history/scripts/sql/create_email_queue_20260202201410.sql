-- Tabla para cola de correos electrónicos
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
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    fecha_envio TIMESTAMP,
    proximo_intento TIMESTAMP,
    mensaje_error TEXT,
    correlation_id VARCHAR(50),
    numero_orden VARCHAR(50),
    orden_id INTEGER,
    tipo_notificacion VARCHAR(50),
    CONSTRAINT chk_estado_email CHECK (estado IN ('PENDIENTE', 'PROCESANDO', 'ENVIADO', 'ERROR', 'CANCELADO'))
);

-- Índices para optimizar consultas
CREATE INDEX IF NOT EXISTS idx_email_queue_estado ON email_queue(estado);
CREATE INDEX IF NOT EXISTS idx_email_queue_proximo_intento ON email_queue(proximo_intento);
CREATE INDEX IF NOT EXISTS idx_email_queue_correlation_id ON email_queue(correlation_id);
CREATE INDEX IF NOT EXISTS idx_email_queue_orden_id ON email_queue(orden_id);

COMMENT ON TABLE email_queue IS 'Cola de correos electrónicos para envío asíncrono';
COMMENT ON COLUMN email_queue.estado IS 'PENDIENTE, PROCESANDO, ENVIADO, ERROR, CANCELADO';
COMMENT ON COLUMN email_queue.intentos IS 'Número de intentos de envío realizados';
COMMENT ON COLUMN email_queue.max_intentos IS 'Número máximo de intentos antes de marcar como ERROR';
