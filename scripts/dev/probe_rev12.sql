SELECT decision, COUNT(*) FROM aocr_tbrevision_documental WHERE codigo_solicitud = 12 GROUP BY decision;
SELECT codigo_usuario, usuario_id, tecnico_id, usuario_login FROM aocr_tbusuario_interno_rt WHERE usuario_id = 43 OR tecnico_id = 43 OR usuario_login LIKE '%43%' LIMIT 10;
