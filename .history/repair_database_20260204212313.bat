@echo off
echo === REPARACION DE BASE DE DATOS AOCR ===

REM Crear script SQL temporal
echo CREATE TABLE IF NOT EXISTS aocr_tbparametro ( > temp_repair.sql
echo     codigoparametro SERIAL PRIMARY KEY, >> temp_repair.sql
echo     clave VARCHAR(100) NOT NULL UNIQUE, >> temp_repair.sql
echo     valor VARCHAR(500) NOT NULL, >> temp_repair.sql
echo     descripcion VARCHAR(1000), >> temp_repair.sql
echo     activo BOOLEAN NOT NULL DEFAULT TRUE, >> temp_repair.sql
echo     createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP, >> temp_repair.sql
echo     createdby INTEGER, >> temp_repair.sql
echo     updatedat TIMESTAMP, >> temp_repair.sql
echo     updatedby INTEGER, >> temp_repair.sql
echo     deletedat TIMESTAMP, >> temp_repair.sql
echo     deletedby INTEGER >> temp_repair.sql
echo ); >> temp_repair.sql
echo. >> temp_repair.sql
echo ALTER TABLE aocr_tbpago ADD COLUMN IF NOT EXISTS banco VARCHAR(255); >> temp_repair.sql
echo. >> temp_repair.sql
echo INSERT INTO aocr_tbparametro (clave, valor, descripcion, activo, createdat, createdby) VALUES >> temp_repair.sql
echo ('TEST_EMPRESA_NOMBRE', 'AERONAUTICA CIVIL', 'Nombre de empresa para testing', TRUE, NOW(), 1), >> temp_repair.sql
echo ('DEMO_MONTO_FIJO', '80.00', 'Monto fijo para demostraciones', TRUE, NOW(), 1), >> temp_repair.sql
echo ('TARIFA_EMI_AOCR', '250.00', 'Tarifa emision AOCR', TRUE, NOW(), 1), >> temp_repair.sql
echo ('TARIFA_REN_AOCR', '200.00', 'Tarifa renovacion AOCR', TRUE, NOW(), 1), >> temp_repair.sql
echo ('PORCENTAJE_ADMIN_VIATICOS', '15', 'Porcentaje administrativo', TRUE, NOW(), 1) >> temp_repair.sql
echo ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor, updatedat = NOW(); >> temp_repair.sql
echo. >> temp_repair.sql
echo UPDATE aocr_tbpago SET banco = 'NO_ESPECIFICADO' WHERE banco IS NULL OR banco = ''; >> temp_repair.sql

echo Script SQL creado: temp_repair.sql
echo.
echo INSTRUCCIONES:
echo 1. Abra pgAdmin o su cliente PostgreSQL preferido
echo 2. Conectese a la base de datos AOCR
echo 3. Ejecute el contenido del archivo temp_repair.sql
echo 4. Reinicie la aplicacion web
echo.
echo Alternativamente, si tiene psql en el PATH:
echo psql -h localhost -U [usuario] -d [base_datos] -f temp_repair.sql
echo.

pause