SELECT codigo_solicitud, numero_solicitud, tipo_solicitud, estado, codigo_tecnico, pago_aprobado
FROM aocr_tbsolicitud WHERE codigo_solicitud = 12;
SELECT codigo_inspeccion, estado, inspector_principal_cedula, fecha_inspeccion
FROM aocr_tbinspeccion WHERE codigo_solicitud = 12 ORDER BY codigo_inspeccion DESC LIMIT 3;
SELECT codigo_orden, estado, concepto FROM aocr_tborden_recaudacion WHERE codigo_solicitud = 12 ORDER BY codigo_orden DESC LIMIT 3;
SELECT estado_nuevo, fecha_cambio, observacion FROM aocr_tbhistorialestado WHERE codigo_solicitud = 12 ORDER BY fecha_cambio DESC LIMIT 8;
