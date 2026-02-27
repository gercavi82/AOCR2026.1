-- ==============================================================================
-- 003b_alter_mirror_raw_add_snap_columns.sql
-- AOCR / AS400 Mirror Sync
-- Idempotente. Agrega columnas faltantes derivadas del SNAP completo AS/400
-- para mirror_raw.opcar5 (CABECERA FR3) y mirror_raw.opcar6 (DETALLE FR3).
-- Requiere ejecutar 001, 002, 003 primero.
-- Ejecutar en PostgreSQL (dgac_des o produccion segun ambiente).
-- CI/CD: incluir luego de 003 en pipeline. Seguro para re-ejecutar.
-- ==============================================================================

-- ----------------------------
-- mirror_raw.opcar5 - columnas SNAP faltantes
-- ----------------------------
DO $$
BEGIN
    -- OPCENV (ENVIADO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcenv') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcenv varchar(1) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcenv IS 'AS400: ENVIADO (OPCENV)';
    END IF;

    -- OPCDER (DERECHO ATERR DIURNO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcder') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcder numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcder IS 'AS400: DERECHO ATERR DIURNO (OPCDER)';
    END IF;

    -- OPCDE2 (DERECHO ATERR NOCTUR)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcde2') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcde2 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcde2 IS 'AS400: DERECHO ATERR NOCTUR (OPCDE2)';
    END IF;

    -- OPCDE3 (DERECHO PROT VLO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcde3') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcde3 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcde3 IS 'AS400: DERECHO PROT VLO (OPCDE3)';
    END IF;

    -- OPCDE4 (DERECHO ESTACIONAMI)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcde4') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcde4 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcde4 IS 'AS400: DERECHO ESTACIONAMI (OPCDE4)';
    END IF;

    -- OPCHO8 (HORA INGRESO PLATAF)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcho8') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcho8 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcho8 IS 'AS400: HORA INGRESO PLATAF (OPCHO8)';
    END IF;

    -- OPCFE5 (FECHA INGRESO PLATAF)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcfe5') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcfe5 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcfe5 IS 'AS400: FECHA INGRESO PLATAF (OPCFE5)';
    END IF;

    -- OPCHO9 (HORA SALIDA PLATAF)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcho9') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcho9 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcho9 IS 'AS400: HORA SALIDA PLATAF (OPCHO9)';
    END IF;

    -- OPCFE6 (FECHA SALIDA PLATAF)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcfe6') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcfe6 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcfe6 IS 'AS400: FECHA SALIDA PLATAF (OPCFE6)';
    END IF;

    -- OPCNR1 (NRO HORAS)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcnr1') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcnr1 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcnr1 IS 'AS400: NRO HORAS (OPCNR1)';
    END IF;

    -- OPCVAL (VALOR POR 4 HORAS)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcval') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcval numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcval IS 'AS400: VALOR POR 4 HORAS (OPCVAL)';
    END IF;

    -- OPCDE5 (DESCRIPCION1)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcde5') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcde5 varchar(30) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcde5 IS 'AS400: DESCRIPCION1 (OPCDE5)';
    END IF;

    -- OPCDE6 (DESCRIPCION2)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcde6') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcde6 varchar(30) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcde6 IS 'AS400: DESCRIPCION2 (OPCDE6)';
    END IF;

    -- OPCFE7 (FECHA OPERACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcfe7') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcfe7 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcfe7 IS 'AS400: FECHA OPERACION (OPCFE7)';
    END IF;

    -- OPCEFE (EFECTIVO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcefe') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcefe numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcefe IS 'AS400: EFECTIVO (OPCEFE)';
    END IF;

    -- OPCVUE (VUELTO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcvue') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcvue numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcvue IS 'AS400: VUELTO (OPCVUE)';
    END IF;

    -- OPCCTA (CTA)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opccta') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opccta varchar(15) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opccta IS 'AS400: CTA (OPCCTA)';
    END IF;

    -- OPCCRU (CRUCE OPMENSAJES)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opccru') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opccru varchar(1) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opccru IS 'AS400: CRUCE OPMENSAJES (OPCCRU)';
    END IF;

    -- OPCOI5 (OIDFACT P550)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcoi5') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcoi5 numeric(10,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcoi5 IS 'AS400: OIDFACT P550 (OPCOI5)';
    END IF;

    -- OPCILU (ILUMINACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcilu') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcilu varchar(1) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcilu IS 'AS400: ILUMINACION (OPCILU)';
    END IF;

    -- OPCPER (PERIODO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcper') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcper numeric(3,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcper IS 'AS400: PERIODO (OPCPER)';
    END IF;

    -- OPCES1 (ESTADO ENVIO EMAIL)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opces1') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opces1 varchar(1) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opces1 IS 'AS400: ESTADO ENVIO EMAIL (OPCES1)';
    END IF;

    -- OPCC07 (CODIGO RECAUDACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcc07') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcc07 varchar(15) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcc07 IS 'AS400: CODIGO RECAUDACION (OPCC07)';
    END IF;

    -- OPCNU1 (NUMERO RECAUDACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcnu1') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcnu1 numeric(10,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcnu1 IS 'AS400: NUMERO RECAUDACION (OPCNU1)';
    END IF;

    -- OPCRE8 (RETORNO1)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcre8') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcre8 varchar(4) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcre8 IS 'AS400: RETORNO1 (OPCRE8)';
    END IF;

    -- OPCRE9 (RETORNO2)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcre9') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcre9 varchar(4) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcre9 IS 'AS400: RETORNO2 (OPCRE9)';
    END IF;

    -- OPCF04 (FECHA RECAUDACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcf04') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcf04 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcf04 IS 'AS400: FECHA RECAUDACION (OPCF04)';
    END IF;

    -- OPCF05 (FECHA ACREDITACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcf05') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcf05 varchar(8) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcf05 IS 'AS400: FECHA ACREDITACION (OPCF05)';
    END IF;

    -- OPCDI6 (DIAS DEMORA RECAUDA)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcdi6') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcdi6 numeric(3,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcdi6 IS 'AS400: DIAS DEMORA RECAUDA (OPCDI6)';
    END IF;

    -- OPCDI7 (DIAS ACREDITACION)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar5' AND column_name='opcdi7') THEN
        ALTER TABLE mirror_raw.opcar5 ADD COLUMN opcdi7 numeric(3,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar5.opcdi7 IS 'AS400: DIAS ACREDITACION (OPCDI7)';
    END IF;

END $$;

-- ----------------------------
-- mirror_raw.opcar6 - columnas SNAP faltantes
-- ----------------------------
DO $$
BEGIN
    -- OPCDE9 (DESCUENTO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcde9') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcde9 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcde9 IS 'AS400: DESCUENTO (OPCDE9)';
    END IF;

    -- OPCIMP (IMPUESTO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcimp') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcimp numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcimp IS 'AS400: IMPUESTO (OPCIMP)';
    END IF;

    -- OPCPOR (PORCENTAJEDESCUENTO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcpor') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcpor numeric(3,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcpor IS 'AS400: PORCENTAJEDESCUENTO (OPCPOR)';
    END IF;

    -- OPCPO1 (PORCENTAJE IMPUETO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcpo1') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcpo1 numeric(3,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcpo1 IS 'AS400: PORCENTAJE IMPUETO (OPCPO1)';
    END IF;

    -- OPCVA2 (VALOR UNITARIO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcva2') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcva2 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcva2 IS 'AS400: VALOR UNITARIO (OPCVA2)';
    END IF;

    -- OPCVA3 (VALOR DESCUENTO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcva3') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcva3 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcva3 IS 'AS400: VALOR DESCUENTO (OPCVA3)';
    END IF;

    -- OPCVA4 (VALOR IMPUESTO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcva4') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcva4 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcva4 IS 'AS400: VALOR IMPUESTO (OPCVA4)';
    END IF;

    -- OPCVA5 (VALOR NETO)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcva5') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcva5 numeric(9,2) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcva5 IS 'AS400: VALOR NETO (OPCVA5)';
    END IF;

    -- OPCUBI (UBICACIONOID)
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                   WHERE table_schema='mirror_raw' AND table_name='opcar6' AND column_name='opcubi') THEN
        ALTER TABLE mirror_raw.opcar6 ADD COLUMN opcubi numeric(10,0) NULL;
        COMMENT ON COLUMN mirror_raw.opcar6.opcubi IS 'AS400: UBICACIONOID (OPCUBI)';
    END IF;

END $$;

-- ----------------------------
-- Refresco de vistas (mirror_clean) para asegurar coherencia
-- ----------------------------
CREATE OR REPLACE VIEW mirror_clean.v_usuario_as400 AS
SELECT
    u.usucod  AS codigo_usuario,
    u.usunom  AS nombres,
    u.usuape  AS apellidos,
    u.usucor  AS correo,
    u.usuest  AS estado_actividad,
    u.usuco4  AS codigo_rol,
    u.usuco5  AS codigo_ciudad,
    u.usuco6  AS codigo_dependencia,
    a.usuno1  AS nombre_corto,
    a.usucar  AS cargo,
    a.usunu1  AS telefono1,
    a.usunu2  AS telefono2,
    a.usuco7  AS correo_adicional,
    a.usuoid  AS oid_centro_contable,
    u._source_updated_at,
    u._mirror_synced_at,
    u._is_deleted
FROM mirror_raw.usuarc u
LEFT JOIN mirror_raw.usuar1 a
       ON a.usuco8 = u.usucod
WHERE COALESCE(u._is_deleted, false) = false;

CREATE OR REPLACE VIEW mirror_clean.v_ciaarc_activa AS
SELECT
    ciacod   AS codigo_oaci,
    ciaco2   AS codigo_iata,
    ciaco3   AS codigo_numero_cia,
    cianom   AS nombre_compania,
    ciaest,
    _mirror_synced_at
FROM mirror_raw.ciaarc
WHERE COALESCE(_is_deleted, false) = false
  AND TRIM(COALESCE(ciaest, '')) = 'AC';

-- Vista FR3 con columnas clave (consumo AOCR Financiero)
CREATE OR REPLACE VIEW mirror_clean.v_fr3_cabecera AS
SELECT
    h.opcsec  AS secuencial,
    h.opcaer  AS aeropuerto,
    h.opcano  AS anio,
    h.opcfe4  AS fecha_control_vuelo,
    h.opctip  AS tipo_operacion,
    h.opcrut  AS ruta_plan_vuelo,
    h.opcnro  AS num_aterriza_pais,
    h.opctot  AS total,
    h.opcgra  AS gran_total,
    h.opcson  AS gran_total_letras,
    h.opcaut  AS autorizacion,
    h.opcobs  AS observacion,
    h.opcoid  AS oid_cia_aviacion,
    h.opcori  AS origen,
    h.opcde7  AS destino,
    h.opcret  AS retorno,
    h.opccal  AS callsign,
    h.opcest  AS estado,
    h.opcru1  AS ruc,
    h.opcno4  AS nombre_cliente,
    h.opcdi3  AS direccion,
    h.opcte1  AS telefono,
    h.opcem1  AS email,
    h.opcnac  AS nac_inter,
    h.opcus7  AS usuario_creacion,
    h.opcda4  AS fecha_creacion,
    h.opch01  AS hora_creacion,
    h.opcno5  AS nombre_cia,
    h.opcmod  AS modelo,
    h.opcpes  AS peso_matricula,
    h.opcc08  AS codigo_oaci_cia,
    h.opcno6  AS nombre_aeropuerto,
    h.opcmat  AS matricula,
    h.opcva6  AS valor_charter,
    h.opcfor  AS forma_pago,
    h.opcban  AS codigo_banco,
    h.opcche  AS deposito,
    h.opcnum  AS numero_factura,
    h.opcfe9  AS fecha_recepcion,
    h.opcpro  AS procesado,
    h._source_updated_at,
    h._mirror_synced_at,
    h._is_deleted
FROM mirror_raw.opcar5 h
WHERE COALESCE(h._is_deleted, false) = false;

-- Vista FR3 detalle
CREATE OR REPLACE VIEW mirror_clean.v_fr3_detalle AS
SELECT
    d.opcse2  AS secuencial,
    d.opcae1  AS aeropuerto,
    d.opcan1  AS anio,
    d.opcse1  AS secuencial_detalle,
    d.opcti1  AS tipo_cobro,
    d.opcoi4  AS oid_formulario,
    d.opcc05  AS codigo_contable,
    d.opcde8  AS descripcion,
    d.opccan  AS cantidad,
    d.opcva1  AS valor,
    d.opchac  AS hacer_descuento,
    d.opccob  AS cobrar_impuesto,
    d.opcing  AS ingresar_cantidad,
    d.opcd01  AS descripcion_cuenta,
    d.opcc06  AS codigo,
    d.opcto1  AS total,
    d.opcubi  AS ubicacion_oid,
    d._mirror_synced_at,
    d._is_deleted
FROM mirror_raw.opcar6 d
WHERE COALESCE(d._is_deleted, false) = false;

-- Rollback de este parche (registrar en 999_rollback_mirror_sync.sql si es necesario):
-- ALTER TABLE mirror_raw.opcar5 DROP COLUMN IF EXISTS opcenv, DROP COLUMN IF EXISTS opcder, ...
-- (incluido como comentario, no ejecutar en el apply)

COMMENT ON VIEW mirror_clean.v_fr3_cabecera IS 'Vista consumo AOCR: FR3 cabecera activas (OPCAR5)';
COMMENT ON VIEW mirror_clean.v_fr3_detalle IS 'Vista consumo AOCR: FR3 detalles activos (OPCAR6)';
COMMENT ON VIEW mirror_clean.v_usuario_as400 IS 'Vista consumo AOCR: Usuarios AS400 con datos adicionales';
COMMENT ON VIEW mirror_clean.v_ciaarc_activa IS 'Vista consumo AOCR: Companias aereas activas (CIAARC)';
