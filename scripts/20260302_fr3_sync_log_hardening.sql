-- =============================================================
-- AOCR - Hardening financiero FR3 + sync_log
-- Fecha: 2026-03-02
-- Motor: PostgreSQL
-- Idempotente
-- =============================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.aocr_tb_factura_pago (
    id SERIAL PRIMARY KEY,
    orden_id INTEGER NOT NULL,
    pago_id INTEGER,
    numero_factura VARCHAR(80) NOT NULL,
    autorizacion_factura VARCHAR(80),
    fecha_emision DATE NOT NULL,
    subtotal NUMERIC(18,2) NOT NULL,
    iva NUMERIC(18,2) NOT NULL,
    total NUMERIC(18,2) NOT NULL,
    observaciones TEXT,
    file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(120) NOT NULL,
    file_size BIGINT NOT NULL,
    file_path TEXT NOT NULL,
    creado_por VARCHAR(120),
    creado_en TIMESTAMP NOT NULL DEFAULT NOW()
);

ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_estado VARCHAR(30);
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_numero VARCHAR(80);
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_secuencial NUMERIC(18,0);
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_aeropuerto VARCHAR(10);
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_anio VARCHAR(4);
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_error TEXT;
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_generado_en TIMESTAMP;
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS fr3_reintentos INTEGER NOT NULL DEFAULT 0;
ALTER TABLE public.aocr_tb_factura_pago ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_fr3_estado
    ON public.aocr_tb_factura_pago(fr3_estado);

CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_fr3_numero
    ON public.aocr_tb_factura_pago(fr3_numero);

CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_orden_id
    ON public.aocr_tb_factura_pago(orden_id);

CREATE TABLE IF NOT EXISTS public.aocr_tb_sync_log (
    id BIGSERIAL PRIMARY KEY,
    idempotency_key VARCHAR(200) NOT NULL,
    orden_id INTEGER NOT NULL,
    pago_id INTEGER,
    modulo VARCHAR(50) NOT NULL,
    operacion VARCHAR(100) NOT NULL,
    estado VARCHAR(30) NOT NULL,
    mensaje TEXT,
    fr3_numero VARCHAR(80),
    payload JSONB,
    intentos INTEGER NOT NULL DEFAULT 0,
    usuario VARCHAR(120),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tb_sync_log_idempotency
    ON public.aocr_tb_sync_log(idempotency_key);

CREATE INDEX IF NOT EXISTS idx_aocr_tb_sync_log_orden_estado
    ON public.aocr_tb_sync_log(orden_id, estado, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_aocr_tb_sync_log_fr3_numero
    ON public.aocr_tb_sync_log(fr3_numero);

COMMIT;
