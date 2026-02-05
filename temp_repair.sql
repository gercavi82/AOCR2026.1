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
 
ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255); 
 
INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) VALUES 
('TEST_EMPRESA_NOMBRE', 'AERONAUTICA CIVIL', 'Nombre de empresa para testing', TRUE, NOW(), 1), 
('DEMO_MONTO_FIJO', '80.00', 'Monto fijo para demostraciones', TRUE, NOW(), 1), 
('TARIFA_EMI_AOCR', '250.00', 'Tarifa emision AOCR', TRUE, NOW(), 1), 
('TARIFA_REN_AOCR', '200.00', 'Tarifa renovacion AOCR', TRUE, NOW(), 1), 
('PORCENTAJE_ADMIN_VIATICOS', '15', 'Porcentaje administrativo', TRUE, NOW(), 1) 
ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, updatedat = NOW(); 
 
UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL OR banco = ''; 
