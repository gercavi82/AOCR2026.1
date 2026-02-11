-- Agrega columnas para el flujo de designación RT en la tabla usuario
ALTER TABLE usuario
ADD COLUMN IF NOT EXISTS estado_designacion_rt VARCHAR(20) DEFAULT 'pendiente';

ALTER TABLE usuario
ADD COLUMN IF NOT EXISTS fecha_revision_designacion TIMESTAMP;

ALTER TABLE usuario
ADD COLUMN IF NOT EXISTS ruta_constancia_rt VARCHAR(255);
