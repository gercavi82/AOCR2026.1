-- =====================================================================
-- MIGRACIÓN ADITIVA E IDEMPOTENTE: AC-01 LIBERAR CORREO RT DEVUELTO
-- FECHA: 2026-09-03
-- DESCRIPCIÓN:
-- Agrega columnas a la tabla usuario para trazabilidad de postulaciones
-- devueltas por Coordinación y soporte para liberación de correo
-- conservando el histórico íntegro.
-- =====================================================================

DO $$
BEGIN
    -- 1. Columna para resguardar el correo original del postulante
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'correo_original'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN correo_original VARCHAR(255);
        COMMENT ON COLUMN public.usuario.correo_original IS 'Resguardo del correo original cuando una postulación RT es devuelta';
    END IF;

    -- 2. Columna booleana que indica si la reserva del correo fue liberada
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'correo_liberado'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN correo_liberado BOOLEAN NOT NULL DEFAULT FALSE;
        COMMENT ON COLUMN public.usuario.correo_liberado IS 'Indica si el correo fue liberado por devolución de la designación provisional';
    END IF;

    -- 3. Fecha en la que Coordinación devolvió la designación
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'fecha_devolucion_designacion'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN fecha_devolucion_designacion TIMESTAMP;
        COMMENT ON COLUMN public.usuario.fecha_devolucion_designacion IS 'Fecha/hora en la que la designación fue devuelta por Coordinación';
    END IF;

    -- 4. ID del usuario Coordinador que ejecutó la devolución
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'coordinador_devolucion_id'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN coordinador_devolucion_id INTEGER;
        COMMENT ON COLUMN public.usuario.coordinador_devolucion_id IS 'ID del usuario Coordinador que devolvió la postulación';
    END IF;

    -- 5. Observación registrada para la devolución
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'observacion_devolucion'
    ) THEN
        ALTER TABLE public.usuario ADD COLUMN observacion_devolucion TEXT;
        COMMENT ON COLUMN public.usuario.observacion_devolucion IS 'Observación y justificación técnica de la devolución ingresada por Coordinación';
    END IF;

    -- 6. Índice condicional para optimizar búsqueda de correos activos / no liberados
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
          AND tablename = 'usuario' 
          AND indexname = 'idx_usuario_correo_activo_lower'
    ) THEN
        CREATE INDEX idx_usuario_correo_activo_lower 
        ON public.usuario (LOWER(correo)) 
        WHERE (correo_liberado = FALSE);
    END IF;
END $$;
