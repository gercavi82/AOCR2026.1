-- Compatibilidad del INSERT desplegado de SolicitudEstacionDAO con la auditoria historica.
-- No modifica registros existentes ni elimina restricciones NOT NULL.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '30s';

ALTER TABLE public.aocr_tbauditoria
    ADD COLUMN IF NOT EXISTS usuario_id integer NULL,
    ADD COLUMN IF NOT EXISTS fecha timestamp without time zone NULL;

CREATE OR REPLACE FUNCTION public.aocr_auditoria_estaciones_compat()
RETURNS trigger LANGUAGE plpgsql AS $$
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

DROP TRIGGER IF EXISTS trg_aocr_auditoria_estaciones_compat ON public.aocr_tbauditoria;
CREATE TRIGGER trg_aocr_auditoria_estaciones_compat
BEFORE INSERT ON public.aocr_tbauditoria
FOR EACH ROW EXECUTE FUNCTION public.aocr_auditoria_estaciones_compat();

COMMIT;
