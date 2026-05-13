SELECT 'aocr_tbsolicitud' AS tabla, COUNT(*) AS registros FROM public.aocr_tbsolicitud
UNION ALL SELECT 'aocr_tbinspeccion', COUNT(*) FROM public.aocr_tbinspeccion
UNION ALL SELECT 'aocr_tbinforme_inspeccion', COUNT(*) FROM public.aocr_tbinforme_inspeccion
UNION ALL SELECT 'aocr_or_orden', COUNT(*) FROM public.aocr_or_orden
UNION ALL SELECT 'aocr_or_orden_detalle', COUNT(*) FROM public.aocr_or_orden_detalle
UNION ALL SELECT 'aocr_tb_factura_pago', COUNT(*) FROM public.aocr_tb_factura_pago
UNION ALL SELECT 'email_queue', COUNT(*) FROM public.email_queue
UNION ALL SELECT 'email_attachment', COUNT(*) FROM public.email_attachment
UNION ALL SELECT 'aocr_tbpago', COUNT(*) FROM public.aocr_tbpago
UNION ALL SELECT 'aocr_tbauditoria', COUNT(*) FROM public.aocr_tbauditoria
ORDER BY tabla;
