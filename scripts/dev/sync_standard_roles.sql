-- 1. Insertar roles canónicos si no existen
INSERT INTO rol (descripcion, activo)
SELECT 'CoordinadorInspecciones', TRUE
WHERE NOT EXISTS (SELECT 1 FROM rol WHERE UPPER(TRIM(descripcion)) = 'COORDINADORINSPECCIONES');

INSERT INTO rol (descripcion, activo)
SELECT 'DIRDAC', TRUE
WHERE NOT EXISTS (SELECT 1 FROM rol WHERE UPPER(TRIM(descripcion)) = 'DIRDAC');

INSERT INTO rol (descripcion, activo)
SELECT 'DCAV', TRUE
WHERE NOT EXISTS (SELECT 1 FROM rol WHERE UPPER(TRIM(descripcion)) = 'DCAV');

-- 2. Asegurar que permisos de coordinación existan para CoordinadorInspecciones (copiar de CoordinacionLegal / CoordinadorLegal si aplica)
DO $$
DECLARE
    v_cod_coordinador INT;
    v_cod_legal INT;
    v_cod_dirdac INT;
    v_cod_jefatura INT;
    v_cod_dcav INT;
    v_cod_inspector INT;
    v_perm_solicitud INT;
    v_perm_inspeccion INT;
BEGIN
    SELECT codigorol INTO v_cod_coordinador FROM rol WHERE UPPER(TRIM(descripcion)) = 'COORDINADORINSPECCIONES' LIMIT 1;
    SELECT codigorol INTO v_cod_legal FROM rol WHERE UPPER(TRIM(descripcion)) IN ('COORDINACIONLEGAL', 'COORDINADORLEGAL') LIMIT 1;
    SELECT codigorol INTO v_cod_dirdac FROM rol WHERE UPPER(TRIM(descripcion)) = 'DIRDAC' LIMIT 1;
    SELECT codigorol INTO v_cod_jefatura FROM rol WHERE UPPER(TRIM(descripcion)) IN ('JEFATURATECNICA', 'DIRECCION') LIMIT 1;
    SELECT codigorol INTO v_cod_dcav FROM rol WHERE UPPER(TRIM(descripcion)) = 'DCAV' LIMIT 1;
    SELECT codigorol INTO v_cod_inspector FROM rol WHERE UPPER(TRIM(descripcion)) = 'INSPECTOR' LIMIT 1;

    -- Permisos para CoordinadorInspecciones copiados de v_cod_legal o creados
    IF v_cod_coordinador IS NOT NULL AND v_cod_legal IS NOT NULL THEN
        INSERT INTO seguridad_rol_permiso (codigorol, id_permiso, activo)
        SELECT v_cod_coordinador, id_permiso, TRUE
        FROM seguridad_rol_permiso
        WHERE codigorol = v_cod_legal AND activo = TRUE
        ON CONFLICT (codigorol, id_permiso) DO NOTHING;
    END IF;

    -- Permisos para DIRDAC y DCAV copiados de JefaturaTecnica
    IF v_cod_dirdac IS NOT NULL AND v_cod_jefatura IS NOT NULL THEN
        INSERT INTO seguridad_rol_permiso (codigorol, id_permiso, activo)
        SELECT v_cod_dirdac, id_permiso, TRUE
        FROM seguridad_rol_permiso
        WHERE codigorol = v_cod_jefatura AND activo = TRUE
        ON CONFLICT (codigorol, id_permiso) DO NOTHING;
    END IF;

    IF v_cod_dcav IS NOT NULL AND v_cod_jefatura IS NOT NULL THEN
        INSERT INTO seguridad_rol_permiso (codigorol, id_permiso, activo)
        SELECT v_cod_dcav, id_permiso, TRUE
        FROM seguridad_rol_permiso
        WHERE codigorol = v_cod_jefatura AND activo = TRUE
        ON CONFLICT (codigorol, id_permiso) DO NOTHING;
    END IF;
END $$;

-- 3. Consultar todos los roles activos
SELECT codigorol, descripcion, activo FROM rol WHERE activo = TRUE ORDER BY codigorol;
