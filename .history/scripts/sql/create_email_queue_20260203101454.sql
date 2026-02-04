-- Tabla para cola de correos electrónicos - ACTUALIZADO
CREATE TABLE IF NOT EXISTS email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'PENDIENTE',
    solicitud_id INTEGER REFERENCES aocr_tbsolicitud(codigo_solicitud),
    proximo_intento TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT chk_status_email CHECK (status IN ('PENDIENTE', 'ENVIANDO', 'ENVIADO', 'ERROR', 'CANCELADO'))
);

-- Índices para optimizar consultas
CREATE INDEX IF NOT EXISTS idx_email_queue_status ON email_queue(status, proximo_intento) WHERE status = 'PENDIENTE';
CREATE INDEX IF NOT EXISTS idx_email_queue_proximo_intento ON email_queue(proximo_intento);
CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud_id ON email_queue(solicitud_id);
CREATE INDEX IF NOT EXISTS idx_email_queue_created_at ON email_queue(created_at);

COMMENT ON TABLE email_queue IS 'Cola de correos electrónicos para envío asíncrono';
COMMENT ON COLUMN email_queue.status IS 'PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO';
COMMENT ON COLUMN email_queue.solicitud_id IS 'Referencia a la solicitud (FK a aocr_tbsolicitud)';
COMMENT ON COLUMN email_queue.created_at IS 'Fecha y hora de creación del registro';
