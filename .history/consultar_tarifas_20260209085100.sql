-- Consulta diagnóstica de tarifas AOCR
-- Base de datos: dgac_des (PostgreSQL 18)

-- 1. Ver la estructura de la tabla aocr_tbparametro
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'aocr_tbparametro'
ORDER BY ordinal_position;

-- 2. Ver todos los parámetros de tarifas (incluyendo valores problemáticos)
SELECT 
    codigoparametro,
    clave,
    valor,
    descripcion,
    activo,
    createdat,
    updatedat,
    deletedat,
    LENGTH(valor) as longitud_valor,
    -- Detectar caracteres problemáticos
    CASE 
        WHEN valor LIKE '%$%' THEN 'Contiene $'
        WHEN valor LIKE '%USD%' THEN 'Contiene USD'
        WHEN valor LIKE '%,%' THEN 'Contiene coma'
        WHEN valor LIKE '% %' THEN 'Contiene espacios'
        WHEN valor LIKE '%_%' THEN 'Contiene guion bajo'
        ELSE 'Formato limpio'
    END as diagnostico_formato
FROM aocr_tbparametro
WHERE clave IN (
    'TARIFA_EMI_AOCR',
    'TARIFA_REN_AOCR',
    'TARIFA_MOD_AOCR_INC',
    'TARIFA_MOD_AOCR_SIN_INC',
    'TARIFA_INSPECCION_EXT',
    'TARIFA_VIATICOS_INSPECTOR',
    'PORCENTAJE_ADMIN_VIATICOS'
)
AND deletedat IS NULL
ORDER BY clave;

-- 3. Ver TODOS los parámetros para encontrar patrones
SELECT 
    clave,
    valor,
    activo,
    CASE 
        WHEN valor ~ '^[0-9]+\.?[0-9]*$' THEN 'Formato válido (punto)'
        WHEN valor ~ '^[0-9]+,?[0-9]*$' THEN 'Formato válido (coma)'
        ELSE 'Formato inválido'
    END as validacion_regex
FROM aocr_tbparametro
WHERE deletedat IS NULL
ORDER BY clave;

-- 4. Contar parámetros activos vs inactivos
SELECT 
    activo,
    COUNT(*) as total
FROM aocr_tbparametro
WHERE deletedat IS NULL
GROUP BY activo;
