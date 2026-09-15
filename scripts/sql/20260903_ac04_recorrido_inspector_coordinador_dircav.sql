-- =============================================================================
-- Migración: 20260903_ac04_recorrido_inspector_coordinador_dircav.sql
-- Descripción: AC-04 Recorrido canónico de revisión documental e informe técnico:
--              Inspector -> Coordinador -> DIRCAV.
--              Agrega llaves foráneas, índices, restricciones de estado y
--              asegura la estructura sin dependencia de creación dinámica en DAO.
-- =============================================================================

BEGIN;

-- 1. Tabla principal de control de revisión documental por Coordinación
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

-- Columnas complementarias si no existen
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS numero_oficio VARCHAR(80) NULL;
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS fecha_habilitacion_lv TIMESTAMP WITHOUT TIME ZONE NULL;
ALTER TABLE public.aocr_revision_documental_coordinador ADD COLUMN IF NOT EXISTS fecha_habilitacion_informe TIMESTAMP WITHOUT TIME ZONE NULL;

-- 2. Clave foránea hacia aocr_tbsolicitud
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_name = 'fk_revdoc_coord_solicitud'
          AND table_name = 'aocr_revision_documental_coordinador'
    ) THEN
        ALTER TABLE public.aocr_revision_documental_coordinador
            ADD CONSTRAINT fk_revdoc_coord_solicitud
            FOREIGN KEY (solicitud_id)
            REFERENCES public.aocr_tbsolicitud(codigo_solicitud)
            ON DELETE CASCADE;
    END IF;
END $$;

-- 3. Índices de optimización
CREATE UNIQUE INDEX IF NOT EXISTS ux_aocr_revdoc_coord_solicitud
    ON public.aocr_revision_documental_coordinador(solicitud_id);

CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_solicitud
    ON public.aocr_revision_documental_coordinador(solicitud_id);

CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_estado
    ON public.aocr_revision_documental_coordinador(estado);

CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_coordinador
    ON public.aocr_revision_documental_coordinador(coordinador_id);

CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_inspector_orig
    ON public.aocr_revision_documental_coordinador(inspector_original_id);

CREATE INDEX IF NOT EXISTS ix_aocr_revdoc_coord_inspector_conf
    ON public.aocr_revision_documental_coordinador(inspector_confirmado_id);

-- 4. Restricción CHECK de estados canónicos permitidos
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_name = 'chk_aocr_revdoc_coord_estado'
          AND table_name = 'aocr_revision_documental_coordinador'
    ) THEN
        ALTER TABLE public.aocr_revision_documental_coordinador
            ADD CONSTRAINT chk_aocr_revdoc_coord_estado
            CHECK (estado IN (
                'PENDIENTE_REVISION_INSPECTOR',
                'REVISION_INSPECTOR_EN_PROCESO',
                'PENDIENTE_COORDINADOR',
                'PENDIENTE_REVISION_COORDINADOR',
                'DEVUELTO_INSPECTOR',
                'PENDIENTE_DIRCAV',
                'OBSERVADA_POR_COORDINADOR',
                'ACEPTADA_POR_COORDINADOR'
            ));
    END IF;
END $$;

-- 5. Tabla de historial de confirmación / reasignación de inspectores
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

-- 6. Clave foránea en historial hacia aocr_tbsolicitud
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_name = 'fk_reasig_solicitud'
          AND table_name = 'aocr_inspector_reasignacion_historial'
    ) THEN
        ALTER TABLE public.aocr_inspector_reasignacion_historial
            ADD CONSTRAINT fk_reasig_solicitud
            FOREIGN KEY (solicitud_id)
            REFERENCES public.aocr_tbsolicitud(codigo_solicitud)
            ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_aocr_reasignacion_solicitud
    ON public.aocr_inspector_reasignacion_historial(solicitud_id);

CREATE INDEX IF NOT EXISTS ix_aocr_reasignacion_inspector_nuevo
    ON public.aocr_inspector_reasignacion_historial(inspector_nuevo_id);

COMMIT;
