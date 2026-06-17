-- Tabla de historial de estados de solicitudes AOCR
-- Compatibiliza esquema antiguo (snake_case) con el esquema usado por HistorialEstadoDAO.

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
    -- Migración de nombres antiguos -> actuales (solo si aplica)
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'id'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'codigohistorial'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN id TO codigohistorial;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'codigo_solicitud'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'codigosolicitud'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN codigo_solicitud TO codigosolicitud;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'estado_anterior'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'estadoanterior'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN estado_anterior TO estadoanterior;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'estado_nuevo'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'estadonuevo'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN estado_nuevo TO estadonuevo;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'codigo_usuario'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'codigousuario'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN codigo_usuario TO codigousuario;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'fecha_cambio'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'aocr_tbhistorialestado'
          AND column_name = 'fechacambio'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado RENAME COLUMN fecha_cambio TO fechacambio;
    END IF;

    -- Asegurar columnas requeridas
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigohistorial'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN codigohistorial SERIAL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigosolicitud'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN codigosolicitud INTEGER;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'estadoanterior'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN estadoanterior VARCHAR(50);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'estadonuevo'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN estadonuevo VARCHAR(50);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'codigousuario'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN codigousuario INTEGER;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'observaciones'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN observaciones TEXT;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'aocr_tbhistorialestado' AND column_name = 'fechacambio'
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado ADD COLUMN fechacambio TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'pk_aocr_tbhistorialestado'
          AND conrelid = 'public.aocr_tbhistorialestado'::regclass
    ) THEN
        ALTER TABLE public.aocr_tbhistorialestado
            ADD CONSTRAINT pk_aocr_tbhistorialestado PRIMARY KEY (codigohistorial);
    END IF;
EXCEPTION WHEN duplicate_object THEN
    NULL;
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
EXCEPTION WHEN undefined_column THEN
    NULL;
WHEN undefined_table THEN
    NULL;
END $$;

CREATE INDEX IF NOT EXISTS idx_historial_solicitud
    ON public.aocr_tbhistorialestado(codigosolicitud);

CREATE INDEX IF NOT EXISTS idx_historial_fecha
    ON public.aocr_tbhistorialestado(fechacambio);

COMMENT ON TABLE public.aocr_tbhistorialestado IS 'Historial de cambios de estado de solicitudes AOCR';
