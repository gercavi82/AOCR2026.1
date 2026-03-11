-- AOCR - Diagnostico de esquema (solo lectura)
-- Fecha: 2026-03-10
-- Objetivo:
-- 1) Comparar columnas reales vs codigo en tablas criticas AOCR.
-- 2) Revisar constraints de estado en documento/solicitud/inspeccion.
-- 3) Verificar presencia de tablas de historial.

-- =========================================================
-- TABLAS CRITICAS: EXISTENCIA
-- =========================================================
SELECT 'aocr_tbsolicitud' AS tabla, to_regclass('public.aocr_tbsolicitud') IS NOT NULL AS existe
UNION ALL
SELECT 'aocr_tbaeronave_solicitud', to_regclass('public.aocr_tbaeronave_solicitud') IS NOT NULL
UNION ALL
SELECT 'aocr_tbdocumento', to_regclass('public.aocr_tbdocumento') IS NOT NULL
UNION ALL
SELECT 'aocr_tbinspeccion', to_regclass('public.aocr_tbinspeccion') IS NOT NULL
UNION ALL
SELECT 'aocr_tbhistorialestado', to_regclass('public.aocr_tbhistorialestado') IS NOT NULL
UNION ALL
SELECT 'aocr_tbhistorial_estado', to_regclass('public.aocr_tbhistorial_estado') IS NOT NULL
ORDER BY tabla;

-- =========================================================
-- COLUMNAS: SOLICITUD / AERONAVE / DOCUMENTO / INSPECCION
-- =========================================================
SELECT
    table_name,
    ordinal_position,
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN (
      'aocr_tbsolicitud',
      'aocr_tbaeronave_solicitud',
      'aocr_tbdocumento',
      'aocr_tbinspeccion',
      'aocr_tbhistorialestado',
      'aocr_tbhistorial_estado'
  )
ORDER BY table_name, ordinal_position;

-- =========================================================
-- CONSTRAINTS CHECK Y FK DE TABLAS CRITICAS
-- =========================================================
SELECT
    t.relname AS table_name,
    c.conname AS constraint_name,
    c.contype AS constraint_type,
    pg_get_constraintdef(c.oid) AS definition
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
WHERE n.nspname = 'public'
  AND t.relname IN (
      'aocr_tbsolicitud',
      'aocr_tbaeronave_solicitud',
      'aocr_tbdocumento',
      'aocr_tbinspeccion',
      'aocr_tbhistorialestado',
      'aocr_tbhistorial_estado'
  )
ORDER BY t.relname, c.contype, c.conname;

-- =========================================================
-- CHECK puntual de estado de documento
-- =========================================================
SELECT
    c.conname,
    pg_get_constraintdef(c.oid) AS definition
FROM pg_constraint c
JOIN pg_class t ON t.oid = c.conrelid
JOIN pg_namespace n ON n.oid = t.relnamespace
WHERE n.nspname = 'public'
  AND t.relname = 'aocr_tbdocumento'
  AND c.contype = 'c'
ORDER BY CASE WHEN c.conname = 'chk_estado_documento' THEN 0 ELSE 1 END, c.conname;

-- =========================================================
-- INDICES de soporte en tablas criticas
-- =========================================================
SELECT
    schemaname,
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
  AND tablename IN (
      'aocr_tbsolicitud',
      'aocr_tbaeronave_solicitud',
      'aocr_tbdocumento',
      'aocr_tbinspeccion',
      'aocr_tbhistorialestado',
      'aocr_tbhistorial_estado'
  )
ORDER BY tablename, indexname;

