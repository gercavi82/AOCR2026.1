-- AOCR FR3: conservar hora cruda OPCH01 y exponer hora formateada HH:MM:SS.
-- No modifica mirror_raw.

CREATE OR REPLACE VIEW mirror_clean.v_fr3_cabecera AS
SELECT
    opcsec AS secuencial_fr3,
    TRIM(BOTH FROM opcaer) AS aeropuerto_codigo,
    TRIM(BOTH FROM opcano) AS anio,
    TRIM(BOTH FROM opcnum) AS numero_documento,
    TRIM(BOTH FROM opctip) AS tipo_operacion_codigo,
    TRIM(BOTH FROM opcnac) AS nacional_internacional,
    CASE
        WHEN upper(TRIM(BOTH FROM COALESCE(opcnac, ''::text))) = 'N'::text THEN 'Nacional'::text
        WHEN upper(TRIM(BOTH FROM COALESCE(opcnac, ''::text))) = 'I'::text THEN 'Internacional'::text
        ELSE 'No determinado'::text
    END AS nacional_internacional_descripcion,
    TRIM(BOTH FROM opcban) AS banco_codigo,
    TRIM(BOTH FROM opcda4) AS fecha_registro_raw,
    CASE
        WHEN TRIM(BOTH FROM COALESCE(opcda4, ''::text)) ~ '^[0-9]{8}$'::text
             AND to_char(to_date(TRIM(BOTH FROM opcda4), 'YYYYMMDD'::text)::timestamp with time zone, 'YYYYMMDD'::text) = TRIM(BOTH FROM opcda4)
        THEN to_date(TRIM(BOTH FROM opcda4), 'YYYYMMDD'::text)
        ELSE NULL::date
    END AS fecha_registro,
    TRIM(BOTH FROM opcru1) AS ruc_cedula,
    TRIM(BOTH FROM opcno4) AS contribuyente_nombre,
    COALESCE(opcgra, 0::numeric) AS valor_total,
    TRIM(BOTH FROM opcest) AS estado_raw,
    TRIM(BOTH FROM opcfe4) AS fecha_control_vuelo_raw,
    CASE
        WHEN TRIM(BOTH FROM COALESCE(opcfe4, ''::text)) ~ '^[0-9]{8}$'::text
             AND to_char(to_date(TRIM(BOTH FROM opcfe4), 'YYYYMMDD'::text)::timestamp with time zone, 'YYYYMMDD'::text) = TRIM(BOTH FROM opcfe4)
        THEN to_date(TRIM(BOTH FROM opcfe4), 'YYYYMMDD'::text)
        ELSE NULL::date
    END AS fecha_control_vuelo,
    TRIM(BOTH FROM opcrut) AS ruta_plan_vuelo,
    opcnro AS numero_aterrizajes_pais,
    COALESCE(opctot, 0::numeric) AS total,
    COALESCE(opcgra, 0::numeric) AS gran_total,
    TRIM(BOTH FROM opcson) AS total_en_letras,
    TRIM(BOTH FROM opcaut) AS autorizacion,
    TRIM(BOTH FROM opcobs) AS observacion,
    opcoid AS oid_raw,
    TRIM(BOTH FROM opcori) AS origen_raw,
    TRIM(BOTH FROM opcde7) AS descripcion_raw,
    TRIM(BOTH FROM opcret) AS retencion_raw,
    TRIM(BOTH FROM opccal) AS calculo_raw,
    TRIM(BOTH FROM opcem1) AS email_principal,
    TRIM(BOTH FROM opcus7) AS usuario_creacion_raw,
    TRIM(BOTH FROM opch01) AS hora_creacion_raw,
    opcoi1 AS codigo_interno_1,
    TRIM(BOTH FROM opcte1) AS telefono_1,
    TRIM(BOTH FROM opcdi3) AS direccion_3,
    opcoi2 AS codigo_interno_2,
    COALESCE(opcva6, 0::numeric) AS valor_charter,
    TRIM(BOTH FROM opcfor) AS forma_pago_codigo,
    TRIM(BOTH FROM opcno5) AS compania_nombre,
    TRIM(BOTH FROM opcmod) AS modelo_aeronave,
    opcpes AS peso_aeronave,
    TRIM(BOTH FROM opcc08) AS codigo_oaci,
    TRIM(BOTH FROM opcno6) AS nombre_adicional,
    TRIM(BOTH FROM opcem2) AS email_adicional,
    TRIM(BOTH FROM opcmat) AS matricula,
    TRIM(BOTH FROM opcpro) AS procesado,
    opcsub AS subtotal_raw,
    opcoi3 AS codigo_interno_3,
    opcdi2 AS direccion_2_raw,
    TRIM(BOTH FROM opcfe9) AS fecha_9_raw,
    TRIM(BOTH FROM opcche) AS deposito,
    _source_updated_at AS source_updated_at,
    _mirror_synced_at AS mirror_synced_at,
    COALESCE(_is_deleted, false) AS is_deleted,
    _mirror_batch_id AS batch_id,
    CASE
        WHEN regexp_replace(TRIM(BOTH FROM COALESCE(opch01, ''::text)), '\D', '', 'g') = '' THEN NULL::text
        ELSE
            SUBSTRING(LPAD(RIGHT(regexp_replace(TRIM(BOTH FROM opch01), '\D', '', 'g'), 6), 6, '0') FROM 1 FOR 2)
            || ':' ||
            SUBSTRING(LPAD(RIGHT(regexp_replace(TRIM(BOTH FROM opch01), '\D', '', 'g'), 6), 6, '0') FROM 3 FOR 2)
            || ':' ||
            SUBSTRING(LPAD(RIGHT(regexp_replace(TRIM(BOTH FROM opch01), '\D', '', 'g'), 6), 6, '0') FROM 5 FOR 2)
    END AS hora_creacion
FROM mirror_raw.opcar5 o;

COMMENT ON VIEW mirror_clean.v_fr3_cabecera IS 'Vista consumo AOCR: FR3 cabecera activas (OPCAR5). Conserva OPCH01 crudo en hora_creacion_raw y expone hora_creacion HH:MM:SS.';
