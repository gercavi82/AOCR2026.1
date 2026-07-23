BEGIN;

CREATE TABLE IF NOT EXISTS public.aocr_revision_documental_coordinador
(
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL UNIQUE,
    inspector_original_id INTEGER NULL,
    inspector_confirmado_id INTEGER NULL,
    coordinador_id INTEGER NULL,
    documento_oficio_id INTEGER NULL,
    numero_oficio VARCHAR(80) NULL,
    estado VARCHAR(80) NOT NULL,
    observacion_inspector TEXT NULL,
    observacion_coordinador TEXT NULL,
    fecha_finalizacion_inspector TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_decision_coordinador TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_habilitacion_lv TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_habilitacion_informe TIMESTAMP WITHOUT TIME ZONE NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    fecha_actualizacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS numero_oficio VARCHAR(80) NULL;
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS fecha_habilitacion_lv TIMESTAMP WITHOUT TIME ZONE NULL;
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS fecha_habilitacion_informe TIMESTAMP WITHOUT TIME ZONE NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_revdoc_coord_solicitud
    ON public.aocr_revision_documental_coordinador(solicitud_id);

CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_solicitud
    ON public.aocr_revision_documental_coordinador(solicitud_id);
CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_estado
    ON public.aocr_revision_documental_coordinador(estado);

CREATE TABLE IF NOT EXISTS public.aocr_inspector_reasignacion_historial
(
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL,
    inspector_anterior_id INTEGER NULL,
    inspector_nuevo_id INTEGER NOT NULL,
    coordinador_id INTEGER NOT NULL,
    motivo TEXT NULL,
    estado VARCHAR(80) NOT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_aocr_reasignacion_solicitud
    ON public.aocr_inspector_reasignacion_historial(solicitud_id);

COMMIT;
