BEGIN;
ALTER TABLE public.aocr_tbsolicitud
 ADD COLUMN IF NOT EXISTS modulo_destino varchar(80),
 ADD COLUMN IF NOT EXISTS tipo_tramite_origen varchar(80);
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='chk_solicitud_modulo_destino_gate4') THEN
  ALTER TABLE public.aocr_tbsolicitud ADD CONSTRAINT chk_solicitud_modulo_destino_gate4 CHECK
  (modulo_destino IS NULL OR modulo_destino IN
   ('M5_SOLICITUD_INSPECCION_EMISION_RENOVACION','M6_SOLICITUD_INSPECCION_MODIFICACION'));
 END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_solicitud_modulo_destino_gate4 ON public.aocr_tbsolicitud(modulo_destino,tipo_tramite_origen);
COMMIT;
