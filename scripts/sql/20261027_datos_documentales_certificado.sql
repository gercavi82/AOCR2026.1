-- Sólo esquema. No reescribe certificados ni documentos históricos.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='30s';
ALTER TABLE public.aocr_tbsolicitud ADD COLUMN IF NOT EXISTS datos_documentales_certificado JSONB NULL;
-- Cada solicitud conserva su certificado/version. Renovaciones y modificaciones
-- comparten el numero de la emision original; la unicidad por solicitud permanece.
ALTER TABLE public.aocr_tbcertificado DROP CONSTRAINT IF EXISTS aocr_tbcertificado_numero_certificado_key;
CREATE INDEX IF NOT EXISTS idx_certificado_numero ON public.aocr_tbcertificado(numero_certificado);
CREATE SEQUENCE IF NOT EXISTS public.aocr_numero_certificado_seq START 1;
DO $$
DECLARE maximo BIGINT; actual BIGINT; utilizada BOOLEAN;
BEGIN
    SELECT COALESCE(MAX(SUBSTRING(numero_aocr FROM '([0-9]+)$')::BIGINT),0) INTO maximo
    FROM public.aocr_tbsolicitud WHERE numero_aocr ~ '^DGAC-129-[A-Z0-9]{3}-[0-9]+$';
    SELECT last_value,is_called INTO actual,utilizada FROM public.aocr_numero_certificado_seq;
    PERFORM setval('public.aocr_numero_certificado_seq',GREATEST(actual,maximo,1),utilizada OR maximo>0);
END $$;
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_original_numero_canonico
ON public.aocr_tbsolicitud(numero_aocr)
WHERE tipo_solicitud=1 AND numero_aocr ~ '^DGAC-129-[A-Z0-9]{3}-[0-9]+$';
CREATE OR REPLACE FUNCTION public.aocr_conservar_numero_original() RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF NULLIF(TRIM(OLD.numero_aocr),'') IS NOT NULL AND NEW.numero_aocr IS DISTINCT FROM OLD.numero_aocr THEN
        RAISE EXCEPTION 'El número AOCR asignado es inmutable';
    END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS trg_aocr_numero_original_inmutable ON public.aocr_tbsolicitud;
CREATE TRIGGER trg_aocr_numero_original_inmutable BEFORE UPDATE OF numero_aocr ON public.aocr_tbsolicitud
FOR EACH ROW EXECUTE PROCEDURE public.aocr_conservar_numero_original();
COMMIT;
