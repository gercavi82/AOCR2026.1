-- AOCR - Rollback permisos/rol Director de Certificaciones DCAV
-- No elimina usuarios. Solo desactiva permisos y el rol sembrado por el script 20260709.

BEGIN;

DO $$
DECLARE
    _rol_id INTEGER;
BEGIN
    IF to_regclass('public.rol') IS NULL THEN
        RETURN;
    END IF;

    SELECT codigorol
      INTO _rol_id
      FROM public.rol
     WHERE UPPER(TRIM(descripcion)) = 'DIRECTOR_CERTIFICACIONES_DCAV'
     LIMIT 1;

    IF _rol_id IS NOT NULL THEN
        IF to_regclass('public.seguridad_rol_permiso') IS NOT NULL
           AND to_regclass('public.seguridad_permiso') IS NOT NULL THEN
            UPDATE public.seguridad_rol_permiso rp
               SET activo = FALSE, actualizado_por = 'SYSTEM_ROLLBACK', actualizado_en = NOW()
              FROM public.seguridad_permiso p
             WHERE rp.id_permiso = p.id_permiso
               AND rp.codigorol = _rol_id
               AND UPPER(p.codigo) IN (
                   'AOCR_DCAV_VER_EXPEDIENTE',
                   'AOCR_DCAV_APROBAR',
                   'AOCR_DCAV_DEVOLVER'
               );

            UPDATE public.seguridad_permiso
               SET activo = FALSE, actualizado_por = 'SYSTEM_ROLLBACK', actualizado_en = NOW()
             WHERE UPPER(codigo) IN (
                   'AOCR_DCAV_VER_EXPEDIENTE',
                   'AOCR_DCAV_APROBAR',
                   'AOCR_DCAV_DEVOLVER'
               );
        END IF;

        UPDATE public.rol
           SET activo = FALSE
         WHERE codigorol = _rol_id;
    END IF;
END $$;

COMMIT;
