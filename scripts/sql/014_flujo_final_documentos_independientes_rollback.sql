BEGIN;

-- Rollback conservador: elimina solo restricciones e indices introducidos por
-- esta migracion. Las columnas y evidencias se conservan para no perder firmas,
-- hashes, versiones ni trazabilidad ya producida.
ALTER TABLE public.aocr_tbdocumento_generado DROP CONSTRAINT IF EXISTS ck_documento_final_estado;
DROP INDEX IF EXISTS public.ix_documento_final_bandeja;
DROP INDEX IF EXISTS public.ux_documento_final_version;
DROP INDEX IF EXISTS public.ux_documento_final_vigente;

COMMIT;
