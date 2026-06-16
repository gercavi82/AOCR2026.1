-- Validacion posterior a limpieza transaccional AOCR.
-- Solo lectura.

SELECT
    current_database() AS base,
    current_user AS usuario,
    now() AS fecha_validacion;

-- Tablas protegidas: deben conservar datos/configuracion.
SELECT 'usuario' AS tabla, COUNT(*) AS total FROM public.usuario UNION ALL
SELECT 'rol', COUNT(*) FROM public.rol UNION ALL
SELECT 'usuario_rol', COUNT(*) FROM public.usuario_rol UNION ALL
SELECT 'seguridad_permiso', COUNT(*) FROM public.seguridad_permiso UNION ALL
SELECT 'seguridad_rol_permiso', COUNT(*) FROM public.seguridad_rol_permiso UNION ALL
SELECT 'permisos', COUNT(*) FROM public.permisos UNION ALL
SELECT 'menu', COUNT(*) FROM public.menu UNION ALL
SELECT 'submenu', COUNT(*) FROM public.submenu UNION ALL
SELECT 'aocr_or_concepto', COUNT(*) FROM public.aocr_or_concepto UNION ALL
SELECT 'aocr_tbcorreo_institucional', COUNT(*) FROM public.aocr_tbcorreo_institucional UNION ALL
SELECT 'aocr_tbinspectores', COUNT(*) FROM public.aocr_tbinspectores UNION ALL
SELECT 'aocr_usuario_compania_rt', COUNT(*) FROM public.aocr_usuario_compania_rt UNION ALL
SELECT 'aocr_usuario_interno_rt', COUNT(*) FROM public.aocr_usuario_interno_rt
ORDER BY tabla;

-- Tablas transaccionales principales: esperado cero tras COMMIT definitivo.
SELECT 'aocr_tbsolicitud' AS tabla, COUNT(*) AS total FROM public.aocr_tbsolicitud UNION ALL
SELECT 'aocr_tbdocumento', COUNT(*) FROM public.aocr_tbdocumento UNION ALL
SELECT 'aocr_tbrevision_documental', COUNT(*) FROM public.aocr_tbrevision_documental UNION ALL
SELECT 'aocr_tbinspeccion', COUNT(*) FROM public.aocr_tbinspeccion UNION ALL
SELECT 'aocr_tblv_operacional_eae', COUNT(*) FROM public.aocr_tblv_operacional_eae UNION ALL
SELECT 'aocr_tbinforme_inspeccion', COUNT(*) FROM public.aocr_tbinforme_inspeccion UNION ALL
SELECT 'aocr_or_orden', COUNT(*) FROM public.aocr_or_orden UNION ALL
SELECT 'aocr_or_orden_detalle', COUNT(*) FROM public.aocr_or_orden_detalle UNION ALL
SELECT 'aocr_tbpago', COUNT(*) FROM public.aocr_tbpago UNION ALL
SELECT 'aocr_tb_factura_pago', COUNT(*) FROM public.aocr_tb_factura_pago UNION ALL
SELECT 'email_queue', COUNT(*) FROM public.email_queue UNION ALL
SELECT 'email_attachment', COUNT(*) FROM public.email_attachment UNION ALL
SELECT 'aocr_tbnotificacion', COUNT(*) FROM public.aocr_tbnotificacion UNION ALL
SELECT 'aocr_tbhistorial_documental', COUNT(*) FROM public.aocr_tbhistorial_documental UNION ALL
SELECT 'aocr_tbhistorial_estado', COUNT(*) FROM public.aocr_tbhistorial_estado UNION ALL
SELECT 'aocr_tbhistorial_estado_inspeccion', COUNT(*) FROM public.aocr_tbhistorial_estado_inspeccion
ORDER BY tabla;

-- FK declaradas: no debe retornar filas con huerfanos en las relaciones principales.
SELECT 'email_queue.solicitud_id -> aocr_tbsolicitud' AS relacion, COUNT(*) AS huerfanos
FROM public.email_queue q
LEFT JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud = q.solicitud_id
WHERE q.solicitud_id IS NOT NULL AND s.codigo_solicitud IS NULL
UNION ALL
SELECT 'email_queue.orden_id -> aocr_or_orden', COUNT(*)
FROM public.email_queue q
LEFT JOIN public.aocr_or_orden o ON o.id = q.orden_id
WHERE q.orden_id IS NOT NULL AND o.id IS NULL
UNION ALL
SELECT 'aocr_tbdocumento.codigo_solicitud -> aocr_tbsolicitud', COUNT(*)
FROM public.aocr_tbdocumento d
LEFT JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud = d.codigo_solicitud
WHERE d.codigo_solicitud IS NOT NULL AND s.codigo_solicitud IS NULL
UNION ALL
SELECT 'aocr_tbinspeccion.codigo_solicitud -> aocr_tbsolicitud', COUNT(*)
FROM public.aocr_tbinspeccion i
LEFT JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud = i.codigo_solicitud
WHERE i.codigo_solicitud IS NOT NULL AND s.codigo_solicitud IS NULL
UNION ALL
SELECT 'aocr_tbpago.codigo_solicitud -> aocr_tbsolicitud', COUNT(*)
FROM public.aocr_tbpago p
LEFT JOIN public.aocr_tbsolicitud s ON s.codigo_solicitud = p.codigo_solicitud
WHERE p.codigo_solicitud IS NOT NULL AND s.codigo_solicitud IS NULL;
