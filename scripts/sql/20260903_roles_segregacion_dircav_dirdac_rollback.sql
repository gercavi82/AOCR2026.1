-- ==============================================================================
-- SCRIPT DE ROLLBACK: SEGREGACIÓN CANÓNICA DE ROLES Y PERMISOS DIRCAV Y DIRDAC
-- Fecha: 2026-09-03
-- ==============================================================================

BEGIN;

-- 1. Eliminar asignaciones de permisos creados
DELETE FROM public.seguridad_rol_permiso
WHERE id_permiso IN (
    SELECT id_permiso FROM public.seguridad_permiso
    WHERE codigo LIKE 'DIRCAV_%' OR codigo LIKE 'DIRDAC_%'
);

-- 2. Eliminar catálogo de permisos creados
DELETE FROM public.seguridad_permiso
WHERE codigo LIKE 'DIRCAV_%' OR codigo LIKE 'DIRDAC_%';

-- 3. Restaurar nombre del rol 27 a DCAV si se requiere reversión
UPDATE public.rol
SET descripcion = 'DCAV',
    fechamodificado = CURRENT_TIMESTAMP,
    usuariomodificado = 'SYSTEM_ROLLBACK'
WHERE codigorol = 27;

-- Nota: No se eliminan roles 28 ni 29 para evitar fallas en claves foráneas si se hubiesen asignado.

COMMIT;
