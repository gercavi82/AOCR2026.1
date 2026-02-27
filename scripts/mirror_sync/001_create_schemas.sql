-- 001_create_schemas.sql
-- AOCR / AS400 Mirror Sync
-- Idempotente. Crea esquemas tecnicos para espejo y control de sincronizacion.

CREATE SCHEMA IF NOT EXISTS mirror_raw;
CREATE SCHEMA IF NOT EXISTS mirror_clean;
CREATE SCHEMA IF NOT EXISTS sync;

COMMENT ON SCHEMA mirror_raw IS 'Espejo tecnico 1:1 (o subset controlado) de tablas AS/400 Db2 for i';
COMMENT ON SCHEMA mirror_clean IS 'Capa opcional de limpieza/normalizacion para consumo AOCR';
COMMENT ON SCHEMA sync IS 'Control de sincronizacion incremental AS400 -> PostgreSQL';
