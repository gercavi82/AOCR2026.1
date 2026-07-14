BEGIN;

-- Evita registrar dos veces la misma evidencia firmada sin impedir nuevas versiones.
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_firma_documento_evidencia
    ON public.aocr_tbfirma_documento(codigo_solicitud, UPPER(tipo_documento), hash_documento)
    WHERE NULLIF(TRIM(hash_documento), '') IS NOT NULL;

CREATE INDEX IF NOT EXISTS ix_aocr_firma_documento_cierre
    ON public.aocr_tbfirma_documento(codigo_solicitud, UPPER(tipo_documento), fecha_firma DESC);

COMMIT;
