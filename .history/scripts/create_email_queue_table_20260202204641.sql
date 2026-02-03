-- =====================================================
-- Script para crear la tabla de cola de correos
-- Base de datos: PostgreSQL (dgac_des)
-- =====================================================

-- Crear la tabla email_queue
CREATE TABLE IF NOT EXISTS public.email_queue (
    id SERIAL PRIMARY KEY,
    para VARCHAR(500) NOT NULL,
    cc VARCHAR(500),
    cco VARCHAR(500),
    asunto VARCHAR(500) NOT NULL,
    cuerpo TEXT NOT NULL,
    es_html BOOLEAN DEFAULT true,
    estado VARCHAR(50) NOT NULL DEFAULT 'Pendiente',
    intentos INTEGER DEFAULT 0,
    max_intentos INTEGER DEFAULT 3,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_envio TIMESTAMP,
    fecha_procesado TIMESTAMP,
    error_mensaje TEXT,
    correlation_id VARCHAR(50),
    orden_id INTEGER,
    tipo_notificacion VARCHAR(100),
    prioridad INTEGER DEFAULT 5,
    fecha_programado TIMESTAMP
);

-- Crear índices para mejorar el rendimiento
CREATE INDEX IF NOT EXISTS idx_email_queue_estado 
    ON public.email_queue(estado);

CREATE INDEX IF NOT EXISTS idx_email_queue_fecha_creacion 
    ON public.email_queue(fecha_creacion);

CREATE INDEX IF NOT EXISTS idx_email_queue_orden_id 
    ON public.email_queue(orden_id);

CREATE INDEX IF NOT EXISTS idx_email_queue_correlation_id 
    ON public.email_queue(correlation_id);

-- Comentarios de la tabla
COMMENT ON TABLE public.email_queue IS 'Cola de correos electrónicos para procesamiento asíncrono';
COMMENT ON COLUMN public.email_queue.id IS 'Identificador único del correo en cola';
COMMENT ON COLUMN public.email_queue.para IS 'Direcciones de correo destinatarios (separadas por coma si son múltiples)';
COMMENT ON COLUMN public.email_queue.cc IS 'Direcciones de correo en copia';
COMMENT ON COLUMN public.email_queue.cco IS 'Direcciones de correo en copia oculta';
COMMENT ON COLUMN public.email_queue.asunto IS 'Asunto del correo';
COMMENT ON COLUMN public.email_queue.cuerpo IS 'Contenido del mensaje (puede ser HTML o texto plano)';
COMMENT ON COLUMN public.email_queue.es_html IS 'Indica si el cuerpo del mensaje es HTML';
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
