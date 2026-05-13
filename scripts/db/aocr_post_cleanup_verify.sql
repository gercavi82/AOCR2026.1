-- VALIDACION POSTERIOR A LA LIMPIEZA
-- Ejecutar despues de aplicar COMMIT en scripts/db/aocr_operational_cleanup.sql.
-- Nota: algunos clientes SQL como pgAdmin suelen dejar visible solo el ultimo SELECT.
-- Si desea una sola grilla consolidada, use scripts/db/aocr_post_cleanup_verify_pgadmin.sql.


-- 1. TABLAS QUE DEBEN CONSERVAR DATOS O ESTRUCTURA BASE
SELECT *
FROM (
    SELECT 'usuario' AS tabla, COUNT(*) AS registros, CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END AS estado FROM public.usuario
    UNION ALL SELECT 'usuario_rol', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_rol
    UNION ALL SELECT 'usuario_as400', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_as400
    UNION ALL SELECT 'usuario_as400_adicional', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_as400_adicional
    UNION ALL SELECT 'usuario_backup_eliminados', COUNT(*), CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_backup_eliminados
    UNION ALL SELECT 'aocr_usuario_interno_rt', COUNT(*), CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_usuario_interno_rt
    UNION ALL SELECT 'aocr_usuario_compania_rt', COUNT(*), CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_usuario_compania_rt
    UNION ALL SELECT 'rol', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.rol
    UNION ALL SELECT 'permisos', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.permisos
    UNION ALL SELECT 'seguridad_permiso', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.seguridad_permiso
    UNION ALL SELECT 'seguridad_rol_permiso', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.seguridad_rol_permiso
    UNION ALL SELECT 'menu', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.menu
    UNION ALL SELECT 'submenu', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.submenu
    UNION ALL SELECT 'aocr_tbparametro', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbparametro
    UNION ALL SELECT 'aocr_tbsesiones', COUNT(*), CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbsesiones
    UNION ALL SELECT 'parametros', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.parametros
    UNION ALL SELECT 'aocr_or_concepto', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_or_concepto
    UNION ALL SELECT 'conceptos', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.conceptos
    UNION ALL SELECT 'contribuyentes', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.contribuyentes
    UNION ALL SELECT 'aocr_tbchecklist_item', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbchecklist_item
    UNION ALL SELECT 'sync_state', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.sync_state
) AS preserve_check
ORDER BY tabla;

-- 2. TABLAS OPERATIVAS QUE DEBEN QUEDAR EN CERO
SELECT *
FROM (
    SELECT 'aocr_asignacion_rt' AS tabla, COUNT(*) AS registros, CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END AS estado FROM public.aocr_asignacion_rt
    UNION ALL SELECT 'aocr_audit_trail', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_audit_trail
    UNION ALL SELECT 'aocr_idempotency_key', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_idempotency_key
    UNION ALL SELECT 'aocr_or_orden', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_or_orden
    UNION ALL SELECT 'aocr_or_orden_detalle', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_or_orden_detalle
    UNION ALL SELECT 'aocr_orden_recaudacion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_orden_recaudacion
    UNION ALL SELECT 'aocr_sync_log', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_sync_log
    UNION ALL SELECT 'aocr_tb_factura_pago', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tb_factura_pago
    UNION ALL SELECT 'aocr_tb_sync_log', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tb_sync_log
    UNION ALL SELECT 'aocr_tbaeronave', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbaeronave
    UNION ALL SELECT 'aocr_tbaeronave_solicitud', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbaeronave_solicitud
    UNION ALL SELECT 'aocr_tbauditoria', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbauditoria
    UNION ALL SELECT 'aocr_tbcertificado', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbcertificado
    UNION ALL SELECT 'aocr_tbchecklist', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbchecklist
    UNION ALL SELECT 'aocr_tbchecklist_solicitud', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbchecklist_solicitud
    UNION ALL SELECT 'aocr_tbdocumento', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbdocumento
    UNION ALL SELECT 'aocr_tbdocumento_inspeccion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbdocumento_inspeccion
    UNION ALL SELECT 'aocr_tbdocumento_subsanacion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbdocumento_subsanacion
    UNION ALL SELECT 'aocr_tbfirma_documento', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbfirma_documento
    UNION ALL SELECT 'aocr_tbfirma_posicion_documento', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbfirma_posicion_documento
    UNION ALL SELECT 'aocr_tbhallazgo', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhallazgo
    UNION ALL SELECT 'aocr_tbhistorial_documental', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhistorial_documental
    UNION ALL SELECT 'aocr_tbhistorial_estado', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhistorial_estado
    UNION ALL SELECT 'aocr_tbhistorial_estado_inspeccion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhistorial_estado_inspeccion
    UNION ALL SELECT 'aocr_tbinforme', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbinforme
    UNION ALL SELECT 'aocr_tbinforme_inspeccion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbinforme_inspeccion
    UNION ALL SELECT 'aocr_tbinspeccion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbinspeccion
    UNION ALL SELECT 'aocr_tblog', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tblog
    UNION ALL SELECT 'aocr_tblv_operacional_eae', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tblv_operacional_eae
    UNION ALL SELECT 'aocr_tbnotificacion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbnotificacion
    UNION ALL SELECT 'aocr_tbobservacion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbobservacion
    UNION ALL SELECT 'aocr_tbpago', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbpago
    UNION ALL SELECT 'aocr_tbrevision_documental', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbrevision_documental
    UNION ALL SELECT 'aocr_tbsolicitud', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbsolicitud
    UNION ALL SELECT 'aocr_tbsubsanacion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbsubsanacion
    UNION ALL SELECT 'aocr_tbviatico', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbviatico
    UNION ALL SELECT 'detalles_orden', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.detalles_orden
    UNION ALL SELECT 'email_attachment', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.email_attachment
    UNION ALL SELECT 'email_queue', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.email_queue
    UNION ALL SELECT 'fr3', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3
    UNION ALL SELECT 'fr3_detalle', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3_detalle
    UNION ALL SELECT 'fr3_detalle_pg', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3_detalle_pg
    UNION ALL SELECT 'fr3_pg', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3_pg
    UNION ALL SELECT 'historial_estados_orden', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.historial_estados_orden
    UNION ALL SELECT 'ordenes_recaudacion', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.ordenes_recaudacion
    UNION ALL SELECT 'pagos', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.pagos
    UNION ALL SELECT 'sync_log', COUNT(*), CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.sync_log
) AS cleanup_check
ORDER BY tabla;

-- 3. TABLAS EN REVISION MANUAL (SOLO INFORME)
SELECT *
FROM (
    SELECT 'aocr_declaracion_historial' AS tabla, COUNT(*) AS registros FROM public.aocr_declaracion_historial
    UNION ALL SELECT 'aocr_declaracion_tmp', COUNT(*) FROM public.aocr_declaracion_tmp
    UNION ALL SELECT 'aocr_usuario_transferencia', COUNT(*) FROM public.aocr_usuario_transferencia
    UNION ALL SELECT 'aocr_usuario_transferencia_detalle', COUNT(*) FROM public.aocr_usuario_transferencia_detalle
    UNION ALL SELECT 'auditoria_seguridad', COUNT(*) FROM public.auditoria_seguridad
) AS review_check
ORDER BY tabla;