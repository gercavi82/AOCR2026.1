-- Revierte solo optimizaciones de la segunda bandeja. No revierte decisiones DCAV ni borra trazabilidad.
BEGIN;
DROP INDEX IF EXISTS public.ix_aocr_historial_envio_documentos_dcav;
DROP INDEX IF EXISTS public.ix_aocr_documentos_revision_dcav;
DROP INDEX IF EXISTS public.ix_aocr_pdf_documento_base;
COMMIT;
