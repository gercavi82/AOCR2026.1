-- Diagnostico operativo: roles y visibilidad de bandejas AOCR
-- Fecha: 2026-04-20
-- Objetivo: validar si la fuente de roles en BD permite ver las bandejas esperadas
-- Nota: replica la estrategia runtime (usuario_rol + rol, fallback usuario.rol)

-- ==========================================================
-- 0) Presencia de tablas clave de seguridad
-- ==========================================================
SELECT
    'usuario' AS tabla,
    (to_regclass('public.usuario') IS NOT NULL) AS existe
UNION ALL
SELECT
    'usuario_rol' AS tabla,
    (to_regclass('public.usuario_rol') IS NOT NULL) AS existe
UNION ALL
SELECT
    'rol' AS tabla,
    (to_regclass('public.rol') IS NOT NULL) AS existe
UNION ALL
SELECT
    'seguridad_permiso' AS tabla,
    (to_regclass('public.seguridad_permiso') IS NOT NULL) AS existe
UNION ALL
SELECT
    'seguridad_rol_permiso' AS tabla,
    (to_regclass('public.seguridad_rol_permiso') IS NOT NULL) AS existe
ORDER BY tabla;


-- ==========================================================
-- 1) Construir roles efectivos por usuario (temp)
-- ==========================================================
DROP TABLE IF EXISTS tmp_roles_efectivos;
CREATE TEMP TABLE tmp_roles_efectivos (
    fuente text,
    idusuario int,
    codigousuario text,
    nombreusuario text,
    correo text,
    rol text
);

DO $$
DECLARE
    has_usuario boolean := to_regclass('public.usuario') IS NOT NULL;
    has_usuario_rol boolean := to_regclass('public.usuario_rol') IS NOT NULL;
    has_rol boolean := to_regclass('public.rol') IS NOT NULL;
BEGIN
    IF has_usuario AND has_usuario_rol AND has_rol THEN
        EXECUTE $q$
            INSERT INTO tmp_roles_efectivos (fuente, idusuario, codigousuario, nombreusuario, correo, rol)
            SELECT DISTINCT
                'usuario_rol+rol' AS fuente,
                COALESCE(NULLIF(to_jsonb(u)->>'idusuario', '')::int, 0) AS idusuario,
                COALESCE(NULLIF(to_jsonb(u)->>'codigousuario', ''), NULLIF(to_jsonb(ur)->>'codigousuario', '')) AS codigousuario,
                COALESCE(NULLIF(to_jsonb(u)->>'nombreusuario', ''), NULLIF(to_jsonb(u)->>'username', '')) AS nombreusuario,
                COALESCE(NULLIF(to_jsonb(u)->>'correo', ''), NULLIF(to_jsonb(u)->>'email', '')) AS correo,
                COALESCE(NULLIF(to_jsonb(r)->>'descripcion', ''), NULLIF(to_jsonb(r)->>'nombre', ''), NULLIF(to_jsonb(r)->>'codigo', '')) AS rol
            FROM usuario u
            INNER JOIN usuario_rol ur
                ON LOWER(TRIM(COALESCE(to_jsonb(u)->>'codigousuario', ''))) = LOWER(TRIM(COALESCE(to_jsonb(ur)->>'codigousuario', '')))
            INNER JOIN rol r
                ON COALESCE(to_jsonb(r)->>'codigorol', '') = COALESCE(to_jsonb(ur)->>'codigorol', '')
            WHERE
                COALESCE(NULLIF(LOWER(TRIM(COALESCE(to_jsonb(ur)->>'activo', 'true'))), ''), 'true')
                    IN ('true', 't', '1', 'si', 's')
                AND COALESCE(NULLIF(LOWER(TRIM(COALESCE(to_jsonb(r)->>'activo', 'true'))), ''), 'true')
                    IN ('true', 't', '1', 'si', 's')
                AND COALESCE(NULLIF(TRIM(COALESCE(to_jsonb(r)->>'descripcion', to_jsonb(r)->>'nombre', to_jsonb(r)->>'codigo', '')), ''), 'x') <> 'x';
        $q$;
    END IF;

    IF has_usuario THEN
        EXECUTE $q$
            INSERT INTO tmp_roles_efectivos (fuente, idusuario, codigousuario, nombreusuario, correo, rol)
            SELECT DISTINCT
                'usuario.rol (fallback)' AS fuente,
                COALESCE(NULLIF(to_jsonb(u)->>'idusuario', '')::int, 0) AS idusuario,
                NULLIF(to_jsonb(u)->>'codigousuario', '') AS codigousuario,
                COALESCE(NULLIF(to_jsonb(u)->>'nombreusuario', ''), NULLIF(to_jsonb(u)->>'username', '')) AS nombreusuario,
                COALESCE(NULLIF(to_jsonb(u)->>'correo', ''), NULLIF(to_jsonb(u)->>'email', '')) AS correo,
                NULLIF(BTRIM(COALESCE(to_jsonb(u)->>'rol', '')), '') AS rol
            FROM usuario u
            WHERE NULLIF(BTRIM(COALESCE(to_jsonb(u)->>'rol', '')), '') IS NOT NULL;
        $q$;
    END IF;
END
$$;


-- ==========================================================
-- 2) Resumen de roles efectivos
-- ==========================================================
WITH roles_norm AS (
    SELECT
        fuente,
        idusuario,
        codigousuario,
        nombreusuario,
        correo,
        rol,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(rol, '')), 'ÁÉÍÓÚ', 'AEIOU')), ' ', '') AS rol_norm
    FROM tmp_roles_efectivos
)
SELECT
    rol,
    rol_norm,
    COUNT(*) AS asignaciones,
    COUNT(DISTINCT idusuario) AS usuarios_distintos
FROM roles_norm
GROUP BY rol, rol_norm
ORDER BY usuarios_distintos DESC, rol;


-- ==========================================================
-- 3) Usuarios con roles target de aprobacion AOCR
-- ==========================================================
WITH roles_norm AS (
    SELECT
        fuente,
        idusuario,
        codigousuario,
        nombreusuario,
        correo,
        rol,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(rol, '')), 'ÁÉÍÓÚ', 'AEIOU')), ' ', '') AS rol_norm
    FROM tmp_roles_efectivos
), target AS (
    SELECT * FROM (VALUES
        ('DIRECCION'),
        ('JEFATURATECNICA'),
        ('COORDINACIONLEGAL'),
        ('COORDINADORLEGAL'),
        ('DIRDAC'),
        ('DIRECTORGENERAL'),
        ('ADMINISTRADOR')
    ) AS t(rol_norm)
)
SELECT
    r.fuente,
    r.idusuario,
    r.codigousuario,
    r.nombreusuario,
    r.correo,
    r.rol,
    r.rol_norm
FROM roles_norm r
INNER JOIN target t ON t.rol_norm = r.rol_norm
ORDER BY r.rol_norm, r.nombreusuario, r.idusuario
LIMIT 500;


-- ==========================================================
-- 4) Conteo por rol target y accesos esperados
-- ==========================================================
WITH roles_norm AS (
    SELECT
        idusuario,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(rol, '')), 'ÁÉÍÓÚ', 'AEIOU')), ' ', '') AS rol_norm
    FROM tmp_roles_efectivos
), target AS (
    SELECT * FROM (VALUES
        ('DIRECCION', true,  false, false),
        ('JEFATURATECNICA', true,  true,  false),
        ('COORDINACIONLEGAL', false, false, true),
        ('COORDINADORLEGAL', false, false, true),
        ('DIRDAC', true, false, false),
        ('DIRECTORGENERAL', true, false, true),
        ('ADMINISTRADOR', true, true, true)
    ) AS t(rol_norm, puede_bandeja_ejecutiva, puede_sub_bandeja_jefatura, puede_sub_bandeja_legal)
)
SELECT
    t.rol_norm,
    COUNT(DISTINCT r.idusuario) AS usuarios_con_rol,
    t.puede_bandeja_ejecutiva,
    t.puede_sub_bandeja_jefatura,
    t.puede_sub_bandeja_legal
FROM target t
LEFT JOIN roles_norm r ON r.rol_norm = t.rol_norm
GROUP BY t.rol_norm, t.puede_bandeja_ejecutiva, t.puede_sub_bandeja_jefatura, t.puede_sub_bandeja_legal
ORDER BY t.rol_norm;


-- ==========================================================
-- 5) Conteo actual de items por bandeja (para cruzar con roles)
-- ==========================================================
WITH base AS (
    SELECT
        s.codigo_solicitud,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
), canon AS (
    SELECT
        b.codigo_solicitud,
        CASE
            WHEN b.estado_norm IN ('AOCR EN ELABORACION') THEN 'AOCR_EN_ELABORACION'
            WHEN b.estado_norm IN ('AOCR EN REVISION', 'ENVIADO A JEFATURA') THEN 'AOCR_EN_REVISION'
            WHEN b.estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION') THEN 'AOCR_VALIDADO'
            WHEN b.estado_norm IN ('OBSERVADA', 'OBSERVADO', 'OBSERVADO JEFATURA') THEN 'OBSERVADA'
            WHEN b.estado_norm IN ('SUBSANADA', 'SUBSANADO') THEN 'SUBSANADA'
            ELSE 'OTRO'
        END AS estado_canon,
        b.estado_norm
    FROM base b
)
SELECT
    COUNT(*) FILTER (WHERE estado_canon IN ('AOCR_EN_ELABORACION', 'AOCR_EN_REVISION', 'AOCR_VALIDADO', 'OBSERVADA', 'SUBSANADA')) AS total_bandeja_ejecutiva,
    COUNT(*) FILTER (WHERE estado_norm IN ('ENVIADO A JEFATURA', 'AOCR EN REVISION', 'AOCR EN ELABORACION')) AS total_sub_bandeja_jefatura,
    COUNT(*) FILTER (WHERE estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION')) AS total_sub_bandeja_legal
FROM canon;


-- ==========================================================
-- 6) Matriz final: usuarios habilitados por bandeja vs items actuales
-- ==========================================================
WITH roles_norm AS (
    SELECT DISTINCT
        idusuario,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(rol, '')), 'ÁÉÍÓÚ', 'AEIOU')), ' ', '') AS rol_norm
    FROM tmp_roles_efectivos
), usuarios_habilitados AS (
    SELECT
        'bandeja_ejecutiva' AS bandeja,
        COUNT(DISTINCT idusuario) AS usuarios_habilitados
    FROM roles_norm
    WHERE rol_norm IN ('DIRDAC', 'DIRECCION', 'JEFATURATECNICA', 'DIRECTORGENERAL', 'ADMINISTRADOR')

    UNION ALL

    SELECT
        'sub_bandeja_jefatura' AS bandeja,
        COUNT(DISTINCT idusuario) AS usuarios_habilitados
    FROM roles_norm
    WHERE rol_norm IN ('JEFATURATECNICA', 'ADMINISTRADOR')

    UNION ALL

    SELECT
        'sub_bandeja_legal' AS bandeja,
        COUNT(DISTINCT idusuario) AS usuarios_habilitados
    FROM roles_norm
    WHERE rol_norm IN ('COORDINACIONLEGAL', 'COORDINADORLEGAL', 'DIRECTORGENERAL', 'ADMINISTRADOR')
), items AS (
    WITH base AS (
        SELECT
            REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm
        FROM aocr_tbsolicitud s
        WHERE s.deleted_at IS NULL
    ), canon AS (
        SELECT
            CASE
                WHEN estado_norm IN ('AOCR EN ELABORACION') THEN 'AOCR_EN_ELABORACION'
                WHEN estado_norm IN ('AOCR EN REVISION', 'ENVIADO A JEFATURA') THEN 'AOCR_EN_REVISION'
                WHEN estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION') THEN 'AOCR_VALIDADO'
                WHEN estado_norm IN ('OBSERVADA', 'OBSERVADO', 'OBSERVADO JEFATURA') THEN 'OBSERVADA'
                WHEN estado_norm IN ('SUBSANADA', 'SUBSANADO') THEN 'SUBSANADA'
                ELSE 'OTRO'
            END AS estado_canon,
            estado_norm
        FROM base
    )
    SELECT
        'bandeja_ejecutiva' AS bandeja,
        COUNT(*) FILTER (WHERE estado_canon IN ('AOCR_EN_ELABORACION', 'AOCR_EN_REVISION', 'AOCR_VALIDADO', 'OBSERVADA', 'SUBSANADA')) AS total_items
    FROM canon

    UNION ALL

    SELECT
        'sub_bandeja_jefatura' AS bandeja,
        COUNT(*) FILTER (WHERE estado_norm IN ('ENVIADO A JEFATURA', 'AOCR EN REVISION', 'AOCR EN ELABORACION')) AS total_items
    FROM canon

    UNION ALL

    SELECT
        'sub_bandeja_legal' AS bandeja,
        COUNT(*) FILTER (WHERE estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION')) AS total_items
    FROM canon
)
SELECT
    u.bandeja,
    u.usuarios_habilitados,
    i.total_items,
    CASE
        WHEN u.usuarios_habilitados = 0 THEN 'RIESGO: SIN USUARIOS CON ROL PARA ESTA BANDEJA'
        WHEN i.total_items = 0 THEN 'SIN DATOS EN ESTA ETAPA (esperable segun flujo)'
        ELSE 'OK: HAY USUARIOS Y HAY ITEMS'
    END AS diagnostico
FROM usuarios_habilitados u
INNER JOIN items i ON i.bandeja = u.bandeja
ORDER BY u.bandeja;
