-- ============================================================
-- SCRIPT: Fix Órdenes de Recaudación - Correcciones SQL
-- Fecha: 5 de febrero de 2026
-- Descripción: Correcciones para alinear código con schema real
-- ============================================================

-- VALIDACIÓN PREVIA: Verificar columnas actuales
-- ============================================================
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'aocr_or_orden'
ORDER BY ordinal_position;

SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'aocr_or_orden_detalle'
ORDER BY ordinal_position;

-- ============================================================
-- OPCIÓN A: AGREGAR columnas faltantes a aocr_or_orden
-- (Elegir esta opción si se necesitan esas columnas)
-- ============================================================
/*
ALTER TABLE aocr_or_orden
ADD COLUMN IF NOT EXISTS observacion TEXT,
ADD COLUMN IF NOT EXISTS subtotal NUMERIC(18,2) DEFAULT 0,
ADD COLUMN IF NOT EXISTS admin NUMERIC(18,2) DEFAULT 0,
ADD COLUMN IF NOT EXISTS lugar_emision VARCHAR(100),
ADD COLUMN IF NOT EXISTS correo VARCHAR(100),
ADD COLUMN IF NOT EXISTS telefono VARCHAR(20),
ADD COLUMN IF NOT EXISTS concepto_id INTEGER;

-- Agregar FK si concepto_id se usa
ALTER TABLE aocr_or_orden
ADD CONSTRAINT fk_orden_concepto 
FOREIGN KEY (concepto_id) REFERENCES aocr_or_concepto(id)
ON DELETE SET NULL;

COMMENT ON COLUMN aocr_or_orden.observacion IS 'Observaciones adicionales de la orden';
COMMENT ON COLUMN aocr_or_orden.subtotal IS 'Subtotal antes de gastos administrativos';
COMMENT ON COLUMN aocr_or_orden.admin IS 'Monto de gastos administrativos';
COMMENT ON COLUMN aocr_or_orden.lugar_emision IS 'Ciudad/lugar donde se emite la orden';
COMMENT ON COLUMN aocr_or_orden.correo IS 'Email de contacto';
COMMENT ON COLUMN aocr_or_orden.telefono IS 'Teléfono de contacto';
COMMENT ON COLUMN aocr_or_orden.concepto_id IS 'Concepto principal de la orden (si aplica)';
*/

-- ============================================================
-- OPCIÓN B: AGREGAR columnas faltantes a aocr_or_orden_detalle
-- (Elegir esta opción si se necesitan esas columnas)
-- ============================================================
/*
ALTER TABLE aocr_or_orden_detalle
ADD COLUMN IF NOT EXISTS concepto_codigo VARCHAR(50),
ADD COLUMN IF NOT EXISTS descripcion TEXT,
ADD COLUMN IF NOT EXISTS porcentaje_admin NUMERIC(5,2) DEFAULT 0,
ADD COLUMN IF NOT EXISTS subtotal NUMERIC(18,2) DEFAULT 0,
ADD COLUMN IF NOT EXISTS admin NUMERIC(18,2) DEFAULT 0;

COMMENT ON COLUMN aocr_or_orden_detalle.concepto_codigo IS 'Código del concepto para referencia';
COMMENT ON COLUMN aocr_or_orden_detalle.descripcion IS 'Descripción detallada del concepto';
COMMENT ON COLUMN aocr_or_orden_detalle.porcentaje_admin IS 'Porcentaje de gastos administrativos aplicado';
COMMENT ON COLUMN aocr_or_orden_detalle.subtotal IS 'Subtotal de la línea antes de admin';
COMMENT ON COLUMN aocr_or_orden_detalle.admin IS 'Monto de gastos administrativos de la línea';
*/

-- ============================================================
-- VALIDACIÓN POST-MODIFICACIÓN
-- ============================================================
/*
-- Verificar que columnas fueron agregadas correctamente
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'aocr_or_orden'
  AND column_name IN ('observacion', 'subtotal', 'admin', 'lugar_emision', 'correo', 'telefono', 'concepto_id')
ORDER BY column_name;

SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns 
WHERE table_name = 'aocr_or_orden_detalle'
  AND column_name IN ('concepto_codigo', 'descripcion', 'porcentaje_admin', 'subtotal', 'admin')
ORDER BY column_name;
*/

-- ============================================================
-- SCRIPT DE ROLLBACK (si algo sale mal)
-- ============================================================
/*
-- Revertir cambios en aocr_or_orden
ALTER TABLE aocr_or_orden
DROP COLUMN IF EXISTS observacion,
DROP COLUMN IF EXISTS subtotal,
DROP COLUMN IF EXISTS admin,
DROP COLUMN IF EXISTS lugar_emision,
DROP COLUMN IF EXISTS correo,
DROP COLUMN IF EXISTS telefono,
DROP COLUMN IF EXISTS concepto_id;

-- Revertir cambios en aocr_or_orden_detalle
ALTER TABLE aocr_or_orden_detalle
DROP COLUMN IF EXISTS concepto_codigo,
DROP COLUMN IF EXISTS descripcion,
DROP COLUMN IF EXISTS porcentaje_admin,
DROP COLUMN IF EXISTS subtotal,
DROP COLUMN IF EXISTS admin;
*/

-- ============================================================
-- VERIFICACIÓN FINAL: Probar INSERT
-- ============================================================
/*
-- Test INSERT en aocr_or_orden (con columnas nuevas si OPCIÓN A)
INSERT INTO aocr_or_orden (
    codigo_usuario, codigo_solicitud, numero_orden,
    fecha_creacion, estado, compania, ruc_cedula, total,
    observacion, subtotal, admin, lugar_emision, correo, telefono
) VALUES (
    1, 100, 'TEST-OR-001',
    NOW(), 'BORRADOR', 'Test Company SA', '1234567890', 3300.00,
    'Orden de prueba', 3000.00, 300.00, 'Quito', 'test@dgac.gob.ec', '0999999999'
) RETURNING id;

-- Test INSERT en aocr_or_orden_detalle (con columnas nuevas si OPCIÓN B)
INSERT INTO aocr_or_orden_detalle (
    orden_id, concepto_id, concepto_codigo, concepto_nombre, descripcion,
    cantidad, valor_unitario, porcentaje_admin, subtotal, admin, total_linea
) VALUES (
    <orden_id_del_test_anterior>, 1, 'EMI_AOCR', 'Emisión AOCR', 'Emisión de certificado AOCR',
    1, 3000.00, 10.00, 3000.00, 300.00, 3300.00
) RETURNING id;

-- Limpiar datos de prueba
DELETE FROM aocr_or_orden_detalle WHERE concepto_codigo = 'TEST-OR-001';
DELETE FROM aocr_or_orden WHERE numero_orden = 'TEST-OR-001';
*/
