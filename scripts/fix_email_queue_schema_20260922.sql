-- =====================================================
-- Fix para email_queue schema - 2026-09-22
-- Problema: Tabla sin columnas status y error_message
-- =====================================================

-- 1. Verificar estructura actual
DO $$
BEGIN
    RAISE NOTICE 'Verificando estructura de email_queue...';
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'email_queue') THEN
        RAISE NOTICE 'Tabla email_queue existe';
    ELSE
        RAISE NOTICE 'Tabla email_queue NO existe - crearla';
    END IF;
END $$;

-- 2. Crear tabla si no existe
CREATE TABLE IF NOT EXISTS public.email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    solicitud_id INTEGER NULL,
    orden_id INTEGER NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    proximo_intento TIMESTAMP NOT NULL DEFAULT NOW()
);

-- 3. Agregar columnas faltantes (IF NOT EXISTS)
ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS status VARCHAR(20) DEFAULT 'PENDIENTE';

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS event_key VARCHAR(200);

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS error_message TEXT;

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS intentos INTEGER NOT NULL DEFAULT 0;

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS tipo_notificacion VARCHAR(120);

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(64);

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS message_id VARCHAR(255);

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS sent_at TIMESTAMP;

-- 4. Crear índices si no existen
CREATE INDEX IF NOT EXISTS idx_email_queue_status_next
    ON public.email_queue(status, proximo_intento);

CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud
    ON public.email_queue(solicitud_id);

CREATE INDEX IF NOT EXISTS idx_email_queue_orden
    ON public.email_queue(orden_id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
    ON public.email_queue(event_key)
    WHERE event_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_email_queue_created
    ON public.email_queue(created_at);

-- 5. Crear tabla de adjuntos si no existe
CREATE TABLE IF NOT EXISTS public.email_attachment (
    id SERIAL PRIMARY KEY,
    email_queue_id INTEGER NOT NULL REFERENCES public.email_queue(id) ON DELETE CASCADE,
    file_name VARCHAR(500) NOT NULL,
    file_content BYTEA NOT NULL,
    mime_type VARCHAR(100),
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_email_attachment_queue_id
    ON public.email_attachment(email_queue_id);

-- 6. Verificar que las columnas existen ahora
DO $$
DECLARE
    col_exists BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'email_queue' AND column_name = 'status'
    ) INTO col_exists;
    
    IF col_exists THEN
        RAISE NOTICE 'Column status: OK';
    ELSE
        RAISE EXCEPTION 'Column status: FAILED';
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'email_queue' AND column_name = 'error_message'
    ) INTO col_exists;
    
    IF col_exists THEN
        RAISE NOTICE 'Column error_message: OK';
    ELSE
        RAISE EXCEPTION 'Column error_message: FAILED';
    END IF;
END $$;

-- 7. Resultado
SELECT 'Email queue schema fix completed successfully' AS resultado;
