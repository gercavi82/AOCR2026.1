CREATE TABLE IF NOT EXISTS public.aocr_tbcorreo_institucional (
    codigo_correo SERIAL PRIMARY KEY,
    codigo_area VARCHAR(80) NOT NULL UNIQUE,
    nombre_area VARCHAR(150) NOT NULL,
    correo_principal VARCHAR(250) NOT NULL,
    correos_cc TEXT NULL,
    correos_bcc TEXT NULL,
    descripcion TEXT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NULL,
    created_by VARCHAR(100) NULL,
    updated_by VARCHAR(100) NULL
);

CREATE TABLE IF NOT EXISTS public.aocr_tbcorreo_institucional_historial (
    codigo_historial SERIAL PRIMARY KEY,
    codigo_correo INTEGER NOT NULL,
    codigo_area VARCHAR(80) NOT NULL,
    correo_anterior VARCHAR(250) NULL,
    correo_nuevo VARCHAR(250) NULL,
    cc_anterior TEXT NULL,
    cc_nuevo TEXT NULL,
    bcc_anterior TEXT NULL,
    bcc_nuevo TEXT NULL,
    usuario_modificacion VARCHAR(100) NULL,
    fecha_modificacion TIMESTAMP NOT NULL DEFAULT NOW(),
    accion VARCHAR(50) NOT NULL
);

INSERT INTO public.aocr_tbcorreo_institucional
(codigo_area, nombre_area, correo_principal, descripcion, activo, created_at, created_by)
VALUES
('COORDINADOR_AOCR', 'Coordinador AOCR', 'coordinador.aocr@aviacioncivil.gob.ec', 'Correo institucional para notificaciones de asignación de inspector.', TRUE, NOW(), 'SYSTEM'),
('FINANCIERO_AOCR', 'Financiero AOCR', 'financiero.aocr@aviacioncivil.gob.ec', 'Correo institucional para notificaciones financieras.', TRUE, NOW(), 'SYSTEM'),
('DIRDAC', 'DIRDAC', 'dirdac@aviacioncivil.gob.ec', 'Correo institucional para decisiones DIRDAC.', TRUE, NOW(), 'SYSTEM'),
('DIRECCION_JEFATURA', 'Dirección / Jefatura', 'direccion.jefatura@aviacioncivil.gob.ec', 'Correo institucional para aprobación institucional.', TRUE, NOW(), 'SYSTEM'),
('SOPORTE_AOCR', 'Soporte AOCR', 'soporte.aocr@aviacioncivil.gob.ec', 'Correo institucional de soporte del sistema AOCR.', TRUE, NOW(), 'SYSTEM'),
('NOTIFICACIONES_AOCR', 'Notificaciones AOCR', 'notificaciones.aocr@aviacioncivil.gob.ec', 'Correo general de notificaciones del sistema AOCR.', TRUE, NOW(), 'SYSTEM')
ON CONFLICT (codigo_area) DO NOTHING;
