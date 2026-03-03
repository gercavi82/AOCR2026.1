-- ============================================================
-- Script de creación de tablas para Control FR3 en PostgreSQL
-- Migrado desde DB2/AS400 (tablas OPCAR5, OPCAR6, OPSARC)
-- Ejecutar contra la base de datos AOCR
-- ============================================================

-- Tabla principal de Control FR3 (equivalente a OPCAR5 en AS400)
CREATE TABLE IF NOT EXISTS aocr_control_fr3 (
    id                     SERIAL PRIMARY KEY,
    secuencial             NUMERIC(10,0) NOT NULL DEFAULT 0,
    aeropuerto             VARCHAR(10) NOT NULL,
    anio                   VARCHAR(4) NOT NULL,
    fecha_control_vuelo    VARCHAR(10),
    tipo_operacion         VARCHAR(20),
    ruta_total_plan_vlo    VARCHAR(200),
    num_aterriza_pais      INTEGER DEFAULT 0,
    subtotal               NUMERIC(15,2) DEFAULT 0,
    valor_charter          NUMERIC(15,2) DEFAULT 0,
    total                  NUMERIC(15,2) DEFAULT 0,
    gran_total             NUMERIC(15,2) DEFAULT 0,
    gran_total_letras      VARCHAR(500),
    autorizacion           VARCHAR(100),
    observacion            VARCHAR(500),
    oid_cia_aviacion       NUMERIC(10,0) DEFAULT 0,
    oid_ubicacion          NUMERIC(10,0) DEFAULT 0,
    origen                 VARCHAR(100),
    destino                VARCHAR(100),
    retorno                VARCHAR(100),
    callsign               VARCHAR(50),
    estado                 VARCHAR(10) DEFAULT 'E',
    ruc                    VARCHAR(20),
    email                  VARCHAR(100),
    nac_inter              VARCHAR(5),
    usuario_cr             VARCHAR(50),
    fecha_cr               VARCHAR(10),
    hora_cr                VARCHAR(10),
    id_aeropuerto          NUMERIC(10,0) DEFAULT 0,
    telefono               VARCHAR(20),
    nombre_cliente         VARCHAR(200),
    direccion              VARCHAR(300),
    oid_ubicacion_cliente  NUMERIC(10,0) DEFAULT 0,
    forma_pago             VARCHAR(50),
    nombre_cia             VARCHAR(200),
    modelo                 VARCHAR(100),
    peso_matricula         NUMERIC(15,4) DEFAULT 0,
    codigo_oaci_cia        VARCHAR(20),
    nombre_aeropuerto      VARCHAR(200),
    email_usuario_dgac     VARCHAR(100),
    matricula              VARCHAR(50),
    procesado              VARCHAR(5) DEFAULT 'E',
    valor_total_millas     NUMERIC(15,2) DEFAULT 0,
    fecha_recepcion        VARCHAR(10),
    codigo_banco           VARCHAR(20),
    deposito               VARCHAR(50),
    numero_factura         VARCHAR(50),
    tipo_tramite           VARCHAR(50),
    nombre_archivo_factura VARCHAR(200),
    -- Campos de auditoría
    fecha_creacion         TIMESTAMP DEFAULT NOW(),
    fecha_actualizacion    TIMESTAMP,
    activo                 BOOLEAN DEFAULT TRUE
);

-- Índices para búsquedas frecuentes
CREATE INDEX IF NOT EXISTS idx_fr3_aeropuerto_anio ON aocr_control_fr3 (aeropuerto, anio);
CREATE INDEX IF NOT EXISTS idx_fr3_secuencial ON aocr_control_fr3 (secuencial);
CREATE INDEX IF NOT EXISTS idx_fr3_estado ON aocr_control_fr3 (estado);
CREATE INDEX IF NOT EXISTS idx_fr3_ruc ON aocr_control_fr3 (ruc);
CREATE INDEX IF NOT EXISTS idx_fr3_matricula ON aocr_control_fr3 (matricula);
CREATE UNIQUE INDEX IF NOT EXISTS idx_fr3_unique_sec_aer_anio ON aocr_control_fr3 (secuencial, aeropuerto, anio);

-- Tabla de detalle del Control FR3 (equivalente a OPCAR6 en AS400)
CREATE TABLE IF NOT EXISTS aocr_control_fr3_detalle (
    id                   SERIAL PRIMARY KEY,
    control_fr3_id       INTEGER NOT NULL REFERENCES aocr_control_fr3(id) ON DELETE CASCADE,
    secuencial           NUMERIC(10,0) NOT NULL DEFAULT 0,
    aeropuerto           VARCHAR(10),
    anio                 VARCHAR(4),
    secuencial_detalle   NUMERIC(10,0) DEFAULT 0,
    tipo_cobro           VARCHAR(20),
    oid_formulario       NUMERIC(15,0) DEFAULT 0,
    codigo_contable      VARCHAR(50),
    descripcion          VARCHAR(500),
    cantidad             NUMERIC(15,4) DEFAULT 0,
    valor                NUMERIC(15,2) DEFAULT 0,
    hacer_descuento      VARCHAR(5) DEFAULT 'N',
    cobrar_impuesto      VARCHAR(5) DEFAULT 'N',
    ingresar_cantidad    VARCHAR(5) DEFAULT 'S',
    descripcion_cuenta   VARCHAR(200),
    codigo               VARCHAR(20),
    total                NUMERIC(15,2) DEFAULT 0,
    -- Campos de auditoría
    fecha_creacion       TIMESTAMP DEFAULT NOW(),
    activo               BOOLEAN DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_fr3_det_control ON aocr_control_fr3_detalle (control_fr3_id);
CREATE INDEX IF NOT EXISTS idx_fr3_det_secuencial ON aocr_control_fr3_detalle (secuencial, aeropuerto, anio);

-- Tabla de secuenciales FR3 por aeropuerto (equivalente a OPSARC en AS400)
CREATE TABLE IF NOT EXISTS aocr_fr3_secuencial (
    id           SERIAL PRIMARY KEY,
    aeropuerto   VARCHAR(10) NOT NULL,
    anio         VARCHAR(4),
    secuencial   NUMERIC(10,0) DEFAULT 0,
    UNIQUE(aeropuerto)
);

-- Insertar aeropuertos base si no existen
INSERT INTO aocr_fr3_secuencial (aeropuerto, anio, secuencial)
VALUES ('UIO', '2026', 0)
ON CONFLICT (aeropuerto) DO NOTHING;

INSERT INTO aocr_fr3_secuencial (aeropuerto, anio, secuencial)
VALUES ('GYE', '2026', 0)
ON CONFLICT (aeropuerto) DO NOTHING;

INSERT INTO aocr_fr3_secuencial (aeropuerto, anio, secuencial)
VALUES ('CUE', '2026', 0)
ON CONFLICT (aeropuerto) DO NOTHING;

-- Comentarios
COMMENT ON TABLE aocr_control_fr3 IS 'Control FR3 para vuelos charter/especiales - Migrado desde AS400 OPCAR5';
COMMENT ON TABLE aocr_control_fr3_detalle IS 'Detalle de líneas de cobro FR3 - Migrado desde AS400 OPCAR6';
COMMENT ON TABLE aocr_fr3_secuencial IS 'Secuenciales FR3 por aeropuerto - Migrado desde AS400 OPSARC';
