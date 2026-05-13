SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name ILIKE '%inspector%'
ORDER BY table_name;
