-- -----------------------------------------------------------------------------
-- MIGRACIÓN DE EXPEDIENTE LÓGICO Y CÓDIGO DE COMPAÑÍA AOCR
-- -----------------------------------------------------------------------------

BEGIN;

-- 1. Crear tabla de expedientes lógicos por compañía y usuario RT
CREATE TABLE IF NOT EXISTS public.aocr_expediente_compania (
    id SERIAL PRIMARY KEY,
    usuario_rt_id INTEGER NOT NULL,
    compania_codigo VARCHAR(50) NOT NULL,
    compania_nombre VARCHAR(200) NULL,
    anio INTEGER NOT NULL,
    estado VARCHAR(50) NOT NULL DEFAULT 'ACTIVO',
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
    observacion TEXT NULL,
    CONSTRAINT uq_aocr_expediente_compania_anio UNIQUE (usuario_rt_id, compania_codigo, anio)
);

CREATE INDEX IF NOT EXISTS ix_aocr_expediente_compania_usuario
ON public.aocr_expediente_compania(usuario_rt_id);

CREATE INDEX IF NOT EXISTS ix_aocr_expediente_compania_compania
ON public.aocr_expediente_compania(compania_codigo);

-- 2. Asegurar columna compania_codigo y expediente_id en aocr_or_orden
ALTER TABLE public.aocr_or_orden 
ADD COLUMN IF NOT EXISTS compania_codigo VARCHAR(50);

ALTER TABLE public.aocr_or_orden 
ADD COLUMN IF NOT EXISTS expediente_id INTEGER;

CREATE INDEX IF NOT EXISTS ix_aocr_or_orden_compania_codigo
ON public.aocr_or_orden(compania_codigo);

CREATE INDEX IF NOT EXISTS ix_aocr_or_orden_expediente_id
ON public.aocr_or_orden(expediente_id);

COMMIT;
