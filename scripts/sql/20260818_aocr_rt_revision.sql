CREATE TABLE IF NOT EXISTS public.aocr_solicitud_rt_revision (
    id BIGSERIAL PRIMARY KEY,
    solicitud_rt_id BIGINT NOT NULL REFERENCES public.django_aocr_registro_rt(id) ON DELETE CASCADE,
    inspector_usuario VARCHAR(150) NOT NULL,
    coordinador_usuario_id INTEGER NOT NULL,
    estado VARCHAR(40) NOT NULL DEFAULT 'ASIGNADA',
    resultado VARCHAR(20),
    observacion TEXT,
    fecha_asignacion TIMESTAMP NOT NULL DEFAULT NOW(),
    fecha_revision TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_aocr_rt_revision_estado CHECK (estado IN ('ASIGNADA', 'EN_REVISION', 'DEVUELTA_COORDINADOR', 'CERRADA')),
    CONSTRAINT ck_aocr_rt_revision_resultado CHECK (resultado IS NULL OR resultado IN ('CONFORME', 'OBSERVADA', 'RECHAZADA'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_rt_revision_solicitud_activa
    ON public.aocr_solicitud_rt_revision (solicitud_rt_id)
    WHERE estado <> 'CERRADA';

CREATE INDEX IF NOT EXISTS ix_aocr_rt_revision_inspector
    ON public.aocr_solicitud_rt_revision (inspector_usuario, estado);

CREATE INDEX IF NOT EXISTS ix_aocr_rt_revision_coordinador
    ON public.aocr_solicitud_rt_revision (coordinador_usuario_id, estado);
