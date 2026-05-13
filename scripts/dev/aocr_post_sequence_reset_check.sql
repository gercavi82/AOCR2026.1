SELECT 'aocr_or_numero_seq' AS secuencia, last_value::text AS last_value, is_called::text AS is_called FROM public.aocr_or_numero_seq
UNION ALL SELECT 'aocr_tbauditoria_codigo_auditoria_seq', last_value::text, is_called::text FROM public.aocr_tbauditoria_codigo_auditoria_seq
UNION ALL SELECT 'aocr_tbinforme_inspeccion_codigo_informe_seq', last_value::text, is_called::text FROM public.aocr_tbinforme_inspeccion_codigo_informe_seq
UNION ALL SELECT 'aocr_tbinspeccion_codigo_inspeccion_seq', last_value::text, is_called::text FROM public.aocr_tbinspeccion_codigo_inspeccion_seq
UNION ALL SELECT 'aocr_tbpago_codigo_pago_seq', last_value::text, is_called::text FROM public.aocr_tbpago_codigo_pago_seq
UNION ALL SELECT 'aocr_tbsolicitud_codigo_solicitud_seq', last_value::text, is_called::text FROM public.aocr_tbsolicitud_codigo_solicitud_seq
UNION ALL SELECT 'email_queue_id_seq', last_value::text, is_called::text FROM public.email_queue_id_seq
ORDER BY secuencia;
