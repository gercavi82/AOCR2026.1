-- Reparación aditiva de la infraestructura de Permisos por rol.
-- Conserva permisos existentes, usuarios y asignaciones por rol.
BEGIN;

ALTER TABLE public.seguridad_permiso
    ADD COLUMN IF NOT EXISTS tipo_accion VARCHAR(30),
    ADD COLUMN IF NOT EXISTS descripcion VARCHAR(300);

UPDATE public.seguridad_permiso
SET tipo_accion = CASE
    WHEN codigo IN ('ADM_GESTION_USUARIOS','ADM_ROLES_PERMISOS','ADM_RESET_PASSWORD') THEN 'EDITAR'
    WHEN UPPER(codigo) LIKE '%FIRMAR%' THEN 'FIRMAR'
    WHEN UPPER(codigo) LIKE '%APROBAR%' OR UPPER(codigo) LIKE '%ACEPTAR%' THEN 'APROBAR'
    WHEN UPPER(codigo) LIKE '%DEVOLVER%' OR UPPER(codigo) LIKE '%RECHAZAR%' THEN 'DEVOLVER'
    WHEN UPPER(codigo) LIKE '%GENERAR%' THEN 'GENERAR'
    WHEN UPPER(codigo) LIKE '%SOLICITAR%' OR UPPER(codigo) LIKE '%CREAR%' THEN 'CREAR'
    WHEN UPPER(codigo) LIKE '%EDITAR%' OR UPPER(codigo) LIKE '%GESTION%' THEN 'EDITAR'
    WHEN UPPER(codigo) LIKE '%ASIGNAR%' OR UPPER(codigo) LIKE '%DESIGNAR%' THEN 'ASIGNAR'
    WHEN UPPER(codigo) LIKE '%DESCARGAR%' THEN 'DESCARGAR'
    WHEN UPPER(codigo) LIKE '%EXPORTAR%' THEN 'EXPORTAR'
    WHEN UPPER(codigo) LIKE '%ELIMINAR%' OR UPPER(codigo) LIKE '%ANULAR%' THEN 'ELIMINAR'
    WHEN UPPER(codigo) LIKE '%REVISAR%' THEN 'REVISAR'
    ELSE 'VER'
END
WHERE tipo_accion IS NULL OR BTRIM(tipo_accion) = '';

UPDATE public.seguridad_permiso
SET descripcion = COALESCE(NULLIF(BTRIM(nombre), ''), codigo)
WHERE descripcion IS NULL OR BTRIM(descripcion) = '';

ALTER TABLE public.seguridad_permiso
    ALTER COLUMN tipo_accion SET NOT NULL,
    ALTER COLUMN descripcion SET NOT NULL;

COMMENT ON COLUMN public.seguridad_permiso.tipo_accion IS 'Acción funcional utilizada por la gestión de permisos.';
COMMENT ON COLUMN public.seguridad_permiso.descripcion IS 'Descripción funcional del permiso.';

COMMIT;
