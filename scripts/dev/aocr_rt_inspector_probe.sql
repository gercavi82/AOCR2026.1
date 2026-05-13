SELECT COUNT(*) AS total_rt,
       COUNT(*) FILTER (WHERE COALESCE(TRIM(rol_interno),'') <> '') AS con_rol,
       COUNT(*) FILTER (WHERE UPPER(COALESCE(TRIM(rol_interno),'')) LIKE '%INSPECTOR%') AS rol_inspector,
       COUNT(*) FILTER (WHERE UPPER(COALESCE(TRIM(tipo),'')) IN ('OPS','AIR','PEL','AVSEC')) AS tipos_conocidos
FROM public.aocr_usuario_interno_rt;

SELECT codigo_usuario, nombre_completo, tipo, correo_institucional, rol_interno, estado_as400
FROM public.aocr_usuario_interno_rt
ORDER BY codigo_usuario
LIMIT 20;
