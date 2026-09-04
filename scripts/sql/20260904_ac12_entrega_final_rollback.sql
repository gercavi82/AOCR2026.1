BEGIN;

UPDATE public.seguridad_rol_permiso SET activo=FALSE,actualizado_en=NOW(),actualizado_por='AC12_ROLLBACK'
WHERE id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR'));
UPDATE public.seguridad_permiso SET activo=FALSE,actualizado_en=NOW(),actualizado_por='AC12_ROLLBACK'
WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR');

DROP INDEX IF EXISTS public.ux_ac12_email_fisico;
DROP INDEX IF EXISTS public.ix_ac12_intento_entrega;
DROP INDEX IF EXISTS public.ix_ac12_destinatario_queue;
DROP INDEX IF EXISTS public.ix_ac12_destinatario_bandeja;
DROP INDEX IF EXISTS public.ix_ac12_documento_lookup;
DROP INDEX IF EXISTS public.ix_ac12_entrega_estado;

-- Las tablas, evidencias, MessageId y hashes se conservan deliberadamente.
-- Eliminar trazabilidad de entregas impediría auditoría y recuperación posterior.
COMMIT;
