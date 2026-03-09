-- AOCR - Transferencia y desactivacion segura de usuarios
-- Script idempotente para auditoria de transferencias operativas.

CREATE TABLE IF NOT EXISTS aocr_usuario_transferencia
(
    id_transferencia BIGSERIAL PRIMARY KEY,
    usuario_origen_id INT NOT NULL,
    usuario_destino_id INT NOT NULL,
    ejecutado_por_usuario_id INT NULL,
    ejecutado_por_codigo VARCHAR(100) NULL,
    motivo VARCHAR(500) NULL,
    ip VARCHAR(64) NULL,
    fecha TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    total_registros_detectados INT NOT NULL DEFAULT 0,
    total_registros_transferidos INT NOT NULL DEFAULT 0,
    resumen_json JSONB NULL
);

CREATE TABLE IF NOT EXISTS aocr_usuario_transferencia_detalle
(
    id_detalle BIGSERIAL PRIMARY KEY,
    transferencia_id BIGINT NOT NULL REFERENCES aocr_usuario_transferencia(id_transferencia) ON DELETE CASCADE,
    grupo VARCHAR(30) NOT NULL,
    tabla VARCHAR(128) NOT NULL,
    campo VARCHAR(128) NOT NULL,
    descripcion VARCHAR(300) NULL,
    estrategia VARCHAR(300) NULL,
    transferible BOOLEAN NOT NULL DEFAULT FALSE,
    registros_detectados INT NOT NULL DEFAULT 0,
    registros_afectados INT NOT NULL DEFAULT 0,
    observacion VARCHAR(500) NULL
);

CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_fecha
    ON aocr_usuario_transferencia(fecha DESC);

CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_origen
    ON aocr_usuario_transferencia(usuario_origen_id);

CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_destino
    ON aocr_usuario_transferencia(usuario_destino_id);

CREATE INDEX IF NOT EXISTS ix_aocr_usuario_transferencia_detalle_transferencia
    ON aocr_usuario_transferencia_detalle(transferencia_id);
