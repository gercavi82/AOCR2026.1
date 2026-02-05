-- ================================
-- SCRIPT COMPLETO DE REPARACIÓN
-- Base de datos AOCR PostgreSQL  
-- ================================

-- 1. CREAR TABLA DE PARÁMETROS SI NO EXISTE
-- ==========================================
CREATE TABLE IF NOT EXISTS aocr_tbparametro (
    codigoparametro SERIAL PRIMARY KEY,
    clave VARCHAR(100) NOT NULL UNIQUE,
    valor VARCHAR(500) NOT NULL,
    descripcion VARCHAR(1000),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    createdby INTEGER,
    updatedat TIMESTAMP,
    updatedby INTEGER,
    deletedat TIMESTAMP,
    deletedby INTEGER
);

-- 2. VERIFICAR/AGREGAR COLUMNA BANCO SI NO EXISTE
-- ==============================================
ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255);

-- 3. DATOS INICIALES DE PARÁMETROS CONFIGURABLES
-- =============================================
INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) 
VALUES 
    ('TEST_EMPRESA_NOMBRE', 'AERONÁUTICA CIVIL', 'Nombre de empresa para testing y demo', TRUE, NOW(), 1),
    ('TEST_EMPRESA_DIRECCION', 'Av. El Dorado # 103-15', 'Dirección de empresa para testing', TRUE, NOW(), 1),
    ('TEST_EMPRESA_TELEFONO', '+57 1 425-1000', 'Teléfono de empresa para testing', TRUE, NOW(), 1),
    ('TEST_EMPRESA_EMAIL', 'info@aerocivil.gov.co', 'Email de empresa para testing', TRUE, NOW(), 1),
    
    ('DEMO_MONTO_FIJO', '80.00', 'Monto fijo para demostraciones en USD', TRUE, NOW(), 1),
    ('DEMO_MONTO_VARIABLE', '120.50', 'Monto variable para ejemplos', TRUE, NOW(), 1),
    
    ('PDF_HEADER_TITULO', 'ORDEN DE RECAUDACIÓN', 'Título principal en PDFs', TRUE, NOW(), 1),
    ('PDF_HEADER_SUBTITULO', 'Dirección General de Aeronáutica Civil', 'Subtítulo en documentos PDF', TRUE, NOW(), 1),
    ('PDF_FOOTER_TEXT', 'Documento generado automáticamente', 'Texto de pie de página en PDFs', TRUE, NOW(), 1),
    
    ('TARIFA_EMI_AOCR', '250.00', 'Tarifa emisión AOCR en USD', TRUE, NOW(), 1),
    ('TARIFA_REN_AOCR', '200.00', 'Tarifa renovación AOCR en USD', TRUE, NOW(), 1),
    ('TARIFA_MOD_AOCR_INC', '150.00', 'Tarifa modificación AOCR con incremento de flota', TRUE, NOW(), 1),
    ('TARIFA_MOD_AOCR_SIN_INC', '100.00', 'Tarifa modificación AOCR sin incremento de flota', TRUE, NOW(), 1),
    ('TARIFA_INSPECCION_EXT', '500.00', 'Tarifa inspección en el exterior', TRUE, NOW(), 1),
    ('TARIFA_VIATICOS_INSPECTOR', '80.00', 'Tarifa diaria de viáticos para inspector', TRUE, NOW(), 1),
    
    ('PORCENTAJE_ADMIN_VIATICOS', '15', 'Porcentaje administrativo sobre viáticos (%)', TRUE, NOW(), 1),
    
    ('SISTEMA_VERSION', '2.1.0', 'Versión actual del sistema AOCR', TRUE, NOW(), 1),
    ('SISTEMA_MANTENIMIENTO', 'FALSE', 'Indica si el sistema está en mantenimiento', TRUE, NOW(), 1)

ON CONFLICT (clave) DO UPDATE SET
    valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    updatedat = NOW(),
    updatedby = 1;

-- 4. ACTUALIZAR REGISTROS EXISTENTES DE PAGOS
-- ==========================================
UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL OR banco = '';

-- 5. CREAR ÍNDICES PARA RENDIMIENTO
-- ================================
CREATE INDEX IF NOT EXISTS idx_parametro_clave ON aocr_tbparametro(clave);
CREATE INDEX IF NOT EXISTS idx_parametro_activo ON aocr_tbparametro(activo);
CREATE INDEX IF NOT EXISTS idx_pago_banco ON aocr_tbpago(banco);

-- 6. VERIFICACIÓN FINAL
-- ====================
SELECT 'TABLA PARÁMETROS' as tipo, 
       COUNT(*) as total_registros,
       COUNT(CASE WHEN activo = TRUE THEN 1 END) as activos
FROM aocr_tbparametro
UNION ALL
SELECT 'PAGOS CON BANCO' as tipo,
       COUNT(*) as total_registros,
       COUNT(CASE WHEN banco IS NOT NULL AND banco != '' THEN 1 END) as con_banco
FROM aocr_tbpago;

COMMIT;