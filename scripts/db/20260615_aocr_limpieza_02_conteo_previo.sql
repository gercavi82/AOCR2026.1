-- Conteo exacto previo a limpieza transaccional AOCR.
-- Solo lectura.

SELECT 'email_attachment' AS tabla, COUNT(*) AS total FROM public.email_attachment UNION ALL
SELECT 'email_queue', COUNT(*) FROM public.email_queue UNION ALL
SELECT 'aocr_tbnotificacion', COUNT(*) FROM public.aocr_tbnotificacion UNION ALL
SELECT 'aocr_tbhistorial_documental', COUNT(*) FROM public.aocr_tbhistorial_documental UNION ALL
SELECT 'aocr_tbhistorial_estado_inspeccion', COUNT(*) FROM public.aocr_tbhistorial_estado_inspeccion UNION ALL
SELECT 'aocr_tbhistorial_estado', COUNT(*) FROM public.aocr_tbhistorial_estado UNION ALL
SELECT 'aocr_audit_trail', COUNT(*) FROM public.aocr_audit_trail UNION ALL
SELECT 'aocr_tbauditoria', COUNT(*) FROM public.aocr_tbauditoria UNION ALL
SELECT 'aocr_tblog', COUNT(*) FROM public.aocr_tblog UNION ALL
SELECT 'aocr_declaracion_historial', COUNT(*) FROM public.aocr_declaracion_historial UNION ALL
SELECT 'aocr_declaracion_tmp', COUNT(*) FROM public.aocr_declaracion_tmp UNION ALL
SELECT 'aocr_idempotency_key', COUNT(*) FROM public.aocr_idempotency_key UNION ALL
SELECT 'aocr_sync_log', COUNT(*) FROM public.aocr_sync_log UNION ALL
SELECT 'aocr_tb_sync_log', COUNT(*) FROM public.aocr_tb_sync_log UNION ALL
SELECT 'sync_log', COUNT(*) FROM public.sync_log UNION ALL
SELECT 'aocr_tbcorreo_institucional_historial', COUNT(*) FROM public.aocr_tbcorreo_institucional_historial UNION ALL
SELECT 'aocr_tbfirma_documento', COUNT(*) FROM public.aocr_tbfirma_documento UNION ALL
SELECT 'aocr_tbdocumento_subsanacion', COUNT(*) FROM public.aocr_tbdocumento_subsanacion UNION ALL
SELECT 'aocr_tbsubsanacion', COUNT(*) FROM public.aocr_tbsubsanacion UNION ALL
SELECT 'aocr_tbrevision_documental', COUNT(*) FROM public.aocr_tbrevision_documental UNION ALL
SELECT 'aocr_tbdocumento_inspeccion', COUNT(*) FROM public.aocr_tbdocumento_inspeccion UNION ALL
SELECT 'aocr_tbdocumento', COUNT(*) FROM public.aocr_tbdocumento UNION ALL
SELECT 'aocr_tbchecklist_solicitud', COUNT(*) FROM public.aocr_tbchecklist_solicitud UNION ALL
SELECT 'aocr_tbchecklist_item', COUNT(*) FROM public.aocr_tbchecklist_item UNION ALL
SELECT 'aocr_tbchecklist', COUNT(*) FROM public.aocr_tbchecklist UNION ALL
SELECT 'aocr_tbhallazgo', COUNT(*) FROM public.aocr_tbhallazgo UNION ALL
SELECT 'aocr_tbinforme_inspeccion', COUNT(*) FROM public.aocr_tbinforme_inspeccion UNION ALL
SELECT 'aocr_tbinforme', COUNT(*) FROM public.aocr_tbinforme UNION ALL
SELECT 'aocr_tblv_operacional_eae', COUNT(*) FROM public.aocr_tblv_operacional_eae UNION ALL
SELECT 'aocr_tbobservacion', COUNT(*) FROM public.aocr_tbobservacion UNION ALL
SELECT 'aocr_tbcertificado', COUNT(*) FROM public.aocr_tbcertificado UNION ALL
SELECT 'aocr_tbaeronave_solicitud', COUNT(*) FROM public.aocr_tbaeronave_solicitud UNION ALL
SELECT 'aocr_tbaeronave', COUNT(*) FROM public.aocr_tbaeronave UNION ALL
SELECT 'aocr_tb_factura_pago', COUNT(*) FROM public.aocr_tb_factura_pago UNION ALL
SELECT 'aocr_tbpago', COUNT(*) FROM public.aocr_tbpago UNION ALL
SELECT 'aocr_or_orden_detalle', COUNT(*) FROM public.aocr_or_orden_detalle UNION ALL
SELECT 'aocr_or_orden', COUNT(*) FROM public.aocr_or_orden UNION ALL
SELECT 'aocr_orden_recaudacion', COUNT(*) FROM public.aocr_orden_recaudacion UNION ALL
SELECT 'detalles_orden', COUNT(*) FROM public.detalles_orden UNION ALL
SELECT 'historial_estados_orden', COUNT(*) FROM public.historial_estados_orden UNION ALL
SELECT 'pagos', COUNT(*) FROM public.pagos UNION ALL
SELECT 'ordenes_recaudacion', COUNT(*) FROM public.ordenes_recaudacion UNION ALL
SELECT 'fr3_detalle_pg', COUNT(*) FROM public.fr3_detalle_pg UNION ALL
SELECT 'fr3_pg', COUNT(*) FROM public.fr3_pg UNION ALL
SELECT 'fr3_detalle', COUNT(*) FROM public.fr3_detalle UNION ALL
SELECT 'fr3', COUNT(*) FROM public.fr3 UNION ALL
SELECT 'aocr_tbviatico', COUNT(*) FROM public.aocr_tbviatico UNION ALL
SELECT 'aocr_tbinspeccion', COUNT(*) FROM public.aocr_tbinspeccion UNION ALL
SELECT 'aocr_tbsolicitud', COUNT(*) FROM public.aocr_tbsolicitud
ORDER BY tabla;

-- Conteo de tablas protegidas clave. Deben conservarse despues de la limpieza.
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
SELECT 'aocr_usuario_interno_rt', COUNT(*) FROM public.aocr_usuario_interno_rt;
