-- VALIDACION POSTERIOR A LA LIMPIEZA EN UNA SOLA GRILLA
-- Diseñado para clientes como pgAdmin que muestran con mas claridad un solo resultset.
-- Ejecutar despues de aplicar COMMIT en scripts/db/aocr_operational_cleanup.sql.

SELECT categoria,
       tabla,
       registros,
       estado_esperado,
       estado
FROM (
    SELECT 'PRESERVAR' AS categoria,
           'usuario' AS tabla,
           COUNT(*) AS registros,
           '> 0' AS estado_esperado,
           CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END AS estado
    FROM public.usuario

    UNION ALL SELECT 'PRESERVAR', 'usuario_rol', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_rol
    UNION ALL SELECT 'PRESERVAR', 'usuario_as400', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_as400
    UNION ALL SELECT 'PRESERVAR', 'usuario_as400_adicional', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_as400_adicional
    UNION ALL SELECT 'PRESERVAR', 'usuario_backup_eliminados', COUNT(*), '>= 0', CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.usuario_backup_eliminados
    UNION ALL SELECT 'PRESERVAR', 'aocr_usuario_interno_rt', COUNT(*), '>= 0', CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_usuario_interno_rt
    UNION ALL SELECT 'PRESERVAR', 'aocr_usuario_compania_rt', COUNT(*), '>= 0', CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_usuario_compania_rt
    UNION ALL SELECT 'PRESERVAR', 'rol', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.rol
    UNION ALL SELECT 'PRESERVAR', 'permisos', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.permisos
    UNION ALL SELECT 'PRESERVAR', 'seguridad_permiso', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.seguridad_permiso
    UNION ALL SELECT 'PRESERVAR', 'seguridad_rol_permiso', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.seguridad_rol_permiso
    UNION ALL SELECT 'PRESERVAR', 'menu', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.menu
    UNION ALL SELECT 'PRESERVAR', 'submenu', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.submenu
    UNION ALL SELECT 'PRESERVAR', 'aocr_tbparametro', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbparametro
    UNION ALL SELECT 'PRESERVAR', 'aocr_tbsesiones', COUNT(*), '>= 0', CASE WHEN COUNT(*) >= 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbsesiones
    UNION ALL SELECT 'PRESERVAR', 'parametros', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.parametros
    UNION ALL SELECT 'PRESERVAR', 'aocr_or_concepto', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_or_concepto
    UNION ALL SELECT 'PRESERVAR', 'conceptos', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.conceptos
    UNION ALL SELECT 'PRESERVAR', 'contribuyentes', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.contribuyentes
    UNION ALL SELECT 'PRESERVAR', 'aocr_tbchecklist_item', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbchecklist_item
    UNION ALL SELECT 'PRESERVAR', 'sync_state', COUNT(*), '> 0', CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'ALERTA' END FROM public.sync_state

    UNION ALL SELECT 'LIMPIAR', 'aocr_asignacion_rt', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_asignacion_rt
    UNION ALL SELECT 'LIMPIAR', 'aocr_audit_trail', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_audit_trail
    UNION ALL SELECT 'LIMPIAR', 'aocr_idempotency_key', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_idempotency_key
    UNION ALL SELECT 'LIMPIAR', 'aocr_or_orden', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_or_orden
    UNION ALL SELECT 'LIMPIAR', 'aocr_or_orden_detalle', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_or_orden_detalle
    UNION ALL SELECT 'LIMPIAR', 'aocr_orden_recaudacion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_orden_recaudacion
    UNION ALL SELECT 'LIMPIAR', 'aocr_sync_log', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_sync_log
    UNION ALL SELECT 'LIMPIAR', 'aocr_tb_factura_pago', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tb_factura_pago
    UNION ALL SELECT 'LIMPIAR', 'aocr_tb_sync_log', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tb_sync_log
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbaeronave', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbaeronave
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbaeronave_solicitud', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbaeronave_solicitud
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbauditoria', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbauditoria
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbcertificado', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbcertificado
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbchecklist', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbchecklist
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbchecklist_solicitud', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbchecklist_solicitud
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbdocumento', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbdocumento
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbdocumento_inspeccion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbdocumento_inspeccion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbdocumento_subsanacion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbdocumento_subsanacion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbfirma_documento', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbfirma_documento
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbfirma_posicion_documento', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbfirma_posicion_documento
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbhallazgo', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhallazgo
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbhistorial_documental', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhistorial_documental
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbhistorial_estado', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhistorial_estado
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbhistorial_estado_inspeccion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbhistorial_estado_inspeccion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbinforme', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbinforme
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbinforme_inspeccion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbinforme_inspeccion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbinspeccion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbinspeccion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tblog', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tblog
    UNION ALL SELECT 'LIMPIAR', 'aocr_tblv_operacional_eae', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tblv_operacional_eae
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbnotificacion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbnotificacion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbobservacion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbobservacion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbpago', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbpago
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbrevision_documental', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbrevision_documental
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbsolicitud', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbsolicitud
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbsubsanacion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbsubsanacion
    UNION ALL SELECT 'LIMPIAR', 'aocr_tbviatico', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.aocr_tbviatico
    UNION ALL SELECT 'LIMPIAR', 'detalles_orden', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.detalles_orden
    UNION ALL SELECT 'LIMPIAR', 'email_attachment', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.email_attachment
    UNION ALL SELECT 'LIMPIAR', 'email_queue', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.email_queue
    UNION ALL SELECT 'LIMPIAR', 'fr3', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3
    UNION ALL SELECT 'LIMPIAR', 'fr3_detalle', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3_detalle
    UNION ALL SELECT 'LIMPIAR', 'fr3_detalle_pg', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3_detalle_pg
    UNION ALL SELECT 'LIMPIAR', 'fr3_pg', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.fr3_pg
    UNION ALL SELECT 'LIMPIAR', 'historial_estados_orden', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.historial_estados_orden
    UNION ALL SELECT 'LIMPIAR', 'ordenes_recaudacion', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.ordenes_recaudacion
    UNION ALL SELECT 'LIMPIAR', 'pagos', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.pagos
    UNION ALL SELECT 'LIMPIAR', 'sync_log', COUNT(*), '= 0', CASE WHEN COUNT(*) = 0 THEN 'OK' ELSE 'ALERTA' END FROM public.sync_log

    UNION ALL SELECT 'REVISAR', 'aocr_declaracion_historial', COUNT(*), 'MANUAL', 'REVISAR' FROM public.aocr_declaracion_historial
    UNION ALL SELECT 'REVISAR', 'aocr_declaracion_tmp', COUNT(*), 'MANUAL', 'REVISAR' FROM public.aocr_declaracion_tmp
    UNION ALL SELECT 'REVISAR', 'aocr_usuario_transferencia', COUNT(*), 'MANUAL', 'REVISAR' FROM public.aocr_usuario_transferencia
    UNION ALL SELECT 'REVISAR', 'aocr_usuario_transferencia_detalle', COUNT(*), 'MANUAL', 'REVISAR' FROM public.aocr_usuario_transferencia_detalle
    UNION ALL SELECT 'REVISAR', 'auditoria_seguridad', COUNT(*), 'MANUAL', 'REVISAR' FROM public.auditoria_seguridad
) AS verify_all
ORDER BY CASE categoria WHEN 'PRESERVAR' THEN 1 WHEN 'LIMPIAR' THEN 2 ELSE 3 END,
         tabla;