-- Rollback GATE F. ADVERTENCIA: elimina metadatos de archivos de subsanación.
DROP INDEX IF EXISTS public.idx_noconf_solicitud_ruta_version;
ALTER TABLE public.aocr_tbnoconformidad DROP COLUMN IF EXISTS fecha_subsanacion_rt;
ALTER TABLE public.aocr_tbnoconformidad DROP COLUMN IF EXISTS ruta_pdf_subsanacion_rt;
