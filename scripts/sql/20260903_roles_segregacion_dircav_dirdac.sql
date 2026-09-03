-- ==============================================================================
-- SCRIPT DE MIGRACIÓN: SEGREGACIÓN CANÓNICA DE ROLES Y PERMISOS DIRCAV Y DIRDAC
-- Fecha: 2026-09-03
-- Objetivo: Establecer los 7 roles canónicos del sistema AOCR, segregando
--           completamente DIRCAV y DIRDAC, con catálogo de permisos y auditoría.
-- ==============================================================================

BEGIN;

-- 1. Regularización en la tabla rol
-- Rol 27: Renombrar/regularizar a DIRCAV (conservando su ID y relaciones preexistentes)
UPDATE public.rol
SET descripcion = 'DIRCAV',
    fechamodificado = CURRENT_TIMESTAMP,
    usuariomodificado = 'SYSTEM_MIGRACION_CANONICA'
WHERE codigorol = 27 AND (descripcion = 'DCAV' OR descripcion = 'DIRCAV');

-- Asegurar rol DIRDAC (codigorol 26)
UPDATE public.rol
SET descripcion = 'DIRDAC',
    activo = TRUE,
    fechamodificado = CURRENT_TIMESTAMP,
    usuariomodificado = 'SYSTEM_MIGRACION_CANONICA'
WHERE codigorol = 26;

-- Insertar roles canónicos adicionales si no existen (COORDINADOR y RT)
INSERT INTO public.rol (codigorol, descripcion, activo, fecha_registro, usuariocreado, fechacreado)
SELECT 28, 'COORDINADOR', TRUE, CURRENT_TIMESTAMP, 'SYSTEM_MIGRACION_CANONICA', CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM public.rol WHERE descripcion = 'COORDINADOR' OR codigorol = 28);

INSERT INTO public.rol (codigorol, descripcion, activo, fecha_registro, usuariocreado, fechacreado)
SELECT 29, 'RT', TRUE, CURRENT_TIMESTAMP, 'SYSTEM_MIGRACION_CANONICA', CURRENT_TIMESTAMP
WHERE NOT EXISTS (SELECT 1 FROM public.rol WHERE descripcion = 'RT' OR codigorol = 29);

-- 2. Catálogo de permisos canónicos en seguridad_permiso
-- Asegurar permisos para DIRCAV
INSERT INTO public.seguridad_permiso (codigo, nombre, modulo, activo, creado_en, creado_por)
VALUES 
    ('DIRCAV_VER_BANDEJA', 'Ver bandeja de trámites DIRCAV', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REVISAR_DOCUMENTACION', 'Revisar expediente documental para aceptación', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_ACEPTAR_DOCUMENTACION', 'Aceptar formalmente documentación técnica', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_DEVOLVER_COORDINADOR', 'Devolver expediente al Coordinador con observaciones', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_DESIGNAR_INSPECTOR', 'Designar formalmente al Inspector de la solicitud', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_FIRMAR_DESIGNACION', 'Firmar digitalmente oficio de designación de Inspector', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REVISAR_INFORME', 'Revisar Informe Técnico remitido por Coordinación', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REVISAR_CL', 'Revisar Condiciones y Limitaciones del AOCR', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_FIRMAR_CL', 'Firmar digitalmente Condiciones y Limitaciones', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_REMITIR_DIRDAC', 'Remitir expediente y AOCR a DIRDAC para firma', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRCAV_VER_HISTORIAL', 'Consultar historial y auditoría de trámites DIRCAV', 'DIRCAV', TRUE, CURRENT_TIMESTAMP, 'SYSTEM')
ON CONFLICT (codigo) DO UPDATE 
SET nombre = EXCLUDED.nombre, modulo = EXCLUDED.modulo, activo = TRUE;

-- Asegurar permisos para DIRDAC
INSERT INTO public.seguridad_permiso (codigo, nombre, modulo, activo, creado_en, creado_por)
VALUES 
    ('DIRDAC_VER_BANDEJA', 'Ver bandeja institucional de AOCR DIRDAC', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_REVISAR_AOCR', 'Revisar AOCR y expediente aprobado por DIRCAV', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_FIRMAR_AOCR', 'Legalizar y firmar digitalmente documento AOCR', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_DEVOLVER_DIRCAV', 'Devolver expediente a DIRCAV con observaciones', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_CONFIRMAR_LEGALIZACION', 'Confirmar culminación y entrega de trámite', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM'),
    ('DIRDAC_VER_HISTORIAL', 'Consultar historial y trámites concluidos DIRDAC', 'DIRDAC', TRUE, CURRENT_TIMESTAMP, 'SYSTEM')
ON CONFLICT (codigo) DO UPDATE 
SET nombre = EXCLUDED.nombre, modulo = EXCLUDED.modulo, activo = TRUE;

-- 3. Asignación exclusiva de permisos a roles en seguridad_rol_permiso
-- Obtener id de permisos y asociar a codigorol = 27 (DIRCAV)
INSERT INTO public.seguridad_rol_permiso (codigorol, id_permiso, activo, creado_en, creado_por)
SELECT 27, p.id_permiso, TRUE, CURRENT_TIMESTAMP, 'SYSTEM'
FROM public.seguridad_permiso p
WHERE p.modulo = 'DIRCAV'
ON CONFLICT (codigorol, id_permiso) DO UPDATE SET activo = TRUE;

-- Asociar permisos a codigorol = 26 (DIRDAC)
INSERT INTO public.seguridad_rol_permiso (codigorol, id_permiso, activo, creado_en, creado_por)
SELECT 26, p.id_permiso, TRUE, CURRENT_TIMESTAMP, 'SYSTEM'
FROM public.seguridad_permiso p
WHERE p.modulo = 'DIRDAC'
ON CONFLICT (codigorol, id_permiso) DO UPDATE SET activo = TRUE;

-- Desactivar cualquier permiso cruzado si existiera previamente
UPDATE public.seguridad_rol_permiso
SET activo = FALSE
WHERE (codigorol = 27 AND id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso WHERE modulo = 'DIRDAC'))
   OR (codigorol = 26 AND id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso WHERE modulo = 'DIRCAV'));

-- Regla 7: El Administrador (codigorol = 1) no debe tener permisos operativos de firma
UPDATE public.seguridad_rol_permiso
SET activo = FALSE
WHERE codigorol = 1 
  AND id_permiso IN (
      SELECT id_permiso FROM public.seguridad_permiso 
      WHERE codigo IN ('DIRCAV_FIRMAR_DESIGNACION', 'DIRCAV_FIRMAR_CL', 'DIRDAC_FIRMAR_AOCR')
  );

COMMIT;
