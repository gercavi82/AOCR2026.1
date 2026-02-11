-- Migration: FASE 1 - Flujo RT / AOCR
-- File: scripts/sql/migrate_20260211_aocr_rt_fase1.sql
-- Purpose: Idempotent migration to create tables, indices, constraints and add missing columns for RT flow.
-- Safe to run multiple times on PostgreSQL >= 9.5 (uses IF NOT EXISTS and conditional DO blocks).
-- Recommended to run in CI as part of deployment (psql -f ...)

BEGIN;

-- 1) Create companies table
CREATE TABLE IF NOT EXISTS aocr_compania (
    id SERIAL PRIMARY KEY,
    razon_social VARCHAR(200) NOT NULL,
    ruc VARCHAR(20) NOT NULL,
    telefono VARCHAR(30) NOT NULL,
    email_contacto VARCHAR(120) NOT NULL,
    area_contable_json JSONB NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_compania_ruc ON aocr_compania(ruc);

-- 2) Create solicitud (request) table
CREATE TABLE IF NOT EXISTS aocr_solicitud_rt (
    id SERIAL PRIMARY KEY,
    usuario_rt_id INT NOT NULL,
    compania_id INT NOT NULL REFERENCES aocr_compania(id) ON DELETE CASCADE,
    estado VARCHAR(20) NOT NULL,
    declaracion_aceptada BOOLEAN NOT NULL DEFAULT FALSE,
    declaracion_texto TEXT NOT NULL,
    fecha_envio TIMESTAMP NULL,
    observacion_coordinador TEXT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now(),
    updated_at TIMESTAMP NOT NULL DEFAULT now()
);

-- Ensure estado has a CHECK constraint listing allowed states
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_class t ON c.conrelid = t.oid
        WHERE t.relname = 'aocr_solicitud_rt' AND c.conname = 'chk_aocr_solicitud_rt_estado'
    ) THEN
        ALTER TABLE aocr_solicitud_rt
        ADD CONSTRAINT chk_aocr_solicitud_rt_estado CHECK (estado IN ('BORRADOR','ENVIADA','DEVUELTA','APROBADA'));
    END IF;
END$$;

CREATE INDEX IF NOT EXISTS idx_solicitud_rt_estado ON aocr_solicitud_rt(estado);
CREATE INDEX IF NOT EXISTS idx_solicitud_rt_usuario ON aocr_solicitud_rt(usuario_rt_id);

-- 3) Documents table
CREATE TABLE IF NOT EXISTS aocr_documento (
    id SERIAL PRIMARY KEY,
    solicitud_rt_id INT NOT NULL REFERENCES aocr_solicitud_rt(id) ON DELETE CASCADE,
    tipo VARCHAR(40) NOT NULL,
    nombre_archivo VARCHAR(255) NOT NULL,
    ruta_storage VARCHAR(500) NOT NULL,
    tamano_bytes BIGINT NOT NULL,
    hash_sha256 VARCHAR(64) NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

-- Ensure tipo has constraint for allowed types (add additional types here if needed)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint c
        JOIN pg_class t ON c.conrelid = t.oid
        WHERE t.relname = 'aocr_documento' AND c.conname = 'chk_aocr_documento_tipo'
    ) THEN
        ALTER TABLE aocr_documento
        ADD CONSTRAINT chk_aocr_documento_tipo CHECK (tipo IN ('DESIGNACION_RT'));
    END IF;
END$$;

CREATE INDEX IF NOT EXISTS idx_doc_solicitud_tipo ON aocr_documento(solicitud_rt_id, tipo);

-- Add created_by for document metadata tracking
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'aocr_documento' AND column_name = 'created_by'
    ) THEN
        ALTER TABLE aocr_documento ADD COLUMN created_by VARCHAR(120);
    END IF;
END$$;

-- Ensure email uniqueness (case-insensitive) at DB level to help enforce server-side validation
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE indexname = 'idx_compania_email_lower'
    ) THEN
        CREATE UNIQUE INDEX idx_compania_email_lower ON aocr_compania(LOWER(email_contacto));
    END IF;
END$$;

-- 4) Historial de estados (trazabilidad)
CREATE TABLE IF NOT EXISTS aocr_solicitud_rt_historial (
    id SERIAL PRIMARY KEY,
    solicitud_rt_id INT NOT NULL REFERENCES aocr_solicitud_rt(id) ON DELETE CASCADE,
    estado VARCHAR(20) NOT NULL,
    motivo TEXT NULL,
    usuario_id INT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_historial_solicitud ON aocr_solicitud_rt_historial(solicitud_rt_id);

-- 5) Ensure usuario table has RT-related columns (some projects already have a script alter_usuario_add_designacion_rt.sql)
-- These ALTERs use IF NOT EXISTS to be idempotent.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'usuario' AND column_name = 'estado_designacion_rt'
    ) THEN
        ALTER TABLE usuario ADD COLUMN estado_designacion_rt VARCHAR(20) DEFAULT 'pendiente';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'usuario' AND column_name = 'fecha_revision_designacion'
    ) THEN
        ALTER TABLE usuario ADD COLUMN fecha_revision_designacion TIMESTAMP;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'usuario' AND column_name = 'ruta_constancia_rt'
    ) THEN
        ALTER TABLE usuario ADD COLUMN ruta_constancia_rt VARCHAR(255);
    END IF;
END$$;

-- 6) Small safety: set default values for existing records where applicable (non-destructive)
UPDATE aocr_solicitud_rt SET estado = 'BORRADOR' WHERE estado IS NULL;

COMMIT;

-- End of migration
-- Exit code: success if script finishes without errors
SELECT 'migrate_20260211_aocr_rt_fase1: OK' AS result;
