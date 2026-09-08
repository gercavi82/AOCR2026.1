-- Reparacion idempotente para bases donde AC12 ya fue aplicado.
-- Ejecutar despues de 20260904_ac12_entrega_final.sql.
BEGIN;

INSERT INTO public.seguridad_permiso(codigo,nombre,modulo,activo,creado_en,creado_por)
SELECT v.codigo,v.nombre,'ENTREGA_FINAL',TRUE,NOW(),'AC12'
FROM (VALUES
 ('ENTREGA_FINAL_SOLICITAR','Solicitar entrega final'),
 ('ENTREGA_FINAL_CONSULTAR','Consultar entrega institucional'),
 ('ENTREGA_FINAL_AUDITAR','Auditar entrega final')) v(codigo,nombre)
WHERE NOT EXISTS(SELECT 1 FROM public.seguridad_permiso p WHERE p.codigo=v.codigo);
UPDATE public.seguridad_permiso SET activo=TRUE,modulo='ENTREGA_FINAL',actualizado_en=NOW(),actualizado_por='AC12'
WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR');

-- Matriz explicita: nunca reactivar permisos de otros roles por su codigo solamente.
CREATE TEMP TABLE ac12_permisos_esperados ON COMMIT DROP AS
SELECT r.codigorol, p.id_permiso
FROM public.rol r
CROSS JOIN public.seguridad_permiso p
CROSS JOIN LATERAL (
    SELECT regexp_replace(translate(UPPER(TRIM(COALESCE(r.descripcion,''))),
        'ÁÉÍÓÚÜÑ','AEIOUUN'),'[^A-Z0-9]','','g') AS token
) n
WHERE r.activo AND (
    (p.codigo = 'ENTREGA_FINAL_SOLICITAR' AND n.token IN
        ('DIRDAC','DIRECTORDIRDAC','DIRECTORGENERAL','DIRECTORDGAC',
         'DIRECCIONJEFATURATECNICA'))
    OR (p.codigo = 'ENTREGA_FINAL_CONSULTAR' AND n.token IN
        ('DIRDAC','DIRECTORDIRDAC','DIRECTORGENERAL','DIRECTORDGAC',
         'DIRECCIONJEFATURATECNICA','DIRCAV','DCAV',
         'DIRECTORCERTIFICACIONESDCAV','COORDINADOR','COORDINACION',
         'COORDINADORINSPECCIONES','COORDINACIONLEGAL','COORDINADORLEGAL'))
    OR (p.codigo = 'ENTREGA_FINAL_AUDITAR' AND n.token IN
        ('ADMINISTRADOR','ADMIN','ADMINISTRADORSISTEMA'))
);

INSERT INTO public.seguridad_rol_permiso(codigorol,id_permiso,activo,creado_en,creado_por)
SELECT codigorol,id_permiso,TRUE,NOW(),'AC12'
FROM ac12_permisos_esperados
ON CONFLICT (codigorol,id_permiso) DO UPDATE
SET activo=TRUE,actualizado_en=NOW(),actualizado_por='AC12';

UPDATE public.seguridad_rol_permiso rp
SET activo=FALSE,actualizado_en=NOW(),actualizado_por='AC12'
WHERE rp.activo
  AND rp.id_permiso IN (SELECT id_permiso FROM public.seguridad_permiso
      WHERE codigo IN ('ENTREGA_FINAL_SOLICITAR','ENTREGA_FINAL_CONSULTAR','ENTREGA_FINAL_AUDITAR'))
  AND NOT EXISTS (SELECT 1 FROM ac12_permisos_esperados e
      WHERE e.codigorol=rp.codigorol AND e.id_permiso=rp.id_permiso);

COMMIT;
