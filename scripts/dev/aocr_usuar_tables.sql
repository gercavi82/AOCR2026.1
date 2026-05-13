SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'mirror_raw'
  AND table_name IN ('usuarc','usuar1')
ORDER BY table_name;
