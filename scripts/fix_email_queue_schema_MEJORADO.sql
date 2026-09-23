-- =====================================================
-- Fix para email_queue schema - 2026-09-22 (VERSIÓN MEJORADA)
-- Problema: Tabla sin columnas status y error_message
-- Autor: Revisión Completa
-- =====================================================

-- PASO 1: Diagnosticar estado actual
DO $$
DECLARE
    tabla_existe BOOLEAN;
    col_status_existe BOOLEAN;
    col_error_existe BOOLEAN;
BEGIN
    -- Verificar si tabla existe
    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' AND table_name = 'email_queue'
    ) INTO tabla_existe;
    
    RAISE NOTICE '=====================================';
    RAISE NOTICE 'DIAGNÓSTICO: EMAIL QUEUE SCHEMA';
    RAISE NOTICE '=====================================';
    RAISE NOTICE 'Tabla email_queue existe: %', tabla_existe;
    
    IF tabla_existe THEN
        SELECT EXISTS (
            SELECT 1 FROM information_schema.columns 
            WHERE table_schema = 'public' AND table_name = 'email_queue' AND column_name = 'status'
        ) INTO col_status_existe;
        
        SELECT EXISTS (
            SELECT 1 FROM information_schema.columns 
            WHERE table_schema = 'public' AND table_name = 'email_queue' AND column_name = 'error_message'
        ) INTO col_error_existe;
        
        RAISE NOTICE 'Columna status existe: %', col_status_existe;
        RAISE NOTICE 'Columna error_message existe: %', col_error_existe;
    END IF;
    RAISE NOTICE '=====================================';
END $$;

-- PASO 2: Crear tabla completa (con todas las columnas necesarias)
-- Si ya existe, se preservan los datos existentes
CREATE TABLE IF NOT EXISTS public.email_queue (
    id SERIAL PRIMARY KEY,
    to_address VARCHAR(255) NOT NULL,
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    solicitud_id INTEGER NULL,
    orden_id INTEGER NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    proximo_intento TIMESTAMP NOT NULL DEFAULT NOW(),
    event_key VARCHAR(200),
    error_message TEXT,
    intentos INTEGER NOT NULL DEFAULT 0,
    updated_at TIMESTAMP,
    tipo_notificacion VARCHAR(120),
    correlation_id VARCHAR(64),
    message_id VARCHAR(255),
    sent_at TIMESTAMP
);

-- PASO 3: Agregar columnas faltantes (si la tabla ya existía sin ellas)
-- Esto maneja el caso de tablas antiguas incompletas
ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS status VARCHAR(20) DEFAULT 'PENDIENTE';

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS event_key VARCHAR(200);

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS error_message TEXT;

ALTER TABLE public.email_queue 
    ADD COLUMN IF NOT EXISTS intentos INTEGER DEFAULT 0;

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

-- PASO 4: Crear índices para optimización
CREATE INDEX IF NOT EXISTS idx_email_queue_status_next
    ON public.email_queue(status, proximo_intento)
    WHERE status IN ('PENDIENTE', 'ENVIANDO');

CREATE INDEX IF NOT EXISTS idx_email_queue_solicitud
    ON public.email_queue(solicitud_id)
    WHERE solicitud_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_email_queue_orden
    ON public.email_queue(orden_id)
    WHERE orden_id IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
    ON public.email_queue(event_key)
    WHERE event_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_email_queue_created
    ON public.email_queue(created_at DESC)
    WHERE created_at >= (NOW() - INTERVAL '30 days');

CREATE INDEX IF NOT EXISTS idx_email_queue_tipo
    ON public.email_queue(tipo_notificacion)
    WHERE tipo_notificacion IS NOT NULL;

-- PASO 5: Crear tabla de adjuntos si no existe
CREATE TABLE IF NOT EXISTS public.email_attachment (
    id SERIAL PRIMARY KEY,
    email_queue_id INTEGER NOT NULL,
    file_name VARCHAR(500) NOT NULL,
    file_content BYTEA NOT NULL,
    mime_type VARCHAR(100),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_email_attachment_queue 
        FOREIGN KEY (email_queue_id) 
        REFERENCES public.email_queue(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_email_attachment_queue_id
    ON public.email_attachment(email_queue_id);

-- PASO 6: Validar que todo está correcto
DO $$
DECLARE
    col_status BOOLEAN;
    col_error BOOLEAN;
    col_proximo BOOLEAN;
    tabla_adjuntos BOOLEAN;
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '=====================================';
    RAISE NOTICE 'VALIDACIÓN POST-FIX';
    RAISE NOTICE '=====================================';
    
    -- Validar columnas críticas
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'email_queue' AND column_name = 'status'
    ) INTO col_status;
    
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'email_queue' AND column_name = 'error_message'
    ) INTO col_error;
    
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' AND table_name = 'email_queue' AND column_name = 'proximo_intento'
    ) INTO col_proximo;
    
    SELECT EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' AND table_name = 'email_attachment'
    ) INTO tabla_adjuntos;
    
    IF col_status THEN
        RAISE NOTICE '✓ Columna status: OK';
    ELSE
        RAISE EXCEPTION '✗ Columna status: FALLA';
    END IF;
    
    IF col_error THEN
        RAISE NOTICE '✓ Columna error_message: OK';
    ELSE
        RAISE EXCEPTION '✗ Columna error_message: FALLA';
    END IF;
    
    IF col_proximo THEN
        RAISE NOTICE '✓ Columna proximo_intento: OK';
    ELSE
        RAISE EXCEPTION '✗ Columna proximo_intento: FALLA';
    END IF;
    
    IF tabla_adjuntos THEN
        RAISE NOTICE '✓ Tabla email_attachment: OK';
    ELSE
        RAISE EXCEPTION '✗ Tabla email_attachment: FALLA';
    END IF;
    
    RAISE NOTICE '=====================================';
    RAISE NOTICE 'RESUMEN: Email queue schema OK ✓';
    RAISE NOTICE '=====================================';
    RAISE NOTICE '';
END $$;

-- PASO 7: Test de funcionalidad (insertar registro de prueba)
BEGIN;
    INSERT INTO public.email_queue 
        (to_address, subject, body, status, created_at, proximo_intento, tipo_notificacion)
    VALUES 
        ('test@aviacioncivil.gob.ec', 'Email Queue Fix Test', 'Testing email queue schema', 'PENDIENTE', NOW(), NOW(), 'TEST');
    ROLLBACK;  -- No guardar registro de prueba, solo validar que funciona
EXCEPTION WHEN OTHERS THEN
    RAISE EXCEPTION 'Error en test de inserción: %', SQLERRM;
END;

SELECT 'Fix completado exitosamente' AS resultado;
