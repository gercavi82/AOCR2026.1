BEGIN;

ALTER TABLE IF EXISTS aocr_usuario_compania_rt
    ADD COLUMN IF NOT EXISTS usuoid VARCHAR(30);

CREATE INDEX IF NOT EXISTS idx_aocr_usuario_compania_rt_usuoid
    ON aocr_usuario_compania_rt (usuoid);

COMMIT;
