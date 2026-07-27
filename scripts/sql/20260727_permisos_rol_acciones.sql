BEGIN;

ALTER TABLE public.seguridad_permiso
    ADD COLUMN IF NOT EXISTS tipo_accion VARCHAR(30);

ALTER TABLE public.seguridad_permiso
    ADD COLUMN IF NOT EXISTS descripcion VARCHAR(300);

UPDATE public.seguridad_permiso
SET tipo_accion = CASE codigo
    WHEN 'ADM_GESTION_USUARIOS' THEN 'EDITAR'
    WHEN 'ADM_ROLES_PERMISOS' THEN 'EDITAR'
    WHEN 'ADM_RESET_PASSWORD' THEN 'EDITAR'
    WHEN 'FIN_VER_PAGOS' THEN 'VER'
    WHEN 'FIN_APROBAR_PAGO' THEN 'APROBAR'
    WHEN 'ORD_ANULAR' THEN 'ELIMINAR'
    WHEN 'LEGAL_REVISAR_SOLICITUD' THEN 'REVISAR'
    WHEN 'LEGAL_GENERAR_CERTIFICADO' THEN 'GENERAR'
    ELSE COALESCE(
        NULLIF(tipo_accion, ''),
        CASE
            WHEN UPPER(codigo) LIKE '%APROBAR%' THEN 'APROBAR'
            WHEN UPPER(codigo) LIKE '%DEVOLVER%' OR UPPER(codigo) LIKE '%RECHAZAR%' THEN 'DEVOLVER'
            WHEN UPPER(codigo) LIKE '%FIRMAR%' THEN 'FIRMAR'
            WHEN UPPER(codigo) LIKE '%GENERAR%' THEN 'GENERAR'
            WHEN UPPER(codigo) LIKE '%CREAR%' THEN 'CREAR'
            WHEN UPPER(codigo) LIKE '%EDITAR%' OR UPPER(codigo) LIKE '%GESTION%' THEN 'EDITAR'
            WHEN UPPER(codigo) LIKE '%ASIGNAR%' THEN 'ASIGNAR'
            WHEN UPPER(codigo) LIKE '%DESCARGAR%' THEN 'DESCARGAR'
            WHEN UPPER(codigo) LIKE '%EXPORTAR%' THEN 'EXPORTAR'
            WHEN UPPER(codigo) LIKE '%ELIMINAR%' OR UPPER(codigo) LIKE '%ANULAR%' THEN 'ELIMINAR'
            WHEN UPPER(codigo) LIKE '%REVISAR%' THEN 'REVISAR'
            ELSE 'VER'
        END)
END,
descripcion = CASE codigo
    WHEN 'ADM_GESTION_USUARIOS' THEN 'Permite administrar cuentas de usuario del sistema'
    WHEN 'ADM_ROLES_PERMISOS' THEN 'Permite gestionar roles y permisos del sistema'
    WHEN 'ADM_RESET_PASSWORD' THEN 'Permite restablecer contraseñas de usuarios'
    WHEN 'FIN_VER_PAGOS' THEN 'Permite visualizar pagos y facturación'
    WHEN 'FIN_APROBAR_PAGO' THEN 'Permite aprobar pagos y registrar facturas'
    WHEN 'ORD_ANULAR' THEN 'Permite anular órdenes de recaudación'
    WHEN 'LEGAL_REVISAR_SOLICITUD' THEN 'Permite revisar solicitudes legales'
    WHEN 'LEGAL_GENERAR_CERTIFICADO' THEN 'Permite generar certificados legales'
    ELSE COALESCE(NULLIF(descripcion, ''), nombre)
END;

ALTER TABLE public.seguridad_permiso
    ALTER COLUMN tipo_accion SET NOT NULL;

ALTER TABLE public.seguridad_permiso
    ALTER COLUMN descripcion SET NOT NULL;

COMMENT ON COLUMN public.seguridad_permiso.tipo_accion IS
    'Acción funcional renderizada por la matriz de permisos; no debe inferirse en Razor o JavaScript.';

COMMENT ON COLUMN public.seguridad_permiso.descripcion IS
    'Descripción funcional y accesible del permiso.';

COMMIT;
