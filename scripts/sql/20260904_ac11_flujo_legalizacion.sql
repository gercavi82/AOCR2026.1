BEGIN;

-- AC-11 reutiliza aocr_proceso_estado como estado/version del expediente y
-- aocr_tbdocumento_generado como estado/version independiente de AOCR y CL.
ALTER TABLE public.aocr_tbdocumento_generado DROP CONSTRAINT IF EXISTS ck_documento_final_estado;
ALTER TABLE public.aocr_tbdocumento_generado ADD CONSTRAINT ck_documento_final_estado CHECK (estado IN (
    'GENERADO','LIBERADO_RT','VERSION_ANTERIOR',
    'AOCR_BORRADOR_INSPECTOR','AOCR_LISTO_PARA_FIRMA','PENDIENTE_FIRMA_AOCR_DIRDAC',
    'AOCR_PENDIENTE_DIRDAC','AOCR_FIRMADO_DIRDAC','AOCR_FIRMADA_DIRDAC',
    'CONDICIONES_BORRADOR_INSPECTOR','CONDICIONES_LISTAS_PARA_FIRMA',
    'PENDIENTE_FIRMA_CONDICIONES_DCAV','CL_PENDIENTE_FIRMA_DIRCAV',
    'CONDICIONES_FIRMADAS_DCAV','CL_FIRMADA_DIRCAV'
));

CREATE UNIQUE INDEX IF NOT EXISTS ux_ac11_evento_idempotente
    ON public.aocr_evento_workflow(event_key);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ac11_firma_documento_version
    ON public.aocr_tbfirma_documento(codigo_solicitud, UPPER(tipo_documento), version);
CREATE UNIQUE INDEX IF NOT EXISTS ux_ac11_notificacion_idempotente
    ON public.aocr_tbnotificacion(event_key) WHERE event_key IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_ac11_email_idempotente
    ON public.email_queue(event_key) WHERE event_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_ac11_bandeja_dirdac
    ON public.aocr_proceso_estado(estado_actual, fecha_estado)
    WHERE activo=TRUE AND estado_actual IN ('AOCR_PENDIENTE_DIRDAC','AOCR_FIRMADA_DIRDAC','FIRMAS_COMPLETAS');

-- Permisos ya introducidos por la migración de segregación DIRCAV/DIRDAC.
UPDATE public.seguridad_permiso SET activo=TRUE
WHERE codigo IN ('DIRCAV_REMITIR_DIRDAC','DIRDAC_VER_BANDEJA','DIRDAC_DEVOLVER_DIRCAV','DIRDAC_FIRMAR_AOCR');

-- Defensa adicional: ADMINISTRADOR no recibe acciones operativas AC-11.
UPDATE public.seguridad_rol_permiso rp SET activo=FALSE
WHERE rp.codigorol=1 AND rp.id_permiso IN (
    SELECT id_permiso FROM public.seguridad_permiso
    WHERE codigo IN ('DIRCAV_REMITIR_DIRDAC','DIRDAC_DEVOLVER_DIRCAV','DIRDAC_FIRMAR_AOCR')
);

COMMIT;
