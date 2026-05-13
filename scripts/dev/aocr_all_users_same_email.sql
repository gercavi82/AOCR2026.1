SELECT idusuario, codigousuario, nombreusuario, correo, cedulaidentificacion, identificaciontributaria, numeroruc, estadoactividad
FROM public.usuario
WHERE LOWER(COALESCE(correo,'')) = LOWER('mancho2002@hotmail.com')
ORDER BY idusuario;

SELECT usuario_id, compania_codigo, compania_nombre, activo
FROM public.aocr_usuario_compania_rt
WHERE usuario_id IN (
    SELECT idusuario FROM public.usuario WHERE LOWER(COALESCE(correo,'')) = LOWER('mancho2002@hotmail.com')
)
ORDER BY usuario_id, compania_codigo;
