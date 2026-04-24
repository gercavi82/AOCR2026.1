-- Diagnostico CIAARC: identificar columna de identificacion en AS400 y validar espejo PostgreSQL.
-- Uso:
-- 1) Ejecutar bloque A en AS400/DB2 (DGACDAT) para detectar columnas candidatas.
-- 2) Con el nombre real de columna, actualizar As400MirrorSyncDefinitions + 003_create_mirror_raw_tables.sql.
-- 3) Ejecutar bloque B en PostgreSQL para validar replicacion.

/*
========================
A) DB2 / AS400 (DGACDAT)
========================

-- Columnas de CIAARC que suenan a identificacion tributaria/cedula/ruc.
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    LENGTH,
    NUMERIC_SCALE
FROM QSYS2.SYSCOLUMNS
WHERE TABLE_SCHEMA = 'DGACDAT'
  AND TABLE_NAME = 'CIAARC'
  AND (
      UPPER(COLUMN_NAME) LIKE '%RUC%'
      OR UPPER(COLUMN_NAME) LIKE '%CED%'
      OR UPPER(COLUMN_NAME) LIKE '%IDE%'
      OR UPPER(COLUMN_NAME) LIKE '%TRI%'
      OR UPPER(COLUMN_NAME) LIKE '%NIT%'
  )
ORDER BY COLUMN_NAME;

-- Vista completa de columnas CIAARC (si no aparece nada en el filtro anterior).
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    LENGTH,
    NUMERIC_SCALE
FROM QSYS2.SYSCOLUMNS
WHERE TABLE_SCHEMA = 'DGACDAT'
  AND TABLE_NAME = 'CIAARC'
ORDER BY ORDINAL_POSITION;

-- Ejemplo para probar valor de la columna candidata (reemplazar COL_CANDIDATA).
-- SELECT CIACOD, CIANOM, COL_CANDIDATA
-- FROM DGACDAT.CIAARC
-- WHERE TRIM(COALESCE(CIAEST, '')) = 'AC'
-- FETCH FIRST 50 ROWS ONLY;

*/

/*
=======================
B) PostgreSQL (mirror)
=======================

-- Ver estructura actual de mirror_raw.ciaarc.
SELECT
    column_name,
    data_type
FROM information_schema.columns
WHERE table_schema = 'mirror_raw'
  AND table_name = 'ciaarc'
ORDER BY ordinal_position;

-- Conteo y muestra basica.
SELECT COUNT(*) AS total_ciaarc
FROM mirror_raw.ciaarc;

SELECT ciacod, cianom, ciaest, _mirror_synced_at
FROM mirror_raw.ciaarc
WHERE COALESCE(_is_deleted, false) = false
ORDER BY _mirror_synced_at DESC
LIMIT 20;

-- RUC en FR3 espejo (respaldo actualmente en uso para ordenes).
SELECT
    UPPER(TRIM(COALESCE(opcc08, ''))) AS codigo_oaci_cia,
    NULLIF(TRIM(COALESCE(opcno5, '')), '') AS nombre_cia,
    NULLIF(TRIM(COALESCE(opcru1, '')), '') AS ruc,
    _mirror_synced_at
FROM mirror_raw.opcar5
WHERE COALESCE(_is_deleted, false) = false
  AND NULLIF(TRIM(COALESCE(opcru1, '')), '') IS NOT NULL
ORDER BY _mirror_synced_at DESC
LIMIT 100;

*/
