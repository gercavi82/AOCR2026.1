-- AOCR - Regla institucional de viaticos (N dias de inspeccion => N - 1 dias pagados).
-- Idempotente. No actualiza ordenes ni detalles historicos.
BEGIN;

ALTER TABLE public.aocr_or_orden_detalle
    ADD COLUMN IF NOT EXISTS lugar_inspeccion VARCHAR(200);

ALTER TABLE public.aocr_or_orden_detalle
    ALTER COLUMN lugar_inspeccion TYPE VARCHAR(200);

ALTER TABLE public.aocr_or_orden_detalle
    ADD COLUMN IF NOT EXISTS provincia_inspeccion VARCHAR(150);

ALTER TABLE public.aocr_or_orden_detalle
    ADD COLUMN IF NOT EXISTS numero_dias_inspeccion INTEGER;

ALTER TABLE public.aocr_or_orden_detalle
    ADD COLUMN IF NOT EXISTS dias_pagados_viatico INTEGER;

CREATE OR REPLACE FUNCTION public.aocr_or_detalle_calcular_linea()
RETURNS trigger
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

COMMIT;
