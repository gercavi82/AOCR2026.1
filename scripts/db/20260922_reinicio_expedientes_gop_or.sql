-- DESTRUCTIVO: reinicio autorizado de TODOS los expedientes GOP/OR de dgac_des.
-- Requiere respaldo verificado previo. Conserva usuarios, roles y catalogos.
-- No reinicia identificadores internos: solo los correlativos institucionales.
BEGIN;
SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '60s';
DO $$ BEGIN
 IF current_database() <> 'dgac_des' OR host(inet_server_addr()) <> '172.20.16.55' THEN
  RAISE EXCEPTION 'Base de datos no autorizada para este reinicio';
 END IF;
END $$;
LOCK TABLE public.aocr_entrega_destinatario, public.aocr_entrega_documento, public.aocr_entrega_intento, public.aocr_entrega_final, public.aocr_tbdesignacion_inspector, public.aocr_revision_documental_coordinador, public.aocr_inspector_reasignacion_historial, public.aocr_tbnoconformidad, public.aocr_tbdocumento_subsanacion, public.aocr_tbsubsanacion, public.aocr_tbdocumento_inspeccion, public.aocr_tbinforme_inspeccion, public.aocr_tbinforme, public.aocr_tbchecklist, public.aocr_tbhallazgo, public.aocr_tblv_operacional_eae, public.aocr_tbhistorial_estado_inspeccion, public.aocr_tbfirma_posicion_documento, public.aocr_tbfirma_documento, public.aocr_tbcondiciones_limitaciones, public.aocr_tbcertificado, public.aocr_tbdocumento_generado, public.aocr_tbsolicitud_estacion, public.aocr_tbinspeccion, public.aocr_tbobservacion, public.aocr_tbviatico, public.aocr_tbrevision_documental, public.aocr_tbhistorial_documental, public.aocr_tbdocumento, public.aocr_tbaeronave_solicitud, public.aocr_tbaeronave, public.aocr_tbchecklist_solicitud, public.aocr_asignacion_rt, public.aocr_tbhistorial_estado, public.aocr_expediente_archivo, public.aocr_fr3_integracion, public.aocr_fr3_operacion, public.aocr_fr3_outbox, public.aocr_fr3_retry_queue, public.aocr_idempotency_key, public.aocr_proceso_estado, public.aocr_proceso_estado_historial, public.aocr_proceso_idempotencia, public.aocr_evento_workflow, public.aocr_sync_log, public.aocr_tb_sync_log, public.aocr_tb_factura_pago, public.aocr_tbpago, public.aocr_pago_comprobante, public.aocr_orden_recaudacion_detalle, public.aocr_orden_recaudacion, public.aocr_or_orden_detalle, public.django_aocr_factura_recaudacion, public.django_aocr_orden_observacion, public.aocr_or_orden, public.aocr_tbsolicitud, public.aocr_expediente_compania IN EXCLUSIVE MODE;
DELETE FROM public.aocr_entrega_destinatario;
DELETE FROM public.aocr_entrega_documento;
DELETE FROM public.aocr_entrega_intento;
DELETE FROM public.aocr_entrega_final;
DELETE FROM public.email_queue WHERE solicitud_id IS NOT NULL OR orden_id IS NOT NULL;
DELETE FROM public.aocr_tbdesignacion_inspector;
DELETE FROM public.aocr_revision_documental_coordinador;
DELETE FROM public.aocr_inspector_reasignacion_historial;
DELETE FROM public.aocr_tbnoconformidad;
DELETE FROM public.aocr_tbdocumento_subsanacion;
DELETE FROM public.aocr_tbsubsanacion;
DELETE FROM public.aocr_tbdocumento_inspeccion;
DELETE FROM public.aocr_tbinforme_inspeccion;
DELETE FROM public.aocr_tbinforme;
DELETE FROM public.aocr_tbchecklist;
DELETE FROM public.aocr_tbhallazgo;
DELETE FROM public.aocr_tblv_operacional_eae;
DELETE FROM public.aocr_tbhistorial_estado_inspeccion;
DELETE FROM public.aocr_tbfirma_posicion_documento;
DELETE FROM public.aocr_tbfirma_documento;
DELETE FROM public.aocr_tbcondiciones_limitaciones;
DELETE FROM public.aocr_tbcertificado;
DELETE FROM public.aocr_tbdocumento_generado;
DELETE FROM public.aocr_tbsolicitud_estacion;
DELETE FROM public.aocr_tbinspeccion;
DELETE FROM public.aocr_tbobservacion;
DELETE FROM public.aocr_tbviatico;
DELETE FROM public.aocr_tbrevision_documental;
DELETE FROM public.aocr_tbhistorial_documental;
DELETE FROM public.aocr_tbdocumento;
DELETE FROM public.aocr_tbaeronave_solicitud;
DELETE FROM public.aocr_tbaeronave;
DELETE FROM public.aocr_tbchecklist_solicitud;
DELETE FROM public.aocr_asignacion_rt;
DELETE FROM public.aocr_tbhistorial_estado;
DELETE FROM public.aocr_expediente_archivo;
DELETE FROM public.aocr_fr3_integracion;
DELETE FROM public.aocr_fr3_operacion;
DELETE FROM public.aocr_fr3_outbox;
DELETE FROM public.aocr_fr3_retry_queue;
DELETE FROM public.aocr_idempotency_key;
DELETE FROM public.aocr_proceso_estado;
DELETE FROM public.aocr_proceso_estado_historial;
DELETE FROM public.aocr_proceso_idempotencia;
DELETE FROM public.aocr_evento_workflow;
DELETE FROM public.aocr_sync_log;
DELETE FROM public.aocr_tb_sync_log;
DELETE FROM public.aocr_tb_factura_pago;
DELETE FROM public.aocr_tbpago;
DELETE FROM public.aocr_pago_comprobante;
DELETE FROM public.aocr_orden_recaudacion_detalle;
DELETE FROM public.aocr_orden_recaudacion;
DELETE FROM public.aocr_or_orden_detalle;
DELETE FROM public.django_aocr_factura_recaudacion;
DELETE FROM public.django_aocr_orden_observacion;
DELETE FROM public.aocr_or_orden;
DELETE FROM public.aocr_tbsolicitud;
DELETE FROM public.aocr_expediente_compania;
DELETE FROM public.aocr_tbnotificacion
 WHERE lower(coalesce(modulo,'')) IN ('solicitudaocr','solicitud_aocr','ordenrecaudacion','inspeccion','fr3')
    OR lower(coalesce(tipo_entidad,'')) IN ('solicitudaocr','ordenrecaudacion','inspeccion');
DELETE FROM public.aocr_tbauditoria WHERE tabla_afectada IN ('aocr_entrega_destinatario','aocr_entrega_documento','aocr_entrega_intento','aocr_entrega_final','aocr_tbdesignacion_inspector','aocr_revision_documental_coordinador','aocr_inspector_reasignacion_historial','aocr_tbnoconformidad','aocr_tbdocumento_subsanacion','aocr_tbsubsanacion','aocr_tbdocumento_inspeccion','aocr_tbinforme_inspeccion','aocr_tbinforme','aocr_tbchecklist','aocr_tbhallazgo','aocr_tblv_operacional_eae','aocr_tbhistorial_estado_inspeccion','aocr_tbfirma_posicion_documento','aocr_tbfirma_documento','aocr_tbcondiciones_limitaciones','aocr_tbcertificado','aocr_tbdocumento_generado','aocr_tbsolicitud_estacion','aocr_tbinspeccion','aocr_tbobservacion','aocr_tbviatico','aocr_tbrevision_documental','aocr_tbhistorial_documental','aocr_tbdocumento','aocr_tbaeronave_solicitud','aocr_tbaeronave','aocr_tbchecklist_solicitud','aocr_asignacion_rt','aocr_tbhistorial_estado','aocr_expediente_archivo','aocr_fr3_integracion','aocr_fr3_operacion','aocr_fr3_outbox','aocr_fr3_retry_queue','aocr_idempotency_key','aocr_proceso_estado','aocr_proceso_estado_historial','aocr_proceso_idempotencia','aocr_evento_workflow','aocr_sync_log','aocr_tb_sync_log','aocr_tb_factura_pago','aocr_tbpago','aocr_pago_comprobante','aocr_orden_recaudacion_detalle','aocr_orden_recaudacion','aocr_or_orden_detalle','django_aocr_factura_recaudacion','django_aocr_orden_observacion','aocr_or_orden','aocr_tbsolicitud','aocr_expediente_compania');
DELETE FROM public.aocr_audit_trail WHERE tabla IN ('aocr_entrega_destinatario','aocr_entrega_documento','aocr_entrega_intento','aocr_entrega_final','aocr_tbdesignacion_inspector','aocr_revision_documental_coordinador','aocr_inspector_reasignacion_historial','aocr_tbnoconformidad','aocr_tbdocumento_subsanacion','aocr_tbsubsanacion','aocr_tbdocumento_inspeccion','aocr_tbinforme_inspeccion','aocr_tbinforme','aocr_tbchecklist','aocr_tbhallazgo','aocr_tblv_operacional_eae','aocr_tbhistorial_estado_inspeccion','aocr_tbfirma_posicion_documento','aocr_tbfirma_documento','aocr_tbcondiciones_limitaciones','aocr_tbcertificado','aocr_tbdocumento_generado','aocr_tbsolicitud_estacion','aocr_tbinspeccion','aocr_tbobservacion','aocr_tbviatico','aocr_tbrevision_documental','aocr_tbhistorial_documental','aocr_tbdocumento','aocr_tbaeronave_solicitud','aocr_tbaeronave','aocr_tbchecklist_solicitud','aocr_asignacion_rt','aocr_tbhistorial_estado','aocr_expediente_archivo','aocr_fr3_integracion','aocr_fr3_operacion','aocr_fr3_outbox','aocr_fr3_retry_queue','aocr_idempotency_key','aocr_proceso_estado','aocr_proceso_estado_historial','aocr_proceso_idempotencia','aocr_evento_workflow','aocr_sync_log','aocr_tb_sync_log','aocr_tb_factura_pago','aocr_tbpago','aocr_pago_comprobante','aocr_orden_recaudacion_detalle','aocr_orden_recaudacion','aocr_or_orden_detalle','django_aocr_factura_recaudacion','django_aocr_orden_observacion','aocr_or_orden','aocr_tbsolicitud','aocr_expediente_compania');
DELETE FROM public.aocr_correlativo_orden;
DELETE FROM public.aocr_correlativo_anual;
COMMIT;
