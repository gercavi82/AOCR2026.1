-- Habilita COO-1 para solicitud #12: cierra revision documental pre-asignacion.
BEGIN;

INSERT INTO aocr_tbrevision_documental (
    codigo_solicitud,
    codigo_documento,
    decision,
    observacion,
    codigo_usuario_revisor,
    fecha_revision,
    created_at,
    created_by)
SELECT
    12,
    d.codigo_documento,
    'ACEPTADO',
    'Revision documental institucional pre-asignacion (COO-1).',
    45,
    NOW(),
    NOW(),
    'GEN_COORDINACION'
FROM aocr_tbdocumento d
WHERE d.codigo_solicitud = 12
  AND NOT EXISTS (
      SELECT 1
      FROM aocr_tbrevision_documental r
      WHERE r.codigo_solicitud = 12
        AND r.codigo_documento = d.codigo_documento
        AND UPPER(COALESCE(r.decision, '')) = 'ACEPTADO'
  );

UPDATE aocr_tbdocumento
SET estado = 'Aprobado',
    validado = TRUE,
    fecha_validacion = NOW(),
    validado_por = 'GEN_COORDINACION'
WHERE codigo_solicitud = 12;

UPDATE aocr_tbsolicitud
SET estado = 'Aceptacion Documental',
    updated_at = NOW()
WHERE codigo_solicitud = 12;

INSERT INTO aocr_tbhistorial_estado (
    codigo_solicitud,
    estado_anterior,
    estado_nuevo,
    codigo_usuario,
    observaciones,
    fecha_cambio)
VALUES (
    12,
    'En Revision',
    'Aceptacion Documental',
    45,
    'Revision documental cerrada. Pendiente firma de coordinacion (COO-1).',
    NOW());

COMMIT;

SELECT codigo_solicitud, numero_solicitud, tipo_solicitud, estado
FROM aocr_tbsolicitud
WHERE codigo_solicitud = 12;

SELECT COUNT(*) AS revisiones_aceptadas
FROM aocr_tbrevision_documental
WHERE codigo_solicitud = 12
  AND UPPER(COALESCE(decision, '')) = 'ACEPTADO';
