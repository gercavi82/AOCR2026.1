-- =====================================================================
-- ROLLBACK MIGRACIÓN: AC-01 LIBERAR CORREO RT DEVUELTO
-- FECHA: 2026-09-03
-- =====================================================================

DO $$
BEGIN
    DROP INDEX IF EXISTS public.idx_usuario_correo_activo_lower;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'observacion_devolucion'
    ) THEN
        ALTER TABLE public.usuario DROP COLUMN IF EXISTS observacion_devolucion;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'coordinador_devolucion_id'
    ) THEN
        ALTER TABLE public.usuario DROP COLUMN IF EXISTS coordinador_devolucion_id;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'fecha_devolucion_designacion'
    ) THEN
        ALTER TABLE public.usuario DROP COLUMN IF EXISTS fecha_devolucion_designacion;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'correo_liberado'
    ) THEN
        ALTER TABLE public.usuario DROP COLUMN IF EXISTS correo_liberado;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
          AND table_name = 'usuario' 
          AND column_name = 'correo_original'
    ) THEN
        ALTER TABLE public.usuario DROP COLUMN IF EXISTS correo_original;
    END IF;
END $$;
