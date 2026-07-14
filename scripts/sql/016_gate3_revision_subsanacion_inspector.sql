BEGIN;

ALTER TABLE public.aocr_tbdocumento_subsanacion
    ADD COLUMN IF NOT EXISTS decision_inspector varchar(40),
    ADD COLUMN IF NOT EXISTS comentario_inspector varchar(2000),
    ADD COLUMN IF NOT EXISTS codigo_usuario_revision integer,
    ADD COLUMN IF NOT EXISTS fecha_revision timestamp;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='chk_docsub_decision_gate3') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion ADD CONSTRAINT chk_docsub_decision_gate3 CHECK
        (decision_inspector IS NULL OR decision_inspector IN ('ACEPTADO_SUBSANACION','RECHAZADO_SUBSANACION'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='chk_docsub_rechazo_comentario_gate3') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion ADD CONSTRAINT chk_docsub_rechazo_comentario_gate3 CHECK
        (decision_inspector<>'RECHAZADO_SUBSANACION' OR NULLIF(TRIM(comentario_inspector),'') IS NOT NULL);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_docsub_revisor_gate3') THEN
        ALTER TABLE public.aocr_tbdocumento_subsanacion ADD CONSTRAINT fk_docsub_revisor_gate3
        FOREIGN KEY(codigo_usuario_revision) REFERENCES public.usuario(idusuario) ON DELETE RESTRICT;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_docsub_revision_gate3
    ON public.aocr_tbdocumento_subsanacion(codigo_no_conformidad,decision_inspector,fecha_revision DESC)
    WHERE codigo_no_conformidad IS NOT NULL;

DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname='chk_estado_documento'
               AND conrelid='public.aocr_tbdocumento'::regclass) THEN
        ALTER TABLE public.aocr_tbdocumento DROP CONSTRAINT chk_estado_documento;
    END IF;
    ALTER TABLE public.aocr_tbdocumento ADD CONSTRAINT chk_estado_documento CHECK
    (estado IS NULL OR estado IN ('Cargado',U&'En Revisi\00F3n','Aprobado','Rechazado','Subsanado',
     'PENDIENTE_REVISION','PENDIENTE_REVISION_SUBSANACION','ACEPTADO','APROBADO','OBSERVADO','DEVUELTO',
     'DEVUELTO_INSPECTOR','PENDIENTE_SUBSANACION','SUBSANADO_RT','SUBSANADO','SUBSANACION',
     'EN_REVISION_INSPECTOR','RECHAZADO','BLOQUEADO','VERSION_ANTERIOR','CARGADO',
     'ACEPTADO_SUBSANACION','RECHAZADO_SUBSANACION'));
END $$;

COMMIT;
