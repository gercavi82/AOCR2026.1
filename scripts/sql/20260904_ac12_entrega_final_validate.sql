SELECT to_regclass('public.aocr_entrega_final')::text entrega,
       to_regclass('public.aocr_entrega_documento')::text documentos,
       to_regclass('public.aocr_entrega_destinatario')::text destinatarios,
       to_regclass('public.aocr_entrega_intento')::text intentos;

SELECT column_name FROM information_schema.columns
WHERE table_schema='public' AND table_name='email_queue' AND column_name IN ('message_id','sent_at')
UNION ALL
SELECT column_name FROM information_schema.columns
WHERE table_schema='public' AND table_name='email_attachment' AND column_name='sha256';

SELECT codigo,activo FROM public.seguridad_permiso
WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR') ORDER BY codigo;

SELECT e.solicitud_id,e.version_aocr,e.version_cl,e.estado,COUNT(DISTINCT d.tipo_documento) documentos,
       COUNT(DISTINCT r.tipo_destinatario) destinatarios
FROM public.aocr_entrega_final e
LEFT JOIN public.aocr_entrega_documento d ON d.entrega_id=e.id
LEFT JOIN public.aocr_entrega_destinatario r ON r.entrega_id=e.id
GROUP BY e.id ORDER BY e.id DESC LIMIT 20;
