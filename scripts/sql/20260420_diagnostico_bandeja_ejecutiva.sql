-- Diagnostico operativo: Bandeja ejecutiva de aprobacion AOCR
-- Fecha: 2026-04-20
-- Objetivo: validar datos reales, conteos, sub-bandejas y flujo previo

-- ==========================================================
-- 0) Catalogo de estados reales en tabla solicitud
-- ==========================================================
SELECT
    estado,
    COUNT(*) AS total
FROM aocr_tbsolicitud
WHERE deleted_at IS NULL
GROUP BY estado
ORDER BY total DESC, estado ASC;


-- ==========================================================
-- 1) Base normalizada de estados (canon + legacy)
-- ==========================================================
WITH base AS (
    SELECT
        s.codigo_solicitud,
        s.numero_solicitud,
        s.estado AS estado_raw,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm,
        s.fecha_solicitud,
        s.codigo_usuario,
        s.codigo_tecnico
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
),
canon AS (
    SELECT
        b.*,
        CASE
            WHEN b.estado_norm IN ('AOCR EN ELABORACION') THEN 'AOCR_EN_ELABORACION'
            WHEN b.estado_norm IN ('AOCR EN REVISION', 'ENVIADO A JEFATURA') THEN 'AOCR_EN_REVISION'
            WHEN b.estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION') THEN 'AOCR_VALIDADO'
            WHEN b.estado_norm IN ('AOCR LEGALIZADO', 'LEGALIZADO', 'CERTIFICADO LEGALIZADO') THEN 'AOCR_LEGALIZADO'
            WHEN b.estado_norm IN ('OBSERVADA', 'OBSERVADO', 'OBSERVADO JEFATURA') THEN 'OBSERVADA'
            WHEN b.estado_norm IN ('SUBSANADA', 'SUBSANADO') THEN 'SUBSANADA'
            ELSE 'OTRO'
        END AS estado_canon
    FROM base b
)
SELECT
    estado_canon,
    COUNT(*) AS total
FROM canon
GROUP BY estado_canon
ORDER BY total DESC, estado_canon;


-- ==========================================================
-- 2) Bandeja ejecutiva (filtro esperado en backend)
-- ==========================================================
WITH base AS (
    SELECT
        s.codigo_solicitud,
        s.numero_solicitud,
        s.estado AS estado_raw,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm,
        s.fecha_solicitud
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
),
canon AS (
    SELECT
        b.*,
        CASE
            WHEN b.estado_norm IN ('AOCR EN ELABORACION') THEN 'AOCR_EN_ELABORACION'
            WHEN b.estado_norm IN ('AOCR EN REVISION', 'ENVIADO A JEFATURA') THEN 'AOCR_EN_REVISION'
            WHEN b.estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION') THEN 'AOCR_VALIDADO'
            WHEN b.estado_norm IN ('OBSERVADA', 'OBSERVADO', 'OBSERVADO JEFATURA') THEN 'OBSERVADA'
            WHEN b.estado_norm IN ('SUBSANADA', 'SUBSANADO') THEN 'SUBSANADA'
            ELSE 'OTRO'
        END AS estado_canon
    FROM base b
),
bandeja AS (
    SELECT *
    FROM canon
    WHERE estado_canon IN ('AOCR_EN_ELABORACION', 'AOCR_EN_REVISION', 'AOCR_VALIDADO', 'OBSERVADA', 'SUBSANADA')
)
SELECT
    COUNT(*) AS total_bandeja,
    SUM(CASE WHEN estado_canon IN ('AOCR_EN_ELABORACION', 'AOCR_EN_REVISION', 'AOCR_VALIDADO') THEN 1 ELSE 0 END) AS en_revision,
    SUM(CASE WHEN estado_canon = 'OBSERVADA' THEN 1 ELSE 0 END) AS observadas,
    SUM(CASE WHEN estado_canon = 'SUBSANADA' THEN 1 ELSE 0 END) AS subsanadas
FROM bandeja;


-- ==========================================================
-- 3) Listado de bandeja (muestra para validacion funcional)
-- ==========================================================
WITH base AS (
    SELECT
        s.codigo_solicitud,
        COALESCE(
            NULLIF(to_jsonb(s)->>'numero_solicitud', ''),
            NULLIF(to_jsonb(s)->>'numeroSolicitud', ''),
            s.codigo_solicitud::text
        ) AS numero_solicitud,
        COALESCE(
            NULLIF(to_jsonb(s)->>'nombre_operador', ''),
            NULLIF(to_jsonb(s)->>'nombre_explotador', ''),
            NULLIF(to_jsonb(s)->>'operador', ''),
            NULLIF(to_jsonb(s)->>'nombre_compania', ''),
            'No especificado'
        ) AS nombre_operador,
        COALESCE(
            NULLIF(to_jsonb(s)->>'codigo_oaci', ''),
            NULLIF(to_jsonb(s)->>'codigo_oasi', ''),
            NULLIF(to_jsonb(s)->>'codigo_icao', ''),
            NULLIF(to_jsonb(s)->>'icao', ''),
            'No registrado'
        ) AS codigo_oaci,
        COALESCE(
            NULLIF(to_jsonb(s)->>'representante_legal', ''),
            NULLIF(to_jsonb(s)->>'nombre_representante_legal', ''),
            'No registrado'
        ) AS representante_legal,
        COALESCE(
            NULLIF(to_jsonb(s)->>'email', ''),
            NULLIF(to_jsonb(s)->>'correo', ''),
            NULLIF(to_jsonb(s)->>'correo_electronico', ''),
            'Sin correo'
        ) AS email,
        s.estado AS estado_raw,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm,
        s.fecha_solicitud
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
),
canon AS (
    SELECT
        b.*,
        CASE
            WHEN b.estado_norm IN ('AOCR EN ELABORACION') THEN 'AOCR_EN_ELABORACION'
            WHEN b.estado_norm IN ('AOCR EN REVISION', 'ENVIADO A JEFATURA') THEN 'AOCR_EN_REVISION'
            WHEN b.estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION') THEN 'AOCR_VALIDADO'
            WHEN b.estado_norm IN ('OBSERVADA', 'OBSERVADO', 'OBSERVADO JEFATURA') THEN 'OBSERVADA'
            WHEN b.estado_norm IN ('SUBSANADA', 'SUBSANADO') THEN 'SUBSANADA'
            ELSE 'OTRO'
        END AS estado_canon
    FROM base b
)
SELECT
    codigo_solicitud,
    numero_solicitud,
    nombre_operador,
    codigo_oaci,
    representante_legal,
    email,
    estado_raw,
    estado_canon,
    fecha_solicitud
FROM canon
WHERE estado_canon IN ('AOCR_EN_ELABORACION', 'AOCR_EN_REVISION', 'AOCR_VALIDADO', 'OBSERVADA', 'SUBSANADA')
ORDER BY fecha_solicitud DESC NULLS LAST
LIMIT 200;


-- ==========================================================
-- 4) Sub-bandeja Jefatura tecnica
-- ==========================================================
WITH base AS (
    SELECT
        s.codigo_solicitud,
        s.numero_solicitud,
        s.estado,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm,
        s.fecha_solicitud
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
)
SELECT
    codigo_solicitud,
    numero_solicitud,
    estado,
    fecha_solicitud
FROM base
WHERE estado_norm IN ('ENVIADO A JEFATURA', 'AOCR EN REVISION', 'AOCR EN ELABORACION')
ORDER BY fecha_solicitud DESC NULLS LAST
LIMIT 200;


-- ==========================================================
-- 5) Sub-bandeja Revision legal
-- ==========================================================
WITH base AS (
    SELECT
        s.codigo_solicitud,
        s.numero_solicitud,
        s.estado,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm,
        s.fecha_solicitud
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
)
SELECT
    codigo_solicitud,
    numero_solicitud,
    estado,
    fecha_solicitud
FROM base
WHERE estado_norm IN ('AOCR VALIDADO', 'VALIDADO', 'VALIDADO TECNICAMENTE', 'ENVIADO A LEGALIZACION', 'APROBADO POR DIRECCION')
ORDER BY fecha_solicitud DESC NULLS LAST
LIMIT 200;


-- ==========================================================
-- 6) Control de consistencia: estados sospechosos no capturados
-- ==========================================================
WITH base AS (
    SELECT
        s.estado,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(s.estado, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_norm
    FROM aocr_tbsolicitud s
    WHERE s.deleted_at IS NULL
),
agrupado AS (
    SELECT
        estado,
        estado_norm,
        COUNT(*) AS total
    FROM base
    GROUP BY estado, estado_norm
)
SELECT
    estado,
    estado_norm,
    total
FROM agrupado
WHERE (
    estado_norm LIKE '%AOCR%'
    OR estado_norm LIKE '%REVISION%'
    OR estado_norm LIKE '%VALID%'
    OR estado_norm LIKE '%OBSERV%'
    OR estado_norm LIKE '%SUBSAN%'
    OR estado_norm LIKE '%JEFATURA%'
    OR estado_norm LIKE '%LEGALIZ%'
)
AND estado_norm NOT IN (
    'AOCR EN ELABORACION',
    'AOCR EN REVISION',
    'ENVIADO A JEFATURA',
    'AOCR VALIDADO',
    'VALIDADO',
    'VALIDADO TECNICAMENTE',
    'ENVIADO A LEGALIZACION',
    'APROBADO POR DIRECCION',
    'OBSERVADA',
    'OBSERVADO',
    'OBSERVADO JEFATURA',
    'SUBSANADA',
    'SUBSANADO',
    'AOCR LEGALIZADO',
    'LEGALIZADO',
    'CERTIFICADO LEGALIZADO'
)
ORDER BY total DESC, estado;


-- ==========================================================
-- 7) Flujo previo -> bandeja (historial de cambios)
--    Requiere: aocr_tbhistorial_estado
-- ==========================================================
WITH hist AS (
    SELECT
        h.codigo_solicitud,
        h.estado_anterior,
        h.estado_nuevo,
        h.fecha_cambio,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(h.estado_nuevo, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_nuevo_norm
    FROM aocr_tbhistorial_estado h
)
SELECT
    codigo_solicitud,
    estado_anterior,
    estado_nuevo,
    fecha_cambio
FROM hist
WHERE estado_nuevo_norm IN (
    'AOCR EN ELABORACION',
    'AOCR EN REVISION',
    'ENVIADO A JEFATURA',
    'AOCR VALIDADO',
    'VALIDADO',
    'VALIDADO TECNICAMENTE',
    'ENVIADO A LEGALIZACION',
    'OBSERVADA',
    'OBSERVADO',
    'SUBSANADA',
    'SUBSANADO'
)
ORDER BY fecha_cambio DESC
LIMIT 300;


-- ==========================================================
-- 8) Ultimo estado por solicitud y entrada a bandeja
-- ==========================================================
WITH ult AS (
    SELECT
        h.codigo_solicitud,
        h.estado_anterior,
        h.estado_nuevo,
        h.fecha_cambio,
        ROW_NUMBER() OVER (PARTITION BY h.codigo_solicitud ORDER BY h.fecha_cambio DESC) AS rn,
        REPLACE(TRIM(TRANSLATE(UPPER(COALESCE(h.estado_nuevo, '')), 'ÁÉÍÓÚ', 'AEIOU')), '_', ' ') AS estado_nuevo_norm
    FROM aocr_tbhistorial_estado h
)
SELECT
    u.codigo_solicitud,
    u.estado_anterior,
    u.estado_nuevo,
    u.fecha_cambio
FROM ult u
WHERE u.rn = 1
AND u.estado_nuevo_norm IN (
    'AOCR EN ELABORACION',
    'AOCR EN REVISION',
    'ENVIADO A JEFATURA',
    'AOCR VALIDADO',
    'VALIDADO',
    'VALIDADO TECNICAMENTE',
    'ENVIADO A LEGALIZACION',
    'OBSERVADA',
    'OBSERVADO',
    'SUBSANADA',
    'SUBSANADO'
)
ORDER BY u.fecha_cambio DESC
LIMIT 300;


-- Fin de script
