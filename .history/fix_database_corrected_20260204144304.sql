-- ==================================================
-- SCRIPT CORREGIDO PARA AOCR_TBPAGO
-- ==================================================

-- 1. La columna banco ya fue agregada exitosamente

-- 2. Verificar constraint actual (PostgreSQL 12+)
DO $$
DECLARE
    constraint_def TEXT;
BEGIN
    -- Obtener constraints de check existentes
    SELECT pg_get_constraintdef(con.oid) INTO constraint_def
    FROM pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    WHERE rel.relname = 'aocr_tbpago' 
    AND con.conname = 'chk_estado_pago'
    AND con.contype = 'c';
    
    IF constraint_def IS NOT NULL THEN
        RAISE NOTICE 'Constraint actual: %', constraint_def;
    ELSE
        RAISE NOTICE 'No se encontro constraint chk_estado_pago';
    END IF;
END $$;

-- 3. Eliminar constraint antigua si existe
ALTER TABLE public.aocr_tbpago DROP CONSTRAINT IF EXISTS chk_estado_pago;

-- 4. Crear nueva constraint para columna 'estado' (no 'estado_pago')
ALTER TABLE public.aocr_tbpago 
ADD CONSTRAINT chk_estado_pago 
CHECK (estado IN (
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
    'ANULADO',
    'Pendiente',
    'Validado',
    'Aprobado',
    'Rechazado'
));

-- 5. Verificar registros existentes
SELECT DISTINCT estado, COUNT(*) as cantidad
FROM public.aocr_tbpago 
GROUP BY estado
ORDER BY estado;

-- 6. Actualizar estados problemáticos si es necesario
UPDATE public.aocr_tbpago 
SET estado = 'PENDIENTE' 
WHERE estado IS NULL OR estado = '';

-- 7. Verificar estructura final
\d+ public.aocr_tbpago

-- 8. Verificar constraints finales
SELECT 
    con.conname as constraint_name,
    pg_get_constraintdef(con.oid) as constraint_definition
FROM pg_constraint con
JOIN pg_class rel ON rel.oid = con.conrelid
WHERE rel.relname = 'aocr_tbpago'
AND con.contype = 'c';

SELECT 'SUCCESS: Base de datos reparada correctamente' as resultado;