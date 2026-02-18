-- Tabla para cola de correos electrónicos - ACTUALIZADO
CREATE TABLE IF NOT EXISTS email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'PENDIENTE',
    solicitud_id INTEGER REFERENCES aocr_tbsolicitud(codigo_solicitud),
    orden_id INTEGER,
    tipo_notificacion VARCHAR(80),
    correlation_id VARCHAR(100),
    event_key VARCHAR(180),
    rol_origen VARCHAR(100),
    estado_final VARCHAR(50),
    intentos INTEGER DEFAULT 0,
    max_intentos INTEGER DEFAULT 3,
    ultimo_error TEXT,
    fecha_envio TIMESTAMP,
    adjunto_ruta TEXT,
    adjunto_nombre VARCHAR(255),
    adjunto_mime VARCHAR(120),
    proximo_intento TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_status_email CHECK (status IN ('PENDIENTE', 'ENVIANDO', 'ENVIADO', 'ERROR', 'CANCELADO'))
);

-- Índices para optimizar consultas
CREATE INDEX IF NOT EXISTS idx_email_queue_status ON email_queue(status, proximo_intento) WHERE status = 'PENDIENTE';
CREATE INDEX IF NOT EXISTS idx_email_queue_proximo_intento ON email_queue(proximo_intento);
CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud_id ON email_queue(solicitud_id);
CREATE INDEX IF NOT EXISTS idx_email_queue_orden_id ON email_queue(orden_id);
CREATE INDEX IF NOT EXISTS idx_email_queue_created_at ON email_queue(created_at);
CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key ON email_queue(event_key) WHERE event_key IS NOT NULL;

COMMENT ON TABLE email_queue IS 'Cola de correos electrónicos para envío asíncrono';
COMMENT ON COLUMN email_queue.status IS 'PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO';
COMMENT ON COLUMN email_queue.solicitud_id IS 'Referencia a la solicitud (FK a aocr_tbsolicitud)';
COMMENT ON COLUMN email_queue.created_at IS 'Fecha y hora de creación del registro';
COMMENT ON COLUMN email_queue.event_key IS 'Clave idempotente por evento de negocio';
