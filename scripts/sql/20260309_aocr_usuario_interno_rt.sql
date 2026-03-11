-- Migracion idempotente: registro de usuario interno RT en Administracion.
-- Fecha: 2026-03-09
-- Base: PostgreSQL

BEGIN;

CREATE TABLE IF NOT EXISTS aocr_usuario_interno_rt
(
    id                SERIAL PRIMARY KEY,
    usuario_id        INT NULL REFERENCES usuario(idusuario) ON DELETE SET NULL,
    codigo_usuario    VARCHAR(64) NOT NULL,
    ciudad_codigo     VARCHAR(10) NOT NULL,
    codigo_financiero NUMERIC(18,0) NOT NULL,
    opcar5            VARCHAR(10) NOT NULL,
    opcaer            VARCHAR(10) NOT NULL,
    opcoi3            NUMERIC(18,0) NOT NULL,
    activo            BOOLEAN NOT NULL DEFAULT TRUE,
    created_at        TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by        VARCHAR(120) NOT NULL DEFAULT 'migracion',
    updated_at        TIMESTAMP NULL,
    updated_by        VARCHAR(120) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uix_aocr_usuario_interno_rt_codigo_activo
    ON aocr_usuario_interno_rt (UPPER(TRIM(codigo_usuario)))
    WHERE activo = TRUE;

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_interno_rt_usuario_id
    ON aocr_usuario_interno_rt(usuario_id);

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_interno_rt_opcaer
    ON aocr_usuario_interno_rt(opcaer);

COMMIT;

SELECT '20260309_aocr_usuario_interno_rt: OK' AS result;
