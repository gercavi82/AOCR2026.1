-- =========================================================================================
-- SCRIPT DE MIGRACIÓN: 20260903_ac10_condiciones_limitaciones.sql
-- OBJETIVO: AC-10 - Generación y firma institucional de Condiciones y Limitaciones (CL)
-- Permite que el INSPECTOR prepare el borrador con estaciones independientes y datos reales,
-- el COORDINADOR revise y devuelva o remita, y DIRCAV revise y aplique la firma exclusiva.
-- DIRDAC y ADMINISTRADOR quedan terminantemente excluidos de la firma de CL.
-- IDEMPOTENTE, ADITIVO Y SEGURO.
-- =========================================================================================

DO $$
BEGIN
    -- 1. Crear tabla principal para ciclo de vida de Condiciones y Limitaciones si no existe
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbcondiciones_limitaciones') THEN
        CREATE TABLE public.aocr_tbcondiciones_limitaciones (
            id SERIAL PRIMARY KEY,
            codigo_solicitud INTEGER NOT NULL,
            codigo_inspeccion INTEGER NULL,
            codigo_informe INTEGER NULL,
            numero_aocr VARCHAR(100) NULL,
            version INTEGER NOT NULL DEFAULT 1,
            estado VARCHAR(50) NOT NULL DEFAULT 'CL_BORRADOR',
            vigente BOOLEAN NOT NULL DEFAULT TRUE,
            
            -- Contenido técnico y documental
            compania VARCHAR(250) NULL,
            operador_extranjero VARCHAR(250) NULL,
            representante_tecnico VARCHAR(250) NULL,
            tipo_operacion VARCHAR(100) NULL,
            rutas_autorizadas TEXT NULL,
            alcance_autorizado TEXT NULL,
            condiciones_aprobadas TEXT NULL,
            limitaciones TEXT NULL,
            observaciones TEXT NULL,
            
            -- Revisión y trazabilidad por rol
            inspector_usuario_id INTEGER NULL,
            inspector_nombre VARCHAR(200) NULL,
            fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW(),
            
            coordinador_usuario_id INTEGER NULL,
            coordinador_nombre VARCHAR(200) NULL,
            observacion_coordinador TEXT NULL,
            fecha_revision_coordinador TIMESTAMP NULL,
            
            dircav_usuario_id INTEGER NULL,
            dircav_nombre VARCHAR(200) NULL,
            observacion_dircav TEXT NULL,
            fecha_firma_dircav TIMESTAMP NULL,
            
            -- Almacenamiento, integridad y firma
            ruta_pdf_borrador VARCHAR(500) NULL,
            ruta_pdf_firmado VARCHAR(500) NULL,
            hash_pdf VARCHAR(128) NULL,
            hash_pdf_firmado VARCHAR(128) NULL,
            tamanio_pdf BIGINT NULL,
            codigo_verificacion VARCHAR(64) NULL,
            
            version_concurrencia BIGINT NOT NULL DEFAULT 1,
            created_at TIMESTAMP NOT NULL DEFAULT NOW(),
            updated_at TIMESTAMP NOT NULL DEFAULT NOW()
        );
    END IF;

    -- 2. Asegurar columnas aditivas si la tabla ya existía
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'aocr_tbcondiciones_limitaciones') THEN
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS codigo_inspeccion INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS codigo_informe INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS numero_aocr VARCHAR(100) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS version INTEGER NOT NULL DEFAULT 1;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS estado VARCHAR(50) NOT NULL DEFAULT 'CL_BORRADOR';
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS compania VARCHAR(250) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS operador_extranjero VARCHAR(250) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS representante_tecnico VARCHAR(250) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS tipo_operacion VARCHAR(100) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS rutas_autorizadas TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS alcance_autorizado TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS condiciones_aprobadas TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS limitaciones TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS observaciones TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS inspector_usuario_id INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS inspector_nombre VARCHAR(200) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS fecha_generacion TIMESTAMP NOT NULL DEFAULT NOW();
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS coordinador_usuario_id INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS coordinador_nombre VARCHAR(200) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS observacion_coordinador TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS fecha_revision_coordinador TIMESTAMP NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS dircav_usuario_id INTEGER NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS dircav_nombre VARCHAR(200) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS observacion_dircav TEXT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS fecha_firma_dircav TIMESTAMP NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS ruta_pdf_borrador VARCHAR(500) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS ruta_pdf_firmado VARCHAR(500) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS hash_pdf VARCHAR(128) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS hash_pdf_firmado VARCHAR(128) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS tamanio_pdf BIGINT NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS codigo_verificacion VARCHAR(64) NULL;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS version_concurrencia BIGINT NOT NULL DEFAULT 1;
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
        ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP NOT NULL DEFAULT NOW();
    END IF;

    -- 3. Actualizar la restricción de estados independientes
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cl_estado') THEN
        ALTER TABLE public.aocr_tbcondiciones_limitaciones DROP CONSTRAINT ck_cl_estado;
    END IF;

    ALTER TABLE public.aocr_tbcondiciones_limitaciones ADD CONSTRAINT ck_cl_estado CHECK (estado IN (
        'CL_NO_GENERADA',
        'CL_BORRADOR',
        'CL_PENDIENTE_COORDINADOR',
        'CL_DEVUELTA_INSPECTOR',
        'CL_PENDIENTE_DIRCAV',
        'CL_DEVUELTA_COORDINADOR',
        'CL_PENDIENTE_FIRMA_DIRCAV',
        'CL_FIRMADA_DIRCAV',
        'CL_ANULADA',
        'CL_REEMPLAZADA'
    ));

END $$;

-- 4. Índices únicos de integridad y versión
CREATE UNIQUE INDEX IF NOT EXISTS ux_cl_solicitud_vigente
    ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud)
    WHERE vigente = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ux_cl_solicitud_version
    ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud, version);

CREATE INDEX IF NOT EXISTS ix_cl_estado_vigente
    ON public.aocr_tbcondiciones_limitaciones(estado, vigente);

CREATE INDEX IF NOT EXISTS ix_cl_solicitud_fecha
    ON public.aocr_tbcondiciones_limitaciones(codigo_solicitud, fecha_generacion DESC);
