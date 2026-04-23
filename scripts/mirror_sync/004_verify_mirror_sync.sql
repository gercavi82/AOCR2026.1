-- 004_verify_mirror_sync.sql
-- Verificaciones rápidas post-despliegue de mirror sync.

-- 1) Estado de watermarks
SELECT table_name, status, last_success_ts, last_success_key, last_batch_id, updated_at
FROM sync.watermark
ORDER BY table_name;

-- 2) Últimos lotes
SELECT batch_id, table_name, status, rows_read, rows_applied, rows_rejected, rows_deleted, latency_ms, started_at, ended_at
FROM sync.batch_log
ORDER BY started_at DESC
LIMIT 50;

-- 3) Rechazos recientes
SELECT id, batch_id, table_name, error, created_at
FROM sync.rejections
ORDER BY created_at DESC
LIMIT 50;

-- 4) Conteos espejo (tablas iniciales)
SELECT 'mirror_raw.usuarc' AS tabla, COUNT(*) AS total, SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END) AS deleted
FROM mirror_raw.usuarc
UNION ALL
SELECT 'mirror_raw.usuar1', COUNT(*), SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END)
FROM mirror_raw.usuar1
UNION ALL
SELECT 'mirror_raw.ciaarc', COUNT(*), SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END)
FROM mirror_raw.ciaarc
UNION ALL
SELECT 'mirror_raw.opuarc01', COUNT(*), SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END)
FROM mirror_raw.opuarc01
UNION ALL
SELECT 'mirror_raw.oidar2', COUNT(*), SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END)
FROM mirror_raw.oidar2;

-- 5) Consistencia básica usuarios espejo
SELECT
    COUNT(*) AS usuarios_sin_codigo
FROM mirror_raw.usuarc
WHERE COALESCE(TRIM(usucod), '') = '';

SELECT
    COUNT(*) AS usuarios_activos,
    COUNT(*) FILTER (WHERE TRIM(COALESCE(usuest,'')) = 'AC') AS usuarios_estado_ac,
    COUNT(*) FILTER (WHERE TRIM(COALESCE(usuest,'')) = 'IA') AS usuarios_estado_ia
FROM mirror_raw.usuarc
WHERE COALESCE(_is_deleted, false) = false;

-- 6) Coincidencia USUAR1 huérfanos
SELECT COUNT(*) AS usuar1_sin_padre
FROM mirror_raw.usuar1 a
LEFT JOIN mirror_raw.usuarc u ON u.usucod = a.usuco8
WHERE u.usucod IS NULL
  AND COALESCE(a._is_deleted, false) = false;

-- 7) Cobertura ubicacion/lugar emision
SELECT COUNT(*) AS opuarc01_con_estacion
FROM mirror_raw.opuarc01
WHERE COALESCE(_is_deleted, false) = false
  AND COALESCE(TRIM(opuest), '') <> '';

SELECT COUNT(*) AS oidar2_con_estacion
FROM mirror_raw.oidar2
WHERE COALESCE(_is_deleted, false) = false
  AND COALESCE(TRIM(oidno2), '') <> '';
