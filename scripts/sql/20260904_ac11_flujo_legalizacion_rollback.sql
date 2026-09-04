BEGIN;

DROP INDEX IF EXISTS public.ix_ac11_bandeja_dirdac;
DROP INDEX IF EXISTS public.ux_ac11_email_idempotente;
DROP INDEX IF EXISTS public.ux_ac11_notificacion_idempotente;
DROP INDEX IF EXISTS public.ux_ac11_evento_idempotente;
DROP INDEX IF EXISTS public.ux_ac11_firma_documento_version;

-- No se eliminan datos, permisos ni estados históricos. Se restaura únicamente
-- la restricción documental previa compatible con AC-10.
ALTER TABLE public.aocr_tbdocumento_generado DROP CONSTRAINT IF EXISTS ck_documento_final_estado;
ALTER TABLE public.aocr_tbdocumento_generado ADD CONSTRAINT ck_documento_final_estado CHECK (estado IN (
    'GENERADO','LIBERADO_RT','VERSION_ANTERIOR',
    'AOCR_BORRADOR_INSPECTOR','AOCR_LISTO_PARA_FIRMA','PENDIENTE_FIRMA_AOCR_DIRDAC','AOCR_FIRMADO_DIRDAC',
    'CONDICIONES_BORRADOR_INSPECTOR','CONDICIONES_LISTAS_PARA_FIRMA','PENDIENTE_FIRMA_CONDICIONES_DCAV','CONDICIONES_FIRMADAS_DCAV'
));

COMMIT;
