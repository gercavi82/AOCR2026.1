-- =====================================================
-- Script para agregar Foreign Keys a email_queue
-- Establece integridad referencial con aocr_or_orden
-- Base de datos: PostgreSQL (dgac_des)
-- =====================================================

-- 1. Verificar que la columna orden_id existe
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'email_queue' 
        AND column_name = 'orden_id'
    ) THEN
        RAISE NOTICE 'Agregando columna orden_id a email_queue...';
        ALTER TABLE public.email_queue ADD COLUMN orden_id INTEGER;
    ELSE
        RAISE NOTICE 'La columna orden_id ya existe en email_queue';
    END IF;
END $$;

-- 2. Limpiar datos huérfanos (emails sin orden válida)
-- ADVERTENCIA: Esto eliminará registros con orden_id que no existan en aocr_or_orden
DO $$
DECLARE
    orphan_count INTEGER;
BEGIN
    -- Contar registros huérfanos
    SELECT COUNT(*) INTO orphan_count
    FROM public.email_queue eq
    WHERE eq.orden_id IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 FROM public.aocr_or_orden o WHERE o.id = eq.orden_id
    );
    
    IF orphan_count > 0 THEN
        RAISE WARNING 'Se encontraron % registros huérfanos en email_queue', orphan_count;
        RAISE NOTICE 'Puedes eliminarlos manualmente con: DELETE FROM public.email_queue WHERE orden_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM public.aocr_or_orden WHERE id = email_queue.orden_id);';
    ELSE
        RAISE NOTICE 'No se encontraron registros huérfanos';
    END IF;
END $$;

-- 3. Agregar Foreign Key constraint (ON DELETE CASCADE para eliminar emails si se borra la orden)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_email_queue_orden' 
        AND table_name = 'email_queue'
    ) THEN
        RAISE NOTICE 'Creando Foreign Key constraint fk_email_queue_orden...';
        
        ALTER TABLE public.email_queue
        ADD CONSTRAINT fk_email_queue_orden
        FOREIGN KEY (orden_id)
        REFERENCES public.aocr_or_orden(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE;
        
        RAISE NOTICE '✅ Foreign Key constraint creada exitosamente';
    ELSE
        RAISE NOTICE 'Foreign Key constraint fk_email_queue_orden ya existe';
    END IF;
END $$;

-- 4. Crear índice para mejorar performance de joins
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM pg_indexes 
        WHERE indexname = 'idx_email_queue_orden_id'
    ) THEN
        RAISE NOTICE 'Creando índice idx_email_queue_orden_id...';
        CREATE INDEX idx_email_queue_orden_id ON public.email_queue(orden_id);
        RAISE NOTICE '✅ Índice creado exitosamente';
    ELSE
        RAISE NOTICE 'Índice idx_email_queue_orden_id ya existe';
    END IF;
END $$;

-- 5. Agregar comentarios a la constraint
COMMENT ON CONSTRAINT fk_email_queue_orden ON public.email_queue IS 
    'Foreign key que vincula emails con órdenes de recaudación. ON DELETE CASCADE elimina emails automáticamente cuando se elimina una orden.';

-- 6. Verificar la constraint creada
SELECT
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name,
    rc.delete_rule,
    rc.update_rule
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
JOIN information_schema.referential_constraints AS rc
    ON tc.constraint_name = rc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
AND tc.table_name = 'email_queue'
AND tc.constraint_name = 'fk_email_queue_orden';

-- Mostrar resultado
DO $$
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '========================================';
    RAISE NOTICE '✅ Script completado exitosamente';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Foreign Key: email_queue.orden_id -> aocr_or_orden.id';
    RAISE NOTICE 'DELETE Rule: CASCADE (elimina emails si se borra orden)';
    RAISE NOTICE 'UPDATE Rule: CASCADE (actualiza referencias si cambia ID)';
    RAISE NOTICE '';
END $$;
