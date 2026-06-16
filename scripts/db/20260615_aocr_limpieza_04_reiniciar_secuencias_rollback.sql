-- Reinicio de secuencias transaccionales AOCR.
-- Ejecutar solo despues de confirmar la limpieza definitiva.
-- Primera ejecucion: mantener ROLLBACK activo.
-- No reinicia secuencias de usuarios, roles, permisos, menus, parametros ni catalogos base.

BEGIN;

ALTER SEQUENCE public.email_attachment_id_seq RESTART WITH 1;
ALTER SEQUENCE public.email_queue_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbnotificacion_codigo_notificacion_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbhistorial_documental_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbhistorial_estado_inspeccion_codigo_historial_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbhistorial_estado_codigo_historial_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_audit_trail_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbauditoria_codigo_auditoria_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tblog_codigo_log_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_declaracion_historial_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_declaracion_tmp_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_idempotency_key_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_sync_log_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tb_sync_log_id_seq RESTART WITH 1;
ALTER SEQUENCE public.sync_log_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbcorreo_institucional_historial_codigo_historial_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbfirma_documento_codigo_firma_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbdocumento_subsanacion_codigo_documento_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbsubsanacion_codigo_subsanacion_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbrevision_documental_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbdocumento_inspeccion_codigo_documento_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbdocumento_codigo_documento_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbchecklist_solicitud_codigo_checklist_solicitud_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbchecklist_item_codigo_item_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbchecklist_codigo_checklist_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbhallazgo_codigo_hallazgo_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbinforme_inspeccion_codigo_informe_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbinforme_codigo_informe_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tblv_operacional_eae_codigo_lv_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbobservacion_codigo_observacion_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbcertificado_codigo_certificado_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbaeronave_solicitud_codigo_aeronave_solicitud_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbaeronave_codigo_aeronave_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tb_factura_pago_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbpago_codigo_pago_seq RESTART WITH 1;
ALTER SEQUENCE public.detalles_orden_id_seq RESTART WITH 1;
ALTER SEQUENCE public.historial_estados_orden_id_seq RESTART WITH 1;
ALTER SEQUENCE public.pagos_id_seq RESTART WITH 1;
ALTER SEQUENCE public.ordenes_recaudacion_id_seq RESTART WITH 1;
ALTER SEQUENCE public.fr3_detalle_pg_id_seq RESTART WITH 1;
ALTER SEQUENCE public.fr3_pg_id_seq RESTART WITH 1;
ALTER SEQUENCE public.fr3_detalle_id_seq RESTART WITH 1;
ALTER SEQUENCE public.fr3_id_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbviatico_codigo_viatico_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbinspeccion_codigo_inspeccion_seq RESTART WITH 1;
ALTER SEQUENCE public.aocr_tbsolicitud_codigo_solicitud_seq RESTART WITH 1;

-- Secuencia de numeracion de ordenes AOCR. Reiniciar solo si el ambiente NO comparte numeracion productiva.
ALTER SEQUENCE public.aocr_or_numero_seq RESTART WITH 1;

SELECT sequence_schema, sequence_name
FROM information_schema.sequences
WHERE sequence_schema = 'public'
  AND sequence_name IN (
      'email_queue_id_seq',
      'aocr_tbdocumento_codigo_documento_seq',
      'aocr_tbinspeccion_codigo_inspeccion_seq',
      'aocr_tbsolicitud_codigo_solicitud_seq',
      'aocr_or_numero_seq'
  )
ORDER BY sequence_name;

ROLLBACK;

-- Ejecucion definitiva, solo tras validar limpieza:
-- COMMIT;
