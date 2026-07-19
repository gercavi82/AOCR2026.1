SELECT 'columnas_documento_final' AS control, COUNT(*)::bigint AS valor
FROM information_schema.columns
WHERE table_schema='public' AND table_name='aocr_tbdocumento_generado'
  AND column_name IN ('version_documento','vigente','completo','bloqueado','hash_pdf','ruta_pdf_firmado','tamanio_pdf_firmado','codigo_usuario_firma','rol_firma','fecha_firma','version_concurrencia');

SELECT 'duplicados_vigentes' AS control, COUNT(*)::bigint AS valor
FROM (
    SELECT codigo_solicitud,UPPER(tipo_documento)
    FROM public.aocr_tbdocumento_generado
    WHERE vigente=TRUE
    GROUP BY codigo_solicitud,UPPER(tipo_documento)
    HAVING COUNT(*)>1
) d;

SELECT 'destinatarios_dirdac_activos' AS control,COUNT(DISTINCT u.idusuario)::bigint AS valor
FROM public.usuario u
JOIN public.usuario_rol ur ON u.codigousuario::text=ur.codigousuario::text
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g') IN
      ('DIRDAC','DIRECCION','DIRECCION_JEFATURA_TECNICA','JEFATURA_TECNICA')
  AND COALESCE(ur.activo,TRUE)=TRUE AND COALESCE(r.activo,TRUE)=TRUE
  AND COALESCE(u.estadoactividad::text,'1')='1' AND NULLIF(TRIM(u.correo),'') IS NOT NULL;

SELECT 'destinatarios_dcav_activos' AS control,COUNT(DISTINCT u.idusuario)::bigint AS valor
FROM public.usuario u
JOIN public.usuario_rol ur ON u.codigousuario::text=ur.codigousuario::text
JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE regexp_replace(UPPER(TRIM(COALESCE(r.descripcion,''))),'[^A-Z0-9]+','_','g') IN
      ('DCAV','DIRECTOR_CERTIFICACIONES_DCAV','DIRECTORCERTIFICACIONESDCAV')
  AND COALESCE(ur.activo,TRUE)=TRUE AND COALESCE(r.activo,TRUE)=TRUE
  AND COALESCE(u.estadoactividad::text,'1')='1' AND NULLIF(TRIM(u.correo),'') IS NOT NULL;
