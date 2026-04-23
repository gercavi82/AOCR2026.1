-- 003_create_mirror_raw_tables.sql
-- AOCR / AS400 Mirror Sync
-- Espejo tecnico inicial (tablas priorizadas).
-- Nota: CIAARC / OPCAR5 / OPCAR6 se crean con el subconjunto actualmente consumido por AOCR.

-- =====================
-- mirror_raw.USUARC
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.usuarc (
    usucod varchar(10) PRIMARY KEY,
    usunom varchar(40) NULL,
    usuape varchar(40) NULL,
    usutip varchar(2) NULL,
    usuced varchar(10) NULL,
    usucor varchar(100) NULL,
    usucla varchar(256) NULL,
    usuest varchar(2) NULL,
    usuti1 varchar(4) NULL,
    usuide varchar(3) NULL,
    usunum varchar(20) NULL,
    usuaux numeric(7,0) NULL,
    usuau1 varchar(2) NULL,
    usuau2 numeric(12,2) NULL,
    usuusu varchar(10) NULL,
    usufec varchar(8) NULL,
    usuhor varchar(8) NULL,
    usudis varchar(15) NULL,
    usuus1 varchar(10) NULL,
    usufe1 varchar(8) NULL,
    usuho1 varchar(8) NULL,
    usudi1 varchar(15) NULL,
    usuco1 varchar(4) NULL,
    usuco2 varchar(4) NULL,
    usuco3 varchar(4) NULL,
    usuco4 varchar(4) NULL,
    usuco5 varchar(4) NULL,
    usuco6 varchar(4) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_usuarc_updated') THEN
        CREATE INDEX ix_usuarc_updated ON mirror_raw.usuarc (_source_updated_at DESC, usucod);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_usuarc_ciudad') THEN
        CREATE INDEX ix_usuarc_ciudad ON mirror_raw.usuarc (usuco5);
    END IF;
END $$;

-- =====================
-- mirror_raw.USUAR1
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.usuar1 (
    usuco8 varchar(10) PRIMARY KEY,
    usutit varchar(6) NULL,
    usuti2 varchar(6) NULL,
    usuno1 varchar(60) NULL,
    usucar varchar(60) NULL,
    usunu1 varchar(20) NULL,
    usunu2 varchar(20) NULL,
    usuco7 varchar(60) NULL,
    usuus2 varchar(10) NULL,
    usufe2 varchar(8) NULL,
    usuho2 varchar(8) NULL,
    usudi2 varchar(15) NULL,
    usuus3 varchar(10) NULL,
    usufe3 varchar(8) NULL,
    usuho3 varchar(8) NULL,
    usudi3 varchar(15) NULL,
    usuoid numeric(10,0) NULL,
    usuco9 varchar(4) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_usuar1_updated') THEN
        CREATE INDEX ix_usuar1_updated ON mirror_raw.usuar1 (_source_updated_at DESC, usuco8);
    END IF;
END $$;

-- =====================
-- mirror_raw.CIAARC (subconjunto utilizado por AOCR)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.ciaarc (
    ciacod varchar(4) PRIMARY KEY,
    ciaco2 varchar(4) NULL,
    ciaco3 varchar(8) NULL,
    cianom varchar(120) NULL,
    ciaest varchar(2) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_ciaarc_estado') THEN
        CREATE INDEX ix_ciaarc_estado ON mirror_raw.ciaarc (ciaest, cianom);
    END IF;
END $$;

-- =====================
-- mirror_raw.OPUARC01 (ubicacion por ciudad)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.opuarc01 (
    opucod varchar(10) PRIMARY KEY,
    opuoid numeric(18,0) NULL,
    opuest varchar(120) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opuarc01_ciudad') THEN
        CREATE INDEX ix_opuarc01_ciudad ON mirror_raw.opuarc01 (opucod);
    END IF;
END $$;

-- =====================
-- mirror_raw.OIDAR2 (aeropuerto por ciudad)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.oidar2 (
    oidco3 varchar(10) NOT NULL,
    oidoi2 numeric(18,0) NOT NULL,
    oidno2 varchar(120) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now(),
    CONSTRAINT pk_mirror_oidar2 PRIMARY KEY (oidco3, oidoi2)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_oidar2_ciudad') THEN
        CREATE INDEX ix_oidar2_ciudad ON mirror_raw.oidar2 (oidco3);
    END IF;
END $$;

-- =====================
-- mirror_raw.OPCAR5 (FR3 cabecera - subconjunto funcional AOCR)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.opcar5 (
    opcsec numeric(18,0) NOT NULL,
    opcaer varchar(4) NOT NULL,
    opcano varchar(4) NOT NULL,
    opcfe4 varchar(8) NULL,
    opctip varchar(4) NULL,
    opcrut varchar(255) NULL,
    opcnro integer NULL,
    opctot numeric(18,2) NULL,
    opcgra numeric(18,2) NULL,
    opcson varchar(512) NULL,
    opcaut varchar(128) NULL,
    opcobs varchar(1024) NULL,
    opcoid numeric(18,0) NULL,
    opcori varchar(32) NULL,
    opcde7 varchar(32) NULL,
    opcret varchar(32) NULL,
    opccal varchar(32) NULL,
    opcest varchar(4) NULL,
    opcru1 varchar(20) NULL,
    opcem1 varchar(120) NULL,
    opcnac varchar(2) NULL,
    opcus7 varchar(20) NULL,
    opcda4 varchar(8) NULL,
    opch01 varchar(8) NULL,
    opcoi1 numeric(18,0) NULL,
    opcte1 varchar(20) NULL,
    opcno4 varchar(120) NULL,
    opcdi3 varchar(255) NULL,
    opcoi2 numeric(18,0) NULL,
    opcva6 numeric(18,2) NULL,
    opcfor varchar(4) NULL,
    opcno5 varchar(120) NULL,
    opcmod varchar(60) NULL,
    opcpes numeric(18,3) NULL,
    opcc08 varchar(8) NULL,
    opcno6 varchar(120) NULL,
    opcem2 varchar(120) NULL,
    opcmat varchar(20) NULL,
    opcpro varchar(4) NULL,
    opcsub numeric(18,2) NULL,
    opcoi3 numeric(18,0) NULL,
    opcdi2 numeric(18,2) NULL,
    opcfe9 varchar(8) NULL,
    opcban varchar(20) NULL,
    opcche varchar(80) NULL,
    opcnum varchar(40) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now(),
    CONSTRAINT pk_mirror_opcar5 PRIMARY KEY (opcsec, opcaer, opcano)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opcar5_factura') THEN
        CREATE INDEX ix_opcar5_factura ON mirror_raw.opcar5 (opcnum, opcaer, opcano);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opcar5_fecha') THEN
        CREATE INDEX ix_opcar5_fecha ON mirror_raw.opcar5 (opcfe4, opcda4, opch01);
    END IF;
END $$;

-- =====================
-- mirror_raw.OPCAR6 (FR3 detalle - subconjunto funcional AOCR)
-- =====================
CREATE TABLE IF NOT EXISTS mirror_raw.opcar6 (
    opcse2 numeric(18,0) NOT NULL,
    opcae1 varchar(4) NOT NULL,
    opcan1 varchar(4) NOT NULL,
    opcse1 numeric(18,0) NOT NULL,
    opcti1 varchar(4) NULL,
    opcoi4 numeric(18,0) NULL,
    opcc05 varchar(32) NULL,
    opcde8 varchar(1024) NULL,
    opccan numeric(18,4) NULL,
    opcva1 numeric(18,4) NULL,
    opchac varchar(2) NULL,
    opccob varchar(2) NULL,
    opcing varchar(2) NULL,
    opcd01 varchar(255) NULL,
    opcc06 varchar(20) NULL,
    opcto1 numeric(18,4) NULL,
    _source_updated_at timestamp NULL,
    _source_op varchar(1) NULL,
    _row_hash varchar(64) NULL,
    _is_deleted boolean NOT NULL DEFAULT false,
    _mirror_batch_id uuid NULL,
    _mirror_synced_at timestamp NOT NULL DEFAULT now(),
    CONSTRAINT pk_mirror_opcar6 PRIMARY KEY (opcse2, opcae1, opcan1, opcse1)
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='mirror_raw' AND indexname='ix_opcar6_parent') THEN
        CREATE INDEX ix_opcar6_parent ON mirror_raw.opcar6 (opcse2, opcae1, opcan1);
    END IF;
END $$;

-- =====================
-- mirror_clean views (opcionales, sin romper AOCR)
-- =====================
CREATE OR REPLACE VIEW mirror_clean.v_usuario_as400 AS
SELECT
    u.usucod AS codigo_usuario,
    u.usunom AS nombres,
    u.usuape AS apellidos,
    u.usucor AS correo,
    u.usuest AS estado_actividad,
    u.usuco4 AS codigo_rol,
    u.usuco5 AS codigo_ciudad,
    u.usuco6 AS codigo_dependencia,
    a.usuno1 AS nombre_corto,
    a.usucar AS cargo,
    a.usunu1 AS telefono1,
    a.usunu2 AS telefono2,
    a.usuco7 AS correo_adicional,
    a.usuoid AS oid_centro_contable,
    u._source_updated_at,
    u._mirror_synced_at,
    u._is_deleted
FROM mirror_raw.usuarc u
LEFT JOIN mirror_raw.usuar1 a
  ON a.usuco8 = u.usucod
WHERE COALESCE(u._is_deleted, false) = false;

CREATE OR REPLACE VIEW mirror_clean.v_ciaarc_activa AS
SELECT
    ciacod AS codigo_oaci,
    ciaco2 AS codigo_iata,
    ciaco3 AS codigo_numero_cia,
    cianom AS nombre_compania,
    ciaest,
    _mirror_synced_at
FROM mirror_raw.ciaarc
WHERE COALESCE(_is_deleted, false) = false
  AND TRIM(COALESCE(ciaest, '')) = 'AC';

CREATE OR REPLACE VIEW mirror_clean.v_lugar_emision_ciudad AS
SELECT
    UPPER(TRIM(opucod)) AS codigo_ciudad,
    NULLIF(TRIM(opuest), '') AS lugar_emision,
    'OPUARC01'::text AS fuente,
    _mirror_synced_at
FROM mirror_raw.opuarc01
WHERE COALESCE(_is_deleted, false) = false
  AND NULLIF(TRIM(opucod), '') IS NOT NULL
  AND NULLIF(TRIM(opuest), '') IS NOT NULL
UNION ALL
SELECT
    UPPER(TRIM(oidco3)) AS codigo_ciudad,
    NULLIF(TRIM(oidno2), '') AS lugar_emision,
    'OIDAR2'::text AS fuente,
    _mirror_synced_at
FROM mirror_raw.oidar2
WHERE COALESCE(_is_deleted, false) = false
  AND NULLIF(TRIM(oidco3), '') IS NOT NULL
  AND NULLIF(TRIM(oidno2), '') IS NOT NULL;
