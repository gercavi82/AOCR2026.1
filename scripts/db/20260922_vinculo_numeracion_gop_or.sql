-- Defensa de la relacion GOP/OR, incluso ante clientes con codigo anterior.
BEGIN;
SET LOCAL lock_timeout = '10s';
CREATE OR REPLACE FUNCTION public.aocr_validar_numero_gop_or()
RETURNS trigger LANGUAGE plpgsql AS $$
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
END $$;
DROP TRIGGER IF EXISTS trg_aocr_numero_or ON public.aocr_or_orden;
CREATE TRIGGER trg_aocr_numero_or BEFORE INSERT OR UPDATE OF numero_orden, codigo_solicitud
 ON public.aocr_or_orden FOR EACH ROW EXECUTE FUNCTION public.aocr_validar_numero_gop_or();
DROP TRIGGER IF EXISTS trg_aocr_numero_gop ON public.aocr_tbsolicitud;
CREATE TRIGGER trg_aocr_numero_gop BEFORE UPDATE OF numero_solicitud
 ON public.aocr_tbsolicitud FOR EACH ROW EXECUTE FUNCTION public.aocr_validar_numero_gop_or();
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_solicitud_numero_institucional
 ON public.aocr_tbsolicitud(numero_solicitud) WHERE numero_solicitud LIKE 'DGAC-GOP-%';
COMMIT;
