CREATE TABLE IF NOT EXISTS public.aocr_proceso_estado
(
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL,
    orden_recaudacion_id INTEGER NULL,
    inspeccion_id INTEGER NULL,
    informe_id INTEGER NULL,
    estado_actual VARCHAR(100) NOT NULL,
    etapa_actual VARCHAR(100) NULL,
    rol_responsable VARCHAR(100) NULL,
    usuario_responsable_id INTEGER NULL,
    siguiente_accion VARCHAR(150) NULL,
    observacion TEXT NULL,
    fecha_estado TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_solicitud
    ON public.aocr_proceso_estado(solicitud_id);

CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_estado
    ON public.aocr_proceso_estado(estado_actual);

CREATE TABLE IF NOT EXISTS public.aocr_proceso_estado_historial
(
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL,
    orden_recaudacion_id INTEGER NULL,
    inspeccion_id INTEGER NULL,
    informe_id INTEGER NULL,
    estado_anterior VARCHAR(100) NULL,
    estado_nuevo VARCHAR(100) NOT NULL,
    accion VARCHAR(150) NULL,
    rol_usuario VARCHAR(100) NULL,
    usuario_id INTEGER NULL,
    observacion TEXT NULL,
    fecha_creacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_aocr_proceso_estado_historial_solicitud
    ON public.aocr_proceso_estado_historial(solicitud_id);
