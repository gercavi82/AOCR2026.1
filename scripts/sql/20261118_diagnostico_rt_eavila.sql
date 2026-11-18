-- Solo lectura. Identifica la cuenta RT sin consultar contraseñas.
SELECT u.idusuario, u.codigousuario, u.nombreusuario, u.correo,
       u.estadoactividad, u.rol AS rol_principal, u.empresa_codigo,
       u.estado_designacion_rt,
       to_jsonb(u)->>'must_change_password' AS cambio_clave_pendiente,
       (SELECT string_agg(DISTINCT r.descripcion, ', ')
        FROM public.usuario_rol ur
        JOIN public.rol r ON r.codigorol = ur.codigorol
        WHERE ur.codigousuario = u.codigousuario
          AND ur.activo = true AND r.activo = true) AS roles_asignados
FROM public.usuario u
WHERE upper(trim(u.codigousuario)) = 'EAVILA'
   OR u.correo ILIKE '%javier%avila%'
   OR u.codigousuario = '1709061814'
ORDER BY u.idusuario;
