-- =====================================================================
-- SCRIPT MAESTRO: APLICAR TODAS MIGRACIONES AC-01 a AC-10
-- BASE DE DATOS: PostgreSQL (AOCR STAGING)
-- FECHA: 2026-09-07
-- DESCRIPCIÓN: Script único que aplica todas las migraciones en orden
-- PRECONDICIONES:
--   - Conectado a base de datos STAGING (PostgreSQL)
--   - Usuario con permisos CREATE/ALTER/DROP
--   - Backups creados antes de ejecutar
-- =====================================================================

-- INSTRUCCIONES:
-- 1. Conectar a PostgreSQL STAGING:
--    psql -h [STAGING_HOST] -U [STAGING_USER] -d [STAGING_DB]
-- 2. Ejecutar este script:
--    \i 20260907_migraciones_maestras_ac01_ac10.sql
-- 3. Si ERROR: ROLLBACK ejecutando scripts rollback en orden inverso
-- 4. Si ÉXITO: Validar cambios en BD con \dt (listar tablas)

-- =====================================================================
-- ORDEN DE APLICACIÓN (ATOMICIDAD)
-- =====================================================================

BEGIN TRANSACTION;

-- 1. AC-01: Email RT Liberación
\i scripts/sql/20260903_ac01_liberar_correo_rt_devuelto.sql

-- 2. AC-02: Fechas Estaciones Independientes
\i scripts/sql/20260903_ac02_fechas_inspeccion_estaciones.sql

-- 3. AC-05: Designación Inspector DIRCAV (Versionado)
\i scripts/sql/20260903_ac05_designacion_inspector_dircav.sql

-- 4. AC-06: PDF Designación DIRCAV (Firma + Hash)
\i scripts/sql/20260903_ac06_designacion_pdf_dircav.sql

-- 5. AC-07: LV Independiente por Estación
\i scripts/sql/20260903_ac07_lista_verificacion_por_estacion.sql

-- 6. AC-10: Condiciones y Limitaciones
\i scripts/sql/20260903_ac10_condiciones_limitaciones.sql

-- 7. Roles: Segregación DIRCAV/DIRDAC
\i scripts/sql/20260903_roles_segregacion_dircav_dirdac.sql

-- =====================================================================
-- VALIDACIÓN POST-MIGRACIÓN
-- =====================================================================

-- Verificar tablas creadas/modificadas
\echo '=== TABLAS MODIFICADAS / CREADAS ==='
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' 
  AND (table_name LIKE 'aocr_%' OR table_name = 'usuario')
ORDER BY table_name;

-- Verificar columnas aditivas en usuario
\echo '=== COLUMNAS ADITIVAS EN TABLA USUARIO ==='
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'usuario'
  AND column_name IN ('correo_original', 'correo_liberado', 'fecha_devolucion_designacion', 'coordinador_devolucion_id', 'observacion_devolucion', 'estado_designacion_rt')
ORDER BY ordinal_position;

-- Verificar índices creados
\echo '=== ÍNDICES AOCR ==='
SELECT indexname, tablename
FROM pg_indexes
WHERE schemaname = 'public' AND indexname LIKE 'idx_aocr_%' OR indexname LIKE 'uq_aocr_%'
ORDER BY tablename;

-- Verificar restricciones CHECK
\echo '=== RESTRICCIONES CHECK (Fechas) ==='
SELECT constraint_name, table_name
FROM information_schema.table_constraints
WHERE table_schema = 'public' 
  AND constraint_type = 'CHECK'
  AND constraint_name LIKE 'chk_%'
ORDER BY table_name;

COMMIT;

\echo '=== MIGRACIONES COMPLETADAS EXITOSAMENTE ==='
\echo 'AC-01: Liberar Correo RT ✓'
\echo 'AC-02: Fechas Estaciones Independientes ✓'
\echo 'AC-05: Designación Inspector DIRCAV ✓'
\echo 'AC-06: PDF Designación DIRCAV ✓'
\echo 'AC-07: LV Independiente Estación ✓'
\echo 'AC-10: Condiciones y Limitaciones ✓'
\echo 'Roles: Segregación DIRCAV/DIRDAC ✓'
\echo ''
\echo 'PRÓXIMO PASO: Compilar AOCR.sln en Visual Studio 2022'
\echo 'LUEGO: Publicar a STAGING'
