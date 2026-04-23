-- 003c_create_missing_mirror_raw_tables.sql
-- AOCR / AS400 Mirror Sync
-- Idempotente. Crea tablas espejo faltantes usadas por AOCR:
-- OPIAR2 (inspectores), TXDGAC (listas P9), OPSARC (secuenciales FR3).
-- Requiere ejecutar 001, 002, 003 primero.

-- =====================
-- mirror_raw.OPIAR2 (inspectores)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.opiar2 (
    opiced varchar(20) NOT NULL,
    opitip varchar(4) NOT NULL,
    opino2 varchar(180) NULL,
    opies1 varchar(2) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now(),
    CONSTRAINT pk_mirror_opiar2 PRIMARY KEY (opiced, opitip)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opiar2_estado_tipo') THEN
        CREATE INDEX ix_opiar2_estado_tipo ON mirror_raw.opiar2 (opies1, opitip, opino2);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opiar2_cedula') THEN
        CREATE INDEX ix_opiar2_cedula ON mirror_raw.opiar2 (opiced);
    END IF;
END $$;

-- =====================
-- mirror_raw.TXDGAC (listas de valores)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.txdgac (
    valdds varchar(50) NOT NULL,
    valval varchar(50) NOT NULL,
    valdes varchar(255) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now(),
    CONSTRAINT pk_mirror_txdgac PRIMARY KEY (valdds, valval)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_txdgac_campo') THEN
        CREATE INDEX ix_txdgac_campo ON mirror_raw.txdgac (valdds, valdes);
    END IF;
END $$;

-- =====================
-- mirror_raw.OPSARC (secuencial FR3)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.opsarc (
    opsaer varchar(4) NOT NULL,
    opsano varchar(4) NOT NULL,
    opssec numeric(18,0) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now(),
    CONSTRAINT pk_mirror_opsarc PRIMARY KEY (opsaer, opsano)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opsarc_sec') THEN
        CREATE INDEX ix_opsarc_sec ON mirror_raw.opsarc (opssec DESC, opsaer, opsano);
    END IF;
END $$;
