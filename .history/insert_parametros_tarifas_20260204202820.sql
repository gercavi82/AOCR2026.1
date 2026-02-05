-- Script para insertar parámetros configurables de tarifas AOCR
-- Esto reemplaza los valores "quemados" en el código con valores configurables

-- Tarifas de emisión y renovación AOCR
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('TARIFA_EMI_AOCR', '3300.00', 'Tarifa para Emisión de Certificado AOCR', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('TARIFA_REN_AOCR', '3300.00', 'Tarifa para Renovación de Certificado AOCR', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Tarifas de modificaciones AOCR
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('TARIFA_MOD_AOCR_INC', '1600.00', 'Tarifa para Modificación AOCR con inclusión de aeronaves distinto modelo y tipo', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('TARIFA_MOD_AOCR_SIN_INC', '80.00', 'Tarifa para Modificación AOCR que no implique incremento de aeronaves', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Tarifa de inspecciones externas
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('TARIFA_INSPECCION_EXT', '500.00', 'Tarifa para Inspección requerida por Operador Aéreo Extranjero (por estación)', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Tarifa de viáticos
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('TARIFA_VIATICOS_INSPECTOR', '80.00', 'Tarifa diaria de viáticos para inspectores', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Porcentaje administrativo sobre viáticos
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('PORCENTAJE_ADMIN_VIATICOS', '8.00', 'Porcentaje de gastos administrativos sobre viáticos (8%)', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Verificar que se insertaron correctamente
SELECT clave, valor, descripcion, activo
FROM parametros 
WHERE clave IN (
    'TARIFA_EMI_AOCR', 
    'TARIFA_REN_AOCR', 
    'TARIFA_MOD_AOCR_INC', 
    'TARIFA_MOD_AOCR_SIN_INC', 
    'TARIFA_INSPECCION_EXT', 
    'TARIFA_VIATICOS_INSPECTOR', 
    'PORCENTAJE_ADMIN_VIATICOS'
)
ORDER BY clave;