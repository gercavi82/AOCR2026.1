SELECT 'fr3' AS tabla, COUNT(*) AS registros FROM public.fr3
UNION ALL SELECT 'fr3_pg', COUNT(*) FROM public.fr3_pg
UNION ALL SELECT 'usuario_as400', COUNT(*) FROM public.usuario_as400
UNION ALL SELECT 'usuario_as400_adicional', COUNT(*) FROM public.usuario_as400_adicional;
