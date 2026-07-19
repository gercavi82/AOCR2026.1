BEGIN;

-- Versionado e integridad de los dos documentos finales.
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version_documento INTEGER NOT NULL DEFAULT 1;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS vigente BOOLEAN NOT NULL DEFAULT TRUE;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS completo BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS bloqueado BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS hash_pdf VARCHAR(128);
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS ruta_pdf_firmado VARCHAR(500);
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS tamanio_pdf_firmado BIGINT;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS codigo_usuario_firma INTEGER;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS rol_firma VARCHAR(100);
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS fecha_firma TIMESTAMP;
ALTER TABLE public.aocr_tbdocumento_generado ADD COLUMN IF NOT EXISTS version_concurrencia BIGINT NOT NULL DEFAULT 1;

WITH numbered AS (
    SELECT codigo_documento,
           row_number() OVER (PARTITION BY codigo_solicitud,UPPER(tipo_documento)
                              ORDER BY fecha_generacion,codigo_documento)::integer AS version_calculada
    FROM public.aocr_tbdocumento_generado
)
UPDATE public.aocr_tbdocumento_generado d
SET version_documento=n.version_calculada
FROM numbered n
WHERE n.codigo_documento=d.codigo_documento AND d.version_documento<>n.version_calculada;

-- Normaliza los duplicados legacy antes de imponer una sola version vigente.
WITH ranked AS (
    SELECT codigo_documento,
           row_number() OVER (PARTITION BY codigo_solicitud, UPPER(tipo_documento)
                              ORDER BY fecha_generacion DESC, codigo_documento DESC) AS rn
    FROM public.aocr_tbdocumento_generado
    WHERE vigente = TRUE
)
UPDATE public.aocr_tbdocumento_generado d
SET vigente = FALSE,
    estado = CASE WHEN d.bloqueado THEN d.estado ELSE 'VERSION_ANTERIOR' END
FROM ranked r
WHERE r.codigo_documento = d.codigo_documento AND r.rn > 1;

CREATE UNIQUE INDEX IF NOT EXISTS ux_documento_final_vigente
    ON public.aocr_tbdocumento_generado(codigo_solicitud, UPPER(tipo_documento))
    WHERE vigente = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ux_documento_final_version
    ON public.aocr_tbdocumento_generado(codigo_solicitud, UPPER(tipo_documento), version_documento);

CREATE INDEX IF NOT EXISTS ix_documento_final_bandeja
    ON public.aocr_tbdocumento_generado(estado, vigente, tipo_documento);

ALTER TABLE public.aocr_tbdocumento_generado DROP CONSTRAINT IF EXISTS ck_documento_final_estado;
ALTER TABLE public.aocr_tbdocumento_generado ADD CONSTRAINT ck_documento_final_estado CHECK (estado IN (
    'GENERADO','LIBERADO_RT','VERSION_ANTERIOR',
    'AOCR_BORRADOR_INSPECTOR','AOCR_LISTO_PARA_FIRMA','PENDIENTE_FIRMA_AOCR_DIRDAC','AOCR_FIRMADO_DIRDAC',
    'CONDICIONES_BORRADOR_INSPECTOR','CONDICIONES_LISTAS_PARA_FIRMA','PENDIENTE_FIRMA_CONDICIONES_DCAV','CONDICIONES_FIRMADAS_DCAV'
));

-- Outbox interna con idempotencia por destinatario. La cola SMTP ya dispone de
-- su indice unico event_key y conserva PENDIENTE/ERROR para reintentos.
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS modulo VARCHAR(100);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS entidad_id INTEGER;
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS tipo_entidad VARCHAR(100);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS event_key VARCHAR(300);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(80);
ALTER TABLE public.aocr_tbnotificacion ADD COLUMN IF NOT EXISTS updated_at TIMESTAMP;

CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_tbnotificacion_event_key
    ON public.aocr_tbnotificacion(event_key) WHERE event_key IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key
    ON public.email_queue(event_key) WHERE event_key IS NOT NULL;

COMMIT;
