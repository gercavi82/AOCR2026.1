SELECT COUNT(*) AS total_docs FROM aocr_tbdocumento WHERE codigo_solicitud = 12;
SELECT estado, COUNT(*) FROM aocr_tbrevision_documental WHERE codigo_solicitud = 12 GROUP BY estado;
SELECT id, estado FROM aocr_or_orden WHERE codigo_solicitud::text = '12' ORDER BY id DESC LIMIT 3;
SELECT codigo_usuario, usuario_id, tecnico_id, usuario_login, nombre_visual FROM aocr_tbusuario_interno_rt WHERE usuario_id = 43 OR tecnico_id = 43 LIMIT 5;
