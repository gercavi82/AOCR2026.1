-- Reconciliación post-aprobación financiera AOCR
-- Base: dgac_des
-- Objetivo: detectar y corregir inconsistencias históricas tras aprobación Financiero
-- Ejecutar en transacción manual; revisar SELECTs antes de COMMIT.

BEGIN;

-- =============================================================================
-- FASE A — DIAGNÓSTICO (solo lectura dentro de la transacción)
-- =============================================================================

-- A1. Órdenes aprobadas/facturadas que siguen en estado editable operativo distinto a ORDEN_CERRADA_POR_SOLICITUD
SELECT 'A1_ORDENES_APROBADAS_SIN_CIERRE_OPERATIVO' AS reporte,
       o.id,
       o.numero_orden,
       o.estado,
       o.codigo_solicitud,
       o.compania,
       o.compania_codigo
FROM aocr_or_orden o
WHERE UPPER(COALESCE(o.estado, '')) IN ('FACTURADA', 'PAGADA', 'COMPLETADA')
   OR EXISTS (
       SELECT 1 FROM aocr_tbpago p
       WHERE (p.codigo_solicitud::text = o.codigo_solicitud::text OR p.codigo_solicitud = o.id)
         AND UPPER(COALESCE(p.estado, '')) IN ('VALIDADO', 'APROBADO', 'PAGO_APROBADO')
   );

-- A2. Órdenes aprobadas que aún aparecerían en bandeja financiera (EN_REVISION + comprobante)
SELECT 'A2_ORDENES_EN_BANDEJA_FINANCIERA_INCONSISTENTE' AS reporte,
       o.id,
       o.numero_orden,
       o.estado,
       o.codigo_solicitud
FROM aocr_or_orden o
WHERE UPPER(REPLACE(COALESCE(o.estado, ''), ' ', '_')) IN ('EN_REVISION_FINANCIERA', 'PROCESADA', 'EN_REVISION')
  AND EXISTS (
      SELECT 1 FROM aocr_tbpago p
      WHERE (p.codigo_solicitud::text = o.codigo_solicitud::text OR p.codigo_solicitud = o.id)
        AND UPPER(COALESCE(p.estado, '')) IN ('VALIDADO', 'APROBADO', 'PAGO_APROBADO')
  );

-- A3. Solicitudes habilitadas sin pago aprobado
SELECT 'A3_SOLICITUD_HABILITADA_SIN_PAGO' AS reporte,
       s.codigo_solicitud,
       s.numero_solicitud,
       s.estado,
       COALESCE(s.pago_aprobado, FALSE) AS pago_aprobado,
       COALESCE(s.modulo_solicitud_rt_habilitado, FALSE) AS modulo_habilitado
FROM aocr_tbsolicitud s
WHERE s.deleted_at IS NULL
  AND (
      COALESCE(s.modulo_solicitud_rt_habilitado, FALSE) = TRUE
      OR UPPER(COALESCE(s.estado, '')) = 'SOLICITUD_AOCR_HABILITADA'
  )
  AND COALESCE(s.pago_aprobado, FALSE) = FALSE;

-- A4. Órdenes aprobadas sin solicitud vinculada
SELECT 'A4_ORDEN_APROBADA_SIN_SOLICITUD' AS reporte,
       o.id,
       o.numero_orden,
       o.estado,
       o.codigo_solicitud,
       o.codigo_usuario
FROM aocr_or_orden o
WHERE (
    UPPER(COALESCE(o.estado, '')) IN ('FACTURADA', 'PAGADA', 'COMPLETADA', 'ORDEN_CERRADA_POR_SOLICITUD')
    OR EXISTS (
        SELECT 1 FROM aocr_tbpago p
        WHERE (p.codigo_solicitud::text = o.codigo_solicitud::text OR p.codigo_solicitud = o.id)
          AND UPPER(COALESCE(p.estado, '')) IN ('VALIDADO', 'APROBADO', 'PAGO_APROBADO')
    )
)
AND (
    o.codigo_solicitud IS NULL
    OR TRIM(o.codigo_solicitud::text) = ''
    OR NOT EXISTS (
        SELECT 1 FROM aocr_tbsolicitud s
        WHERE s.codigo_solicitud::text = o.codigo_solicitud::text
          AND s.deleted_at IS NULL
    )
);

-- A5. Compañías con más de una solicitud AOCR activa (misma compañía en companias_seleccionadas)
SELECT 'A5_MULTIPLES_SOLICITUDES_ACTIVAS_COMPANIA' AS reporte,
       TRIM(s.companias_seleccionadas) AS compania_codigo,
       COUNT(*) AS total_activas
FROM aocr_tbsolicitud s
WHERE s.deleted_at IS NULL
  AND COALESCE(s.solicitud_finalizada_rt, FALSE) = FALSE
  AND UPPER(COALESCE(s.estado, '')) NOT IN ('FINALIZADO', 'ANULADA', 'CERRADO', 'AOCR_EMITIDO_RECIBIDO', 'AOCR_LEGALIZADO')
  AND NULLIF(TRIM(COALESCE(s.companias_seleccionadas, '')), '') IS NOT NULL
GROUP BY TRIM(s.companias_seleccionadas)
HAVING COUNT(*) > 1;

-- =============================================================================
-- FASE B — CORRECCIÓN SEGURA (idempotente)
-- =============================================================================

-- B1. Normalizar estado pago histórico VALIDADO → APROBADO (chk_estado_pago no admite PAGO_APROBADO)
UPDATE aocr_tbpago
SET estado = 'APROBADO'
WHERE UPPER(COALESCE(estado, '')) = 'VALIDADO'
  AND estado <> 'APROBADO';

-- B2. Cerrar operativamente órdenes con pago aprobado que no estén anuladas (chk_estado + varchar(20))
UPDATE aocr_or_orden o
SET estado = 'COMPLETADA',
    observacion = CASE
        WHEN NULLIF(TRIM(COALESCE(o.observacion, '')), '') IS NULL
        THEN 'Esta Orden de Recaudación fue aprobada por Financiero y quedó cerrada para el proceso AOCR actual. La Solicitud AOCR ya se encuentra habilitada para continuar el trámite.'
        ELSE o.observacion
    END
WHERE UPPER(COALESCE(o.estado, '')) NOT IN ('ANULADA', 'ORDEN_CERRADA_POR_SOLICITUD', 'ORDEN_INACTIVA')
  AND (
      UPPER(COALESCE(o.estado, '')) IN ('FACTURADA', 'PAGADA', 'COMPLETADA')
      OR EXISTS (
          SELECT 1 FROM aocr_tbpago p
          WHERE (p.codigo_solicitud::text = o.codigo_solicitud::text OR p.codigo_solicitud = o.id)
            AND UPPER(COALESCE(p.estado, '')) IN ('VALIDADO', 'APROBADO', 'PAGO_APROBADO')
      )
  );

-- B3. Habilitar solicitudes vinculadas a órdenes cerradas con pago aprobado
UPDATE aocr_tbsolicitud s
SET pago_aprobado = TRUE,
    fecha_aprobacion_pago = COALESCE(s.fecha_aprobacion_pago, NOW()),
    modulo_solicitud_rt_habilitado = TRUE,
    solicitud_finalizada_rt = FALSE,
    requiere_nueva_orden = FALSE,
    pendiente_carga_documental_rt = CASE
        WHEN EXISTS (
            SELECT 1 FROM aocr_tbdocumento d
            WHERE d.codigo_solicitud = s.codigo_solicitud
              AND COALESCE(d.tamano_bytes, 0) > 0
              AND NULLIF(TRIM(COALESCE(d.nombre_archivo, '')), '') IS NOT NULL
              AND UPPER(TRIM(COALESCE(d.tipo_documento, ''))) NOT IN ('BORRADOR_AOCR', 'AOCR_GENERADO', 'AOCR')
        ) THEN FALSE
        ELSE TRUE
    END,
    estado = CASE
        WHEN UPPER(COALESCE(s.estado, '')) IN (
            '', 'PENDIENTE', 'PAGO_PENDIENTE', 'PAGO_VALIDADO',
            'PENDIENTE_CARGA_DOCUMENTAL_RT', 'SOLICITUD_CREADA', 'DOCUMENTACION_PENDIENTE'
        ) THEN 'SOLICITUD_AOCR_HABILITADA'
        ELSE s.estado
    END,
    updated_at = NOW(),
    updated_by = 'RECONCILIACION_20260613'
WHERE s.deleted_at IS NULL
  AND EXISTS (
      SELECT 1
      FROM aocr_or_orden o
      WHERE o.codigo_solicitud::text = s.codigo_solicitud::text
        AND UPPER(COALESCE(o.estado, '')) IN ('ORDEN_CERRADA_POR_SOLICITUD', 'FACTURADA', 'PAGADA', 'COMPLETADA', 'ORDEN_INACTIVA')
  )
  AND (
      COALESCE(s.pago_aprobado, FALSE) = FALSE
      OR COALESCE(s.modulo_solicitud_rt_habilitado, FALSE) = FALSE
  );

-- =============================================================================
-- FASE C — VERIFICACIÓN POST-CORRECCIÓN
-- =============================================================================

SELECT 'C1_RESUMEN' AS reporte,
       (SELECT COUNT(*) FROM aocr_or_orden WHERE UPPER(COALESCE(estado,'')) = 'ORDEN_CERRADA_POR_SOLICITUD') AS ordenes_cerradas,
       (SELECT COUNT(*) FROM aocr_tbsolicitud WHERE COALESCE(modulo_solicitud_rt_habilitado,FALSE) = TRUE AND deleted_at IS NULL) AS solicitudes_habilitadas,
       (SELECT COUNT(*) FROM aocr_tbpago WHERE UPPER(COALESCE(estado,'')) = 'PAGO_APROBADO') AS pagos_pago_aprobado;

-- Revisar resultados. Si todo OK:
-- COMMIT;
-- Si algo falla:
ROLLBACK;
