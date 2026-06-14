-- Gate C — solicitudes modificación tipo 3
SELECT codigo_solicitud, numero_solicitud, tipo_solicitud, estado,
       aeropuertos_ecuador, aeropuertos_ecuador_otros, codigo_usuario
FROM aocr_tbsolicitud
WHERE tipo_solicitud = 3
ORDER BY codigo_solicitud DESC
LIMIT 10;

SELECT o.id, o.codigo_solicitud, o.numero_orden, o.estado, o.fecha_creacion, o.total
FROM aocr_or_orden o
JOIN aocr_tbsolicitud s ON o.codigo_solicitud::text = s.codigo_solicitud::text
WHERE s.tipo_solicitud = 3
ORDER BY o.id DESC
LIMIT 10;
