-- ===================================================================
-- SCRIPT DE REPARACIÓN PARA TABLA aocr_tbparametro
-- Base de datos: dgac_des
-- Servidor: 172.20.16.55:5432
-- Usuario: root
-- ===================================================================

-- 1. Verificar estructura actual de la tabla
SELECT 'Verificando estructura actual de la tabla...' AS status;
\d aocr_tbparametro;

-- 2. Agregar columnas necesarias si no existen
SELECT 'Agregando columnas necesarias...' AS status;

ALTER TABLE aocr_tbparametro 
ADD COLUMN IF NOT EXISTS codigoparametro VARCHAR(100) UNIQUE;

ALTER TABLE aocr_tbparametro 
ADD COLUMN IF NOT EXISTS valorparametro DECIMAL(10,2);

ALTER TABLE aocr_tbparametro 
ADD COLUMN IF NOT EXISTS descripcionparametro VARCHAR(255);

-- 3. Insertar parámetros necesarios para el cálculo de PDF
SELECT 'Insertando parámetros de cálculo...' AS status;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_VALOR_POR_ESTACION', 500.00, 'Valor por estación para cálculo de inspecciones')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_VALOR_POR_DIA_VIATICO', 80.00, 'Valor por día de viático para inspectores')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

INSERT INTO aocr_tbparametro (codigoparametro, valorparametro, descripcionparametro) 
VALUES ('CALCULO_PORCENTAJE_GASTOS_ADMIN', 8.00, 'Porcentaje de gastos administrativos (como porcentaje, ej: 8 para 8%)')
ON CONFLICT (codigoparametro) DO UPDATE SET 
    valorparametro = EXCLUDED.valorparametro,
    descripcionparametro = EXCLUDED.descripcionparametro;

-- 4. Verificar que los parámetros se insertaron correctamente
SELECT 'Verificando parámetros insertados...' AS status;

SELECT 
    codigoparametro, 
    valorparametro, 
    descripcionparametro 
FROM aocr_tbparametro 
WHERE codigoparametro IN (
    'CALCULO_VALOR_POR_ESTACION', 
    'CALCULO_VALOR_POR_DIA_VIATICO', 
    'CALCULO_PORCENTAJE_GASTOS_ADMIN'
)
ORDER BY codigoparametro;

-- 5. Verificar estructura final de la tabla
SELECT 'Estructura final de la tabla:' AS status;
SELECT column_name, data_type, is_nullable 
FROM information_schema.columns 
WHERE table_name = 'aocr_tbparametro' 
ORDER BY ordinal_position;

SELECT 'REPARACIÓN COMPLETADA EXITOSAMENTE!' AS status;