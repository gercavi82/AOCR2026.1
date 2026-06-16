-- Inventario controlado previo a limpieza AOCR.
-- Solo lectura. No elimina ni modifica datos.

SELECT
    current_database() AS base,
    current_user AS usuario,
    now() AS fecha_ejecucion;

SELECT
    table_schema,
    table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_type = 'BASE TABLE'
ORDER BY table_name;

SELECT
    schemaname,
    relname,
    n_live_tup
FROM pg_stat_user_tables
WHERE schemaname = 'public'
ORDER BY relname;

SELECT
    tc.table_name AS tabla_hija,
    kcu.column_name AS columna_hija,
    ccu.table_name AS tabla_padre,
    ccu.column_name AS columna_padre,
    tc.constraint_name
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
  ON tc.constraint_name = kcu.constraint_name
 AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
  ON ccu.constraint_name = tc.constraint_name
 AND ccu.table_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema = 'public'
ORDER BY tabla_padre, tabla_hija, columna_hija;

SELECT
    sequence_schema,
    sequence_name
FROM information_schema.sequences
WHERE sequence_schema = 'public'
ORDER BY sequence_name;
