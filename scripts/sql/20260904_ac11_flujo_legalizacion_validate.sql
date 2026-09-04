SELECT codigo, activo FROM public.seguridad_permiso
WHERE codigo IN ('DIRCAV_REMITIR_DIRDAC','DIRDAC_VER_BANDEJA','DIRDAC_DEVOLVER_DIRCAV','DIRDAC_FIRMAR_AOCR')
ORDER BY codigo;

SELECT indexname FROM pg_indexes WHERE schemaname='public'
AND indexname IN ('ux_ac11_evento_idempotente','ux_ac11_firma_documento_version','ux_ac11_notificacion_idempotente','ux_ac11_email_idempotente','ix_ac11_bandeja_dirdac')
ORDER BY indexname;

SELECT estado_actual, COUNT(*) FROM public.aocr_proceso_estado
WHERE activo=TRUE AND estado_actual IN ('AOCR_PENDIENTE_DIRDAC','DEVUELTO_DIRCAV','AOCR_FIRMADA_DIRDAC','FIRMAS_COMPLETAS')
GROUP BY estado_actual ORDER BY estado_actual;
