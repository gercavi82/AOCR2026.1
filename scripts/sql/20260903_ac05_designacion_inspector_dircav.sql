-- =============================================================================
-- MIGRACIÓN ADITIVA: AC-05 DESIGNACIÓN DEL INSPECTOR POR DIRCAV
-- Tabla: public.aocr_tbdesignacion_inspector
-- Garantiza: Trazabilidad, histórico de reasignaciones y unicidad de designación vigente.
-- =============================================================================

START TRANSACTION;

CREATE TABLE IF NOT EXISTS public.aocr_tbdesignacion_inspector (
    id SERIAL PRIMARY KEY,
    solicitud_id INTEGER NOT NULL REFERENCES public.aocr_tbsolicitud(codigo_solicitud),
    inspeccion_id INTEGER NULL,
    estacion_id INTEGER NULL REFERENCES public.aocr_tbsolicitud_estacion(id),
    inspector_id INTEGER NOT NULL,
    inspector_cedula VARCHAR(30) NOT NULL,
    inspector_nombre VARCHAR(200) NOT NULL,
    inspector_apoyo_cedula VARCHAR(30) NULL,
    inspector_apoyo_nombre VARCHAR(200) NULL,
    dircav_usuario_id INTEGER NOT NULL,
    dircav_usuario_nombre VARCHAR(200) NULL,
    estado VARCHAR(80) NOT NULL DEFAULT 'DESIGNACION_PENDIENTE_FIRMA_DIRCAV',
    motivo TEXT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    vigente BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_designacion TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    fecha_firma TIMESTAMP WITHOUT TIME ZONE NULL,
    creado_en TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    creado_por VARCHAR(100) NULL,
    actualizado_en TIMESTAMP WITHOUT TIME ZONE NULL,
    actualizado_por VARCHAR(100) NULL
);

COMMENT ON TABLE public.aocr_tbdesignacion_inspector IS 
    'Registro formal e histórico de designaciones y reasignaciones de inspectores por DIRCAV (AC-05).';

-- Índice para unicidad de la designación vigente por solicitud y estación
CREATE UNIQUE INDEX IF NOT EXISTS uq_aocr_designacion_vigente 
    ON public.aocr_tbdesignacion_inspector (solicitud_id, COALESCE(estacion_id, 0)) 
    WHERE vigente = TRUE;

-- Índices de consulta rápida
CREATE INDEX IF NOT EXISTS idx_aocr_designacion_solicitud 
    ON public.aocr_tbdesignacion_inspector (solicitud_id);

CREATE INDEX IF NOT EXISTS idx_aocr_designacion_inspector 
    ON public.aocr_tbdesignacion_inspector (inspector_cedula);

CREATE INDEX IF NOT EXISTS idx_aocr_designacion_estado 
    ON public.aocr_tbdesignacion_inspector (estado);

COMMIT;
