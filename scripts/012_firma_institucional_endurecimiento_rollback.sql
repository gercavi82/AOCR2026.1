BEGIN;

DROP INDEX IF EXISTS public.idx_aocr_documento_pendiente_firma_dgac;
DROP INDEX IF EXISTS public.idx_aocr_documento_pendiente_firma_dcav;

-- Los estados documentales no se revierten automáticamente para no invalidar
-- firmas institucionales que pudieran haberse generado después del despliegue.

COMMIT;
