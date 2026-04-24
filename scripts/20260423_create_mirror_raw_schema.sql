-- ============================================================================
-- Migración: Creación del schema mirror_raw y tablas espejo desde AS400
-- Fecha: 2026-04-23
-- Propósito: Soporte para As400MirrorSyncService - sincronización AS400→PostgreSQL
-- IDEMPOTENTE: Puede ejecutarse múltiples veces sin errores
-- ============================================================================

-- Schema contenedor
CREATE SCHEMA IF NOT EXISTS mirror_raw;

-- ============================================================================
-- Tabla de estado de sincronización (requerida por PostgresSyncStateStore)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.sync_state (
    id                  SERIAL PRIMARY KEY,
    table_name          VARCHAR(100) NOT NULL,
    last_watermark_date VARCHAR(8),
    last_watermark_time VARCHAR(6),
    last_sync_at        TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    rows_synced         BIGINT DEFAULT 0,
    batch_id            UUID,
    CONSTRAINT uq_sync_state_table UNIQUE (table_name)
);

-- ============================================================================
-- USUARC — Usuarios AS400 (DGACDAT.USUARC)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.usuarc (
    usucod  VARCHAR(20),
    usunom  VARCHAR(60),
    usuape  VARCHAR(60),
    usutip  VARCHAR(10),
    usuced  VARCHAR(20),   -- Cédula
    usucor  VARCHAR(100),
    usucla  VARCHAR(50),
    usuest  VARCHAR(5),
    usuti1  VARCHAR(10),
    usuide  VARCHAR(20),   -- Identificación adicional
    usunum  VARCHAR(20),   -- RUC (campo usado en ObtenerIdentificacionPorClavesUsuario)
    usuaux  VARCHAR(50),
    usuau1  VARCHAR(50),
    usuau2  VARCHAR(50),
    usuusu  VARCHAR(20),
    usufec  VARCHAR(8),
    usuhor  VARCHAR(6),
    usudis  VARCHAR(5),
    usuus1  VARCHAR(20),
    usufe1  VARCHAR(10),   -- Watermark fecha
    usuho1  VARCHAR(10),   -- Watermark hora (HH:MM:SS)
    usudi1  VARCHAR(5),
    usuco1  VARCHAR(10),
    usuco2  VARCHAR(10),
    usuco3  VARCHAR(10),
    usuco4  VARCHAR(10),
    usuco5  VARCHAR(10),   -- Código ciudad
    usuco6  VARCHAR(10),
    -- Metadata mirror
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_usuarc PRIMARY KEY (usucod)
);
CREATE INDEX IF NOT EXISTS idx_mirror_usuarc_usuced ON mirror_raw.usuarc (usuced);
CREATE INDEX IF NOT EXISTS idx_mirror_usuarc_usunum ON mirror_raw.usuarc (usunum);
CREATE INDEX IF NOT EXISTS idx_mirror_usuarc_synced ON mirror_raw.usuarc (_mirror_synced_at);

-- ============================================================================
-- USUAR1 — Usuario adicional (DGACDAT.USUAR1)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.usuar1 (
    usuco8  VARCHAR(20),
    usutit  VARCHAR(10),
    usuti2  VARCHAR(10),
    usuno1  VARCHAR(60),
    usucar  VARCHAR(50),
    usunu1  VARCHAR(20),
    usunu2  VARCHAR(20),
    usuco7  VARCHAR(10),
    usuus2  VARCHAR(20),
    usufe2  VARCHAR(8),
    usuho2  VARCHAR(6),
    usudi2  VARCHAR(5),
    usuus3  VARCHAR(20),
    usufe3  VARCHAR(10),   -- Watermark fecha
    usuho3  VARCHAR(10),   -- Watermark hora (HH:MM:SS)
    usudi3  VARCHAR(5),
    usuoid  VARCHAR(20),
    usuco9  VARCHAR(10),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_usuar1 PRIMARY KEY (usuco8)
);

-- ============================================================================
-- CIAARC — Catálogo de compañías (DGACDAT.CIAARC)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.ciaarc (
    ciaoid  NUMERIC(10,0),
    ciacod  VARCHAR(20),
    ciaco2  VARCHAR(20),
    ciaco3  VARCHAR(20),
    cianom  VARCHAR(200),
    ciati1  VARCHAR(10),
    ciadir  VARCHAR(300),
    ciaruc  VARCHAR(20),    -- RUC de la compañía ← campo clave para lookup por OACI
    ciaema  VARCHAR(100),
    ciatel  VARCHAR(30),
    ciacel  VARCHAR(30),
    ciadi2  VARCHAR(300),
    ciate1  VARCHAR(30),
    ciacor  VARCHAR(100),
    ciarep  VARCHAR(200),
    ciano1  VARCHAR(200),
    ciatip  VARCHAR(10),
    ciaest  VARCHAR(5),
    ciaciu  VARCHAR(50),
    ciaes1  VARCHAR(10),
    ciaoi1  NUMERIC(10,0),
    ciausu  VARCHAR(20),
    ciafec  VARCHAR(10),
    ciahor  VARCHAR(10),
    ciadis  VARCHAR(5),
    ciaus1  VARCHAR(20),
    ciafe1  VARCHAR(10),    -- Watermark fecha
    ciaho1  VARCHAR(10),    -- Watermark hora
    ciadi1  VARCHAR(5),
    ciadi3  VARCHAR(300),
    ciate2  VARCHAR(30),
    ciace1  VARCHAR(20),
    ciaem1  VARCHAR(100),
    ciare1  VARCHAR(200),
    ciaoi2  NUMERIC(10,0),
    ciaco4  VARCHAR(20),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_ciaarc PRIMARY KEY (ciacod)
);
CREATE INDEX IF NOT EXISTS idx_mirror_ciaarc_cianom ON mirror_raw.ciaarc (UPPER(cianom));
CREATE INDEX IF NOT EXISTS idx_mirror_ciaarc_ruc    ON mirror_raw.ciaarc (ciaruc);

-- ============================================================================
-- OPUARC01 — Catálogo ubicación usuario / lugar de emisión (DGACDAT.OPUARC01)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.opuarc01 (
    opuoid  VARCHAR(20),
    opucod  VARCHAR(20),
    opuest  VARCHAR(5),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_opuarc01 PRIMARY KEY (opucod)
);

-- ============================================================================
-- OIDAR2 — Ubicación aeropuerto por ciudad (DGACDAT.OIDAR2)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.oidar2 (
    oidoi2  VARCHAR(20),
    oidco3  VARCHAR(10),   -- Código ciudad
    oidno2  VARCHAR(100),  -- Nombre estación/aeropuerto
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_oidar2 PRIMARY KEY (oidco3, oidoi2)
);
CREATE INDEX IF NOT EXISTS idx_mirror_oidar2_ciudad ON mirror_raw.oidar2 (oidco3);

-- ============================================================================
-- OPIAR2 — Inspectores institucionales (DGACDAT.OPIAR2)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.opiar2 (
    opiced  VARCHAR(20),   -- Cédula inspector (PK parcial)
    opino2  VARCHAR(100),
    opies1  VARCHAR(5),
    opitip  VARCHAR(10),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_opiar2 PRIMARY KEY (opiced, opitip)
);

-- ============================================================================
-- TXDGAC — Listas de valores AS400/P9 (DGACSYS.TXDGAC)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.txdgac (
    valdds  VARCHAR(20),
    valval  VARCHAR(20),
    valdes  VARCHAR(200),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_txdgac PRIMARY KEY (valdds, valval)
);

-- ============================================================================
-- OPSARC — Secuenciales FR3 por aeropuerto/año (DGACDAT.OPSARC)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.opsarc (
    opsaer  VARCHAR(10),
    opsano  VARCHAR(4),
    opssec  NUMERIC(10,0),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_opsarc PRIMARY KEY (opsaer, opsano)
);

-- ============================================================================
-- OPCAR5 — FR3 Cabecera (DGACDAT.OPCAR5)
-- Fuente del RUC de la compañía: OPCRU1
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.opcar5 (
    opcsec  NUMERIC(10,0),
    opcaer  VARCHAR(10),
    opcano  VARCHAR(4),
    opcfe4  VARCHAR(8),
    opctip  VARCHAR(10),
    opcrut  VARCHAR(200),
    opcnro  INTEGER,
    opctot  NUMERIC(15,2),
    opcgra  NUMERIC(15,2),
    opcson  VARCHAR(500),
    opcaut  VARCHAR(100),
    opcobs  VARCHAR(500),
    opcoid  NUMERIC(10,0),
    opcori  VARCHAR(100),
    opcde7  VARCHAR(100),
    opcret  VARCHAR(100),
    opccal  VARCHAR(50),
    opcest  VARCHAR(5),
    opcru1  VARCHAR(20),   -- RUC del cliente ← campo clave para contribuyente
    opcem1  VARCHAR(100),
    opcnac  VARCHAR(5),
    opcus7  VARCHAR(50),
    opcda4  VARCHAR(8),    -- Watermark fecha creación
    opch01  VARCHAR(10),   -- Watermark hora creación (HH:MM:SS = 8 chars)
    opcoi1  NUMERIC(10,0),
    opcte1  VARCHAR(20),
    opcno4  VARCHAR(200),  -- Nombre del cliente
    opcdi3  VARCHAR(300),
    opcoi2  NUMERIC(10,0),
    opcva6  NUMERIC(15,2),
    opcfor  VARCHAR(50),
    opcno5  VARCHAR(200),  -- Nombre compañía ← usado para búsqueda por nombre
    opcmod  VARCHAR(100),
    opcpes  NUMERIC(15,4),
    opcc08  VARCHAR(20),   -- Código OACI compañía ← usado para búsqueda por código
    opcno6  VARCHAR(200),
    opcem2  VARCHAR(100),
    opcmat  VARCHAR(50),
    opcpro  VARCHAR(5),
    opcsub  NUMERIC(15,2),
    opcoi3  NUMERIC(10,0),
    opcdi2  NUMERIC(15,2),
    opcfe9  VARCHAR(8),
    opcban  VARCHAR(20),
    opcche  VARCHAR(50),
    opcnum  VARCHAR(50),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_opcar5 PRIMARY KEY (opcsec, opcaer, opcano)
);
CREATE INDEX IF NOT EXISTS idx_mirror_opcar5_ruc    ON mirror_raw.opcar5 (opcru1);
CREATE INDEX IF NOT EXISTS idx_mirror_opcar5_oaci   ON mirror_raw.opcar5 (UPPER(opcc08));
CREATE INDEX IF NOT EXISTS idx_mirror_opcar5_nombre ON mirror_raw.opcar5 (UPPER(opcno5));
CREATE INDEX IF NOT EXISTS idx_mirror_opcar5_synced ON mirror_raw.opcar5 (_mirror_synced_at);

-- ============================================================================
-- OPCARC — Catalogo de Operadores DGAC (DGACDAT.OPCARC)
-- Incluye RUC/identificacion fiscal. 5000+ operadores.
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw.opcarc (
    opccod  VARCHAR(5),
    opcsig  VARCHAR(5),
    opcco1  VARCHAR(6),
    opcnom  VARCHAR(132),
    opcco2  VARCHAR(2),
    opcno1  VARCHAR(60),
    opcdir  VARCHAR(132),
    opcruc  VARCHAR(20),
    opcema  VARCHAR(60),
    opcrep  VARCHAR(60),
    opcus3  VARCHAR(10),
    opcda2  VARCHAR(8),
    opcho4  VARCHAR(10),
    opcus4  VARCHAR(10),
    opcda3  VARCHAR(8),
    opcho5  VARCHAR(10),
    opccel  VARCHAR(20),
    opctel  VARCHAR(20),
    CONSTRAINT pk_mirror_opcarc PRIMARY KEY (opccod)
);
CREATE INDEX IF NOT EXISTS idx_mirror_opcarc_ruc ON mirror_raw.opcarc (opcruc)
    WHERE opcruc IS NOT NULL AND opcruc <> '';
CREATE INDEX IF NOT EXISTS idx_mirror_opcarc_nom ON mirror_raw.opcarc (UPPER(opcnom));
CREATE INDEX IF NOT EXISTS idx_mirror_opcarc_co1 ON mirror_raw.opcarc (UPPER(opcco1));


CREATE TABLE IF NOT EXISTS mirror_raw.opcar6 (
    opcse2  NUMERIC(10,0),
    opcae1  VARCHAR(10),
    opcan1  VARCHAR(4),
    opcse1  NUMERIC(10,0),
    opcti1  VARCHAR(20),
    opcoi4  NUMERIC(10,0),
    opcc05  VARCHAR(50),
    opcde8  VARCHAR(500),
    opccan  NUMERIC(15,4),
    opcva1  NUMERIC(15,2),
    opcde9  VARCHAR(5),
    opcimp  VARCHAR(5),
    opchac  VARCHAR(5),
    opcpor  NUMERIC(10,4),
    opccob  VARCHAR(5),
    opcpo1  NUMERIC(10,4),
    opcing  VARCHAR(5),
    opcd01  VARCHAR(200),
    opcva2  NUMERIC(15,2),
    opcva3  NUMERIC(15,2),
    opcva4  NUMERIC(15,2),
    opcc06  VARCHAR(20),
    opcva5  NUMERIC(15,2),
    opcto1  NUMERIC(15,2),
    opcubi  VARCHAR(10),
    _mirror_synced_at   TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    _is_deleted         BOOLEAN DEFAULT FALSE,
    _batch_id           UUID,
    CONSTRAINT pk_mirror_opcar6 PRIMARY KEY (opcse2, opcae1, opcan1, opcse1)
);

-- ============================================================================
-- Tabla de tombstone para deletes lógicos (requerida por DeleteStrategy.TombstoneTable)
-- ============================================================================
CREATE TABLE IF NOT EXISTS mirror_raw._tombstone (
    id          SERIAL PRIMARY KEY,
    table_name  VARCHAR(100) NOT NULL,
    source_key  VARCHAR(500) NOT NULL,
    deleted_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    batch_id    UUID
);
CREATE INDEX IF NOT EXISTS idx_tombstone_table_key ON mirror_raw._tombstone (table_name, source_key);
