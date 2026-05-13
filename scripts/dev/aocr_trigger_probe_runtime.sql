SELECT 'SEQ' AS kind,
       'aocr_tbauditoria_codigo_auditoria_seq' AS object_name,
       last_value::text AS value1,
       is_called::text AS value2,
       NULL::text AS value3
FROM public.aocr_tbauditoria_codigo_auditoria_seq
UNION ALL
SELECT 'MAX',
       'aocr_tbauditoria.codigo_auditoria',
       COALESCE(MAX(codigo_auditoria), 0)::text,
       NULL::text,
       NULL::text
FROM public.aocr_tbauditoria;

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
  AND p.proname ILIKE '%auditoria%'
ORDER BY c.relname, t.tgname;

SELECT p.proname,
       pg_get_functiondef(p.oid) AS function_def
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public'
  AND p.proname = 'fn_aocr_auditoria';
