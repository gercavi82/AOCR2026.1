-- Migracion idempotente: usuarios internos vinculados a técnicos y correo institucional.
-- Fecha: 2026-03-18
-- Base: PostgreSQL

BEGIN;

CREATE TABLE IF NOT EXISTS aocr_usuario_interno_rt
(
    id                  SERIAL PRIMARY KEY,
    usuario_id          INT NULL REFERENCES usuario(idusuario) ON DELETE SET NULL,
    tecnico_id          INT NULL,
    codigo_usuario      VARCHAR(64) NOT NULL,
    identificacion      VARCHAR(32) NULL,
    nombres             VARCHAR(120) NULL,
    apellidos           VARCHAR(120) NULL,
    nombre_completo     VARCHAR(200) NULL,
    tipo                VARCHAR(10) NULL,
    estado_as400        VARCHAR(10) NULL,
    ciudad_codigo       VARCHAR(10) NULL,
    codigo_financiero   NUMERIC(18,0) NULL,
    opcar5              VARCHAR(10) NULL,
    opcaer              VARCHAR(10) NULL,
    opcoi3              NUMERIC(18,0) NULL,
    correo_institucional VARCHAR(200) NULL,
    rol_interno         VARCHAR(100) NULL,
    observaciones       TEXT NULL,
    activo              BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by          VARCHAR(120) NOT NULL DEFAULT 'migracion',
    updated_at          TIMESTAMP NULL,
    updated_by          VARCHAR(120) NULL
);

ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS tecnico_id INT NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS identificacion VARCHAR(32) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS nombres VARCHAR(120) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS apellidos VARCHAR(120) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS nombre_completo VARCHAR(200) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS correo_institucional VARCHAR(200) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS rol_interno VARCHAR(100) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS observaciones TEXT NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS tipo VARCHAR(10) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS estado_as400 VARCHAR(10) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS ciudad_codigo VARCHAR(10) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS codigo_financiero NUMERIC(18,0) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS opcar5 VARCHAR(10) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS opcaer VARCHAR(10) NULL;
ALTER TABLE aocr_usuario_interno_rt ADD COLUMN IF NOT EXISTS opcoi3 NUMERIC(18,0) NULL;

DO $$
BEGIN
    BEGIN
        ALTER TABLE aocr_usuario_interno_rt ALTER COLUMN ciudad_codigo DROP NOT NULL;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
    BEGIN
        ALTER TABLE aocr_usuario_interno_rt ALTER COLUMN codigo_financiero DROP NOT NULL;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
    BEGIN
        ALTER TABLE aocr_usuario_interno_rt ALTER COLUMN opcar5 DROP NOT NULL;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
    BEGIN
        ALTER TABLE aocr_usuario_interno_rt ALTER COLUMN opcaer DROP NOT NULL;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
    BEGIN
        ALTER TABLE aocr_usuario_interno_rt ALTER COLUMN opcoi3 DROP NOT NULL;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_name = 'aocr_tbtecnico'
    ) AND NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_aocr_usuario_interno_rt_tecnico'
    ) THEN
        ALTER TABLE aocr_usuario_interno_rt
            ADD CONSTRAINT fk_aocr_usuario_interno_rt_tecnico
            FOREIGN KEY (tecnico_id) REFERENCES aocr_tbtecnico(codigotecnico) ON DELETE SET NULL;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uix_aocr_usuario_interno_rt_codigo_activo
    ON aocr_usuario_interno_rt (UPPER(TRIM(codigo_usuario)))
    WHERE activo = TRUE;

DO $$
BEGIN
    BEGIN
        CREATE UNIQUE INDEX IF NOT EXISTS uix_aocr_usuario_interno_rt_tecnico_activo
            ON aocr_usuario_interno_rt (tecnico_id)
            WHERE activo = TRUE AND tecnico_id IS NOT NULL;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;

    BEGIN
        CREATE UNIQUE INDEX IF NOT EXISTS uix_aocr_usuario_interno_rt_correo_activo
            ON aocr_usuario_interno_rt (LOWER(TRIM(correo_institucional)))
            WHERE activo = TRUE
              AND correo_institucional IS NOT NULL
              AND BTRIM(correo_institucional) <> '';
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
END $$;

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_interno_rt_usuario_id
    ON aocr_usuario_interno_rt (usuario_id);

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_interno_rt_rol_interno
    ON aocr_usuario_interno_rt (rol_interno);

COMMIT;

SELECT '20260318_aocr_usuario_interno_tecnicos: OK' AS result;
