-- 001_estado_central_upgrade.sql
-- FASE 5: Upgrades estructurales para la máquina de estado central

-- 1. Agregar columna version para concurrencia optimista
ALTER TABLE public.aocr_proceso_estado ADD COLUMN IF NOT EXISTS version BIGINT NOT NULL DEFAULT 1;

-- 2. Agregar columnas de trazabilidad y idempotencia al historial
ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS ip VARCHAR(50) NULL;
ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(100) NULL;
ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS clave_idempotencia VARCHAR(100) NULL;
ALTER TABLE public.aocr_proceso_estado_historial ADD COLUMN IF NOT EXISTS resultado VARCHAR(50) NULL;

-- 3. Crear tabla de idempotencia
CREATE TABLE IF NOT EXISTS public.aocr_proceso_idempotencia
(
    clave VARCHAR(100) PRIMARY KEY,
    solicitud_id INTEGER NOT NULL,
    fecha_registro TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
