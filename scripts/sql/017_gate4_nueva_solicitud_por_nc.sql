BEGIN;
ALTER TABLE public.aocr_tbsolicitud
 ADD COLUMN IF NOT EXISTS codigo_solicitud_origen integer,
 ADD COLUMN IF NOT EXISTS codigo_inspeccion_origen integer,
 ADD COLUMN IF NOT EXISTS codigo_informe_origen integer,
 ADD COLUMN IF NOT EXISTS codigo_nc_origen integer,
 ADD COLUMN IF NOT EXISTS modulo_origen varchar(80);

DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_solicitud_nc_origen_gate4') THEN
  ALTER TABLE public.aocr_tbsolicitud ADD CONSTRAINT fk_solicitud_nc_origen_gate4
   FOREIGN KEY(codigo_nc_origen) REFERENCES public.aocr_tbnoconformidad(codigo_no_conformidad) ON DELETE RESTRICT;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_solicitud_solicitud_origen_gate4') THEN
  ALTER TABLE public.aocr_tbsolicitud ADD CONSTRAINT fk_solicitud_solicitud_origen_gate4
   FOREIGN KEY(codigo_solicitud_origen) REFERENCES public.aocr_tbsolicitud(codigo_solicitud) ON DELETE RESTRICT;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_solicitud_inspeccion_origen_gate4') THEN
  ALTER TABLE public.aocr_tbsolicitud ADD CONSTRAINT fk_solicitud_inspeccion_origen_gate4
   FOREIGN KEY(codigo_inspeccion_origen) REFERENCES public.aocr_tbinspeccion(codigo_inspeccion) ON DELETE RESTRICT;
 END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_solicitud_informe_origen_gate4') THEN
  ALTER TABLE public.aocr_tbsolicitud ADD CONSTRAINT fk_solicitud_informe_origen_gate4
   FOREIGN KEY(codigo_informe_origen) REFERENCES public.aocr_tbinforme_inspeccion(codigo_informe) ON DELETE RESTRICT;
 END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_solicitud_activa_nc_gate4
 ON public.aocr_tbsolicitud(codigo_nc_origen)
 WHERE codigo_nc_origen IS NOT NULL AND deleted_at IS NULL
 AND UPPER(COALESCE(estado,'')) NOT IN ('FINALIZADO','CANCELADA','CANCELADO','ANULADA','ANULADO','RECHAZADA','RECHAZADO');
CREATE INDEX IF NOT EXISTS ix_solicitud_origen_gate4
 ON public.aocr_tbsolicitud(codigo_solicitud_origen,codigo_inspeccion_origen,codigo_informe_origen);
COMMIT;
