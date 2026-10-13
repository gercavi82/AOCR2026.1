-- Aplicar antes de publicar: el RT solicita lugares sin fijar fechas.
-- Conserva todas las fechas históricas; no modifica usuarios ni expedientes.
BEGIN;
ALTER TABLE public.aocr_tbsolicitud_estacion
    ALTER COLUMN fecha_inicio DROP NOT NULL,
    ALTER COLUMN fecha_fin DROP NOT NULL,
    ALTER COLUMN fecha_inicio DROP DEFAULT,
    ALTER COLUMN fecha_fin DROP DEFAULT;
CREATE TABLE IF NOT EXISTS public.aocr_or_correccion_financiera (
    id bigserial PRIMARY KEY,
    orden_id integer NOT NULL REFERENCES public.aocr_or_orden(id),
    solicitud_id integer NOT NULL,
    estado_original text NOT NULL,
    estado text NOT NULL DEFAULT 'PENDIENTE',
    motivo text NOT NULL,
    solicitado_por integer NOT NULL,
    solicitado_en timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
    orden_anterior jsonb NOT NULL,
    detalles_anteriores jsonb NOT NULL,
    corregido_por integer,
    corregido_en timestamp,
    observacion_financiera text,
    validado_por integer,
    validado_en timestamp,
    referencia_validacion text,
    comprobante_rectificado_ruta text,
    monto_verificado numeric(18,2),
    factura_rectificada_ruta text
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_or_correccion_pendiente
    ON public.aocr_or_correccion_financiera(orden_id) WHERE estado <> 'VALIDADA';
COMMIT;
