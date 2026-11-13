--
-- PostgreSQL database dump
--

\restrict NmiQsbFfZhYuNYt7zVwsuyNS1knibEz0pmeTcvhuIYPq39ZB0OsBtn7lQl0sg0l

-- Dumped from database version 16.10 (Ubuntu 16.10-0ubuntu0.24.04.1)
-- Dumped by pg_dump version 18.1

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: mirror_clean; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA mirror_clean;


--
-- Name: mirror_raw; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA mirror_raw;


--
-- Name: sync; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA sync;


--
-- Name: pgcrypto; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pgcrypto WITH SCHEMA public;


--
-- Name: EXTENSION pgcrypto; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION pgcrypto IS 'cryptographic functions';


--
-- Name: aocr_audit_insert_compat(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_audit_insert_compat() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.fecha IS NULL AND NEW.fecha_accion IS NOT NULL THEN
        NEW.fecha := NEW.fecha_accion;
    END IF;
    IF NEW.fecha_accion IS NULL AND NEW.fecha IS NOT NULL THEN
        NEW.fecha_accion := NEW.fecha;
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: aocr_auditoria_estaciones_compat(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_auditoria_estaciones_compat() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.modulo = 'SOLICITUD_AOCR'
       AND NEW.accion = 'ACTUALIZAR_ESTACIONES_INSPECCION' THEN
        NEW.tabla_afectada := COALESCE(NEW.tabla_afectada, 'aocr_tbsolicitud_estacion');
        NEW.usuario := COALESCE(NEW.usuario, NEW.usuario_id::text, 'sistema');
        NEW.fecha := COALESCE(NEW.fecha, NEW.fecha_accion, CURRENT_TIMESTAMP);
        NEW.fecha_accion := NEW.fecha;
        NEW.fecha_hora := NEW.fecha;
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: aocr_conservar_numero_original(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_conservar_numero_original() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NULLIF(TRIM(OLD.numero_aocr),'') IS NOT NULL AND NEW.numero_aocr IS DISTINCT FROM OLD.numero_aocr THEN
        RAISE EXCEPTION 'El número AOCR asignado es inmutable';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: aocr_fn_nc_asignar_raiz(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_fn_nc_asignar_raiz() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.codigo_nc_raiz IS NULL THEN
        NEW.codigo_nc_raiz := NEW.codigo_no_conformidad;
    END IF;
    RETURN NEW;
END $$;


--
-- Name: aocr_or_calcular_totales(integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_or_calcular_totales(p_orden_id integer) RETURNS void
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_concepto_id integer;
    v_cantidad integer;

    v_tipo varchar(30);
    v_valor_base numeric(18,2);
    v_porc numeric(9,4);

    v_subtotal numeric(18,2);
    v_total numeric(18,2);
BEGIN
    SELECT concepto_id, cantidad
      INTO v_concepto_id, v_cantidad
    FROM public.aocr_orden_recaudacion
    WHERE id = p_orden_id;

    IF v_concepto_id IS NULL THEN
        RAISE EXCEPTION 'Orden % no existe o no tiene concepto', p_orden_id;
    END IF;

    SELECT tipo_calculo, valor_base, porcentaje_admin
      INTO v_tipo, v_valor_base, v_porc
    FROM public.aocr_or_concepto
    WHERE id = v_concepto_id AND activo = true;

    IF v_tipo IS NULL THEN
        RAISE EXCEPTION 'Concepto % no existe o está inactivo', v_concepto_id;
    END IF;

    IF v_tipo = 'FIJO' THEN
        v_subtotal := v_valor_base;
        v_total := v_valor_base;

    ELSIF v_tipo = 'POR_ESTACION' THEN
        v_subtotal := v_valor_base * v_cantidad;
        v_total := v_subtotal;

    ELSIF v_tipo = 'POR_DIA_MAS_PORC' THEN
        v_subtotal := v_valor_base * v_cantidad;
        v_total := v_subtotal * (1 + v_porc);

    ELSE
        RAISE EXCEPTION 'TipoCalculo no soportado: %', v_tipo;
    END IF;

    UPDATE public.aocr_orden_recaudacion
    SET
        valor_base_snap = v_valor_base,
        porc_admin_snap = v_porc,
        subtotal = round(v_subtotal, 2),
        total = round(v_total, 2)
    WHERE id = p_orden_id;
END;
$$;


--
-- Name: aocr_or_crear_orden(integer, integer, character varying, integer, integer, text); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_or_crear_orden(p_id_usuario integer, p_codigo_solicitud integer, p_concepto_principal_codigo character varying, p_estaciones integer DEFAULT 0, p_dias integer DEFAULT 0, p_observacion text DEFAULT NULL::text) RETURNS integer
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_orden_id INT;
    v_numero VARCHAR(30);
    v_concepto_principal_id INT;
    v_inspeccion_id INT;
    v_viaticos_id INT;
    v_valor NUMERIC(12,2);
    v_padm NUMERIC(12,4);
    v_nombre VARCHAR(200);
BEGIN
    -- Validaciones base
    IF p_id_usuario IS NULL OR p_id_usuario <= 0 THEN
        RAISE EXCEPTION 'ID de usuario inválido';
    END IF;

    -- Generar numero OR
    v_numero := 'OR-' || TO_CHAR(NOW(), 'YYYY') || '-' || LPAD(nextval('public.aocr_or_numero_seq')::text, 6, '0');

    -- Crear cabecera
    INSERT INTO public.aocr_or_orden(id_usuario, codigo_solicitud, numero_orden, estado, observacion)
    VALUES (p_id_usuario, p_codigo_solicitud, v_numero, 'BORRADOR', p_observacion)
    RETURNING id INTO v_orden_id;

    -- Concepto principal
    SELECT id, valor_base, porcentaje_admin, nombre
    INTO v_concepto_principal_id, v_valor, v_padm, v_nombre
    FROM public.aocr_or_concepto
    WHERE codigo = p_concepto_principal_codigo
      AND activo = true
    LIMIT 1;

    IF v_concepto_principal_id IS NULL THEN
        RAISE EXCEPTION 'Concepto principal % no existe o no está activo', p_concepto_principal_codigo;
    END IF;

    INSERT INTO public.aocr_or_orden_detalle(
        orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion,
        cantidad, valor_unitario, porcentaje_admin
    )
    VALUES (
        v_orden_id, v_concepto_principal_id, p_concepto_principal_codigo, v_nombre, v_nombre,
        1, v_valor, v_padm
    );

    -- Inspección por estación
    IF p_estaciones IS NOT NULL AND p_estaciones > 0 THEN
        SELECT id, valor_base, porcentaje_admin, nombre
        INTO v_inspeccion_id, v_valor, v_padm, v_nombre
        FROM public.aocr_or_concepto
        WHERE codigo = 'INSPECCION_POR_ESTACION'
          AND activo = true
        LIMIT 1;

        IF v_inspeccion_id IS NULL THEN
            RAISE EXCEPTION 'Concepto INSPECCION_POR_ESTACION no existe o no está activo';
        END IF;

        INSERT INTO public.aocr_or_orden_detalle(
            orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion,
            cantidad, valor_unitario, porcentaje_admin
        )
        VALUES (
            v_orden_id, v_inspeccion_id, 'INSPECCION_POR_ESTACION', v_nombre,
            'Inspección por estación solicitada',
            p_estaciones, v_valor, v_padm
        );
    END IF;

    -- Viáticos por día
    IF p_dias IS NOT NULL AND p_dias > 0 THEN
        SELECT id, valor_base, porcentaje_admin, nombre
        INTO v_viaticos_id, v_valor, v_padm, v_nombre
        FROM public.aocr_or_concepto
        WHERE codigo = 'VIATICOS_INSPECTORES'
          AND activo = true
        LIMIT 1;

        IF v_viaticos_id IS NULL THEN
            RAISE EXCEPTION 'Concepto VIATICOS_INSPECTORES no existe o no está activo';
        END IF;

        INSERT INTO public.aocr_or_orden_detalle(
            orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion,
            cantidad, valor_unitario, porcentaje_admin
        )
        VALUES (
            v_orden_id, v_viaticos_id, 'VIATICOS_INSPECTORES', v_nombre,
            'Viáticos por día requerido',
            p_dias, v_valor, v_padm
        );
    END IF;

    -- Recalcular
    PERFORM public.aocr_or_recalcular_orden(v_orden_id);

    RETURN v_orden_id;
END;
$$;


--
-- Name: aocr_or_detalle_after_recalc(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_or_detalle_after_recalc() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_orden_id INT;
BEGIN
    v_orden_id := COALESCE(NEW.orden_id, OLD.orden_id);
    PERFORM public.aocr_or_recalcular_orden(v_orden_id);
    RETURN COALESCE(NEW, OLD);
END;
$$;


--
-- Name: aocr_or_detalle_calcular_linea(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_or_detalle_calcular_linea() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_codigo_concepto TEXT;
    v_cantidad_facturable NUMERIC;
    v_numero_dias_inspeccion INTEGER;
    v_estado_orden TEXT;
BEGIN
    v_estado_orden := UPPER(TRIM(COALESCE(
        (SELECT o.estado FROM public.aocr_or_orden o WHERE o.id = NEW.orden_id),
        ''
    )));

    -- Una actualizacion accidental de un detalle historico no debe recalcular montos.
    IF TG_OP = 'UPDATE'
       AND v_estado_orden IN (
           'PAGO_APROBADO', 'APROBADO', 'PAGADO', 'COMPLETADA',
           'FACTURADA', 'FR3_VINCULADO', 'CERRADA', 'FINALIZADA'
       ) THEN
        NEW.subtotal := OLD.subtotal;
        NEW.admin := OLD.admin;
        NEW.total_linea := OLD.total_linea;
        NEW.numero_dias_inspeccion := OLD.numero_dias_inspeccion;
        NEW.dias_pagados_viatico := OLD.dias_pagados_viatico;
        RETURN NEW;
    END IF;

    v_codigo_concepto := UPPER(TRIM(COALESCE(
        NEW.concepto_codigo,
        (SELECT c.codigo FROM public.aocr_or_concepto c WHERE c.id = NEW.concepto_id),
        ''
    )));

    v_cantidad_facturable := COALESCE(NEW.cantidad, 0);
    IF v_codigo_concepto = 'VIATICOS_INSPECTOR' THEN
        -- En registros nuevos Cantidad conserva su significado normal (1 concepto)
        -- y los dias viven en una columna independiente. El fallback a cantidad
        -- mantiene compatibles los formularios o registros anteriores a esta migracion.
        v_numero_dias_inspeccion := COALESCE(NEW.numero_dias_inspeccion, NEW.cantidad, 0);
        NEW.numero_dias_inspeccion := v_numero_dias_inspeccion;
        NEW.dias_pagados_viatico := GREATEST(v_numero_dias_inspeccion - 1, 0);
        v_cantidad_facturable := NEW.dias_pagados_viatico;
    ELSE
        NEW.numero_dias_inspeccion := NULL;
        NEW.dias_pagados_viatico := NULL;
    END IF;

    NEW.subtotal := ROUND(v_cantidad_facturable * COALESCE(NEW.valor_unitario, 0), 2);
    NEW.admin := ROUND(COALESCE(NEW.subtotal, 0) * (COALESCE(NEW.porcentaje_admin, 0) / 100.0), 2);
    NEW.total_linea := ROUND(COALESCE(NEW.subtotal, 0) + COALESCE(NEW.admin, 0), 2);

    RETURN NEW;
END;
$$;


--
-- Name: aocr_or_recalcular_orden(integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_or_recalcular_orden(p_orden_id integer) RETURNS void
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_sub NUMERIC(12,2);
    v_adm NUMERIC(12,2);
    v_tot NUMERIC(12,2);
BEGIN
    SELECT
        COALESCE(SUM(subtotal),0),
        COALESCE(SUM(admin),0),
        COALESCE(SUM(total_linea),0)
    INTO v_sub, v_adm, v_tot
    FROM public.aocr_or_orden_detalle
    WHERE orden_id = p_orden_id;

    UPDATE public.aocr_or_orden
    SET subtotal = v_sub,
        admin = v_adm,
        total = v_tot
    WHERE id = p_orden_id;
END;
$$;


--
-- Name: aocr_validar_numero_gop_or(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.aocr_validar_numero_gop_or() RETURNS trigger
    LANGUAGE plpgsql
    AS $_$
DECLARE numero_gop text;
BEGIN
 IF TG_TABLE_NAME = 'aocr_or_orden' THEN
  IF NULLIF(TRIM(NEW.codigo_solicitud::text), '') IS NOT NULL
     AND NEW.codigo_solicitud::text <> '0' THEN
   SELECT numero_solicitud INTO numero_gop FROM public.aocr_tbsolicitud
    WHERE codigo_solicitud::text = NEW.codigo_solicitud::text;
   IF numero_gop IS NULL OR numero_gop !~ '^DGAC-GOP-[0-9]{4}-AOCR[0-9]+$'
      OR NEW.numero_orden IS DISTINCT FROM replace(numero_gop, 'DGAC-GOP-', 'DGAC-OR-') THEN
    RAISE EXCEPTION 'GOP y OR deben existir y compartir el mismo correlativo institucional';
   END IF;
  END IF;
 ELSE
  IF EXISTS (SELECT 1 FROM public.aocr_or_orden o
      WHERE o.codigo_solicitud::text = NEW.codigo_solicitud::text
        AND o.numero_orden IS DISTINCT FROM replace(NEW.numero_solicitud, 'DGAC-GOP-', 'DGAC-OR-')) THEN
   RAISE EXCEPTION 'No se puede desvincular el correlativo GOP de su OR';
  END IF;
 END IF;
 RETURN NEW;
END $_$;


--
-- Name: asignar_permiso_a_rol(integer, integer); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.asignar_permiso_a_rol(p_codigorol integer, p_idsubmenu integer) RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO permisos (
        codigorol,
        idsubmenu,
        activo,
        fecharegistro
    )
    VALUES (
        p_codigorol,
        p_idsubmenu,
        true,
        NOW()
    );
END;
$$;


--
-- Name: asignar_rol_a_usuario(character varying, integer, character varying); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.asignar_rol_a_usuario(p_codigousuario character varying, p_codigorol integer, p_usuarioadmin character varying) RETURNS void
    LANGUAGE plpgsql
    AS $$
BEGIN
    INSERT INTO usuario_rol (
        codigousuario,
        codigorol,
        fechaasignacion,
        usuariocreado,
        activo
    )
    VALUES (
        p_codigousuario,
        p_codigorol,
        NOW(),
        p_usuarioadmin,
        true
    );
END;
$$;


--
-- Name: fn_aocr_auditoria(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_aocr_auditoria() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF (TG_OP = 'INSERT') THEN
        INSERT INTO aocr_tbauditoria (
            tabla_afectada, registro_id, accion, usuario, 
            datos_nuevos, descripcion
        ) VALUES (
            TG_TABLE_NAME, NEW.codigo_solicitud, 'INSERT', 
            COALESCE(NEW.created_by, 'SYSTEM'), 
            row_to_json(NEW), 'Registro creado'
        );
        RETURN NEW;
    ELSIF (TG_OP = 'UPDATE') THEN
        INSERT INTO aocr_tbauditoria (
            tabla_afectada, registro_id, accion, usuario, 
            datos_anteriores, datos_nuevos, descripcion
        ) VALUES (
            TG_TABLE_NAME, NEW.codigo_solicitud, 'UPDATE', 
            COALESCE(NEW.updated_by, 'SYSTEM'), 
            row_to_json(OLD), row_to_json(NEW), 'Registro actualizado'
        );
        RETURN NEW;
    ELSIF (TG_OP = 'DELETE') THEN
        INSERT INTO aocr_tbauditoria (
            tabla_afectada, registro_id, accion, usuario, 
            datos_anteriores, descripcion
        ) VALUES (
            TG_TABLE_NAME, OLD.codigo_solicitud, 'DELETE', 
            'SYSTEM', row_to_json(OLD), 'Registro eliminado'
        );
        RETURN OLD;
    END IF;
    RETURN NULL;
END;
$$;


--
-- Name: fn_audit_subsanacion(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_audit_subsanacion() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF (TG_OP = 'UPDATE') THEN
        NEW.updated_at = CURRENT_TIMESTAMP;
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: fn_generar_numero_solicitud(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_generar_numero_solicitud() RETURNS character varying
    LANGUAGE plpgsql
    AS $$
DECLARE
    anio VARCHAR;
    consecutivo INTEGER;
    nuevo_numero VARCHAR;
BEGIN
    anio := TO_CHAR(CURRENT_DATE, 'YYYY');
    
    SELECT COALESCE(MAX(CAST(SUBSTRING(numero_solicitud FROM 11) AS INTEGER)), 0) + 1 
    INTO consecutivo 
    FROM aocr_tbsolicitud 
    WHERE numero_solicitud LIKE 'AOCR-' || anio || '-%';
    
    nuevo_numero := 'AOCR-' || anio || '-' || LPAD(consecutivo::TEXT, 4, '0');
    
    RETURN nuevo_numero;
END;
$$;


--
-- Name: fn_registrar_cambio_estado(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_registrar_cambio_estado() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF OLD.estado IS DISTINCT FROM NEW.estado THEN
        INSERT INTO aocr_tbhistorial_estado (
            codigo_solicitud, estado_anterior, estado_nuevo,
            codigo_usuario, observaciones
        ) VALUES (
            NEW.codigo_solicitud, OLD.estado, NEW.estado,
            NEW.codigo_usuario, 'Cambio automático'
        );
    END IF;
    RETURN NEW;
END;
$$;


--
-- Name: fn_vincular_solicitud_por_ruc(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.fn_vincular_solicitud_por_ruc() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
  -- Solo hacer si codigo_solicitud aún no está seteado
  IF NEW.codigo_solicitud IS NULL THEN
    UPDATE aocr_or_orden
    SET codigo_solicitud = s.codigo_solicitud
    FROM aocr_tbsolicitud s
    WHERE s.ruc = NEW.ruc_cedula
      AND s.estado = 'Pendiente'
      AND aocr_or_orden.id = NEW.id;
  END IF;

  RETURN NEW;
END;
$$;


--
-- Name: trg_aocr_or_calcular(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.trg_aocr_or_calcular() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
  PERFORM public.aocr_or_calcular_totales(NEW.id);
  RETURN NEW;
END;
$$;


--
-- Name: trg_asignar_numero_solicitud(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.trg_asignar_numero_solicitud() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.numero_solicitud IS NULL OR NEW.numero_solicitud = '' THEN
        NEW.numero_solicitud := fn_generar_numero_solicitud();
    END IF;
    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: opcar5; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.opcar5 (
    opcsec numeric(10,0) NOT NULL,
    opcaer text NOT NULL,
    opcano text NOT NULL,
    opcfe4 text,
    opctip text,
    opcrut text,
    opcnro integer,
    opctot numeric(15,2),
    opcgra numeric(15,2),
    opcson text,
    opcaut text,
    opcobs text,
    opcoid numeric(10,0),
    opcori text,
    opcde7 text,
    opcret text,
    opccal text,
    opcest text,
    opcru1 text,
    opcem1 text,
    opcnac text,
    opcus7 text,
    opcda4 text,
    opch01 text,
    opcoi1 numeric(10,0),
    opcte1 text,
    opcno4 text,
    opcdi3 text,
    opcoi2 numeric(10,0),
    opcva6 numeric(15,2),
    opcfor text,
    opcno5 text,
    opcmod text,
    opcpes numeric(15,4),
    opcc08 text,
    opcno6 text,
    opcem2 text,
    opcmat text,
    opcpro text,
    opcsub numeric(15,2),
    opcoi3 numeric(10,0),
    opcdi2 numeric(15,2),
    opcfe9 text,
    opcban text,
    opcche text,
    opcnum text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: v_fr3_cabecera; Type: VIEW; Schema: mirror_clean; Owner: -
--

CREATE VIEW mirror_clean.v_fr3_cabecera AS
 SELECT opcsec AS secuencial_fr3,
    TRIM(BOTH FROM opcaer) AS aeropuerto_codigo,
    TRIM(BOTH FROM opcano) AS anio,
    TRIM(BOTH FROM opcnum) AS numero_documento,
    TRIM(BOTH FROM opctip) AS tipo_operacion_codigo,
    TRIM(BOTH FROM opcnac) AS nacional_internacional,
        CASE
            WHEN (upper(TRIM(BOTH FROM COALESCE(opcnac, ''::text))) = 'N'::text) THEN 'Nacional'::text
            WHEN (upper(TRIM(BOTH FROM COALESCE(opcnac, ''::text))) = 'I'::text) THEN 'Internacional'::text
            ELSE 'No determinado'::text
        END AS nacional_internacional_descripcion,
    TRIM(BOTH FROM opcban) AS banco_codigo,
    TRIM(BOTH FROM opcda4) AS fecha_registro_raw,
        CASE
            WHEN ((TRIM(BOTH FROM COALESCE(opcda4, ''::text)) ~ '^[0-9]{8}$'::text) AND (to_char((to_date(TRIM(BOTH FROM opcda4), 'YYYYMMDD'::text))::timestamp with time zone, 'YYYYMMDD'::text) = TRIM(BOTH FROM opcda4))) THEN to_date(TRIM(BOTH FROM opcda4), 'YYYYMMDD'::text)
            ELSE NULL::date
        END AS fecha_registro,
    TRIM(BOTH FROM opcru1) AS ruc_cedula,
    TRIM(BOTH FROM opcno4) AS contribuyente_nombre,
    COALESCE(opcgra, (0)::numeric) AS valor_total,
    TRIM(BOTH FROM opcest) AS estado_raw,
    TRIM(BOTH FROM opcfe4) AS fecha_control_vuelo_raw,
        CASE
            WHEN ((TRIM(BOTH FROM COALESCE(opcfe4, ''::text)) ~ '^[0-9]{8}$'::text) AND (to_char((to_date(TRIM(BOTH FROM opcfe4), 'YYYYMMDD'::text))::timestamp with time zone, 'YYYYMMDD'::text) = TRIM(BOTH FROM opcfe4))) THEN to_date(TRIM(BOTH FROM opcfe4), 'YYYYMMDD'::text)
            ELSE NULL::date
        END AS fecha_control_vuelo,
    TRIM(BOTH FROM opcrut) AS ruta_plan_vuelo,
    opcnro AS numero_aterrizajes_pais,
    COALESCE(opctot, (0)::numeric) AS total,
    COALESCE(opcgra, (0)::numeric) AS gran_total,
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
    COALESCE(opcva6, (0)::numeric) AS valor_charter,
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
            WHEN (regexp_replace(TRIM(BOTH FROM COALESCE(opch01, ''::text)), '\D'::text, ''::text, 'g'::text) = ''::text) THEN NULL::text
            ELSE ((((SUBSTRING(lpad("right"(regexp_replace(TRIM(BOTH FROM opch01), '\D'::text, ''::text, 'g'::text), 6), 6, '0'::text) FROM 1 FOR 2) || ':'::text) || SUBSTRING(lpad("right"(regexp_replace(TRIM(BOTH FROM opch01), '\D'::text, ''::text, 'g'::text), 6), 6, '0'::text) FROM 3 FOR 2)) || ':'::text) || SUBSTRING(lpad("right"(regexp_replace(TRIM(BOTH FROM opch01), '\D'::text, ''::text, 'g'::text), 6), 6, '0'::text) FROM 5 FOR 2))
        END AS hora_creacion
   FROM mirror_raw.opcar5 o;


--
-- Name: VIEW v_fr3_cabecera; Type: COMMENT; Schema: mirror_clean; Owner: -
--

COMMENT ON VIEW mirror_clean.v_fr3_cabecera IS 'Vista consumo AOCR: FR3 cabecera activas (OPCAR5). Conserva OPCH01 crudo en hora_creacion_raw y expone hora_creacion HH:MM:SS.';


--
-- Name: opcar6; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.opcar6 (
    opcse2 numeric(10,0) NOT NULL,
    opcae1 text NOT NULL,
    opcan1 text NOT NULL,
    opcse1 numeric(10,0) NOT NULL,
    opcti1 text,
    opcoi4 numeric(10,0),
    opcc05 text,
    opcde8 text,
    opccan numeric(15,4),
    opcva1 numeric(15,2),
    opcde9 text,
    opcimp text,
    opchac text,
    opcpor numeric(10,4),
    opccob text,
    opcpo1 numeric(10,4),
    opcing text,
    opcd01 text,
    opcva2 numeric(15,2),
    opcva3 numeric(15,2),
    opcva4 numeric(15,2),
    opcc06 text,
    opcva5 numeric(15,2),
    opcto1 numeric(15,2),
    opcubi text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: v_fr3_detalle; Type: VIEW; Schema: mirror_clean; Owner: -
--

CREATE VIEW mirror_clean.v_fr3_detalle AS
 SELECT opcse2 AS secuencial,
    opcae1 AS aeropuerto,
    opcan1 AS anio,
    opcse1 AS secuencial_detalle,
    opcti1 AS tipo_cobro,
    opcoi4 AS oid_formulario,
    opcc05 AS codigo_contable,
    opcde8 AS descripcion,
    opccan AS cantidad,
    opcva1 AS valor,
    opchac AS hacer_descuento,
    opccob AS cobrar_impuesto,
    opcing AS ingresar_cantidad,
    opcd01 AS descripcion_cuenta,
    opcc06 AS codigo,
    opcto1 AS total,
    opcubi AS ubicacion_oid,
    _mirror_synced_at,
    COALESCE(_is_deleted, false) AS is_deleted
   FROM mirror_raw.opcar6 d;


--
-- Name: VIEW v_fr3_detalle; Type: COMMENT; Schema: mirror_clean; Owner: -
--

COMMENT ON VIEW mirror_clean.v_fr3_detalle IS 'Vista clean FR3 detalle desde mirror_raw.opcar6';


--
-- Name: _tombstone; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw._tombstone (
    id integer NOT NULL,
    table_name text NOT NULL,
    source_key text NOT NULL,
    deleted_at timestamp with time zone DEFAULT now(),
    batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _is_deleted boolean DEFAULT false NOT NULL,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_synced_at timestamp without time zone DEFAULT now() NOT NULL,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: _tombstone_id_seq; Type: SEQUENCE; Schema: mirror_raw; Owner: -
--

CREATE SEQUENCE mirror_raw._tombstone_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: _tombstone_id_seq; Type: SEQUENCE OWNED BY; Schema: mirror_raw; Owner: -
--

ALTER SEQUENCE mirror_raw._tombstone_id_seq OWNED BY mirror_raw._tombstone.id;


--
-- Name: ciaarc; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.ciaarc (
    ciacod text NOT NULL,
    ciaco2 text,
    ciaco3 text,
    cianom text,
    ciaest text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    ciaoid numeric(10,0),
    ciati1 text,
    ciadir text,
    ciaruc text,
    ciaema text,
    ciatel text,
    ciacel text,
    ciadi2 text,
    ciate1 text,
    ciacor text,
    ciarep text,
    ciano1 text,
    ciatip text,
    ciaciu text,
    ciaes1 text,
    ciaoi1 numeric(10,0),
    ciausu text,
    ciafec text,
    ciahor text,
    ciadis text,
    ciaus1 text,
    ciafe1 text,
    ciaho1 text,
    ciadi1 text,
    ciadi3 text,
    ciate2 text,
    ciace1 text,
    ciaem1 text,
    ciare1 text,
    ciaoi2 numeric(10,0),
    ciaco4 text,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: oidar2; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.oidar2 (
    oidoi2 text NOT NULL,
    oidco3 text NOT NULL,
    oidno2 text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: opcarc; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.opcarc (
    opccod text NOT NULL,
    opcsig text,
    opcco1 text,
    opcnom text,
    opcco2 text,
    opcno1 text,
    opcdir text,
    opcruc text,
    opcema text,
    opcrep text,
    opcus3 text,
    opcda2 text,
    opcho4 text,
    opcus4 text,
    opcda3 text,
    opcho5 text,
    opccel text,
    opctel text,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _is_deleted boolean DEFAULT false NOT NULL,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_synced_at timestamp without time zone DEFAULT now() NOT NULL,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: opiar2; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.opiar2 (
    opiced text NOT NULL,
    opino2 text,
    opies1 text,
    opitip text NOT NULL,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: opsarc; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.opsarc (
    opsaer text NOT NULL,
    opsano text NOT NULL,
    opssec numeric(10,0),
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: opuarc01; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.opuarc01 (
    opuoid text,
    opucod text NOT NULL,
    opuest text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: sync_state; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.sync_state (
    id integer NOT NULL,
    table_name text NOT NULL,
    last_watermark_date text,
    last_watermark_time text,
    last_sync_at timestamp with time zone DEFAULT now(),
    rows_synced bigint DEFAULT 0,
    batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _is_deleted boolean DEFAULT false NOT NULL,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_synced_at timestamp without time zone DEFAULT now() NOT NULL,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: sync_state_id_seq; Type: SEQUENCE; Schema: mirror_raw; Owner: -
--

CREATE SEQUENCE mirror_raw.sync_state_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: sync_state_id_seq; Type: SEQUENCE OWNED BY; Schema: mirror_raw; Owner: -
--

ALTER SEQUENCE mirror_raw.sync_state_id_seq OWNED BY mirror_raw.sync_state.id;


--
-- Name: txdgac; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.txdgac (
    valdds text NOT NULL,
    valval text NOT NULL,
    valdes text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: usuar1; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.usuar1 (
    usuco8 text NOT NULL,
    usutit text,
    usuti2 text,
    usuno1 text,
    usucar text,
    usunu1 text,
    usunu2 text,
    usuco7 text,
    usuus2 text,
    usufe2 text,
    usuho2 text,
    usudi2 text,
    usuus3 text,
    usufe3 text,
    usuho3 text,
    usudi3 text,
    usuoid text,
    usuco9 text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: usuarc; Type: TABLE; Schema: mirror_raw; Owner: -
--

CREATE TABLE mirror_raw.usuarc (
    usucod text NOT NULL,
    usunom text,
    usuape text,
    usutip text,
    usuced text,
    usucor text,
    usucla text,
    usuest text,
    usuti1 text,
    usuide text,
    usunum text,
    usuaux text,
    usuau1 text,
    usuau2 text,
    usuusu text,
    usufec text,
    usuhor text,
    usudis text,
    usuus1 text,
    usufe1 text,
    usuho1 text,
    usudi1 text,
    usuco1 text,
    usuco2 text,
    usuco3 text,
    usuco4 text,
    usuco5 text,
    usuco6 text,
    _mirror_synced_at timestamp with time zone DEFAULT now(),
    _is_deleted boolean DEFAULT false,
    _batch_id uuid,
    _source_updated_at timestamp without time zone,
    _source_op text,
    _row_hash text,
    _mirror_batch_id uuid,
    _mirror_deleted boolean DEFAULT false NOT NULL,
    _mirror_hash text,
    _mirror_source text DEFAULT 'AS400'::text
);


--
-- Name: acciones_personal; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.acciones_personal (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    tipo_movimiento_id bigint NOT NULL,
    numero_accion character varying(50),
    fecha_accion date NOT NULL,
    fecha_rige date NOT NULL,
    explicacion text,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: acciones_personal_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.acciones_personal_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: acciones_personal_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.acciones_personal_id_seq OWNED BY public.acciones_personal.id;


--
-- Name: accounts_compania; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.accounts_compania (
    id bigint NOT NULL,
    ruc character varying(20) NOT NULL,
    razon_social character varying(255) NOT NULL,
    nombre_comercial character varying(255) NOT NULL,
    email character varying(254) NOT NULL,
    activa boolean NOT NULL
);


--
-- Name: accounts_compania_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.accounts_compania ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.accounts_compania_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: accounts_perfil_usuario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.accounts_perfil_usuario (
    id bigint NOT NULL,
    identificacion character varying(50) NOT NULL,
    telefono character varying(30) NOT NULL,
    cargo character varying(120) NOT NULL,
    activo boolean NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_actualizacion timestamp with time zone NOT NULL,
    user_id bigint NOT NULL
);


--
-- Name: accounts_perfil_usuario_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.accounts_perfil_usuario ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.accounts_perfil_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: accounts_sesion_compania_activa; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.accounts_sesion_compania_activa (
    id bigint NOT NULL,
    fecha_seleccion timestamp with time zone NOT NULL,
    compania_id bigint NOT NULL,
    user_id bigint NOT NULL
);


--
-- Name: accounts_sesion_compania_activa_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.accounts_sesion_compania_activa ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.accounts_sesion_compania_activa_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: accounts_usuario_compania; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.accounts_usuario_compania (
    id bigint NOT NULL,
    rol_en_compania character varying(80) NOT NULL,
    activa boolean NOT NULL,
    fecha_asignacion timestamp with time zone NOT NULL,
    compania_id bigint NOT NULL,
    user_id bigint NOT NULL
);


--
-- Name: accounts_usuario_compania_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.accounts_usuario_compania ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.accounts_usuario_compania_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: aocr_asignacion_rt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_asignacion_rt (
    id integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    rt_cedula character varying(64) NOT NULL,
    rt_nombre character varying(200),
    rt_tipo character varying(10),
    fecha_asignacion timestamp without time zone DEFAULT now() NOT NULL,
    usuario_asigna character varying(120) NOT NULL,
    observacion text,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_asignacion_rt_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_asignacion_rt_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_asignacion_rt_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_asignacion_rt_id_seq OWNED BY public.aocr_asignacion_rt.id;


--
-- Name: aocr_audit_trail; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_audit_trail (
    id integer NOT NULL,
    tabla character varying(100) NOT NULL,
    registro_id integer,
    accion character varying(100) NOT NULL,
    campo_modificado character varying(100),
    valor_anterior text,
    valor_nuevo text,
    usuario_id integer,
    usuario_nombre character varying(100),
    ip_origen character varying(45),
    user_agent character varying(500),
    modulo character varying(100),
    correlacion_id character varying(100),
    metadata text,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    estado_anterior character varying(100),
    estado_nuevo character varying(100)
);


--
-- Name: aocr_audit_trail_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_audit_trail_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_audit_trail_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_audit_trail_id_seq OWNED BY public.aocr_audit_trail.id;


--
-- Name: aocr_correlativo_anual; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_correlativo_anual (
    id integer NOT NULL,
    anio integer NOT NULL,
    ultimo_numero integer DEFAULT 0 NOT NULL,
    fecha_actualizacion timestamp without time zone DEFAULT now()
);


--
-- Name: aocr_correlativo_anual_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_correlativo_anual_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_correlativo_anual_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_correlativo_anual_id_seq OWNED BY public.aocr_correlativo_anual.id;


--
-- Name: aocr_correlativo_orden; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_correlativo_orden (
    anio integer NOT NULL,
    ultimo_numero integer NOT NULL,
    fecha_actualizacion timestamp without time zone DEFAULT now()
);


--
-- Name: aocr_declaracion_historial; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_declaracion_historial (
    id integer NOT NULL,
    email character varying(120) NOT NULL,
    identificacion character varying(30),
    empresa_codigo character varying(20),
    empresa_nombre character varying(200),
    nombres character varying(80),
    apellidos character varying(80),
    aceptada boolean DEFAULT false NOT NULL,
    ip character varying(60),
    user_agent character varying(512),
    created_at timestamp without time zone,
    updated_at timestamp without time zone,
    finalized_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_declaracion_historial_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_declaracion_historial_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_declaracion_historial_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_declaracion_historial_id_seq OWNED BY public.aocr_declaracion_historial.id;


--
-- Name: aocr_declaracion_tmp; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_declaracion_tmp (
    id integer NOT NULL,
    email character varying(120) NOT NULL,
    identificacion character varying(30),
    empresa_codigo character varying(20),
    empresa_nombre character varying(200),
    nombres character varying(80),
    apellidos character varying(80),
    aceptada boolean DEFAULT false NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    ip character varying(60),
    user_agent character varying(512),
    expires_at timestamp without time zone DEFAULT (now() + '00:15:00'::interval) NOT NULL
);


--
-- Name: aocr_declaracion_tmp_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_declaracion_tmp_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_declaracion_tmp_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_declaracion_tmp_id_seq OWNED BY public.aocr_declaracion_tmp.id;


--
-- Name: aocr_entrega_destinatario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_entrega_destinatario (
    id bigint NOT NULL,
    entrega_id bigint NOT NULL,
    tipo_destinatario character varying(20) NOT NULL,
    usuario_id integer NOT NULL,
    correo character varying(255) NOT NULL,
    email_queue_id integer NOT NULL,
    estado_bandeja character varying(30) DEFAULT 'DISPONIBLE'::character varying NOT NULL,
    estado_correo character varying(40) DEFAULT 'ENCOLADO'::character varying NOT NULL,
    message_id character varying(255),
    ultimo_error text,
    fecha_envio timestamp without time zone,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_ac12_destinatario_tipo CHECK (((tipo_destinatario)::text = ANY ((ARRAY['RT'::character varying, 'INSPECTOR'::character varying])::text[])))
);


--
-- Name: aocr_entrega_destinatario_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_entrega_destinatario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_entrega_destinatario_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_entrega_destinatario_id_seq OWNED BY public.aocr_entrega_destinatario.id;


--
-- Name: aocr_entrega_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_entrega_documento (
    id bigint NOT NULL,
    entrega_id bigint NOT NULL,
    documento_id integer NOT NULL,
    tipo_documento character varying(60) NOT NULL,
    version_documento integer NOT NULL,
    nombre_archivo character varying(255) NOT NULL,
    ruta_fisica text NOT NULL,
    hash_sha256 character varying(64) NOT NULL,
    tamanio bigint NOT NULL,
    mime_type character varying(100) DEFAULT 'application/pdf'::character varying NOT NULL,
    nombre_firmante character varying(250),
    rol_firma character varying(50) NOT NULL,
    fecha_firma timestamp without time zone,
    vigente boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_ac12_documento_pdf CHECK ((((mime_type)::text = 'application/pdf'::text) AND (tamanio > 0))),
    CONSTRAINT ck_ac12_documento_tipo CHECK (((tipo_documento)::text = ANY ((ARRAY['RECONOCIMIENTO'::character varying, 'CONDICIONES_LIMITACIONES'::character varying])::text[])))
);


--
-- Name: aocr_entrega_documento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_entrega_documento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_entrega_documento_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_entrega_documento_id_seq OWNED BY public.aocr_entrega_documento.id;


--
-- Name: aocr_entrega_final; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_entrega_final (
    id bigint NOT NULL,
    solicitud_id integer NOT NULL,
    inspeccion_id integer NOT NULL,
    codigo_compania character varying(100) NOT NULL,
    compania character varying(300) NOT NULL,
    version_aocr integer NOT NULL,
    version_cl integer NOT NULL,
    estado character varying(50) NOT NULL,
    correlation_id character varying(80) NOT NULL,
    event_key character varying(200) NOT NULL,
    fecha_completada timestamp without time zone,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by integer NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_ac12_entrega_estado CHECK (((estado)::text = ANY ((ARRAY['ENTREGA_NO_SOLICITADA'::character varying, 'ENTREGA_ENCOLADA'::character varying, 'ENTREGA_EN_PROCESO'::character varying, 'ENTREGA_PARCIAL'::character varying, 'ENTREGA_COMPLETA'::character varying, 'ENTREGA_FALLIDA_REINTENTABLE'::character varying, 'ENTREGA_FALLIDA_DEFINITIVA'::character varying])::text[]))),
    CONSTRAINT ck_ac12_versiones_positivas CHECK (((version_aocr > 0) AND (version_cl > 0)))
);


--
-- Name: aocr_entrega_final_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_entrega_final_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_entrega_final_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_entrega_final_id_seq OWNED BY public.aocr_entrega_final.id;


--
-- Name: aocr_entrega_intento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_entrega_intento (
    id bigint NOT NULL,
    entrega_id bigint NOT NULL,
    email_queue_id integer NOT NULL,
    estado character varying(40) NOT NULL,
    detalle_error text,
    message_id character varying(255),
    fecha timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_entrega_intento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_entrega_intento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_entrega_intento_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_entrega_intento_id_seq OWNED BY public.aocr_entrega_intento.id;


--
-- Name: aocr_evento_workflow; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_evento_workflow (
    id bigint NOT NULL,
    evento character varying(80) NOT NULL,
    event_key character varying(300) NOT NULL,
    correlation_id character varying(80) NOT NULL,
    modulo character varying(80),
    accion character varying(100),
    entidad character varying(100),
    entidad_id integer,
    solicitud_id integer,
    inspeccion_id integer,
    informe_id integer,
    nc_id integer,
    documento_id integer,
    estado_anterior character varying(100),
    estado_nuevo character varying(100),
    usuario_id integer,
    usuario character varying(150),
    rol character varying(100),
    ip character varying(64),
    observacion text,
    version integer,
    hash character varying(128),
    resultado character varying(40) DEFAULT 'REGISTRADO'::character varying NOT NULL,
    detalle_error text,
    intentos integer DEFAULT 1 NOT NULL,
    fecha timestamp without time zone DEFAULT now() NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_aocr_evento_intentos CHECK ((intentos > 0))
);


--
-- Name: aocr_evento_workflow_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_evento_workflow_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_evento_workflow_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_evento_workflow_id_seq OWNED BY public.aocr_evento_workflow.id;


--
-- Name: aocr_expediente_archivo; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_expediente_archivo (
    id bigint NOT NULL,
    compania_id character varying(50) NOT NULL,
    codigo_oaci character varying(4) NOT NULL,
    solicitud_id integer NOT NULL,
    numero_solicitud character varying(100) NOT NULL,
    tipo_documento character varying(50) NOT NULL,
    version integer NOT NULL,
    nombre_original character varying(255) NOT NULL,
    nombre_guardado character varying(255) NOT NULL,
    ruta_relativa character varying(1000) NOT NULL,
    sha256 character(64) NOT NULL,
    mime_type character varying(100) NOT NULL,
    tamano_bytes bigint NOT NULL,
    estado character varying(30) DEFAULT 'ACTIVO'::character varying NOT NULL,
    usuario_creacion character varying(100) NOT NULL,
    fecha_creacion_utc timestamp with time zone DEFAULT now() NOT NULL,
    archivo_anterior_id bigint,
    activo boolean DEFAULT true NOT NULL,
    fr3_operacion_id bigint,
    CONSTRAINT ck_aocr_expediente_tamano CHECK ((tamano_bytes >= 0)),
    CONSTRAINT ck_aocr_expediente_version CHECK ((version > 0))
);


--
-- Name: aocr_expediente_archivo_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_expediente_archivo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_expediente_archivo_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_expediente_archivo_id_seq OWNED BY public.aocr_expediente_archivo.id;


--
-- Name: aocr_expediente_compania; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_expediente_compania (
    id integer NOT NULL,
    usuario_rt_id integer NOT NULL,
    compania_codigo character varying(50) NOT NULL,
    compania_nombre character varying(200),
    anio integer NOT NULL,
    estado character varying(50) DEFAULT 'ACTIVO'::character varying NOT NULL,
    fecha_creacion timestamp without time zone DEFAULT now(),
    fecha_actualizacion timestamp without time zone DEFAULT now(),
    observacion text
);


--
-- Name: aocr_expediente_compania_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_expediente_compania_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_expediente_compania_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_expediente_compania_id_seq OWNED BY public.aocr_expediente_compania.id;


--
-- Name: aocr_fix_backup_20260825_historial4; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fix_backup_20260825_historial4 (
    codigo_historial integer,
    codigo_solicitud integer,
    estado_anterior character varying(50),
    estado_nuevo character varying(50),
    codigo_usuario integer,
    observaciones text,
    fecha_cambio timestamp without time zone
);


--
-- Name: aocr_fix_backup_20260825_inspeccion4; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fix_backup_20260825_inspeccion4 (
    codigo_inspeccion integer,
    codigo_solicitud integer,
    numero_inspeccion character varying(50),
    tipo integer,
    fecha_programada date,
    hora_programada time without time zone,
    fecha_realizada date,
    hora_inicio time without time zone,
    hora_fin time without time zone,
    codigo_inspector integer,
    lugar character varying(300),
    resultado character varying(50),
    comentarios text,
    observaciones_generales text,
    hallazgos_principales text,
    recomendaciones text,
    estado character varying(50),
    completada boolean,
    aprobada boolean,
    created_at timestamp without time zone,
    updated_at timestamp without time zone,
    created_by character varying(100),
    updated_by character varying(100),
    informe_pdf text,
    historial_estados text,
    estado_documental character varying(50),
    resultado_evaluacion character varying(50)
);


--
-- Name: aocr_fix_backup_20260825_orden3; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fix_backup_20260825_orden3 (
    id integer,
    codigo_usuario integer,
    codigo_solicitud character varying(50),
    numero_orden character varying(30),
    fecha_creacion timestamp with time zone,
    estado character varying(20),
    observacion text,
    subtotal numeric(12,2),
    admin numeric(12,2),
    total numeric(12,2),
    lugar_emision character varying(100),
    compania character varying(100),
    ruc_cedula character varying(20),
    correo character varying(100),
    telefono character varying(20),
    concepto_id integer,
    numero_fr3 character varying(50),
    fecha_fr3 timestamp without time zone,
    fecha_actualizacion timestamp without time zone,
    compania_codigo character varying(50),
    expediente_id integer
);


--
-- Name: aocr_fix_backup_20260825_solicitud4; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fix_backup_20260825_solicitud4 (
    codigo_solicitud integer,
    numero_solicitud character varying(50),
    fecha_solicitud timestamp without time zone,
    tipo_solicitud integer,
    estado character varying(50),
    nombre_operador character varying(200),
    ruc character varying(20),
    razon_social character varying(250),
    email character varying(200),
    telefono character varying(50),
    direccion text,
    ciudad character varying(100),
    provincia character varying(100),
    pais character varying(100),
    representante_legal character varying(150),
    cedula_representante character varying(20),
    tipo_operacion character varying(100),
    descripcion_operacion text,
    observaciones text,
    codigo_usuario integer,
    created_at timestamp without time zone,
    updated_at timestamp without time zone,
    created_by character varying(100),
    updated_by character varying(100),
    deleted_at timestamp without time zone,
    deleted_by character varying(100),
    codigo_tecnico integer,
    fecha_recepcion timestamp without time zone,
    fecha_solicitud_subsanacion timestamp without time zone,
    fecha_subsanacion timestamp without time zone,
    fecha_aprobacion_coordinador timestamp without time zone,
    fecha_emision_aocr timestamp without time zone,
    fecha_entrega_aocr timestamp without time zone,
    numero_aocr character varying(50),
    ruta_archivo_pdf_aocr character varying(500),
    codigo_usuario_aprobacion_coordinador integer,
    codigo_usuario_aprobacion_director integer,
    pago_aprobado boolean,
    fecha_aprobacion_pago timestamp without time zone,
    modulo_solicitud_rt_habilitado boolean,
    notificado_coordinador_pago_aprobado boolean,
    fecha_notificacion_coordinador_pago timestamp without time zone,
    notificado_rt_modulo_habilitado boolean,
    fecha_notificacion_rt_modulo_habilitado timestamp without time zone,
    pendiente_asignacion_inspector boolean,
    pendiente_carga_documental_rt boolean,
    solicitud_finalizada_rt boolean,
    fecha_finalizacion_rt timestamp without time zone,
    requiere_nueva_orden boolean,
    resumen_operaciones_eae text,
    numero_aoc character varying(100),
    aprobaciones_especiales text,
    aprobaciones_especiales_otros text,
    aeropuertos_ecuador text,
    aeropuertos_ecuador_otros text,
    companias_seleccionadas text,
    codigo_oaci character varying(10),
    correo_representante_tecnico character varying(200),
    nombre_comercial character varying(200)
);


--
-- Name: aocr_fr3_integracion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fr3_integracion (
    id bigint NOT NULL,
    orden_recaudacion_id integer NOT NULL,
    secuencial_fr3 character varying(80),
    aeropuerto character varying(20),
    anio integer,
    numero_factura character varying(120),
    estado_integracion character varying(50) DEFAULT 'NO_GENERADO'::character varying NOT NULL,
    fecha_envio_as400 timestamp without time zone,
    mensaje_as400 text,
    payload_json jsonb,
    respuesta_json jsonb,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_aocr_fr3_integracion_estado CHECK (((estado_integracion)::text = ANY (ARRAY[('NO_GENERADO'::character varying)::text, ('PENDIENTE_ENVIO'::character varying)::text, ('PENDIENTE_FR3'::character varying)::text, ('PENDIENTE_OPCNAC'::character varying)::text, ('ENVIADO_AS400'::character varying)::text, ('GENERADO'::character varying)::text, ('VINCULADO_AS400'::character varying)::text, ('VINCULADO_DESDE_ESPEJO'::character varying)::text, ('VINCULADO_AS400_PENDIENTE_SYNC'::character varying)::text, ('PENDIENTE_OPCTIP'::character varying)::text, ('ERROR_AS400'::character varying)::text, ('ERROR_OPCTIP'::character varying)::text, ('ANULADO'::character varying)::text])))
);


--
-- Name: aocr_fr3_integracion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_fr3_integracion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_fr3_integracion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_fr3_integracion_id_seq OWNED BY public.aocr_fr3_integracion.id;


--
-- Name: aocr_fr3_operacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fr3_operacion (
    id bigint NOT NULL,
    solicitud_id integer NOT NULL,
    orden_recaudacion_id integer NOT NULL,
    compania_id character varying(50) NOT NULL,
    aeropuerto character varying(4),
    anio character varying(4),
    secuencial numeric(15,0),
    estado character varying(30) NOT NULL,
    correlation_id character varying(64) NOT NULL,
    intentos integer DEFAULT 1 NOT NULL,
    error_codigo character varying(80),
    error_detalle_seguro character varying(500),
    fecha_inicio_utc timestamp with time zone DEFAULT now() NOT NULL,
    fecha_fin_utc timestamp with time zone,
    usuario_id integer,
    etapa_actual character varying(40),
    idempotency_key character varying(128) NOT NULL,
    cantidad_detalles_esperada integer,
    cantidad_detalles_confirmada integer,
    total_esperado numeric(15,2),
    total_confirmado numeric(15,2),
    error_tabla character varying(20),
    error_operacion character varying(40),
    fecha_actualizacion_utc timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_aocr_fr3_operacion_estado CHECK (((estado)::text = ANY (ARRAY[('PENDIENTE'::character varying)::text, ('VALIDANDO'::character varying)::text, ('BLOQUEADO'::character varying)::text, ('SECUENCIAL_RESERVADO'::character varying)::text, ('CABECERA_CONFIRMADA'::character varying)::text, ('DETALLES_PARCIALES'::character varying)::text, ('DETALLES_CONFIRMADOS'::character varying)::text, ('COMPLETADO'::character varying)::text, ('FALLIDO_REINTENTABLE'::character varying)::text, ('FALLIDO_NO_REINTENTABLE'::character varying)::text, ('REQUIERE_RECONCILIACION'::character varying)::text]))),
    CONSTRAINT ck_aocr_fr3_operacion_secuencial CHECK (((secuencial IS NULL) OR (secuencial > (0)::numeric)))
);


--
-- Name: aocr_fr3_operacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_fr3_operacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_fr3_operacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_fr3_operacion_id_seq OWNED BY public.aocr_fr3_operacion.id;


--
-- Name: aocr_fr3_outbox; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fr3_outbox (
    id integer NOT NULL,
    event_key character varying(128) NOT NULL,
    orden_id integer NOT NULL,
    pago_id integer,
    estado character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    intentos integer DEFAULT 0 NOT NULL,
    proximo_intento timestamp with time zone,
    worker_id character varying(100),
    lock_until timestamp with time zone,
    payload text,
    error_last text,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_fr3_outbox_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_fr3_outbox_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_fr3_outbox_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_fr3_outbox_id_seq OWNED BY public.aocr_fr3_outbox.id;


--
-- Name: aocr_fr3_retry_queue; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fr3_retry_queue (
    id integer NOT NULL,
    orden_id integer NOT NULL,
    pago_id integer,
    numero_factura character varying(50),
    autorizacion character varying(100),
    estado character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    intentos integer DEFAULT 0 NOT NULL,
    max_intentos integer DEFAULT 5 NOT NULL,
    ultimo_error text,
    proximo_intento timestamp with time zone DEFAULT now() NOT NULL,
    factor_backoff integer DEFAULT 1 NOT NULL,
    prioridad integer DEFAULT 0 NOT NULL,
    usuario_creacion character varying(100),
    usuario_ultimo character varying(100),
    correlacion_id character varying(100),
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    fecha_actualizacion timestamp with time zone DEFAULT now() NOT NULL,
    fecha_completado timestamp with time zone,
    fr3_numero character varying(50),
    fr3_secuencial numeric(15,0)
);


--
-- Name: aocr_fr3_retry_queue_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_fr3_retry_queue_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_fr3_retry_queue_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_fr3_retry_queue_id_seq OWNED BY public.aocr_fr3_retry_queue.id;


--
-- Name: aocr_fr3_tipo_operacion_map; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_fr3_tipo_operacion_map (
    id integer NOT NULL,
    tipo_solicitud integer,
    codigo_concepto text,
    accion text,
    opc_nac character(1),
    opctip text NOT NULL,
    descripcion text,
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: aocr_fr3_tipo_operacion_map_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_fr3_tipo_operacion_map_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_fr3_tipo_operacion_map_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_fr3_tipo_operacion_map_id_seq OWNED BY public.aocr_fr3_tipo_operacion_map.id;


--
-- Name: aocr_idempotency_key; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_idempotency_key (
    id integer NOT NULL,
    clave character varying(128) NOT NULL,
    operacion character varying(50) NOT NULL,
    orden_id integer,
    resultado text,
    estado character varying(20) DEFAULT 'PROCESANDO'::character varying NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    fecha_expiracion timestamp with time zone DEFAULT (now() + '24:00:00'::interval) NOT NULL,
    usuario character varying(100)
);


--
-- Name: aocr_idempotency_key_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_idempotency_key_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_idempotency_key_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_idempotency_key_id_seq OWNED BY public.aocr_idempotency_key.id;


--
-- Name: aocr_inspector_reasignacion_historial; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_inspector_reasignacion_historial (
    id integer NOT NULL,
    solicitud_id integer NOT NULL,
    inspector_anterior_id integer,
    inspector_nuevo_id integer NOT NULL,
    coordinador_id integer NOT NULL,
    motivo text,
    estado character varying(80) NOT NULL,
    fecha_creacion timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_inspector_reasignacion_historial_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_inspector_reasignacion_historial_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_inspector_reasignacion_historial_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_inspector_reasignacion_historial_id_seq OWNED BY public.aocr_inspector_reasignacion_historial.id;


--
-- Name: aocr_jefatura_aeropuerto_correo; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_jefatura_aeropuerto_correo (
    estacion_codigo character varying(10) NOT NULL,
    correo character varying(254) NOT NULL,
    nombre character varying(200) DEFAULT ''::character varying NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT aocr_jefatura_aeropuerto_correo_correo_check CHECK ((POSITION(('@'::text) IN (correo)) > 1)),
    CONSTRAINT aocr_jefatura_aeropuerto_correo_estacion_codigo_check CHECK (((estacion_codigo)::text = upper(TRIM(BOTH FROM estacion_codigo))))
);


--
-- Name: aocr_numero_certificado_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_numero_certificado_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_operador_espejo_as400; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_operador_espejo_as400 (
    id bigint NOT NULL,
    oid_cia_aviacion character varying(50),
    codigo_oaci_cia character varying(20),
    ruc character varying(20),
    nombre_cia character varying(255) NOT NULL,
    email character varying(254),
    telefono character varying(50),
    direccion text,
    estado character varying(50) NOT NULL,
    fuente character varying(20) NOT NULL,
    activo boolean NOT NULL,
    ultimo_sync timestamp with time zone,
    raw_data jsonb,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_actualizacion timestamp with time zone NOT NULL
);


--
-- Name: aocr_operador_espejo_as400_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.aocr_operador_espejo_as400 ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.aocr_operador_espejo_as400_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: aocr_or_concepto; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_or_concepto (
    id integer NOT NULL,
    codigo character varying(30) NOT NULL,
    nombre character varying(200) NOT NULL,
    tipo_calculo character varying(30) NOT NULL,
    valor_base numeric(18,2) DEFAULT 0 NOT NULL,
    porcentaje_admin numeric(9,4) DEFAULT 0 NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    orden integer DEFAULT 1 NOT NULL,
    descripcion text DEFAULT ''::text NOT NULL,
    por_estacion boolean DEFAULT false NOT NULL,
    por_dia boolean DEFAULT false NOT NULL,
    es_viatico boolean DEFAULT false,
    CONSTRAINT ck_aocr_or_concepto_porc CHECK ((porcentaje_admin >= (0)::numeric)),
    CONSTRAINT ck_aocr_or_concepto_tipo CHECK (((tipo_calculo)::text = ANY (ARRAY[('FIJO'::character varying)::text, ('POR_ESTACION'::character varying)::text, ('POR_DIA_MAS_PORC'::character varying)::text, ('POR_DIA'::character varying)::text]))),
    CONSTRAINT ck_aocr_or_concepto_valor CHECK ((valor_base >= (0)::numeric))
);


--
-- Name: aocr_or_concepto_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.aocr_or_concepto ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.aocr_or_concepto_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: aocr_or_correccion_financiera; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_or_correccion_financiera (
    id bigint NOT NULL,
    orden_id integer NOT NULL,
    solicitud_id integer NOT NULL,
    estado_original text NOT NULL,
    estado text DEFAULT 'PENDIENTE'::text NOT NULL,
    motivo text NOT NULL,
    solicitado_por integer NOT NULL,
    solicitado_en timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    orden_anterior jsonb NOT NULL,
    detalles_anteriores jsonb NOT NULL,
    corregido_por integer,
    corregido_en timestamp without time zone,
    observacion_financiera text,
    validado_por integer,
    validado_en timestamp without time zone,
    referencia_validacion text,
    comprobante_rectificado_ruta text,
    monto_verificado numeric(18,2),
    factura_rectificada_ruta text
);


--
-- Name: aocr_or_correccion_financiera_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_or_correccion_financiera_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_or_correccion_financiera_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_or_correccion_financiera_id_seq OWNED BY public.aocr_or_correccion_financiera.id;


--
-- Name: aocr_or_numero_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_or_numero_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_or_orden; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_or_orden (
    id integer NOT NULL,
    codigo_usuario integer,
    codigo_solicitud character varying(50),
    numero_orden character varying(30) NOT NULL,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    estado character varying(20) DEFAULT 'BORRADOR'::character varying NOT NULL,
    observacion text,
    subtotal numeric(12,2) DEFAULT 0 NOT NULL,
    admin numeric(12,2) DEFAULT 0 NOT NULL,
    total numeric(12,2) DEFAULT 0 NOT NULL,
    lugar_emision character varying(100),
    compania character varying(100),
    ruc_cedula character varying(20),
    correo character varying(100),
    telefono character varying(20),
    concepto_id integer,
    numero_fr3 character varying(50),
    fecha_fr3 timestamp without time zone,
    fecha_actualizacion timestamp without time zone,
    compania_codigo character varying(50),
    expediente_id integer,
    CONSTRAINT chk_estado CHECK (((estado)::text = ANY (ARRAY[('BORRADOR'::character varying)::text, ('PENDIENTE'::character varying)::text, ('PENDIENTE_PAGO'::character varying)::text, ('PAGO_CARGADO'::character varying)::text, ('PROCESADA'::character varying)::text, ('GENERADA'::character varying)::text, ('ENVIADA'::character varying)::text, ('EN_REVISION_FINANCIERA'::character varying)::text, ('DEVUELTA'::character varying)::text, ('OBSERVADA'::character varying)::text, ('PAGO_APROBADO'::character varying)::text, ('FR3_PENDIENTE'::character varying)::text, ('FR3_ENVIADO_AS400'::character varying)::text, ('FR3_VINCULADO'::character varying)::text, ('COMPLETADA'::character varying)::text, ('FACTURADA'::character varying)::text, ('PAGADA'::character varying)::text, ('ANULADA'::character varying)::text, ('ORDEN_CERRADA_POR_SOLICITUD'::character varying)::text, ('ORDEN_INACTIVA'::character varying)::text])))
);


--
-- Name: aocr_or_orden_detalle; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_or_orden_detalle (
    id integer NOT NULL,
    orden_id integer NOT NULL,
    concepto_id integer NOT NULL,
    concepto_codigo character varying(60) NOT NULL,
    concepto_nombre character varying(200) NOT NULL,
    descripcion text,
    cantidad numeric(12,2) DEFAULT 1 NOT NULL,
    valor_unitario numeric(12,2) DEFAULT 0 NOT NULL,
    porcentaje_admin numeric(12,4) DEFAULT 0 NOT NULL,
    subtotal numeric(12,2) DEFAULT 0 NOT NULL,
    admin numeric(12,2) DEFAULT 0 NOT NULL,
    total_linea numeric(12,2) DEFAULT 0 NOT NULL,
    lugar_inspeccion character varying(200),
    provincia_inspeccion character varying(150),
    numero_dias_inspeccion integer,
    dias_pagados_viatico integer
);


--
-- Name: aocr_or_orden_detalle_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.aocr_or_orden_detalle ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.aocr_or_orden_detalle_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: aocr_or_orden_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.aocr_or_orden ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.aocr_or_orden_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: aocr_orden_recaudacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_orden_recaudacion (
    id integer NOT NULL,
    numero_or character varying(50),
    codigo_usuario integer NOT NULL,
    codigo_solicitud integer,
    fecha_emision timestamp without time zone DEFAULT now() NOT NULL,
    lugar_emision character varying(60) DEFAULT 'Quito'::character varying NOT NULL,
    compania character varying(200) NOT NULL,
    ruc_cedula character varying(20) NOT NULL,
    correo character varying(120),
    telefono character varying(50),
    concepto_id integer NOT NULL,
    cantidad integer DEFAULT 1 NOT NULL,
    estaciones character varying(50),
    observacion character varying(500),
    valor_base_snap numeric(18,2) DEFAULT 0 NOT NULL,
    porc_admin_snap numeric(9,4) DEFAULT 0 NOT NULL,
    subtotal numeric(18,2) DEFAULT 0 NOT NULL,
    total numeric(18,2) DEFAULT 0 NOT NULL,
    completa boolean DEFAULT false NOT NULL,
    fecha_completa timestamp without time zone,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by character varying(120),
    solicitud_id integer,
    compania_id integer,
    numero_orden character varying(80),
    estado_orden character varying(50),
    valor_administracion numeric(14,2) DEFAULT 0,
    total_orden numeric(14,2) DEFAULT 0,
    total_pagado_aprobado numeric(14,2) DEFAULT 0,
    saldo_pendiente numeric(14,2) DEFAULT 0,
    fecha_creacion timestamp without time zone DEFAULT now(),
    fecha_generacion_pdf timestamp without time zone,
    ruta_pdf text,
    usuario_creacion character varying(120),
    activo boolean DEFAULT true,
    CONSTRAINT ck_aocr_or_cantidad CHECK ((cantidad > 0))
);


--
-- Name: aocr_orden_recaudacion_detalle; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_orden_recaudacion_detalle (
    id bigint NOT NULL,
    orden_recaudacion_id integer NOT NULL,
    codigo_concepto character varying(80),
    descripcion_concepto text,
    cantidad numeric(14,2) DEFAULT 0 NOT NULL,
    valor_unitario numeric(14,2) DEFAULT 0 NOT NULL,
    porcentaje_administracion numeric(8,4) DEFAULT 0 NOT NULL,
    valor_administracion numeric(14,2) DEFAULT 0 NOT NULL,
    total_linea numeric(14,2) DEFAULT 0 NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: aocr_orden_recaudacion_detalle_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_orden_recaudacion_detalle_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_orden_recaudacion_detalle_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_orden_recaudacion_detalle_id_seq OWNED BY public.aocr_orden_recaudacion_detalle.id;


--
-- Name: aocr_orden_recaudacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.aocr_orden_recaudacion ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.aocr_orden_recaudacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: aocr_pago_comprobante; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_pago_comprobante (
    id bigint NOT NULL,
    orden_recaudacion_id integer NOT NULL,
    monto_reportado numeric(14,2) DEFAULT 0 NOT NULL,
    monto_validado numeric(14,2),
    referencia character varying(120),
    metodo_pago character varying(80),
    banco character varying(120),
    ruta_archivo text,
    nombre_archivo character varying(255),
    estado_pago character varying(50) DEFAULT 'CARGADO_RT'::character varying NOT NULL,
    observacion_rt text,
    observacion_financiero text,
    usuario_carga_id integer,
    fecha_carga timestamp without time zone DEFAULT now() NOT NULL,
    usuario_validacion_id integer,
    fecha_validacion timestamp without time zone,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_aocr_pago_comprobante_estado CHECK (((estado_pago)::text = ANY (ARRAY[('CARGADO_RT'::character varying)::text, ('PENDIENTE_VALIDACION_FINANCIERA'::character varying)::text, ('APROBADO_FINANCIERO'::character varying)::text, ('RECHAZADO_FINANCIERO'::character varying)::text, ('ANULADO'::character varying)::text])))
);


--
-- Name: aocr_pago_comprobante_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_pago_comprobante_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_pago_comprobante_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_pago_comprobante_id_seq OWNED BY public.aocr_pago_comprobante.id;


--
-- Name: aocr_proceso_estado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_proceso_estado (
    id integer NOT NULL,
    solicitud_id integer NOT NULL,
    orden_recaudacion_id integer,
    inspeccion_id integer,
    informe_id integer,
    estado_actual character varying(100) NOT NULL,
    etapa_actual character varying(100),
    rol_responsable character varying(100),
    usuario_responsable_id integer,
    siguiente_accion character varying(150),
    observacion text,
    fecha_estado timestamp without time zone DEFAULT now() NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by integer DEFAULT 0 NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_by integer DEFAULT 0 NOT NULL
);


--
-- Name: aocr_proceso_estado_historial; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_proceso_estado_historial (
    id integer NOT NULL,
    solicitud_id integer NOT NULL,
    orden_recaudacion_id integer,
    inspeccion_id integer,
    informe_id integer,
    estado_anterior character varying(100),
    estado_nuevo character varying(100) NOT NULL,
    accion character varying(150),
    rol_usuario character varying(100),
    usuario_id integer,
    observacion text,
    fecha_creacion timestamp without time zone DEFAULT now() NOT NULL,
    etapa character varying(150),
    rol_responsable character varying(100),
    usuario_responsable_id integer,
    ip character varying(50),
    correlation_id character varying(100),
    clave_idempotencia character varying(100),
    resultado character varying(50)
);


--
-- Name: aocr_proceso_estado_historial_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_proceso_estado_historial_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_proceso_estado_historial_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_proceso_estado_historial_id_seq OWNED BY public.aocr_proceso_estado_historial.id;


--
-- Name: aocr_proceso_estado_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_proceso_estado_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_proceso_estado_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_proceso_estado_id_seq OWNED BY public.aocr_proceso_estado.id;


--
-- Name: aocr_proceso_idempotencia; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_proceso_idempotencia (
    clave character varying(100) NOT NULL,
    solicitud_id integer NOT NULL,
    fecha_registro timestamp without time zone DEFAULT now() NOT NULL,
    aocr_id integer,
    condiciones_id integer,
    estado_anterior character varying(100),
    estado_nuevo character varying(100),
    resultado character varying(50),
    correlation_id character varying(100)
);


--
-- Name: aocr_revision_documental_coordinador; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_revision_documental_coordinador (
    id integer NOT NULL,
    solicitud_id integer NOT NULL,
    inspector_original_id integer,
    inspector_confirmado_id integer,
    coordinador_id integer,
    documento_oficio_id integer,
    numero_oficio character varying(80),
    estado character varying(80) NOT NULL,
    observacion_inspector text,
    observacion_coordinador text,
    fecha_finalizacion_inspector timestamp without time zone,
    fecha_decision_coordinador timestamp without time zone,
    fecha_habilitacion_lv timestamp without time zone,
    fecha_habilitacion_informe timestamp without time zone,
    fecha_creacion timestamp without time zone DEFAULT now() NOT NULL,
    fecha_actualizacion timestamp without time zone DEFAULT now() NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    CONSTRAINT chk_aocr_revdoc_coord_estado CHECK ((((estado)::text = ANY (ARRAY[('PENDIENTE_REVISION_INSPECTOR'::character varying)::text, ('REVISION_INSPECTOR_EN_PROCESO'::character varying)::text, ('PENDIENTE_COORDINADOR'::character varying)::text, ('PENDIENTE_REVISION_COORDINADOR'::character varying)::text, ('DEVUELTO_INSPECTOR'::character varying)::text, ('PENDIENTE_DIRCAV'::character varying)::text, ('OBSERVADA_POR_COORDINADOR'::character varying)::text, ('ACEPTADA_POR_COORDINADOR'::character varying)::text])) OR ((estado)::text = ANY ((ARRAY['PENDIENTE_ACEPTACION_DCAV'::character varying, 'ACEPTADA_POR_DCAV'::character varying])::text[]))))
);


--
-- Name: aocr_revision_documental_coordinador_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_revision_documental_coordinador_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_revision_documental_coordinador_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_revision_documental_coordinador_id_seq OWNED BY public.aocr_revision_documental_coordinador.id;


--
-- Name: aocr_solicitud_rt_revision; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_solicitud_rt_revision (
    id bigint NOT NULL,
    solicitud_rt_id bigint NOT NULL,
    inspector_usuario character varying(150) NOT NULL,
    coordinador_usuario_id integer NOT NULL,
    estado character varying(40) DEFAULT 'ASIGNADA'::character varying NOT NULL,
    resultado character varying(20),
    observacion text,
    fecha_asignacion timestamp without time zone DEFAULT now() NOT NULL,
    fecha_revision timestamp without time zone,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_aocr_rt_revision_estado CHECK (((estado)::text = ANY ((ARRAY['ASIGNADA'::character varying, 'EN_REVISION'::character varying, 'DEVUELTA_COORDINADOR'::character varying, 'CERRADA'::character varying])::text[]))),
    CONSTRAINT ck_aocr_rt_revision_resultado CHECK (((resultado IS NULL) OR ((resultado)::text = ANY ((ARRAY['CONFORME'::character varying, 'OBSERVADA'::character varying, 'RECHAZADA'::character varying])::text[]))))
);


--
-- Name: aocr_solicitud_rt_revision_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_solicitud_rt_revision_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_solicitud_rt_revision_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_solicitud_rt_revision_id_seq OWNED BY public.aocr_solicitud_rt_revision.id;


--
-- Name: aocr_sync_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_sync_log (
    id integer NOT NULL,
    operacion character varying(50) NOT NULL,
    sistema_origen character varying(20) DEFAULT 'AOCR'::character varying NOT NULL,
    sistema_destino character varying(20) DEFAULT 'AS400'::character varying NOT NULL,
    orden_id integer,
    pago_id integer,
    idempotency_key character varying(128),
    estado character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    detalle_request text,
    detalle_response text,
    error_mensaje text,
    error_codigo character varying(50),
    intentos integer DEFAULT 0 NOT NULL,
    max_intentos integer DEFAULT 3 NOT NULL,
    proximo_reintento timestamp with time zone,
    inicio_operacion timestamp with time zone DEFAULT now() NOT NULL,
    fin_operacion timestamp with time zone,
    duracion_ms bigint,
    usuario character varying(100),
    ip_origen character varying(45),
    correlacion_id character varying(100),
    fr3_numero character varying(50),
    fr3_secuencial numeric(15,0),
    fr3_aeropuerto character varying(10),
    fr3_anio character varying(4),
    metadata text,
    fecha_creacion timestamp with time zone DEFAULT now() NOT NULL,
    fecha_actualizacion timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_sync_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_sync_log_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_sync_log_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_sync_log_id_seq OWNED BY public.aocr_sync_log.id;


--
-- Name: aocr_tb_factura_pago; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tb_factura_pago (
    id integer NOT NULL,
    orden_id integer NOT NULL,
    pago_id integer,
    numero_factura character varying(80) NOT NULL,
    autorizacion_factura character varying(80),
    fecha_emision date NOT NULL,
    subtotal numeric(18,2) NOT NULL,
    iva numeric(18,2) NOT NULL,
    total numeric(18,2) NOT NULL,
    observaciones text,
    file_name character varying(255) NOT NULL,
    content_type character varying(120) NOT NULL,
    file_size bigint NOT NULL,
    file_path text NOT NULL,
    creado_por character varying(120),
    creado_en timestamp without time zone DEFAULT now() NOT NULL,
    fr3_estado character varying(30),
    fr3_numero character varying(80),
    fr3_secuencial numeric(18,0),
    fr3_aeropuerto character varying(10),
    fr3_anio character varying(4),
    fr3_error text,
    fr3_generado_en timestamp without time zone,
    fr3_reintentos integer DEFAULT 0 NOT NULL,
    updated_at timestamp without time zone,
    CONSTRAINT chk_factura_pago_totales_non_negative CHECK (((subtotal >= (0)::numeric) AND (iva >= (0)::numeric) AND (total > (0)::numeric)))
);


--
-- Name: aocr_tb_factura_pago_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tb_factura_pago_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tb_factura_pago_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tb_factura_pago_id_seq OWNED BY public.aocr_tb_factura_pago.id;


--
-- Name: aocr_tb_sync_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tb_sync_log (
    id bigint NOT NULL,
    idempotency_key character varying(200) NOT NULL,
    orden_id integer NOT NULL,
    pago_id integer,
    modulo character varying(50) NOT NULL,
    operacion character varying(100) NOT NULL,
    estado character varying(30) NOT NULL,
    mensaje text,
    fr3_numero character varying(80),
    payload jsonb,
    intentos integer DEFAULT 0 NOT NULL,
    usuario character varying(120),
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone
);


--
-- Name: aocr_tb_sync_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tb_sync_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tb_sync_log_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tb_sync_log_id_seq OWNED BY public.aocr_tb_sync_log.id;


--
-- Name: aocr_tbaeronave; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbaeronave (
    codigo_aeronave integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    matricula character varying(20) NOT NULL,
    marca character varying(100),
    modelo character varying(100),
    numero_serie character varying(100),
    anio_fabricacion integer,
    capacidad_pasajeros integer,
    capacidad_carga numeric(10,2),
    tipo_motor character varying(50),
    numero_motores integer,
    activo boolean DEFAULT true,
    fecha_registro timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    designador_oaci character varying(10)
);


--
-- Name: aocr_tbaeronave_codigo_aeronave_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbaeronave_codigo_aeronave_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbaeronave_codigo_aeronave_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbaeronave_codigo_aeronave_seq OWNED BY public.aocr_tbaeronave.codigo_aeronave;


--
-- Name: aocr_tbaeronave_solicitud; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbaeronave_solicitud (
    codigo_aeronave_solicitud integer NOT NULL,
    codigosolicitud integer NOT NULL,
    marca character varying(50) NOT NULL,
    modelo character varying(50) NOT NULL,
    serie character varying(50),
    matricula character varying(20) NOT NULL,
    configuracion character varying(50),
    etapa_ruido character varying(20),
    fecha_registro timestamp without time zone DEFAULT now() NOT NULL,
    usuario_registro character varying(50)
);


--
-- Name: aocr_tbaeronave_solicitud_codigo_aeronave_solicitud_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbaeronave_solicitud_codigo_aeronave_solicitud_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbaeronave_solicitud_codigo_aeronave_solicitud_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbaeronave_solicitud_codigo_aeronave_solicitud_seq OWNED BY public.aocr_tbaeronave_solicitud.codigo_aeronave_solicitud;


--
-- Name: aocr_tbauditoria; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbauditoria (
    codigo_auditoria integer NOT NULL,
    tabla_afectada character varying(100) NOT NULL,
    registro_id integer,
    accion character varying(50) NOT NULL,
    usuario character varying(100) NOT NULL,
    fecha_accion timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    fecha_hora timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    ip_address character varying(50),
    datos_anteriores jsonb,
    datos_nuevos jsonb,
    descripcion text,
    detalle text,
    modulo character varying(100),
    resultado character varying(20),
    mensaje_error text,
    usuario_id integer,
    fecha timestamp without time zone,
    entidad character varying(100)
);


--
-- Name: COLUMN aocr_tbauditoria.usuario_id; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbauditoria.usuario_id IS 'ID del usuario que realizó la acción (referencia a tabla usuario.idusuario)';


--
-- Name: COLUMN aocr_tbauditoria.fecha; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbauditoria.fecha IS 'Timestamp de la acción (alias para fecha_accion para compatibilidad)';


--
-- Name: COLUMN aocr_tbauditoria.entidad; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbauditoria.entidad IS 'Nombre de la entidad auditada (compatibilidad con AocrDesignacionDAO)';


--
-- Name: aocr_tbauditoria_codigo_auditoria_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbauditoria_codigo_auditoria_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbauditoria_codigo_auditoria_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbauditoria_codigo_auditoria_seq OWNED BY public.aocr_tbauditoria.codigo_auditoria;


--
-- Name: aocr_tbcertificado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbcertificado (
    codigo_certificado integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    numero_certificado character varying(50) NOT NULL,
    fecha_emision date NOT NULL,
    fecha_vencimiento date NOT NULL,
    vigencia_anios integer DEFAULT 2,
    estado character varying(50) DEFAULT 'Vigente'::character varying,
    condiciones_especiales text,
    firmado_por character varying(100),
    ruta_pdf text,
    codigo_verificacion character varying(20),
    emitido_por character varying(100),
    aprobado_por character varying(100),
    CONSTRAINT chk_estado_certificado CHECK (((estado)::text = ANY (ARRAY[('Vigente'::character varying)::text, ('Vencido'::character varying)::text, ('Suspendido'::character varying)::text, ('Revocado'::character varying)::text])))
);


--
-- Name: COLUMN aocr_tbcertificado.emitido_por; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbcertificado.emitido_por IS 'Nombre del Director de Certificacion Aeronautica y Vigilancia Continua para Condiciones y Limitaciones.';


--
-- Name: COLUMN aocr_tbcertificado.aprobado_por; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbcertificado.aprobado_por IS 'Nombre del Director General de Aviacion Civil para el reconocimiento AOCR.';


--
-- Name: aocr_tbcertificado_codigo_certificado_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbcertificado_codigo_certificado_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbcertificado_codigo_certificado_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbcertificado_codigo_certificado_seq OWNED BY public.aocr_tbcertificado.codigo_certificado;


--
-- Name: aocr_tbchecklist; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbchecklist (
    codigo_checklist integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    seccion character varying(100),
    item_numero character varying(10),
    descripcion text NOT NULL,
    cumple boolean,
    observaciones text,
    criticidad character varying(20)
);


--
-- Name: aocr_tbchecklist_codigo_checklist_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbchecklist_codigo_checklist_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbchecklist_codigo_checklist_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbchecklist_codigo_checklist_seq OWNED BY public.aocr_tbchecklist.codigo_checklist;


--
-- Name: aocr_tbchecklist_item; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbchecklist_item (
    codigo_item integer NOT NULL,
    codigo_formulario character varying(20) NOT NULL,
    descripcion character varying(300) NOT NULL,
    orden integer NOT NULL,
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: aocr_tbchecklist_item_codigo_item_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbchecklist_item_codigo_item_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbchecklist_item_codigo_item_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbchecklist_item_codigo_item_seq OWNED BY public.aocr_tbchecklist_item.codigo_item;


--
-- Name: aocr_tbchecklist_solicitud; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbchecklist_solicitud (
    codigo_checklist_solicitud integer NOT NULL,
    codigosolicitud integer NOT NULL,
    codigo_item integer NOT NULL,
    cumple boolean,
    observacion character varying(500),
    fecha_registro timestamp without time zone DEFAULT now() NOT NULL,
    usuario_registro character varying(50)
);


--
-- Name: aocr_tbchecklist_solicitud_codigo_checklist_solicitud_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbchecklist_solicitud_codigo_checklist_solicitud_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbchecklist_solicitud_codigo_checklist_solicitud_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbchecklist_solicitud_codigo_checklist_solicitud_seq OWNED BY public.aocr_tbchecklist_solicitud.codigo_checklist_solicitud;


--
-- Name: aocr_tbcondiciones_limitaciones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbcondiciones_limitaciones (
    id integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_inspeccion integer,
    codigo_informe integer,
    numero_aocr character varying(100),
    version integer DEFAULT 1 NOT NULL,
    estado character varying(50) DEFAULT 'CL_BORRADOR'::character varying NOT NULL,
    vigente boolean DEFAULT true NOT NULL,
    compania character varying(250),
    operador_extranjero character varying(250),
    representante_tecnico character varying(250),
    tipo_operacion character varying(100),
    rutas_autorizadas text,
    alcance_autorizado text,
    condiciones_aprobadas text,
    limitaciones text,
    observaciones text,
    inspector_usuario_id integer,
    inspector_nombre character varying(200),
    fecha_generacion timestamp without time zone DEFAULT now() NOT NULL,
    coordinador_usuario_id integer,
    coordinador_nombre character varying(200),
    observacion_coordinador text,
    fecha_revision_coordinador timestamp without time zone,
    dircav_usuario_id integer,
    dircav_nombre character varying(200),
    observacion_dircav text,
    fecha_firma_dircav timestamp without time zone,
    ruta_pdf_borrador character varying(500),
    ruta_pdf_firmado character varying(500),
    hash_pdf character varying(128),
    hash_pdf_firmado character varying(128),
    tamanio_pdf bigint,
    codigo_verificacion character varying(64),
    version_concurrencia bigint DEFAULT 1 NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    CONSTRAINT ck_cl_estado CHECK (((estado)::text = ANY ((ARRAY['CL_NO_GENERADA'::character varying, 'CL_BORRADOR'::character varying, 'CL_PENDIENTE_COORDINADOR'::character varying, 'CL_DEVUELTA_INSPECTOR'::character varying, 'CL_PENDIENTE_DIRCAV'::character varying, 'CL_DEVUELTA_COORDINADOR'::character varying, 'CL_PENDIENTE_FIRMA_DIRCAV'::character varying, 'CL_FIRMADA_DIRCAV'::character varying, 'CL_ANULADA'::character varying, 'CL_REEMPLAZADA'::character varying])::text[])))
);


--
-- Name: aocr_tbcondiciones_limitaciones_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbcondiciones_limitaciones_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbcondiciones_limitaciones_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbcondiciones_limitaciones_id_seq OWNED BY public.aocr_tbcondiciones_limitaciones.id;


--
-- Name: aocr_tbcorreo_institucional; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbcorreo_institucional (
    codigo_correo integer NOT NULL,
    codigo_area character varying(80) NOT NULL,
    nombre_area character varying(150) NOT NULL,
    correo_principal character varying(250) NOT NULL,
    correos_cc text,
    correos_bcc text,
    descripcion text,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone,
    created_by character varying(100),
    updated_by character varying(100)
);


--
-- Name: aocr_tbcorreo_institucional_codigo_correo_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbcorreo_institucional_codigo_correo_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbcorreo_institucional_codigo_correo_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbcorreo_institucional_codigo_correo_seq OWNED BY public.aocr_tbcorreo_institucional.codigo_correo;


--
-- Name: aocr_tbcorreo_institucional_historial; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbcorreo_institucional_historial (
    codigo_historial integer NOT NULL,
    codigo_correo integer NOT NULL,
    codigo_area character varying(80) NOT NULL,
    correo_anterior character varying(250),
    correo_nuevo character varying(250),
    cc_anterior text,
    cc_nuevo text,
    bcc_anterior text,
    bcc_nuevo text,
    usuario_modificacion character varying(100),
    fecha_modificacion timestamp without time zone DEFAULT now() NOT NULL,
    accion character varying(50) NOT NULL
);


--
-- Name: aocr_tbcorreo_institucional_historial_codigo_historial_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbcorreo_institucional_historial_codigo_historial_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbcorreo_institucional_historial_codigo_historial_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbcorreo_institucional_historial_codigo_historial_seq OWNED BY public.aocr_tbcorreo_institucional_historial.codigo_historial;


--
-- Name: aocr_tbdesignacion_inspector; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbdesignacion_inspector (
    id integer NOT NULL,
    solicitud_id integer NOT NULL,
    inspeccion_id integer,
    estacion_id integer,
    inspector_id integer NOT NULL,
    inspector_cedula character varying(30) NOT NULL,
    inspector_nombre character varying(200) NOT NULL,
    inspector_apoyo_cedula character varying(30),
    inspector_apoyo_nombre character varying(200),
    dircav_usuario_id integer NOT NULL,
    dircav_usuario_nombre character varying(200),
    estado character varying(80) DEFAULT 'DESIGNACION_PENDIENTE_FIRMA_DIRCAV'::character varying NOT NULL,
    motivo text,
    version integer DEFAULT 1 NOT NULL,
    vigente boolean DEFAULT true NOT NULL,
    fecha_designacion timestamp without time zone DEFAULT now() NOT NULL,
    fecha_firma timestamp without time zone,
    creado_en timestamp without time zone DEFAULT now() NOT NULL,
    creado_por character varying(100),
    actualizado_en timestamp without time zone,
    actualizado_por character varying(100),
    ruta_pdf character varying(500),
    ruta_documento_firmado character varying(500),
    hash_documento character varying(256),
    firmado boolean DEFAULT false NOT NULL,
    usuario_firma character varying(200),
    tamanio_bytes bigint,
    mime_type character varying(100) DEFAULT 'application/pdf'::character varying NOT NULL,
    huella_certificado character varying(100),
    codigo_verificacion character varying(100)
);


--
-- Name: TABLE aocr_tbdesignacion_inspector; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.aocr_tbdesignacion_inspector IS 'Registro formal e histórico de designaciones y reasignaciones de inspectores por DIRCAV (AC-05).';


--
-- Name: aocr_tbdesignacion_inspector_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbdesignacion_inspector_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbdesignacion_inspector_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbdesignacion_inspector_id_seq OWNED BY public.aocr_tbdesignacion_inspector.id;


--
-- Name: aocr_tbdocumento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbdocumento (
    codigo_documento integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    tipo_documento character varying(100) NOT NULL,
    nombre_archivo character varying(255) NOT NULL,
    ruta_guardada character varying(500),
    tipo character varying(100),
    hash_archivo character varying(500),
    tamano_bytes bigint,
    extension character varying(20),
    estado character varying(50) DEFAULT 'Cargado'::character varying,
    validado boolean DEFAULT false,
    fecha_carga timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    fecha_validacion timestamp without time zone,
    validado_por character varying(100),
    observaciones text,
    version integer DEFAULT 1,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    created_by character varying(100),
    nombre_original character varying(500),
    nombre_visible character varying(500),
    nombre_fisico character varying(500),
    CONSTRAINT chk_estado_documento CHECK (((estado IS NULL) OR ((estado)::text = ANY (ARRAY[('Cargado'::character varying)::text, ('En Revisión'::character varying)::text, ('Aprobado'::character varying)::text, ('Rechazado'::character varying)::text, ('Subsanado'::character varying)::text, ('PENDIENTE_REVISION'::character varying)::text, ('PENDIENTE_REVISION_SUBSANACION'::character varying)::text, ('ACEPTADO'::character varying)::text, ('APROBADO'::character varying)::text, ('OBSERVADO'::character varying)::text, ('DEVUELTO'::character varying)::text, ('DEVUELTO_INSPECTOR'::character varying)::text, ('PENDIENTE_SUBSANACION'::character varying)::text, ('SUBSANADO_RT'::character varying)::text, ('SUBSANADO'::character varying)::text, ('SUBSANACION'::character varying)::text, ('EN_REVISION_INSPECTOR'::character varying)::text, ('RECHAZADO'::character varying)::text, ('BLOQUEADO'::character varying)::text, ('VERSION_ANTERIOR'::character varying)::text, ('CARGADO'::character varying)::text]))))
);


--
-- Name: aocr_tbdocumento_codigo_documento_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbdocumento_codigo_documento_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbdocumento_codigo_documento_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbdocumento_codigo_documento_seq OWNED BY public.aocr_tbdocumento.codigo_documento;


--
-- Name: aocr_tbdocumento_generado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbdocumento_generado (
    codigo_documento integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_inspeccion integer,
    tipo_documento character varying(80) NOT NULL,
    numero_aocr character varying(120),
    nombre_archivo character varying(260),
    ruta_documento character varying(500),
    tamanio_pdf bigint,
    estado character varying(80) DEFAULT 'GENERADO'::character varying NOT NULL,
    fecha_generacion timestamp without time zone DEFAULT now() NOT NULL,
    codigo_usuario integer,
    usuario_nombre character varying(160),
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    hash_pdf_firmado character varying(128),
    fecha_liberacion timestamp without time zone,
    codigo_usuario_liberacion integer,
    disponible_rt boolean DEFAULT false NOT NULL,
    fecha_disponible_rt timestamp without time zone,
    codigo_compania character varying(160),
    codigo_inspector integer,
    version integer DEFAULT 1 NOT NULL,
    vigente boolean DEFAULT true NOT NULL,
    eliminado boolean DEFAULT false NOT NULL,
    usuario_creador_id integer,
    fecha_actualizacion timestamp without time zone,
    version_documento integer DEFAULT 1 NOT NULL,
    completo boolean DEFAULT false NOT NULL,
    bloqueado boolean DEFAULT false NOT NULL,
    hash_pdf character varying(128),
    ruta_pdf_firmado character varying(500),
    tamanio_pdf_firmado bigint,
    codigo_usuario_firma integer,
    rol_firma character varying(100),
    fecha_firma timestamp without time zone,
    version_concurrencia bigint DEFAULT 1 NOT NULL
);


--
-- Name: aocr_tbdocumento_generado_codigo_documento_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbdocumento_generado_codigo_documento_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbdocumento_generado_codigo_documento_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbdocumento_generado_codigo_documento_seq OWNED BY public.aocr_tbdocumento_generado.codigo_documento;


--
-- Name: aocr_tbdocumento_inspeccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbdocumento_inspeccion (
    codigo_documento integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    codigo_informe integer,
    codigo_documento_base integer,
    version integer DEFAULT 1 NOT NULL,
    tipo_documento character varying(150),
    nombre_archivo_original character varying(255),
    nombre_archivo_storage character varying(255),
    ruta_archivo character varying(600) NOT NULL,
    hash_archivo character varying(64),
    tamano_bytes bigint,
    content_type character varying(150),
    observacion text,
    subido_por_rol character varying(60),
    codigo_usuario integer,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_tbdocumento_inspeccion_codigo_documento_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbdocumento_inspeccion_codigo_documento_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbdocumento_inspeccion_codigo_documento_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbdocumento_inspeccion_codigo_documento_seq OWNED BY public.aocr_tbdocumento_inspeccion.codigo_documento;


--
-- Name: aocr_tbdocumento_subsanacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbdocumento_subsanacion (
    codigo_documento integer NOT NULL,
    codigo_subsanacion integer NOT NULL,
    nombre_archivo character varying(255) NOT NULL,
    ruta_archivo character varying(500) NOT NULL,
    tipo_documento character varying(100),
    tamanio_bytes integer,
    fecha_carga timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    codigo_usuario_carga integer NOT NULL
);


--
-- Name: TABLE aocr_tbdocumento_subsanacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.aocr_tbdocumento_subsanacion IS 'Documentos adjuntos cargados por operadores en respuesta a subsanaciones';


--
-- Name: COLUMN aocr_tbdocumento_subsanacion.codigo_subsanacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbdocumento_subsanacion.codigo_subsanacion IS 'SubsanaciÃ³n relacionada';


--
-- Name: COLUMN aocr_tbdocumento_subsanacion.nombre_archivo; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbdocumento_subsanacion.nombre_archivo IS 'Nombre original del archivo';


--
-- Name: COLUMN aocr_tbdocumento_subsanacion.ruta_archivo; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbdocumento_subsanacion.ruta_archivo IS 'Ruta fÃ­sica/URL del documento';


--
-- Name: aocr_tbdocumento_subsanacion_codigo_documento_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbdocumento_subsanacion_codigo_documento_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbdocumento_subsanacion_codigo_documento_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbdocumento_subsanacion_codigo_documento_seq OWNED BY public.aocr_tbdocumento_subsanacion.codigo_documento;


--
-- Name: aocr_tbfirma_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbfirma_documento (
    codigo_firma integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_inspeccion integer,
    tipo_documento character varying(80) NOT NULL,
    numero_aocr character varying(120),
    nombre_archivo character varying(260),
    ruta_documento character varying(500),
    hash_documento character varying(256),
    codigo_qr text,
    sujeto_certificado text,
    nombre_firmante character varying(250),
    cargo_firmante character varying(250),
    fecha_firma timestamp without time zone NOT NULL,
    codigo_usuario integer,
    usuario_nombre character varying(160),
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    tamanio_pdf_firmado bigint,
    firmado_por_rol character varying(80),
    ruta_pdf_preliminar character varying(500),
    ruta_pdf_firmado character varying(500),
    hash_pdf_firmado character varying(256),
    tamanio_pdf_preliminar bigint,
    fecha_generacion timestamp without time zone,
    nombre_director character varying(250),
    cargo_director character varying(250),
    estado_documento character varying(80),
    version integer DEFAULT 1 NOT NULL
);


--
-- Name: aocr_tbfirma_documento_codigo_firma_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbfirma_documento_codigo_firma_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbfirma_documento_codigo_firma_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbfirma_documento_codigo_firma_seq OWNED BY public.aocr_tbfirma_documento.codigo_firma;


--
-- Name: aocr_tbfirma_posicion_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbfirma_posicion_documento (
    codigo_posicion_firma integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_inspeccion integer,
    tipo_documento character varying(80) NOT NULL,
    rol_firmante character varying(80) NOT NULL,
    origen_posicion character varying(30) DEFAULT 'PUNTERO'::character varying NOT NULL,
    numero_pagina integer DEFAULT 1 NOT NULL,
    posicion_x_ratio numeric(10,6) NOT NULL,
    posicion_y_ratio numeric(10,6) NOT NULL,
    ancho_ratio numeric(10,6) NOT NULL,
    alto_ratio numeric(10,6) NOT NULL,
    codigo_usuario integer,
    usuario_nombre character varying(160),
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_tbfirma_posicion_documento_codigo_posicion_firma_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbfirma_posicion_documento_codigo_posicion_firma_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbfirma_posicion_documento_codigo_posicion_firma_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbfirma_posicion_documento_codigo_posicion_firma_seq OWNED BY public.aocr_tbfirma_posicion_documento.codigo_posicion_firma;


--
-- Name: aocr_tbhallazgo; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbhallazgo (
    codigo_hallazgo integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    descripcion text,
    criticidad character varying(30),
    estado character varying(40),
    accion_correctiva text,
    fecha_deteccion timestamp without time zone,
    fecha_cierre timestamp without time zone,
    responsable character varying(200),
    created_at timestamp without time zone,
    created_by character varying(100),
    updated_at timestamp without time zone,
    updated_by character varying(100)
);


--
-- Name: aocr_tbhallazgo_codigo_hallazgo_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbhallazgo_codigo_hallazgo_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbhallazgo_codigo_hallazgo_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbhallazgo_codigo_hallazgo_seq OWNED BY public.aocr_tbhallazgo.codigo_hallazgo;


--
-- Name: aocr_tbhistorial_documental; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbhistorial_documental (
    id integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_documento integer,
    evento character varying(120) NOT NULL,
    detalle text,
    codigo_usuario integer,
    fecha_evento timestamp without time zone DEFAULT now() NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by character varying(150)
);


--
-- Name: aocr_tbhistorial_documental_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbhistorial_documental_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbhistorial_documental_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbhistorial_documental_id_seq OWNED BY public.aocr_tbhistorial_documental.id;


--
-- Name: aocr_tbhistorial_estado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbhistorial_estado (
    codigo_historial integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    estado_anterior character varying(50),
    estado_nuevo character varying(50) NOT NULL,
    codigo_usuario integer,
    observaciones text,
    fecha_cambio timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: aocr_tbhistorial_estado_codigo_historial_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbhistorial_estado_codigo_historial_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbhistorial_estado_codigo_historial_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbhistorial_estado_codigo_historial_seq OWNED BY public.aocr_tbhistorial_estado.codigo_historial;


--
-- Name: aocr_tbhistorial_estado_inspeccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbhistorial_estado_inspeccion (
    codigo_historial integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    estado_anterior character varying(120),
    estado_nuevo character varying(120) NOT NULL,
    observacion text,
    origen character varying(60),
    codigo_usuario integer,
    usuario_nombre character varying(160),
    fecha_cambio timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: aocr_tbhistorial_estado_inspeccion_codigo_historial_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbhistorial_estado_inspeccion_codigo_historial_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbhistorial_estado_inspeccion_codigo_historial_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbhistorial_estado_inspeccion_codigo_historial_seq OWNED BY public.aocr_tbhistorial_estado_inspeccion.codigo_historial;


--
-- Name: aocr_tbinforme; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbinforme (
    codigo_informe integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    resumen_ejecutivo text,
    conclusiones text,
    recomendaciones text,
    hallazgos text,
    acciones_correctivas text,
    resultado character varying(50),
    fecha_emision timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: aocr_tbinforme_codigo_informe_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbinforme_codigo_informe_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbinforme_codigo_informe_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbinforme_codigo_informe_seq OWNED BY public.aocr_tbinforme.codigo_informe;


--
-- Name: aocr_tbinforme_inspeccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbinforme_inspeccion (
    codigo_informe integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    version integer NOT NULL,
    titulo character varying(250),
    resumen text,
    resultado character varying(100),
    observaciones text,
    conclusiones text,
    recomendaciones text,
    ruta_pdf character varying(500),
    finalizado boolean DEFAULT false NOT NULL,
    correo_enviado boolean DEFAULT false NOT NULL,
    fecha_finalizacion timestamp without time zone,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by integer,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_by integer,
    estado_informe character varying(80),
    firmado_inspector boolean DEFAULT false NOT NULL,
    firmado_dirdac boolean DEFAULT false NOT NULL,
    ruta_documento_firmado character varying(500),
    hash_documento character varying(256),
    fecha_firma_1 timestamp without time zone,
    fecha_firma_2 timestamp without time zone,
    usuario_firma_1 character varying(160),
    usuario_firma_2 character varying(160),
    fecha_envio_dirdac timestamp without time zone,
    usuario_envio_dirdac character varying(160),
    antecedentes text,
    alcance text,
    desarrollo text,
    evidencias text,
    numero_licencia_inspector character varying(120),
    trabajos_realizados text,
    operacion_comercial text,
    servicios_estaciones text,
    notas text,
    no_conformidades text,
    documentos_adjuntos text,
    otros_adjuntos text,
    aprobado_dirdac boolean,
    fecha_aprobacion_dirdac timestamp without time zone,
    usuario_aprobacion_dirdac character varying(160),
    motivo_rechazo text,
    observacion_devolucion text,
    fecha_devolucion timestamp without time zone,
    usuario_devolucion character varying(160),
    fechas_inspeccion_manual text,
    estaciones_inspeccion_manual text,
    documentos_adjuntos_archivos text,
    tipo_resultado_insatisfactorio character varying(30),
    notificado_rt boolean DEFAULT false NOT NULL,
    fecha_notificacion_rt timestamp without time zone,
    codigo_informe_anterior integer,
    base_legal text,
    estacion_id integer
);


--
-- Name: aocr_tbinforme_inspeccion_codigo_informe_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbinforme_inspeccion_codigo_informe_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbinforme_inspeccion_codigo_informe_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbinforme_inspeccion_codigo_informe_seq OWNED BY public.aocr_tbinforme_inspeccion.codigo_informe;


--
-- Name: aocr_tbinspeccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbinspeccion (
    codigo_inspeccion integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    numero_inspeccion character varying(50) NOT NULL,
    tipo integer NOT NULL,
    fecha_programada date NOT NULL,
    hora_programada time without time zone,
    fecha_realizada date,
    hora_inicio time without time zone,
    hora_fin time without time zone,
    codigo_inspector integer,
    lugar character varying(300),
    resultado character varying(50),
    comentarios text,
    observaciones_generales text,
    hallazgos_principales text,
    recomendaciones text,
    estado character varying(50) DEFAULT 'Programada'::character varying,
    completada boolean DEFAULT false,
    aprobada boolean DEFAULT false,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    created_by character varying(100),
    updated_by character varying(100),
    informe_pdf text,
    historial_estados text,
    estado_documental character varying(50),
    resultado_evaluacion character varying(50),
    inspector_principal_cedula character varying(20),
    inspector_principal_nombre character varying(200),
    inspector_principal_tipo character varying(10),
    inspector_apoyo_cedula character varying(20),
    inspector_apoyo_nombre character varying(200),
    inspector_apoyo_tipo character varying(10),
    CONSTRAINT chk_estado_inspeccion CHECK (((estado)::text = ANY (ARRAY[('CREADA'::character varying)::text, ('PROGRAMADA'::character varying)::text, ('EN_CURSO'::character varying)::text, ('APLAZADA'::character varying)::text, ('FINALIZADA'::character varying)::text, ('APROBADA'::character varying)::text, ('RECHAZADA'::character varying)::text, ('CANCELADA'::character varying)::text, ('CERRADA'::character varying)::text, ('SOLICITUD_INSPECCION_CREADA'::character varying)::text, ('VERIFICACION_SOLICITUD'::character varying)::text, ('ACEPTADA'::character varying)::text, ('OBSERVADA'::character varying)::text, ('SUBSANADA'::character varying)::text, ('VIATICOS_REQUERIDOS'::character varying)::text, ('PAGO_VALIDADO'::character varying)::text, ('EN_INSPECCION'::character varying)::text, ('INFORME_ELABORADO'::character varying)::text, ('RESULTADO_SATISFACTORIO'::character varying)::text, ('RESULTADO_NO_SATISFACTORIO'::character varying)::text, ('OBSERVACION_DOCUMENTAL'::character varying)::text])))
);


--
-- Name: TABLE aocr_tbinspeccion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.aocr_tbinspeccion IS 'Tabla de inspecciones tecnicas con estados formalizados (CREADA, PROGRAMADA, EN_CURSO, APLAZADA, FINALIZADA, APROBADA, RECHAZADA, CANCELADA, CERRADA)';


--
-- Name: COLUMN aocr_tbinspeccion.estado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbinspeccion.estado IS 'Estado actual de la inspeccion: CREADA | PROGRAMADA | EN_CURSO | APLAZADA | FINALIZADA | APROBADA | RECHAZADA | CANCELADA | CERRADA';


--
-- Name: COLUMN aocr_tbinspeccion.historial_estados; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbinspeccion.historial_estados IS 'JSON con historial de cambios de estado: [{fecha, usuario, estado_anterior, estado_nuevo, motivo}]';


--
-- Name: aocr_tbinspeccion_codigo_inspeccion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbinspeccion_codigo_inspeccion_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbinspeccion_codigo_inspeccion_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbinspeccion_codigo_inspeccion_seq OWNED BY public.aocr_tbinspeccion.codigo_inspeccion;


--
-- Name: aocr_tbinspectores; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbinspectores (
    id integer NOT NULL,
    cedula character varying(20) NOT NULL,
    nombre_completo character varying(200),
    estado character varying(5),
    tipo character varying(10),
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE aocr_tbinspectores; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.aocr_tbinspectores IS 'Espejo local de DGACDAT.OPIAR2 (AS400). Se sincroniza vía SyncAdmin/RunInspectoresRt.';


--
-- Name: aocr_tbinspectores_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbinspectores_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbinspectores_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbinspectores_id_seq OWNED BY public.aocr_tbinspectores.id;


--
-- Name: aocr_tblog; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tblog (
    codigo_log integer NOT NULL,
    nivel character varying(20) NOT NULL,
    categoria character varying(100),
    mensaje text NOT NULL,
    detalle text,
    codigo_usuario integer,
    ip_address character varying(45),
    url character varying(500),
    fecha_registro timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: aocr_tblog_codigo_log_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tblog_codigo_log_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tblog_codigo_log_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tblog_codigo_log_seq OWNED BY public.aocr_tblog.codigo_log;


--
-- Name: aocr_tblv_operacional_eae; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tblv_operacional_eae (
    codigo_lv integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    estado_lista character varying(50) DEFAULT 'BORRADOR'::character varying NOT NULL,
    resumen_verificacion text,
    observaciones_generales text,
    resultado_general character varying(120),
    items_json text,
    ruta_pdf text,
    ruta_documento_firmado text,
    hash_documento character varying(256),
    finalizado boolean DEFAULT false NOT NULL,
    firmado_tecnico boolean DEFAULT false NOT NULL,
    fecha_finalizacion timestamp without time zone,
    fecha_firma timestamp without time zone,
    usuario_firma character varying(250),
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by integer,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_by integer,
    nombre_eae text,
    numero_aoc_fecha_validez text,
    direccion_estado_explotador text,
    direccion_estado_reconocimiento text,
    tipos_aeronaves text,
    tipo_operacion text,
    fecha_lista timestamp without time zone,
    inspector_responsable text,
    cargo_inspector text,
    solicitud_id integer,
    estacion_id integer,
    tipo_lista character varying(50) DEFAULT 'EAE'::character varying NOT NULL,
    vigente boolean DEFAULT true NOT NULL
);


--
-- Name: aocr_tblv_operacional_eae_codigo_lv_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tblv_operacional_eae_codigo_lv_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tblv_operacional_eae_codigo_lv_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tblv_operacional_eae_codigo_lv_seq OWNED BY public.aocr_tblv_operacional_eae.codigo_lv;


--
-- Name: aocr_tbnoconformidad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbnoconformidad (
    codigo_no_conformidad integer NOT NULL,
    codigo_inspeccion integer NOT NULL,
    codigo_informe integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    tipo_ruta character varying(50) NOT NULL,
    estado character varying(50) NOT NULL,
    numero_no_conformidad character varying(100),
    resumen text,
    detalle text,
    fundamento_tecnico text,
    acciones_requeridas text,
    plazo_subsanacion integer,
    requiere_nueva_inspeccion boolean DEFAULT false,
    version integer DEFAULT 1,
    ruta_pdf character varying(500),
    ruta_pdf_firmado_inspector character varying(500),
    ruta_pdf_firmado_coordinador character varying(500),
    hash_documento character varying(256),
    fecha_generacion timestamp without time zone,
    fecha_firma_inspector timestamp without time zone,
    fecha_envio_coordinador timestamp without time zone,
    fecha_devolucion timestamp without time zone,
    fecha_firma_coordinador timestamp without time zone,
    fecha_notificacion_rt timestamp without time zone,
    usuario_creacion integer,
    usuario_firma_inspector integer,
    usuario_firma_coordinador integer,
    observacion_devolucion text,
    created_at timestamp without time zone DEFAULT now(),
    updated_at timestamp without time zone,
    ruta_pdf_subsanacion_rt character varying(500),
    fecha_subsanacion_rt timestamp without time zone,
    codigo_nc_raiz integer NOT NULL,
    codigo_solicitud_origen integer,
    codigo_inspeccion_origen integer,
    codigo_informe_origen integer,
    codigo_solicitud_nueva integer,
    codigo_inspeccion_nueva integer,
    codigo_informe_cierre integer,
    ciclo_evaluacion integer DEFAULT 1 NOT NULL,
    fecha_cierre timestamp without time zone,
    usuario_cierre integer,
    observacion_cierre text,
    correlation_id character varying(100),
    CONSTRAINT ck_aocr_nc_tipo_ruta CHECK ((upper((tipo_ruta)::text) = ANY (ARRAY['CON_INSPECCION'::text, 'SIN_INSPECCION'::text]))),
    CONSTRAINT ck_aocr_nc_version_positiva CHECK (((version > 0) AND (ciclo_evaluacion > 0)))
);


--
-- Name: aocr_tbnoconformidad_codigo_no_conformidad_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbnoconformidad_codigo_no_conformidad_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbnoconformidad_codigo_no_conformidad_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbnoconformidad_codigo_no_conformidad_seq OWNED BY public.aocr_tbnoconformidad.codigo_no_conformidad;


--
-- Name: aocr_tbnotificacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbnotificacion (
    codigonotificacion integer NOT NULL,
    codigousuario integer NOT NULL,
    titulo character varying(200) NOT NULL,
    mensaje text NOT NULL,
    tipo character varying(50),
    leida boolean DEFAULT false,
    fecha_lectura timestamp without time zone,
    url character varying(500),
    icono character varying(50),
    fechacreacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    fechalectura timestamp without time zone,
    modulo character varying(100),
    entidad_id integer,
    tipo_entidad character varying(100),
    event_key character varying(300),
    correlation_id character varying(80),
    updated_at timestamp without time zone
);


--
-- Name: aocr_tbnotificacion_codigo_notificacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbnotificacion_codigo_notificacion_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbnotificacion_codigo_notificacion_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbnotificacion_codigo_notificacion_seq OWNED BY public.aocr_tbnotificacion.codigonotificacion;


--
-- Name: aocr_tbobservacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbobservacion (
    codigo_observacion integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_usuario integer NOT NULL,
    mensaje text NOT NULL,
    fecha_registro timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    codigo_usuario_respuesta integer
);


--
-- Name: aocr_tbobservacion_codigo_observacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbobservacion_codigo_observacion_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbobservacion_codigo_observacion_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbobservacion_codigo_observacion_seq OWNED BY public.aocr_tbobservacion.codigo_observacion;


--
-- Name: aocr_tbpago; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbpago (
    codigo_pago integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    numero_factura character varying(50),
    monto numeric(10,2) NOT NULL,
    moneda character varying(3) DEFAULT 'USD'::character varying,
    concepto text,
    metodo_pago character varying(50),
    estado character varying(50) DEFAULT 'Pendiente'::character varying,
    fecha_pago timestamp without time zone,
    fecha_validacion timestamp without time zone,
    validado_por character varying(100),
    observaciones text,
    comprobante_ruta character varying(255),
    banco character varying(100),
    CONSTRAINT chk_estado_pago CHECK (((estado)::text = ANY (ARRAY[('PENDIENTE'::character varying)::text, ('PENDIENTE_REVISION'::character varying)::text, ('VALIDADO'::character varying)::text, ('APROBADO'::character varying)::text, ('APROBADO_FINANCIERO'::character varying)::text, ('RECHAZADO'::character varying)::text, ('RECHAZADO_FINANCIERO'::character varying)::text, ('ANULADO'::character varying)::text, ('CONFIRMADO'::character varying)::text, ('PAGADO'::character varying)::text, ('COMPLETADO'::character varying)::text, ('CANCELADO'::character varying)::text, ('EN_REVISION'::character varying)::text, ('PROCESANDO'::character varying)::text, ('Pendiente'::character varying)::text, ('Validado'::character varying)::text, ('Aprobado'::character varying)::text, ('Rechazado'::character varying)::text]))),
    CONSTRAINT chk_monto_positivo CHECK ((monto > (0)::numeric))
);


--
-- Name: aocr_tbpago_codigo_pago_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbpago_codigo_pago_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbpago_codigo_pago_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbpago_codigo_pago_seq OWNED BY public.aocr_tbpago.codigo_pago;


--
-- Name: aocr_tbparametro; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbparametro (
    codigo_parametro integer NOT NULL,
    clave character varying(100) NOT NULL,
    valor text,
    descripcion text,
    tipo character varying(50),
    tipo_dato character varying(20),
    categoria character varying(100),
    editable boolean DEFAULT true,
    modificable boolean DEFAULT true,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_by character varying(100),
    codigoparametro character varying(100),
    valorparametro numeric(10,2),
    descripcionparametro character varying(255),
    activo boolean DEFAULT true,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updatedat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    createdby character varying(100),
    updatedby character varying(100),
    deletedat timestamp without time zone,
    deletedby character varying(100)
);


--
-- Name: aocr_tbparametro_codigo_parametro_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbparametro_codigo_parametro_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbparametro_codigo_parametro_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbparametro_codigo_parametro_seq OWNED BY public.aocr_tbparametro.codigo_parametro;


--
-- Name: aocr_tbrevision_documental; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbrevision_documental (
    id integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_documento integer NOT NULL,
    decision character varying(60) NOT NULL,
    observacion text,
    codigo_usuario_revisor integer,
    fecha_revision timestamp without time zone DEFAULT now() NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by character varying(150)
);


--
-- Name: aocr_tbrevision_documental_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbrevision_documental_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbrevision_documental_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbrevision_documental_id_seq OWNED BY public.aocr_tbrevision_documental.id;


--
-- Name: aocr_tbsesiones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbsesiones (
    id integer NOT NULL,
    codigousuario integer NOT NULL,
    token_sesion character varying(500) NOT NULL,
    ip_address character varying(50),
    user_agent text,
    fecha_inicio timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    fecha_expiracion timestamp without time zone NOT NULL,
    activa boolean DEFAULT true,
    fecha_cierre timestamp without time zone
);


--
-- Name: aocr_tbsesiones_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbsesiones_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbsesiones_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbsesiones_id_seq OWNED BY public.aocr_tbsesiones.id;


--
-- Name: aocr_tbsolicitud; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbsolicitud (
    codigo_solicitud integer NOT NULL,
    numero_solicitud character varying(50) NOT NULL,
    fecha_solicitud timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    tipo_solicitud integer NOT NULL,
    estado character varying(50) DEFAULT 'Pendiente'::character varying,
    nombre_operador character varying(200) NOT NULL,
    ruc character varying(20),
    razon_social character varying(250),
    email character varying(200),
    telefono character varying(50),
    direccion text,
    ciudad character varying(100),
    provincia character varying(100),
    pais character varying(100) DEFAULT 'Ecuador'::character varying,
    representante_legal character varying(150),
    cedula_representante character varying(20),
    tipo_operacion character varying(100) NOT NULL,
    descripcion_operacion text,
    observaciones text,
    codigo_usuario integer,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    updated_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    created_by character varying(100),
    updated_by character varying(100),
    deleted_at timestamp without time zone,
    deleted_by character varying(100),
    codigo_tecnico integer,
    fecha_recepcion timestamp without time zone,
    fecha_solicitud_subsanacion timestamp without time zone,
    fecha_subsanacion timestamp without time zone,
    fecha_aprobacion_coordinador timestamp without time zone,
    fecha_emision_aocr timestamp without time zone,
    fecha_entrega_aocr timestamp without time zone,
    numero_aocr character varying(50),
    ruta_archivo_pdf_aocr character varying(500),
    codigo_usuario_aprobacion_coordinador integer,
    codigo_usuario_aprobacion_director integer,
    pago_aprobado boolean DEFAULT false,
    fecha_aprobacion_pago timestamp without time zone,
    modulo_solicitud_rt_habilitado boolean DEFAULT false,
    notificado_coordinador_pago_aprobado boolean DEFAULT false,
    fecha_notificacion_coordinador_pago timestamp without time zone,
    notificado_rt_modulo_habilitado boolean DEFAULT false,
    fecha_notificacion_rt_modulo_habilitado timestamp without time zone,
    pendiente_asignacion_inspector boolean DEFAULT false,
    pendiente_carga_documental_rt boolean DEFAULT true,
    solicitud_finalizada_rt boolean DEFAULT false,
    fecha_finalizacion_rt timestamp without time zone,
    requiere_nueva_orden boolean DEFAULT false,
    resumen_operaciones_eae text,
    numero_aoc character varying(100),
    aprobaciones_especiales text,
    aprobaciones_especiales_otros text,
    aeropuertos_ecuador text,
    aeropuertos_ecuador_otros text,
    companias_seleccionadas text,
    codigo_oaci character varying(10),
    correo_representante_tecnico character varying(200),
    nombre_comercial character varying(200),
    tecnico_responsable_cedula character varying(20),
    tecnico_responsable_nombre character varying(200),
    tecnico_responsable_tipo character varying(10),
    inspector_apoyo_cedula character varying(20),
    inspector_apoyo_nombre character varying(200),
    inspector_apoyo_tipo character varying(10),
    version integer DEFAULT 1 NOT NULL,
    estado_documental character varying(50),
    datos_documentales_certificado jsonb,
    CONSTRAINT chk_tipo_solicitud CHECK ((tipo_solicitud = ANY (ARRAY[0, 1, 2])))
);


--
-- Name: COLUMN aocr_tbsolicitud.fecha_recepcion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.fecha_recepcion IS 'Fecha formal de recepciÃ³n de la solicitud (estado RECEPCIONADO)';


--
-- Name: COLUMN aocr_tbsolicitud.fecha_solicitud_subsanacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.fecha_solicitud_subsanacion IS 'Fecha cuando se solicitÃ³ subsanaciÃ³n de documentos (estado SUBSANACION)';


--
-- Name: COLUMN aocr_tbsolicitud.fecha_subsanacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.fecha_subsanacion IS 'Fecha cuando el operador completÃ³ subsanaciÃ³n (estado SUBSANADO)';


--
-- Name: COLUMN aocr_tbsolicitud.fecha_aprobacion_coordinador; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.fecha_aprobacion_coordinador IS 'Fecha de aprobaciÃ³n por Coordinador (estado EN_APROBACION_COORDINADOR)';


--
-- Name: COLUMN aocr_tbsolicitud.fecha_emision_aocr; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.fecha_emision_aocr IS 'Fecha de emisiÃ³n del certificado AOCR (estado AOCR_EMITIDO)';


--
-- Name: COLUMN aocr_tbsolicitud.fecha_entrega_aocr; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.fecha_entrega_aocr IS 'Fecha de entrega fÃ­sica del certificado (estado AOCR_ENTREGADO)';


--
-- Name: COLUMN aocr_tbsolicitud.numero_aocr; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.numero_aocr IS 'NÃºmero Ãºnico del certificado AOCR (ej: AOCR-2024-001)';


--
-- Name: COLUMN aocr_tbsolicitud.ruta_archivo_pdf_aocr; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud.ruta_archivo_pdf_aocr IS 'Ruta del archivo PDF del certificado generado';


--
-- Name: aocr_tbsolicitud_codigo_solicitud_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbsolicitud_codigo_solicitud_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbsolicitud_codigo_solicitud_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbsolicitud_codigo_solicitud_seq OWNED BY public.aocr_tbsolicitud.codigo_solicitud;


--
-- Name: aocr_tbsolicitud_estacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbsolicitud_estacion (
    id integer NOT NULL,
    solicitud_id integer NOT NULL,
    estacion_codigo character varying(20) NOT NULL,
    estacion_nombre character varying(150) NOT NULL,
    fecha_inicio date,
    fecha_fin date,
    inspector_id integer,
    inspector_nombre character varying(200),
    inspeccion_id integer,
    estado character varying(50) DEFAULT 'SOLICITADA'::character varying NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    observacion text,
    creado_en timestamp without time zone DEFAULT now() NOT NULL,
    creado_por integer,
    actualizado_en timestamp without time zone,
    actualizado_por integer,
    CONSTRAINT chk_fechas_estacion CHECK ((fecha_fin >= fecha_inicio))
);


--
-- Name: TABLE aocr_tbsolicitud_estacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.aocr_tbsolicitud_estacion IS 'Estaciones operativas solicitadas y rangos de fechas de inspección independientes (AC-02)';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.solicitud_id; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.solicitud_id IS 'Identificador de la solicitud AOCR (aocr_tbsolicitud.codigo_solicitud)';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.estacion_codigo; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estacion_codigo IS 'Código OACI/IATA o identificador de la estación (ej. UIO, GYE, MEC, LTX)';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.estacion_nombre; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estacion_nombre IS 'Nombre descriptivo de la estación/aeropuerto';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.fecha_inicio; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.fecha_inicio IS 'Fecha inicial programada o solicitada para la inspección de la estación';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.fecha_fin; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.fecha_fin IS 'Fecha final programada o solicitada para la inspección de la estación';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.inspector_id; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.inspector_id IS 'ID del inspector asignado específicamente a la estación (si aplica)';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.inspeccion_id; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.inspeccion_id IS 'ID de la inspección vinculada en aocr_tbinspeccion (si aplica)';


--
-- Name: COLUMN aocr_tbsolicitud_estacion.estado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsolicitud_estacion.estado IS 'Estado de la estación en el flujo (SOLICITADA, PLANIFICADA, INSPECCIONADA, etc.)';


--
-- Name: aocr_tbsolicitud_estacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbsolicitud_estacion_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbsolicitud_estacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbsolicitud_estacion_id_seq OWNED BY public.aocr_tbsolicitud_estacion.id;


--
-- Name: aocr_tbsubsanacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbsubsanacion (
    codigo_subsanacion integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    fecha_solicitud timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    observaciones text NOT NULL,
    codigo_usuario_solicitante integer NOT NULL,
    fecha_respuesta timestamp without time zone,
    respuesta text,
    codigo_usuario_respuesta integer,
    estado character varying(20) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    created_at timestamp without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at timestamp without time zone,
    created_by character varying(100),
    updated_by character varying(100)
);


--
-- Name: TABLE aocr_tbsubsanacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.aocr_tbsubsanacion IS 'Registro de solicitudes de subsanaciÃ³n de documentos en solicitudes AOCR';


--
-- Name: COLUMN aocr_tbsubsanacion.codigo_subsanacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.codigo_subsanacion IS 'CÃ³digo Ãºnico de subsanaciÃ³n';


--
-- Name: COLUMN aocr_tbsubsanacion.codigo_solicitud; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.codigo_solicitud IS 'Solicitud relacionada';


--
-- Name: COLUMN aocr_tbsubsanacion.fecha_solicitud; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.fecha_solicitud IS 'Fecha cuando se solicitÃ³ la subsanaciÃ³n';


--
-- Name: COLUMN aocr_tbsubsanacion.observaciones; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.observaciones IS 'DescripciÃ³n de documentos/requisitos a subsanar';


--
-- Name: COLUMN aocr_tbsubsanacion.codigo_usuario_solicitante; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.codigo_usuario_solicitante IS 'Usuario tÃ©cnico que solicita la subsanaciÃ³n';


--
-- Name: COLUMN aocr_tbsubsanacion.fecha_respuesta; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.fecha_respuesta IS 'Fecha cuando el operador respondiÃ³/completÃ³';


--
-- Name: COLUMN aocr_tbsubsanacion.respuesta; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.respuesta IS 'Comentarios del operador al completar subsanaciÃ³n';


--
-- Name: COLUMN aocr_tbsubsanacion.codigo_usuario_respuesta; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.codigo_usuario_respuesta IS 'Usuario operador que completÃ³ subsanaciÃ³n';


--
-- Name: COLUMN aocr_tbsubsanacion.estado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.aocr_tbsubsanacion.estado IS 'Estado: PENDIENTE, COMPLETADA, VENCIDA';


--
-- Name: aocr_tbsubsanacion_codigo_subsanacion_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbsubsanacion_codigo_subsanacion_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbsubsanacion_codigo_subsanacion_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbsubsanacion_codigo_subsanacion_seq OWNED BY public.aocr_tbsubsanacion.codigo_subsanacion;


--
-- Name: aocr_tbviatico; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_tbviatico (
    codigo_viatico integer NOT NULL,
    codigo_solicitud integer NOT NULL,
    codigo_inspector integer NOT NULL,
    monto_viaticos numeric(10,2) NOT NULL,
    monto_movilizacion numeric(10,2),
    dias_estimados integer,
    justificacion text,
    estado character varying(50) DEFAULT 'Pendiente'::character varying,
    fecha_asignacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: aocr_tbviatico_codigo_viatico_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_tbviatico_codigo_viatico_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_tbviatico_codigo_viatico_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_tbviatico_codigo_viatico_seq OWNED BY public.aocr_tbviatico.codigo_viatico;


--
-- Name: aocr_usuario_compania_rt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_usuario_compania_rt (
    id integer NOT NULL,
    usuario_id integer NOT NULL,
    compania_codigo character varying(20) NOT NULL,
    compania_nombre character varying(250),
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by character varying(120) DEFAULT 'sistema'::character varying NOT NULL,
    updated_at timestamp without time zone,
    updated_by character varying(120),
    usuoid character varying(30)
);


--
-- Name: aocr_usuario_compania_rt_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_usuario_compania_rt_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_usuario_compania_rt_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_usuario_compania_rt_id_seq OWNED BY public.aocr_usuario_compania_rt.id;


--
-- Name: aocr_usuario_interno_rt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_usuario_interno_rt (
    id integer NOT NULL,
    usuario_id integer,
    codigo_usuario character varying(64) NOT NULL,
    ciudad_codigo character varying(10),
    codigo_financiero numeric(18,0),
    opcar5 character varying(10),
    opcaer character varying(10),
    opcoi3 numeric(18,0),
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    created_by character varying(120) DEFAULT 'sistema'::character varying NOT NULL,
    updated_at timestamp without time zone,
    updated_by character varying(120),
    nombre_completo character varying(200),
    tipo character varying(10),
    estado_as400 character varying(10),
    tecnico_id integer,
    identificacion character varying(32),
    nombres character varying(120),
    apellidos character varying(120),
    correo_institucional character varying(200),
    rol_interno character varying(100),
    observaciones text
);


--
-- Name: aocr_usuario_interno_rt_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_usuario_interno_rt_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_usuario_interno_rt_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_usuario_interno_rt_id_seq OWNED BY public.aocr_usuario_interno_rt.id;


--
-- Name: aocr_usuario_transferencia; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_usuario_transferencia (
    id_transferencia bigint NOT NULL,
    usuario_origen_id integer NOT NULL,
    usuario_destino_id integer NOT NULL,
    ejecutado_por_usuario_id integer,
    ejecutado_por_codigo character varying(100),
    motivo character varying(500),
    ip character varying(64),
    fecha timestamp without time zone DEFAULT now() NOT NULL,
    total_registros_detectados integer DEFAULT 0 NOT NULL,
    total_registros_transferidos integer DEFAULT 0 NOT NULL,
    resumen_json jsonb
);


--
-- Name: aocr_usuario_transferencia_detalle; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.aocr_usuario_transferencia_detalle (
    id_detalle bigint NOT NULL,
    transferencia_id bigint NOT NULL,
    grupo character varying(30) NOT NULL,
    tabla character varying(128) NOT NULL,
    campo character varying(128) NOT NULL,
    descripcion character varying(300),
    estrategia character varying(300),
    transferible boolean DEFAULT false NOT NULL,
    registros_detectados integer DEFAULT 0 NOT NULL,
    registros_afectados integer DEFAULT 0 NOT NULL,
    observacion character varying(500)
);


--
-- Name: aocr_usuario_transferencia_detalle_id_detalle_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_usuario_transferencia_detalle_id_detalle_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_usuario_transferencia_detalle_id_detalle_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_usuario_transferencia_detalle_id_detalle_seq OWNED BY public.aocr_usuario_transferencia_detalle.id_detalle;


--
-- Name: aocr_usuario_transferencia_id_transferencia_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.aocr_usuario_transferencia_id_transferencia_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: aocr_usuario_transferencia_id_transferencia_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.aocr_usuario_transferencia_id_transferencia_seq OWNED BY public.aocr_usuario_transferencia.id_transferencia;


--
-- Name: auditoria_historialestado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auditoria_historialestado (
    id bigint NOT NULL,
    modelo character varying(100) NOT NULL,
    objeto_id bigint NOT NULL,
    estado_anterior character varying(100) NOT NULL,
    estado_nuevo character varying(100) NOT NULL,
    observacion text NOT NULL,
    fecha timestamp with time zone NOT NULL,
    usuario_id bigint,
    CONSTRAINT auditoria_historialestado_objeto_id_check CHECK ((objeto_id >= 0))
);


--
-- Name: auditoria_historialestado_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auditoria_historialestado ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.auditoria_historialestado_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auditoria_seguridad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auditoria_seguridad (
    id_auditoria bigint NOT NULL,
    actor_usuario_id integer,
    actor_codigo_usuario character varying(64),
    accion character varying(60) NOT NULL,
    objetivo_tipo character varying(40) NOT NULL,
    objetivo_id character varying(80),
    detalle_json jsonb,
    fecha timestamp without time zone DEFAULT now() NOT NULL,
    ip character varying(64)
);


--
-- Name: auditoria_seguridad_id_auditoria_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.auditoria_seguridad_id_auditoria_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: auditoria_seguridad_id_auditoria_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.auditoria_seguridad_id_auditoria_seq OWNED BY public.auditoria_seguridad.id_auditoria;


--
-- Name: auditorias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auditorias (
    id bigint NOT NULL,
    usuario_id bigint,
    user_name character varying(255),
    role_name character varying(255),
    modulo character varying(50) NOT NULL,
    tabla character varying(60) NOT NULL,
    registro_id bigint,
    accion character varying(30) NOT NULL,
    valores_anteriores jsonb,
    valores_nuevos jsonb,
    direccion_ip character varying(45),
    user_agent text,
    resultado character varying(20) DEFAULT 'EXITOSO'::character varying NOT NULL,
    motivo text,
    created_at timestamp(0) without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: auditorias_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.auditorias_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: auditorias_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.auditorias_id_seq OWNED BY public.auditorias.id;


--
-- Name: auth_group; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auth_group (
    id integer NOT NULL,
    name character varying(150) NOT NULL
);


--
-- Name: auth_group_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auth_group ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.auth_group_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auth_group_permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auth_group_permissions (
    id bigint NOT NULL,
    group_id integer NOT NULL,
    permission_id integer NOT NULL
);


--
-- Name: auth_group_permissions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auth_group_permissions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.auth_group_permissions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: auth_permission; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.auth_permission (
    id integer NOT NULL,
    name character varying(255) NOT NULL,
    content_type_id integer NOT NULL,
    codename character varying(100) NOT NULL
);


--
-- Name: auth_permission_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.auth_permission ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.auth_permission_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cache; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cache (
    key character varying(255) NOT NULL,
    value text NOT NULL,
    expiration integer NOT NULL
);


--
-- Name: cache_locks; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cache_locks (
    key character varying(255) NOT NULL,
    owner character varying(255) NOT NULL,
    expiration integer NOT NULL
);


--
-- Name: capacitaciones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.capacitaciones (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    curso_id bigint,
    nombre_capacitacion character varying(200) NOT NULL,
    institucion_capacitadora_id bigint,
    fecha_inicio date,
    fecha_fin date,
    duracion_horas integer DEFAULT 0 NOT NULL,
    calificacion numeric(5,2),
    estado_capacitacion_id bigint,
    comentarios_observacion text,
    user_id_validador bigint,
    fecha_validacion timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: capacitaciones_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.capacitaciones_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: capacitaciones_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.capacitaciones_id_seq OWNED BY public.capacitaciones.id;


--
-- Name: certificados_certificadoaocr; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.certificados_certificadoaocr (
    id bigint NOT NULL,
    estado character varying(40) NOT NULL,
    observacion text NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_actualizacion timestamp with time zone NOT NULL,
    documento_aocr_id bigint,
    documento_condiciones_id bigint,
    solicitud_id bigint NOT NULL,
    coordinador_revisor_id bigint,
    dcav_firmante_id bigint,
    dirdac_firmante_id bigint,
    fecha_envio_dcav timestamp with time zone,
    fecha_envio_dirdac timestamp with time zone,
    fecha_firma_dcav timestamp with time zone,
    fecha_firma_dirdac timestamp with time zone,
    inspeccion_id bigint,
    tipo character varying(30) NOT NULL
);


--
-- Name: certificados_certificadoaocr_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.certificados_certificadoaocr ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.certificados_certificadoaocr_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: competencias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.competencias (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    tipo_competencia character varying(100) NOT NULL,
    descripcion text NOT NULL,
    nivel_conocimiento character varying(50),
    certificado boolean DEFAULT false NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: competencias_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.competencias_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: competencias_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.competencias_id_seq OWNED BY public.competencias.id;


--
-- Name: conceptos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.conceptos (
    id integer NOT NULL,
    codigo character varying(50) NOT NULL,
    nombre character varying(200) NOT NULL,
    descripcion text,
    precio_base numeric(18,2) DEFAULT 0,
    aplica_iva boolean DEFAULT true,
    activo boolean DEFAULT true,
    fecha_creacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: TABLE conceptos; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.conceptos IS 'CatÃ¡logo de conceptos de cobro';


--
-- Name: conceptos_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.conceptos_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: conceptos_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.conceptos_id_seq OWNED BY public.conceptos.id;


--
-- Name: contactos_institucionales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.contactos_institucionales (
    id bigint NOT NULL,
    institucion_id bigint NOT NULL,
    tipo character varying(50) NOT NULL,
    valor character varying(255) NOT NULL,
    principal boolean DEFAULT false NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: COLUMN contactos_institucionales.tipo; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.contactos_institucionales.tipo IS 'TELEFONO, CORREO, DIRECCION';


--
-- Name: contactos_institucionales_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.contactos_institucionales_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: contactos_institucionales_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.contactos_institucionales_id_seq OWNED BY public.contactos_institucionales.id;


--
-- Name: contactos_servidor; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.contactos_servidor (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    tipo character varying(50) NOT NULL,
    valor character varying(150) NOT NULL,
    emergencia boolean DEFAULT false NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: contactos_servidor_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.contactos_servidor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: contactos_servidor_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.contactos_servidor_id_seq OWNED BY public.contactos_servidor.id;


--
-- Name: contribuyentes; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.contribuyentes (
    id integer NOT NULL,
    ruc_cedula character varying(20) NOT NULL,
    nombre character varying(200) NOT NULL,
    razon_social character varying(200),
    direccion text,
    telefono character varying(50),
    correo character varying(100),
    tipo character varying(50),
    activo boolean DEFAULT true,
    fecha_creacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: TABLE contribuyentes; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.contribuyentes IS 'Registro de contribuyentes/clientes';


--
-- Name: contribuyentes_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.contribuyentes_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: contribuyentes_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.contribuyentes_id_seq OWNED BY public.contribuyentes.id;


--
-- Name: cursos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cursos (
    id bigint NOT NULL,
    codigo_legado character varying(30) NOT NULL,
    nombre character varying(200) NOT NULL,
    tipo_curso_id bigint,
    programa_capacitacion_id bigint,
    nivel character varying(50),
    duracion_horas integer DEFAULT 0 NOT NULL,
    mandatorio boolean DEFAULT false NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    tabla_origen character varying(50) DEFAULT 'CURARC'::character varying NOT NULL,
    fecha_importacion timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN cursos.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.cursos.codigo_legado IS 'CURCOD de CURARC';


--
-- Name: cursos_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.cursos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: cursos_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.cursos_id_seq OWNED BY public.cursos.id;


--
-- Name: cursos_mandatorios; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cursos_mandatorios (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    curso_id bigint NOT NULL,
    estado_cumplimiento character varying(30) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    fecha_proximo_vencimiento date,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: cursos_mandatorios_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.cursos_mandatorios_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: cursos_mandatorios_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.cursos_mandatorios_id_seq OWNED BY public.cursos_mandatorios.id;


--
-- Name: datos_laborales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.datos_laborales (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    gestion_id bigint,
    unidad_id bigint,
    puesto_id bigint,
    grupo_ocupacional_id bigint,
    modalidad_id bigint,
    lugar_trabajo_id bigint,
    fecha_inicio date NOT NULL,
    fecha_fin date,
    vigente boolean DEFAULT true NOT NULL,
    rmu numeric(10,2),
    user_id_registro bigint,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: datos_laborales_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.datos_laborales_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: datos_laborales_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.datos_laborales_id_seq OWNED BY public.datos_laborales.id;


--
-- Name: detalles_orden; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.detalles_orden (
    id integer NOT NULL,
    orden_id integer NOT NULL,
    concepto_id integer,
    descripcion text,
    cantidad integer DEFAULT 1 NOT NULL,
    precio_unitario numeric(18,2) DEFAULT 0 NOT NULL,
    subtotal numeric(18,2) DEFAULT 0 NOT NULL,
    fecha_creacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: TABLE detalles_orden; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.detalles_orden IS 'Detalles/lÃ­neas de cada orden de recaudaciÃ³n';


--
-- Name: detalles_orden_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.detalles_orden_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: detalles_orden_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.detalles_orden_id_seq OWNED BY public.detalles_orden.id;


--
-- Name: django_admin_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_admin_log (
    id integer NOT NULL,
    action_time timestamp with time zone NOT NULL,
    object_id text,
    object_repr character varying(200) NOT NULL,
    action_flag smallint NOT NULL,
    change_message text NOT NULL,
    content_type_id integer,
    user_id bigint NOT NULL,
    CONSTRAINT django_admin_log_action_flag_check CHECK ((action_flag >= 0))
);


--
-- Name: django_admin_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_admin_log ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_admin_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_auditoria_evento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_auditoria_evento (
    id bigint NOT NULL,
    username_snapshot character varying(150) NOT NULL,
    rol_snapshot character varying(80) NOT NULL,
    ip_address inet,
    user_agent character varying(500) NOT NULL,
    accion character varying(120) NOT NULL,
    entidad character varying(120) NOT NULL,
    entidad_id character varying(80) NOT NULL,
    estado_anterior character varying(80) NOT NULL,
    estado_nuevo character varying(80) NOT NULL,
    observacion text NOT NULL,
    metodo_http character varying(12) NOT NULL,
    path character varying(500) NOT NULL,
    payload_resumen jsonb NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    usuario_id bigint
);


--
-- Name: django_aocr_auditoria_evento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_auditoria_evento ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_auditoria_evento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_bandeja_item; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_bandeja_item (
    id bigint NOT NULL,
    rol_destino character varying(80) NOT NULL,
    titulo character varying(200) NOT NULL,
    descripcion text NOT NULL,
    entidad_tipo character varying(100) NOT NULL,
    entidad_id character varying(80) NOT NULL,
    modulo character varying(80) NOT NULL,
    estado character varying(80) NOT NULL,
    url character varying(500) NOT NULL,
    prioridad smallint NOT NULL,
    leido boolean NOT NULL,
    activo boolean NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    grupo_destino_id integer,
    usuario_destino_id bigint,
    CONSTRAINT django_aocr_bandeja_item_prioridad_check CHECK ((prioridad >= 0))
);


--
-- Name: django_aocr_bandeja_item_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_bandeja_item ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_bandeja_item_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_certificado_aocr; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_certificado_aocr (
    id bigint NOT NULL,
    numero_aocr character varying(50),
    archivo character varying(100),
    nombre_original character varying(255),
    hash_sha256 character varying(255),
    version integer NOT NULL,
    estado character varying(50) NOT NULL,
    fecha_firma timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    inspeccion_id bigint NOT NULL,
    solicitud_formal_id bigint,
    firmado_por_id bigint
);


--
-- Name: django_aocr_certificado_aocr_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_certificado_aocr ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_certificado_aocr_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_comprobante_pago; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_comprobante_pago (
    id bigint NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(255) NOT NULL,
    version integer NOT NULL,
    fecha_carga timestamp with time zone NOT NULL,
    estado character varying(50) NOT NULL,
    observacion_revision text,
    orden_id bigint NOT NULL,
    usuario_carga_id bigint NOT NULL
);


--
-- Name: django_aocr_comprobante_pago_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_comprobante_pago ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_comprobante_pago_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_concepto_recaudacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_concepto_recaudacion (
    id bigint NOT NULL,
    codigo character varying(50) NOT NULL,
    nombre character varying(255) NOT NULL,
    descripcion text NOT NULL,
    valor_base numeric(10,2) NOT NULL,
    activo boolean NOT NULL,
    creado_en timestamp with time zone NOT NULL
);


--
-- Name: django_aocr_concepto_recaudacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_concepto_recaudacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_concepto_recaudacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_condiciones_limitaciones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_condiciones_limitaciones (
    id bigint NOT NULL,
    tipo character varying(50) NOT NULL,
    archivo character varying(100),
    nombre_original character varying(255),
    hash_sha256 character varying(255),
    version integer NOT NULL,
    estado character varying(50) NOT NULL,
    fecha_firma timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    inspeccion_id bigint NOT NULL,
    solicitud_formal_id bigint,
    firmado_por_id bigint
);


--
-- Name: django_aocr_condiciones_limitaciones_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_condiciones_limitaciones ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_condiciones_limitaciones_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_contador_bandeja; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_contador_bandeja (
    id bigint NOT NULL,
    rol character varying(80) NOT NULL,
    modulo character varying(80) NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(150) NOT NULL,
    cantidad integer NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    usuario_id bigint,
    CONSTRAINT django_aocr_contador_bandeja_cantidad_check CHECK ((cantidad >= 0))
);


--
-- Name: django_aocr_contador_bandeja_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_contador_bandeja ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_contador_bandeja_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_descarga_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_descarga_documento (
    id integer NOT NULL,
    documento_id integer NOT NULL,
    usuario_id integer,
    rol_snapshot character varying(100),
    ip_address inet,
    user_agent text,
    descargado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: django_aocr_descarga_documento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.django_aocr_descarga_documento_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: django_aocr_descarga_documento_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.django_aocr_descarga_documento_id_seq OWNED BY public.django_aocr_descarga_documento.id;


--
-- Name: django_aocr_designacion_inspector; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_designacion_inspector (
    id bigint NOT NULL,
    fecha_designacion timestamp with time zone NOT NULL,
    activo boolean NOT NULL,
    observacion text,
    coordinador_id bigint NOT NULL,
    inspector_id bigint NOT NULL,
    solicitud_id bigint NOT NULL
);


--
-- Name: django_aocr_designacion_inspector_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_designacion_inspector ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_designacion_inspector_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_documento_generado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_documento_generado (
    id integer NOT NULL,
    entidad_tipo character varying(50) NOT NULL,
    entidad_id bigint NOT NULL,
    tipo_documento character varying(50) NOT NULL,
    archivo character varying(255) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(255) NOT NULL,
    version integer DEFAULT 1 NOT NULL,
    estado character varying(50) DEFAULT 'VIGENTE'::character varying NOT NULL,
    generado_por_id integer,
    firmado boolean DEFAULT false NOT NULL,
    firmado_por_id integer,
    fecha_firma timestamp with time zone,
    creado_en timestamp with time zone DEFAULT now() NOT NULL,
    actualizado_en timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: django_aocr_documento_generado_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.django_aocr_documento_generado_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: django_aocr_documento_generado_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.django_aocr_documento_generado_id_seq OWNED BY public.django_aocr_documento_generado.id;


--
-- Name: django_aocr_documento_solicitud; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_documento_solicitud (
    id bigint NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(255) NOT NULL,
    version integer NOT NULL,
    estado character varying(50) NOT NULL,
    observacion_actual text,
    fecha_carga timestamp with time zone NOT NULL,
    activo boolean NOT NULL,
    solicitud_id bigint NOT NULL,
    usuario_carga_id bigint NOT NULL,
    tipo_documento_id bigint NOT NULL
);


--
-- Name: django_aocr_documento_solicitud_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_documento_solicitud ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_documento_solicitud_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_documento_subsanacion_nc; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_documento_subsanacion_nc (
    id bigint NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(255) NOT NULL,
    version integer NOT NULL,
    estado character varying(50) NOT NULL,
    observacion text,
    creado_en timestamp with time zone NOT NULL,
    usuario_carga_id bigint NOT NULL,
    no_conformidad_id bigint NOT NULL
);


--
-- Name: django_aocr_documento_subsanacion_nc_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_documento_subsanacion_nc ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_documento_subsanacion_nc_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_email_queue; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_email_queue (
    id bigint NOT NULL,
    destinatario character varying(254) NOT NULL,
    cc text NOT NULL,
    bcc text NOT NULL,
    asunto character varying(255) NOT NULL,
    cuerpo_html text NOT NULL,
    cuerpo_texto text NOT NULL,
    estado character varying(30) NOT NULL,
    intentos integer NOT NULL,
    ultimo_error text NOT NULL,
    entidad_tipo character varying(100) NOT NULL,
    entidad_id character varying(80) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    enviado_en timestamp with time zone,
    CONSTRAINT django_aocr_email_queue_intentos_check CHECK ((intentos >= 0))
);


--
-- Name: django_aocr_email_queue_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_email_queue ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_email_queue_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_estado_tramite; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_estado_tramite (
    id bigint NOT NULL,
    codigo character varying(50) NOT NULL,
    nombre character varying(150) NOT NULL,
    descripcion text NOT NULL,
    modulo character varying(80) NOT NULL,
    orden integer NOT NULL,
    es_final boolean NOT NULL,
    es_error boolean NOT NULL,
    activo boolean NOT NULL,
    CONSTRAINT django_aocr_estado_tramite_orden_check CHECK ((orden >= 0))
);


--
-- Name: django_aocr_estado_tramite_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_estado_tramite ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_estado_tramite_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_factura_recaudacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_factura_recaudacion (
    id bigint NOT NULL,
    numero_factura character varying(100) NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(255) NOT NULL,
    fecha_emision date NOT NULL,
    total numeric(10,2) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    generado_por_id bigint NOT NULL,
    orden_id bigint NOT NULL
);


--
-- Name: django_aocr_factura_recaudacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_factura_recaudacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_factura_recaudacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_firma_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_firma_documento (
    id integer NOT NULL,
    documento_id integer NOT NULL,
    usuario_firmante_id integer NOT NULL,
    rol_firmante character varying(100) NOT NULL,
    nombre_firmante_snapshot character varying(255) NOT NULL,
    cargo_firmante_snapshot character varying(255) NOT NULL,
    posicion_x double precision DEFAULT 0.0 NOT NULL,
    posicion_y double precision DEFAULT 0.0 NOT NULL,
    pagina integer DEFAULT 1 NOT NULL,
    ancho double precision DEFAULT 150.0 NOT NULL,
    alto double precision DEFAULT 50.0 NOT NULL,
    hash_antes_firma character varying(255) NOT NULL,
    hash_despues_firma character varying(255) NOT NULL,
    fecha_firma timestamp with time zone DEFAULT now() NOT NULL,
    ip_firma inet,
    user_agent text,
    estado character varying(50) DEFAULT 'FIRMADO'::character varying NOT NULL
);


--
-- Name: django_aocr_firma_documento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.django_aocr_firma_documento_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: django_aocr_firma_documento_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.django_aocr_firma_documento_id_seq OWNED BY public.django_aocr_firma_documento.id;


--
-- Name: django_aocr_historial_estado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_historial_estado (
    id bigint NOT NULL,
    entidad_tipo character varying(100) NOT NULL,
    entidad_id character varying(80) NOT NULL,
    estado_anterior character varying(80) NOT NULL,
    estado_nuevo character varying(80) NOT NULL,
    observacion text NOT NULL,
    rol_snapshot character varying(80) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    usuario_id bigint
);


--
-- Name: django_aocr_historial_estado_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_historial_estado ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_historial_estado_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_informe_tecnico; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_informe_tecnico (
    id bigint NOT NULL,
    antecedentes text NOT NULL,
    alcance text NOT NULL,
    desarrollo text NOT NULL,
    evidencias text NOT NULL,
    observaciones text NOT NULL,
    hallazgos text NOT NULL,
    conclusiones text NOT NULL,
    resultado character varying(50),
    archivo_pdf character varying(100),
    hash_pdf character varying(255),
    fecha_firma timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    firmado_por_id bigint,
    inspeccion_id bigint NOT NULL
);


--
-- Name: django_aocr_informe_tecnico_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_informe_tecnico ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_informe_tecnico_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_inspeccion_ejecucion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_inspeccion_ejecucion (
    id bigint NOT NULL,
    tipo_inspeccion character varying(50) NOT NULL,
    estado character varying(50) NOT NULL,
    resultado character varying(50),
    requiere_nueva_inspeccion boolean NOT NULL,
    requiere_subsanacion_documental boolean NOT NULL,
    observacion_actual text,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    coordinador_id bigint NOT NULL,
    inspector_id bigint NOT NULL,
    solicitud_formal_id bigint,
    solicitud_inspeccion_id bigint NOT NULL
);


--
-- Name: django_aocr_inspeccion_ejecucion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_inspeccion_ejecucion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_inspeccion_ejecucion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_lista_verificacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_lista_verificacion (
    id bigint NOT NULL,
    respuestas jsonb NOT NULL,
    completada boolean NOT NULL,
    archivo_pdf character varying(100),
    hash_pdf character varying(255),
    firmada boolean NOT NULL,
    fecha_firma timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    firmado_por_id bigint,
    inspeccion_id bigint NOT NULL
);


--
-- Name: django_aocr_lista_verificacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_lista_verificacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_lista_verificacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_no_conformidad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_no_conformidad (
    id bigint NOT NULL,
    tipo character varying(50) NOT NULL,
    descripcion text NOT NULL,
    archivo_pdf character varying(100),
    hash_pdf character varying(255),
    estado character varying(50) NOT NULL,
    fecha_firma timestamp with time zone,
    creado_en timestamp with time zone NOT NULL,
    firmado_por_id bigint,
    inspeccion_id bigint NOT NULL
);


--
-- Name: django_aocr_no_conformidad_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_no_conformidad ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_no_conformidad_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_notificacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_notificacion (
    id bigint NOT NULL,
    correo_destino character varying(254) NOT NULL,
    asunto character varying(255) NOT NULL,
    mensaje text NOT NULL,
    enviado boolean NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_envio timestamp with time zone,
    error_envio text NOT NULL,
    referencia character varying(255) NOT NULL,
    usuario_destino_id bigint,
    tipo_evento character varying(50) NOT NULL,
    tramite_id bigint,
    creado_en timestamp with time zone NOT NULL,
    entidad_id character varying(80) NOT NULL,
    entidad_tipo character varying(100) NOT NULL,
    leida boolean NOT NULL,
    leida_en timestamp with time zone,
    rol_destino character varying(80) NOT NULL,
    tipo character varying(50) NOT NULL,
    titulo character varying(200) NOT NULL,
    url character varying(500) NOT NULL
);


--
-- Name: django_aocr_orden_observacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_orden_observacion (
    id bigint NOT NULL,
    rol_snapshot character varying(100) NOT NULL,
    observacion text NOT NULL,
    estado_origen character varying(50) NOT NULL,
    estado_destino character varying(50) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    orden_id bigint NOT NULL,
    usuario_id bigint NOT NULL
);


--
-- Name: django_aocr_orden_observacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_orden_observacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_orden_observacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_orden_recaudacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_orden_recaudacion (
    id bigint NOT NULL,
    compania character varying(255) NOT NULL,
    ruc character varying(20) NOT NULL,
    email character varying(254) NOT NULL,
    tipo_tramite character varying(100) NOT NULL,
    inspecciones_requeridas boolean NOT NULL,
    fechas_inspeccion text,
    aeropuertos text,
    viaticos_inspector numeric(10,2) NOT NULL,
    numero_orden character varying(50),
    subtotal numeric(10,2) NOT NULL,
    iva numeric(10,2) NOT NULL,
    total numeric(10,2) NOT NULL,
    estado character varying(50) NOT NULL,
    pdf_orden character varying(100),
    pdf_orden_hash character varying(255),
    pdf_generado_en timestamp with time zone,
    bloqueada boolean NOT NULL,
    observacion_actual text,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    usuario_rt_id bigint NOT NULL
);


--
-- Name: django_aocr_orden_recaudacion_detalle; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_orden_recaudacion_detalle (
    id bigint NOT NULL,
    descripcion character varying(255) NOT NULL,
    cantidad integer NOT NULL,
    valor_unitario numeric(10,2) NOT NULL,
    total numeric(10,2) NOT NULL,
    concepto_id bigint NOT NULL,
    orden_id bigint NOT NULL
);


--
-- Name: django_aocr_orden_recaudacion_detalle_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_orden_recaudacion_detalle ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_orden_recaudacion_detalle_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_orden_recaudacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_orden_recaudacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_orden_recaudacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_plantilla_correo; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_plantilla_correo (
    id bigint NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(150) NOT NULL,
    asunto character varying(255) NOT NULL,
    cuerpo_html text NOT NULL,
    cuerpo_texto text NOT NULL,
    activa boolean NOT NULL
);


--
-- Name: django_aocr_plantilla_correo_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_plantilla_correo ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_plantilla_correo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_registro_rt; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_registro_rt (
    id bigint NOT NULL,
    compania character varying(120) NOT NULL,
    email character varying(254) NOT NULL,
    nombre character varying(200) NOT NULL,
    identificacion character varying(50) NOT NULL,
    nombre_eae character varying(255) DEFAULT ''::character varying NOT NULL,
    estado character varying(40) NOT NULL,
    declaracion_aceptada boolean NOT NULL,
    fecha_declaracion timestamp with time zone,
    ip_declaracion inet,
    formulario_designacion_archivo character varying(100),
    formulario_designacion_nombre_original character varying(255) DEFAULT ''::character varying NOT NULL,
    formulario_designacion_hash character varying(64) DEFAULT ''::character varying NOT NULL,
    formulario_designacion_fecha_carga timestamp with time zone,
    aceptacion_archivo character varying(100),
    aceptacion_hash character varying(64) DEFAULT ''::character varying NOT NULL,
    aceptacion_fecha timestamp with time zone,
    fecha_firma timestamp with time zone,
    observacion_actual text DEFAULT ''::text NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    firmado_por_id bigint,
    usuario_rt_id bigint NOT NULL
);


--
-- Name: django_aocr_registro_rt_archivo_version; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_registro_rt_archivo_version (
    id bigint NOT NULL,
    tipo_archivo character varying(50) NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(64) NOT NULL,
    version integer NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    activo boolean NOT NULL,
    registro_id bigint NOT NULL,
    usuario_carga_id bigint,
    CONSTRAINT django_aocr_registro_rt_archivo_version_version_check CHECK ((version >= 0))
);


--
-- Name: django_aocr_registro_rt_archivo_version_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_registro_rt_archivo_version ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_registro_rt_archivo_version_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_registro_rt_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_registro_rt ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_registro_rt_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_registro_rt_observacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_registro_rt_observacion (
    id bigint NOT NULL,
    rol_snapshot character varying(80) NOT NULL,
    observacion text NOT NULL,
    tipo character varying(50) NOT NULL,
    estado_origen character varying(40) NOT NULL,
    estado_destino character varying(40) NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    registro_id bigint NOT NULL,
    usuario_id bigint
);


--
-- Name: django_aocr_registro_rt_observacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_registro_rt_observacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_registro_rt_observacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_revision_documental; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_revision_documental (
    id bigint NOT NULL,
    rol_snapshot character varying(100) NOT NULL,
    decision_general character varying(50) NOT NULL,
    observacion_general text,
    todos_aceptados boolean NOT NULL,
    fecha_revision timestamp with time zone NOT NULL,
    solicitud_id bigint NOT NULL,
    usuario_revisor_id bigint NOT NULL
);


--
-- Name: django_aocr_revision_documental_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_revision_documental ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_revision_documental_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_revision_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_revision_documento (
    id bigint NOT NULL,
    rol_snapshot character varying(100) NOT NULL,
    decision character varying(50) NOT NULL,
    observacion text,
    fecha_revision timestamp with time zone NOT NULL,
    documento_id bigint NOT NULL,
    usuario_revisor_id bigint NOT NULL
);


--
-- Name: django_aocr_revision_documento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_revision_documento ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_revision_documento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_solicitud_formal; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_solicitud_formal (
    id bigint NOT NULL,
    compania character varying(255) NOT NULL,
    tipo_solicitud character varying(50) NOT NULL,
    subtipo_modificacion character varying(50),
    incluye_nuevo_aeropuerto boolean NOT NULL,
    estado character varying(50) NOT NULL,
    video_orientacion_visto boolean NOT NULL,
    fecha_video_visto timestamp with time zone,
    formulario_data jsonb NOT NULL,
    aceptacion_documental_archivo character varying(100),
    aceptacion_documental_hash character varying(255),
    condiciones_modificacion_archivo character varying(100),
    condiciones_modificacion_hash character varying(255),
    observacion_actual text,
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    coordinador_asignado_id bigint,
    inspector_asignado_id bigint,
    usuario_rt_id bigint NOT NULL
);


--
-- Name: django_aocr_solicitud_formal_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_solicitud_formal ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_solicitud_formal_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_solicitud_inspeccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_solicitud_inspeccion (
    id bigint NOT NULL,
    estado character varying(50) NOT NULL,
    actualizado_en timestamp with time zone NOT NULL,
    autorizacion_archivo character varying(100),
    autorizacion_hash character varying(255),
    compania character varying(255),
    coordinador_asignado_id bigint,
    creado_en timestamp with time zone NOT NULL,
    factura_archivo character varying(100),
    factura_hash character varying(255),
    factura_nombre_original character varying(255),
    factura_version integer NOT NULL,
    fecha_autorizacion timestamp with time zone,
    firmado_por_id bigint,
    formulario_data jsonb NOT NULL,
    inspector_asignado_id bigint,
    observacion_actual text,
    solicitud_formal_id bigint,
    tipo_inspeccion character varying(50) NOT NULL,
    usuario_rt_id bigint NOT NULL
);


--
-- Name: django_aocr_solicitud_inspeccion_archivo_version; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_solicitud_inspeccion_archivo_version (
    id bigint NOT NULL,
    tipo_archivo character varying(50) NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    hash_sha256 character varying(255) NOT NULL,
    version integer NOT NULL,
    creado_en timestamp with time zone NOT NULL,
    activo boolean NOT NULL,
    solicitud_inspeccion_id bigint NOT NULL,
    usuario_carga_id bigint NOT NULL
);


--
-- Name: django_aocr_solicitud_inspeccion_archivo_version_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_solicitud_inspeccion_archivo_version ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_solicitud_inspeccion_archivo_version_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_solicitud_inspeccion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_solicitud_inspeccion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_solicitud_inspeccion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_solicitud_inspeccion_observacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_solicitud_inspeccion_observacion (
    id bigint NOT NULL,
    rol_snapshot character varying(100) NOT NULL,
    observacion text NOT NULL,
    tipo character varying(50) NOT NULL,
    estado_origen character varying(50),
    estado_destino character varying(50),
    creado_en timestamp with time zone NOT NULL,
    solicitud_inspeccion_id bigint NOT NULL,
    usuario_id bigint NOT NULL
);


--
-- Name: django_aocr_solicitud_inspeccion_observacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_solicitud_inspeccion_observacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_solicitud_inspeccion_observacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_solicitud_observacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_solicitud_observacion (
    id bigint NOT NULL,
    rol_snapshot character varying(100) NOT NULL,
    observacion text NOT NULL,
    tipo character varying(50) NOT NULL,
    estado_origen character varying(50),
    estado_destino character varying(50),
    creado_en timestamp with time zone NOT NULL,
    solicitud_id bigint NOT NULL,
    usuario_id bigint NOT NULL
);


--
-- Name: django_aocr_solicitud_observacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_solicitud_observacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_solicitud_observacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_aocr_tipo_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_aocr_tipo_documento (
    id bigint NOT NULL,
    codigo character varying(50) NOT NULL,
    nombre character varying(255) NOT NULL,
    descripcion text,
    obligatorio boolean NOT NULL,
    aplica_emision boolean NOT NULL,
    aplica_renovacion boolean NOT NULL,
    aplica_modificacion boolean NOT NULL,
    activo boolean NOT NULL,
    orden integer NOT NULL
);


--
-- Name: django_aocr_tipo_documento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_tipo_documento ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_aocr_tipo_documento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_content_type; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_content_type (
    id integer NOT NULL,
    app_label character varying(100) NOT NULL,
    model character varying(100) NOT NULL
);


--
-- Name: django_content_type_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_content_type ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_content_type_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_migrations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_migrations (
    id bigint NOT NULL,
    app character varying(255) NOT NULL,
    name character varying(255) NOT NULL,
    applied timestamp with time zone NOT NULL
);


--
-- Name: django_migrations_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_migrations ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.django_migrations_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: django_session; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.django_session (
    session_key character varying(40) NOT NULL,
    session_data text NOT NULL,
    expire_date timestamp with time zone NOT NULL
);


--
-- Name: documentos_documento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.documentos_documento (
    id bigint NOT NULL,
    tipo character varying(40) NOT NULL,
    archivo character varying(100) NOT NULL,
    nombre_original character varying(255) NOT NULL,
    version integer NOT NULL,
    observacion text NOT NULL,
    fecha_carga timestamp with time zone NOT NULL,
    activo boolean NOT NULL,
    cargado_por_id bigint,
    estado character varying(20) NOT NULL,
    reemplazado_por_id bigint,
    tramite_id bigint,
    CONSTRAINT documentos_documento_version_check CHECK ((version >= 0))
);


--
-- Name: documentos_documento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.documentos_documento ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.documentos_documento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: email_attachment; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.email_attachment (
    id integer NOT NULL,
    email_queue_id integer NOT NULL,
    file_name character varying(255) NOT NULL,
    content_type character varying(120) NOT NULL,
    file_path text NOT NULL,
    file_size bigint,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    sha256 character varying(64)
);


--
-- Name: email_attachment_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.email_attachment_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: email_attachment_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.email_attachment_id_seq OWNED BY public.email_attachment.id;


--
-- Name: email_queue; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.email_queue (
    id integer NOT NULL,
    to_address character varying(255) NOT NULL,
    subject character varying(255) NOT NULL,
    body text NOT NULL,
    status character varying(20) NOT NULL,
    solicitud_id integer,
    orden_id integer,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    proximo_intento timestamp without time zone DEFAULT now() NOT NULL,
    event_key character varying(200),
    error_message text,
    intentos integer DEFAULT 0 NOT NULL,
    updated_at timestamp without time zone,
    tipo_notificacion character varying(120),
    correlation_id character varying(64),
    message_id character varying(255),
    sent_at timestamp without time zone
);


--
-- Name: TABLE email_queue; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.email_queue IS 'Cola de correos electrónicos para envío asíncrono';


--
-- Name: COLUMN email_queue.status; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.email_queue.status IS 'PENDIENTE, ENVIANDO, ENVIADO, ERROR, CANCELADO';


--
-- Name: COLUMN email_queue.solicitud_id; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.email_queue.solicitud_id IS 'Referencia a la solicitud (FK a aocr_tbsolicitud)';


--
-- Name: COLUMN email_queue.created_at; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.email_queue.created_at IS 'Fecha y hora de creación del registro';


--
-- Name: COLUMN email_queue.proximo_intento; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.email_queue.proximo_intento IS 'Fecha y hora del prÃ³ximo intento de envÃ­o (para lÃ³gica de reintentos)';


--
-- Name: email_queue_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.email_queue_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: email_queue_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.email_queue_id_seq OWNED BY public.email_queue.id;


--
-- Name: errores_importacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.errores_importacion (
    id bigint NOT NULL,
    importacion_id bigint NOT NULL,
    numero_fila integer NOT NULL,
    columna character varying(100),
    codigo_error character varying(50),
    descripcion text NOT NULL,
    datos_originales jsonb,
    created_at timestamp(0) without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: errores_importacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.errores_importacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: errores_importacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.errores_importacion_id_seq OWNED BY public.errores_importacion.id;


--
-- Name: especialidades; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.especialidades (
    id bigint NOT NULL,
    nombre character varying(255) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: especialidades_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.especialidades_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: especialidades_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.especialidades_id_seq OWNED BY public.especialidades.id;


--
-- Name: estados_capacitacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.estados_capacitacion (
    id bigint NOT NULL,
    nombre character varying(50) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: estados_capacitacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.estados_capacitacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: estados_capacitacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.estados_capacitacion_id_seq OWNED BY public.estados_capacitacion.id;


--
-- Name: estados_servidor; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.estados_servidor (
    id bigint NOT NULL,
    nombre character varying(50) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: estados_servidor_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.estados_servidor_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: estados_servidor_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.estados_servidor_id_seq OWNED BY public.estados_servidor.id;


--
-- Name: estatutos_organicos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.estatutos_organicos (
    id bigint NOT NULL,
    institucion_id bigint NOT NULL,
    nombre character varying(255) NOT NULL,
    fecha_aprobacion date,
    archivo_resolucion character varying(255),
    vigente boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: estatutos_organicos_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.estatutos_organicos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: estatutos_organicos_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.estatutos_organicos_id_seq OWNED BY public.estatutos_organicos.id;


--
-- Name: experiencia_profesional; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.experiencia_profesional (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    empresa character varying(150) NOT NULL,
    cargo character varying(150) NOT NULL,
    fecha_inicio date NOT NULL,
    fecha_fin date,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: experiencia_profesional_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.experiencia_profesional_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: experiencia_profesional_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.experiencia_profesional_id_seq OWNED BY public.experiencia_profesional.id;


--
-- Name: failed_jobs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.failed_jobs (
    id bigint NOT NULL,
    uuid character varying(255) NOT NULL,
    connection text NOT NULL,
    queue text NOT NULL,
    payload text NOT NULL,
    exception text NOT NULL,
    failed_at timestamp(0) without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL
);


--
-- Name: failed_jobs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.failed_jobs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: failed_jobs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.failed_jobs_id_seq OWNED BY public.failed_jobs.id;


--
-- Name: firmas_firmadocumento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.firmas_firmadocumento (
    id bigint NOT NULL,
    rol_firmante character varying(30) NOT NULL,
    fecha_firma timestamp with time zone,
    observacion text NOT NULL,
    activo boolean NOT NULL,
    documento_id bigint NOT NULL,
    firmado_por_id bigint
);


--
-- Name: firmas_firmadocumento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.firmas_firmadocumento ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.firmas_firmadocumento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: fr3; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.fr3 (
    id bigint NOT NULL,
    secuencial numeric,
    aeropuerto character varying(4),
    anio character varying(4),
    fecha character varying(8),
    tipo_operacion character varying(2),
    ruta character varying(20),
    nro_aterriza integer,
    subtotal numeric(9,2),
    total numeric(9,2),
    gran_total numeric(9,2),
    autorizacion character varying(20),
    observacion character varying(60),
    ruc character varying(13),
    email character varying(60),
    banco character varying(2),
    deposito character varying(15),
    numero_factura numeric,
    fecha_recepcion character varying(8),
    valor_charter numeric(9,2),
    created_at timestamp without time zone DEFAULT now()
);


--
-- Name: fr3_detalle; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.fr3_detalle (
    id bigint NOT NULL,
    fr3_secuencial numeric,
    aeropuerto character varying(4),
    anio character varying(4),
    secuencial_detalle numeric,
    tipo_cobro character varying(2),
    oid_formulario numeric,
    codigo_contable character varying(15),
    descripcion character varying(200),
    cantidad numeric,
    valor numeric(9,2),
    total numeric(9,2)
);


--
-- Name: fr3_detalle_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.fr3_detalle_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: fr3_detalle_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.fr3_detalle_id_seq OWNED BY public.fr3_detalle.id;


--
-- Name: fr3_detalle_pg; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.fr3_detalle_pg (
    id bigint NOT NULL,
    fr3_id bigint NOT NULL,
    secuencial_detalle numeric,
    tipo_cobro character varying(2),
    oid_formulario numeric,
    codigo_contable character varying(15),
    descripcion character varying(200),
    cantidad numeric,
    valor numeric(9,2),
    total numeric(9,2)
);


--
-- Name: fr3_detalle_pg_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.fr3_detalle_pg_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: fr3_detalle_pg_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.fr3_detalle_pg_id_seq OWNED BY public.fr3_detalle_pg.id;


--
-- Name: fr3_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.fr3_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: fr3_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.fr3_id_seq OWNED BY public.fr3.id;


--
-- Name: fr3_pg; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.fr3_pg (
    id bigint NOT NULL,
    num_solicitud integer NOT NULL,
    aeropuerto character varying(4) NOT NULL,
    anio character varying(4) NOT NULL,
    secuencial_db2 numeric,
    fecha character varying(8),
    tipo_operacion character varying(2),
    ruta character varying(20),
    nro_aterriza integer,
    subtotal numeric(9,2),
    total numeric(9,2),
    gran_total numeric(9,2),
    autorizacion character varying(20),
    observacion character varying(60),
    ruc character varying(13),
    email character varying(60),
    banco character varying(2),
    deposito character varying(15),
    numero_factura numeric,
    fecha_recepcion character varying(8),
    valor_charter numeric(9,2),
    sync_db2_estado character varying(15) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    sync_db2_intentos integer DEFAULT 0 NOT NULL,
    sync_db2_ultimo_error text,
    sync_db2_ultima_fecha timestamp without time zone,
    idempotency_key character varying(80) NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    updated_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: fr3_pg_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.fr3_pg_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: fr3_pg_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.fr3_pg_id_seq OWNED BY public.fr3_pg.id;


--
-- Name: gestiones_administrativas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.gestiones_administrativas (
    id bigint NOT NULL,
    codigo_legado character varying(20) NOT NULL,
    nombre character varying(150) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    usuario_creacion character varying(50),
    tabla_origen character varying(50) DEFAULT 'GESARC'::character varying NOT NULL,
    fecha_importacion timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN gestiones_administrativas.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.gestiones_administrativas.codigo_legado IS 'GESCOD de GESARC';


--
-- Name: gestiones_administrativas_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.gestiones_administrativas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: gestiones_administrativas_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.gestiones_administrativas_id_seq OWNED BY public.gestiones_administrativas.id;


--
-- Name: grupos_ocupacionales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.grupos_ocupacionales (
    id bigint NOT NULL,
    codigo_legado character varying(20),
    nombre character varying(150) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN grupos_ocupacionales.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.grupos_ocupacionales.codigo_legado IS 'GRUCOD de GRUARC';


--
-- Name: grupos_ocupacionales_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.grupos_ocupacionales_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: grupos_ocupacionales_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.grupos_ocupacionales_id_seq OWNED BY public.grupos_ocupacionales.id;


--
-- Name: historial_estados_orden; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.historial_estados_orden (
    id integer NOT NULL,
    orden_id integer NOT NULL,
    estado_anterior character varying(50),
    estado_nuevo character varying(50) NOT NULL,
    fecha_cambio timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    usuario_cambio character varying(100)
);


--
-- Name: historial_estados_orden_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.historial_estados_orden_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: historial_estados_orden_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.historial_estados_orden_id_seq OWNED BY public.historial_estados_orden.id;


--
-- Name: historial_laboral; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.historial_laboral (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    descripcion text NOT NULL,
    fecha date NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: historial_laboral_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.historial_laboral_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: historial_laboral_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.historial_laboral_id_seq OWNED BY public.historial_laboral.id;


--
-- Name: importaciones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.importaciones (
    id bigint NOT NULL,
    nombre_archivo character varying(255) NOT NULL,
    hash_archivo character varying(64) NOT NULL,
    tamano_bytes integer NOT NULL,
    delimitador character varying(10) DEFAULT '.'::character varying NOT NULL,
    tabla_origen character varying(100),
    total_registros integer DEFAULT 0 NOT NULL,
    registros_validos integer DEFAULT 0 NOT NULL,
    registros_rechazados integer DEFAULT 0 NOT NULL,
    estado character varying(30) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    usuario_id bigint,
    fecha_inicio timestamp(0) without time zone DEFAULT CURRENT_TIMESTAMP NOT NULL,
    fecha_fin timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: importaciones_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.importaciones_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: importaciones_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.importaciones_id_seq OWNED BY public.importaciones.id;


--
-- Name: inspecciones_inspecciontecnica; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.inspecciones_inspecciontecnica (
    id bigint NOT NULL,
    resultado character varying(20) NOT NULL,
    observaciones text NOT NULL,
    fecha_resultado timestamp with time zone,
    informe_tecnico_id bigint,
    inspector_id bigint,
    lv_id bigint,
    solicitud_id bigint NOT NULL,
    con_inspeccion boolean NOT NULL,
    estado character varying(40) NOT NULL,
    requiere_nc boolean NOT NULL,
    requiere_nueva_inspeccion boolean NOT NULL,
    tipo_inspeccion character varying(50) NOT NULL
);


--
-- Name: inspecciones_inspecciontecnica_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.inspecciones_inspecciontecnica ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.inspecciones_inspecciontecnica_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: inspecciones_noconformidad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.inspecciones_noconformidad (
    id bigint NOT NULL,
    descripcion text NOT NULL,
    con_inspeccion boolean NOT NULL,
    requiere_nueva_inspeccion boolean NOT NULL,
    estado character varying(40) NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    documento_id bigint,
    inspeccion_id bigint NOT NULL,
    fecha_firma timestamp with time zone,
    firmado_por_coordinador boolean NOT NULL,
    revisado_por_coordinador_id bigint
);


--
-- Name: inspecciones_noconformidad_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.inspecciones_noconformidad ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.inspecciones_noconformidad_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: inspecciones_solicitudinspeccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.inspecciones_solicitudinspeccion (
    id bigint NOT NULL,
    estado character varying(40) NOT NULL,
    observacion text NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_actualizacion timestamp with time zone NOT NULL,
    factura_id bigint,
    inspector_id bigint,
    solicitud_id bigint NOT NULL,
    autorizacion_id bigint,
    fecha_firma_coordinador timestamp with time zone,
    firmado_por_coordinador_id bigint,
    tipo character varying(40) NOT NULL
);


--
-- Name: inspecciones_solicitudinspeccion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.inspecciones_solicitudinspeccion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.inspecciones_solicitudinspeccion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: instituciones; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.instituciones (
    id bigint NOT NULL,
    ruc character varying(20) NOT NULL,
    nombre character varying(255) NOT NULL,
    siglas character varying(20),
    activo boolean DEFAULT true NOT NULL,
    tabla_origen character varying(50),
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: instituciones_capacitadoras; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.instituciones_capacitadoras (
    id bigint NOT NULL,
    nombre character varying(255) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: instituciones_capacitadoras_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.instituciones_capacitadoras_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: instituciones_capacitadoras_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.instituciones_capacitadoras_id_seq OWNED BY public.instituciones_capacitadoras.id;


--
-- Name: instituciones_educativas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.instituciones_educativas (
    id bigint NOT NULL,
    nombre character varying(255) NOT NULL,
    pais character varying(100) DEFAULT 'ECUADOR'::character varying NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: instituciones_educativas_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.instituciones_educativas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: instituciones_educativas_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.instituciones_educativas_id_seq OWNED BY public.instituciones_educativas.id;


--
-- Name: instituciones_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.instituciones_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: instituciones_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.instituciones_id_seq OWNED BY public.instituciones.id;


--
-- Name: instrucciones_formales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.instrucciones_formales (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    nivel_instruccion_id bigint,
    institucion_educativa_id bigint,
    numero_registro_senescyt character varying(60),
    fecha_obtencion date,
    estado_validation character varying(30) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    observaciones text,
    codigo_legado character varying(50),
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: instrucciones_formales_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.instrucciones_formales_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: instrucciones_formales_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.instrucciones_formales_id_seq OWNED BY public.instrucciones_formales.id;


--
-- Name: job_batches; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.job_batches (
    id character varying(255) NOT NULL,
    name character varying(255) NOT NULL,
    total_jobs integer NOT NULL,
    pending_jobs integer NOT NULL,
    failed_jobs integer NOT NULL,
    failed_job_ids text NOT NULL,
    options text,
    cancelled_at integer,
    created_at integer NOT NULL,
    finished_at integer
);


--
-- Name: jobs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.jobs (
    id bigint NOT NULL,
    queue character varying(255) NOT NULL,
    payload text NOT NULL,
    attempts smallint NOT NULL,
    reserved_at integer,
    available_at integer NOT NULL,
    created_at integer NOT NULL
);


--
-- Name: jobs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.jobs_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: jobs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.jobs_id_seq OWNED BY public.jobs.id;


--
-- Name: lugares_trabajo; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.lugares_trabajo (
    id bigint NOT NULL,
    codigo_legado character varying(20),
    nombre character varying(150) NOT NULL,
    provincia character varying(100),
    canton character varying(100),
    tipo character varying(50),
    activo boolean DEFAULT true NOT NULL,
    tabla_origen character varying(50) DEFAULT 'LUGARC'::character varying NOT NULL,
    fecha_importacion timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN lugares_trabajo.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.lugares_trabajo.codigo_legado IS 'LUGOID / LUGCOD de LUGARC';


--
-- Name: lugares_trabajo_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.lugares_trabajo_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: lugares_trabajo_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.lugares_trabajo_id_seq OWNED BY public.lugares_trabajo.id;


--
-- Name: materias; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.materias (
    id bigint NOT NULL,
    curso_id bigint,
    codigo_legado character varying(30),
    nombre character varying(180) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN materias.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.materias.codigo_legado IS 'MATMAT de MATARC';


--
-- Name: materias_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.materias_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: materias_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.materias_id_seq OWNED BY public.materias.id;


--
-- Name: menu; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.menu (
    idmenu integer NOT NULL,
    nombre character varying(100) NOT NULL,
    icono character varying(50),
    activo boolean DEFAULT true,
    fecharegistro timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: menu_idmenu_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.menu_idmenu_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: menu_idmenu_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.menu_idmenu_seq OWNED BY public.menu.idmenu;


--
-- Name: migrations; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.migrations (
    id integer NOT NULL,
    migration character varying(255) NOT NULL,
    batch integer NOT NULL
);


--
-- Name: migrations_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.migrations_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: migrations_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.migrations_id_seq OWNED BY public.migrations.id;


--
-- Name: modalidades_prestacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modalidades_prestacion (
    id bigint NOT NULL,
    codigo_legado character varying(20),
    nombre character varying(150) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN modalidades_prestacion.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.modalidades_prestacion.codigo_legado IS 'MODCOD de MODAR';


--
-- Name: modalidades_prestacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.modalidades_prestacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: modalidades_prestacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.modalidades_prestacion_id_seq OWNED BY public.modalidades_prestacion.id;


--
-- Name: model_has_permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.model_has_permissions (
    permission_id bigint NOT NULL,
    model_type character varying(255) NOT NULL,
    model_id bigint NOT NULL
);


--
-- Name: model_has_roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.model_has_roles (
    role_id bigint NOT NULL,
    model_type character varying(255) NOT NULL,
    model_id bigint NOT NULL
);


--
-- Name: modificacion_cyl_aeropuertomodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_aeropuertomodificacioncyl (
    id bigint NOT NULL,
    nombre_aeropuerto character varying(255) NOT NULL,
    ciudad character varying(100) NOT NULL,
    provincia character varying(100) NOT NULL,
    tipo_operacion character varying(100) NOT NULL,
    justificacion text NOT NULL,
    fecha_tentativa date,
    requiere_inspeccion boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    created_by_id bigint NOT NULL,
    solicitud_id bigint NOT NULL
);


--
-- Name: modificacion_cyl_aeropuertomodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_aeropuertomodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_aeropuertomodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: modificacion_cyl_documentogeneradomodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_documentogeneradomodificacioncyl (
    id bigint NOT NULL,
    tipo_documento character varying(50) NOT NULL,
    numero_documento character varying(100) NOT NULL,
    archivo_pdf character varying(100) NOT NULL,
    archivo_firmado_pdf character varying(100),
    hash_pdf character varying(64) NOT NULL,
    hash_pdf_firmado character varying(64),
    firmado boolean NOT NULL,
    rol_firmante character varying(50),
    fecha_generacion timestamp with time zone NOT NULL,
    fecha_firma timestamp with time zone,
    estado character varying(50),
    created_at timestamp with time zone NOT NULL,
    firmado_por_id bigint,
    generado_por_id bigint,
    solicitud_id bigint NOT NULL
);


--
-- Name: modificacion_cyl_documentogeneradomodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_documentogeneradomodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_documentogeneradomodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: modificacion_cyl_documentomodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_documentomodificacioncyl (
    id bigint NOT NULL,
    tipo_documento character varying(50) NOT NULL,
    nombre_visible character varying(255) NOT NULL,
    nombre_archivo_original character varying(255) NOT NULL,
    nombre_archivo_storage character varying(255) NOT NULL,
    archivo character varying(100),
    ruta_guardada character varying(500) NOT NULL,
    extension character varying(10) NOT NULL,
    content_type character varying(100) NOT NULL,
    tamano_bytes integer,
    hash_archivo character varying(64) NOT NULL,
    estado character varying(20) NOT NULL,
    version integer NOT NULL,
    observacion_actual text,
    obligatorio boolean NOT NULL,
    validado boolean NOT NULL,
    fecha_carga timestamp with time zone,
    fecha_revision timestamp with time zone,
    activo boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    cargado_por_id bigint,
    created_by_id bigint NOT NULL,
    documento_base_id bigint,
    revisado_por_id bigint,
    updated_by_id bigint,
    solicitud_id bigint NOT NULL,
    CONSTRAINT modificacion_cyl_documentomodificacioncyl_tamano_bytes_check CHECK ((tamano_bytes >= 0)),
    CONSTRAINT modificacion_cyl_documentomodificacioncyl_version_check CHECK ((version >= 0))
);


--
-- Name: modificacion_cyl_documentomodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_documentomodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_documentomodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: modificacion_cyl_historialdocumentomodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_historialdocumentomodificacioncyl (
    id bigint NOT NULL,
    evento character varying(255) NOT NULL,
    detalle text,
    estado_anterior character varying(50),
    estado_nuevo character varying(50) NOT NULL,
    rol character varying(50) NOT NULL,
    fecha timestamp with time zone NOT NULL,
    documento_id bigint NOT NULL,
    usuario_id bigint,
    solicitud_id bigint NOT NULL
);


--
-- Name: modificacion_cyl_historialdocumentomodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_historialdocumentomodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_historialdocumentomodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: modificacion_cyl_historialmodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_historialmodificacioncyl (
    id bigint NOT NULL,
    estado_anterior character varying(50),
    estado_nuevo character varying(50) NOT NULL,
    accion character varying(255) NOT NULL,
    observacion text,
    rol character varying(50) NOT NULL,
    fecha timestamp with time zone NOT NULL,
    usuario_id bigint,
    solicitud_id bigint NOT NULL
);


--
-- Name: modificacion_cyl_historialmodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_historialmodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_historialmodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_revisiondocumentomodificacioncyl (
    id bigint NOT NULL,
    decision character varying(20) NOT NULL,
    observacion text,
    rol_revisor character varying(50) NOT NULL,
    fecha_revision timestamp with time zone NOT NULL,
    created_at timestamp with time zone NOT NULL,
    created_by_id bigint NOT NULL,
    documento_id bigint NOT NULL,
    revisado_por_id bigint,
    solicitud_id bigint NOT NULL
);


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_revisiondocumentomodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_revisiondocumentomodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: modificacion_cyl_solicitudmodificacioncyl; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.modificacion_cyl_solicitudmodificacioncyl (
    id bigint NOT NULL,
    numero_solicitud character varying(50),
    compania character varying(255) NOT NULL,
    ruc character varying(20) NOT NULL,
    correo character varying(254) NOT NULL,
    telefono character varying(30) NOT NULL,
    estado character varying(50) NOT NULL,
    tipo_modificacion character varying(50) NOT NULL,
    descripcion_modificacion text,
    cambio_rt boolean NOT NULL,
    inclusion_equipos boolean NOT NULL,
    inclusion_nuevo_aeropuerto boolean NOT NULL,
    otro_cambio boolean NOT NULL,
    detalle_cambio_rt text,
    detalle_equipos text,
    detalle_aeropuertos text,
    justificacion text NOT NULL,
    implica_nuevo_aeropuerto boolean,
    fecha_decision_nuevo_aeropuerto timestamp with time zone,
    fecha_designacion_inspector timestamp with time zone,
    observacion_coordinador text,
    observacion_inspector text,
    archivo_cierre_documental_pdf character varying(100),
    archivo_cyl_pdf character varying(100),
    archivo_cyl_firmado_pdf character varying(100),
    firmado_dcav boolean NOT NULL,
    fecha_firma_dcav timestamp with time zone,
    hash_documento_firmado character varying(64),
    activa boolean NOT NULL,
    created_at timestamp with time zone NOT NULL,
    updated_at timestamp with time zone NOT NULL,
    coordinador_designa_id bigint,
    created_by_id bigint NOT NULL,
    decision_nuevo_aeropuerto_por_id bigint,
    firmado_por_dcav_id bigint,
    inspector_designado_id bigint,
    operador_eae_id bigint NOT NULL,
    rt_id bigint NOT NULL,
    updated_by_id bigint
);


--
-- Name: modificacion_cyl_solicitudmodificacioncyl_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.modificacion_cyl_solicitudmodificacioncyl ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.modificacion_cyl_solicitudmodificacioncyl_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: niveles_instruccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.niveles_instruccion (
    id bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: niveles_instruccion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.niveles_instruccion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: niveles_instruccion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.niveles_instruccion_id_seq OWNED BY public.niveles_instruccion.id;


--
-- Name: niveles_proceso; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.niveles_proceso (
    id bigint NOT NULL,
    codigo_legado character varying(20),
    nombre character varying(150) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN niveles_proceso.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.niveles_proceso.codigo_legado IS 'NIVCOD de NIVARC';


--
-- Name: niveles_proceso_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.niveles_proceso_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: niveles_proceso_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.niveles_proceso_id_seq OWNED BY public.niveles_proceso.id;


--
-- Name: notificaciones_notificacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.django_aocr_notificacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.notificaciones_notificacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: operadores_operador; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.operadores_operador (
    id bigint NOT NULL,
    nombre_eae character varying(255) NOT NULL,
    ruc character varying(20) NOT NULL,
    email character varying(254) NOT NULL,
    pais character varying(100) NOT NULL,
    representante_legal character varying(255) NOT NULL,
    activo boolean NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL
);


--
-- Name: operadores_operador_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.operadores_operador ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.operadores_operador_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: operadores_registrort; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.operadores_registrort (
    id bigint NOT NULL,
    nombre_rt character varying(255) NOT NULL,
    email_rt character varying(254) NOT NULL,
    identificacion_rt character varying(30) NOT NULL,
    acepta_declaracion boolean NOT NULL,
    estado character varying(50) NOT NULL,
    observacion_coordinador text NOT NULL,
    aceptacion_firmada boolean NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_envio timestamp with time zone,
    fecha_revision timestamp with time zone,
    fecha_actualizacion timestamp with time zone NOT NULL,
    formulario_designacion_id bigint,
    operador_id bigint NOT NULL,
    revisado_por_id bigint,
    usuario_rt_id bigint NOT NULL,
    documento_aceptacion_id bigint
);


--
-- Name: operadores_registrort_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.operadores_registrort ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.operadores_registrort_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: operadores_representantetecnico; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.operadores_representantetecnico (
    id bigint NOT NULL,
    identificacion character varying(30) NOT NULL,
    cargo character varying(120) NOT NULL,
    telefono character varying(30) NOT NULL,
    activo boolean NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    operador_id bigint NOT NULL,
    usuario_id bigint NOT NULL,
    registro_rt_id bigint
);


--
-- Name: operadores_representantetecnico_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.operadores_representantetecnico ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.operadores_representantetecnico_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: ordenes_recaudacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.ordenes_recaudacion (
    id integer NOT NULL,
    numero_orden character varying(50) NOT NULL,
    solicitud_id integer,
    concepto_id integer,
    contribuyente_id integer,
    subtotal numeric(18,2) DEFAULT 0 NOT NULL,
    iva numeric(18,2) DEFAULT 0 NOT NULL,
    total numeric(18,2) DEFAULT 0 NOT NULL,
    observaciones text,
    estado character varying(50) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    fecha_creacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion timestamp without time zone,
    usuario_creacion character varying(100),
    usuario_actualizacion character varying(100),
    activo boolean DEFAULT true,
    fecha_vencimiento date,
    fecha_pago timestamp without time zone,
    referencia_pago character varying(100),
    motivo_anulacion text,
    fecha_anulacion timestamp without time zone,
    usuario_anulacion character varying(100)
);


--
-- Name: TABLE ordenes_recaudacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.ordenes_recaudacion IS 'Tabla principal de Ã³rdenes de recaudaciÃ³n del sistema AOCR';


--
-- Name: ordenes_recaudacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.ordenes_recaudacion_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: ordenes_recaudacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.ordenes_recaudacion_id_seq OWNED BY public.ordenes_recaudacion.id;


--
-- Name: otros_estudios; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.otros_estudios (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    tipo_instruccion character varying(100) NOT NULL,
    nombre_curso character varying(200) NOT NULL,
    entidad_educativa character varying(180) NOT NULL,
    ciudad character varying(80),
    fecha_fin date,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: otros_estudios_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.otros_estudios_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: otros_estudios_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.otros_estudios_id_seq OWNED BY public.otros_estudios.id;


--
-- Name: pagos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.pagos (
    id integer NOT NULL,
    orden_id integer NOT NULL,
    numero_comprobante character varying(100),
    monto_pagado numeric(18,2) NOT NULL,
    fecha_pago timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    metodo_pago character varying(50),
    banco_origen character varying(100),
    observaciones text,
    ruta_comprobante character varying(500),
    estado character varying(50) DEFAULT 'PENDIENTE'::character varying,
    fecha_registro timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    usuario_registro character varying(100),
    fecha_validacion timestamp without time zone,
    usuario_validacion character varying(100)
);


--
-- Name: TABLE pagos; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.pagos IS 'Registro de pagos asociados a Ã³rdenes de recaudaciÃ³n';


--
-- Name: pagos_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.pagos_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: pagos_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.pagos_id_seq OWNED BY public.pagos.id;


--
-- Name: parametros; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.parametros (
    id integer NOT NULL,
    clave character varying(100) NOT NULL,
    valor text NOT NULL,
    descripcion text,
    tipo character varying(20) DEFAULT 'STRING'::character varying,
    activo boolean DEFAULT true,
    fecha_creacion timestamp without time zone DEFAULT now(),
    fecha_modificacion timestamp without time zone DEFAULT now(),
    usuario_modificacion character varying(50)
);


--
-- Name: parametros_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.parametros_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: parametros_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.parametros_id_seq OWNED BY public.parametros.id;


--
-- Name: password_reset_tokens; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.password_reset_tokens (
    email character varying(255) NOT NULL,
    token character varying(255) NOT NULL,
    created_at timestamp(0) without time zone
);


--
-- Name: permisos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.permisos (
    idpermisos integer NOT NULL,
    codigorol integer,
    idsubmenu integer,
    activo boolean DEFAULT true,
    fecharegistro timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: permisos_idpermisos_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.permisos_idpermisos_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: permisos_idpermisos_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.permisos_idpermisos_seq OWNED BY public.permisos.idpermisos;


--
-- Name: permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.permissions (
    id bigint NOT NULL,
    name character varying(255) NOT NULL,
    guard_name character varying(255) NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: permissions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.permissions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: permissions_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.permissions_id_seq OWNED BY public.permissions.id;


--
-- Name: personas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.personas (
    id bigint NOT NULL,
    numero_documento character varying(20) NOT NULL,
    tipo_identificacion character varying(10) DEFAULT 'CEDULA'::character varying NOT NULL,
    primer_nombre character varying(60) NOT NULL,
    segundo_nombre character varying(60),
    primer_apellido character varying(60) NOT NULL,
    segundo_apellido character varying(60),
    nombre_completo character varying(200) NOT NULL,
    genero character varying(20),
    fecha_nacimiento date,
    email_personal character varying(100),
    telefono character varying(30),
    celular character varying(30),
    direccion_domicilio text,
    codigo_legado character varying(30),
    tabla_origen character varying(50) DEFAULT 'MAEAR1'::character varying NOT NULL,
    fecha_importacion timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN personas.numero_documento; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.personas.numero_documento IS 'MAEDOC (cédula/pasaporte)';


--
-- Name: personas_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.personas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: personas_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.personas_id_seq OWNED BY public.personas.id;


--
-- Name: programas_capacitacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.programas_capacitacion (
    id bigint NOT NULL,
    nombre character varying(255) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: programas_capacitacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.programas_capacitacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: programas_capacitacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.programas_capacitacion_id_seq OWNED BY public.programas_capacitacion.id;


--
-- Name: puestos_institucionales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.puestos_institucionales (
    id bigint NOT NULL,
    codigo_legado character varying(20),
    nombre character varying(150) NOT NULL,
    grupo_ocupacional_id bigint,
    nivel_proceso_id bigint,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN puestos_institucionales.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.puestos_institucionales.codigo_legado IS 'PUEOID / PUECOD de PUEARC';


--
-- Name: puestos_institucionales_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.puestos_institucionales_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: puestos_institucionales_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.puestos_institucionales_id_seq OWNED BY public.puestos_institucionales.id;


--
-- Name: recaudacion_comprobantepago; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.recaudacion_comprobantepago (
    id bigint NOT NULL,
    archivo character varying(100) NOT NULL,
    version integer NOT NULL,
    estado character varying(20) NOT NULL,
    observacion text NOT NULL,
    fecha_carga timestamp with time zone NOT NULL,
    activo boolean NOT NULL,
    cargado_por_id bigint,
    orden_id bigint NOT NULL,
    reemplazado_por_id bigint,
    CONSTRAINT recaudacion_comprobantepago_version_check CHECK ((version >= 0))
);


--
-- Name: recaudacion_comprobantepago_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.recaudacion_comprobantepago ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.recaudacion_comprobantepago_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: recaudacion_factura; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.recaudacion_factura (
    id bigint NOT NULL,
    numero_factura character varying(50) NOT NULL,
    fecha_generacion timestamp with time zone NOT NULL,
    enviada_rt boolean NOT NULL,
    fecha_envio timestamp with time zone,
    activa boolean NOT NULL,
    archivo_pdf_factura_id bigint,
    generada_por_id bigint,
    orden_id bigint NOT NULL
);


--
-- Name: recaudacion_factura_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.recaudacion_factura ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.recaudacion_factura_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: recaudacion_ordenrecaudacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.recaudacion_ordenrecaudacion (
    id bigint NOT NULL,
    ruc character varying(20) NOT NULL,
    email character varying(254) NOT NULL,
    tipo_tramite character varying(40) NOT NULL,
    inspecciones_requeridas integer NOT NULL,
    viaticos_inspector numeric(10,2) NOT NULL,
    numero_orden character varying(50) NOT NULL,
    estado character varying(40) NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_actualizacion timestamp with time zone NOT NULL,
    operador_id bigint NOT NULL,
    rt_id bigint,
    aeropuertos text NOT NULL,
    documento_orden_id bigint,
    fecha_envio timestamp with time zone,
    fecha_revision timestamp with time zone,
    fechas_inspeccion text NOT NULL,
    revisado_por_id bigint,
    activa boolean NOT NULL,
    bloqueada boolean NOT NULL,
    creada_por_id bigint,
    tramite_id bigint,
    valor_base numeric(10,2) NOT NULL,
    valor_total numeric(10,2) NOT NULL,
    valor_viaticos numeric(10,2) NOT NULL,
    CONSTRAINT recaudacion_ordenrecaudacion_inspecciones_requeridas_check CHECK ((inspecciones_requeridas >= 0))
);


--
-- Name: recaudacion_ordenrecaudacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.recaudacion_ordenrecaudacion ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.recaudacion_ordenrecaudacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: rol; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.rol (
    codigorol integer NOT NULL,
    descripcion character varying(200) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    fecha_registro timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    usuariocreado character varying(100),
    fechacreado timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    usuariomodificado character varying(100),
    fechamodificado timestamp without time zone
);


--
-- Name: TABLE rol; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON TABLE public.rol IS 'Roles del sistema AOCR para control de acceso basado en roles (RBAC)';


--
-- Name: rol_codigorol_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.rol_codigorol_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: rol_codigorol_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.rol_codigorol_seq OWNED BY public.rol.codigorol;


--
-- Name: role_has_permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.role_has_permissions (
    permission_id bigint NOT NULL,
    role_id bigint NOT NULL
);


--
-- Name: roles; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.roles (
    id bigint NOT NULL,
    name character varying(255) NOT NULL,
    guard_name character varying(255) NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: roles_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.roles_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: roles_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.roles_id_seq OWNED BY public.roles.id;


--
-- Name: seguridad_permiso; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.seguridad_permiso (
    id_permiso bigint NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(180) NOT NULL,
    modulo character varying(80) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    creado_en timestamp without time zone DEFAULT now() NOT NULL,
    creado_por character varying(100),
    actualizado_en timestamp without time zone,
    actualizado_por character varying(100),
    tipo_accion character varying(30) NOT NULL,
    descripcion character varying(300) NOT NULL
);


--
-- Name: COLUMN seguridad_permiso.tipo_accion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.seguridad_permiso.tipo_accion IS 'Acción funcional utilizada por la gestión de permisos.';


--
-- Name: COLUMN seguridad_permiso.descripcion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.seguridad_permiso.descripcion IS 'Descripción funcional del permiso.';


--
-- Name: seguridad_permiso_id_permiso_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.seguridad_permiso_id_permiso_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: seguridad_permiso_id_permiso_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.seguridad_permiso_id_permiso_seq OWNED BY public.seguridad_permiso.id_permiso;


--
-- Name: seguridad_rol_permiso; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.seguridad_rol_permiso (
    codigorol integer NOT NULL,
    id_permiso bigint NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    creado_en timestamp without time zone DEFAULT now() NOT NULL,
    creado_por character varying(100),
    actualizado_en timestamp without time zone,
    actualizado_por character varying(100)
);


--
-- Name: seq_aocr_orden_recaudacion; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.seq_aocr_orden_recaudacion
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: servidor_discapacidades; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.servidor_discapacidades (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    tipo_discapacidad character varying(100) NOT NULL,
    porcentaje numeric(5,2) NOT NULL,
    carnet_conadis character varying(50),
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: servidor_discapacidades_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.servidor_discapacidades_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: servidor_discapacidades_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.servidor_discapacidades_id_seq OWNED BY public.servidor_discapacidades.id;


--
-- Name: servidor_titulos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.servidor_titulos (
    id bigint NOT NULL,
    servidor_id bigint NOT NULL,
    titulo_id bigint NOT NULL,
    institucion_educativa_id bigint,
    especialidad_id bigint,
    fecha_registro date,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: servidor_titulos_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.servidor_titulos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: servidor_titulos_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.servidor_titulos_id_seq OWNED BY public.servidor_titulos.id;


--
-- Name: servidores; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.servidores (
    id bigint NOT NULL,
    persona_id bigint NOT NULL,
    institucion_id bigint NOT NULL,
    codigo_servidor character varying(30),
    email_institucional character varying(100),
    fecha_ingreso date,
    estado_servidor character varying(30) DEFAULT 'ACTIVO'::character varying NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: servidores_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.servidores_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: servidores_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.servidores_id_seq OWNED BY public.servidores.id;


--
-- Name: sessions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sessions (
    id character varying(255) NOT NULL,
    user_id bigint,
    ip_address character varying(45),
    user_agent text,
    payload text NOT NULL,
    last_activity integer NOT NULL
);


--
-- Name: solicitudes_documentosolicitud; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.solicitudes_documentosolicitud (
    id bigint NOT NULL,
    obligatorio boolean NOT NULL,
    estado_revision character varying(20) NOT NULL,
    observacion text NOT NULL,
    fecha_revision timestamp with time zone,
    documento_id bigint NOT NULL,
    revisado_por_id bigint,
    solicitud_id bigint NOT NULL
);


--
-- Name: solicitudes_documentosolicitud_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.solicitudes_documentosolicitud ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.solicitudes_documentosolicitud_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: solicitudes_solicitudaocr; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.solicitudes_solicitudaocr (
    id bigint NOT NULL,
    tipo character varying(40) NOT NULL,
    estado character varying(40) NOT NULL,
    observacion_actual text NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_envio timestamp with time zone,
    fecha_actualizacion timestamp with time zone NOT NULL,
    coordinador_id bigint,
    inspector_id bigint,
    operador_id bigint NOT NULL,
    orden_recaudacion_id bigint,
    rt_id bigint,
    cambio_rt boolean NOT NULL,
    cierre_fase_documental_id bigint,
    coordinador_revisor_id bigint,
    dcav_firmante_id bigint,
    documento_condiciones_generado_id bigint,
    fecha_firma_dcav timestamp with time zone,
    formulario_completado boolean NOT NULL,
    implica_nuevo_aeropuerto boolean NOT NULL,
    inclusion_equipos boolean NOT NULL,
    inclusion_nuevos_aeropuertos boolean NOT NULL,
    video_orientacion_visto boolean NOT NULL
);


--
-- Name: solicitudes_solicitudaocr_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.solicitudes_solicitudaocr ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.solicitudes_solicitudaocr_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: staging_curar; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.staging_curar (
    id bigint NOT NULL,
    importacion_id bigint NOT NULL,
    numero_fila integer NOT NULL,
    curcod character varying(255),
    curdes character varying(255),
    estado_validacion character varying(30) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    errores jsonb,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: staging_curar_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.staging_curar_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: staging_curar_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.staging_curar_id_seq OWNED BY public.staging_curar.id;


--
-- Name: staging_maear; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.staging_maear (
    id bigint NOT NULL,
    importacion_id bigint NOT NULL,
    numero_fila integer NOT NULL,
    maedoc character varying(255),
    maenom character varying(255),
    maegen character varying(255),
    maefna character varying(255),
    estado_validacion character varying(30) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    errores jsonb,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: staging_maear_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.staging_maear_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: staging_maear_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.staging_maear_id_seq OWNED BY public.staging_maear.id;


--
-- Name: staging_uniar; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.staging_uniar (
    id bigint NOT NULL,
    importacion_id bigint NOT NULL,
    numero_fila integer NOT NULL,
    uniid character varying(255),
    unipad character varying(255),
    unides character varying(255),
    estado_validacion character varying(30) DEFAULT 'PENDIENTE'::character varying NOT NULL,
    errores jsonb,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: staging_uniar_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.staging_uniar_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: staging_uniar_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.staging_uniar_id_seq OWNED BY public.staging_uniar.id;


--
-- Name: submenu; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.submenu (
    idsubmenu integer NOT NULL,
    idmenu integer NOT NULL,
    nombre character varying(100) NOT NULL,
    controlador character varying(100),
    vista character varying(100),
    icono character varying(50),
    activo boolean DEFAULT true,
    fecharegistro timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: submenu_idsubmenu_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.submenu_idsubmenu_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: submenu_idsubmenu_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.submenu_idsubmenu_seq OWNED BY public.submenu.idsubmenu;


--
-- Name: sync_log; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sync_log (
    id bigint NOT NULL,
    entity character varying(30) NOT NULL,
    direction character varying(10) NOT NULL,
    entity_key character varying(120) NOT NULL,
    status character varying(12) NOT NULL,
    attempts integer NOT NULL,
    message text,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: sync_log_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.sync_log_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: sync_log_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.sync_log_id_seq OWNED BY public.sync_log.id;


--
-- Name: sync_state; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sync_state (
    entity character varying(30) NOT NULL,
    last_date character varying(8),
    last_time character varying(8),
    updated_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: temarios; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.temarios (
    id bigint NOT NULL,
    materia_id bigint NOT NULL,
    codigo_legado character varying(30),
    descripcion text NOT NULL,
    orden integer DEFAULT 1 NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN temarios.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.temarios.codigo_legado IS 'TEMCA1 de TEMAR';


--
-- Name: temarios_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.temarios_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: temarios_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.temarios_id_seq OWNED BY public.temarios.id;


--
-- Name: tipos_curso; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_curso (
    id bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: tipos_curso_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.tipos_curso_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tipos_curso_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.tipos_curso_id_seq OWNED BY public.tipos_curso.id;


--
-- Name: tipos_instruccion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_instruccion (
    id bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: tipos_instruccion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.tipos_instruccion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tipos_instruccion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.tipos_instruccion_id_seq OWNED BY public.tipos_instruccion.id;


--
-- Name: tipos_movimiento; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_movimiento (
    id bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: tipos_movimiento_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.tipos_movimiento_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tipos_movimiento_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.tipos_movimiento_id_seq OWNED BY public.tipos_movimiento.id;


--
-- Name: tipos_relacion_laboral; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_relacion_laboral (
    id bigint NOT NULL,
    nombre character varying(100) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: tipos_relacion_laboral_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.tipos_relacion_laboral_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tipos_relacion_laboral_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.tipos_relacion_laboral_id_seq OWNED BY public.tipos_relacion_laboral.id;


--
-- Name: tipos_unidad; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tipos_unidad (
    id bigint NOT NULL,
    nombre character varying(150) NOT NULL,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: tipos_unidad_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.tipos_unidad_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tipos_unidad_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.tipos_unidad_id_seq OWNED BY public.tipos_unidad.id;


--
-- Name: titulos; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.titulos (
    id bigint NOT NULL,
    nombre character varying(255) NOT NULL,
    nivel_instruccion_id bigint,
    activo boolean DEFAULT true NOT NULL,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: titulos_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.titulos_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: titulos_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.titulos_id_seq OWNED BY public.titulos.id;


--
-- Name: tramites_historialestado; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tramites_historialestado (
    id bigint NOT NULL,
    estado_anterior character varying(50),
    estado_nuevo character varying(50) NOT NULL,
    observacion text,
    fecha_hora timestamp with time zone NOT NULL,
    usuario_id bigint,
    tramite_id bigint NOT NULL
);


--
-- Name: tramites_historialestado_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.tramites_historialestado ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.tramites_historialestado_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tramites_tramite; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.tramites_tramite (
    id bigint NOT NULL,
    numero_tramite character varying(50) NOT NULL,
    tipo_tramite character varying(50) NOT NULL,
    estado character varying(50) NOT NULL,
    fecha_creacion timestamp with time zone NOT NULL,
    fecha_actualizacion timestamp with time zone NOT NULL,
    creado_por_id bigint NOT NULL,
    operador_id bigint NOT NULL,
    responsable_actual_id bigint
);


--
-- Name: tramites_tramite_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.tramites_tramite ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.tramites_tramite_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: unidades_administrativas; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.unidades_administrativas (
    id bigint NOT NULL,
    institucion_id bigint NOT NULL,
    tipo_unidad_id bigint,
    codigo_legado character varying(20),
    nombre character varying(150) NOT NULL,
    unidad_padre_id bigint,
    gestion_id bigint,
    activo boolean DEFAULT true NOT NULL,
    tabla_origen character varying(50) DEFAULT 'UNIAR'::character varying NOT NULL,
    fecha_importacion timestamp(0) without time zone,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    deleted_at timestamp(0) without time zone
);


--
-- Name: COLUMN unidades_administrativas.codigo_legado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.unidades_administrativas.codigo_legado IS 'UNIOID de UNIAR';


--
-- Name: unidades_administrativas_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.unidades_administrativas_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: unidades_administrativas_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.unidades_administrativas_id_seq OWNED BY public.unidades_administrativas.id;


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    id bigint NOT NULL,
    name character varying(255) NOT NULL,
    email character varying(255) NOT NULL,
    email_verified_at timestamp(0) without time zone,
    password character varying(255) NOT NULL,
    remember_token character varying(100),
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone,
    activo boolean DEFAULT true NOT NULL
);


--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.users_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- Name: usuario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario (
    idusuario integer NOT NULL,
    codigousuario character varying(50),
    nombreusuario character varying(200),
    apellidousuario character varying(200),
    tipoidentificacion character varying(50),
    cedulaidentificacion character varying(50),
    correo character varying(200),
    clave text,
    estadoactividad character varying(50),
    tipoaplicacion character varying(50),
    identificaciontributaria character varying(50),
    numeroruc character varying(50),
    codigociudad character varying(50),
    codigodependencia character varying(50),
    codigosubsistema character varying(50),
    codigogestion character varying(50),
    codigomodulo character varying(50),
    codigorol integer,
    centrocontable character varying(50),
    usuariocreado character varying(100),
    fechacreado character varying(50),
    horacreado character varying(50),
    dispositivocreacion character varying(100),
    usuariomodificado character varying(100),
    fechamodificado character varying(50),
    horamodificado character varying(50),
    dispositivomodificacion character varying(100),
    descripcionsubsistema character varying(200),
    horainicio character varying(50),
    horafin character varying(50),
    fechasistema character varying(50),
    tipohorario character varying(50),
    numeroaleatorio character varying(50),
    nombrecorto character varying(100),
    cargo character varying(100),
    estadocertificado character varying(50),
    estadoautoridad character varying(50),
    rol character varying(50),
    fechaultimaconexion timestamp without time zone,
    empresa_codigo character varying(5),
    ruta_documento_legal character varying(255),
    estado_designacion_rt character varying(20) DEFAULT 'pendiente'::character varying,
    fecha_revision_designacion timestamp without time zone,
    ruta_constancia_rt character varying(255),
    titulo1 character varying(6),
    titulo2 character varying(6),
    telefono1 character varying(20),
    telefono2 character varying(20),
    correo_adicional character varying(60),
    oid_centro_contable numeric(10,0),
    codigo_ciudad_adic character varying(4),
    codigorol_as400 character varying(4),
    must_change_password boolean DEFAULT false,
    password_changed_at timestamp without time zone,
    correo_original character varying(255),
    correo_liberado boolean DEFAULT false NOT NULL,
    fecha_devolucion_designacion timestamp without time zone,
    coordinador_devolucion_id integer,
    observacion_devolucion text
);


--
-- Name: COLUMN usuario.correo_original; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.usuario.correo_original IS 'Resguardo del correo original cuando una postulación RT es devuelta';


--
-- Name: COLUMN usuario.correo_liberado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.usuario.correo_liberado IS 'Indica si el correo fue liberado por devolución de la designación provisional';


--
-- Name: COLUMN usuario.fecha_devolucion_designacion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.usuario.fecha_devolucion_designacion IS 'Fecha/hora en la que la designación fue devuelta por Coordinación';


--
-- Name: COLUMN usuario.coordinador_devolucion_id; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.usuario.coordinador_devolucion_id IS 'ID del usuario Coordinador que devolvió la postulación';


--
-- Name: COLUMN usuario.observacion_devolucion; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON COLUMN public.usuario.observacion_devolucion IS 'Observación y justificación técnica de la devolución ingresada por Coordinación';


--
-- Name: usuario_as400; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario_as400 (
    usucod character varying(10) NOT NULL,
    usunom character varying(40),
    usuape character varying(40),
    usutip character varying(2),
    usuced character varying(10),
    usucor character varying(100),
    usucla character varying(256),
    usuest character varying(2),
    usuti1 character varying(4),
    usuide character varying(3),
    usunum character varying(20),
    usuco1 character varying(4),
    usuco2 character varying(4),
    usuco3 character varying(4),
    usuco4 character varying(4),
    usuco5 character varying(4),
    usuco6 character varying(4),
    usuusu character varying(10),
    usufec character varying(8),
    usuhor character varying(8),
    usudis character varying(15),
    usuus1 character varying(10),
    usufe1 character varying(8),
    usuho1 character varying(8),
    usudi1 character varying(15),
    last_sync_at timestamp without time zone,
    last_sync_hash character varying(64)
);


--
-- Name: usuario_as400_adicional; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario_as400_adicional (
    usuco8 character varying(10) NOT NULL,
    usutit character varying(6),
    usuti2 character varying(6),
    usuno1 character varying(60),
    usucar character varying(60),
    usunu1 character varying(20),
    usunu2 character varying(20),
    usuco7 character varying(60),
    usuoid numeric(10,0),
    usuco9 character varying(4),
    last_sync_at timestamp without time zone,
    last_sync_hash character varying(64)
);


--
-- Name: usuario_backup_eliminados; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario_backup_eliminados (
    idusuario integer,
    codigousuario character varying(50),
    nombreusuario character varying(200),
    apellidousuario character varying(200),
    tipoidentificacion character varying(50),
    cedulaidentificacion character varying(50),
    correo character varying(200),
    clave character varying(200),
    estadoactividad character varying(50),
    tipoaplicacion character varying(50),
    identificaciontributaria character varying(50),
    numeroruc character varying(50),
    codigociudad character varying(50),
    codigodependencia character varying(50),
    codigosubsistema character varying(50),
    codigogestion character varying(50),
    codigomodulo character varying(50),
    codigorol integer,
    centrocontable character varying(50),
    usuariocreado character varying(100),
    fechacreado character varying(50),
    horacreado character varying(50),
    dispositivocreacion character varying(100),
    usuariomodificado character varying(100),
    fechamodificado character varying(50),
    horamodificado character varying(50),
    dispositivomodificacion character varying(100),
    descripcionsubsistema character varying(200),
    horainicio character varying(50),
    horafin character varying(50),
    fechasistema character varying(50),
    tipohorario character varying(50),
    numeroaleatorio character varying(50),
    nombrecorto character varying(100),
    cargo character varying(100),
    estadocertificado character varying(50),
    estadoautoridad character varying(50),
    rol character varying(50),
    fechaultimaconexion timestamp without time zone,
    empresa_codigo character varying(5),
    ruta_documento_legal character varying(255),
    estado_designacion_rt character varying(20),
    fecha_revision_designacion timestamp without time zone,
    ruta_constancia_rt character varying(255),
    titulo1 character varying(6),
    titulo2 character varying(6),
    telefono1 character varying(20),
    telefono2 character varying(20),
    correo_adicional character varying(60),
    oid_centro_contable numeric(10,0),
    codigo_ciudad_adic character varying(4),
    codigorol_as400 character varying(4)
);


--
-- Name: usuario_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: usuario_idusuario_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.usuario_idusuario_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: usuario_idusuario_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.usuario_idusuario_seq OWNED BY public.usuario.idusuario;


--
-- Name: usuario_rol; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuario_rol (
    idusuariorol integer NOT NULL,
    codigousuario character varying(50) NOT NULL,
    codigorol integer NOT NULL,
    fechaasignacion timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    usuariocreado character varying(100),
    activo boolean DEFAULT true
);


--
-- Name: usuario_rol_idusuariorol_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.usuario_rol_idusuariorol_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: usuario_rol_idusuariorol_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.usuario_rol_idusuariorol_seq OWNED BY public.usuario_rol.idusuariorol;


--
-- Name: usuarios_usuario; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuarios_usuario (
    id bigint NOT NULL,
    password character varying(128) NOT NULL,
    last_login timestamp with time zone,
    is_superuser boolean NOT NULL,
    username character varying(150) NOT NULL,
    first_name character varying(150) NOT NULL,
    last_name character varying(150) NOT NULL,
    email character varying(254) NOT NULL,
    is_staff boolean NOT NULL,
    is_active boolean NOT NULL,
    date_joined timestamp with time zone NOT NULL,
    rol character varying(30) NOT NULL,
    telefono character varying(30)
);


--
-- Name: usuarios_usuario_groups; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuarios_usuario_groups (
    id bigint NOT NULL,
    usuario_id bigint NOT NULL,
    group_id integer NOT NULL
);


--
-- Name: usuarios_usuario_groups_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuarios_usuario_groups ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.usuarios_usuario_groups_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuarios_usuario_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuarios_usuario ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.usuarios_usuario_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: usuarios_usuario_user_permissions; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.usuarios_usuario_user_permissions (
    id bigint NOT NULL,
    usuario_id bigint NOT NULL,
    permission_id integer NOT NULL
);


--
-- Name: usuarios_usuario_user_permissions_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.usuarios_usuario_user_permissions ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.usuarios_usuario_user_permissions_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: v_usuario_as400_adicional; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.v_usuario_as400_adicional AS
 SELECT usuco8 AS usucod,
    usutit,
    usuti2,
    usuno1,
    usucar,
    usunu1,
    usunu2,
    usuco7,
    usuoid,
    usuco9,
    last_sync_at,
    last_sync_hash
   FROM public.usuario_as400_adicional;


--
-- Name: validaciones_capacitacion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.validaciones_capacitacion (
    id bigint NOT NULL,
    capacitacion_id bigint NOT NULL,
    user_id_validador bigint NOT NULL,
    estado_capacitacion_id bigint NOT NULL,
    comentarios text,
    created_at timestamp(0) without time zone,
    updated_at timestamp(0) without time zone
);


--
-- Name: validaciones_capacitacion_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.validaciones_capacitacion_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: validaciones_capacitacion_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.validaciones_capacitacion_id_seq OWNED BY public.validaciones_capacitacion.id;


--
-- Name: vista_usuarios_con_roles; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vista_usuarios_con_roles AS
 SELECT u.codigousuario,
    u.nombreusuario,
    u.apellidousuario,
    u.correo,
    r.descripcion AS rol,
    ur.fechaasignacion
   FROM ((public.usuario u
     JOIN public.usuario_rol ur ON (((u.codigousuario)::text = (ur.codigousuario)::text)))
     JOIN public.rol r ON ((ur.codigorol = r.codigorol)))
  WHERE (ur.activo = true);


--
-- Name: vw_aocr_resumen_financiero_orden; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_aocr_resumen_financiero_orden AS
 WITH orden AS (
         SELECT o_1.id AS orden_recaudacion_id,
            o_1.numero_orden,
            o_1.estado,
                CASE
                    WHEN (TRIM(BOTH FROM COALESCE((o_1.codigo_solicitud)::text, ''::text)) ~ '^[0-9]+$'::text) THEN (TRIM(BOTH FROM (o_1.codigo_solicitud)::text))::integer
                    ELSE NULL::integer
                END AS codigo_solicitud,
            COALESCE(o_1.total, (0)::numeric) AS total_cabecera
           FROM public.aocr_or_orden o_1
        ), detalle AS (
         SELECT d_1.orden_id AS orden_recaudacion_id,
            COALESCE(sum(
                CASE
                    WHEN (COALESCE(d_1.total_linea, (0)::numeric) > (0)::numeric) THEN d_1.total_linea
                    ELSE (COALESCE(d_1.subtotal, (0)::numeric) + COALESCE(d_1.admin, (0)::numeric))
                END), (0)::numeric) AS total_detalle
           FROM public.aocr_or_orden_detalle d_1
          GROUP BY d_1.orden_id
        ), pagos AS (
         SELECT o_1.orden_recaudacion_id,
            COALESCE(sum(p_1.monto), (0)::numeric) AS total_pagado_aprobado
           FROM (orden o_1
             LEFT JOIN public.aocr_tbpago p_1 ON ((((p_1.codigo_solicitud = o_1.orden_recaudacion_id) OR (p_1.codigo_solicitud = COALESCE(o_1.codigo_solicitud, 0))) AND (upper(TRIM(BOTH FROM COALESCE(p_1.estado, ''::character varying))) = ANY (ARRAY['APROBADO'::text, 'VALIDADO'::text, 'APROBADO_FINANCIERO'::text])) AND (upper(TRIM(BOTH FROM COALESCE(o_1.estado, ''::character varying))) = ANY (ARRAY['FACTURADA'::text, 'COMPLETADA'::text, 'PAGADA'::text, 'ORDEN_CERRADA_POR_SOLICITUD'::text, 'ORDEN_INACTIVA'::text])))))
          GROUP BY o_1.orden_recaudacion_id
        )
 SELECT o.orden_recaudacion_id,
    o.numero_orden,
    round(
        CASE
            WHEN (COALESCE(d.total_detalle, (0)::numeric) > (0)::numeric) THEN d.total_detalle
            ELSE o.total_cabecera
        END, 2) AS total_orden,
    round(COALESCE(p.total_pagado_aprobado, (0)::numeric), 2) AS total_pagado_aprobado,
    round(GREATEST((
        CASE
            WHEN (COALESCE(d.total_detalle, (0)::numeric) > (0)::numeric) THEN d.total_detalle
            ELSE o.total_cabecera
        END - COALESCE(p.total_pagado_aprobado, (0)::numeric)), (0)::numeric), 2) AS saldo_pendiente
   FROM ((orden o
     LEFT JOIN detalle d ON ((d.orden_recaudacion_id = o.orden_recaudacion_id)))
     LEFT JOIN pagos p ON ((p.orden_recaudacion_id = o.orden_recaudacion_id)));


--
-- Name: vw_inspecciones_por_estado; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_inspecciones_por_estado AS
 SELECT estado,
    count(*) AS total,
    count(*) FILTER (WHERE (fecha_programada IS NOT NULL)) AS con_fecha,
    count(*) FILTER (WHERE (informe_pdf IS NOT NULL)) AS con_informe,
    max(updated_at) AS ultima_actualizacion
   FROM public.aocr_tbinspeccion
  WHERE (estado IS NOT NULL)
  GROUP BY estado
  ORDER BY
        CASE estado
            WHEN 'CREADA'::text THEN 1
            WHEN 'PROGRAMADA'::text THEN 2
            WHEN 'EN_CURSO'::text THEN 3
            WHEN 'APLAZADA'::text THEN 4
            WHEN 'FINALIZADA'::text THEN 5
            WHEN 'APROBADA'::text THEN 6
            WHEN 'RECHAZADA'::text THEN 7
            WHEN 'CANCELADA'::text THEN 8
            WHEN 'CERRADA'::text THEN 9
            ELSE 99
        END;


--
-- Name: VIEW vw_inspecciones_por_estado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON VIEW public.vw_inspecciones_por_estado IS 'Vista de resumen con contadores de inspecciones agrupadas por estado para dashboard';


--
-- Name: vw_subsanaciones_pendientes; Type: VIEW; Schema: public; Owner: -
--

CREATE VIEW public.vw_subsanaciones_pendientes AS
 SELECT s.codigo_subsanacion,
    s.codigo_solicitud,
    sol.numero_solicitud,
    sol.nombre_operador,
    s.fecha_solicitud,
    s.observaciones,
    COALESCE((((u_sol.nombreusuario)::text || ' '::text) || (u_sol.apellidousuario)::text), 'N/A'::text) AS tecnico_solicitante,
        CASE
            WHEN (s.fecha_respuesta IS NULL) THEN (EXTRACT(day FROM (CURRENT_TIMESTAMP - (s.fecha_solicitud)::timestamp with time zone)))::integer
            ELSE 0
        END AS dias_pendiente,
    s.estado
   FROM ((public.aocr_tbsubsanacion s
     JOIN public.aocr_tbsolicitud sol ON ((s.codigo_solicitud = sol.codigo_solicitud)))
     LEFT JOIN public.usuario u_sol ON ((s.codigo_usuario_solicitante = u_sol.idusuario)))
  WHERE ((s.estado)::text = 'PENDIENTE'::text)
  ORDER BY s.fecha_solicitud;


--
-- Name: VIEW vw_subsanaciones_pendientes; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON VIEW public.vw_subsanaciones_pendientes IS 'Vista r\u00e1pida de subsanaciones pendientes con d\u00edas transcurridos';


--
-- Name: batch_log; Type: TABLE; Schema: sync; Owner: -
--

CREATE TABLE sync.batch_log (
    batch_id uuid NOT NULL,
    started_at timestamp without time zone NOT NULL,
    ended_at timestamp without time zone,
    table_name text NOT NULL,
    rows_read integer DEFAULT 0 NOT NULL,
    rows_applied integer DEFAULT 0 NOT NULL,
    rows_rejected integer DEFAULT 0 NOT NULL,
    rows_deleted integer DEFAULT 0 NOT NULL,
    latency_ms bigint,
    status text DEFAULT 'RUNNING'::character varying NOT NULL,
    error text,
    finished_at timestamp without time zone,
    message text,
    read_count integer DEFAULT 0 NOT NULL,
    applied_count integer DEFAULT 0 NOT NULL,
    rejected_count integer DEFAULT 0 NOT NULL,
    deleted_count integer DEFAULT 0 NOT NULL,
    error_count integer DEFAULT 0 NOT NULL,
    duration_ms integer
);


--
-- Name: TABLE batch_log; Type: COMMENT; Schema: sync; Owner: -
--

COMMENT ON TABLE sync.batch_log IS 'Bitacora por lote de sincronizacion';


--
-- Name: rejections; Type: TABLE; Schema: sync; Owner: -
--

CREATE TABLE sync.rejections (
    id bigint NOT NULL,
    batch_id uuid NOT NULL,
    table_name text NOT NULL,
    payload jsonb NOT NULL,
    error text NOT NULL,
    created_at timestamp without time zone DEFAULT now() NOT NULL
);


--
-- Name: TABLE rejections; Type: COMMENT; Schema: sync; Owner: -
--

COMMENT ON TABLE sync.rejections IS 'Filas rechazadas al aplicar espejo';


--
-- Name: rejections_id_seq; Type: SEQUENCE; Schema: sync; Owner: -
--

CREATE SEQUENCE sync.rejections_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: rejections_id_seq; Type: SEQUENCE OWNED BY; Schema: sync; Owner: -
--

ALTER SEQUENCE sync.rejections_id_seq OWNED BY sync.rejections.id;


--
-- Name: tombstones; Type: TABLE; Schema: sync; Owner: -
--

CREATE TABLE sync.tombstones (
    id bigint NOT NULL,
    table_name text NOT NULL,
    pk_payload jsonb NOT NULL,
    source_deleted_at timestamp without time zone,
    source_reference text,
    batch_id uuid,
    applied boolean DEFAULT false NOT NULL,
    applied_at timestamp without time zone,
    created_at timestamp without time zone DEFAULT now() NOT NULL,
    error text
);


--
-- Name: TABLE tombstones; Type: COMMENT; Schema: sync; Owner: -
--

COMMENT ON TABLE sync.tombstones IS 'Deletes fisicos detectados/inyectados para aplicar por PK';


--
-- Name: tombstones_id_seq; Type: SEQUENCE; Schema: sync; Owner: -
--

CREATE SEQUENCE sync.tombstones_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: tombstones_id_seq; Type: SEQUENCE OWNED BY; Schema: sync; Owner: -
--

ALTER SEQUENCE sync.tombstones_id_seq OWNED BY sync.tombstones.id;


--
-- Name: v_batch_log_actual_por_tabla; Type: VIEW; Schema: sync; Owner: -
--

CREATE VIEW sync.v_batch_log_actual_por_tabla AS
 SELECT DISTINCT ON (table_name) table_name,
    status,
    read_count,
    applied_count,
    rejected_count,
    error_count,
    message,
    started_at,
    finished_at,
        CASE
            WHEN (message IS NULL) THEN NULL::text
            WHEN (length(message) > 300) THEN (SUBSTRING(message FROM 1 FOR 300) || '...'::text)
            ELSE message
        END AS message_short
   FROM sync.batch_log
  ORDER BY table_name, started_at DESC, finished_at DESC NULLS LAST;


--
-- Name: v_batch_log_ultimo_por_tabla; Type: VIEW; Schema: sync; Owner: -
--

CREATE VIEW sync.v_batch_log_ultimo_por_tabla AS
 WITH ultimo AS (
         SELECT b.batch_id,
            b.started_at,
            b.ended_at,
            b.table_name,
            b.rows_read,
            b.rows_applied,
            b.rows_rejected,
            b.rows_deleted,
            b.latency_ms,
            b.status,
            b.error,
            b.finished_at,
            b.message,
            b.read_count,
            b.applied_count,
            b.rejected_count,
            b.deleted_count,
            b.error_count,
            b.duration_ms,
            row_number() OVER (PARTITION BY (upper(btrim(b.table_name))) ORDER BY b.started_at DESC, COALESCE(b.ended_at, b.finished_at, b.started_at) DESC) AS rn
           FROM sync.batch_log b
          WHERE (b.table_name IS NOT NULL)
        )
 SELECT table_name,
    status,
        CASE
            WHEN (COALESCE(rows_read, 0) > 0) THEN rows_read
            ELSE COALESCE(read_count, 0)
        END AS read_count,
        CASE
            WHEN (COALESCE(rows_applied, 0) > 0) THEN rows_applied
            ELSE COALESCE(applied_count, 0)
        END AS applied_count,
        CASE
            WHEN (COALESCE(rows_rejected, 0) > 0) THEN rows_rejected
            ELSE COALESCE(rejected_count, 0)
        END AS rejected_count,
        CASE
            WHEN (COALESCE(rows_deleted, 0) > 0) THEN rows_deleted
            ELSE COALESCE(deleted_count, 0)
        END AS deleted_count,
    COALESCE(error_count, 0) AS error_count,
        CASE
            WHEN (COALESCE(duration_ms, 0) > 0) THEN duration_ms
            ELSE COALESCE((latency_ms)::integer, 0)
        END AS duration_ms,
        CASE
            WHEN (status = 'OK'::text) THEN NULL::text
            WHEN (message IS NULL) THEN NULL::text
            WHEN (length(message) > 300) THEN (SUBSTRING(message FROM 1 FOR 300) || '...'::text)
            ELSE message
        END AS message,
    started_at,
    COALESCE(ended_at, finished_at) AS finished_at
   FROM ultimo
  WHERE (rn = 1);


--
-- Name: watermark; Type: TABLE; Schema: sync; Owner: -
--

CREATE TABLE sync.watermark (
    table_name text NOT NULL,
    last_success_ts timestamp without time zone,
    last_success_key text,
    last_batch_id uuid,
    status text DEFAULT 'OK'::character varying NOT NULL,
    last_error text,
    updated_at timestamp without time zone DEFAULT now() NOT NULL,
    last_key text,
    last_sync_at timestamp without time zone,
    last_status text,
    last_message text
);


--
-- Name: TABLE watermark; Type: COMMENT; Schema: sync; Owner: -
--

COMMENT ON TABLE sync.watermark IS 'Estado incremental por tabla (watermark)';


--
-- Name: _tombstone id; Type: DEFAULT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw._tombstone ALTER COLUMN id SET DEFAULT nextval('mirror_raw._tombstone_id_seq'::regclass);


--
-- Name: sync_state id; Type: DEFAULT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.sync_state ALTER COLUMN id SET DEFAULT nextval('mirror_raw.sync_state_id_seq'::regclass);


--
-- Name: acciones_personal id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.acciones_personal ALTER COLUMN id SET DEFAULT nextval('public.acciones_personal_id_seq'::regclass);


--
-- Name: aocr_asignacion_rt id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_asignacion_rt ALTER COLUMN id SET DEFAULT nextval('public.aocr_asignacion_rt_id_seq'::regclass);


--
-- Name: aocr_audit_trail id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_audit_trail ALTER COLUMN id SET DEFAULT nextval('public.aocr_audit_trail_id_seq'::regclass);


--
-- Name: aocr_correlativo_anual id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_correlativo_anual ALTER COLUMN id SET DEFAULT nextval('public.aocr_correlativo_anual_id_seq'::regclass);


--
-- Name: aocr_declaracion_historial id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_declaracion_historial ALTER COLUMN id SET DEFAULT nextval('public.aocr_declaracion_historial_id_seq'::regclass);


--
-- Name: aocr_declaracion_tmp id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_declaracion_tmp ALTER COLUMN id SET DEFAULT nextval('public.aocr_declaracion_tmp_id_seq'::regclass);


--
-- Name: aocr_entrega_destinatario id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_destinatario ALTER COLUMN id SET DEFAULT nextval('public.aocr_entrega_destinatario_id_seq'::regclass);


--
-- Name: aocr_entrega_documento id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_documento ALTER COLUMN id SET DEFAULT nextval('public.aocr_entrega_documento_id_seq'::regclass);


--
-- Name: aocr_entrega_final id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_final ALTER COLUMN id SET DEFAULT nextval('public.aocr_entrega_final_id_seq'::regclass);


--
-- Name: aocr_entrega_intento id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_intento ALTER COLUMN id SET DEFAULT nextval('public.aocr_entrega_intento_id_seq'::regclass);


--
-- Name: aocr_evento_workflow id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_evento_workflow ALTER COLUMN id SET DEFAULT nextval('public.aocr_evento_workflow_id_seq'::regclass);


--
-- Name: aocr_expediente_archivo id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_archivo ALTER COLUMN id SET DEFAULT nextval('public.aocr_expediente_archivo_id_seq'::regclass);


--
-- Name: aocr_expediente_compania id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_compania ALTER COLUMN id SET DEFAULT nextval('public.aocr_expediente_compania_id_seq'::regclass);


--
-- Name: aocr_fr3_integracion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_integracion ALTER COLUMN id SET DEFAULT nextval('public.aocr_fr3_integracion_id_seq'::regclass);


--
-- Name: aocr_fr3_operacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_operacion ALTER COLUMN id SET DEFAULT nextval('public.aocr_fr3_operacion_id_seq'::regclass);


--
-- Name: aocr_fr3_outbox id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_outbox ALTER COLUMN id SET DEFAULT nextval('public.aocr_fr3_outbox_id_seq'::regclass);


--
-- Name: aocr_fr3_retry_queue id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_retry_queue ALTER COLUMN id SET DEFAULT nextval('public.aocr_fr3_retry_queue_id_seq'::regclass);


--
-- Name: aocr_fr3_tipo_operacion_map id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_tipo_operacion_map ALTER COLUMN id SET DEFAULT nextval('public.aocr_fr3_tipo_operacion_map_id_seq'::regclass);


--
-- Name: aocr_idempotency_key id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_idempotency_key ALTER COLUMN id SET DEFAULT nextval('public.aocr_idempotency_key_id_seq'::regclass);


--
-- Name: aocr_inspector_reasignacion_historial id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_inspector_reasignacion_historial ALTER COLUMN id SET DEFAULT nextval('public.aocr_inspector_reasignacion_historial_id_seq'::regclass);


--
-- Name: aocr_or_correccion_financiera id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_correccion_financiera ALTER COLUMN id SET DEFAULT nextval('public.aocr_or_correccion_financiera_id_seq'::regclass);


--
-- Name: aocr_orden_recaudacion_detalle id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_orden_recaudacion_detalle ALTER COLUMN id SET DEFAULT nextval('public.aocr_orden_recaudacion_detalle_id_seq'::regclass);


--
-- Name: aocr_pago_comprobante id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_pago_comprobante ALTER COLUMN id SET DEFAULT nextval('public.aocr_pago_comprobante_id_seq'::regclass);


--
-- Name: aocr_proceso_estado id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_proceso_estado ALTER COLUMN id SET DEFAULT nextval('public.aocr_proceso_estado_id_seq'::regclass);


--
-- Name: aocr_proceso_estado_historial id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_proceso_estado_historial ALTER COLUMN id SET DEFAULT nextval('public.aocr_proceso_estado_historial_id_seq'::regclass);


--
-- Name: aocr_revision_documental_coordinador id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_revision_documental_coordinador ALTER COLUMN id SET DEFAULT nextval('public.aocr_revision_documental_coordinador_id_seq'::regclass);


--
-- Name: aocr_solicitud_rt_revision id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_solicitud_rt_revision ALTER COLUMN id SET DEFAULT nextval('public.aocr_solicitud_rt_revision_id_seq'::regclass);


--
-- Name: aocr_sync_log id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_sync_log ALTER COLUMN id SET DEFAULT nextval('public.aocr_sync_log_id_seq'::regclass);


--
-- Name: aocr_tb_factura_pago id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_factura_pago ALTER COLUMN id SET DEFAULT nextval('public.aocr_tb_factura_pago_id_seq'::regclass);


--
-- Name: aocr_tb_sync_log id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_sync_log ALTER COLUMN id SET DEFAULT nextval('public.aocr_tb_sync_log_id_seq'::regclass);


--
-- Name: aocr_tbaeronave codigo_aeronave; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbaeronave ALTER COLUMN codigo_aeronave SET DEFAULT nextval('public.aocr_tbaeronave_codigo_aeronave_seq'::regclass);


--
-- Name: aocr_tbaeronave_solicitud codigo_aeronave_solicitud; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbaeronave_solicitud ALTER COLUMN codigo_aeronave_solicitud SET DEFAULT nextval('public.aocr_tbaeronave_solicitud_codigo_aeronave_solicitud_seq'::regclass);


--
-- Name: aocr_tbauditoria codigo_auditoria; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbauditoria ALTER COLUMN codigo_auditoria SET DEFAULT nextval('public.aocr_tbauditoria_codigo_auditoria_seq'::regclass);


--
-- Name: aocr_tbcertificado codigo_certificado; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcertificado ALTER COLUMN codigo_certificado SET DEFAULT nextval('public.aocr_tbcertificado_codigo_certificado_seq'::regclass);


--
-- Name: aocr_tbchecklist codigo_checklist; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist ALTER COLUMN codigo_checklist SET DEFAULT nextval('public.aocr_tbchecklist_codigo_checklist_seq'::regclass);


--
-- Name: aocr_tbchecklist_item codigo_item; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist_item ALTER COLUMN codigo_item SET DEFAULT nextval('public.aocr_tbchecklist_item_codigo_item_seq'::regclass);


--
-- Name: aocr_tbchecklist_solicitud codigo_checklist_solicitud; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist_solicitud ALTER COLUMN codigo_checklist_solicitud SET DEFAULT nextval('public.aocr_tbchecklist_solicitud_codigo_checklist_solicitud_seq'::regclass);


--
-- Name: aocr_tbcondiciones_limitaciones id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcondiciones_limitaciones ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbcondiciones_limitaciones_id_seq'::regclass);


--
-- Name: aocr_tbcorreo_institucional codigo_correo; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcorreo_institucional ALTER COLUMN codigo_correo SET DEFAULT nextval('public.aocr_tbcorreo_institucional_codigo_correo_seq'::regclass);


--
-- Name: aocr_tbcorreo_institucional_historial codigo_historial; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcorreo_institucional_historial ALTER COLUMN codigo_historial SET DEFAULT nextval('public.aocr_tbcorreo_institucional_historial_codigo_historial_seq'::regclass);


--
-- Name: aocr_tbdesignacion_inspector id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdesignacion_inspector ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbdesignacion_inspector_id_seq'::regclass);


--
-- Name: aocr_tbdocumento codigo_documento; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento ALTER COLUMN codigo_documento SET DEFAULT nextval('public.aocr_tbdocumento_codigo_documento_seq'::regclass);


--
-- Name: aocr_tbdocumento_generado codigo_documento; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_generado ALTER COLUMN codigo_documento SET DEFAULT nextval('public.aocr_tbdocumento_generado_codigo_documento_seq'::regclass);


--
-- Name: aocr_tbdocumento_inspeccion codigo_documento; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_inspeccion ALTER COLUMN codigo_documento SET DEFAULT nextval('public.aocr_tbdocumento_inspeccion_codigo_documento_seq'::regclass);


--
-- Name: aocr_tbdocumento_subsanacion codigo_documento; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_subsanacion ALTER COLUMN codigo_documento SET DEFAULT nextval('public.aocr_tbdocumento_subsanacion_codigo_documento_seq'::regclass);


--
-- Name: aocr_tbfirma_documento codigo_firma; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbfirma_documento ALTER COLUMN codigo_firma SET DEFAULT nextval('public.aocr_tbfirma_documento_codigo_firma_seq'::regclass);


--
-- Name: aocr_tbfirma_posicion_documento codigo_posicion_firma; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbfirma_posicion_documento ALTER COLUMN codigo_posicion_firma SET DEFAULT nextval('public.aocr_tbfirma_posicion_documento_codigo_posicion_firma_seq'::regclass);


--
-- Name: aocr_tbhallazgo codigo_hallazgo; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhallazgo ALTER COLUMN codigo_hallazgo SET DEFAULT nextval('public.aocr_tbhallazgo_codigo_hallazgo_seq'::regclass);


--
-- Name: aocr_tbhistorial_documental id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_documental ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbhistorial_documental_id_seq'::regclass);


--
-- Name: aocr_tbhistorial_estado codigo_historial; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_estado ALTER COLUMN codigo_historial SET DEFAULT nextval('public.aocr_tbhistorial_estado_codigo_historial_seq'::regclass);


--
-- Name: aocr_tbhistorial_estado_inspeccion codigo_historial; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_estado_inspeccion ALTER COLUMN codigo_historial SET DEFAULT nextval('public.aocr_tbhistorial_estado_inspeccion_codigo_historial_seq'::regclass);


--
-- Name: aocr_tbinforme codigo_informe; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinforme ALTER COLUMN codigo_informe SET DEFAULT nextval('public.aocr_tbinforme_codigo_informe_seq'::regclass);


--
-- Name: aocr_tbinforme_inspeccion codigo_informe; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinforme_inspeccion ALTER COLUMN codigo_informe SET DEFAULT nextval('public.aocr_tbinforme_inspeccion_codigo_informe_seq'::regclass);


--
-- Name: aocr_tbinspeccion codigo_inspeccion; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinspeccion ALTER COLUMN codigo_inspeccion SET DEFAULT nextval('public.aocr_tbinspeccion_codigo_inspeccion_seq'::regclass);


--
-- Name: aocr_tbinspectores id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinspectores ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbinspectores_id_seq'::regclass);


--
-- Name: aocr_tblog codigo_log; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tblog ALTER COLUMN codigo_log SET DEFAULT nextval('public.aocr_tblog_codigo_log_seq'::regclass);


--
-- Name: aocr_tblv_operacional_eae codigo_lv; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tblv_operacional_eae ALTER COLUMN codigo_lv SET DEFAULT nextval('public.aocr_tblv_operacional_eae_codigo_lv_seq'::regclass);


--
-- Name: aocr_tbnoconformidad codigo_no_conformidad; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnoconformidad ALTER COLUMN codigo_no_conformidad SET DEFAULT nextval('public.aocr_tbnoconformidad_codigo_no_conformidad_seq'::regclass);


--
-- Name: aocr_tbnotificacion codigonotificacion; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnotificacion ALTER COLUMN codigonotificacion SET DEFAULT nextval('public.aocr_tbnotificacion_codigo_notificacion_seq'::regclass);


--
-- Name: aocr_tbobservacion codigo_observacion; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbobservacion ALTER COLUMN codigo_observacion SET DEFAULT nextval('public.aocr_tbobservacion_codigo_observacion_seq'::regclass);


--
-- Name: aocr_tbpago codigo_pago; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbpago ALTER COLUMN codigo_pago SET DEFAULT nextval('public.aocr_tbpago_codigo_pago_seq'::regclass);


--
-- Name: aocr_tbparametro codigo_parametro; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbparametro ALTER COLUMN codigo_parametro SET DEFAULT nextval('public.aocr_tbparametro_codigo_parametro_seq'::regclass);


--
-- Name: aocr_tbrevision_documental id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbrevision_documental ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbrevision_documental_id_seq'::regclass);


--
-- Name: aocr_tbsesiones id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsesiones ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbsesiones_id_seq'::regclass);


--
-- Name: aocr_tbsolicitud codigo_solicitud; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsolicitud ALTER COLUMN codigo_solicitud SET DEFAULT nextval('public.aocr_tbsolicitud_codigo_solicitud_seq'::regclass);


--
-- Name: aocr_tbsolicitud_estacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsolicitud_estacion ALTER COLUMN id SET DEFAULT nextval('public.aocr_tbsolicitud_estacion_id_seq'::regclass);


--
-- Name: aocr_tbsubsanacion codigo_subsanacion; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsubsanacion ALTER COLUMN codigo_subsanacion SET DEFAULT nextval('public.aocr_tbsubsanacion_codigo_subsanacion_seq'::regclass);


--
-- Name: aocr_tbviatico codigo_viatico; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbviatico ALTER COLUMN codigo_viatico SET DEFAULT nextval('public.aocr_tbviatico_codigo_viatico_seq'::regclass);


--
-- Name: aocr_usuario_compania_rt id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_compania_rt ALTER COLUMN id SET DEFAULT nextval('public.aocr_usuario_compania_rt_id_seq'::regclass);


--
-- Name: aocr_usuario_interno_rt id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_interno_rt ALTER COLUMN id SET DEFAULT nextval('public.aocr_usuario_interno_rt_id_seq'::regclass);


--
-- Name: aocr_usuario_transferencia id_transferencia; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_transferencia ALTER COLUMN id_transferencia SET DEFAULT nextval('public.aocr_usuario_transferencia_id_transferencia_seq'::regclass);


--
-- Name: aocr_usuario_transferencia_detalle id_detalle; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_transferencia_detalle ALTER COLUMN id_detalle SET DEFAULT nextval('public.aocr_usuario_transferencia_detalle_id_detalle_seq'::regclass);


--
-- Name: auditoria_seguridad id_auditoria; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditoria_seguridad ALTER COLUMN id_auditoria SET DEFAULT nextval('public.auditoria_seguridad_id_auditoria_seq'::regclass);


--
-- Name: auditorias id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditorias ALTER COLUMN id SET DEFAULT nextval('public.auditorias_id_seq'::regclass);


--
-- Name: capacitaciones id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones ALTER COLUMN id SET DEFAULT nextval('public.capacitaciones_id_seq'::regclass);


--
-- Name: competencias id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.competencias ALTER COLUMN id SET DEFAULT nextval('public.competencias_id_seq'::regclass);


--
-- Name: conceptos id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.conceptos ALTER COLUMN id SET DEFAULT nextval('public.conceptos_id_seq'::regclass);


--
-- Name: contactos_institucionales id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contactos_institucionales ALTER COLUMN id SET DEFAULT nextval('public.contactos_institucionales_id_seq'::regclass);


--
-- Name: contactos_servidor id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contactos_servidor ALTER COLUMN id SET DEFAULT nextval('public.contactos_servidor_id_seq'::regclass);


--
-- Name: contribuyentes id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contribuyentes ALTER COLUMN id SET DEFAULT nextval('public.contribuyentes_id_seq'::regclass);


--
-- Name: cursos id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos ALTER COLUMN id SET DEFAULT nextval('public.cursos_id_seq'::regclass);


--
-- Name: cursos_mandatorios id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos_mandatorios ALTER COLUMN id SET DEFAULT nextval('public.cursos_mandatorios_id_seq'::regclass);


--
-- Name: datos_laborales id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales ALTER COLUMN id SET DEFAULT nextval('public.datos_laborales_id_seq'::regclass);


--
-- Name: detalles_orden id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalles_orden ALTER COLUMN id SET DEFAULT nextval('public.detalles_orden_id_seq'::regclass);


--
-- Name: django_aocr_descarga_documento id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_descarga_documento ALTER COLUMN id SET DEFAULT nextval('public.django_aocr_descarga_documento_id_seq'::regclass);


--
-- Name: django_aocr_documento_generado id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_documento_generado ALTER COLUMN id SET DEFAULT nextval('public.django_aocr_documento_generado_id_seq'::regclass);


--
-- Name: django_aocr_firma_documento id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_firma_documento ALTER COLUMN id SET DEFAULT nextval('public.django_aocr_firma_documento_id_seq'::regclass);


--
-- Name: email_attachment id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_attachment ALTER COLUMN id SET DEFAULT nextval('public.email_attachment_id_seq'::regclass);


--
-- Name: email_queue id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_queue ALTER COLUMN id SET DEFAULT nextval('public.email_queue_id_seq'::regclass);


--
-- Name: errores_importacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.errores_importacion ALTER COLUMN id SET DEFAULT nextval('public.errores_importacion_id_seq'::regclass);


--
-- Name: especialidades id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.especialidades ALTER COLUMN id SET DEFAULT nextval('public.especialidades_id_seq'::regclass);


--
-- Name: estados_capacitacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estados_capacitacion ALTER COLUMN id SET DEFAULT nextval('public.estados_capacitacion_id_seq'::regclass);


--
-- Name: estados_servidor id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estados_servidor ALTER COLUMN id SET DEFAULT nextval('public.estados_servidor_id_seq'::regclass);


--
-- Name: estatutos_organicos id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estatutos_organicos ALTER COLUMN id SET DEFAULT nextval('public.estatutos_organicos_id_seq'::regclass);


--
-- Name: experiencia_profesional id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.experiencia_profesional ALTER COLUMN id SET DEFAULT nextval('public.experiencia_profesional_id_seq'::regclass);


--
-- Name: failed_jobs id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.failed_jobs ALTER COLUMN id SET DEFAULT nextval('public.failed_jobs_id_seq'::regclass);


--
-- Name: fr3 id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3 ALTER COLUMN id SET DEFAULT nextval('public.fr3_id_seq'::regclass);


--
-- Name: fr3_detalle id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_detalle ALTER COLUMN id SET DEFAULT nextval('public.fr3_detalle_id_seq'::regclass);


--
-- Name: fr3_detalle_pg id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_detalle_pg ALTER COLUMN id SET DEFAULT nextval('public.fr3_detalle_pg_id_seq'::regclass);


--
-- Name: fr3_pg id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_pg ALTER COLUMN id SET DEFAULT nextval('public.fr3_pg_id_seq'::regclass);


--
-- Name: gestiones_administrativas id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.gestiones_administrativas ALTER COLUMN id SET DEFAULT nextval('public.gestiones_administrativas_id_seq'::regclass);


--
-- Name: grupos_ocupacionales id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.grupos_ocupacionales ALTER COLUMN id SET DEFAULT nextval('public.grupos_ocupacionales_id_seq'::regclass);


--
-- Name: historial_estados_orden id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.historial_estados_orden ALTER COLUMN id SET DEFAULT nextval('public.historial_estados_orden_id_seq'::regclass);


--
-- Name: historial_laboral id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.historial_laboral ALTER COLUMN id SET DEFAULT nextval('public.historial_laboral_id_seq'::regclass);


--
-- Name: importaciones id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.importaciones ALTER COLUMN id SET DEFAULT nextval('public.importaciones_id_seq'::regclass);


--
-- Name: instituciones id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones ALTER COLUMN id SET DEFAULT nextval('public.instituciones_id_seq'::regclass);


--
-- Name: instituciones_capacitadoras id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones_capacitadoras ALTER COLUMN id SET DEFAULT nextval('public.instituciones_capacitadoras_id_seq'::regclass);


--
-- Name: instituciones_educativas id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones_educativas ALTER COLUMN id SET DEFAULT nextval('public.instituciones_educativas_id_seq'::regclass);


--
-- Name: instrucciones_formales id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instrucciones_formales ALTER COLUMN id SET DEFAULT nextval('public.instrucciones_formales_id_seq'::regclass);


--
-- Name: jobs id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.jobs ALTER COLUMN id SET DEFAULT nextval('public.jobs_id_seq'::regclass);


--
-- Name: lugares_trabajo id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.lugares_trabajo ALTER COLUMN id SET DEFAULT nextval('public.lugares_trabajo_id_seq'::regclass);


--
-- Name: materias id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.materias ALTER COLUMN id SET DEFAULT nextval('public.materias_id_seq'::regclass);


--
-- Name: menu idmenu; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.menu ALTER COLUMN idmenu SET DEFAULT nextval('public.menu_idmenu_seq'::regclass);


--
-- Name: migrations id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.migrations ALTER COLUMN id SET DEFAULT nextval('public.migrations_id_seq'::regclass);


--
-- Name: modalidades_prestacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modalidades_prestacion ALTER COLUMN id SET DEFAULT nextval('public.modalidades_prestacion_id_seq'::regclass);


--
-- Name: niveles_instruccion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.niveles_instruccion ALTER COLUMN id SET DEFAULT nextval('public.niveles_instruccion_id_seq'::regclass);


--
-- Name: niveles_proceso id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.niveles_proceso ALTER COLUMN id SET DEFAULT nextval('public.niveles_proceso_id_seq'::regclass);


--
-- Name: ordenes_recaudacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ordenes_recaudacion ALTER COLUMN id SET DEFAULT nextval('public.ordenes_recaudacion_id_seq'::regclass);


--
-- Name: otros_estudios id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.otros_estudios ALTER COLUMN id SET DEFAULT nextval('public.otros_estudios_id_seq'::regclass);


--
-- Name: pagos id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pagos ALTER COLUMN id SET DEFAULT nextval('public.pagos_id_seq'::regclass);


--
-- Name: parametros id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.parametros ALTER COLUMN id SET DEFAULT nextval('public.parametros_id_seq'::regclass);


--
-- Name: permisos idpermisos; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos ALTER COLUMN idpermisos SET DEFAULT nextval('public.permisos_idpermisos_seq'::regclass);


--
-- Name: permissions id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permissions ALTER COLUMN id SET DEFAULT nextval('public.permissions_id_seq'::regclass);


--
-- Name: personas id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.personas ALTER COLUMN id SET DEFAULT nextval('public.personas_id_seq'::regclass);


--
-- Name: programas_capacitacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.programas_capacitacion ALTER COLUMN id SET DEFAULT nextval('public.programas_capacitacion_id_seq'::regclass);


--
-- Name: puestos_institucionales id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.puestos_institucionales ALTER COLUMN id SET DEFAULT nextval('public.puestos_institucionales_id_seq'::regclass);


--
-- Name: rol codigorol; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rol ALTER COLUMN codigorol SET DEFAULT nextval('public.rol_codigorol_seq'::regclass);


--
-- Name: roles id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles ALTER COLUMN id SET DEFAULT nextval('public.roles_id_seq'::regclass);


--
-- Name: seguridad_permiso id_permiso; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.seguridad_permiso ALTER COLUMN id_permiso SET DEFAULT nextval('public.seguridad_permiso_id_permiso_seq'::regclass);


--
-- Name: servidor_discapacidades id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_discapacidades ALTER COLUMN id SET DEFAULT nextval('public.servidor_discapacidades_id_seq'::regclass);


--
-- Name: servidor_titulos id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_titulos ALTER COLUMN id SET DEFAULT nextval('public.servidor_titulos_id_seq'::regclass);


--
-- Name: servidores id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidores ALTER COLUMN id SET DEFAULT nextval('public.servidores_id_seq'::regclass);


--
-- Name: staging_curar id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_curar ALTER COLUMN id SET DEFAULT nextval('public.staging_curar_id_seq'::regclass);


--
-- Name: staging_maear id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_maear ALTER COLUMN id SET DEFAULT nextval('public.staging_maear_id_seq'::regclass);


--
-- Name: staging_uniar id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_uniar ALTER COLUMN id SET DEFAULT nextval('public.staging_uniar_id_seq'::regclass);


--
-- Name: submenu idsubmenu; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.submenu ALTER COLUMN idsubmenu SET DEFAULT nextval('public.submenu_idsubmenu_seq'::regclass);


--
-- Name: sync_log id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sync_log ALTER COLUMN id SET DEFAULT nextval('public.sync_log_id_seq'::regclass);


--
-- Name: temarios id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.temarios ALTER COLUMN id SET DEFAULT nextval('public.temarios_id_seq'::regclass);


--
-- Name: tipos_curso id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_curso ALTER COLUMN id SET DEFAULT nextval('public.tipos_curso_id_seq'::regclass);


--
-- Name: tipos_instruccion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_instruccion ALTER COLUMN id SET DEFAULT nextval('public.tipos_instruccion_id_seq'::regclass);


--
-- Name: tipos_movimiento id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_movimiento ALTER COLUMN id SET DEFAULT nextval('public.tipos_movimiento_id_seq'::regclass);


--
-- Name: tipos_relacion_laboral id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_relacion_laboral ALTER COLUMN id SET DEFAULT nextval('public.tipos_relacion_laboral_id_seq'::regclass);


--
-- Name: tipos_unidad id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_unidad ALTER COLUMN id SET DEFAULT nextval('public.tipos_unidad_id_seq'::regclass);


--
-- Name: titulos id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.titulos ALTER COLUMN id SET DEFAULT nextval('public.titulos_id_seq'::regclass);


--
-- Name: unidades_administrativas id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unidades_administrativas ALTER COLUMN id SET DEFAULT nextval('public.unidades_administrativas_id_seq'::regclass);


--
-- Name: users id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- Name: usuario idusuario; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario ALTER COLUMN idusuario SET DEFAULT nextval('public.usuario_idusuario_seq'::regclass);


--
-- Name: usuario_rol idusuariorol; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_rol ALTER COLUMN idusuariorol SET DEFAULT nextval('public.usuario_rol_idusuariorol_seq'::regclass);


--
-- Name: validaciones_capacitacion id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.validaciones_capacitacion ALTER COLUMN id SET DEFAULT nextval('public.validaciones_capacitacion_id_seq'::regclass);


--
-- Name: rejections id; Type: DEFAULT; Schema: sync; Owner: -
--

ALTER TABLE ONLY sync.rejections ALTER COLUMN id SET DEFAULT nextval('sync.rejections_id_seq'::regclass);


--
-- Name: tombstones id; Type: DEFAULT; Schema: sync; Owner: -
--

ALTER TABLE ONLY sync.tombstones ALTER COLUMN id SET DEFAULT nextval('sync.tombstones_id_seq'::regclass);


--
-- Name: _tombstone _tombstone_pkey; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw._tombstone
    ADD CONSTRAINT _tombstone_pkey PRIMARY KEY (id);


--
-- Name: ciaarc pk_mirror_ciaarc; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.ciaarc
    ADD CONSTRAINT pk_mirror_ciaarc PRIMARY KEY (ciacod);


--
-- Name: oidar2 pk_mirror_oidar2; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.oidar2
    ADD CONSTRAINT pk_mirror_oidar2 PRIMARY KEY (oidco3, oidoi2);


--
-- Name: opcar5 pk_mirror_opcar5; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.opcar5
    ADD CONSTRAINT pk_mirror_opcar5 PRIMARY KEY (opcsec, opcaer, opcano);


--
-- Name: opcar6 pk_mirror_opcar6; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.opcar6
    ADD CONSTRAINT pk_mirror_opcar6 PRIMARY KEY (opcse2, opcae1, opcan1, opcse1);


--
-- Name: opcarc pk_mirror_opcarc; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.opcarc
    ADD CONSTRAINT pk_mirror_opcarc PRIMARY KEY (opccod);


--
-- Name: opiar2 pk_mirror_opiar2; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.opiar2
    ADD CONSTRAINT pk_mirror_opiar2 PRIMARY KEY (opiced, opitip);


--
-- Name: opsarc pk_mirror_opsarc; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.opsarc
    ADD CONSTRAINT pk_mirror_opsarc PRIMARY KEY (opsaer, opsano);


--
-- Name: opuarc01 pk_mirror_opuarc01; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.opuarc01
    ADD CONSTRAINT pk_mirror_opuarc01 PRIMARY KEY (opucod);


--
-- Name: txdgac pk_mirror_txdgac; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.txdgac
    ADD CONSTRAINT pk_mirror_txdgac PRIMARY KEY (valdds, valval);


--
-- Name: usuar1 pk_mirror_usuar1; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.usuar1
    ADD CONSTRAINT pk_mirror_usuar1 PRIMARY KEY (usuco8);


--
-- Name: usuarc pk_mirror_usuarc; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.usuarc
    ADD CONSTRAINT pk_mirror_usuarc PRIMARY KEY (usucod);


--
-- Name: sync_state sync_state_pkey; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.sync_state
    ADD CONSTRAINT sync_state_pkey PRIMARY KEY (id);


--
-- Name: sync_state uq_sync_state_table; Type: CONSTRAINT; Schema: mirror_raw; Owner: -
--

ALTER TABLE ONLY mirror_raw.sync_state
    ADD CONSTRAINT uq_sync_state_table UNIQUE (table_name);


--
-- Name: acciones_personal acciones_personal_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.acciones_personal
    ADD CONSTRAINT acciones_personal_pkey PRIMARY KEY (id);


--
-- Name: accounts_compania accounts_compania_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_compania
    ADD CONSTRAINT accounts_compania_pkey PRIMARY KEY (id);


--
-- Name: accounts_compania accounts_compania_ruc_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_compania
    ADD CONSTRAINT accounts_compania_ruc_key UNIQUE (ruc);


--
-- Name: accounts_perfil_usuario accounts_perfil_usuario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_perfil_usuario
    ADD CONSTRAINT accounts_perfil_usuario_pkey PRIMARY KEY (id);


--
-- Name: accounts_perfil_usuario accounts_perfil_usuario_user_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_perfil_usuario
    ADD CONSTRAINT accounts_perfil_usuario_user_id_key UNIQUE (user_id);


--
-- Name: accounts_sesion_compania_activa accounts_sesion_compania_activa_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_sesion_compania_activa
    ADD CONSTRAINT accounts_sesion_compania_activa_pkey PRIMARY KEY (id);


--
-- Name: accounts_sesion_compania_activa accounts_sesion_compania_activa_user_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_sesion_compania_activa
    ADD CONSTRAINT accounts_sesion_compania_activa_user_id_key UNIQUE (user_id);


--
-- Name: accounts_usuario_compania accounts_usuario_compania_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_usuario_compania
    ADD CONSTRAINT accounts_usuario_compania_pkey PRIMARY KEY (id);


--
-- Name: aocr_asignacion_rt aocr_asignacion_rt_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_asignacion_rt
    ADD CONSTRAINT aocr_asignacion_rt_pkey PRIMARY KEY (id);


--
-- Name: aocr_audit_trail aocr_audit_trail_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_audit_trail
    ADD CONSTRAINT aocr_audit_trail_pkey PRIMARY KEY (id);


--
-- Name: aocr_correlativo_anual aocr_correlativo_anual_anio_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_correlativo_anual
    ADD CONSTRAINT aocr_correlativo_anual_anio_key UNIQUE (anio);


--
-- Name: aocr_correlativo_anual aocr_correlativo_anual_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_correlativo_anual
    ADD CONSTRAINT aocr_correlativo_anual_pkey PRIMARY KEY (id);


--
-- Name: aocr_correlativo_orden aocr_correlativo_orden_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_correlativo_orden
    ADD CONSTRAINT aocr_correlativo_orden_pkey PRIMARY KEY (anio);


--
-- Name: aocr_declaracion_historial aocr_declaracion_historial_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_declaracion_historial
    ADD CONSTRAINT aocr_declaracion_historial_pkey PRIMARY KEY (id);


--
-- Name: aocr_declaracion_tmp aocr_declaracion_tmp_email_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_declaracion_tmp
    ADD CONSTRAINT aocr_declaracion_tmp_email_key UNIQUE (email);


--
-- Name: aocr_declaracion_tmp aocr_declaracion_tmp_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_declaracion_tmp
    ADD CONSTRAINT aocr_declaracion_tmp_pkey PRIMARY KEY (id);


--
-- Name: aocr_entrega_destinatario aocr_entrega_destinatario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_destinatario
    ADD CONSTRAINT aocr_entrega_destinatario_pkey PRIMARY KEY (id);


--
-- Name: aocr_entrega_documento aocr_entrega_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_documento
    ADD CONSTRAINT aocr_entrega_documento_pkey PRIMARY KEY (id);


--
-- Name: aocr_entrega_final aocr_entrega_final_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_final
    ADD CONSTRAINT aocr_entrega_final_pkey PRIMARY KEY (id);


--
-- Name: aocr_entrega_intento aocr_entrega_intento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_intento
    ADD CONSTRAINT aocr_entrega_intento_pkey PRIMARY KEY (id);


--
-- Name: aocr_evento_workflow aocr_evento_workflow_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_evento_workflow
    ADD CONSTRAINT aocr_evento_workflow_pkey PRIMARY KEY (id);


--
-- Name: aocr_expediente_archivo aocr_expediente_archivo_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_archivo
    ADD CONSTRAINT aocr_expediente_archivo_pkey PRIMARY KEY (id);


--
-- Name: aocr_expediente_compania aocr_expediente_compania_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_compania
    ADD CONSTRAINT aocr_expediente_compania_pkey PRIMARY KEY (id);


--
-- Name: aocr_fr3_integracion aocr_fr3_integracion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_integracion
    ADD CONSTRAINT aocr_fr3_integracion_pkey PRIMARY KEY (id);


--
-- Name: aocr_fr3_operacion aocr_fr3_operacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_operacion
    ADD CONSTRAINT aocr_fr3_operacion_pkey PRIMARY KEY (id);


--
-- Name: aocr_fr3_outbox aocr_fr3_outbox_event_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_outbox
    ADD CONSTRAINT aocr_fr3_outbox_event_key_key UNIQUE (event_key);


--
-- Name: aocr_fr3_outbox aocr_fr3_outbox_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_outbox
    ADD CONSTRAINT aocr_fr3_outbox_pkey PRIMARY KEY (id);


--
-- Name: aocr_fr3_retry_queue aocr_fr3_retry_queue_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_retry_queue
    ADD CONSTRAINT aocr_fr3_retry_queue_pkey PRIMARY KEY (id);


--
-- Name: aocr_fr3_tipo_operacion_map aocr_fr3_tipo_operacion_map_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_tipo_operacion_map
    ADD CONSTRAINT aocr_fr3_tipo_operacion_map_pkey PRIMARY KEY (id);


--
-- Name: aocr_idempotency_key aocr_idempotency_key_clave_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_idempotency_key
    ADD CONSTRAINT aocr_idempotency_key_clave_key UNIQUE (clave);


--
-- Name: aocr_idempotency_key aocr_idempotency_key_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_idempotency_key
    ADD CONSTRAINT aocr_idempotency_key_pkey PRIMARY KEY (id);


--
-- Name: aocr_inspector_reasignacion_historial aocr_inspector_reasignacion_historial_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_inspector_reasignacion_historial
    ADD CONSTRAINT aocr_inspector_reasignacion_historial_pkey PRIMARY KEY (id);


--
-- Name: aocr_jefatura_aeropuerto_correo aocr_jefatura_aeropuerto_correo_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_jefatura_aeropuerto_correo
    ADD CONSTRAINT aocr_jefatura_aeropuerto_correo_pkey PRIMARY KEY (estacion_codigo, correo);


--
-- Name: aocr_operador_espejo_as400 aocr_operador_espejo_as400_oid_cia_aviacion_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_operador_espejo_as400
    ADD CONSTRAINT aocr_operador_espejo_as400_oid_cia_aviacion_key UNIQUE (oid_cia_aviacion);


--
-- Name: aocr_operador_espejo_as400 aocr_operador_espejo_as400_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_operador_espejo_as400
    ADD CONSTRAINT aocr_operador_espejo_as400_pkey PRIMARY KEY (id);


--
-- Name: aocr_or_concepto aocr_or_concepto_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_concepto
    ADD CONSTRAINT aocr_or_concepto_codigo_key UNIQUE (codigo);


--
-- Name: aocr_or_concepto aocr_or_concepto_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_concepto
    ADD CONSTRAINT aocr_or_concepto_pkey PRIMARY KEY (id);


--
-- Name: aocr_or_correccion_financiera aocr_or_correccion_financiera_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_correccion_financiera
    ADD CONSTRAINT aocr_or_correccion_financiera_pkey PRIMARY KEY (id);


--
-- Name: aocr_or_orden_detalle aocr_or_orden_detalle_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden_detalle
    ADD CONSTRAINT aocr_or_orden_detalle_pkey PRIMARY KEY (id);


--
-- Name: aocr_or_orden aocr_or_orden_numero_orden_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden
    ADD CONSTRAINT aocr_or_orden_numero_orden_key UNIQUE (numero_orden);


--
-- Name: aocr_or_orden aocr_or_orden_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden
    ADD CONSTRAINT aocr_or_orden_pkey PRIMARY KEY (id);


--
-- Name: aocr_orden_recaudacion_detalle aocr_orden_recaudacion_detalle_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_orden_recaudacion_detalle
    ADD CONSTRAINT aocr_orden_recaudacion_detalle_pkey PRIMARY KEY (id);


--
-- Name: aocr_orden_recaudacion aocr_orden_recaudacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_orden_recaudacion
    ADD CONSTRAINT aocr_orden_recaudacion_pkey PRIMARY KEY (id);


--
-- Name: aocr_pago_comprobante aocr_pago_comprobante_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_pago_comprobante
    ADD CONSTRAINT aocr_pago_comprobante_pkey PRIMARY KEY (id);


--
-- Name: aocr_proceso_estado_historial aocr_proceso_estado_historial_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_proceso_estado_historial
    ADD CONSTRAINT aocr_proceso_estado_historial_pkey PRIMARY KEY (id);


--
-- Name: aocr_proceso_estado aocr_proceso_estado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_proceso_estado
    ADD CONSTRAINT aocr_proceso_estado_pkey PRIMARY KEY (id);


--
-- Name: aocr_proceso_idempotencia aocr_proceso_idempotencia_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_proceso_idempotencia
    ADD CONSTRAINT aocr_proceso_idempotencia_pkey PRIMARY KEY (clave);


--
-- Name: aocr_revision_documental_coordinador aocr_revision_documental_coordinador_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_revision_documental_coordinador
    ADD CONSTRAINT aocr_revision_documental_coordinador_pkey PRIMARY KEY (id);


--
-- Name: aocr_revision_documental_coordinador aocr_revision_documental_coordinador_solicitud_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_revision_documental_coordinador
    ADD CONSTRAINT aocr_revision_documental_coordinador_solicitud_id_key UNIQUE (solicitud_id);


--
-- Name: aocr_solicitud_rt_revision aocr_solicitud_rt_revision_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_solicitud_rt_revision
    ADD CONSTRAINT aocr_solicitud_rt_revision_pkey PRIMARY KEY (id);


--
-- Name: aocr_sync_log aocr_sync_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_sync_log
    ADD CONSTRAINT aocr_sync_log_pkey PRIMARY KEY (id);


--
-- Name: aocr_tb_factura_pago aocr_tb_factura_pago_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_factura_pago
    ADD CONSTRAINT aocr_tb_factura_pago_pkey PRIMARY KEY (id);


--
-- Name: aocr_tb_sync_log aocr_tb_sync_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_sync_log
    ADD CONSTRAINT aocr_tb_sync_log_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbaeronave aocr_tbaeronave_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbaeronave
    ADD CONSTRAINT aocr_tbaeronave_pkey PRIMARY KEY (codigo_aeronave);


--
-- Name: aocr_tbaeronave_solicitud aocr_tbaeronave_solicitud_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbaeronave_solicitud
    ADD CONSTRAINT aocr_tbaeronave_solicitud_pkey PRIMARY KEY (codigo_aeronave_solicitud);


--
-- Name: aocr_tbauditoria aocr_tbauditoria_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbauditoria
    ADD CONSTRAINT aocr_tbauditoria_pkey PRIMARY KEY (codigo_auditoria);


--
-- Name: aocr_tbcertificado aocr_tbcertificado_codigo_solicitud_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcertificado
    ADD CONSTRAINT aocr_tbcertificado_codigo_solicitud_key UNIQUE (codigo_solicitud);


--
-- Name: aocr_tbcertificado aocr_tbcertificado_codigo_verificacion_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcertificado
    ADD CONSTRAINT aocr_tbcertificado_codigo_verificacion_key UNIQUE (codigo_verificacion);


--
-- Name: aocr_tbcertificado aocr_tbcertificado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcertificado
    ADD CONSTRAINT aocr_tbcertificado_pkey PRIMARY KEY (codigo_certificado);


--
-- Name: aocr_tbchecklist_item aocr_tbchecklist_item_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist_item
    ADD CONSTRAINT aocr_tbchecklist_item_pkey PRIMARY KEY (codigo_item);


--
-- Name: aocr_tbchecklist aocr_tbchecklist_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist
    ADD CONSTRAINT aocr_tbchecklist_pkey PRIMARY KEY (codigo_checklist);


--
-- Name: aocr_tbchecklist_solicitud aocr_tbchecklist_solicitud_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist_solicitud
    ADD CONSTRAINT aocr_tbchecklist_solicitud_pkey PRIMARY KEY (codigo_checklist_solicitud);


--
-- Name: aocr_tbcondiciones_limitaciones aocr_tbcondiciones_limitaciones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcondiciones_limitaciones
    ADD CONSTRAINT aocr_tbcondiciones_limitaciones_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbcorreo_institucional aocr_tbcorreo_institucional_codigo_area_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcorreo_institucional
    ADD CONSTRAINT aocr_tbcorreo_institucional_codigo_area_key UNIQUE (codigo_area);


--
-- Name: aocr_tbcorreo_institucional_historial aocr_tbcorreo_institucional_historial_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcorreo_institucional_historial
    ADD CONSTRAINT aocr_tbcorreo_institucional_historial_pkey PRIMARY KEY (codigo_historial);


--
-- Name: aocr_tbcorreo_institucional aocr_tbcorreo_institucional_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcorreo_institucional
    ADD CONSTRAINT aocr_tbcorreo_institucional_pkey PRIMARY KEY (codigo_correo);


--
-- Name: aocr_tbdesignacion_inspector aocr_tbdesignacion_inspector_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdesignacion_inspector
    ADD CONSTRAINT aocr_tbdesignacion_inspector_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbdocumento_generado aocr_tbdocumento_generado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_generado
    ADD CONSTRAINT aocr_tbdocumento_generado_pkey PRIMARY KEY (codigo_documento);


--
-- Name: aocr_tbdocumento_inspeccion aocr_tbdocumento_inspeccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_inspeccion
    ADD CONSTRAINT aocr_tbdocumento_inspeccion_pkey PRIMARY KEY (codigo_documento);


--
-- Name: aocr_tbdocumento aocr_tbdocumento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento
    ADD CONSTRAINT aocr_tbdocumento_pkey PRIMARY KEY (codigo_documento);


--
-- Name: aocr_tbdocumento_subsanacion aocr_tbdocumento_subsanacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_subsanacion
    ADD CONSTRAINT aocr_tbdocumento_subsanacion_pkey PRIMARY KEY (codigo_documento);


--
-- Name: aocr_tbfirma_documento aocr_tbfirma_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbfirma_documento
    ADD CONSTRAINT aocr_tbfirma_documento_pkey PRIMARY KEY (codigo_firma);


--
-- Name: aocr_tbfirma_posicion_documento aocr_tbfirma_posicion_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbfirma_posicion_documento
    ADD CONSTRAINT aocr_tbfirma_posicion_documento_pkey PRIMARY KEY (codigo_posicion_firma);


--
-- Name: aocr_tbhallazgo aocr_tbhallazgo_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhallazgo
    ADD CONSTRAINT aocr_tbhallazgo_pkey PRIMARY KEY (codigo_hallazgo);


--
-- Name: aocr_tbhistorial_documental aocr_tbhistorial_documental_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_documental
    ADD CONSTRAINT aocr_tbhistorial_documental_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbhistorial_estado_inspeccion aocr_tbhistorial_estado_inspeccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_estado_inspeccion
    ADD CONSTRAINT aocr_tbhistorial_estado_inspeccion_pkey PRIMARY KEY (codigo_historial);


--
-- Name: aocr_tbhistorial_estado aocr_tbhistorial_estado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_estado
    ADD CONSTRAINT aocr_tbhistorial_estado_pkey PRIMARY KEY (codigo_historial);


--
-- Name: aocr_tbinforme aocr_tbinforme_codigo_inspeccion_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinforme
    ADD CONSTRAINT aocr_tbinforme_codigo_inspeccion_key UNIQUE (codigo_inspeccion);


--
-- Name: aocr_tbinforme_inspeccion aocr_tbinforme_inspeccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinforme_inspeccion
    ADD CONSTRAINT aocr_tbinforme_inspeccion_pkey PRIMARY KEY (codigo_informe);


--
-- Name: aocr_tbinforme aocr_tbinforme_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinforme
    ADD CONSTRAINT aocr_tbinforme_pkey PRIMARY KEY (codigo_informe);


--
-- Name: aocr_tbinspeccion aocr_tbinspeccion_numero_inspeccion_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinspeccion
    ADD CONSTRAINT aocr_tbinspeccion_numero_inspeccion_key UNIQUE (numero_inspeccion);


--
-- Name: aocr_tbinspeccion aocr_tbinspeccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinspeccion
    ADD CONSTRAINT aocr_tbinspeccion_pkey PRIMARY KEY (codigo_inspeccion);


--
-- Name: aocr_tbinspectores aocr_tbinspectores_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinspectores
    ADD CONSTRAINT aocr_tbinspectores_pkey PRIMARY KEY (id);


--
-- Name: aocr_tblog aocr_tblog_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tblog
    ADD CONSTRAINT aocr_tblog_pkey PRIMARY KEY (codigo_log);


--
-- Name: aocr_tblv_operacional_eae aocr_tblv_operacional_eae_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tblv_operacional_eae
    ADD CONSTRAINT aocr_tblv_operacional_eae_pkey PRIMARY KEY (codigo_lv);


--
-- Name: aocr_tbnoconformidad aocr_tbnoconformidad_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnoconformidad
    ADD CONSTRAINT aocr_tbnoconformidad_pkey PRIMARY KEY (codigo_no_conformidad);


--
-- Name: aocr_tbnotificacion aocr_tbnotificacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnotificacion
    ADD CONSTRAINT aocr_tbnotificacion_pkey PRIMARY KEY (codigonotificacion);


--
-- Name: aocr_tbobservacion aocr_tbobservacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbobservacion
    ADD CONSTRAINT aocr_tbobservacion_pkey PRIMARY KEY (codigo_observacion);


--
-- Name: aocr_tbpago aocr_tbpago_numero_factura_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbpago
    ADD CONSTRAINT aocr_tbpago_numero_factura_key UNIQUE (numero_factura);


--
-- Name: aocr_tbpago aocr_tbpago_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbpago
    ADD CONSTRAINT aocr_tbpago_pkey PRIMARY KEY (codigo_pago);


--
-- Name: aocr_tbparametro aocr_tbparametro_clave_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbparametro
    ADD CONSTRAINT aocr_tbparametro_clave_key UNIQUE (clave);


--
-- Name: aocr_tbparametro aocr_tbparametro_codigoparametro_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbparametro
    ADD CONSTRAINT aocr_tbparametro_codigoparametro_key UNIQUE (codigoparametro);


--
-- Name: aocr_tbparametro aocr_tbparametro_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbparametro
    ADD CONSTRAINT aocr_tbparametro_pkey PRIMARY KEY (codigo_parametro);


--
-- Name: aocr_tbrevision_documental aocr_tbrevision_documental_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbrevision_documental
    ADD CONSTRAINT aocr_tbrevision_documental_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbsesiones aocr_tbsesiones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsesiones
    ADD CONSTRAINT aocr_tbsesiones_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbsesiones aocr_tbsesiones_token_sesion_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsesiones
    ADD CONSTRAINT aocr_tbsesiones_token_sesion_key UNIQUE (token_sesion);


--
-- Name: aocr_tbsolicitud_estacion aocr_tbsolicitud_estacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsolicitud_estacion
    ADD CONSTRAINT aocr_tbsolicitud_estacion_pkey PRIMARY KEY (id);


--
-- Name: aocr_tbsolicitud aocr_tbsolicitud_numero_solicitud_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsolicitud
    ADD CONSTRAINT aocr_tbsolicitud_numero_solicitud_key UNIQUE (numero_solicitud);


--
-- Name: aocr_tbsolicitud aocr_tbsolicitud_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsolicitud
    ADD CONSTRAINT aocr_tbsolicitud_pkey PRIMARY KEY (codigo_solicitud);


--
-- Name: aocr_tbsubsanacion aocr_tbsubsanacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsubsanacion
    ADD CONSTRAINT aocr_tbsubsanacion_pkey PRIMARY KEY (codigo_subsanacion);


--
-- Name: aocr_tbviatico aocr_tbviatico_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbviatico
    ADD CONSTRAINT aocr_tbviatico_pkey PRIMARY KEY (codigo_viatico);


--
-- Name: aocr_usuario_compania_rt aocr_usuario_compania_rt_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_compania_rt
    ADD CONSTRAINT aocr_usuario_compania_rt_pkey PRIMARY KEY (id);


--
-- Name: aocr_usuario_interno_rt aocr_usuario_interno_rt_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_interno_rt
    ADD CONSTRAINT aocr_usuario_interno_rt_pkey PRIMARY KEY (id);


--
-- Name: aocr_usuario_transferencia_detalle aocr_usuario_transferencia_detalle_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_transferencia_detalle
    ADD CONSTRAINT aocr_usuario_transferencia_detalle_pkey PRIMARY KEY (id_detalle);


--
-- Name: aocr_usuario_transferencia aocr_usuario_transferencia_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_transferencia
    ADD CONSTRAINT aocr_usuario_transferencia_pkey PRIMARY KEY (id_transferencia);


--
-- Name: auditoria_historialestado auditoria_historialestado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditoria_historialestado
    ADD CONSTRAINT auditoria_historialestado_pkey PRIMARY KEY (id);


--
-- Name: auditoria_seguridad auditoria_seguridad_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditoria_seguridad
    ADD CONSTRAINT auditoria_seguridad_pkey PRIMARY KEY (id_auditoria);


--
-- Name: auditorias auditorias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditorias
    ADD CONSTRAINT auditorias_pkey PRIMARY KEY (id);


--
-- Name: auth_group auth_group_name_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_group
    ADD CONSTRAINT auth_group_name_key UNIQUE (name);


--
-- Name: auth_group_permissions auth_group_permissions_group_id_permission_id_0cd325b0_uniq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_group_permissions
    ADD CONSTRAINT auth_group_permissions_group_id_permission_id_0cd325b0_uniq UNIQUE (group_id, permission_id);


--
-- Name: auth_group_permissions auth_group_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_group_permissions
    ADD CONSTRAINT auth_group_permissions_pkey PRIMARY KEY (id);


--
-- Name: auth_group auth_group_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_group
    ADD CONSTRAINT auth_group_pkey PRIMARY KEY (id);


--
-- Name: auth_permission auth_permission_content_type_id_codename_01ab375a_uniq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_permission
    ADD CONSTRAINT auth_permission_content_type_id_codename_01ab375a_uniq UNIQUE (content_type_id, codename);


--
-- Name: auth_permission auth_permission_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_permission
    ADD CONSTRAINT auth_permission_pkey PRIMARY KEY (id);


--
-- Name: cache_locks cache_locks_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cache_locks
    ADD CONSTRAINT cache_locks_pkey PRIMARY KEY (key);


--
-- Name: cache cache_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cache
    ADD CONSTRAINT cache_pkey PRIMARY KEY (key);


--
-- Name: capacitaciones capacitaciones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones
    ADD CONSTRAINT capacitaciones_pkey PRIMARY KEY (id);


--
-- Name: certificados_certificadoaocr certificados_certificadoaocr_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certificadoaocr_pkey PRIMARY KEY (id);


--
-- Name: competencias competencias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.competencias
    ADD CONSTRAINT competencias_pkey PRIMARY KEY (id);


--
-- Name: conceptos conceptos_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.conceptos
    ADD CONSTRAINT conceptos_codigo_key UNIQUE (codigo);


--
-- Name: conceptos conceptos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.conceptos
    ADD CONSTRAINT conceptos_pkey PRIMARY KEY (id);


--
-- Name: contactos_institucionales contactos_institucionales_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contactos_institucionales
    ADD CONSTRAINT contactos_institucionales_pkey PRIMARY KEY (id);


--
-- Name: contactos_servidor contactos_servidor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contactos_servidor
    ADD CONSTRAINT contactos_servidor_pkey PRIMARY KEY (id);


--
-- Name: contribuyentes contribuyentes_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contribuyentes
    ADD CONSTRAINT contribuyentes_pkey PRIMARY KEY (id);


--
-- Name: contribuyentes contribuyentes_ruc_cedula_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contribuyentes
    ADD CONSTRAINT contribuyentes_ruc_cedula_key UNIQUE (ruc_cedula);


--
-- Name: cursos cursos_codigo_legado_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos
    ADD CONSTRAINT cursos_codigo_legado_unique UNIQUE (codigo_legado);


--
-- Name: cursos_mandatorios cursos_mandatorios_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos_mandatorios
    ADD CONSTRAINT cursos_mandatorios_pkey PRIMARY KEY (id);


--
-- Name: cursos cursos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos
    ADD CONSTRAINT cursos_pkey PRIMARY KEY (id);


--
-- Name: datos_laborales datos_laborales_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_pkey PRIMARY KEY (id);


--
-- Name: detalles_orden detalles_orden_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalles_orden
    ADD CONSTRAINT detalles_orden_pkey PRIMARY KEY (id);


--
-- Name: django_admin_log django_admin_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_admin_log
    ADD CONSTRAINT django_admin_log_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_auditoria_evento django_aocr_auditoria_evento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_auditoria_evento
    ADD CONSTRAINT django_aocr_auditoria_evento_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_bandeja_item django_aocr_bandeja_item_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_bandeja_item
    ADD CONSTRAINT django_aocr_bandeja_item_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_certificado_aocr django_aocr_certificado_aocr_inspeccion_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_certificado_aocr
    ADD CONSTRAINT django_aocr_certificado_aocr_inspeccion_id_key UNIQUE (inspeccion_id);


--
-- Name: django_aocr_certificado_aocr django_aocr_certificado_aocr_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_certificado_aocr
    ADD CONSTRAINT django_aocr_certificado_aocr_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_comprobante_pago django_aocr_comprobante_pago_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_comprobante_pago
    ADD CONSTRAINT django_aocr_comprobante_pago_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_concepto_recaudacion django_aocr_concepto_recaudacion_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_concepto_recaudacion
    ADD CONSTRAINT django_aocr_concepto_recaudacion_codigo_key UNIQUE (codigo);


--
-- Name: django_aocr_concepto_recaudacion django_aocr_concepto_recaudacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_concepto_recaudacion
    ADD CONSTRAINT django_aocr_concepto_recaudacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_condiciones_limitaciones django_aocr_condiciones_limitaciones_inspeccion_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_condiciones_limitaciones
    ADD CONSTRAINT django_aocr_condiciones_limitaciones_inspeccion_id_key UNIQUE (inspeccion_id);


--
-- Name: django_aocr_condiciones_limitaciones django_aocr_condiciones_limitaciones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_condiciones_limitaciones
    ADD CONSTRAINT django_aocr_condiciones_limitaciones_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_contador_bandeja django_aocr_contador_ban_usuario_id_rol_modulo_co_b3626b36_uniq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_contador_bandeja
    ADD CONSTRAINT django_aocr_contador_ban_usuario_id_rol_modulo_co_b3626b36_uniq UNIQUE (usuario_id, rol, modulo, codigo);


--
-- Name: django_aocr_contador_bandeja django_aocr_contador_bandeja_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_contador_bandeja
    ADD CONSTRAINT django_aocr_contador_bandeja_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_descarga_documento django_aocr_descarga_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_descarga_documento
    ADD CONSTRAINT django_aocr_descarga_documento_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_designacion_inspector django_aocr_designacion_inspector_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_designacion_inspector
    ADD CONSTRAINT django_aocr_designacion_inspector_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_documento_generado django_aocr_documento_generado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_documento_generado
    ADD CONSTRAINT django_aocr_documento_generado_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_documento_solicitud django_aocr_documento_solicitud_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_documento_solicitud
    ADD CONSTRAINT django_aocr_documento_solicitud_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_documento_subsanacion_nc django_aocr_documento_subsanacion_nc_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_documento_subsanacion_nc
    ADD CONSTRAINT django_aocr_documento_subsanacion_nc_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_email_queue django_aocr_email_queue_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_email_queue
    ADD CONSTRAINT django_aocr_email_queue_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_estado_tramite django_aocr_estado_tramite_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_estado_tramite
    ADD CONSTRAINT django_aocr_estado_tramite_codigo_key UNIQUE (codigo);


--
-- Name: django_aocr_estado_tramite django_aocr_estado_tramite_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_estado_tramite
    ADD CONSTRAINT django_aocr_estado_tramite_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_factura_recaudacion django_aocr_factura_recaudacion_orden_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_factura_recaudacion
    ADD CONSTRAINT django_aocr_factura_recaudacion_orden_id_key UNIQUE (orden_id);


--
-- Name: django_aocr_factura_recaudacion django_aocr_factura_recaudacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_factura_recaudacion
    ADD CONSTRAINT django_aocr_factura_recaudacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_firma_documento django_aocr_firma_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_firma_documento
    ADD CONSTRAINT django_aocr_firma_documento_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_historial_estado django_aocr_historial_estado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_historial_estado
    ADD CONSTRAINT django_aocr_historial_estado_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_informe_tecnico django_aocr_informe_tecnico_inspeccion_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_informe_tecnico
    ADD CONSTRAINT django_aocr_informe_tecnico_inspeccion_id_key UNIQUE (inspeccion_id);


--
-- Name: django_aocr_informe_tecnico django_aocr_informe_tecnico_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_informe_tecnico
    ADD CONSTRAINT django_aocr_informe_tecnico_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_inspeccion_ejecucion django_aocr_inspeccion_ejecucion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_inspeccion_ejecucion
    ADD CONSTRAINT django_aocr_inspeccion_ejecucion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_inspeccion_ejecucion django_aocr_inspeccion_ejecucion_solicitud_inspeccion_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_inspeccion_ejecucion
    ADD CONSTRAINT django_aocr_inspeccion_ejecucion_solicitud_inspeccion_id_key UNIQUE (solicitud_inspeccion_id);


--
-- Name: django_aocr_lista_verificacion django_aocr_lista_verificacion_inspeccion_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_lista_verificacion
    ADD CONSTRAINT django_aocr_lista_verificacion_inspeccion_id_key UNIQUE (inspeccion_id);


--
-- Name: django_aocr_lista_verificacion django_aocr_lista_verificacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_lista_verificacion
    ADD CONSTRAINT django_aocr_lista_verificacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_no_conformidad django_aocr_no_conformidad_inspeccion_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_no_conformidad
    ADD CONSTRAINT django_aocr_no_conformidad_inspeccion_id_key UNIQUE (inspeccion_id);


--
-- Name: django_aocr_no_conformidad django_aocr_no_conformidad_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_no_conformidad
    ADD CONSTRAINT django_aocr_no_conformidad_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_orden_observacion django_aocr_orden_observacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_observacion
    ADD CONSTRAINT django_aocr_orden_observacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_orden_recaudacion_detalle django_aocr_orden_recaudacion_detalle_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_recaudacion_detalle
    ADD CONSTRAINT django_aocr_orden_recaudacion_detalle_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_orden_recaudacion django_aocr_orden_recaudacion_numero_orden_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_recaudacion
    ADD CONSTRAINT django_aocr_orden_recaudacion_numero_orden_key UNIQUE (numero_orden);


--
-- Name: django_aocr_orden_recaudacion django_aocr_orden_recaudacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_recaudacion
    ADD CONSTRAINT django_aocr_orden_recaudacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_plantilla_correo django_aocr_plantilla_correo_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_plantilla_correo
    ADD CONSTRAINT django_aocr_plantilla_correo_codigo_key UNIQUE (codigo);


--
-- Name: django_aocr_plantilla_correo django_aocr_plantilla_correo_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_plantilla_correo
    ADD CONSTRAINT django_aocr_plantilla_correo_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_registro_rt_archivo_version django_aocr_registro_rt_archivo_version_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt_archivo_version
    ADD CONSTRAINT django_aocr_registro_rt_archivo_version_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_registro_rt_observacion django_aocr_registro_rt_observacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt_observacion
    ADD CONSTRAINT django_aocr_registro_rt_observacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_registro_rt django_aocr_registro_rt_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt
    ADD CONSTRAINT django_aocr_registro_rt_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_revision_documental django_aocr_revision_documental_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_revision_documental
    ADD CONSTRAINT django_aocr_revision_documental_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_revision_documento django_aocr_revision_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_revision_documento
    ADD CONSTRAINT django_aocr_revision_documento_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_solicitud_formal django_aocr_solicitud_formal_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_solicitud_formal
    ADD CONSTRAINT django_aocr_solicitud_formal_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_solicitud_inspeccion_archivo_version django_aocr_solicitud_inspeccion_archivo_version_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_solicitud_inspeccion_archivo_version
    ADD CONSTRAINT django_aocr_solicitud_inspeccion_archivo_version_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_solicitud_inspeccion_observacion django_aocr_solicitud_inspeccion_observacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_solicitud_inspeccion_observacion
    ADD CONSTRAINT django_aocr_solicitud_inspeccion_observacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_solicitud_inspeccion django_aocr_solicitud_inspeccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_solicitud_inspeccion
    ADD CONSTRAINT django_aocr_solicitud_inspeccion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_solicitud_observacion django_aocr_solicitud_observacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_solicitud_observacion
    ADD CONSTRAINT django_aocr_solicitud_observacion_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_tipo_documento django_aocr_tipo_documento_codigo_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_tipo_documento
    ADD CONSTRAINT django_aocr_tipo_documento_codigo_key UNIQUE (codigo);


--
-- Name: django_aocr_tipo_documento django_aocr_tipo_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_tipo_documento
    ADD CONSTRAINT django_aocr_tipo_documento_pkey PRIMARY KEY (id);


--
-- Name: django_content_type django_content_type_app_label_model_76bd3d3b_uniq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_content_type
    ADD CONSTRAINT django_content_type_app_label_model_76bd3d3b_uniq UNIQUE (app_label, model);


--
-- Name: django_content_type django_content_type_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_content_type
    ADD CONSTRAINT django_content_type_pkey PRIMARY KEY (id);


--
-- Name: django_migrations django_migrations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_migrations
    ADD CONSTRAINT django_migrations_pkey PRIMARY KEY (id);


--
-- Name: django_session django_session_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_session
    ADD CONSTRAINT django_session_pkey PRIMARY KEY (session_key);


--
-- Name: documentos_documento documentos_documento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentos_documento
    ADD CONSTRAINT documentos_documento_pkey PRIMARY KEY (id);


--
-- Name: email_attachment email_attachment_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_attachment
    ADD CONSTRAINT email_attachment_pkey PRIMARY KEY (id);


--
-- Name: email_queue email_queue_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_queue
    ADD CONSTRAINT email_queue_pkey PRIMARY KEY (id);


--
-- Name: errores_importacion errores_importacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.errores_importacion
    ADD CONSTRAINT errores_importacion_pkey PRIMARY KEY (id);


--
-- Name: especialidades especialidades_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.especialidades
    ADD CONSTRAINT especialidades_pkey PRIMARY KEY (id);


--
-- Name: estados_capacitacion estados_capacitacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estados_capacitacion
    ADD CONSTRAINT estados_capacitacion_pkey PRIMARY KEY (id);


--
-- Name: estados_servidor estados_servidor_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estados_servidor
    ADD CONSTRAINT estados_servidor_pkey PRIMARY KEY (id);


--
-- Name: estatutos_organicos estatutos_organicos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estatutos_organicos
    ADD CONSTRAINT estatutos_organicos_pkey PRIMARY KEY (id);


--
-- Name: experiencia_profesional experiencia_profesional_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.experiencia_profesional
    ADD CONSTRAINT experiencia_profesional_pkey PRIMARY KEY (id);


--
-- Name: failed_jobs failed_jobs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.failed_jobs
    ADD CONSTRAINT failed_jobs_pkey PRIMARY KEY (id);


--
-- Name: failed_jobs failed_jobs_uuid_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.failed_jobs
    ADD CONSTRAINT failed_jobs_uuid_unique UNIQUE (uuid);


--
-- Name: firmas_firmadocumento firmas_firmadocumento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.firmas_firmadocumento
    ADD CONSTRAINT firmas_firmadocumento_pkey PRIMARY KEY (id);


--
-- Name: fr3_detalle_pg fr3_detalle_pg_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_detalle_pg
    ADD CONSTRAINT fr3_detalle_pg_pkey PRIMARY KEY (id);


--
-- Name: fr3_detalle fr3_detalle_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_detalle
    ADD CONSTRAINT fr3_detalle_pkey PRIMARY KEY (id);


--
-- Name: fr3_pg fr3_pg_idempotency_key_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_pg
    ADD CONSTRAINT fr3_pg_idempotency_key_key UNIQUE (idempotency_key);


--
-- Name: fr3_pg fr3_pg_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_pg
    ADD CONSTRAINT fr3_pg_pkey PRIMARY KEY (id);


--
-- Name: fr3 fr3_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3
    ADD CONSTRAINT fr3_pkey PRIMARY KEY (id);


--
-- Name: gestiones_administrativas gestiones_administrativas_codigo_legado_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.gestiones_administrativas
    ADD CONSTRAINT gestiones_administrativas_codigo_legado_unique UNIQUE (codigo_legado);


--
-- Name: gestiones_administrativas gestiones_administrativas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.gestiones_administrativas
    ADD CONSTRAINT gestiones_administrativas_pkey PRIMARY KEY (id);


--
-- Name: grupos_ocupacionales grupos_ocupacionales_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.grupos_ocupacionales
    ADD CONSTRAINT grupos_ocupacionales_pkey PRIMARY KEY (id);


--
-- Name: historial_estados_orden historial_estados_orden_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.historial_estados_orden
    ADD CONSTRAINT historial_estados_orden_pkey PRIMARY KEY (id);


--
-- Name: historial_laboral historial_laboral_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.historial_laboral
    ADD CONSTRAINT historial_laboral_pkey PRIMARY KEY (id);


--
-- Name: importaciones importaciones_hash_archivo_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.importaciones
    ADD CONSTRAINT importaciones_hash_archivo_unique UNIQUE (hash_archivo);


--
-- Name: importaciones importaciones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.importaciones
    ADD CONSTRAINT importaciones_pkey PRIMARY KEY (id);


--
-- Name: inspecciones_inspecciontecnica inspecciones_inspecciontecnica_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_inspecciontecnica
    ADD CONSTRAINT inspecciones_inspecciontecnica_pkey PRIMARY KEY (id);


--
-- Name: inspecciones_noconformidad inspecciones_noconformidad_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_noconformidad
    ADD CONSTRAINT inspecciones_noconformidad_pkey PRIMARY KEY (id);


--
-- Name: inspecciones_solicitudinspeccion inspecciones_solicitudinspeccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_solicitudinspeccion
    ADD CONSTRAINT inspecciones_solicitudinspeccion_pkey PRIMARY KEY (id);


--
-- Name: instituciones_capacitadoras instituciones_capacitadoras_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones_capacitadoras
    ADD CONSTRAINT instituciones_capacitadoras_pkey PRIMARY KEY (id);


--
-- Name: instituciones_educativas instituciones_educativas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones_educativas
    ADD CONSTRAINT instituciones_educativas_pkey PRIMARY KEY (id);


--
-- Name: instituciones instituciones_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones
    ADD CONSTRAINT instituciones_pkey PRIMARY KEY (id);


--
-- Name: instituciones instituciones_ruc_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instituciones
    ADD CONSTRAINT instituciones_ruc_unique UNIQUE (ruc);


--
-- Name: instrucciones_formales instrucciones_formales_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instrucciones_formales
    ADD CONSTRAINT instrucciones_formales_pkey PRIMARY KEY (id);


--
-- Name: job_batches job_batches_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.job_batches
    ADD CONSTRAINT job_batches_pkey PRIMARY KEY (id);


--
-- Name: jobs jobs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.jobs
    ADD CONSTRAINT jobs_pkey PRIMARY KEY (id);


--
-- Name: lugares_trabajo lugares_trabajo_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.lugares_trabajo
    ADD CONSTRAINT lugares_trabajo_pkey PRIMARY KEY (id);


--
-- Name: materias materias_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.materias
    ADD CONSTRAINT materias_pkey PRIMARY KEY (id);


--
-- Name: menu menu_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.menu
    ADD CONSTRAINT menu_pkey PRIMARY KEY (idmenu);


--
-- Name: migrations migrations_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.migrations
    ADD CONSTRAINT migrations_pkey PRIMARY KEY (id);


--
-- Name: modalidades_prestacion modalidades_prestacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modalidades_prestacion
    ADD CONSTRAINT modalidades_prestacion_pkey PRIMARY KEY (id);


--
-- Name: model_has_permissions model_has_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.model_has_permissions
    ADD CONSTRAINT model_has_permissions_pkey PRIMARY KEY (permission_id, model_id, model_type);


--
-- Name: model_has_roles model_has_roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.model_has_roles
    ADD CONSTRAINT model_has_roles_pkey PRIMARY KEY (role_id, model_id, model_type);


--
-- Name: modificacion_cyl_aeropuertomodificacioncyl modificacion_cyl_aeropuertomodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_aeropuertomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_aeropuertomodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: modificacion_cyl_documentogeneradomodificacioncyl modificacion_cyl_documentogeneradomodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentogeneradomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_documentogeneradomodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_documentomodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_documentomodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: modificacion_cyl_historialdocumentomodificacioncyl modificacion_cyl_historialdocumentomodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialdocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_historialdocumentomodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: modificacion_cyl_historialmodificacioncyl modificacion_cyl_historialmodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_historialmodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl modificacion_cyl_revisiondocumentomodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_revisiondocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_revisiondocumentomodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_solicitudmodificacioncyl_numero_solicitud_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_solicitudmodificacioncyl_numero_solicitud_key UNIQUE (numero_solicitud);


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_solicitudmodificacioncyl_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_solicitudmodificacioncyl_pkey PRIMARY KEY (id);


--
-- Name: niveles_instruccion niveles_instruccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.niveles_instruccion
    ADD CONSTRAINT niveles_instruccion_pkey PRIMARY KEY (id);


--
-- Name: niveles_proceso niveles_proceso_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.niveles_proceso
    ADD CONSTRAINT niveles_proceso_pkey PRIMARY KEY (id);


--
-- Name: django_aocr_notificacion notificaciones_notificacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_notificacion
    ADD CONSTRAINT notificaciones_notificacion_pkey PRIMARY KEY (id);


--
-- Name: operadores_operador operadores_operador_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_operador
    ADD CONSTRAINT operadores_operador_pkey PRIMARY KEY (id);


--
-- Name: operadores_operador operadores_operador_ruc_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_operador
    ADD CONSTRAINT operadores_operador_ruc_key UNIQUE (ruc);


--
-- Name: operadores_registrort operadores_registrort_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_registrort
    ADD CONSTRAINT operadores_registrort_pkey PRIMARY KEY (id);


--
-- Name: operadores_representantetecnico operadores_representantetecnico_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_representantetecnico
    ADD CONSTRAINT operadores_representantetecnico_pkey PRIMARY KEY (id);


--
-- Name: operadores_representantetecnico operadores_representantetecnico_usuario_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_representantetecnico
    ADD CONSTRAINT operadores_representantetecnico_usuario_id_key UNIQUE (usuario_id);


--
-- Name: ordenes_recaudacion ordenes_recaudacion_numero_orden_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ordenes_recaudacion
    ADD CONSTRAINT ordenes_recaudacion_numero_orden_key UNIQUE (numero_orden);


--
-- Name: ordenes_recaudacion ordenes_recaudacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ordenes_recaudacion
    ADD CONSTRAINT ordenes_recaudacion_pkey PRIMARY KEY (id);


--
-- Name: otros_estudios otros_estudios_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.otros_estudios
    ADD CONSTRAINT otros_estudios_pkey PRIMARY KEY (id);


--
-- Name: pagos pagos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pagos
    ADD CONSTRAINT pagos_pkey PRIMARY KEY (id);


--
-- Name: parametros parametros_clave_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.parametros
    ADD CONSTRAINT parametros_clave_key UNIQUE (clave);


--
-- Name: parametros parametros_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.parametros
    ADD CONSTRAINT parametros_pkey PRIMARY KEY (id);


--
-- Name: password_reset_tokens password_reset_tokens_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.password_reset_tokens
    ADD CONSTRAINT password_reset_tokens_pkey PRIMARY KEY (email);


--
-- Name: permisos permisos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT permisos_pkey PRIMARY KEY (idpermisos);


--
-- Name: permissions permissions_name_guard_name_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permissions
    ADD CONSTRAINT permissions_name_guard_name_unique UNIQUE (name, guard_name);


--
-- Name: permissions permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permissions
    ADD CONSTRAINT permissions_pkey PRIMARY KEY (id);


--
-- Name: personas personas_numero_documento_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.personas
    ADD CONSTRAINT personas_numero_documento_unique UNIQUE (numero_documento);


--
-- Name: personas personas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.personas
    ADD CONSTRAINT personas_pkey PRIMARY KEY (id);


--
-- Name: seguridad_rol_permiso pk_seguridad_rol_permiso; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.seguridad_rol_permiso
    ADD CONSTRAINT pk_seguridad_rol_permiso PRIMARY KEY (codigorol, id_permiso);


--
-- Name: programas_capacitacion programas_capacitacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.programas_capacitacion
    ADD CONSTRAINT programas_capacitacion_pkey PRIMARY KEY (id);


--
-- Name: puestos_institucionales puestos_institucionales_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.puestos_institucionales
    ADD CONSTRAINT puestos_institucionales_pkey PRIMARY KEY (id);


--
-- Name: recaudacion_comprobantepago recaudacion_comprobantepago_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_comprobantepago
    ADD CONSTRAINT recaudacion_comprobantepago_pkey PRIMARY KEY (id);


--
-- Name: recaudacion_factura recaudacion_factura_numero_factura_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_factura
    ADD CONSTRAINT recaudacion_factura_numero_factura_key UNIQUE (numero_factura);


--
-- Name: recaudacion_factura recaudacion_factura_orden_id_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_factura
    ADD CONSTRAINT recaudacion_factura_orden_id_key UNIQUE (orden_id);


--
-- Name: recaudacion_factura recaudacion_factura_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_factura
    ADD CONSTRAINT recaudacion_factura_pkey PRIMARY KEY (id);


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrecaudacion_numero_orden_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrecaudacion_numero_orden_key UNIQUE (numero_orden);


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrecaudacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrecaudacion_pkey PRIMARY KEY (id);


--
-- Name: rol rol_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.rol
    ADD CONSTRAINT rol_pkey PRIMARY KEY (codigorol);


--
-- Name: role_has_permissions role_has_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.role_has_permissions
    ADD CONSTRAINT role_has_permissions_pkey PRIMARY KEY (permission_id, role_id);


--
-- Name: roles roles_name_guard_name_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_name_guard_name_unique UNIQUE (name, guard_name);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id);


--
-- Name: seguridad_permiso seguridad_permiso_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.seguridad_permiso
    ADD CONSTRAINT seguridad_permiso_pkey PRIMARY KEY (id_permiso);


--
-- Name: servidor_discapacidades servidor_discapacidades_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_discapacidades
    ADD CONSTRAINT servidor_discapacidades_pkey PRIMARY KEY (id);


--
-- Name: servidor_titulos servidor_titulos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_titulos
    ADD CONSTRAINT servidor_titulos_pkey PRIMARY KEY (id);


--
-- Name: servidores servidores_codigo_servidor_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidores
    ADD CONSTRAINT servidores_codigo_servidor_unique UNIQUE (codigo_servidor);


--
-- Name: servidores servidores_persona_id_institucion_id_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidores
    ADD CONSTRAINT servidores_persona_id_institucion_id_unique UNIQUE (persona_id, institucion_id);


--
-- Name: servidores servidores_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidores
    ADD CONSTRAINT servidores_pkey PRIMARY KEY (id);


--
-- Name: sessions sessions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sessions
    ADD CONSTRAINT sessions_pkey PRIMARY KEY (id);


--
-- Name: solicitudes_documentosolicitud solicitudes_documentosolicitud_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_documentosolicitud
    ADD CONSTRAINT solicitudes_documentosolicitud_pkey PRIMARY KEY (id);


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitudaocr_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitudaocr_pkey PRIMARY KEY (id);


--
-- Name: staging_curar staging_curar_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_curar
    ADD CONSTRAINT staging_curar_pkey PRIMARY KEY (id);


--
-- Name: staging_maear staging_maear_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_maear
    ADD CONSTRAINT staging_maear_pkey PRIMARY KEY (id);


--
-- Name: staging_uniar staging_uniar_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_uniar
    ADD CONSTRAINT staging_uniar_pkey PRIMARY KEY (id);


--
-- Name: submenu submenu_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.submenu
    ADD CONSTRAINT submenu_pkey PRIMARY KEY (idsubmenu);


--
-- Name: sync_log sync_log_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sync_log
    ADD CONSTRAINT sync_log_pkey PRIMARY KEY (id);


--
-- Name: sync_state sync_state_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sync_state
    ADD CONSTRAINT sync_state_pkey PRIMARY KEY (entity);


--
-- Name: temarios temarios_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.temarios
    ADD CONSTRAINT temarios_pkey PRIMARY KEY (id);


--
-- Name: tipos_curso tipos_curso_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_curso
    ADD CONSTRAINT tipos_curso_pkey PRIMARY KEY (id);


--
-- Name: tipos_instruccion tipos_instruccion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_instruccion
    ADD CONSTRAINT tipos_instruccion_pkey PRIMARY KEY (id);


--
-- Name: tipos_movimiento tipos_movimiento_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_movimiento
    ADD CONSTRAINT tipos_movimiento_pkey PRIMARY KEY (id);


--
-- Name: tipos_relacion_laboral tipos_relacion_laboral_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_relacion_laboral
    ADD CONSTRAINT tipos_relacion_laboral_pkey PRIMARY KEY (id);


--
-- Name: tipos_unidad tipos_unidad_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tipos_unidad
    ADD CONSTRAINT tipos_unidad_pkey PRIMARY KEY (id);


--
-- Name: titulos titulos_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.titulos
    ADD CONSTRAINT titulos_pkey PRIMARY KEY (id);


--
-- Name: tramites_historialestado tramites_historialestado_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_historialestado
    ADD CONSTRAINT tramites_historialestado_pkey PRIMARY KEY (id);


--
-- Name: tramites_tramite tramites_tramite_numero_tramite_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_tramite
    ADD CONSTRAINT tramites_tramite_numero_tramite_key UNIQUE (numero_tramite);


--
-- Name: tramites_tramite tramites_tramite_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_tramite
    ADD CONSTRAINT tramites_tramite_pkey PRIMARY KEY (id);


--
-- Name: usuario_rol uk_usuario_rol_unico; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_rol
    ADD CONSTRAINT uk_usuario_rol_unico UNIQUE (codigousuario, codigorol);


--
-- Name: unidades_administrativas unidades_administrativas_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unidades_administrativas
    ADD CONSTRAINT unidades_administrativas_pkey PRIMARY KEY (id);


--
-- Name: operadores_representantetecnico unique_usuario_operador_rt; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_representantetecnico
    ADD CONSTRAINT unique_usuario_operador_rt UNIQUE (usuario_id, operador_id);


--
-- Name: aocr_or_orden unq_numero_orden; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden
    ADD CONSTRAINT unq_numero_orden UNIQUE (numero_orden);


--
-- Name: accounts_usuario_compania uq_accounts_usuario_compania_rol; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_usuario_compania
    ADD CONSTRAINT uq_accounts_usuario_compania_rol UNIQUE (user_id, compania_id, rol_en_compania);


--
-- Name: aocr_evento_workflow uq_aocr_evento_workflow_event_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_evento_workflow
    ADD CONSTRAINT uq_aocr_evento_workflow_event_key UNIQUE (event_key);


--
-- Name: aocr_expediente_compania uq_aocr_expediente_compania_anio; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_compania
    ADD CONSTRAINT uq_aocr_expediente_compania_anio UNIQUE (usuario_rt_id, compania_codigo, anio);


--
-- Name: aocr_expediente_archivo uq_aocr_expediente_ruta; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_archivo
    ADD CONSTRAINT uq_aocr_expediente_ruta UNIQUE (ruta_relativa);


--
-- Name: aocr_expediente_archivo uq_aocr_expediente_version; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_archivo
    ADD CONSTRAINT uq_aocr_expediente_version UNIQUE (solicitud_id, tipo_documento, version);


--
-- Name: aocr_fr3_operacion uq_aocr_fr3_operacion_idem; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_operacion
    ADD CONSTRAINT uq_aocr_fr3_operacion_idem UNIQUE (idempotency_key);


--
-- Name: aocr_fr3_operacion uq_aocr_fr3_operacion_orden; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_operacion
    ADD CONSTRAINT uq_aocr_fr3_operacion_orden UNIQUE (orden_recaudacion_id);


--
-- Name: aocr_fr3_operacion uq_aocr_fr3_operacion_solicitud; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_fr3_operacion
    ADD CONSTRAINT uq_aocr_fr3_operacion_solicitud UNIQUE (solicitud_id);


--
-- Name: aocr_tb_factura_pago uq_aocr_tb_factura_pago_orden; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_factura_pago
    ADD CONSTRAINT uq_aocr_tb_factura_pago_orden UNIQUE (orden_id);


--
-- Name: aocr_tb_factura_pago uq_aocr_tb_factura_pago_pago; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_factura_pago
    ADD CONSTRAINT uq_aocr_tb_factura_pago_pago UNIQUE (pago_id);


--
-- Name: seguridad_permiso uq_seguridad_permiso_codigo; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.seguridad_permiso
    ADD CONSTRAINT uq_seguridad_permiso_codigo UNIQUE (codigo);


--
-- Name: users users_email_unique; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_unique UNIQUE (email);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: usuario_as400_adicional usuario_as400_adicional_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_as400_adicional
    ADD CONSTRAINT usuario_as400_adicional_pkey PRIMARY KEY (usuco8);


--
-- Name: usuario_as400 usuario_as400_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_as400
    ADD CONSTRAINT usuario_as400_pkey PRIMARY KEY (usucod);


--
-- Name: usuario usuario_codigousuario_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_codigousuario_key UNIQUE (codigousuario);


--
-- Name: usuario usuario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_pkey PRIMARY KEY (idusuario);


--
-- Name: usuario_rol usuario_rol_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_rol
    ADD CONSTRAINT usuario_rol_pkey PRIMARY KEY (idusuariorol);


--
-- Name: usuarios_usuario_groups usuarios_usuario_groups_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_groups
    ADD CONSTRAINT usuarios_usuario_groups_pkey PRIMARY KEY (id);


--
-- Name: usuarios_usuario_groups usuarios_usuario_groups_usuario_id_group_id_4ed5b09e_uniq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_groups
    ADD CONSTRAINT usuarios_usuario_groups_usuario_id_group_id_4ed5b09e_uniq UNIQUE (usuario_id, group_id);


--
-- Name: usuarios_usuario usuarios_usuario_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario
    ADD CONSTRAINT usuarios_usuario_pkey PRIMARY KEY (id);


--
-- Name: usuarios_usuario_user_permissions usuarios_usuario_user_pe_usuario_id_permission_id_217cadcd_uniq; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_user_permissions
    ADD CONSTRAINT usuarios_usuario_user_pe_usuario_id_permission_id_217cadcd_uniq UNIQUE (usuario_id, permission_id);


--
-- Name: usuarios_usuario_user_permissions usuarios_usuario_user_permissions_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_user_permissions
    ADD CONSTRAINT usuarios_usuario_user_permissions_pkey PRIMARY KEY (id);


--
-- Name: usuarios_usuario usuarios_usuario_username_key; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario
    ADD CONSTRAINT usuarios_usuario_username_key UNIQUE (username);


--
-- Name: validaciones_capacitacion validaciones_capacitacion_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.validaciones_capacitacion
    ADD CONSTRAINT validaciones_capacitacion_pkey PRIMARY KEY (id);


--
-- Name: batch_log batch_log_pkey; Type: CONSTRAINT; Schema: sync; Owner: -
--

ALTER TABLE ONLY sync.batch_log
    ADD CONSTRAINT batch_log_pkey PRIMARY KEY (batch_id);


--
-- Name: rejections rejections_pkey; Type: CONSTRAINT; Schema: sync; Owner: -
--

ALTER TABLE ONLY sync.rejections
    ADD CONSTRAINT rejections_pkey PRIMARY KEY (id);


--
-- Name: tombstones tombstones_pkey; Type: CONSTRAINT; Schema: sync; Owner: -
--

ALTER TABLE ONLY sync.tombstones
    ADD CONSTRAINT tombstones_pkey PRIMARY KEY (id);


--
-- Name: watermark watermark_pkey; Type: CONSTRAINT; Schema: sync; Owner: -
--

ALTER TABLE ONLY sync.watermark
    ADD CONSTRAINT watermark_pkey PRIMARY KEY (table_name);


--
-- Name: idx_mirror_ciaarc_cianom; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_ciaarc_cianom ON mirror_raw.ciaarc USING btree (upper(cianom));


--
-- Name: idx_mirror_ciaarc_ruc; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_ciaarc_ruc ON mirror_raw.ciaarc USING btree (ciaruc);


--
-- Name: idx_mirror_oidar2_ciudad; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_oidar2_ciudad ON mirror_raw.oidar2 USING btree (oidco3);


--
-- Name: idx_mirror_opcar5_nombre; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_opcar5_nombre ON mirror_raw.opcar5 USING btree (upper(opcno5));


--
-- Name: idx_mirror_opcar5_oaci; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_opcar5_oaci ON mirror_raw.opcar5 USING btree (upper(opcc08));


--
-- Name: idx_mirror_opcar5_ruc; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_opcar5_ruc ON mirror_raw.opcar5 USING btree (opcru1);


--
-- Name: idx_mirror_opcar5_synced; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_opcar5_synced ON mirror_raw.opcar5 USING btree (_mirror_synced_at);


--
-- Name: idx_mirror_opcarc_nom; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_opcarc_nom ON mirror_raw.opcarc USING btree (opcnom);


--
-- Name: idx_mirror_opcarc_ruc; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_opcarc_ruc ON mirror_raw.opcarc USING btree (opcruc) WHERE ((opcruc IS NOT NULL) AND (opcruc <> ''::text));


--
-- Name: idx_mirror_usuarc_synced; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_usuarc_synced ON mirror_raw.usuarc USING btree (_mirror_synced_at);


--
-- Name: idx_mirror_usuarc_usuced; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_usuarc_usuced ON mirror_raw.usuarc USING btree (usuced);


--
-- Name: idx_mirror_usuarc_usunum; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_mirror_usuarc_usunum ON mirror_raw.usuarc USING btree (usunum);


--
-- Name: idx_tombstone_table_key; Type: INDEX; Schema: mirror_raw; Owner: -
--

CREATE INDEX idx_tombstone_table_key ON mirror_raw._tombstone USING btree (table_name, source_key);


--
-- Name: accounts_compania_ruc_6e416272_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX accounts_compania_ruc_6e416272_like ON public.accounts_compania USING btree (ruc varchar_pattern_ops);


--
-- Name: accounts_sesion_compania_activa_compania_id_ca3078b3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX accounts_sesion_compania_activa_compania_id_ca3078b3 ON public.accounts_sesion_compania_activa USING btree (compania_id);


--
-- Name: accounts_usuario_compania_compania_id_3a2aaf55; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX accounts_usuario_compania_compania_id_3a2aaf55 ON public.accounts_usuario_compania USING btree (compania_id);


--
-- Name: accounts_usuario_compania_user_id_f192f90b; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX accounts_usuario_compania_user_id_f192f90b ON public.accounts_usuario_compania USING btree (user_id);


--
-- Name: aocr_operador_espejo_as400_oid_cia_aviacion_fe0b4ea1_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX aocr_operador_espejo_as400_oid_cia_aviacion_fe0b4ea1_like ON public.aocr_operador_espejo_as400 USING btree (oid_cia_aviacion varchar_pattern_ops);


--
-- Name: auditoria_historialestado_usuario_id_473a140c; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auditoria_historialestado_usuario_id_473a140c ON public.auditoria_historialestado USING btree (usuario_id);


--
-- Name: auditorias_accion_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auditorias_accion_index ON public.auditorias USING btree (accion);


--
-- Name: auditorias_created_at_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auditorias_created_at_index ON public.auditorias USING btree (created_at);


--
-- Name: auditorias_modulo_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auditorias_modulo_index ON public.auditorias USING btree (modulo);


--
-- Name: auditorias_registro_id_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auditorias_registro_id_index ON public.auditorias USING btree (registro_id);


--
-- Name: auditorias_tabla_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auditorias_tabla_index ON public.auditorias USING btree (tabla);


--
-- Name: auth_group_name_a6ea08ec_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auth_group_name_a6ea08ec_like ON public.auth_group USING btree (name varchar_pattern_ops);


--
-- Name: auth_group_permissions_group_id_b120cbf9; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auth_group_permissions_group_id_b120cbf9 ON public.auth_group_permissions USING btree (group_id);


--
-- Name: auth_group_permissions_permission_id_84c5c92e; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auth_group_permissions_permission_id_84c5c92e ON public.auth_group_permissions USING btree (permission_id);


--
-- Name: auth_permission_content_type_id_2f476e4b; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX auth_permission_content_type_id_2f476e4b ON public.auth_permission USING btree (content_type_id);


--
-- Name: cache_expiration_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX cache_expiration_index ON public.cache USING btree (expiration);


--
-- Name: cache_locks_expiration_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX cache_locks_expiration_index ON public.cache_locks USING btree (expiration);


--
-- Name: certificados_certificadoaocr_coordinador_revisor_id_6053cd99; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_coordinador_revisor_id_6053cd99 ON public.certificados_certificadoaocr USING btree (coordinador_revisor_id);


--
-- Name: certificados_certificadoaocr_dcav_firmante_id_91d7d7b4; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_dcav_firmante_id_91d7d7b4 ON public.certificados_certificadoaocr USING btree (dcav_firmante_id);


--
-- Name: certificados_certificadoaocr_dirdac_firmante_id_03afde35; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_dirdac_firmante_id_03afde35 ON public.certificados_certificadoaocr USING btree (dirdac_firmante_id);


--
-- Name: certificados_certificadoaocr_documento_aocr_id_61934974; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_documento_aocr_id_61934974 ON public.certificados_certificadoaocr USING btree (documento_aocr_id);


--
-- Name: certificados_certificadoaocr_documento_condiciones_id_90199c62; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_documento_condiciones_id_90199c62 ON public.certificados_certificadoaocr USING btree (documento_condiciones_id);


--
-- Name: certificados_certificadoaocr_inspeccion_id_ced678c3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_inspeccion_id_ced678c3 ON public.certificados_certificadoaocr USING btree (inspeccion_id);


--
-- Name: certificados_certificadoaocr_solicitud_id_ff94af14; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX certificados_certificadoaocr_solicitud_id_ff94af14 ON public.certificados_certificadoaocr USING btree (solicitud_id);


--
-- Name: cursos_mandatorio_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX cursos_mandatorio_index ON public.cursos USING btree (mandatorio);


--
-- Name: cursos_mandatorios_estado_cumplimiento_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX cursos_mandatorios_estado_cumplimiento_index ON public.cursos_mandatorios USING btree (estado_cumplimiento);


--
-- Name: datos_laborales_vigente_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX datos_laborales_vigente_index ON public.datos_laborales USING btree (vigente);


--
-- Name: django_admin_log_content_type_id_c4bce8eb; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_admin_log_content_type_id_c4bce8eb ON public.django_admin_log USING btree (content_type_id);


--
-- Name: django_admin_log_user_id_c564eba6; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_admin_log_user_id_c564eba6 ON public.django_admin_log USING btree (user_id);


--
-- Name: django_aocr_accion_3f9e0a_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_accion_3f9e0a_idx ON public.django_aocr_auditoria_evento USING btree (accion, entidad);


--
-- Name: django_aocr_auditoria_evento_usuario_id_d134fd50; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_auditoria_evento_usuario_id_d134fd50 ON public.django_aocr_auditoria_evento USING btree (usuario_id);


--
-- Name: django_aocr_bandeja_item_grupo_destino_id_aefda6ad; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_bandeja_item_grupo_destino_id_aefda6ad ON public.django_aocr_bandeja_item USING btree (grupo_destino_id);


--
-- Name: django_aocr_bandeja_item_usuario_destino_id_22404cfe; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_bandeja_item_usuario_destino_id_22404cfe ON public.django_aocr_bandeja_item USING btree (usuario_destino_id);


--
-- Name: django_aocr_contador_bandeja_usuario_id_08db5efa; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_contador_bandeja_usuario_id_08db5efa ON public.django_aocr_contador_bandeja USING btree (usuario_id);


--
-- Name: django_aocr_creado__88e3db_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_creado__88e3db_idx ON public.django_aocr_auditoria_evento USING btree (creado_en);


--
-- Name: django_aocr_entidad_0aaf3e_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_entidad_0aaf3e_idx ON public.django_aocr_auditoria_evento USING btree (entidad, entidad_id);


--
-- Name: django_aocr_entidad_bef254_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_entidad_bef254_idx ON public.django_aocr_historial_estado USING btree (entidad_tipo, entidad_id);


--
-- Name: django_aocr_entidad_dbccb0_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_entidad_dbccb0_idx ON public.django_aocr_bandeja_item USING btree (entidad_tipo, entidad_id);


--
-- Name: django_aocr_estado__2c0b85_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_estado__2c0b85_idx ON public.django_aocr_historial_estado USING btree (estado_nuevo);


--
-- Name: django_aocr_estado_da2fbd_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_estado_da2fbd_idx ON public.django_aocr_registro_rt USING btree (estado);


--
-- Name: django_aocr_estado_tramite_codigo_66298268_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_estado_tramite_codigo_66298268_like ON public.django_aocr_estado_tramite USING btree (codigo varchar_pattern_ops);


--
-- Name: django_aocr_factura_recaudacion_generado_por_id_d083c175; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_factura_recaudacion_generado_por_id_d083c175 ON public.django_aocr_factura_recaudacion USING btree (generado_por_id);


--
-- Name: django_aocr_historial_estado_usuario_id_601fe8f6; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_historial_estado_usuario_id_601fe8f6 ON public.django_aocr_historial_estado USING btree (usuario_id);


--
-- Name: django_aocr_orden_observacion_orden_id_f3a2d2ca; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_orden_observacion_orden_id_f3a2d2ca ON public.django_aocr_orden_observacion USING btree (orden_id);


--
-- Name: django_aocr_orden_observacion_usuario_id_aeaf2037; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_orden_observacion_usuario_id_aeaf2037 ON public.django_aocr_orden_observacion USING btree (usuario_id);


--
-- Name: django_aocr_plantilla_correo_codigo_c6178557_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_plantilla_correo_codigo_c6178557_like ON public.django_aocr_plantilla_correo USING btree (codigo varchar_pattern_ops);


--
-- Name: django_aocr_registr_a1c3c0_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registr_a1c3c0_idx ON public.django_aocr_registro_rt_archivo_version USING btree (registro_id, tipo_archivo, activo);


--
-- Name: django_aocr_registro_rt_ar_usuario_carga_id_a993adce; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registro_rt_ar_usuario_carga_id_a993adce ON public.django_aocr_registro_rt_archivo_version USING btree (usuario_carga_id);


--
-- Name: django_aocr_registro_rt_archivo_version_registro_id_ab6a0e2f; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registro_rt_archivo_version_registro_id_ab6a0e2f ON public.django_aocr_registro_rt_archivo_version USING btree (registro_id);


--
-- Name: django_aocr_registro_rt_firmado_por_id_1b29ecec; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registro_rt_firmado_por_id_1b29ecec ON public.django_aocr_registro_rt USING btree (firmado_por_id);


--
-- Name: django_aocr_registro_rt_observacion_registro_id_694ef78e; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registro_rt_observacion_registro_id_694ef78e ON public.django_aocr_registro_rt_observacion USING btree (registro_id);


--
-- Name: django_aocr_registro_rt_observacion_usuario_id_15fa41c2; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registro_rt_observacion_usuario_id_15fa41c2 ON public.django_aocr_registro_rt_observacion USING btree (usuario_id);


--
-- Name: django_aocr_registro_rt_usuario_rt_id_6d760608; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_registro_rt_usuario_rt_id_6d760608 ON public.django_aocr_registro_rt USING btree (usuario_rt_id);


--
-- Name: django_aocr_rol_des_d0d8f3_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_rol_des_d0d8f3_idx ON public.django_aocr_bandeja_item USING btree (rol_destino, leido, activo);


--
-- Name: django_aocr_usuario_30ac6c_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_usuario_30ac6c_idx ON public.django_aocr_registro_rt USING btree (usuario_rt_id, compania);


--
-- Name: django_aocr_usuario_de6b21_idx; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_aocr_usuario_de6b21_idx ON public.django_aocr_bandeja_item USING btree (usuario_destino_id, leido, activo);


--
-- Name: django_session_expire_date_a5c62663; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_session_expire_date_a5c62663 ON public.django_session USING btree (expire_date);


--
-- Name: django_session_session_key_c0390e0f_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX django_session_session_key_c0390e0f_like ON public.django_session USING btree (session_key varchar_pattern_ops);


--
-- Name: documentos_documento_cargado_por_id_139ff584; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX documentos_documento_cargado_por_id_139ff584 ON public.documentos_documento USING btree (cargado_por_id);


--
-- Name: documentos_documento_reemplazado_por_id_a4e0c5af; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX documentos_documento_reemplazado_por_id_a4e0c5af ON public.documentos_documento USING btree (reemplazado_por_id);


--
-- Name: documentos_documento_tramite_id_e4ced3bc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX documentos_documento_tramite_id_e4ced3bc ON public.documentos_documento USING btree (tramite_id);


--
-- Name: firmas_firmadocumento_documento_id_405888ae; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX firmas_firmadocumento_documento_id_405888ae ON public.firmas_firmadocumento USING btree (documento_id);


--
-- Name: firmas_firmadocumento_firmado_por_id_6d36556e; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX firmas_firmadocumento_firmado_por_id_6d36556e ON public.firmas_firmadocumento USING btree (firmado_por_id);


--
-- Name: idx_aeronave_matricula; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aeronave_matricula ON public.aocr_tbaeronave USING btree (matricula);


--
-- Name: idx_aeronave_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aeronave_solicitud ON public.aocr_tbaeronave USING btree (codigo_solicitud);


--
-- Name: idx_aocr_asignacion_rt_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_asignacion_rt_solicitud ON public.aocr_asignacion_rt USING btree (codigo_solicitud);


--
-- Name: idx_aocr_declaracion_tmp_email; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_declaracion_tmp_email ON public.aocr_declaracion_tmp USING btree (email);


--
-- Name: idx_aocr_declaracion_tmp_expires; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_declaracion_tmp_expires ON public.aocr_declaracion_tmp USING btree (expires_at);


--
-- Name: idx_aocr_designacion_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_designacion_estado ON public.aocr_tbdesignacion_inspector USING btree (estado);


--
-- Name: idx_aocr_designacion_inspector; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_designacion_inspector ON public.aocr_tbdesignacion_inspector USING btree (inspector_cedula);


--
-- Name: idx_aocr_designacion_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_designacion_solicitud ON public.aocr_tbdesignacion_inspector USING btree (solicitud_id);


--
-- Name: idx_aocr_doc_inspector_bandeja; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_doc_inspector_bandeja ON public.aocr_tbdocumento_generado USING btree (codigo_inspector, codigo_solicitud, codigo_inspeccion, estado) WHERE ((vigente = true) AND (eliminado = false));


--
-- Name: idx_aocr_documento_generado_solicitud_tipo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_documento_generado_solicitud_tipo ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, tipo_documento, fecha_generacion DESC);


--
-- Name: idx_aocr_documento_pendiente_firma_dcav; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_documento_pendiente_firma_dcav ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, codigo_documento, version) WHERE ((vigente = true) AND (eliminado = false) AND ((estado)::text = 'PENDIENTE_FIRMA_DCAV'::text));


--
-- Name: idx_aocr_documento_pendiente_firma_dgac; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_documento_pendiente_firma_dgac ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, codigo_documento, version) WHERE ((vigente = true) AND (eliminado = false) AND ((estado)::text = 'PENDIENTE_FIRMA_DGAC'::text));


--
-- Name: idx_aocr_estado_firma_institucional; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_estado_firma_institucional ON public.aocr_proceso_estado USING btree (fecha_estado, solicitud_id, inspeccion_id) WHERE ((activo = true) AND ((estado_actual)::text = 'PENDIENTE_FIRMA_DIRDAC'::text));


--
-- Name: idx_aocr_estado_firmas_institucionales; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_estado_firmas_institucionales ON public.aocr_proceso_estado USING btree (fecha_estado, solicitud_id, inspeccion_id) WHERE ((activo = true) AND ((estado_actual)::text = 'PENDIENTE_FIRMAS_INSTITUCIONALES'::text));


--
-- Name: idx_aocr_expediente_compania_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_expediente_compania_solicitud ON public.aocr_expediente_archivo USING btree (compania_id, solicitud_id);


--
-- Name: idx_aocr_expediente_fr3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_expediente_fr3 ON public.aocr_expediente_archivo USING btree (fr3_operacion_id);


--
-- Name: idx_aocr_expediente_sha256; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_expediente_sha256 ON public.aocr_expediente_archivo USING btree (sha256);


--
-- Name: idx_aocr_firma_documento_hash; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_firma_documento_hash ON public.aocr_tbfirma_documento USING btree (hash_documento);


--
-- Name: idx_aocr_firma_documento_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_firma_documento_solicitud ON public.aocr_tbfirma_documento USING btree (codigo_solicitud, fecha_firma DESC);


--
-- Name: idx_aocr_firma_posicion_documento_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_firma_posicion_documento_solicitud ON public.aocr_tbfirma_posicion_documento USING btree (codigo_solicitud, updated_at DESC);


--
-- Name: idx_aocr_fr3_integracion_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_fr3_integracion_orden ON public.aocr_fr3_integracion USING btree (orden_recaudacion_id);


--
-- Name: idx_aocr_hist_aprobacion_documentos_dcav; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_hist_aprobacion_documentos_dcav ON public.aocr_proceso_estado_historial USING btree (solicitud_id, inspeccion_id, fecha_creacion DESC, id DESC) WHERE (((accion)::text = 'APROBAR_DOCUMENTOS_DCAV'::text) AND ((estado_nuevo)::text = 'PENDIENTE_FIRMA_DIRDAC'::text));


--
-- Name: idx_aocr_observacion_dcav_documental; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_observacion_dcav_documental ON public.aocr_tbobservacion USING btree (codigo_solicitud, fecha_registro DESC, codigo_observacion DESC) WHERE (mensaje ~~ '{%DCAV_DOCUMENTAL_V1%'::text);


--
-- Name: INDEX idx_aocr_observacion_dcav_documental; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON INDEX public.idx_aocr_observacion_dcav_documental IS 'Lectura selectiva de observaciones documentales DCAV almacenadas en mensaje JSON; no altera datos ni esquema funcional.';


--
-- Name: idx_aocr_or_detalle_concepto; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_or_detalle_concepto ON public.aocr_or_orden_detalle USING btree (concepto_id);


--
-- Name: idx_aocr_or_detalle_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_or_detalle_orden ON public.aocr_or_orden_detalle USING btree (orden_id);


--
-- Name: idx_aocr_or_orden_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_or_orden_estado ON public.aocr_or_orden USING btree (estado);


--
-- Name: idx_aocr_or_orden_id_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_or_orden_id_usuario ON public.aocr_or_orden USING btree (codigo_usuario);


--
-- Name: idx_aocr_or_orden_numero_fr3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_or_orden_numero_fr3 ON public.aocr_or_orden USING btree (numero_fr3);


--
-- Name: idx_aocr_orden_recaudacion_detalle_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_orden_recaudacion_detalle_orden ON public.aocr_orden_recaudacion_detalle USING btree (orden_recaudacion_id);


--
-- Name: idx_aocr_pago_comprobante_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_pago_comprobante_estado ON public.aocr_pago_comprobante USING btree (estado_pago);


--
-- Name: idx_aocr_pago_comprobante_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_pago_comprobante_orden ON public.aocr_pago_comprobante USING btree (orden_recaudacion_id);


--
-- Name: idx_aocr_tb_factura_pago_fecha_emision; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tb_factura_pago_fecha_emision ON public.aocr_tb_factura_pago USING btree (fecha_emision);


--
-- Name: idx_aocr_tb_factura_pago_fr3_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tb_factura_pago_fr3_estado ON public.aocr_tb_factura_pago USING btree (fr3_estado);


--
-- Name: idx_aocr_tb_factura_pago_fr3_numero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tb_factura_pago_fr3_numero ON public.aocr_tb_factura_pago USING btree (fr3_numero);


--
-- Name: idx_aocr_tb_factura_pago_orden_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tb_factura_pago_orden_id ON public.aocr_tb_factura_pago USING btree (orden_id);


--
-- Name: idx_aocr_tb_sync_log_fr3_numero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tb_sync_log_fr3_numero ON public.aocr_tb_sync_log USING btree (fr3_numero);


--
-- Name: idx_aocr_tb_sync_log_orden_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tb_sync_log_orden_estado ON public.aocr_tb_sync_log USING btree (orden_id, estado, created_at DESC);


--
-- Name: idx_aocr_tbauditoria_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tbauditoria_fecha ON public.aocr_tbauditoria USING btree (fecha);


--
-- Name: idx_aocr_tbauditoria_modulo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tbauditoria_modulo ON public.aocr_tbauditoria USING btree (modulo);


--
-- Name: idx_aocr_tbauditoria_usuario_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tbauditoria_usuario_id ON public.aocr_tbauditoria USING btree (usuario_id);


--
-- Name: idx_aocr_tbinspectores_cedula; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tbinspectores_cedula ON public.aocr_tbinspectores USING btree (cedula);


--
-- Name: idx_aocr_tbinspectores_estado_tipo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_tbinspectores_estado_tipo ON public.aocr_tbinspectores USING btree (estado, tipo);


--
-- Name: idx_aocr_usuario_compania_rt_usuario_codigo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_usuario_compania_rt_usuario_codigo ON public.aocr_usuario_compania_rt USING btree (usuario_id, compania_codigo);


--
-- Name: idx_aocr_usuario_compania_rt_usuario_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_usuario_compania_rt_usuario_id ON public.aocr_usuario_compania_rt USING btree (usuario_id);


--
-- Name: idx_aocr_usuario_compania_rt_usuoid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_usuario_compania_rt_usuoid ON public.aocr_usuario_compania_rt USING btree (usuoid);


--
-- Name: idx_aocr_usuario_interno_rt_opcaer; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_usuario_interno_rt_opcaer ON public.aocr_usuario_interno_rt USING btree (opcaer);


--
-- Name: idx_aocr_usuario_interno_rt_rol_interno; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_usuario_interno_rt_rol_interno ON public.aocr_usuario_interno_rt USING btree (rol_interno);


--
-- Name: idx_aocr_usuario_interno_rt_usuario_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_aocr_usuario_interno_rt_usuario_id ON public.aocr_usuario_interno_rt USING btree (usuario_id);


--
-- Name: idx_audit_accion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_audit_accion ON public.aocr_audit_trail USING btree (accion);


--
-- Name: idx_audit_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_audit_fecha ON public.aocr_audit_trail USING btree (fecha_creacion);


--
-- Name: idx_audit_modulo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_audit_modulo ON public.aocr_audit_trail USING btree (modulo);


--
-- Name: idx_audit_tabla; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_audit_tabla ON public.aocr_audit_trail USING btree (tabla, registro_id);


--
-- Name: idx_audit_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_audit_usuario ON public.aocr_audit_trail USING btree (usuario_id);


--
-- Name: idx_auditoria_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_auditoria_fecha ON public.aocr_tbauditoria USING btree (fecha_hora DESC);


--
-- Name: idx_auditoria_tabla; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_auditoria_tabla ON public.aocr_tbauditoria USING btree (tabla_afectada);


--
-- Name: idx_auditoria_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_auditoria_usuario ON public.aocr_tbauditoria USING btree (usuario);


--
-- Name: idx_certificado_numero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_certificado_numero ON public.aocr_tbcertificado USING btree (numero_certificado);


--
-- Name: idx_certificado_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_certificado_solicitud ON public.aocr_tbcertificado USING btree (codigo_solicitud);


--
-- Name: idx_checklist_inspeccion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_checklist_inspeccion ON public.aocr_tbchecklist USING btree (codigo_inspeccion);


--
-- Name: idx_detalles_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_detalles_orden ON public.detalles_orden USING btree (orden_id);


--
-- Name: idx_doc_subsanacion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_doc_subsanacion ON public.aocr_tbdocumento_subsanacion USING btree (codigo_subsanacion);


--
-- Name: idx_documento_inspeccion_base; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documento_inspeccion_base ON public.aocr_tbdocumento_inspeccion USING btree (codigo_documento_base, version DESC);


--
-- Name: idx_documento_inspeccion_codigo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documento_inspeccion_codigo ON public.aocr_tbdocumento_inspeccion USING btree (codigo_inspeccion, created_at DESC);


--
-- Name: idx_documento_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documento_solicitud ON public.aocr_tbdocumento USING btree (codigo_solicitud);


--
-- Name: idx_documento_tipo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_documento_tipo ON public.aocr_tbdocumento USING btree (tipo_documento);


--
-- Name: idx_email_attachment_queue_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_attachment_queue_id ON public.email_attachment USING btree (email_queue_id);


--
-- Name: idx_email_proximo_intento; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_proximo_intento ON public.email_queue USING btree (proximo_intento);


--
-- Name: idx_email_queue_created; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_created ON public.email_queue USING btree (created_at);


--
-- Name: idx_email_queue_created_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_created_at ON public.email_queue USING btree (created_at);


--
-- Name: idx_email_queue_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_orden ON public.email_queue USING btree (orden_id);


--
-- Name: idx_email_queue_proximo_intento; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_proximo_intento ON public.email_queue USING btree (proximo_intento);


--
-- Name: idx_email_queue_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_solicitud ON public.email_queue USING btree (solicitud_id);


--
-- Name: idx_email_queue_solicitud_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_solicitud_id ON public.email_queue USING btree (solicitud_id);


--
-- Name: idx_email_queue_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_status ON public.email_queue USING btree (status, proximo_intento) WHERE ((status)::text = 'PENDIENTE'::text);


--
-- Name: idx_email_queue_status_next; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_email_queue_status_next ON public.email_queue USING btree (status, proximo_intento);


--
-- Name: idx_fr3_outbox_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fr3_outbox_estado ON public.aocr_fr3_outbox USING btree (estado);


--
-- Name: idx_fr3_outbox_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fr3_outbox_orden ON public.aocr_fr3_outbox USING btree (orden_id);


--
-- Name: idx_fr3_outbox_proximo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fr3_outbox_proximo ON public.aocr_fr3_outbox USING btree (proximo_intento) WHERE ((estado)::text = 'PENDIENTE'::text);


--
-- Name: idx_fr3_retry_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fr3_retry_estado ON public.aocr_fr3_retry_queue USING btree (estado);


--
-- Name: idx_fr3_retry_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fr3_retry_orden ON public.aocr_fr3_retry_queue USING btree (orden_id);


--
-- Name: idx_fr3_retry_proximo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_fr3_retry_proximo ON public.aocr_fr3_retry_queue USING btree (proximo_intento) WHERE ((estado)::text = ANY ((ARRAY['PENDIENTE'::character varying, 'EN_PROCESO'::character varying])::text[]));


--
-- Name: idx_hallazgo_inspeccion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_hallazgo_inspeccion ON public.aocr_tbhallazgo USING btree (codigo_inspeccion, fecha_deteccion DESC);


--
-- Name: idx_hist_inspeccion_estado_inspeccion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_hist_inspeccion_estado_inspeccion ON public.aocr_tbhistorial_estado_inspeccion USING btree (codigo_inspeccion, fecha_cambio DESC);


--
-- Name: idx_historial_documental_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_historial_documental_solicitud ON public.aocr_tbhistorial_documental USING btree (codigo_solicitud, fecha_evento DESC, created_at DESC);


--
-- Name: idx_historial_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_historial_orden ON public.historial_estados_orden USING btree (orden_id);


--
-- Name: idx_historial_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_historial_solicitud ON public.aocr_tbhistorial_estado USING btree (codigo_solicitud);


--
-- Name: idx_idempotency_clave; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_idempotency_clave ON public.aocr_idempotency_key USING btree (clave);


--
-- Name: idx_idempotency_expiracion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_idempotency_expiracion ON public.aocr_idempotency_key USING btree (fecha_expiracion);


--
-- Name: idx_idempotency_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_idempotency_orden ON public.aocr_idempotency_key USING btree (orden_id);


--
-- Name: idx_informe_inspeccion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_informe_inspeccion ON public.aocr_tbinforme USING btree (codigo_inspeccion);


--
-- Name: idx_informe_inspeccion_codigo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_informe_inspeccion_codigo ON public.aocr_tbinforme_inspeccion USING btree (codigo_inspeccion, version DESC);


--
-- Name: idx_informe_inspeccion_estacion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_informe_inspeccion_estacion ON public.aocr_tbinforme_inspeccion USING btree (codigo_inspeccion, estacion_id, version DESC);


--
-- Name: idx_inspeccion_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_inspeccion_estado ON public.aocr_tbinspeccion USING btree (estado) WHERE (estado IS NOT NULL);


--
-- Name: INDEX idx_inspeccion_estado; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON INDEX public.idx_inspeccion_estado IS 'Indice para optimizar consultas de inspecciones por estado';


--
-- Name: idx_inspeccion_inspector; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_inspeccion_inspector ON public.aocr_tbinspeccion USING btree (codigo_inspector);


--
-- Name: idx_inspeccion_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_inspeccion_solicitud ON public.aocr_tbinspeccion USING btree (codigo_solicitud);


--
-- Name: idx_log_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_log_fecha ON public.aocr_tblog USING btree (fecha_registro DESC);


--
-- Name: idx_log_nivel; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_log_nivel ON public.aocr_tblog USING btree (nivel);


--
-- Name: idx_noconf_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_noconf_estado ON public.aocr_tbnoconformidad USING btree (estado);


--
-- Name: idx_noconf_informe; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_noconf_informe ON public.aocr_tbnoconformidad USING btree (codigo_informe);


--
-- Name: idx_noconf_inspeccion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_noconf_inspeccion ON public.aocr_tbnoconformidad USING btree (codigo_inspeccion);


--
-- Name: idx_noconf_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_noconf_solicitud ON public.aocr_tbnoconformidad USING btree (codigo_solicitud);


--
-- Name: idx_notificacion_leida; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_notificacion_leida ON public.aocr_tbnotificacion USING btree (leida);


--
-- Name: idx_notificacion_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_notificacion_usuario ON public.aocr_tbnotificacion USING btree (codigousuario);


--
-- Name: idx_observacion_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_observacion_solicitud ON public.aocr_tbobservacion USING btree (codigo_solicitud);


--
-- Name: idx_or_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_or_estado ON public.aocr_or_orden USING btree (estado);


--
-- Name: idx_or_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_or_fecha ON public.aocr_or_orden USING btree (fecha_creacion);


--
-- Name: idx_or_numero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_or_numero ON public.aocr_or_orden USING btree (numero_orden);


--
-- Name: idx_or_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_or_usuario ON public.aocr_or_orden USING btree (codigo_usuario);


--
-- Name: idx_ordenes_contribuyente; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_ordenes_contribuyente ON public.ordenes_recaudacion USING btree (contribuyente_id);


--
-- Name: idx_ordenes_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_ordenes_estado ON public.ordenes_recaudacion USING btree (estado);


--
-- Name: idx_ordenes_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_ordenes_fecha ON public.ordenes_recaudacion USING btree (fecha_creacion);


--
-- Name: idx_ordenes_numero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_ordenes_numero ON public.ordenes_recaudacion USING btree (numero_orden);


--
-- Name: idx_pago_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_pago_estado ON public.aocr_tbpago USING btree (estado);


--
-- Name: idx_pago_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_pago_solicitud ON public.aocr_tbpago USING btree (codigo_solicitud);


--
-- Name: idx_pagos_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_pagos_estado ON public.pagos USING btree (estado);


--
-- Name: idx_pagos_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_pagos_orden ON public.pagos USING btree (orden_id);


--
-- Name: idx_parametro_clave; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_parametro_clave ON public.aocr_tbparametro USING btree (clave);


--
-- Name: idx_parametros_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_parametros_activo ON public.parametros USING btree (activo);


--
-- Name: idx_parametros_clave; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_parametros_clave ON public.parametros USING btree (clave);


--
-- Name: idx_revision_documental_solicitud_doc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_revision_documental_solicitud_doc ON public.aocr_tbrevision_documental USING btree (codigo_solicitud, codigo_documento, fecha_revision DESC, created_at DESC);


--
-- Name: idx_sesion_token; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_sesion_token ON public.aocr_tbsesiones USING btree (token_sesion);


--
-- Name: idx_sesion_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_sesion_usuario ON public.aocr_tbsesiones USING btree (codigousuario);


--
-- Name: idx_solicitud_estacion_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_solicitud_estacion_solicitud ON public.aocr_tbsolicitud_estacion USING btree (solicitud_id) WHERE (activo = true);


--
-- Name: idx_solicitud_estacion_unicidad; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX idx_solicitud_estacion_unicidad ON public.aocr_tbsolicitud_estacion USING btree (solicitud_id, upper((estacion_codigo)::text)) WHERE (activo = true);


--
-- Name: idx_solicitud_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_solicitud_estado ON public.aocr_tbsolicitud USING btree (estado);


--
-- Name: idx_solicitud_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_solicitud_fecha ON public.aocr_tbsolicitud USING btree (fecha_solicitud DESC);


--
-- Name: idx_solicitud_numero; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_solicitud_numero ON public.aocr_tbsolicitud USING btree (numero_solicitud);


--
-- Name: idx_solicitud_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_solicitud_usuario ON public.aocr_tbsolicitud USING btree (codigo_usuario);


--
-- Name: idx_subsanacion_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_subsanacion_estado ON public.aocr_tbsubsanacion USING btree (estado);


--
-- Name: idx_subsanacion_fecha_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_subsanacion_fecha_solicitud ON public.aocr_tbsubsanacion USING btree (fecha_solicitud);


--
-- Name: idx_subsanacion_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_subsanacion_solicitud ON public.aocr_tbsubsanacion USING btree (codigo_solicitud);


--
-- Name: idx_sync_log_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_sync_log_estado ON public.aocr_sync_log USING btree (estado);


--
-- Name: idx_sync_log_idempotency; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_sync_log_idempotency ON public.aocr_sync_log USING btree (idempotency_key) WHERE (idempotency_key IS NOT NULL);


--
-- Name: idx_sync_log_operacion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_sync_log_operacion ON public.aocr_sync_log USING btree (operacion);


--
-- Name: idx_sync_log_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_sync_log_orden ON public.aocr_sync_log USING btree (orden_id);


--
-- Name: idx_usuario_correo_activo_lower; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_usuario_correo_activo_lower ON public.usuario USING btree (lower((correo)::text)) WHERE (correo_liberado = false);


--
-- Name: idx_usuariorol_rol; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_usuariorol_rol ON public.usuario_rol USING btree (codigorol);


--
-- Name: idx_usuariorol_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_usuariorol_usuario ON public.usuario_rol USING btree (codigousuario);


--
-- Name: idx_viatico_inspector; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_viatico_inspector ON public.aocr_tbviatico USING btree (codigo_inspector);


--
-- Name: idx_viatico_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX idx_viatico_solicitud ON public.aocr_tbviatico USING btree (codigo_solicitud);


--
-- Name: importaciones_estado_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX importaciones_estado_index ON public.importaciones USING btree (estado);


--
-- Name: inspecciones_inspecciontecnica_informe_tecnico_id_dfe15e85; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_inspecciontecnica_informe_tecnico_id_dfe15e85 ON public.inspecciones_inspecciontecnica USING btree (informe_tecnico_id);


--
-- Name: inspecciones_inspecciontecnica_inspector_id_cd716503; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_inspecciontecnica_inspector_id_cd716503 ON public.inspecciones_inspecciontecnica USING btree (inspector_id);


--
-- Name: inspecciones_inspecciontecnica_lv_id_940ed1de; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_inspecciontecnica_lv_id_940ed1de ON public.inspecciones_inspecciontecnica USING btree (lv_id);


--
-- Name: inspecciones_inspecciontecnica_solicitud_id_4d00075b; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_inspecciontecnica_solicitud_id_4d00075b ON public.inspecciones_inspecciontecnica USING btree (solicitud_id);


--
-- Name: inspecciones_noconformidad_documento_id_67d4053a; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_noconformidad_documento_id_67d4053a ON public.inspecciones_noconformidad USING btree (documento_id);


--
-- Name: inspecciones_noconformidad_inspeccion_id_9283a51a; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_noconformidad_inspeccion_id_9283a51a ON public.inspecciones_noconformidad USING btree (inspeccion_id);


--
-- Name: inspecciones_noconformidad_revisado_por_coordinador_id_1cb5c764; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_noconformidad_revisado_por_coordinador_id_1cb5c764 ON public.inspecciones_noconformidad USING btree (revisado_por_coordinador_id);


--
-- Name: inspecciones_solicitudinsp_firmado_por_coordinador_id_1b623a69; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_solicitudinsp_firmado_por_coordinador_id_1b623a69 ON public.inspecciones_solicitudinspeccion USING btree (firmado_por_coordinador_id);


--
-- Name: inspecciones_solicitudinspeccion_autorizacion_id_83d21c2e; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_solicitudinspeccion_autorizacion_id_83d21c2e ON public.inspecciones_solicitudinspeccion USING btree (autorizacion_id);


--
-- Name: inspecciones_solicitudinspeccion_factura_id_a3ade54c; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_solicitudinspeccion_factura_id_a3ade54c ON public.inspecciones_solicitudinspeccion USING btree (factura_id);


--
-- Name: inspecciones_solicitudinspeccion_inspector_id_f856a2f3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_solicitudinspeccion_inspector_id_f856a2f3 ON public.inspecciones_solicitudinspeccion USING btree (inspector_id);


--
-- Name: inspecciones_solicitudinspeccion_solicitud_id_21e1b32c; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX inspecciones_solicitudinspeccion_solicitud_id_21e1b32c ON public.inspecciones_solicitudinspeccion USING btree (solicitud_id);


--
-- Name: instrucciones_formales_estado_validation_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX instrucciones_formales_estado_validation_index ON public.instrucciones_formales USING btree (estado_validation);


--
-- Name: instrucciones_formales_numero_registro_senescyt_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX instrucciones_formales_numero_registro_senescyt_index ON public.instrucciones_formales USING btree (numero_registro_senescyt);


--
-- Name: ix_ac12_destinatario_bandeja; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_ac12_destinatario_bandeja ON public.aocr_entrega_destinatario USING btree (usuario_id, tipo_destinatario, estado_bandeja);


--
-- Name: ix_ac12_destinatario_queue; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_ac12_destinatario_queue ON public.aocr_entrega_destinatario USING btree (email_queue_id);


--
-- Name: ix_ac12_documento_lookup; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_ac12_documento_lookup ON public.aocr_entrega_documento USING btree (documento_id, entrega_id) WHERE (vigente = true);


--
-- Name: ix_ac12_entrega_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_ac12_entrega_estado ON public.aocr_entrega_final USING btree (estado, updated_at);


--
-- Name: ix_ac12_intento_entrega; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_ac12_intento_entrega ON public.aocr_entrega_intento USING btree (entrega_id, email_queue_id, fecha);


--
-- Name: ix_aocr_designacion_firmado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_designacion_firmado ON public.aocr_tbdesignacion_inspector USING btree (solicitud_id, firmado, vigente);


--
-- Name: ix_aocr_documento_envio_dcav; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_documento_envio_dcav ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, codigo_inspeccion, estado, version DESC) WHERE ((vigente = true) AND (eliminado = false));


--
-- Name: ix_aocr_documentos_revision_dcav; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_documentos_revision_dcav ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, codigo_inspeccion, estado, codigo_documento) WHERE ((vigente = true) AND (eliminado = false) AND ((estado)::text = 'ENVIADO_DCAV'::text));


--
-- Name: ix_aocr_evento_workflow_correlation; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_evento_workflow_correlation ON public.aocr_evento_workflow USING btree (correlation_id, fecha);


--
-- Name: ix_aocr_evento_workflow_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_evento_workflow_solicitud ON public.aocr_evento_workflow USING btree (solicitud_id, fecha);


--
-- Name: ix_aocr_expediente_compania_compania; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_expediente_compania_compania ON public.aocr_expediente_compania USING btree (compania_codigo);


--
-- Name: ix_aocr_expediente_compania_usuario; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_expediente_compania_usuario ON public.aocr_expediente_compania USING btree (usuario_rt_id);


--
-- Name: ix_aocr_firma_documento_cierre; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_firma_documento_cierre ON public.aocr_tbfirma_documento USING btree (codigo_solicitud, upper((tipo_documento)::text), fecha_firma DESC);


--
-- Name: ix_aocr_historial_envio_documentos_dcav; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_historial_envio_documentos_dcav ON public.aocr_proceso_estado_historial USING btree (solicitud_id, fecha_creacion DESC, id DESC) WHERE ((accion)::text = 'ENVIAR_DOCUMENTOS_DCAV'::text);


--
-- Name: ix_aocr_nc_relaciones_origen; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_nc_relaciones_origen ON public.aocr_tbnoconformidad USING btree (codigo_solicitud_origen, codigo_inspeccion_origen, codigo_informe_origen);


--
-- Name: ix_aocr_nc_relaciones_reevaluacion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_nc_relaciones_reevaluacion ON public.aocr_tbnoconformidad USING btree (codigo_solicitud_nueva, codigo_inspeccion_nueva, codigo_informe_cierre);


--
-- Name: ix_aocr_or_orden_compania_codigo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_or_orden_compania_codigo ON public.aocr_or_orden USING btree (compania_codigo);


--
-- Name: ix_aocr_or_orden_expediente_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_or_orden_expediente_id ON public.aocr_or_orden USING btree (expediente_id);


--
-- Name: ix_aocr_pdf_documento_base; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_pdf_documento_base ON public.aocr_tbdocumento_inspeccion USING btree (codigo_documento_base, codigo_documento);


--
-- Name: ix_aocr_proceso_documentos_dcav; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_proceso_documentos_dcav ON public.aocr_proceso_estado USING btree (estado_actual, fecha_estado DESC) WHERE (activo = true);


--
-- Name: ix_aocr_proceso_estado_bandeja; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_proceso_estado_bandeja ON public.aocr_proceso_estado USING btree (estado_actual, activo, inspeccion_id);


--
-- Name: ix_aocr_proceso_estado_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_proceso_estado_estado ON public.aocr_proceso_estado USING btree (estado_actual);


--
-- Name: ix_aocr_proceso_estado_historial_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_proceso_estado_historial_solicitud ON public.aocr_proceso_estado_historial USING btree (solicitud_id);


--
-- Name: ix_aocr_proceso_estado_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_proceso_estado_solicitud ON public.aocr_proceso_estado USING btree (solicitud_id);


--
-- Name: ix_aocr_reasignacion_inspector_nuevo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_reasignacion_inspector_nuevo ON public.aocr_inspector_reasignacion_historial USING btree (inspector_nuevo_id);


--
-- Name: ix_aocr_reasignacion_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_reasignacion_solicitud ON public.aocr_inspector_reasignacion_historial USING btree (solicitud_id);


--
-- Name: ix_aocr_revdoc_coord_coordinador; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_revdoc_coord_coordinador ON public.aocr_revision_documental_coordinador USING btree (coordinador_id);


--
-- Name: ix_aocr_revdoc_coord_estado; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_revdoc_coord_estado ON public.aocr_revision_documental_coordinador USING btree (estado);


--
-- Name: ix_aocr_revdoc_coord_inspector_conf; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_revdoc_coord_inspector_conf ON public.aocr_revision_documental_coordinador USING btree (inspector_confirmado_id);


--
-- Name: ix_aocr_revdoc_coord_inspector_orig; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_revdoc_coord_inspector_orig ON public.aocr_revision_documental_coordinador USING btree (inspector_original_id);


--
-- Name: ix_aocr_revdoc_coord_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_revdoc_coord_solicitud ON public.aocr_revision_documental_coordinador USING btree (solicitud_id);


--
-- Name: ix_aocr_rt_revision_coordinador; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_rt_revision_coordinador ON public.aocr_solicitud_rt_revision USING btree (coordinador_usuario_id, estado);


--
-- Name: ix_aocr_rt_revision_inspector; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_rt_revision_inspector ON public.aocr_solicitud_rt_revision USING btree (inspector_usuario, estado);


--
-- Name: ix_aocr_tblv_eae_solicitud_estacion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_tblv_eae_solicitud_estacion ON public.aocr_tblv_operacional_eae USING btree (solicitud_id, estacion_id, codigo_inspeccion, version DESC);


--
-- Name: ix_aocr_tblv_eae_vigente_lookup; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_tblv_eae_vigente_lookup ON public.aocr_tblv_operacional_eae USING btree (solicitud_id, COALESCE(estacion_id, 0), tipo_lista) WHERE (vigente = true);


--
-- Name: ix_aocr_tblv_operacional_eae_inspeccion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_tblv_operacional_eae_inspeccion ON public.aocr_tblv_operacional_eae USING btree (codigo_inspeccion, version DESC);


--
-- Name: ix_aocr_usuario_transferencia_destino; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_usuario_transferencia_destino ON public.aocr_usuario_transferencia USING btree (usuario_destino_id);


--
-- Name: ix_aocr_usuario_transferencia_detalle_transferencia; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_usuario_transferencia_detalle_transferencia ON public.aocr_usuario_transferencia_detalle USING btree (transferencia_id);


--
-- Name: ix_aocr_usuario_transferencia_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_usuario_transferencia_fecha ON public.aocr_usuario_transferencia USING btree (fecha DESC);


--
-- Name: ix_aocr_usuario_transferencia_origen; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_aocr_usuario_transferencia_origen ON public.aocr_usuario_transferencia USING btree (usuario_origen_id);


--
-- Name: ix_auditoria_seguridad_accion; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_auditoria_seguridad_accion ON public.auditoria_seguridad USING btree (accion, fecha DESC);


--
-- Name: ix_auditoria_seguridad_actor; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_auditoria_seguridad_actor ON public.auditoria_seguridad USING btree (actor_usuario_id, fecha DESC);


--
-- Name: ix_auditoria_seguridad_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_auditoria_seguridad_fecha ON public.auditoria_seguridad USING btree (fecha DESC);


--
-- Name: ix_cl_estado_vigente; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cl_estado_vigente ON public.aocr_tbcondiciones_limitaciones USING btree (estado, vigente);


--
-- Name: ix_cl_solicitud_fecha; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_cl_solicitud_fecha ON public.aocr_tbcondiciones_limitaciones USING btree (codigo_solicitud, fecha_generacion DESC);


--
-- Name: ix_seguridad_permiso_modulo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_seguridad_permiso_modulo ON public.seguridad_permiso USING btree (modulo);


--
-- Name: ix_seguridad_rol_permiso_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_seguridad_rol_permiso_activo ON public.seguridad_rol_permiso USING btree (activo);


--
-- Name: ix_seguridad_rol_permiso_permiso; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_seguridad_rol_permiso_permiso ON public.seguridad_rol_permiso USING btree (id_permiso);


--
-- Name: ix_usuario_as400_ciudad; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_usuario_as400_ciudad ON public.usuario_as400 USING btree (usuco5);


--
-- Name: ix_usuario_as400_correo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_usuario_as400_correo ON public.usuario_as400 USING btree (usucor);


--
-- Name: ix_usuario_as400_dependencia; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_usuario_as400_dependencia ON public.usuario_as400 USING btree (usuco6);


--
-- Name: jobs_queue_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX jobs_queue_index ON public.jobs USING btree (queue);


--
-- Name: model_has_permissions_model_id_model_type_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX model_has_permissions_model_id_model_type_index ON public.model_has_permissions USING btree (model_id, model_type);


--
-- Name: model_has_roles_model_id_model_type_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX model_has_roles_model_id_model_type_index ON public.model_has_roles USING btree (model_id, model_type);


--
-- Name: modificacion_cyl_aeropuert_created_by_id_6c7988ce; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_aeropuert_created_by_id_6c7988ce ON public.modificacion_cyl_aeropuertomodificacioncyl USING btree (created_by_id);


--
-- Name: modificacion_cyl_aeropuert_solicitud_id_1eb406c5; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_aeropuert_solicitud_id_1eb406c5 ON public.modificacion_cyl_aeropuertomodificacioncyl USING btree (solicitud_id);


--
-- Name: modificacion_cyl_documento_cargado_por_id_3b37782c; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_cargado_por_id_3b37782c ON public.modificacion_cyl_documentomodificacioncyl USING btree (cargado_por_id);


--
-- Name: modificacion_cyl_documento_created_by_id_a675b9b3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_created_by_id_a675b9b3 ON public.modificacion_cyl_documentomodificacioncyl USING btree (created_by_id);


--
-- Name: modificacion_cyl_documento_documento_base_id_0388a0ab; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_documento_base_id_0388a0ab ON public.modificacion_cyl_documentomodificacioncyl USING btree (documento_base_id);


--
-- Name: modificacion_cyl_documento_firmado_por_id_d5ba4db0; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_firmado_por_id_d5ba4db0 ON public.modificacion_cyl_documentogeneradomodificacioncyl USING btree (firmado_por_id);


--
-- Name: modificacion_cyl_documento_generado_por_id_8537fd62; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_generado_por_id_8537fd62 ON public.modificacion_cyl_documentogeneradomodificacioncyl USING btree (generado_por_id);


--
-- Name: modificacion_cyl_documento_revisado_por_id_f7525581; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_revisado_por_id_f7525581 ON public.modificacion_cyl_documentomodificacioncyl USING btree (revisado_por_id);


--
-- Name: modificacion_cyl_documento_solicitud_id_36b9ef8d; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_solicitud_id_36b9ef8d ON public.modificacion_cyl_documentogeneradomodificacioncyl USING btree (solicitud_id);


--
-- Name: modificacion_cyl_documento_updated_by_id_a7919936; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documento_updated_by_id_a7919936 ON public.modificacion_cyl_documentomodificacioncyl USING btree (updated_by_id);


--
-- Name: modificacion_cyl_documentomodificacioncyl_solicitud_id_b57c7ccc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_documentomodificacioncyl_solicitud_id_b57c7ccc ON public.modificacion_cyl_documentomodificacioncyl USING btree (solicitud_id);


--
-- Name: modificacion_cyl_historial_documento_id_e2fa9e30; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_historial_documento_id_e2fa9e30 ON public.modificacion_cyl_historialdocumentomodificacioncyl USING btree (documento_id);


--
-- Name: modificacion_cyl_historial_solicitud_id_941e89b2; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_historial_solicitud_id_941e89b2 ON public.modificacion_cyl_historialdocumentomodificacioncyl USING btree (solicitud_id);


--
-- Name: modificacion_cyl_historial_usuario_id_e75634cf; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_historial_usuario_id_e75634cf ON public.modificacion_cyl_historialdocumentomodificacioncyl USING btree (usuario_id);


--
-- Name: modificacion_cyl_historialmodificacioncyl_solicitud_id_af26c98d; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_historialmodificacioncyl_solicitud_id_af26c98d ON public.modificacion_cyl_historialmodificacioncyl USING btree (solicitud_id);


--
-- Name: modificacion_cyl_historialmodificacioncyl_usuario_id_b9ccf213; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_historialmodificacioncyl_usuario_id_b9ccf213 ON public.modificacion_cyl_historialmodificacioncyl USING btree (usuario_id);


--
-- Name: modificacion_cyl_revisiond_created_by_id_c27e4e9c; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_revisiond_created_by_id_c27e4e9c ON public.modificacion_cyl_revisiondocumentomodificacioncyl USING btree (created_by_id);


--
-- Name: modificacion_cyl_revisiond_documento_id_dba4c9ac; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_revisiond_documento_id_dba4c9ac ON public.modificacion_cyl_revisiondocumentomodificacioncyl USING btree (documento_id);


--
-- Name: modificacion_cyl_revisiond_revisado_por_id_231c52b3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_revisiond_revisado_por_id_231c52b3 ON public.modificacion_cyl_revisiondocumentomodificacioncyl USING btree (revisado_por_id);


--
-- Name: modificacion_cyl_revisiond_solicitud_id_e378af42; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_revisiond_solicitud_id_e378af42 ON public.modificacion_cyl_revisiondocumentomodificacioncyl USING btree (solicitud_id);


--
-- Name: modificacion_cyl_solicit_numero_solicitud_29c28260_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicit_numero_solicitud_29c28260_like ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (numero_solicitud varchar_pattern_ops);


--
-- Name: modificacion_cyl_solicitud_coordinador_designa_id_c1f8dccb; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_coordinador_designa_id_c1f8dccb ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (coordinador_designa_id);


--
-- Name: modificacion_cyl_solicitud_created_by_id_5163db75; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_created_by_id_5163db75 ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (created_by_id);


--
-- Name: modificacion_cyl_solicitud_decision_nuevo_aeropuerto__0462b183; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_decision_nuevo_aeropuerto__0462b183 ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (decision_nuevo_aeropuerto_por_id);


--
-- Name: modificacion_cyl_solicitud_firmado_por_dcav_id_82a2ebd4; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_firmado_por_dcav_id_82a2ebd4 ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (firmado_por_dcav_id);


--
-- Name: modificacion_cyl_solicitud_inspector_designado_id_3c841970; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_inspector_designado_id_3c841970 ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (inspector_designado_id);


--
-- Name: modificacion_cyl_solicitud_operador_eae_id_4677dcd1; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_operador_eae_id_4677dcd1 ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (operador_eae_id);


--
-- Name: modificacion_cyl_solicitud_updated_by_id_cd9c988f; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitud_updated_by_id_cd9c988f ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (updated_by_id);


--
-- Name: modificacion_cyl_solicitudmodificacioncyl_rt_id_2c3a421d; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX modificacion_cyl_solicitudmodificacioncyl_rt_id_2c3a421d ON public.modificacion_cyl_solicitudmodificacioncyl USING btree (rt_id);


--
-- Name: notificaciones_notificacion_tramite_id_540a3cfe; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX notificaciones_notificacion_tramite_id_540a3cfe ON public.django_aocr_notificacion USING btree (tramite_id);


--
-- Name: notificaciones_notificacion_usuario_destino_id_8dfc66d6; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX notificaciones_notificacion_usuario_destino_id_8dfc66d6 ON public.django_aocr_notificacion USING btree (usuario_destino_id);


--
-- Name: operadores_operador_ruc_74f36999_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_operador_ruc_74f36999_like ON public.operadores_operador USING btree (ruc varchar_pattern_ops);


--
-- Name: operadores_registrort_documento_aceptacion_id_45b042a3; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_registrort_documento_aceptacion_id_45b042a3 ON public.operadores_registrort USING btree (documento_aceptacion_id);


--
-- Name: operadores_registrort_formulario_designacion_id_82fd1a2b; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_registrort_formulario_designacion_id_82fd1a2b ON public.operadores_registrort USING btree (formulario_designacion_id);


--
-- Name: operadores_registrort_operador_id_4199a948; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_registrort_operador_id_4199a948 ON public.operadores_registrort USING btree (operador_id);


--
-- Name: operadores_registrort_revisado_por_id_dc81cfae; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_registrort_revisado_por_id_dc81cfae ON public.operadores_registrort USING btree (revisado_por_id);


--
-- Name: operadores_registrort_usuario_rt_id_878932bc; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_registrort_usuario_rt_id_878932bc ON public.operadores_registrort USING btree (usuario_rt_id);


--
-- Name: operadores_representantetecnico_operador_id_a5042fba; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_representantetecnico_operador_id_a5042fba ON public.operadores_representantetecnico USING btree (operador_id);


--
-- Name: operadores_representantetecnico_registro_rt_id_a675ffbb; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX operadores_representantetecnico_registro_rt_id_a675ffbb ON public.operadores_representantetecnico USING btree (registro_rt_id);


--
-- Name: personas_nombre_completo_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX personas_nombre_completo_index ON public.personas USING btree (nombre_completo);


--
-- Name: recaudacion_comprobantepago_cargado_por_id_67ce7569; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_comprobantepago_cargado_por_id_67ce7569 ON public.recaudacion_comprobantepago USING btree (cargado_por_id);


--
-- Name: recaudacion_comprobantepago_orden_id_366161b9; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_comprobantepago_orden_id_366161b9 ON public.recaudacion_comprobantepago USING btree (orden_id);


--
-- Name: recaudacion_comprobantepago_reemplazado_por_id_329a4009; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_comprobantepago_reemplazado_por_id_329a4009 ON public.recaudacion_comprobantepago USING btree (reemplazado_por_id);


--
-- Name: recaudacion_factura_archivo_pdf_factura_id_400838b0; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_factura_archivo_pdf_factura_id_400838b0 ON public.recaudacion_factura USING btree (archivo_pdf_factura_id);


--
-- Name: recaudacion_factura_generada_por_id_b7284b04; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_factura_generada_por_id_b7284b04 ON public.recaudacion_factura USING btree (generada_por_id);


--
-- Name: recaudacion_factura_numero_factura_41c5422b_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_factura_numero_factura_41c5422b_like ON public.recaudacion_factura USING btree (numero_factura varchar_pattern_ops);


--
-- Name: recaudacion_ordenrecaudacion_creada_por_id_bfcefba5; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_creada_por_id_bfcefba5 ON public.recaudacion_ordenrecaudacion USING btree (creada_por_id);


--
-- Name: recaudacion_ordenrecaudacion_documento_orden_id_660781cd; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_documento_orden_id_660781cd ON public.recaudacion_ordenrecaudacion USING btree (documento_orden_id);


--
-- Name: recaudacion_ordenrecaudacion_numero_orden_709797c6_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_numero_orden_709797c6_like ON public.recaudacion_ordenrecaudacion USING btree (numero_orden varchar_pattern_ops);


--
-- Name: recaudacion_ordenrecaudacion_operador_id_982d95f1; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_operador_id_982d95f1 ON public.recaudacion_ordenrecaudacion USING btree (operador_id);


--
-- Name: recaudacion_ordenrecaudacion_revisado_por_id_9b56e6b8; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_revisado_por_id_9b56e6b8 ON public.recaudacion_ordenrecaudacion USING btree (revisado_por_id);


--
-- Name: recaudacion_ordenrecaudacion_rt_id_79c56475; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_rt_id_79c56475 ON public.recaudacion_ordenrecaudacion USING btree (rt_id);


--
-- Name: recaudacion_ordenrecaudacion_tramite_id_c58d5f82; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX recaudacion_ordenrecaudacion_tramite_id_c58d5f82 ON public.recaudacion_ordenrecaudacion USING btree (tramite_id);


--
-- Name: servidores_estado_servidor_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX servidores_estado_servidor_index ON public.servidores USING btree (estado_servidor);


--
-- Name: sessions_last_activity_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX sessions_last_activity_index ON public.sessions USING btree (last_activity);


--
-- Name: sessions_user_id_index; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX sessions_user_id_index ON public.sessions USING btree (user_id);


--
-- Name: solicitudes_documentosolicitud_documento_id_e4c03fb4; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_documentosolicitud_documento_id_e4c03fb4 ON public.solicitudes_documentosolicitud USING btree (documento_id);


--
-- Name: solicitudes_documentosolicitud_revisado_por_id_089b611e; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_documentosolicitud_revisado_por_id_089b611e ON public.solicitudes_documentosolicitud USING btree (revisado_por_id);


--
-- Name: solicitudes_documentosolicitud_solicitud_id_8e078eef; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_documentosolicitud_solicitud_id_8e078eef ON public.solicitudes_documentosolicitud USING btree (solicitud_id);


--
-- Name: solicitudes_solicitudaocr_cierre_fase_documental_id_9adaaed9; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_cierre_fase_documental_id_9adaaed9 ON public.solicitudes_solicitudaocr USING btree (cierre_fase_documental_id);


--
-- Name: solicitudes_solicitudaocr_coordinador_id_42f37cd4; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_coordinador_id_42f37cd4 ON public.solicitudes_solicitudaocr USING btree (coordinador_id);


--
-- Name: solicitudes_solicitudaocr_coordinador_revisor_id_5e1b80c0; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_coordinador_revisor_id_5e1b80c0 ON public.solicitudes_solicitudaocr USING btree (coordinador_revisor_id);


--
-- Name: solicitudes_solicitudaocr_dcav_firmante_id_ad5d2d1b; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_dcav_firmante_id_ad5d2d1b ON public.solicitudes_solicitudaocr USING btree (dcav_firmante_id);


--
-- Name: solicitudes_solicitudaocr_documento_condiciones_gene_efe26db2; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_documento_condiciones_gene_efe26db2 ON public.solicitudes_solicitudaocr USING btree (documento_condiciones_generado_id);


--
-- Name: solicitudes_solicitudaocr_inspector_id_542fca3c; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_inspector_id_542fca3c ON public.solicitudes_solicitudaocr USING btree (inspector_id);


--
-- Name: solicitudes_solicitudaocr_operador_id_89813004; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_operador_id_89813004 ON public.solicitudes_solicitudaocr USING btree (operador_id);


--
-- Name: solicitudes_solicitudaocr_orden_recaudacion_id_7eb6376b; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_orden_recaudacion_id_7eb6376b ON public.solicitudes_solicitudaocr USING btree (orden_recaudacion_id);


--
-- Name: solicitudes_solicitudaocr_rt_id_926f0520; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX solicitudes_solicitudaocr_rt_id_926f0520 ON public.solicitudes_solicitudaocr USING btree (rt_id);


--
-- Name: tramites_historialestado_tramite_id_a475ddb0; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tramites_historialestado_tramite_id_a475ddb0 ON public.tramites_historialestado USING btree (tramite_id);


--
-- Name: tramites_historialestado_usuario_id_55b8c026; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tramites_historialestado_usuario_id_55b8c026 ON public.tramites_historialestado USING btree (usuario_id);


--
-- Name: tramites_tramite_creado_por_id_0a28b2d6; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tramites_tramite_creado_por_id_0a28b2d6 ON public.tramites_tramite USING btree (creado_por_id);


--
-- Name: tramites_tramite_numero_tramite_373bbd12_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tramites_tramite_numero_tramite_373bbd12_like ON public.tramites_tramite USING btree (numero_tramite varchar_pattern_ops);


--
-- Name: tramites_tramite_operador_id_6466b484; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tramites_tramite_operador_id_6466b484 ON public.tramites_tramite USING btree (operador_id);


--
-- Name: tramites_tramite_responsable_actual_id_09c15818; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX tramites_tramite_responsable_actual_id_09c15818 ON public.tramites_tramite USING btree (responsable_actual_id);


--
-- Name: uix_aocr_usuario_interno_rt_codigo_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uix_aocr_usuario_interno_rt_codigo_activo ON public.aocr_usuario_interno_rt USING btree (upper(TRIM(BOTH FROM codigo_usuario))) WHERE (activo = true);


--
-- Name: uix_aocr_usuario_interno_rt_correo_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uix_aocr_usuario_interno_rt_correo_activo ON public.aocr_usuario_interno_rt USING btree (lower(TRIM(BOTH FROM correo_institucional))) WHERE ((activo = true) AND (correo_institucional IS NOT NULL) AND (btrim((correo_institucional)::text) <> ''::text));


--
-- Name: uix_aocr_usuario_interno_rt_tecnico_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uix_aocr_usuario_interno_rt_tecnico_activo ON public.aocr_usuario_interno_rt USING btree (tecnico_id) WHERE ((activo = true) AND (tecnico_id IS NOT NULL));


--
-- Name: uq_aocr_designacion_vigente; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_designacion_vigente ON public.aocr_tbdesignacion_inspector USING btree (solicitud_id, COALESCE(estacion_id, 0)) WHERE (vigente = true);


--
-- Name: uq_aocr_doc_vigente_expediente_tipo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_doc_vigente_expediente_tipo ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, codigo_inspeccion, upper(TRIM(BOTH FROM tipo_documento))) WHERE ((vigente = true) AND (eliminado = false) AND (codigo_inspeccion IS NOT NULL) AND (upper(TRIM(BOTH FROM tipo_documento)) = ANY (ARRAY['RECONOCIMIENTO'::text, 'CONDICIONES_LIMITACIONES'::text])));


--
-- Name: uq_aocr_firma_institucional_vigente; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_firma_institucional_vigente ON public.aocr_tbfirma_documento USING btree (codigo_solicitud, codigo_inspeccion, upper((tipo_documento)::text), version) WHERE ((estado_documento)::text = ANY (ARRAY[('FIRMADO_DGAC'::character varying)::text, ('FIRMADO_DCAV'::character varying)::text]));


--
-- Name: uq_aocr_firma_posicion_documento; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_firma_posicion_documento ON public.aocr_tbfirma_posicion_documento USING btree (codigo_solicitud, tipo_documento, rol_firmante);


--
-- Name: uq_aocr_fr3_integracion_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_fr3_integracion_orden ON public.aocr_fr3_integracion USING btree (orden_recaudacion_id);


--
-- Name: uq_aocr_fr3_operacion_clave_as400; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_fr3_operacion_clave_as400 ON public.aocr_fr3_operacion USING btree (aeropuerto, anio, secuencial) WHERE (secuencial IS NOT NULL);


--
-- Name: uq_aocr_fr3_operacion_correlation; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_fr3_operacion_correlation ON public.aocr_fr3_operacion USING btree (correlation_id);


--
-- Name: uq_aocr_fr3_operacion_idempotency; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_fr3_operacion_idempotency ON public.aocr_fr3_operacion USING btree (idempotency_key);


--
-- Name: uq_aocr_fr3_tipo_operacion_map_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_fr3_tipo_operacion_map_activo ON public.aocr_fr3_tipo_operacion_map USING btree (COALESCE(tipo_solicitud, '-1'::integer), COALESCE(upper(TRIM(BOTH FROM codigo_concepto)), ''::text), COALESCE(upper(TRIM(BOTH FROM accion)), ''::text), COALESCE(upper(TRIM(BOTH FROM opc_nac)), ''::text)) WHERE (activo = true);


--
-- Name: uq_aocr_tb_sync_log_idempotency; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_tb_sync_log_idempotency ON public.aocr_tb_sync_log USING btree (idempotency_key);


--
-- Name: uq_aocr_tbinspectores_cedula; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_tbinspectores_cedula ON public.aocr_tbinspectores USING btree (lower(TRIM(BOTH FROM cedula)));


--
-- Name: uq_aocr_tblv_eae_vigente; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_tblv_eae_vigente ON public.aocr_tblv_operacional_eae USING btree (solicitud_id, COALESCE(estacion_id, 0), tipo_lista) WHERE (vigente = true);


--
-- Name: uq_aocr_tbnotificacion_event_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_aocr_tbnotificacion_event_key ON public.aocr_tbnotificacion USING btree (event_key) WHERE (event_key IS NOT NULL);


--
-- Name: uq_email_queue_event_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_email_queue_event_key ON public.email_queue USING btree (event_key) WHERE (event_key IS NOT NULL);


--
-- Name: uq_email_queue_idempotencia; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_email_queue_idempotencia ON public.email_queue USING btree (correlation_id, tipo_notificacion, to_address) WHERE (correlation_id IS NOT NULL);


--
-- Name: uq_informe_inspeccion_version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX uq_informe_inspeccion_version ON public.aocr_tbinforme_inspeccion USING btree (codigo_inspeccion, version);


--
-- Name: usuarios_usuario_groups_group_id_e77f6dcf; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX usuarios_usuario_groups_group_id_e77f6dcf ON public.usuarios_usuario_groups USING btree (group_id);


--
-- Name: usuarios_usuario_groups_usuario_id_7a34077f; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX usuarios_usuario_groups_usuario_id_7a34077f ON public.usuarios_usuario_groups USING btree (usuario_id);


--
-- Name: usuarios_usuario_user_permissions_permission_id_4e5c0f2f; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX usuarios_usuario_user_permissions_permission_id_4e5c0f2f ON public.usuarios_usuario_user_permissions USING btree (permission_id);


--
-- Name: usuarios_usuario_user_permissions_usuario_id_60aeea80; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX usuarios_usuario_user_permissions_usuario_id_60aeea80 ON public.usuarios_usuario_user_permissions USING btree (usuario_id);


--
-- Name: usuarios_usuario_username_be9def2b_like; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX usuarios_usuario_username_be9def2b_like ON public.usuarios_usuario USING btree (username varchar_pattern_ops);


--
-- Name: ux_ac12_destinatario_logico; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_ac12_destinatario_logico ON public.aocr_entrega_destinatario USING btree (entrega_id, tipo_destinatario, usuario_id);


--
-- Name: ux_ac12_email_fisico; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_ac12_email_fisico ON public.email_queue USING btree (event_key) WHERE ((event_key)::text ~~ 'ENTREGA_FINAL:%'::text);


--
-- Name: ux_ac12_entrega_documento; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_ac12_entrega_documento ON public.aocr_entrega_documento USING btree (entrega_id, tipo_documento, version_documento);


--
-- Name: ux_ac12_entrega_event_key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_ac12_entrega_event_key ON public.aocr_entrega_final USING btree (event_key);


--
-- Name: ux_ac12_entrega_version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_ac12_entrega_version ON public.aocr_entrega_final USING btree (solicitud_id, version_aocr, version_cl);


--
-- Name: ux_aocr_firma_documento_evidencia; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_firma_documento_evidencia ON public.aocr_tbfirma_documento USING btree (codigo_solicitud, upper((tipo_documento)::text), hash_documento) WHERE (NULLIF(TRIM(BOTH FROM hash_documento), ''::text) IS NOT NULL);


--
-- Name: ux_aocr_nc_correlation; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_nc_correlation ON public.aocr_tbnoconformidad USING btree (correlation_id) WHERE (NULLIF(TRIM(BOTH FROM correlation_id), ''::text) IS NOT NULL);


--
-- Name: ux_aocr_nc_numero_version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_nc_numero_version ON public.aocr_tbnoconformidad USING btree (upper(TRIM(BOTH FROM numero_no_conformidad)), version) WHERE (NULLIF(TRIM(BOTH FROM numero_no_conformidad), ''::text) IS NOT NULL);


--
-- Name: ux_aocr_nc_raiz_version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_nc_raiz_version ON public.aocr_tbnoconformidad USING btree (codigo_nc_raiz, version);


--
-- Name: ux_aocr_nc_solicitud_activa_por_raiz; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_nc_solicitud_activa_por_raiz ON public.aocr_tbnoconformidad USING btree (codigo_nc_raiz) WHERE ((codigo_solicitud_nueva IS NOT NULL) AND (fecha_cierre IS NULL));


--
-- Name: ux_aocr_nc_solicitud_nueva; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_nc_solicitud_nueva ON public.aocr_tbnoconformidad USING btree (codigo_solicitud_nueva) WHERE (codigo_solicitud_nueva IS NOT NULL);


--
-- Name: ux_aocr_or_orden_numero_orden; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_or_orden_numero_orden ON public.aocr_or_orden USING btree (numero_orden);


--
-- Name: ux_aocr_original_numero_canonico; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_original_numero_canonico ON public.aocr_tbsolicitud USING btree (numero_aocr) WHERE ((tipo_solicitud = 1) AND ((numero_aocr)::text ~ '^DGAC-129-[A-Z0-9]{3}-[0-9]+$'::text));


--
-- Name: ux_aocr_proceso_estado_solicitud_activo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_proceso_estado_solicitud_activo ON public.aocr_proceso_estado USING btree (solicitud_id) WHERE (activo = true);


--
-- Name: ux_aocr_revdoc_coord_solicitud; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_revdoc_coord_solicitud ON public.aocr_revision_documental_coordinador USING btree (solicitud_id);


--
-- Name: ux_aocr_rt_revision_solicitud_activa; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_rt_revision_solicitud_activa ON public.aocr_solicitud_rt_revision USING btree (solicitud_rt_id) WHERE ((estado)::text <> 'CERRADA'::text);


--
-- Name: ux_aocr_solicitud_numero_institucional; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_aocr_solicitud_numero_institucional ON public.aocr_tbsolicitud USING btree (numero_solicitud) WHERE ((numero_solicitud)::text ~~ 'DGAC-GOP-%'::text);


--
-- Name: ux_cl_solicitud_version; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_cl_solicitud_version ON public.aocr_tbcondiciones_limitaciones USING btree (codigo_solicitud, version);


--
-- Name: ux_cl_solicitud_vigente; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_cl_solicitud_vigente ON public.aocr_tbcondiciones_limitaciones USING btree (codigo_solicitud) WHERE (vigente = true);


--
-- Name: ux_documento_final_vigente; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_documento_final_vigente ON public.aocr_tbdocumento_generado USING btree (codigo_solicitud, upper((tipo_documento)::text)) WHERE (vigente = true);


--
-- Name: ux_or_correccion_pendiente; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_or_correccion_pendiente ON public.aocr_or_correccion_financiera USING btree (orden_id) WHERE (estado <> 'VALIDADA'::text);


--
-- Name: ux_usuario_codigousuario; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_usuario_codigousuario ON public.usuario USING btree (codigousuario);


--
-- Name: ix_sync_batch_log_table_started; Type: INDEX; Schema: sync; Owner: -
--

CREATE INDEX ix_sync_batch_log_table_started ON sync.batch_log USING btree (table_name, started_at DESC);


--
-- Name: ix_sync_rejections_batch; Type: INDEX; Schema: sync; Owner: -
--

CREATE INDEX ix_sync_rejections_batch ON sync.rejections USING btree (batch_id, table_name, created_at DESC);


--
-- Name: ix_sync_tombstones_pending; Type: INDEX; Schema: sync; Owner: -
--

CREATE INDEX ix_sync_tombstones_pending ON sync.tombstones USING btree (table_name, applied, created_at);


--
-- Name: ux_sync_batch_log_batch_id; Type: INDEX; Schema: sync; Owner: -
--

CREATE UNIQUE INDEX ux_sync_batch_log_batch_id ON sync.batch_log USING btree (batch_id);


--
-- Name: aocr_orden_recaudacion aocr_or_calc_aiu; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER aocr_or_calc_aiu AFTER INSERT OR UPDATE OF concepto_id, cantidad ON public.aocr_orden_recaudacion FOR EACH ROW EXECUTE FUNCTION public.trg_aocr_or_calcular();


--
-- Name: aocr_tbsolicitud audit_solicitud; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER audit_solicitud AFTER INSERT OR DELETE OR UPDATE ON public.aocr_tbsolicitud FOR EACH ROW EXECUTE FUNCTION public.fn_aocr_auditoria();


--
-- Name: aocr_tbsolicitud set_numero_solicitud; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER set_numero_solicitud BEFORE INSERT ON public.aocr_tbsolicitud FOR EACH ROW EXECUTE FUNCTION public.trg_asignar_numero_solicitud();


--
-- Name: aocr_tbauditoria trg_aocr_audit_sync_fecha; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_audit_sync_fecha BEFORE INSERT OR UPDATE ON public.aocr_tbauditoria FOR EACH ROW EXECUTE FUNCTION public.aocr_audit_insert_compat();


--
-- Name: aocr_tbauditoria trg_aocr_auditoria_estaciones_compat; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_auditoria_estaciones_compat BEFORE INSERT ON public.aocr_tbauditoria FOR EACH ROW EXECUTE FUNCTION public.aocr_auditoria_estaciones_compat();


--
-- Name: aocr_tbnoconformidad trg_aocr_nc_asignar_raiz; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_nc_asignar_raiz BEFORE INSERT ON public.aocr_tbnoconformidad FOR EACH ROW EXECUTE FUNCTION public.aocr_fn_nc_asignar_raiz();


--
-- Name: aocr_tbsolicitud trg_aocr_numero_gop; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_numero_gop BEFORE UPDATE OF numero_solicitud ON public.aocr_tbsolicitud FOR EACH ROW EXECUTE FUNCTION public.aocr_validar_numero_gop_or();


--
-- Name: aocr_or_orden trg_aocr_numero_or; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_numero_or BEFORE INSERT OR UPDATE OF numero_orden, codigo_solicitud ON public.aocr_or_orden FOR EACH ROW EXECUTE FUNCTION public.aocr_validar_numero_gop_or();


--
-- Name: aocr_tbsolicitud trg_aocr_numero_original_inmutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_numero_original_inmutable BEFORE UPDATE OF numero_aocr ON public.aocr_tbsolicitud FOR EACH ROW EXECUTE FUNCTION public.aocr_conservar_numero_original();


--
-- Name: aocr_or_orden_detalle trg_aocr_or_detalle_after_recalc; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_or_detalle_after_recalc AFTER INSERT OR DELETE OR UPDATE ON public.aocr_or_orden_detalle FOR EACH ROW EXECUTE FUNCTION public.aocr_or_detalle_after_recalc();


--
-- Name: aocr_or_orden_detalle trg_aocr_or_detalle_calcular_linea; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_aocr_or_detalle_calcular_linea BEFORE INSERT OR UPDATE ON public.aocr_or_orden_detalle FOR EACH ROW EXECUTE FUNCTION public.aocr_or_detalle_calcular_linea();


--
-- Name: aocr_tbsubsanacion trg_audit_subsanacion; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_audit_subsanacion BEFORE UPDATE ON public.aocr_tbsubsanacion FOR EACH ROW EXECUTE FUNCTION public.fn_audit_subsanacion();


--
-- Name: aocr_or_orden trg_auto_vincular_solicitud; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_auto_vincular_solicitud AFTER INSERT ON public.aocr_or_orden FOR EACH ROW EXECUTE FUNCTION public.fn_vincular_solicitud_por_ruc();


--
-- Name: aocr_tbsolicitud trg_cambio_estado; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_cambio_estado AFTER UPDATE ON public.aocr_tbsolicitud FOR EACH ROW EXECUTE FUNCTION public.fn_registrar_cambio_estado();


--
-- Name: acciones_personal acciones_personal_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.acciones_personal
    ADD CONSTRAINT acciones_personal_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: acciones_personal acciones_personal_tipo_movimiento_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.acciones_personal
    ADD CONSTRAINT acciones_personal_tipo_movimiento_id_foreign FOREIGN KEY (tipo_movimiento_id) REFERENCES public.tipos_movimiento(id) ON DELETE RESTRICT;


--
-- Name: accounts_perfil_usuario accounts_perfil_usuario_user_id_b2df1649_fk_usuarios_usuario_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_perfil_usuario
    ADD CONSTRAINT accounts_perfil_usuario_user_id_b2df1649_fk_usuarios_usuario_id FOREIGN KEY (user_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: accounts_sesion_compania_activa accounts_sesion_comp_compania_id_ca3078b3_fk_accounts_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_sesion_compania_activa
    ADD CONSTRAINT accounts_sesion_comp_compania_id_ca3078b3_fk_accounts_ FOREIGN KEY (compania_id) REFERENCES public.accounts_compania(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: accounts_sesion_compania_activa accounts_sesion_comp_user_id_511a9540_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_sesion_compania_activa
    ADD CONSTRAINT accounts_sesion_comp_user_id_511a9540_fk_usuarios_ FOREIGN KEY (user_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: accounts_usuario_compania accounts_usuario_com_compania_id_3a2aaf55_fk_accounts_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_usuario_compania
    ADD CONSTRAINT accounts_usuario_com_compania_id_3a2aaf55_fk_accounts_ FOREIGN KEY (compania_id) REFERENCES public.accounts_compania(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: accounts_usuario_compania accounts_usuario_com_user_id_f192f90b_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.accounts_usuario_compania
    ADD CONSTRAINT accounts_usuario_com_user_id_f192f90b_fk_usuarios_ FOREIGN KEY (user_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: aocr_entrega_destinatario aocr_entrega_destinatario_email_queue_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_destinatario
    ADD CONSTRAINT aocr_entrega_destinatario_email_queue_id_fkey FOREIGN KEY (email_queue_id) REFERENCES public.email_queue(id);


--
-- Name: aocr_entrega_destinatario aocr_entrega_destinatario_entrega_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_destinatario
    ADD CONSTRAINT aocr_entrega_destinatario_entrega_id_fkey FOREIGN KEY (entrega_id) REFERENCES public.aocr_entrega_final(id);


--
-- Name: aocr_entrega_destinatario aocr_entrega_destinatario_usuario_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_destinatario
    ADD CONSTRAINT aocr_entrega_destinatario_usuario_id_fkey FOREIGN KEY (usuario_id) REFERENCES public.usuario(idusuario);


--
-- Name: aocr_entrega_documento aocr_entrega_documento_documento_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_documento
    ADD CONSTRAINT aocr_entrega_documento_documento_id_fkey FOREIGN KEY (documento_id) REFERENCES public.aocr_tbdocumento_generado(codigo_documento);


--
-- Name: aocr_entrega_documento aocr_entrega_documento_entrega_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_documento
    ADD CONSTRAINT aocr_entrega_documento_entrega_id_fkey FOREIGN KEY (entrega_id) REFERENCES public.aocr_entrega_final(id);


--
-- Name: aocr_entrega_final aocr_entrega_final_inspeccion_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_final
    ADD CONSTRAINT aocr_entrega_final_inspeccion_id_fkey FOREIGN KEY (inspeccion_id) REFERENCES public.aocr_tbinspeccion(codigo_inspeccion);


--
-- Name: aocr_entrega_final aocr_entrega_final_solicitud_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_final
    ADD CONSTRAINT aocr_entrega_final_solicitud_id_fkey FOREIGN KEY (solicitud_id) REFERENCES public.aocr_tbsolicitud(codigo_solicitud);


--
-- Name: aocr_entrega_intento aocr_entrega_intento_email_queue_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_intento
    ADD CONSTRAINT aocr_entrega_intento_email_queue_id_fkey FOREIGN KEY (email_queue_id) REFERENCES public.email_queue(id);


--
-- Name: aocr_entrega_intento aocr_entrega_intento_entrega_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_entrega_intento
    ADD CONSTRAINT aocr_entrega_intento_entrega_id_fkey FOREIGN KEY (entrega_id) REFERENCES public.aocr_entrega_final(id);


--
-- Name: aocr_expediente_archivo aocr_expediente_archivo_archivo_anterior_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_archivo
    ADD CONSTRAINT aocr_expediente_archivo_archivo_anterior_id_fkey FOREIGN KEY (archivo_anterior_id) REFERENCES public.aocr_expediente_archivo(id);


--
-- Name: aocr_expediente_archivo aocr_expediente_archivo_fr3_operacion_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_expediente_archivo
    ADD CONSTRAINT aocr_expediente_archivo_fr3_operacion_id_fkey FOREIGN KEY (fr3_operacion_id) REFERENCES public.aocr_fr3_operacion(id);


--
-- Name: aocr_or_correccion_financiera aocr_or_correccion_financiera_orden_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_correccion_financiera
    ADD CONSTRAINT aocr_or_correccion_financiera_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id);


--
-- Name: aocr_orden_recaudacion aocr_orden_recaudacion_concepto_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_orden_recaudacion
    ADD CONSTRAINT aocr_orden_recaudacion_concepto_id_fkey FOREIGN KEY (concepto_id) REFERENCES public.aocr_or_concepto(id);


--
-- Name: aocr_solicitud_rt_revision aocr_solicitud_rt_revision_solicitud_rt_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_solicitud_rt_revision
    ADD CONSTRAINT aocr_solicitud_rt_revision_solicitud_rt_id_fkey FOREIGN KEY (solicitud_rt_id) REFERENCES public.django_aocr_registro_rt(id) ON DELETE CASCADE;


--
-- Name: aocr_tbdesignacion_inspector aocr_tbdesignacion_inspector_estacion_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdesignacion_inspector
    ADD CONSTRAINT aocr_tbdesignacion_inspector_estacion_id_fkey FOREIGN KEY (estacion_id) REFERENCES public.aocr_tbsolicitud_estacion(id);


--
-- Name: aocr_tbdesignacion_inspector aocr_tbdesignacion_inspector_solicitud_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdesignacion_inspector
    ADD CONSTRAINT aocr_tbdesignacion_inspector_solicitud_id_fkey FOREIGN KEY (solicitud_id) REFERENCES public.aocr_tbsolicitud(codigo_solicitud);


--
-- Name: aocr_usuario_interno_rt aocr_usuario_interno_rt_usuario_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_interno_rt
    ADD CONSTRAINT aocr_usuario_interno_rt_usuario_id_fkey FOREIGN KEY (usuario_id) REFERENCES public.usuario(idusuario) ON DELETE SET NULL;


--
-- Name: aocr_usuario_transferencia_detalle aocr_usuario_transferencia_detalle_transferencia_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_usuario_transferencia_detalle
    ADD CONSTRAINT aocr_usuario_transferencia_detalle_transferencia_id_fkey FOREIGN KEY (transferencia_id) REFERENCES public.aocr_usuario_transferencia(id_transferencia) ON DELETE CASCADE;


--
-- Name: auditoria_historialestado auditoria_historiale_usuario_id_473a140c_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditoria_historialestado
    ADD CONSTRAINT auditoria_historiale_usuario_id_473a140c_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: auditorias auditorias_usuario_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auditorias
    ADD CONSTRAINT auditorias_usuario_id_foreign FOREIGN KEY (usuario_id) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: auth_group_permissions auth_group_permissio_permission_id_84c5c92e_fk_auth_perm; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_group_permissions
    ADD CONSTRAINT auth_group_permissio_permission_id_84c5c92e_fk_auth_perm FOREIGN KEY (permission_id) REFERENCES public.auth_permission(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: auth_group_permissions auth_group_permissions_group_id_b120cbf9_fk_auth_group_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_group_permissions
    ADD CONSTRAINT auth_group_permissions_group_id_b120cbf9_fk_auth_group_id FOREIGN KEY (group_id) REFERENCES public.auth_group(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: auth_permission auth_permission_content_type_id_2f476e4b_fk_django_co; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.auth_permission
    ADD CONSTRAINT auth_permission_content_type_id_2f476e4b_fk_django_co FOREIGN KEY (content_type_id) REFERENCES public.django_content_type(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: capacitaciones capacitaciones_curso_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones
    ADD CONSTRAINT capacitaciones_curso_id_foreign FOREIGN KEY (curso_id) REFERENCES public.cursos(id) ON DELETE RESTRICT;


--
-- Name: capacitaciones capacitaciones_estado_capacitacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones
    ADD CONSTRAINT capacitaciones_estado_capacitacion_id_foreign FOREIGN KEY (estado_capacitacion_id) REFERENCES public.estados_capacitacion(id) ON DELETE RESTRICT;


--
-- Name: capacitaciones capacitaciones_institucion_capacitadora_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones
    ADD CONSTRAINT capacitaciones_institucion_capacitadora_id_foreign FOREIGN KEY (institucion_capacitadora_id) REFERENCES public.instituciones_capacitadoras(id) ON DELETE RESTRICT;


--
-- Name: capacitaciones capacitaciones_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones
    ADD CONSTRAINT capacitaciones_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: capacitaciones capacitaciones_user_id_validador_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.capacitaciones
    ADD CONSTRAINT capacitaciones_user_id_validador_foreign FOREIGN KEY (user_id_validador) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: certificados_certificadoaocr certificados_certifi_coordinador_revisor__6053cd99_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_coordinador_revisor__6053cd99_fk_usuarios_ FOREIGN KEY (coordinador_revisor_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: certificados_certificadoaocr certificados_certifi_dcav_firmante_id_91d7d7b4_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_dcav_firmante_id_91d7d7b4_fk_usuarios_ FOREIGN KEY (dcav_firmante_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: certificados_certificadoaocr certificados_certifi_dirdac_firmante_id_03afde35_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_dirdac_firmante_id_03afde35_fk_usuarios_ FOREIGN KEY (dirdac_firmante_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: certificados_certificadoaocr certificados_certifi_documento_aocr_id_61934974_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_documento_aocr_id_61934974_fk_documento FOREIGN KEY (documento_aocr_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: certificados_certificadoaocr certificados_certifi_documento_condicione_90199c62_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_documento_condicione_90199c62_fk_documento FOREIGN KEY (documento_condiciones_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: certificados_certificadoaocr certificados_certifi_inspeccion_id_ced678c3_fk_inspeccio; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_inspeccion_id_ced678c3_fk_inspeccio FOREIGN KEY (inspeccion_id) REFERENCES public.inspecciones_inspecciontecnica(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: certificados_certificadoaocr certificados_certifi_solicitud_id_ff94af14_fk_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.certificados_certificadoaocr
    ADD CONSTRAINT certificados_certifi_solicitud_id_ff94af14_fk_solicitud FOREIGN KEY (solicitud_id) REFERENCES public.solicitudes_solicitudaocr(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: competencias competencias_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.competencias
    ADD CONSTRAINT competencias_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: contactos_institucionales contactos_institucionales_institucion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contactos_institucionales
    ADD CONSTRAINT contactos_institucionales_institucion_id_foreign FOREIGN KEY (institucion_id) REFERENCES public.instituciones(id) ON DELETE RESTRICT;


--
-- Name: contactos_servidor contactos_servidor_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.contactos_servidor
    ADD CONSTRAINT contactos_servidor_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: cursos_mandatorios cursos_mandatorios_curso_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos_mandatorios
    ADD CONSTRAINT cursos_mandatorios_curso_id_foreign FOREIGN KEY (curso_id) REFERENCES public.cursos(id) ON DELETE CASCADE;


--
-- Name: cursos_mandatorios cursos_mandatorios_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos_mandatorios
    ADD CONSTRAINT cursos_mandatorios_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: cursos cursos_programa_capacitacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos
    ADD CONSTRAINT cursos_programa_capacitacion_id_foreign FOREIGN KEY (programa_capacitacion_id) REFERENCES public.programas_capacitacion(id) ON DELETE RESTRICT;


--
-- Name: cursos cursos_tipo_curso_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cursos
    ADD CONSTRAINT cursos_tipo_curso_id_foreign FOREIGN KEY (tipo_curso_id) REFERENCES public.tipos_curso(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_gestion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_gestion_id_foreign FOREIGN KEY (gestion_id) REFERENCES public.gestiones_administrativas(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_grupo_ocupacional_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_grupo_ocupacional_id_foreign FOREIGN KEY (grupo_ocupacional_id) REFERENCES public.grupos_ocupacionales(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_lugar_trabajo_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_lugar_trabajo_id_foreign FOREIGN KEY (lugar_trabajo_id) REFERENCES public.lugares_trabajo(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_modalidad_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_modalidad_id_foreign FOREIGN KEY (modalidad_id) REFERENCES public.modalidades_prestacion(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_puesto_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_puesto_id_foreign FOREIGN KEY (puesto_id) REFERENCES public.puestos_institucionales(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: datos_laborales datos_laborales_unidad_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_unidad_id_foreign FOREIGN KEY (unidad_id) REFERENCES public.unidades_administrativas(id) ON DELETE RESTRICT;


--
-- Name: datos_laborales datos_laborales_user_id_registro_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.datos_laborales
    ADD CONSTRAINT datos_laborales_user_id_registro_foreign FOREIGN KEY (user_id_registro) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: detalles_orden detalles_orden_concepto_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalles_orden
    ADD CONSTRAINT detalles_orden_concepto_id_fkey FOREIGN KEY (concepto_id) REFERENCES public.conceptos(id);


--
-- Name: detalles_orden detalles_orden_orden_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.detalles_orden
    ADD CONSTRAINT detalles_orden_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.ordenes_recaudacion(id) ON DELETE CASCADE;


--
-- Name: django_admin_log django_admin_log_content_type_id_c4bce8eb_fk_django_co; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_admin_log
    ADD CONSTRAINT django_admin_log_content_type_id_c4bce8eb_fk_django_co FOREIGN KEY (content_type_id) REFERENCES public.django_content_type(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_admin_log django_admin_log_user_id_c564eba6_fk_usuarios_usuario_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_admin_log
    ADD CONSTRAINT django_admin_log_user_id_c564eba6_fk_usuarios_usuario_id FOREIGN KEY (user_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_auditoria_evento django_aocr_auditori_usuario_id_d134fd50_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_auditoria_evento
    ADD CONSTRAINT django_aocr_auditori_usuario_id_d134fd50_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_bandeja_item django_aocr_bandeja__grupo_destino_id_aefda6ad_fk_auth_grou; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_bandeja_item
    ADD CONSTRAINT django_aocr_bandeja__grupo_destino_id_aefda6ad_fk_auth_grou FOREIGN KEY (grupo_destino_id) REFERENCES public.auth_group(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_bandeja_item django_aocr_bandeja__usuario_destino_id_22404cfe_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_bandeja_item
    ADD CONSTRAINT django_aocr_bandeja__usuario_destino_id_22404cfe_fk_usuarios_ FOREIGN KEY (usuario_destino_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_comprobante_pago django_aocr_comprobante_pago_orden_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_comprobante_pago
    ADD CONSTRAINT django_aocr_comprobante_pago_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.django_aocr_orden_recaudacion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_comprobante_pago django_aocr_comprobante_pago_usuario_carga_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_comprobante_pago
    ADD CONSTRAINT django_aocr_comprobante_pago_usuario_carga_id_fkey FOREIGN KEY (usuario_carga_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_contador_bandeja django_aocr_contador_usuario_id_08db5efa_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_contador_bandeja
    ADD CONSTRAINT django_aocr_contador_usuario_id_08db5efa_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_descarga_documento django_aocr_descarga_documento_documento_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_descarga_documento
    ADD CONSTRAINT django_aocr_descarga_documento_documento_id_fkey FOREIGN KEY (documento_id) REFERENCES public.django_aocr_documento_generado(id) ON DELETE CASCADE;


--
-- Name: django_aocr_descarga_documento django_aocr_descarga_documento_usuario_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_descarga_documento
    ADD CONSTRAINT django_aocr_descarga_documento_usuario_id_fkey FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) ON DELETE SET NULL;


--
-- Name: django_aocr_documento_generado django_aocr_documento_generado_firmado_por_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_documento_generado
    ADD CONSTRAINT django_aocr_documento_generado_firmado_por_id_fkey FOREIGN KEY (firmado_por_id) REFERENCES public.usuarios_usuario(id) ON DELETE SET NULL;


--
-- Name: django_aocr_documento_generado django_aocr_documento_generado_generado_por_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_documento_generado
    ADD CONSTRAINT django_aocr_documento_generado_generado_por_id_fkey FOREIGN KEY (generado_por_id) REFERENCES public.usuarios_usuario(id) ON DELETE SET NULL;


--
-- Name: django_aocr_factura_recaudacion django_aocr_factura__generado_por_id_d083c175_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_factura_recaudacion
    ADD CONSTRAINT django_aocr_factura__generado_por_id_d083c175_fk_usuarios_ FOREIGN KEY (generado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_factura_recaudacion django_aocr_factura__orden_id_0f2d1f98_fk_aocr_or_o; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_factura_recaudacion
    ADD CONSTRAINT django_aocr_factura__orden_id_0f2d1f98_fk_aocr_or_o FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_firma_documento django_aocr_firma_documento_documento_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_firma_documento
    ADD CONSTRAINT django_aocr_firma_documento_documento_id_fkey FOREIGN KEY (documento_id) REFERENCES public.django_aocr_documento_generado(id) ON DELETE CASCADE;


--
-- Name: django_aocr_firma_documento django_aocr_firma_documento_usuario_firmante_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_firma_documento
    ADD CONSTRAINT django_aocr_firma_documento_usuario_firmante_id_fkey FOREIGN KEY (usuario_firmante_id) REFERENCES public.usuarios_usuario(id) ON DELETE RESTRICT;


--
-- Name: django_aocr_historial_estado django_aocr_historia_usuario_id_601fe8f6_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_historial_estado
    ADD CONSTRAINT django_aocr_historia_usuario_id_601fe8f6_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_orden_observacion django_aocr_orden_ob_orden_id_f3a2d2ca_fk_aocr_or_o; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_observacion
    ADD CONSTRAINT django_aocr_orden_ob_orden_id_f3a2d2ca_fk_aocr_or_o FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_orden_observacion django_aocr_orden_ob_usuario_id_aeaf2037_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_observacion
    ADD CONSTRAINT django_aocr_orden_ob_usuario_id_aeaf2037_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_orden_recaudacion_detalle django_aocr_orden_recaudacion_detalle_concepto_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_recaudacion_detalle
    ADD CONSTRAINT django_aocr_orden_recaudacion_detalle_concepto_id_fkey FOREIGN KEY (concepto_id) REFERENCES public.django_aocr_concepto_recaudacion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_orden_recaudacion_detalle django_aocr_orden_recaudacion_detalle_orden_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_recaudacion_detalle
    ADD CONSTRAINT django_aocr_orden_recaudacion_detalle_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.django_aocr_orden_recaudacion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_orden_recaudacion django_aocr_orden_recaudacion_usuario_rt_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_orden_recaudacion
    ADD CONSTRAINT django_aocr_orden_recaudacion_usuario_rt_id_fkey FOREIGN KEY (usuario_rt_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_registro_rt django_aocr_registro_firmado_por_id_1b29ecec_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt
    ADD CONSTRAINT django_aocr_registro_firmado_por_id_1b29ecec_fk_usuarios_ FOREIGN KEY (firmado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_registro_rt_observacion django_aocr_registro_registro_id_694ef78e_fk_django_ao; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt_observacion
    ADD CONSTRAINT django_aocr_registro_registro_id_694ef78e_fk_django_ao FOREIGN KEY (registro_id) REFERENCES public.django_aocr_registro_rt(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_registro_rt_archivo_version django_aocr_registro_registro_id_ab6a0e2f_fk_django_ao; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt_archivo_version
    ADD CONSTRAINT django_aocr_registro_registro_id_ab6a0e2f_fk_django_ao FOREIGN KEY (registro_id) REFERENCES public.django_aocr_registro_rt(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_registro_rt_archivo_version django_aocr_registro_usuario_carga_id_a993adce_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt_archivo_version
    ADD CONSTRAINT django_aocr_registro_usuario_carga_id_a993adce_fk_usuarios_ FOREIGN KEY (usuario_carga_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_registro_rt_observacion django_aocr_registro_usuario_id_15fa41c2_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt_observacion
    ADD CONSTRAINT django_aocr_registro_usuario_id_15fa41c2_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_registro_rt django_aocr_registro_usuario_rt_id_6d760608_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_registro_rt
    ADD CONSTRAINT django_aocr_registro_usuario_rt_id_6d760608_fk_usuarios_ FOREIGN KEY (usuario_rt_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: documentos_documento documentos_documento_cargado_por_id_139ff584_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentos_documento
    ADD CONSTRAINT documentos_documento_cargado_por_id_139ff584_fk_usuarios_ FOREIGN KEY (cargado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: documentos_documento documentos_documento_reemplazado_por_id_a4e0c5af_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentos_documento
    ADD CONSTRAINT documentos_documento_reemplazado_por_id_a4e0c5af_fk_documento FOREIGN KEY (reemplazado_por_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: documentos_documento documentos_documento_tramite_id_e4ced3bc_fk_tramites_tramite_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.documentos_documento
    ADD CONSTRAINT documentos_documento_tramite_id_e4ced3bc_fk_tramites_tramite_id FOREIGN KEY (tramite_id) REFERENCES public.tramites_tramite(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: errores_importacion errores_importacion_importacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.errores_importacion
    ADD CONSTRAINT errores_importacion_importacion_id_foreign FOREIGN KEY (importacion_id) REFERENCES public.importaciones(id) ON DELETE CASCADE;


--
-- Name: estatutos_organicos estatutos_organicos_institucion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.estatutos_organicos
    ADD CONSTRAINT estatutos_organicos_institucion_id_foreign FOREIGN KEY (institucion_id) REFERENCES public.instituciones(id) ON DELETE RESTRICT;


--
-- Name: experiencia_profesional experiencia_profesional_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.experiencia_profesional
    ADD CONSTRAINT experiencia_profesional_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: firmas_firmadocumento firmas_firmadocument_documento_id_405888ae_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.firmas_firmadocumento
    ADD CONSTRAINT firmas_firmadocument_documento_id_405888ae_fk_documento FOREIGN KEY (documento_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: firmas_firmadocumento firmas_firmadocument_firmado_por_id_6d36556e_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.firmas_firmadocumento
    ADD CONSTRAINT firmas_firmadocument_firmado_por_id_6d36556e_fk_usuarios_ FOREIGN KEY (firmado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: aocr_tbaeronave fk_aeronave_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbaeronave
    ADD CONSTRAINT fk_aeronave_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: aocr_tbnoconformidad fk_aocr_nc_informe_cierre; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnoconformidad
    ADD CONSTRAINT fk_aocr_nc_informe_cierre FOREIGN KEY (codigo_informe_cierre) REFERENCES public.aocr_tbinforme_inspeccion(codigo_informe) NOT VALID;


--
-- Name: aocr_tbnoconformidad fk_aocr_nc_inspeccion_nueva; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnoconformidad
    ADD CONSTRAINT fk_aocr_nc_inspeccion_nueva FOREIGN KEY (codigo_inspeccion_nueva) REFERENCES public.aocr_tbinspeccion(codigo_inspeccion) NOT VALID;


--
-- Name: aocr_tbnoconformidad fk_aocr_nc_raiz; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnoconformidad
    ADD CONSTRAINT fk_aocr_nc_raiz FOREIGN KEY (codigo_nc_raiz) REFERENCES public.aocr_tbnoconformidad(codigo_no_conformidad);


--
-- Name: aocr_tbnoconformidad fk_aocr_nc_solicitud_nueva; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbnoconformidad
    ADD CONSTRAINT fk_aocr_nc_solicitud_nueva FOREIGN KEY (codigo_solicitud_nueva) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) NOT VALID;


--
-- Name: aocr_tbcertificado fk_certificado_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbcertificado
    ADD CONSTRAINT fk_certificado_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE RESTRICT;


--
-- Name: aocr_tbchecklist fk_checklist_inspeccion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbchecklist
    ADD CONSTRAINT fk_checklist_inspeccion FOREIGN KEY (codigo_inspeccion) REFERENCES public.aocr_tbinspeccion(codigo_inspeccion) ON DELETE CASCADE;


--
-- Name: aocr_tbdocumento_subsanacion fk_doc_subsanacion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_subsanacion
    ADD CONSTRAINT fk_doc_subsanacion FOREIGN KEY (codigo_subsanacion) REFERENCES public.aocr_tbsubsanacion(codigo_subsanacion) ON DELETE CASCADE;


--
-- Name: aocr_tbdocumento_subsanacion fk_doc_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento_subsanacion
    ADD CONSTRAINT fk_doc_usuario FOREIGN KEY (codigo_usuario_carga) REFERENCES public.usuario(idusuario) ON DELETE RESTRICT;


--
-- Name: aocr_tbdocumento fk_documento_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbdocumento
    ADD CONSTRAINT fk_documento_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: email_attachment fk_email_attachment_queue; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_attachment
    ADD CONSTRAINT fk_email_attachment_queue FOREIGN KEY (email_queue_id) REFERENCES public.email_queue(id) ON DELETE CASCADE;


--
-- Name: email_queue fk_email_queue_orden; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_queue
    ADD CONSTRAINT fk_email_queue_orden FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id) ON UPDATE CASCADE ON DELETE CASCADE;


--
-- Name: CONSTRAINT fk_email_queue_orden ON email_queue; Type: COMMENT; Schema: public; Owner: -
--

COMMENT ON CONSTRAINT fk_email_queue_orden ON public.email_queue IS 'Foreign key que vincula emails con Ã³rdenes de recaudaciÃ³n. ON DELETE CASCADE elimina emails automÃ¡ticamente cuando se elimina una orden.';


--
-- Name: aocr_tb_factura_pago fk_factura_pago_orden; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_factura_pago
    ADD CONSTRAINT fk_factura_pago_orden FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id);


--
-- Name: aocr_tb_factura_pago fk_factura_pago_pago; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tb_factura_pago
    ADD CONSTRAINT fk_factura_pago_pago FOREIGN KEY (pago_id) REFERENCES public.aocr_tbpago(codigo_pago);


--
-- Name: aocr_tbhistorial_estado fk_historial_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbhistorial_estado
    ADD CONSTRAINT fk_historial_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: aocr_tbinforme fk_informe_inspeccion; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinforme
    ADD CONSTRAINT fk_informe_inspeccion FOREIGN KEY (codigo_inspeccion) REFERENCES public.aocr_tbinspeccion(codigo_inspeccion) ON DELETE CASCADE;


--
-- Name: aocr_tbinspeccion fk_inspeccion_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbinspeccion
    ADD CONSTRAINT fk_inspeccion_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: aocr_tbobservacion fk_observacion_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbobservacion
    ADD CONSTRAINT fk_observacion_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: aocr_or_orden_detalle fk_or_detalle_concepto; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden_detalle
    ADD CONSTRAINT fk_or_detalle_concepto FOREIGN KEY (concepto_id) REFERENCES public.aocr_or_concepto(id);


--
-- Name: aocr_or_orden_detalle fk_or_detalle_orden; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden_detalle
    ADD CONSTRAINT fk_or_detalle_orden FOREIGN KEY (orden_id) REFERENCES public.aocr_or_orden(id) ON DELETE CASCADE;


--
-- Name: aocr_or_orden fk_orden_usuario; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_or_orden
    ADD CONSTRAINT fk_orden_usuario FOREIGN KEY (codigo_usuario) REFERENCES public.usuario(idusuario);


--
-- Name: aocr_tbpago fk_pago_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbpago
    ADD CONSTRAINT fk_pago_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: permisos fk_permisos_rol; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT fk_permisos_rol FOREIGN KEY (codigorol) REFERENCES public.rol(codigorol) ON DELETE CASCADE;


--
-- Name: permisos fk_permisos_submenu; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT fk_permisos_submenu FOREIGN KEY (idsubmenu) REFERENCES public.submenu(idsubmenu) ON DELETE CASCADE;


--
-- Name: aocr_inspector_reasignacion_historial fk_reasig_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_inspector_reasignacion_historial
    ADD CONSTRAINT fk_reasig_solicitud FOREIGN KEY (solicitud_id) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: aocr_revision_documental_coordinador fk_revdoc_coord_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_revision_documental_coordinador
    ADD CONSTRAINT fk_revdoc_coord_solicitud FOREIGN KEY (solicitud_id) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: seguridad_rol_permiso fk_seguridad_rol_permiso_permiso; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.seguridad_rol_permiso
    ADD CONSTRAINT fk_seguridad_rol_permiso_permiso FOREIGN KEY (id_permiso) REFERENCES public.seguridad_permiso(id_permiso);


--
-- Name: seguridad_rol_permiso fk_seguridad_rol_permiso_rol; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.seguridad_rol_permiso
    ADD CONSTRAINT fk_seguridad_rol_permiso_rol FOREIGN KEY (codigorol) REFERENCES public.rol(codigorol);


--
-- Name: email_queue fk_solicitud_email; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.email_queue
    ADD CONSTRAINT fk_solicitud_email FOREIGN KEY (solicitud_id) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: submenu fk_submenu_menu; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.submenu
    ADD CONSTRAINT fk_submenu_menu FOREIGN KEY (idmenu) REFERENCES public.menu(idmenu) ON DELETE CASCADE;


--
-- Name: aocr_tbsubsanacion fk_subsanacion_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsubsanacion
    ADD CONSTRAINT fk_subsanacion_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: aocr_tbsubsanacion fk_subsanacion_usuario_respuesta; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsubsanacion
    ADD CONSTRAINT fk_subsanacion_usuario_respuesta FOREIGN KEY (codigo_usuario_respuesta) REFERENCES public.usuario(idusuario) ON DELETE RESTRICT;


--
-- Name: aocr_tbsubsanacion fk_subsanacion_usuario_solicitante; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbsubsanacion
    ADD CONSTRAINT fk_subsanacion_usuario_solicitante FOREIGN KEY (codigo_usuario_solicitante) REFERENCES public.usuario(idusuario) ON DELETE RESTRICT;


--
-- Name: usuario fk_usuario_rol; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT fk_usuario_rol FOREIGN KEY (codigorol) REFERENCES public.rol(codigorol) ON DELETE SET NULL;


--
-- Name: usuario_rol fk_usuariorol_rol; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_rol
    ADD CONSTRAINT fk_usuariorol_rol FOREIGN KEY (codigorol) REFERENCES public.rol(codigorol) ON DELETE CASCADE;


--
-- Name: aocr_tbviatico fk_viatico_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.aocr_tbviatico
    ADD CONSTRAINT fk_viatico_solicitud FOREIGN KEY (codigo_solicitud) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE CASCADE;


--
-- Name: fr3_detalle_pg fr3_detalle_pg_fr3_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.fr3_detalle_pg
    ADD CONSTRAINT fr3_detalle_pg_fr3_id_fkey FOREIGN KEY (fr3_id) REFERENCES public.fr3_pg(id) ON DELETE CASCADE;


--
-- Name: historial_estados_orden historial_estados_orden_orden_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.historial_estados_orden
    ADD CONSTRAINT historial_estados_orden_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.ordenes_recaudacion(id);


--
-- Name: historial_laboral historial_laboral_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.historial_laboral
    ADD CONSTRAINT historial_laboral_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: importaciones importaciones_usuario_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.importaciones
    ADD CONSTRAINT importaciones_usuario_id_foreign FOREIGN KEY (usuario_id) REFERENCES public.users(id) ON DELETE SET NULL;


--
-- Name: inspecciones_inspecciontecnica inspecciones_inspecc_informe_tecnico_id_dfe15e85_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_inspecciontecnica
    ADD CONSTRAINT inspecciones_inspecc_informe_tecnico_id_dfe15e85_fk_documento FOREIGN KEY (informe_tecnico_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_inspecciontecnica inspecciones_inspecc_inspector_id_cd716503_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_inspecciontecnica
    ADD CONSTRAINT inspecciones_inspecc_inspector_id_cd716503_fk_usuarios_ FOREIGN KEY (inspector_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_inspecciontecnica inspecciones_inspecc_lv_id_940ed1de_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_inspecciontecnica
    ADD CONSTRAINT inspecciones_inspecc_lv_id_940ed1de_fk_documento FOREIGN KEY (lv_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_inspecciontecnica inspecciones_inspecc_solicitud_id_4d00075b_fk_inspeccio; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_inspecciontecnica
    ADD CONSTRAINT inspecciones_inspecc_solicitud_id_4d00075b_fk_inspeccio FOREIGN KEY (solicitud_id) REFERENCES public.inspecciones_solicitudinspeccion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_noconformidad inspecciones_noconfo_documento_id_67d4053a_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_noconformidad
    ADD CONSTRAINT inspecciones_noconfo_documento_id_67d4053a_fk_documento FOREIGN KEY (documento_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_noconformidad inspecciones_noconfo_inspeccion_id_9283a51a_fk_inspeccio; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_noconformidad
    ADD CONSTRAINT inspecciones_noconfo_inspeccion_id_9283a51a_fk_inspeccio FOREIGN KEY (inspeccion_id) REFERENCES public.inspecciones_inspecciontecnica(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_noconformidad inspecciones_noconfo_revisado_por_coordin_1cb5c764_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_noconformidad
    ADD CONSTRAINT inspecciones_noconfo_revisado_por_coordin_1cb5c764_fk_usuarios_ FOREIGN KEY (revisado_por_coordinador_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_solicitudinspeccion inspecciones_solicit_autorizacion_id_83d21c2e_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_solicitudinspeccion
    ADD CONSTRAINT inspecciones_solicit_autorizacion_id_83d21c2e_fk_documento FOREIGN KEY (autorizacion_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_solicitudinspeccion inspecciones_solicit_factura_id_a3ade54c_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_solicitudinspeccion
    ADD CONSTRAINT inspecciones_solicit_factura_id_a3ade54c_fk_documento FOREIGN KEY (factura_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_solicitudinspeccion inspecciones_solicit_firmado_por_coordina_1b623a69_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_solicitudinspeccion
    ADD CONSTRAINT inspecciones_solicit_firmado_por_coordina_1b623a69_fk_usuarios_ FOREIGN KEY (firmado_por_coordinador_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_solicitudinspeccion inspecciones_solicit_inspector_id_f856a2f3_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_solicitudinspeccion
    ADD CONSTRAINT inspecciones_solicit_inspector_id_f856a2f3_fk_usuarios_ FOREIGN KEY (inspector_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: inspecciones_solicitudinspeccion inspecciones_solicit_solicitud_id_21e1b32c_fk_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.inspecciones_solicitudinspeccion
    ADD CONSTRAINT inspecciones_solicit_solicitud_id_21e1b32c_fk_solicitud FOREIGN KEY (solicitud_id) REFERENCES public.solicitudes_solicitudaocr(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: instrucciones_formales instrucciones_formales_institucion_educativa_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instrucciones_formales
    ADD CONSTRAINT instrucciones_formales_institucion_educativa_id_foreign FOREIGN KEY (institucion_educativa_id) REFERENCES public.instituciones_educativas(id) ON DELETE RESTRICT;


--
-- Name: instrucciones_formales instrucciones_formales_nivel_instruccion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instrucciones_formales
    ADD CONSTRAINT instrucciones_formales_nivel_instruccion_id_foreign FOREIGN KEY (nivel_instruccion_id) REFERENCES public.niveles_instruccion(id) ON DELETE RESTRICT;


--
-- Name: instrucciones_formales instrucciones_formales_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.instrucciones_formales
    ADD CONSTRAINT instrucciones_formales_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: materias materias_curso_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.materias
    ADD CONSTRAINT materias_curso_id_foreign FOREIGN KEY (curso_id) REFERENCES public.cursos(id) ON DELETE CASCADE;


--
-- Name: model_has_permissions model_has_permissions_permission_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.model_has_permissions
    ADD CONSTRAINT model_has_permissions_permission_id_foreign FOREIGN KEY (permission_id) REFERENCES public.permissions(id) ON DELETE CASCADE;


--
-- Name: model_has_roles model_has_roles_role_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.model_has_roles
    ADD CONSTRAINT model_has_roles_role_id_foreign FOREIGN KEY (role_id) REFERENCES public.roles(id) ON DELETE CASCADE;


--
-- Name: modificacion_cyl_aeropuertomodificacioncyl modificacion_cyl_aer_created_by_id_6c7988ce_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_aeropuertomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_aer_created_by_id_6c7988ce_fk_usuarios_ FOREIGN KEY (created_by_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_aeropuertomodificacioncyl modificacion_cyl_aer_solicitud_id_1eb406c5_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_aeropuertomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_aer_solicitud_id_1eb406c5_fk_modificac FOREIGN KEY (solicitud_id) REFERENCES public.modificacion_cyl_solicitudmodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_doc_cargado_por_id_3b37782c_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_cargado_por_id_3b37782c_fk_usuarios_ FOREIGN KEY (cargado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_doc_created_by_id_a675b9b3_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_created_by_id_a675b9b3_fk_usuarios_ FOREIGN KEY (created_by_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_doc_documento_base_id_0388a0ab_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_documento_base_id_0388a0ab_fk_documento FOREIGN KEY (documento_base_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentogeneradomodificacioncyl modificacion_cyl_doc_firmado_por_id_d5ba4db0_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentogeneradomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_firmado_por_id_d5ba4db0_fk_usuarios_ FOREIGN KEY (firmado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentogeneradomodificacioncyl modificacion_cyl_doc_generado_por_id_8537fd62_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentogeneradomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_generado_por_id_8537fd62_fk_usuarios_ FOREIGN KEY (generado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_doc_revisado_por_id_f7525581_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_revisado_por_id_f7525581_fk_usuarios_ FOREIGN KEY (revisado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentogeneradomodificacioncyl modificacion_cyl_doc_solicitud_id_36b9ef8d_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentogeneradomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_solicitud_id_36b9ef8d_fk_modificac FOREIGN KEY (solicitud_id) REFERENCES public.modificacion_cyl_solicitudmodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_doc_solicitud_id_b57c7ccc_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_solicitud_id_b57c7ccc_fk_modificac FOREIGN KEY (solicitud_id) REFERENCES public.modificacion_cyl_solicitudmodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_documentomodificacioncyl modificacion_cyl_doc_updated_by_id_a7919936_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_documentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_doc_updated_by_id_a7919936_fk_usuarios_ FOREIGN KEY (updated_by_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_historialdocumentomodificacioncyl modificacion_cyl_his_documento_id_e2fa9e30_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialdocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_his_documento_id_e2fa9e30_fk_modificac FOREIGN KEY (documento_id) REFERENCES public.modificacion_cyl_documentomodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_historialdocumentomodificacioncyl modificacion_cyl_his_solicitud_id_941e89b2_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialdocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_his_solicitud_id_941e89b2_fk_modificac FOREIGN KEY (solicitud_id) REFERENCES public.modificacion_cyl_solicitudmodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_historialmodificacioncyl modificacion_cyl_his_solicitud_id_af26c98d_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_his_solicitud_id_af26c98d_fk_modificac FOREIGN KEY (solicitud_id) REFERENCES public.modificacion_cyl_solicitudmodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_historialmodificacioncyl modificacion_cyl_his_usuario_id_b9ccf213_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_his_usuario_id_b9ccf213_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_historialdocumentomodificacioncyl modificacion_cyl_his_usuario_id_e75634cf_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_historialdocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_his_usuario_id_e75634cf_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl modificacion_cyl_rev_created_by_id_c27e4e9c_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_revisiondocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_rev_created_by_id_c27e4e9c_fk_usuarios_ FOREIGN KEY (created_by_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl modificacion_cyl_rev_documento_id_dba4c9ac_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_revisiondocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_rev_documento_id_dba4c9ac_fk_modificac FOREIGN KEY (documento_id) REFERENCES public.modificacion_cyl_documentomodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl modificacion_cyl_rev_revisado_por_id_231c52b3_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_revisiondocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_rev_revisado_por_id_231c52b3_fk_usuarios_ FOREIGN KEY (revisado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_revisiondocumentomodificacioncyl modificacion_cyl_rev_solicitud_id_e378af42_fk_modificac; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_revisiondocumentomodificacioncyl
    ADD CONSTRAINT modificacion_cyl_rev_solicitud_id_e378af42_fk_modificac FOREIGN KEY (solicitud_id) REFERENCES public.modificacion_cyl_solicitudmodificacioncyl(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_coordinador_designa__c1f8dccb_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_coordinador_designa__c1f8dccb_fk_usuarios_ FOREIGN KEY (coordinador_designa_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_created_by_id_5163db75_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_created_by_id_5163db75_fk_usuarios_ FOREIGN KEY (created_by_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_decision_nuevo_aerop_0462b183_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_decision_nuevo_aerop_0462b183_fk_usuarios_ FOREIGN KEY (decision_nuevo_aeropuerto_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_firmado_por_dcav_id_82a2ebd4_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_firmado_por_dcav_id_82a2ebd4_fk_usuarios_ FOREIGN KEY (firmado_por_dcav_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_inspector_designado__3c841970_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_inspector_designado__3c841970_fk_usuarios_ FOREIGN KEY (inspector_designado_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_operador_eae_id_4677dcd1_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_operador_eae_id_4677dcd1_fk_operadore FOREIGN KEY (operador_eae_id) REFERENCES public.operadores_operador(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_rt_id_2c3a421d_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_rt_id_2c3a421d_fk_operadore FOREIGN KEY (rt_id) REFERENCES public.operadores_representantetecnico(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: modificacion_cyl_solicitudmodificacioncyl modificacion_cyl_sol_updated_by_id_cd9c988f_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.modificacion_cyl_solicitudmodificacioncyl
    ADD CONSTRAINT modificacion_cyl_sol_updated_by_id_cd9c988f_fk_usuarios_ FOREIGN KEY (updated_by_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_notificacion notificaciones_notif_tramite_id_540a3cfe_fk_tramites_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_notificacion
    ADD CONSTRAINT notificaciones_notif_tramite_id_540a3cfe_fk_tramites_ FOREIGN KEY (tramite_id) REFERENCES public.tramites_tramite(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: django_aocr_notificacion notificaciones_notif_usuario_destino_id_8dfc66d6_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.django_aocr_notificacion
    ADD CONSTRAINT notificaciones_notif_usuario_destino_id_8dfc66d6_fk_usuarios_ FOREIGN KEY (usuario_destino_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_registrort operadores_registror_documento_aceptacion_45b042a3_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_registrort
    ADD CONSTRAINT operadores_registror_documento_aceptacion_45b042a3_fk_documento FOREIGN KEY (documento_aceptacion_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_registrort operadores_registror_formulario_designaci_82fd1a2b_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_registrort
    ADD CONSTRAINT operadores_registror_formulario_designaci_82fd1a2b_fk_documento FOREIGN KEY (formulario_designacion_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_registrort operadores_registror_operador_id_4199a948_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_registrort
    ADD CONSTRAINT operadores_registror_operador_id_4199a948_fk_operadore FOREIGN KEY (operador_id) REFERENCES public.operadores_operador(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_registrort operadores_registror_revisado_por_id_dc81cfae_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_registrort
    ADD CONSTRAINT operadores_registror_revisado_por_id_dc81cfae_fk_usuarios_ FOREIGN KEY (revisado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_registrort operadores_registror_usuario_rt_id_878932bc_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_registrort
    ADD CONSTRAINT operadores_registror_usuario_rt_id_878932bc_fk_usuarios_ FOREIGN KEY (usuario_rt_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_representantetecnico operadores_represent_operador_id_a5042fba_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_representantetecnico
    ADD CONSTRAINT operadores_represent_operador_id_a5042fba_fk_operadore FOREIGN KEY (operador_id) REFERENCES public.operadores_operador(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_representantetecnico operadores_represent_registro_rt_id_a675ffbb_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_representantetecnico
    ADD CONSTRAINT operadores_represent_registro_rt_id_a675ffbb_fk_operadore FOREIGN KEY (registro_rt_id) REFERENCES public.operadores_registrort(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: operadores_representantetecnico operadores_represent_usuario_id_e94847c8_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.operadores_representantetecnico
    ADD CONSTRAINT operadores_represent_usuario_id_e94847c8_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: ordenes_recaudacion ordenes_recaudacion_concepto_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ordenes_recaudacion
    ADD CONSTRAINT ordenes_recaudacion_concepto_id_fkey FOREIGN KEY (concepto_id) REFERENCES public.conceptos(id);


--
-- Name: ordenes_recaudacion ordenes_recaudacion_contribuyente_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.ordenes_recaudacion
    ADD CONSTRAINT ordenes_recaudacion_contribuyente_id_fkey FOREIGN KEY (contribuyente_id) REFERENCES public.contribuyentes(id);


--
-- Name: otros_estudios otros_estudios_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.otros_estudios
    ADD CONSTRAINT otros_estudios_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: pagos pagos_orden_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.pagos
    ADD CONSTRAINT pagos_orden_id_fkey FOREIGN KEY (orden_id) REFERENCES public.ordenes_recaudacion(id);


--
-- Name: permisos permisos_codigorol_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT permisos_codigorol_fkey FOREIGN KEY (codigorol) REFERENCES public.rol(codigorol);


--
-- Name: permisos permisos_idsubmenu_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.permisos
    ADD CONSTRAINT permisos_idsubmenu_fkey FOREIGN KEY (idsubmenu) REFERENCES public.submenu(idsubmenu);


--
-- Name: puestos_institucionales puestos_institucionales_grupo_ocupacional_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.puestos_institucionales
    ADD CONSTRAINT puestos_institucionales_grupo_ocupacional_id_foreign FOREIGN KEY (grupo_ocupacional_id) REFERENCES public.grupos_ocupacionales(id) ON DELETE RESTRICT;


--
-- Name: puestos_institucionales puestos_institucionales_nivel_proceso_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.puestos_institucionales
    ADD CONSTRAINT puestos_institucionales_nivel_proceso_id_foreign FOREIGN KEY (nivel_proceso_id) REFERENCES public.niveles_proceso(id) ON DELETE RESTRICT;


--
-- Name: recaudacion_comprobantepago recaudacion_comproba_cargado_por_id_67ce7569_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_comprobantepago
    ADD CONSTRAINT recaudacion_comproba_cargado_por_id_67ce7569_fk_usuarios_ FOREIGN KEY (cargado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_comprobantepago recaudacion_comproba_orden_id_366161b9_fk_recaudaci; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_comprobantepago
    ADD CONSTRAINT recaudacion_comproba_orden_id_366161b9_fk_recaudaci FOREIGN KEY (orden_id) REFERENCES public.recaudacion_ordenrecaudacion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_comprobantepago recaudacion_comproba_reemplazado_por_id_329a4009_fk_recaudaci; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_comprobantepago
    ADD CONSTRAINT recaudacion_comproba_reemplazado_por_id_329a4009_fk_recaudaci FOREIGN KEY (reemplazado_por_id) REFERENCES public.recaudacion_comprobantepago(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_factura recaudacion_factura_archivo_pdf_factura__400838b0_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_factura
    ADD CONSTRAINT recaudacion_factura_archivo_pdf_factura__400838b0_fk_documento FOREIGN KEY (archivo_pdf_factura_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_factura recaudacion_factura_generada_por_id_b7284b04_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_factura
    ADD CONSTRAINT recaudacion_factura_generada_por_id_b7284b04_fk_usuarios_ FOREIGN KEY (generada_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_factura recaudacion_factura_orden_id_5b6e331b_fk_recaudaci; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_factura
    ADD CONSTRAINT recaudacion_factura_orden_id_5b6e331b_fk_recaudaci FOREIGN KEY (orden_id) REFERENCES public.recaudacion_ordenrecaudacion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrec_creada_por_id_bfcefba5_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrec_creada_por_id_bfcefba5_fk_usuarios_ FOREIGN KEY (creada_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrec_documento_orden_id_660781cd_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrec_documento_orden_id_660781cd_fk_documento FOREIGN KEY (documento_orden_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrec_operador_id_982d95f1_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrec_operador_id_982d95f1_fk_operadore FOREIGN KEY (operador_id) REFERENCES public.operadores_operador(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrec_revisado_por_id_9b56e6b8_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrec_revisado_por_id_9b56e6b8_fk_usuarios_ FOREIGN KEY (revisado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrec_rt_id_79c56475_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrec_rt_id_79c56475_fk_operadore FOREIGN KEY (rt_id) REFERENCES public.operadores_representantetecnico(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: recaudacion_ordenrecaudacion recaudacion_ordenrec_tramite_id_c58d5f82_fk_tramites_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.recaudacion_ordenrecaudacion
    ADD CONSTRAINT recaudacion_ordenrec_tramite_id_c58d5f82_fk_tramites_ FOREIGN KEY (tramite_id) REFERENCES public.tramites_tramite(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: role_has_permissions role_has_permissions_permission_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.role_has_permissions
    ADD CONSTRAINT role_has_permissions_permission_id_foreign FOREIGN KEY (permission_id) REFERENCES public.permissions(id) ON DELETE CASCADE;


--
-- Name: role_has_permissions role_has_permissions_role_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.role_has_permissions
    ADD CONSTRAINT role_has_permissions_role_id_foreign FOREIGN KEY (role_id) REFERENCES public.roles(id) ON DELETE CASCADE;


--
-- Name: servidor_discapacidades servidor_discapacidades_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_discapacidades
    ADD CONSTRAINT servidor_discapacidades_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: servidor_titulos servidor_titulos_especialidad_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_titulos
    ADD CONSTRAINT servidor_titulos_especialidad_id_foreign FOREIGN KEY (especialidad_id) REFERENCES public.especialidades(id) ON DELETE RESTRICT;


--
-- Name: servidor_titulos servidor_titulos_institucion_educativa_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_titulos
    ADD CONSTRAINT servidor_titulos_institucion_educativa_id_foreign FOREIGN KEY (institucion_educativa_id) REFERENCES public.instituciones_educativas(id) ON DELETE RESTRICT;


--
-- Name: servidor_titulos servidor_titulos_servidor_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_titulos
    ADD CONSTRAINT servidor_titulos_servidor_id_foreign FOREIGN KEY (servidor_id) REFERENCES public.servidores(id) ON DELETE CASCADE;


--
-- Name: servidor_titulos servidor_titulos_titulo_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidor_titulos
    ADD CONSTRAINT servidor_titulos_titulo_id_foreign FOREIGN KEY (titulo_id) REFERENCES public.titulos(id) ON DELETE RESTRICT;


--
-- Name: servidores servidores_institucion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidores
    ADD CONSTRAINT servidores_institucion_id_foreign FOREIGN KEY (institucion_id) REFERENCES public.instituciones(id) ON DELETE RESTRICT;


--
-- Name: servidores servidores_persona_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.servidores
    ADD CONSTRAINT servidores_persona_id_foreign FOREIGN KEY (persona_id) REFERENCES public.personas(id) ON DELETE CASCADE;


--
-- Name: solicitudes_documentosolicitud solicitudes_document_documento_id_e4c03fb4_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_documentosolicitud
    ADD CONSTRAINT solicitudes_document_documento_id_e4c03fb4_fk_documento FOREIGN KEY (documento_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_documentosolicitud solicitudes_document_revisado_por_id_089b611e_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_documentosolicitud
    ADD CONSTRAINT solicitudes_document_revisado_por_id_089b611e_fk_usuarios_ FOREIGN KEY (revisado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_documentosolicitud solicitudes_document_solicitud_id_8e078eef_fk_solicitud; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_documentosolicitud
    ADD CONSTRAINT solicitudes_document_solicitud_id_8e078eef_fk_solicitud FOREIGN KEY (solicitud_id) REFERENCES public.solicitudes_solicitudaocr(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_cierre_fase_document_9adaaed9_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_cierre_fase_document_9adaaed9_fk_documento FOREIGN KEY (cierre_fase_documental_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_coordinador_id_42f37cd4_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_coordinador_id_42f37cd4_fk_usuarios_ FOREIGN KEY (coordinador_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_coordinador_revisor__5e1b80c0_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_coordinador_revisor__5e1b80c0_fk_usuarios_ FOREIGN KEY (coordinador_revisor_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_dcav_firmante_id_ad5d2d1b_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_dcav_firmante_id_ad5d2d1b_fk_usuarios_ FOREIGN KEY (dcav_firmante_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_documento_condicione_efe26db2_fk_documento; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_documento_condicione_efe26db2_fk_documento FOREIGN KEY (documento_condiciones_generado_id) REFERENCES public.documentos_documento(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_inspector_id_542fca3c_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_inspector_id_542fca3c_fk_usuarios_ FOREIGN KEY (inspector_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_operador_id_89813004_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_operador_id_89813004_fk_operadore FOREIGN KEY (operador_id) REFERENCES public.operadores_operador(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_orden_recaudacion_id_7eb6376b_fk_recaudaci; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_orden_recaudacion_id_7eb6376b_fk_recaudaci FOREIGN KEY (orden_recaudacion_id) REFERENCES public.recaudacion_ordenrecaudacion(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: solicitudes_solicitudaocr solicitudes_solicitu_rt_id_926f0520_fk_operadore; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.solicitudes_solicitudaocr
    ADD CONSTRAINT solicitudes_solicitu_rt_id_926f0520_fk_operadore FOREIGN KEY (rt_id) REFERENCES public.operadores_representantetecnico(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: staging_curar staging_curar_importacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_curar
    ADD CONSTRAINT staging_curar_importacion_id_foreign FOREIGN KEY (importacion_id) REFERENCES public.importaciones(id) ON DELETE CASCADE;


--
-- Name: staging_maear staging_maear_importacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_maear
    ADD CONSTRAINT staging_maear_importacion_id_foreign FOREIGN KEY (importacion_id) REFERENCES public.importaciones(id) ON DELETE CASCADE;


--
-- Name: staging_uniar staging_uniar_importacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.staging_uniar
    ADD CONSTRAINT staging_uniar_importacion_id_foreign FOREIGN KEY (importacion_id) REFERENCES public.importaciones(id) ON DELETE CASCADE;


--
-- Name: temarios temarios_materia_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.temarios
    ADD CONSTRAINT temarios_materia_id_foreign FOREIGN KEY (materia_id) REFERENCES public.materias(id) ON DELETE CASCADE;


--
-- Name: titulos titulos_nivel_instruccion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.titulos
    ADD CONSTRAINT titulos_nivel_instruccion_id_foreign FOREIGN KEY (nivel_instruccion_id) REFERENCES public.niveles_instruccion(id) ON DELETE RESTRICT;


--
-- Name: tramites_historialestado tramites_historiales_tramite_id_a475ddb0_fk_tramites_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_historialestado
    ADD CONSTRAINT tramites_historiales_tramite_id_a475ddb0_fk_tramites_ FOREIGN KEY (tramite_id) REFERENCES public.tramites_tramite(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: tramites_historialestado tramites_historiales_usuario_id_55b8c026_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_historialestado
    ADD CONSTRAINT tramites_historiales_usuario_id_55b8c026_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: tramites_tramite tramites_tramite_creado_por_id_0a28b2d6_fk_usuarios_usuario_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_tramite
    ADD CONSTRAINT tramites_tramite_creado_por_id_0a28b2d6_fk_usuarios_usuario_id FOREIGN KEY (creado_por_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: tramites_tramite tramites_tramite_operador_id_6466b484_fk_operadores_operador_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_tramite
    ADD CONSTRAINT tramites_tramite_operador_id_6466b484_fk_operadores_operador_id FOREIGN KEY (operador_id) REFERENCES public.operadores_operador(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: tramites_tramite tramites_tramite_responsable_actual_i_09c15818_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.tramites_tramite
    ADD CONSTRAINT tramites_tramite_responsable_actual_i_09c15818_fk_usuarios_ FOREIGN KEY (responsable_actual_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: unidades_administrativas unidades_administrativas_gestion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unidades_administrativas
    ADD CONSTRAINT unidades_administrativas_gestion_id_foreign FOREIGN KEY (gestion_id) REFERENCES public.gestiones_administrativas(id) ON DELETE RESTRICT;


--
-- Name: unidades_administrativas unidades_administrativas_institucion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unidades_administrativas
    ADD CONSTRAINT unidades_administrativas_institucion_id_foreign FOREIGN KEY (institucion_id) REFERENCES public.instituciones(id) ON DELETE RESTRICT;


--
-- Name: unidades_administrativas unidades_administrativas_tipo_unidad_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unidades_administrativas
    ADD CONSTRAINT unidades_administrativas_tipo_unidad_id_foreign FOREIGN KEY (tipo_unidad_id) REFERENCES public.tipos_unidad(id) ON DELETE RESTRICT;


--
-- Name: unidades_administrativas unidades_administrativas_unidad_padre_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.unidades_administrativas
    ADD CONSTRAINT unidades_administrativas_unidad_padre_id_foreign FOREIGN KEY (unidad_padre_id) REFERENCES public.unidades_administrativas(id) ON DELETE SET NULL;


--
-- Name: usuario_as400_adicional usuario_as400_adicional_usuco8_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario_as400_adicional
    ADD CONSTRAINT usuario_as400_adicional_usuco8_fkey FOREIGN KEY (usuco8) REFERENCES public.usuario_as400(usucod) ON DELETE CASCADE;


--
-- Name: usuario usuario_codigorol_fkey; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuario
    ADD CONSTRAINT usuario_codigorol_fkey FOREIGN KEY (codigorol) REFERENCES public.rol(codigorol);


--
-- Name: usuarios_usuario_groups usuarios_usuario_gro_usuario_id_7a34077f_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_groups
    ADD CONSTRAINT usuarios_usuario_gro_usuario_id_7a34077f_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: usuarios_usuario_groups usuarios_usuario_groups_group_id_e77f6dcf_fk_auth_group_id; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_groups
    ADD CONSTRAINT usuarios_usuario_groups_group_id_e77f6dcf_fk_auth_group_id FOREIGN KEY (group_id) REFERENCES public.auth_group(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: usuarios_usuario_user_permissions usuarios_usuario_use_permission_id_4e5c0f2f_fk_auth_perm; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_user_permissions
    ADD CONSTRAINT usuarios_usuario_use_permission_id_4e5c0f2f_fk_auth_perm FOREIGN KEY (permission_id) REFERENCES public.auth_permission(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: usuarios_usuario_user_permissions usuarios_usuario_use_usuario_id_60aeea80_fk_usuarios_; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.usuarios_usuario_user_permissions
    ADD CONSTRAINT usuarios_usuario_use_usuario_id_60aeea80_fk_usuarios_ FOREIGN KEY (usuario_id) REFERENCES public.usuarios_usuario(id) DEFERRABLE INITIALLY DEFERRED;


--
-- Name: validaciones_capacitacion validaciones_capacitacion_capacitacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.validaciones_capacitacion
    ADD CONSTRAINT validaciones_capacitacion_capacitacion_id_foreign FOREIGN KEY (capacitacion_id) REFERENCES public.capacitaciones(id) ON DELETE CASCADE;


--
-- Name: validaciones_capacitacion validaciones_capacitacion_estado_capacitacion_id_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.validaciones_capacitacion
    ADD CONSTRAINT validaciones_capacitacion_estado_capacitacion_id_foreign FOREIGN KEY (estado_capacitacion_id) REFERENCES public.estados_capacitacion(id) ON DELETE RESTRICT;


--
-- Name: validaciones_capacitacion validaciones_capacitacion_user_id_validador_foreign; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.validaciones_capacitacion
    ADD CONSTRAINT validaciones_capacitacion_user_id_validador_foreign FOREIGN KEY (user_id_validador) REFERENCES public.users(id) ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict NmiQsbFfZhYuNYt7zVwsuyNS1knibEz0pmeTcvhuIYPq39ZB0OsBtn7lQl0sg0l

