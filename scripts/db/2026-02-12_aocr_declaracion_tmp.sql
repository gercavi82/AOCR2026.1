-- Tabla temporal para registrar aceptación de declaración antes de crear el usuario
CREATE TABLE IF NOT EXISTS aocr_declaracion_tmp (
    id SERIAL PRIMARY KEY,
    email VARCHAR(120) NOT NULL UNIQUE,
    identificacion VARCHAR(30) NULL,
    empresa_codigo VARCHAR(20) NULL,
    empresa_nombre VARCHAR(200) NULL,
    nombres VARCHAR(80) NULL,
    apellidos VARCHAR(80) NULL,
    aceptada BOOLEAN NOT NULL DEFAULT FALSE,
    ip VARCHAR(60) NULL,
    user_agent VARCHAR(512) NULL,
    expires_at TIMESTAMP NOT NULL DEFAULT (NOW() + INTERVAL '15 minutes'),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_aocr_declaracion_tmp_email ON aocr_declaracion_tmp (email);
CREATE INDEX IF NOT EXISTS idx_aocr_declaracion_tmp_expires ON aocr_declaracion_tmp (expires_at);
