-- AOCR - Rol Director de Certificaciones DCAV y permisos de revision
-- Idempotente para PostgreSQL. No asigna usuarios; solo deja rol/permisos disponibles.

BEGIN;

DO $$
DECLARE
    _rol_id INTEGER;
    _perm_id BIGINT;
    _perm TEXT;
    _has_rol BOOLEAN;
    _has_perm BOOLEAN;
    _has_rol_perm BOOLEAN;
    _has_usuariocreado BOOLEAN;
    _has_fechacreado BOOLEAN;
BEGIN
    SELECT to_regclass('public.rol') IS NOT NULL INTO _has_rol;
    IF NOT _has_rol THEN
        RAISE NOTICE 'No existe public.rol. Se omite seed del rol DIRECTOR_CERTIFICACIONES_DCAV.';
        RETURN;
    END IF;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'rol' AND column_name = 'usuariocreado'
    ) INTO _has_usuariocreado;

    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'rol' AND column_name = 'fechacreado'
    ) INTO _has_fechacreado;

    SELECT codigorol
      INTO _rol_id
      FROM public.rol
     WHERE UPPER(TRIM(descripcion)) IN (
           'DIRECTOR_CERTIFICACIONES_DCAV',
           'DIRECTORCERTIFICACIONESDCAV',
           'DIRECTOR DE CERTIFICACIONES DCAV',
           'DCAV')
     ORDER BY CASE WHEN UPPER(TRIM(descripcion)) = 'DIRECTOR_CERTIFICACIONES_DCAV' THEN 0 ELSE 1 END
     LIMIT 1;

    IF _rol_id IS NULL THEN
        IF _has_usuariocreado AND _has_fechacreado THEN
            EXECUTE 'INSERT INTO public.rol (descripcion, activo, usuariocreado, fechacreado) VALUES ($1, TRUE, ''SYSTEM'', NOW()) RETURNING codigorol'
            INTO _rol_id
            USING 'DIRECTOR_CERTIFICACIONES_DCAV';
        ELSIF _has_usuariocreado THEN
            EXECUTE 'INSERT INTO public.rol (descripcion, activo, usuariocreado) VALUES ($1, TRUE, ''SYSTEM'') RETURNING codigorol'
            INTO _rol_id
            USING 'DIRECTOR_CERTIFICACIONES_DCAV';
        ELSIF _has_fechacreado THEN
            EXECUTE 'INSERT INTO public.rol (descripcion, activo, fechacreado) VALUES ($1, TRUE, NOW()) RETURNING codigorol'
            INTO _rol_id
            USING 'DIRECTOR_CERTIFICACIONES_DCAV';
        ELSE
            EXECUTE 'INSERT INTO public.rol (descripcion, activo) VALUES ($1, TRUE) RETURNING codigorol'
            INTO _rol_id
            USING 'DIRECTOR_CERTIFICACIONES_DCAV';
        END IF;
    ELSE
        UPDATE public.rol SET activo = TRUE WHERE codigorol = _rol_id;
    END IF;

    SELECT to_regclass('public.seguridad_permiso') IS NOT NULL INTO _has_perm;
    SELECT to_regclass('public.seguridad_rol_permiso') IS NOT NULL INTO _has_rol_perm;

    IF _has_perm AND _has_rol_perm THEN
        FOREACH _perm IN ARRAY ARRAY[
            'AOCR_DCAV_VER_EXPEDIENTE',
            'AOCR_DCAV_APROBAR',
            'AOCR_DCAV_DEVOLVER'
        ]
        LOOP
            INSERT INTO public.seguridad_permiso (codigo, nombre, modulo, activo, creado_por)
            SELECT
                _perm,
                CASE _perm
                    WHEN 'AOCR_DCAV_VER_EXPEDIENTE' THEN 'Ver expediente AOCR en revision DCAV'
                    WHEN 'AOCR_DCAV_APROBAR' THEN 'Aprobar revision DCAV'
                    ELSE 'Devolver expediente con observaciones DCAV'
                END,
                'AOCR_DCAV',
                TRUE,
                'SYSTEM'
            WHERE NOT EXISTS (
                SELECT 1 FROM public.seguridad_permiso WHERE UPPER(codigo) = UPPER(_perm)
            );

            SELECT id_permiso INTO _perm_id
              FROM public.seguridad_permiso
             WHERE UPPER(codigo) = UPPER(_perm)
             LIMIT 1;

            INSERT INTO public.seguridad_rol_permiso (codigorol, id_permiso, activo, creado_por)
            SELECT _rol_id, _perm_id, TRUE, 'SYSTEM'
            WHERE _perm_id IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1
                    FROM public.seguridad_rol_permiso
                   WHERE codigorol = _rol_id
                     AND id_permiso = _perm_id
              );

            UPDATE public.seguridad_rol_permiso
               SET activo = TRUE, actualizado_por = 'SYSTEM', actualizado_en = NOW()
             WHERE codigorol = _rol_id
               AND id_permiso = _perm_id;
        END LOOP;
    END IF;
END $$;

COMMIT;

SELECT codigorol, descripcion, activo
  FROM public.rol
 WHERE UPPER(TRIM(descripcion)) IN (
       'DIRECTOR_CERTIFICACIONES_DCAV',
       'DIRECTORCERTIFICACIONESDCAV',
       'DIRECTOR DE CERTIFICACIONES DCAV',
       'DCAV')
 ORDER BY descripcion;
