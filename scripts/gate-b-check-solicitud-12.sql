-- Gate B — diagnóstico solicitud #12 (DGAC-GOP-2026-AOCR012)
SELECT 'SOLICITUD' AS bloque, codigo_solicitud, numero_solicitud, tipo_solicitud, estado, codigo_usuario
FROM aocr_tbsolicitud WHERE codigo_solicitud = 12;

SELECT 'INSPECCION' AS bloque, codigo_inspeccion, codigo_solicitud, codigo_inspector, estado, estado_documental, created_at
FROM aocr_tbinspeccion WHERE codigo_solicitud = 12 ORDER BY codigo_inspeccion DESC LIMIT 5;

SELECT 'HISTORIAL' AS bloque, codigo_historial, estado_anterior, estado_nuevo, fecha_cambio, LEFT(observaciones, 100) AS observaciones
FROM aocr_tbhistorial_estado WHERE codigo_solicitud = 12 ORDER BY codigo_historial DESC LIMIT 15;

SELECT 'LV' AS bloque, lv.codigo_lv, lv.codigo_inspeccion, lv.finalizado, lv.firmado_tecnico, lv.updated_at
FROM aocr_tblv_operacional_eae lv
JOIN aocr_tbinspeccion i ON i.codigo_inspeccion = lv.codigo_inspeccion
WHERE i.codigo_solicitud = 12 ORDER BY lv.codigo_lv DESC LIMIT 3;

SELECT 'INFORME' AS bloque, inf.codigo_informe, inf.codigo_inspeccion, inf.finalizado, inf.firmado_inspector, inf.resultado
FROM aocr_tbinforme_inspeccion inf
JOIN aocr_tbinspeccion i ON i.codigo_inspeccion = inf.codigo_inspeccion
WHERE i.codigo_solicitud = 12 ORDER BY inf.codigo_informe DESC LIMIT 3;

SELECT 'ORDEN' AS bloque, id, codigo_solicitud, numero_orden, estado, fecha_creacion, total
FROM aocr_or_orden WHERE codigo_solicitud::text = '12' ORDER BY id DESC LIMIT 3;

SELECT 'DOCUMENTOS' AS bloque, COUNT(*) AS total,
  SUM(CASE WHEN UPPER(COALESCE(estado,'')) IN ('APROBADO','ACEPTADO') THEN 1 ELSE 0 END) AS aceptados,
  SUM(CASE WHEN UPPER(COALESCE(estado,'')) IN ('DEVUELTO','OBSERVADO') THEN 1 ELSE 0 END) AS devueltos
FROM aocr_tbdocumento WHERE codigo_solicitud = 12;
