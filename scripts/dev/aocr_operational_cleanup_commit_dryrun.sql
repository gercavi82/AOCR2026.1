-- VERSION DE EJECUCION REAL
-- Usa COMMIT al final. Ejecute solo despues de confirmar el respaldo.

-- Si el cliente SQL deja la sesion en estado aborted tras un fallo anterior,
-- este ROLLBACK inicial limpia ese estado antes de empezar una nueva transaccion.
ROLLBACK;
BEGIN;

-- PRECONDICIONES OBLIGATORIAS
-- 1. Ejecutar scripts/db/backup_aocr_before_cleanup.ps1 y confirmar que existen ambos respaldos.
-- 2. Revisar scripts/db/aocr_cleanup_inventory.md.
-- 3. Ejecutar scripts/db/aocr_post_cleanup_verify.sql antes y despues si desea contrastar conteos.
-- 4. Este archivo SI hace COMMIT al final.
-- 5. NO incluye tablas de usuarios, roles, permisos, menus, parametros, conceptos ni catalogos base.

CREATE TEMP TABLE tmp_aocr_cleanup_counts
(
    phase TEXT NOT NULL,
    table_name TEXT NOT NULL,
    row_count BIGINT NOT NULL
) ON COMMIT DROP;

DO $$
DECLARE
    r RECORD;
    c BIGINT;
BEGIN
    FOR r IN
        SELECT table_name
        FROM (
            VALUES
                ('aocr_asignacion_rt'),
                ('aocr_audit_trail'),
                ('aocr_idempotency_key'),
                ('aocr_or_orden'),
                ('aocr_or_orden_detalle'),
                ('aocr_orden_recaudacion'),
                ('aocr_sync_log'),
                ('aocr_tb_factura_pago'),
                ('aocr_tb_sync_log'),
                ('aocr_tbaeronave'),
                ('aocr_tbaeronave_solicitud'),
                ('aocr_tbauditoria'),
                ('aocr_tbcertificado'),
                ('aocr_tbchecklist'),
                ('aocr_tbchecklist_solicitud'),
                ('aocr_tbdocumento'),
                ('aocr_tbdocumento_inspeccion'),
                ('aocr_tbdocumento_subsanacion'),
                ('aocr_tbfirma_documento'),
                ('aocr_tbfirma_posicion_documento'),
                ('aocr_tbhallazgo'),
                ('aocr_tbhistorial_documental'),
                ('aocr_tbhistorial_estado'),
                ('aocr_tbhistorial_estado_inspeccion'),
                ('aocr_tbinforme'),
                ('aocr_tbinforme_inspeccion'),
                ('aocr_tbinspeccion'),
                ('aocr_tblog'),
                ('aocr_tblv_operacional_eae'),
                ('aocr_tbnotificacion'),
                ('aocr_tbobservacion'),
                ('aocr_tbpago'),
                ('aocr_tbrevision_documental'),
                ('aocr_tbsolicitud'),
                ('aocr_tbsubsanacion'),
                ('aocr_tbviatico'),
                ('detalles_orden'),
                ('email_attachment'),
                ('email_queue'),
                ('fr3'),
                ('fr3_detalle'),
                ('fr3_detalle_pg'),
                ('fr3_pg'),
                ('historial_estados_orden'),
                ('ordenes_recaudacion'),
                ('pagos'),
                ('sync_log')
        ) AS targets(table_name)
    LOOP
        EXECUTE format('SELECT count(*) FROM public.%I', r.table_name) INTO c;
        INSERT INTO tmp_aocr_cleanup_counts(phase, table_name, row_count)
        VALUES ('ANTES', r.table_name, c);
    END LOOP;
END $$;

SELECT phase, table_name, row_count
FROM tmp_aocr_cleanup_counts
WHERE phase = 'ANTES'
ORDER BY table_name;

-- TABLAS EN REVISION MANUAL Y EXCLUIDAS DE ESTE SCRIPT
-- aocr_declaracion_historial
-- aocr_declaracion_tmp
-- aocr_usuario_transferencia
-- aocr_usuario_transferencia_detalle
-- auditoria_seguridad

-- DESHABILITAR TEMPORALMENTE EL TRIGGER DE AUDITORIA DE SOLICITUDES
-- La limpieza borra tambien aocr_tbauditoria; no debe reinsertar auditoria durante los DELETE.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'aocr_tbsolicitud'
          AND t.tgname = 'audit_solicitud'
          AND NOT t.tgisinternal
    ) THEN
        EXECUTE 'ALTER TABLE public.aocr_tbsolicitud DISABLE TRIGGER audit_solicitud';
    END IF;
END $$;

-- HIJAS / DETALLE / COLAS
DELETE FROM public.email_attachment;
DELETE FROM public.email_queue;
DELETE FROM public.aocr_tb_factura_pago;
DELETE FROM public.aocr_or_orden_detalle;
DELETE FROM public.detalles_orden;
DELETE FROM public.pagos;
DELETE FROM public.historial_estados_orden;
DELETE FROM public.fr3_detalle;
DELETE FROM public.fr3_detalle_pg;

-- DOCUMENTOS / FIRMAS / RESULTADOS DE INSPECCION
DELETE FROM public.aocr_tbfirma_posicion_documento;
DELETE FROM public.aocr_tbfirma_documento;
DELETE FROM public.aocr_tbdocumento_inspeccion;
DELETE FROM public.aocr_tbdocumento_subsanacion;
DELETE FROM public.aocr_tbdocumento;
DELETE FROM public.aocr_tbhallazgo;
DELETE FROM public.aocr_tbhistorial_documental;
DELETE FROM public.aocr_tbhistorial_estado_inspeccion;
DELETE FROM public.aocr_tbinforme;
DELETE FROM public.aocr_tbinforme_inspeccion;
DELETE FROM public.aocr_tbchecklist;
DELETE FROM public.aocr_tbchecklist_solicitud;
DELETE FROM public.aocr_tblv_operacional_eae;
DELETE FROM public.aocr_tbrevision_documental;

-- EXPEDIENTE / TRAZABILIDAD OPERATIVA
DELETE FROM public.aocr_tbnotificacion;
DELETE FROM public.aocr_tbobservacion;
DELETE FROM public.aocr_tbsubsanacion;
DELETE FROM public.aocr_tbviatico;
DELETE FROM public.aocr_tbcertificado;
DELETE FROM public.aocr_tbhistorial_estado;
DELETE FROM public.aocr_tbpago;
DELETE FROM public.aocr_tbaeronave;
DELETE FROM public.aocr_tbaeronave_solicitud;
DELETE FROM public.aocr_asignacion_rt;

-- ORDENES / FR3 / SOLICITUDES / INSPECCIONES
DELETE FROM public.fr3;
DELETE FROM public.fr3_pg;
DELETE FROM public.aocr_or_orden;
DELETE FROM public.aocr_orden_recaudacion;
DELETE FROM public.ordenes_recaudacion;
DELETE FROM public.aocr_tbinspeccion;
DELETE FROM public.aocr_tbsolicitud;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_trigger t
        JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname = 'aocr_tbsolicitud'
          AND t.tgname = 'audit_solicitud'
          AND NOT t.tgisinternal
    ) THEN
        EXECUTE 'ALTER TABLE public.aocr_tbsolicitud ENABLE TRIGGER audit_solicitud';
    END IF;
END $$;

-- LOGS / AUDITORIA FUNCIONAL / IDEMPOTENCIA
DELETE FROM public.aocr_audit_trail;
DELETE FROM public.aocr_tbauditoria;
DELETE FROM public.aocr_tblog;
DELETE FROM public.aocr_sync_log;
DELETE FROM public.aocr_tb_sync_log;
DELETE FROM public.sync_log;
DELETE FROM public.aocr_idempotency_key;

DO $$
DECLARE
    r RECORD;
    c BIGINT;
BEGIN
    FOR r IN
        SELECT table_name
        FROM (
            VALUES
                ('aocr_asignacion_rt'),
                ('aocr_audit_trail'),
                ('aocr_idempotency_key'),
                ('aocr_or_orden'),
                ('aocr_or_orden_detalle'),
                ('aocr_orden_recaudacion'),
                ('aocr_sync_log'),
                ('aocr_tb_factura_pago'),
                ('aocr_tb_sync_log'),
                ('aocr_tbaeronave'),
                ('aocr_tbaeronave_solicitud'),
                ('aocr_tbauditoria'),
                ('aocr_tbcertificado'),
                ('aocr_tbchecklist'),
                ('aocr_tbchecklist_solicitud'),
                ('aocr_tbdocumento'),
                ('aocr_tbdocumento_inspeccion'),
                ('aocr_tbdocumento_subsanacion'),
                ('aocr_tbfirma_documento'),
                ('aocr_tbfirma_posicion_documento'),
                ('aocr_tbhallazgo'),
                ('aocr_tbhistorial_documental'),
                ('aocr_tbhistorial_estado'),
                ('aocr_tbhistorial_estado_inspeccion'),
                ('aocr_tbinforme'),
                ('aocr_tbinforme_inspeccion'),
                ('aocr_tbinspeccion'),
                ('aocr_tblog'),
                ('aocr_tblv_operacional_eae'),
                ('aocr_tbnotificacion'),
                ('aocr_tbobservacion'),
                ('aocr_tbpago'),
                ('aocr_tbrevision_documental'),
                ('aocr_tbsolicitud'),
                ('aocr_tbsubsanacion'),
                ('aocr_tbviatico'),
                ('detalles_orden'),
                ('email_attachment'),
                ('email_queue'),
                ('fr3'),
                ('fr3_detalle'),
                ('fr3_detalle_pg'),
                ('fr3_pg'),
                ('historial_estados_orden'),
                ('ordenes_recaudacion'),
                ('pagos'),
                ('sync_log')
        ) AS targets(table_name)
    LOOP
        EXECUTE format('SELECT count(*) FROM public.%I', r.table_name) INTO c;
        INSERT INTO tmp_aocr_cleanup_counts(phase, table_name, row_count)
        VALUES ('DESPUES', r.table_name, c);
    END LOOP;
END $$;

SELECT phase, table_name, row_count
FROM tmp_aocr_cleanup_counts
WHERE phase = 'DESPUES'
ORDER BY table_name;

ROLLBACK;
