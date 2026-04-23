-- ==============================================================================
-- 006_acceptance_gate.sql
-- AOCR / AS400 Mirror Sync - Gate de aceptacion para produccion
-- Ejecutar despues de 005_validate_full_mirror_sync.sql
-- Solo lectura (SELECT). No modifica datos.
-- ==============================================================================

\echo '--- [A1] Tablas mirror requeridas ---'
WITH required(table_name) AS (
    VALUES
      ('usuarc'),('usuar1'),('ciaarc'),('opuarc01'),('oidar2'),
      ('opiar2'),('txdgac'),('opsarc'),('opcar5'),('opcar6')
)
SELECT
    r.table_name,
    CASE WHEN t.table_name IS NULL THEN 'MISSING' ELSE 'OK' END AS status
FROM required r
LEFT JOIN information_schema.tables t
  ON t.table_schema = 'mirror_raw'
 AND t.table_name = r.table_name
ORDER BY r.table_name;

\echo '--- [A2] Conteo por tabla requerida ---'
SELECT 'mirror_raw.usuarc' tabla, COUNT(*) total FROM mirror_raw.usuarc
UNION ALL SELECT 'mirror_raw.usuar1', COUNT(*) FROM mirror_raw.usuar1
UNION ALL SELECT 'mirror_raw.ciaarc', COUNT(*) FROM mirror_raw.ciaarc
UNION ALL SELECT 'mirror_raw.opuarc01', COUNT(*) FROM mirror_raw.opuarc01
UNION ALL SELECT 'mirror_raw.oidar2', COUNT(*) FROM mirror_raw.oidar2
UNION ALL SELECT 'mirror_raw.opiar2', COUNT(*) FROM mirror_raw.opiar2
UNION ALL SELECT 'mirror_raw.txdgac', COUNT(*) FROM mirror_raw.txdgac
UNION ALL SELECT 'mirror_raw.opsarc', COUNT(*) FROM mirror_raw.opsarc
UNION ALL SELECT 'mirror_raw.opcar5', COUNT(*) FROM mirror_raw.opcar5
UNION ALL SELECT 'mirror_raw.opcar6', COUNT(*) FROM mirror_raw.opcar6;

\echo '--- [A3] Duplicados por PK (debe retornar 0 filas en cada bloque) ---'
SELECT usucod, COUNT(*) cnt FROM mirror_raw.usuarc GROUP BY usucod HAVING COUNT(*) > 1;
SELECT usuco8, COUNT(*) cnt FROM mirror_raw.usuar1 GROUP BY usuco8 HAVING COUNT(*) > 1;
SELECT ciacod, COUNT(*) cnt FROM mirror_raw.ciaarc GROUP BY ciacod HAVING COUNT(*) > 1;
SELECT opucod, COUNT(*) cnt FROM mirror_raw.opuarc01 GROUP BY opucod HAVING COUNT(*) > 1;
SELECT oidco3, oidoi2, COUNT(*) cnt FROM mirror_raw.oidar2 GROUP BY oidco3, oidoi2 HAVING COUNT(*) > 1;
SELECT opiced, opitip, COUNT(*) cnt FROM mirror_raw.opiar2 GROUP BY opiced, opitip HAVING COUNT(*) > 1;
SELECT valdds, valval, COUNT(*) cnt FROM mirror_raw.txdgac GROUP BY valdds, valval HAVING COUNT(*) > 1;
SELECT opsaer, opsano, COUNT(*) cnt FROM mirror_raw.opsarc GROUP BY opsaer, opsano HAVING COUNT(*) > 1;
SELECT opcsec, opcaer, opcano, COUNT(*) cnt FROM mirror_raw.opcar5 GROUP BY opcsec, opcaer, opcano HAVING COUNT(*) > 1;
SELECT opcse2, opcae1, opcan1, opcse1, COUNT(*) cnt FROM mirror_raw.opcar6 GROUP BY opcse2, opcae1, opcan1, opcse1 HAVING COUNT(*) > 1;

\echo '--- [A4] Estado watermarks esperados ---'
WITH required(name) AS (
    VALUES
      ('USUARC'),('USUAR1'),('CIAARC'),('OPUARC01'),('OIDAR2'),
      ('OPIAR2'),('TXDGAC'),('OPSARC'),('OPCAR5'),('OPCAR6')
)
SELECT
    r.name AS table_name,
    COALESCE(w.status, 'MISSING') AS status,
    w.last_success_ts,
    w.updated_at
FROM required r
LEFT JOIN sync.watermark w ON w.table_name = r.name
ORDER BY r.name;

\echo '--- [A5] Calidad operativa (rejections y tombstones) ---'
SELECT table_name, COUNT(*) pending
FROM sync.tombstones
WHERE applied = false
GROUP BY table_name
ORDER BY table_name;

SELECT table_name, COUNT(*) rejects_24h
FROM sync.rejections
WHERE created_at >= (now() - interval '24 hours')
GROUP BY table_name
ORDER BY table_name;

\echo '--- [A6] Gate resumen (PASS/FAIL) ---'
WITH required_tables AS (
    SELECT COUNT(*)::int AS cnt
    FROM (VALUES
      ('usuarc'),('usuar1'),('ciaarc'),('opuarc01'),('oidar2'),
      ('opiar2'),('txdgac'),('opsarc'),('opcar5'),('opcar6')
    ) v(t)
),
existing_tables AS (
    SELECT COUNT(*)::int AS cnt
    FROM information_schema.tables
    WHERE table_schema='mirror_raw'
      AND table_name IN ('usuarc','usuar1','ciaarc','opuarc01','oidar2','opiar2','txdgac','opsarc','opcar5','opcar6')
),
required_wm AS (
    SELECT COUNT(*)::int AS cnt
    FROM (VALUES
      ('USUARC'),('USUAR1'),('CIAARC'),('OPUARC01'),('OIDAR2'),
      ('OPIAR2'),('TXDGAC'),('OPSARC'),('OPCAR5'),('OPCAR6')
    ) v(t)
),
ok_wm AS (
    SELECT COUNT(*)::int AS cnt
    FROM sync.watermark
    WHERE table_name IN ('USUARC','USUAR1','CIAARC','OPUARC01','OIDAR2','OPIAR2','TXDGAC','OPSARC','OPCAR5','OPCAR6')
      AND status = 'OK'
)
SELECT
    rt.cnt AS required_tables,
    et.cnt AS existing_tables,
    rw.cnt AS required_watermarks,
    ow.cnt AS ok_watermarks,
    CASE
      WHEN et.cnt = rt.cnt AND ow.cnt = rw.cnt THEN 'PASS'
      ELSE 'FAIL'
    END AS acceptance_gate
FROM required_tables rt
CROSS JOIN existing_tables et
CROSS JOIN required_wm rw
CROSS JOIN ok_wm ow;

\echo '=== FIN 006 ACCEPTANCE GATE ==='
