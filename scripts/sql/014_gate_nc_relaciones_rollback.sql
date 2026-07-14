BEGIN;

-- Rollback no destructivo: elimina enforcement nuevo pero conserva relaciones ya registradas.
DROP TRIGGER IF EXISTS trg_aocr_nc_asignar_raiz ON public.aocr_tbnoconformidad;
DROP FUNCTION IF EXISTS public.aocr_fn_nc_asignar_raiz();
DROP INDEX IF EXISTS public.ix_aocr_nc_relaciones_reevaluacion;
DROP INDEX IF EXISTS public.ix_aocr_nc_relaciones_origen;
DROP INDEX IF EXISTS public.ux_aocr_nc_correlation;
DROP INDEX IF EXISTS public.ux_aocr_nc_solicitud_nueva;
DROP INDEX IF EXISTS public.ux_aocr_nc_solicitud_activa_por_raiz;
DROP INDEX IF EXISTS public.ux_aocr_nc_numero_version;
DROP INDEX IF EXISTS public.ux_aocr_nc_raiz_version;

ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_informe_cierre;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_inspeccion_nueva;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_solicitud_nueva;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_informe_origen;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_inspeccion_origen;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_solicitud_origen;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS fk_aocr_nc_raiz;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS ck_aocr_nc_version_positiva;
ALTER TABLE public.aocr_tbnoconformidad DROP CONSTRAINT IF EXISTS ck_aocr_nc_tipo_ruta;

COMMIT;
