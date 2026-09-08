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

-- Roles actuales y permisos efectivos (catalogo y asignacion activos).
SELECT r.codigorol,r.descripcion,r.activo,
       COUNT(p.id_permiso) FILTER (WHERE rp.activo AND p.activo) permisos_activos,
       STRING_AGG(p.codigo, ', ' ORDER BY p.codigo)
           FILTER (WHERE rp.activo AND p.activo AND p.modulo='ENTREGA_FINAL') entrega_final
FROM public.rol r
LEFT JOIN public.seguridad_rol_permiso rp ON rp.codigorol=r.codigorol
LEFT JOIN public.seguridad_permiso p ON p.id_permiso=rp.id_permiso
GROUP BY r.codigorol,r.descripcion,r.activo ORDER BY r.codigorol;

-- El selector muestra roles asignados al usuario, no todo el catalogo.
SELECT u.idusuario,u.codigousuario,r.codigorol,r.descripcion,
       ur.activo asignacion_activa,r.activo rol_activo
FROM public.usuario u
JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE ur.activo AND r.activo
ORDER BY u.idusuario,r.codigorol;

-- Direccion es ambiguo: requiere asignacion explicita a DIRCAV o DIRDAC.
SELECT u.idusuario,u.codigousuario,r.descripcion rol_pendiente_de_migrar
FROM public.usuario u
JOIN public.usuario_rol ur ON ur.codigousuario=u.codigousuario
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE ur.activo AND r.activo AND UPPER(TRIM(r.descripcion)) IN ('DIRECCION','DIRECCIÓN');
