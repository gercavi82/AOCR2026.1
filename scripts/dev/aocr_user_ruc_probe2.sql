SELECT idusuario, codigousuario, nombreusuario, correo, tipoidentificacion, cedulaidentificacion, identificaciontributaria, numeroruc
FROM public.usuario
WHERE LOWER(COALESCE(correo,'')) = LOWER('mancho2002@hotmail.com')
   OR LOWER(COALESCE(nombreusuario,'')) LIKE LOWER('%mancho2002%');

SELECT id, usuario_id, compania_codigo, compania_nombre, activo, usuoid
FROM public.aocr_usuario_compania_rt
WHERE LOWER(COALESCE(compania_nombre,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(usuoid,'')) LIKE LOWER('%mancho%');

SELECT id, ruc_cedula, nombre, razon_social, correo, activo
FROM public.contribuyentes
WHERE LOWER(COALESCE(nombre,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(razon_social,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(correo,'')) = LOWER('mancho2002@hotmail.com');
