-- Script para verificar las tablas de usuario disponibles
-- Este script ayuda a identificar las tablas correctas en la base de datos AOCR

-- Verificar estructura de la tabla usuario
SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
WHERE table_name = 'usuario' 
ORDER BY ordinal_position;

-- Verificar estructura de la tabla usuario_rol
SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
WHERE table_name = 'usuario_rol' 
ORDER BY ordinal_position;

-- Verificar si existe la tabla aocr_tbusuario
SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
WHERE table_name = 'aocr_tbusuario' 
ORDER BY ordinal_position;

-- Mostrar una muestra de datos de usuario (sin contraseñas)
SELECT codigousuario, nombre, email, activo 
FROM usuario 
LIMIT 5;