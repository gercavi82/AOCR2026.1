-- =====================================================================
-- MIGRACIÓN IDEMPOTENTE — TRAZABILIDAD AOCR
-- Fecha: 2026-04-21
-- Objetivo:
--   1. Asegurar existencia de aocr_tbhistorial_documental (eventos por doc).
--   2. Asegurar existencia de aocr_tbdocumento_subsanacion (cargas RT tras observación).
--   3. Crear vista consolidada v_aocr_trazabilidad_tramite que unifica
--      TODOS los eventos (cambios de estado + revisiones documentales +
--      eventos documentales + cargas iniciales de documentos del RT +
--      cargas de subsanación del RT + emisión/respuesta de subsanaciones).
--
-- Esta migración NO elimina datos ni estructuras. Es 100% aditiva.
-- Se puede ejecutar varias veces sin efectos colaterales.
-- =====================================================================

BEGIN;

-- 1) aocr_tbhistorial_documental (si no existe) ----------------------
CREATE TABLE IF NOT EXISTS aocr_tbhistorial_documental (
    codigo_historial      BIGSERIAL PRIMARY KEY,
    codigo_solicitud      INTEGER NOT NULL,
    codigo_documento      INTEGER NULL,
    evento                VARCHAR(80) NOT NULL,
    detalle               TEXT NULL,
    codigo_usuario        INTEGER NULL,
    fecha_evento          TIMESTAMP NOT NULL DEFAULT NOW(),
    created_at            TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by            VARCHAR(100) NULL
);

CREATE INDEX IF NOT EXISTS idx_histdoc_solicitud ON aocr_tbhistorial_documental(codigo_solicitud);
CREATE INDEX IF NOT EXISTS idx_histdoc_documento ON aocr_tbhistorial_documental(codigo_documento);
CREATE INDEX IF NOT EXISTS idx_histdoc_fecha     ON aocr_tbhistorial_documental(fecha_evento);

-- 2) aocr_tbdocumento_subsanacion (si no existe) --------------------
--    Se asume ya creada por migrate_solicitud_aocr_estados.sql; se refuerza.
CREATE TABLE IF NOT EXISTS aocr_tbdocumento_subsanacion (
    codigo_documento      SERIAL PRIMARY KEY,
    codigo_subsanacion    INTEGER NOT NULL,
    nombre_archivo        VARCHAR(255) NOT NULL,
    ruta_archivo          VARCHAR(500) NOT NULL,
    tipo_documento        VARCHAR(100) NULL,
    tamanio_bytes         INTEGER NULL,
    fecha_carga           TIMESTAMP NOT NULL DEFAULT NOW(),
    codigo_usuario_carga  INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_docsub_subsanacion ON aocr_tbdocumento_subsanacion(codigo_subsanacion);

-- 3) Vista unificada de trazabilidad --------------------------------
-- Columnas estandarizadas:
--   codigo_solicitud, fecha_evento, usuario_id, usuario_nombre, rol,
--   modulo, accion, estado_anterior, estado_nuevo, observacion,
--   codigo_documento, documento_afectado, fuente
-- NOTA: usuario/rol pueden venir NULL si las tablas origen no lo guardan;
-- la presentación debe mostrar "—" en esos casos.

CREATE OR REPLACE VIEW v_aocr_trazabilidad_tramite AS
-- 3.1 Cambios de estado (aocr_tbhistorialestado)
SELECT
    h.codigosolicitud                      AS codigo_solicitud,
    COALESCE(h.fechacambio, NOW())         AS fecha_evento,
    h.codigousuario                        AS usuario_id,
    COALESCE(u.nombres || ' ' || u.apellidos, u.usuario, '—') AS usuario_nombre,
    COALESCE(r.nombre, '—')                AS rol,
    'ESTADO'                               AS modulo,
    'CAMBIO_ESTADO'                        AS accion,
    h.estadoanterior                       AS estado_anterior,
    h.estadonuevo                          AS estado_nuevo,
    h.observaciones                        AS observacion,
    NULL::INTEGER                          AS codigo_documento,
    NULL::VARCHAR                          AS documento_afectado,
    'historialestado'                      AS fuente
FROM aocr_tbhistorialestado h
LEFT JOIN usuario u ON u.idusuario = h.codigousuario
LEFT JOIN aocr_tbusuariorol ur ON ur.codigo_usuario = h.codigousuario
LEFT JOIN aocr_tbrol r ON r.codigo_rol = ur.codigo_rol

UNION ALL

-- 3.2 Decisiones documentales del revisor (aocr_tbrevision_documental)
SELECT
    rd.codigo_solicitud,
    COALESCE(rd.fecha_revision, NOW())     AS fecha_evento,
    rd.codigo_usuario_revisor              AS usuario_id,
    COALESCE(u.nombres || ' ' || u.apellidos, u.usuario, '—') AS usuario_nombre,
    COALESCE(r.nombre, '—')                AS rol,
    'REVISION_DOCUMENTAL'                  AS modulo,
    UPPER(COALESCE(rd.decision, 'REVISION')) AS accion,
    NULL                                   AS estado_anterior,
    NULL                                   AS estado_nuevo,
    rd.observacion                         AS observacion,
    rd.codigo_documento,
    d.nombre_archivo                       AS documento_afectado,
    'revision_documental'                  AS fuente
FROM aocr_tbrevision_documental rd
LEFT JOIN usuario u ON u.idusuario = rd.codigo_usuario_revisor
LEFT JOIN aocr_tbusuariorol ur ON ur.codigo_usuario = rd.codigo_usuario_revisor
LEFT JOIN aocr_tbrol r ON r.codigo_rol = ur.codigo_rol
LEFT JOIN aocr_tbdocumento d ON d.codigo_documento = rd.codigo_documento

UNION ALL

-- 3.3 Eventos documentales (aocr_tbhistorial_documental)
SELECT
    hd.codigo_solicitud,
    COALESCE(hd.fecha_evento, NOW())       AS fecha_evento,
    hd.codigo_usuario                      AS usuario_id,
    COALESCE(u.nombres || ' ' || u.apellidos, u.usuario, hd.created_by, '—') AS usuario_nombre,
    COALESCE(r.nombre, '—')                AS rol,
    'DOCUMENTAL'                           AS modulo,
    UPPER(COALESCE(hd.evento, 'EVENTO'))   AS accion,
    NULL                                   AS estado_anterior,
    NULL                                   AS estado_nuevo,
    hd.detalle                             AS observacion,
    hd.codigo_documento,
    d.nombre_archivo                       AS documento_afectado,
    'historial_documental'                 AS fuente
FROM aocr_tbhistorial_documental hd
LEFT JOIN usuario u ON u.idusuario = hd.codigo_usuario
LEFT JOIN aocr_tbusuariorol ur ON ur.codigo_usuario = hd.codigo_usuario
LEFT JOIN aocr_tbrol r ON r.codigo_rol = ur.codigo_rol
LEFT JOIN aocr_tbdocumento d ON d.codigo_documento = hd.codigo_documento

UNION ALL

-- 3.4 Carga de documento por el RT (aocr_tbdocumento — evento de carga)
SELECT
    d.codigo_solicitud,
    COALESCE(d.fecha_carga, d.created_at, NOW()) AS fecha_evento,
    NULL::INTEGER                          AS usuario_id,
    COALESCE(d.created_by, 'RT')           AS usuario_nombre,
    'RT'                                   AS rol,
    'DOCUMENTAL'                           AS modulo,
    CASE WHEN COALESCE(d.version, 1) > 1 THEN 'CARGA_SUBSANACION' ELSE 'CARGA_INICIAL' END AS accion,
    NULL                                   AS estado_anterior,
    NULL                                   AS estado_nuevo,
    d.observaciones                        AS observacion,
    d.codigo_documento,
    d.nombre_archivo                       AS documento_afectado,
    'documento'                            AS fuente
FROM aocr_tbdocumento d

UNION ALL

-- 3.5 Cargas del RT como respuesta a subsanación (aocr_tbdocumento_subsanacion)
SELECT
    s.codigo_solicitud,
    COALESCE(ds.fecha_carga, NOW())        AS fecha_evento,
    ds.codigo_usuario_carga                AS usuario_id,
    COALESCE(u.nombres || ' ' || u.apellidos, u.usuario, 'RT') AS usuario_nombre,
    'RT'                                   AS rol,
    'SUBSANACION'                          AS modulo,
    'RESPUESTA_OBSERVACION'                AS accion,
    NULL                                   AS estado_anterior,
    NULL                                   AS estado_nuevo,
    s.observacion                          AS observacion,
    NULL::INTEGER                          AS codigo_documento,
    ds.nombre_archivo                      AS documento_afectado,
    'documento_subsanacion'                AS fuente
FROM aocr_tbdocumento_subsanacion ds
JOIN aocr_tbsubsanacion s ON s.codigo_subsanacion = ds.codigo_subsanacion
LEFT JOIN usuario u ON u.idusuario = ds.codigo_usuario_carga

UNION ALL

-- 3.6 Subsanaciones emitidas (aocr_tbsubsanacion)
SELECT
    s.codigo_solicitud,
    COALESCE(s.fecha_solicitud, NOW())     AS fecha_evento,
    s.codigo_usuario_solicita              AS usuario_id,
    COALESCE(u.nombres || ' ' || u.apellidos, u.usuario, '—') AS usuario_nombre,
    COALESCE(r.nombre, '—')                AS rol,
    'SUBSANACION'                          AS modulo,
    'SUBSANACION_SOLICITADA'               AS accion,
    NULL                                   AS estado_anterior,
    NULL                                   AS estado_nuevo,
    s.observacion                          AS observacion,
    NULL::INTEGER                          AS codigo_documento,
    NULL::VARCHAR                          AS documento_afectado,
    'subsanacion'                          AS fuente
FROM aocr_tbsubsanacion s
LEFT JOIN usuario u ON u.idusuario = s.codigo_usuario_solicita
LEFT JOIN aocr_tbusuariorol ur ON ur.codigo_usuario = s.codigo_usuario_solicita
LEFT JOIN aocr_tbrol r ON r.codigo_rol = ur.codigo_rol
;

COMMENT ON VIEW v_aocr_trazabilidad_tramite IS
  'Línea de tiempo unificada del expediente AOCR: cambios de estado + revisiones + eventos documentales + cargas del RT + subsanaciones. Consumida por el detalle de solicitud (Revisar solicitud).';

COMMIT;
