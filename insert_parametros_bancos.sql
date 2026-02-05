-- Script para insertar parámetros de bancos configurables
-- Reemplaza los valores "quemados" en BancoP9DAO con valores configurables

-- Bancos principales de Ecuador (los que estaban hardcodeados)
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_001', 'BANCO CENTRAL DEL ECUADOR|BCE|Estado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_002', 'BANCO PICHINCHA|PICHINCHA|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_003', 'BANCO DEL PACIFICO|PACIFICO|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_004', 'BANCO GUAYAQUIL|GUAYAQUIL|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_005', 'BANCO INTERNACIONAL|INTERNACIONAL|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_006', 'BANCO BOLIVARIANO|BOLIVARIANO|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_007', 'BANCO MACHALA|MACHALA|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_008', 'BANCO PRODUBANCO|PRODUBANCO|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Bancos adicionales que pueden agregarse fácilmente
INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_009', 'BANCO LOJA|LOJA|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_010', 'BANCO SOLIDARIO|SOLIDARIO|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_011', 'COOPERATIVA DE AHORRO Y CREDITO JEP|COAC JEP|Cooperativa', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

INSERT INTO parametros (clave, valor, descripcion, activo, fecha_creacion, fecha_modificacion) 
VALUES ('BANCO_012', 'BANCO GENERAL RUMIÑAHUI|RUMIÑAHUI|Privado', 1, NOW(), NOW())
ON DUPLICATE KEY UPDATE 
    valor = VALUES(valor),
    descripcion = VALUES(descripcion),
    fecha_modificacion = NOW();

-- Verificar que se insertaron correctamente
SELECT clave, valor, descripcion, activo
FROM parametros 
WHERE clave LIKE 'BANCO_%'
ORDER BY clave;