SELECT s.codigo_solicitud, s.numero_solicitud, s.estado, s.pago_aprobado, s.modulo_solicitud_rt_habilitado, s.codigo_tecnico
FROM aocr_tbsolicitud s WHERE s.codigo_solicitud IN (9,10,11,12) ORDER BY s.codigo_solicitud;
SELECT i.codigo_inspeccion, i.codigo_solicitud, i.codigo_inspector, i.estado, i.fecha_programada
FROM aocr_tbinspeccion i WHERE i.codigo_solicitud IN (9,10,11,12) ORDER BY i.codigo_inspeccion DESC;
