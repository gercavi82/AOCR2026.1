-- =====================================================
-- Script para crear la tabla de cola de correos
-- Base de datos: PostgreSQL (dgac_des)
-- =====================================================

-- Crear la tabla email_queue - ACTUALIZADO para coincidir con base de datos real
CREATE TABLE IF NOT EXISTS public.email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(20) DEFAULT 'PENDIENTE',
    solicitud_id INTEGER REFERENCES public.aocr_tbsolicitud(codigo_solicitud),
    proximo_intento TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Crear índices para mejorar el rendimiento
CREATE INDEX IF NOT EXISTS idx_email_queue_status 
    ON public.email_queue(status, proximo_intento) WHERE status = 'PENDIENTE';

CREATE INDEX IF NOT EXISTS idx_email_queue_created_at 
    ON public.email_queue(created_at);

CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud_id 
    ON public.email_queue(solicitud_id);

CREATE INDEX IF NOT EXISTS idx_email_queue_proximo_intento 
    ON public.email_queue(proximo_intento);

-- Comentarios de la tabla
COMMENT ON TABLE public.email_queue IS 'Cola de correos electrónicos para procesamiento asíncrono';
COMMENT ON COLUMN public.email_queue.id IS 'Identificador único del correo en cola';
COMMENT ON COLUMN public.email_queue.to_address IS 'Dirección de correo del destinatario';
COMMENT ON COLUMN public.email_queue.subject IS 'Asunto del correo electrónico';
COMMENT ON COLUMN public.email_queue.body IS 'Contenido del mensaje en formato HTML';
COMMENT ON COLUMN public.email_queue.status IS 'Estado: PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO';
COMMENT ON COLUMN public.email_queue.solicitud_id IS 'Referencia a la solicitud (FK a aocr_tbsolicitud)';
COMMENT ON COLUMN public.email_queue.created_at IS 'Fecha y hora de creación del registro';
COMMENT ON COLUMN public.email_queue.estado IS 'Estado del correo: Pendiente, Enviado, Error, Reintentando';
COMMENT ON COLUMN public.email_queue.intentos IS 'Número de intentos de envío realizados';
COMMENT ON COLUMN public.email_queue.max_intentos IS 'Número máximo de intentos permitidos';
COMMENT ON COLUMN public.email_queue.fecha_creacion IS 'Fecha y hora de creación del registro';
COMMENT ON COLUMN public.email_queue.fecha_envio IS 'Fecha y hora del último intento de envío';
COMMENT ON COLUMN public.email_queue.fecha_procesado IS 'Fecha y hora en que se procesó exitosamente';
COMMENT ON COLUMN public.email_queue.error_mensaje IS 'Mensaje de error del último intento fallido';
COMMENT ON COLUMN public.email_queue.correlation_id IS 'ID de correlación para seguimiento';
COMMENT ON COLUMN public.email_queue.orden_id IS 'ID de la orden de recaudación asociada (si aplica)';
COMMENT ON COLUMN public.email_queue.tipo_notificacion IS 'Tipo de notificación: NuevaOrden, PagoRecibido, CambioEstado, etc.';
COMMENT ON COLUMN public.email_queue.prioridad IS 'Prioridad del correo (1=más alta, 10=más baja)';
COMMENT ON COLUMN public.email_queue.fecha_programado IS 'Fecha programada para envío (opcional)';

-- Insertar registro de prueba (opcional - comentar si no deseas insertar)
-- INSERT INTO public.email_queue (para, asunto, cuerpo, estado)
-- VALUES ('test@aviacioncivil.gob.ec', 'Correo de Prueba', '<h1>Prueba del Sistema</h1>', 'Pendiente');

-- Verificar que la tabla se creó correctamente
SELECT 
    table_name, 
    column_name, 
    data_type, 
    is_nullable
FROM 
    information_schema.columns
WHERE 
    table_schema = 'public' 
    AND table_name = 'email_queue'
ORDER BY 
    ordinal_position;

-- Mensaje de confirmación
DO $$
BEGIN
    RAISE NOTICE 'Tabla email_queue creada exitosamente';
END $$;
