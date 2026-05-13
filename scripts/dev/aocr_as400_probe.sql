SELECT table_name, count(*)::text AS registros
FROM (
    SELECT 'fr3' AS table_name, COUNT(*) AS count FROM public.fr3
    UNION ALL SELECT 'fr3_pg', COUNT(*) FROM public.fr3_pg
    UNION ALL SELECT 'usuario_as400', COUNT(*) FROM public.usuario_as400
    UNION ALL SELECT 'usuario_as400_adicional', COUNT(*) FROM public.usuario_as400_adicional
) s
ORDER BY table_name;

SELECT * FROM public.usuario_as400;
SELECT * FROM public.usuario_as400_adicional;
