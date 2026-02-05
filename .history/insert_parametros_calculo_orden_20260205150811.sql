-- Script para insertar parámetros de cálculo de órdenes de recaudación
-- Elimina valores hardcodeados ($500 por estación, $80 por día, 8% admin)

-- Verificar si los parámetros ya existen antes de insertarlos
DO $$
BEGIN
    -- Parámetro: Valor por estación para inspecciones
    IF NOT EXISTS (SELECT 1 FROM aocr_tbparametro WHERE clave = 'CALCULO_VALOR_POR_ESTACION') THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) 
        VALUES ('CALCULO_VALOR_POR_ESTACION', '500.00', 'Valor en USD por estación para cálculo de inspecciones en órdenes de recaudación', TRUE, NOW(), 1);
    END IF;

    -- Parámetro: Valor por día para viáticos
    IF NOT EXISTS (SELECT 1 FROM aocr_tbparametro WHERE clave = 'CALCULO_VALOR_POR_DIA_VIATICO') THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) 
        VALUES ('CALCULO_VALOR_POR_DIA_VIATICO', '80.00', 'Valor en USD por día para cálculo de viáticos en órdenes de recaudación', TRUE, NOW(), 1);
    END IF;

    -- Parámetro: Porcentaje de gastos administrativos
    IF NOT EXISTS (SELECT 1 FROM aocr_tbparametro WHERE clave = 'CALCULO_PORCENTAJE_GASTOS_ADMIN') THEN
        INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) 
        VALUES ('CALCULO_PORCENTAJE_GASTOS_ADMIN', '8.00', 'Porcentaje de gastos administrativos aplicado sobre viáticos (sin símbolo %)', TRUE, NOW(), 1);
    END IF;

    -- Mensaje de confirmación
    RAISE NOTICE 'Parámetros de cálculo de órdenes insertados correctamente.';
    
END $$;

-- Verificar que se insertaron correctamente
SELECT clave, valor, descripcion, activo 
FROM aocr_tbparametro 
WHERE clave LIKE 'CALCULO_%' 
ORDER BY clave;