BEGIN;

ALTER TABLE public.seguridad_permiso
    DROP COLUMN IF EXISTS descripcion;

ALTER TABLE public.seguridad_permiso
    DROP COLUMN IF EXISTS tipo_accion;

COMMIT;
