-- Asignacion solicitada para el selector de roles de USU_ADMIN en dgac_des.
-- No concede estos roles al resto de administradores.
BEGIN;

DO $$
BEGIN
    IF (SELECT COUNT(*) FROM public.usuario WHERE codigousuario='USU_ADMIN') <> 1 THEN
        RAISE EXCEPTION 'Se esperaba exactamente un usuario USU_ADMIN';
    END IF;
    IF (SELECT COUNT(*) FROM public.rol
        WHERE activo AND UPPER(TRIM(descripcion)) IN ('DIRDAC','DIRCAV')) <> 2 THEN
        RAISE EXCEPTION 'Se requieren los roles activos DIRDAC y DIRCAV sin duplicados';
    END IF;
END $$;

UPDATE public.usuario_rol ur SET activo=TRUE
FROM public.rol r
WHERE ur.codigousuario='USU_ADMIN' AND ur.codigorol=r.codigorol
  AND r.activo AND UPPER(TRIM(r.descripcion)) IN ('DIRDAC','DIRCAV');

INSERT INTO public.usuario_rol(codigousuario,codigorol,fechaasignacion,usuariocreado,activo)
SELECT u.codigousuario,r.codigorol,NOW(),'AJUSTE_ROLES_20260908',TRUE
FROM public.usuario u CROSS JOIN public.rol r
WHERE u.codigousuario='USU_ADMIN' AND r.activo
  AND UPPER(TRIM(r.descripcion)) IN ('DIRDAC','DIRCAV')
  AND NOT EXISTS (SELECT 1 FROM public.usuario_rol ur
      WHERE ur.codigousuario=u.codigousuario AND ur.codigorol=r.codigorol);

UPDATE public.usuario_rol ur SET activo=FALSE
FROM public.rol r
WHERE ur.codigousuario='USU_ADMIN' AND ur.codigorol=r.codigorol
  AND UPPER(TRIM(r.descripcion)) IN ('DIRECCION','DIRECCIÓN');

COMMIT;

SELECT r.codigorol,r.descripcion,ur.activo
FROM public.usuario_rol ur JOIN public.rol r ON r.codigorol=ur.codigorol
WHERE ur.codigousuario='USU_ADMIN' AND ur.activo AND r.activo
ORDER BY r.codigorol;
