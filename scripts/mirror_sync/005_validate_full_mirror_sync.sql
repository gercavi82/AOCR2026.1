-- ==============================================================================
-- 005_validate_full_mirror_sync.sql
-- AOCR / AS400 Mirror Sync — Suite de Validación Completa (Fase 5)
-- Ejecutar DESPUÉS de: 001 → 002 → 003 → 003b → al menos 1 sync exitoso.
-- Seguro para re-ejecutar. Solo SELECT + comentarios. No modifica nada.
-- ==============================================================================

-- =============================================
-- SECCIÓN 1: Infraestructura (schemas/tablas)
-- =============================================
\echo '--- [1] Schemas creados ---'
SELECT schema_name, schema_owner
FROM information_schema.schemata
WHERE schema_name IN ('mirror_raw', 'mirror_clean', 'sync')
ORDER BY schema_name;
-- ESPERADO: 3 filas (mirror_clean, mirror_raw, sync)

\echo '--- [1b] Tablas sync ---'
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema IN ('mirror_raw', 'sync')
  AND table_type = 'BASE TABLE'
ORDER BY table_schema, table_name;
-- ESPERADO: mirror_raw (ciaarc, opcar5, opcar6, usuar1, usuarc) + sync (batch_log, rejections, tombstones, watermark)
--            + mirror_raw (opuarc01, oidar2) para lugar de emision

\echo '--- [1c] Vistas mirror_clean ---'
SELECT table_schema, table_name
FROM information_schema.views
WHERE table_schema = 'mirror_clean'
ORDER BY table_name;
-- ESPERADO: v_ciaarc_activa, v_fr3_cabecera, v_fr3_detalle, v_lugar_emision_ciudad, v_usuario_as400

-- =============================================
-- SECCIÓN 2: Estado de Watermarks
-- =============================================
\echo '--- [2] Watermarks ---'
SELECT
    table_name,
    status,
    last_success_ts,
    last_success_key,
    TO_CHAR(updated_at, 'YYYY-MM-DD HH24:MI:SS') AS updated_at,
    COALESCE(last_error, '') AS last_error
FROM sync.watermark
ORDER BY table_name;
-- ESPERADO: al menos USUARC, USUAR1, CIAARC, OPUARC01, OIDAR2, OPCAR5, OPCAR6 con status='OK' tras primer sync

-- =============================================
-- SECCIÓN 3: Conteos por tabla
-- =============================================
\echo '--- [3] Conteos espejo ---'
SELECT
    'mirror_raw.usuarc' AS tabla,
    COUNT(*) AS total,
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END) AS eliminados,
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL) AS sin_watermark
FROM mirror_raw.usuarc
UNION ALL
SELECT 'mirror_raw.usuar1', COUNT(*),
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END),
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL)
FROM mirror_raw.usuar1
UNION ALL
SELECT 'mirror_raw.ciaarc', COUNT(*),
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END),
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL)
FROM mirror_raw.ciaarc
UNION ALL
SELECT 'mirror_raw.opcar5', COUNT(*),
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END),
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL)
FROM mirror_raw.opcar5
UNION ALL
SELECT 'mirror_raw.opcar6', COUNT(*),
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END),
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL)
FROM mirror_raw.opcar6
UNION ALL
SELECT 'mirror_raw.opuarc01', COUNT(*),
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END),
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL)
FROM mirror_raw.opuarc01
UNION ALL
SELECT 'mirror_raw.oidar2', COUNT(*),
    SUM(CASE WHEN COALESCE(_is_deleted,false) THEN 1 ELSE 0 END),
    COUNT(*) FILTER (WHERE _source_updated_at IS NULL)
FROM mirror_raw.oidar2;
-- ESPERADO: total > 0, eliminados puede ser 0 o mayor, sin_watermark = 0 idealmente

-- =============================================
-- SECCIÓN 4: Idempotencia — Verificar no duplicados
-- =============================================
\echo '--- [4] Duplicados en USUARC (PK = usucod) ---'
SELECT usucod, COUNT(*) AS cnt
FROM mirror_raw.usuarc
GROUP BY usucod HAVING COUNT(*) > 1;
-- ESPERADO: 0 filas

\echo '--- [4b] Duplicados en OPCAR5 (PK = opcsec, opcaer, opcano) ---'
SELECT opcsec, opcaer, opcano, COUNT(*) AS cnt
FROM mirror_raw.opcar5
GROUP BY opcsec, opcaer, opcano HAVING COUNT(*) > 1;
-- ESPERADO: 0 filas

\echo '--- [4c] Duplicados en OPCAR6 (PK compuesto) ---'
SELECT opcse2, opcae1, opcan1, opcse1, COUNT(*) AS cnt
FROM mirror_raw.opcar6
GROUP BY opcse2, opcae1, opcan1, opcse1 HAVING COUNT(*) > 1;
-- ESPERADO: 0 filas

-- =============================================
-- SECCIÓN 5: Consistencia de datos
-- =============================================
\echo '--- [5a] Usuarios sin código ---'
SELECT COUNT(*) AS usuarios_sin_codigo
FROM mirror_raw.usuarc
WHERE COALESCE(TRIM(usucod), '') = '';
-- ESPERADO: 0

\echo '--- [5b] Distribución estado usuarios ---'
SELECT
    COALESCE(TRIM(usuest), '(null)') AS estado,
    COUNT(*) AS total
FROM mirror_raw.usuarc
WHERE COALESCE(_is_deleted, false) = false
GROUP BY COALESCE(TRIM(usuest), '(null)')
ORDER BY total DESC;
-- ESPERADO: AC (activos) y opcionalmente IA (inactivos)

\echo '--- [5c] USUAR1 huérfanos (sin padre en USUARC) ---'
SELECT COUNT(*) AS usuar1_sin_padre
FROM mirror_raw.usuar1 a
LEFT JOIN mirror_raw.usuarc u ON u.usucod = a.usuco8
WHERE u.usucod IS NULL
  AND COALESCE(a._is_deleted, false) = false;
-- ESPERADO: 0 o bajo. Si > 0, puede ser race condition de sync o datos históricos AS400.

\echo '--- [5d] FR3 sin RUC ni cliente ---'
SELECT COUNT(*) AS fr3_sin_datos_cliente
FROM mirror_raw.opcar5
WHERE COALESCE(TRIM(opcru1), '') = ''
  AND COALESCE(TRIM(opcno4), '') = ''
  AND COALESCE(_is_deleted, false) = false;
-- ESPERADO: 0 idealmente; si > 0 son registros AS400 con datos incompletos

\echo '--- [5e] FR3 gran total inconsistente ---'
SELECT COUNT(*) AS fr3_gran_total_cero
FROM mirror_raw.opcar5
WHERE COALESCE(opcgra, 0) = 0
  AND COALESCE(_is_deleted, false) = false;
-- Informativo: FR3 con monto=0 (pueden ser anulados o de prueba)

-- =============================================
-- SECCIÓN 6: Últimos lotes (éxitos y errores)
-- =============================================
\echo '--- [6] Últimos 20 lotes ---'
SELECT
    table_name,
    status,
    rows_read,
    rows_applied,
    rows_rejected,
    rows_deleted,
    latency_ms,
    TO_CHAR(started_at, 'YYYY-MM-DD HH24:MI:SS') AS started_at,
    COALESCE(error, '') AS error
FROM sync.batch_log
ORDER BY started_at DESC
LIMIT 20;

\echo '--- [6b] Rechazos recientes ---'
SELECT id, batch_id, table_name, error, TO_CHAR(created_at, 'YYYY-MM-DD HH24:MI:SS') AS at
FROM sync.rejections
ORDER BY created_at DESC
LIMIT 20;
-- ESPERADO: 0 filas o errores puntuales conocidos (valor nulo en campo NOT NULL de AS400, etc.)

-- =============================================
-- SECCIÓN 7: Test dry-run idempotencia (SELECT only)
-- =============================================
\echo '--- [7] Verificar columnas SNAP en opcar5 (003b parche) ---'
SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'mirror_raw'
  AND table_name = 'opcar5'
  AND column_name IN ('opcenv','opcder','opcde2','opcde3','opcde4',
                      'opcho8','opcfe5','opcho9','opcfe6','opcnr1',
                      'opcval','opcde5','opcde6','opcfe7','opcefe',
                      'opcvue','opccta','opccru','opcoi5','opcilu',
                      'opcper','opces1','opcc07','opcnu1','opcre8',
                      'opcre9','opcf04','opcf05','opcdi6','opcdi7')
ORDER BY column_name;
-- ESPERADO: 30 filas (todas las columnas del parche 003b)

\echo '--- [7b] Verificar columnas SNAP en opcar6 (003b parche) ---'
SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'mirror_raw'
  AND table_name = 'opcar6'
  AND column_name IN ('opcde9','opcimp','opcpor','opcpo1','opcva2',
                      'opcva3','opcva4','opcva5','opcubi')
ORDER BY column_name;
-- ESPERADO: 9 filas

-- =============================================
-- SECCIÓN 8: Coherencia vistas mirror_clean
-- =============================================
\echo '--- [8] mirror_clean.v_usuario_as400 ---'
SELECT COUNT(*) AS total_usuarios_clean
FROM mirror_clean.v_usuario_as400;

\echo '--- [8b] mirror_clean.v_ciaarc_activa ---'
SELECT COUNT(*) AS total_cias_activas
FROM mirror_clean.v_ciaarc_activa;

\echo '--- [8c] mirror_clean.v_lugar_emision_ciudad ---'
SELECT COUNT(*) AS total_lugar_emision_mirror
FROM mirror_clean.v_lugar_emision_ciudad;

\echo '--- [8d] mirror_clean.v_fr3_cabecera ---'
SELECT COUNT(*) AS total_fr3, SUM(gran_total) AS suma_gran_total
FROM mirror_clean.v_fr3_cabecera;

-- =============================================
-- SECCIÓN 9: Sumatorias FR3 por año y aeropuerto
-- =============================================
\echo '--- [9] FR3 sumatorio por año/aeropuerto ---'
SELECT
    aeropuerto,
    anio,
    COUNT(*) AS registros,
    SUM(gran_total) AS suma_gran_total,
    MIN(fecha_creacion) AS primera_fecha,
    MAX(fecha_creacion) AS ultima_fecha
FROM mirror_clean.v_fr3_cabecera
GROUP BY aeropuerto, anio
ORDER BY anio DESC, suma_gran_total DESC;

-- =============================================
-- SECCIÓN 10: Tombstones pendientes
-- =============================================
\echo '--- [10] Tombstones pendientes de aplicar ---'
SELECT table_name, COUNT(*) AS pendientes
FROM sync.tombstones
WHERE applied = false
GROUP BY table_name;
-- ESPERADO: 0 pendientes tras sync exitoso; si hay muchos, revisar estrategia de deletes.

\echo '=== FIN VALIDACION MIRROR SYNC ==='
