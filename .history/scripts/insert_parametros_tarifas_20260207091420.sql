-- ============================================================
-- SCRIPT: Insertar Parámetros de Tarifas Configurables
-- Fecha: 5 de febrero de 2026
-- Descripción: Parametriza tarifas hardcodeadas en código
-- ============================================================

-- Verificar que existe la tabla parametros
-- Si no existe, descomentar la siguiente sección:
/*
CREATE TABLE IF NOT EXISTS parametros (
    id SERIAL PRIMARY KEY,
    clave VARCHAR(100) UNIQUE NOT NULL,
    valor TEXT NOT NULL,
    descripcion TEXT,
    tipo VARCHAR(20) DEFAULT 'STRING',
    activo BOOLEAN DEFAULT true,
    fecha_creacion TIMESTAMP DEFAULT NOW(),
    fecha_modificacion TIMESTAMP DEFAULT NOW(),
    usuario_modificacion VARCHAR(50)
);

CREATE INDEX idx_parametros_clave ON parametros(clave);
CREATE INDEX idx_parametros_activo ON parametros(activo);

COMMENT ON TABLE parametros IS 'Tabla de parámetros configurables del sistema';
COMMENT ON COLUMN parametros.clave IS 'Clave única del parámetro (ej: TARIFA_EMI_AOCR)';
COMMENT ON COLUMN parametros.valor IS 'Valor del parámetro (puede ser string, número, etc.)';
COMMENT ON COLUMN parametros.tipo IS 'Tipo de dato: STRING, INTEGER, DECIMAL, BOOLEAN, DATE';
*/

-- ============================================================
-- INSERTAR TARIFAS CONFIGURABLES
-- ============================================================

-- Tarifas base de conceptos AOCR
INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('TARIFA_EMI_AOCR', '3300.00', 'Tarifa para Emisión de Certificado AOCR', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('TARIFA_REN_AOCR', '3300.00', 'Tarifa para Renovación de Certificado AOCR', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('TARIFA_MOD_AOCR_INC', '1600.00', 'Tarifa para Modificación AOCR con inclusión de aeronaves de distinto modelo y tipo', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('TARIFA_MOD_AOCR_SIN_INC', '80.00', 'Tarifa para Modificación AOCR sin incremento de aeronaves', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('TARIFA_INSPECCION_EXT', '500.00', 'Tarifa por estación de Inspección requerida por Operador Aéreo Extranjero', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('TARIFA_VIATICOS_INSPECTOR', '80.00', 'Tarifa de Viáticos para Inspectores (por día)', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

-- Porcentajes de gastos administrativos
INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('PORCENTAJE_ADMIN_VIATICOS', '8.00', 'Porcentaje de gastos administrativos sobre viáticos', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('PORCENTAJE_ADMIN_EMI_AOCR', '0.00', 'Porcentaje de gastos administrativos sobre emisión AOCR', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('PORCENTAJE_ADMIN_REN_AOCR', '0.00', 'Porcentaje de gastos administrativos sobre renovación AOCR', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('PORCENTAJE_ADMIN_MOD', '0.00', 'Porcentaje de gastos administrativos sobre modificaciones', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

INSERT INTO parametros (clave, valor, descripcion, tipo, activo, usuario_modificacion) VALUES
('PORCENTAJE_ADMIN_INSPECCION', '0.00', 'Porcentaje de gastos administrativos sobre inspecciones', 'DECIMAL', true, 'SYSTEM')
ON CONFLICT (clave) DO UPDATE SET 
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'SYSTEM';

-- ============================================================
-- VERIFICACIÓN
-- ============================================================

-- Consultar todos los parámetros insertados
SELECT 
    clave,
    valor,
    descripcion,
    tipo,
    activo,
    fecha_creacion,
    fecha_modificacion
FROM parametros 
WHERE clave LIKE 'TARIFA_%' OR clave LIKE 'PORCENTAJE_%'
ORDER BY clave;

-- ============================================================
-- EJEMPLOS DE USO
-- ============================================================

-- Cambiar una tarifa (ejemplo: aumentar emisión AOCR a $3,500)
/*
UPDATE parametros 
SET 
    valor = '3500.00',
    fecha_modificacion = NOW(),
    usuario_modificacion = 'ADMIN'
WHERE clave = 'TARIFA_EMI_AOCR';
*/

-- Cambiar porcentaje administrativo (ejemplo: subir a 10%)
/*
UPDATE parametros 
SET 
    valor = '10.00',
    fecha_modificacion = NOW(),
    usuario_modificacion = 'ADMIN'
WHERE clave = 'PORCENTAJE_ADMIN_VIATICOS';
*/

-- Desactivar una tarifa temporalmente
/*
UPDATE parametros 
SET 
    activo = false,
    fecha_modificacion = NOW(),
    usuario_modificacion = 'ADMIN'
WHERE clave = 'TARIFA_INSPECCION_EXT';
*/

-- Consultar historial de cambios (si se tiene tabla de auditoría)
/*
SELECT 
    p.clave,
    p.valor AS valor_actual,
    p.fecha_modificacion,
    p.usuario_modificacion
FROM parametros p
WHERE p.clave = 'TARIFA_EMI_AOCR'
ORDER BY p.fecha_modificacion DESC;
*/
