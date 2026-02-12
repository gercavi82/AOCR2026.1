-- Ajustes para tabla temporal de declaración (si ya existe)
ALTER TABLE aocr_declaracion_tmp
    ADD COLUMN IF NOT EXISTS ip VARCHAR(60) NULL;

ALTER TABLE aocr_declaracion_tmp
    ADD COLUMN IF NOT EXISTS user_agent VARCHAR(512) NULL;

ALTER TABLE aocr_declaracion_tmp
    ADD COLUMN IF NOT EXISTS expires_at TIMESTAMP NOT NULL DEFAULT (NOW() + INTERVAL '15 minutes');

CREATE INDEX IF NOT EXISTS idx_aocr_declaracion_tmp_expires
    ON aocr_declaracion_tmp (expires_at);
