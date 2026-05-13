SELECT idusuario, nombreusuario, codigousuario, email, ruc, empresa_codigo, nombrecompleto
FROM public.usuario
WHERE LOWER(COALESCE(email,'')) = LOWER('mancho2002@hotmail.com')
   OR LOWER(COALESCE(nombreusuario,'')) LIKE LOWER('%mancho2002%')
   OR LOWER(COALESCE(nombrecompleto,'')) LIKE LOWER('%ontario%');

SELECT *
FROM public.usuario_as400
WHERE LOWER(COALESCE(correo,'')) = LOWER('mancho2002@hotmail.com')
   OR LOWER(COALESCE(razon_social,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(nombre_compania,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(numero_ruc,'')) LIKE LOWER('%1432766%');

SELECT *
FROM public.usuario_as400_adicional
WHERE LOWER(COALESCE(email,'')) = LOWER('mancho2002@hotmail.com')
   OR LOWER(COALESCE(compania_nombre,'')) LIKE LOWER('%ontario%');

SELECT *
FROM public.aocr_usuario_compania_rt
WHERE LOWER(COALESCE(compania_nombre,'')) LIKE LOWER('%ontario%');

SELECT *
FROM public.contribuyentes
WHERE LOWER(COALESCE(nombre,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(razon_social,'')) LIKE LOWER('%ontario%')
   OR LOWER(COALESCE(correo,'')) = LOWER('mancho2002@hotmail.com');
