-- =============================================================
-- AOCR - Fase 1 Refactor documental
-- Fecha: 2026-03-12
-- Objetivo:
-- 1) Agregar codigo_oaci a aocr_tbsolicitud
-- 2) Crear tabla de revision documental por documento
-- 3) Crear tabla de historial documental (trazabilidad)
-- =============================================================

BEGIN;

ALTER TABLE IF EXISTS aocr_tbsolicitud
    ADD COLUMN IF NOT EXISTS codigo_oaci VARCHAR(8);

COMMENT ON COLUMN aocr_tbsolicitud.codigo_oaci IS 'Codigo OACI del operador/tramite AOCR';

UPDATE aocr_tbsolicitud
SET codigo_oaci = UPPER(TRIM(companias_seleccionadas))
WHERE (codigo_oaci IS NULL OR TRIM(codigo_oaci) = '')
  AND companias_seleccionadas IS NOT NULL
  AND TRIM(companias_seleccionadas) <> '';

CREATE TABLE IF NOT EXISTS aocr_tbrevision_documental
(
    codigo_revision BIGSERIAL PRIMARY KEY,
    codigo_solicitud INT NOT NULL,
    codigo_documento INT NOT NULL,
    decision VARCHAR(20) NOT NULL,
    observacion TEXT NULL,
    codigo_usuario_revisor INT NOT NULL,
    fecha_revision TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NULL,
    updated_at TIMESTAMP NULL,
    updated_by VARCHAR(100) NULL,
    deleted_at TIMESTAMP NULL,
    deleted_by VARCHAR(100) NULL,
    CONSTRAINT fk_rev_doc_solicitud FOREIGN KEY (codigo_solicitud)
        REFERENCES aocr_tbsolicitud(codigo_solicitud),
    CONSTRAINT fk_rev_doc_documento FOREIGN KEY (codigo_documento)
        REFERENCES aocr_tbdocumento(codigo_documento),
    CONSTRAINT chk_rev_doc_decision CHECK (UPPER(decision) IN ('ACEPTADO', 'DEVUELTO', 'OBSERVADO'))
);

CREATE INDEX IF NOT EXISTS idx_rev_doc_solicitud
    ON aocr_tbrevision_documental(codigo_solicitud);

CREATE INDEX IF NOT EXISTS idx_rev_doc_documento
    ON aocr_tbrevision_documental(codigo_documento);

CREATE INDEX IF NOT EXISTS idx_rev_doc_fecha
    ON aocr_tbrevision_documental(fecha_revision DESC);

CREATE TABLE IF NOT EXISTS aocr_tbhistorial_documental
(
    codigo_historial BIGSERIAL PRIMARY KEY,
    codigo_solicitud INT NOT NULL,
    codigo_documento INT NULL,
    evento VARCHAR(80) NOT NULL,
    detalle TEXT NULL,
    codigo_usuario INT NULL,
    fecha_evento TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NULL,
    deleted_at TIMESTAMP NULL,
    deleted_by VARCHAR(100) NULL,
    CONSTRAINT fk_hist_doc_solicitud FOREIGN KEY (codigo_solicitud)
        REFERENCES aocr_tbsolicitud(codigo_solicitud),
    CONSTRAINT fk_hist_doc_documento FOREIGN KEY (codigo_documento)
        REFERENCES aocr_tbdocumento(codigo_documento)
);

CREATE INDEX IF NOT EXISTS idx_hist_doc_solicitud
    ON aocr_tbhistorial_documental(codigo_solicitud, fecha_evento DESC);

CREATE INDEX IF NOT EXISTS idx_hist_doc_documento
    ON aocr_tbhistorial_documental(codigo_documento);

COMMIT;
