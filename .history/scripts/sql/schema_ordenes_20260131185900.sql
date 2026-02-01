-- =====================================================
-- Script de creación de tablas para Órdenes de Recaudación
-- Base de datos: PostgreSQL
-- Ejecutar con: psql -U root -d dgac_des -f schema_ordenes.sql
-- =====================================================

-- Tabla de conceptos (catálogo)
CREATE TABLE IF NOT EXISTS conceptos (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(50) UNIQUE NOT NULL,
    nombre VARCHAR(200) NOT NULL,
    descripcion TEXT,
    precio_base DECIMAL(18,2) DEFAULT 0,
    aplica_iva BOOLEAN DEFAULT TRUE,
    activo BOOLEAN DEFAULT TRUE,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de contribuyentes
CREATE TABLE IF NOT EXISTS contribuyentes (
    id SERIAL PRIMARY KEY,
    ruc_cedula VARCHAR(20) UNIQUE NOT NULL,
    nombre VARCHAR(200) NOT NULL,
    razon_social VARCHAR(200),
    direccion TEXT,
    telefono VARCHAR(50),
    correo VARCHAR(100),
    tipo VARCHAR(50), -- PERSONA_NATURAL, PERSONA_JURIDICA
    activo BOOLEAN DEFAULT TRUE,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla principal de órdenes de recaudación
CREATE TABLE IF NOT EXISTS ordenes_recaudacion (
    id SERIAL PRIMARY KEY,
    numero_orden VARCHAR(50) UNIQUE NOT NULL,
    solicitud_id INTEGER,
    concepto_id INTEGER REFERENCES conceptos(id),
    contribuyente_id INTEGER REFERENCES contribuyentes(id),
    subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    iva DECIMAL(18,2) NOT NULL DEFAULT 0,
    total DECIMAL(18,2) NOT NULL DEFAULT 0,
    observaciones TEXT,
    estado VARCHAR(50) NOT NULL DEFAULT 'PENDIENTE',
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion TIMESTAMP,
    usuario_creacion VARCHAR(100),
    usuario_actualizacion VARCHAR(100),
    activo BOOLEAN DEFAULT TRUE,
    
    -- Campos adicionales para el flujo
    fecha_vencimiento DATE,
    fecha_pago TIMESTAMP,
    referencia_pago VARCHAR(100),
    motivo_anulacion TEXT,
    fecha_anulacion TIMESTAMP,
    usuario_anulacion VARCHAR(100)
);

-- Tabla de detalles de orden
CREATE TABLE IF NOT EXISTS detalles_orden (
    id SERIAL PRIMARY KEY,
    orden_id INTEGER NOT NULL REFERENCES ordenes_recaudacion(id) ON DELETE CASCADE,
    concepto_id INTEGER REFERENCES conceptos(id),
    descripcion TEXT,
    cantidad INTEGER NOT NULL DEFAULT 1,
    precio_unitario DECIMAL(18,2) NOT NULL DEFAULT 0,
    subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    fecha_creacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabla de pagos
CREATE TABLE IF NOT EXISTS pagos (
    id SERIAL PRIMARY KEY,
    orden_id INTEGER NOT NULL REFERENCES ordenes_recaudacion(id),
    numero_comprobante VARCHAR(100),
    monto_pagado DECIMAL(18,2) NOT NULL,
    fecha_pago TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    metodo_pago VARCHAR(50), -- TRANSFERENCIA, DEPOSITO, EFECTIVO, etc.
    banco_origen VARCHAR(100),
    observaciones TEXT,
    ruta_comprobante VARCHAR(500),
    estado VARCHAR(50) DEFAULT 'PENDIENTE', -- PENDIENTE, VALIDADO, RECHAZADO
    fecha_registro TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    usuario_registro VARCHAR(100),
    fecha_validacion TIMESTAMP,
    usuario_validacion VARCHAR(100)
);

-- Índices para mejorar rendimiento
CREATE INDEX IF NOT EXISTS idx_ordenes_estado ON ordenes_recaudacion(estado);
CREATE INDEX IF NOT EXISTS idx_ordenes_contribuyente ON ordenes_recaudacion(contribuyente_id);
CREATE INDEX IF NOT EXISTS idx_ordenes_fecha ON ordenes_recaudacion(fecha_creacion);
CREATE INDEX IF NOT EXISTS idx_ordenes_numero ON ordenes_recaudacion(numero_orden);
CREATE INDEX IF NOT EXISTS idx_detalles_orden ON detalles_orden(orden_id);
CREATE INDEX IF NOT EXISTS idx_pagos_orden ON pagos(orden_id);
CREATE INDEX IF NOT EXISTS idx_pagos_estado ON pagos(estado);

-- Insertar conceptos de ejemplo
INSERT INTO conceptos (codigo, nombre, descripcion, precio_base, aplica_iva) VALUES
    ('CERT_AOC', 'Certificado AOC', 'Certificado de Operador Aéreo', 500.00, TRUE),
    ('INSP_AERON', 'Inspección Aeronave', 'Inspección técnica de aeronave', 350.00, TRUE),
    ('LIC_PILOTO', 'Licencia de Piloto', 'Emisión de licencia de piloto', 200.00, TRUE),
    ('RENO_LIC', 'Renovación de Licencia', 'Renovación de licencia aeronáutica', 150.00, TRUE),
    ('EXAM_MED', 'Examen Médico', 'Certificado médico aeronáutico', 100.00, TRUE)
ON CONFLICT (codigo) DO NOTHING;

-- Insertar contribuyente de ejemplo (para pruebas)
INSERT INTO contribuyentes (ruc_cedula, nombre, razon_social, direccion, correo, tipo) VALUES
    ('1234567890001', 'Empresa de Prueba S.A.', 'EMPRESA DE PRUEBA SOCIEDAD ANÓNIMA', 'Av. Principal 123, Quito', 'prueba@empresa.com', 'PERSONA_JURIDICA')
ON CONFLICT (ruc_cedula) DO NOTHING;

-- Comentarios en las tablas
COMMENT ON TABLE ordenes_recaudacion IS 'Tabla principal de órdenes de recaudación del sistema AOCR';
COMMENT ON TABLE detalles_orden IS 'Detalles/líneas de cada orden de recaudación';
COMMENT ON TABLE pagos IS 'Registro de pagos asociados a órdenes de recaudación';
COMMENT ON TABLE conceptos IS 'Catálogo de conceptos de cobro';
COMMENT ON TABLE contribuyentes IS 'Registro de contribuyentes/clientes';

-- Verificar creación
SELECT 'Tablas creadas exitosamente' AS resultado;
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('ordenes_recaudacion', 'detalles_orden', 'pagos', 'conceptos', 'contribuyentes');
