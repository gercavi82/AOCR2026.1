SELECT string_agg(table_name, E'\n' ORDER BY table_name) AS tablas
FROM information_schema.tables
WHERE table_schema = 'public';

SELECT string_agg(
    format('%s.%s -> %s.%s', tc.table_name, kcu.column_name, ccu.table_name, ccu.column_name),
    E'\n' ORDER BY tc.table_name, kcu.column_name, ccu.table_name, ccu.column_name) AS foreign_keys
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
   AND tc.table_schema = kcu.table_schema
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
   AND ccu.constraint_schema = tc.table_schema
WHERE tc.constraint_type = 'FOREIGN KEY'
  AND tc.table_schema = 'public';

SELECT string_agg(sequence_name, E'\n' ORDER BY sequence_name) AS secuencias
FROM information_schema.sequences
WHERE sequence_schema = 'public';
