-- ============================================================================
-- Migración: Tabla Outbox para procesamiento FR3 (Fase 1)
-- Fecha: 2026-07-19
-- IDEMPOTENTE: Puede ejecutarse múltiples veces sin errores
-- ============================================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'aocr_fr3_outbox') THEN
        CREATE TABLE aocr_fr3_outbox (
            id                  SERIAL PRIMARY KEY,
            event_key           VARCHAR(128) NOT NULL UNIQUE,
            orden_id            INTEGER NOT NULL,
            pago_id             INTEGER,
            estado              VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
            intentos            INTEGER NOT NULL DEFAULT 0,
            proximo_intento     TIMESTAMP WITH TIME ZONE,
            worker_id           VARCHAR(100),
            lock_until          TIMESTAMP WITH TIME ZONE,
            payload             TEXT,
            error_last          TEXT,
            created_at          TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            updated_at          TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
        );

        CREATE INDEX idx_fr3_outbox_estado ON aocr_fr3_outbox(estado);
        CREATE INDEX idx_fr3_outbox_proximo ON aocr_fr3_outbox(proximo_intento) WHERE estado = 'PENDIENTE';
        CREATE INDEX idx_fr3_outbox_orden ON aocr_fr3_outbox(orden_id);

        RAISE NOTICE 'Tabla aocr_fr3_outbox creada exitosamente.';
    ELSE
        RAISE NOTICE 'Tabla aocr_fr3_outbox ya existe.';
    END IF;
END $$;
