-- Limpieza transaccional AOCR.
-- Primera ejecucion: mantener ROLLBACK activo.
-- Para ejecucion definitiva: revisar conteos, respaldos y cambiar ROLLBACK por COMMIT manualmente.
-- No usa DROP, no usa TRUNCATE, no usa CASCADE, no toca usuarios/roles/permisos/menus/catalogos/parametros.

BEGIN;

SELECT 'ANTES email_queue' AS marcador, COUNT(*) AS total FROM public.email_queue;
SELECT 'ANTES aocr_tbsolicitud' AS marcador, COUNT(*) AS total FROM public.aocr_tbsolicitud;
SELECT 'ANTES aocr_tbdocumento' AS marcador, COUNT(*) AS total FROM public.aocr_tbdocumento;
SELECT 'ANTES aocr_tbinspeccion' AS marcador, COUNT(*) AS total FROM public.aocr_tbinspeccion;
SELECT 'ANTES aocr_or_orden' AS marcador, COUNT(*) AS total FROM public.aocr_or_orden;
SELECT 'ANTES aocr_tbpago' AS marcador, COUNT(*) AS total FROM public.aocr_tbpago;

-- Cola de correos y notificaciones.
DELETE FROM public.email_attachment;
DELETE FROM public.email_queue;
DELETE FROM public.aocr_tbnotificacion;

-- Historiales, auditoria funcional y logs transaccionales de pruebas.
DELETE FROM public.aocr_tbhistorial_documental;
DELETE FROM public.aocr_tbhistorial_estado_inspeccion;
DELETE FROM public.aocr_tbhistorial_estado;
DELETE FROM public.aocr_audit_trail;
DELETE FROM public.aocr_tbauditoria;
DELETE FROM public.aocr_tblog;

-- Temporales, idempotencia y logs de sincronizacion transaccional.
DELETE FROM public.aocr_declaracion_historial;
DELETE FROM public.aocr_declaracion_tmp;
DELETE FROM public.aocr_idempotency_key;
DELETE FROM public.aocr_sync_log;
DELETE FROM public.aocr_tb_sync_log;
DELETE FROM public.sync_log;

-- Historial de correo institucional. No elimina configuracion de correos.
DELETE FROM public.aocr_tbcorreo_institucional_historial;

-- Firmas y documentos del tramite.
DELETE FROM public.aocr_tbfirma_documento;
DELETE FROM public.aocr_tbdocumento_subsanacion;
DELETE FROM public.aocr_tbsubsanacion;
DELETE FROM public.aocr_tbrevision_documental;
DELETE FROM public.aocr_tbdocumento_inspeccion;
DELETE FROM public.aocr_tbdocumento;

-- Checklist, hallazgos, informes, LV e inspecciones.
DELETE FROM public.aocr_tbchecklist_solicitud;
DELETE FROM public.aocr_tbchecklist_item;
DELETE FROM public.aocr_tbchecklist;
DELETE FROM public.aocr_tbhallazgo;
DELETE FROM public.aocr_tbinforme_inspeccion;
DELETE FROM public.aocr_tbinforme;
DELETE FROM public.aocr_tblv_operacional_eae;
DELETE FROM public.aocr_tbobservacion;
DELETE FROM public.aocr_tbcertificado;
DELETE FROM public.aocr_tbaeronave_solicitud;
DELETE FROM public.aocr_tbaeronave;

-- Pagos, facturacion, ordenes y AS400 transaccional.
DELETE FROM public.aocr_tb_factura_pago;
DELETE FROM public.aocr_tbpago;
DELETE FROM public.aocr_or_orden_detalle;
DELETE FROM public.aocr_or_orden;
DELETE FROM public.aocr_orden_recaudacion;
DELETE FROM public.detalles_orden;
DELETE FROM public.historial_estados_orden;
DELETE FROM public.pagos;
DELETE FROM public.ordenes_recaudacion;
DELETE FROM public.fr3_detalle_pg;
DELETE FROM public.fr3_pg;
DELETE FROM public.fr3_detalle;
DELETE FROM public.fr3;

-- Dependientes directos de solicitud.
DELETE FROM public.aocr_tbviatico;
DELETE FROM public.aocr_tbinspeccion;

-- Padre principal del flujo AOCR.
DELETE FROM public.aocr_tbsolicitud;

SELECT 'DESPUES email_queue' AS marcador, COUNT(*) AS total FROM public.email_queue;
SELECT 'DESPUES aocr_tbsolicitud' AS marcador, COUNT(*) AS total FROM public.aocr_tbsolicitud;
SELECT 'DESPUES aocr_tbdocumento' AS marcador, COUNT(*) AS total FROM public.aocr_tbdocumento;
SELECT 'DESPUES aocr_tbinspeccion' AS marcador, COUNT(*) AS total FROM public.aocr_tbinspeccion;
SELECT 'DESPUES aocr_or_orden' AS marcador, COUNT(*) AS total FROM public.aocr_or_orden;
SELECT 'DESPUES aocr_tbpago' AS marcador, COUNT(*) AS total FROM public.aocr_tbpago;

-- Deben conservar datos.
SELECT 'PROTEGIDA usuario' AS marcador, COUNT(*) AS total FROM public.usuario;
SELECT 'PROTEGIDA rol' AS marcador, COUNT(*) AS total FROM public.rol;
SELECT 'PROTEGIDA seguridad_permiso' AS marcador, COUNT(*) AS total FROM public.seguridad_permiso;
SELECT 'PROTEGIDA seguridad_rol_permiso' AS marcador, COUNT(*) AS total FROM public.seguridad_rol_permiso;
SELECT 'PROTEGIDA aocr_or_concepto' AS marcador, COUNT(*) AS total FROM public.aocr_or_concepto;

-- Primera ejecucion obligatoria: no persistir cambios.
ROLLBACK;

-- Ejecucion definitiva, solo tras validar respaldos y conteos:
-- COMMIT;
