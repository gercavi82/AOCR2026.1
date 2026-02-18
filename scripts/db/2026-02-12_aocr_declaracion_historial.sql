-- Crear tabla de historial definitivo de declaraciones RT
CREATE TABLE IF NOT EXISTS aocr_declaracion_historial (
    id SERIAL PRIMARY KEY,
    email VARCHAR(120) NOT NULL,
    identificacion VARCHAR(30) NULL,
    empresa_codigo VARCHAR(20) NULL,
    empresa_nombre VARCHAR(200) NULL,
    nombres VARCHAR(80) NULL,
    apellidos VARCHAR(80) NULL,
    aceptada BOOLEAN NOT NULL DEFAULT FALSE,
    ip VARCHAR(60) NULL,
    user_agent VARCHAR(512) NULL,
    created_at TIMESTAMP NULL,
    updated_at TIMESTAMP NULL,
    finalized_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_aocr_declaracion_historial_email
    ON aocr_declaracion_historial (email);

CREATE INDEX IF NOT EXISTS idx_aocr_declaracion_historial_finalized
    ON aocr_declaracion_historial (finalized_at);
