-- Amplía únicamente la restricción de estados de documentos finales.
-- Conserva los estados válidos anteriores y no cambia expedientes históricos.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='30s';
DO $$
DECLARE definicion TEXT;
BEGIN
    SELECT pg_get_expr(conbin,conrelid) INTO definicion FROM pg_constraint
    WHERE conrelid='public.aocr_tbdocumento_generado'::regclass AND conname='ck_documento_final_estado';
    IF definicion IS NOT NULL AND POSITION('DOCUMENTOS_FINALES_COORDINADOR' IN definicion)=0 THEN
        ALTER TABLE public.aocr_tbdocumento_generado DROP CONSTRAINT ck_documento_final_estado;
        EXECUTE 'ALTER TABLE public.aocr_tbdocumento_generado ADD CONSTRAINT ck_documento_final_estado CHECK (('
            || definicion
            || ') OR estado IN (''DOCUMENTOS_FINALES_COORDINADOR'',''DOCUMENTOS_FINALES_DEVUELTOS''))';
    END IF;
END $$;
COMMIT;
