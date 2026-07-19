BEGIN;
CREATE TABLE IF NOT EXISTS public.aocr_evento_workflow (
 id BIGSERIAL PRIMARY KEY, evento VARCHAR(80) NOT NULL, event_key VARCHAR(300) NOT NULL,
 correlation_id VARCHAR(80) NOT NULL, modulo VARCHAR(80), accion VARCHAR(100), entidad VARCHAR(100), entidad_id INTEGER,
 solicitud_id INTEGER, inspeccion_id INTEGER, informe_id INTEGER, nc_id INTEGER, documento_id INTEGER,
 estado_anterior VARCHAR(100), estado_nuevo VARCHAR(100), usuario_id INTEGER, usuario VARCHAR(150), rol VARCHAR(100), ip VARCHAR(64),
 observacion TEXT, version INTEGER, hash VARCHAR(128), resultado VARCHAR(40) NOT NULL DEFAULT 'REGISTRADO', detalle_error TEXT,
 intentos INTEGER NOT NULL DEFAULT 1, fecha TIMESTAMP NOT NULL DEFAULT NOW(), created_at TIMESTAMP NOT NULL DEFAULT NOW(), updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
 CONSTRAINT uq_aocr_evento_workflow_event_key UNIQUE(event_key), CONSTRAINT ck_aocr_evento_intentos CHECK(intentos>0));
ALTER TABLE public.aocr_evento_workflow ADD COLUMN IF NOT EXISTS created_at TIMESTAMP NOT NULL DEFAULT NOW();
CREATE INDEX IF NOT EXISTS ix_aocr_evento_workflow_correlation ON public.aocr_evento_workflow(correlation_id,fecha);
CREATE INDEX IF NOT EXISTS ix_aocr_evento_workflow_solicitud ON public.aocr_evento_workflow(solicitud_id,fecha);
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS event_key VARCHAR(300);
ALTER TABLE public.email_queue ADD COLUMN IF NOT EXISTS correlation_id VARCHAR(80);
CREATE UNIQUE INDEX IF NOT EXISTS uq_email_queue_event_key ON public.email_queue(event_key) WHERE event_key IS NOT NULL;
COMMIT;
