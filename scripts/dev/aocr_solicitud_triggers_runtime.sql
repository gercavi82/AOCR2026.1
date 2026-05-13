SELECT t.tgname,
       c.relname AS table_name,
       p.proname AS function_name,
       pg_get_triggerdef(t.oid) AS trigger_def
FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_proc p ON p.oid = t.tgfoid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND NOT t.tgisinternal
  AND c.relname = 'aocr_tbsolicitud'
ORDER BY t.tgname;
