-- 2026-02-13: Extiende email_queue para cola financiera y adjuntos
-- Idempotente: se puede ejecutar varias veces.

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS orden_id INTEGER;

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS tipo_notificacion VARCHAR(80);

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(40);

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS intentos INTEGER DEFAULT 0;

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS max_intentos INTEGER DEFAULT 3;

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS ultimo_error TEXT;

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS fecha_envio TIMESTAMP;

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS adjunto_ruta TEXT;

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS adjunto_nombre VARCHAR(255);

ALTER TABLE IF EXISTS email_queue
    ADD COLUMN IF NOT EXISTS adjunto_mime VARCHAR(120);

COMMENT ON COLUMN email_queue.orden_id IS 'Referencia a orden de recaudacion (financiero)';
COMMENT ON COLUMN email_queue.tipo_notificacion IS 'Tipo de notificacion (PagoRecibido, OrdenFacturada, OrdenRechazada, etc.)';
COMMENT ON COLUMN email_queue.correlation_id IS 'Identificador de correlacion para trazabilidad';
COMMENT ON COLUMN email_queue.intentos IS 'Intentos de envio realizados';
COMMENT ON COLUMN email_queue.max_intentos IS 'Maximo de intentos permitidos';
COMMENT ON COLUMN email_queue.ultimo_error IS 'Ultimo error registrado al enviar';
COMMENT ON COLUMN email_queue.fecha_envio IS 'Fecha/hora de envio exitoso';
COMMENT ON COLUMN email_queue.adjunto_ruta IS 'Ruta fisica del adjunto (server)';
COMMENT ON COLUMN email_queue.adjunto_nombre IS 'Nombre de archivo del adjunto';
COMMENT ON COLUMN email_queue.adjunto_mime IS 'Mime type del adjunto';
