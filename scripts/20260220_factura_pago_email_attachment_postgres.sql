-- =============================================================
-- AOCR - Migracion financiera: factura de pago + adjuntos de email
-- Motor: PostgreSQL
-- Fecha: 2026-02-20
-- =============================================================

BEGIN;

CREATE TABLE IF NOT EXISTS public.aocr_tb_factura_pago (
    id                 SERIAL PRIMARY KEY,
    orden_id           INTEGER NOT NULL,
    pago_id            INTEGER,
    numero_factura     VARCHAR(80) NOT NULL,
    autorizacion_factura VARCHAR(80),
    fecha_emision      DATE NOT NULL,
    subtotal           NUMERIC(18,2) NOT NULL,
    iva                NUMERIC(18,2) NOT NULL,
    total              NUMERIC(18,2) NOT NULL,
    observaciones      TEXT,
    file_name          VARCHAR(255) NOT NULL,
    content_type       VARCHAR(120) NOT NULL,
    file_size          BIGINT NOT NULL,
    file_path          TEXT NOT NULL,
    creado_por         VARCHAR(120),
    creado_en          TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_factura_pago_totales_non_negative CHECK (subtotal >= 0 AND iva >= 0 AND total > 0),
    CONSTRAINT fk_factura_pago_orden FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id),
    CONSTRAINT fk_factura_pago_pago FOREIGN KEY (pago_id) REFERENCES public.aocr_tbpago(codigo_pago)
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'uq_aocr_tb_factura_pago_orden'
    ) THEN
        ALTER TABLE public.aocr_tb_factura_pago
        ADD CONSTRAINT uq_aocr_tb_factura_pago_orden UNIQUE (orden_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'uq_aocr_tb_factura_pago_pago'
    ) THEN
        ALTER TABLE public.aocr_tb_factura_pago
        ADD CONSTRAINT uq_aocr_tb_factura_pago_pago UNIQUE (pago_id);
    END IF;
END
$$;

CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_orden_id
    ON public.aocr_tb_factura_pago(orden_id);

CREATE INDEX IF NOT EXISTS idx_aocr_tb_factura_pago_fecha_emision
    ON public.aocr_tb_factura_pago(fecha_emision);

-- Extensiones idempotentes para email_queue
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS event_key VARCHAR(200);
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS error_message TEXT;
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS intentos INTEGER NOT NULL DEFAULT 0;
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
    ON public.email_queue(event_key)
    WHERE event_key IS NOT NULL;

CREATE TABLE IF NOT EXISTS public.email_attachment (
    id               SERIAL PRIMARY KEY,
    email_queue_id   INTEGER NOT NULL,
    file_name        VARCHAR(255) NOT NULL,
    content_type     VARCHAR(120) NOT NULL,
    file_path        TEXT NOT NULL,
    file_size        BIGINT,
    created_at       TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_email_attachment_queue FOREIGN KEY (email_queue_id)
        REFERENCES public.email_queue(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_email_attachment_queue_id
    ON public.email_attachment(email_queue_id);

COMMIT;
