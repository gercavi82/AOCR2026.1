SELECT 'aocr_tbsolicitud' AS tabla, COUNT(*) AS registros FROM public.aocr_tbsolicitud
UNION ALL SELECT 'aocr_tbinspeccion', COUNT(*) FROM public.aocr_tbinspeccion
UNION ALL SELECT 'aocr_tbinforme_inspeccion', COUNT(*) FROM public.aocr_tbinforme_inspeccion
UNION ALL SELECT 'aocr_or_orden', COUNT(*) FROM public.aocr_or_orden
UNION ALL SELECT 'email_queue', COUNT(*) FROM public.email_queue
UNION ALL SELECT 'usuario', COUNT(*) FROM public.usuario
UNION ALL SELECT 'usuario_rol', COUNT(*) FROM public.usuario_rol
UNION ALL SELECT 'rol', COUNT(*) FROM public.rol
UNION ALL SELECT 'aocr_declaracion_historial', COUNT(*) FROM public.aocr_declaracion_historial
UNION ALL SELECT 'aocr_usuario_transferencia', COUNT(*) FROM public.aocr_usuario_transferencia
UNION ALL SELECT 'auditoria_seguridad', COUNT(*) FROM public.auditoria_seguridad
ORDER BY tabla;
