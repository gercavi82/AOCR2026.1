-- 002_create_sync_tables.sql
-- AOCR / AS400 Mirror Sync
-- Idempotente. Requiere ejecutar 001_create_schemas.sql antes.

CREATE TABLE IF NOT EXISTS sync.watermark (
    table_name        text PRIMARY KEY,
    last_success_ts   timestamp NULL,
    last_success_key  text NULL,
    last_batch_id     uuid NULL,
    status            varchar(16) NOT NULL DEFAULT 'OK',
    last_error        text NULL,
    updated_at        timestamp NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS sync.batch_log (
    batch_id          uuid PRIMARY KEY,
    started_at        timestamp NOT NULL,
    ended_at          timestamp NULL,
    table_name        text NOT NULL,
    rows_read         integer NOT NULL DEFAULT 0,
    rows_applied      integer NOT NULL DEFAULT 0,
    rows_rejected     integer NOT NULL DEFAULT 0,
    rows_deleted      integer NOT NULL DEFAULT 0,
    latency_ms        bigint NULL,
    status            varchar(16) NOT NULL DEFAULT 'RUNNING',
    error             text NULL
);

CREATE TABLE IF NOT EXISTS sync.rejections (
    id                bigserial PRIMARY KEY,
    batch_id          uuid NOT NULL,
    table_name        text NOT NULL,
    payload           jsonb NOT NULL,
    error             text NOT NULL,
    created_at        timestamp NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS sync.tombstones (
    id                bigserial PRIMARY KEY,
    table_name        text NOT NULL,
    pk_payload        jsonb NOT NULL,
    source_deleted_at timestamp NULL,
    source_reference  text NULL,
    batch_id          uuid NULL,
    applied           boolean NOT NULL DEFAULT false,
    applied_at        timestamp NULL,
    created_at        timestamp NOT NULL DEFAULT now(),
    error             text NULL
);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE schemaname = 'sync' AND indexname = 'ix_sync_batch_log_table_started'
    ) THEN
        CREATE INDEX ix_sync_batch_log_table_started ON sync.batch_log (table_name, started_at DESC);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE schemaname = 'sync' AND indexname = 'ix_sync_rejections_batch'
    ) THEN
        CREATE INDEX ix_sync_rejections_batch ON sync.rejections (batch_id, table_name, created_at DESC);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE schemaname = 'sync' AND indexname = 'ix_sync_tombstones_pending'
    ) THEN
        CREATE INDEX ix_sync_tombstones_pending ON sync.tombstones (table_name, applied, created_at);
    END IF;
END $$;

COMMENT ON TABLE sync.watermark IS 'Estado incremental por tabla (watermark)';
COMMENT ON TABLE sync.batch_log IS 'Bitacora por lote de sincronizacion';
COMMENT ON TABLE sync.rejections IS 'Filas rechazadas al aplicar espejo';
COMMENT ON TABLE sync.tombstones IS 'Deletes fisicos detectados/inyectados para aplicar por PK';
