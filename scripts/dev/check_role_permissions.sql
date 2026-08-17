SELECT r.codigorol, r.descripcion, count(srp.id_permiso) as total_permisos
FROM rol r
LEFT JOIN seguridad_rol_permiso srp ON srp.codigorol = r.codigorol AND srp.activo = TRUE
WHERE r.activo = TRUE
GROUP BY r.codigorol, r.descripcion
ORDER BY r.codigorol;
