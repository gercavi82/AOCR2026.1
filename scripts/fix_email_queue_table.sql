-- =====================================================
-- Script para corregir la tabla email_queue
-- Agrega columna faltante y ajusta valores de estado
-- Base de datos: PostgreSQL (dgac_des)
-- =====================================================

-- 1. Agregar columna proximo_intento (requerida por el procesador)
ALTER TABLE public.email_queue 
ADD COLUMN IF NOT EXISTS proximo_intento TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

-- 2. Agregar columna solicitud_id para vincular con codigo_solicitud de la tabla padre
ALTER TABLE public.email_queue 
ADD COLUMN IF NOT EXISTS solicitud_id VARCHAR(50);

-- 3. Crear índices para mejorar rendimiento
CREATE INDEX IF NOT EXISTS idx_email_queue_proximo_intento 
    ON public.email_queue(proximo_intento);

CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud_id 
    ON public.email_queue(solicitud_id);

-- 4. Actualizar valores de estado a mayúsculas (el código espera PENDIENTE, ENVIADO, ERROR, ENVIANDO)
UPDATE public.email_queue SET estado = 'PENDIENTE' WHERE estado = 'Pendiente';
UPDATE public.email_queue SET estado = 'ENVIADO' WHERE estado = 'Enviado';
UPDATE public.email_queue SET estado = 'ERROR' WHERE estado = 'Error';
UPDATE public.email_queue SET estado = 'ENVIANDO' WHERE estado = 'Enviando';
UPDATE public.email_queue SET estado = 'REINTENTANDO' WHERE estado = 'Reintentando';

-- 5. Cambiar el valor por defecto de estado a mayúsculas
ALTER TABLE public.email_queue 
ALTER COLUMN estado SET DEFAULT 'PENDIENTE';

-- 6. Actualizar comentario de la columna estado
COMMENT ON COLUMN public.email_queue.estado IS 'Estado del correo: PENDIENTE, ENVIADO, ERROR, ENVIANDO, REINTENTANDO (valores en mayúsculas)';

-- 7. Agregar comentarios a las nuevas columnas
COMMENT ON COLUMN public.email_queue.proximo_intento IS 'Fecha y hora del próximo intento de envío (para lógica de reintentos)';
COMMENT ON COLUMN public.email_queue.solicitud_id IS 'Código de solicitud (codigo_solicitud) para vincular con la tabla padre';

-- Verificar las columnas actualizadas
SELECT 
    column_name, 
    data_type, 
    column_default,
    is_nullable
FROM 
    information_schema.columns
WHERE 
    table_schema = 'public' 
    AND table_name = 'email_queue', 'solicitud_id'
    AND column_name IN ('estado', 'proximo_intento')
ORDER BY 
    ordinal_position;

-- Verificar registros existentes
SELECT 
    COUNT(*) as total_registros,
    estado,
    COUNT(*) as cantidad
FROM 
    public.email_queue
GROUP BY 
    estado;

-- Mensaje de confirmación
DO $$
BEGIN
    RAISE NOTICE 'Tabla email_queue corregida exitosamente';
    RAISE NOTICE 'Columna solicitud_id agregada para vincular con codigo_solicitud';
    RAISE NOTICE 'Columna proximo_intento agregada';
    RAISE NOTICE 'Estados actualizados a mayúsculas';
END $$;
