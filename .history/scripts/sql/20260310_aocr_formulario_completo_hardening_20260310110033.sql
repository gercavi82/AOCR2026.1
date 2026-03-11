-- Hardening AOCR FormularioCompleto
-- Objetivo: alinear esquema para evitar 42703 (columna faltante), 23514 (check estado), 42P01 (tabla historial)

BEGIN;

-- =========================================================
-- 1) aocr_tbaeronave_solicitud: asegurar created_at
-- =========================================================
DO $$
BEGIN
    IF to_regclass('public.aocr_tbaeronave_solicitud') IS NOT NULL THEN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'aocr_tbaeronave_solicitud'
              AND column_name = 'created_at'
        ) THEN
            ALTER TABLE public.aocr_tbaeronave_solicitud
                ADD COLUMN created_at TIMESTAMP NOT NULL DEFAULT NOW();
        END IF;

        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'aocr_tbaeronave_solicitud'
              AND column_name = 'created_by'
        ) THEN
            ALTER TABLE public.aocr_tbaeronave_solicitud
                ADD COLUMN created_by VARCHAR(100) NULL;
        END IF;
    END IF;
END $$;

-- =========================================================
-- 2) aocr_tbdocumento: normalizar constraint de estado
-- =========================================================
DO $$
DECLARE
    v_def text;
BEGIN
    IF to_regclass('public.aocr_tbdocumento') IS NOT NULL THEN
        -- Asegurar que la columna estado existe
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'aocr_tbdocumento'
              AND column_name = 'estado'
        ) THEN
            ALTER TABLE public.aocr_tbdocumento
                ADD COLUMN estado VARCHAR(30) NOT NULL DEFAULT 'PENDIENTE';
        END IF;

        -- Obtener definicion actual del check si existe
        SELECT pg_get_constraintdef(c.oid)
        INTO v_def
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname = 'aocr_tbdocumento'
          AND c.conname = 'chk_estado_documento'
        LIMIT 1;

        IF v_def IS NOT NULL THEN
            ALTER TABLE public.aocr_tbdocumento DROP CONSTRAINT chk_estado_documento;
        END IF;

        -- Constraint de estados permitidos para flujo AOCR
        ALTER TABLE public.aocr_tbdocumento
            ADD CONSTRAINT chk_estado_documento
            CHECK (
                estado IN (
                    'PENDIENTE',
                    'Cargado',
                    'CARGADO',
                    'REGISTRADO',
                    'BORRADOR',
                    'VALIDADO',
                    'RECHAZADO',
                    'SUBSANACION',
                    'OBSERVADO',
                    'APROBADO'
                )
            );
    END IF;
END $$;

-- =========================================================
-- 3) aocr_tbhistorialestado: crear/compatibilizar estructura
-- =========================================================
CREATE TABLE IF NOT EXISTS public.aocr_tbhistorialestado (
    codigohistorial SERIAL PRIMARY KEY,
    codigosolicitud INTEGER NOT NULL,
    estadoanterior VARCHAR(50),
    estadonuevo VARCHAR(50) NOT NULL,
    codigousuario INTEGER,
    observaciones TEXT,
    fechacambio TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigo_solicitud'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigosolicitud'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN codigo_solicitud TO codigosolicitud;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'estado_anterior'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'estadoanterior'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN estado_anterior TO estadoanterior;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'estado_nuevo'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'estadonuevo'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN estado_nuevo TO estadonuevo;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigo_usuario'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigousuario'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN codigo_usuario TO codigousuario;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'fecha_cambio'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'fechacambio'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN fecha_cambio TO fechacambio;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_historial_solicitud'
          AND conrelid = 'public.aocr_tbhistorialestado'::regclass
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado
            ADD CONSTRAINT fk_historial_solicitud
            FOREIGN KEY (codigosolicitud)
            REFERENCES public.aocr_tbsolicitud(codigo_solicitud)
            ON DELETE CASCADE;
    END IF;
EXCEPTION
    WHEN undefined_column THEN
        NULL;
END $$;

CREATE INDEX IF NOT EXISTS idx_historial_solicitud
    ON public.aocr_tbhistorialestado(codigosolicitud);

CREATE INDEX IF NOT EXISTS idx_historial_fecha
    ON public.aocr_tbhistorialestado(fechacambio);

COMMIT;
