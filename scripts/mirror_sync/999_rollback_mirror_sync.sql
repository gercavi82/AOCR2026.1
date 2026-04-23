-- 999_rollback_mirror_sync.sql
-- Rollback simple (NO elimina datos de AOCR). Ejecutar solo si desea retirar el módulo espejo.
-- Recomendación: respaldar antes de ejecutar.
-- Orden: vistas → tablas detalle → tablas cabecera → tablas sync → esquemas (opcional).

-- =======================
-- 003b rollback: columnas SNAP agregadas a opcar5 / opcar6
-- =======================
-- Descomente las líneas necesarias si desea revertir el parche 003b:
/*
ALTER TABLE mirror_raw.opcar5
    DROP COLUMN IF EXISTS opcenv,
    DROP COLUMN IF EXISTS opcder,
    DROP COLUMN IF EXISTS opcde2,
    DROP COLUMN IF EXISTS opcde3,
    DROP COLUMN IF EXISTS opcde4,
    DROP COLUMN IF EXISTS opcho8,
    DROP COLUMN IF EXISTS opcfe5,
    DROP COLUMN IF EXISTS opcho9,
    DROP COLUMN IF EXISTS opcfe6,
    DROP COLUMN IF EXISTS opcnr1,
    DROP COLUMN IF EXISTS opcval,
    DROP COLUMN IF EXISTS opcde5,
    DROP COLUMN IF EXISTS opcde6,
    DROP COLUMN IF EXISTS opcfe7,
    DROP COLUMN IF EXISTS opcefe,
    DROP COLUMN IF EXISTS opcvue,
    DROP COLUMN IF EXISTS opccta,
    DROP COLUMN IF EXISTS opccru,
    DROP COLUMN IF EXISTS opcoi5,
    DROP COLUMN IF EXISTS opcilu,
    DROP COLUMN IF EXISTS opcper,
    DROP COLUMN IF EXISTS opces1,
    DROP COLUMN IF EXISTS opcc07,
    DROP COLUMN IF EXISTS opcnu1,
    DROP COLUMN IF EXISTS opcre8,
    DROP COLUMN IF EXISTS opcre9,
    DROP COLUMN IF EXISTS opcf04,
    DROP COLUMN IF EXISTS opcf05,
    DROP COLUMN IF EXISTS opcdi6,
    DROP COLUMN IF EXISTS opcdi7;

ALTER TABLE mirror_raw.opcar6
    DROP COLUMN IF EXISTS opcde9,
    DROP COLUMN IF EXISTS opcimp,
    DROP COLUMN IF EXISTS opcpor,
    DROP COLUMN IF EXISTS opcpo1,
    DROP COLUMN IF EXISTS opcva2,
    DROP COLUMN IF EXISTS opcva3,
    DROP COLUMN IF EXISTS opcva4,
    DROP COLUMN IF EXISTS opcva5,
    DROP COLUMN IF EXISTS opcubi;
*/

-- =======================
-- 003 rollback: vistas y tablas espejo
-- =======================

DROP VIEW IF EXISTS mirror_clean.v_ciaarc_activa;
DROP VIEW IF EXISTS mirror_clean.v_usuario_as400;
DROP VIEW IF EXISTS mirror_clean.v_lugar_emision_ciudad;

DROP TABLE IF EXISTS mirror_raw.opcar6;
DROP TABLE IF EXISTS mirror_raw.opcar5;
DROP TABLE IF EXISTS mirror_raw.opsarc;
DROP TABLE IF EXISTS mirror_raw.txdgac;
DROP TABLE IF EXISTS mirror_raw.opiar2;
DROP TABLE IF EXISTS mirror_raw.oidar2;
DROP TABLE IF EXISTS mirror_raw.opuarc01;
DROP TABLE IF EXISTS mirror_raw.ciaarc;
DROP TABLE IF EXISTS mirror_raw.usuar1;
DROP TABLE IF EXISTS mirror_raw.usuarc;

DROP TABLE IF EXISTS sync.tombstones;
DROP TABLE IF EXISTS sync.rejections;
DROP TABLE IF EXISTS sync.batch_log;
DROP TABLE IF EXISTS sync.watermark;

-- Opcional: eliminar esquemas si quedan vacíos
-- DROP SCHEMA IF EXISTS mirror_clean;
-- DROP SCHEMA IF EXISTS mirror_raw;
-- DROP SCHEMA IF EXISTS sync;
