-- ==================================================
-- SCRIPT PARA ARREGLAR PROBLEMAS EN AOCR_TBPAGO
-- ==================================================

-- 1. Primero, agregar la columna banco que falta
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name='aocr_tbpago' 
        AND column_name='banco'
        AND table_schema='public'
    ) THEN
        ALTER TABLE public.aocr_tbpago 
        ADD COLUMN banco VARCHAR(100);
        
        -- Actualizar registros existentes con valor por defecto
        UPDATE public.aocr_tbpago 
        SET banco = 'BANCO POR DEFINIR' 
        WHERE banco IS NULL;
        
        RAISE NOTICE 'Columna banco agregada exitosamente';
    ELSE
        RAISE NOTICE 'Columna banco ya existe';
    END IF;
END $$;

-- 2. Verificar la constraint actual de estados
DO $$
DECLARE
    constraint_def TEXT;
BEGIN
    -- Obtener la definición de la constraint actual
    SELECT consrc INTO constraint_def
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    WHERE rel.relname = 'aocr_tbpago' 
    AND con.conname = 'chk_estado_pago';
    
    IF constraint_def IS NOT NULL THEN
        RAISE NOTICE 'Constraint actual: %', constraint_def;
    ELSE
        RAISE NOTICE 'No se encontró constraint chk_estado_pago';
    END IF;
END $$;

-- 3. Eliminar constraint antigua si existe
ALTER TABLE public.aocr_tbpago DROP CONSTRAINT IF EXISTS chk_estado_pago;

-- 4. Crear nueva constraint que permita todos los estados necesarios
ALTER TABLE public.aocr_tbpago 
ADD CONSTRAINT chk_estado_pago 
CHECK (estado_pago IN (
    'PENDIENTE', 
    'VALIDADO', 
    'APROBADO', 
    'RECHAZADO',
    'CONFIRMADO',
    'PAGADO',
    'COMPLETADO',
    'CANCELADO',
    'EN_REVISION',
    'PROCESANDO',
    'ANULADO'
));

-- 5. Verificar registros existentes que podrían tener estados no válidos
SELECT DISTINCT estado_pago, COUNT(*) as cantidad
FROM public.aocr_tbpago 
GROUP BY estado_pago
ORDER BY estado_pago;

-- 6. Actualizar estados problemáticos si es necesario
UPDATE public.aocr_tbpago 
SET estado_pago = 'PENDIENTE' 
WHERE estado_pago IS NULL OR estado_pago = '';

-- 7. Ver estructura final de la tabla
SELECT 
    column_name, 
    data_type, 
    is_nullable, 
    column_default,
    character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'aocr_tbpago' 
AND table_schema = 'public'
ORDER BY ordinal_position;

-- 8. Verificar constraints finales
SELECT 
    con.conname,
    con.consrc
FROM pg_constraint con
JOIN pg_class rel ON rel.oid = con.conrelid
WHERE rel.relname = 'aocr_tbpago';

COMMIT;